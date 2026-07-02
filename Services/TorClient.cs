using MatriX.GST.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MatriX.GST.Services;

public class TorClient
{
    readonly HttpClient httpClient;

    public TorClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<string> GetHash(TorInfo ts, string magnet, CancellationToken uct)
    {
        try
        {
            string body = JsonSerializer.Serialize(new
            {
                action = "add",
                link = magnet,
                title = "",
                poster = "",
                save_to_db = false
            }, JsonOptions);

            using (var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{ts.port}/torrents")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            })
            {
                request.Headers.TryAddWithoutValidation("Authorization", TorManager.BasicAuthorization);

                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(uct))
                {
                    cts.CancelAfter(TimeSpan.FromSeconds(60));

                    using (var resp = await httpClient.SendAsync(request, cts.Token))
                    {
                        if (!resp.IsSuccessStatusCode)
                            return null;

                        string response = await resp.Content.ReadAsStringAsync(cts.Token);
                        if (string.IsNullOrEmpty(response))
                            return null;

                        var json = JsonSerializer.Deserialize<TorrentAddResponse>(response, JsonOptions);
                        if (json == null || string.IsNullOrEmpty(json.hash))
                            return null;

                        return json.hash;
                    }
                }
            }
        }
        catch
        {
            return null;
        }
    }

    public Task<HttpResponseMessage> SendProxyRequest(HttpContext context, string uri)
    {
        var request = CreateProxyHttpRequest(context, new Uri(uri));
        return httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
    }

    static HttpRequestMessage CreateProxyHttpRequest(HttpContext context, Uri uri)
    {
        var requestMessage = new HttpRequestMessage();

        if (context.Request.Headers.TryGetValue("Range", out var range))
            requestMessage.Headers.TryAddWithoutValidation("Range", range.ToString());

        requestMessage.Headers.Add("Authorization", TorManager.BasicAuthorization);
        requestMessage.Headers.Host = context.Request.Host.Value;
        requestMessage.RequestUri = uri;
        requestMessage.Method = HttpMethod.Get;

        return requestMessage;
    }

    public void CopyHttpResponse(HttpContext context, HttpResponseMessage responseMessage)
    {
        var response = context.Response;
        response.StatusCode = (int)responseMessage.StatusCode;

        void UpdateHeaders(HttpHeaders headers)
        {
            foreach (var header in headers)
            {
                try
                {
                    if (header.Key.Equals("content-type", StringComparison.OrdinalIgnoreCase) ||
                        header.Key.Equals("content-length", StringComparison.OrdinalIgnoreCase) ||
                        header.Key.Equals("accept-ranges", StringComparison.OrdinalIgnoreCase) ||
                        header.Key.Equals("content-range", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!response.Headers.ContainsKey(header.Key))
                            response.Headers[header.Key] = header.Value.ToArray();
                    }
                }
                catch { }
            }
        }

        UpdateHeaders(responseMessage.Headers);
        UpdateHeaders(responseMessage.Content.Headers);
    }

    public async Task CopyStreamAsync(HttpContext context, HttpResponseMessage responseMessage)
    {
        var response = context.Response;
        CopyHttpResponse(context, responseMessage);

        await using (var responseStream = await responseMessage.Content.ReadAsStreamAsync(context.RequestAborted).ConfigureAwait(false))
        {
            if (response.Body == null)
                throw new ArgumentNullException("destination");

            if (!responseStream.CanRead && !responseStream.CanWrite)
                throw new ObjectDisposedException("ObjectDisposed_StreamClosed");

            if (!response.Body.CanRead && !response.Body.CanWrite)
                throw new ObjectDisposedException("ObjectDisposed_StreamClosed");

            if (!responseStream.CanRead)
                throw new NotSupportedException("NotSupported_UnreadableStream");

            if (!response.Body.CanWrite)
                throw new NotSupportedException("NotSupported_UnwritableStream");

            using (var pool = new BufferPool())
            {
                int bytesRead;

                while ((bytesRead = await responseStream.ReadAsync(pool.Buffer, context.RequestAborted).ConfigureAwait(false)) > 0)
                    await response.Body.WriteAsync(pool.Buffer, 0, bytesRead, context.RequestAborted).ConfigureAwait(false);
            }
        }
    }
}
