using Npgsql;
using Pgvector;

namespace EmailArchive.Search;

public class SearchRepository
{
    private readonly string _connectionString;

    public SearchRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<EmailSearchResult>> SearchEmailsAsync(
        float[] queryEmbedding,
        int limit = 10,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? senderFilter = null)
    {
        await using var conn = await CreateConnectionAsync();

        var conditions = new List<string> { "embedding IS NOT NULL" };
        var parameters = new List<NpgsqlParameter>
        {
            new("embedding", new Vector(queryEmbedding)),
            new("limit", limit)
        };

        if (startDate.HasValue)
        {
            conditions.Add("date >= @startDate");
            parameters.Add(new("startDate", startDate.Value));
        }

        if (endDate.HasValue)
        {
            conditions.Add("date <= @endDate");
            parameters.Add(new("endDate", endDate.Value));
        }

        if (!string.IsNullOrEmpty(senderFilter))
        {
            conditions.Add("(sender ILIKE @senderFilter OR sender_name ILIKE @senderFilter)");
            parameters.Add(new("senderFilter", $"%{senderFilter}%"));
        }

        var whereClause = string.Join(" AND ", conditions);

        var sql = $"""
            SELECT id, date, sender, sender_name, subject, body_text, has_attachments,
                   1 - (embedding <=> @embedding) as similarity
            FROM emails
            WHERE {whereClause}
            ORDER BY embedding <=> @embedding
            LIMIT @limit
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        foreach (var param in parameters)
        {
            cmd.Parameters.Add(param);
        }

        var results = new List<EmailSearchResult>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new EmailSearchResult
            {
                Id = reader.GetGuid(0),
                Date = reader.GetDateTime(1),
                Sender = reader.GetString(2),
                SenderName = reader.GetString(3),
                Subject = reader.GetString(4),
                BodySnippet = CreateSnippet(reader.GetString(5), 200),
                HasAttachments = reader.GetBoolean(6),
                Similarity = reader.GetDouble(7)
            });
        }

        return results;
    }

    public async Task<List<AttachmentSearchResult>> SearchAttachmentsAsync(
        float[] queryEmbedding,
        int limit = 10)
    {
        await using var conn = await CreateConnectionAsync();

        var sql = """
            SELECT a.id, a.email_id, a.filename, a.extracted_text,
                   1 - (a.embedding <=> @embedding) as similarity,
                   e.date, e.sender, e.subject
            FROM attachments a
            JOIN emails e ON a.email_id = e.id
            WHERE a.embedding IS NOT NULL
            ORDER BY a.embedding <=> @embedding
            LIMIT @limit
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("embedding", new Vector(queryEmbedding));
        cmd.Parameters.AddWithValue("limit", limit);

        var results = new List<AttachmentSearchResult>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new AttachmentSearchResult
            {
                Id = reader.GetGuid(0),
                EmailId = reader.GetGuid(1),
                Filename = reader.GetString(2),
                TextSnippet = CreateSnippet(reader.IsDBNull(3) ? "" : reader.GetString(3), 200),
                Similarity = reader.GetDouble(4),
                EmailDate = reader.GetDateTime(5),
                EmailSender = reader.GetString(6),
                EmailSubject = reader.GetString(7)
            });
        }

        return results;
    }

    public static string CreateSnippet(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Normalize whitespace
        text = string.Join(" ", text.Split(default(char[]), StringSplitOptions.RemoveEmptyEntries));

        if (text.Length <= maxLength)
            return text;

        return text[..maxLength] + "...";
    }

    private async Task<NpgsqlConnection> CreateConnectionAsync()
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(_connectionString);
        dataSourceBuilder.UseVector();
        await using var dataSource = dataSourceBuilder.Build();
        var conn = await dataSource.OpenConnectionAsync();
        return conn;
    }
}
