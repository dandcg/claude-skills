using EmailArchive.Ingest;
using EmailArchive.Models;
using Xunit;

namespace EmailArchive.Tests.Ingest;

public class PstParserTests
{
    [Fact]
    public void MapToEmail_ExtractsBasicFields()
    {
        var parser = new PstParser();

        var mockData = new PstMessageData
        {
            Subject = "Test Subject",
            SenderEmailAddress = "sender@example.com",
            SenderName = "Sender Name",
            Body = "This is the email body with enough words to pass the filter test.",
            ClientSubmitTime = new DateTime(2015, 6, 15, 10, 30, 0),
            Recipients = new List<string> { "recipient@example.com" },
            MessageId = "<test123@example.com>"
        };

        var email = parser.MapToEmail(mockData);

        Assert.Equal("Test Subject", email.Subject);
        Assert.Equal("sender@example.com", email.Sender);
        Assert.Equal("Sender Name", email.SenderName);
        Assert.Equal("<test123@example.com>", email.MessageId);
        Assert.Contains("recipient@example.com", email.Recipients);
    }

    [Fact]
    public void MapToEmail_HandlesNullBody()
    {
        var parser = new PstParser();

        var mockData = new PstMessageData
        {
            Subject = "No Body Email",
            SenderEmailAddress = "sender@example.com",
            SenderName = "Sender",
            Body = null,
            ClientSubmitTime = DateTime.UtcNow,
            Recipients = new List<string>(),
            MessageId = "<test@example.com>"
        };

        var email = parser.MapToEmail(mockData);

        Assert.Equal(string.Empty, email.BodyText);
        Assert.Equal(0, email.BodyWordCount);
    }

    [Fact]
    public void MapToEmail_GeneratesMessageIdIfMissing()
    {
        var parser = new PstParser();

        var mockData = new PstMessageData
        {
            Subject = "No Message ID",
            SenderEmailAddress = "sender@example.com",
            SenderName = "Sender",
            Body = "Some body text",
            ClientSubmitTime = DateTime.UtcNow,
            Recipients = new List<string>(),
            MessageId = null
        };

        var email = parser.MapToEmail(mockData);

        Assert.NotNull(email.MessageId);
        Assert.Contains("@local>", email.MessageId);
    }

    [Fact]
    public void MapToEmail_WithAttachments_SetsHasAttachmentsTrue()
    {
        var parser = new PstParser();

        var mockData = new PstMessageData
        {
            Subject = "Email with attachment",
            SenderEmailAddress = "sender@example.com",
            SenderName = "Sender",
            Body = "Please see attached document.",
            ClientSubmitTime = DateTime.UtcNow,
            Recipients = new List<string>(),
            MessageId = "<test@example.com>",
            HasAttachments = true,
            Attachments = new List<PstAttachmentData>
            {
                new PstAttachmentData
                {
                    Filename = "document.pdf",
                    MimeType = "application/pdf",
                    Content = new byte[] { 0x25, 0x50, 0x44, 0x46 },
                    SizeBytes = 4
                }
            }
        };

        var email = parser.MapToEmail(mockData);

        Assert.True(email.HasAttachments);
    }

    [Fact]
    public void GetAttachments_ReturnsAttachmentData()
    {
        var parser = new PstParser();

        var mockData = new PstMessageData
        {
            Subject = "Email with attachments",
            SenderEmailAddress = "sender@example.com",
            SenderName = "Sender",
            Body = "Multiple attachments",
            ClientSubmitTime = DateTime.UtcNow,
            Recipients = new List<string>(),
            MessageId = "<test@example.com>",
            HasAttachments = true,
            Attachments = new List<PstAttachmentData>
            {
                new PstAttachmentData { Filename = "doc1.pdf", SizeBytes = 1024 },
                new PstAttachmentData { Filename = "doc2.docx", SizeBytes = 2048 }
            }
        };

        var attachments = parser.GetAttachments(mockData);

        Assert.Equal(2, attachments.Count);
        Assert.Equal("doc1.pdf", attachments[0].Filename);
        Assert.Equal("doc2.docx", attachments[1].Filename);
    }
}
