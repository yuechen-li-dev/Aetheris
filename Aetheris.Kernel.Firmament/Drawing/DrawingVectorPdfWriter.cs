using System.Globalization;
using System.Text;

namespace Aetheris.Kernel.Firmament.Drawing;

/// <summary>Small deterministic PDF 1.4 vector backend: paths stay paths and text stays text.</summary>
public static class DrawingVectorPdfWriter
{
    private const double PointsPerMillimetre = 72d / 25.4d;

    public static void Write(DrawingIr drawing, string path)
    {
        var objects = new List<byte[]> { Array.Empty<byte>() };
        int AddString(string value) { objects.Add(Encoding.ASCII.GetBytes(value)); return objects.Count - 1; }
        int AddBytes(byte[] value) { objects.Add(value); return objects.Count - 1; }

        var font = AddString("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        var fontBold = AddString("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
        var pageObjects = new List<int>();
        var contentObjects = new List<int>();
        foreach (var page in drawing.Pages)
        {
            var stream = BuildPage(drawing, page);
            contentObjects.Add(AddBytes(StreamObject(stream)));
            pageObjects.Add(AddString("pending"));
        }
        var pagesObject = AddString("pending");
        for (var i = 0; i < drawing.Pages.Count; i++)
        {
            var page = drawing.Pages[i];
            var width = Pt(page.WidthMillimetres); var height = Pt(page.HeightMillimetres);
            objects[pageObjects[i]] = Encoding.ASCII.GetBytes($"<< /Type /Page /Parent {pagesObject} 0 R /MediaBox [0 0 {F(width)} {F(height)}] /Resources << /Font << /F1 {font} 0 R /F2 {fontBold} 0 R >> >> /Contents {contentObjects[i]} 0 R >>");
        }
        objects[pagesObject] = Encoding.ASCII.GetBytes($"<< /Type /Pages /Count {pageObjects.Count} /Kids [{string.Join(" ", pageObjects.Select(value => $"{value} 0 R"))}] >>");
        var catalog = AddString($"<< /Type /Catalog /Pages {pagesObject} 0 R >>");
        var info = AddString($"<< /Title ({PdfText(drawing.Metadata.Title)}) /Subject ({PdfText(drawing.Metadata.ProductName)}) /Author (Aetheris) /Creator (Aetheris Drawing M0) /Producer (Aetheris vector PDF) /CreationDate (D:20260810000000-07'00') /ModDate (D:20260810000000-07'00') >>");

        using var output = new MemoryStream();
        Write(output, "%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");
        var offsets = new long[objects.Count];
        for (var i = 1; i < objects.Count; i++)
        {
            offsets[i] = output.Position;
            Write(output, $"{i} 0 obj\n"); output.Write(objects[i]); Write(output, "\nendobj\n");
        }
        var xref = output.Position;
        Write(output, $"xref\n0 {objects.Count}\n0000000000 65535 f \n");
        for (var i = 1; i < objects.Count; i++) Write(output, $"{offsets[i]:0000000000} 00000 n \n");
        Write(output, $"trailer\n<< /Size {objects.Count} /Root {catalog} 0 R /Info {info} 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        File.WriteAllBytes(path, output.ToArray());
    }

    private static byte[] StreamObject(byte[] stream)
    {
        var header = Encoding.ASCII.GetBytes($"<< /Length {stream.Length} >>\nstream\n");
        var footer = Encoding.ASCII.GetBytes("\nendstream");
        return [.. header, .. stream, .. footer];
    }

    private static byte[] BuildPage(DrawingIr drawing, DrawingPageIr page)
    {
        var b = new StringBuilder();
        double X(double mm) => Pt(mm);
        double Y(double mm) => Pt(page.HeightMillimetres - mm);
        void Line(DrawingPoint2 a, DrawingPoint2 c, double width = .25, bool dashed = false)
        {
            b.Append($"{F(width)} w {(dashed ? "[5 3] 0 d" : "[] 0 d")} {F(X(a.X))} {F(Y(a.Y))} m {F(X(c.X))} {F(Y(c.Y))} l S\n");
        }
        void Rect(DrawingRect rect, double width = .2, bool fill = false)
        {
            b.Append($"{F(width)} w {F(X(rect.X))} {F(Y(rect.Bottom))} {F(X(rect.Width))} {F(X(rect.Height))} re {(fill ? "B" : "S")}\n");
        }
        void Text(double x, double y, string text, double size = 8, bool bold = false)
        {
            b.Append($"BT /{(bold ? "F2" : "F1")} {F(size)} Tf {F(X(x))} {F(Y(y))} Td ({PdfText(text)}) Tj ET\n");
        }

        Text(10, 9, drawing.Metadata.Title, 10, true);
        Text(page.WidthMillimetres - 85, 9, $"{drawing.Metadata.ProductName} | REV {drawing.Metadata.Revision ?? "-"} | {page.PageNumber}/{drawing.Pages.Count}", 7);
        foreach (var view in page.Views)
        {
            Text(view.Viewport.X, view.Viewport.Y + 3.5, $"{view.Identity}  SCALE x{view.Scale:0.###}", 7, true);
            foreach (var primitive in view.Primitives)
                for (var i = 1; i < primitive.Points.Count; i++) Line(primitive.Points[i - 1], primitive.Points[i], primitive.Kind == DrawingPrimitiveKind.Silhouette ? .55 : .35, primitive.Kind == DrawingPrimitiveKind.Hidden);
        }
        foreach (var annotation in page.Annotations)
        {
            for (var i = 1; i < annotation.SelectedCandidate.Leader.Count; i++) Line(annotation.SelectedCandidate.Leader[i - 1], annotation.SelectedCandidate.Leader[i], .25);
            b.Append("1 1 1 rg "); Rect(annotation.SelectedCandidate.Body, .25, true); b.Append("0 0 0 rg\n");
            Text(annotation.SelectedCandidate.Body.X + 1, annotation.SelectedCandidate.Body.Y + 4.2, annotation.EngineeringDisplay.Replace("Ø", "DIA ", StringComparison.Ordinal).Replace("±", "+/-", StringComparison.Ordinal), 7);
        }
        var tableY = page.ContentRect.Y + 10;
        foreach (var table in page.Tables)
        {
            Text(page.ContentRect.X, tableY - 3, table.Identity, 9, true);
            var cellWidth = page.ContentRect.Width / table.Columns.Count;
            const double rowHeight = 7;
            for (var column = 0; column < table.Columns.Count; column++)
            {
                var rect = new DrawingRect(page.ContentRect.X + column * cellWidth, tableY, cellWidth, rowHeight);
                b.Append("0.88 0.91 0.95 rg "); Rect(rect, .2, true); b.Append("0 0 0 rg\n");
                Text(rect.X + 1, rect.Y + 4.6, table.Columns[column], 7, true);
            }
            tableY += rowHeight;
            foreach (var row in table.Rows)
            {
                for (var column = 0; column < table.Columns.Count; column++)
                {
                    var rect = new DrawingRect(page.ContentRect.X + column * cellWidth, tableY, cellWidth, rowHeight);
                    Rect(rect); Text(rect.X + 1, rect.Y + 4.6, row[column], 7);
                }
                tableY += rowHeight;
            }
            tableY += 10;
        }
        var noteY = page.HeightMillimetres - 15;
        foreach (var note in page.Notes) { Text(10, noteY, note, 7); noteY += 4; }
        Text(10, page.HeightMillimetres - 5, $"A4 {page.Orientation} | ACTUAL SIZE | {drawing.Provenance.TemplateIdentity} | {drawing.Provenance.SpecializationIdentity}", 6);
        return Encoding.ASCII.GetBytes(b.ToString());
    }

    private static double Pt(double millimetres) => millimetres * PointsPerMillimetre;
    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string PdfText(string value) => string.Concat(value.Select(character => character is >= ' ' and <= '~' ? character : '?')).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);
    private static void Write(Stream stream, string value) => stream.Write(Encoding.Latin1.GetBytes(value));
}
