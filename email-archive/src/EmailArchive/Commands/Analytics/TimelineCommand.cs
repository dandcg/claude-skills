// src/EmailArchive/Commands/Analytics/TimelineCommand.cs
using System.CommandLine;
using EmailArchive.Analytics;
using EmailArchive.Configuration;
using Spectre.Console;

namespace EmailArchive.Commands.Analytics;

public class TimelineCommand : Command
{
    public TimelineCommand() : base("timeline", "Show email volume over time")
    {
        var monthlyOption = new Option<bool>(
            "--monthly",
            description: "Group by month instead of year");

        var yearOption = new Option<int?>(
            "--year",
            description: "Filter to specific year");

        AddOption(monthlyOption);
        AddOption(yearOption);

        this.SetHandler(ExecuteAsync, monthlyOption, yearOption);
    }

    private async Task ExecuteAsync(bool monthly, int? year)
    {
        try
        {
            var settings = new AppSettings();
            var repository = new AnalyticsRepository(settings.ConnectionString);

            var timeline = await repository.GetTimelineAsync(groupByMonth: monthly);

            if (year.HasValue)
            {
                timeline = timeline.Where(t => t.Year == year.Value).ToList();
            }

            if (timeline.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No email data found.[/]");
                return;
            }

            AnsiConsole.MarkupLine("[bold cyan]Email Timeline[/]");
            AnsiConsole.WriteLine();

            // Display as bar chart
            var chart = new BarChart()
                .Width(60)
                .Label("[green bold]Email Volume[/]")
                .CenterLabel();

            foreach (var period in timeline)
            {
                var label = monthly && period.Month.HasValue
                    ? $"{period.Year}-{period.Month:D2}"
                    : period.Year.ToString();

                chart.AddItem(label, period.EmailCount, Color.Blue);
            }

            AnsiConsole.Write(chart);
            AnsiConsole.WriteLine();

            // Display table with details
            var table = new Table();
            table.AddColumn("Period");
            table.AddColumn(new TableColumn("Total").RightAligned());
            table.AddColumn(new TableColumn("Sent").RightAligned());
            table.AddColumn(new TableColumn("Received").RightAligned());

            foreach (var period in timeline)
            {
                var label = monthly && period.Month.HasValue
                    ? $"{period.Year}-{period.Month:D2}"
                    : period.Year.ToString();

                table.AddRow(
                    label,
                    period.EmailCount.ToString("N0"),
                    $"[blue]{period.SentCount:N0}[/]",
                    $"[green]{period.ReceivedCount:N0}[/]"
                );
            }

            AnsiConsole.Write(table);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
        }
    }
}
