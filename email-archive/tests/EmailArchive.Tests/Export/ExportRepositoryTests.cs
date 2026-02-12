using EmailArchive.Export;
using EmailArchive.Models;
using EmailArchive.Storage;
using Xunit;

namespace EmailArchive.Tests.Export;

public class ExportRepositoryTests : IAsyncLifetime
{
    private readonly string? _connectionString;
    private EmailRepository? _emailRepository;
    private ExportRepository? _exportRepository;

    public ExportRepositoryTests()
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
        _exportRepository = new ExportRepository(_connectionString);

        await _emailRepository.TruncateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetContactsForPeriodAsync_ReturnsContactsInRange()
    {
        if (_emailRepository is null || _exportRepository is null) return;

        // Seed 4 emails: 3 in January 2023, 1 in February 2023
        // 2 emails from alice@example.com in January (1 sent to, 1 received from)
        // 1 email from bob@example.com in January (received)
        // 1 email from charlie@example.com in February (received)

        var jan15 = new DateTime(2023, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var jan20 = new DateTime(2023, 1, 20, 14, 0, 0, DateTimeKind.Utc);
        var jan25 = new DateTime(2023, 1, 25, 9, 0, 0, DateTimeKind.Utc);
        var feb10 = new DateTime(2023, 2, 10, 11, 0, 0, DateTimeKind.Utc);

        // Email received from Alice
        await _emailRepository.InsertAsync(new Email
        {
            MessageId = $"<{Guid.NewGuid()}@example.com>",
            Date = jan15,
            Sender = "alice@example.com",
            SenderName = "Alice Smith",
            Recipients = new List<string> { "me@example.com" },
            Subject = "Hello from Alice",
            BodyText = "First email from Alice",
            IsSent = false,
            Tier = Tier.MetadataOnly
        });

        // Email sent to Alice
        await _emailRepository.InsertAsync(new Email
        {
            MessageId = $"<{Guid.NewGuid()}@example.com>",
            Date = jan20,
            Sender = "me@example.com",
            SenderName = "Me",
            Recipients = new List<string> { "alice@example.com" },
            Subject = "Reply to Alice",
            BodyText = "My reply to Alice",
            IsSent = true,
            Tier = Tier.MetadataOnly
        });

        // Email received from Bob
        await _emailRepository.InsertAsync(new Email
        {
            MessageId = $"<{Guid.NewGuid()}@example.com>",
            Date = jan25,
            Sender = "bob@example.com",
            SenderName = "Bob Jones",
            Recipients = new List<string> { "me@example.com" },
            Subject = "Hello from Bob",
            BodyText = "Email from Bob",
            IsSent = false,
            Tier = Tier.MetadataOnly
        });

        // Email received from Charlie (February - outside query range)
        await _emailRepository.InsertAsync(new Email
        {
            MessageId = $"<{Guid.NewGuid()}@example.com>",
            Date = feb10,
            Sender = "charlie@example.com",
            SenderName = "Charlie Brown",
            Recipients = new List<string> { "me@example.com" },
            Subject = "Hello from Charlie",
            BodyText = "Email from Charlie",
            IsSent = false,
            Tier = Tier.MetadataOnly
        });

        // Query January only
        var janStart = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var janEnd = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var contacts = await _exportRepository.GetContactsForPeriodAsync(janStart, janEnd, 10);

        // Should have 2 unique contacts in January: Alice and Bob
        Assert.Equal(2, contacts.Count);

        // Verify Alice's stats (2 emails: 1 sent, 1 received)
        var alice = contacts.FirstOrDefault(c => c.Email == "alice@example.com");
        Assert.NotNull(alice);
        Assert.Equal(2, alice.TotalEmails);
        Assert.Equal(1, alice.SentTo);
        Assert.Equal(1, alice.ReceivedFrom);
        Assert.Equal("bidirectional", alice.CommunicationDirection);

        // Verify Bob's stats (1 email received)
        var bob = contacts.FirstOrDefault(c => c.Email == "bob@example.com");
        Assert.NotNull(bob);
        Assert.Equal(1, bob.TotalEmails);
        Assert.Equal(0, bob.SentTo);
        Assert.Equal(1, bob.ReceivedFrom);
        Assert.Equal("inbound", bob.CommunicationDirection);

        // Charlie should not be in results (February email)
        var charlie = contacts.FirstOrDefault(c => c.Email == "charlie@example.com");
        Assert.Null(charlie);
    }

    [Fact]
    public async Task GetReviewDataAsync_ReturnsPeriodSummary()
    {
        if (_emailRepository is null || _exportRepository is null) return;

        // Seed emails for January 2023
        var jan15Mon = new DateTime(2023, 1, 16, 10, 0, 0, DateTimeKind.Utc); // Monday 10am
        var jan16Mon = new DateTime(2023, 1, 16, 14, 0, 0, DateTimeKind.Utc); // Monday 2pm
        var jan17Tue = new DateTime(2023, 1, 17, 10, 0, 0, DateTimeKind.Utc); // Tuesday 10am
        var jan18Wed = new DateTime(2023, 1, 18, 10, 0, 0, DateTimeKind.Utc); // Wednesday 10am

        // Sent email
        await _emailRepository.InsertAsync(new Email
        {
            MessageId = $"<{Guid.NewGuid()}@example.com>",
            Date = jan15Mon,
            Sender = "me@example.com",
            SenderName = "Me",
            Recipients = new List<string> { "alice@example.com" },
            Subject = "Outgoing 1",
            BodyText = "Sent email",
            IsSent = true,
            Tier = Tier.MetadataOnly
        });

        // Received emails
        await _emailRepository.InsertAsync(new Email
        {
            MessageId = $"<{Guid.NewGuid()}@example.com>",
            Date = jan16Mon,
            Sender = "alice@example.com",
            SenderName = "Alice Smith",
            Recipients = new List<string> { "me@example.com" },
            Subject = "Incoming 1",
            BodyText = "Received email",
            IsSent = false,
            Tier = Tier.MetadataOnly
        });

        await _emailRepository.InsertAsync(new Email
        {
            MessageId = $"<{Guid.NewGuid()}@example.com>",
            Date = jan17Tue,
            Sender = "bob@example.com",
            SenderName = "Bob Jones",
            Recipients = new List<string> { "me@example.com" },
            Subject = "Incoming 2",
            BodyText = "Another received email",
            IsSent = false,
            Tier = Tier.MetadataOnly
        });

        await _emailRepository.InsertAsync(new Email
        {
            MessageId = $"<{Guid.NewGuid()}@example.com>",
            Date = jan18Wed,
            Sender = "alice@example.com",
            SenderName = "Alice Smith",
            Recipients = new List<string> { "me@example.com" },
            Subject = "Incoming 3",
            BodyText = "Third received email",
            IsSent = false,
            Tier = Tier.MetadataOnly
        });

        var janStart = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var janEnd = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var reviewData = await _exportRepository.GetReviewDataAsync(janStart, janEnd, 5);

        // Verify period boundaries
        Assert.Equal(janStart, reviewData.PeriodStart);
        Assert.Equal(janEnd, reviewData.PeriodEnd);

        // Verify email counts
        Assert.Equal(4, reviewData.EmailCount);
        Assert.Equal(1, reviewData.SentCount);
        Assert.Equal(3, reviewData.ReceivedCount);

        // Verify top contacts are populated
        Assert.NotEmpty(reviewData.TopContacts);

        // Alice should be top contact (2 emails with her)
        var topContact = reviewData.TopContacts.First();
        Assert.Equal("alice@example.com", topContact.Email);
        Assert.Equal(3, topContact.TotalEmails); // 1 sent + 2 received

        // Verify peak activity - Monday has 2 emails, should be peak day
        Assert.Equal("Monday", reviewData.PeakActivityDay);
    }

    [Fact]
    public async Task GetContactsForPeriodAsync_RespectsLimit()
    {
        if (_emailRepository is null || _exportRepository is null) return;

        var jan15 = new DateTime(2023, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        // Create emails from 5 different contacts
        var contacts = new[] { "a@test.com", "b@test.com", "c@test.com", "d@test.com", "e@test.com" };
        foreach (var contact in contacts)
        {
            await _emailRepository.InsertAsync(new Email
            {
                MessageId = $"<{Guid.NewGuid()}@example.com>",
                Date = jan15,
                Sender = contact,
                SenderName = contact,
                Recipients = new List<string> { "me@example.com" },
                Subject = "Test",
                BodyText = "Test body",
                IsSent = false,
                Tier = Tier.MetadataOnly
            });
        }

        var janStart = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var janEnd = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var result = await _exportRepository.GetContactsForPeriodAsync(janStart, janEnd, 3);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetContactsForPeriodAsync_EmptyRange_ReturnsEmpty()
    {
        if (_emailRepository is null || _exportRepository is null) return;

        // Seed an email in February
        await _emailRepository.InsertAsync(new Email
        {
            MessageId = $"<{Guid.NewGuid()}@example.com>",
            Date = new DateTime(2023, 2, 15, 10, 0, 0, DateTimeKind.Utc),
            Sender = "alice@example.com",
            SenderName = "Alice",
            Recipients = new List<string> { "me@example.com" },
            Subject = "Test",
            BodyText = "Test body",
            IsSent = false,
            Tier = Tier.MetadataOnly
        });

        // Query January (should be empty)
        var janStart = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var janEnd = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var contacts = await _exportRepository.GetContactsForPeriodAsync(janStart, janEnd, 10);

        Assert.Empty(contacts);
    }
}
