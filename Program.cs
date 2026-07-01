using MatriX.GST.Middlewares;
using MatriX.GST.Models;
using MatriX.GST.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MatriX.GST;

public class Program
{
    public static void Main(string[] args)
    {
        #region test payload
        var jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        string json = JsonConvert.SerializeObject(new UserData()
        {
            userId = "gstmandalorian",
            target = "gst",
            magnet = "magnet:?xt=urn:btih:2a18dd802983c38426854835a192c134c38ab84a",
            queryString = "index=1&audio=0",
            default_settings = "gst_settings.json"
        }, jsonSettings);

        Console.WriteLine($"GST\n{json}\n\n{AesTo.Encrypt(json)}\n\n");

        json = JsonConvert.SerializeObject(new UserData()
        {
            userId = "mandalorian",
            magnet = "magnet:?xt=urn:btih:2a18dd802983c38426854835a192c134c38ab84a",
            queryString = "index=1&play"
        }, jsonSettings);

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

        #region check node
        ThreadPool.QueueUserWorkItem(async _ =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(20));

                try
                {
                    foreach (var node in TorAPI.db.ToArray())
                    {
                        if (node.Value.countError >= 2 || DateTime.UtcNow.AddMinutes(-AppInit.settings.worknodetominutes) > node.Value.lastActive)
                        {
                            node.Value.Dispose();
                            TorAPI.db.TryRemove(node.Key, out TorInfo torInfo);
                        }
                        else
                        {
                            if (node.Value.lastActive.AddSeconds(10) > DateTime.UtcNow)
                                continue;

                            if (await TorAPI.CheckPort(node.Value.port) == false)
                            {
                                node.Value.countError += 1;
                            }
                            else
                            {
                                node.Value.countError = 0;
                            }
                        }
                    }
                }
                catch { }
            }
        });
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
}
