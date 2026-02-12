// src/EmailArchive/Commands/SearchCommand.cs
using System.CommandLine;
using EmailArchive.Configuration;
using EmailArchive.Embedding;
using EmailArchive.Search;
using Spectre.Console;

namespace EmailArchive.Commands;

public class SearchCommand : Command
{
    public SearchCommand() : base("search", "Search emails and attachments using natural language")
    {
        var queryArgument = new Argument<string>(
            "query",
            description: "Natural language search query");

        var limitOption = new Option<int>(
            "--limit",
            getDefaultValue: () => 10,
            description: "Maximum number of results to return");

        var emailsOnlyOption = new Option<bool>(
            "--emails-only",
            description: "Only search emails, skip attachments");

        var attachmentsOnlyOption = new Option<bool>(
            "--attachments-only",
            description: "Only search attachments, skip emails");

        var startDateOption = new Option<DateTime?>(
            "--from",
            description: "Filter emails from this date (YYYY-MM-DD)");

        var endDateOption = new Option<DateTime?>(
            "--to",
            description: "Filter emails until this date (YYYY-MM-DD)");

        var senderOption = new Option<string?>(
            "--sender",
            description: "Filter by sender name or email address");

        AddArgument(queryArgument);
        AddOption(limitOption);
        AddOption(emailsOnlyOption);
        AddOption(attachmentsOnlyOption);
        AddOption(startDateOption);
        AddOption(endDateOption);
        AddOption(senderOption);

        this.SetHandler(ExecuteAsync, queryArgument, limitOption, emailsOnlyOption,
            attachmentsOnlyOption, startDateOption, endDateOption, senderOption);
    }

    private async Task ExecuteAsync(
        string query,
        int limit,
        bool emailsOnly,
        bool attachmentsOnly,
        DateTime? startDate,
        DateTime? endDate,
        string? sender)
    {
        try
        {
            var settings = new AppSettings();

            if (string.IsNullOrEmpty(settings.OpenAIApiKey))
            {
                AnsiConsole.MarkupLine("[red]Error: OpenAI API key not configured.[/]");
                AnsiConsole.MarkupLine("Set EMAIL_ARCHIVE_OPENAI_API_KEY environment variable.");
                return;
            }

            var embeddingService = new OpenAIEmbeddingService(settings.OpenAIApiKey);
            var searchRepository = new SearchRepository(settings.ConnectionString);

            // Embed the query
            AnsiConsole.MarkupLine($"[dim]Searching for:[/] [bold]{Markup.Escape(query)}[/]");
            AnsiConsole.WriteLine();

            var queryEmbedding = await embeddingService.GetEmbeddingAsync(query);
            if (queryEmbedding is null)
            {
                AnsiConsole.MarkupLine("[red]Error: Failed to embed query.[/]");
                return;
            }

            // Search emails
            if (!attachmentsOnly)
            {
                var emailResults = await searchRepository.SearchEmailsAsync(
                    queryEmbedding, limit, startDate, endDate, sender);

                if (emailResults.Count > 0)
                {
                    AnsiConsole.MarkupLine($"[bold cyan]Emails ({emailResults.Count} results)[/]");
                    AnsiConsole.WriteLine();

                    foreach (var result in emailResults)
                    {
                        DisplayEmailResult(result);
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine("[dim]No matching emails found.[/]");
                }
            }

            // Search attachments
            if (!emailsOnly)
            {
                var attachmentResults = await searchRepository.SearchAttachmentsAsync(
                    queryEmbedding, limit);

                if (attachmentResults.Count > 0)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[bold cyan]Attachments ({attachmentResults.Count} results)[/]");
                    AnsiConsole.WriteLine();

                    foreach (var result in attachmentResults)
                    {
                        DisplayAttachmentResult(result);
                    }
                }
                else if (!attachmentsOnly)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]No matching attachments found.[/]");
                }
            }
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Configuration error: {Markup.Escape(ex.Message)}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
        }
    }

    private static void DisplayEmailResult(EmailSearchResult result)
    {
        var similarityColor = result.Similarity >= 0.8 ? "green" :
                             result.Similarity >= 0.6 ? "yellow" : "dim";

        var panel = new Panel(new Markup($"""
            [bold]{Markup.Escape(result.Subject)}[/]
            [dim]{result.Date:yyyy-MM-dd HH:mm}[/] | [blue]{Markup.Escape(result.SenderName)}[/] <{Markup.Escape(result.Sender)}>

            {Markup.Escape(result.BodySnippet)}
            """))
        {
            Header = new PanelHeader($"[{similarityColor}]{result.Similarity:P0} match[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0, 1, 0)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private static void DisplayAttachmentResult(AttachmentSearchResult result)
    {
        var similarityColor = result.Similarity >= 0.8 ? "green" :
                             result.Similarity >= 0.6 ? "yellow" : "dim";

        var panel = new Panel(new Markup($"""
            [bold]{Markup.Escape(result.Filename)}[/]
            [dim]From email:[/] {Markup.Escape(result.EmailSubject)} ({result.EmailDate:yyyy-MM-dd})
            [dim]Sender:[/] {Markup.Escape(result.EmailSender)}

            {Markup.Escape(result.TextSnippet)}
            """))
        {
            Header = new PanelHeader($"[{similarityColor}]{result.Similarity:P0} match[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0, 1, 0)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }
}
