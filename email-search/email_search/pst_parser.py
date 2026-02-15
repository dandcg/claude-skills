"""PST file parser using libpff."""

from __future__ import annotations

import uuid
from datetime import datetime
from typing import Iterator

import pypff

from .models import Email, ParsedEmail, RawAttachment


def parse_pst(pst_path: str) -> Iterator[ParsedEmail]:
    """Parse a PST file and yield ParsedEmail objects.

    Recursively traverses all folders, extracts email metadata, body text,
    and raw attachment data.
    """
    pst = pypff.file()
    pst.open(pst_path)

    try:
        root = pst.get_root_folder()
        yield from _process_folder(root)
    finally:
        pst.close()


def _process_folder(folder: pypff.folder) -> Iterator[ParsedEmail]:
    """Recursively process a PST folder."""
    # Process messages in this folder
    for i in range(folder.number_of_sub_messages):
        try:
            message = folder.get_sub_message(i)
            parsed = _extract_message(message)
            if parsed is not None:
                yield parsed
        except Exception:
            # Skip problematic messages
            continue

    # Recurse into sub-folders
    for i in range(folder.number_of_sub_folders):
        try:
            sub_folder = folder.get_sub_folder(i)
            yield from _process_folder(sub_folder)
        except Exception:
            continue


def _extract_message(message: pypff.message) -> ParsedEmail | None:
    """Extract email data from a PST message."""
    try:
        subject = message.subject or ""
        sender_email = message.sender_email_address or ""
        sender_name = message.sender_name or ""

        # Get body text — prefer plain text, fall back to HTML
        body = message.plain_text_body
        if body is None:
            body = message.html_body
        if isinstance(body, bytes):
            body = body.decode("utf-8", errors="replace")
        body = body or ""

        # Date
        submit_time = message.client_submit_time
        if submit_time is None:
            submit_time = datetime.utcnow()

        # Message ID
        message_id = message.transport_message_headers or ""
        # Try to extract Message-ID from headers
        if message_id:
            for line in message_id.split("\n"):
                if line.lower().startswith("message-id:"):
                    message_id = line.split(":", 1)[1].strip()
                    break
            else:
                message_id = ""

        if not message_id:
            message_id = f"<pst-{uuid.uuid4()}@local>"

        # Recipients
        recipients: list[str] = []
        try:
            recipients_obj = message.get_recipients()
            if recipients_obj:
                for j in range(recipients_obj.number_of_records):
                    try:
                        record = recipients_obj.get_record(j)
                        # Try to get email from record entries
                        email_addr = None
                        for k in range(record.number_of_entries):
                            entry = record.get_entry(k)
                            entry_type = entry.entry_type
                            # 0x39FE = SMTP address, 0x3003 = Email address
                            if entry_type in (0x39FE, 0x3003):
                                try:
                                    email_addr = entry.get_data_as_string()
                                except Exception:
                                    pass
                                if email_addr:
                                    break
                        if email_addr:
                            recipients.append(email_addr)
                    except Exception:
                        continue
        except Exception:
            pass

        # Attachments
        attachments: list[RawAttachment] = []
        has_attachments = False
        try:
            num_attachments = message.number_of_attachments
            has_attachments = num_attachments > 0
            for j in range(num_attachments):
                try:
                    att = message.get_attachment(j)
                    filename = att.name or "unknown"
                    try:
                        content = att.read_buffer(att.size) if att.size > 0 else b""
                    except Exception:
                        content = b""

                    attachments.append(
                        RawAttachment(
                            filename=filename,
                            mime_type=None,  # libpff doesn't expose MIME type directly
                            content=content,
                            size_bytes=att.size or 0,
                        )
                    )
                except Exception:
                    continue
        except Exception:
            pass

        email = Email(
            message_id=message_id,
            date=submit_time,
            sender=sender_email,
            sender_name=sender_name,
            recipients=recipients,
            subject=subject,
            body_text=body,
            has_attachments=has_attachments,
        )

        return ParsedEmail(email=email, attachments=attachments)

    except Exception:
        return None
