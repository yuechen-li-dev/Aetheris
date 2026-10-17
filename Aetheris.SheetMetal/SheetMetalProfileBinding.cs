using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.SheetMetal;

/// <summary>Sheet capability admission over the ordinary resolved Profile authority.</summary>
internal static class SheetMetalProfileBinding
{
    private const double Tol = 1e-7;

    internal static AuthoredSheetBaseSpec? Base(string source, string name, FirmamentSyntaxFields fields, List<string> errors,FirmamentDomainBinding? binding=null)
    {
        var value = fields.Take("Profile");
        var origin = fields.Take("Origin");
        var offset = Vector3(origin) ?? Point3D.Origin;
        if (origin is not null && Vector3(origin) is null) errors.Add($"sheetmetal-base-origin-invalid: Base '{name}' origin requires three Length coordinates.");
        var result = Base(source, name, value, offset, errors, binding);
        fields.Complete();
        return result;
    }

    internal static AuthoredSheetBaseSpec? Base(string source, string name, ExpressionSyntax? value,
        Point3D offset, List<string> errors, FirmamentDomainBinding? binding = null)
    {
        AuthoredSheetBaseSpec? result = null;
        if (value is ObjectSyntax { Type: ReferenceSyntax { Name.Text: "Rectangle" } } legacy)
        {
            var dimensions = FirmamentSyntaxFields.Object(legacy, name, errors);
            var width = dimensions.Number("Width", "mm");
            var height = dimensions.Take("Height") ?? dimensions.Take("Depth");
            if (FirmamentSyntaxValues.TryMeasure(height, "mm", out var depth)) result = new(name, width, depth) { Origin = offset };
            dimensions.Complete();
        }
        else if (Resolve(source, value, errors,binding) is { } profile)
        {
            var segments = profile.Loops.Single().Segments;
            var bounds = Bounds(profile);
            var lines = segments.Select(s => s.Geometry).OfType<LineArcLineSegment2D>().ToArray();
            var corners = new[] { (bounds.MinX, bounds.MinY), (bounds.MaxX, bounds.MinY), (bounds.MaxX, bounds.MaxY), (bounds.MinX, bounds.MaxY) };
            if (lines.Length == 4 && segments.Count == 4 && lines.All(l => Math.Abs(l.Start.X - l.End.X) < Tol || Math.Abs(l.Start.Y - l.End.Y) < Tol)
                && corners.All(c => lines.Any(l => Distance(c, l.Start) < Tol)))
                result = new(name, bounds.MaxX - bounds.MinX, bounds.MaxY - bounds.MinY)
                    { Origin = new(offset.X + bounds.MinX, offset.Y + bounds.MinY, offset.Z) };
            else errors.Add($"sheetmetal-base-profile-unsupported: Base '{name}' requires one axis-aligned rectangular Profile; arbitrary blank boundaries are not admitted by the formed/flat lowerer.");
        }
        return result;
    }

    internal static AuthoredSheetCutSpec? Cut(string source, string name, FirmamentSyntaxFields fields, List<string> errors,FirmamentDomainBinding? binding=null)
    {
        var on = fields.Word("On");
        var atValue = fields.Take("At") ?? fields.Take("Center");
        var at = atValue is null ? (X: 0d, Y: 0d) : Point(atValue);
        var value = fields.Take("Profile");
        if (on is null || at is null) { errors.Add($"sheetmetal-cut-placement-invalid: Cut '{name}' requires a named region and a valid Point2 offset."); return null; }

        // Historical Slot/Rectangle cuts are rectangular. Preserve their exact
        // interpretation; modern Slot2 reaches this owner as an analytic Profile.
        var legacy = value is ObjectSyntax o ? FirmamentSyntaxValues.Reference(o.Type) : FirmamentSyntaxValues.Reference(value);
        var namedProfile = value is not ObjectSyntax && FirmamentSyntaxTraversal.Declarations(FirmamentSyntaxParser.Parse(source).Document)
            .Any(d => d.Type.Name.Text == "Profile" && d.Name?.Text == legacy);
        if (!namedProfile && (legacy is "Circle" or "Slot" or "Rectangle"))
        {
            if (atValue is null) { errors.Add($"sheetmetal-cut-placement-invalid: Legacy Cut '{name}' requires At or Center."); return null; }
            var dimensions = value is ObjectSyntax obj ? FirmamentSyntaxFields.Object(obj, name, errors) : fields;
            var diameter = legacy == "Circle" ? dimensions.Number("Diameter", "mm") : double.NaN;
            var width = legacy != "Circle" ? dimensions.Number("Width", "mm") : double.NaN;
            var length = legacy != "Circle" ? dimensions.Number("Length", "mm") : double.NaN;
            if (!ReferenceEquals(dimensions, fields)) dimensions.Complete();
            fields.Complete();
            if (legacy == "Circle" && diameter > 0) return new(name, on, SheetFeatureKind.CircularHole, at.Value.X, at.Value.Y, diameter, null, null);
            if (legacy != "Circle" && width > 0 && length > 0) return new(name, on, SheetFeatureKind.Slot, at.Value.X, at.Value.Y, null, width, length);
            errors.Add($"sheetmetal-cut-profile-invalid: Cut '{name}' requires positive profile dimensions."); return null;
        }
        var resolved = Resolve(source, value, errors,binding);
        fields.Complete();
        if (resolved is null) return null;
        LineArcProfileCurve2D Move(LineArcProfileCurve2D curve) => curve switch
        {
            LineArcLineSegment2D l => new LineArcLineSegment2D((l.Start.X + at.Value.X, l.Start.Y + at.Value.Y), (l.End.X + at.Value.X, l.End.Y + at.Value.Y)),
            LineArcCircularArc2D a => a with { Center = (a.Center.X + at.Value.X, a.Center.Y + at.Value.Y) },
            LineArcFullCircle2D c => c with { Center = (c.Center.X + at.Value.X, c.Center.Y + at.Value.Y) },
            _ => throw new InvalidOperationException("Profile capability was not admitted.")
        };
        var profile = resolved with { Loops = resolved.Loops.Select(l => l with { Segments = l.Segments.Select(s => s with { Geometry = Move(s.Geometry) }).ToArray() }).ToArray() };
        if (profile.Loops[0].Segments is [ { Geometry: LineArcFullCircle2D circle } ])
            return new(name, on, SheetFeatureKind.CircularHole, circle.Center.X, circle.Center.Y, circle.Radius * 2, null, null);
        var box = Bounds(profile);
        return new(name, on, SheetFeatureKind.ProfileHole, (box.MinX + box.MaxX) / 2, (box.MinY + box.MaxY) / 2,
            null, box.MaxY - box.MinY, box.MaxX - box.MinX) { Profile = profile };
    }

