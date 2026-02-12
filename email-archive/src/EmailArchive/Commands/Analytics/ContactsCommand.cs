// src/EmailArchive/Commands/Analytics/ContactsCommand.cs
using System.CommandLine;
using EmailArchive.Analytics;
using EmailArchive.Configuration;
using Spectre.Console;

namespace EmailArchive.Commands.Analytics;

public class ContactsCommand : Command
{
    public ContactsCommand() : base("contacts", "Show top contacts by email volume")
    {
        var limitOption = new Option<int>(
            "--limit",
            getDefaultValue: () => 20,
            description: "Number of contacts to show");

        AddOption(limitOption);

        this.SetHandler(ExecuteAsync, limitOption);
    }

    private async Task ExecuteAsync(int limit)
    {
        try
        {
            var settings = new AppSettings();
            var repository = new AnalyticsRepository(settings.ConnectionString);

            var contacts = await repository.GetTopContactsAsync(limit);

            if (contacts.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No contacts found.[/]");
                return;
            }

            AnsiConsole.MarkupLine($"[bold cyan]Top {contacts.Count} Contacts[/]");
            AnsiConsole.WriteLine();

            var table = new Table();
            table.AddColumn("#");
            table.AddColumn("Contact");
            table.AddColumn(new TableColumn("Total").RightAligned());
            table.AddColumn(new TableColumn("Sent").RightAligned());
            table.AddColumn(new TableColumn("Received").RightAligned());
            table.AddColumn("First");
            table.AddColumn("Last");

            var rank = 1;
            foreach (var contact in contacts)
            {
                var displayName = !string.IsNullOrEmpty(contact.Name) && contact.Name != contact.Email.Split('@')[0]
                    ? $"{Markup.Escape(contact.Name)}\n[dim]{Markup.Escape(contact.Email)}[/]"
                    : Markup.Escape(contact.Email);

                table.AddRow(
                    rank.ToString(),
                    displayName,
                    $"[bold]{contact.TotalEmails:N0}[/]",
                    $"[blue]{contact.SentTo:N0}[/]",
                    $"[green]{contact.ReceivedFrom:N0}[/]",
                    contact.FirstContact.ToString("yyyy-MM-dd"),
                    contact.LastContact.ToString("yyyy-MM-dd")
                );
                rank++;
            }

            AnsiConsole.Write(table);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
        }
    }
}
