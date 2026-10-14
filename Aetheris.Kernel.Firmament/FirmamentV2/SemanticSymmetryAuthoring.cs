using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>
/// Bounded authoring-time Mirror and radial Pattern expansion. Every admitted declaration
/// becomes ordinary Profile/Feature source before AIR and BRep materialization.
/// </summary>
internal static class SemanticSymmetryAuthoring
{
    internal const string Prefix = "firmament-symmetry-";
    private const double Tolerance = 1e-8;
    private const int MaxInstances = 1024;
    private enum MirrorField { Across }
    private enum RadialField { Source, About, Count, Angle }
    internal static IReadOnlyList<string> FieldsFor(DeclarationSyntax d) =>
        d.Type.Name.Text is "Feature" or "Profile" && d.Modifiers.Any(m => m.Name.Text == "Mirrored") ? Enum.GetNames<MirrorField>() :
        d.Type.Name.Text == "Pattern" && d.Modifiers.Any(m => m.Name.Text == "Radial") ? Enum.GetNames<RadialField>() : [];
    private static DeclarationSyntax[] Declarations(string source) => FirmamentSyntaxTraversal.Declarations(FirmamentSyntaxParser.Parse(source).Document).ToArray();
    private static Change Remove(DeclarationSyntax d) => new(d.Span.Start, d.Span.Length, string.Empty);

    internal sealed record Result(string Source, IReadOnlyList<FirmamentV2MirrorDerivation> Mirrors, IReadOnlyList<FirmamentV2RadialPatternDecl> RadialPatterns);

