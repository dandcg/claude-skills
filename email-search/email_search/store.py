"""ChromaDB storage layer.

Replaces PostgreSQL + pgvector with a local ChromaDB persistent database.
Two collections: 'emails' and 'attachments'.
ChromaDB handles embeddings automatically using its default model (all-MiniLM-L6-v2).
"""

from __future__ import annotations

import json
from collections import Counter
from dataclasses import dataclass
from datetime import datetime
from typing import Any

import chromadb

from .models import Attachment, Email, Tier

# Default data directory (relative to working directory)
DEFAULT_DATA_DIR = "./email-search-data"


@dataclass
class StatusCounts:
    total: int
    excluded: int
    metadata_only: int
    vectorize: int
    embedded: int


@dataclass
class EmailSearchResult:
    id: str
    date: datetime
    sender: str
    sender_name: str
    subject: str
    body_snippet: str
    similarity: float
    has_attachments: bool


@dataclass
class AttachmentSearchResult:
    id: str
    email_id: str
    filename: str
    text_snippet: str
    similarity: float
    email_date: datetime
    email_sender: str
    email_subject: str


@dataclass
class TimelinePeriod:
    year: int
    month: int | None
    email_count: int
    sent_count: int
    received_count: int


@dataclass
class ContactStats:
    email: str
    name: str
    total_emails: int
    sent_to: int
    received_from: int
    first_contact: datetime
    last_contact: datetime


@dataclass
class ActivityStats:
    hour: int
    day_of_week: int
    email_count: int


@dataclass
class ArchiveSummary:
    total_emails: int
    unique_contacts: int
    earliest_email: datetime
    latest_email: datetime
    total_years_span: int
    avg_emails_per_day: float


@dataclass
class ContactExport:
    email: str
    name: str
    total_emails: int
    sent_to: int
    received_from: int
    first_contact: datetime
    last_contact: datetime
    communication_direction: str


@dataclass
class ReviewPeriodExport:
    period_start: datetime
    period_end: datetime
    email_count: int
    sent_count: int
    received_count: int
    top_contacts: list[ContactExport]
    peak_activity_day: str
    peak_activity_hour: int


