using Aetheris.Kernel.Firmament.Materializer;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>
/// Bounded lowering for Firmament's canonical ClosedBoundary2 family. Declarations remain
/// inspectable semantic facts while geometry is erased to the existing Profile frontend.
/// </summary>
internal static class ClosedBoundary2Authoring
{
    internal const string Prefix = "firmament-boundary2-";
    private const double Tol = 1e-9;
    private static readonly Regex Header = new(
        @"\b(?:(?<plain>Rect2|Square2|Circle2|Ellipse2|Slot2|RoundedRect2|SmoothRoundedRect2)|(?<family>Triangle2|Polygon2)\s*<\s*(?<variant>[A-Za-z_]\w*)\s*>|RegularPolygon2\s*<\s*(?<count>[-+]?\d+)\s*>)\s+(?<name>[A-Za-z_]\w*)\s*\{",
        RegexOptions.CultureInvariant);

    internal sealed record Result(string Source, IReadOnlyList<FirmamentV2ClosedBoundary2Decl> Boundaries)
    {
        public IReadOnlyList<FirmamentV2Polygon2Decl> Polygons => Boundaries.Where(x => x.ShapeType == "Polygon2")
            .Select(x => new FirmamentV2Polygon2Decl(x.Name, x.Variant!, x.CenterX, x.CenterY,
                x.Dimensions.GetValueOrDefault("DiagonalX"), x.Dimensions.GetValueOrDefault("DiagonalY"),
                x.GeneratedPoints, x.GeneratedEdges, x.SourceSpan)).ToArray();
    }

    private sealed record Shape(FirmamentV2ClosedBoundary2Decl Declaration, string? LoweredSource,
        IReadOnlyDictionary<string, string> MemberMap, string TraceExpression, int Start, int Length);

