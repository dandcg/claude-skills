using EmailArchive.Analytics;
using EmailArchive.Models;
using EmailArchive.Storage;
using Xunit;

namespace EmailArchive.Tests.Analytics;

public class AnalyticsRepositoryTests : IAsyncLifetime
{
    private readonly string? _connectionString;
    private EmailRepository? _emailRepository;
    private AnalyticsRepository? _analyticsRepository;

    public AnalyticsRepositoryTests()
    {
        _connectionString = Environment.GetEnvironmentVariable("EMAIL_ARCHIVE_TEST_DB");
    }

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(_connectionString))
            return;

        var initializer = new DatabaseInitializer(_connectionString);
        await initializer.InitializeAsync();
        _emailRepository = new EmailRepository(_connectionString);
        _analyticsRepository = new AnalyticsRepository(_connectionString);
        await _emailRepository.TruncateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetTimelineAsync_ReturnsYearlyAggregations()
    {
        if (_emailRepository is null || _analyticsRepository is null) return;

        // Insert emails across different years
        await InsertTestEmail("sender@test.com", new DateTime(2020, 3, 15), isSent: false);
        await InsertTestEmail("sender@test.com", new DateTime(2020, 6, 20), isSent: false);
        await InsertTestEmail("me@test.com", new DateTime(2021, 1, 10), isSent: true);

        var timeline = await _analyticsRepository.GetTimelineAsync(groupByMonth: false);

        Assert.Equal(2, timeline.Count);
        var year2020 = timeline.First(t => t.Year == 2020);
        Assert.Equal(2, year2020.EmailCount);
        Assert.Equal(0, year2020.SentCount);
        Assert.Equal(2, year2020.ReceivedCount);
    }

    [Fact]
    public async Task GetTopContactsAsync_ReturnsContactsSortedByTotal()
    {
        if (_emailRepository is null || _analyticsRepository is null) return;

        // Insert emails with different contacts
        await InsertTestEmail("alice@test.com", DateTime.UtcNow, isSent: false);
        await InsertTestEmail("alice@test.com", DateTime.UtcNow, isSent: false);
        await InsertTestEmail("bob@test.com", DateTime.UtcNow, isSent: false);

        var contacts = await _analyticsRepository.GetTopContactsAsync(10);

        Assert.Equal(2, contacts.Count);
        Assert.Equal("alice@test.com", contacts[0].Email);
        Assert.Equal(2, contacts[0].TotalEmails);
    }

    [Fact]
    public async Task GetArchiveSummaryAsync_ReturnsCorrectStats()
    {
        if (_emailRepository is null || _analyticsRepository is null) return;

        await InsertTestEmail("alice@test.com", new DateTime(2020, 1, 1), isSent: false);
        await InsertTestEmail("bob@test.com", new DateTime(2023, 12, 31), isSent: true);

        var summary = await _analyticsRepository.GetArchiveSummaryAsync();

        Assert.Equal(2, summary.TotalEmails);
        Assert.Equal(2, summary.UniqueContacts);
        Assert.Equal(2020, summary.EarliestEmail.Year);
        Assert.Equal(2023, summary.LatestEmail.Year);
    }

    private async Task InsertTestEmail(string sender, DateTime date, bool isSent)
    {
        var email = new Email
        {
            MessageId = $"<{Guid.NewGuid()}@test.com>",
            Date = date,
            Sender = sender,
            SenderName = sender.Split('@')[0],
            Recipients = new List<string> { "recipient@test.com" },
            Subject = "Test Subject",
            BodyText = "Test body content",
            IsSent = isSent,
            Tier = Tier.Vectorize
        };
        await _emailRepository!.InsertAsync(email);
    }
}
