"""CLI entry point using Click and Rich for display."""

from __future__ import annotations

import os
import sys
from datetime import datetime, timedelta

import click
from rich.console import Console
from rich.panel import Panel
from rich.progress import Progress
from rich.table import Table

from . import attachment_extractor, email_filter, markdown_formatter
from .models import Attachment, Tier
from .pst_parser import parse_pst
from .store import Store

console = Console()

DEFAULT_DATA_DIR = os.environ.get("EMAIL_SEARCH_DATA_DIR", "./email-search-data")


def _get_store() -> Store:
    return Store(data_dir=DEFAULT_DATA_DIR)


@click.group()
def main():
    """Process email archives into a searchable vector database."""
    pass


# ── ingest ────────────────────────────────────────────────────────────


@main.command()
@click.argument("pst_path", type=click.Path(exists=True))
def ingest(pst_path: str):
    """Ingest a PST file into the archive."""
    if not pst_path.lower().endswith(".pst"):
        console.print("[yellow]Warning: File does not have .pst extension[/]")

    try:
        store = _get_store()

        counts = {
            "total": 0,
            "excluded": 0,
            "metadata_only": 0,
            "vectorize": 0,
            "attachments": 0,
            "attachments_with_text": 0,
        }

        console.print(f"[bold]Ingesting:[/] {pst_path}")
        console.print()

        with Progress(console=console) as progress:
            task = progress.add_task("[cyan]Processing emails...", total=None)

            for parsed in parse_pst(pst_path):
                counts["total"] += 1
                email = parsed.email

                # Classify
                tier = email_filter.classify(email, email.has_attachments)
                email.tier = tier

                # Skip Tier 1 entirely
                if tier == Tier.EXCLUDED:
                    counts["excluded"] += 1
                    progress.update(task, description=f"[cyan]Processed {counts['total']:,} emails...")
                    continue

                # Store Tier 2 and 3
                store.insert_email(email)

                if tier == Tier.METADATA_ONLY:
                    counts["metadata_only"] += 1
                else:
                    counts["vectorize"] += 1

                    # Process attachments for Tier 3
                    if email.has_attachments and parsed.attachments:
                        for raw_att in parsed.attachments:
                            counts["attachments"] += 1

                            extracted_text = None
                            if raw_att.content:
                                extracted_text = attachment_extractor.extract_text(
                                    raw_att.filename,
                                    raw_att.content,
                                    raw_att.mime_type,
                                )

                            if extracted_text:
                                counts["attachments_with_text"] += 1

                            att = Attachment(
                                email_id=email.id,
                                filename=raw_att.filename,
                                mime_type=raw_att.mime_type or attachment_extractor.get_mime_type(raw_att.filename),
                                size_bytes=raw_att.size_bytes,
                                extracted_text=extracted_text,
                            )
                            store.insert_attachment(att, email)

                progress.update(
                    task,
                    description=f"[cyan]Processed {counts['total']:,} emails, {counts['attachments']:,} attachments...",
                )

        console.print()
        console.print("[bold green]Ingest complete![/]")
        console.print(f"  Total emails: [bold]{counts['total']:,}[/]")
        console.print(f"  Excluded (Tier 1): [grey50]{counts['excluded']:,}[/]")
        console.print(f"  Metadata only (Tier 2): {counts['metadata_only']:,}")
        console.print(f"  Vectorised (Tier 3): [green]{counts['vectorize']:,}[/]")
        console.print(
            f"  Attachments: [bold]{counts['attachments']:,}[/] "
            f"({counts['attachments_with_text']:,} with extracted text)"
        )
        console.print()
        console.print(
            "[dim]Tier 3 emails and attachments with text are automatically "
            "embedded by ChromaDB — no separate embed step needed.[/]"
        )

    except Exception as e:
        console.print(f"[red]Error: {e}[/]")
        sys.exit(1)


# ── status ────────────────────────────────────────────────────────────


