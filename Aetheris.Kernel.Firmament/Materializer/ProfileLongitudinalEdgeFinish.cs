using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>Exact full-length finish of one polygon corner in an extruded profile.</summary>
public static class ProfileLongitudinalEdgeFinish
{
    public const double Clearance = .01;
    public static bool IsLongitudinal(DeclarationSyntax declaration) => declaration.Type.Name.Text == "EdgeFinish"
        && declaration.Body?.Statements.OfType<FieldSyntax>().Any(f => (FirmamentSyntaxFields.SameField(f.Name.Text,"Face") || FirmamentSyntaxFields.SameField(f.Name.Text,"On"))
            && FirmamentSyntaxValues.Reference(f.Value) == "Longitudinal") == true;
    public static bool Requested(string source) => ProfileBoundaryChamferSourceBinder.FinishDeclarations(source).Any(IsLongitudinal);

    public sealed record Result(ResolvedProfile2D? Profile, string? Name, string? Kind, string? Diagnostic, double Size = 0, string? Target = null, bool AddsMaterial = false);
    public sealed record Batch(ResolvedProfile2D? Profile, IReadOnlyList<Result> Finishes, string? Diagnostic);
    public static Batch ApplyAll(string source, ResolvedProfile2D profile, string body)
    {
        var declarations = ProfileBoundaryChamferSourceBinder.FinishDeclarations(source).ToArray();
        if (declarations.Length > 0 && (profile.Loops.Count != 1 || !profile.Loops[0].IsOuter
            || profile.Loops[0].Segments.Any(s => s.Geometry is not LineArcLineSegment2D)))
            return new(null, [], "longitudinal-edge-finish: Start with a polygonal outer profile without inner loops.");
        var results = new List<Result>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var targets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var declaration in declarations)
        {
            var finish = ApplyOne(source, declaration, profile, body);
            if (finish.Profile is null) return new(null, results, finish.Diagnostic);
            if (!names.Add(finish.Name!) || !targets.Add(finish.Target!))
                return new(null, results, "longitudinal-edge-finish: Each finish needs a unique name and a different original corner.");
            results.Add(finish);
            profile = finish.Profile;
        }
        return new(profile, results, null);
    }
    public static Result Apply(string source, ResolvedProfile2D profile, string body)
    {
        var declarations = ProfileBoundaryChamferSourceBinder.FinishDeclarations(source).ToArray();
        return declarations.Length == 1 ? ApplyOne(source, declarations[0], profile, body)
            : new(null, null, null, "longitudinal-edge-finish: Expected one finish.");
    }
    private static Result ApplyOne(string source, DeclarationSyntax declaration, ResolvedProfile2D profile, string body)
    {
        Result Fail(string reason) => new(null, declaration.Name?.Text, null, "longitudinal-edge-finish:" + declaration.Name?.Text + ": " + reason);
        var errors = new List<string>(); var fields = new FirmamentSyntaxFields(declaration, errors);
        if (declaration.Name is null) return Fail("Give the edge finish a name.");
        var receiver = FirmamentV2Parser.MechanicalDeclarations(FirmamentSyntaxParser.Parse(source).Document.Statements)
            .SingleOrDefault(d => d.Type.Name.Text == "Modify" && d.Body?.Statements.OfType<DeclarationSyntax>().Any(f => f.Span == declaration.Span) == true);
        if (receiver?.Name?.Text != body) return Fail("The selected edge must belong to the modified extrusion.");
        var face = FirmamentSyntaxValues.Reference(fields.Take("Face") ?? fields.Take("On"));
        var target = FirmamentSyntaxValues.Reference(fields.Take("Target"));
        var kind = FirmamentSyntaxValues.Reference(fields.Take("Kind"));
        var variant = declaration.Type.Arguments.Count == 1 ? FirmamentSyntaxValues.Reference(declaration.Type.Arguments[0].Value) : null;
        if (declaration.Type.Arguments.Count > 1 || declaration.Type.Arguments.Count == 1 && variant is not ("Fillet" or "Chamfer")) return Fail("Use EdgeFinish<Fillet> or EdgeFinish<Chamfer>.");
        if (kind is not null && variant is not null && kind != variant) return Fail("Kind must agree with the EdgeFinish variant.");
        kind ??= variant;
        var radius = fields.Take("Radius"); var distance = fields.Take("Distance");
        if (radius is not null && distance is not null || kind is not ("Fillet" or "Chamfer") || face != "Longitudinal"
            || kind == "Fillet" && radius is null || kind == "Chamfer" && distance is null
            || !FirmamentSyntaxValues.TryMeasure(radius ?? distance, "mm", out var amount) || !double.IsFinite(amount) || amount < Clearance)
            return Fail("Use a fillet Radius or chamfer Distance of at least 0.01 mm, and Face: Longitudinal.");
        fields.Complete(); if (errors.Count != 0) return Fail(string.Join("; ", errors));
        var parts = target?.Split('.') ?? [];
        if (parts.Length != 4 || parts[0] != profile.Name || parts[1] != "Outer" || parts[3] != "Start") return Fail("Select one source-owned longitudinal corner edge.");
        if (profile.Loops.Count != 1 || !profile.Loops[0].IsOuter || profile.Loops[0].Segments.Any(s => s.Geometry is not (LineArcLineSegment2D or LineArcCircularArc2D)))
            return Fail("Full-length corner finishes currently require a single polygonal outer profile without inner loops.");
        var loop = profile.Loops[0]; var segments = loop.Segments.ToArray();
        if (segments.Count(s => s.Name == parts[2]) != 1) return Fail("The selected corner is missing or ambiguous in this profile.");
        var index = Array.FindIndex(segments, s => s.Name == parts[2]);
        if (index < 0) return Fail("The selected corner no longer exists in this profile.");
        var previousIndex = (index + segments.Length - 1) % segments.Length;
        if (segments[previousIndex].Geometry is not LineArcLineSegment2D || segments[index].Geometry is not LineArcLineSegment2D)
            return Fail("This corner is already finished or is curved. Choose a different straight-sided corner.");
        var classification = ProfileJunctionClassifier.Classify(profile, loop).Single(j => j.SuccessorSegmentId == parts[2]).Classification;
        if (classification is not (ProfileJunctionKind.ConvexProfileJunction or ProfileJunctionKind.ReflexProfileJunction)) return Fail("Select a nondegenerate corner between two straight sides.");
        var previous = (LineArcLineSegment2D)segments[previousIndex].Geometry;
        var next = (LineArcLineSegment2D)segments[index].Geometry;
        LineArcProfileCurve2D replacement;
        if (kind == "Fillet")
        {
            var round = ProfileCornerRound.TryRound(previous, next, amount, out var reason);
            if (round is null) return Fail(reason ?? "This corner cannot be rounded.");
            previous = (LineArcLineSegment2D)round.Previous; next = (LineArcLineSegment2D)round.Next; replacement = round.Arc;
        }
        else
        {
            var a = Length(previous); var b = Length(next);
            if (Math.Min(a, b) - amount < Clearance) return Fail("The distance consumes an adjacent edge. Leave at least 0.01 mm of straight edge.");
            var start = (X: previous.End.X + (previous.Start.X - previous.End.X) * amount / a, Y: previous.End.Y + (previous.Start.Y - previous.End.Y) * amount / a);
            var end = (X: next.Start.X + (next.End.X - next.Start.X) * amount / b, Y: next.Start.Y + (next.End.Y - next.Start.Y) * amount / b);
            previous = previous with { End = start }; next = next with { Start = end }; replacement = new LineArcLineSegment2D(start, end);
        }
        if (Math.Min(Length(previous), Length(next)) < Clearance) return Fail("The size consumes an adjacent edge. Leave at least 0.01 mm of straight edge.");
        foreach (var other in segments.Where((_, i) => i != index && i != previousIndex).Select(s => s.Geometry))
            if (CurveDistance(replacement, other) < Clearance - 1e-8) return Fail("The finish reaches another boundary or finish. Reduce its size to retain at least 0.01 mm clearance.");
        segments[previousIndex] = segments[previousIndex] with { Geometry = previous };
        segments[index] = segments[index] with { Geometry = next };
        var name = declaration.Name!.Text;
        var added = new ResolvedProfileSegment2D(name, replacement,
            new("edgefinish:" + name, profile.Name, $"offset:{declaration.Span.Start}", "FullLength" + kind, profile.PlaneFrame));
        var result = profile with { Loops = [loop with { Segments = segments.Take(index).Concat([added]).Concat(segments.Skip(index)).ToArray() }] };
        var validation = ResolvedProfile2DValidator.Validate(result);
        return validation.IsValid ? new(result, name, kind, null, amount, target, classification == ProfileJunctionKind.ReflexProfileJunction) : Fail(string.Join("; ", validation.Diagnostics));
    }

    private static double Length(LineArcLineSegment2D l) => Hypot(l.End.X - l.Start.X, l.End.Y - l.Start.Y);
    private static double CurveDistance(LineArcProfileCurve2D a, LineArcProfileCurve2D b)
    {
        if (b is LineArcLineSegment2D line) return Distance(a, line);
        if (a is LineArcLineSegment2D otherLine) return Distance(b, otherLine);
        var first = (LineArcCircularArc2D)a; var second = (LineArcCircularArc2D)b;
        var candidates = new List<double>();
        foreach (var p in new[] { At(first, first.StartAngleRadians), At(first, first.StartAngleRadians + first.SweepAngleRadians) }) candidates.Add(PointArc(p, second));
        foreach (var p in new[] { At(second, second.StartAngleRadians), At(second, second.StartAngleRadians + second.SweepAngleRadians) }) candidates.Add(PointArc(p, first));
        var dx = second.Center.X - first.Center.X; var dy = second.Center.Y - first.Center.Y; var centers = Hypot(dx, dy);
        if (centers > 1e-12)
        {
            var angle = Math.Atan2(dy, dx);
            foreach (var signA in new[] { 0d, Math.PI }) foreach (var signB in new[] { 0d, Math.PI })
            {
                var p = At(first, angle + signA); var q = At(second, angle + signB);
                if (InArc(p, first) && InArc(q, second)) candidates.Add(Hypot(p.X - q.X, p.Y - q.Y));
            }
            var along = (first.Radius * first.Radius - second.Radius * second.Radius + centers * centers) / (2 * centers);
            var height2 = first.Radius * first.Radius - along * along;
            if (height2 >= -1e-10)
            {
                foreach (var sign in new[] { -1d, 1d })
                {
                    var h = sign * Math.Sqrt(Math.Max(0, height2));
                    var p = (X: first.Center.X + (along * dx - h * dy) / centers, Y: first.Center.Y + (along * dy + h * dx) / centers);
                    if (InArc(p, first) && InArc(p, second)) return 0;
                }
            }
        }
        return candidates.Min();
    }
    private static (double X, double Y) At(LineArcCircularArc2D arc, double angle) => (arc.Center.X + arc.Radius * Math.Cos(angle), arc.Center.Y + arc.Radius * Math.Sin(angle));
    private static bool InArc((double X, double Y) p, LineArcCircularArc2D arc)
    {
        var angle = Math.Atan2(p.Y - arc.Center.Y, p.X - arc.Center.X) - arc.StartAngleRadians;
        angle = arc.SweepAngleRadians > 0 ? (angle % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI) : -((-angle % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI));
        return Math.Abs(angle) <= Math.Abs(arc.SweepAngleRadians) + 1e-8;
    }
    private static double PointArc((double X, double Y) p, LineArcCircularArc2D arc)
    {
        if (InArc(p, arc)) return Math.Abs(Hypot(p.X - arc.Center.X, p.Y - arc.Center.Y) - arc.Radius);
        var start = At(arc, arc.StartAngleRadians); var end = At(arc, arc.StartAngleRadians + arc.SweepAngleRadians);
        return Math.Min(Hypot(p.X - start.X, p.Y - start.Y), Hypot(p.X - end.X, p.Y - end.Y));
    }
    private static double Hypot(double x, double y) => Math.Sqrt(x*x + y*y);
    private static double PointLine((double X, double Y) p, LineArcLineSegment2D l)
    {
        var dx = l.End.X-l.Start.X; var dy = l.End.Y-l.Start.Y;
        var t = Math.Clamp(((p.X-l.Start.X)*dx+(p.Y-l.Start.Y)*dy)/(dx*dx+dy*dy),0,1);
        return Hypot(p.X-l.Start.X-t*dx,p.Y-l.Start.Y-t*dy);
    }
    private static double Distance(LineArcProfileCurve2D curve, LineArcLineSegment2D line)
    {
        if (curve is LineArcLineSegment2D chord)
        {
            var ax=chord.End.X-chord.Start.X; var ay=chord.End.Y-chord.Start.Y;
            var bx=line.End.X-line.Start.X; var by=line.End.Y-line.Start.Y; var cross=ax*by-ay*bx;
            if (Math.Abs(cross)>1e-12) {
                var deltaX=line.Start.X-chord.Start.X; var deltaY=line.Start.Y-chord.Start.Y;
                var t=(deltaX*by-deltaY*bx)/cross; var u=(deltaX*ay-deltaY*ax)/cross;
                if (t>=0 && t<=1 && u>=0 && u<=1) return 0;
            }
            return new[] { PointLine(chord.Start,line),PointLine(chord.End,line),PointLine(line.Start,chord),PointLine(line.End,chord) }.Min();
        }
        var arc=(LineArcCircularArc2D)curve;
        (double X,double Y) At(double a)=>(arc.Center.X+arc.Radius*Math.Cos(a),arc.Center.Y+arc.Radius*Math.Sin(a));
        bool InArc((double X,double Y) p) {
            var a=Math.Atan2(p.Y-arc.Center.Y,p.X-arc.Center.X)-arc.StartAngleRadians;
            a=arc.SweepAngleRadians>0 ? (a%(2*Math.PI)+2*Math.PI)%(2*Math.PI) : -((-a%(2*Math.PI)+2*Math.PI)%(2*Math.PI));
            return Math.Abs(a)<=Math.Abs(arc.SweepAngleRadians)+1e-8;
        }
        var start=At(arc.StartAngleRadians); var end=At(arc.StartAngleRadians+arc.SweepAngleRadians);
        double PointArc((double X,double Y) p)=>InArc(p)?Math.Abs(Hypot(p.X-arc.Center.X,p.Y-arc.Center.Y)-arc.Radius)
            : Math.Min(Hypot(p.X-start.X,p.Y-start.Y),Hypot(p.X-end.X,p.Y-end.Y));
        var minimum=new[] { PointLine(start,line),PointLine(end,line),PointArc(line.Start),PointArc(line.End) }.Min();
        var dx=line.End.X-line.Start.X; var dy=line.End.Y-line.Start.Y; var length2=dx*dx+dy*dy;
        var fx=line.Start.X-arc.Center.X; var fy=line.Start.Y-arc.Center.Y;
        var b=2*(fx*dx+fy*dy); var c=fx*fx+fy*fy-arc.Radius*arc.Radius; var disc=b*b-4*length2*c;
        if(disc>=0) foreach(var t in new[]{(-b-Math.Sqrt(disc))/(2*length2),(-b+Math.Sqrt(disc))/(2*length2)})
            if(t>=0 && t<=1 && InArc((line.Start.X+t*dx,line.Start.Y+t*dy))) return 0;
        var length=Math.Sqrt(length2);
        foreach(var sign in new[]{-1d,1d}) {
            var p=(X:arc.Center.X+sign*arc.Radius*(-dy)/length,Y:arc.Center.Y+sign*arc.Radius*dx/length);
            var t=((p.X-line.Start.X)*dx+(p.Y-line.Start.Y)*dy)/length2;
            if(t>=0 && t<=1 && InArc(p)) minimum=Math.Min(minimum,PointLine(p,line));
        }
        return minimum;
    }
}
