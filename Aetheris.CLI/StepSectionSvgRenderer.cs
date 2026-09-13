using System.Globalization;
using System.Text;

namespace Aetheris.CLI;

internal static class StepSectionSvgRenderer
{
    public static string Render(SectionAnalysisResult section)
    {
        var bounds = section.Summary.SectionBoundingBox2D ?? throw new InvalidOperationException("Section has no bounded geometry to render.");
        var width = Math.Max(bounds.Max.U - bounds.Min.U, 1e-6d);
        var height = Math.Max(bounds.Max.V - bounds.Min.V, 1e-6d);
        var margin = Math.Max(width, height) * .05d;
        var minU = bounds.Min.U - margin;
        var minV = bounds.Min.V - margin;
        var viewWidth = width + 2d * margin;
        var viewHeight = height + 2d * margin;
        var sb = new StringBuilder();
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"").Append(F(minU)).Append(' ').Append(F(-minV-viewHeight)).Append(' ').Append(F(viewWidth)).Append(' ').Append(F(viewHeight)).Append("\">\n");
        sb.Append("  <rect x=\"").Append(F(minU)).Append("\" y=\"").Append(F(-minV-viewHeight)).Append("\" width=\"").Append(F(viewWidth)).Append("\" height=\"").Append(F(viewHeight)).Append("\" fill=\"#f4f7fa\"/>\n");
        sb.Append("  <g transform=\"scale(1,-1)\" fill=\"none\" stroke=\"#17212b\" stroke-width=\"").Append(F(Math.Max(width, height) / 500d)).Append("\">\n");
        foreach (var loop in section.Loops.OrderBy(loop => loop.LoopId))
        {
            sb.Append("    <path data-loop=\"").Append(loop.LoopId).Append("\" data-role=\"").Append(loop.Role ?? "Unknown").Append("\" d=\"");
            if (loop.Segments.Count > 0)
            {
                var first = loop.Segments[0];
                sb.Append('M').Append(F(first.Start.U)).Append(' ').Append(F(first.Start.V));
                foreach (var segment in loop.Segments)
                {
                    if (segment.Kind == "line") sb.Append(" L").Append(F(segment.End.U)).Append(' ').Append(F(segment.End.V));
                    else if (segment.Kind == "arc" && segment.Radius is { } radius && segment.SweepRadians is { } sweep)
                    {
                        if (Math.Abs(sweep) >= 2d * Math.PI - 1e-9d)
                        {
                            var center = segment.Center!;
                            sb.Append(" M").Append(F(center.U + radius)).Append(' ').Append(F(center.V));
                            sb.Append(" A").Append(F(radius)).Append(' ').Append(F(radius)).Append(" 0 1 1 ").Append(F(center.U - radius)).Append(' ').Append(F(center.V));
                            sb.Append(" A").Append(F(radius)).Append(' ').Append(F(radius)).Append(" 0 1 1 ").Append(F(center.U + radius)).Append(' ').Append(F(center.V));
                        }
                        else sb.Append(" A").Append(F(radius)).Append(' ').Append(F(radius)).Append(" 0 ").Append(Math.Abs(sweep) > Math.PI ? '1' : '0').Append(' ').Append(sweep >= 0d ? '1' : '0').Append(' ').Append(F(segment.End.U)).Append(' ').Append(F(segment.End.V));
                    }
                }
                if (loop.IsClosed) sb.Append(" Z");
            }
            sb.Append("\"/>\n");
        }
        sb.Append("  </g>\n</svg>\n");
        return sb.ToString();
    }

    private static string F(double value) => value.ToString("0.###############", CultureInfo.InvariantCulture);
}
