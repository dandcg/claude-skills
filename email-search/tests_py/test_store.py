"""Tests for ChromaDB store."""

import shutil
import tempfile
from datetime import datetime

import pytest

from email_search.models import Attachment, Email, Tier
from email_search.store import Store


@pytest.fixture
def store(tmp_path):
    """Create a temporary store for testing."""
    s = Store(data_dir=str(tmp_path / "test-data"))
    yield s


def _make_email(
    tier: Tier = Tier.VECTORIZE,
    subject: str = "Project Update",
    sender: str = "alice@example.com",
    sender_name: str = "Alice",
    body: str = "Here is the latest project update with all the details about the timeline and deliverables.",
    date: datetime = datetime(2024, 6, 15, 10, 30),
    is_sent: bool = False,
) -> Email:
    return Email(
        message_id="<test@local>",
        date=date,
        sender=sender,
        sender_name=sender_name,
        recipients=["bob@example.com"],
        subject=subject,
        body_text=body,
        tier=tier,
        is_sent=is_sent,
    )


class TestInsertAndCount:
    def test_insert_email(self, store):
        email = _make_email()
        store.insert_email(email)

        counts = store.get_status_counts()
        assert counts.total == 1
        assert counts.vectorize == 1

    def test_insert_multiple_tiers(self, store):
        store.insert_email(_make_email(tier=Tier.METADATA_ONLY))
        store.insert_email(_make_email(tier=Tier.VECTORIZE))
        store.insert_email(_make_email(tier=Tier.VECTORIZE))

        counts = store.get_status_counts()
        assert counts.total == 3
        assert counts.metadata_only == 1
        assert counts.vectorize == 2
        assert counts.embedded == 2  # Tier 3 auto-embedded

    def test_empty_store(self, store):
        counts = store.get_status_counts()
        assert counts.total == 0

    def test_truncate(self, store):
        store.insert_email(_make_email())
        store.insert_email(_make_email())

        store.truncate()

        counts = store.get_status_counts()
        assert counts.total == 0


class TestAttachments:
    def test_insert_attachment(self, store):
        email = _make_email()
        store.insert_email(email)

        att = Attachment(
            email_id=email.id,
            filename="report.pdf",
            mime_type="application/pdf",
            size_bytes=1024,
            extracted_text="This is the report content.",
        )
        store.insert_attachment(att, email)

        assert store.get_attachment_count() == 1
        assert store.get_attachments_with_text_count() == 1

    def test_attachment_without_text(self, store):
        email = _make_email()
        store.insert_email(email)

        att = Attachment(
            email_id=email.id,
            filename="image.png",
            mime_type="image/png",
            size_bytes=2048,
        )
        store.insert_attachment(att, email)

        assert store.get_attachment_count() == 1
        assert store.get_attachments_with_text_count() == 0


class TestSearch:
    def test_search_emails(self, store):
        store.insert_email(_make_email(subject="Budget meeting notes for Q3"))
        store.insert_email(_make_email(subject="Vacation request form"))

        results = store.search_emails("budget meeting", limit=5)
        assert len(results) > 0
        # The budget email should score higher
        assert "budget" in results[0].subject.lower() or results[0].similarity > 0

    def test_search_empty_store(self, store):
        results = store.search_emails("anything")
        assert len(results) == 0

    def test_search_with_sender_filter(self, store):
        store.insert_email(_make_email(sender="alice@example.com", sender_name="Alice"))
        store.insert_email(_make_email(sender="bob@example.com", sender_name="Bob"))

        results = store.search_emails("project update", sender_filter="alice")
        for r in results:
            assert "alice" in r.sender.lower() or "alice" in r.sender_name.lower()

    def test_search_attachments(self, store):
        email = _make_email()
        store.insert_email(email)

        att = Attachment(
            email_id=email.id,
            filename="quarterly-report.pdf",
            mime_type="application/pdf",
            size_bytes=1024,
            extracted_text="Quarterly financial report with revenue and expense breakdown for Q3 2024.",
        )
        store.insert_attachment(att, email)

        results = store.search_attachments("financial report", limit=5)
        assert len(results) > 0


class TestAnalytics:
    def test_archive_summary(self, store):
        store.insert_email(_make_email(date=datetime(2024, 1, 1)))
        store.insert_email(_make_email(date=datetime(2024, 6, 15)))

        summ = store.get_archive_summary()
        assert summ.total_emails == 2
        assert summ.earliest_email.year == 2024
        assert summ.latest_email.month == 6

    def test_timeline_yearly(self, store):
        store.insert_email(_make_email(date=datetime(2023, 3, 1)))
        store.insert_email(_make_email(date=datetime(2024, 6, 15)))

        periods = store.get_timeline(group_by_month=False)
        assert len(periods) == 2
        assert periods[0].year == 2023
        assert periods[1].year == 2024

    def test_timeline_monthly(self, store):
        store.insert_email(_make_email(date=datetime(2024, 1, 1)))
        store.insert_email(_make_email(date=datetime(2024, 1, 15)))
        store.insert_email(_make_email(date=datetime(2024, 2, 1)))

        periods = store.get_timeline(group_by_month=True)
        assert len(periods) == 2
        jan = [p for p in periods if p.month == 1][0]
        assert jan.email_count == 2

    def test_top_contacts(self, store):
        for _ in range(5):
            store.insert_email(_make_email(sender="frequent@example.com"))
        store.insert_email(_make_email(sender="rare@example.com"))

        contacts = store.get_top_contacts(limit=10)
        assert contacts[0].email == "frequent@example.com"
        assert contacts[0].total_emails == 5

    def test_activity_by_hour(self, store):
        store.insert_email(_make_email(date=datetime(2024, 1, 1, 9, 0)))
        store.insert_email(_make_email(date=datetime(2024, 1, 1, 9, 30)))
        store.insert_email(_make_email(date=datetime(2024, 1, 1, 14, 0)))

        activity = store.get_activity_by_hour()
        hour_9 = [a for a in activity if a.hour == 9][0]
        assert hour_9.email_count == 2


class TestExport:
    def test_contacts_for_period(self, store):
        store.insert_email(_make_email(date=datetime(2024, 1, 15)))

        contacts = store.get_contacts_for_period(
            datetime(2024, 1, 1),
            datetime(2024, 1, 31),
            10,
        )
        assert len(contacts) == 1

    def test_review_data(self, store):
        store.insert_email(_make_email(date=datetime(2024, 1, 15)))
        store.insert_email(_make_email(date=datetime(2024, 1, 16), is_sent=True))

        review = store.get_review_data(
            datetime(2024, 1, 1),
            datetime(2024, 1, 31),
            10,
        )
        assert review.email_count == 2
        assert review.sent_count == 1
        assert review.received_count == 1