@main.command()
def status():
    """Show current archive status."""
    try:
        store = _get_store()
        counts = store.get_status_counts()
        att_count = store.get_attachment_count()
        att_with_text = store.get_attachments_with_text_count()
        att_embedded = store.get_attachments_embedded_count()

        table = Table(title="Email Archive Status")
        table.add_column("Category", justify="center")
        table.add_column("Count", justify="right")

        table.add_row("Total Emails", f"{counts.total:,}")
        table.add_row("[grey50]Tier 1 (Excluded)[/]", f"[grey50]{counts.excluded:,}[/]")
        table.add_row("Tier 2 (Metadata Only)", f"{counts.metadata_only:,}")
        table.add_row("Tier 3 (Vectorised)", f"{counts.vectorize:,}")
        table.add_row("[green]  Emails Embedded[/]", f"[green]{counts.embedded:,}[/]")
        table.add_row("", "")
        table.add_row("[bold]Attachments[/]", f"[bold]{att_count:,}[/]")
        table.add_row("  With Extracted Text", f"{att_with_text:,}")
        table.add_row("[green]  Attachments Embedded[/]", f"[green]{att_embedded:,}[/]")

        console.print(table)

    except Exception as e:
        console.print(f"[red]Error: {e}[/]")


# ── search ────────────────────────────────────────────────────────────


@main.command()
@click.argument("query")
@click.option("--limit", default=10, help="Maximum results to return")
@click.option("--emails-only", is_flag=True, help="Only search emails")
@click.option("--attachments-only", is_flag=True, help="Only search attachments")
@click.option("--from", "start_date", type=click.DateTime(formats=["%Y-%m-%d"]), help="Filter from date")
@click.option("--to", "end_date", type=click.DateTime(formats=["%Y-%m-%d"]), help="Filter until date")
@click.option("--sender", help="Filter by sender name or email")
def search(
    query: str,
    limit: int,
    emails_only: bool,
    attachments_only: bool,
    start_date: datetime | None,
    end_date: datetime | None,
    sender: str | None,
):
    """Search emails and attachments using natural language."""
    try:
        store = _get_store()

        console.print(f"[dim]Searching for:[/] [bold]{query}[/]")
        console.print()

        # Search emails
        if not attachments_only:
            email_results = store.search_emails(
                query, limit, start_date, end_date, sender
            )

            if email_results:
                console.print(f"[bold cyan]Emails ({len(email_results)} results)[/]")
                console.print()

                for result in email_results:
                    _display_email_result(result)
            else:
                console.print("[dim]No matching emails found.[/]")

        # Search attachments
        if not emails_only:
            att_results = store.search_attachments(query, limit)

            if att_results:
                console.print()
                console.print(f"[bold cyan]Attachments ({len(att_results)} results)[/]")
                console.print()

                for result in att_results:
                    _display_attachment_result(result)
            elif not attachments_only:
                console.print()
                console.print("[dim]No matching attachments found.[/]")

    except Exception as e:
        console.print(f"[red]Error: {e}[/]")


def _display_email_result(result):
    from .store import EmailSearchResult

    color = "green" if result.similarity >= 0.8 else "yellow" if result.similarity >= 0.6 else "dim"

    content = (
        f"[bold]{_escape(result.subject)}[/]\n"
        f"[dim]{result.date:%Y-%m-%d %H:%M}[/] | [blue]{_escape(result.sender_name)}[/] <{_escape(result.sender)}>\n\n"
        f"{_escape(result.body_snippet)}"
    )

    panel = Panel(
        content,
        title=f"[{color}]{result.similarity:.0%} match[/]",
        border_style="rounded",
        padding=(0, 1),
    )
    console.print(panel)
    console.print()


def _display_attachment_result(result):
    from .store import AttachmentSearchResult

    color = "green" if result.similarity >= 0.8 else "yellow" if result.similarity >= 0.6 else "dim"

    content = (
        f"[bold]{_escape(result.filename)}[/]\n"
        f"[dim]From email:[/] {_escape(result.email_subject)} ({result.email_date:%Y-%m-%d})\n"
        f"[dim]Sender:[/] {_escape(result.email_sender)}\n\n"
        f"{_escape(result.text_snippet)}"
    )

    panel = Panel(
        content,
        title=f"[{color}]{result.similarity:.0%} match[/]",
        border_style="rounded",
        padding=(0, 1),
    )
    console.print(panel)
    console.print()


def _escape(text: str) -> str:
    """Escape Rich markup characters."""
    return text.replace("[", "\\[").replace("]", "\\]")


# ── analytics ─────────────────────────────────────────────────────────


@main.group()
def analytics():
    """Analyse email patterns and statistics."""
    pass


