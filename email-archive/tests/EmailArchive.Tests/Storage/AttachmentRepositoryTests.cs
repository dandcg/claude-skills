using EmailArchive.Models;
using EmailArchive.Storage;
using Xunit;

namespace EmailArchive.Tests.Storage;

public class AttachmentRepositoryTests : IAsyncLifetime
{
    private readonly string? _connectionString;
    private EmailRepository? _emailRepository;
    private AttachmentRepository? _attachmentRepository;

    public AttachmentRepositoryTests()
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
    public async Task InsertAsync_StoresAttachment()
    {
        if (_emailRepository is null || _attachmentRepository is null) return;

        var email = CreateTestEmail();
        await _emailRepository.InsertAsync(email);

        var attachment = new Attachment
        {
            EmailId = email.Id,
            Filename = "document.pdf",
            MimeType = "application/pdf",
            SizeBytes = 1024,
            ExtractedText = "This is extracted text from the PDF."
        };

        await _attachmentRepository.InsertAsync(attachment);

        var retrieved = await _attachmentRepository.GetByIdAsync(attachment.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("document.pdf", retrieved.Filename);
        Assert.Equal("This is extracted text from the PDF.", retrieved.ExtractedText);
    }

    [Fact]
    public async Task GetByEmailIdAsync_ReturnsAllAttachments()
    {
        if (_emailRepository is null || _attachmentRepository is null) return;

        var email = CreateTestEmail();
        await _emailRepository.InsertAsync(email);

        await _attachmentRepository.InsertAsync(new Attachment
        {
            EmailId = email.Id,
            Filename = "doc1.pdf",
            MimeType = "application/pdf",
            SizeBytes = 1024
        });

        await _attachmentRepository.InsertAsync(new Attachment
        {
            EmailId = email.Id,
            Filename = "doc2.docx",
            MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            SizeBytes = 2048
        });

        var attachments = await _attachmentRepository.GetByEmailIdAsync(email.Id);
        Assert.Equal(2, attachments.Count);
    }

    [Fact]
    public async Task GetCountAsync_ReturnsCorrectCount()
    {
        if (_emailRepository is null || _attachmentRepository is null) return;

        var email = CreateTestEmail();
        await _emailRepository.InsertAsync(email);

        await _attachmentRepository.InsertAsync(new Attachment
        {
            EmailId = email.Id,
            Filename = "doc1.pdf",
            MimeType = "application/pdf",
            SizeBytes = 1024
        });

        await _attachmentRepository.InsertAsync(new Attachment
        {
            EmailId = email.Id,
            Filename = "doc2.pdf",
            MimeType = "application/pdf",
            SizeBytes = 2048
        });

        var count = await _attachmentRepository.GetCountAsync();
        Assert.Equal(2, count);
    }

    private static Email CreateTestEmail()
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
            Tier = Tier.Vectorize
        };
    }
}
