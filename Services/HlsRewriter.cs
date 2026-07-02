using MatriX.GST.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MatriX.GST.Services;

public static class HlsRewriter
{
    static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    static readonly Regex HlsRegex = new Regex(
        "(URI=\"|^)(init\\.mp4\\?audio=\\d+|seg/\\d+\\.m4s)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline
    );

    public static string RewritePlaylist(string text, string infohash, UserData userData)
    {
        return HlsRegex.Replace(text, m =>
        {
            var payload = JsonSerializer.Serialize(new UserData()
            {
                target = "gst",
                reqUri = $"/gst/{infohash}/{m.Groups[2].Value}",
                userId = userData.userId,
                maxSize = userData.maxSize,
                infohash = infohash,
                versionts = userData.versionts,
                default_settings = userData.default_settings
            }, JsonOptions);

            return m.Groups[1].Value + AesTo.Encrypt(payload) + (m.Groups[2].Value.EndsWith(".m4s") ? ".m4s" : ".mp4");
        });
    }
}