@analytics.command()
def summary():
    """Show archive overview and statistics."""
    try:
        store = _get_store()

        summ = store.get_archive_summary()
        hourly = store.get_activity_by_hour()
        daily = store.get_activity_by_day_of_week()

        summary_text = (
            f"[bold]Total Emails:[/] {summ.total_emails:,}\n"
            f"[bold]Unique Contacts:[/] {summ.unique_contacts:,}\n"
            f"[bold]Date Range:[/] {summ.earliest_email:%Y-%m-%d} to {summ.latest_email:%Y-%m-%d}\n"
            f"[bold]Time Span:[/] {summ.total_years_span} years\n"
            f"[bold]Avg Emails/Day:[/] {summ.avg_emails_per_day:.1f}"
        )

        panel = Panel(summary_text, title="[bold cyan]Archive Summary[/]", padding=(1, 2))
        console.print(panel)
        console.print()

        # Activity by hour
        if hourly:
            console.print("[bold cyan]Activity by Hour[/]")
            _print_bar_chart([(f"{s.hour:02d}:00", s.email_count) for s in hourly])
            console.print()

        # Activity by day of week
        if daily:
            console.print("[bold cyan]Activity by Day of Week[/]")
            day_names = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"]
            _print_bar_chart(
                [(day_names[s.day_of_week] if 0 <= s.day_of_week < 7 else str(s.day_of_week), s.email_count) for s in daily]
            )

    except Exception as e:
        console.print(f"[red]Error: {e}[/]")


@analytics.command()
@click.option("--monthly", is_flag=True, help="Group by month instead of year")
@click.option("--year", type=int, help="Filter to specific year")
def timeline(monthly: bool, year: int | None):
    """Show email volume over time."""
    try:
        store = _get_store()

        periods = store.get_timeline(group_by_month=monthly)

        if year is not None:
            periods = [p for p in periods if p.year == year]

        if not periods:
            console.print("[yellow]No email data found.[/]")
            return

        console.print("[bold cyan]Email Timeline[/]")
        console.print()

        # Bar chart
        labels = []
        for p in periods:
            if monthly and p.month is not None:
                labels.append((f"{p.year}-{p.month:02d}", p.email_count))
            else:
                labels.append((str(p.year), p.email_count))

        _print_bar_chart(labels)
        console.print()

        # Detail table
        table = Table()
        table.add_column("Period")
        table.add_column("Total", justify="right")
        table.add_column("Sent", justify="right")
        table.add_column("Received", justify="right")

        for p in periods:
            label = f"{p.year}-{p.month:02d}" if monthly and p.month else str(p.year)
            table.add_row(
                label,
                f"{p.email_count:,}",
                f"[blue]{p.sent_count:,}[/]",
                f"[green]{p.received_count:,}[/]",
            )

        console.print(table)

    except Exception as e:
        console.print(f"[red]Error: {e}[/]")


@analytics.command()
@click.option("--limit", default=20, help="Number of contacts to show")
def contacts(limit: int):
    """Show top contacts by email volume."""
    try:
        store = _get_store()

        contact_list = store.get_top_contacts(limit)

        if not contact_list:
            console.print("[yellow]No contacts found.[/]")
            return

        console.print(f"[bold cyan]Top {len(contact_list)} Contacts[/]")
        console.print()

        table = Table()
        table.add_column("#")
        table.add_column("Contact")
        table.add_column("Total", justify="right")
        table.add_column("Sent", justify="right")
        table.add_column("Received", justify="right")
        table.add_column("First")
        table.add_column("Last")

        for rank, contact in enumerate(contact_list, 1):
            display_name = contact.name if contact.name and contact.name != contact.email.split("@")[0] else ""
            if display_name:
                name_col = f"{_escape(display_name)}\n[dim]{_escape(contact.email)}[/]"
            else:
                name_col = _escape(contact.email)

            table.add_row(
                str(rank),
                name_col,
                f"[bold]{contact.total_emails:,}[/]",
                f"[blue]{contact.sent_to:,}[/]",
                f"[green]{contact.received_from:,}[/]",
                f"{contact.first_contact:%Y-%m-%d}",
                f"{contact.last_contact:%Y-%m-%d}",
            )

        console.print(table)

    except Exception as e:
        console.print(f"[red]Error: {e}[/]")


# ── export ────────────────────────────────────────────────────────────


@main.group()
def export():
    """Export email data to second brain markdown."""
    pass


