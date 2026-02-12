using EmailArchive.Models;
using PSTParse;
using PSTParse.MessageLayer;

namespace EmailArchive.Ingest;

/// <summary>
/// Data transfer object for PST message parsing.
/// Used for both real PST parsing and testing.
/// </summary>
public record PstMessageData
{
    public string? Subject { get; init; }
    public string? SenderEmailAddress { get; init; }
    public string? SenderName { get; init; }
    public string? Body { get; init; }
    public DateTime? ClientSubmitTime { get; init; }
    public List<string> Recipients { get; init; } = new();
    public string? MessageId { get; init; }
    public bool HasAttachments { get; init; }
    public List<PstAttachmentData> Attachments { get; init; } = new();
}

/// <summary>
/// Data transfer object for PST attachment parsing.
/// </summary>
public record PstAttachmentData
{
    public string Filename { get; init; } = string.Empty;
    public string? MimeType { get; init; }
    public byte[] Content { get; init; } = Array.Empty<byte>();
    public int SizeBytes { get; init; }
}

/// <summary>
/// Tuple record containing both the Email model and raw PST data.
/// Used when attachment data is needed during processing.
/// </summary>
public record ParsedEmail(Email Email, PstMessageData Data);

public class PstParser
{
    /// <summary>
    /// Parse a PST file and yield emails.
    /// </summary>
    public IEnumerable<Email> ParseFile(string pstPath)
    {
        using var pstFile = new PSTFile(pstPath);

        foreach (var folder in GetAllFolders(pstFile.TopOfPST))
        {
            foreach (var item in folder.GetIpmItems())
            {
                if (item is not Message message)
                {
                    continue;
                }

                PstMessageData? data = null;
                try
                {
                    data = ExtractMessageData(message);
                }
                catch
                {
                    // Skip problematic messages
                    continue;
                }

                if (data is not null)
                {
                    yield return MapToEmail(data);
                }
            }
        }
    }

    /// <summary>
    /// Parse a PST file and yield both the Email model and raw PST data.
    /// Use this method when you need access to attachment content.
    /// </summary>
    public IEnumerable<ParsedEmail> ParseFileWithData(string pstPath)
    {
        using var pstFile = new PSTFile(pstPath);

        foreach (var folder in GetAllFolders(pstFile.TopOfPST))
        {
            foreach (var item in folder.GetIpmItems())
            {
                if (item is not Message message)
                {
                    continue;
                }

                PstMessageData? data = null;
                try
                {
                    data = ExtractMessageData(message);
                }
                catch
                {
                    // Skip problematic messages
                    continue;
                }

                if (data is not null)
                {
                    yield return new ParsedEmail(MapToEmail(data), data);
                }
            }
        }
    }

    /// <summary>
    /// Get parsed attachment data for an email.
    /// </summary>
    public List<PstAttachmentData> GetAttachments(PstMessageData data)
    {
        return data.Attachments;
    }

    /// <summary>
    /// Map message data to an Email model.
    /// </summary>
    public Email MapToEmail(PstMessageData data)
    {
        var messageId = data.MessageId;
        if (string.IsNullOrEmpty(messageId))
        {
            messageId = $"<pst-{Guid.NewGuid()}@local>";
        }

        return new Email
        {
            MessageId = messageId,
            Date = data.ClientSubmitTime ?? DateTime.UtcNow,
            Sender = data.SenderEmailAddress ?? string.Empty,
            SenderName = data.SenderName ?? string.Empty,
            Recipients = data.Recipients,
            Subject = data.Subject ?? string.Empty,
            BodyText = data.Body ?? string.Empty,
            HasAttachments = data.HasAttachments
        };
    }

    private PstMessageData ExtractMessageData(Message message)
    {
        var recipients = new List<string>();

        // Extract recipient email addresses from Recipients property
        // The Recipients property may have different structures across PSTParse versions
        try
        {
            // Try to access To property via dynamic to handle API differences
            dynamic dynamicMessage = message;
            var toRecipients = dynamicMessage.Recipients?.To as IEnumerable<dynamic>;
            if (toRecipients != null)
            {
                foreach (var recipient in toRecipients)
                {
                    string? email = recipient.EmailAddress ?? recipient.DisplayName;
                    if (!string.IsNullOrEmpty(email))
                    {
                        recipients.Add(email);
                    }
                }
            }
        }
        catch
        {
            // Recipients extraction failed - continue without recipients
            // This is acceptable as recipient data may not always be available
        }

        // Get sender email - prefer SMTP address, fall back to SenderAddress
        var senderEmail = message.SenderSMTPAddress ?? message.SenderAddress;

        // Extract attachments
        var attachments = new List<PstAttachmentData>();
        try
        {
            if (message.HasAttachments && message.Attachments != null)
            {
                foreach (var attachment in message.Attachments)
                {
                    try
                    {
                        var filename = attachment.AttachmentLongFileName ?? attachment.Filename ?? "unknown";
                        var content = attachment.Data ?? Array.Empty<byte>();
                        var sizeBytes = (int)attachment.Size;

                        attachments.Add(new PstAttachmentData
                        {
                            Filename = filename,
                            MimeType = null, // PSTParse doesn't expose MIME type
                            Content = content,
                            SizeBytes = sizeBytes
                        });
                    }
                    catch
                    {
                        // Skip problematic attachments
                    }
                }
            }
        }
        catch
        {
            // Attachment extraction failed - continue with empty list
        }

        return new PstMessageData
        {
            Subject = message.Subject,
            SenderEmailAddress = senderEmail,
            SenderName = message.SenderName,
            Body = message.BodyPlainText ?? message.BodyHtml,
            ClientSubmitTime = message.ClientSubmitTime,
            Recipients = recipients,
            MessageId = message.InternetMessageID,
            HasAttachments = message.HasAttachments,
            Attachments = attachments
        };
    }

    private IEnumerable<MailFolder> GetAllFolders(MailFolder root)
    {
        yield return root;

        foreach (var subFolder in root.SubFolders)
        {
            foreach (var folder in GetAllFolders(subFolder))
            {
                yield return folder;
            }
        }
    }
}
