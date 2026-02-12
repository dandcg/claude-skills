"""Email classification into tiers."""

from __future__ import annotations

import re

from .models import Email, Tier

MIN_WORDS_FOR_VECTORIZATION = 30

TIER1_SUBJECT_PATTERNS = [
    r"password reset",
    r"reset your password",
    r"verification code",
    r"verify your email",
    r"confirm your email",
    r"unsubscribe",
    r"has been delivered",
    r"out for delivery",
    r"has shipped",
    r"delivery notification",
    r"delivery confirmation",
    r"accepted:\s",
    r"declined:\s",
    r"tentative:\s",
    r"canceled:\s",
]

TIER1_BODY_PATTERNS = [
    r"click here to reset your password",
    r"your verification code is",
    r"your package (has been |was )?(delivered|shipped)",
    r"you have successfully unsubscribed",
    r"delivery failure",
    r"mail delivery (failed|subsystem)",
    r"mailer-daemon",
]

AUTOMATED_SENDER_PATTERNS = [
    r"^noreply@",
    r"^no-reply@",
    r"^notifications?@",
    r"^alerts?@",
    r"^mailer-daemon@",
    r"^postmaster@",
    r"^bounce",
]

ONE_WORD_REPLIES = frozenset(
    s.lower()
    for s in [
        "thanks",
        "thank you",
        "thanks!",
        "thank you!",
        "ok",
        "okay",
        "ok!",
        "okay!",
        "got it",
        "got it!",
        "sounds good",
        "sounds good!",
        "great",
        "great!",
        "perfect",
        "perfect!",
        "sure",
        "sure!",
        "yes",
        "no",
        "yep",
        "nope",
        "agreed",
        "agreed!",
        "done",
        "done!",
        "noted",
        "noted!",
        "will do",
        "will do!",
    ]
)


def classify(email: Email, has_ics_attachment: bool = False) -> Tier:
    """Classify an email into a tier.

    Tier 1 (Excluded): Calendar invites, password resets, delivery notifications.
    Tier 2 (Metadata Only): Automated senders, short replies, low word count.
    Tier 3 (Vectorize): Real conversations with substantive content.
    """
    # Tier 1 checks
    if has_ics_attachment:
        return Tier.EXCLUDED

    subject_lower = email.subject.lower()
    body_lower = email.body_text.lower()

    for pattern in TIER1_SUBJECT_PATTERNS:
        if re.search(pattern, subject_lower, re.IGNORECASE):
            return Tier.EXCLUDED

    for pattern in TIER1_BODY_PATTERNS:
        if re.search(pattern, body_lower, re.IGNORECASE):
            return Tier.EXCLUDED

    # Tier 2 checks
    sender_lower = email.sender.lower()

    for pattern in AUTOMATED_SENDER_PATTERNS:
        if re.search(pattern, sender_lower):
            return Tier.METADATA_ONLY

    body_stripped = email.body_text.strip().lower()
    if body_stripped in ONE_WORD_REPLIES:
        return Tier.METADATA_ONLY

    if email.body_word_count < MIN_WORDS_FOR_VECTORIZATION:
        return Tier.METADATA_ONLY

    # Tier 3: Everything else
    return Tier.VECTORIZE
