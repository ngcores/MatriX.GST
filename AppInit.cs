using MatriX.GST.Models;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MatriX.GST;

public class AppInit
{
    static AppInit()
    {
        #region updateSettings
        void updateSettings()
        {
            try
            {
                string path = $"{appfolder}/settings.json";

                if (!File.Exists(path))
                {
                    if (cachesettings.Item1 == null)
                        cachesettings.Item1 = new Setting();

                    return;
                }

                var lastWriteTime = File.GetLastWriteTime(path);

                if (cachesettings.Item2 != lastWriteTime)
                {
                    cachesettings.Item1 = JsonConvert.DeserializeObject<Setting>(File.ReadAllText(path));
                    cachesettings.Item2 = lastWriteTime;
                }
            }
            catch { }
        }
        #endregion

        updateSettings();

        ThreadPool.QueueUserWorkItem(async _ =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                updateSettings();
            }
        });
    }


    static (Setting, DateTime) cachesettings = default;

    public static Setting settings
        => cachesettings.Item1;

    public static bool Win32NT
        => Environment.OSVersion.Platform == PlatformID.Win32NT;

    public static string appfolder = Directory.GetCurrentDirectory();

}
