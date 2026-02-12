using Npgsql;

namespace EmailArchive.Storage;

public class DatabaseInitializer
{
    private readonly string _connectionString;

    public DatabaseInitializer(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task InitializeAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // Enable pgvector
        await using (var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector", conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        // Create emails table
        var createEmailsSql = """
            CREATE TABLE IF NOT EXISTS emails (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                message_id TEXT,
                thread_id TEXT,
                date TIMESTAMPTZ,
                sender TEXT,
                sender_name TEXT,
                recipients JSONB,
                subject TEXT,
                body_text TEXT,
                body_word_count INTEGER,
                is_sent BOOLEAN DEFAULT FALSE,
                has_attachments BOOLEAN DEFAULT FALSE,
                tier INTEGER,
                embedding vector(1536),
                embedded_at TIMESTAMPTZ,
                created_at TIMESTAMPTZ DEFAULT NOW()
            )
            """;

        await using (var cmd = new NpgsqlCommand(createEmailsSql, conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        // Create attachments table
        var createAttachmentsSql = """
            CREATE TABLE IF NOT EXISTS attachments (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                email_id UUID REFERENCES emails(id) ON DELETE CASCADE,
                filename TEXT,
                mime_type TEXT,
                size_bytes INTEGER,
                extracted_text TEXT,
                embedding vector(1536),
                embedded_at TIMESTAMPTZ,
                created_at TIMESTAMPTZ DEFAULT NOW()
            )
            """;

        await using (var cmd = new NpgsqlCommand(createAttachmentsSql, conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        // Create indexes
        var createIndexesSql = """
            CREATE INDEX IF NOT EXISTS idx_emails_date ON emails(date);
            CREATE INDEX IF NOT EXISTS idx_emails_sender ON emails(sender);
            CREATE INDEX IF NOT EXISTS idx_emails_tier ON emails(tier);
            CREATE INDEX IF NOT EXISTS idx_attachments_email ON attachments(email_id);
            """;

        await using (var cmd = new NpgsqlCommand(createIndexesSql, conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        // Create vector indexes for similarity search
        var createVectorIndexesSql = """
            CREATE INDEX IF NOT EXISTS idx_emails_embedding ON emails
            USING hnsw (embedding vector_cosine_ops);
            CREATE INDEX IF NOT EXISTS idx_attachments_embedding ON attachments
            USING hnsw (embedding vector_cosine_ops);
            """;

        await using (var cmd = new NpgsqlCommand(createVectorIndexesSql, conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<bool> TableExistsAsync(string tableName)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = """
            SELECT EXISTS (
                SELECT FROM information_schema.tables
                WHERE table_schema = 'public'
                AND table_name = @tableName
            )
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tableName", tableName);

        var result = await cmd.ExecuteScalarAsync();
        return result is true;
    }
}
