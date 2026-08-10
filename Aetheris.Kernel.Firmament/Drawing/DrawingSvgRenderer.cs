using System.Globalization;
using System.Security;
using System.Text;

namespace Aetheris.Kernel.Firmament.Drawing;

public static class DrawingSvgRenderer
{
    public static string Render(DrawingIr drawing)
    {
        var width = drawing.Pages.Max(page => page.WidthMillimetres);
        var height = drawing.Pages.Sum(page => page.HeightMillimetres + 8) - 8;
        var builder = new StringBuilder();
        builder.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(width)}mm\" height=\"{F(height)}mm\" viewBox=\"0 0 {F(width)} {F(height)}\" data-drawing-schema=\"{drawing.SchemaVersion}\">");
        builder.AppendLine("<style>.sheet{fill:white;stroke:#aab2bd;stroke-width:.25}.visible,.silhouette{fill:none;stroke:#111827;stroke-width:.35;stroke-linecap:round;stroke-linejoin:round}.silhouette{stroke-width:.5}.hidden{fill:none;stroke:#64748b;stroke-width:.25;stroke-dasharray:2 1}.leader{fill:none;stroke:#374151;stroke-width:.22}.annotation{font:3.2px Arial,sans-serif;fill:#111827}.label{font:600 3.2px Arial,sans-serif;fill:#334155}.meta{font:2.8px Arial,sans-serif;fill:#334155}.table-header{font:600 3px Arial,sans-serif}.table-cell{font:2.8px Arial,sans-serif}</style>");
        var offset = 0d;
        foreach (var page in drawing.Pages)
        {
            builder.AppendLine($"<g transform=\"translate(0 {F(offset)})\" data-page=\"{page.PageNumber}\">");
            builder.AppendLine($"<rect class=\"sheet\" x=\"0\" y=\"0\" width=\"{F(page.WidthMillimetres)}\" height=\"{F(page.HeightMillimetres)}\"/>");
            builder.AppendLine($"<text class=\"label\" x=\"10\" y=\"9\">{E(drawing.Metadata.Title)}</text>");
            builder.AppendLine($"<text class=\"meta\" x=\"{F(page.WidthMillimetres - 10)}\" y=\"9\" text-anchor=\"end\">{E(drawing.Metadata.ProductName)} · {E(drawing.Metadata.Revision ?? "-")} · {page.PageNumber}/{drawing.Pages.Count}</text>");
            foreach (var view in page.Views) RenderView(builder, view);
            foreach (var annotation in page.Annotations) RenderAnnotation(builder, annotation);
            RenderTables(builder, page.Tables, page.ContentRect);
            var noteY = page.HeightMillimetres - 15;
            foreach (var note in page.Notes) { builder.AppendLine($"<text class=\"meta\" x=\"10\" y=\"{F(noteY)}\">{E(note)}</text>"); noteY += 4; }
            builder.AppendLine($"<text class=\"meta\" x=\"10\" y=\"{F(page.HeightMillimetres - 5)}\">A4 {page.Orientation} · actual size · {E(drawing.Provenance.TemplateIdentity)} · {E(drawing.Provenance.SpecializationIdentity)}</text>");
            builder.AppendLine("</g>");
            offset += page.HeightMillimetres + 8;
        }
        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static void RenderView(StringBuilder builder, DrawingViewIr view)
    {
        builder.AppendLine($"<g data-view=\"{E(view.Identity)}\">");
        builder.AppendLine($"<text class=\"label\" x=\"{F(view.Viewport.X)}\" y=\"{F(view.Viewport.Y + 3.5)}\">{E(view.Identity)} · scale ×{F(view.Scale)}</text>");
        foreach (var primitive in view.Primitives)
        {
            var points = string.Join(" ", primitive.Points.Select(point => $"{F(point.X)},{F(point.Y)}"));
            builder.AppendLine($"<polyline class=\"{primitive.Kind.ToString().ToLowerInvariant()}\" points=\"{points}\" data-semantic-id=\"{E(primitive.StableId)}\"/>");
        }
        builder.AppendLine("</g>");
    }

    private static void RenderAnnotation(StringBuilder builder, DrawingAnnotationIr annotation)
    {
        var leader = string.Join(" ", annotation.SelectedCandidate.Leader.Select(point => $"{F(point.X)},{F(point.Y)}"));
        builder.AppendLine($"<g data-annotation=\"{E(annotation.Identity)}\" data-semantic-reference=\"{E(annotation.SemanticReference)}\">");
        builder.AppendLine($"<polyline class=\"leader\" points=\"{leader}\"/>");
        builder.AppendLine($"<rect x=\"{F(annotation.SelectedCandidate.Body.X)}\" y=\"{F(annotation.SelectedCandidate.Body.Y)}\" width=\"{F(annotation.SelectedCandidate.Body.Width)}\" height=\"{F(annotation.SelectedCandidate.Body.Height)}\" fill=\"white\" stroke=\"#374151\" stroke-width=\".18\"/>");
        builder.AppendLine($"<text class=\"annotation\" x=\"{F(annotation.SelectedCandidate.Body.X + 1.2)}\" y=\"{F(annotation.SelectedCandidate.Body.Y + 4)}\">{E(annotation.EngineeringDisplay)}</text>");
        builder.AppendLine("</g>");
    }

    private static void RenderTables(StringBuilder builder, IReadOnlyList<DrawingTableIr> tables, DrawingRect content)
    {
        var y = content.Y + 10;
        foreach (var table in tables)
        {
            var cellWidth = content.Width / Math.Max(1, table.Columns.Count);
            const double rowHeight = 7;
            builder.AppendLine($"<text class=\"label\" x=\"{F(content.X)}\" y=\"{F(y - 3)}\">{E(table.Identity)}</text>");
            for (var column = 0; column < table.Columns.Count; column++)
            {
                var x = content.X + column * cellWidth;
                builder.AppendLine($"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(cellWidth)}\" height=\"{rowHeight}\" fill=\"#e2e8f0\" stroke=\"#64748b\" stroke-width=\".2\"/>");
                builder.AppendLine($"<text class=\"table-header\" x=\"{F(x + 1)}\" y=\"{F(y + 4.5)}\">{E(table.Columns[column])}</text>");
            }
            y += rowHeight;
            foreach (var row in table.Rows)
            {
                for (var column = 0; column < table.Columns.Count; column++)
                {
                    var x = content.X + column * cellWidth;
                    builder.AppendLine($"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(cellWidth)}\" height=\"{rowHeight}\" fill=\"white\" stroke=\"#94a3b8\" stroke-width=\".18\"/>");
                    builder.AppendLine($"<text class=\"table-cell\" x=\"{F(x + 1)}\" y=\"{F(y + 4.5)}\">{E(row[column])}</text>");
                }
                y += rowHeight;
            }
            y += 10;
        }
    }

    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string E(string value) => SecurityElement.Escape(value) ?? string.Empty;
}
