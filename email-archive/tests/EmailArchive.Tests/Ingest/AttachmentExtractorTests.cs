using EmailArchive.Ingest;
using Xunit;

namespace EmailArchive.Tests.Ingest;

public class AttachmentExtractorTests
{
    private readonly AttachmentExtractor _extractor = new();

    [Fact]
    public void CanExtract_PdfFile_ReturnsTrue()
    {
        Assert.True(_extractor.CanExtract("document.pdf", "application/pdf"));
    }

    [Fact]
    public void CanExtract_WordFile_ReturnsTrue()
    {
        Assert.True(_extractor.CanExtract("document.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"));
    }

    [Fact]
    public void CanExtract_ExcelFile_ReturnsTrue()
    {
        Assert.True(_extractor.CanExtract("spreadsheet.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
    }

    [Fact]
    public void CanExtract_TextFile_ReturnsTrue()
    {
        Assert.True(_extractor.CanExtract("readme.txt", "text/plain"));
    }

    [Fact]
    public void CanExtract_ImageFile_ReturnsFalse()
    {
        Assert.False(_extractor.CanExtract("photo.jpg", "image/jpeg"));
    }

    [Fact]
    public void CanExtract_ZipFile_ReturnsFalse()
    {
        Assert.False(_extractor.CanExtract("archive.zip", "application/zip"));
    }

    [Fact]
    public void CanExtract_ByExtensionOnly_ReturnsTrue()
    {
        // Should work even without MIME type if extension is recognized
        Assert.True(_extractor.CanExtract("document.pdf", null));
        Assert.True(_extractor.CanExtract("document.docx", null));
        Assert.True(_extractor.CanExtract("spreadsheet.xlsx", null));
    }

    [Fact]
    public void GetMimeType_FromExtension_ReturnsCorrectType()
    {
        Assert.Equal("application/pdf", _extractor.GetMimeType("document.pdf"));
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", _extractor.GetMimeType("document.docx"));
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", _extractor.GetMimeType("spreadsheet.xlsx"));
        Assert.Equal("text/plain", _extractor.GetMimeType("readme.txt"));
    }

    [Fact]
    public void ExtractText_PlainTextBytes_ReturnsText()
    {
        var content = "Hello, this is plain text content.";
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);

        var result = _extractor.ExtractText("readme.txt", "text/plain", bytes);

        Assert.Equal(content, result);
    }

    [Fact]
    public void ExtractText_UnsupportedType_ReturnsNull()
    {
        var bytes = new byte[] { 0x00, 0x01, 0x02 };

        var result = _extractor.ExtractText("photo.jpg", "image/jpeg", bytes);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractText_EmptyBytes_ReturnsNull()
    {
        var bytes = Array.Empty<byte>();

        var result = _extractor.ExtractText("readme.txt", "text/plain", bytes);

        Assert.Null(result);
    }
}
