using System.Globalization;
using System.Text;

namespace Aetheris.Kernel.Firmament.Drawing;

/// <summary>Deterministic PDF 1.4 vector backend with an embedded Inter TrueType Type0 font.</summary>
public static class DrawingVectorPdfWriter
{
    private const double PointsPerMillimetre = 72d / 25.4d;

    public static void Write(DrawingIr drawing, string path)
    {
        var objects = new List<byte[]> { Array.Empty<byte>() };
        int Add(string value) { objects.Add(Encoding.ASCII.GetBytes(value)); return objects.Count - 1; }
        int AddBytes(byte[] value) { objects.Add(value); return objects.Count - 1; }
        var inter = DrawingInterFont.Load(); var characters = CollectText(drawing).SelectMany(value => value.EnumerateRunes()).Select(rune => rune.Value).Where(value => value <= ushort.MaxValue)
            .Concat(Enumerable.Range(32, 95)).Concat([0x00b1, 0x00b7, 0x00d7, 0x00d8]).Distinct().Order().ToArray();
        var fontFile = AddBytes(StreamObject(inter.Bytes));
        var descriptor = Add($"<< /Type /FontDescriptor /FontName /Inter /Flags 32 /FontBBox [{Scale(inter.XMin, inter.UnitsPerEm)} {Scale(inter.YMin, inter.UnitsPerEm)} {Scale(inter.XMax, inter.UnitsPerEm)} {Scale(inter.YMax, inter.UnitsPerEm)}] /ItalicAngle 0 /Ascent {Scale(inter.Ascent, inter.UnitsPerEm)} /Descent {Scale(inter.Descent, inter.UnitsPerEm)} /CapHeight {Scale(inter.Ascent, inter.UnitsPerEm)} /StemV 80 /FontFile2 {fontFile} 0 R >>");
        var maxCid = characters.Max(); var cidMap = new byte[(maxCid + 1) * 2]; foreach (var code in characters) { var glyph = inter.Glyph(code); cidMap[code * 2] = (byte)(glyph >> 8); cidMap[code * 2 + 1] = (byte)glyph; }
        var cidMapObject = AddBytes(StreamObject(cidMap));
        var widths = string.Join(" ", characters.Select(code => $"{code} [{inter.Width1000(code)}]"));
        var cidFont = Add($"<< /Type /Font /Subtype /CIDFontType2 /BaseFont /Inter /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor {descriptor} 0 R /CIDToGIDMap {cidMapObject} 0 R /DW 1000 /W [{widths}] >>");
        var toUnicode = AddBytes(StreamObject(Encoding.ASCII.GetBytes(ToUnicode(characters))));
        var font = Add($"<< /Type /Font /Subtype /Type0 /BaseFont /Inter /Encoding /Identity-H /DescendantFonts [{cidFont} 0 R] /ToUnicode {toUnicode} 0 R >>");

        var pageObjects = new List<int>(); var contentObjects = new List<int>();
        foreach (var page in drawing.Pages) { contentObjects.Add(AddBytes(StreamObject(BuildPage(drawing, page)))); pageObjects.Add(Add("pending")); }
        var pagesObject = Add("pending");
        for (var index = 0; index < drawing.Pages.Count; index++)
        {
            var page = drawing.Pages[index];
            objects[pageObjects[index]] = Encoding.ASCII.GetBytes($"<< /Type /Page /Parent {pagesObject} 0 R /MediaBox [0 0 {F(Pt(page.WidthMillimetres))} {F(Pt(page.HeightMillimetres))}] /Resources << /Font << /F1 {font} 0 R >> >> /Contents {contentObjects[index]} 0 R >>");
        }
        objects[pagesObject] = Encoding.ASCII.GetBytes($"<< /Type /Pages /Count {pageObjects.Count} /Kids [{string.Join(" ", pageObjects.Select(value => $"{value} 0 R"))}] >>");
        var catalog = Add($"<< /Type /Catalog /Pages {pagesObject} 0 R >>");
        var info = Add($"<< /Title ({PdfAscii(drawing.Metadata.Title)}) /Subject ({PdfAscii(drawing.Metadata.Description ?? drawing.Metadata.ProductName)}) /Author ({PdfAscii(drawing.Metadata.Author ?? "Aetheris")}) /Creator (Aetheris Drawing M0B) /Producer (Aetheris native vector PDF) /Keywords (Revision {drawing.Metadata.Revision?.ToString() ?? "-"}; Date {drawing.Metadata.Date ?? "-"}; Inter embedded) /CreationDate (D:20260810000000-07'00') /ModDate (D:20260810000000-07'00') >>");
        using var output = new MemoryStream(); Write(output, "%PDF-1.4\n%\xE2\xE3\xCF\xD3\n"); var offsets = new long[objects.Count];
        for (var index = 1; index < objects.Count; index++) { offsets[index] = output.Position; Write(output, $"{index} 0 obj\n"); output.Write(objects[index]); Write(output, "\nendobj\n"); }
        var xref = output.Position; Write(output, $"xref\n0 {objects.Count}\n0000000000 65535 f \n"); for (var index = 1; index < objects.Count; index++) Write(output, $"{offsets[index]:0000000000} 00000 n \n");
        Write(output, $"trailer\n<< /Size {objects.Count} /Root {catalog} 0 R /Info {info} 0 R >>\nstartxref\n{xref}\n%%EOF\n"); File.WriteAllBytes(path, output.ToArray());
    }