class Store:
    """ChromaDB-backed email archive store."""

    def __init__(self, data_dir: str = DEFAULT_DATA_DIR):
        self._client = chromadb.PersistentClient(path=data_dir)
        self._emails = self._client.get_or_create_collection(
            name="emails",
            metadata={"hnsw:space": "cosine"},
        )
        self._attachments = self._client.get_or_create_collection(
            name="attachments",
            metadata={"hnsw:space": "cosine"},
        )

    # ── Email Operations ──────────────────────────────────────────────

    def insert_email(self, email: Email) -> None:
        """Insert an email. Tier 3 emails get auto-embedded via documents."""
        metadata = _build_email_metadata(email)
        document = f"Subject: {email.subject}\nFrom: {email.sender}\n\n{email.body_text}"

        if email.tier == Tier.VECTORIZE:
            # ChromaDB auto-embeds the document text
            self._emails.add(
                ids=[email.id],
                documents=[document],
                metadatas=[metadata],
            )
        else:
            # Tier 2: store metadata only, no document for embedding
            self._emails.add(
                ids=[email.id],
                documents=[document],
                metadatas=[metadata],
            )

    def get_status_counts(self) -> StatusCounts:
        """Get counts of emails by tier and embedding status."""
        total = self._emails.count()
        if total == 0:
            return StatusCounts(0, 0, 0, 0, 0)

        all_meta = self._get_all_email_metadata()

        excluded = sum(1 for m in all_meta if m.get("tier") == Tier.EXCLUDED)
        metadata_only = sum(1 for m in all_meta if m.get("tier") == Tier.METADATA_ONLY)
        vectorize = sum(1 for m in all_meta if m.get("tier") == Tier.VECTORIZE)
        # In ChromaDB, Tier 3 emails are always embedded (auto-embedded on add)
        embedded = vectorize

        return StatusCounts(total, excluded, metadata_only, vectorize, embedded)

    def truncate(self) -> None:
        """Delete all data."""
        self._client.delete_collection("emails")
        self._client.delete_collection("attachments")
        self._emails = self._client.get_or_create_collection(
            name="emails",
            metadata={"hnsw:space": "cosine"},
        )
        self._attachments = self._client.get_or_create_collection(
            name="attachments",
            metadata={"hnsw:space": "cosine"},
        )

    # ── Attachment Operations ─────────────────────────────────────────

    def insert_attachment(self, attachment: Attachment, email: Email) -> None:
        """Insert an attachment. Those with extracted text get auto-embedded."""
        metadata = _build_attachment_metadata(attachment, email)
        document = attachment.extracted_text or ""

        self._attachments.add(
            ids=[attachment.id],
            documents=[document],
            metadatas=[metadata],
        )

    def get_attachment_count(self) -> int:
        return self._attachments.count()

    def get_attachments_with_text_count(self) -> int:
        result = self._attachments.get(
            where={"has_text": True},
            include=[],
        )
        return len(result["ids"])

    def get_attachments_embedded_count(self) -> int:
        """All attachments with text are embedded (auto-embedded on add)."""
        return self.get_attachments_with_text_count()

    # ── Search Operations ─────────────────────────────────────────────

    def search_emails(
        self,
        query: str,
        limit: int = 10,
        start_date: datetime | None = None,
        end_date: datetime | None = None,
        sender_filter: str | None = None,
    ) -> list[EmailSearchResult]:
        """Search emails using natural language. ChromaDB auto-embeds the query."""
        where_conditions: list[dict[str, Any]] = [{"tier": Tier.VECTORIZE}]

        if start_date:
            where_conditions.append({"date_ticks": {"$gte": start_date.timestamp()}})
        if end_date:
            where_conditions.append({"date_ticks": {"$lte": end_date.timestamp()}})

        where: dict[str, Any] | None = None
        if len(where_conditions) == 1:
            where = where_conditions[0]
        elif len(where_conditions) > 1:
            where = {"$and": where_conditions}

        # Over-fetch if we need to post-filter by sender
        n_results = limit * 3 if sender_filter else limit

        results = self._emails.query(
            query_texts=[query],
            n_results=n_results,
            where=where,
            include=["metadatas", "documents", "distances"],
        )

        search_results: list[EmailSearchResult] = []

        if not results["ids"] or not results["ids"][0]:
            return search_results

        for i, id_ in enumerate(results["ids"][0]):
            meta = results["metadatas"][0][i] if results["metadatas"] else {}
            document = results["documents"][0][i] if results["documents"] else ""
            distance = results["distances"][0][i] if results["distances"] else 1.0

            sender = meta.get("sender", "")
            sender_name = meta.get("sender_name", "")

            # Post-filter by sender if needed
            if sender_filter:
                filt = sender_filter.lower()
                if filt not in sender.lower() and filt not in sender_name.lower():
                    continue

            # ChromaDB returns cosine distance; similarity = 1 - distance
            similarity = 1.0 - distance

            search_results.append(
                EmailSearchResult(
                    id=id_,
                    date=_parse_date(meta.get("date", "")),
                    sender=sender,
                    sender_name=sender_name,
                    subject=meta.get("subject", ""),
                    body_snippet=_create_snippet(document, 200),
                    similarity=similarity,
                    has_attachments=meta.get("has_attachments", False),
                )
            )

            if len(search_results) >= limit:
                break

        return search_results

    def search_attachments(
        self,
        query: str,
        limit: int = 10,
    ) -> list[AttachmentSearchResult]:
        """Search attachments using natural language."""
        results = self._attachments.query(
            query_texts=[query],
            n_results=limit,
            where={"has_text": True},
            include=["metadatas", "documents", "distances"],
        )

        search_results: list[AttachmentSearchResult] = []

        if not results["ids"] or not results["ids"][0]:
            return search_results

        for i, id_ in enumerate(results["ids"][0]):
            meta = results["metadatas"][0][i] if results["metadatas"] else {}
            document = results["documents"][0][i] if results["documents"] else ""
            distance = results["distances"][0][i] if results["distances"] else 1.0

            similarity = 1.0 - distance

            search_results.append(
                AttachmentSearchResult(
                    id=id_,
                    email_id=meta.get("email_id", ""),
                    filename=meta.get("filename", ""),
                    text_snippet=_create_snippet(document, 200),
                    similarity=similarity,
                    email_date=_parse_date(meta.get("email_date", "")),
                    email_sender=meta.get("email_sender", ""),
                    email_subject=meta.get("email_subject", ""),
                )
            )

        return search_results

    # ── Analytics Operations ──────────────────────────────────────────

    def get_archive_summary(self) -> ArchiveSummary:
        all_meta = self._get_stored_email_metadata()
        if not all_meta:
            now = datetime.utcnow()
            return ArchiveSummary(0, 0, now, now, 1, 0.0)

        dates = [_parse_date(m.get("date", "")) for m in all_meta]
        senders = {m.get("sender", "") for m in all_meta}

        earliest = min(dates)
        latest = max(dates)
        days_span = max(1, (latest - earliest).days)

        return ArchiveSummary(
            total_emails=len(all_meta),
            unique_contacts=len(senders),
            earliest_email=earliest,
            latest_email=latest,
            total_years_span=latest.year - earliest.year + 1,
            avg_emails_per_day=round(len(all_meta) / days_span, 2),
        )

    def get_timeline(self, group_by_month: bool = False) -> list[TimelinePeriod]:
        all_meta = self._get_stored_email_metadata()
        if not all_meta:
            return []

        counter: dict[tuple[int, int | None], dict[str, int]] = {}

        for m in all_meta:
            dt = _parse_date(m.get("date", ""))
            is_sent = m.get("is_sent", False)

            if group_by_month:
                key = (dt.year, dt.month)
            else:
                key = (dt.year, None)

            if key not in counter:
                counter[key] = {"total": 0, "sent": 0, "received": 0}

            counter[key]["total"] += 1
            if is_sent:
                counter[key]["sent"] += 1
            else:
                counter[key]["received"] += 1

        periods = []
        for (year, month), counts in sorted(counter.items()):
            periods.append(
                TimelinePeriod(
                    year=year,
                    month=month,
                    email_count=counts["total"],
                    sent_count=counts["sent"],
                    received_count=counts["received"],
                )
            )

        return periods

    def get_top_contacts(self, limit: int = 20) -> list[ContactStats]:
        all_meta = self._get_stored_email_metadata()
        if not all_meta:
            return []

        contact_data: dict[str, dict[str, Any]] = {}

        for m in all_meta:
            email_addr = m.get("sender", "")
            name = m.get("sender_name", "")
            dt = _parse_date(m.get("date", ""))
            is_sent = m.get("is_sent", False)

            if email_addr not in contact_data:
                contact_data[email_addr] = {
                    "name": name,
                    "total": 0,
                    "sent": 0,
                    "received": 0,
                    "first": dt,
                    "last": dt,
                }

            cd = contact_data[email_addr]
            cd["total"] += 1
            if is_sent:
                cd["sent"] += 1
            else:
                cd["received"] += 1
            if name and name != email_addr.split("@")[0]:
                cd["name"] = name
            if dt < cd["first"]:
                cd["first"] = dt
            if dt > cd["last"]:
                cd["last"] = dt

        sorted_contacts = sorted(contact_data.items(), key=lambda x: x[1]["total"], reverse=True)

        return [
            ContactStats(
                email=email_addr,
                name=data["name"],
                total_emails=data["total"],
                sent_to=data["sent"],
                received_from=data["received"],
                first_contact=data["first"],
                last_contact=data["last"],
            )
            for email_addr, data in sorted_contacts[:limit]
        ]

    def get_activity_by_hour(self) -> list[ActivityStats]:
        all_meta = self._get_stored_email_metadata()
        counter = Counter[int]()
        for m in all_meta:
            dt = _parse_date(m.get("date", ""))
            counter[dt.hour] += 1

        return [
            ActivityStats(hour=hour, day_of_week=0, email_count=count)
            for hour, count in sorted(counter.items())
        ]

    def get_activity_by_day_of_week(self) -> list[ActivityStats]:
        all_meta = self._get_stored_email_metadata()
        counter = Counter[int]()
        for m in all_meta:
            dt = _parse_date(m.get("date", ""))
            counter[dt.weekday()] += 1  # Monday=0, Sunday=6

        # Convert to Sunday=0 format to match C# DayOfWeek
        return [
            ActivityStats(hour=0, day_of_week=dow, email_count=count)
            for dow, count in sorted(counter.items())
        ]

    # ── Export Operations ─────────────────────────────────────────────

    def get_contacts_for_period(
        self, start: datetime, end: datetime, limit: int
    ) -> list[ContactExport]:
        all_meta = self._get_stored_email_metadata()

        # Filter by date
        period_meta = [
            m
            for m in all_meta
            if start <= _parse_date(m.get("date", "")) <= end
        ]

        contact_data: dict[str, dict[str, Any]] = {}

        for m in period_meta:
            is_sent = m.get("is_sent", False)
            if is_sent:
                # For sent emails, the "contact" is the first recipient
                recipients_json = m.get("recipients", "[]")
                try:
                    recipients = json.loads(recipients_json) if isinstance(recipients_json, str) else []
                except (json.JSONDecodeError, TypeError):
                    recipients = []
                contact_email = recipients[0] if recipients else ""
                contact_name = ""
            else:
                contact_email = m.get("sender", "")
                contact_name = m.get("sender_name", "")

            if not contact_email:
                continue

            dt = _parse_date(m.get("date", ""))

            if contact_email not in contact_data:
                contact_data[contact_email] = {
                    "name": contact_name,
                    "total": 0,
                    "sent_to": 0,
                    "received_from": 0,
                    "first": dt,
                    "last": dt,
                }

            cd = contact_data[contact_email]
            cd["total"] += 1
            if is_sent:
                cd["sent_to"] += 1
            else:
                cd["received_from"] += 1
            if contact_name:
                cd["name"] = contact_name
            if dt < cd["first"]:
                cd["first"] = dt
            if dt > cd["last"]:
                cd["last"] = dt

        sorted_contacts = sorted(contact_data.items(), key=lambda x: x[1]["total"], reverse=True)

        results = []
        for email_addr, data in sorted_contacts[:limit]:
            has_sent = data["sent_to"] > 0
            has_received = data["received_from"] > 0
            if has_sent and has_received:
                direction = "bidirectional"
            elif has_sent:
                direction = "outbound"
            elif has_received:
                direction = "inbound"
            else:
                direction = "unknown"

            results.append(
                ContactExport(
                    email=email_addr,
                    name=data["name"],
                    total_emails=data["total"],
                    sent_to=data["sent_to"],
                    received_from=data["received_from"],
                    first_contact=data["first"],
                    last_contact=data["last"],
                    communication_direction=direction,
                )
            )

        return results

    def get_review_data(
        self, start: datetime, end: datetime, top_contacts_limit: int
    ) -> ReviewPeriodExport:
        all_meta = self._get_stored_email_metadata()

        period_meta = [
            m
            for m in all_meta
            if start <= _parse_date(m.get("date", "")) <= end
        ]

        email_count = len(period_meta)
        sent_count = sum(1 for m in period_meta if m.get("is_sent", False))
        received_count = email_count - sent_count

        # Peak activity
        dow_counter = Counter[int]()
        hour_counter = Counter[int]()
        for m in period_meta:
            dt = _parse_date(m.get("date", ""))
            dow_counter[dt.weekday()] += 1
            hour_counter[dt.hour] += 1

        day_names = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"]
        peak_day = ""
        peak_hour = 0
        if dow_counter:
            peak_dow = dow_counter.most_common(1)[0][0]
            peak_day = day_names[peak_dow] if 0 <= peak_dow < 7 else "Unknown"
        if hour_counter:
            peak_hour = hour_counter.most_common(1)[0][0]

        top_contacts = self.get_contacts_for_period(start, end, top_contacts_limit)

        return ReviewPeriodExport(
            period_start=start,
            period_end=end,
            email_count=email_count,
            sent_count=sent_count,
            received_count=received_count,
            top_contacts=top_contacts,
            peak_activity_day=peak_day,
            peak_activity_hour=peak_hour,
        )

    # ── Internal Helpers ──────────────────────────────────────────────

    def _get_all_email_metadata(self) -> list[dict[str, Any]]:
        """Get metadata for all emails."""
        count = self._emails.count()
        if count == 0:
            return []

        result = self._emails.get(include=["metadatas"])
        return result["metadatas"] or []

    def _get_stored_email_metadata(self) -> list[dict[str, Any]]:
        """Get metadata for tiers 2 and 3 (stored, non-excluded)."""
        all_meta = self._get_all_email_metadata()
        return [
            m
            for m in all_meta
            if m.get("tier") in (Tier.METADATA_ONLY, Tier.VECTORIZE)
        ]


