namespace InstaImporter.Services;

using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using InstaImporter.Config;
using InstaImporter.Models;

public partial class InstagramContentFetcher : IContentFetcher
{
    private readonly HttpClient _httpClient;
    private readonly AppSettings _settings;
    private readonly string _tempDir;

    public InstagramContentFetcher(HttpClient httpClient, AppSettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
        _tempDir = Path.Combine(Path.GetTempPath(), "insta-importer-videos");
        Directory.CreateDirectory(_tempDir);
    }

    public async Task<ContentItem> FetchContentAsync(ContentItem item)
    {
        try
        {
            await Task.Delay(_settings.Instagram.RateLimitMs);

            var request = new HttpRequestMessage(HttpMethod.Get, item.Url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 16_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.0 Mobile/15E148 Safari/604.1");
            request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            request.Headers.Add("Accept-Language", "en-US,en;q=0.9");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                item.Status = FetchStatus.PostDeleted;
                item.ErrorMessage = $"HTTP {(int)response.StatusCode}";
                return item;
            }

            var html = await response.Content.ReadAsStringAsync();

            var (caption, videoUrl) = await ParseInstagramPage(html);

            item.Caption = caption;

            if (!string.IsNullOrEmpty(videoUrl) && item.IsReel)
            {
                var videoPath = await DownloadVideoAsync(videoUrl, item.Url);
                if (videoPath != null)
                {
                    item.VideoPath = videoPath;
                }
            }

            item.Status = string.IsNullOrEmpty(caption) && string.IsNullOrEmpty(item.VideoPath)
                ? FetchStatus.NoContent
                : FetchStatus.Success;

            return item;
        }
        catch (Exception ex)
        {
            item.Status = FetchStatus.Failed;
            item.ErrorMessage = ex.Message;
            return item;
        }
    }

    private async Task<(string? caption, string? videoUrl)> ParseInstagramPage(string html)
    {
        string? caption = null;
        string? videoUrl = null;

        // Try to extract from meta tags first
        var config = Configuration.Default;
        var context = BrowsingContext.New(config);
        var document = await context.OpenAsync(req => req.Content(html));

        var ogDescription = document.QuerySelector("meta[property='og:description']");
        if (ogDescription != null)
        {
            caption = ogDescription.GetAttribute("content");
        }

        var ogVideo = document.QuerySelector("meta[property='og:video']");
        if (ogVideo != null)
        {
            videoUrl = ogVideo.GetAttribute("content");
        }

        // Try to extract from embedded JSON
        var scriptTags = document.QuerySelectorAll("script");
        foreach (var script in scriptTags)
        {
            var content = script.TextContent;
            if (content.Contains("\"caption\"") || content.Contains("\"edge_media_to_caption\""))
            {
                var captionMatch = CaptionRegex().Match(content);
                if (captionMatch.Success && string.IsNullOrEmpty(caption))
                {
                    caption = JsonSerializer.Deserialize<string>(captionMatch.Groups[1].Value);
                }

                var videoMatch = VideoUrlRegex().Match(content);
                if (videoMatch.Success && string.IsNullOrEmpty(videoUrl))
                {
                    videoUrl = JsonSerializer.Deserialize<string>(videoMatch.Groups[1].Value);
                }
            }
        }

        return (caption, videoUrl);
    }

    private async Task<string?> DownloadVideoAsync(string videoUrl, string sourceUrl)
    {
        try
        {
            var response = await _httpClient.GetAsync(videoUrl, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode) return null;

            var contentLength = response.Content.Headers.ContentLength ?? 0;
            var maxBytes = _settings.Instagram.MaxVideoSizeMb * 1024 * 1024;

            if (contentLength > maxBytes)
            {
                return null; // Video too large
            }

            var shortcode = ExtractShortcode(sourceUrl);
            var filePath = Path.Combine(_tempDir, $"{shortcode}.mp4");

            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = File.Create(filePath);
            await stream.CopyToAsync(fileStream);

            return filePath;
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractShortcode(string url)
    {
        var match = ShortcodeRegex().Match(url);
        return match.Success ? match.Groups[2].Value : Guid.NewGuid().ToString();
    }

    [GeneratedRegex(@"""text""\s*:\s*(""[^""]*"")")]
    private static partial Regex CaptionRegex();

    [GeneratedRegex(@"""video_url""\s*:\s*(""[^""]*"")")]
    private static partial Regex VideoUrlRegex();

    [GeneratedRegex(@"/(p|reel|reels)/([A-Za-z0-9_-]+)")]
    private static partial Regex ShortcodeRegex();
}
