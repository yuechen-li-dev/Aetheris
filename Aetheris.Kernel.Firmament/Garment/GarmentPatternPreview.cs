using System.Globalization;
using System.Security;
using System.Text;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Garment;

/// <summary>Projects resolved analytic curves directly to SVG. No mesh or simulation dependency.</summary>
public static class GarmentPatternPreview
{
    private static readonly string[] SeamColors = ["#c45d21", "#933c83", "#177b66", "#3758b1", "#ac7022", "#b7384a"];

    public static string Svg(GarmentSource source)
    {
        var seams = GarmentPatternInspection.Inspect(source);
        int rows = (source.Panels.Length + 1) / 2;
        int height = 130 + rows * 650;
        var svg = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1600\" height=\"{height}\" viewBox=\"0 0 1600 {height}\">\n");
        svg.AppendLine($"<rect width=\"1600\" height=\"{height}\" fill=\"#f5f3ee\"/>");
        svg.AppendLine("<g font-family=\"sans-serif\" fill=\"#24344c\">");
        svg.AppendLine($"<text x=\"30\" y=\"38\" font-size=\"26\">{Escape(source.Name)} — resolved material patterns (mm)</text>");
        svg.AppendLine("<text x=\"30\" y=\"68\" font-size=\"15\">Matching seam colors · blue open boundaries · green supports · arrows indicate seam correspondence</text>");
        svg.AppendLine("<text x=\"30\" y=\"92\" font-size=\"15\">Analytic profiles before meshing. Cut boundary; no seam allowance added.</text>");
        for (int ordinal = 0; ordinal < source.Panels.Length; ordinal++)
        {
            var panel = source.Panels[ordinal];
            var segments = panel.Profile.Loops.Single().Segments;
            var bounds = segments.SelectMany(segment => Bounds(segment.Geometry)).ToArray();
            var boundaries = GarmentPatternInspection.Boundaries(panel);
            double minX = bounds.Min(point => point.X);
            double minY = bounds.Min(point => point.Y);
            double maxX = bounds.Max(point => point.X);
            double maxY = bounds.Max(point => point.Y);
            double scale = Math.Min(430 / (maxX - minX), 450 / (maxY - minY));
            double left = 35 + ordinal % 2 * 800;
            double top = 155 + ordinal / 2 * 650;
            svg.AppendLine($"<text x=\"{Number(left)}\" y=\"{Number(top)}\" font-size=\"21\">{Escape(panel.Identity)}</text>");
            svg.AppendLine($"<text x=\"{Number(left)}\" y=\"{Number(top + 25)}\" font-size=\"13\">Envelope {Number(maxX - minX)} × {Number(maxY - minY)} mm</text>");
            svg.AppendLine($"<g transform=\"translate({Number(left - minX * scale)},{Number(top + 50 + maxY * scale)}) scale({Number(scale)},-{Number(scale)})\">");
            foreach (var segment in segments)
            {
                var boundaryNames = boundaries.Values
                    .Where(boundary => boundary.Segments.Any(child => child.Name == segment.Name))
                    .Select(boundary => boundary.Name).ToArray();
                int seamIndex = FindSeam(seams, panel, boundaryNames);
                bool pinned = panel.Pins.Any(boundaryNames.Contains);
                string color = BoundaryColor(seamIndex, pinned);
                svg.AppendLine($"<path data-boundary=\"{Escape(segment.Name)}\" d=\"{Path(segment.Geometry)}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"2\" vector-effect=\"non-scaling-stroke\"/>");
            }
            double cx = (minX + maxX) / 2;
            double cy = (minY + maxY) / 2;
            double arrow = Math.Min(maxX - minX, maxY - minY) * .13;
            double gx = panel.Grain.X * arrow;
            double gy = panel.Grain.Y * arrow;
            svg.AppendLine($"<path d=\"M {Number(cx - gx)} {Number(cy - gy)} L {Number(cx + gx)} {Number(cy + gy)} M {Number(cx + gx - gx * .2 - gy * .1)} {Number(cy + gy - gy * .2 + gx * .1)} L {Number(cx + gx)} {Number(cy + gy)} L {Number(cx + gx - gx * .2 + gy * .1)} {Number(cy + gy - gy * .2 - gx * .1)}\" fill=\"none\" stroke=\"#24344c\" stroke-width=\"1.5\" vector-effect=\"non-scaling-stroke\"/>");
            svg.AppendLine("</g>");
            double lineY = top + 62;
            foreach (var boundary in boundaries.Values)
            {
                bool unnamedChild = (panel.Profile.BoundaryEdits ?? []).Any(edit => edit.Descendants.Contains(boundary.Name)
                    && boundary.Name.StartsWith(edit.Name + "_Span", StringComparison.Ordinal));
                bool unselectedGroup = (panel.Profile.BoundaryEdits ?? []).Any(edit => edit.Name == boundary.Name)
                    && FindSeam(seams, panel, [boundary.Name]) < 0 && !panel.Pins.Contains(boundary.Name)
                    && !(panel.Profile.BoundaryEdits ?? []).Any(edit => edit.Name == boundary.Name
                        && edit.Descendants.All(name => name.StartsWith(edit.Name + "_Span", StringComparison.Ordinal)));
                if (unnamedChild || unselectedGroup)
                {
                    continue;
                }
                int seamIndex = FindSeam(seams, panel, [boundary.Name]);
                bool pinned = panel.Pins.Contains(boundary.Name);
                string color = BoundaryColor(seamIndex, pinned);
                string correspondence = pinned ? "support" : "open";
                if (seamIndex >= 0)
                {
                    string direction = seams[seamIndex].Reversed ? " ↔ reversed" : " → same";
                    correspondence = seams[seamIndex].Name + direction;
                }
                svg.AppendLine($"<text x=\"{Number(left + 455)}\" y=\"{Number(lineY)}\" font-size=\"12\" fill=\"{color}\">{Escape(boundary.Name)} · {Number(boundary.LengthMm)}mm</text>");
                svg.AppendLine($"<text x=\"{Number(left + 455)}\" y=\"{Number(lineY + 16)}\" font-size=\"11\">{Escape(correspondence)}</text>");
                lineY += 38;
            }
        }
        svg.AppendLine("</g></svg>");
        return svg.ToString();
    }

    private static int FindSeam(IReadOnlyList<GarmentPatternSeam> seams, GarmentPanel panel, string[] names)
    {
        for (int index = 0; index < seams.Count; index++)
        {
            foreach (string name in names)
            {
                string identity = panel.Identity + ".edges." + name;
                string local = panel.Name + ".edges." + name;
                if (seams[index].A == identity || seams[index].B == identity || seams[index].A == local || seams[index].B == local)
                {
                    return index;
                }
            }
        }
        return -1;
    }

    private static string BoundaryColor(int seamIndex, bool pinned)
    {
        if (pinned)
        {
            return "#16835b";
        }
        if (seamIndex < 0)
        {
            return "#3274a0";
        }
        return SeamColors[seamIndex % SeamColors.Length];
    }

    private static IEnumerable<(double X, double Y)> Bounds(LineArcProfileCurve2D curve)
    {
        // Reuse profile curve bounds. An arc's unused circle must not enlarge the panel preview.
        var bounds = BezierSectionCurve2D.Bounds(curve, 0, 1);
        return [(bounds.MinX, bounds.MinY), (bounds.MaxX, bounds.MaxY)];
    }

    private static string Path(LineArcProfileCurve2D curve)
    {
        var start = GarmentPatternInspection.Start(curve);
        string path = $"M {Number(start.X)} {Number(start.Y)} ";
        return curve switch
        {
            LineArcLineSegment2D line => path + $"L {Number(line.End.X)} {Number(line.End.Y)}",
            LineArcCubicBezier2D cubic => path + $"C {Number(cubic.Control1.X)} {Number(cubic.Control1.Y)} {Number(cubic.Control2.X)} {Number(cubic.Control2.Y)} {Number(cubic.End.X)} {Number(cubic.End.Y)}",
            LineArcCircularArc2D arc => path + $"A {Number(arc.Radius)} {Number(arc.Radius)} 0 {(Math.Abs(arc.SweepAngleRadians) > Math.PI ? 1 : 0)} {(arc.SweepAngleRadians > 0 ? 1 : 0)} {Number(arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians))} {Number(arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians))}",
            _ => throw new NotSupportedException("Unsupported pattern curve."),
        };
    }

    private static string Number(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
    private static string Escape(string value) => SecurityElement.Escape(value) ?? "";
}
