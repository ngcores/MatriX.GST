using System.Text.Json;
using MatriX.GST.Config;
using MatriX.GST.Services;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace MatriX.GST;

public class Program
{
    public static void Main(string[] args)
    {
        #region test payload
        var settings = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        string json = JsonSerializer.Serialize(new Models.UserData()
        {
            userId = "gstmandalorian",
            target = "gst",
            magnet = "magnet:?xt=urn:btih:2a18dd802983c38426854835a192c134c38ab84a",
            queryString = "index=1&audio=0",
            default_settings = "gst_settings.json"
        }, settings);

        Console.WriteLine($"GST\n{json}\n\n{AesTo.Encrypt(json)}\n\n");

        json = JsonSerializer.Serialize(new Models.UserData()
        {
            userId = "mandalorian",
            magnet = "magnet:?xt=urn:btih:2a18dd802983c38426854835a192c134c38ab84a",
            queryString = "index=1&play"
        }, settings);

        Console.WriteLine($"Stream\n{json}\n\n{AesTo.Encrypt(json)}\n\n");
        #endregion

        Bash.Run($"chmod +x {AppInit.appfolder}/TorrServer/latest");

        #region ThreadPool
        int cpu = Environment.ProcessorCount;
        ThreadPool.GetMinThreads(out int workerThreads, out int completionPortThreads);
        ThreadPool.SetMinThreads(
            workerThreads: Math.Max(workerThreads, cpu * 16),
            completionPortThreads: Math.Max(completionPortThreads, cpu * 8)
        );
        #endregion

        CreateHostBuilder(null).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.None);
            })
            .ConfigureServices(services =>
            {
                services.AddMemoryCache();

                services.AddHttpClient("ts")
                    .ConfigurePrimaryHttpMessageHandler(CreateTsHandler);

                services.AddHttpClient<TorClient>(client => { })
                    .ConfigurePrimaryHttpMessageHandler(CreateTsHandler);

                services.AddSingleton<PortService>();
                services.AddSingleton<TorManager>();
                services.AddSingleton<StatsService>();

                services.AddHostedService<Background.TorCleanupService>();
            })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseKestrel(op =>
                    op.Listen(
                        AppInit.settings.IPAddressAny
                            ? IPAddress.Any
                            : IPAddress.Parse("127.0.0.1"),
                        AppInit.settings.port
                    )
                );

                webBuilder.UseStartup<Startup>();
            });

    static HttpMessageHandler CreateTsHandler()
    {
        var handler = new HttpClientHandler()
        {
            AutomaticDecompression = DecompressionMethods.None,
            AllowAutoRedirect = true
        };

        handler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
        return handler;
    }
}
