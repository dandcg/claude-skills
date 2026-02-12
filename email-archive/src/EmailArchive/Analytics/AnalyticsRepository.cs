using Npgsql;

namespace EmailArchive.Analytics;

public class AnalyticsRepository
{
    private readonly string _connectionString;

    public AnalyticsRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<TimelinePeriod>> GetTimelineAsync(bool groupByMonth = false)
    {
        await using var conn = await CreateConnectionAsync();

        var sql = groupByMonth
            ? """
                SELECT
                    EXTRACT(YEAR FROM date)::int AS year,
                    EXTRACT(MONTH FROM date)::int AS month,
                    COUNT(*) AS total,
                    COUNT(*) FILTER (WHERE is_sent = true) AS sent,
                    COUNT(*) FILTER (WHERE is_sent = false) AS received
                FROM emails
                WHERE tier IN (2, 3)
                GROUP BY year, month
                ORDER BY year, month
                """
            : """
                SELECT
                    EXTRACT(YEAR FROM date)::int AS year,
                    NULL::int AS month,
                    COUNT(*) AS total,
                    COUNT(*) FILTER (WHERE is_sent = true) AS sent,
                    COUNT(*) FILTER (WHERE is_sent = false) AS received
                FROM emails
                WHERE tier IN (2, 3)
                GROUP BY year
                ORDER BY year
                """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        var results = new List<TimelinePeriod>();

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new TimelinePeriod
            {
                Year = reader.GetInt32(0),
                Month = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                EmailCount = reader.GetInt32(2),
                SentCount = reader.GetInt32(3),
                ReceivedCount = reader.GetInt32(4)
            });
        }

        return results;
    }

    public async Task<List<ContactStats>> GetTopContactsAsync(int limit = 20)
    {
        await using var conn = await CreateConnectionAsync();

        var sql = """
            WITH contact_emails AS (
                SELECT
                    sender AS email,
                    sender_name AS name,
                    date,
                    CASE WHEN is_sent THEN 1 ELSE 0 END AS sent_count,
                    CASE WHEN NOT is_sent THEN 1 ELSE 0 END AS received_count
                FROM emails
                WHERE tier IN (2, 3)
            )
            SELECT
                email,
                MAX(name) AS name,
                COUNT(*) AS total,
                SUM(sent_count) AS sent,
                SUM(received_count) AS received,
                MIN(date) AS first_contact,
                MAX(date) AS last_contact
            FROM contact_emails
            GROUP BY email
            ORDER BY total DESC
            LIMIT @limit
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("limit", limit);

        var results = new List<ContactStats>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new ContactStats
            {
                Email = reader.GetString(0),
                Name = reader.GetString(1),
                TotalEmails = reader.GetInt32(2),
                SentTo = reader.GetInt32(3),
                ReceivedFrom = reader.GetInt32(4),
                FirstContact = reader.GetDateTime(5),
                LastContact = reader.GetDateTime(6)
            });
        }

        return results;
    }

    public async Task<List<ActivityStats>> GetActivityByHourAsync()
    {
        await using var conn = await CreateConnectionAsync();

        var sql = """
            SELECT
                EXTRACT(HOUR FROM date)::int AS hour,
                0 AS day_of_week,
                COUNT(*) AS count
            FROM emails
            WHERE tier IN (2, 3)
            GROUP BY hour
            ORDER BY hour
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        var results = new List<ActivityStats>();

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new ActivityStats
            {
                Hour = reader.GetInt32(0),
                DayOfWeek = reader.GetInt32(1),
                EmailCount = reader.GetInt32(2)
            });
        }

        return results;
    }

    public async Task<List<ActivityStats>> GetActivityByDayOfWeekAsync()
    {
        await using var conn = await CreateConnectionAsync();

        var sql = """
            SELECT
                0 AS hour,
                EXTRACT(DOW FROM date)::int AS day_of_week,
                COUNT(*) AS count
            FROM emails
            WHERE tier IN (2, 3)
            GROUP BY day_of_week
            ORDER BY day_of_week
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        var results = new List<ActivityStats>();

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new ActivityStats
            {
                Hour = reader.GetInt32(0),
                DayOfWeek = reader.GetInt32(1),
                EmailCount = reader.GetInt32(2)
            });
        }

        return results;
    }

    public async Task<ArchiveSummary> GetArchiveSummaryAsync()
    {
        await using var conn = await CreateConnectionAsync();

        var sql = """
            SELECT
                COUNT(*) AS total_emails,
                COUNT(DISTINCT sender) AS unique_contacts,
                MIN(date) AS earliest,
                MAX(date) AS latest
            FROM emails
            WHERE tier IN (2, 3)
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();

        var totalEmails = reader.GetInt32(0);
        var uniqueContacts = reader.GetInt32(1);
        var earliest = reader.IsDBNull(2) ? DateTime.UtcNow : reader.GetDateTime(2);
        var latest = reader.IsDBNull(3) ? DateTime.UtcNow : reader.GetDateTime(3);
        var daysSpan = Math.Max(1, (latest - earliest).Days);

        return new ArchiveSummary
        {
            TotalEmails = totalEmails,
            UniqueContacts = uniqueContacts,
            EarliestEmail = earliest,
            LatestEmail = latest,
            TotalYearsSpan = latest.Year - earliest.Year + 1,
            AvgEmailsPerDay = Math.Round((double)totalEmails / daysSpan, 2)
        };
    }

    private async Task<NpgsqlConnection> CreateConnectionAsync()
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }
}
