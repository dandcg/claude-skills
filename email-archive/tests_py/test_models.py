"""Tests for data models."""

from datetime import datetime

from email_archive.models import Attachment, Email, Tier


def test_email_body_word_count():
    email = Email(
        message_id="<test@local>",
        date=datetime(2024, 1, 1),
        sender="alice@example.com",
        sender_name="Alice",
        recipients=["bob@example.com"],
        subject="Test",
        body_text="Hello world this is a test",
    )
    assert email.body_word_count == 6


def test_email_body_word_count_empty():
    email = Email(
        message_id="<test@local>",
        date=datetime(2024, 1, 1),
        sender="alice@example.com",
        sender_name="Alice",
        recipients=[],
        subject="Test",
        body_text="",
    )
    assert email.body_word_count == 0


def test_email_body_word_count_whitespace():
    email = Email(
        message_id="<test@local>",
        date=datetime(2024, 1, 1),
        sender="alice@example.com",
        sender_name="Alice",
        recipients=[],
        subject="Test",
        body_text="   ",
    )
    assert email.body_word_count == 0


def test_email_defaults():
    email = Email(
        message_id="<test@local>",
        date=datetime(2024, 1, 1),
        sender="alice@example.com",
        sender_name="Alice",
        recipients=[],
        subject="Test",
        body_text="Hello",
    )
    assert email.tier == Tier.UNCLASSIFIED
    assert email.is_sent is False
    assert email.has_attachments is False
    assert email.id  # should be a non-empty UUID string
    assert email.created_at is not None


def test_tier_values():
    assert Tier.UNCLASSIFIED == 0
    assert Tier.EXCLUDED == 1
    assert Tier.METADATA_ONLY == 2
    assert Tier.VECTORIZE == 3
