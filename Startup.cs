using MatriX.GST.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;

namespace MatriX.GST;

public class Startup
{
    public static IHttpClientFactory httpClientFactory = default;

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddMemoryCache();

        services.AddHttpClient("ts").ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler()
            {
                AutomaticDecompression = System.Net.DecompressionMethods.None,
                AllowAutoRedirect = true
            };

            handler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            return handler;
        });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHttpClientFactory _httpClientFactory)
    {
        httpClientFactory = _httpClientFactory;

        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });

        app.UseModHeaders();
        app.UseAccs();
        app.UseTorAPI();
    }
}