    private static byte[] BuildPage(DrawingIr drawing, DrawingPageIr page)
    {
        var b = new StringBuilder(); var inter = DrawingInterFont.Load(); double X(double mm) => Pt(mm); double Y(double mm) => Pt(page.HeightMillimetres - mm);
        void Line(DrawingPoint2 a, DrawingPoint2 c, double width = .25, bool dashed = false) => b.Append($"{F(width)} w {(dashed ? "[5 3] 0 d" : "[] 0 d")} {F(X(a.X))} {F(Y(a.Y))} m {F(X(c.X))} {F(Y(c.Y))} l S\n");
        void Rect(DrawingRect rect, double width = .2, bool fill = false) => b.Append($"{F(width)} w {F(X(rect.X))} {F(Y(rect.Bottom))} {F(X(rect.Width))} {F(X(rect.Height))} re {(fill ? "B" : "S")}\n");
        void Text(double x, double y, string text, double size = 8) => b.Append($"BT /F1 {F(size)} Tf {F(X(x))} {F(Y(y))} Td <{Utf16Hex(text)}> Tj ET\n");
        void RightText(double right, double y, string text, double size = 8) => Text(right - inter.MeasureMillimetres(text, size / PointsPerMillimetre), y, text, size);
        RenderZones(page, Line, Rect, Text);
        Text(10, 9, drawing.Metadata.Title, 10); RightText(page.WidthMillimetres - 10, 9, $"{drawing.Metadata.ProductName} · REV {drawing.Metadata.Revision?.ToString() ?? "-"} · {page.PageNumber}/{drawing.Pages.Count}", 7);
        foreach (var view in page.Views)
        {
            Text(view.Viewport.X, view.Viewport.Y + 3.5, $"{view.Identity}  SCALE ×{view.Scale:0.###}", 7);
            foreach (var primitive in view.Primitives) for (var index = 1; index < primitive.Points.Count; index++) Line(primitive.Points[index - 1], primitive.Points[index], primitive.Kind == DrawingPrimitiveKind.Silhouette ? .55 : .35, primitive.Kind == DrawingPrimitiveKind.Hidden);
        }
        foreach (var annotation in page.Annotations)
        {
            for (var index = 1; index < annotation.SelectedCandidate.Leader.Count; index++) Line(annotation.SelectedCandidate.Leader[index - 1], annotation.SelectedCandidate.Leader[index], .25);
            b.Append("1 1 1 rg "); Rect(annotation.SelectedCandidate.Body, .25, true); b.Append("0 0 0 rg\n"); Text(annotation.SelectedCandidate.Body.X + 1, annotation.SelectedCandidate.Body.Y + 4.2, annotation.EngineeringDisplay, 7);
        }
        foreach (var table in page.Tables) RenderTable(table, page.ContentRect, b, Rect, Text);
        foreach (var note in page.LocatedNotes ?? []) Text(note.Bounds.X, note.Bounds.Y + 3, note.Text, 7);
        RenderInformation(page.InformationBlock, b, Rect, Text);
        return Encoding.ASCII.GetBytes(b.ToString());
    }

    private static void RenderZones(DrawingPageIr page, Action<DrawingPoint2, DrawingPoint2, double, bool> line, Action<DrawingRect, double, bool> rect, Action<double, double, string, double> text)
    {
        if (page.ZoneScheme is null) return; var zones = page.ZoneScheme.Zones; var border = new DrawingRect(zones.Min(z => z.Bounds.X), zones.Min(z => z.Bounds.Y), zones.Max(z => z.Bounds.Right) - zones.Min(z => z.Bounds.X), zones.Max(z => z.Bounds.Bottom) - zones.Min(z => z.Bounds.Y)); rect(border, .3, false);
        for (var column = 0; column < page.ZoneScheme.Columns; column++) { var zone = zones[column]; var x = zone.Bounds.Center.X; text(x - 1, border.Y - 2, page.ZoneScheme.ColumnLabels[column], 5.5); text(x - 1, border.Bottom + 4, page.ZoneScheme.ColumnLabels[column], 5.5); if (column > 0) { line(new(x, border.Y), new(x, border.Y + 3), .2, false); line(new(x, border.Bottom), new(x, border.Bottom - 3), .2, false); } }
        for (var row = 0; row < page.ZoneScheme.Rows; row++) { var zone = zones[row * page.ZoneScheme.Columns]; var y = zone.Bounds.Center.Y; text(border.X - 3.5, y + 1, page.ZoneScheme.RowLabels[row], 5.5); text(border.Right + 1.5, y + 1, page.ZoneScheme.RowLabels[row], 5.5); if (row > 0) { line(new(border.X, zone.Bounds.Y), new(border.X + 3, zone.Bounds.Y), .2, false); line(new(border.Right, zone.Bounds.Y), new(border.Right - 3, zone.Bounds.Y), .2, false); } }
    }

