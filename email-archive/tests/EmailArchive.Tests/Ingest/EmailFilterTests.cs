using EmailArchive.Ingest;
using EmailArchive.Models;
using Xunit;

namespace EmailArchive.Tests.Ingest;

public class EmailFilterTests
{
    private readonly EmailFilter _filter = new();

    #region Tier 1: Excluded

    [Fact]
    public void Classify_CalendarInvite_ReturnsExcluded()
    {
        var email = CreateEmail(
            subject: "Meeting: Weekly Standup",
            body: "You have been invited to a meeting."
        );

        var result = _filter.Classify(email, hasIcsAttachment: true);

        Assert.Equal(Tier.Excluded, result);
    }

    [Fact]
    public void Classify_DeliveryNotification_ReturnsExcluded()
    {
        var email = CreateEmail(
            sender: "noreply@amazon.com",
            subject: "Your package has been delivered",
            body: "Your package was delivered today."
        );

        var result = _filter.Classify(email);

        Assert.Equal(Tier.Excluded, result);
    }

    [Fact]
    public void Classify_PasswordReset_ReturnsExcluded()
    {
        var email = CreateEmail(
            sender: "noreply@service.com",
            subject: "Password Reset Request",
            body: "Click here to reset your password. Code: 123456"
        );

        var result = _filter.Classify(email);

        Assert.Equal(Tier.Excluded, result);
    }

    #endregion

    #region Tier 2: Metadata Only

    [Fact]
    public void Classify_ShortEmail_ReturnsMetadataOnly()
    {
        var email = CreateEmail(
            sender: "friend@example.com",
            subject: "Re: Lunch",
            body: "Sounds good, see you then!"
        );

        var result = _filter.Classify(email);

        Assert.Equal(Tier.MetadataOnly, result);
    }

    [Fact]
    public void Classify_NoReplySender_ReturnsMetadataOnly()
    {
        var email = CreateEmail(
            sender: "noreply@newsletter.com",
            subject: "Your weekly digest",
            body: string.Join(" ", Enumerable.Repeat("content", 50))
        );

        var result = _filter.Classify(email);

        Assert.Equal(Tier.MetadataOnly, result);
    }

    [Fact]
    public void Classify_OneWordReply_ReturnsMetadataOnly()
    {
        var email = CreateEmail(
            sender: "colleague@work.com",
            subject: "Re: Report",
            body: "Thanks!"
        );

        var result = _filter.Classify(email);

        Assert.Equal(Tier.MetadataOnly, result);
    }

    #endregion

    #region Tier 3: Vectorize

    [Fact]
    public void Classify_RealConversation_ReturnsVectorize()
    {
        var email = CreateEmail(
            sender: "colleague@work.com",
            subject: "Project proposal thoughts",
            body: """
                Hi Dan,

                I've been thinking about the project proposal and have some thoughts.
                The architecture seems solid but I think we should consider a few
                alternatives for the database layer. Let me know when you have time
                to discuss this further. I've attached some notes.

                Best,
                Colleague
                """
        );

        var result = _filter.Classify(email);

        Assert.Equal(Tier.Vectorize, result);
    }

    #endregion

    private static Email CreateEmail(
        string sender = "test@example.com",
        string subject = "Test",
        string body = "Test body")
    {
        return new Email
        {
            MessageId = $"<{Guid.NewGuid()}@example.com>",
            Date = DateTime.UtcNow,
            Sender = sender,
            SenderName = "Test Sender",
            Recipients = new List<string> { "recipient@example.com" },
            Subject = subject,
            BodyText = body
        };
    }
}
