using EmailArchive.Export;
using Xunit;

namespace EmailArchive.Tests.Export;

public class MarkdownFormatterTests
{
    [Fact]
    public void FormatContactSection_WithContact_ContainsNameAsHeader()
    {
        var contact = new ContactExport
        {
            Email = "alice@example.com",
            Name = "Alice Smith",
            TotalEmails = 150,
            SentTo = 50,
            ReceivedFrom = 100,
            FirstContact = new DateTime(2020, 1, 15),
            LastContact = new DateTime(2024, 6, 20),
            CommunicationDirection = "bidirectional"
        };

        var result = MarkdownFormatter.FormatContactSection(contact);

        Assert.Contains("### Alice Smith", result);
    }

    [Fact]
    public void FormatContactSection_WithContact_ContainsEmail()
    {
        var contact = new ContactExport
        {
            Email = "alice@example.com",
            Name = "Alice Smith",
            TotalEmails = 150,
            SentTo = 50,
            ReceivedFrom = 100,
            FirstContact = new DateTime(2020, 1, 15),
            LastContact = new DateTime(2024, 6, 20),
            CommunicationDirection = "bidirectional"
        };

        var result = MarkdownFormatter.FormatContactSection(contact);

        Assert.Contains("alice@example.com", result);
    }

    [Fact]
    public void FormatContactSection_WithContact_ContainsTotalEmails()
    {
        var contact = new ContactExport
        {
            Email = "alice@example.com",
            Name = "Alice Smith",
            TotalEmails = 150,
            SentTo = 50,
            ReceivedFrom = 100,
            FirstContact = new DateTime(2020, 1, 15),
            LastContact = new DateTime(2024, 6, 20),
            CommunicationDirection = "bidirectional"
        };

        var result = MarkdownFormatter.FormatContactSection(contact);

        Assert.Contains("150 total emails", result);
    }

    [Fact]
    public void FormatContactSection_WithContact_ContainsDates()
    {
        var contact = new ContactExport
        {
            Email = "alice@example.com",
            Name = "Alice Smith",
            TotalEmails = 150,
            SentTo = 50,
            ReceivedFrom = 100,
            FirstContact = new DateTime(2020, 1, 15),
            LastContact = new DateTime(2024, 6, 20),
            CommunicationDirection = "bidirectional"
        };

        var result = MarkdownFormatter.FormatContactSection(contact);

        Assert.Contains("2020-01-15", result);
        Assert.Contains("2024-06-20", result);
    }

    [Fact]
    public void FormatContactSection_WithEmptyName_UsesEmailAsHeader()
    {
        var contact = new ContactExport
        {
            Email = "bob@example.com",
            Name = "",
            TotalEmails = 25,
            SentTo = 10,
            ReceivedFrom = 15,
            FirstContact = new DateTime(2022, 3, 10),
            LastContact = new DateTime(2024, 1, 5),
            CommunicationDirection = "inbound"
        };

        var result = MarkdownFormatter.FormatContactSection(contact);

        Assert.Contains("### bob@example.com", result);
    }

    [Fact]
    public void FormatContactSection_WithContact_ContainsSentAndReceivedCounts()
    {
        var contact = new ContactExport
        {
            Email = "alice@example.com",
            Name = "Alice Smith",
            TotalEmails = 150,
            SentTo = 50,
            ReceivedFrom = 100,
            FirstContact = new DateTime(2020, 1, 15),
            LastContact = new DateTime(2024, 6, 20),
            CommunicationDirection = "bidirectional"
        };

        var result = MarkdownFormatter.FormatContactSection(contact);

        Assert.Contains("50 sent", result);
        Assert.Contains("100 received", result);
    }

    [Fact]
    public void FormatReviewEmailSection_ContainsEmailActivityHeader()
    {
        var review = new ReviewPeriodExport
        {
            PeriodStart = new DateTime(2024, 1, 1),
            PeriodEnd = new DateTime(2024, 1, 31),
            EmailCount = 200,
            SentCount = 80,
            ReceivedCount = 120,
            TopContacts = new List<ContactExport>(),
            PeakActivityDay = "Monday",
            PeakActivityHour = 10
        };

        var result = MarkdownFormatter.FormatReviewEmailSection(review);

        Assert.Contains("## Email Activity", result);
    }

