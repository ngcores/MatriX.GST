using System.Text.Json;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MatriX.GST.Config;

public class AppInit
{
    static AppInit()
    {
        #region updateSettings
        void updateSettings(bool ignoreCatch = true)
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
                    var setting = JsonSerializer.Deserialize<Setting>(File.ReadAllText(path), new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (setting != null)
                    {
                        cachesettings.Item1 = setting;
                        cachesettings.Item2 = lastWriteTime;
                    }
                }
            }
            catch
            {
                if (!ignoreCatch)
                    throw;
            }
        }
        #endregion

        updateSettings(ignoreCatch: false);

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
