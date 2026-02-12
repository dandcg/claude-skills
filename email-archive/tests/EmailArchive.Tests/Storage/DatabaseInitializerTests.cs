using EmailArchive.Storage;
using Xunit;

namespace EmailArchive.Tests.Storage;

public class DatabaseInitializerTests
{
    [Fact]
    public async Task InitializeAsync_CreatesEmailsTable()
    {
        var connectionString = Environment.GetEnvironmentVariable("EMAIL_ARCHIVE_TEST_DB");
        if (string.IsNullOrEmpty(connectionString))
        {
            return; // Skip if no test DB
        }

        var initializer = new DatabaseInitializer(connectionString);
        await initializer.InitializeAsync();

        var tableExists = await initializer.TableExistsAsync("emails");
        Assert.True(tableExists);
    }

    [Fact]
    public async Task InitializeAsync_CreatesAttachmentsTable()
    {
        var connectionString = Environment.GetEnvironmentVariable("EMAIL_ARCHIVE_TEST_DB");
        if (string.IsNullOrEmpty(connectionString))
        {
            return;
        }

        var initializer = new DatabaseInitializer(connectionString);
        await initializer.InitializeAsync();

        var tableExists = await initializer.TableExistsAsync("attachments");
        Assert.True(tableExists);
    }
}
