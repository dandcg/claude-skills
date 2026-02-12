using System.Text.Json;
using EmailArchive.Models;
using Npgsql;
using Pgvector;

namespace EmailArchive.Storage;

public record StatusCounts(int Total, int Excluded, int MetadataOnly, int Vectorize, int Embedded);

public class EmailRepository
{
    private readonly string _connectionString;

    public EmailRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task InsertAsync(Email email)
    {
        await using var conn = await CreateConnectionAsync();

        var sql = """
            INSERT INTO emails (
                id, message_id, thread_id, date, sender, sender_name,
                recipients, subject, body_text, body_word_count,
                is_sent, has_attachments, tier, embedding, embedded_at, created_at
            ) VALUES (
                @id, @messageId, @threadId, @date, @sender, @senderName,
                @recipients::jsonb, @subject, @bodyText, @bodyWordCount,
                @isSent, @hasAttachments, @tier, @embedding, @embeddedAt, @createdAt
            )
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", email.Id);
        cmd.Parameters.AddWithValue("messageId", email.MessageId);
        cmd.Parameters.AddWithValue("threadId", (object?)email.ThreadId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("date", email.Date);
        cmd.Parameters.AddWithValue("sender", email.Sender);
        cmd.Parameters.AddWithValue("senderName", email.SenderName);
        cmd.Parameters.AddWithValue("recipients", JsonSerializer.Serialize(email.Recipients));
        cmd.Parameters.AddWithValue("subject", email.Subject);
        cmd.Parameters.AddWithValue("bodyText", email.BodyText);
        cmd.Parameters.AddWithValue("bodyWordCount", email.BodyWordCount);
        cmd.Parameters.AddWithValue("isSent", email.IsSent);
        cmd.Parameters.AddWithValue("hasAttachments", email.HasAttachments);
        cmd.Parameters.AddWithValue("tier", (int)email.Tier);
        cmd.Parameters.AddWithValue("embedding", email.Embedding is not null ? new Vector(email.Embedding) : DBNull.Value);
        cmd.Parameters.AddWithValue("embeddedAt", (object?)email.EmbeddedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("createdAt", email.CreatedAt);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<Email?> GetByIdAsync(Guid id)
    {
        await using var conn = await CreateConnectionAsync();

        var sql = "SELECT * FROM emails WHERE id = @id";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return MapEmail(reader);
    }

    public async Task<List<Email>> GetByTierAsync(Tier tier)
    {
        await using var conn = await CreateConnectionAsync();

        var sql = "SELECT * FROM emails WHERE tier = @tier";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tier", (int)tier);

        var emails = new List<Email>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            emails.Add(MapEmail(reader));
        }

        return emails;
    }

    public async Task<StatusCounts> GetStatusCountsAsync()
    {
        await using var conn = await CreateConnectionAsync();

        var sql = """
            SELECT
                COUNT(*) AS total,
                COUNT(*) FILTER (WHERE tier = 1) AS excluded,
                COUNT(*) FILTER (WHERE tier = 2) AS metadata_only,
                COUNT(*) FILTER (WHERE tier = 3) AS vectorize,
                COUNT(*) FILTER (WHERE embedded_at IS NOT NULL) AS embedded
            FROM emails
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();

        return new StatusCounts(
            Total: reader.GetInt32(0),
            Excluded: reader.GetInt32(1),
            MetadataOnly: reader.GetInt32(2),
            Vectorize: reader.GetInt32(3),
            Embedded: reader.GetInt32(4)
        );
    }

    public async Task TruncateAsync()
    {
        await using var conn = await CreateConnectionAsync();
        await using var cmd = new NpgsqlCommand("TRUNCATE emails CASCADE", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Email>> GetUnembeddedEmailsAsync(int limit)
    {
        await using var conn = await CreateConnectionAsync();

        var sql = """
            SELECT * FROM emails
            WHERE tier = 3 AND embedded_at IS NULL
            ORDER BY date DESC
            LIMIT @limit
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("limit", limit);

        var emails = new List<Email>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            emails.Add(MapEmail(reader));
        }

        return emails;
    }

    public async Task UpdateEmbeddingAsync(Guid id, float[] embedding)
    {
        await using var conn = await CreateConnectionAsync();

        var sql = """
            UPDATE emails
            SET embedding = @embedding, embedded_at = @embeddedAt
            WHERE id = @id
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("embedding", new Vector(embedding));
        cmd.Parameters.AddWithValue("embeddedAt", DateTime.UtcNow);

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<NpgsqlConnection> CreateConnectionAsync()
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(_connectionString);
        dataSourceBuilder.UseVector();
        await using var dataSource = dataSourceBuilder.Build();
        var conn = await dataSource.OpenConnectionAsync();
        return conn;
    }

    private static Email MapEmail(NpgsqlDataReader reader)
    {
        var recipientsJson = reader.GetString(reader.GetOrdinal("recipients"));
        var recipients = JsonSerializer.Deserialize<List<string>>(recipientsJson) ?? new List<string>();

        return new Email
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            MessageId = reader.GetString(reader.GetOrdinal("message_id")),
            ThreadId = reader.IsDBNull(reader.GetOrdinal("thread_id")) ? null : reader.GetString(reader.GetOrdinal("thread_id")),
            Date = reader.GetDateTime(reader.GetOrdinal("date")),
            Sender = reader.GetString(reader.GetOrdinal("sender")),
            SenderName = reader.GetString(reader.GetOrdinal("sender_name")),
            Recipients = recipients,
            Subject = reader.GetString(reader.GetOrdinal("subject")),
            BodyText = reader.GetString(reader.GetOrdinal("body_text")),
            IsSent = reader.GetBoolean(reader.GetOrdinal("is_sent")),
            HasAttachments = reader.GetBoolean(reader.GetOrdinal("has_attachments")),
            Tier = (Tier)reader.GetInt32(reader.GetOrdinal("tier")),
            EmbeddedAt = reader.IsDBNull(reader.GetOrdinal("embedded_at")) ? null : reader.GetDateTime(reader.GetOrdinal("embedded_at")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
        };
    }
}