@export.command("contacts")
@click.option("-o", "--output", help="Output file path (default: stdout)")
@click.option("-n", "--limit", default=20, help="Number of top contacts")
@click.option("--min-emails", default=5, help="Minimum emails to include")
def export_contacts(output: str | None, limit: int, min_emails: int):
    """Export top contacts to markdown."""
    try:
        store = _get_store()

        all_contacts = store.get_contacts_for_period(
            datetime.min,
            datetime.max,
            limit + 100,
        )

        filtered = [c for c in all_contacts if c.total_emails >= min_emails][:limit]

        if not filtered:
            console.print("[yellow]No contacts found matching criteria.[/]")
            return

        md = _generate_contacts_markdown(filtered)

        if output:
            with open(output, "w") as f:
                f.write(md)
            console.print(f"[green]Exported {len(filtered)} contacts to {output}[/]")
        else:
            console.print(md)

    except Exception as e:
        console.print(f"[red]Error: {e}[/]")


@export.command("review")
@click.option("-p", "--period", default="week", help="Review period: 'week' or 'month'")
@click.option("-d", "--date", "date_str", help="Date within period (YYYY-MM-DD)")
@click.option("-o", "--output", help="Output file path (default: stdout)")
def export_review(period: str, date_str: str | None, output: str | None):
    """Export email activity for weekly or monthly review."""
    try:
        # Parse date
        if date_str:
            try:
                target_date = datetime.strptime(date_str, "%Y-%m-%d")
            except ValueError:
                console.print("[red]Error: Invalid date format. Use YYYY-MM-DD.[/]")
                return
        else:
            target_date = datetime.utcnow().replace(hour=0, minute=0, second=0, microsecond=0)

        period = period.lower()
        if period not in ("week", "month"):
            console.print("[red]Error: Period must be 'week' or 'month'.[/]")
            return

        # Calculate period bounds
        if period == "month":
            period_start = target_date.replace(day=1)
            if period_start.month == 12:
                period_end = period_start.replace(year=period_start.year + 1, month=1)
            else:
                period_end = period_start.replace(month=period_start.month + 1)
            period_label = f"Monthly Review: {period_start:%B %Y}"
        else:
            # ISO week: Monday to next Monday
            days_from_monday = target_date.weekday()  # Monday=0
            period_start = target_date - timedelta(days=days_from_monday)
            period_end = period_start + timedelta(days=7)
            iso_year, iso_week, _ = period_start.isocalendar()
            period_label = f"Weekly Review: {iso_year}-W{iso_week:02d}"

        store = _get_store()
        review_data = store.get_review_data(period_start, period_end, 10)

        md = _generate_review_markdown(review_data, period_label)

        if output:
            with open(output, "w") as f:
                f.write(md)
            console.print(f"[green]Exported {period} review to {output}[/]")
        else:
            console.print(md)

        # Show summary
        console.print(f"[blue]Period:[/] {period_start:%Y-%m-%d} to {(period_end - timedelta(days=1)):%Y-%m-%d}")
        console.print(f"[blue]Total Emails:[/] {review_data.email_count}")
        console.print(f"[blue]Sent:[/] {review_data.sent_count} | [blue]Received:[/] {review_data.received_count}")
        console.print(f"[blue]Top Contacts:[/] {len(review_data.top_contacts)}")

    except Exception as e:
        console.print(f"[red]Error: {e}[/]")


# ── Helpers ───────────────────────────────────────────────────────────


def _generate_contacts_markdown(contacts: list) -> str:
    lines = [
        markdown_formatter.format_ideas_header("Email Contacts", datetime.now(), "developing"),
        "## Core Idea",
        "Top email contacts extracted from email archive for relationship tracking.",
        "",
        "## Contacts",
        "",
    ]
    for contact in contacts:
        lines.append(markdown_formatter.format_contact_section(contact))

    lines.extend([
        "## Open Questions",
        "- Which contacts need more attention?",
        "- Are there relationships that have gone cold?",
        "",
    ])

    return "\n".join(lines)


def _generate_review_markdown(review_data, period_label: str) -> str:
    lines = [
        f"<!-- {period_label} -->",
        f"<!-- Generated: {datetime.utcnow():%Y-%m-%d %H:%M:%S} UTC -->",
        "",
        markdown_formatter.format_review_email_section(review_data),
    ]
    return "\n".join(lines)


def _print_bar_chart(data: list[tuple[str, int]], max_width: int = 40) -> None:
    """Print a simple horizontal bar chart."""
    if not data:
        return

    max_val = max(v for _, v in data)
    if max_val == 0:
        max_val = 1

    max_label = max(len(label) for label, _ in data)

    for label, value in data:
        bar_len = int(value / max_val * max_width)
        bar = "█" * bar_len
        console.print(f"  {label:>{max_label}} │ {bar} {value:,}")


if __name__ == "__main__":
    main()
