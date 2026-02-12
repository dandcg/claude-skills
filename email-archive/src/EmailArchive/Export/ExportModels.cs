// src/EmailArchive/Export/ExportModels.cs
namespace EmailArchive.Export;

/// <summary>
/// Contact export with communication summary for relationships area.
/// </summary>
/// <remarks>
/// CommunicationDirection values: "inbound", "outbound", "bidirectional"
/// </remarks>
public record ContactExport
{
    public string Email { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int TotalEmails { get; init; }
    public int SentTo { get; init; }
    public int ReceivedFrom { get; init; }
    public DateTime FirstContact { get; init; }
    public DateTime LastContact { get; init; }
    public string CommunicationDirection { get; init; } = string.Empty;
}

/// <summary>
/// Review period data for weekly/monthly reviews.
/// </summary>
public record ReviewPeriodExport
{
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }
    public int EmailCount { get; init; }
    public int SentCount { get; init; }
    public int ReceivedCount { get; init; }
    public IReadOnlyList<ContactExport> TopContacts { get; init; } = new List<ContactExport>();
    public string PeakActivityDay { get; init; } = string.Empty;
    public int PeakActivityHour { get; init; }
}
