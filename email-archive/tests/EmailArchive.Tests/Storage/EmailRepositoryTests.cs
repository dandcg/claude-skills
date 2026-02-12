using System.Text.Json;
using EmailArchive.Models;
using EmailArchive.Storage;
using Xunit;

namespace EmailArchive.Tests.Storage;

public class EmailRepositoryTests : IAsyncLifetime
{
    private readonly string? _connectionString;
    private EmailRepository? _repository;

    public EmailRepositoryTests()
    {
        _connectionString = Environment.GetEnvironmentVariable("EMAIL_ARCHIVE_TEST_DB");
    }

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(_connectionString))
            return;

        var initializer = new DatabaseInitializer(_connectionString);
        await initializer.InitializeAsync();
        _repository = new EmailRepository(_connectionString);

        await _repository.TruncateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task InsertAsync_StoresEmail()
    {
        if (_repository is null) return;

        var email = CreateTestEmail();

        await _repository.InsertAsync(email);

        var retrieved = await _repository.GetByIdAsync(email.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(email.Sender, retrieved.Sender);
        Assert.Equal(email.Subject, retrieved.Subject);
    }

    [Fact]
    public async Task GetByTierAsync_FiltersCorrectly()
    {
        if (_repository is null) return;

        var email1 = CreateTestEmail(tier: Tier.Excluded);
        var email2 = CreateTestEmail(tier: Tier.MetadataOnly);
        var email3 = CreateTestEmail(tier: Tier.Vectorize);
        var email4 = CreateTestEmail(tier: Tier.Vectorize);

        await _repository.InsertAsync(email1);
        await _repository.InsertAsync(email2);
        await _repository.InsertAsync(email3);
        await _repository.InsertAsync(email4);

        var vectorizeEmails = await _repository.GetByTierAsync(Tier.Vectorize);

        Assert.Equal(2, vectorizeEmails.Count);
    }

    [Fact]
    public async Task GetStatusCountsAsync_ReturnsCorrectCounts()
    {
        if (_repository is null) return;

        await _repository.InsertAsync(CreateTestEmail(tier: Tier.Excluded));
        await _repository.InsertAsync(CreateTestEmail(tier: Tier.MetadataOnly));
        await _repository.InsertAsync(CreateTestEmail(tier: Tier.MetadataOnly));
        await _repository.InsertAsync(CreateTestEmail(tier: Tier.Vectorize));
        await _repository.InsertAsync(CreateTestEmail(tier: Tier.Vectorize));
        await _repository.InsertAsync(CreateTestEmail(tier: Tier.Vectorize));

        var counts = await _repository.GetStatusCountsAsync();

        Assert.Equal(6, counts.Total);
        Assert.Equal(1, counts.Excluded);
        Assert.Equal(2, counts.MetadataOnly);
        Assert.Equal(3, counts.Vectorize);
    }

    private static Email CreateTestEmail(Tier tier = Tier.Unclassified)
    {
        return new Email
        {
            MessageId = $"<{Guid.NewGuid()}@example.com>",
            Date = DateTime.UtcNow,
            Sender = "test@example.com",
            SenderName = "Test Sender",
            Recipients = new List<string> { "recipient@example.com" },
            Subject = "Test Subject",
            BodyText = "Test body content",
            Tier = tier
        };
    }
}
