namespace InstaImporter.Models;

public class ContentItem
{
    public required string Url { get; set; }
    public required DateTime SharedAt { get; set; }
    public string? Caption { get; set; }
    public string? VideoPath { get; set; }
    public string? Transcript { get; set; }
    public bool IsReel { get; set; }
    public FetchStatus Status { get; set; } = FetchStatus.Pending;
    public string? ErrorMessage { get; set; }
}

public enum FetchStatus
{
    Pending,
    Success,
    Failed,
    NoContent,
    VideoTooLarge,
    PostDeleted
}
