namespace InstaImporter.Services;

using InstaImporter.Models;

public interface IKnowledgeExtractor
{
    Task<ExtractedKnowledge> ExtractAsync(ContentItem item);
}
