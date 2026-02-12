namespace InstaImporter.Services;

using InstaImporter.Models;

public interface IContentFetcher
{
    Task<ContentItem> FetchContentAsync(ContentItem item);
}
