using System.CommandLine;
using EmailArchive.Configuration;
using EmailArchive.Embedding;
using EmailArchive.Storage;
using Spectre.Console;

namespace EmailArchive.Commands;

public class EmbedCommand : Command
{
    public EmbedCommand() : base("embed", "Generate embeddings for emails and attachments")
    {
        var batchSizeOption = new Option<int>(
            "--batch-size",
            getDefaultValue: () => 100,
            description: "Number of items to process per batch");

        var emailsOnlyOption = new Option<bool>(
            "--emails-only",
            description: "Only embed emails, skip attachments");

        var attachmentsOnlyOption = new Option<bool>(
            "--attachments-only",
            description: "Only embed attachments, skip emails");

        AddOption(batchSizeOption);
        AddOption(emailsOnlyOption);
        AddOption(attachmentsOnlyOption);

        this.SetHandler(ExecuteAsync, batchSizeOption, emailsOnlyOption, attachmentsOnlyOption);
    }

    private async Task ExecuteAsync(int batchSize, bool emailsOnly, bool attachmentsOnly)
    {
        try
        {
            var settings = new AppSettings();

            if (string.IsNullOrEmpty(settings.OpenAIApiKey))
            {
                AnsiConsole.MarkupLine("[red]Error: OpenAI API key not configured.[/]");
                AnsiConsole.MarkupLine("Set EMAIL_ARCHIVE_OPENAI_API_KEY environment variable or add to appsettings.json");
                return;
            }

            var emailRepository = new EmailRepository(settings.ConnectionString);
            var attachmentRepository = new AttachmentRepository(settings.ConnectionString);
            var embeddingService = new OpenAIEmbeddingService(settings.OpenAIApiKey);

            var emailsProcessed = 0;
            var attachmentsProcessed = 0;

            // Process emails
            if (!attachmentsOnly)
            {
                emailsProcessed = await ProcessEmailsAsync(
                    emailRepository, embeddingService, batchSize);
            }

            // Process attachments
            if (!emailsOnly)
            {
                attachmentsProcessed = await ProcessAttachmentsAsync(
                    attachmentRepository, embeddingService, batchSize);
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold green]Embedding complete![/]");
            AnsiConsole.MarkupLine($"  Emails embedded: [bold]{emailsProcessed:N0}[/]");
            AnsiConsole.MarkupLine($"  Attachments embedded: [bold]{attachmentsProcessed:N0}[/]");
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Configuration error: {ex.Message}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
        }
    }

    private async Task<int> ProcessEmailsAsync(
        EmailRepository repository,
        OpenAIEmbeddingService embeddingService,
        int batchSize)
    {
        var totalProcessed = 0;

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[cyan]Embedding emails...[/]");
                task.IsIndeterminate = true;

                while (true)
                {
                    var emails = await repository.GetUnembeddedEmailsAsync(batchSize);
                    if (emails.Count == 0)
                        break;

                    task.IsIndeterminate = false;
                    task.MaxValue = emails.Count;
                    task.Value = 0;

                    // Prepare texts for batch embedding
                    var texts = emails.Select(e =>
                        embeddingService.CreateEmailEmbeddingText(e.Subject, e.Sender, e.BodyText)
                    ).ToList();

                    var embeddings = await embeddingService.GetEmbeddingsBatchAsync(texts);

                    for (int i = 0; i < emails.Count; i++)
                    {
                        if (embeddings[i] is not null)
                        {
                            await repository.UpdateEmbeddingAsync(emails[i].Id, embeddings[i]!);
                            totalProcessed++;
                        }
                        task.Increment(1);
                    }
                }

                task.Value = task.MaxValue;
                task.Description = $"[green]Embedded {totalProcessed:N0} emails[/]";
            });

        return totalProcessed;
    }

    private async Task<int> ProcessAttachmentsAsync(
        AttachmentRepository repository,
        OpenAIEmbeddingService embeddingService,
        int batchSize)
    {
        var totalProcessed = 0;

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[cyan]Embedding attachments...[/]");
                task.IsIndeterminate = true;

                while (true)
                {
                    var attachments = await repository.GetUnembeddedAttachmentsAsync(batchSize);
                    if (attachments.Count == 0)
                        break;

                    task.IsIndeterminate = false;
                    task.MaxValue = attachments.Count;
                    task.Value = 0;

                    var texts = attachments.Select(a => a.ExtractedText ?? string.Empty).ToList();
                    var embeddings = await embeddingService.GetEmbeddingsBatchAsync(texts);

                    for (int i = 0; i < attachments.Count; i++)
                    {
                        if (embeddings[i] is not null)
                        {
                            await repository.UpdateEmbeddingAsync(attachments[i].Id, embeddings[i]!);
                            totalProcessed++;
                        }
                        task.Increment(1);
                    }
                }

                task.Value = task.MaxValue;
                task.Description = $"[green]Embedded {totalProcessed:N0} attachments[/]";
            });

        return totalProcessed;
    }
}
