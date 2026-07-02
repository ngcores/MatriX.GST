using System.Diagnostics;

namespace MatriX.GST.Services;

public static class Bash
{
    public static string Run(string comand)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                FileName = "/bin/bash",
                Arguments = $" -c \"{comand}\""
            };

            using var process = Process.Start(processInfo);
            if (process == null)
                return null;

            var outPut = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return outPut;
        }
        catch
        {
            return null;
        }
    }
}
