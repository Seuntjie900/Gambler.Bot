using Avalonia.Threading;
using Gambler.Bot.Classes.SiteBrowser;
using Gambler.Bot.Core.SiteClient;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Gambler.Bot.Classes.SiteClient
{
    // The simple name SiteBrowser would otherwise resolve to the sibling *namespace*
    // Gambler.Bot.Classes.SiteBrowser; the alias pins it to the class.
    using SiteBrowser = Gambler.Bot.Classes.SiteBrowser.SiteBrowser;

    /// <summary>
    /// An <see cref="ISiteSocket"/> implementation backed by the page's native JavaScript
    /// <c>WebSocket</c> inside the built-in browser (<see cref="SiteBrowser"/>). Socket lifecycle
    /// events are pushed from the page via <c>chrome.webview.postMessage</c> and dispatched by the
    /// owning <see cref="BrowserSiteClientFactory"/>.
    /// <para>
    /// Unlike the direct-connection <c>WebSocket4NetSiteSocket</c>, cookies for the wss origin are
    /// sent by the browser natively, so the explicit <c>Cookies</c>, <c>Headers</c>,
    /// <c>UserAgent</c> and <c>Origin</c> values in <see cref="SiteSocketOptions"/> are ignored in
    /// this implementation. Only text messages are supported.
    /// </para>
    /// </summary>
    public class BrowserSiteSocket : ISiteSocket
    {
        private static readonly TimeSpan OpenTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan CloseGrace = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// The socket manager injected into the page on first use (idempotent, so it survives
        /// being re-injected after a navigation created a new document).
        /// </summary>
        private const string ManagerScript = @"
window.__gbWs = {
  next: 1, sockets: {},
  create(id, url, protocols) {
    const ws = protocols ? new WebSocket(url, protocols) : new WebSocket(url);
    this.sockets[id] = ws;
    ws.onopen = () => window.chrome.webview.postMessage({ t: 'ws', id, ev: 'open' });
    ws.onclose = (e) => window.chrome.webview.postMessage({ t: 'ws', id, ev: 'close', code: e.code, reason: e.reason });
    ws.onerror = () => window.chrome.webview.postMessage({ t: 'ws', id, ev: 'error' });
    ws.onmessage = (e) => window.chrome.webview.postMessage({ t: 'ws', id, ev: 'message', data: String(e.data) });
    return true;
  },
  send(id, data) { this.sockets[id].send(data); return true; },
  close(id) { this.sockets[id].close(); return true; },
  state(id) { return this.sockets[id] ? this.sockets[id].readyState : 3; }
};";

        private readonly BrowserSiteClientFactory _factory;
        private readonly SiteBrowser _browser;
        private readonly ILogger _logger;
        private readonly string _url;
        private readonly string _protocol;
        private readonly TaskCompletionSource<bool> _openTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly object _gate = new object();
        private int _state; // JS readyState: 0 Connecting, 1 Open, 2 Closing, 3 Closed
        private bool _closedRaised;
        private bool _disposed;

        public event EventHandler<SiteSocketMessageEventArgs> MessageReceived;
        public event EventHandler<SiteSocketErrorEventArgs> Error;
        public event EventHandler Opened;
        public event EventHandler Closed;

        /// <summary>The factory-assigned id used to dispatch messages from the page.</summary>
        internal int Id { get; }

        internal BrowserSiteSocket(int id, BrowserSiteClientFactory factory, SiteBrowser browser, ILogger logger, SiteSocketOptions options)
        {
            options ??= new SiteSocketOptions();
            if (options.Url == null)
                throw new ArgumentNullException(nameof(options.Url));

            Id = id;
            _factory = factory;
            _browser = browser;
            _logger = logger;
            _url = options.Url.ToString();
            _protocol = options.Protocol;
            _state = 3; // Closed until Open()

            _factory.RegisterSocket(this);
        }

        public SiteSocketState State
        {
            get
            {
                lock (_gate)
                {
                    switch (_state)
                    {
                        case 0: return SiteSocketState.Connecting;
                        case 1: return SiteSocketState.Open;
                        case 2: return SiteSocketState.Closing;
                        default: return SiteSocketState.Closed;
                    }
                }
            }
        }

        /// <summary>
        /// Starts connecting (non-blocking). Callers poll <see cref="State"/> as with the direct
        /// transport; if the socket does not open within ~30 seconds it is closed and the
        /// <see cref="Error"/>/<see cref="Closed"/> events are raised.
        /// </summary>
        public void Open()
        {
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(BrowserSiteSocket));
                if (_state != 3)
                    return; // already connecting/open/closing
                _state = 0;
            }
            _ = OpenAsync();
        }

        public void Close()
        {
            lock (_gate)
            {
                if (_disposed || _state == 3)
                    return;
                if (_state != 2)
                    _state = 2;
            }

            RunScript($"window.__gbWs.close({Id})", "close");
            if (!OnUiThread())
            {
                // Wait briefly for the close event so State reflects the outcome; if it never
                // arrives (e.g. the page navigated) ensure Closed is still raised below.
                var sw = Stopwatch.StartNew();
                while (sw.Elapsed < CloseGrace)
                {
                    if (State == SiteSocketState.Closed)
                        return;
                    Thread.Sleep(50);
                }
            }
            MarkClosed();
        }

        public void Send(string message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(BrowserSiteSocket));
                if (_state != 1)
                    throw new InvalidOperationException("The WebSocket is not in the Open state.");
            }

            string js = $"window.__gbWs.send({Id}, {JsonSerializer.Serialize(message)})";
            if (OnUiThread())
            {
                // Blocking here would deadlock the UI thread; report failures via the logger.
                Task task;
                try
                {
                    task = _browser.ExecuteScriptAsync(js);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to send WebSocket message.");
                    return;
                }
                task.ContinueWith(
                    t => { if (t.IsFaulted) _logger?.LogError(t.Exception?.GetBaseException() ?? t.Exception, "Failed to send WebSocket message."); },
                    CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            }
            else
            {
                try
                {
                    _browser.ExecuteScriptAsync(js).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Failed to send message on the WebSocket.", ex);
                }
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
            }
            _factory.UnregisterSocket(Id);
            try
            {
                var task = _browser.ExecuteScriptAsync($"window.__gbWs.close({Id})");
                task.ContinueWith(
                    t => { if (t.IsFaulted) _logger?.LogDebug("WebSocket close during dispose failed."); },
                    CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            }
            catch
            {
            }
            MarkClosed();
        }

        internal void OnOpened()
        {
            lock (_gate)
            {
                if (_disposed || _state == 3 || _state == 1)
                    return;
                _state = 1;
            }
            _openTcs.TrySetResult(true);
            try
            {
                Opened?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Handler for socket Opened event threw.");
            }
        }

        internal void OnClosed()
        {
            MarkClosed();
        }

        internal void OnError(Exception exception)
        {
            RaiseError(exception);
            MarkClosed();
        }

        internal void OnMessage(string message)
        {
            try
            {
                MessageReceived?.Invoke(this, new SiteSocketMessageEventArgs(message));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Handler for socket MessageReceived event threw.");
            }
        }

        /// <summary>
        /// Marks the socket closed (idempotent) and raises <see cref="Closed"/> exactly once.
        /// </summary>
        internal void MarkClosed()
        {
            bool raise = false;
            lock (_gate)
            {
                if (_state != 3)
                    _state = 3;
                if (!_closedRaised)
                {
                    _closedRaised = true;
                    raise = true;
                }
            }
            _openTcs.TrySetResult(false);
            if (raise)
            {
                try
                {
                    Closed?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Handler for socket Closed event threw.");
                }
            }
        }

        private void RaiseError(Exception exception)
        {
            try
            {
                Error?.Invoke(this, new SiteSocketErrorEventArgs(exception));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Handler for socket Error event threw.");
            }
        }

        private async Task OpenAsync()
        {
            try
            {
                string js = $"(() => {{ if (!window.__gbWs) {{ {ManagerScript} }} return window.__gbWs.create({Id}, {JsonSerializer.Serialize(_url)}, {JsonSerializer.Serialize(_protocol)}); }})()";
                await _browser.ExecuteScriptAsync(js).ConfigureAwait(false);

                var completed = await Task.WhenAny(_openTcs.Task, Task.Delay(OpenTimeout)).ConfigureAwait(false);
                if (completed != _openTcs.Task)
                {
                    // Never opened: surface a timeout, then close.
                    if (State == SiteSocketState.Connecting)
                        RaiseError(new TimeoutException($"Timed out opening WebSocket '{_url}'."));
                    MarkClosed();
                }
            }
            catch (Exception ex)
            {
                if (State != SiteSocketState.Closed)
                {
                    if (State == SiteSocketState.Connecting)
                        RaiseError(ex);
                    MarkClosed();
                }
            }
        }

        private void RunScript(string js, string context)
        {
            Task task;
            try
            {
                task = _browser.ExecuteScriptAsync(js);
            }
            catch
            {
                return;
            }
            if (!OnUiThread())
            {
                try
                {
                    task.GetAwaiter().GetResult();
                }
                catch
                {
                    // The close event (or lack of it) decides the outcome; see Close().
                }
            }
            else
            {
                task.ContinueWith(
                    t => { if (t.IsFaulted) _logger?.LogDebug("WebSocket {0} failed.", context); },
                    CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            }
        }

        private static bool OnUiThread()
        {
            try
            {
                return Dispatcher.UIThread.CheckAccess();
            }
            catch
            {
                return false;
            }
        }
    }
}
