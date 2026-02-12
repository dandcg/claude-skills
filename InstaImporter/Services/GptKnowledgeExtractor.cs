namespace InstaImporter.Services;

using System.Text.Json;
using OpenAI;
using OpenAI.Chat;
using InstaImporter.Config;
using InstaImporter.Models;

public class GptKnowledgeExtractor : IKnowledgeExtractor
{
    private readonly ChatClient _chatClient;
    private readonly AppSettings _settings;

    private const string SystemPrompt = """
        You are extracting factual knowledge from social media content.
        Be concise. Extract only concrete, actionable information.
        Ignore opinions, entertainment, and fluff.

        Respond ONLY with valid JSON in this exact format:
        {
            "hasContent": true,
            "facts": ["fact 1", "fact 2"],
            "category": "health",
            "confidence": 0.85,
            "summary": "Brief 10-word max summary"
        }

        If there is no useful factual content, respond with:
        {"hasContent": false, "facts": [], "category": "general", "confidence": 0, "summary": ""}

        Valid categories: health, relationships, finance, business, technical, philosophy, mental, career, income, general
        """;

    public GptKnowledgeExtractor(AppSettings settings)
    {
        _settings = settings;
        var client = new OpenAIClient(settings.OpenAI.ApiKey);
        _chatClient = client.GetChatClient(settings.OpenAI.Model);
    }

    public async Task<ExtractedKnowledge> ExtractAsync(ContentItem item)
    {
        var result = new ExtractedKnowledge { Source = item };

        if (item.Status != FetchStatus.Success)
        {
            result.HasContent = false;
            return result;
        }

        var userPrompt = BuildUserPrompt(item);

        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(SystemPrompt),
                new UserChatMessage(userPrompt)
            };

            var response = await _chatClient.CompleteChatAsync(messages, new ChatCompletionOptions
            {
                Temperature = 0.3f,
                MaxOutputTokenCount = 500
            });

            var content = response.Value.Content[0].Text;
            var parsed = JsonSerializer.Deserialize<ExtractionResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed != null)
            {
                result.HasContent = parsed.HasContent;
                result.Facts = parsed.Facts ?? [];
                result.Category = BrainCategories.IsValid(parsed.Category ?? "")
                    ? parsed.Category!.ToLowerInvariant()
                    : "general";
                result.Confidence = Math.Clamp(parsed.Confidence, 0, 1);
                result.Summary = parsed.Summary ?? "";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Knowledge extraction failed: {ex.Message}");
            result.HasContent = false;
        }

        return result;
    }

    private static string BuildUserPrompt(ContentItem item)
    {
        var type = item.IsReel ? "reel" : "post";
        var date = item.SharedAt.ToString("yyyy-MM-dd");

        var prompt = $"Content source: Instagram {type} shared on {date}\n\n";

        if (!string.IsNullOrEmpty(item.Caption))
        {
            prompt += $"Caption: {item.Caption}\n\n";
        }

        if (!string.IsNullOrEmpty(item.Transcript))
        {
            prompt += $"Video transcript: {item.Transcript}\n\n";
        }

        prompt += """
            Extract:
            1. Key facts (bullet points, max 5)
            2. Category (one of: health, relationships, finance, business, technical, philosophy, mental, career, income, general)
            3. Confidence in category (0.0-1.0)
            4. One-line summary (max 10 words)
            """;

        return prompt;
    }

    private class ExtractionResponse
    {
        public bool HasContent { get; set; }
        public List<string>? Facts { get; set; }
        public string? Category { get; set; }
        public double Confidence { get; set; }
        public string? Summary { get; set; }
    }
}
