using EmailArchive.Models;
using EmailArchive.Storage;
using Xunit;

namespace EmailArchive.Tests.Storage;

public class EmbeddingRepositoryTests : IAsyncLifetime
{
    private readonly string? _connectionString;
    private EmailRepository? _emailRepository;
    private AttachmentRepository? _attachmentRepository;

    public EmbeddingRepositoryTests()
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
        _attachmentRepository = new AttachmentRepository(_connectionString);
        await _emailRepository.TruncateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetUnembeddedEmailsAsync_ReturnsOnlyTier3WithoutEmbedding()
    {
        if (_emailRepository is null) return;

        // Create emails: Tier 2 (should not return), Tier 3 unembedded (should return), Tier 3 embedded (should not return)
        var tier2 = CreateTestEmail("tier2@test.com", Tier.MetadataOnly);
        var tier3Unembedded = CreateTestEmail("tier3@test.com", Tier.Vectorize);
        var tier3Embedded = CreateTestEmail("embedded@test.com", Tier.Vectorize);
        tier3Embedded.Embedding = new float[1536];
        tier3Embedded.EmbeddedAt = DateTime.UtcNow;

        await _emailRepository.InsertAsync(tier2);
        await _emailRepository.InsertAsync(tier3Unembedded);
        await _emailRepository.InsertAsync(tier3Embedded);

        var unembedded = await _emailRepository.GetUnembeddedEmailsAsync(100);

        Assert.Single(unembedded);
        Assert.Equal("tier3@test.com", unembedded[0].Sender);
    }

    [Fact]
    public async Task UpdateEmbeddingAsync_SetsEmbeddingAndTimestamp()
    {
        if (_emailRepository is null) return;

        var email = CreateTestEmail("test@test.com", Tier.Vectorize);
        await _emailRepository.InsertAsync(email);

        var embedding = Enumerable.Range(0, 1536).Select(i => (float)i / 1536).ToArray();
        await _emailRepository.UpdateEmbeddingAsync(email.Id, embedding);

        var updated = await _emailRepository.GetByIdAsync(email.Id);
        Assert.NotNull(updated);
        Assert.NotNull(updated.EmbeddedAt);
        // Note: Embedding is not loaded by GetByIdAsync to save memory
    }

    [Fact]
    public async Task GetUnembeddedAttachmentsAsync_ReturnsOnlyWithTextAndNoEmbedding()
    {
        if (_emailRepository is null || _attachmentRepository is null) return;

        var email = CreateTestEmail("test@test.com", Tier.Vectorize);
        await _emailRepository.InsertAsync(email);

        // Attachment with text, no embedding (should return)
        var withText = new Attachment
        {
            EmailId = email.Id,
            Filename = "doc.pdf",
            MimeType = "application/pdf",
            SizeBytes = 1024,
            ExtractedText = "Important document content"
        };

        // Attachment without text (should not return)
        var noText = new Attachment
        {
            EmailId = email.Id,
            Filename = "image.jpg",
            MimeType = "image/jpeg",
            SizeBytes = 2048,
            ExtractedText = null
        };

        await _attachmentRepository.InsertAsync(withText);
        await _attachmentRepository.InsertAsync(noText);

        var unembedded = await _attachmentRepository.GetUnembeddedAttachmentsAsync(100);

        Assert.Single(unembedded);
        Assert.Equal("doc.pdf", unembedded[0].Filename);
    }

    private static Email CreateTestEmail(string sender, Tier tier)
    {
        return new Email
        {
            MessageId = $"<{Guid.NewGuid()}@example.com>",
            Date = DateTime.UtcNow,
            Sender = sender,
            SenderName = "Test Sender",
            Recipients = new List<string> { "recipient@example.com" },
            Subject = "Test Subject",
            BodyText = "Test body content for embedding",
            Tier = tier
        };
    }
}
