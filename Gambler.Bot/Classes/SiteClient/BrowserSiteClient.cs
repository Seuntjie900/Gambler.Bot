using Gambler.Bot.Classes.SiteBrowser;
using Gambler.Bot.Core.SiteClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Gambler.Bot.Classes.SiteClient
{
    // The simple name SiteBrowser would otherwise resolve to the sibling *namespace*
    // Gambler.Bot.Classes.SiteBrowser; the alias pins it to the class.
    using SiteBrowser = Gambler.Bot.Classes.SiteBrowser.SiteBrowser;

    /// <summary>
    /// An <see cref="ISiteClient"/> implementation that performs site HTTP requests through the
    /// instance's built-in browser (<see cref="SiteBrowser"/>), using the page's native
    /// <c>fetch()</c> so the browser's cookies, TLS session and CORS context are used.
    /// <para>
    /// Responses are read as text (the sites consume them as text/JSON) and non-2xx statuses are
    /// returned as ordinary <see cref="HttpResponseMessage"/>s (fetch semantics — no exceptions),
    /// so sites keep checking <see cref="HttpResponseMessage.IsSuccessStatusCode"/> as today.
    /// <c>credentials: 'include'</c> is set on every request so cookies are sent cross-origin
    /// (e.g. <c>api.cryptogames.org</c> from <c>cryptogames.org</c>).
    /// </para>
    /// <para>
    /// The browser's own <c>User-Agent</c> is used, and request <c>Cookie</c> values come from the
    /// browser's cookie jar, so <c>SiteClientOptions.Cookies</c> and <c>SiteClientOptions.UserAgent</c>
    /// are not applied in this implementation (documented behavior). Browsers silently drop
    /// forbidden headers (<c>User-Agent</c>, <c>Origin</c>, <c>Referer</c>, <c>Sec-Fetch-*</c>, ...);
    /// the real values sent by the page are the ones the site expects.
    /// </para>
    /// </summary>
    public class BrowserSiteClient : ISiteClient
    {
        private readonly BrowserSiteClientFactory _factory;
        private readonly SiteBrowser _browser;
        private readonly ILogger _logger;
        private readonly HeaderDictionary _defaultHeaders = new HeaderDictionary();
        private Uri _baseAddress;
        private bool _disposed;

        internal BrowserSiteClient(BrowserSiteClientFactory factory, SiteBrowser browser, SiteClientOptions options, ILogger logger)
        {
            _factory = factory;
            _browser = browser;
            browser.WebMessage += Browser_WebMessage;
            _logger = logger;
            options ??= new SiteClientOptions();
            _baseAddress = options.BaseAddress;
            if (options.Headers != null)
            {
                foreach (var header in options.Headers)
                    _defaultHeaders[header.Key] = header.Value;
            }
        }

        private void Browser_WebMessage(object? sender, string e)
        {
            using JsonDocument doc = JsonDocument.Parse(e);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return;
            if (root.TryGetProperty("handler", out JsonElement type))
            {
                requests.Add(type.GetGuid(), e);
            }
        }

        public Uri BaseAddress
        {
            get => _baseAddress;
            set => _baseAddress = value;
        }

        public IDictionary<string, string> DefaultHeaders => _defaultHeaders;
        private Dictionary<Guid, string> requests = new Dictionary<Guid, string>();
        public Task<HttpResponseMessage> GetAsync(string uri, CancellationToken ct = default)
            => RequestAsync("GET", ResolveUri(uri), null, null, null, ct);

        public async Task<HttpResponseMessage> PostAsync(string uri, HttpContent content, CancellationToken ct = default)
        {
            (string body, string contentType) = await ReadContentAsync(content).ConfigureAwait(false);
            return await RequestAsync("POST", ResolveUri(uri), body, contentType, null, ct).ConfigureAwait(false);
        }

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            ThrowIfDisposed();

            string method = request.Method?.Method ?? "GET";
            var uri = ResolveUri(request.RequestUri);

            (string body, string contentType) = await ReadContentAsync(request.Content).ConfigureAwait(false);

            IDictionary<string, string> extraHeaders = null;
            if (request.Headers != null)
            {
                extraHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var header in request.Headers)
                    extraHeaders[header.Key] = string.Join(", ", header.Value);
            }

            return await RequestAsync(method, uri, body, contentType, extraHeaders, ct).ConfigureAwait(false);
        }

        public async Task<string> GetStringAsync(string uri, CancellationToken ct = default)
        {
            var response = await GetAsync(uri, ct).ConfigureAwait(false);
            if (response.Content == null)
                return string.Empty;
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        public ISiteSocket CreateSocket(SiteSocketOptions options)
        {
            ThrowIfDisposed();
            return _factory.CreateSocket(options);
        }

        public async Task<CookieContainer> GetCookiesAsync(Uri uri)
        {
            ThrowIfDisposed();
            var container = new CookieContainer();
            var cookies = await _browser.GetCookiesAsync(uri).ConfigureAwait(false);
            foreach (var cookie in cookies)
            {
                try
                {
                    container.Add(cookie);
                }
                catch
                {
                    // Ignore cookies the container rejects (invalid name/value).
                }
            }
            return container;
        }

        /// <summary>
        /// Detaches from the factory tracking. The webview is owned by the
        /// <see cref="SiteBrowser"/> and is never closed by this client.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _factory.UnregisterClient(this);
        }

        private async Task<HttpResponseMessage> RequestAsync(
            string method,
            Uri uri,
            string body,
            string contentType,
            IDictionary<string, string> extraHeaders,
            CancellationToken ct)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();

            var headers = _defaultHeaders.Snapshot();
            if (extraHeaders != null)
            {
                foreach (var header in extraHeaders)
                    headers[header.Key] = header.Value;
            }
            if (!string.IsNullOrWhiteSpace(contentType))
                headers["Content-Type"] = contentType;
            Guid guid = Guid.NewGuid();
            string js = BuildFetchScript(method, uri, body, headers, guid);

            string raw;
            try
            {
                raw = await _browser.ExecuteScriptAsync(js).ConfigureAwait(false);
                while (!requests.ContainsKey(guid))
                {
                    await Task.Delay(10);
                }
                raw = requests[guid];
                requests.Remove(guid);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new HttpRequestException($"The browser transport failed to send the request to '{uri}'.", ex);
            }

            try
            {
                return ParseResponse(raw, uri);
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new HttpRequestException($"The browser transport returned an invalid response from '{uri}'.", ex);
            }
        }

        private static string BuildFetchScript(string method, Uri uri, string body, IDictionary<string, string> headers, Guid guid)
        {
            // All string literals are serialized with System.Text.Json so values cannot break
            // out of the JS string (injection-safe).
            string urlJson = JsonSerializer.Serialize(uri.PathAndQuery);
            string methodJson = JsonSerializer.Serialize(method.ToUpperInvariant());
            string headersJson = JsonSerializer.Serialize(headers);
            string bodyJson = body == null ? "null" : JsonSerializer.Serialize(body);


            return $@"(async () => {{
  const res = await fetch({urlJson}, {{
    method: {methodJson},    
    body: {bodyJson},
headers: {headersJson},
    credentials: 'include',
    redirect: 'follow'
  }});
  const text = await res.text();
  const h = {{}};
  res.headers.forEach((v, k) => h[k] = v);
 window.chrome.webview.postMessage({{
            handler: ""{guid.ToString()}"",
            status: res.status, 
headers: h, 
body: text}});
  return JSON.stringify({{ status: res.status, headers: h, body: text }});
}})()";

            /*
             return $@"(async () => {{
  const res = await fetch({urlJson}, {{
    method: {methodJson},
    headers: {headersJson},
    body: {bodyJson},
    credentials: 'include',
    redirect: 'follow'
  }});
  const text = await res.text();
  const h = {{}};
  res.headers.forEach((v, k) => h[k] = v);
  return JSON.stringify({{ status: res.status, headers: h, body: text }});
}})()";
             */
        }

        private static HttpResponseMessage ParseResponse(string raw, Uri requestUri)
        {
            string json = DecodeScriptResult(raw);
            if (string.IsNullOrWhiteSpace(json))
                throw new HttpRequestException("The browser transport did not return a response.");

            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new HttpRequestException("The browser transport returned an unexpected response.");

            int status = 0;
            string body = string.Empty;
            var responseHeaders = new List<KeyValuePair<string, string>>();
            if (root.TryGetProperty("status", out JsonElement statusElement))
                status = statusElement.GetInt32();
            if (root.TryGetProperty("body", out JsonElement bodyElement) && bodyElement.ValueKind == JsonValueKind.String)
                body = bodyElement.GetString() ?? string.Empty;
            if (root.TryGetProperty("headers", out JsonElement headersElement) && headersElement.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty header in headersElement.EnumerateObject())
                {
                    responseHeaders.Add(new KeyValuePair<string, string>(
                        header.Name,
                        header.Value.ValueKind == JsonValueKind.String ? header.Value.GetString() ?? string.Empty : header.Value.GetRawText()));
                }
            }

            var response = new HttpResponseMessage((HttpStatusCode)status)
            {
                RequestMessage = new HttpRequestMessage { RequestUri = requestUri }
            };

            string contentType = null;
            foreach (var header in responseHeaders)
            {
                if (string.Equals(header.Key, "content-type", StringComparison.OrdinalIgnoreCase))
                {
                    contentType = header.Value;
                }
                else
                {
                    response.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            var content = new StringContent(body, Encoding.UTF8);
            if (!string.IsNullOrWhiteSpace(contentType))
            {
                if (MediaTypeHeaderValue.TryParse(contentType, out MediaTypeHeaderValue mediaType))
                    content.Headers.ContentType = mediaType;
                else
                    content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            }
            response.Content = content;
            return response;
        }

        /// <summary>
        /// Decodes the <c>InvokeScript</c> result: the JS result is returned JSON-encoded, so a JS
        /// string (our response JSON) arrives as a quoted string that must be unquoted first.
        /// </summary>
        private static string DecodeScriptResult(string raw)
        {
            if (raw == null)
                return null;
            if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
            {
                try
                {
                    return JsonSerializer.Deserialize<string>(raw);
                }
                catch (JsonException)
                {
                }
            }
            // Some platforms additionally escape the surrounding quotes; strip one pair and retry.
            if (raw.Length >= 4 && raw[0] == '\\' && raw[1] == '"' && raw[raw.Length - 2] == '"' && raw[raw.Length - 1] == '\\')
            {
                try
                {
                    return JsonSerializer.Deserialize<string>(raw.Substring(1, raw.Length - 2));
                }
                catch (JsonException)
                {
                }
            }
            return raw;
        }

        private Uri ResolveUri(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri))
                throw new ArgumentException("A URI is required.", nameof(uri));
            Uri baseAddress = _baseAddress;
            if (baseAddress == null)
                return new Uri(uri, UriKind.Absolute);
            return new Uri(baseAddress, uri);
        }

        private Uri ResolveUri(Uri uri)
        {
            if (uri == null)
                throw new ArgumentException("A request URI is required.");
            Uri baseAddress = _baseAddress;
            if (baseAddress == null || uri.IsAbsoluteUri)
                return uri;
            return new Uri(baseAddress, uri);
        }

        private static async Task<(string body, string contentType)> ReadContentAsync(HttpContent content)
        {
            if (content == null)
                return (null, null);
            string body = await content.ReadAsStringAsync().ConfigureAwait(false);
            string contentType = content.Headers.ContentType?.ToString();
            return (body, contentType);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BrowserSiteClient));
        }

        /// <summary>
        /// A thread-safe <see cref="IDictionary{String,String}"/> backing
        /// <see cref="BrowserSiteClient.DefaultHeaders"/>; each request takes a snapshot under the
        /// lock so site threads can keep adding headers while requests are in flight.
        /// </summary>
        private sealed class HeaderDictionary : IDictionary<string, string>
        {
            private readonly Dictionary<string, string> _dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            private readonly object _lock = new object();

            public ICollection<string> Keys
            {
                get
                {
                    lock (_lock)
                        return new List<string>(_dictionary.Keys);
                }
            }

            public ICollection<string> Values
            {
                get
                {
                    lock (_lock)
                        return new List<string>(_dictionary.Values);
                }
            }

            public int Count
            {
                get
                {
                    lock (_lock)
                        return _dictionary.Count;
                }
            }

            public bool IsReadOnly => false;

            public string this[string key]
            {
                get
                {
                    lock (_lock)
                        return _dictionary.TryGetValue(key, out string value) ? value : null;
                }
                set
                {
                    lock (_lock)
                        _dictionary[key] = value;
                }
            }

            public void Add(string key, string value)
            {
                lock (_lock)
                    _dictionary.Add(key, value);
            }

            public void Add(KeyValuePair<string, string> item)
            {
                Add(item.Key, item.Value);
            }

            public void Clear()
            {
                lock (_lock)
                    _dictionary.Clear();
            }

            public bool Contains(KeyValuePair<string, string> item)
            {
                lock (_lock)
                    return _dictionary.TryGetValue(item.Key, out string value) && string.Equals(value, item.Value, StringComparison.Ordinal);
            }

            public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
            {
                lock (_lock)
                    ((ICollection<KeyValuePair<string, string>>)_dictionary).CopyTo(array, arrayIndex);
            }

            public bool ContainsKey(string key)
            {
                lock (_lock)
                    return _dictionary.ContainsKey(key);
            }

            public bool Remove(string key)
            {
                lock (_lock)
                    return _dictionary.Remove(key);
            }

            public bool Remove(KeyValuePair<string, string> item)
            {
                lock (_lock)
                {
                    if (_dictionary.TryGetValue(item.Key, out string value) && string.Equals(value, item.Value, StringComparison.Ordinal))
                    {
                        _dictionary.Remove(item.Key);
                        return true;
                    }
                    return false;
                }
            }

            public bool TryGetValue(string key, out string value)
            {
                lock (_lock)
                    return _dictionary.TryGetValue(key, out value);
            }

            public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
            {
                lock (_lock)
                    return new List<KeyValuePair<string, string>>(_dictionary).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            /// <summary>Returns a consistent copy of the current headers for a single request.</summary>
            public Dictionary<string, string> Snapshot()
            {
                lock (_lock)
                    return new Dictionary<string, string>(_dictionary, StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}
