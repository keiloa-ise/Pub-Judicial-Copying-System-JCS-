using ResourceIQ.Jcs.Application.Reports;

namespace ResourceIQ.Jcs.Application.Abstractions;

/// <summary>Serialized export payload: the file bytes plus how to serve them.</summary>
public sealed record ExportResult(byte[] Content, string ContentType, string FileExtension);

/// <summary>
/// Renders a flat <see cref="ReportTable"/> to a downloadable file. CSV is UTF-8 WITH BOM (so Excel
/// reads Arabic correctly); XLSX is a true workbook (RTL sheet). Implemented in Infrastructure.
/// </summary>
public interface IReportExporter
{
    ExportResult Export(ReportTable table, ExportFormat format);
}
