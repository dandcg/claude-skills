using EmailArchive.Models;
using Npgsql;
using Pgvector;

namespace EmailArchive.Storage;

public class AttachmentRepository
{
    private readonly string _connectionString;

    public AttachmentRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task InsertAsync(Attachment attachment)
    {
        await using var conn = await CreateConnectionAsync();

        var sql = """
            INSERT INTO attachments (
                id, email_id, filename, mime_type, size_bytes,
                extracted_text, embedding, embedded_at, created_at
            ) VALUES (
                @id, @emailId, @filename, @mimeType, @sizeBytes,
                @extractedText, @embedding, @embeddedAt, @createdAt
            )
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", attachment.Id);
        cmd.Parameters.AddWithValue("emailId", attachment.EmailId);
        cmd.Parameters.AddWithValue("filename", attachment.Filename);
        cmd.Parameters.AddWithValue("mimeType", attachment.MimeType);
        cmd.Parameters.AddWithValue("sizeBytes", attachment.SizeBytes);
        cmd.Parameters.AddWithValue("extractedText", (object?)attachment.ExtractedText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("embedding", attachment.Embedding is not null ? new Vector(attachment.Embedding) : DBNull.Value);
        cmd.Parameters.AddWithValue("embeddedAt", (object?)attachment.EmbeddedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("createdAt", attachment.CreatedAt);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<Attachment?> GetByIdAsync(Guid id)
    {
        await using var conn = await CreateConnectionAsync();

        var sql = "SELECT * FROM attachments WHERE id = @id";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return MapAttachment(reader);
    }

    public async Task<List<Attachment>> GetByEmailIdAsync(Guid emailId)
    {
        await using var conn = await CreateConnectionAsync();

        var sql = "SELECT * FROM attachments WHERE email_id = @emailId";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("emailId", emailId);

        var attachments = new List<Attachment>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            attachments.Add(MapAttachment(reader));
        }

        return attachments;
    }

    public async Task<int> GetCountAsync()
    {
        await using var conn = await CreateConnectionAsync();
        await using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM attachments", conn);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<int> GetWithTextCountAsync()
    {
        await using var conn = await CreateConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM attachments WHERE extracted_text IS NOT NULL", conn);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<List<Attachment>> GetUnembeddedAttachmentsAsync(int limit)
    {
        await using var conn = await CreateConnectionAsync();

        var sql = """
            SELECT * FROM attachments
            WHERE extracted_text IS NOT NULL AND embedded_at IS NULL
            ORDER BY created_at DESC
            LIMIT @limit
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("limit", limit);

        var attachments = new List<Attachment>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            attachments.Add(MapAttachment(reader));
        }

        return attachments;
    }

    public async Task UpdateEmbeddingAsync(Guid id, float[] embedding)
    {
        await using var conn = await CreateConnectionAsync();

        var sql = """
            UPDATE attachments
            SET embedding = @embedding, embedded_at = @embeddedAt
            WHERE id = @id
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("embedding", new Vector(embedding));
        cmd.Parameters.AddWithValue("embeddedAt", DateTime.UtcNow);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> GetEmbeddedCountAsync()
    {
        await using var conn = await CreateConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM attachments WHERE embedded_at IS NOT NULL", conn);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private async Task<NpgsqlConnection> CreateConnectionAsync()
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(_connectionString);
        dataSourceBuilder.UseVector();
        await using var dataSource = dataSourceBuilder.Build();
        var conn = await dataSource.OpenConnectionAsync();
        return conn;
    }

    private static Attachment MapAttachment(NpgsqlDataReader reader)
    {
        return new Attachment
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            EmailId = reader.GetGuid(reader.GetOrdinal("email_id")),
            Filename = reader.GetString(reader.GetOrdinal("filename")),
            MimeType = reader.GetString(reader.GetOrdinal("mime_type")),
            SizeBytes = reader.GetInt32(reader.GetOrdinal("size_bytes")),
            ExtractedText = reader.IsDBNull(reader.GetOrdinal("extracted_text")) ? null : reader.GetString(reader.GetOrdinal("extracted_text")),
            EmbeddedAt = reader.IsDBNull(reader.GetOrdinal("embedded_at")) ? null : reader.GetDateTime(reader.GetOrdinal("embedded_at")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
        };
    }
}
