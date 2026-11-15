using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using SourcingTenderManagementSystem.Web.Models.Reports;
namespace SourcingTenderManagementSystem.Web.Data;

public static class ReportExports
{
    public static byte[] Excel(ReportDocument report)
    {
        var tables = new[] { new ReportTable("Overview", ["Field", "Value"], new[] { new[] { new ReportCell("Report"), new ReportCell(report.Title) }, new[] { new ReportCell("Generated UTC"), new ReportCell(report.GeneratedAt.ToString("O")) } }.Concat(report.Metrics.Select(m => new[] { new ReportCell(m.Label), new ReportCell(m.Value) })).Concat(report.Notes.Select(n => new[] { new ReportCell("Note"), new ReportCell(n) })).ToArray()) }.Concat(report.Tables).ToArray();
        using var result = new MemoryStream();
        using (var zip = new ZipArchive(result, ZipArchiveMode.Create, true))
        {
            void Xml(string path, Action<XmlWriter> content) { using var stream = zip.CreateEntry(path, CompressionLevel.Optimal).Open(); using var xml = XmlWriter.Create(stream, new() { Encoding = new UTF8Encoding(false), CloseOutput = false }); content(xml); }
            const string main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main", rel = "http://schemas.openxmlformats.org/package/2006/relationships", docRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            Xml("[Content_Types].xml", x => { x.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types"); void Type(string tag, string key, string name, string type) { x.WriteStartElement(tag); x.WriteAttributeString(key, name); x.WriteAttributeString("ContentType", type); x.WriteEndElement(); } Type("Default", "Extension", "rels", "application/vnd.openxmlformats-package.relationships+xml"); Type("Default", "Extension", "xml", "application/xml"); Type("Override", "PartName", "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"); Type("Override", "PartName", "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"); for (int i = 0; i < tables.Length; i++) Type("Override", "PartName", $"/xl/worksheets/sheet{i + 1}.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"); x.WriteEndElement(); });
            void Relation(XmlWriter x, string id, string type, string target) { x.WriteStartElement("Relationship"); x.WriteAttributeString("Id", id); x.WriteAttributeString("Type", docRel + "/" + type); x.WriteAttributeString("Target", target); x.WriteEndElement(); }
            Xml("_rels/.rels", x => { x.WriteStartElement("Relationships", rel); Relation(x, "rId1", "officeDocument", "xl/workbook.xml"); x.WriteEndElement(); });
            Xml("xl/workbook.xml", x => { x.WriteStartElement("workbook", main); x.WriteStartElement("sheets"); for (int i = 0; i < tables.Length; i++) { x.WriteStartElement("sheet"); x.WriteAttributeString("name", $"{i + 1} " + Regex.Replace(tables[i].Title, @"[\\/*?:\[\]]", "")[..Math.Min(26, Regex.Replace(tables[i].Title, @"[\\/*?:\[\]]", "").Length)]); x.WriteAttributeString("sheetId", (i + 1).ToString(CultureInfo.InvariantCulture)); x.WriteAttributeString("r", "id", docRel, "rId" + (i + 1)); x.WriteEndElement(); } x.WriteEndElement(); x.WriteEndElement(); });
            Xml("xl/_rels/workbook.xml.rels", x => { x.WriteStartElement("Relationships", rel); for (int i = 0; i < tables.Length; i++) Relation(x, "rId" + (i + 1), "worksheet", $"worksheets/sheet{i + 1}.xml"); Relation(x, "rIdStyles", "styles", "styles.xml"); x.WriteEndElement(); });
            Xml("xl/styles.xml", x =>
            {
                x.WriteStartElement("styleSheet", main);
                x.WriteStartElement("numFmts"); x.WriteAttributeString("count", "1"); x.WriteStartElement("numFmt"); x.WriteAttributeString("numFmtId", "164"); x.WriteAttributeString("formatCode", "0.######"); x.WriteEndElement(); x.WriteEndElement();
                x.WriteStartElement("fonts"); x.WriteAttributeString("count", "2"); for (int i = 0; i < 2; i++) { x.WriteStartElement("font"); if (i == 1) x.WriteElementString("b", ""); x.WriteStartElement("sz"); x.WriteAttributeString("val", "11"); x.WriteEndElement(); x.WriteStartElement("name"); x.WriteAttributeString("val", "Segoe UI"); x.WriteEndElement(); if (i == 1) { x.WriteStartElement("color"); x.WriteAttributeString("rgb", "FFFFFFFF"); x.WriteEndElement(); } x.WriteEndElement(); }
                x.WriteEndElement();
                x.WriteStartElement("fills"); x.WriteAttributeString("count", "3"); foreach (var pattern in new[] { "none", "gray125", "solid" }) { x.WriteStartElement("fill"); x.WriteStartElement("patternFill"); x.WriteAttributeString("patternType", pattern); if (pattern == "solid") { x.WriteStartElement("fgColor"); x.WriteAttributeString("rgb", "FF152B40"); x.WriteEndElement(); x.WriteStartElement("bgColor"); x.WriteAttributeString("indexed", "64"); x.WriteEndElement(); } x.WriteEndElement(); x.WriteEndElement(); }
                x.WriteEndElement();
                x.WriteStartElement("borders"); x.WriteAttributeString("count", "1"); x.WriteElementString("border", ""); x.WriteEndElement();
                void Style(int font, int fill, int format) { x.WriteStartElement("xf"); x.WriteAttributeString("numFmtId", format.ToString()); x.WriteAttributeString("fontId", font.ToString()); x.WriteAttributeString("fillId", fill.ToString()); x.WriteAttributeString("borderId", "0"); x.WriteAttributeString("xfId", "0"); x.WriteAttributeString("applyAlignment", "1"); if (font > 0) x.WriteAttributeString("applyFont", "1"); if (fill > 0) x.WriteAttributeString("applyFill", "1"); if (format > 0) x.WriteAttributeString("applyNumberFormat", "1"); x.WriteStartElement("alignment"); x.WriteAttributeString("vertical", "top"); x.WriteAttributeString("wrapText", "1"); x.WriteEndElement(); x.WriteEndElement(); }
                x.WriteStartElement("cellStyleXfs"); x.WriteAttributeString("count", "1"); x.WriteStartElement("xf"); x.WriteAttributeString("numFmtId", "0"); x.WriteAttributeString("fontId", "0"); x.WriteAttributeString("fillId", "0"); x.WriteAttributeString("borderId", "0"); x.WriteEndElement(); x.WriteEndElement();
                x.WriteStartElement("cellXfs"); x.WriteAttributeString("count", "3"); Style(0, 0, 0); Style(1, 2, 0); Style(0, 0, 164); x.WriteEndElement();
                x.WriteStartElement("cellStyles"); x.WriteAttributeString("count", "1"); x.WriteStartElement("cellStyle"); x.WriteAttributeString("name", "Normal"); x.WriteAttributeString("xfId", "0"); x.WriteAttributeString("builtinId", "0"); x.WriteEndElement(); x.WriteEndElement(); x.WriteEndElement();
            });
            for (int i = 0; i < tables.Length; i++)
            {
                var table = tables[i]; ReportRules.Limit(table.Rows.Count);
                Xml($"xl/worksheets/sheet{i + 1}.xml", x =>
                {
                    x.WriteStartElement("worksheet", main); x.WriteStartElement("sheetViews"); x.WriteStartElement("sheetView"); x.WriteAttributeString("workbookViewId", "0"); x.WriteStartElement("pane"); x.WriteAttributeString("ySplit", "1"); x.WriteAttributeString("topLeftCell", "A2"); x.WriteAttributeString("activePane", "bottomLeft"); x.WriteAttributeString("state", "frozen"); x.WriteEndElement(); x.WriteEndElement(); x.WriteEndElement();
                    x.WriteStartElement("cols"); for (int c = 0; c < table.Columns.Length; c++) { x.WriteStartElement("col"); x.WriteAttributeString("min", (c + 1).ToString()); x.WriteAttributeString("max", (c + 1).ToString()); x.WriteAttributeString("width", Math.Clamp(table.Columns[c].Length + 8, 18, 55).ToString()); x.WriteAttributeString("customWidth", "1"); x.WriteEndElement(); }
                    x.WriteEndElement();
                    x.WriteStartElement("sheetData"); var rows = new[] { table.Columns.Select(c => new ReportCell(c)).ToArray() }.Concat(table.Rows); var row = 0; if (report.AccessWatermark is not null) rows = rows.Concat(new[] { new[] { new ReportCell(report.AccessWatermark) } });
                    foreach (var cells in rows) { row++; x.WriteStartElement("row"); x.WriteAttributeString("r", row.ToString()); if (report.AccessWatermark is not null && row == table.Rows.Count + 2) { x.WriteAttributeString("ht", Math.Clamp(Math.Ceiling(report.AccessWatermark.Length / Math.Max(8d, table.Columns.Sum(c => Math.Clamp(c.Length + 8, 18, 55)) * .75)) * 15 + 10, 48, 400).ToString(CultureInfo.InvariantCulture)); x.WriteAttributeString("customHeight", "1"); } for (var c = 0; c < cells.Length; c++) { var cell = cells[c]; var numericText = cell.Number?.ToString("0.############################", CultureInfo.InvariantCulture); var numeric = numericText is not null && numericText.Replace(".", "").TrimStart('-', '0').Length <= 15; BidRules.Require(cell.Text.Length <= 32767, 400, "An Excel cell exceeds 32,767 characters. Narrow the report."); x.WriteStartElement("c"); x.WriteAttributeString("r", Column(c + 1) + row); x.WriteAttributeString("s", row == 1 ? "1" : numeric ? "2" : "0"); x.WriteAttributeString("t", numeric ? "n" : "inlineStr"); if (numeric) x.WriteElementString("v", numericText); else { x.WriteStartElement("is"); x.WriteStartElement("t"); x.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve"); x.WriteString(Clean(numericText ?? cell.Text)); x.WriteEndElement(); x.WriteEndElement(); } x.WriteEndElement(); } x.WriteEndElement(); }
                    x.WriteEndElement(); x.WriteStartElement("autoFilter"); x.WriteAttributeString("ref", $"A1:{Column(table.Columns.Length)}{row - (report.AccessWatermark is null ? 0 : 1)}"); x.WriteEndElement(); if (report.AccessWatermark is not null) { if (table.Columns.Length > 1) { x.WriteStartElement("mergeCells"); x.WriteAttributeString("count", "1"); x.WriteStartElement("mergeCell"); x.WriteAttributeString("ref", $"A{row}:{Column(table.Columns.Length)}{row}"); x.WriteEndElement(); x.WriteEndElement(); } var header = Clean(report.AccessWatermark).Replace("&", ""); x.WriteStartElement("headerFooter"); x.WriteElementString("oddHeader", "&L" + header[..Math.Min(header.Length, 250)]); x.WriteEndElement(); } x.WriteEndElement();
                });
            }
        }
        return result.ToArray();
    }
    private static string Column(int n) { var text = ""; while (n > 0) { n--; text = (char)('A' + n % 26) + text; n /= 26; } return text; }
    private static string Clean(string text) => string.Concat(text.EnumerateRunes().Where(r => r.Value is 9 or 10 or 13 || r.Value >= 32 && r.Value <= 0xD7FF || r.Value >= 0xE000 && r.Value <= 0xFFFD || r.Value >= 0x10000 && r.Value <= 0x10FFFF).Select(r => r.ToString()));
    public static bool HasRightToLeftText(ReportDocument report) =>
        new[] { report.Title }.Concat(report.Notes).Concat(report.Metrics.SelectMany(m => new[] { m.Label, m.Value }))
            .Concat(report.Tables.SelectMany(t => t.Rows.SelectMany(row => row.Select(c => c.Text))))
            .Any(text => Regex.IsMatch(text, @"[\u0590-\u08FF\uFB1D-\uFDFF\uFE70-\uFEFF\u200F\u202B\u202E\u2067]"));
    private static readonly object fontLock = new();
    public static byte[] Pdf(ReportDocument report)
    {
        BidRules.Require(!HasRightToLeftText(report), 400, "This report contains right-to-left text. Use Excel or the web report; Arabic PDF layout is not supported by the current exporter.");
        lock (fontLock) { GlobalFontSettings.FontResolver ??= new ReportFontResolver(); }
        using var document = new PdfDocument(); document.Info.Title = report.Title; document.Info.Author = "STMS";
        var font = new XFont("STMS Report", 9); var bold = new XFont("STMS Report", 9, XFontStyleEx.Bold); var title = new XFont("STMS Report", 18, XFontStyleEx.Bold);
        XGraphics? graphics = null; PdfPage? page = null; double y = 0, width = 0; int pages = 0;
        var navy = XColor.FromArgb(21, 43, 64); var muted = XColor.FromArgb(98, 110, 119); var gold = XColor.FromArgb(164, 121, 43);
        void NewPage() { graphics?.Dispose(); BidRules.Require(++pages <= 250, 400, "The PDF exceeds 250 pages. Refine the report filters."); page = document.AddPage(); page.Size = PageSize.A4; page.Orientation = PageOrientation.Landscape; graphics = XGraphics.FromPdfPage(page); width = page.Width.Point - 64; y = 34; graphics.DrawString("STMS  /  REPORTS", bold, new XSolidBrush(gold), 32, y); y += 27; graphics.DrawString(report.Title, title, new XSolidBrush(navy), 32, y); y += 19; graphics.DrawString("Authorized export · " + report.GeneratedAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'"), font, new XSolidBrush(muted), 32, y); y += 18; graphics.DrawLine(new XPen(navy, .6), 32, y, width + 32, y); y += 16; }
        List<string> Wrap(string raw, double available, XFont face) { var lines = new List<string>(); foreach (var paragraph in Clean(raw).Replace("\r", "").Split('\n')) { var current = ""; foreach (var rune in paragraph.EnumerateRunes()) { var next = current + rune; if (graphics!.MeasureString(next, face).Width > available && current.Length > 0) { lines.Add(current); current = rune.ToString(); } else current = next; } lines.Add(current); } return lines.Count == 0 ? [""] : lines; }
        try
        {
            NewPage();
            foreach (var metric in report.Metrics) { if (y > page!.Height.Point - 90) NewPage(); graphics!.DrawString(metric.Label + ": " + metric.Value, bold, new XSolidBrush(navy), 32, y); y += 16; }
            y += 8;
            foreach (var note in report.Notes) { foreach (var line in Wrap(note, width, font)) { if (y > page!.Height.Point - 90) NewPage(); graphics!.DrawString(line, font, new XSolidBrush(muted), 32, y); y += 13; } y += 6; }
            foreach (var table in report.Tables)
            {
                if (y > page!.Height.Point - 100) NewPage(); y += 12; graphics!.DrawString(table.Title, new XFont("STMS Report", 12, XFontStyleEx.Bold), new XSolidBrush(navy), 32, y); y += 12;
                var widths = Enumerable.Repeat(width / table.Columns.Length, table.Columns.Length).ToArray(); if (table.Columns.Length == 2) { widths[0] = width * .25; widths[1] = width * .75; }
                void Row(ReportCell[] cells, bool heading)
                {
                    var wrapped = cells.Select((c, i) => Wrap(c.Text, widths[i] - 12, heading ? bold : font)).ToArray(); var total = wrapped.Max(l => l.Count); int offset = 0;
                    while (offset < total)
                    {
                        var room = (int)Math.Floor((page!.Height.Point - 86 - y - 12) / 13); if (room < 1) { NewPage(); room = (int)Math.Floor((page!.Height.Point - 86 - y - 12) / 13); if (!heading) { Row(table.Columns.Select(c => new ReportCell(c)).ToArray(), true); room = (int)Math.Floor((page!.Height.Point - 86 - y - 12) / 13); } }
                        var count = Math.Min(room, total - offset); double x = 32; var height = count * 13 + 12;
                        for (int i = 0; i < cells.Length; i++) { graphics!.DrawRectangle(new XSolidBrush(heading ? navy : XColor.FromArgb(248, 247, 244)), x, y, widths[i], height); for (int line = 0; line < count; line++) if (offset + line < wrapped[i].Count) graphics.DrawString(wrapped[i][offset + line], heading ? bold : font, heading ? XBrushes.White : new XSolidBrush(navy), x + 6, y + 15 + line * 13); x += widths[i]; }
                        y += height + 1; offset += count;
                    }
                }
                Row(table.Columns.Select(c => new ReportCell(c)).ToArray(), true); if (table.Rows.Count == 0) Row(table.Columns.Select((c, i) => new ReportCell(i == 0 ? "No matching records." : "")).ToArray(), false); foreach (var row in table.Rows) Row(row, false); y += 12;
            }
            graphics?.Dispose(); graphics = null;
            for (var i = 0; i < document.PageCount; i++)
            {
                var current = document.Pages[i];
                using var footer = XGraphics.FromPdfPage(current, XGraphicsPdfPageOptions.Append);
                footer.DrawString($"STMS / Page {i + 1} / {document.PageCount}", font, new XSolidBrush(muted), 32, current.Height.Point - 20);
                if (report.AccessWatermark is not null)
                {
                    var label = Clean(report.AccessWatermark);
                    var stampFont = new XFont("STMS Report", 6.5);
                    double lineY = current.Height.Point - 65;
                    var stampLine = "";
                    foreach (var rune in label.EnumerateRunes())
                    {
                        var candidate = stampLine + rune;
                        if (stampLine.Length > 0 && footer.MeasureString(candidate, stampFont).Width > current.Width.Point - 64) { footer.DrawString(stampLine, stampFont, new XSolidBrush(muted), 32, lineY); lineY += 7; stampLine = rune.ToString(); }
                        else stampLine = candidate;
                    }
                    footer.DrawString(stampLine, stampFont, new XSolidBrush(muted), 32, lineY);
                    var state = footer.Save();
                    footer.TranslateTransform(current.Width.Point / 2, current.Height.Point / 2);
                    footer.RotateTransform(-25);
                    footer.DrawString(label, new XFont("STMS Report", 11), new XSolidBrush(XColor.FromArgb(45, 21, 43, 64)), new XRect(-360, -20, 720, 40), XStringFormats.Center);
                    footer.Restore(state);
                }
            }
            using var output = new MemoryStream(); document.Save(output, false); return output.ToArray();
        }
        finally { graphics?.Dispose(); }
    }
    private sealed class ReportFontResolver : IFontResolver
    {
        private readonly string path = Environment.GetEnvironmentVariable("STMS_REPORT_FONT_PATH") ?? (OperatingSystem.IsWindows() ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf") : "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf");
        public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) => new("stms-report", bold, italic);
        public byte[]? GetFont(string faceName) { if (!File.Exists(path)) throw new BidException(503, "The report font is unavailable. Configure STMS_REPORT_FONT_PATH with a readable Unicode TTF font."); return File.ReadAllBytes(path); }
    }
}
