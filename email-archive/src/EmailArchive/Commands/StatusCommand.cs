using System.CommandLine;
using EmailArchive.Configuration;
using EmailArchive.Storage;
using Spectre.Console;

namespace EmailArchive.Commands;

public class StatusCommand : Command
{
    public StatusCommand() : base("status", "Show current archive status")
    {
        this.SetHandler(ExecuteAsync);
    }

    private async Task ExecuteAsync()
    {
        try
        {
            var settings = new AppSettings();
            var emailRepository = new EmailRepository(settings.ConnectionString);
            var attachmentRepository = new AttachmentRepository(settings.ConnectionString);

            var counts = await emailRepository.GetStatusCountsAsync();
            var attachmentCount = await attachmentRepository.GetCountAsync();
            var attachmentsWithText = await attachmentRepository.GetWithTextCountAsync();
            var attachmentsEmbedded = await attachmentRepository.GetEmbeddedCountAsync();

            var table = new Table();
            table.Title = new TableTitle("Email Archive Status");
            table.AddColumn(new TableColumn("Category").Centered());
            table.AddColumn(new TableColumn("Count").RightAligned());

            table.AddRow("Total Emails", counts.Total.ToString("N0"));
            table.AddRow("[grey]Tier 1 (Excluded)[/]", $"[grey]{counts.Excluded:N0}[/]");
            table.AddRow("Tier 2 (Metadata Only)", counts.MetadataOnly.ToString("N0"));
            table.AddRow("Tier 3 (To Vectorize)", counts.Vectorize.ToString("N0"));
            table.AddRow("[green]  Emails Embedded[/]", $"[green]{counts.Embedded:N0}[/]");
            table.AddRow("[yellow]  Emails Pending[/]", $"[yellow]{(counts.Vectorize - counts.Embedded):N0}[/]");
            table.AddEmptyRow();
            table.AddRow("[bold]Attachments[/]", $"[bold]{attachmentCount:N0}[/]");
            table.AddRow("  With Extracted Text", attachmentsWithText.ToString("N0"));
            table.AddRow("[green]  Attachments Embedded[/]", $"[green]{attachmentsEmbedded:N0}[/]");
            table.AddRow("[yellow]  Attachments Pending[/]", $"[yellow]{(attachmentsWithText - attachmentsEmbedded):N0}[/]");

            AnsiConsole.Write(table);
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
        }
        catch (Npgsql.NpgsqlException)
        {
            AnsiConsole.MarkupLine("[yellow]No archive loaded yet.[/]");
            AnsiConsole.MarkupLine("Run [bold]email-archive ingest <path-to-pst>[/] to get started.");
        }
    }
}
