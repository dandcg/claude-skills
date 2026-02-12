namespace InstaImporter.Services;

using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using InstaImporter.Config;
using InstaImporter.Models;

public partial class InstagramExportParser : IInstagramExportParser
{
    private readonly AppSettings _settings;

    public InstagramExportParser(AppSettings settings)
    {
        _settings = settings;
    }

    public async Task<List<ContentItem>> ParseExportAsync(string exportPath, string targetUsername)
    {
        var items = new List<ContentItem>();
        var messagesPath = await GetMessagesPathAsync(exportPath);

        var conversationDir = FindConversationDirectory(messagesPath, targetUsername);
        if (conversationDir == null)
        {
            throw new InvalidOperationException($"No conversation found for user: {targetUsername}");
        }

        var messageFiles = Directory.GetFiles(conversationDir, "message_*.json");
        foreach (var file in messageFiles)
        {
            var json = await File.ReadAllTextAsync(file);
            var export = JsonSerializer.Deserialize<InstagramExport>(json);

            if (export?.Messages == null) continue;

            foreach (var message in export.Messages)
            {
                if (message.Share?.Link == null) continue;

                var url = message.Share.Link;
                if (!IsInstagramPostOrReel(url)) continue;

                items.Add(new ContentItem
                {
                    Url = NormalizeUrl(url),
                    SharedAt = message.Timestamp,
                    IsReel = url.Contains("/reel/")
                });
            }
        }

        return items.OrderBy(i => i.SharedAt).ToList();
    }

    private async Task<string> GetMessagesPathAsync(string exportPath)
    {
        if (exportPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var extractDir = Path.Combine(Path.GetTempPath(), "insta-export-" + Guid.NewGuid());
            ZipFile.ExtractToDirectory(exportPath, extractDir);
            exportPath = extractDir;
        }

        var inboxPath = Path.Combine(exportPath, "your_instagram_activity", "messages", "inbox");
        if (Directory.Exists(inboxPath)) return inboxPath;

        inboxPath = Path.Combine(exportPath, "messages", "inbox");
        if (Directory.Exists(inboxPath)) return inboxPath;

        throw new DirectoryNotFoundException($"Could not find messages/inbox in export: {exportPath}");
    }

    private static string? FindConversationDirectory(string inboxPath, string targetUsername)
    {
        var normalized = targetUsername.ToLowerInvariant().Replace("@", "");

        foreach (var dir in Directory.GetDirectories(inboxPath))
        {
            var dirName = Path.GetFileName(dir).ToLowerInvariant();
            if (dirName.Contains(normalized))
            {
                return dir;
            }
        }

        return null;
    }

    private static bool IsInstagramPostOrReel(string url)
    {
        return url.Contains("instagram.com/p/") ||
               url.Contains("instagram.com/reel/") ||
               url.Contains("instagram.com/reels/");
    }

    private static string NormalizeUrl(string url)
    {
        var match = InstagramUrlRegex().Match(url);
        if (match.Success)
        {
            var type = match.Groups[1].Value;
            var shortcode = match.Groups[2].Value;
            return $"https://www.instagram.com/{type}/{shortcode}/";
        }
        return url;
    }

    [GeneratedRegex(@"instagram\.com/(p|reel|reels)/([A-Za-z0-9_-]+)")]
    private static partial Regex InstagramUrlRegex();
}
