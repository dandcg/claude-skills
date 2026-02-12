using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using InstaImporter.Config;
using InstaImporter.Models;
using InstaImporter.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables();

var settings = builder.Configuration.Get<AppSettings>()
    ?? throw new InvalidOperationException("Failed to load settings");

// Allow command-line override for export path
if (args.Length > 0 && args[0] == "--export" && args.Length > 1)
{
    settings.Instagram.ExportPath = args[1];
}

builder.Services.AddSingleton(settings);
builder.Services.AddSingleton<IInstagramExportParser, InstagramExportParser>();
builder.Services.AddHttpClient<IContentFetcher, InstagramContentFetcher>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5
    });
builder.Services.AddSingleton<ITranscriptionService, WhisperTranscriptionService>();
builder.Services.AddSingleton<IKnowledgeExtractor, GptKnowledgeExtractor>();
builder.Services.AddSingleton<IBrainWriter, BrainWriter>();

var host = builder.Build();

// Run the pipeline
var parser = host.Services.GetRequiredService<IInstagramExportParser>();
var fetcher = host.Services.GetRequiredService<IContentFetcher>();
var transcriber = host.Services.GetRequiredService<ITranscriptionService>();
var extractor = host.Services.GetRequiredService<IKnowledgeExtractor>();
var writer = host.Services.GetRequiredService<IBrainWriter>();

Console.WriteLine("=== Instagram DM Importer ===");
Console.WriteLine($"Export path: {settings.Instagram.ExportPath}");
Console.WriteLine($"Target user: {settings.Instagram.TargetUsername}");
Console.WriteLine($"Brain repo: {settings.Brain.RepoPath}");
Console.WriteLine();

// Step 1: Parse export
Console.WriteLine("1. Parsing Instagram export...");
var items = await parser.ParseExportAsync(settings.Instagram.ExportPath, settings.Instagram.TargetUsername);
Console.WriteLine($"   Found {items.Count} posts/reels from {settings.Instagram.TargetUsername}");
Console.WriteLine();

if (items.Count == 0)
{
    Console.WriteLine("No items to process. Exiting.");
    return;
}

// Initialize summary
var summary = new ImportSummary
{
    SourceUsername = settings.Instagram.TargetUsername,
    TotalProcessed = items.Count,
    EarliestDate = items.Min(i => i.SharedAt),
    LatestDate = items.Max(i => i.SharedAt)
};

// Step 2: Fetch content
Console.WriteLine("2. Fetching content...");
var fetchedCount = 0;
var videoCount = 0;
foreach (var item in items)
{
    await fetcher.FetchContentAsync(item);
    fetchedCount++;
    if (!string.IsNullOrEmpty(item.VideoPath)) videoCount++;

    var statusChar = item.Status switch
    {
        FetchStatus.Success => "✓",
        FetchStatus.Failed => "✗",
        FetchStatus.PostDeleted => "⊘",
        FetchStatus.NoContent => "○",
        _ => "?"
    };
    Console.Write($"\r   [{fetchedCount}/{items.Count}] {statusChar}");
}
Console.WriteLine($"\n   Success: {items.Count(i => i.Status == FetchStatus.Success)} | Failed: {items.Count(i => i.Status == FetchStatus.Failed)} | Videos: {videoCount}");
Console.WriteLine();

// Step 3: Transcribe videos
var videosToTranscribe = items.Where(i => !string.IsNullOrEmpty(i.VideoPath)).ToList();
if (videosToTranscribe.Count > 0)
{
    Console.WriteLine("3. Transcribing videos...");
    var transcribedCount = 0;
    var totalMinutes = 0.0;
    foreach (var item in videosToTranscribe)
    {
        item.Transcript = await transcriber.TranscribeAsync(item.VideoPath!);
        transcribedCount++;

        // Estimate duration from file size (rough: 1MB ≈ 1 minute for compressed video)
        var fileInfo = new FileInfo(item.VideoPath!);
        totalMinutes += fileInfo.Length / (1024.0 * 1024.0);

        Console.Write($"\r   [{transcribedCount}/{videosToTranscribe.Count}]");
    }
    summary.WhisperCost = (decimal)(totalMinutes * 0.006);
    Console.WriteLine($"\n   Transcribed {transcribedCount} videos (~{totalMinutes:F1} minutes)");
    Console.WriteLine();
}

// Step 4: Extract knowledge
Console.WriteLine("4. Extracting knowledge...");
var successItems = items.Where(i => i.Status == FetchStatus.Success).ToList();
var extractedCount = 0;
var knowledgeResults = new List<ExtractedKnowledge>();
foreach (var item in successItems)
{
    var knowledge = await extractor.ExtractAsync(item);
    knowledgeResults.Add(knowledge);
    extractedCount++;
    Console.Write($"\r   [{extractedCount}/{successItems.Count}]");
}
summary.GptCost = (decimal)(extractedCount * 0.02);
Console.WriteLine($"\n   Processed {extractedCount} items");
Console.WriteLine();

// Step 5: Write to brain
Console.WriteLine("5. Writing to brain repo...");
foreach (var knowledge in knowledgeResults)
{
    if (knowledge.HasContent)
    {
        await writer.WriteKnowledgeAsync(knowledge);

        var isHighConfidence = knowledge.Confidence >= settings.Brain.ConfidenceThreshold;
        if (isHighConfidence)
        {
            if (!summary.CategoryCounts.ContainsKey(knowledge.Category))
                summary.CategoryCounts[knowledge.Category] = 0;
            summary.CategoryCounts[knowledge.Category]++;
        }
        else
        {
            summary.InboxCount++;
        }
    }
    else
    {
        summary.NoContentCount++;
    }
}

// Track failures
foreach (var item in items.Where(i => i.Status == FetchStatus.Failed || i.Status == FetchStatus.PostDeleted))
{
    summary.FailedCount++;
    summary.FailedItems.Add((item.Url, item.ErrorMessage ?? item.Status.ToString()));
}

// Step 6: Write summary
await writer.WriteSummaryAsync(summary);

Console.WriteLine();
Console.WriteLine("=== Import Complete ===");
Console.WriteLine($"   High-confidence: {summary.CategoryCounts.Values.Sum()} items across {summary.CategoryCounts.Count} areas");
Console.WriteLine($"   Needs review: {summary.InboxCount} items in inbox");
Console.WriteLine($"   No content: {summary.NoContentCount} items");
Console.WriteLine($"   Failed: {summary.FailedCount} items");
Console.WriteLine($"   Total cost: ${summary.WhisperCost + summary.GptCost:F2}");
Console.WriteLine();
Console.WriteLine($"Summary saved to: outputs/instagram-import-summary-{DateTime.Now:yyyy-MM-dd}.md");

// Cleanup temp videos
var tempDir = Path.Combine(Path.GetTempPath(), "insta-importer-videos");
if (Directory.Exists(tempDir))
{
    Directory.Delete(tempDir, true);
    Console.WriteLine("Cleaned up temporary video files.");
}
