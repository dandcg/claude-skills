using Npgsql;

namespace EmailArchive.Export;

public class ExportRepository
{
    private readonly string _connectionString;

    public ExportRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Gets contacts with communication statistics for the specified date range.
    /// </summary>
    public async Task<List<ContactExport>> GetContactsForPeriodAsync(DateTime start, DateTime end, int limit)
    {
        await using var conn = await CreateConnectionAsync();

        var sql = """
            WITH contact_emails AS (
                SELECT
                    CASE WHEN is_sent THEN recipients[1] ELSE sender END AS contact_email,
                    CASE WHEN is_sent THEN '' ELSE sender_name END AS contact_name,
                    is_sent,
                    date
                FROM emails
                WHERE date >= @start AND date <= @end
            )
            SELECT
                contact_email,
                MAX(contact_name) AS contact_name,
                COUNT(*) AS total_emails,
                COUNT(*) FILTER (WHERE is_sent) AS sent_to,
                COUNT(*) FILTER (WHERE NOT is_sent) AS received_from,
                MIN(date) AS first_contact,
                MAX(date) AS last_contact
            FROM contact_emails
            GROUP BY contact_email
            ORDER BY total_emails DESC
            LIMIT @limit
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("start", start);
        cmd.Parameters.AddWithValue("end", end);
        cmd.Parameters.AddWithValue("limit", limit);

        var contacts = new List<ContactExport>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var sentTo = reader.GetInt32(reader.GetOrdinal("sent_to"));
            var receivedFrom = reader.GetInt32(reader.GetOrdinal("received_from"));

            var direction = (sentTo > 0, receivedFrom > 0) switch
            {
                (true, true) => "bidirectional",
                (true, false) => "outbound",
                (false, true) => "inbound",
                _ => "unknown"
            };

            contacts.Add(new ContactExport
            {
                Email = reader.GetString(reader.GetOrdinal("contact_email")),
                Name = reader.GetString(reader.GetOrdinal("contact_name")),
                TotalEmails = reader.GetInt32(reader.GetOrdinal("total_emails")),
                SentTo = sentTo,
                ReceivedFrom = receivedFrom,
                FirstContact = reader.GetDateTime(reader.GetOrdinal("first_contact")),
                LastContact = reader.GetDateTime(reader.GetOrdinal("last_contact")),
                CommunicationDirection = direction
            });
        }

        return contacts;
    }

    /// <summary>
    /// Gets review data for the specified period including email counts, peak activity, and top contacts.
    /// </summary>
    public async Task<ReviewPeriodExport> GetReviewDataAsync(DateTime start, DateTime end, int topContactsLimit)
    {
        await using var conn = await CreateConnectionAsync();

        // Get email counts and peak activity
        var countsSql = """
            SELECT
                COUNT(*) AS email_count,
                COUNT(*) FILTER (WHERE is_sent) AS sent_count,
                COUNT(*) FILTER (WHERE NOT is_sent) AS received_count
            FROM emails
            WHERE date >= @start AND date <= @end
            """;

        await using var countsCmd = new NpgsqlCommand(countsSql, conn);
        countsCmd.Parameters.AddWithValue("start", start);
        countsCmd.Parameters.AddWithValue("end", end);

        int emailCount = 0, sentCount = 0, receivedCount = 0;
        await using (var reader = await countsCmd.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                emailCount = reader.GetInt32(0);
                sentCount = reader.GetInt32(1);
                receivedCount = reader.GetInt32(2);
            }
        }

        // Get peak activity (day of week and hour) in a single query
        var peakActivitySql = """
            SELECT
                (SELECT EXTRACT(DOW FROM date)::int FROM emails
                 WHERE date >= @start AND date <= @end
                 GROUP BY EXTRACT(DOW FROM date)
                 ORDER BY COUNT(*) DESC LIMIT 1) AS peak_dow,
                (SELECT EXTRACT(HOUR FROM date)::int FROM emails
                 WHERE date >= @start AND date <= @end
                 GROUP BY EXTRACT(HOUR FROM date)
                 ORDER BY COUNT(*) DESC LIMIT 1) AS peak_hour
            """;

        await using var peakCmd = new NpgsqlCommand(peakActivitySql, conn);
        peakCmd.Parameters.AddWithValue("start", start);
        peakCmd.Parameters.AddWithValue("end", end);

        string peakDay = "";
        int peakHour = 0;
        string[] dayNames = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

        await using (var reader = await peakCmd.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0))
                {
                    var dow = reader.GetInt32(0);
                    peakDay = dow >= 0 && dow < 7 ? dayNames[dow] : "Unknown";
                }
                if (!reader.IsDBNull(1))
                {
                    peakHour = reader.GetInt32(1);
                }
            }
        }

        // Get top contacts
        var topContacts = await GetContactsForPeriodAsync(start, end, topContactsLimit);

        return new ReviewPeriodExport
        {
            PeriodStart = start,
            PeriodEnd = end,
            EmailCount = emailCount,
            SentCount = sentCount,
            ReceivedCount = receivedCount,
            TopContacts = topContacts,
            PeakActivityDay = peakDay,
            PeakActivityHour = peakHour
        };
    }

    private async Task<NpgsqlConnection> CreateConnectionAsync()
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }
}