    public static Result? Expand(string source, List<string> diagnostics)
    {
        // Negative fast path only. Positive recognition and admission belong to AST nodes.
        if (!source.Contains("Mirrored", StringComparison.Ordinal) && !source.Contains("Radial", StringComparison.Ordinal)) return new(source, [], []);
        var declarations = Declarations(source);
        var mirrorDeclarations = declarations.Where(d => d.Modifiers.Any(m => m.Name.Text == "Mirrored")).ToArray();
        if (mirrorDeclarations.Length == 0 && !declarations.Any(d => d.Modifiers.Any(m => m.Name.Text == "Radial"))) return new(source, [], []);
        var before = diagnostics.Count;
        var mirrors = new List<FirmamentV2MirrorDerivation>();
        var radialPatterns = new List<FirmamentV2RadialPatternDecl>();
        var resolutionSource = Apply(source, mirrorDeclarations.Select(Remove));
        var mirrorChanges = new List<Change>();
        var planeRemovals = new Dictionary<int, Change>();
        foreach (var declaration in mirrorDeclarations)
        {
            var destination = declaration.Name?.Text;
            var kind = declaration.Type.Name.Text;
            if (kind is not ("Profile" or "Feature") || declaration.Type.Arguments.Count > 0)
            { diagnostics.Add(Prefix + "type-unsupported:" + kind); continue; }
            var from = declaration.Clauses.Where(c => c.Kind == ClauseKind.From).ToArray();
            var sourceName = from.Length == 1 ? FirmamentSyntaxValues.Reference(from[0].Value) : null;
            var fields = new FirmamentSyntaxFields(declaration with { Clauses = declaration.Clauses.Where(c => c.Kind != ClauseKind.From).ToArray() }, diagnostics);
            var acrossName = fields.Word(nameof(MirrorField.Across)); fields.Complete();
            if (destination is null || sourceName is null || declaration.Body is null)
            { diagnostics.Add(Prefix + "mirror-malformed:" + destination); continue; }
            if (Declarations(resolutionSource).Any(d => d.Name?.Text == destination && d.Type.Name.Text == (kind == "Profile" ? "Profile" : "Hole"))
                || kind == "Feature" && FindFeature(resolutionSource, destination) is not null)
            { diagnostics.Add(Prefix + "identity-collision:" + destination); continue; }
            string? planeDiagnostic = null;
            SemanticSymmetryTransform.Plane? across = null;
            if (acrossName is null || !TryResolvePlane(resolutionSource, acrossName, diagnostics, out across, out planeDiagnostic))
            { diagnostics.Add(planeDiagnostic ?? Prefix + "plane-unresolved:" + destination); continue; }
            IReadOnlyList<FirmamentV2MirrorMemberProvenance> members;
            var lowered = kind == "Profile"
                ? MirrorProfile(resolutionSource, destination, sourceName, across!, diagnostics, out members)
                : MirrorFeature(resolutionSource, destination, sourceName, across!, diagnostics, out members);
            if (lowered is null) continue;
            mirrorChanges.Add(new(declaration.Span.Start, declaration.Span.Length, lowered));
            var directPlane = Declarations(source).FirstOrDefault(d => d.Type.Name.Text == "Plane" && d.Name?.Text == acrossName && d.Modifiers.Count == 0);
            if (directPlane is not null) planeRemovals[directPlane.Span.Start] = Remove(directPlane);
            resolutionSource += Environment.NewLine + lowered;
            mirrors.Add(new(destination, kind, sourceName, acrossName, "CanonicalRightHanded", members,
                declaration.Span, [sourceName, $"Mirror({acrossName})", destination]));
        }
        if (diagnostics.Count != before) return null;
        source = Apply(source, mirrorChanges.Concat(planeRemovals.Values));
        source = ExpandFeatureWith(source, mirrors, diagnostics);
        if (diagnostics.Count != before) return null;

        var radialChanges = new List<Change>();
        var sourceRemovals = new Dictionary<int, Change>();
        var axisRemovals = new Dictionary<int, Change>();
        var radialDeclarations = Declarations(source);
        foreach (var declaration in radialDeclarations.Where(d => d.Modifiers.Any(m => m.Name.Text == "Radial")))
        {
            var name = declaration.Name?.Text;
            if (declaration.Type.Name.Text != "Pattern" || name is null)
            { diagnostics.Add(Prefix + "type-unsupported:" + declaration.Type.Name.Text); continue; }
            var fields = new FirmamentSyntaxFields(declaration, diagnostics);
            var sourceName = fields.Word(nameof(RadialField.Source)); var axisName = fields.Word(nameof(RadialField.About));
            var countValue = fields.Number(nameof(RadialField.Count), ""); var angleValue = fields.Take(nameof(RadialField.Angle));
            fields.Complete();
            if (sourceName is null || axisName is null || angleValue is null || !double.IsFinite(countValue))
            { diagnostics.Add(Prefix + "radial-field-missing:" + name); continue; }
            if (countValue != Math.Truncate(countValue) || countValue < 1 || countValue > MaxInstances)
            { diagnostics.Add(Prefix + "radial-count-invalid:" + name + ":" + countValue); continue; }
            var count = (int)countValue;
            if (!RevolveAuthoringParser.TryAngle(source.Substring(angleValue.Span.Start, angleValue.Span.Length), out var angle, out _))
            { diagnostics.Add(Prefix + "radial-angle-invalid:" + name); continue; }
            if (!TryResolveAxis(source, axisName, diagnostics, out var axis))
            { diagnostics.Add(Prefix + "radial-axis-invalid:" + name + ":" + axisName); continue; }
            var feature = FindFeature(source, sourceName, radialDeclarations);
            if (feature is null) { diagnostics.Add(Prefix + "radial-source-unsupported:" + name + ":" + sourceName); continue; }
            if (!TryPoint2(feature, out var center)) { diagnostics.Add(Prefix + "feature-center-unsupported:" + sourceName); continue; }
            if (Math.Abs(Math.Abs(axis!.Direction.Z) - 1d) > Tolerance)
            { diagnostics.Add(Prefix + "radial-axis-not-profile-normal:" + name); continue; }
            if (Enumerable.Range(0, count).Any(index => radialDeclarations.Any(d => d.Name?.Text == name + "_Instance" + index.ToString(CultureInfo.InvariantCulture))))
            { diagnostics.Add(Prefix + "identity-collision:" + name); continue; }
            var denominator = NearlyFull(angle) ? count : Math.Max(1, count - 1);
            var instances = new List<FirmamentV2RadialInstance>(); var emitted = new List<string>();
            for (var index = 0; index < count; index++)
            {
                var theta = count == 1 ? 0d : angle * index / denominator;
                var rotated = SemanticSymmetryTransform.Rotate(new Point3D(center.X, center.Y, 0), axis, theta);
                var semanticId = name + ".Instance" + index.ToString(CultureInfo.InvariantCulture);
                var materialId = name + "_Instance" + index.ToString(CultureInfo.InvariantCulture);
                emitted.Add(CloneFeature(feature, materialId, (rotated.X, rotated.Y)));
                instances.Add(new(semanticId, index, theta, rotated.X, rotated.Y, [sourceName, $"Rotate({axisName},{theta:R})", semanticId]));
            }
            radialChanges.Add(new(declaration.Span.Start, declaration.Span.Length, string.Join(Environment.NewLine, emitted)));
            sourceRemovals[feature.Start] = new(feature.Start, feature.Length, string.Empty);
            var axisDeclaration = radialDeclarations.Single(d => d.Type.Name.Text == "Axis" && d.Name?.Text == axisName);
            axisRemovals[axisDeclaration.Span.Start] = Remove(axisDeclaration);
            radialPatterns.Add(new(name, sourceName, axisName, count, angle, NearlyFull(angle) ? "FullOpenEndpoint" : "PartialInclusiveEndpoints", instances, declaration.Span));
        }
        if (diagnostics.Count != before) return null;
        return new(Apply(source, radialChanges.Concat(sourceRemovals.Values).Concat(axisRemovals.Values)), mirrors, radialPatterns);
    }

