// src/EmailArchive/Commands/Analytics/SummaryCommand.cs
using System.CommandLine;
using EmailArchive.Analytics;
using EmailArchive.Configuration;
using Spectre.Console;

namespace EmailArchive.Commands.Analytics;

public class SummaryCommand : Command
{
    public SummaryCommand() : base("summary", "Show archive overview and statistics")
    {
        this.SetHandler(ExecuteAsync);
    }

    private async Task ExecuteAsync()
    {
        try
        {
            var settings = new AppSettings();
            var repository = new AnalyticsRepository(settings.ConnectionString);

            var summary = await repository.GetArchiveSummaryAsync();
            var hourlyActivity = await repository.GetActivityByHourAsync();
            var dailyActivity = await repository.GetActivityByDayOfWeekAsync();

            // Main summary panel
            var summaryPanel = new Panel(new Markup($"""
                [bold]Total Emails:[/] {summary.TotalEmails:N0}
                [bold]Unique Contacts:[/] {summary.UniqueContacts:N0}
                [bold]Date Range:[/] {summary.EarliestEmail:yyyy-MM-dd} to {summary.LatestEmail:yyyy-MM-dd}
                [bold]Time Span:[/] {summary.TotalYearsSpan} years
                [bold]Avg Emails/Day:[/] {summary.AvgEmailsPerDay:F1}
                """))
            {
                Header = new PanelHeader("[bold cyan]Archive Summary[/]"),
                Border = BoxBorder.Rounded,
                Padding = new Padding(2, 1)
            };

            AnsiConsole.Write(summaryPanel);
            AnsiConsole.WriteLine();

            // Activity by hour
            if (hourlyActivity.Count > 0)
            {
                AnsiConsole.MarkupLine("[bold cyan]Activity by Hour[/]");
                var hourChart = new BarChart().Width(60);

                foreach (var stat in hourlyActivity.OrderBy(s => s.Hour))
                {
                    hourChart.AddItem($"{stat.Hour:D2}:00", stat.EmailCount, Color.Blue);
                }

                AnsiConsole.Write(hourChart);
                AnsiConsole.WriteLine();
            }

            // Activity by day of week
            if (dailyActivity.Count > 0)
            {
                AnsiConsole.MarkupLine("[bold cyan]Activity by Day of Week[/]");
                var dayNames = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
                var dayChart = new BarChart().Width(60);

                foreach (var stat in dailyActivity.OrderBy(s => s.DayOfWeek))
                {
                    var dayName = stat.DayOfWeek >= 0 && stat.DayOfWeek < 7
                        ? dayNames[stat.DayOfWeek]
                        : stat.DayOfWeek.ToString();
                    dayChart.AddItem(dayName, stat.EmailCount, Color.Green);
                }

                AnsiConsole.Write(dayChart);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
        }
    }
}
