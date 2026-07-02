using MatriX.GST.Config;
using MatriX.GST.Models;
using MatriX.GST.Services;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace MatriX.GST.Middlewares;

public class TorAPI
{
    private readonly RequestDelegate _next;

    readonly TorManager torManager;
    readonly TorClient torClient;

    public TorAPI(RequestDelegate next, TorManager torManager, TorClient torClient)
    {
        _next = next;
        this.torManager = torManager;
        this.torClient = torClient;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        var userData = httpContext.Features.Get<UserData>();

        if (string.IsNullOrEmpty(userData?.userId))
        {
            await httpContext.Response.WriteAsync("user id empty", httpContext.RequestAborted).ConfigureAwait(false);
            return;
        }

        string inDir = AppInit.appfolder;
        string version = string.IsNullOrEmpty(userData.versionts) ? "latest" : userData.versionts;

        if (version != "latest" && !System.IO.File.Exists($"{inDir}/TorrServer/{version}"))
            version = "latest";

        var (info, errorNewToTS) = await torManager.GetOrCreateNodeAsync(userData, version)
            .ConfigureAwait(false);

        if (errorNewToTS != null)
        {
            await httpContext.Response.WriteAsync(errorNewToTS, httpContext.RequestAborted).ConfigureAwait(false);
            return;
        }

        if (info.taskCompletionSource != null)
        {
            if (await info.taskCompletionSource.Task == false)
            {
                await httpContext.Response.WriteAsync($"failed to start\n{info.exception}\n\n{info.process_log.ToString()}", httpContext.RequestAborted).ConfigureAwait(false);
                return;
            }
        }

        string infohash = 
            userData.infohash ?? 
            await torClient.GetHash(info, userData.magnet, httpContext.RequestAborted).ConfigureAwait(false);

        if (string.IsNullOrEmpty(infohash))
        {
            await httpContext.Response.WriteAsync("failed infohash", httpContext.RequestAborted).ConfigureAwait(false);
            return;
        }

        info.lastActive = DateTime.UtcNow;

        string servUri = userData.target == "gst"
            ? $"http://127.0.0.1:{info.port}/gst/{infohash}/master.m3u8?{userData.queryString}"
            : $"http://127.0.0.1:{info.port}/stream?link={infohash}&{userData.queryString}";

        if (userData.reqUri != null)
            servUri = $"http://127.0.0.1:{info.port}{userData.reqUri}";

        using (var response = await torClient.SendProxyRequest(httpContext, servUri).ConfigureAwait(false))
        {
            if (userData.target == "gst" && servUri.Contains(".m3u8"))
            {
                string result = await response.Content.ReadAsStringAsync(httpContext.RequestAborted).ConfigureAwait(false);
                if (string.IsNullOrEmpty(result))
                {
                    httpContext.Response.StatusCode = 502;
                    return;
                }

                torClient.CopyHttpResponse(httpContext, response);

                result = HlsRewriter.RewritePlaylist(result, infohash, userData);
                httpContext.Response.ContentType = "application/vnd.apple.mpegurl; charset=utf-8";
                await httpContext.Response.WriteAsync(result, httpContext.RequestAborted).ConfigureAwait(false);
            }
            else
            {
                await torClient.CopyStreamAsync(httpContext, response).ConfigureAwait(false);
            }
        }
    }
}
