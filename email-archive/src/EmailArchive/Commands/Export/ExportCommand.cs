// src/EmailArchive/Commands/Export/ExportCommand.cs
using System.CommandLine;

namespace EmailArchive.Commands.Export;

public class ExportCommand : Command
{
    public ExportCommand() : base("export", "Export email data to second brain areas")
    {
        AddCommand(new ContactsExportCommand());
        AddCommand(new ReviewExportCommand());
    }
}
