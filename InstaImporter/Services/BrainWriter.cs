namespace InstaImporter.Services;

using System.Text;
using InstaImporter.Config;
using InstaImporter.Models;

public class BrainWriter : IBrainWriter
{
    private readonly AppSettings _settings;
    private readonly Dictionary<string, StringBuilder> _categoryBuffers = new();

    public BrainWriter(AppSettings settings)
    {
        _settings = settings;
    }

    public async Task WriteKnowledgeAsync(ExtractedKnowledge knowledge)
    {
        if (!knowledge.HasContent || knowledge.Facts.Count == 0)
        {
            return;
        }

        var isHighConfidence = knowledge.Confidence >= _settings.Brain.ConfidenceThreshold;
        var targetPath = isHighConfidence
            ? GetAreaPath(knowledge.Category)
            : GetInboxPath();

        var content = FormatKnowledgeEntry(knowledge);

        await AppendToFileAsync(targetPath, content, knowledge.Category, isHighConfidence);
    }

    public async Task WriteSummaryAsync(ImportSummary summary)
    {
        // Flush all category buffers first
        foreach (var (category, buffer) in _categoryBuffers)
        {
            var isArea = BrainCategories.IsValid(category) && category != "inbox";
            var path = isArea ? GetAreaPath(category) : GetInboxPath();

            var header = isArea
                ? $"# Instagram Import: {char.ToUpper(category[0]) + category[1..]}\n\n**Imported:** {DateTime.Now:yyyy-MM-dd}\n**Source:** {summary.SourceUsername}'s shared content\n\n"
                : $"# Instagram Import (Review Needed)\n\n**Imported:** {DateTime.Now:yyyy-MM-dd}\n**Source:** {summary.SourceUsername}'s shared content\n**Note:** These items had low confidence categorization - please review and move to appropriate areas.\n\n";

            var fullContent = header + buffer.ToString();
            await File.WriteAllTextAsync(path, fullContent);
        }

        // Write summary report
        var summaryPath = Path.Combine(_settings.Brain.RepoPath, "outputs", $"instagram-import-summary-{DateTime.Now:yyyy-MM-dd}.md");
        Directory.CreateDirectory(Path.GetDirectoryName(summaryPath)!);

        var sb = new StringBuilder();
        sb.AppendLine($"# Instagram Import Summary: {DateTime.Now:yyyy-MM-dd}");
        sb.AppendLine();
        sb.AppendLine($"**Source:** {summary.SourceUsername}'s shared posts/reels");
        if (summary.EarliestDate.HasValue && summary.LatestDate.HasValue)
        {
            sb.AppendLine($"**Date range:** {summary.EarliestDate:yyyy-MM-dd} to {summary.LatestDate:yyyy-MM-dd}");
        }
        sb.AppendLine($"**Total items processed:** {summary.TotalProcessed}");
        sb.AppendLine();
        sb.AppendLine("## Distribution");
        sb.AppendLine("| Area | Items | Auto-routed |");
        sb.AppendLine("|------|-------|-------------|");

        foreach (var (category, count) in summary.CategoryCounts.OrderByDescending(x => x.Value))
        {
            sb.AppendLine($"| {char.ToUpper(category[0]) + category[1..]} | {count} | ✓ |");
        }

        if (summary.InboxCount > 0)
        {
            sb.AppendLine($"| Inbox (review) | {summary.InboxCount} | - |");
        }
        if (summary.NoContentCount > 0)
        {
            sb.AppendLine($"| No content | {summary.NoContentCount} | - |");
        }
        if (summary.FailedCount > 0)
        {
            sb.AppendLine($"| Failed | {summary.FailedCount} | - |");
        }

        if (summary.FailedItems.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Failed Items (manual review)");
            foreach (var (url, reason) in summary.FailedItems)
            {
                sb.AppendLine($"- {url} - {reason}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Cost");
        sb.AppendLine($"- Whisper: ${summary.WhisperCost:F2}");
        sb.AppendLine($"- GPT-4o: ${summary.GptCost:F2}");
        sb.AppendLine($"- **Total: ${summary.WhisperCost + summary.GptCost:F2}**");

        await File.WriteAllTextAsync(summaryPath, sb.ToString());
    }

    private string GetAreaPath(string category)
    {
        return Path.Combine(_settings.Brain.RepoPath, "areas", category, "instagram-imports.md");
    }

    private string GetInboxPath()
    {
        return Path.Combine(_settings.Brain.RepoPath, "inbox", $"instagram-import-{DateTime.Now:yyyy-MM-dd}.md");
    }

    private static string FormatKnowledgeEntry(ExtractedKnowledge knowledge)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## {knowledge.Source.SharedAt:yyyy-MM-dd}: {knowledge.Summary}");

        foreach (var fact in knowledge.Facts)
        {
            sb.AppendLine($"- {fact}");
        }

        sb.AppendLine($"> Source: {knowledge.Source.Url}");
        sb.AppendLine();

        return sb.ToString();
    }

    private Task AppendToFileAsync(string path, string content, string category, bool isHighConfidence)
    {
        var key = isHighConfidence ? category : "inbox";

        if (!_categoryBuffers.ContainsKey(key))
        {
            _categoryBuffers[key] = new StringBuilder();
        }

        _categoryBuffers[key].Append(content);
        return Task.CompletedTask;
    }
}
