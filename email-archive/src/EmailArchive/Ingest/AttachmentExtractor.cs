using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace EmailArchive.Ingest;

public class AttachmentExtractor
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".xlsx", ".txt", ".text", ".csv"
    };

    private static readonly Dictionary<string, string> ExtensionToMimeType = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".doc"] = "application/msword",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".xls"] = "application/vnd.ms-excel",
        [".txt"] = "text/plain",
        [".text"] = "text/plain",
        [".csv"] = "text/csv"
    };

    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "text/plain",
        "text/csv"
    };

    /// <summary>
    /// Check if text can be extracted from this attachment type.
    /// </summary>
    public bool CanExtract(string filename, string? mimeType)
    {
        var extension = Path.GetExtension(filename);

        if (SupportedExtensions.Contains(extension))
            return true;

        if (!string.IsNullOrEmpty(mimeType) && SupportedMimeTypes.Contains(mimeType))
            return true;

        return false;
    }

    /// <summary>
    /// Get MIME type from filename extension.
    /// </summary>
    public string GetMimeType(string filename)
    {
        var extension = Path.GetExtension(filename);
        return ExtensionToMimeType.GetValueOrDefault(extension, "application/octet-stream");
    }

    /// <summary>
    /// Extract text from attachment bytes.
    /// </summary>
    public string? ExtractText(string filename, string? mimeType, byte[] content)
    {
        if (content.Length == 0)
            return null;

        if (!CanExtract(filename, mimeType))
            return null;

        var extension = Path.GetExtension(filename).ToLowerInvariant();
        var effectiveMimeType = mimeType ?? GetMimeType(filename);

        try
        {
            return extension switch
            {
                ".pdf" => ExtractFromPdf(content),
                ".docx" => ExtractFromDocx(content),
                ".xlsx" => ExtractFromXlsx(content),
                ".txt" or ".text" or ".csv" => ExtractFromText(content),
                _ when effectiveMimeType == "application/pdf" => ExtractFromPdf(content),
                _ when effectiveMimeType.Contains("wordprocessingml") => ExtractFromDocx(content),
                _ when effectiveMimeType.Contains("spreadsheetml") => ExtractFromXlsx(content),
                _ when effectiveMimeType.StartsWith("text/") => ExtractFromText(content),
                _ => null
            };
        }
        catch
        {
            // If extraction fails, return null rather than crashing
            return null;
        }
    }

    private string? ExtractFromPdf(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var document = PdfDocument.Open(stream);

        var sb = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            sb.AppendLine(page.Text);
        }

        var text = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private string? ExtractFromDocx(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var doc = WordprocessingDocument.Open(stream, false);

        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null)
            return null;

        var sb = new StringBuilder();
        foreach (var para in body.Elements<Paragraph>())
        {
            sb.AppendLine(para.InnerText);
        }

        var text = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private string? ExtractFromXlsx(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var workbook = new XLWorkbook(stream);

        var sb = new StringBuilder();
        foreach (var worksheet in workbook.Worksheets)
        {
            sb.AppendLine($"--- {worksheet.Name} ---");

            var usedRange = worksheet.RangeUsed();
            if (usedRange is null)
                continue;

            foreach (var row in usedRange.Rows())
            {
                var cells = row.Cells().Select(c => c.GetString());
                sb.AppendLine(string.Join("\t", cells));
            }
        }

        var text = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private string? ExtractFromText(byte[] content)
    {
        var text = Encoding.UTF8.GetString(content).Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