    private static string? MirrorProfile(string source, string destination, string sourceName, SemanticSymmetryTransform.Plane across,
        List<string> diagnostics, out IReadOnlyList<FirmamentV2MirrorMemberProvenance> memberEvidence)
    {
        memberEvidence = [];
        var profile = ProfileAuthoringParser.ResolveNamedProfile(source, sourceName, out var profileDiagnostics);
        if (profile is null)
        { diagnostics.AddRange(profileDiagnostics); diagnostics.Add(Prefix + "profile-source-invalid:" + sourceName); return null; }
        var frame = profile.EffectiveConstructionPlane;
        if (Math.Abs(across.Normal.ToVector().Dot(frame.AxisZ.ToVector())) > Tolerance)
        { diagnostics.Add(Prefix + "plane-not-profile-reflection-line:" + destination); return null; }
        var members = new List<FirmamentV2MirrorMemberProvenance>();
        var declarations = new StringBuilder(); var loopTexts = new List<string>();
        foreach (var loop in profile.Loops)
        {
            var transformed = loop.Segments.Select(segment => (segment, geometry: ReflectCurve(segment.Geometry, frame, across, diagnostics, destination))).ToArray();
            if (transformed.Any(item => item.geometry is null)) return null;
            // Reflection reverses winding. Reverse order and traversal to restore the source's
            // outer/inner canonical orientation while retaining each authored segment name.
            var canonical = transformed.Reverse().Select(item => (item.segment, geometry: Reverse(item.geometry!))).ToArray();
            var segmentTexts = new List<string>();
            foreach (var item in canonical)
            {
                var stem = destination + "_" + Safe(loop.Name) + "_" + Safe(item.segment.Name);
                if (item.geometry is LineArcFullCircle2D circle)
                {
                    var centerName = stem + "_Center"; var guideName = stem + "_Guide";
                    declarations.AppendLine($"Point2 {centerName} {{ Position: [{F(circle.Center.X)}mm, {F(circle.Center.Y)}mm] }}");
                    declarations.AppendLine($"Circle2 {guideName} {{ Center: {centerName}; Radius: {F(circle.Radius)}mm }}");
                    segmentTexts.Add(guideName + " |> TraceLoop");
                }
                else if (item.geometry is LineArcFullEllipse2D ellipse)
                {
                    var centerName = stem + "_Center"; var guideName = stem + "_Guide";
                    declarations.AppendLine($"Point2 {centerName} {{ Position: [{F(ellipse.Center.X)}mm, {F(ellipse.Center.Y)}mm] }}");
                    declarations.AppendLine($"Ellipse2Guide {guideName} {{ Center: {centerName}; SemiAxes: [{F(ellipse.MajorRadius)}mm, {F(ellipse.MinorRadius)}mm]; Rotation: {F(ellipse.RotationRadians * 180d / Math.PI)}deg }}");
                    segmentTexts.Add(guideName + " |> TraceLoop");
                }
                else
                {
                    Endpoints(item.geometry, out var start, out var end);
                    var from = stem + "_From"; var to = stem + "_To"; var guide = stem + "_Guide";
                    declarations.AppendLine($"Point2 {from} {{ Position: [{F(start.X)}mm, {F(start.Y)}mm] }}");
                    declarations.AppendLine($"Point2 {to} {{ Position: [{F(end.X)}mm, {F(end.Y)}mm] }}");
                    if (item.geometry is LineArcLineSegment2D)
                        declarations.AppendLine($"Line2 {guide} {{ From: {from}; To: {to} }}");
                    else if (item.geometry is LineArcCircularArc2D arc)
                    {
                        var centerName = stem + "_Center";
                        declarations.AppendLine($"Point2 {centerName} {{ Position: [{F(arc.Center.X)}mm, {F(arc.Center.Y)}mm] }}");
                        declarations.AppendLine($"Circle2 {guide} {{ Center: {centerName}; Radius: {F(arc.Radius)}mm }}");
                    }
                    var sweep = item.geometry is LineArcCircularArc2D a ? (a.SweepAngleRadians > 0 ? "; Sweep: CounterClockwise" : "; Sweep: Clockwise") : string.Empty;
                    segmentTexts.Add($"Segment {Safe(item.segment.Name)} {{ Trace: {guide}; From: {from}; To: {to}{sweep} }}");
                }
                members.Add(new(destination + "." + item.segment.Name, sourceName + "." + item.segment.Name, "Mirror", item.segment.Provenance.SourceSpan));
            }
            loopTexts.Add($"Loop {Safe(loop.Name)} {{ {string.Join(Environment.NewLine, segmentTexts)} }}");
        }
        memberEvidence = members;
        return declarations + $"Profile {destination}" + (profile.PlaneFrame == "XY" ? string.Empty : " Using " + profile.PlaneFrame) + " {\n" + string.Join(Environment.NewLine, loopTexts) + "\n}";
    }

