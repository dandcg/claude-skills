// src/EmailArchive/Commands/Export/ReviewExportCommand.cs
using System.CommandLine;
using System.Globalization;
using System.Text;
using EmailArchive.Configuration;
using EmailArchive.Export;
using Spectre.Console;

namespace EmailArchive.Commands.Export;

public class ReviewExportCommand : Command
{
    public ReviewExportCommand() : base("review", "Export email activity for weekly or monthly review")
    {
        var periodOption = new Option<string>(
            aliases: ["-p", "--period"],
            getDefaultValue: () => "week",
            description: "Review period: 'week' or 'month'");

        var dateOption = new Option<string?>(
            aliases: ["-d", "--date"],
            description: "Date within period (format: YYYY-MM-DD), default: current date");

        var outputOption = new Option<string?>(
            aliases: ["-o", "--output"],
            description: "Output file path (default: stdout)");

        AddOption(periodOption);
        AddOption(dateOption);
        AddOption(outputOption);

        this.SetHandler(ExecuteAsync, periodOption, dateOption, outputOption);
    }

    private async Task ExecuteAsync(string period, string? dateString, string? outputPath)
    {
        try
        {
            // Parse the date or use current UTC date
            DateTime targetDate;
            if (!string.IsNullOrEmpty(dateString))
            {
                if (!DateTime.TryParseExact(dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out targetDate))
                {
                    AnsiConsole.MarkupLine($"[red]Error: Invalid date format. Use YYYY-MM-DD.[/]");
                    return;
                }
            }
            else
            {
                targetDate = DateTime.UtcNow.Date;
            }

            // Validate period
            period = period.ToLowerInvariant();
            if (period != "week" && period != "month")
            {
                AnsiConsole.MarkupLine($"[red]Error: Period must be 'week' or 'month'.[/]");
                return;
            }

            // Calculate period bounds
            DateTime periodStart, periodEnd;
            string periodLabel;

            if (period == "month")
            {
                // First of month to first of next month
                periodStart = new DateTime(targetDate.Year, targetDate.Month, 1);
                periodEnd = periodStart.AddMonths(1);
                periodLabel = $"Monthly Review: {periodStart:MMMM yyyy}";
            }
            else
            {
                // ISO week: Monday to next Monday
                // DayOfWeek: Sunday=0, Monday=1, ..., Saturday=6
                // For ISO week, we need Monday as start
                int daysFromMonday = ((int)targetDate.DayOfWeek + 6) % 7; // Monday=0, Tuesday=1, ..., Sunday=6
                periodStart = targetDate.AddDays(-daysFromMonday);
                periodEnd = periodStart.AddDays(7);

                // Get ISO week number
                var calendar = CultureInfo.InvariantCulture.Calendar;
                int weekNumber = calendar.GetWeekOfYear(periodStart, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                periodLabel = $"Weekly Review: {periodStart.Year}-W{weekNumber:D2}";
            }

            var settings = new AppSettings();
            var repository = new ExportRepository(settings.ConnectionString);

            // Get review data
            var reviewData = await repository.GetReviewDataAsync(periodStart, periodEnd, 10);

            // Generate markdown
            var markdown = GenerateReviewMarkdown(reviewData, periodLabel);

            // Output to file or stdout
            if (!string.IsNullOrEmpty(outputPath))
            {
                await File.WriteAllTextAsync(outputPath, markdown);
                AnsiConsole.MarkupLine($"[green]Exported {period} review to {Markup.Escape(outputPath)}[/]");
            }
            else
            {
                AnsiConsole.WriteLine(markdown);
            }

            // Show summary
            AnsiConsole.MarkupLine($"[blue]Period:[/] {periodStart:yyyy-MM-dd} to {periodEnd.AddDays(-1):yyyy-MM-dd}");
            AnsiConsole.MarkupLine($"[blue]Total Emails:[/] {reviewData.EmailCount}");
            AnsiConsole.MarkupLine($"[blue]Sent:[/] {reviewData.SentCount} | [blue]Received:[/] {reviewData.ReceivedCount}");
            AnsiConsole.MarkupLine($"[blue]Top Contacts:[/] {reviewData.TopContacts.Count}");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
        }
    }

    private static string GenerateReviewMarkdown(ReviewPeriodExport reviewData, string periodLabel)
    {
        var sb = new StringBuilder();

        // HTML comment header with period label and generation timestamp
        sb.AppendLine($"<!-- {periodLabel} -->");
        sb.AppendLine($"<!-- Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC -->");
        sb.AppendLine();

        // Use the MarkdownFormatter to format the review section
        sb.Append(MarkdownFormatter.FormatReviewEmailSection(reviewData));

        return sb.ToString();
    }
}
