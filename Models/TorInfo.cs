using System;
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MatriX.GST.Services;

namespace MatriX.GST.Models;

public class TorInfo
{
    public int port { get; set; }

    public int countError { get; set; }

    [JsonIgnore]
    public TaskCompletionSource<bool> taskCompletionSource { get; set; }

    public UserData user { get; set; }

    [JsonIgnore]
    public Thread thread { get; set; }

    public DateTime lastActive { get; set; }


    #region process
    [JsonIgnore]
    public Process process { get; set; }

    public string process_log { get; set; } = string.Empty;

    public string exception { get; set; }

    public event EventHandler processForExit;

    public void OnProcessForExit()
    {
        processForExit?.Invoke(this, null);
    }
    #endregion

    #region Dispose
    bool IsDispose;

    public void Dispose()
    {
        if (IsDispose)
            return;

        try
        {
            IsDispose = true;
            int _pid = process.Id;

            #region process
            try
            {
                process.Kill(true);
                process.Dispose();
            }
            catch { }
            #endregion

            #region Bash
            try
            {
                Bash.Run($"kill -9 $(ps axu | grep \"/sandbox/{user.userId}\" | grep -v grep | awk '{{print $2}}')");
                Bash.Run($"kill -9 {_pid}");
            }
            catch { }

            Bash.Run($"rm -rf /home/matrixgst/sandbox/{user.userId}");
            #endregion

            thread = null;
        }
        catch { }
    }
    #endregion
}
