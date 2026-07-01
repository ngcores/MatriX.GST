using MatriX.GST.Models;
using MatriX.GST.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MatriX.GST.Middlewares;

public class TorAPI
{
    #region TorAPI - static
    public static readonly ConcurrentDictionary<string, TorInfo> db = new ConcurrentDictionary<string, TorInfo>();

    static string lsof = string.Empty;

    static readonly string passwd = DateTime.Now.ToBinary().ToString();

    public static readonly string BasicAuthorization = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"ts:{passwd}"));

    static TorAPI()
    {
        ThreadPool.QueueUserWorkItem(async _ =>
        {
            while (!AppInit.Win32NT)
            {
                if (!AppInit.settings.lsof)
                    await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);

                string result = Bash.Run("lsof -i -P -n");
                if (result != null)
                    lsof = result;
            }
        });
    }
    #endregion

    #region TorAPI
    private readonly RequestDelegate _next;

    IMemoryCache memory;

    IHttpClientFactory httpClientFactory;

    public TorAPI(RequestDelegate next, IMemoryCache memory, IHttpClientFactory httpClientFactory)
    {
        _next = next;
        this.memory = memory;
        this.httpClientFactory = httpClientFactory;
    }
    #endregion

    #region NextPort
    static readonly object portLock = new object();
    static int currentport = 40000;

    static int NextPort(bool useRandom = true)
    {
        lock (portLock)
        {
            if (useRandom)
            {
                currentport = currentport + Random.Shared.Next(5, 10);
                if (currentport > 60000)
                    currentport = 40000 + Random.Shared.Next(5, 10);
            }
            else
            {
                currentport++;
            }

            if (lsof.Contains(currentport.ToString()))
            {
                for (int i = currentport + 1; i < 60000; i++)
                {
                    currentport = i;
                    if (!lsof.Contains(currentport.ToString()))
                        break;
                }
            }

            return currentport;
        }
    }

    static (int ts, int peersListen) goPort()
    {
        return (NextPort(), NextPort(useRandom: false));
    }
    #endregion

    #region IsPortInUse
    static bool IsPortInUse(int port)
    {
        bool isUsed = false;
        TcpListener listener = null;

        try
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
        }
        catch (SocketException)
        {
            isUsed = true; // Порт занят
        }
        finally
        {
            // Если слушатель был создан, останавливаем его
            listener?.Stop();
        }

        return isUsed;
    }
    #endregion


    static readonly object newTsLock = new object();

    async public Task InvokeAsync(HttpContext httpContext)
    {
        var userData = httpContext.Features.Get<UserData>();

        if (string.IsNullOrEmpty(userData.userId))
        {
            await httpContext.Response.WriteAsync("user id empty", httpContext.RequestAborted).ConfigureAwait(false);
            return;
        }

        TorInfo info;
        string errorNewToTS = null;
        bool startNewTS = false;

        string inDir = AppInit.appfolder;
        string version = string.IsNullOrEmpty(userData.versionts) ? "latest" : userData.versionts;

        if (version != "latest" && !File.Exists($"{inDir}/TorrServer/{version}"))
            version = "latest";

        #region add newTs
        lock (newTsLock)
        {
            if (!db.TryGetValue(userData.userId, out info))
            {
                startNewTS = true;

                info = new TorInfo()
                {
                    user = userData,
                    lastActive = DateTime.UtcNow
                };

                if (db.TryAdd(info.user.userId, info))
                {
                    info.taskCompletionSource = new TaskCompletionSource<bool>();
                }
                else
                {
                    errorNewToTS = "error: db.TryAdd(dbKeyOrLogin, info)";
                }
            }
        }
        #endregion

        if (errorNewToTS != null)
        {
            await httpContext.Response.WriteAsync(errorNewToTS, httpContext.RequestAborted).ConfigureAwait(false);
            return;
        }

        if (startNewTS)
        {
            try
            {
                Bash.Run($"kill -9 $(ps axu | grep \"/sandbox/{info.user.userId}\" | grep -v grep | awk '{{print $2}}')");

                var port = goPort();
                while (IsPortInUse(port.ts) || IsPortInUse(port.peersListen))
                    port = goPort();

                info.port = port.ts;

                Directory.CreateDirectory($"{inDir}/sandbox/{info.user.userId}");
                File.Copy($"{inDir}/TorrServer/{info.user.default_settings}", $"{inDir}/sandbox/{info.user.userId}/settings.json", true);

                #region Отслеживанием падение процесса
                info.processForExit += (s, e) =>
                {
                    if (info.thread == null)
                        return;

                    info.Dispose();
                    db.TryRemove(info.user.userId, out _);
                };
                #endregion

                #region Запускаем TorrServer
                info.thread = new Thread(() =>
                {
                    try
                    {
                        File.WriteAllText($"{inDir}/sandbox/{info.user.userId}/accs.db", $"{{\"ts\":\"{passwd}\"}}");

                        string arguments = $"--httpauth -p {info.port} -d {inDir}/sandbox/{info.user.userId}";

                        if (info.user.maxSize > 0)
                            arguments += $" -m {info.user.maxSize}";

                        if (!string.IsNullOrEmpty(AppInit.settings.tsargs))
                            arguments += $" {AppInit.settings.tsargs}";

                        var processInfo = new ProcessStartInfo
                        {
                            UseShellExecute = false,
                            RedirectStandardError = true,
                            RedirectStandardOutput = true,
                            FileName = $"{inDir}/TorrServer/{version}",
                            Arguments = arguments
                        };

                        var process = Process.Start(processInfo);
                        if (process != null)
                        {
                            process.OutputDataReceived += (sender, args) =>
                            {
                                if (!string.IsNullOrEmpty(args.Data))
                                    info.process_log += args.Data + "\n";
                            };

                            process.ErrorDataReceived += (sender, args) =>
                            {
                                if (!string.IsNullOrEmpty(args.Data))
                                    info.process_log += args.Data + "\n";
                            };

                            info.process = process;
                            process.BeginOutputReadLine();
                            process.BeginErrorReadLine();
                            process.WaitForExit();
                        }
                        else
                        {
                            info.exception = "process == null";
                        }
                    }
                    catch (Exception ex)
                    {
                        info.exception = ex.ToString();
                    }

                    info.OnProcessForExit();
                });

                info.thread.Start();
                #endregion

                #region Проверяем доступность сервера
                if (await CheckPort(info.port, info) == false)
                {
                    info.taskCompletionSource.SetResult(false);
                    info.taskCompletionSource = null;

                    info.Dispose();
                    db.TryRemove(info.user.userId, out _);
                    await httpContext.Response.WriteAsync(info?.exception ?? "failed to start", httpContext.RequestAborted).ConfigureAwait(false);
                    return;
                }
                #endregion

                info.taskCompletionSource.SetResult(true);
                info.taskCompletionSource = null;
            }
            catch (Exception ex) 
            {
                info.Dispose();
                db.TryRemove(info.user.userId, out _);

                info.taskCompletionSource.SetResult(false);
                info.taskCompletionSource = null;

                await httpContext.Response.WriteAsync(ex.Message, httpContext.RequestAborted).ConfigureAwait(false);
                return;
            }
        }

        if (info.taskCompletionSource != null)
        {
            if (await info.taskCompletionSource.Task == false)
            {
                await httpContext.Response.WriteAsync($"failed to start\n{info.exception}\n\n{info.process_log}", httpContext.RequestAborted).ConfigureAwait(false);
                return;
            }
        }

        string infohash = userData.infohash ?? await GetHash(info, userData.magnet);
        if (string.IsNullOrEmpty(infohash))
        {
            await httpContext.Response.WriteAsync("failed infohash", httpContext.RequestAborted).ConfigureAwait(false);
            return;
        }

        // время последнего запроса
        info.lastActive = DateTime.UtcNow;

        #region Отправляем запрос в torrserver
        string servUri = userData.target == "gst" ?
            $"http://127.0.0.1:{info.port}/gst/{infohash}/master.m3u8?{userData.queryString}" :
            $"http://127.0.0.1:{info.port}/stream?link={infohash}&{userData.queryString}";

        if (userData.reqUri != null)
            servUri = $"http://127.0.0.1:{info.port}{userData.reqUri}";

        using (var client = httpClientFactory.CreateClient("ts"))
        {
            var request = CreateProxyHttpRequest(httpContext, new Uri(servUri));

            using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, httpContext.RequestAborted).ConfigureAwait(false))
            {
                if (userData.target == "gst" && servUri.Contains(".m3u8"))
                {
                    string result = await response.Content.ReadAsStringAsync(httpContext.RequestAborted).ConfigureAwait(false);
                    if (string.IsNullOrEmpty(result))
                    {
                        httpContext.Response.StatusCode = 502;
                        return;
                    }

                    result = Regex.Replace(result, "(?m)(URI=\"|^)(init\\.mp4\\?audio=\\d+|seg/\\d+\\.m4s)", m =>
                    {
                        var payload = JsonConvert.SerializeObject(new UserData()
                        {
                            target = "gst",
                            reqUri = $"/gst/{infohash}/{m.Groups[2].Value}",
                            userId = userData.userId,
                            maxSize = userData.maxSize,
                            infohash = infohash,
                            versionts = userData.versionts,
                            default_settings = userData.default_settings
                        });

                        return m.Groups[1].Value + AesTo.Encrypt(payload) + (m.Groups[2].Value.EndsWith(".m4s") ? ".m4s" : ".mp4");
                    });

                    httpContext.Response.ContentType = "application/vnd.apple.mpegurl; charset=utf-8";
                    await httpContext.Response.WriteAsync(result, httpContext.RequestAborted).ConfigureAwait(false);
                }
                else
                {
                    await CopyProxyHttpResponse(httpContext, response, info).ConfigureAwait(false);
                }
            }
        }
        #endregion
    }


    #region CreateProxyHttpRequest
    HttpRequestMessage CreateProxyHttpRequest(HttpContext context, Uri uri)
    {
        var requestMessage = new HttpRequestMessage();

        foreach (var header in context.Request.Headers)
        {
            try
            {
                if (header.Key.Equals("range", StringComparison.OrdinalIgnoreCase))
                {
                    if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
                        requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }
            catch { }
        }

        requestMessage.Headers.Add("Authorization", BasicAuthorization);
        requestMessage.Headers.Host = context.Request.Host.Value;
        requestMessage.RequestUri = uri;
        requestMessage.Method = HttpMethod.Get;

        return requestMessage;
    }
    #endregion

    #region CopyProxyHttpResponse
    async Task CopyProxyHttpResponse(HttpContext context, HttpResponseMessage responseMessage, TorInfo info)
    {
        var response = context.Response;
        response.StatusCode = (int)responseMessage.StatusCode;

        #region UpdateHeaders
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
                        response.Headers.TryAdd(header.Key, header.Value.ToArray());
                    }
                }
                catch { }
            }
        }
        #endregion

        UpdateHeaders(responseMessage.Headers);
        UpdateHeaders(responseMessage.Content.Headers);

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
    #endregion


    #region GetHash
    async static Task<string> GetHash(TorInfo ts, string magnet)
    {
        try
        {
            string body = JsonConvert.SerializeObject(new
            {
                action = "add",
                link = magnet,
                title = "",
                poster = "",
                save_to_db = false
            });

            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{ts.port}/torrents");

            request.Content = new StringContent(
                body,
                Encoding.UTF8,
                "application/json"
            );

            request.Headers.TryAddWithoutValidation("Authorization", BasicAuthorization);

            using var resp = await httpClient.SendAsync(request);

            if (!resp.IsSuccessStatusCode)
                return null;

            string response = await resp.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(response))
                return null;

            var json = Newtonsoft.Json.Linq.JObject.Parse(response);
            string hash = json.Value<string>("hash");

            if (string.IsNullOrEmpty(hash))
                return null;

            return hash;
        }
        catch
        {
            return null;
        }
    }
    #endregion

    #region CheckPort
    async public static Task<bool> CheckPort(int port, TorInfo info = null)
    {
        try
        {
            bool servIsWork = false;
            DateTime endTimeCheckort = DateTime.UtcNow.AddSeconds(AppInit.settings.tsCheckPortTimeout);

            while (true)
            {
                try
                {
                    if (DateTime.UtcNow > endTimeCheckort || info != null && info.thread == null)
                        break;

                    await Task.Delay(50);

                    using (HttpClient client = Startup.httpClientFactory != default ? Startup.httpClientFactory.CreateClient("ts") : new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(2);

                        var response = await client.GetAsync($"http://127.0.0.1:{port}/echo");
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            string echo = await response.Content.ReadAsStringAsync();
                            if (echo.StartsWith("MatriX."))
                            {
                                servIsWork = true;
                                break;
                            }
                        }
                    }
                }
                catch { }
            }

            return servIsWork;
        }
        catch
        {
            return false;
        }
    }
    #endregion
}
