using System.CommandLine;
using EmailArchive.Commands;
using EmailArchive.Commands.Analytics;
using EmailArchive.Commands.Export;

var rootCommand = new RootCommand("Process email archives into a searchable vector database");

rootCommand.AddCommand(new StatusCommand());
rootCommand.AddCommand(new IngestCommand());
rootCommand.AddCommand(new EmbedCommand());
rootCommand.AddCommand(new SearchCommand());
rootCommand.AddCommand(new AnalyticsCommand());
rootCommand.AddCommand(new ExportCommand());

return await rootCommand.InvokeAsync(args);
