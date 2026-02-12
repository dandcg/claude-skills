namespace InstaImporter.Models;

public class ExtractedKnowledge
{
    public required ContentItem Source { get; set; }
    public List<string> Facts { get; set; } = [];
    public string Category { get; set; } = "general";
    public double Confidence { get; set; }
    public string Summary { get; set; } = string.Empty;
    public bool HasContent { get; set; } = true;
}

public static class BrainCategories
{
    public static readonly string[] ValidCategories =
    [
        "health",
        "relationships",
        "finance",
        "business",
        "technical",
        "philosophy",
        "mental",
        "career",
        "income",
        "general"
    ];

    public static bool IsValid(string category) =>
        ValidCategories.Contains(category.ToLowerInvariant());
}
