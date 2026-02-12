using EmailArchive.Analytics;
using EmailArchive.Embedding;
using EmailArchive.Ingest;
using EmailArchive.Models;
using EmailArchive.Search;
using EmailArchive.Storage;
using Xunit;

namespace EmailArchive.Tests;

public class IntegrationTests : IAsyncLifetime
{
    private readonly string? _connectionString;
    private EmailRepository? _emailRepository;
    private AttachmentRepository? _attachmentRepository;

    public IntegrationTests()
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
    public async Task FullPipeline_EmailFlowsFromParsingToStorage()
    {
        if (_emailRepository is null) return;

        // Create mock message data
        var messageData = new PstMessageData
        {
            Subject = "Project Proposal Discussion",
            SenderEmailAddress = "colleague@work.com",
            SenderName = "Work Colleague",
            Body = """
                Hi Dan,

                I've been reviewing the project proposal and have some thoughts
                I'd like to share with you. The architecture looks solid overall,
                but I think we should consider some alternatives for the database
                layer before we commit to a final design.

                Can we schedule a meeting to discuss this further?

                Best regards,
                Colleague
                """,
            ClientSubmitTime = new DateTime(2015, 6, 15, 10, 30, 0),
            Recipients = new List<string> { "dan@work.com" },
            MessageId = "<proposal123@work.com>"
        };

        // Parse
        var parser = new PstParser();
        var email = parser.MapToEmail(messageData);

        Assert.Equal("colleague@work.com", email.Sender);
        Assert.Equal("<proposal123@work.com>", email.MessageId);

        // Classify
        var filter = new EmailFilter();
        var tier = filter.Classify(email);
        email.Tier = tier;

        Assert.Equal(Tier.Vectorize, tier);

        // Store
        await _emailRepository.InsertAsync(email);

        // Verify
        var stored = await _emailRepository.GetByIdAsync(email.Id);
        Assert.NotNull(stored);
        Assert.Equal("colleague@work.com", stored.Sender);
        Assert.Equal(Tier.Vectorize, stored.Tier);

        // Verify counts
        var counts = await _emailRepository.GetStatusCountsAsync();
        Assert.Equal(1, counts.Total);
        Assert.Equal(1, counts.Vectorize);
    }

    [Fact]
    public async Task FullPipeline_ExcludedEmailNotStored()
    {
        if (_emailRepository is null) return;

        var messageData = new PstMessageData
        {
            Subject = "Password Reset Request",
            SenderEmailAddress = "noreply@service.com",
            SenderName = "Service",
            Body = "Click here to reset your password. Your code is 123456.",
            ClientSubmitTime = DateTime.UtcNow,
            Recipients = new List<string> { "dan@example.com" },
            MessageId = "<reset@service.com>"
        };

        var parser = new PstParser();
        var email = parser.MapToEmail(messageData);

        var filter = new EmailFilter();
        var tier = filter.Classify(email);

        Assert.Equal(Tier.Excluded, tier);

        // In real pipeline, excluded emails are not stored
        // Verify database remains empty
        var counts = await _emailRepository.GetStatusCountsAsync();
        Assert.Equal(0, counts.Total);
    }

    [Fact]
    public async Task FullPipeline_MetadataOnlyEmailStoredNotVectorized()
    {
        if (_emailRepository is null) return;

        var messageData = new PstMessageData
        {
            Subject = "Re: Lunch tomorrow?",
            SenderEmailAddress = "friend@example.com",
            SenderName = "Friend",
            Body = "Sounds good!",
            ClientSubmitTime = DateTime.UtcNow,
            Recipients = new List<string> { "dan@example.com" },
            MessageId = "<short@example.com>"
        };

        var parser = new PstParser();
        var email = parser.MapToEmail(messageData);

        var filter = new EmailFilter();
        var tier = filter.Classify(email);
        email.Tier = tier;

        Assert.Equal(Tier.MetadataOnly, tier);

        // Store metadata-only email
        await _emailRepository.InsertAsync(email);

        var counts = await _emailRepository.GetStatusCountsAsync();
        Assert.Equal(1, counts.Total);
        Assert.Equal(1, counts.MetadataOnly);
        Assert.Equal(0, counts.Vectorize);
    }

