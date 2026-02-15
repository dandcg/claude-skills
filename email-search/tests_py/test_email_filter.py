"""Tests for email classification."""

from datetime import datetime

from email_search.email_filter import classify
from email_search.models import Email, Tier


def _make_email(
    subject: str = "Test",
    body: str = "This is a substantive email body with more than thirty words in it to ensure that it passes the minimum word count threshold for tier three classification checking in the email filter module.",
    sender: str = "alice@example.com",
    sender_name: str = "Alice",
) -> Email:
    return Email(
        message_id="<test@local>",
        date=datetime(2024, 1, 1),
        sender=sender,
        sender_name=sender_name,
        recipients=["bob@example.com"],
        subject=subject,
        body_text=body,
    )


# ── Tier 1 (Excluded) ────────────────────────────────────────────────


def test_calendar_invite_excluded():
    assert classify(_make_email(), has_ics_attachment=True) == Tier.EXCLUDED


def test_password_reset_subject_excluded():
    email = _make_email(subject="Password Reset Request")
    assert classify(email) == Tier.EXCLUDED


def test_verification_code_subject_excluded():
    email = _make_email(subject="Your verification code is 123456")
    assert classify(email) == Tier.EXCLUDED


def test_delivery_notification_subject_excluded():
    email = _make_email(subject="Your package has been delivered")
    assert classify(email) == Tier.EXCLUDED


def test_delivery_body_excluded():
    email = _make_email(body="Your package has been delivered to your front door.")
    assert classify(email) == Tier.EXCLUDED


def test_mailer_daemon_body_excluded():
    email = _make_email(body="Mail delivery failed. Mailer-daemon returned an error.")
    assert classify(email) == Tier.EXCLUDED


def test_calendar_accepted_excluded():
    email = _make_email(subject="Accepted: Team Meeting")
    assert classify(email) == Tier.EXCLUDED


# ── Tier 2 (MetadataOnly) ────────────────────────────────────────────


def test_noreply_sender_metadata_only():
    email = _make_email(sender="noreply@example.com")
    assert classify(email) == Tier.METADATA_ONLY


def test_notifications_sender_metadata_only():
    email = _make_email(sender="notifications@example.com")
    assert classify(email) == Tier.METADATA_ONLY


def test_one_word_reply_metadata_only():
    email = _make_email(body="Thanks!")
    assert classify(email) == Tier.METADATA_ONLY


def test_short_body_metadata_only():
    email = _make_email(body="Short message here")
    assert classify(email) == Tier.METADATA_ONLY


def test_got_it_reply_metadata_only():
    email = _make_email(body="Got it!")
    assert classify(email) == Tier.METADATA_ONLY


# ── Tier 3 (Vectorize) ───────────────────────────────────────────────


def test_substantive_email_vectorized():
    email = _make_email()
    assert classify(email) == Tier.VECTORIZE


def test_normal_conversation_vectorized():
    body = (
        "Hi Bob, I wanted to follow up on our conversation from last week "
        "about the project timeline. I think we should move the deadline "
        "to next month given the complexity of the remaining tasks. "
        "Let me know your thoughts on this approach."
    )
    email = _make_email(body=body)
    assert classify(email) == Tier.VECTORIZE