    public static Result? Expand(string source, List<string> diagnostics)
    {
        // Template bodies have local names and unbound dimensions. Their selected
        // specializations pass through this lowering after ordinary template binding.
        var templates = FirmamentV2TemplateExpansion.DeclarationSpans(source, diagnostics);
        bool InTemplate(int offset) => templates.Any(span => offset >= span.Start && offset < span.Start + span.Length);
        var shapes = new List<Shape>(); var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match header in Header.Matches(source))
        {
            // `Concept Circle2 Name { ... }` is a concise reusable curve concept, not a
            // closed material boundary declaration. Its Circle2 payload is consumed by
            // the ordinary profile-guide binder and must not be rewritten here.
            var prefix = source[..header.Index].TrimEnd();
            if (prefix.EndsWith("Concept", StringComparison.Ordinal) && (prefix.Length == 7 || !char.IsLetterOrDigit(prefix[^8]) && prefix[^8] != '_')) continue;
            if (InTemplate(header.Index)) continue;
            var open = source.IndexOf('{', header.Index); var close = Matching(source, open); var name = header.Groups["name"].Value;
            if (close < 0) { diagnostics.Add(Prefix + "malformed:" + name); continue; }
            if (!names.Add(name)) { diagnostics.Add(Prefix + "duplicate-name:" + name); continue; }
            var shape = Bind(source, header, source[(open + 1)..close], close, diagnostics);
            if (shape is not null) shapes.Add(shape);
        }
        if (diagnostics.Any(x => x.StartsWith(Prefix, StringComparison.Ordinal) || x.StartsWith("firmament-polygon2-", StringComparison.Ordinal))) return null;
        var changes = new List<(int Start, int Length, string Text)>();
        foreach (var shape in shapes)
        {
            if (shape.LoweredSource is not null) changes.Add((shape.Start, shape.Length, shape.LoweredSource));
            foreach (Match trace in Regex.Matches(source, $@"\b{Regex.Escape(shape.Declaration.Name)}(?:\s+As\s+(?<alias>[A-Za-z_]\w*))?\s*\|>\s*TraceLoop\b", RegexOptions.CultureInvariant))
            {
                if (Inside(trace.Index, shapes) || InTemplate(trace.Index)) continue;
                var replacement = shape.TraceExpression;
                if (trace.Groups["alias"].Success && replacement.Count(character => character == '|') == 1)
                    replacement = replacement.Replace(" |> TraceLoop", $" As {trace.Groups["alias"].Value} |> TraceLoop", StringComparison.Ordinal);
                changes.Add((trace.Index, trace.Length, replacement));
            }
            foreach (var member in shape.MemberMap)
                foreach (Match reference in Regex.Matches(source, $@"\b{Regex.Escape(shape.Declaration.Name)}\s*\.\s*{Regex.Escape(member.Key)}\b", RegexOptions.CultureInvariant))
                    if (!Inside(reference.Index, shapes) && !InTemplate(reference.Index)) changes.Add((reference.Index, reference.Length, member.Value));
        }
        foreach (var change in changes.OrderByDescending(x => x.Start)) source = source.Remove(change.Start, change.Length).Insert(change.Start, change.Text);
        return new(source, shapes.Select(x => x.Declaration).ToArray());
    }

    private static Shape? Bind(string source, Match header, string body, int close, List<string> diagnostics)
    {
        var name = header.Groups["name"].Value;
        var type = header.Groups["plain"].Success ? header.Groups["plain"].Value : header.Groups["family"].Success ? header.Groups["family"].Value : "RegularPolygon2";
        var variant = header.Groups["variant"].Success ? header.Groups["variant"].Value : header.Groups["count"].Success ? header.Groups["count"].Value : null;
        var rotation = Angle(body, "Rotation", 0d, out var rotationOk); if (!rotationOk) return Invalid(name, "invalid-rotation", diagnostics);
        var span = new FirmamentV2SourceSpan(header.Index, close - header.Index + 1);
        if (type == "Rect2")
        {
            if (!Point(body, "Center", out var c) || !Vector(body, "Size", out var size)) return null; // legacy/template Rect2 is bound after static specialization
            if (!Positive(size.X, size.Y)) return Invalid(name, "invalid-dimensions", diagnostics);
            return Math.Abs(rotation) <= Tol ? ExistingRect(name, c, size.X, size.Y, span)
                : Linear(name, type, null, c, rotation, Rectangle(c, size.X, size.Y, rotation), ["BottomLeft", "BottomRight", "TopRight", "TopLeft"], ["Bottom", "Right", "Top", "Left"], span, new Dictionary<string, double> { ["Width"] = size.X, ["Height"] = size.Y });
        }
        if (type == "Square2")
        {
            if (!Point(body, "Center", out var c) || !Length(body, "Size", out var size) || size <= 0) return Invalid(name, "invalid-dimensions", diagnostics);
            return Linear(name, type, null, c, rotation, Rectangle(c, size, size, rotation), ["BottomLeft", "BottomRight", "TopRight", "TopLeft"], ["Bottom", "Right", "Top", "Left"], span, new Dictionary<string, double> { ["Size"] = size });
        }
        return type switch
        {
            "Triangle2" => Triangle(name, variant!, body, rotation, span, diagnostics),
            "Circle2" => Circle(source, name, body, rotation, span, diagnostics),
            "Ellipse2" => Ellipse(name, body, rotation, span, diagnostics),
            "Slot2" => Slot(name, body, rotation, span, diagnostics),
            "SmoothRoundedRect2" => SmoothRoundedRect(name, body, rotation, span, diagnostics),
            "RoundedRect2" => RoundedRect(name, body, rotation, span, diagnostics),
            "Polygon2" => Polygon(source, name, variant!, body, rotation, span, diagnostics),
            _ => RegularPolygon(name, variant!, body, rotation, span, diagnostics)
        };
    }

    private static Shape ExistingRect(string name, (double X, double Y) c, double w, double h, FirmamentV2SourceSpan span)
    {
        var points = new[] { "BottomLeft", "BottomRight", "TopRight", "TopLeft" }; var edges = new[] { "Bottom", "Right", "Top", "Left" };
        var map = points.Concat(edges).ToDictionary(x => x, x => name + "." + x, StringComparer.Ordinal);
        return Make(name, "Rect2", null, c, 0, new Dictionary<string, double> { ["Width"] = w, ["Height"] = h }, points, edges, "Line", span, null, map, string.Join(" |> ", edges.Select(x => name + "." + x)) + " |> Close", w * h, 2 * (w + h));
    }

    private static Shape? Triangle(string name, string variant, string body, double rotation, FirmamentV2SourceSpan span, List<string> diagnostics)
    {
        (double X, double Y) c; IReadOnlyList<(double X, double Y)> vertices; var dimensions = new Dictionary<string, double>();
        switch (variant)
        {
            case "Equilateral":
                if (!Point(body, "Center", out c) || !Length(body, "Side", out var side) || side <= 0) return Invalid(name, "invalid-dimensions", diagnostics);
                var h = side * Math.Sqrt(3) / 2; vertices = [Rotate((c.X, c.Y + 2 * h / 3), c, rotation), Rotate((c.X - side / 2, c.Y - h / 3), c, rotation), Rotate((c.X + side / 2, c.Y - h / 3), c, rotation)]; dimensions["Side"] = side; break;
            case "Isosceles":
                if (!Point(body, "Center", out c) || !Length(body, "Base", out var b) || !Length(body, "Height", out var ih) || !Positive(b, ih)) return Invalid(name, "invalid-dimensions", diagnostics);
                vertices = [Rotate((c.X, c.Y + 2 * ih / 3), c, rotation), Rotate((c.X - b / 2, c.Y - ih / 3), c, rotation), Rotate((c.X + b / 2, c.Y - ih / 3), c, rotation)]; dimensions["Base"] = b; dimensions["Height"] = ih; break;
            case "Right":
                if (!Point(body, "Origin", out c) || !Vector(body, "Legs", out var legs) || !Positive(legs.X, legs.Y)) return Invalid(name, "invalid-dimensions", diagnostics);
                vertices = [c, Rotate((c.X + legs.X, c.Y), c, rotation), Rotate((c.X, c.Y + legs.Y), c, rotation)]; dimensions["LegX"] = legs.X; dimensions["LegY"] = legs.Y; break;
            case "Explicit":
                if (!Point(body, "A", out var a) || !Point(body, "B", out var bb) || !Point(body, "C", out var cc)) return Invalid(name, "invalid-dimensions", diagnostics);
                vertices = [a, bb, cc]; c = Centroid(vertices); rotation = 0; break;
            default: diagnostics.Add(Prefix + "variant-unsupported:" + name + ":" + variant); return null;
        }
        if (Math.Abs(SignedArea(vertices)) <= Tol || HasDuplicate(vertices)) return Invalid(name, "degenerate:" + variant, diagnostics);
        if (SignedArea(vertices) < 0) vertices = vertices.Reverse().ToArray();
        return Linear(name, "Triangle2", variant, c, rotation, vertices, ["A", "B", "C"], ["AB", "BC", "CA"], span, dimensions);
    }

    private static Shape? Circle(string source, string name, string body, double rotation, FirmamentV2SourceSpan span, List<string> diagnostics)
    {
        var inline = Point(body, "Center", out var c); var centerRef = Identifier(body, "Center");
        if (!inline && (centerRef is null || !ResolvePoint(source, centerRef, out c))) return null; // legacy/template Circle2 remains on its existing binder path
        var hasRadius = Length(body, "Radius", out var radius); var hasDiameter = Length(body, "Diameter", out var diameter);
        if (hasRadius == hasDiameter || (radius = hasRadius ? radius : diameter / 2) <= 0) return Invalid(name, "invalid-dimensions", diagnostics);
        var lowered = inline || hasDiameter ? $"Point2 {name}_Center {{ Position: [{N(c.X)}, {N(c.Y)}] }}\nConcept Circle2 {name}_Curve {{ Center: {name}_Center; Radius: {N(radius)} }}" : null;
        var guide = lowered is null ? name : name + "_Curve"; var map = new Dictionary<string, string>(StringComparer.Ordinal) { ["Center"] = inline || hasDiameter ? name + "_Center" : centerRef! };
        return Make(name, "Circle2", null, c, rotation, new Dictionary<string, double> { ["Radius"] = radius, ["Diameter"] = 2 * radius }, ["Center"], ["Boundary"], "Circle", span, lowered, map, guide + " |> TraceLoop", Math.PI * radius * radius, 2 * Math.PI * radius);
    }

    private static Shape? Ellipse(string name, string body, double rotation, FirmamentV2SourceSpan span, List<string> diagnostics)
    {
        if (!Point(body, "Center", out var c) || !(Vector(body, "AxisLengths", out var axes) || Vector(body, "Axes", out axes)) || !Positive(axes.X, axes.Y)) return Invalid(name, "invalid-dimensions", diagnostics);
        var major = Math.Max(axes.X, axes.Y) / 2; var minor = Math.Min(axes.X, axes.Y) / 2; if (axes.Y > axes.X) rotation += 90;
        var majorPositive = Rotate((c.X + major, c.Y), c, rotation); var majorNegative = Rotate((c.X - major, c.Y), c, rotation);
        var minorPositive = Rotate((c.X, c.Y + minor), c, rotation); var minorNegative = Rotate((c.X, c.Y - minor), c, rotation);
        var lowered = $"Point2 {name}_Center {{ Position: [{N(c.X)}, {N(c.Y)}] }}\nPoint2 {name}_MajorPositive {{ Position: [{N(majorPositive.X)}, {N(majorPositive.Y)}] }}\nPoint2 {name}_MajorNegative {{ Position: [{N(majorNegative.X)}, {N(majorNegative.Y)}] }}\nPoint2 {name}_MinorPositive {{ Position: [{N(minorPositive.X)}, {N(minorPositive.Y)}] }}\nPoint2 {name}_MinorNegative {{ Position: [{N(minorNegative.X)}, {N(minorNegative.Y)}] }}\nEllipse2Guide {name}_Curve {{ Center: {name}_Center; SemiAxes: [{N(major)}, {N(minor)}]; Rotation: {Ndeg(rotation)} }}";
        return Make(name, "Ellipse2", null, c, rotation, new Dictionary<string, double> { ["MajorAxisLength"] = 2 * major, ["MinorAxisLength"] = 2 * minor }, ["Center", "MajorPositive", "MajorNegative", "MinorPositive", "MinorNegative"], ["Boundary"], "Ellipse", span, lowered,
            new Dictionary<string, string> { ["Center"] = name + "_Center", ["MajorPositive"] = name + "_MajorPositive", ["MajorNegative"] = name + "_MajorNegative", ["MinorPositive"] = name + "_MinorPositive", ["MinorNegative"] = name + "_MinorNegative", ["Boundary"] = name + "_Curve" }, name + "_Curve |> TraceLoop", Math.PI * major * minor, EllipsePerimeter(major, minor));
    }

    private static Shape? Slot(string name, string body, double rotation, FirmamentV2SourceSpan span, List<string> diagnostics)
    {
        if (!Point(body, "Center", out var c) || !Length(body, "Length", out var length) || !Length(body, "Width", out var width) || width <= 0 || length < width) return Invalid(name, "invalid-dimensions", diagnostics);
        var straight = length - width; var r = width / 2; var start = Rotate((c.X - straight / 2, c.Y - r), c, rotation);
        if (straight <= Tol)
        {
            var lowerCircle = $"Point2 {name}_Center {{ Position: [{N(c.X)}, {N(c.Y)}] }}\nConcept Circle2 {name}_Curve {{ Center: {name}_Center; Radius: {N(r)} }}";
            return Make(name, "Slot2", null, c, rotation, new Dictionary<string, double> { ["OverallLength"] = length, ["Width"] = width }, ["Center"], ["Boundary"], "Circle", span, lowerCircle,
                new Dictionary<string, string> { ["Center"] = name + "_Center", ["Boundary"] = name + "_Curve" }, name + "_Curve |> TraceLoop", Math.PI * r * r, 2 * Math.PI * r);
        }
        var path = $"Concept Path {name}_Path {{ Start: Point2({N(start.X)}, {N(start.Y)}) Heading: {Ndeg(rotation)} Line Top {{ Length: {N(straight)} }} Arc EndArc {{ Radius: {N(r)}; Turn: 180deg }} Line Bottom {{ Length: {N(straight)} }} Arc StartArc {{ Radius: {N(r)}; Turn: 180deg }} }}";
        var guides = new[] { "Top", "EndArc", "Bottom", "StartArc" }; var map = guides.ToDictionary(x => x, x => name + "_Path." + x, StringComparer.Ordinal);
        return Make(name, "Slot2", null, c, rotation, new Dictionary<string, double> { ["OverallLength"] = length, ["Width"] = width }, [], guides, "Line+Circle", span, path, map, name + "_Path |> TraceLoop", width * straight + Math.PI * r * r, 2 * straight + 2 * Math.PI * r);
    }

    private static Shape? RoundedRect(string name, string body, double rotation, FirmamentV2SourceSpan span, List<string> diagnostics)
    {
        if (!Point(body, "Center", out var c) || !Vector(body, "Size", out var size) || !Length(body, "Radius", out var r) || !Positive(size.X, size.Y) || r < 0 || r > Math.Min(size.X, size.Y) / 2) return Invalid(name, "invalid-dimensions", diagnostics);
        if (r <= Tol) return Linear(name, "RoundedRect2", null, c, rotation, Rectangle(c, size.X, size.Y, rotation), ["BottomLeft", "BottomRight", "TopRight", "TopLeft"], ["Bottom", "Right", "Top", "Left"], span, new Dictionary<string, double> { ["Width"] = size.X, ["Height"] = size.Y, ["Radius"] = 0 });
        var start = Rotate((c.X - size.X / 2 + r, c.Y - size.Y / 2), c, rotation);
        var path = $"Concept Path {name}_Path {{ Start: Point2({N(start.X)}, {N(start.Y)}) Heading: {Ndeg(rotation)} Line Bottom {{ Length: {N(size.X - 2 * r)} }} Arc BottomRightCorner {{ Radius: {N(r)}; Turn: 90deg }} Line Right {{ Length: {N(size.Y - 2 * r)} }} Arc TopRightCorner {{ Radius: {N(r)}; Turn: 90deg }} Line Top {{ Length: {N(size.X - 2 * r)} }} Arc TopLeftCorner {{ Radius: {N(r)}; Turn: 90deg }} Line Left {{ Length: {N(size.Y - 2 * r)} }} Arc BottomLeftCorner {{ Radius: {N(r)}; Turn: 90deg }} }}";
        var guides = new[] { "Bottom", "BottomRightCorner", "Right", "TopRightCorner", "Top", "TopLeftCorner", "Left", "BottomLeftCorner" }; var map = guides.ToDictionary(x => x, x => name + "_Path." + x, StringComparer.Ordinal);
        var area = size.X * size.Y - (4 - Math.PI) * r * r; var perimeter = 2 * (size.X + size.Y - 4 * r) + 2 * Math.PI * r;
        return Make(name, "RoundedRect2", null, c, rotation, new Dictionary<string, double> { ["Width"] = size.X, ["Height"] = size.Y, ["Radius"] = r }, [], guides, "Line+Circle", span, path, map, name + "_Path |> TraceLoop", area, perimeter);
    }

    // A fixed polynomial corner law. The two cubic halves have collinear first
    // three controls at the side, and reflection symmetry gives G2 at the bisector.
    internal static IReadOnlyDictionary<string, LineArcProfileCurve2D> SmoothGuides(string source, List<string> diagnostics)
    {
        var guides = new Dictionary<string, LineArcProfileCurve2D>();
        var templates = FirmamentV2TemplateExpansion.DeclarationSpans(source, diagnostics);
        foreach (Match header in Header.Matches(source))
        {
            if (templates.Any(span => header.Index >= span.Start && header.Index < span.Start + span.Length)) continue;
            if (header.Groups["plain"].Value != "SmoothRoundedRect2") continue;
            var open = source.IndexOf('{', header.Index); var close = Matching(source, open);
            if (close < 0) continue;
            var body = source[(open + 1)..close];
            var rotation = Angle(body, "Rotation", 0d, out _);
            var shape = SmoothRoundedRect(header.Groups["name"].Value, body, rotation,
                new(header.Index, close - header.Index + 1), diagnostics);
            if (shape is null) continue;
            Point(body, "Center", out var center); Vector(body, "Size", out var size); Length(body, "CornerExtent", out var extent);
            foreach (var curve in SmoothCurves(center, size, extent, rotation)) guides.TryAdd(shape.Declaration.Name + "." + curve.Key, curve.Value);
        }
        return guides;
    }

    private static Shape? SmoothRoundedRect(string name, string body, double rotation, FirmamentV2SourceSpan span, List<string> diagnostics)
    {
        if (!Point(body, "Center", out var c) || !Vector(body, "Size", out var size) ||
            !Length(body, "CornerExtent", out var r) || !Positive(size.X, size.Y, r) || r >= Math.Min(size.X, size.Y) / 2)
            return Invalid(name, "invalid-dimensions", diagnostics);
        var curves = SmoothCurves(c, size, r, rotation);
        var edges = curves.Keys.ToArray();
        var map = edges.ToDictionary(edge => edge, edge => name + "." + edge);
        // Cubic area integration is exact; perimeter uses fixed 256-interval Simpson quadrature (an estimate).
        var area = curves.Values.Sum(ResolvedProfile2DValidator.SignedAreaContribution);
        var perimeter = curves.Values.Sum(curve => curve is LineArcLineSegment2D line ? Distance(line.Start, line.End)
            : SmoothCubicLength((LineArcCubicBezier2D)curve));
        return Make(name, "SmoothRoundedRect2", null, c, rotation,
            new Dictionary<string, double> { ["Width"] = size.X, ["Height"] = size.Y, ["CornerExtent"] = r },
            [], edges, "Line+CubicBezier", span, null, map,
            string.Join(" |> ", edges.Select(edge => name + "." + edge + " As " + edge)) + " |> Close", area, perimeter);
    }

    private static Dictionary<string, LineArcProfileCurve2D> SmoothCurves((double X, double Y) c, (double X, double Y) size, double r, double rotation)
    {
        var result = new Dictionary<string, LineArcProfileCurve2D>();
        var sides = new[] { "Bottom", "Right", "Top", "Left" };
        var corners = new[] { "BottomRightCorner", "TopRightCorner", "TopLeftCorner", "BottomLeftCorner" };
        var b = Math.Pow(0.5, 0.25); var q = 1 - b; var a = b - 0.5;
        for (var i = 0; i < 4; i++)
        {
            var w = i % 2 == 0 ? size.X : size.Y; var h = i % 2 == 0 ? size.Y : size.X;
            (double X, double Y) Transform(double x, double y) => Rotate((c.X + x, c.Y + y), c, rotation + 90 * i);
            (double X, double Y) Corner(double x, double y) => Transform(w / 2 - r + r * x, -h / 2 + r * y);
            result.Add(sides[i], new LineArcLineSegment2D(Transform(-w / 2 + r, -h / 2), Corner(0, 0)));
            result.Add(corners[i] + "A", new LineArcCubicBezier2D(Corner(0, 0), Corner(a, 0), Corner(2 * a, 0), Corner(b, q)));
            result.Add(corners[i] + "B", new LineArcCubicBezier2D(Corner(b, q), Corner(1, 1 - 2 * a), Corner(1, 1 - a), Corner(1, 1)));
        }
        return result;
    }

    private static double SmoothCubicLength(LineArcCubicBezier2D curve)
    {
        double Speed(double t)
        {
            var u = 1 - t;
            var x = 3 * (u * u * (curve.Control1.X - curve.Start.X) + 2 * u * t * (curve.Control2.X - curve.Control1.X) + t * t * (curve.End.X - curve.Control2.X));
            var y = 3 * (u * u * (curve.Control1.Y - curve.Start.Y) + 2 * u * t * (curve.Control2.Y - curve.Control1.Y) + t * t * (curve.End.Y - curve.Control2.Y));
            return Math.Sqrt(x * x + y * y);
        }
        const int n = 256;
        return (Speed(0) + Speed(1) + Enumerable.Range(1, n - 1).Sum(i => (i % 2 == 0 ? 2 : 4) * Speed((double)i / n))) / (3 * n);
    }

    private static Shape? Polygon(string source, string name, string variant, string body, double rotation, FirmamentV2SourceSpan span, List<string> diagnostics)
    {
        IReadOnlyList<(double X, double Y)> vertices; IReadOnlyList<string> pointNames; Dictionary<string, double> dimensions = []; (double X, double Y) c;
        switch (variant)
        {
            case "Rhombus":
                if (!Point(body, "Center", out c) || !Vector(body, "Diagonals", out var d) || !Positive(d.X, d.Y)) { diagnostics.Add("firmament-polygon2-invalid-geometry:" + name); return null; }
                vertices = [Rotate((c.X, c.Y - d.Y / 2), c, rotation), Rotate((c.X + d.X / 2, c.Y), c, rotation), Rotate((c.X, c.Y + d.Y / 2), c, rotation), Rotate((c.X - d.X / 2, c.Y), c, rotation)]; pointNames = ["South", "East", "North", "West"]; dimensions["DiagonalX"] = d.X; dimensions["DiagonalY"] = d.Y; break;
            case "Parallelogram":
                if (!Point(body, "Center", out c) || !Length(body, "Base", out var b) || !Length(body, "Height", out var h) || !Length(body, "Shear", out var shear) || !Positive(b, h)) return Invalid(name, "invalid-dimensions", diagnostics);
                vertices = new (double X, double Y)[] { (-b / 2 - shear / 2, -h / 2), (b / 2 - shear / 2, -h / 2), (b / 2 + shear / 2, h / 2), (-b / 2 + shear / 2, h / 2) }.Select(p => Rotate((p.X + c.X, p.Y + c.Y), c, rotation)).ToArray(); pointNames = ["BottomLeft", "BottomRight", "TopRight", "TopLeft"]; dimensions["Base"] = b; dimensions["Height"] = h; dimensions["Shear"] = shear; break;
            case "Trapezoid":
                if (!Point(body, "Center", out c) || !Length(body, "BottomWidth", out var bw) || !Length(body, "TopWidth", out var tw) || !Length(body, "Height", out var th) || !Positive(bw, tw, th)) return Invalid(name, "invalid-dimensions", diagnostics);
                vertices = new (double X, double Y)[] { (-bw / 2, -th / 2), (bw / 2, -th / 2), (tw / 2, th / 2), (-tw / 2, th / 2) }.Select(p => Rotate((p.X + c.X, p.Y + c.Y), c, rotation)).ToArray(); pointNames = ["BottomLeft", "BottomRight", "TopRight", "TopLeft"]; dimensions["BottomWidth"] = bw; dimensions["TopWidth"] = tw; dimensions["Height"] = th; break;
            case "Explicit":
                var setName = Identifier(body, "Vertices");
                if (setName is null || !PointSet(source, setName, out pointNames, out vertices) || vertices.Count < 3) return Invalid(name, "explicit-too-few-vertices", diagnostics);
                if (HasDuplicate(vertices)) return Invalid(name, "explicit-duplicate-vertex", diagnostics); if (SelfIntersects(vertices)) return Invalid(name, "explicit-self-intersection", diagnostics); if (Math.Abs(SignedArea(vertices)) <= Tol) return Invalid(name, "explicit-degenerate-area", diagnostics);
                c = PolygonCentroid(vertices); rotation = 0; break;
            default: diagnostics.Add("firmament-polygon2-variant-unsupported:" + name + ":" + variant); return null;
        }
        if (SignedArea(vertices) < 0) { vertices = vertices.Reverse().ToArray(); pointNames = pointNames.Reverse().ToArray(); }
        var edgeNames = Enumerable.Range(0, vertices.Count).Select(i => pointNames[i] + "_" + pointNames[(i + 1) % vertices.Count]).ToArray();
        return Linear(name, "Polygon2", variant, c, rotation, vertices, pointNames, edgeNames, span, dimensions);
    }

    private static Shape? RegularPolygon(string name, string countText, string body, double rotation, FirmamentV2SourceSpan span, List<string> diagnostics)
    {
        if (!int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) || count is < 3 or > 1024) return Invalid(name, "regular-count-out-of-range", diagnostics);
        if (!Point(body, "Center", out var c)) return Invalid(name, "invalid-center", diagnostics); double radius; var dimensions = new Dictionary<string, double> { ["VertexCount"] = count };
        if (Length(body, "Circumradius", out radius) && radius > 0) dimensions["Circumradius"] = radius;
        else if (Length(body, "AcrossFlats", out var flats) && flats > 0) { radius = flats / (2 * Math.Cos(Math.PI / count)); dimensions["AcrossFlats"] = flats; }
        else return Invalid(name, "invalid-size-parameter", diagnostics);
        var vertices = Enumerable.Range(0, count).Select(i => { var a = (rotation + 360d * i / count) * Math.PI / 180d; return (c.X + radius * Math.Cos(a), c.Y + radius * Math.Sin(a)); }).ToArray();
        return Linear(name, "RegularPolygon2", countText, c, rotation, vertices, Enumerable.Range(0, count).Select(i => "Vertex" + i).ToArray(), Enumerable.Range(0, count).Select(i => "Edge" + i).ToArray(), span, dimensions);
    }

    private static Shape Linear(string name, string type, string? variant, (double X, double Y) c, double rotation, IReadOnlyList<(double X, double Y)> vertices, IReadOnlyList<string> pointNames, IReadOnlyList<string> edgeNames, FirmamentV2SourceSpan span, IReadOnlyDictionary<string, double> dimensions)
    {
        var text = new StringBuilder(); var generatedPoints = new List<string>(); var generatedEdges = new List<string>(); var map = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < vertices.Count; i++) { var id = name + "_" + pointNames[i]; generatedPoints.Add(id); map[pointNames[i]] = id; text.Append("Point2 ").Append(id).Append(" { Position: [").Append(N(vertices[i].X)).Append(", ").Append(N(vertices[i].Y)).AppendLine("] }"); }
        for (var i = 0; i < vertices.Count; i++) { var id = name + "_" + edgeNames[i]; generatedEdges.Add(id); map[edgeNames[i]] = id; text.Append("Line2 ").Append(id).Append(" { From: ").Append(generatedPoints[i]).Append("; To: ").Append(generatedPoints[(i + 1) % vertices.Count]).AppendLine(" }"); }
        var area = Math.Abs(SignedArea(vertices)); var perimeter = Enumerable.Range(0, vertices.Count).Sum(i => Distance(vertices[i], vertices[(i + 1) % vertices.Count]));
        var trace = string.Join(" |> ", generatedEdges.Select((edge, i) => edge + " As " + edgeNames[i])) + " |> Close";
        return Make(name, type, variant, c, rotation, dimensions, pointNames, edgeNames, "Line", span, text.ToString(), map, trace, area, perimeter);
    }

    private static Shape Make(string name, string type, string? variant, (double X, double Y) c, double rotation, IReadOnlyDictionary<string, double> dimensions, IReadOnlyList<string> points, IReadOnlyList<string> edges, string carrier, FirmamentV2SourceSpan span, string? lower, IReadOnlyDictionary<string, string> map, string trace, double area, double perimeter) =>
        new(new(name, type, variant, "ClosedBoundary2", c.X, c.Y, rotation, dimensions, points, edges, carrier, area, perimeter, span), lower, map, trace, span.Start, span.Length);
    private static Shape? Invalid(string name, string reason, List<string> diagnostics) { diagnostics.Add(Prefix + reason + ":" + name); return null; }
    private static bool Inside(int index, IReadOnlyList<Shape> shapes) => shapes.Any(x => index > x.Start && index < x.Start + x.Length);
    private static IReadOnlyList<(double X, double Y)> Rectangle((double X, double Y) c, double w, double h, double r) => [Rotate((c.X - w / 2, c.Y - h / 2), c, r), Rotate((c.X + w / 2, c.Y - h / 2), c, r), Rotate((c.X + w / 2, c.Y + h / 2), c, r), Rotate((c.X - w / 2, c.Y + h / 2), c, r)];
    private static (double X, double Y) Rotate((double X, double Y) p, (double X, double Y) c, double degrees) { var a = degrees * Math.PI / 180; var x = p.X - c.X; var y = p.Y - c.Y; return (c.X + x * Math.Cos(a) - y * Math.Sin(a), c.Y + x * Math.Sin(a) + y * Math.Cos(a)); }
    private static bool Positive(params double[] values) => values.All(x => double.IsFinite(x) && x > 0);
    private static double SignedArea(IReadOnlyList<(double X, double Y)> p) => Enumerable.Range(0, p.Count).Sum(i => p[i].X * p[(i + 1) % p.Count].Y - p[(i + 1) % p.Count].X * p[i].Y) / 2;
    private static (double X, double Y) Centroid(IReadOnlyList<(double X, double Y)> p) => (p.Average(x => x.X), p.Average(x => x.Y));
    private static (double X, double Y) PolygonCentroid(IReadOnlyList<(double X, double Y)> p) { var a = SignedArea(p); var cx = 0d; var cy = 0d; for (var i = 0; i < p.Count; i++) { var q = p[(i + 1) % p.Count]; var cross = p[i].X * q.Y - q.X * p[i].Y; cx += (p[i].X + q.X) * cross; cy += (p[i].Y + q.Y) * cross; } return (cx / (6 * a), cy / (6 * a)); }
    private static bool HasDuplicate(IReadOnlyList<(double X, double Y)> p) => p.SelectMany((a, i) => p.Skip(i + 1).Select(b => Distance(a, b))).Any(x => x <= Tol);
    private static bool SelfIntersects(IReadOnlyList<(double X, double Y)> p) { for (var i = 0; i < p.Count; i++) for (var j = i + 1; j < p.Count; j++) { if (j == i + 1 || i == 0 && j == p.Count - 1) continue; if (Intersects(p[i], p[(i + 1) % p.Count], p[j], p[(j + 1) % p.Count])) return true; } return false; }
    private static bool Intersects((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) d) => Cross(a, b, c) * Cross(a, b, d) < -Tol && Cross(c, d, a) * Cross(c, d, b) < -Tol;
    private static double Cross((double X, double Y) a, (double X, double Y) b, (double X, double Y) c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
    private static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static double EllipsePerimeter(double a, double b) => Math.PI * (3 * (a + b) - Math.Sqrt((3 * a + b) * (a + 3 * b)));
    private static bool Point(string body, string field, out (double X, double Y) value)
    {
        value = default;
        var expression = ProfileAuthoringParser.Property(body, field)?.Trim();
        if (expression is null) return false;
        string components;
        if (expression.StartsWith("[") && expression.EndsWith("]")) components = expression[1..^1];
        else if (expression.StartsWith("Point2(") && expression.EndsWith(")")) components = expression[7..^1];
        else return false;
        var items = components.Split(',');
        return items.Length == 2 && ProfileAuthoringParser.TryMeasure(items[0], "mm", out value.X)
            && ProfileAuthoringParser.TryMeasure(items[1], "mm", out value.Y);
    }
    private static bool Vector(string body, string field, out (double X, double Y) value) => Point(body, field, out value);
    private static bool Length(string body, string field, out double value) =>
        ProfileAuthoringParser.TryMeasure(ProfileAuthoringParser.Property(body, field) ?? "", "mm", out value);
    private static double Angle(string body, string field, double fallback, out bool valid) { var m = Regex.Match(body, $@"\b{field}\s*:\s*(?<v>[-+.\deE]+)deg\b", RegexOptions.CultureInvariant); if (!m.Success) { valid = true; return fallback; } valid = Number(m.Groups["v"].Value, out var value); return value; }
    private static string? Identifier(string body, string field) { var m = Regex.Match(body, $@"\b{field}\s*:\s*(?<v>[A-Za-z_]\w*)\b", RegexOptions.CultureInvariant); return m.Success ? m.Groups["v"].Value : null; }
    private static bool ResolvePoint(string source, string name, out (double X, double Y) p) { var m = Regex.Match(source, $@"\bPoint2\s+{Regex.Escape(name)}\s*\{{\s*Position\s*:\s*(?:\[|Point2\s*\()\s*(?<x>[-+.\deE]+)mm\s*,\s*(?<y>[-+.\deE]+)mm", RegexOptions.CultureInvariant); p = default; return m.Success && Number(m.Groups["x"].Value, out p.X) && Number(m.Groups["y"].Value, out p.Y); }
    private static bool PointSet(string source, string name, out IReadOnlyList<string> names, out IReadOnlyList<(double X, double Y)> points) { names = []; points = []; var h = Regex.Match(source, $@"\bStatic\s+{Regex.Escape(name)}\s*:\s*Set\s*<\s*Point2\s*>\s*\{{", RegexOptions.CultureInvariant); if (!h.Success) return false; var open = source.IndexOf('{', h.Index); var close = Matching(source, open); if (close < 0) return false; var ns = new List<string>(); var ps = new List<(double, double)>(); foreach (Match e in Regex.Matches(source[(open + 1)..close], @"\b(?<n>[A-Za-z_]\w*)\s*=>\s*Point2\s*\(\s*(?<x>[-+.\deE]+)mm\s*,\s*(?<y>[-+.\deE]+)mm\s*\)", RegexOptions.CultureInvariant)) if (Number(e.Groups["x"].Value, out var x) && Number(e.Groups["y"].Value, out var y)) { ns.Add(e.Groups["n"].Value); ps.Add((x, y)); } names = ns; points = ps; return true; }
    private static bool Number(string text, out double value) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    private static string N(double value) => (Math.Abs(value) < 1e-13 ? 0d : value).ToString("R", CultureInfo.InvariantCulture) + "mm";
    private static string Ndeg(double value) => value.ToString("R", CultureInfo.InvariantCulture) + "deg";
    private static int Matching(string source, int open) { var depth = 0; for (var i = open; i >= 0 && i < source.Length; i++) { if (source[i] == '{') depth++; else if (source[i] == '}' && --depth == 0) return i; } return -1; }
}