    [Fact]
    public void FormatReviewEmailSection_ContainsCounts()
    {
        var review = new ReviewPeriodExport
        {
            PeriodStart = new DateTime(2024, 1, 1),
            PeriodEnd = new DateTime(2024, 1, 31),
            EmailCount = 200,
            SentCount = 80,
            ReceivedCount = 120,
            TopContacts = new List<ContactExport>(),
            PeakActivityDay = "Monday",
            PeakActivityHour = 10
        };

        var result = MarkdownFormatter.FormatReviewEmailSection(review);

        Assert.Contains("200", result);
        Assert.Contains("80", result);
        Assert.Contains("120", result);
    }

    [Fact]
    public void FormatReviewEmailSection_ContainsTopContacts()
    {
        var topContacts = new List<ContactExport>
        {
            new ContactExport
            {
                Email = "alice@example.com",
                Name = "Alice Smith",
                TotalEmails = 50,
                SentTo = 20,
                ReceivedFrom = 30,
                FirstContact = new DateTime(2024, 1, 5),
                LastContact = new DateTime(2024, 1, 28),
                CommunicationDirection = "bidirectional"
            },
            new ContactExport
            {
                Email = "bob@example.com",
                Name = "Bob Jones",
                TotalEmails = 30,
                SentTo = 15,
                ReceivedFrom = 15,
                FirstContact = new DateTime(2024, 1, 10),
                LastContact = new DateTime(2024, 1, 25),
                CommunicationDirection = "bidirectional"
            }
        };

        var review = new ReviewPeriodExport
        {
            PeriodStart = new DateTime(2024, 1, 1),
            PeriodEnd = new DateTime(2024, 1, 31),
            EmailCount = 200,
            SentCount = 80,
            ReceivedCount = 120,
            TopContacts = topContacts,
            PeakActivityDay = "Monday",
            PeakActivityHour = 10
        };

        var result = MarkdownFormatter.FormatReviewEmailSection(review);

        Assert.Contains("Alice Smith", result);
        Assert.Contains("Bob Jones", result);
    }

    [Fact]
    public void FormatReviewEmailSection_ContainsPeakActivity()
    {
        var review = new ReviewPeriodExport
        {
            PeriodStart = new DateTime(2024, 1, 1),
            PeriodEnd = new DateTime(2024, 1, 31),
            EmailCount = 200,
            SentCount = 80,
            ReceivedCount = 120,
            TopContacts = new List<ContactExport>(),
            PeakActivityDay = "Monday",
            PeakActivityHour = 10
        };

        var result = MarkdownFormatter.FormatReviewEmailSection(review);

        Assert.Contains("Monday", result);
        Assert.Contains("10", result);
    }

    [Fact]
    public void FormatIdeasHeader_ContainsTitle()
    {
        var result = MarkdownFormatter.FormatIdeasHeader("Email Integration Ideas", new DateTime(2024, 2, 15), "seed");

        Assert.Contains("Email Integration Ideas", result);
    }

    [Fact]
    public void FormatIdeasHeader_ContainsAddedDate()
    {
        var result = MarkdownFormatter.FormatIdeasHeader("Email Integration Ideas", new DateTime(2024, 2, 15), "seed");

        Assert.Contains("**Added:**", result);
        Assert.Contains("2024-02-15", result);
    }

    [Fact]
    public void FormatIdeasHeader_ContainsStatus()
    {
        var result = MarkdownFormatter.FormatIdeasHeader("Email Integration Ideas", new DateTime(2024, 2, 15), "seed");

        Assert.Contains("**Status:**", result);
        Assert.Contains("seed", result);
    }

    [Fact]
    public void FormatIdeasHeader_WithDifferentStatus_ContainsCorrectStatus()
    {
        var result = MarkdownFormatter.FormatIdeasHeader("My Idea", new DateTime(2024, 3, 1), "developing");

        Assert.Contains("**Status:** developing", result);
    }
}
