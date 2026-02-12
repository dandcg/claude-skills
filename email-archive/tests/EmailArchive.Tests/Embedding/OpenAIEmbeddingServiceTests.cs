using EmailArchive.Embedding;
using Xunit;

namespace EmailArchive.Tests.Embedding;

public class OpenAIEmbeddingServiceTests
{
    [Fact]
    public void PrepareTextForEmbedding_TruncatesLongText()
    {
        var service = new OpenAIEmbeddingService("fake-key");
        var longText = new string('a', 50000); // Very long text

        var result = service.PrepareTextForEmbedding(longText);

        // Should truncate to ~8000 tokens worth (approx 32000 chars)
        Assert.True(result.Length <= 32000);
    }

    [Fact]
    public void PrepareTextForEmbedding_TrimsWhitespace()
    {
        var service = new OpenAIEmbeddingService("fake-key");
        var text = "  Hello World  \n\n  ";

        var result = service.PrepareTextForEmbedding(text);

        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void PrepareTextForEmbedding_NormalizesNewlines()
    {
        var service = new OpenAIEmbeddingService("fake-key");
        var text = "Line1\r\n\r\n\r\nLine2\n\n\n\nLine3";

        var result = service.PrepareTextForEmbedding(text);

        Assert.Equal("Line1\n\nLine2\n\nLine3", result);
    }

    [Fact]
    public void CreateEmailEmbeddingText_CombinesFieldsCorrectly()
    {
        var service = new OpenAIEmbeddingService("fake-key");

        var result = service.CreateEmailEmbeddingText(
            subject: "Project Update",
            sender: "alice@example.com",
            body: "Here is the latest status."
        );

        Assert.Contains("Subject: Project Update", result);
        Assert.Contains("From: alice@example.com", result);
        Assert.Contains("Here is the latest status.", result);
    }

    [Fact]
    public async Task GetEmbeddingAsync_ReturnsNullForEmptyText()
    {
        var service = new OpenAIEmbeddingService("fake-key");

        var result = await service.GetEmbeddingAsync("");

        Assert.Null(result);
    }
}
