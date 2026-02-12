namespace InstaImporter.Config;

public class AppSettings
{
    public OpenAISettings OpenAI { get; set; } = new();
    public InstagramSettings Instagram { get; set; } = new();
    public BrainSettings Brain { get; set; } = new();
}

public class OpenAISettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o";
}

public class InstagramSettings
{
    public string ExportPath { get; set; } = string.Empty;
    public string TargetUsername { get; set; } = string.Empty;
    public int RateLimitMs { get; set; } = 2000;
    public int MaxVideoSizeMb { get; set; } = 100;
}

public class BrainSettings
{
    public string RepoPath { get; set; } = string.Empty;
    public double ConfidenceThreshold { get; set; } = 0.8;
}
