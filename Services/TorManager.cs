using MatriX.GST.Config;
using MatriX.GST.Models;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace MatriX.GST.Services;

public class TorManager
{
    static readonly string passwd = DateTime.Now.ToBinary().ToString();

    public static readonly string BasicAuthorization = "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"ts:{passwd}"));

    private readonly PortService portService;

    private static readonly object newTsLock = new();

    readonly string inDir = AppInit.appfolder;

    public readonly ConcurrentDictionary<string, TorInfo> db = new();

    public TorManager(PortService portService)
    {
        this.portService = portService;
    }

    public async Task<(TorInfo, string)> GetOrCreateNodeAsync(UserData userData)
    {
        TorInfo info;
        string errorNewToTS = null;
        bool startNewTS = false;

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
                    info.taskCompletionSource = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously
                    );
                }
                else
                {
                    errorNewToTS = "error: db.TryAdd(dbKeyOrLogin, info)";
                }
            }
        }

        if (errorNewToTS != null)
            return (info, errorNewToTS);

        if (startNewTS)
        {
            try
            {
                string version = string.IsNullOrEmpty(userData.versionts) ? "latest" : userData.versionts;
                if (version != "latest" && !File.Exists($"{inDir}/TorrServer/{version}"))
                    version = "latest";

                await StartNewServerAsync(info, version).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (info.taskCompletionSource != null)
                {
                    info.taskCompletionSource.SetResult(false);
                    info.taskCompletionSource = null;
                }

                if (string.IsNullOrEmpty(errorNewToTS))
                    info.Dispose();

                db.TryRemove(info.user.userId, out _);

                return (info, ex.Message);
            }
        }

        return (info, null);
    }

    async Task StartNewServerAsync(TorInfo info, string version)
    {
        Bash.Run($"kill -9 $(ps axu | grep \"/sandbox/{info.user.userId}\" | grep -v grep | awk '{{print $2}}')");

        int port = portService.NextPort();
        while (portService.IsPortInUse(port))
            port = portService.NextPort();

        info.port = port;

        Directory.CreateDirectory($"{inDir}/sandbox/{info.user.userId}");
        File.Copy($"{inDir}/TorrServer/{info.user.default_settings}", $"{inDir}/sandbox/{info.user.userId}/settings.json", true);

        info.processForExit += (s, e) =>
        {
            if (info.thread == null)
                return;

            info.Dispose();
            db.TryRemove(info.user.userId, out _);
        };

        info.thread = new System.Threading.Thread(() =>
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
                        {
                            lock (info.processLogLock)
                                info.process_log.AppendLine(args.Data);
                        }
                    };

                    process.ErrorDataReceived += (sender, args) =>
                    {
                        if (!string.IsNullOrEmpty(args.Data))
                        {
                            lock (info.processLogLock)
                                info.process_log.AppendLine(args.Data);
                        }
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

        if (await portService.CheckPort(info.port, info) == false)
        {
            info.taskCompletionSource.SetResult(false);
            info.taskCompletionSource = null;

            info.Dispose();
            db.TryRemove(info.user.userId, out _);
            throw new Exception(info?.exception ?? "failed to start");
        }

        info.taskCompletionSource.SetResult(true);
        info.taskCompletionSource = null;
    }

    public async Task CleanupAsync()
    {
        foreach (var node in db.ToArray())
        {
            if (node.Value.countError >= 2 || DateTime.UtcNow.AddMinutes(-AppInit.settings.worknodetominutes) > node.Value.lastActive)
            {
                node.Value.Dispose();
                db.TryRemove(node.Key, out _);
            }
            else
            {
                if (node.Value.lastActive.AddSeconds(10) > DateTime.UtcNow)
                    continue;

                if (await portService.CheckPort(node.Value.port) == false)
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
}
