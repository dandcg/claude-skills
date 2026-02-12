using EmailArchive.Models;
using Xunit;

namespace EmailArchive.Tests.Models;

public class EmailTests
{
    [Fact]
    public void Email_CalculatesWordCount()
    {
        var email = new Email
        {
            MessageId = "<test@example.com>",
            Date = DateTime.UtcNow,
            Sender = "test@example.com",
            SenderName = "Test",
            Recipients = new List<string> { "recipient@example.com" },
            Subject = "Test",
            BodyText = "This is a test email with seven words."
        };

        Assert.Equal(8, email.BodyWordCount);
    }

    [Fact]
    public void Email_GeneratesId()
    {
        var email = new Email
        {
            MessageId = "<test@example.com>",
            Date = DateTime.UtcNow,
            Sender = "test@example.com",
            SenderName = "Test",
            Recipients = new List<string>(),
            Subject = "Test",
            BodyText = "Test"
        };

        Assert.NotEqual(Guid.Empty, email.Id);
    }

    [Fact]
    public void Email_DefaultTierIsUnclassified()
    {
        var email = new Email
        {
            MessageId = "<test@example.com>",
            Date = DateTime.UtcNow,
            Sender = "test@example.com",
            SenderName = "Test",
            Recipients = new List<string>(),
            Subject = "Test",
            BodyText = "Test"
        };

        Assert.Equal(Tier.Unclassified, email.Tier);
    }
}

public class TierTests
{
    [Fact]
    public void Tier_HasCorrectValues()
    {
        Assert.Equal(0, (int)Tier.Unclassified);
        Assert.Equal(1, (int)Tier.Excluded);
        Assert.Equal(2, (int)Tier.MetadataOnly);
        Assert.Equal(3, (int)Tier.Vectorize);
    }
}
