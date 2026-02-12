"""Tests for markdown formatter."""

from datetime import datetime

from email_archive.markdown_formatter import (
    format_contact_section,
    format_ideas_header,
    format_review_email_section,
)
from email_archive.store import ContactExport, ReviewPeriodExport


def test_format_contact_section():
    contact = ContactExport(
        email="alice@example.com",
        name="Alice Smith",
        total_emails=42,
        sent_to=20,
        received_from=22,
        first_contact=datetime(2023, 1, 1),
        last_contact=datetime(2024, 6, 15),
        communication_direction="bidirectional",
    )

    md = format_contact_section(contact)
    assert "### Alice Smith" in md
    assert "alice@example.com" in md
    assert "42 total emails" in md
    assert "bidirectional" in md
    assert "2023-01-01" in md
    assert "2024-06-15" in md


def test_format_contact_section_no_name():
    contact = ContactExport(
        email="bob@example.com",
        name="",
        total_emails=10,
        sent_to=5,
        received_from=5,
        first_contact=datetime(2024, 1, 1),
        last_contact=datetime(2024, 6, 1),
        communication_direction="bidirectional",
    )

    md = format_contact_section(contact)
    assert "### bob@example.com" in md


def test_format_review_email_section():
    review = ReviewPeriodExport(
        period_start=datetime(2024, 1, 1),
        period_end=datetime(2024, 1, 31),
        email_count=100,
        sent_count=40,
        received_count=60,
        top_contacts=[
            ContactExport(
                email="alice@example.com",
                name="Alice",
                total_emails=15,
                sent_to=5,
                received_from=10,
                first_contact=datetime(2024, 1, 1),
                last_contact=datetime(2024, 1, 30),
                communication_direction="bidirectional",
            )
        ],
        peak_activity_day="Monday",
        peak_activity_hour=9,
    )

    md = format_review_email_section(review)
    assert "## Email Activity" in md
    assert "100" in md
    assert "Monday at 9:00" in md
    assert "Alice" in md


def test_format_ideas_header():
    md = format_ideas_header("Email Contacts", datetime(2024, 6, 15), "developing")
    assert "# Email Contacts" in md
    assert "2024-06-15" in md
    assert "developing" in md