    private static void RenderTable(DrawingTableIr table, DrawingRect content, StringBuilder b, Action<DrawingRect, double, bool> rect, Action<double, double, string, double> text)
    {
        var bounds = table.Bounds.Width > 0 ? table.Bounds : new DrawingRect(content.X, content.Y + 10, content.Width, 60); var y = bounds.Y; var cellWidth = bounds.Width / table.Columns.Count; const double rowHeight = 7; text(bounds.X, y - 3, table.Identity, 9);
        for (var column = 0; column < table.Columns.Count; column++) { var cell = new DrawingRect(bounds.X + column * cellWidth, y, cellWidth, rowHeight); b.Append("0.88 0.91 0.95 rg "); rect(cell, .2, true); b.Append("0 0 0 rg\n"); text(cell.X + 1, cell.Y + 4.6, table.Columns[column], 7); }
        y += rowHeight; foreach (var row in table.Rows) { for (var column = 0; column < table.Columns.Count; column++) { var cell = new DrawingRect(bounds.X + column * cellWidth, y, cellWidth, rowHeight); rect(cell, .2, false); text(cell.X + 1, cell.Y + 4.6, row[column], 7); } y += rowHeight; }
    }

    private static void RenderInformation(DrawingInformationBlockIr? block, StringBuilder b, Action<DrawingRect, double, bool> rect, Action<double, double, string, double> text)
    {
        if (block is null) return; b.Append("1 1 1 rg "); rect(block.Bounds, .35, true); b.Append("0 0 0 rg\n"); var entries = block.Fields.ToArray(); const int columns = 3; var rows = (int)Math.Ceiling(entries.Length / 3d); var width = block.Bounds.Width / columns; var height = block.Bounds.Height / rows;
        for (var index = 0; index < entries.Length; index++) { var cell = new DrawingRect(block.Bounds.X + index % columns * width, block.Bounds.Y + index / columns * height, width, height); rect(cell, .12, false); text(cell.X + .8, cell.Y + 2.2, entries[index].Key.ToUpperInvariant(), 4.8); text(cell.X + .8, cell.Y + 5, entries[index].Value, 6.2); }
    }

    private static IEnumerable<string> CollectText(DrawingIr drawing)
    {
        yield return drawing.Metadata.Title; yield return drawing.Metadata.ProductName; yield return drawing.Metadata.Description ?? ""; yield return drawing.Metadata.Company ?? ""; yield return drawing.Metadata.Author ?? ""; yield return drawing.Metadata.Material ?? ""; yield return drawing.Metadata.Revision?.ToString() ?? "";
        foreach (var page in drawing.Pages) { foreach (var view in page.Views) yield return view.Identity; foreach (var annotation in page.Annotations) yield return annotation.EngineeringDisplay; foreach (var table in page.Tables) { yield return table.Identity; foreach (var column in table.Columns) yield return column; foreach (var row in table.Rows) foreach (var cell in row) yield return cell; } foreach (var note in page.Notes) yield return note; foreach (var field in page.InformationBlock?.Fields ?? new Dictionary<string, string>()) { yield return field.Key; yield return field.Value; } }
    }

    private static string ToUnicode(IReadOnlyList<int> characters)
    {
        var b = new StringBuilder("/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n/CMapName /Inter-UCS def\n/CMapType 2 def\n1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n");
        foreach (var chunk in characters.Chunk(100)) { b.Append(chunk.Length).Append(" beginbfchar\n"); foreach (var code in chunk) b.Append('<').Append(code.ToString("X4")).Append("> <").Append(code.ToString("X4")).Append(">\n"); b.Append("endbfchar\n"); }
        return b.Append("endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n").ToString();
    }

    private static byte[] StreamObject(byte[] stream) => [.. Encoding.ASCII.GetBytes($"<< /Length {stream.Length} >>\nstream\n"), .. stream, .. Encoding.ASCII.GetBytes("\nendstream")];
    private static int Scale(int value, int units) => (int)Math.Round(value * 1000d / units);
    private static string Utf16Hex(string value) => Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(value));
    private static double Pt(double millimetres) => millimetres * PointsPerMillimetre;
    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string PdfAscii(string value) => string.Concat(value.Select(character => character is >= ' ' and <= '~' ? character : '?')).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);
    private static void Write(Stream stream, string value) => stream.Write(Encoding.Latin1.GetBytes(value));
}
