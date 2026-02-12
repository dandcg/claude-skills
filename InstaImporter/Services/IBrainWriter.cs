namespace InstaImporter.Services;

using InstaImporter.Models;

public interface IBrainWriter
{
    Task WriteKnowledgeAsync(ExtractedKnowledge knowledge);
    Task WriteSummaryAsync(ImportSummary summary);
}

public class ImportSummary
{
    public DateTime ImportDate { get; set; } = DateTime.Now;
    public string SourceUsername { get; set; } = string.Empty;
    public DateTime? EarliestDate { get; set; }
    public DateTime? LatestDate { get; set; }
    public int TotalProcessed { get; set; }
    public Dictionary<string, int> CategoryCounts { get; set; } = new();
    public int InboxCount { get; set; }
    public int NoContentCount { get; set; }
    public int FailedCount { get; set; }
    public List<(string Url, string Reason)> FailedItems { get; set; } = [];
    public decimal WhisperCost { get; set; }
    public decimal GptCost { get; set; }
}
