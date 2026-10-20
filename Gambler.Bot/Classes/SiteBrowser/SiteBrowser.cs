using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Gambler.Bot.Classes.SiteBrowser
{
    /// <summary>
    /// Carries the request URI and a snapshot of its headers for the
    /// <see cref="SiteBrowser.WebResourceRequested"/> event.
    /// </summary>
    public class WebResourceEventArgs : EventArgs
    {
        public Uri Uri { get; }
        public IReadOnlyDictionary<string, string> Headers { get; }

        public WebResourceEventArgs(Uri uri, IReadOnlyDictionary<string, string> headers)
        {
            Uri = uri;
            Headers = headers;
        }
    }

    /// <summary>
    /// Owns the instance's built-in browser (<see cref="NativeWebView"/> on non-Linux,
    /// <see cref="NativeWebDialog"/> on Linux) and the generic browser operations (navigation,
    /// script execution, cookie access, captcha pages). Bypass/session collection lives in
    /// <see cref="BypassSession"/>.
    /// The <see cref="NativeWebView"/> is created by the hosting view and attached via
    /// <see cref="AttachWebView"/>; this class never creates one itself.
    /// All webview access is marshalled to the Avalonia UI thread; script execution is
    /// serialized so concurrent callers (site login threads) cannot interleave JS.
    /// </summary>
    public class SiteBrowser : IDisposable
    {
        private static readonly TimeSpan NavigationWait = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan CaptchaWait = TimeSpan.FromSeconds(120);

        private readonly bool _isLinux;
        private readonly ILogger? _logger;
        private readonly SemaphoreSlim _scriptLock = new SemaphoreSlim(1, 1);
        private readonly List<TaskCompletionSource<bool>> _pendingNavigations = new List<TaskCompletionSource<bool>>();
        private NativeWebDialog _dialog;
        private bool _disposed;

        /// <summary>
        /// The embedded webview, attached by the hosting view via <see cref="AttachWebView"/>.
        /// Null until attached and always null on Linux, where the browser is a per-session
        /// dialog (<see cref="NativeWebDialog"/>).
        /// </summary>
        public NativeWebView WebView { get; private set; }

        /// <summary>Raised when a captcha page is about to be loaded into the browser.</summary>
        public event EventHandler CaptchaStarting;

        /// <summary>Raised after the captcha page has been written and its navigation settles.</summary>
        public event EventHandler CaptchaFinished;

        /// <summary>Raised after each completed navigation (including redirects).</summary>
        public event EventHandler NavigationChanged;

        /// <summary>
        /// Raised (off the UI thread) with the raw <c>chrome.webview.postMessage</c> payload
        /// from the page.
        /// </summary>
        public event EventHandler<string> WebMessage;

        /// <summary>
        /// Raised for every web resource request the browser issues, with the request URI and a
        /// snapshot of its headers.
        /// </summary>
        public event EventHandler<WebResourceEventArgs> WebResourceRequested;

        public SiteBrowser(ILogger? logger = null)
        {
            _logger = logger;
            _isLinux = OperatingSystem.IsLinux();
        }

        /// <summary>
        /// Attaches the hosting view's <see cref="NativeWebView"/> (one-shot). Non-Linux only;
        /// on Linux the browser is a per-session dialog and this is a no-op.
        /// </summary>
        public void AttachWebView(NativeWebView webview)
        {
            if (_isLinux)
                return;
            if (WebView != null)
                return;
            WebView = webview ?? throw new ArgumentNullException(nameof(webview));
            WebView.EnvironmentRequested += OnEnvironmentRequested;
            WebView.NavigationCompleted += OnNavigationCompleted;
            WebView.WebMessageReceived += OnWebMessageReceived;
            WebView.WebResourceRequested += OnWebResourceRequested;
        }

        /// <summary>Navigates the browser to <paramref name="url"/> and waits for the navigation to complete (or the wait times out).</summary>
        public Task NavigateAsync(Uri url)
        {
            if (url == null)
                throw new ArgumentNullException(nameof(url));
            if (!_isLinux && WebView == null)
                throw new InvalidOperationException("The embedded webview is not attached to the browser.");

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            RunOnUi(() =>
            {
                lock (_pendingNavigations)
                    _pendingNavigations.Add(tcs);
                if (_isLinux)
                {
                    EnsureDialog(url);
                    _dialog.Navigate(url);
                }
                else
                {
                    WebView.Navigate(url);
                }
            });
            return WaitNavigationAsync(tcs, NavigationWait);
        }

        /// <summary>
        /// Writes a captcha/challenge page (full HTML) into the browser via
        /// <c>document.open/write/close</c> and waits for the resulting navigation to settle.
        /// </summary>
        public async Task LoadCaptchaPageAsync(string html)
        {
            if (html == null)
                throw new ArgumentNullException(nameof(html));

            CaptchaStarting?.Invoke(this, EventArgs.Empty);

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            RunOnUi(() =>
            {
                lock (_pendingNavigations)
                    _pendingNavigations.Add(tcs);
                if (_isLinux)
                    EnsureDialog(new Uri("about:blank"));
            });

            string escaped = html.Replace("'", "\\'").Replace("\r", "").Replace("\n", "");
            try
            {
                await RunOnUiAsync(async () =>
                {
                    await InvokeScript("document.open();");
                    await InvokeScript($"document.write('{escaped}');");
                    await InvokeScript("document.close();");
                }).ConfigureAwait(false);
            }
            catch
            {
                // A failed script write should not wedge the login flow; the caller reacts to
                // the (possibly empty) response of the next request.
            }

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(CaptchaWait)).ConfigureAwait(false);
            if (completed != tcs.Task)
                tcs.TrySetResult(false);

            CaptchaFinished?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Runs a script in the page and returns the result. Serialized so concurrent callers
        /// cannot interleave, and marshalled to the UI thread.
        /// </summary>
        public async Task<string> ExecuteScriptAsync(string js)
        {
            if (!_isLinux && WebView == null)
                throw new InvalidOperationException("The embedded webview is not attached to the browser.");

            await _scriptLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_isLinux)
                {
                    if (_dialog == null)
                        throw new InvalidOperationException("No browser session is active.");
                    return await RunOnUiAsync(() => _dialog.InvokeScript(js)).ConfigureAwait(false);
                }
                return await RunOnUiAsync(() => WebView.InvokeScript(js)).ConfigureAwait(false);
            }
            finally
            {
                _scriptLock.Release();
            }
        }

        /// <summary>Returns the cookies visible to the browser, filtered to the host of <paramref name="uri"/>.</summary>
        public async Task<IReadOnlyList<Cookie>> GetCookiesAsync(Uri uri)
        {
            NativeWebViewCookieManager manager = null;
            await RunOnUiAsync(() =>
            {
                manager = GetCookieManager();
                return Task.CompletedTask;
            }).ConfigureAwait(false);
            if (manager == null)
                return Array.Empty<Cookie>();

            IReadOnlyList<Cookie> cookies = null;
            try
            {
                cookies = await manager.GetCookiesAsync().ConfigureAwait(false);
            }
            catch
            {
            }
            if (cookies == null)
                return Array.Empty<Cookie>();
            if (uri == null)
                return cookies;

            var host = uri.Host.ToLower();
            return cookies
                .Where(c => c.Domain != null && c.Domain.ToLower().Contains(host))
                .ToList();
        }

        /// <summary>
        /// Closes the per-session Linux dialog browser. No-op when no dialog is open or on
        /// non-Linux platforms.
        /// </summary>
        public void CloseDialog()
        {
            var dialog = _dialog;
            _dialog = null;
            if (dialog == null)
                return;
            dialog.NavigationCompleted -= OnNavigationCompleted;
            dialog.WebMessageReceived -= OnWebMessageReceived;
            dialog.WebResourceRequested -= OnWebResourceRequested;
            try
            {
                dialog.Close();
            }
            catch
            {
            }
            try
            {
                dialog.Dispose();
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            _scriptLock.Dispose();

            RunOnUi(() =>
            {
                if (WebView != null)
                {
                    WebView.EnvironmentRequested -= OnEnvironmentRequested;
                    WebView.NavigationCompleted -= OnNavigationCompleted;
                    WebView.WebMessageReceived -= OnWebMessageReceived;
                    WebView.WebResourceRequested -= OnWebResourceRequested;
                }
            });
            if (_isLinux)
                RunOnUi(CloseDialog);
        }

        private Task<string> InvokeScript(string js)
        {
            if (_isLinux)
            {
                if (_dialog == null)
                    throw new InvalidOperationException("No browser session is active.");
                return _dialog.InvokeScript(js);
            }
            if (WebView == null)
                throw new InvalidOperationException("The embedded webview is not attached to the browser.");
            return WebView.InvokeScript(js);
        }

        private NativeWebViewCookieManager GetCookieManager()
        {
            if (_isLinux)
                return _dialog?.TryGetCookieManager();
            return WebView?.TryGetCookieManager();
        }

        private void EnsureDialog(Uri url)
        {
            if (_dialog != null)
                return;
            _dialog = new NativeWebDialog
            {
                Source = url
            };
            _dialog.Resize(400, 500);
            _dialog.EnvironmentRequested += OnEnvironmentRequested;
            _dialog.NavigationCompleted += OnNavigationCompleted;
            _dialog.WebMessageReceived += OnWebMessageReceived;
            _dialog.WebResourceRequested += OnWebResourceRequested;
            _dialog.Show();
        }

        private void OnEnvironmentRequested(object sender, WebViewEnvironmentRequestedEventArgs e)
        {
            e.EnableDevTools = true;
        }

        private void OnNavigationCompleted(object sender, WebViewNavigationCompletedEventArgs e)
        {
            List<TaskCompletionSource<bool>> done;
            lock (_pendingNavigations)
            {
                done = new List<TaskCompletionSource<bool>>(_pendingNavigations);
                _pendingNavigations.Clear();
            }
            foreach (var tcs in done)
                tcs.TrySetResult(true);
            NavigationChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnWebMessageReceived(object sender, WebMessageReceivedEventArgs e)
        {
            var body = e.Body;
            Task.Run(() =>
            {
                try
                {
                    WebMessage?.Invoke(this, body);
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "A WebMessage handler threw an exception.");
                }
            });
        }

        private void OnWebResourceRequested(object sender, WebResourceRequestedEventArgs e)
        {
            Uri uri;
            Dictionary<string, string> headers;
            try
            {
                uri = e.Request.Uri;
                headers = new Dictionary<string, string>();
                foreach (var header in e.Request.Headers)
                    headers[header.Key] = header.Value;
            }
            catch
            {
                return;
            }
            WebResourceRequested?.Invoke(this, new WebResourceEventArgs(uri, headers));
        }

        private async Task WaitNavigationAsync(TaskCompletionSource<bool> tcs, TimeSpan timeout)
        {
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeout)).ConfigureAwait(false);
            if (completed != tcs.Task)
                tcs.TrySetResult(false);
        }

        private static void RunOnUi(Action action)
        {
            if (Dispatcher.UIThread.CheckAccess())
                action();
            else
                Dispatcher.UIThread.Post(action);
        }

        private static async Task RunOnUiAsync(Func<Task> action)
        {
            if (Dispatcher.UIThread.CheckAccess())
                await action();
            else
                await Dispatcher.UIThread.InvokeAsync(action);
        }

        private static async Task<T> RunOnUiAsync<T>(Func<Task<T>> action)
        {
            if (Dispatcher.UIThread.CheckAccess())
                return await action();
            return await Dispatcher.UIThread.InvokeAsync(action);
        }
    }
}
