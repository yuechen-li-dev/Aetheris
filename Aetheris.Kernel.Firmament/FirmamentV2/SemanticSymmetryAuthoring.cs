using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Math;
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
    private static readonly Regex MirrorHeader = new(@"\bMirrored\s+(?<kind>Profile|Feature)\s+(?<destination>[A-Za-z_]\w*)\s+From\s+(?<source>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex UnsupportedMirrorHeader = new(@"\bMirrored\s+(?<kind>[A-Za-z_]\w*(?:\s*<[^>]+>)?)\s+(?<destination>[A-Za-z_]\w*)\s+From\s+(?<source>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex RadialHeader = new(@"\bRadial\s+Pattern\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex FeatureHeader = new(@"\b(?<kind>Hole\s*<\s*(?:Shaft|Counterbore|Countersink)\s*>|Boss|Pocket)\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex AxisHeader = new(@"\bAxis\s+(?<name>[A-Za-z_]\w*)\s*\{\s*Origin\s*:\s*\[\s*(?<x>[-+.\deE]+)mm\s*,\s*(?<y>[-+.\deE]+)mm\s*,\s*(?<z>[-+.\deE]+)mm\s*\]\s*;?\s*Direction\s*:\s*\[\s*(?<dx>[-+.\deE]+)\s*,\s*(?<dy>[-+.\deE]+)\s*,\s*(?<dz>[-+.\deE]+)\s*\]", RegexOptions.CultureInvariant);
    private static readonly Regex PlaneHeader = new(@"\bPlane\s+(?<name>[A-Za-z_]\w*)\s*\{\s*Origin\s*:\s*\[\s*(?<x>[-+.\deE]+)mm\s*,\s*(?<y>[-+.\deE]+)mm\s*,\s*(?<z>[-+.\deE]+)mm\s*\]\s*;?\s*Normal\s*:\s*\[\s*(?<nx>[-+.\deE]+)\s*,\s*(?<ny>[-+.\deE]+)\s*,\s*(?<nz>[-+.\deE]+)\s*\](?:\s*;?\s*Up\s*:\s*\[[^]]+\])?", RegexOptions.CultureInvariant);

    internal sealed record Result(string Source, IReadOnlyList<FirmamentV2MirrorDerivation> Mirrors, IReadOnlyList<FirmamentV2RadialPatternDecl> RadialPatterns);

    public static Result? Expand(string source, List<string> diagnostics)
    {
        if (!Regex.IsMatch(source, @"\b(?:Mirrored\s+|Radial\s+Pattern\s+)", RegexOptions.CultureInvariant))
            return new(source, [], []);

        var mirrors = new List<FirmamentV2MirrorDerivation>();
        var radialPatterns = new List<FirmamentV2RadialPatternDecl>();

        foreach (Match unsupported in UnsupportedMirrorHeader.Matches(source))
            if (!MirrorHeader.Matches(source).Cast<Match>().Any(valid => valid.Index == unsupported.Index))
                diagnostics.Add(Prefix + "type-unsupported:" + unsupported.Groups["kind"].Value.Replace(" ", string.Empty, StringComparison.Ordinal));
        if (HasErrors(diagnostics)) return null;

        // Profiles are resolved from semantic guides and emitted again as semantic guides.
        // Removing all derivation declarations avoids recursive expansion in ResolveNamedProfile.
        var resolutionSource = RemoveDeclarations(source, MirrorHeader).Source;
        var mirrorChanges = new List<Change>();
        var mirrorPlaneRemovals = new Dictionary<int, Change>();
        foreach (Match declaration in MirrorHeader.Matches(source))
        {
            var open = source.IndexOf('{', declaration.Index);
            var close = Matching(source, open, '{', '}');
            if (close < 0) { diagnostics.Add(Prefix + "mirror-malformed:" + declaration.Groups["destination"].Value); continue; }
            var destination = declaration.Groups["destination"].Value;
            var sourceName = declaration.Groups["source"].Value;
            var kind = Regex.Replace(declaration.Groups["kind"].Value, @"\s+", string.Empty);
            if ((kind == "Profile" && Regex.IsMatch(resolutionSource, $@"\bProfile\s+{Regex.Escape(destination)}\b", RegexOptions.CultureInvariant))
                || (kind != "Profile" && FindFeature(resolutionSource, destination) is not null))
            { diagnostics.Add(Prefix + "identity-collision:" + destination); continue; }
            var acrossName = Field(source[(open + 1)..close], "Across");
            string? planeDiagnostic = null;
            SemanticSymmetryTransform.Plane? across = null;
            if (acrossName is null || !TryResolvePlane(resolutionSource, acrossName, out across, out planeDiagnostic))
            { diagnostics.Add(planeDiagnostic ?? Prefix + "plane-unresolved:" + destination); continue; }

            string? lowered;
            IReadOnlyList<FirmamentV2MirrorMemberProvenance> members;
            if (kind == "Profile")
                lowered = MirrorProfile(resolutionSource, destination, sourceName, across!, diagnostics, out members);
            else
                lowered = MirrorFeature(resolutionSource, destination, sourceName, across!, diagnostics, out members);
            if (lowered is null) continue;
            mirrorChanges.Add(new(declaration.Index, close - declaration.Index + 1, lowered));
            var directPlane = PlaneHeader.Matches(source).Cast<Match>().FirstOrDefault(match => match.Groups["name"].Value == acrossName);
            if (directPlane is not null)
            {
                var planeOpen = source.IndexOf('{', directPlane.Index); var planeClose = Matching(source, planeOpen, '{', '}');
                if (planeClose >= 0) mirrorPlaneRemovals[directPlane.Index] = new(directPlane.Index, planeClose - directPlane.Index + 1, string.Empty);
            }
            resolutionSource += Environment.NewLine + lowered;
            mirrors.Add(new(destination, kind, sourceName, acrossName, "CanonicalRightHanded", members,
                new(declaration.Index, close - declaration.Index + 1), [sourceName, $"Mirror({acrossName})", destination]));
        }
        if (HasErrors(diagnostics)) return null;
        source = Apply(source, mirrorChanges.Concat(mirrorPlaneRemovals.Values));

        // Immutable divergence is ordinary source cloning plus explicit field replacement.
        source = ExpandFeatureWith(source, mirrors, diagnostics);
        if (HasErrors(diagnostics)) return null;

        var radialChanges = new List<Change>();
        var radialSourceRemovals = new Dictionary<int, Change>();
        var radialAxisRemovals = new Dictionary<int, Change>();
        foreach (Match declaration in RadialHeader.Matches(source))
        {
            var open = source.IndexOf('{', declaration.Index); var close = Matching(source, open, '{', '}');
            var name = declaration.Groups["name"].Value;
            if (close < 0) { diagnostics.Add(Prefix + "radial-malformed:" + name); continue; }
            var body = source[(open + 1)..close];
            var sourceName = Field(body, "Source"); var axisName = Field(body, "About"); var countText = Field(body, "Count"); var angleText = Field(body, "Angle");
            if (sourceName is null || axisName is null || countText is null || angleText is null)
            { diagnostics.Add(Prefix + "radial-field-missing:" + name); continue; }
            if (!int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) || count < 1 || count > MaxInstances)
            { diagnostics.Add(Prefix + "radial-count-invalid:" + name + ":" + countText); continue; }
            if (!RevolveAuthoringParser.TryAngle(angleText, out var angle, out _))
            { diagnostics.Add(Prefix + "radial-angle-invalid:" + name + ":" + angleText); continue; }
            if (!TryResolveAxis(source, axisName, out var axis))
            { diagnostics.Add(Prefix + "radial-axis-invalid:" + name + ":" + axisName); continue; }
            var feature = FindFeature(source, sourceName);
            if (feature is null) { diagnostics.Add(Prefix + "radial-source-unsupported:" + name + ":" + sourceName); continue; }
            if (!TryPoint2(feature.Body, out var center)) { diagnostics.Add(Prefix + "feature-center-unsupported:" + sourceName); continue; }
            if (Math.Abs(Math.Abs(axis!.Direction.Z) - 1d) > Tolerance)
            { diagnostics.Add(Prefix + "radial-axis-not-profile-normal:" + name); continue; }
            if (Enumerable.Range(0, count).Any(index => FindFeature(source, name + "_Instance" + index.ToString(CultureInfo.InvariantCulture)) is not null))
            { diagnostics.Add(Prefix + "identity-collision:" + name); continue; }

            var denominator = NearlyFull(angle) ? count : Math.Max(1, count - 1);
            var instances = new List<FirmamentV2RadialInstance>(); var emitted = new List<string>();
            for (var index = 0; index < count; index++)
            {
                var theta = count == 1 ? 0d : angle * index / denominator;
                var rotated = SemanticSymmetryTransform.Rotate(new Point3D(center.X, center.Y, 0), axis, theta);
                var semanticId = name + ".Instance" + index.ToString(CultureInfo.InvariantCulture);
                var materialId = name + "_Instance" + index.ToString(CultureInfo.InvariantCulture);
                emitted.Add(CloneFeature(feature, materialId, (rotated.X, rotated.Y), semanticId));
                instances.Add(new(semanticId, index, theta, rotated.X, rotated.Y, [sourceName, $"Rotate({axisName},{theta:R})", semanticId]));
            }
            radialChanges.Add(new(declaration.Index, close - declaration.Index + 1, string.Join(Environment.NewLine, emitted)));
            radialSourceRemovals[feature.Start] = new(feature.Start, feature.Length, string.Empty);
            var axisDeclaration = AxisHeader.Matches(source).Cast<Match>().First(item => item.Groups["name"].Value == axisName);
            var axisOpen = source.IndexOf('{', axisDeclaration.Index); var axisClose = Matching(source, axisOpen, '{', '}');
            if (axisClose >= 0) radialAxisRemovals[axisDeclaration.Index] = new(axisDeclaration.Index, axisClose - axisDeclaration.Index + 1, string.Empty);
            radialPatterns.Add(new(name, sourceName, axisName, count, angle, NearlyFull(angle) ? "FullOpenEndpoint" : "PartialInclusiveEndpoints", instances,
                new(declaration.Index, close - declaration.Index + 1)));
        }
        if (HasErrors(diagnostics)) return null;
        source = Apply(source, radialChanges.Concat(radialSourceRemovals.Values).Concat(radialAxisRemovals.Values));
        return new(source, mirrors, radialPatterns);
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
        if (!TryPoint2(feature.Body, out var center)) { diagnostics.Add(Prefix + "feature-center-unsupported:" + sourceName); return null; }
        var reflected = SemanticSymmetryTransform.Reflect(new Point3D(center.X, center.Y, 0), across);
        if (Math.Abs(reflected.Z) > Tolerance) { diagnostics.Add(Prefix + "feature-plane-mismatch:" + destination); return null; }
        if (Math.Abs(reflected.X - center.X) <= Tolerance && Math.Abs(reflected.Y - center.Y) <= Tolerance)
        { diagnostics.Add(Prefix + "redundant-feature-mirror:" + destination); return null; }
        members = [new(destination + ".Center", sourceName + ".Center", "Mirror", $"offset:{feature.Start}")];
        return CloneFeature(feature, destination, (reflected.X, reflected.Y), destination);
    }

    private static string ExpandFeatureWith(string source, List<FirmamentV2MirrorDerivation> mirrors, List<string> diagnostics)
    {
        var changes = new List<Change>();
        foreach (Match declaration in Regex.Matches(source, @"\bFeature\s+(?<destination>[A-Za-z_]\w*)\s*=\s*(?<source>[A-Za-z_]\w*)\s+with\s*\{", RegexOptions.CultureInvariant))
        {
            var open = source.IndexOf('{', declaration.Index); var close = Matching(source, open, '{', '}');
            if (close < 0) { diagnostics.Add(Prefix + "with-malformed:" + declaration.Groups["destination"].Value); continue; }
            var feature = FindFeature(source, declaration.Groups["source"].Value);
            if (feature is null) { diagnostics.Add(Prefix + "with-source-invalid:" + declaration.Groups["source"].Value); continue; }
            var body = feature.Body;
            foreach (Match field in Regex.Matches(source[(open + 1)..close], @"\b(?<name>[A-Za-z_]\w*)\s*:\s*(?<value>[^;\r\n}]+)", RegexOptions.CultureInvariant))
            {
                var replacement = new Regex($@"\b{Regex.Escape(field.Groups["name"].Value)}\s*:\s*[^;\r\n}}]+", RegexOptions.CultureInvariant);
                if (!replacement.IsMatch(body)) { diagnostics.Add(Prefix + "with-field-unknown:" + declaration.Groups["destination"].Value + ":" + field.Groups["name"].Value); continue; }
                body = replacement.Replace(body, field.Groups["name"].Value + ": " + field.Groups["value"].Value.Trim(), 1);
            }
            var destination = declaration.Groups["destination"].Value;
            var lowered = feature.Kind + " " + destination + " {" + body + "}";
            changes.Add(new(declaration.Index, close - declaration.Index + 1, lowered));
            changes.Add(new(feature.Start, feature.Length, string.Empty));
            var origin = mirrors.FirstOrDefault(item => item.Destination == declaration.Groups["source"].Value);
            if (origin is not null) mirrors.Add(origin with { Destination = destination, Kind = "FeatureWith", Provenance = origin.Provenance.Concat([$"with({destination})"]).ToArray() });
        }
        return Apply(source, changes);
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

    private static bool TryResolvePlane(string source, string reference, out SemanticSymmetryTransform.Plane? plane, out string? diagnostic)
    {
        plane = null; diagnostic = null; var conceptReference = reference;
        var direct = PlaneHeader.Matches(source).Cast<Match>().FirstOrDefault(match => match.Groups["name"].Value == reference);
        if (direct is not null && Numbers(direct, ["x", "y", "z", "nx", "ny", "nz"], out var values)
            && Direction3D.TryCreate(new(values[3], values[4], values[5]), out var directNormal))
        {
            plane = new(new(values[0], values[1], values[2]), directNormal);
            return true;
        }
        var construction = Regex.Matches(source, @"\bConstruction\s+Plane\s+(?<name>[A-Za-z_]\w*)\s*\{\s*Trace\s*:\s*(?<trace>[\w.]+)", RegexOptions.CultureInvariant)
            .Cast<Match>().FirstOrDefault(match => match.Groups["name"].Value == reference);
        if (construction is not null) conceptReference = construction.Groups["trace"].Value;
        if (!ConceptIrResolver.TryResolvePlane(source, conceptReference, out var resolved, out diagnostic) || resolved is null)
        { diagnostic = Prefix + "plane-invalid:" + reference + ":" + (diagnostic ?? "unresolved"); return false; }
        if (!Direction3D.TryCreate(new(resolved.Normal.X, resolved.Normal.Y, resolved.Normal.Z), out var normal))
        { diagnostic = Prefix + "plane-invalid:" + reference + ":normal"; return false; }
        plane = new(new(resolved.Origin.X, resolved.Origin.Y, resolved.Origin.Z), normal); return true;
    }

    private static bool TryResolveAxis(string source, string name, out SemanticSymmetryTransform.Axis? axis)
    {
        axis = null;
        var match = AxisHeader.Matches(source).Cast<Match>().FirstOrDefault(item => item.Groups["name"].Value == name);
        if (match is null || !Numbers(match, ["x", "y", "z", "dx", "dy", "dz"], out var v) || !Direction3D.TryCreate(new(v[3], v[4], v[5]), out var direction)) return false;
        axis = new(new(v[0], v[1], v[2]), direction); return true;
    }

    private static FeatureBlock? FindFeature(string source, string name)
    {
        foreach (Match header in FeatureHeader.Matches(source))
        {
            if (header.Groups["name"].Value != name) continue;
            var open = source.IndexOf('{', header.Index); var close = Matching(source, open, '{', '}');
            if (close >= 0) return new(header.Groups["kind"].Value, name, source[(open + 1)..close], header.Index, close - header.Index + 1);
        }
        return null;
    }

    private static string CloneFeature(FeatureBlock feature, string name, (double X, double Y) center, string semanticId)
    {
        var body = Regex.Replace(feature.Body, @"\bCenter\s*:\s*Point2\(\s*[-+0-9.eE]+mm\s*,\s*[-+0-9.eE]+mm\s*\)", $"Center: Point2({F(center.X)}mm, {F(center.Y)}mm)", RegexOptions.CultureInvariant);
        return feature.Kind + " " + name + " {" + body + Environment.NewLine + "}";
    }

    private static bool TryPoint2(string body, out (double X, double Y) center)
    {
        center = default; var match = Regex.Match(body, @"\bCenter\s*:\s*Point2\(\s*(?<x>[-+0-9.eE]+)mm\s*,\s*(?<y>[-+0-9.eE]+)mm\s*\)", RegexOptions.CultureInvariant);
        return match.Success && double.TryParse(match.Groups["x"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out center.X) && double.TryParse(match.Groups["y"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out center.Y);
    }

    private static bool Numbers(Match match, string[] groups, out double[] values)
    {
        values = new double[groups.Length];
        for (var i = 0; i < groups.Length; i++) if (!double.TryParse(match.Groups[groups[i]].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) || !double.IsFinite(values[i])) return false;
        return true;
    }

    private static string? Field(string body, string name)
    {
        var match = Regex.Match(body, $@"\b{Regex.Escape(name)}\s*:\s*(?<value>[^;\s}}]+)", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    private static (string Source, IReadOnlyList<Change> Changes) RemoveDeclarations(string source, Regex header)
    {
        var changes = new List<Change>();
        foreach (Match match in header.Matches(source)) { var open = source.IndexOf('{', match.Index); var close = Matching(source, open, '{', '}'); if (close >= 0) changes.Add(new(match.Index, close - match.Index + 1, string.Empty)); }
        return (Apply(source, changes), changes);
    }

    private static string Apply(string source, IEnumerable<Change> changes)
    {
        foreach (var change in changes.GroupBy(item => (item.Start, item.Length)).Select(group => group.Last()).OrderByDescending(item => item.Start))
            source = source.Remove(change.Start, change.Length).Insert(change.Start, change.Text);
        return source;
    }

    private static int Matching(string source, int open, char opening, char closing)
    {
        if (open < 0) return -1; var depth = 0;
        for (var i = open; i < source.Length; i++) { if (source[i] == opening) depth++; else if (source[i] == closing && --depth == 0) return i; }
        return -1;
    }

    private static void Endpoints(LineArcProfileCurve2D curve, out (double X, double Y) start, out (double X, double Y) end)
    {
        if (curve is LineArcLineSegment2D line) { start = line.Start; end = line.End; return; }
        var arc = (LineArcCircularArc2D)curve;
        start = (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians));
        end = (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians));
    }

    private static bool HasErrors(IEnumerable<string> diagnostics) => diagnostics.Any(item => item.StartsWith(Prefix, StringComparison.Ordinal));
    private static bool NearlyFull(double angle) => Math.Abs(Math.Abs(angle) - 2d * Math.PI) <= Tolerance;
    private static string Safe(string value) => Regex.Replace(value, @"[^A-Za-z0-9_]", "_");
    private static string F(double value) => (Math.Abs(value) < 1e-12 ? 0d : value).ToString("R", CultureInfo.InvariantCulture);
    private sealed record Change(int Start, int Length, string Text);
    private sealed record FeatureBlock(string Kind, string Name, string Body, int Start, int Length);
}
