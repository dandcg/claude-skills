// src/EmailArchive/Analytics/AnalyticsModels.cs
namespace EmailArchive.Analytics;

public record TimelinePeriod
{
    public int Year { get; init; }
    public int? Month { get; init; }
    public int EmailCount { get; init; }
    public int SentCount { get; init; }
    public int ReceivedCount { get; init; }
}

public record ContactStats
{
    public string Email { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int TotalEmails { get; init; }
    public int SentTo { get; init; }
    public int ReceivedFrom { get; init; }
    public DateTime FirstContact { get; init; }
    public DateTime LastContact { get; init; }
}

public record ActivityStats
{
    public int Hour { get; init; }
    public int DayOfWeek { get; init; }
    public int EmailCount { get; init; }
}

public record ArchiveSummary
{
    public int TotalEmails { get; init; }
    public int UniqueContacts { get; init; }
    public DateTime EarliestEmail { get; init; }
    public DateTime LatestEmail { get; init; }
    public int TotalYearsSpan { get; init; }
    public double AvgEmailsPerDay { get; init; }
}
