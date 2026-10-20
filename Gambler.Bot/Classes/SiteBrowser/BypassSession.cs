using Gambler.Bot.Core.Events;
using Gambler.Bot.Core.Helpers;
using Microsoft.Extensions.Logging;
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
    /// The bypass/session-collection state machine, testable without UI controls. Drives a
    /// <see cref="SiteBrowser"/> to collect cookies, request headers, the user-agent, and an
    /// optional post-navigation script response for the given bypass arguments. All webview
    /// access goes through the browser's public API, which marshals to the UI thread.
    /// </summary>
    public sealed class BypassSession : IDisposable
    {
        private static readonly TimeSpan BypassPollInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan BypassTimeout = TimeSpan.FromSeconds(15);

        private readonly SiteBrowser _browser;
        private readonly ILogger? _logger;
        private readonly object _stateLock = new object();
        private readonly Dictionary<string, string> _capturedHeaders = new Dictionary<string, string>();

        private CancellationTokenSource _cts;
        private BypassRequiredArgs _args;
        private BrowserConfig _config;
        private bool _doneRequested;
        private bool _checking;
        private string _agent;
        private DateTime _startedAt;
        private bool _disposed;

        /// <summary>Raised when a bypass session starts (before navigation).</summary>
        public event EventHandler<BypassStartedEventArgs> Started;

        /// <summary>Raised when a bypass session ends with the collected config (may be empty).</summary>
        public event EventHandler<BrowserConfig> Finished;

        public BypassSession(SiteBrowser browser, ILogger? logger = null)
        {
            _browser = browser ?? throw new ArgumentNullException(nameof(browser));
            _logger = logger;
            _browser.WebResourceRequested += OnWebResourceRequested;
        }

        /// <summary>
        /// Starts a bypass session: navigates the browser to the bypass URL, waits for the
        /// completion conditions (required cookies, post-nav script, elapsed timeout, or
        /// Done/Cancel), and returns the collected <see cref="BrowserConfig"/>. Starting a new
        /// session while one is running cancels the previous one.
        /// </summary>
        public async Task<BrowserConfig> StartAsync(BypassRequiredArgs args)
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
            }

            Started?.Invoke(this, new BypassStartedEventArgs(
                args.HasTimeout
                    ? "Please wait a moment. You do not need to log in to the website below, but please solve any captchas if there are any."
                    : "Please log in to the site below."));

            await _browser.NavigateAsync(new Uri(args.URL)).ConfigureAwait(false);

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

            await TryCollectCookiesAsync().ConfigureAwait(false);

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

            _browser.CloseDialog();

            Finished?.Invoke(this, conf);
            return conf;
        }

        /// <summary>Signals the active bypass session to collect now and finish (Done button).</summary>
        public void Finish()
        {
            lock (_stateLock)
                _doneRequested = true;
        }

        /// <summary>Cancels the active bypass session (Cancel button); it finishes with whatever was collected.</summary>
        public void Cancel()
        {
            _cts?.Cancel();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            _cts?.Cancel();
            _browser.WebResourceRequested -= OnWebResourceRequested;
            lock (_stateLock)
            {
                _args = null;
                _config = null;
            }
        }

        private void OnWebResourceRequested(object sender, WebResourceEventArgs e)
        {
            BypassRequiredArgs args;
            lock (_stateLock)
                args = _args;
            if (args?.HeadersPath == null)
                return;
            if (e?.Uri == null || e.Headers == null)
                return;
            try
            {
                if (e.Uri.ToString().ToLower().Contains(args.HeadersPath.ToLower()))
                {
                    var headers = new Dictionary<string, string>();
                    foreach (var header in e.Headers)
                        headers[header.Key] = header.Value;
                    lock (_stateLock)
                    {
                        _capturedHeaders.Clear();
                        foreach (var header in headers)
                            _capturedHeaders[header.Key] = header.Value;
                    }
                }
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
                await TryCollectCookiesAsync().ConfigureAwait(false);
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
                    await TryCollectCookiesAsync().ConfigureAwait(false);
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
        /// One collection pass. Sets the session config when the completion conditions are met:
        /// required cookies found, post-nav script responded, the timeout elapsed, or Done/Cancel
        /// was requested.
        /// </summary>
        private async Task TryCollectCookiesAsync()
        {
            BypassRequiredArgs args;
            lock (_stateLock)
            {
                if (_config != null || _args == null || _checking)
                    return;
                args = _args;
                _checking = true;
            }
            try
            {
                var container = new CookieContainer();
                IReadOnlyList<Cookie> cookies = null;
                try
                {
                    cookies = await _browser.GetCookiesAsync(new Uri(args.URL)).ConfigureAwait(false);
                }
                catch
                {
                }
                if (cookies != null)
                {
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
                        scriptResponse = await _browser.ExecuteScriptAsync(args.PostNavScript).ConfigureAwait(false);
                    }
                    catch
                    {
                    }
                }

                var foundCookies = new HashSet<string>();
                if (cookies != null && args.RequiredCookies != null)
                {
                    var host = new Uri(args.URL).Host.ToLower();
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
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "A bypass session collection pass failed.");
            }
            finally
            {
                lock (_stateLock)
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
                var agent = await _browser.ExecuteScriptAsync("navigator.userAgent").ConfigureAwait(false);
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
    }
}
