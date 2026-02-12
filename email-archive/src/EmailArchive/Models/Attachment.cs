namespace EmailArchive.Models;

public class Attachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid EmailId { get; set; }
    public required string Filename { get; set; }
    public required string MimeType { get; set; }
    public required int SizeBytes { get; set; }
    public string? ExtractedText { get; set; }
    public float[]? Embedding { get; set; }
    public DateTime? EmbeddedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
