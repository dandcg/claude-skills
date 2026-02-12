using EmailArchive.Commands.Export;
using Xunit;

namespace EmailArchive.Tests.Commands.Export;

public class ExportCommandTests
{
    [Fact]
    public void ContactsExportCommand_CanBeCreated()
    {
        var cmd = new ContactsExportCommand();
        Assert.Equal("contacts", cmd.Name);
    }

    [Fact]
    public void ContactsExportCommand_HasCorrectDescription()
    {
        var cmd = new ContactsExportCommand();
        Assert.Equal("Export top contacts to second brain relationships area", cmd.Description);
    }

    [Fact]
    public void ContactsExportCommand_HasOutputOption()
    {
        var cmd = new ContactsExportCommand();
        var outputOption = cmd.Options.FirstOrDefault(o => o.Name == "output");
        Assert.NotNull(outputOption);
        Assert.Contains("-o", outputOption.Aliases);
        Assert.Contains("--output", outputOption.Aliases);
    }

    [Fact]
    public void ContactsExportCommand_HasLimitOption()
    {
        var cmd = new ContactsExportCommand();
        var limitOption = cmd.Options.FirstOrDefault(o => o.Name == "limit");
        Assert.NotNull(limitOption);
        Assert.Contains("-n", limitOption.Aliases);
        Assert.Contains("--limit", limitOption.Aliases);
    }

    [Fact]
    public void ContactsExportCommand_HasMinEmailsOption()
    {
        var cmd = new ContactsExportCommand();
        var minEmailsOption = cmd.Options.FirstOrDefault(o => o.Name == "min-emails");
        Assert.NotNull(minEmailsOption);
        Assert.Contains("--min-emails", minEmailsOption.Aliases);
    }

    [Fact]
    public void ReviewExportCommand_CanBeCreated()
    {
        var cmd = new ReviewExportCommand();
        Assert.Equal("review", cmd.Name);
    }

    [Fact]
    public void ReviewExportCommand_HasCorrectDescription()
    {
        var cmd = new ReviewExportCommand();
        Assert.Equal("Export email activity for weekly or monthly review", cmd.Description);
    }

    [Fact]
    public void ReviewExportCommand_HasPeriodOption()
    {
        var cmd = new ReviewExportCommand();
        var periodOption = cmd.Options.FirstOrDefault(o => o.Name == "period");
        Assert.NotNull(periodOption);
        Assert.Contains("-p", periodOption.Aliases);
        Assert.Contains("--period", periodOption.Aliases);
    }

    [Fact]
    public void ReviewExportCommand_HasDateOption()
    {
        var cmd = new ReviewExportCommand();
        var dateOption = cmd.Options.FirstOrDefault(o => o.Name == "date");
        Assert.NotNull(dateOption);
        Assert.Contains("-d", dateOption.Aliases);
        Assert.Contains("--date", dateOption.Aliases);
    }

    [Fact]
    public void ReviewExportCommand_HasOutputOption()
    {
        var cmd = new ReviewExportCommand();
        var outputOption = cmd.Options.FirstOrDefault(o => o.Name == "output");
        Assert.NotNull(outputOption);
        Assert.Contains("-o", outputOption.Aliases);
        Assert.Contains("--output", outputOption.Aliases);
    }

    [Fact]
    public void ExportCommand_CanBeCreated()
    {
        var cmd = new ExportCommand();
        Assert.Equal("export", cmd.Name);
    }

    [Fact]
    public void ExportCommand_HasCorrectDescription()
    {
        var cmd = new ExportCommand();
        Assert.Equal("Export email data to second brain areas", cmd.Description);
    }

    [Fact]
    public void ExportCommand_HasSubcommands()
    {
        var cmd = new ExportCommand();
        Assert.Equal("export", cmd.Name);
        Assert.Equal(2, cmd.Subcommands.Count);
    }

    [Fact]
    public void ExportCommand_HasContactsSubcommand()
    {
        var cmd = new ExportCommand();
        var contactsSubcommand = cmd.Subcommands.FirstOrDefault(s => s.Name == "contacts");
        Assert.NotNull(contactsSubcommand);
        Assert.IsType<ContactsExportCommand>(contactsSubcommand);
    }

    [Fact]
    public void ExportCommand_HasReviewSubcommand()
    {
        var cmd = new ExportCommand();
        var reviewSubcommand = cmd.Subcommands.FirstOrDefault(s => s.Name == "review");
        Assert.NotNull(reviewSubcommand);
        Assert.IsType<ReviewExportCommand>(reviewSubcommand);
    }
}
