// src/EmailArchive/Commands/Export/ContactsExportCommand.cs
using System.CommandLine;
using System.Text;
using EmailArchive.Configuration;
using EmailArchive.Export;
using Spectre.Console;

namespace EmailArchive.Commands.Export;

public class ContactsExportCommand : Command
{
    public ContactsExportCommand() : base("contacts", "Export top contacts to second brain relationships area")
    {
        var outputOption = new Option<string?>(
            aliases: ["-o", "--output"],
            description: "Output file path (default: stdout)");

        var limitOption = new Option<int>(
            aliases: ["-n", "--limit"],
            getDefaultValue: () => 20,
            description: "Number of top contacts to export");

        var minEmailsOption = new Option<int>(
            "--min-emails",
            getDefaultValue: () => 5,
            description: "Minimum emails to include contact");

        AddOption(outputOption);
        AddOption(limitOption);
        AddOption(minEmailsOption);

        this.SetHandler(ExecuteAsync, outputOption, limitOption, minEmailsOption);
    }

    private async Task ExecuteAsync(string? outputPath, int limit, int minEmails)
    {
        try
        {
            var settings = new AppSettings();
            var repository = new ExportRepository(settings.ConnectionString);

            // Get all-time contacts
            var contacts = await repository.GetContactsForPeriodAsync(
                DateTime.MinValue,
                DateTime.MaxValue,
                limit + 100); // Fetch extra to account for filtering

            // Filter by minimum emails and take the limit
            var filteredContacts = contacts
                .Where(c => c.TotalEmails >= minEmails)
                .Take(limit)
                .ToList();

            if (filteredContacts.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No contacts found matching criteria.[/]");
                return;
            }

            // Generate markdown
            var markdown = GenerateContactsMarkdown(filteredContacts);

            // Output to file or stdout
            if (!string.IsNullOrEmpty(outputPath))
            {
                await File.WriteAllTextAsync(outputPath, markdown);
                AnsiConsole.MarkupLine($"[green]Exported {filteredContacts.Count} contacts to {Markup.Escape(outputPath)}[/]");
            }
            else
            {
                AnsiConsole.WriteLine(markdown);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
        }
    }

    private static string GenerateContactsMarkdown(List<ContactExport> contacts)
    {
        var sb = new StringBuilder();

        sb.AppendLine(MarkdownFormatter.FormatIdeasHeader(
            "Email Contacts",
            DateTime.Now,
            "developing"));

        sb.AppendLine("## Core Idea");
        sb.AppendLine("Top email contacts extracted from email archive for relationship tracking.");
        sb.AppendLine();

        sb.AppendLine("## Contacts");
        sb.AppendLine();

        foreach (var contact in contacts)
        {
            sb.Append(MarkdownFormatter.FormatContactSection(contact));
        }

        sb.AppendLine("## Open Questions");
        sb.AppendLine("- Which contacts need more attention?");
        sb.AppendLine("- Are there relationships that have gone cold?");
        sb.AppendLine();

        return sb.ToString();
    }
}
