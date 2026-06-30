using System.Text;
using ClosedXML.Excel;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Reports;

namespace ResourceIQ.Jcs.Infrastructure.Reports;

/// <summary>
/// Renders a <see cref="ReportTable"/> to CSV (UTF-8 WITH BOM, so Excel reads Arabic correctly) or
/// a true XLSX workbook (RTL sheet) via ClosedXML — a pure-managed library (no native/headless deps).
/// </summary>
public sealed class ReportExporter : IReportExporter
{
    private const string CsvType = "text/csv";
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public ExportResult Export(ReportTable table, ExportFormat format) =>
        format == ExportFormat.Xlsx ? Xlsx(table) : Csv(table);

    private static ExportResult Csv(ReportTable table)
    {
        var sb = new StringBuilder();
        sb.Append('﻿'); // UTF-8 BOM → bytes EF BB BF
        sb.Append(string.Join(',', table.Headers.Select(Escape))).Append("\r\n");
        foreach (var row in table.Rows)
            sb.Append(string.Join(',', row.Select(Escape))).Append("\r\n");

        // Encoding without an extra BOM (the BOM char is already in the string).
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(sb.ToString());
        return new ExportResult(bytes, CsvType, "csv");

        static string Escape(string field)
        {
            field ??= string.Empty;
            if (field.Contains('"') || field.Contains(',') || field.Contains('\n') || field.Contains('\r'))
                return $"\"{field.Replace("\"", "\"\"")}\"";
            return field;
        }
    }

    private static ExportResult Xlsx(ReportTable table)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(SafeSheetName(table.Title));
        ws.RightToLeft = true;

        for (var c = 0; c < table.Headers.Count; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = table.Headers[c];
            cell.Style.Font.Bold = true;
        }

        for (var r = 0; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            for (var c = 0; c < row.Count; c++)
                ws.Cell(r + 2, c + 1).Value = row[c];
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return new ExportResult(ms.ToArray(), XlsxType, "xlsx");
    }

    // Excel sheet names: max 31 chars, and none of : \ / ? * [ ]
    private static string SafeSheetName(string title)
    {
        var cleaned = new string(title.Where(ch => ch is not (':' or '\\' or '/' or '?' or '*' or '[' or ']')).ToArray());
        if (string.IsNullOrWhiteSpace(cleaned)) cleaned = "Report";
        return cleaned.Length > 31 ? cleaned[..31] : cleaned;
    }
}
