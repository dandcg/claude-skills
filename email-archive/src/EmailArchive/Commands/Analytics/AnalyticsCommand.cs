using System.CommandLine;

namespace EmailArchive.Commands.Analytics;

public class AnalyticsCommand : Command
{
    public AnalyticsCommand() : base("analytics", "Analyze email patterns and statistics")
    {
        AddCommand(new SummaryCommand());
        AddCommand(new TimelineCommand());
        AddCommand(new ContactsCommand());
    }
}
