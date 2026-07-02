using MatriX.GST.Config;
using MatriX.GST.Models;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MatriX.GST.Services;

public class PortService
{
    readonly IHttpClientFactory httpClientFactory;

    static readonly object portLock = new object();
    static int currentport = 40000;

    string lsof = string.Empty;

    public PortService(IHttpClientFactory httpClientFactory)
    {
        this.httpClientFactory = httpClientFactory;

        ThreadPool.QueueUserWorkItem(async _ =>
        {
            while (true)
            {
                if (!AppInit.settings.lsof)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                    continue;
                }

                string result = Bash.Run("lsof -i -P -n");
                if (result != null)
                    lsof = result;
            }
        });
    }

    public int NextPort()
    {
        lock (portLock)
        {
            currentport = currentport + Random.Shared.Next(5, 10);
            if (currentport > 60000)
                currentport = 40000 + Random.Shared.Next(5, 10);

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

    public bool IsPortInUse(int port)
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
            isUsed = true;
        }
        finally
        {
            listener?.Stop();
        }

        return isUsed;
    }

    public async Task<bool> CheckPort(int port, TorInfo info = null)
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

                    using (HttpClient client = httpClientFactory.CreateClient("ts"))
                    {
                        client.Timeout = TimeSpan.FromSeconds(2);

                        using (var response = await client.GetAsync($"http://127.0.0.1:{port}/echo"))
                        {
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
}