    private static ResolvedProfile2D? Resolve(string source, ExpressionSyntax? value, List<string> errors,FirmamentDomainBinding? binding)
    {
        var name = FirmamentSyntaxValues.Reference(value);
        if (name is null) { errors.Add("sheetmetal-profile-reference-required: Declare a named Profile with Rect2, Circle2, Slot2 or an exact line/arc boundary, then reference it."); return null; }
        IReadOnlyList<string> diagnostics;
        var profile = binding is null?ProfileAuthoringParser.ResolveNamedProfile(source, name, out diagnostics):binding.ResolveProfile(name,out diagnostics);
        errors.AddRange(diagnostics);
        if (profile is null) return null;
        var frame = profile.EffectiveConstructionPlane;
        if (frame.Origin != Point3D.Origin || (frame.AxisX.ToVector() - new Vector3D(1, 0, 0)).Length > Tol
            || (frame.AxisY.ToVector() - new Vector3D(0, 1, 0)).Length > Tol)
        { errors.Add($"sheetmetal-profile-frame-unsupported: Profile '{name}' must be in local XY; use the Base origin or Cut at offset for placement."); return null; }
        if (profile.Loops.Count != 1 || profile.Loops[0].Segments.Any(s => s.Geometry is not (LineArcLineSegment2D or LineArcCircularArc2D or LineArcFullCircle2D)))
        { errors.Add($"sheetmetal-profile-capability-unsupported: Profile '{name}' requires one closed line/arc loop or a circle; islands, ellipses and splines are not admitted for sheet regions/cuts."); return null; }
        return profile;
    }

    internal static (double X, double Y)? Point(ExpressionSyntax? value)
    {
        if (value is SequenceSyntax { Kind: SequenceKind.Tuple } tuple) value = tuple with { Kind = SequenceKind.List };
        return FirmamentSyntaxValues.Vector(value, "mm", out var values) && values.Length == 2 ? (values[0], values[1]) : null;
    }

    private static Point3D? Vector3(ExpressionSyntax? value)
    {
        if (value is SequenceSyntax { Kind: SequenceKind.Tuple } tuple) value = tuple with { Kind = SequenceKind.List };
        return FirmamentSyntaxValues.Vector(value, "mm", out var values) && values.Length == 3 ? new(values[0], values[1], values[2]) : null;
    }

    internal static (double MinX, double MinY, double MaxX, double MaxY) Bounds(ResolvedProfile2D profile)
    {
        var points = new List<(double X, double Y)>();
        foreach (var s in profile.Loops[0].Segments)
            switch (s.Geometry)
            {
                case LineArcLineSegment2D l: points.Add(l.Start); points.Add(l.End); break;
                case LineArcFullCircle2D c: points.Add((c.Center.X - c.Radius, c.Center.Y - c.Radius)); points.Add((c.Center.X + c.Radius, c.Center.Y + c.Radius)); break;
                case LineArcCircularArc2D a:
                    Add(a.StartAngleRadians); Add(a.StartAngleRadians + a.SweepAngleRadians);
                    for (var i = 0; i < 4; i++)
                    {
                        var angle = i * Math.PI / 2;
                        var delta = a.SweepAngleRadians >= 0 ? angle - a.StartAngleRadians : a.StartAngleRadians - angle;
                        delta = (delta % Math.Tau + Math.Tau) % Math.Tau;
                        if (delta <= Math.Abs(a.SweepAngleRadians) + Tol) Add(angle);
                    }
                    void Add(double angle) => points.Add((a.Center.X + a.Radius * Math.Cos(angle), a.Center.Y + a.Radius * Math.Sin(angle)));
                    break;
            }
        return (points.Min(p => p.X), points.Min(p => p.Y), points.Max(p => p.X), points.Max(p => p.Y));
    }

    private static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}