    private static string? MirrorFeature(string source, string destination, string sourceName, SemanticSymmetryTransform.Plane across,
        List<string> diagnostics, out IReadOnlyList<FirmamentV2MirrorMemberProvenance> members)
    {
        members = [];
        var feature = FindFeature(source, sourceName);
        if (feature is null) { diagnostics.Add(Prefix + "feature-source-invalid:" + sourceName); return null; }
        if (!TryPoint2(feature, out var center)) { diagnostics.Add(Prefix + "feature-center-unsupported:" + sourceName); return null; }
        var reflected = SemanticSymmetryTransform.Reflect(new Point3D(center.X, center.Y, 0), across);
        if (Math.Abs(reflected.Z) > Tolerance) { diagnostics.Add(Prefix + "feature-plane-mismatch:" + destination); return null; }
        if (Math.Abs(reflected.X - center.X) <= Tolerance && Math.Abs(reflected.Y - center.Y) <= Tolerance)
        { diagnostics.Add(Prefix + "redundant-feature-mirror:" + destination); return null; }
        members = [new(destination + ".Center", sourceName + ".Center", "Mirror", $"offset:{feature.Start}")];
        return CloneFeature(feature, destination, (reflected.X, reflected.Y));
    }

    private static string ExpandFeatureWith(string source, List<FirmamentV2MirrorDerivation> mirrors, List<string> diagnostics)
    {
        // Rebind after each derivation so a later with may refer to its predecessor.
        while (Declarations(source).FirstOrDefault(d => d.Type.Name.Text == "Feature" && d.Initializer is WithSyntax) is { } declaration)
        {
            var derived = (WithSyntax)declaration.Initializer!;
            var sourceName = FirmamentSyntaxValues.Reference(derived.Target);
            var feature = sourceName is null ? null : FindFeature(source, sourceName);
            if (feature is null || declaration.Name is null)
            { diagnostics.Add(Prefix + "with-source-invalid:" + sourceName); return source; }
            if (declaration.Span.Start < feature.Start + feature.Length && feature.Start < declaration.Span.Start + declaration.Span.Length)
            { diagnostics.Add(Prefix + "with-source-cycle:" + sourceName); return source; }
            var replacements = new List<Change>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var statement in derived.Body.Statements)
            {
                if (statement is not FieldSyntax field)
                { diagnostics.Add(Prefix + "with-statement-unsupported:" + declaration.Name.Text); continue; }
                var candidates = feature.Declaration.Body!.Statements.OfType<FieldSyntax>()
                    .Where(f => FirmamentSyntaxFields.SameField(field.Name.Text, f.Name.Text) || FirmamentSyntaxFields.SameField(f.Name.Text, field.Name.Text)).ToArray();
                if (candidates.Length != 1)
                { diagnostics.Add(Prefix + "with-field-unknown:" + declaration.Name.Text + ":" + field.Name.Text); continue; }
                var target = candidates[0];
                if (!seen.Add(target.Name.Text)) { diagnostics.Add(Prefix + "with-field-duplicate:" + field.Name.Text); continue; }
                replacements.Add(new(target.Value.Span.Start - feature.BodyStart, target.Value.Span.Length,
                    source.Substring(field.Value.Span.Start, field.Value.Span.Length)));
            }
            if (replacements.Count == 0 || declaration.Type.Arguments.Count > 0 || declaration.Parameters.Count > 0 || declaration.Clauses.Count > 0)
            { diagnostics.Add(Prefix + "with-malformed:" + declaration.Name.Text); return source; }
            var body = Apply(feature.Body, replacements);
            var destination = declaration.Name.Text;
            var lowered = feature.Kind + " " + destination + " {" + body + "}";
            source = Apply(source, [new(declaration.Span.Start, declaration.Span.Length, lowered), new(feature.Start, feature.Length, string.Empty)]);
            var origin = mirrors.FirstOrDefault(item => item.Destination == sourceName);
            if (origin is not null) mirrors.Add(origin with { Destination = destination, Kind = "FeatureWith", Provenance = origin.Provenance.Concat([$"with({destination})"]).ToArray() });
        }
        return source;
    }

    private static LineArcProfileCurve2D? ReflectCurve(LineArcProfileCurve2D curve, ConstructionPlane frame, SemanticSymmetryTransform.Plane across, List<string> diagnostics, string destination)
    {
        (double X, double Y) P((double X, double Y) point)
        {
            var local = frame.ToLocal(SemanticSymmetryTransform.Reflect(frame.ToWorld(point), across));
            if (Math.Abs(local.Z) > Tolerance) diagnostics.Add(Prefix + "profile-plane-mismatch:" + destination);
            return (local.X, local.Y);
        }
        return curve switch
        {
            LineArcLineSegment2D line => new LineArcLineSegment2D(P(line.Start), P(line.End)),
            LineArcCircularArc2D arc => ReflectArc(arc, P),
            LineArcFullCircle2D circle => new LineArcFullCircle2D(P(circle.Center), circle.Radius),
            LineArcFullEllipse2D ellipse => ReflectEllipse(ellipse, P),
            _ => Unsupported()
        };
        LineArcProfileCurve2D? Unsupported() { diagnostics.Add(Prefix + "profile-curve-unsupported:" + destination + ":" + curve.GetType().Name); return null; }
    }

    private static LineArcCircularArc2D ReflectArc(LineArcCircularArc2D arc, Func<(double X, double Y), (double X, double Y)> reflect)
    {
        var center = reflect(arc.Center); var start = reflect((arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians)));
        return new(center, arc.Radius, Math.Atan2(start.Y - center.Y, start.X - center.X), -arc.SweepAngleRadians);
    }

    private static LineArcFullEllipse2D ReflectEllipse(LineArcFullEllipse2D ellipse, Func<(double X, double Y), (double X, double Y)> reflect)
    {
        var center = reflect(ellipse.Center);
        var major = reflect((ellipse.Center.X + Math.Cos(ellipse.RotationRadians), ellipse.Center.Y + Math.Sin(ellipse.RotationRadians)));
        return new(center, ellipse.MajorRadius, ellipse.MinorRadius, Math.Atan2(major.Y - center.Y, major.X - center.X));
    }

    private static LineArcProfileCurve2D Reverse(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => new LineArcLineSegment2D(line.End, line.Start),
        LineArcCircularArc2D arc => new LineArcCircularArc2D(arc.Center, arc.Radius, arc.StartAngleRadians + arc.SweepAngleRadians, -arc.SweepAngleRadians),
        _ => curve
    };

    private static bool TryResolvePlane(string source, string reference, List<string> diagnostics, out SemanticSymmetryTransform.Plane? plane, out string? diagnostic)
    {
        plane = null; diagnostic = null;
        var candidates = Declarations(source).Where(d => d.Type.Name.Text == "Plane" && d.Name?.Text == reference).ToArray();
        var conceptReference = reference;
        if (candidates.Length > 1) { diagnostic = Prefix + "plane-ambiguous:" + reference; return false; }
        if (candidates.SingleOrDefault() is { } declaration)
        {
            var fields = new FirmamentSyntaxFields(declaration, diagnostics);
            if (declaration.Modifiers.Any(m => m.Name.Text == "Construction"))
            {
                conceptReference = fields.Word("Trace") ?? reference; fields.Complete();
            }
            else
            {
                var valid = fields.Vector("Origin", "mm", out var origin) && origin.Length == 3;
                valid &= fields.Vector("Normal", "", out var normalValues) && normalValues.Length == 3;
                var up = fields.Take("Up");
                if (up is not null) valid &= FirmamentSyntaxValues.Vector(up, "", out var u) && u.Length == 3;
                fields.Complete();
                if (!valid || !Direction3D.TryCreate(new(normalValues[0], normalValues[1], normalValues[2]), out var normal))
                { diagnostic = Prefix + "plane-invalid:" + reference; return false; }
                plane = new(new(origin[0], origin[1], origin[2]), normal); return true;
            }
        }
        if (!ConceptIrResolver.TryResolvePlane(source, conceptReference, out var resolved, out diagnostic) || resolved is null)
        { diagnostic = Prefix + "plane-invalid:" + reference + ":" + (diagnostic ?? "unresolved"); return false; }
        if (!Direction3D.TryCreate(new(resolved.Normal.X, resolved.Normal.Y, resolved.Normal.Z), out var resolvedNormal))
        { diagnostic = Prefix + "plane-invalid:" + reference + ":normal"; return false; }
        plane = new(new(resolved.Origin.X, resolved.Origin.Y, resolved.Origin.Z), resolvedNormal); return true;
    }

    private static bool TryResolveAxis(string source, string name, List<string> diagnostics, out SemanticSymmetryTransform.Axis? axis)
    {
        axis = null;
        var candidates = Declarations(source).Where(d => d.Type.Name.Text == "Axis" && d.Name?.Text == name).ToArray();
        if (candidates.Length != 1) return false;
        var fields = new FirmamentSyntaxFields(candidates[0], diagnostics);
        var valid = fields.Vector("Origin", "mm", out var origin) && origin.Length == 3;
        valid &= fields.Vector("Direction", "", out var vector) && vector.Length == 3;
        fields.Complete();
        if (!valid || !Direction3D.TryCreate(new(vector[0], vector[1], vector[2]), out var direction)) return false;
        axis = new(new(origin[0], origin[1], origin[2]), direction); return true;
    }

    private static FeatureBlock? FindFeature(string source, string name, DeclarationSyntax[]? declarations = null)
    {
        var candidates = (declarations ?? Declarations(source)).Where(d => d.Name?.Text == name && d.Modifiers.Count == 0
            && d.Type.Name.Text is "Hole" or "Boss" or "Pocket" && d.Body is not null).ToArray();
        if (candidates.Length != 1) return null;
        var declaration = candidates[0]; var body = declaration.Body!.ContentSpan;
        return new(declaration, source.Substring(declaration.Type.Span.Start, declaration.Type.Span.Length), name,
            source.Substring(body.Start, body.Length), body.Start, declaration.Span.Start, declaration.Span.Length);
    }

    private static string CloneFeature(FeatureBlock feature, string name, (double X, double Y) center)
    {
        var field = feature.Declaration.Body!.Statements.OfType<FieldSyntax>().Single(f => FirmamentSyntaxFields.SameField(f.Name.Text, "Center"));
        var body = Apply(feature.Body, [new(field.Span.Start - feature.BodyStart, field.Span.Length,
            $"Center: Point2({F(center.X)}mm, {F(center.Y)}mm)")]);
        return feature.Kind + " " + name + " {" + body + Environment.NewLine + "}";
    }

    private static bool TryPoint2(FeatureBlock feature, out (double X, double Y) center)
    {
        center = default;
        var fields = feature.Declaration.Body!.Statements.OfType<FieldSyntax>().Where(f => FirmamentSyntaxFields.SameField(f.Name.Text, "Center")).ToArray();
        if (fields.Length != 1 || fields[0].Value is not CallSyntax { Target: ReferenceSyntax { Name.Text: "Point2" } }
            || !FirmamentSyntaxValues.Vector(fields[0].Value, "mm", out var values) || values.Length != 2) return false;
        center = (values[0], values[1]); return true;
    }

    private static string Apply(string source, IEnumerable<Change> changes)
    {
        foreach (var change in changes.GroupBy(item => (item.Start, item.Length)).Select(group => group.Last()).OrderByDescending(item => item.Start))
            source = source.Remove(change.Start, change.Length).Insert(change.Start, change.Text);
        return source;
    }

    private static void Endpoints(LineArcProfileCurve2D curve, out (double X, double Y) start, out (double X, double Y) end)
    {
        if (curve is LineArcLineSegment2D line) { start = line.Start; end = line.End; return; }
        var arc = (LineArcCircularArc2D)curve;
        start = (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians));
        end = (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians));
    }

    private static bool NearlyFull(double angle) => Math.Abs(Math.Abs(angle) - 2d * Math.PI) <= Tolerance;
    private static string Safe(string value) => Regex.Replace(value, @"[^A-Za-z0-9_]", "_");
    private static string F(double value) => (Math.Abs(value) < 1e-12 ? 0d : value).ToString("R", CultureInfo.InvariantCulture);
    private sealed record Change(int Start, int Length, string Text);
    private sealed record FeatureBlock(DeclarationSyntax Declaration, string Kind, string Name, string Body, int BodyStart, int Start, int Length);
}
