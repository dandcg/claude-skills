using System.Text.RegularExpressions;
using EmailArchive.Models;

namespace EmailArchive.Ingest;

public class EmailFilter
{
    private const int MinWordsForVectorization = 30;

    private static readonly string[] Tier1SubjectPatterns =
    [
        @"password reset",
        @"reset your password",
        @"verification code",
        @"verify your email",
        @"confirm your email",
        @"unsubscribe",
        @"has been delivered",
        @"out for delivery",
        @"has shipped",
        @"delivery notification",
        @"delivery confirmation",
        @"accepted:\s",
        @"declined:\s",
        @"tentative:\s",
        @"canceled:\s"
    ];

    private static readonly string[] Tier1BodyPatterns =
    [
        @"click here to reset your password",
        @"your verification code is",
        @"your package (has been |was )?(delivered|shipped)",
        @"you have successfully unsubscribed",
        @"delivery failure",
        @"mail delivery (failed|subsystem)",
        @"mailer-daemon"
    ];

    private static readonly string[] AutomatedSenderPatterns =
    [
        @"^noreply@",
        @"^no-reply@",
        @"^notifications?@",
        @"^alerts?@",
        @"^mailer-daemon@",
        @"^postmaster@",
        @"^bounce"
    ];

    private static readonly HashSet<string> OneWordReplies = new(StringComparer.OrdinalIgnoreCase)
    {
        "thanks", "thank you", "thanks!", "thank you!",
        "ok", "okay", "ok!", "okay!",
        "got it", "got it!",
        "sounds good", "sounds good!",
        "great", "great!",
        "perfect", "perfect!",
        "sure", "sure!",
        "yes", "no", "yep", "nope",
        "agreed", "agreed!",
        "done", "done!",
        "noted", "noted!",
        "will do", "will do!"
    };

    public Tier Classify(Email email, bool hasIcsAttachment = false)
    {
        // Tier 1 checks
        if (hasIcsAttachment)
            return Tier.Excluded;

        var subjectLower = email.Subject.ToLowerInvariant();
        var bodyLower = email.BodyText.ToLowerInvariant();

        foreach (var pattern in Tier1SubjectPatterns)
        {
            if (Regex.IsMatch(subjectLower, pattern, RegexOptions.IgnoreCase))
                return Tier.Excluded;
        }

        foreach (var pattern in Tier1BodyPatterns)
        {
            if (Regex.IsMatch(bodyLower, pattern, RegexOptions.IgnoreCase))
                return Tier.Excluded;
        }

        // Tier 2 checks
        var senderLower = email.Sender.ToLowerInvariant();

        foreach (var pattern in AutomatedSenderPatterns)
        {
            if (Regex.IsMatch(senderLower, pattern))
                return Tier.MetadataOnly;
        }

        var bodyStripped = email.BodyText.Trim();
        if (OneWordReplies.Contains(bodyStripped))
            return Tier.MetadataOnly;

        if (email.BodyWordCount < MinWordsForVectorization)
            return Tier.MetadataOnly;

        // Tier 3: Everything else
        return Tier.Vectorize;
    }
}
