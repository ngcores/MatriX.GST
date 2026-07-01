namespace MatriX.GST.Models;

public class UserData
{
    public string userId { get; set; }

    public string reqUri { get; set; }

    public string target { get; set; }

    public string magnet { get; set; }

    public string infohash { get; set; }

    public string queryString { get; set; }

    public string versionts { get; set; }

    public string default_settings { get; set; } = "default_settings.json";

    /// <summary>
    /// Максимальный размер файла для просмотра в ts
    /// </summary>
    public long maxSize { get; set; }
}