# ── Module-Level Helpers ──────────────────────────────────────────────


def _build_email_metadata(email: Email) -> dict[str, Any]:
    return {
        "message_id": email.message_id,
        "thread_id": email.thread_id or "",
        "date": email.date.isoformat(),
        "date_ticks": email.date.timestamp(),
        "date_year": email.date.year,
        "date_month": email.date.month,
        "date_hour": email.date.hour,
        "date_dow": email.date.weekday(),
        "sender": email.sender,
        "sender_name": email.sender_name,
        "recipients": json.dumps(email.recipients),
        "subject": email.subject,
        "body_word_count": email.body_word_count,
        "is_sent": email.is_sent,
        "has_attachments": email.has_attachments,
        "tier": int(email.tier),
    }


def _build_attachment_metadata(attachment: Attachment, email: Email) -> dict[str, Any]:
    return {
        "email_id": attachment.email_id,
        "filename": attachment.filename,
        "mime_type": attachment.mime_type,
        "size_bytes": attachment.size_bytes,
        "has_text": attachment.extracted_text is not None,
        "email_date": email.date.isoformat(),
        "email_sender": email.sender,
        "email_subject": email.subject,
    }


def _parse_date(value: str) -> datetime:
    if not value:
        return datetime.min
    try:
        return datetime.fromisoformat(value)
    except (ValueError, TypeError):
        return datetime.min


def _create_snippet(text: str, max_length: int = 200) -> str:
    if not text:
        return ""
    text = " ".join(text.split())
    if len(text) <= max_length:
        return text
    return text[:max_length] + "..."
