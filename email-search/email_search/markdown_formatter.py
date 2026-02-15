"""Markdown formatting utilities for export commands."""

from __future__ import annotations

from datetime import datetime

from .store import ContactExport, ReviewPeriodExport


def format_contact_section(contact: ContactExport) -> str:
    """Format a contact export as a markdown section."""
    header = contact.name if contact.name else contact.email
    lines = [f"### {header}", ""]

    if contact.name:
        lines.append(f"**Email:** {contact.email}")

    lines.append(
        f"**Communication:** {contact.total_emails} total emails "
        f"({contact.sent_to} sent, {contact.received_from} received)"
    )
    lines.append(f"**Direction:** {contact.communication_direction}")
    lines.append(f"**First Contact:** {contact.first_contact:%Y-%m-%d}")
    lines.append(f"**Last Contact:** {contact.last_contact:%Y-%m-%d}")
    lines.append("")

    return "\n".join(lines) + "\n"


def format_review_email_section(review: ReviewPeriodExport) -> str:
    """Format a review period export as a markdown email activity section."""
    lines = [
        "## Email Activity",
        "",
        f"**Period:** {review.period_start:%Y-%m-%d} to {review.period_end:%Y-%m-%d}",
        "",
        "### Summary",
        f"- **Total Emails:** {review.email_count}",
        f"- **Sent:** {review.sent_count}",
        f"- **Received:** {review.received_count}",
        f"- **Peak Activity:** {review.peak_activity_day} at {review.peak_activity_hour}:00",
        "",
    ]

    if review.top_contacts:
        lines.append("### Top Contacts")
        for contact in review.top_contacts:
            name = contact.name if contact.name else contact.email
            lines.append(f"- **{name}** ({contact.email}): {contact.total_emails} emails")
        lines.append("")

    return "\n".join(lines) + "\n"


def format_ideas_header(title: str, date: datetime, status: str) -> str:
    """Format a header for ideas/thoughts files."""
    lines = [
        f"# {title}",
        "",
        f"**Added:** {date:%Y-%m-%d}",
        f"**Status:** {status}",
        "**Related:** ",
        "",
    ]
    return "\n".join(lines) + "\n"
