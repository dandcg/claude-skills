namespace InstaImporter.Services;

using InstaImporter.Models;

public interface IInstagramExportParser
{
    Task<List<ContentItem>> ParseExportAsync(string exportPath, string targetUsername);
}
