namespace EmailArchive.Models;

public class Email
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string MessageId { get; set; }
    public string? ThreadId { get; set; }
    public required DateTime Date { get; set; }
    public required string Sender { get; set; }
    public required string SenderName { get; set; }
    public required List<string> Recipients { get; set; }
    public required string Subject { get; set; }
    public required string BodyText { get; set; }
    public bool IsSent { get; set; }
    public bool HasAttachments { get; set; }
    public Tier Tier { get; set; } = Tier.Unclassified;
    public float[]? Embedding { get; set; }
    public DateTime? EmbeddedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int BodyWordCount => string.IsNullOrWhiteSpace(BodyText)
        ? 0
        : BodyText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
}
