// src/EmailArchive/Export/MarkdownFormatter.cs
using System.Text;

namespace EmailArchive.Export;

/// <summary>
/// Static utility class for formatting export data as markdown.
/// </summary>
public static class MarkdownFormatter
{
    /// <summary>
    /// Formats a contact export as a markdown section.
    /// </summary>
    /// <param name="contact">The contact to format.</param>
    /// <returns>Markdown formatted contact section.</returns>
    public static string FormatContactSection(ContactExport contact)
    {
        var sb = new StringBuilder();

        // Use name as header, or email if name is empty
        var header = string.IsNullOrWhiteSpace(contact.Name) ? contact.Email : contact.Name;
        sb.AppendLine($"### {header}");
        sb.AppendLine();

        // Email (only if we used name as header)
        if (!string.IsNullOrWhiteSpace(contact.Name))
        {
            sb.AppendLine($"**Email:** {contact.Email}");
        }

        // Communication stats
        sb.AppendLine($"**Communication:** {contact.TotalEmails} total emails ({contact.SentTo} sent, {contact.ReceivedFrom} received)");
        sb.AppendLine($"**Direction:** {contact.CommunicationDirection}");
        sb.AppendLine($"**First Contact:** {contact.FirstContact:yyyy-MM-dd}");
        sb.AppendLine($"**Last Contact:** {contact.LastContact:yyyy-MM-dd}");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Formats a review period export as a markdown email activity section.
    /// </summary>
    /// <param name="review">The review period data to format.</param>
    /// <returns>Markdown formatted email activity section.</returns>
    public static string FormatReviewEmailSection(ReviewPeriodExport review)
    {
        var sb = new StringBuilder();

        sb.AppendLine("## Email Activity");
        sb.AppendLine();
        sb.AppendLine($"**Period:** {review.PeriodStart:yyyy-MM-dd} to {review.PeriodEnd:yyyy-MM-dd}");
        sb.AppendLine();

        // Summary stats
        sb.AppendLine("### Summary");
        sb.AppendLine($"- **Total Emails:** {review.EmailCount}");
        sb.AppendLine($"- **Sent:** {review.SentCount}");
        sb.AppendLine($"- **Received:** {review.ReceivedCount}");
        sb.AppendLine($"- **Peak Activity:** {review.PeakActivityDay} at {review.PeakActivityHour}:00");
        sb.AppendLine();

        // Top contacts
        if (review.TopContacts.Count > 0)
        {
            sb.AppendLine("### Top Contacts");
            foreach (var contact in review.TopContacts)
            {
                var name = string.IsNullOrWhiteSpace(contact.Name) ? contact.Email : contact.Name;
                sb.AppendLine($"- **{name}** ({contact.Email}): {contact.TotalEmails} emails");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats a header for ideas/thoughts files in the areas/ directory.
    /// </summary>
    /// <param name="title">The title of the idea.</param>
    /// <param name="date">The date the idea was added.</param>
    /// <param name="status">The status (seed, developing, mature, archived).</param>
    /// <returns>Markdown formatted header section.</returns>
    public static string FormatIdeasHeader(string title, DateTime date, string status)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# {title}");
        sb.AppendLine();
        sb.AppendLine($"**Added:** {date:yyyy-MM-dd}");
        sb.AppendLine($"**Status:** {status}");
        sb.AppendLine("**Related:** ");
        sb.AppendLine();

        return sb.ToString();
    }
}
