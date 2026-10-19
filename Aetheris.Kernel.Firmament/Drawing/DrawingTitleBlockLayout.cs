using System.Globalization;

namespace Aetheris.Kernel.Firmament.Drawing;

/// <summary>One table model and measured paper-space layout for all drawing backends.</summary>
internal static class DrawingTitleBlockLayout
{
    private static readonly Lazy<DrawingInterFont> Font = new(DrawingInterFont.Load);
    public static DrawingRect Bounds(double width, double height) => new(width - 177, height - 53, 170, 41);
    public static double FooterTop(double width, double height) => width >= 270 ? height - 53 : height - 87;
    public static DrawingRect NotesArea(double width, double height) => width >= 270
        ? new(10, height - 51, width - 191, 39) : new(10, height - 85, width - 20, 30);

    public static DrawingInformationBlockIr Create(DrawingMetadataIr metadata, IReadOnlyDictionary<string, string> fields,
        double width, double height, DrawingLocationIr location)
    {
        var bounds = Bounds(width, height);
        var signoff = metadata.Signoff ?? new(new(metadata.Author, metadata.Date), new(), new());
        var values = new Dictionary<string, string>(fields, StringComparer.Ordinal)
        {
            ["DrawnBy"] = signoff.Drawn.Name ?? "", ["DrawnDate"] = signoff.Drawn.Date ?? "",
            ["CheckedBy"] = signoff.Checked.Name ?? "", ["CheckedDate"] = signoff.Checked.Date ?? "",
            ["ApprovedBy"] = signoff.Approved.Name ?? "", ["ApprovedDate"] = signoff.Approved.Date ?? ""
        };
        var rows = new List<DrawingTitleBlockRowIr>();
        var column = bounds.Width / 6; var y = bounds.Y;
        void Row(double h, params (string Key, string Label, string Value, int Span, double Size)[] specs)
        {
            var x = bounds.X; var cells = new List<DrawingTitleBlockCellIr>();
            foreach (var s in specs)
            {
                var rect = new DrawingRect(x, y, column * s.Span, h);
                var lines = Wrap(s.Value, rect.Width - 2, s.Size);
                if (lines.Count * s.Size * 1.15 + 3 > h)
                    throw new InvalidOperationException($"drawing-title-block-overflow: '{s.Label}' does not fit its table cell. Shorten this presentation value; engineering data was not truncated.");
                cells.Add(new(s.Key, s.Label, s.Value, s.Span, rect, lines, s.Size, rows.Count == 0)); x += rect.Width;
            }
            rows.Add(new(h, cells)); y += h;
        }
        Row(11, ("Title", metadata.Company ?? "ENGINEERING DRAWING", metadata.Title, 4, 3.2),
            ("Drawing", "DRAWING", metadata.DrawingIdentity, 2, 2.6));
        Row(8, ("Material", "MATERIAL", metadata.Material ?? "", 3, 2.6),
            ("Part", "PART NUMBER", metadata.PartNumber ?? "", 2, 2.6), ("Revision", "REV", metadata.Revision?.ToString() ?? "", 1, 2.6));
        Row(7, ("Scale", "SCALE", fields.GetValueOrDefault("Scale", ""), 2, 2.4), ("Units", "UNITS", fields.GetValueOrDefault("Units", "mm"), 1, 2.4),
            ("Page", "SHEET", fields.GetValueOrDefault("Page", fields.GetValueOrDefault("Sheet", "")), 1, 2.4),
            ("Status", "STATUS", fields.GetValueOrDefault("Status", "ENGINEERING REVIEW"), 2, 2.4));
        Row(8, ("DrawnBy", "DRAWN BY", values["DrawnBy"], 2, 2.6), ("CheckedBy", "CHECKED BY", values["CheckedBy"], 2, 2.6),
            ("ApprovedBy", "APPROVED BY", values["ApprovedBy"], 2, 2.6));
        Row(7, ("DrawnDate", "DRAWN DATE", values["DrawnDate"], 2, 2.4), ("CheckedDate", "CHECKED DATE", values["CheckedDate"], 2, 2.4),
            ("ApprovedDate", "APPROVED DATE", values["ApprovedDate"], 2, 2.4));
        return new(bounds, location, values) { Table = new(Enumerable.Repeat(column, 6).ToArray(), rows) };
    }

    public static IReadOnlyList<string> Wrap(string text, double width, double size)
    {
        if (string.IsNullOrEmpty(text)) return [];
        var result = new List<string>(); var line = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = line.Length == 0 ? word : line + " " + word;
            if (Font.Value.MeasureMillimetres(candidate, size) <= width) { line = candidate; continue; }
            if (line.Length > 0) { result.Add(line); line = ""; }
            foreach (var c in word)
            {
                if (line.Length > 0 && Font.Value.MeasureMillimetres(line + c, size) > width) { result.Add(line); line = ""; }
                line += c;
            }
        }
        if (line.Length > 0) result.Add(line);
        return result;
    }

    public static string? ValidateSignoff(DrawingSignoffIr? signoff)
    {
        if (signoff is null) return null;
        foreach (var (role, entry) in new[] { ("Drawn", signoff.Drawn), ("Checked", signoff.Checked), ("Approved", signoff.Approved) })
        {
            if (string.IsNullOrWhiteSpace(entry.Name) != string.IsNullOrWhiteSpace(entry.Date))
                return $"drawing-signoff-incomplete: {role} requires both a name and date, or both must be blank.";
            if (!string.IsNullOrWhiteSpace(entry.Date) && !DateOnly.TryParseExact(entry.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                return $"drawing-date-iso-invalid: {role} date '{entry.Date}' must be yyyy-MM-dd.";
        }
        return null;
    }
}
