using Avalonia.Controls;
using Avalonia.Threading;
using Gambler.Bot.Core.Events;
using Gambler.Bot.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Gambler.Bot.Classes.SiteBrowser
{
    public class BypassStartedEventArgs : EventArgs
    {
        public string Message { get; }

        public BypassStartedEventArgs(string message)
        {
            Message = message;
        }
    }

    /// <summary>
    /// Owns the instance's built-in browser (<see cref="NativeWebView"/> on non-Linux,
    /// <see cref="NativeWebDialog"/> on Linux) and the bypass/captcha/session-collection
    /// logic that previously lived in InstanceView.
    /// All webview access is marshalled to the Avalonia UI thread; script execution is
    /// serialized so concurrent callers (site login threads) cannot interleave JS.
    /// </summary>
    public class SiteBrowser : IDisposable
    {
        private static readonly TimeSpan NavigationWait = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan BypassPollInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan BypassTimeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan CaptchaWait = TimeSpan.FromSeconds(120);

        private readonly bool _isLinux;
        private readonly SemaphoreSlim _scriptLock = new SemaphoreSlim(1, 1);
        private readonly object _stateLock = new object();
        private readonly List<TaskCompletionSource<bool>> _pendingNavigations = new List<TaskCompletionSource<bool>>();

        private readonly Dictionary<string, string> _capturedHeaders = new Dictionary<string, string>();

        private CancellationTokenSource _cts;
        private BypassRequiredArgs _args;
        private BrowserConfig _config;
        private bool _doneRequested;
        private bool _checking;
        private string _agent;
        private string _capturedCookieHeader = string.Empty;
        private DateTime _startedAt;
        private NativeWebDialog _dialog;
        private bool _disposed;

        /// <summary>
        /// The embedded webview. Non-null on non-Linux platforms; on Linux the browser is a
        /// per-session dialog (<see cref="NativeWebDialog"/>) and this is null.
        /// </summary>
        public NativeWebView WebView { get; }

        /// <summary>Raised when a bypass session starts (before navigation).</summary>
        public event EventHandler<BypassStartedEventArgs> BypassStarted;

        /// <summary>Raised when a bypass session ends with the collected config (may be empty).</summary>
        public event EventHandler<BrowserConfig> BypassFinished;

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

        public SiteBrowser()
        {
            _isLinux = OperatingSystem.IsLinux();
            if (!_isLinux)
            {
                WebView = new NativeWebView();
                WebView.EnvironmentRequested += OnEnvironmentRequested;
                WebView.NavigationCompleted += OnNavigationCompleted;
                WebView.WebMessageReceived += OnWebMessageReceived;
                WebView.WebResourceRequested += OnWebResourceRequested;
                WebView.NavigationStarted += WebView_NavigationStarted;
            }
        }
        int navcount = 0;
        private void WebView_NavigationStarted(object? sender, WebViewNavigationStartingEventArgs e)
        {
            lock (_pendingNavigations)
                navcount++;
        }

        /// <summary>Navigates the browser to <paramref name="url"/> and waits for the navigation to complete (or the wait times out).</summary>
        public Task NavigateAsync(Uri url)
        {
            if (url == null)
                throw new ArgumentNullException(nameof(url));

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
        /// Collects a browser session (cookies, headers, user-agent, optional post-navigation
        /// script response) from the built-in browser for the given bypass arguments.
        /// </summary>
        public async Task<BrowserConfig> CollectSessionAsync(BypassRequiredArgs args)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args));
            if (string.IsNullOrWhiteSpace(args.URL))
                throw new ArgumentException("URL is required.", nameof(args));

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            lock (_stateLock)
            {
                _args = args;
                _config = null;
                _doneRequested = false;
                _checking = false;
                _agent = null;
                _startedAt = DateTime.Now;
                _capturedHeaders.Clear();
                _capturedCookieHeader = string.Empty;
            }

            BypassStarted?.Invoke(this, new BypassStartedEventArgs(
                args.HasTimeout
                    ? "Please wait a moment. You do not need to log in to the website below, but please solve any captchas if there are any."
                    : "Please log in to the site below."));

            await NavigateAsync(new Uri(args.URL));

            Task completion;
            if (args.HasTimeout)
                completion = TimeoutSessionAsync(token);
            else
                completion = WaitUserSessionAsync(token);

            try
            {
                await completion.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            await RunOnUiAsync(() => TryCollectCookiesAsync()).ConfigureAwait(false);

            _cts.Cancel();
            lock (_stateLock)
                _args = null;

            BrowserConfig conf;
            Dictionary<string, string> headers;
            lock (_stateLock)
            {
                conf = _config ?? new BrowserConfig();
                conf.UserAgent ??= _agent;
                conf.Cookies ??= new CookieContainer();
                headers = new Dictionary<string, string>(_capturedHeaders);
            }
            conf.Headers = headers;

            if (_isLinux)
                CloseDialog();

            BypassFinished?.Invoke(this, conf);
            return conf;
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

        /// <summary>Signals the active bypass session to collect now and finish (Done button).</summary>
        public void FinishBypass()
        {
            lock (_stateLock)
                _doneRequested = true;
        }

        /// <summary>Cancels the active bypass session (Cancel button); it finishes with whatever was collected.</summary>
        public void CancelBypass()
        {
            _cts?.Cancel();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            _cts?.Cancel();
            lock (_stateLock)
            {
                _args = null;
                _config = null;
            }
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

        private void CloseDialog()
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

        private async Task TimeoutSessionAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_config != null)
                    break;
                if (DateTime.Now - _startedAt > BypassTimeout)
                    break;
                try
                {
                    await Task.Delay(BypassPollInterval, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                if (token.IsCancellationRequested)
                    break;
                await RunOnUiAsync(() => TryCollectCookiesAsync()).ConfigureAwait(false);
            }
        }

        private async Task WaitUserSessionAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_config != null)
                    break;
                if (_doneRequested)
                {
                    await RunOnUiAsync(() => TryCollectCookiesAsync()).ConfigureAwait(false);
                    break;
                }
                try
                {
                    await Task.Delay(500, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// One collection pass (must run on the UI thread). Sets the session config when the
        /// completion conditions are met: required cookies found, post-nav script responded,
        /// the timeout elapsed, or Done/Cancel was requested.
        /// </summary>
        private async Task TryCollectCookiesAsync()
        {
            BypassRequiredArgs args;
            lock (_stateLock)
            {
                args = _args;
                if (_config != null)
                    return;
            }
            if (args == null)
                return;
            if (_checking)
                return;
            _checking = true;
            try
            {
                var container = new CookieContainer();
                var host = new Uri(args.URL).Host.ToLower();
                IReadOnlyList<Cookie> cookies = null;

                var manager = GetCookieManager();
                if (manager != null)
                {
                    cookies = await manager.GetCookiesAsync().ConfigureAwait(false);
                    foreach (var cookie in cookies)
                    {
                        try
                        {
                            container.Add(cookie);
                        }
                        catch
                        {
                        }
                    }
                }

                string scriptResponse = null;
                bool doneRequested;
                lock (_stateLock)
                    doneRequested = _doneRequested;
                if (doneRequested && !string.IsNullOrEmpty(args.PostNavScript))
                {
                    try
                    {
                        scriptResponse = await ExecuteScriptAsync(args.PostNavScript).ConfigureAwait(false);
                    }
                    catch
                    {
                    }
                }

                var foundCookies = new HashSet<string>();
                if (cookies != null && args.RequiredCookies != null)
                {
                    foreach (var cookie in cookies)
                    {
                        if (args.RequiredCookies.Contains(cookie.Name)
                            && cookie.Domain != null
                            && cookie.Domain.ToLower().Contains(host))
                        {
                            foundCookies.Add(cookie.Name);
                        }
                    }
                }

                bool cancelled;
                lock (_stateLock)
                    cancelled = _cts != null && _cts.IsCancellationRequested;
                bool complete = foundCookies.Count >= args.RequiredCookies.Length
                    || (!string.IsNullOrEmpty(args.PostNavScript) && !string.IsNullOrEmpty(scriptResponse))
                    || (args.HasTimeout && DateTime.Now - _startedAt > BypassTimeout)
                    || doneRequested
                    || cancelled;

                if (complete)
                {
                    await EnsureAgentAsync().ConfigureAwait(false);
                    string agent;
                    lock (_stateLock)
                        agent = _agent;
                    var config = new BrowserConfig
                    {
                        UserAgent = agent,
                        Cookies = container,
                        scriptResponse = scriptResponse
                    };
                    lock (_stateLock)
                        _config = config;
                }
            }
            catch
            {
                // Collection failures are not fatal: the session loop retries or finishes with
                // whatever was collected.
            }
            finally
            {
                _checking = false;
            }
        }

        private async Task EnsureAgentAsync()
        {
            lock (_stateLock)
            {
                if (_agent != null)
                    return;
            }
            try
            {
                var agent = await ExecuteScriptAsync("navigator.userAgent").ConfigureAwait(false);
                agent = UnquoteResult(agent);
                if (!string.IsNullOrWhiteSpace(agent))
                {
                    lock (_stateLock)
                        _agent = agent;
                }
            }
            catch
            {
            }
        }

        private static string UnquoteResult(string value)
        {
            if (value == null)
                return null;
            if (value.StartsWith("\\"))
                value = value.Substring(1);
            if (value.EndsWith("\\"))
                value = value.Substring(0, value.Length - 1);
            if (value.StartsWith("\""))
                value = value.Substring(1);
            if (value.EndsWith("\""))
                value = value.Substring(0, value.Length - 1);
            return value;
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
                navcount--;
            }
            foreach (var tcs in done)
                tcs.TrySetResult(true);
            NavigationChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnWebMessageReceived(object sender, WebMessageReceivedEventArgs e)
        {
            
            var body = e.Body;
            Task.Run(() => WebMessage?.Invoke(this, body));
        }

        private void OnWebResourceRequested(object sender, WebResourceRequestedEventArgs e)
        {
            //e.Request.
            BypassRequiredArgs args;
            lock (_stateLock)
                args = _args;
            if (args?.HeadersPath == null)
                return;
            try
            {
                if (e.Request.Uri.ToString().ToLower().Contains(args.HeadersPath.ToLower()))
                {
                    var headers = new Dictionary<string, string>();
                    foreach (var header in e.Request.Headers)
                        headers[header.Key] = header.Value;
                    string cookieHeader = e.Request.Headers.ContainsKey("cookie") ? e.Request.Headers["cookie"] : string.Empty;
                    lock (_stateLock)
                    {
                        _capturedHeaders.Clear();
                        foreach (var header in headers)
                            _capturedHeaders[header.Key] = header.Value;
                        _capturedCookieHeader = cookieHeader;
                    }
                }
            }
            catch
            {
            }
        }

        private async Task WaitNavigationAsync(TaskCompletionSource<bool> tcs, TimeSpan timeout)
        {
            await Task.Delay(new TimeSpan(0, 0, 15));//wait for at least 15 seconds for navigation
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