    [Fact]
    public async Task FullPipeline_EmailWithAttachmentsProcessed()
    {
        if (_emailRepository is null || _attachmentRepository is null) return;

        // Create message data with attachments
        var messageData = new PstMessageData
        {
            Subject = "Q4 Report Attached",
            SenderEmailAddress = "finance@company.com",
            SenderName = "Finance Team",
            Body = """
                Hi Dan,

                Please find the Q4 financial report attached. Let me know if you have
                any questions about the numbers or need additional breakdowns.

                Regards,
                Finance Team
                """,
            ClientSubmitTime = new DateTime(2020, 1, 15, 9, 0, 0),
            Recipients = new List<string> { "dan@company.com" },
            MessageId = "<q4report@company.com>",
            HasAttachments = true,
            Attachments = new List<PstAttachmentData>
            {
                new PstAttachmentData
                {
                    Filename = "Q4_Report.txt",
                    MimeType = "text/plain",
                    Content = System.Text.Encoding.UTF8.GetBytes("Q4 Revenue: $1,234,567\nQ4 Expenses: $987,654"),
                    SizeBytes = 45
                }
            }
        };

        // Parse email
        var parser = new PstParser();
        var email = parser.MapToEmail(messageData);

        // Classify - should be Tier 3 (has meaningful content)
        var filter = new EmailFilter();
        var tier = filter.Classify(email, email.HasAttachments);
        email.Tier = tier;

        Assert.Equal(Tier.Vectorize, tier);

        // Store email
        await _emailRepository.InsertAsync(email);

        // Process attachments
        var extractor = new AttachmentExtractor();
        foreach (var attachmentData in messageData.Attachments)
        {
            var extractedText = extractor.ExtractText(
                attachmentData.Filename,
                attachmentData.MimeType,
                attachmentData.Content
            );

            var attachment = new Attachment
            {
                EmailId = email.Id,
                Filename = attachmentData.Filename,
                MimeType = attachmentData.MimeType ?? extractor.GetMimeType(attachmentData.Filename),
                SizeBytes = attachmentData.SizeBytes,
                ExtractedText = extractedText
            };

            await _attachmentRepository.InsertAsync(attachment);
        }

        // Verify email stored
        var storedEmail = await _emailRepository.GetByIdAsync(email.Id);
        Assert.NotNull(storedEmail);
        Assert.True(storedEmail.HasAttachments);

        // Verify attachments stored
        var attachments = await _attachmentRepository.GetByEmailIdAsync(email.Id);
        Assert.Single(attachments);
        Assert.Equal("Q4_Report.txt", attachments[0].Filename);
        Assert.Contains("Q4 Revenue", attachments[0].ExtractedText);

        // Verify counts
        var emailCounts = await _emailRepository.GetStatusCountsAsync();
        Assert.Equal(1, emailCounts.Total);
        Assert.Equal(1, emailCounts.Vectorize);

        var attachmentCount = await _attachmentRepository.GetCountAsync();
        Assert.Equal(1, attachmentCount);

        var attachmentsWithText = await _attachmentRepository.GetWithTextCountAsync();
        Assert.Equal(1, attachmentsWithText);
    }

    [Fact]
    public async Task FullPipeline_UnsupportedAttachmentStoredWithoutText()
    {
        if (_emailRepository is null || _attachmentRepository is null) return;

        var email = new Email
        {
            MessageId = "<withimage@example.com>",
            Date = DateTime.UtcNow,
            Sender = "photos@example.com",
            SenderName = "Photo Sender",
            Recipients = new List<string> { "dan@example.com" },
            Subject = "Check out this photo",
            BodyText = "Here's the image from our trip last weekend. Great memories!",
            HasAttachments = true,
            Tier = Tier.Vectorize
        };

        await _emailRepository.InsertAsync(email);

        // Simulate an image attachment (unsupported for text extraction)
        var extractor = new AttachmentExtractor();
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // JPEG magic bytes

        var extractedText = extractor.ExtractText("vacation.jpg", "image/jpeg", imageBytes);
        Assert.Null(extractedText); // Should not extract text from images

        var attachment = new Attachment
        {
            EmailId = email.Id,
            Filename = "vacation.jpg",
            MimeType = "image/jpeg",
            SizeBytes = imageBytes.Length,
            ExtractedText = extractedText
        };

        await _attachmentRepository.InsertAsync(attachment);

        // Verify attachment is stored
        var attachments = await _attachmentRepository.GetByEmailIdAsync(email.Id);
        Assert.Single(attachments);
        Assert.Equal("vacation.jpg", attachments[0].Filename);
        Assert.Null(attachments[0].ExtractedText);

        // Verify counts
        var totalAttachments = await _attachmentRepository.GetCountAsync();
        Assert.Equal(1, totalAttachments);

        var withText = await _attachmentRepository.GetWithTextCountAsync();
        Assert.Equal(0, withText);
    }

