using System.Text.RegularExpressions;
using OpenAI;
using OpenAI.Embeddings;

namespace EmailArchive.Embedding;

public class OpenAIEmbeddingService
{
    private readonly string _apiKey;
    private readonly string _model;
    private const int MaxChars = 32000; // ~8000 tokens for text-embedding-3-small

    public OpenAIEmbeddingService(string apiKey, string model = "text-embedding-3-small")
    {
        _apiKey = apiKey;
        _model = model;
    }

    /// <summary>
    /// Prepare text for embedding by normalizing and truncating.
    /// </summary>
    public string PrepareTextForEmbedding(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Normalize newlines
        text = text.Replace("\r\n", "\n");

        // Collapse multiple newlines to max 2
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        // Trim whitespace
        text = text.Trim();

        // Truncate if too long
        if (text.Length > MaxChars)
            text = text[..MaxChars];

        return text;
    }

    /// <summary>
    /// Create structured text for email embedding.
    /// </summary>
    public string CreateEmailEmbeddingText(string subject, string sender, string body)
    {
        return $"Subject: {subject}\nFrom: {sender}\n\n{body}";
    }

    /// <summary>
    /// Get embedding vector for text.
    /// </summary>
    public async Task<float[]?> GetEmbeddingAsync(string text)
    {
        var prepared = PrepareTextForEmbedding(text);
        if (string.IsNullOrWhiteSpace(prepared))
            return null;

        var client = new OpenAIClient(_apiKey);
        var embeddingClient = client.GetEmbeddingClient(_model);

        var response = await embeddingClient.GenerateEmbeddingAsync(prepared);
        return response.Value.ToFloats().ToArray();
    }

    /// <summary>
    /// Get embeddings for multiple texts in a batch.
    /// </summary>
    public async Task<List<float[]?>> GetEmbeddingsBatchAsync(IEnumerable<string> texts)
    {
        var preparedTexts = texts.Select(PrepareTextForEmbedding).ToList();
        var results = new List<float[]?>();

        // Filter out empty texts but track their positions
        var nonEmptyIndices = new List<int>();
        var nonEmptyTexts = new List<string>();

        for (int i = 0; i < preparedTexts.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(preparedTexts[i]))
            {
                nonEmptyIndices.Add(i);
                nonEmptyTexts.Add(preparedTexts[i]);
            }
        }

        if (nonEmptyTexts.Count == 0)
        {
            return preparedTexts.Select(_ => (float[]?)null).ToList();
        }

        var client = new OpenAIClient(_apiKey);
        var embeddingClient = client.GetEmbeddingClient(_model);

        var response = await embeddingClient.GenerateEmbeddingsAsync(nonEmptyTexts);

        // Build results array with nulls for empty texts
        results = new List<float[]?>(new float[]?[preparedTexts.Count]);
        for (int i = 0; i < nonEmptyIndices.Count; i++)
        {
            results[nonEmptyIndices[i]] = response.Value[i].ToFloats().ToArray();
        }

        return results;
    }
}
