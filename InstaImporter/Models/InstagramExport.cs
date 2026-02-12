namespace InstaImporter.Models;

using System.Text.Json.Serialization;

public class InstagramExport
{
    [JsonPropertyName("participants")]
    public List<Participant> Participants { get; set; } = [];

    [JsonPropertyName("messages")]
    public List<Message> Messages { get; set; } = [];
}

public class Participant
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class Message
{
    [JsonPropertyName("sender_name")]
    public string SenderName { get; set; } = string.Empty;

    [JsonPropertyName("timestamp_ms")]
    public long TimestampMs { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("share")]
    public SharedContent? Share { get; set; }

    public DateTime Timestamp => DateTimeOffset.FromUnixTimeMilliseconds(TimestampMs).DateTime;
}

public class SharedContent
{
    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonPropertyName("share_text")]
    public string? ShareText { get; set; }
}
