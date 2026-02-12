using Microsoft.Extensions.Configuration;

namespace EmailArchive.Configuration;

public class AppSettings
{
    public string ConnectionString { get; }
    public string? OpenAIApiKey { get; }

    public AppSettings()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables("EMAIL_ARCHIVE_")
            .Build();

        ConnectionString = config.GetConnectionString("Postgres")
            ?? config["CONNECTION_STRING"]
            ?? throw new InvalidOperationException(
                "Database connection string not configured. " +
                "Set EMAIL_ARCHIVE_CONNECTION_STRING or add to appsettings.json");

        OpenAIApiKey = config["OpenAI:ApiKey"] ?? config["OPENAI_API_KEY"];
    }
}
