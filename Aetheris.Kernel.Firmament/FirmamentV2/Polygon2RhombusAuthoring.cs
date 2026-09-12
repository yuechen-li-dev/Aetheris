using System.Globalization;
using System.Text.RegularExpressions;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>
/// Bounded source lowering for the built-in Polygon2&lt;Rhombus&gt; shape. The parameter is
/// a closed shape variant, not a user-defined generic. Lowering reuses ordinary Point2,
/// Line2, Profile pipeline, validation, and materialization paths.
/// </summary>
internal static class Polygon2RhombusAuthoring
{
    internal const string Prefix = "firmament-polygon2-";
    internal sealed record Result(string Source, IReadOnlyList<FirmamentV2Polygon2Decl> Polygons);

    public static Result? Expand(string source, List<string> diagnostics)
    {
        var changes = new List<(int Start, int Length, string Text)>();
        var polygons = new List<FirmamentV2Polygon2Decl>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match header in Regex.Matches(source,
                     @"\bPolygon2\s*<\s*(?<variant>[A-Za-z_]\w*)\s*>\s+(?<name>[A-Za-z_]\w*)\s*\{",
                     RegexOptions.CultureInvariant))
        {
            var open = source.IndexOf('{', header.Index); var close = Matching(source, open);
            var name = header.Groups["name"].Value; var variant = header.Groups["variant"].Value;
            if (close < 0) { diagnostics.Add(Prefix + "malformed:" + name); continue; }
            if (!string.Equals(variant, "Rhombus", StringComparison.Ordinal)) { diagnostics.Add(Prefix + "variant-unsupported:" + name + ":" + variant); continue; }
            if (!names.Add(name)) { diagnostics.Add(Prefix + "duplicate-name:" + name); continue; }
            var body = source[(open + 1)..close];
            var center = Regex.Match(body, @"\bCenter\s*:\s*(?:\[|Point2\s*\()\s*(?<x>[-+.\deE]+)mm\s*,\s*(?<y>[-+.\deE]+)mm\s*(?:\]|\))", RegexOptions.CultureInvariant);
            var diagonals = Regex.Match(body, @"\bDiagonals\s*:\s*\[\s*(?<x>[-+.\deE]+)mm\s*,\s*(?<y>[-+.\deE]+)mm\s*\]", RegexOptions.CultureInvariant);
            if (!center.Success || !diagonals.Success
                || !TryNumber(center, "x", out var centerX) || !TryNumber(center, "y", out var centerY)
                || !TryNumber(diagonals, "x", out var diagonalX) || !TryNumber(diagonals, "y", out var diagonalY)
                || diagonalX <= 0d || diagonalY <= 0d)
            { diagnostics.Add(Prefix + "invalid-geometry:" + name); continue; }
            var generatedPoints = new[] { name + "_South", name + "_East", name + "_North", name + "_West" };
            var generatedEdges = new[] { name + "_SouthEast", name + "_EastNorth", name + "_NorthWest", name + "_WestSouth" };
            changes.Add((header.Index, close - header.Index + 1, Lower(name, centerX, centerY, diagonalX, diagonalY)));
            polygons.Add(new(name, "Rhombus", centerX, centerY, diagonalX, diagonalY, generatedPoints, generatedEdges,
                new(header.Index, close - header.Index + 1)));
        }
        if (diagnostics.Any(item => item.StartsWith(Prefix, StringComparison.Ordinal))) return null;
        foreach (var polygon in polygons)
        {
            var trace = $@"\b{Regex.Escape(polygon.Name)}\s*\|>\s*TraceLoop\b";
            foreach (Match match in Regex.Matches(source, trace, RegexOptions.CultureInvariant))
                changes.Add((match.Index, match.Length, string.Join(" |> ", polygon.GeneratedEdges) + " |> Close"));
        }
        var containers = changes.Where(change => change.Text.StartsWith("Point2 ", StringComparison.Ordinal)).ToArray();
        foreach (var change in changes.Where(change => !containers.Any(container => container.Start < change.Start && change.Start < container.Start + container.Length)).OrderByDescending(change => change.Start))
            source = source.Remove(change.Start, change.Length).Insert(change.Start, change.Text);
        return new(source, polygons);
    }

    private static string Lower(string name, double x, double y, double dx, double dy)
    {
        static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture) + "mm";
        var south = (X: x, Y: y - dy / 2d); var east = (X: x + dx / 2d, Y: y);
        var north = (X: x, Y: y + dy / 2d); var west = (X: x - dx / 2d, Y: y);
        return $$"""
            Point2 {{name}}_South { Position: [{{N(south.X)}}, {{N(south.Y)}}] }
            Point2 {{name}}_East { Position: [{{N(east.X)}}, {{N(east.Y)}}] }
            Point2 {{name}}_North { Position: [{{N(north.X)}}, {{N(north.Y)}}] }
            Point2 {{name}}_West { Position: [{{N(west.X)}}, {{N(west.Y)}}] }
            Line2 {{name}}_SouthEast { From: {{name}}_South; To: {{name}}_East }
            Line2 {{name}}_EastNorth { From: {{name}}_East; To: {{name}}_North }
            Line2 {{name}}_NorthWest { From: {{name}}_North; To: {{name}}_West }
            Line2 {{name}}_WestSouth { From: {{name}}_West; To: {{name}}_South }
            """;
    }

    private static bool TryNumber(Match match, string group, out double value) =>
        double.TryParse(match.Groups[group].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    private static int Matching(string source, int open)
    {
        var depth = 0;
        for (var index = open; index < source.Length; index++)
        { if (source[index] == '{') depth++; else if (source[index] == '}' && --depth == 0) return index; }
        return -1;
    }
}
