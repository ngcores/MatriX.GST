using MatriX.GST.Models;
using MatriX.GST.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MatriX.GST.Middlewares;

public class Accs
{
    #region Accs
    private readonly RequestDelegate _next;

    IMemoryCache memory;

    public Accs(RequestDelegate next, IMemoryCache memory)
    {
        _next = next;
        this.memory = memory;
    }
    #endregion

    public Task Invoke(HttpContext httpContext)
    {
        #region echo / favicon.ico
        string path = httpContext.Request.Path.Value;

        if (path.StartsWith("/favicon.ico", StringComparison.OrdinalIgnoreCase))
            return httpContext.Response.SendFileAsync("favicon.ico");

        if (path.StartsWith("/echo", StringComparison.OrdinalIgnoreCase))
            return httpContext.Response.WriteAsync("MatriX.GST");

        if (path.StartsWith("/gst/echo", StringComparison.OrdinalIgnoreCase))
        {
            httpContext.Response.ContentType = "application/json; charset=utf-8";
            return httpContext.Response.WriteAsync("{\"gst_discoverer\":{\"found\":true,\"available\":true,\"works\":true},\"gstreamer\":{\"found\":true,\"available\":true,\"works\":true}}");
        }
        #endregion

        ReadOnlySpan<char> payload = httpContext.Request.Path.Value.AsSpan(1);

        int dot = payload.LastIndexOf('.');
        if (dot > 0 && dot < payload.Length - 1)
            payload = payload[..dot];

        string json = AesTo.Decrypt(payload);

        if (json == null)
        {
            httpContext.Response.StatusCode = 403;
            httpContext.Response.ContentType = "text/plain; charset=utf-8";
            return httpContext.Response.WriteAsync("Bad AES payload");
        }

        var userdata = JsonConvert.DeserializeObject<UserData>(json);

        httpContext.Features.Set(userdata);
        return _next(httpContext);
    }
}
