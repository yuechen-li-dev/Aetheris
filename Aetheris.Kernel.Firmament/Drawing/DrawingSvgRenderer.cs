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
        builder.AppendLine("<style>.sheet{fill:white;stroke:#aab2bd;stroke-width:.25}.border,.zone-tick{fill:none;stroke:#334155;stroke-width:.25}.visible,.silhouette{fill:none;stroke:#111827;stroke-width:.35;stroke-linecap:round;stroke-linejoin:round}.silhouette{stroke-width:.5}.hidden{fill:none;stroke:#64748b;stroke-width:.25;stroke-dasharray:2 1}.leader{fill:none;stroke:#374151;stroke-width:.22}.annotation{font:3.2px Inter,Arial,sans-serif;fill:#111827}.label{font:600 3.2px Inter,Arial,sans-serif;fill:#334155}.meta,.zone-label{font:2.8px Inter,Arial,sans-serif;fill:#334155}.zone-label{font-size:2.4px}.table-header{font:600 3px Inter,Arial,sans-serif}.table-cell{font:2.8px Inter,Arial,sans-serif}.info-key{font:2px Inter,Arial,sans-serif;fill:#64748b}.info-value{font:2.6px Inter,Arial,sans-serif;fill:#111827}</style>");
        var offset = 0d;
        foreach (var page in drawing.Pages)
        {
            builder.AppendLine($"<g transform=\"translate(0 {F(offset)})\" data-page=\"{page.PageNumber}\">");
            builder.AppendLine($"<rect class=\"sheet\" x=\"0\" y=\"0\" width=\"{F(page.WidthMillimetres)}\" height=\"{F(page.HeightMillimetres)}\"/>");
            RenderZones(builder, page);
            builder.AppendLine($"<text class=\"label\" x=\"10\" y=\"9\">{E(drawing.Metadata.Title)}</text>");
            builder.AppendLine($"<text class=\"meta\" x=\"{F(page.WidthMillimetres - 10)}\" y=\"9\" text-anchor=\"end\">{E(drawing.Metadata.ProductName)} · REV {E(drawing.Metadata.Revision?.ToString() ?? "-")} · {page.PageNumber}/{drawing.Pages.Count}</text>");
            foreach (var view in page.Views) RenderView(builder, view);
            foreach (var annotation in page.Annotations) RenderAnnotation(builder, annotation);
            RenderTables(builder, page.Tables, page.ContentRect);
            foreach (var note in page.LocatedNotes ?? []) builder.AppendLine($"<text class=\"meta\" x=\"{F(note.Bounds.X)}\" y=\"{F(note.Bounds.Y + 3)}\" data-zone=\"{E(note.Location.Zone)}\">{E(note.Text)}</text>");
            RenderInformationBlock(builder, page.InformationBlock);
            builder.AppendLine("</g>");
            offset += page.HeightMillimetres + 8;
        }
        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static void RenderZones(StringBuilder builder, DrawingPageIr page)
    {
        if (page.ZoneScheme is null) return;
        var all = page.ZoneScheme.Zones; var border = new DrawingRect(all.Min(z => z.Bounds.X), all.Min(z => z.Bounds.Y), all.Max(z => z.Bounds.Right) - all.Min(z => z.Bounds.X), all.Max(z => z.Bounds.Bottom) - all.Min(z => z.Bounds.Y));
        builder.AppendLine($"<rect class=\"border\" x=\"{F(border.X)}\" y=\"{F(border.Y)}\" width=\"{F(border.Width)}\" height=\"{F(border.Height)}\"/>");
        for (var column = 0; column < page.ZoneScheme.Columns; column++) { var zone = all[column]; var x = zone.Bounds.X + zone.Bounds.Width / 2; builder.AppendLine($"<text class=\"zone-label\" x=\"{F(x)}\" y=\"{F(border.Y - 2)}\" text-anchor=\"middle\">{page.ZoneScheme.ColumnLabels[column]}</text><text class=\"zone-label\" x=\"{F(x)}\" y=\"{F(border.Bottom + 4)}\" text-anchor=\"middle\">{page.ZoneScheme.ColumnLabels[column]}</text>"); if (column > 0) builder.AppendLine($"<path class=\"zone-tick\" d=\"M {F(zone.Bounds.X)} {F(border.Y)} v 3 M {F(zone.Bounds.X)} {F(border.Bottom)} v -3\"/>"); }
        for (var row = 0; row < page.ZoneScheme.Rows; row++) { var zone = all[row * page.ZoneScheme.Columns]; var y = zone.Bounds.Y + zone.Bounds.Height / 2; builder.AppendLine($"<text class=\"zone-label\" x=\"{F(border.X - 3)}\" y=\"{F(y + 1)}\" text-anchor=\"middle\">{page.ZoneScheme.RowLabels[row]}</text><text class=\"zone-label\" x=\"{F(border.Right + 3)}\" y=\"{F(y + 1)}\" text-anchor=\"middle\">{page.ZoneScheme.RowLabels[row]}</text>"); if (row > 0) builder.AppendLine($"<path class=\"zone-tick\" d=\"M {F(border.X)} {F(zone.Bounds.Y)} h 3 M {F(border.Right)} {F(zone.Bounds.Y)} h -3\"/>"); }
    }

    private static void RenderInformationBlock(StringBuilder builder, DrawingInformationBlockIr? block)
    {
        if (block is null) return; builder.AppendLine($"<g data-information-block=\"true\" data-zone=\"{E(block.Location.Zone)}\"><rect x=\"{F(block.Bounds.X)}\" y=\"{F(block.Bounds.Y)}\" width=\"{F(block.Bounds.Width)}\" height=\"{F(block.Bounds.Height)}\" fill=\"#fff\" stroke=\"#334155\" stroke-width=\".3\"/>");
        var entries = block.Fields.ToArray(); var columns = 3; var cellWidth = block.Bounds.Width / columns; var rows = (int)Math.Ceiling(entries.Length / (double)columns); var cellHeight = block.Bounds.Height / rows;
        for (var i = 0; i < entries.Length; i++) { var x = block.Bounds.X + i % columns * cellWidth; var y = block.Bounds.Y + i / columns * cellHeight; builder.AppendLine($"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(cellWidth)}\" height=\"{F(cellHeight)}\" fill=\"none\" stroke=\"#94a3b8\" stroke-width=\".12\"/><text class=\"info-key\" x=\"{F(x + .8)}\" y=\"{F(y + 2.2)}\">{E(entries[i].Key.ToUpperInvariant())}</text><text class=\"info-value\" x=\"{F(x + .8)}\" y=\"{F(y + 5)}\">{E(entries[i].Value)}</text>"); }
        builder.AppendLine("</g>");
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
        foreach (var table in tables)
        {
            var bounds = table.Bounds.Width > 0 ? table.Bounds : new DrawingRect(content.X, content.Y + 10, content.Width, 60);
            var y = bounds.Y; var cellWidth = bounds.Width / Math.Max(1, table.Columns.Count);
            const double rowHeight = 7;
            builder.AppendLine($"<g data-table-kind=\"{table.Kind}\" data-zone=\"{E(table.Location?.Zone ?? "-")}\"><text class=\"label\" x=\"{F(bounds.X)}\" y=\"{F(y - 3)}\">{E(table.Identity)}</text>");
            for (var column = 0; column < table.Columns.Count; column++)
            {
                var x = bounds.X + column * cellWidth;
                builder.AppendLine($"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(cellWidth)}\" height=\"{rowHeight}\" fill=\"#e2e8f0\" stroke=\"#64748b\" stroke-width=\".2\"/>");
                builder.AppendLine($"<text class=\"table-header\" x=\"{F(x + 1)}\" y=\"{F(y + 4.5)}\">{E(table.Columns[column])}</text>");
            }
            y += rowHeight;
            foreach (var row in table.Rows)
            {
                for (var column = 0; column < table.Columns.Count; column++)
                {
                    var x = bounds.X + column * cellWidth;
                    builder.AppendLine($"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(cellWidth)}\" height=\"{rowHeight}\" fill=\"white\" stroke=\"#94a3b8\" stroke-width=\".18\"/>");
                    builder.AppendLine($"<text class=\"table-cell\" x=\"{F(x + 1)}\" y=\"{F(y + 4.5)}\">{E(row[column])}</text>");
                }
                y += rowHeight;
            }
            builder.AppendLine("</g>");
        }
    }

    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string E(string value) => SecurityElement.Escape(value) ?? string.Empty;
}