    [Fact]
    public async Task FullPipeline_EmbeddingServicePreparesTextCorrectly()
    {
        // This test doesn't call OpenAI API, just verifies text preparation
        var embeddingService = new OpenAIEmbeddingService("fake-key");

        var emailText = embeddingService.CreateEmailEmbeddingText(
            subject: "Q4 Budget Review",
            sender: "finance@company.com",
            body: "Please review the attached budget spreadsheet for Q4."
        );

        Assert.Contains("Subject: Q4 Budget Review", emailText);
        Assert.Contains("From: finance@company.com", emailText);
        Assert.Contains("budget spreadsheet", emailText);
    }

    [Fact]
    public async Task FullPipeline_UnembeddedEmailsRetrievedCorrectly()
    {
        if (_emailRepository is null) return;

        // Insert Tier 3 email without embedding
        var email = new Email
        {
            MessageId = "<unembedded@example.com>",
            Date = DateTime.UtcNow,
            Sender = "test@example.com",
            SenderName = "Test",
            Recipients = new List<string> { "recipient@example.com" },
            Subject = "Test Email",
            BodyText = "This email needs embedding.",
            Tier = Tier.Vectorize
        };

        await _emailRepository.InsertAsync(email);

        var unembedded = await _emailRepository.GetUnembeddedEmailsAsync(10);

        Assert.Single(unembedded);
        Assert.Equal("<unembedded@example.com>", unembedded[0].MessageId);
    }

    [Fact]
    public async Task FullPipeline_SemanticSearchFindsRelevantEmails()
    {
        if (_emailRepository is null) return;

        // Insert email with embedding
        var embedding = CreateNormalizedTestEmbedding();
        var email = new Email
        {
            MessageId = "<semantic-search-test@example.com>",
            Date = DateTime.UtcNow,
            Sender = "budget@company.com",
            SenderName = "Budget Team",
            Recipients = new List<string> { "dan@company.com" },
            Subject = "Q4 Marketing Budget Review",
            BodyText = "Please review the attached Q4 marketing budget spreadsheet.",
            Tier = Tier.Vectorize,
            Embedding = embedding,
            EmbeddedAt = DateTime.UtcNow
        };

        await _emailRepository.InsertAsync(email);

        // Search using same embedding (simulating semantic similarity)
        var searchRepository = new SearchRepository(_connectionString!);
        var results = await searchRepository.SearchEmailsAsync(embedding, limit: 5);

        Assert.Single(results);
        Assert.Contains("Budget", results[0].Subject);
        Assert.True(results[0].Similarity > 0.99); // Same embedding = perfect match
    }

    [Fact]
    public async Task FullPipeline_AnalyticsReturnsCorrectSummary()
    {
        if (_emailRepository is null) return;

        // Insert emails across different dates
        var email1 = new Email
        {
            MessageId = "<analytics-1@example.com>",
            Date = new DateTime(2020, 6, 15, 10, 30, 0),
            Sender = "alice@example.com",
            SenderName = "Alice",
            Recipients = new List<string> { "me@example.com" },
            Subject = "Hello",
            BodyText = "Test email from Alice",
            IsSent = false,
            Tier = Tier.Vectorize
        };

        var email2 = new Email
        {
            MessageId = "<analytics-2@example.com>",
            Date = new DateTime(2022, 12, 25, 14, 0, 0),
            Sender = "me@example.com",
            SenderName = "Me",
            Recipients = new List<string> { "bob@example.com" },
            Subject = "Reply",
            BodyText = "Test email to Bob",
            IsSent = true,
            Tier = Tier.Vectorize
        };

        await _emailRepository.InsertAsync(email1);
        await _emailRepository.InsertAsync(email2);

        var analyticsRepository = new AnalyticsRepository(_connectionString!);
        var summary = await analyticsRepository.GetArchiveSummaryAsync();

        Assert.Equal(2, summary.TotalEmails);
        Assert.Equal(2, summary.UniqueContacts);
        Assert.Equal(2020, summary.EarliestEmail.Year);
        Assert.Equal(2022, summary.LatestEmail.Year);
    }

    private static float[] CreateNormalizedTestEmbedding()
    {
        var embedding = new float[1536];
        for (int i = 0; i < 1536; i++)
        {
            embedding[i] = (float)Math.Sin(i * 0.01);
        }
        var magnitude = (float)Math.Sqrt(embedding.Sum(x => x * x));
        for (int i = 0; i < 1536; i++)
        {
            embedding[i] /= magnitude;
        }
        return embedding;
    }
}
