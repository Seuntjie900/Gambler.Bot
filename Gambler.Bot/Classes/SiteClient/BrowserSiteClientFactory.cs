using Gambler.Bot.Classes.SiteBrowser;
using Gambler.Bot.Core.SiteClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Gambler.Bot.Classes.SiteClient
{
    // The simple name SiteBrowser would otherwise resolve to the sibling *namespace*
    // Gambler.Bot.Classes.SiteBrowser; the alias pins it to the class.
    using SiteBrowser = Gambler.Bot.Classes.SiteBrowser.SiteBrowser;

    /// <summary>
    /// An <see cref="ISiteClientFactory"/> that routes site traffic through the instance's
    /// built-in browser (<see cref="SiteBrowser"/>): HTTP via the page's native <c>fetch()</c>
    /// (see <see cref="BrowserSiteClient"/>) and WebSockets via the page's native
    /// <c>WebSocket</c> (see <see cref="BrowserSiteSocket"/>).
    /// <para>
    /// The factory assigns socket ids, dispatches the socket lifecycle events pushed from the
    /// page via <c>chrome.webview.postMessage</c> to the owning <see cref="BrowserSiteSocket"/>
    /// instances (off the UI thread), and invalidates all live sockets when the page navigates
    /// (login redirect, CF challenge), raising their <c>Closed</c> event exactly once.
    /// </para>
    /// </summary>
    public class BrowserSiteClientFactory : ISiteClientFactory, IDisposable
    {
        private readonly SiteBrowser _browser;
        private readonly ILogger _logger;
        private readonly object _gate = new object();
        private readonly Dictionary<int, BrowserSiteSocket> _sockets = new Dictionary<int, BrowserSiteSocket>();
        private readonly List<BrowserSiteClient> _clients = new List<BrowserSiteClient>();
        private int _nextSocketId;
        private bool _disposed;

        public BrowserSiteClientFactory(SiteBrowser browser, ILogger logger)
        {
            _browser = browser ?? throw new ArgumentNullException(nameof(browser));
            _logger = logger;
            _browser.WebMessage += OnWebMessage;
            _browser.NavigationChanged += OnNavigationChanged;
        }

        /// <summary>
        /// Creates a new <see cref="BrowserSiteClient"/>. If the browser is not currently on the
        /// <c>options.BaseAddress</c> host, it is navigated there first and the call waits for the
        /// navigation to settle before returning the client.
        /// </summary>
        public async Task<ISiteClient> CreateClientAsync(SiteClientOptions options)
        {
            ThrowIfDisposed();
            options ??= new SiteClientOptions();

            var client = new BrowserSiteClient(this, _browser, options, _logger);
            RegisterClient(client);

            var current = CurrentSource();
            if (options.BaseAddress != null && !OnSameHost(current, options.BaseAddress))
            {
                try
                {
                    await _browser.NavigateAsync(options.BaseAddress).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Continue with the client anyway; the requests will surface the failure.
                    _logger?.LogError(ex, "Navigation to '{0}' before creating the browser client failed.", options.BaseAddress);
                }
            }
            return client;
        }

        /// <summary>Creates a new <see cref="BrowserSiteSocket"/> with a factory-assigned id (the socket registers itself).</summary>
        internal BrowserSiteSocket CreateSocket(SiteSocketOptions options)
        {
            ThrowIfDisposed();
            int id = Interlocked.Increment(ref _nextSocketId);
            return new BrowserSiteSocket(id, this, _browser, _logger, options);
        }

        internal void RegisterSocket(BrowserSiteSocket socket)
        {
            lock (_gate)
                _sockets[socket.Id] = socket;
        }

        internal void UnregisterSocket(int id)
        {
            lock (_gate)
                _sockets.Remove(id);
        }

        internal void RegisterClient(BrowserSiteClient client)
        {
            lock (_gate)
                _clients.Add(client);
        }

        internal void UnregisterClient(BrowserSiteClient client)
        {
            lock (_gate)
                _clients.Remove(client);
        }

        /// <summary>
        /// Closes all tracked sockets and clients and detaches from the browser. The webview
        /// itself is owned by the <see cref="SiteBrowser"/> and is never closed here.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            _browser.WebMessage -= OnWebMessage;
            _browser.NavigationChanged -= OnNavigationChanged;

            List<BrowserSiteSocket> sockets;
            List<BrowserSiteClient> clients;
            lock (_gate)
            {
                sockets = new List<BrowserSiteSocket>(_sockets.Values);
                _sockets.Clear();
                clients = new List<BrowserSiteClient>(_clients);
                _clients.Clear();
            }

            Task.Run(() =>
            {
                foreach (var socket in sockets)
                {
                    try
                    {
                        socket.Dispose();
                    }
                    catch
                    {
                        // Disposal failures are not fatal.
                    }
                }
                foreach (var client in clients)
                {
                    try
                    {
                        client.Dispose();
                    }
                    catch
                    {
                        // Disposal failures are not fatal.
                    }
                }
            });
        }

        private void OnWebMessage(object sender, string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
                return;
            try
            {
                using JsonDocument doc = JsonDocument.Parse(payload);
                JsonElement root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    return;
                if (!root.TryGetProperty("t", out JsonElement type) || type.GetString() != "ws")
                    return;
                if (!root.TryGetProperty("id", out JsonElement idElement) || !idElement.TryGetInt32(out int id))
                    return;
                if (!root.TryGetProperty("ev", out JsonElement ev) || ev.ValueKind != JsonValueKind.String)
                    return;

                BrowserSiteSocket socket;
                lock (_gate)
                {
                    if (!_sockets.TryGetValue(id, out socket))
                        return;
                }

                switch (ev.GetString())
                {
                    case "open":
                        socket.OnOpened();
                        break;
                    case "close":
                        socket.OnClosed();
                        break;
                    case "error":
                        socket.OnError(new Exception("The WebSocket encountered an error."));
                        break;
                    case "message":
                        string data = string.Empty;
                        if (root.TryGetProperty("data", out JsonElement dataElement) && dataElement.ValueKind == JsonValueKind.String)
                            data = dataElement.GetString() ?? string.Empty;
                        socket.OnMessage(data);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                // A webview message that is not a well-formed socket event is ignored.
                _logger?.LogDebug(ex, "Ignoring a webview message that is not a socket event.");
            }
        }

        private void OnNavigationChanged(object sender, EventArgs e)
        {
            List<BrowserSiteSocket> sockets;
            lock (_gate)
            {
                if (_sockets.Count == 0)
                    return;
                sockets = new List<BrowserSiteSocket>(_sockets.Values);
                _sockets.Clear();
            }

            // Raise Closed off the UI thread so site handlers behave like the direct transport's
            // (WebSocket4Net raises on worker threads). The site is expected to handle this by
            // disconnecting and re-connecting after the new page is loaded.
            Task.Run(() =>
            {
                foreach (var socket in sockets)
                {
                    try
                    {
                        socket.MarkClosed();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug(ex, "Failed to mark a socket closed after navigation.");
                    }
                }
            });
        }

        private Uri CurrentSource()
        {
            try
            {
                return _browser.WebView?.Source;
            }
            catch
            {
                return null;
            }
        }

        private static bool OnSameHost(Uri current, Uri target)
        {
            if (current == null || target == null)
                return false;
            if (string.IsNullOrEmpty(current.Host) || string.IsNullOrEmpty(target.Host))
                return false;
            return string.Equals(current.Host, target.Host, StringComparison.OrdinalIgnoreCase);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BrowserSiteClientFactory));
        }
    }
}
