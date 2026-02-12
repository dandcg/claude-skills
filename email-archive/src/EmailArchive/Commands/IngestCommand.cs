using System.CommandLine;
using EmailArchive.Configuration;
using EmailArchive.Ingest;
using EmailArchive.Models;
using EmailArchive.Storage;
using Spectre.Console;

namespace EmailArchive.Commands;

public class IngestCommand : Command
{
    public IngestCommand() : base("ingest", "Ingest a PST file into the archive")
    {
        var pstPathArg = new Argument<FileInfo>("pst-path", "Path to the PST file to ingest");
        AddArgument(pstPathArg);

        this.SetHandler(ExecuteAsync, pstPathArg);
    }

    private async Task ExecuteAsync(FileInfo pstFile)
    {
        if (!pstFile.Exists)
        {
            AnsiConsole.MarkupLine($"[red]Error: File not found: {pstFile.FullName}[/]");
            return;
        }

        if (!pstFile.Extension.Equals(".pst", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[yellow]Warning: File does not have .pst extension[/]");
        }

        try
        {
            var settings = new AppSettings();

            // Initialize database
            var initializer = new DatabaseInitializer(settings.ConnectionString);
            await initializer.InitializeAsync();

            var emailRepository = new EmailRepository(settings.ConnectionString);
            var attachmentRepository = new AttachmentRepository(settings.ConnectionString);
            var parser = new PstParser();
            var filter = new EmailFilter();
            var attachmentExtractor = new AttachmentExtractor();

            var counts = new Dictionary<string, int>
            {
                ["total"] = 0,
                ["excluded"] = 0,
                ["metadata_only"] = 0,
                ["vectorize"] = 0,
                ["attachments"] = 0,
                ["attachments_with_text"] = 0
            };

            AnsiConsole.MarkupLine($"[bold]Ingesting:[/] {pstFile.FullName}");
            AnsiConsole.WriteLine();

            await AnsiConsole.Status()
                .StartAsync("Processing emails...", async ctx =>
                {
                    foreach (var parsed in parser.ParseFileWithData(pstFile.FullName))
                    {
                        counts["total"]++;
                        var email = parsed.Email;
                        var messageData = parsed.Data;

                        // Classify the email
                        var tier = filter.Classify(email, email.HasAttachments);
                        email.Tier = tier;

                        // Skip Tier 1 entirely
                        if (tier == Tier.Excluded)
                        {
                            counts["excluded"]++;
                            continue;
                        }

                        // Store Tier 2 and Tier 3
                        await emailRepository.InsertAsync(email);

                        if (tier == Tier.MetadataOnly)
                        {
                            counts["metadata_only"]++;
                        }
                        else
                        {
                            counts["vectorize"]++;

                            // Process attachments for Tier 3 emails
                            if (email.HasAttachments && messageData.Attachments.Count > 0)
                            {
                                foreach (var attachmentData in messageData.Attachments)
                                {
                                    counts["attachments"]++;

                                    // Try to extract text
                                    string? extractedText = null;
                                    if (attachmentData.Content.Length > 0)
                                    {
                                        extractedText = attachmentExtractor.ExtractText(
                                            attachmentData.Filename,
                                            attachmentData.MimeType,
                                            attachmentData.Content
                                        );
                                    }

                                    if (extractedText != null)
                                    {
                                        counts["attachments_with_text"]++;
                                    }

                                    var attachment = new Attachment
                                    {
                                        EmailId = email.Id,
                                        Filename = attachmentData.Filename,
                                        MimeType = attachmentData.MimeType ?? attachmentExtractor.GetMimeType(attachmentData.Filename),
                                        SizeBytes = attachmentData.SizeBytes,
                                        ExtractedText = extractedText
                                    };

                                    await attachmentRepository.InsertAsync(attachment);
                                }
                            }
                        }

                        ctx.Status($"Processed {counts["total"]:N0} emails, {counts["attachments"]:N0} attachments...");
                    }
                });

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold green]Ingest complete![/]");
            AnsiConsole.MarkupLine($"  Total emails: [bold]{counts["total"]:N0}[/]");
            AnsiConsole.MarkupLine($"  Excluded (Tier 1): [grey]{counts["excluded"]:N0}[/]");
            AnsiConsole.MarkupLine($"  Metadata only (Tier 2): {counts["metadata_only"]:N0}");
            AnsiConsole.MarkupLine($"  Ready to vectorize (Tier 3): [green]{counts["vectorize"]:N0}[/]");
            AnsiConsole.MarkupLine($"  Attachments: [bold]{counts["attachments"]:N0}[/] ({counts["attachments_with_text"]:N0} with extracted text)");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("Run [bold]email-archive embed[/] to vectorize Tier 3 emails.");
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
}
