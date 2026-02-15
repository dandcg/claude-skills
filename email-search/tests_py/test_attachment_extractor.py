"""Tests for attachment text extraction."""

from email_search.attachment_extractor import can_extract, extract_text, get_mime_type


class TestCanExtract:
    def test_pdf(self):
        assert can_extract("report.pdf") is True

    def test_docx(self):
        assert can_extract("document.docx") is True

    def test_xlsx(self):
        assert can_extract("spreadsheet.xlsx") is True

    def test_txt(self):
        assert can_extract("readme.txt") is True

    def test_csv(self):
        assert can_extract("data.csv") is True

    def test_unsupported(self):
        assert can_extract("image.png") is False
        assert can_extract("archive.zip") is False

    def test_mime_type_fallback(self):
        assert can_extract("unknown", "application/pdf") is True
        assert can_extract("unknown", "text/plain") is True


class TestGetMimeType:
    def test_pdf(self):
        assert get_mime_type("file.pdf") == "application/pdf"

    def test_docx(self):
        assert "wordprocessingml" in get_mime_type("file.docx")

    def test_unknown(self):
        assert get_mime_type("file.xyz") == "application/octet-stream"


class TestExtractText:
    def test_plain_text(self):
        content = b"Hello, this is plain text content."
        result = extract_text("readme.txt", content)
        assert result == "Hello, this is plain text content."

    def test_csv(self):
        content = b"name,age\nAlice,30\nBob,25"
        result = extract_text("data.csv", content)
        assert "Alice" in result
        assert "Bob" in result

    def test_empty_content(self):
        assert extract_text("file.txt", b"") is None

    def test_unsupported_extension(self):
        assert extract_text("image.png", b"some data") is None

    def test_whitespace_only(self):
        assert extract_text("file.txt", b"   \n  \n  ") is None
