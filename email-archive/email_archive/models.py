"""Data models for email archive."""

from __future__ import annotations

import uuid
from dataclasses import dataclass, field
from datetime import datetime, timezone
from enum import IntEnum


class Tier(IntEnum):
    """Email classification tiers."""

    UNCLASSIFIED = 0
    EXCLUDED = 1  # Skip entirely
    METADATA_ONLY = 2  # Store metadata, don't vectorise
    VECTORIZE = 3  # Full vectorisation


@dataclass
class Email:
    """Represents a parsed email."""

    message_id: str
    date: datetime
    sender: str
    sender_name: str
    recipients: list[str]
    subject: str
    body_text: str
    is_sent: bool = False
    has_attachments: bool = False
    tier: Tier = Tier.UNCLASSIFIED
    thread_id: str | None = None
    id: str = field(default_factory=lambda: str(uuid.uuid4()))
    created_at: datetime = field(default_factory=lambda: datetime.now(timezone.utc))

    @property
    def body_word_count(self) -> int:
        if not self.body_text or not self.body_text.strip():
            return 0
        return len(self.body_text.split())


@dataclass
class Attachment:
    """Represents an email attachment."""

    email_id: str
    filename: str
    mime_type: str
    size_bytes: int
    extracted_text: str | None = None
    id: str = field(default_factory=lambda: str(uuid.uuid4()))
    created_at: datetime = field(default_factory=lambda: datetime.now(timezone.utc))


@dataclass
class ParsedEmail:
    """Container for parsed email with raw attachment data."""

    email: Email
    attachments: list[RawAttachment] = field(default_factory=list)


@dataclass
class RawAttachment:
    """Raw attachment data from PST parsing."""

    filename: str
    mime_type: str | None
    content: bytes
    size_bytes: int
