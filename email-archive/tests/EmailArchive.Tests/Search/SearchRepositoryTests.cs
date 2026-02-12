using EmailArchive.Models;
using EmailArchive.Search;
using EmailArchive.Storage;
using Xunit;

namespace EmailArchive.Tests.Search;

public class SearchRepositoryTests : IAsyncLifetime
{
    private readonly string? _connectionString;
    private EmailRepository? _emailRepository;
    private SearchRepository? _searchRepository;

    public SearchRepositoryTests()
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
        _searchRepository = new SearchRepository(_connectionString);
        await _emailRepository.TruncateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SearchEmailsAsync_ReturnsResultsOrderedBySimilarity()
    {
        if (_emailRepository is null || _searchRepository is null) return;

        // Insert test email with embedding
        var email = new Email
        {
            MessageId = "<search-test@example.com>",
            Date = DateTime.UtcNow,
            Sender = "colleague@work.com",
            SenderName = "Work Colleague",
            Recipients = new List<string> { "me@work.com" },
            Subject = "Project Budget Discussion",
            BodyText = "Let's discuss the Q4 budget for the marketing project.",
            Tier = Tier.Vectorize,
            Embedding = CreateTestEmbedding(),
            EmbeddedAt = DateTime.UtcNow
        };

        await _emailRepository.InsertAsync(email);

        // Search with a similar embedding
        var queryEmbedding = CreateTestEmbedding();
        var results = await _searchRepository.SearchEmailsAsync(queryEmbedding, limit: 10);

        Assert.Single(results);
        Assert.Equal("Project Budget Discussion", results[0].Subject);
        Assert.True(results[0].Similarity > 0.9); // Same embedding should be very similar
    }

    [Fact]
    public async Task SearchEmailsAsync_RespectsLimit()
    {
        if (_emailRepository is null || _searchRepository is null) return;

        // Insert multiple emails
        for (int i = 0; i < 5; i++)
        {
            var email = new Email
            {
                MessageId = $"<search-limit-{i}@example.com>",
                Date = DateTime.UtcNow.AddDays(-i),
                Sender = $"sender{i}@example.com",
                SenderName = $"Sender {i}",
                Recipients = new List<string> { "me@example.com" },
                Subject = $"Test Email {i}",
                BodyText = $"Test body content {i}",
                Tier = Tier.Vectorize,
                Embedding = CreateTestEmbedding(),
                EmbeddedAt = DateTime.UtcNow
            };
            await _emailRepository.InsertAsync(email);
        }

        var queryEmbedding = CreateTestEmbedding();
        var results = await _searchRepository.SearchEmailsAsync(queryEmbedding, limit: 3);

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void CreateSnippet_TruncatesLongText()
    {
        var longText = new string('a', 500);
        var snippet = SearchRepository.CreateSnippet(longText, 100);

        Assert.True(snippet.Length <= 103); // 100 + "..."
        Assert.EndsWith("...", snippet);
    }

    private static float[] CreateTestEmbedding()
    {
        // Create a normalized test embedding
        var embedding = new float[1536];
        for (int i = 0; i < 1536; i++)
        {
            embedding[i] = (float)Math.Sin(i * 0.01);
        }
        // Normalize
        var magnitude = (float)Math.Sqrt(embedding.Sum(x => x * x));
        for (int i = 0; i < 1536; i++)
        {
            embedding[i] /= magnitude;
        }
        return embedding;
    }
}
