using MatriX.GST.Config;
using MatriX.GST.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Threading.Tasks;

namespace MatriX.GST.Middlewares;

public class StatsAPI
{
    readonly RequestDelegate next;
    readonly StatsService statsService;

    public StatsAPI(RequestDelegate next, StatsService statsService)
    {
        this.next = next;
        this.statsService = statsService;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        if (!httpContext.Request.Path.Equals("/api/stats", StringComparison.OrdinalIgnoreCase))
        {
            await next(httpContext);
            return;
        }

        string token = AppInit.settings.authToken;

        if (string.IsNullOrEmpty(token))
        {
            httpContext.Response.StatusCode = 403;
            await httpContext.Response.WriteAsync("stats token empty", httpContext.RequestAborted);
            return;
        }

        if (!httpContext.Request.Headers.TryGetValue("Authorization", out var authorization) ||
            !TokenEquals(authorization, token))
        {
            httpContext.Response.StatusCode = 401;
            await httpContext.Response.WriteAsync("unauthorized", httpContext.RequestAborted);
            return;
        }

        httpContext.Response.ContentType = "application/json; charset=utf-8";
        await httpContext.Response.WriteAsync(statsService.GetJson(), httpContext.RequestAborted);
    }

    static bool TokenEquals(StringValues authorization, string token)
    {
        foreach (var value in authorization)
        {
            if (string.Equals(value, token, StringComparison.Ordinal))
                return true;

            if (value != null &&
                value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(value.Substring("Bearer ".Length), token, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
