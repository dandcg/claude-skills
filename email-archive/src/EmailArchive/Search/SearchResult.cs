// src/EmailArchive/Search/SearchResult.cs
namespace EmailArchive.Search;

public record EmailSearchResult
{
    public Guid Id { get; init; }
    public DateTime Date { get; init; }
    public string Sender { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string BodySnippet { get; init; } = string.Empty;
    public double Similarity { get; init; }
    public bool HasAttachments { get; init; }
}

public record AttachmentSearchResult
{
    public Guid Id { get; init; }
    public Guid EmailId { get; init; }
    public string Filename { get; init; } = string.Empty;
    public string TextSnippet { get; init; } = string.Empty;
    public double Similarity { get; init; }

    // Parent email info for context
    public DateTime EmailDate { get; init; }
    public string EmailSender { get; init; } = string.Empty;
    public string EmailSubject { get; init; } = string.Empty;
}

public record SearchResults
{
    public List<EmailSearchResult> Emails { get; init; } = new();
    public List<AttachmentSearchResult> Attachments { get; init; } = new();
    public string Query { get; init; } = string.Empty;
    public int TotalFound => Emails.Count + Attachments.Count;
}
