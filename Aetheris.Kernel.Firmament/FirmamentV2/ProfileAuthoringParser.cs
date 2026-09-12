using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>
/// Binds the small, static Profile authoring language into the single resolved-profile
/// representation used by extrusion.  Construction paths are deliberately only another
/// producer of named points and guides; no path-specific materialization exists.
/// </summary>
public static class ProfileAuthoringParser
{
    public const string SegmentEndpointMustReferenceNamedPoint = "ProfileSegmentEndpointMustReferenceNamedPoint";
    private const double Tolerance = 1e-9;
    private static readonly Regex Point = new(@"\bPoint2\s+(?<n>[A-Za-z_]\w*)\s*\{\s*Position\s*:\s*(?:\[|Point2\s*\()\s*(?<x>[-+.\deE]+)mm\s*,\s*(?<y>[-+.\deE]+)mm\s*(?:\]|\))", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex Line = new(@"\bLine2\s+(?<n>[A-Za-z_]\w*)\s*\{\s*From\s*:\s*(?<a>[\w.]+)\s*;?\s*To\s*:\s*(?<b>[\w.]+)", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex Circle = new(@"\bCircle2\s+(?<n>[A-Za-z_]\w*)\s*\{\s*Center\s*:\s*(?<c>[\w.]+)\s*;?\s*Radius\s*:\s*(?<r>[-+.\deE]+)mm", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex Rect = new(@"\bRect2\s+(?<n>[A-Za-z_]\w*)\s*\{\s*Center\s*:\s*(?:\[|Point2\s*\()\s*(?<x>[-+.\deE]+)mm\s*,\s*(?<y>[-+.\deE]+)mm\s*(?:\]|\))\s*;?\s*Size\s*:\s*\[(?<w>[-+.\deE]+)mm\s*,\s*(?<h>[-+.\deE]+)mm\]", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex ConstructionPlaneDeclaration = new(@"\bConstruction\s+Plane\s+(?<name>\w+)\s*\{\s*Trace\s*:\s*(?<trace>[\w.]+)\s*;?\s*\}", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex Segment = new(@"\bSegment\s+(?<n>\w+)\s*\{\s*Trace\s*:\s*(?<trace>[\w.]+)\s*;?\s*From\s*:\s*(?<from>[\w.]+)\s*;?\s*To\s*:\s*(?<to>[\w.]+)(?:\s*;?\s*Sweep\s*:\s*(?<sweep>Clockwise|CounterClockwise))?", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex SpanHeader = new(@"\bSpan\s*<\s*(?<type>[A-Za-z_]\w*)\s*>\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex Pipeline = new(@"(?<expression>(?:Reverse\s+)?[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*(?:\s+As\s+[A-Za-z_]\w*)?\s*(?:\|>\s*(?:(?:Reverse\s+)?[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*(?:\s+As\s+[A-Za-z_]\w*)?|Close|TraceLoop)\s*)+)", RegexOptions.CultureInvariant);
    private static readonly Regex Extrude = new(@"\bExtrude\s+\w+\s*\{\s*Profile\s*:\s*(?<p>\w+)\s*;?\s*From\s*:\s*(?<a>[-+.\deE]+)mm\s*;?\s*To\s*:\s*(?<b>[-+.\deE]+)mm", RegexOptions.Singleline | RegexOptions.CultureInvariant);

    public static bool IsProfileSource(string source) => Regex.IsMatch(source, @"\bProfile\s+[A-Za-z_]\w*", RegexOptions.CultureInvariant);

    /// <summary>Inspects first-class geometric views without materializing independent guides.</summary>
    public static GeometricSpanInspection InspectGeometricSpans(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var diagnostics = new List<string>();
        source = ExpandBuiltInPolygons(source, diagnostics);
        var expansion = FirmamentV2TemplateExpansion.Expand(source, diagnostics);
        if (expansion is not null) source = expansion.Source;
        var staticExpansion = CanonicalStaticAuthoring.Expand(source, diagnostics);
        if (staticExpansion is not null) source = staticExpansion.Source;
        var points = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
        var guides = new Dictionary<string, LineArcProfileCurve2D>(StringComparer.Ordinal);
        AddOrdinaryGuides(source, points, guides, diagnostics, applySpans: false);
        var spans = ApplyGeometricSpans(source, points, guides, diagnostics, addGuides: false);
        return new(spans, diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>Resolves one named Profile without requiring an Extrude consumer.</summary>
    public static ResolvedProfile2D? ResolveNamedProfile(string source, string profileName, out IReadOnlyList<string> reportedDiagnostics)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        var diagnostics = new List<string>();
        source = ExpandBuiltInPolygons(source, diagnostics);
        var expansion = FirmamentV2TemplateExpansion.Expand(source, diagnostics);
        if (expansion is not null) source = expansion.Source;
        var declaration = FindProfiles(source).FirstOrDefault(profile => profile.Name == profileName);
        if (declaration is null) { reportedDiagnostics = [$"profile-source-missing-profile:{profileName}"]; return null; }
        var points = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
        var guides = new Dictionary<string, LineArcProfileCurve2D>(StringComparer.Ordinal);
        AddOrdinaryGuides(source, points, guides, diagnostics, applySpans: false);
        var paths = BindPaths(source, points, guides, diagnostics);
        var plane = ResolveConstructionPlane(source, declaration.Frame, diagnostics);
        var loops = BindProfileLoops(declaration, paths, points, guides, diagnostics);
        var profile = plane is null || loops.Count == 0 ? null : new ResolvedProfile2D(declaration.Name, declaration.Frame ?? "XY", loops, plane);
        if (profile is not null)
        {
            var validation = ResolvedProfile2DValidator.Validate(profile);
            diagnostics.AddRange(validation.Diagnostics);
            if (!validation.IsValid) profile = null;
        }
        reportedDiagnostics = diagnostics.Distinct(StringComparer.Ordinal).ToArray();
        return profile;
    }

    internal static IReadOnlyList<string> ValidatePipelineContexts(string source)
    {
        var allowed = FindBlocks(source, @"\b(?:Profile\s+[A-Za-z_]\w*(?:\s+Using\s+[A-Za-z_]\w*)?|Concept\s+Path\s+[A-Za-z_]\w*)\s*\{").ToArray();
        return Pipeline.Matches(source).Cast<Match>()
            .Where(pipeline => !allowed.Any(block => pipeline.Index >= block.Match.Index && pipeline.Index < block.EndIndex))
            .Select(pipeline => $"firmament-pipeline-context-type:{ParsePipelineStages(pipeline.Groups["expression"].Value)[0].Reference}")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Resolves one domain-neutral Concept Path without converting it into a closed Profile.
    /// Sweep and other ordered-path consumers use this boundary so authored line/arc identity
    /// and provenance survive independently of the profile-extrusion frontend.
    /// </summary>
    public static ResolvedConceptPath2D? ResolveConceptPath(string source, string pathName, out IReadOnlyList<string> reportedDiagnostics)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(pathName);
        var diagnostics = new List<string>();
        source = ExpandBuiltInPolygons(source, diagnostics);
        var points = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
        var guides = new Dictionary<string, LineArcProfileCurve2D>(StringComparer.Ordinal);
        AddOrdinaryGuides(source, points, guides, diagnostics);
        var paths = BindPaths(source, points, guides, diagnostics);
        if (!paths.TryGetValue(pathName, out var path))
            diagnostics.Add($"concept-path-missing:{pathName}");
        reportedDiagnostics = diagnostics;
        return path is null ? null : new ResolvedConceptPath2D(
            path.Name,
            path.Steps.SelectMany(step => step.Curves.Select((curve, ordinal) => new ResolvedConceptPathSegment2D(
                step.Curves.Count == 1 ? step.Name : $"{step.Name}.curve{ordinal:D2}",
                step.Kind,
                curve,
                $"concept-path:{path.Name}.{step.Name}",
                step.SourceReference,
                step.WasReversed,
                step.PipelineIndex,
                step.SourceRange))).ToArray(),
            $"concept-path:{path.Name}");
    }

    public static IReadOnlyList<ConceptPathInspection> InspectConceptPaths(string source)
    {
        var diagnostics = new List<string>();
        source = ExpandBuiltInPolygons(source, diagnostics);
        var expansion = FirmamentV2TemplateExpansion.Expand(source, diagnostics);
        if (expansion is not null) source = expansion.Source;
        var points = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
        var guides = new Dictionary<string, LineArcProfileCurve2D>(StringComparer.Ordinal);
        AddOrdinaryGuides(source, points, guides, diagnostics);
        var profiles = FindProfiles(source).Where(profile => profile.FromPath is not null).ToArray();
        var operations = Regex.Matches(source, @"\b(?:Base|Add|Remove)\s+(?<name>[A-Za-z_]\w*)\s*\{[\s\S]*?\bProfile\s*:\s*(?<profile>[A-Za-z_]\w*)", RegexOptions.CultureInvariant)
            .Cast<Match>().Select(match => (Name: match.Groups["name"].Value, Profile: match.Groups["profile"].Value, Offset: match.Index)).ToArray();
        return BindPaths(source, points, guides, diagnostics).Values.Select(path =>
        {
            var pathProfiles = profiles.Where(profile => profile.FromPath == path.Name).ToArray();
            var profileNames = pathProfiles.Select(profile => profile.Name).ToHashSet(StringComparer.Ordinal);
            var composeOperations = operations.Where(operation => profileNames.Contains(operation.Profile)).ToArray();
            var capabilities = new List<string> { "OrderedPlanarGeometry" };
            if (pathProfiles.Length > 0) capabilities.Add("ProfileSource");
            if (composeOperations.Length > 0) capabilities.Add("ComposeProfileOperand");
            var exposed = new List<ConceptPathExposedMemberInspection>
            {
                new("Start", "Point2", "ProfileEndpoint", $"{path.Name}.Start")
            };
            exposed.AddRange(path.Steps.SelectMany(step => new[]
            {
                new ConceptPathExposedMemberInspection(step.Name, step.Kind, "ProfileGuide", step.GuideName),
                new ConceptPathExposedMemberInspection(step.Name + ".End", "Point2", "ProfileEndpoint", step.EndpointName)
            }));
            var consumers = pathProfiles.Select(profile => new ConceptPathConsumerInspection("Profile", profile.Name, "ProfileSource", profile.SourceSpan))
                .Concat(composeOperations.Select(operation => new ConceptPathConsumerInspection("ComposeOperation", operation.Name, "ComposeProfileOperand", $"offset:{operation.Offset}")))
                .ToArray();
            return new ConceptPathInspection(
                path.Name, path.Start.X, path.Start.Y, path.InitialHeading,
                path.Steps.Select(step => new ConceptPathEntryInspection(step.Name, step.Curves.Count == 1 && step.Curves[0] is LineArcCircularArc2D ? "Arc" : step.Curves.All(curve => curve is LineArcLineSegment2D) ? "LineChain" : "CurveChain", step.Start.X, step.Start.Y, step.End.X, step.End.Y, step.Heading,
                    step.Curves.Count == 1 && step.Curves[0] is LineArcCircularArc2D arc ? arc.Radius : null, step.Curves.Count == 1 && step.Curves[0] is LineArcCircularArc2D arc2 ? arc2.SweepAngleRadians * 180d / Math.PI : null, step.GuideName, step.EndpointName, step.Curves.Count, step.Kind)).ToArray(),
                capabilities, exposed, consumers, $"concept-path:{path.Name}");
        }).ToArray();
    }

    public static (ResolvedProfile2D? Profile, double Height, IReadOnlyList<string> Diagnostics) Parse(string source)
    {
        var diagnostics = new List<string>();
        source = ExpandBuiltInPolygons(source, diagnostics);
        var profile = FindProfiles(source).FirstOrDefault();
        if (profile is null)
            return (null, 0, ["profile-source-missing-profile"]);

        var points = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
        var guides = new Dictionary<string, LineArcProfileCurve2D>(StringComparer.Ordinal);
        AddOrdinaryGuides(source, points, guides, diagnostics);
        var paths = BindPaths(source, points, guides, diagnostics);

        ConstructionPlane? plane = ResolveConstructionPlane(source, profile.Frame, diagnostics);
        var loops = BindProfileLoops(profile, paths, points, guides, diagnostics);
        var (start, end, height) = ResolveExtrude(source, profile.Name, diagnostics);
        if (diagnostics.Count != 0)
            return (null, height, diagnostics);
        return (new ResolvedProfile2D(profile.Name, profile.Frame ?? "XY", loops, plane ?? ConstructionPlane.WorldXY, start, end), height, diagnostics);
    }

    /// <summary>
    /// Resolves every Profile whose source is a Concept Path into the ordinary
    /// resolved-profile representation consumed by extrusion and composition.
    /// Concept Path syntax is intentionally erased at this boundary; segment
    /// provenance retains the authored path and step identities.
    /// </summary>
    internal static IReadOnlyDictionary<string, ResolvedProfile2D> BindPathDerivedProfiles(string source, List<string> diagnostics)
    {
        // This is the canonical semantic-profile adapter used by Compose and SectionChain.
        // Its historical name remains source-compatible; pipeline syntax is erased here too.
        source = ExpandBuiltInPolygons(source, diagnostics);
        var authoredProfiles = FindProfiles(source).Where(candidate => candidate.FromPath is not null || candidate.Body?.Contains("|>", StringComparison.Ordinal) == true).ToArray();
        if (authoredProfiles.Length == 0) return new Dictionary<string, ResolvedProfile2D>();
        var points = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
        var guides = new Dictionary<string, LineArcProfileCurve2D>(StringComparer.Ordinal);
        AddOrdinaryGuides(source, points, guides, diagnostics);
        var paths = BindPaths(source, points, guides, diagnostics);
        var profiles = new Dictionary<string, ResolvedProfile2D>(StringComparer.Ordinal);
        foreach (var profile in authoredProfiles)
        {
            var plane = ResolveConstructionPlane(source, profile.Frame, diagnostics);
            var loops = BindProfileLoops(profile, paths, points, guides, diagnostics);
            if (plane is null || loops.Count == 0) continue;
            var resolved = new ResolvedProfile2D(profile.Name, profile.Frame ?? "XY", loops, plane);
            var validation = ResolvedProfile2DValidator.Validate(resolved);
            diagnostics.AddRange(validation.Diagnostics);
            if (validation.IsValid && !profiles.TryAdd(profile.Name, resolved))
                diagnostics.Add($"profile-duplicate:{profile.Name}");
        }
        return profiles;
    }

    private static string ExpandBuiltInPolygons(string source, List<string> diagnostics) =>
        Polygon2RhombusAuthoring.Expand(source, diagnostics)?.Source ?? source;

    internal static ConstructionPlane? ResolveNamedConstructionPlane(string source, string frame, List<string> diagnostics)
        => ResolveConstructionPlane(source, frame, diagnostics);

    private static void AddOrdinaryGuides(string source, Dictionary<string, (double X, double Y)> points, Dictionary<string, LineArcProfileCurve2D> guides, List<string> diagnostics, bool applySpans = true)
    {
        foreach (Match match in Point.Matches(source))
            if (TryNumber(match.Groups["x"].Value, out var x) && TryNumber(match.Groups["y"].Value, out var y))
                points[match.Groups["n"].Value] = (x, y);
        foreach (Match match in Rect.Matches(source))
        {
            var name = match.Groups["n"].Value;
            if (!TryNumber(match.Groups["w"].Value, out var width) || !TryNumber(match.Groups["h"].Value, out var height) || !TryNumber(match.Groups["x"].Value, out var x) || !TryNumber(match.Groups["y"].Value, out var y) || width <= 0 || height <= 0)
            { diagnostics.Add($"rect2-invalid-size:{name}"); continue; }
            points[$"{name}.BottomLeft"] = (x - width / 2, y - height / 2);
            points[$"{name}.BottomRight"] = (x + width / 2, y - height / 2);
            points[$"{name}.TopRight"] = (x + width / 2, y + height / 2);
            points[$"{name}.TopLeft"] = (x - width / 2, y + height / 2);
        }
        foreach (Match match in Line.Matches(source))
        {
            var name = match.Groups["n"].Value;
            if (!points.TryGetValue(match.Groups["a"].Value, out var from) || !points.TryGetValue(match.Groups["b"].Value, out var to)) diagnostics.Add($"profile-layout-unresolved-line:{name}");
            else guides[name] = new LineArcLineSegment2D(from, to);
        }
        foreach (Match match in Rect.Matches(source))
        {
            var name = match.Groups["n"].Value;
            if (points.TryGetValue($"{name}.BottomLeft", out var bl) && points.TryGetValue($"{name}.BottomRight", out var br) && points.TryGetValue($"{name}.TopRight", out var tr) && points.TryGetValue($"{name}.TopLeft", out var tl))
            {
                guides[$"{name}.Bottom"] = new LineArcLineSegment2D(bl, br); guides[$"{name}.Right"] = new LineArcLineSegment2D(br, tr);
                guides[$"{name}.Top"] = new LineArcLineSegment2D(tr, tl); guides[$"{name}.Left"] = new LineArcLineSegment2D(tl, bl);
            }
        }
        foreach (Match match in Circle.Matches(source))
        {
            if (!points.TryGetValue(match.Groups["c"].Value, out var center) || !TryNumber(match.Groups["r"].Value, out var radius) || radius <= 0) { diagnostics.Add($"profile-layout-unresolved-circle:{match.Groups["n"].Value}"); continue; }
            // A circle remains a guide; segments choose its directed arc below.
            guides[match.Groups["n"].Value] = new LineArcFullCircle2D(center, radius);
        }
        if (applySpans) ApplyGeometricSpans(source, points, guides, diagnostics, addGuides: true);
    }

    private static IReadOnlyList<GeometricSpanView> ApplyGeometricSpans(
        string source,
        IReadOnlyDictionary<string, (double X, double Y)> points,
        IDictionary<string, LineArcProfileCurve2D> guides,
        List<string> diagnostics,
        bool addGuides)
    {
        var result = new List<GeometricSpanView>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match header in SpanHeader.Matches(source))
        {
            var open = source.IndexOf('{', header.Index, header.Length);
            var close = FindMatchingBrace(source, open);
            var name = header.Groups["name"].Value;
            var type = header.Groups["type"].Value;
            if (close < 0) { diagnostics.Add($"firmament-span-domain-invalid:{name}"); continue; }
            if (!names.Add(name) || guides.ContainsKey(name)) { diagnostics.Add($"firmament-span-duplicate:{name}"); continue; }
            var body = source[(open + 1)..close];
            var parent = Property(body, "On");
            if (parent is null) { diagnostics.Add($"firmament-span-parent-not-found:{name}"); continue; }

            // Plane spans are semantic surface views only in X1: a named, already-authored
            // Profile is their boundary authority. They intentionally do not create a face.
            if (type == "Plane")
            {
                var boundary = Property(body, "Boundary");
                var planeDiagnostics = new List<string>();
                var parentPlane = ResolveConstructionPlane(source, parent, planeDiagnostics);
                if (!ConstructionPlaneDeclaration.Matches(source).Cast<Match>().Any(match => match.Groups["name"].Value == parent) || parentPlane is null)
                { diagnostics.Add($"firmament-span-parent-not-found:{name}"); continue; }
                if (boundary is null) { diagnostics.Add($"firmament-span-surface-boundary-open:{name}"); continue; }
                var boundaryProfile = ResolveNamedProfile(source, boundary, out var boundaryDiagnostics);
                if (boundaryProfile is null)
                {
                    diagnostics.Add(boundaryDiagnostics.Any(item => item.Contains("self-intersection", StringComparison.Ordinal))
                        ? $"firmament-span-surface-self-intersection:{name}"
                        : $"firmament-span-surface-boundary-open:{name}");
                    continue;
                }
                // An unqualified XY Profile is interpreted in the parent-local 2D frame by
                // the Span declaration. An explicitly qualified different frame is invalid.
                if (!string.Equals(boundaryProfile.PlaneFrame, "XY", StringComparison.Ordinal)
                    && !string.Equals(boundaryProfile.PlaneFrame, parent, StringComparison.Ordinal)
                    && !IsWorldXyLayout(source, boundaryProfile.PlaneFrame))
                { diagnostics.Add($"firmament-span-surface-boundary-off-parent:{name}"); continue; }
                var area = boundaryProfile.Loops.Sum(loop =>
                    (loop.IsOuter ? 1d : -1d) * Math.Abs(PrismaticSectionStackCompiler.ProfileArea(boundaryProfile with { Loops = [loop] })));
                if (!double.IsFinite(area) || area <= Tolerance) { diagnostics.Add($"firmament-span-surface-domain-invalid:{name}"); continue; }
                var normal = parentPlane.AxisZ.ToVector();
                var orientation = string.Equals(Property(body, "Orientation"), "Reverse", StringComparison.Ordinal) ? "Reverse" : "Forward";
                var consumers = FindSpanConsumers(source, name);
                result.Add(new(name, type, parent, "Plane", $"Boundary:{boundary}", orientation, null, null, boundary, null,
                    $"span:{name};parent:{parent};boundary:{boundary}", Area: area,
                    Normal: orientation == "Reverse" ? [-normal.X, -normal.Y, -normal.Z] : [normal.X, normal.Y, normal.Z],
                    LocalFrame: parentPlane.StableId, ConsumerReferences: consumers, Boundary: boundaryProfile, ParentPlane: parentPlane));
                continue;
            }

            if (type is not ("Line" or "Arc" or "Curve")) { diagnostics.Add($"firmament-span-type-invalid:{type}"); continue; }
            if (!guides.TryGetValue(parent, out var parentGeometry)) { diagnostics.Add($"firmament-span-parent-not-found:{name}"); continue; }
            var parentType = parentGeometry switch { LineArcLineSegment2D => "Line", LineArcFullCircle2D => "Arc", LineArcCircularArc2D => "Arc", _ => parentGeometry.GetType().Name };
            if ((type == "Line" && parentGeometry is not LineArcLineSegment2D) || (type == "Arc" && parentGeometry is not LineArcFullCircle2D and not LineArcCircularArc2D))
            { diagnostics.Add($"firmament-span-type-invalid:{type}"); continue; }
            var fromName = Property(body, "From"); var toName = Property(body, "To");
            if (fromName is null || toName is null || !points.TryGetValue(fromName, out var from) || !points.TryGetValue(toName, out var to))
            { diagnostics.Add($"firmament-span-domain-invalid:{name}"); continue; }
            var sweep = Property(body, "Sweep") ?? "CounterClockwise";
            var curve = SelectGuide(parentGeometry, from, to, sweep, name, parent, diagnostics);
            if (curve is null) continue;
            var length = CurveLength(curve);
            if (!double.IsFinite(length) || length <= Tolerance) { diagnostics.Add($"firmament-span-zero-length:{name}"); continue; }
            var reverse = string.Equals(Property(body, "Orientation"), "Reverse", StringComparison.Ordinal);
            if (reverse) curve = Reverse(curve);
            var domain = curve switch
            {
                LineArcLineSegment2D => $"Endpoints:{fromName}..{toName}",
                LineArcCircularArc2D arc => $"NativeRadians:{arc.StartAngleRadians:R}..{(arc.StartAngleRadians + arc.SweepAngleRadians):R}",
                _ => "Endpoints"
            };
            var view = new GeometricSpanView(name, type, parent, parentType, domain, reverse ? "Reverse" : "Forward", fromName, toName, null, length, $"span:{name};parent:{parent};parentType:{parentType}", curve);
            result.Add(view);
            if (addGuides) guides[name] = curve;
        }
        return result;
    }

    private static bool IsWorldXyLayout(string source, string frame) =>
        Regex.IsMatch(source, $@"\bConcept\s+Struct\s+{Regex.Escape(frame)}\s+On\s+XY\s*\{{", RegexOptions.CultureInvariant);

    private static IReadOnlyList<string> FindSpanConsumers(string source, string spanName)
    {
        var consumers = new List<string>();
        foreach (var kind in new[] { "Hole", "Boss", "Pocket", "Fixed", "Force", "Pressure", "Traction" })
        foreach (Match header in Regex.Matches(source, $@"\b{kind}(?:\s*<[^>]+>)?\s+(?<name>[A-Za-z_]\w*)\s*\{{", RegexOptions.CultureInvariant))
        {
            var open = source.IndexOf('{', header.Index, header.Length);
            var close = FindMatchingBrace(source, open);
            if (close > open && string.Equals(Property(source[(open + 1)..close], "On") ?? Property(source[(open + 1)..close], "region"), spanName, StringComparison.Ordinal))
                consumers.Add($"{kind}:{header.Groups["name"].Value}");
        }
        return consumers.Order(StringComparer.Ordinal).ToArray();
    }

    private static double CurveLength(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => Distance(line.Start, line.End),
        LineArcCircularArc2D arc => Math.Abs(arc.Radius * arc.SweepAngleRadians),
        _ => double.NaN
    };

    private static IReadOnlyDictionary<string, BoundPath> BindPaths(string source, Dictionary<string, (double X, double Y)> points, Dictionary<string, LineArcProfileCurve2D> guides, List<string> diagnostics)
    {
        var result = new Dictionary<string, BoundPath>(StringComparer.Ordinal);
        foreach (var block in FindBlocks(source, @"\bConcept\s+Path\s+(?<name>[A-Za-z_]\w*)\s*\{"))
        {
            var name = block.Match.Groups["name"].Value;
            if (!result.TryAdd(name, default!)) { diagnostics.Add($"concept-path-duplicate:{name}"); continue; }
            if (block.Body.Contains("|>", StringComparison.Ordinal))
            {
                var pipelinePath = BindPathPipeline(name, block.Body, guides, diagnostics);
                if (pipelinePath is not null)
                {
                    result[name] = pipelinePath;
                    points[$"{name}.Start"] = pipelinePath.Start;
                    foreach (var step in pipelinePath.Steps)
                    {
                        if (step.Curves.Count == 1) guides[$"{name}.{step.Name}"] = step.Curves[0];
                        points[$"{name}.{step.Name}.End"] = step.End;
                    }
                }
                continue;
            }
            var startMatch = Regex.Match(block.Body, @"\bStart\s*:\s*Point2\s*\(\s*(?<x>[-+.\deE]+)mm\s*,\s*(?<y>[-+.\deE]+)mm\s*\)", RegexOptions.CultureInvariant);
            if (!startMatch.Success || !TryNumber(startMatch.Groups["x"].Value, out var sx) || !TryNumber(startMatch.Groups["y"].Value, out var sy)) { diagnostics.Add($"concept-path-start-invalid:{name}"); continue; }
            var heading = 0d;
            var headingMatch = Regex.Match(block.Body, @"\bHeading\s*:\s*(?<value>[-+.\deE]+)deg", RegexOptions.CultureInvariant);
            if (headingMatch.Success && (!TryNumber(headingMatch.Groups["value"].Value, out heading) || !double.IsFinite(heading))) { diagnostics.Add($"concept-path-heading-invalid:{name}"); continue; }
            var path = new BoundPath(name, (sx, sy), heading, []);
            if (points.ContainsKey($"{name}.Start") || guides.ContainsKey(name)) { diagnostics.Add($"concept-path-name-collision:{name}"); continue; }
            points[$"{name}.Start"] = path.Start;
            var current = path.Start;
            foreach (var step in FindPathSteps(block.Body))
            {
                if (path.Steps.Any(x => x.Name == step.Name)) { diagnostics.Add($"concept-path-duplicate-step:{name}:{step.Name}"); continue; }
                var guideName = $"{name}.{step.Name}";
                var endpointName = $"{guideName}.End";
                if (guides.ContainsKey(guideName) || points.ContainsKey(endpointName)) { diagnostics.Add($"concept-path-name-collision:{guideName}"); continue; }
                if (!TryBindStep(name, step, current, ref heading, path.Start, points, out var curves, out var endpoint, out var diagnostic)) { diagnostics.Add(diagnostic!); continue; }
                if (curves!.Count == 1) guides[guideName] = curves[0];
                points[endpointName] = endpoint; path.Steps.Add(new BoundPathStep(step.Name, step.Kind == "Line" ? "Span" : step.Kind == "Arc" ? "ArcTransition" : step.Kind, guideName, endpointName, curves, current, endpoint, heading)); current = endpoint;
            }
            result[name] = path;
        }
        BindRectEdgeProfiles(source, result, diagnostics);
        return result;
    }

    private static void BindRectEdgeProfiles(string source, Dictionary<string, BoundPath> result, List<string> diagnostics)
    {
        var programs = FindBlocks(source, @"\bEdgeProfile\s+(?<owner>[A-Za-z_]\w*)\.(?<edge>[A-Za-z_]\w*)\s*\{").ToArray();
        var parsedDeltas=SemanticProfileDeltaParser.Parse(source);
        diagnostics.AddRange(parsedDeltas.Diagnostics);
        var cornerPrograms = FindBlocks(source, @"\bCornerProfile\s+(?<owner>[A-Za-z_]\w*)\.(?<corner>[A-Za-z_]\w*)\s*\{").ToArray();
        foreach (Match rectangle in Rect.Matches(source))
        {
            var name = rectangle.Groups["n"].Value;
            var isProfileSource = Regex.IsMatch(source, $@"\bProfile\s+[A-Za-z_]\w*\s+From\s+{Regex.Escape(name)}\b", RegexOptions.CultureInvariant);
            if (!isProfileSource && !programs.Any(x => x.Match.Groups["owner"].Value == name) && !cornerPrograms.Any(x => x.Match.Groups["owner"].Value == name)
                &&!parsedDeltas.Deltas.Any(delta=>delta.OwnerPath.StartsWith(name+".",StringComparison.Ordinal))) continue;
            if (!TryNumber(rectangle.Groups["w"].Value, out var width) || !TryNumber(rectangle.Groups["h"].Value, out var height) ||
                !TryNumber(rectangle.Groups["x"].Value, out var cx) || !TryNumber(rectangle.Groups["y"].Value, out var cy)) continue;
            if (result.ContainsKey(name)) { diagnostics.Add($"profile-edge-owner-name-collision:{name}"); continue; }
            var bl = (X: cx - width / 2, Y: cy - height / 2); var br = (X: cx + width / 2, Y: cy - height / 2);
            var tr = (X: cx + width / 2, Y: cy + height / 2); var tl = (X: cx - width / 2, Y: cy + height / 2);
            var path = new BoundPath(name, bl, 0, []);
            var edges = new Dictionary<string, ((double X, double Y) Start, (double X, double Y) End)>(StringComparer.Ordinal)
            {
                ["Bottom"] = (bl, br), ["Right"] = (br, tr), ["Top"] = (tr, tl), ["Left"] = (tl, bl)
            };
            var cornerEdges = new Dictionary<string, (string EdgeA, string EdgeB)>(StringComparer.Ordinal)
            {
                ["BottomRight"] = ("Bottom", "Right"), ["TopRight"] = ("Right", "Top"),
                ["TopLeft"] = ("Top", "Left"), ["BottomLeft"] = ("Left", "Bottom")
            };
            var resolvedCorners = new Dictionary<string, ResolvedSemanticCornerProfileIr>(StringComparer.Ordinal);
            foreach (var corner in cornerEdges)
            {
                var matching = cornerPrograms.Where(x => x.Match.Groups["owner"].Value == name && x.Match.Groups["corner"].Value == corner.Key).ToArray();
                if (matching.Length == 0) continue;
                if (matching.Length > 1) { diagnostics.Add($"semantic-corner-duplicate-program:{name}.{corner.Key}"); continue; }
                var operations = FindBlocks(matching[0].Body, @"\b(?<kind>Chamfer|Cutback|Taper|NotchCorner)\s+(?<name>[A-Za-z_]\w*)\s*\{").ToArray();
                if (operations.Length != 1) { diagnostics.Add($"semantic-corner-operation-required:{name}.{corner.Key}"); continue; }
                var operation = operations[0]; var operationName = operation.Match.Groups["name"].Value; var operationKind = operation.Match.Groups["kind"].Value;
                var equal = Property(operation.Body, "Setback"); var aText = Property(operation.Body, "SetbackA") ?? equal; var bText = Property(operation.Body, "SetbackB") ?? equal;
                var operationId = $"{name}.{corner.Key}.{operationName}";
                if (aText is null || bText is null || !TryMeasure(aText, "mm", out var setbackA) || !TryMeasure(bText, "mm", out var setbackB))
                { diagnostics.Add($"semantic-corner-setbacks-required:{operationId}"); continue; }
                var pair = corner.Value; var edgeA = edges[pair.EdgeA]; var edgeB = edges[pair.EdgeB];
                SemanticCornerOperationIr authored = operationKind switch
                {
                    "Chamfer" => new SemanticCornerChamferIr(operationName, operationId, setbackA, setbackB, $"offset:{operation.Match.Index}"),
                    "Cutback" => new SemanticCornerCutbackIr(operationName, operationId, setbackA, setbackB, $"offset:{operation.Match.Index}"),
                    "Taper" => new SemanticCornerTaperIr(operationName, operationId, setbackA, setbackB, $"offset:{operation.Match.Index}"),
                    "NotchCorner" => new SemanticCornerNotchIr(operationName, operationId, setbackA, setbackB, $"offset:{operation.Match.Index}"),
                    _ => throw new InvalidOperationException()
                };
                var cornerPath = $"{name}.{corner.Key}";
                var resolution = SemanticCornerProfileResolver.Resolve(new(cornerPath, cornerPath, $"{name}.{pair.EdgeA}", $"{name}.{pair.EdgeB}",
                    new(edgeA.Start.X, edgeA.Start.Y), new(edgeA.End.X, edgeA.End.Y), new(edgeB.End.X, edgeB.End.Y), authored,
                    $"{cornerPath}[u=away-on-{pair.EdgeA},v=away-on-{pair.EdgeB}]", $"CornerProfile {cornerPath}"));
                if (!resolution.IsSuccess) diagnostics.AddRange(resolution.Diagnostics);
                else resolvedCorners[corner.Key] = resolution.Corner!;
            }
            AddEdge("Bottom", bl, br); AddEdge("Right", br, tr); AddEdge("Top", tr, tl); AddEdge("Left", tl, bl);
            result[name] = path;

            void AddEdge(string edge, (double X, double Y) start, (double X, double Y) end)
            {
                var matching = programs.Where(x => x.Match.Groups["owner"].Value == name && x.Match.Groups["edge"].Value == edge).ToArray();
                var startCorner = cornerEdges.First(x => x.Value.EdgeB == edge).Key;
                var endCorner = cornerEdges.First(x => x.Value.EdgeA == edge).Key;
                resolvedCorners.TryGetValue(startCorner, out var startResolution);
                resolvedCorners.TryGetValue(endCorner, out var endResolution);
                var edgeDeltas=parsedDeltas.Deltas.Where(delta=>delta.OwnerPath.Equals($"{name}.{edge}",StringComparison.Ordinal)).ToArray();
                if (matching.Length == 0 && edgeDeltas.Length==0 && startResolution is null && endResolution is null)
                {
                    path.Steps.Add(new(edge, "Span", $"{name}.{edge}", $"{name}.{edge}.End", [new LineArcLineSegment2D(start, end)], start, end, Heading(start, end)));
                    return;
                }
                if (matching.Length > 1) { diagnostics.Add($"semantic-edge-duplicate-program:{name}.{edge}"); return; }
                var fragments = new List<SemanticEdgeFragmentIr>();
                foreach(var parsed in edgeDeltas)
                {
                    var id=$"{name}.{edge}.{parsed.Delta.Name}";
                    fragments.Add(parsed.Delta with { StableId=id,Side=-parsed.Delta.Side,
                        Levels=parsed.Delta.Levels.Select(level=>level with { StableId=$"{id}.{level.Name}" }).ToArray(),
                        Members=parsed.Delta.Members.Select(member=>member with { StableId=$"{id}.{member.Name}",ExposeAs=member.ExposeAs is null?null:$"{id}.{member.ExposeAs.Split('.').Last()}" }).ToArray() });
                }
                foreach (var step in matching.Length == 0 ? Enumerable.Empty<PathStep>() : FindPathSteps(matching[0].Body).Where(x => x.Kind is "Chamfer" or "Step" or "Notch" or "Cutback" or "Tab"))
                {
                    var id = $"{name}.{edge}.{step.Name}";
                    var anchors = new[] { ("FromStart", SemanticEdgeAnchorKind.FromStart), ("FromEnd", SemanticEdgeAnchorKind.FromEnd), ("CenteredAt", SemanticEdgeAnchorKind.CenteredAt) }
                        .Select(x => (x.Item2, Value: Property(step.Body, x.Item1))).Where(x => x.Value is not null).ToArray();
                    if (anchors.Length != 1 || !TryMeasure(anchors[0].Value!, "mm", out var offset)) { diagnostics.Add($"semantic-edge-anchor-required:{id}"); continue; }
                    var sideText = Property(step.Body, "Side"); var side = sideText?.Equals("Left", StringComparison.OrdinalIgnoreCase) == true ? 1 : sideText?.Equals("Right", StringComparison.OrdinalIgnoreCase) == true ? -1 : 0;
                    bool Measure(string property, out double value) { value = default; var text = Property(step.Body, property); return text is not null && TryMeasure(text, "mm", out value); }
                    var anchor = new SemanticEdgeAnchorIr(anchors[0].Item1, offset); SemanticEdgeFragmentIr? fragment = null;
                    if (step.Kind == "Tab" && Measure("Width", out var tabWidth) && Measure("Extension", out var extension)) fragment = new SemanticEdgeTabIr(step.Name, id, anchor, tabWidth, extension, side, $"offset:{step.Index}");
                    else if (step.Kind == "Notch" && Measure("Width", out var notchWidth) && Measure("Depth", out var depth)) fragment = new SemanticEdgeNotchIr(step.Name, id, anchor, notchWidth, depth, side, $"offset:{step.Index}");
                    else if (step.Kind == "Step" && Measure("Width", out var stepWidth) && Measure("Rise", out var rise)) fragment = new SemanticEdgeStepIr(step.Name, id, anchor, stepWidth, rise, side, $"offset:{step.Index}");
                    else if (step.Kind == "Chamfer" && Measure("Run", out var chamferRun) && Measure("Offset", out var chamferOffset)) fragment = new SemanticEdgeChamferIr(step.Name, id, anchor, chamferRun, chamferOffset, side, $"offset:{step.Index}");
                    else if (step.Kind == "Cutback" && Measure("Run", out var cutbackRun) && Measure("Offset", out var cutbackOffset)) fragment = new SemanticEdgeCutbackIr(step.Name, id, anchor, cutbackRun, cutbackOffset, side, $"offset:{step.Index}");
                    else diagnostics.Add($"semantic-edge-fragment-properties-invalid:{id}:{step.Kind}");
                    if (fragment is not null) fragments.Add(fragment);
                }
                var consumption = new SemanticEdgeEndpointConsumptionIr(
                    startResolution?.EdgeBConsumption ?? 0d, endResolution?.EdgeAConsumption ?? 0d,
                    startResolution?.Source.CornerPath, endResolution?.Source.CornerPath);
                var resolution = SemanticEdgeProfileResolver.Resolve(new($"{name}.{edge}", $"{name}.{edge}", new(start.X, start.Y), new(end.X, end.Y), fragments, $"{name}.{edge}[u,v]", $"EdgeProfile {name}.{edge}"), consumption);
                if (!resolution.IsSuccess) { diagnostics.AddRange(resolution.Diagnostics); return; }
                foreach (var member in resolution.Profile!.OrderedMembers)
                {
                    if(member.Kind=="ProfileDelta")
                    {
                        foreach(var descendant in member.CurveDescendants)
                        {
                            var curveStart=Start(descendant.Geometry);var curveEnd=End(descendant.Geometry);var leaf=descendant.StableId.EndsWith(".curve00",StringComparison.Ordinal)?descendant.StableId[..^8]:descendant.StableId;
                            path.Steps.Add(new($"{edge}.{leaf}","ProfileDeltaMember",leaf,$"{leaf}.End",[descendant.Geometry],curveStart,curveEnd,Heading(curveStart,curveEnd)));
                        }
                        continue;
                    }
                    var first = ((LineArcLineSegment2D)member.CurveDescendants.First().Geometry).Start; var last = ((LineArcLineSegment2D)member.CurveDescendants.Last().Geometry).End;
                    path.Steps.Add(new($"{edge}.{member.Name}", member.Kind, $"{name}.{edge}.{member.Name}", $"{name}.{edge}.{member.Name}.End", member.CurveDescendants.Select(x => x.Geometry).ToArray(), first, last, Heading(first, last)));
                }
                if (endResolution is not null)
                {
                    var member = endResolution.Source.Operation; var first = ((LineArcLineSegment2D)endResolution.CurveDescendants.First().Geometry).Start; var last = ((LineArcLineSegment2D)endResolution.CurveDescendants.Last().Geometry).End;
                    path.Steps.Add(new($"{endCorner}.{member.Name}", member.Kind, member.StableId, $"{member.StableId}.End", endResolution.CurveDescendants.Select(x => x.Geometry).ToArray(), first, last, Heading(first, last)));
                }
            }
        }
        foreach (var program in programs.Where(p => !result.ContainsKey(p.Match.Groups["owner"].Value)))
            diagnostics.Add($"semantic-edge-owner-path-missing:{program.Match.Groups["owner"].Value}.{program.Match.Groups["edge"].Value}");
        foreach (var program in programs.Where(p => result.ContainsKey(p.Match.Groups["owner"].Value) && p.Match.Groups["edge"].Value is not ("Bottom" or "Right" or "Top" or "Left")))
            diagnostics.Add($"semantic-edge-owner-member-missing:{program.Match.Groups["owner"].Value}.{program.Match.Groups["edge"].Value}:available=Bottom,Right,Top,Left");
        foreach (var program in cornerPrograms.Where(p => !result.ContainsKey(p.Match.Groups["owner"].Value)))
            diagnostics.Add($"semantic-corner-owner-path-missing:{program.Match.Groups["owner"].Value}.{program.Match.Groups["corner"].Value}");
        foreach (var program in cornerPrograms.Where(p => result.ContainsKey(p.Match.Groups["owner"].Value) && p.Match.Groups["corner"].Value is not ("BottomRight" or "TopRight" or "TopLeft" or "BottomLeft")))
            diagnostics.Add($"semantic-corner-owner-member-missing:{program.Match.Groups["owner"].Value}.{program.Match.Groups["corner"].Value}:available=BottomRight,TopRight,TopLeft,BottomLeft");
    }

    private static bool TryBindStep(string path, PathStep step, (double X, double Y) current, ref double headingDegrees, (double X, double Y) start, IReadOnlyDictionary<string, (double X, double Y)> points, out IReadOnlyList<LineArcProfileCurve2D>? curves, out (double X, double Y) endpoint, out string? diagnostic)
    {
        curves = null; endpoint = default; diagnostic = null;
        var prefix = $"concept-path-{step.Kind.ToLowerInvariant()}-invalid:{path}:{step.Name}";
        if (step.Kind == "Close")
        {
            endpoint = start;
            if (Distance(current, endpoint) <= Tolerance) { diagnostic = $"concept-path-zero-length:{path}:{step.Name}"; return false; }
            curves = [new LineArcLineSegment2D(current, endpoint)]; headingDegrees = Heading(current, endpoint); return true;
        }
        var body = step.Body;
        var turn = Property(body, "Turn"); var absolute = Property(body, "Heading"); var length = Property(body, "Length"); var to = Property(body, "To");
        if (step.Kind is "Line" or "Span")
        {
            if (turn is not null && absolute is not null) { diagnostic = $"concept-path-turn-and-heading:{path}:{step.Name}"; return false; }
            if (to is not null && (length is not null || turn is not null || absolute is not null)) { diagnostic = $"concept-path-to-mixed-direction-or-length:{path}:{step.Name}"; return false; }
            if (to is not null)
            {
                var targetName = string.Equals(to, "Start", StringComparison.Ordinal) ? $"{path}.Start" : to;
                if (!points.TryGetValue(targetName, out endpoint)) { diagnostic = $"concept-path-unknown-target:{path}:{step.Name}:{to}"; return false; }
                if (Distance(current, endpoint) <= Tolerance) { diagnostic = $"concept-path-zero-length:{path}:{step.Name}"; return false; }
                curves = [new LineArcLineSegment2D(current, endpoint)]; headingDegrees = Heading(current, endpoint); return true;
            }
            if (length is null || !TryMeasure(length, "mm", out var distance) || distance <= 0) { diagnostic = $"concept-path-length-invalid:{path}:{step.Name}"; return false; }
            if (turn is not null) { if (!TryMeasure(turn, "deg", out var degrees) || !double.IsFinite(degrees)) { diagnostic = prefix; return false; } headingDegrees += degrees; }
            else if (absolute is not null) { if (!TryMeasure(absolute, "deg", out var degrees) || !double.IsFinite(degrees)) { diagnostic = prefix; return false; } headingDegrees = degrees; }
            endpoint = Advance(current, headingDegrees, distance); curves = [new LineArcLineSegment2D(current, endpoint)]; return true;
        }
        if (step.Kind is "Chamfer" or "Cutback" or "Step" or "Notch" or "Tab")
        {
            if (!TrySemanticFeature(path, step, current, headingDegrees, out curves, out endpoint, out diagnostic)) return false;
            return true;
        }
        var radiusText = Property(body, "Radius");
        if (step.Kind != "Arc" || radiusText is null || turn is null || absolute is not null || length is not null || to is not null || !TryMeasure(radiusText, "mm", out var radius) || radius <= 0 || !TryMeasure(turn, "deg", out var sweepDegrees) || !double.IsFinite(sweepDegrees) || Math.Abs(sweepDegrees) <= Tolerance || Math.Abs(sweepDegrees) >= 360d - Tolerance)
        { diagnostic = $"concept-path-arc-invalid:{path}:{step.Name}"; return false; }
        var headingRadians = DegreesToRadians(headingDegrees); var sweepRadians = DegreesToRadians(sweepDegrees); var sign = Math.Sign(sweepRadians);
        (double X, double Y) center = (current.X - sign * radius * Math.Sin(headingRadians), current.Y + sign * radius * Math.Cos(headingRadians));
        var startAngle = Math.Atan2(current.Y - center.Y, current.X - center.X);
        curves = [new LineArcCircularArc2D(center, radius, startAngle, sweepRadians)];
        endpoint = (center.Item1 + radius * Math.Cos(startAngle + sweepRadians), center.Item2 + radius * Math.Sin(startAngle + sweepRadians));
        headingDegrees += sweepDegrees;
        return true;
    }

    private static bool TrySemanticFeature(string path, PathStep step, (double X, double Y) current, double headingDegrees, out IReadOnlyList<LineArcProfileCurve2D>? curves, out (double X, double Y) endpoint, out string? diagnostic)
    {
        curves = null; endpoint = default; diagnostic = null;
        var stableId = $"concept-path:{path}.{step.Name}";
        var sideText = Property(step.Body, "Side");
        var side = string.Equals(sideText, "Left", StringComparison.OrdinalIgnoreCase) ? 1
            : string.Equals(sideText, "Right", StringComparison.OrdinalIgnoreCase) ? -1 : 0;
        bool Measure(string name, out double value)
        {
            value = default;
            var text = Property(step.Body, name);
            return text is not null && TryMeasure(text, "mm", out value);
        }

        SemanticProfileMemberIr? member = null;
        if (step.Kind is "Chamfer" or "Cutback")
        {
            if (!Measure("Run", out var run) || !Measure("Offset", out var offset))
            { diagnostic = $"semantic-profile-invalid-{step.Kind.ToLowerInvariant()}:{stableId}:Run and Offset are required"; return false; }
            member = step.Kind == "Chamfer"
                ? new SemanticProfileChamferIr(step.Name, stableId, run, offset, side, $"offset:{step.Index}")
                : new SemanticProfileCutbackIr(step.Name, stableId, run, offset, side, $"offset:{step.Index}");
        }
        else if (step.Kind == "Step")
        {
            if (!Measure("Run", out var run) || !Measure("Rise", out var rise))
            { diagnostic = $"semantic-profile-invalid-step:{stableId}:Run and Rise are required"; return false; }
            member = new SemanticProfileStepIr(step.Name, stableId, run, rise, side, $"offset:{step.Index}");
        }
        else if (step.Kind == "Notch")
        {
            if (!Measure("Width", out var width) || !Measure("Depth", out var depth))
            { diagnostic = $"semantic-profile-invalid-notch:{stableId}:Width and Depth are required"; return false; }
            member = new SemanticProfileNotchIr(step.Name, stableId, width, depth, side, $"offset:{step.Index}");
        }
        else if (step.Kind == "Tab")
        {
            if (!Measure("Width", out var width) || !Measure("Extension", out var extension))
            { diagnostic = $"semantic-profile-invalid-tab:{stableId}:Width and Extension are required"; return false; }
            member = new SemanticProfileTabIr(step.Name, stableId, width, extension, side, $"offset:{step.Index}");
        }

        var ir = new SemanticProfileIr(path, "concept-path:" + path, "XY", new(current.X, current.Y), headingDegrees,
            [member!], [], [], "Firmament Concept Path semantic member");
        var resolution = SemanticProfileMirResolver.Resolve(ir);
        if (!resolution.IsSuccess)
        {
            diagnostic = resolution.Diagnostics.FirstOrDefault() ?? $"semantic-profile-member-unresolved:{stableId}";
            return false;
        }
        var resolved = AssertSingle(resolution.Profile!.Members);
        curves = resolved.CurveDescendants.Select(descendant => descendant.Geometry).ToArray();
        endpoint = (resolved.End.X, resolved.End.Y);
        return true;

        static ResolvedSemanticProfileMemberIr AssertSingle(IReadOnlyList<ResolvedSemanticProfileMemberIr> members) =>
            members.Count == 1 ? members[0] : throw new InvalidOperationException("A bounded semantic feature must resolve to exactly one semantic member.");
    }

    private static IReadOnlyList<ResolvedProfileLoop2D> BindProfileLoops(ProfileBlock profile, IReadOnlyDictionary<string, BoundPath> paths, IReadOnlyDictionary<string, (double X, double Y)> points, IReadOnlyDictionary<string, LineArcProfileCurve2D> guides, List<string> diagnostics)
    {
        var loops = new List<ResolvedProfileLoop2D>();
        if (profile.FromPath is not null) { AddPathLoop("Outer", true, profile.FromPath, profile, paths, loops, diagnostics); return loops; }
        var loopBlocks = FindBlocks(profile.Body ?? string.Empty, @"\bLoop\s+(?<name>[A-Za-z_]\w*)(?:\s+From\s+(?<path>[A-Za-z_]\w*))?\s*\{").ToList();
        var blockLoopNames = new HashSet<string>(loopBlocks.Select(x => x.Match.Groups["name"].Value), StringComparer.Ordinal);
        foreach (Match loop in Regex.Matches(profile.Body ?? string.Empty, @"\bLoop\s+(?<name>[A-Za-z_]\w*)\s+From\s+(?<path>[A-Za-z_]\w*)\b(?!\s*\{)", RegexOptions.CultureInvariant))
            if (blockLoopNames.Add(loop.Groups["name"].Value))
                AddPathLoop(loop.Groups["name"].Value, string.Equals(loop.Groups["name"].Value, "Outer", StringComparison.Ordinal), loop.Groups["path"].Value, profile, paths, loops, diagnostics);
        foreach (var loop in loopBlocks)
        {
            var loopName = loop.Match.Groups["name"].Value; var fromPath = loop.Match.Groups["path"].Success ? loop.Match.Groups["path"].Value : null;
            var fromInsideMatch = Regex.Match(loop.Body, @"^\s*From\s*:\s*(?<path>[A-Za-z_]\w*)\b", RegexOptions.CultureInvariant);
            var fromInside = fromInsideMatch.Success ? fromInsideMatch.Groups["path"].Value : null;
            if (fromPath is not null || fromInside is not null) { AddPathLoop(loopName, string.Equals(loopName, "Outer", StringComparison.Ordinal), fromPath ?? fromInside!, profile, paths, loops, diagnostics); continue; }
            loops.Add(new ResolvedProfileLoop2D(loopName, string.Equals(loopName, "Outer", StringComparison.Ordinal), BindSegments(profile, loopName, loop.Body, paths, points, guides, diagnostics)));
        }
        // The first Profile frontend permitted direct segments without an explicit Loop.
        if (loops.Count == 0 && !string.IsNullOrWhiteSpace(profile.Body))
            loops.Add(new ResolvedProfileLoop2D("Outer", true, BindSegments(profile, "Outer", profile.Body, paths, points, guides, diagnostics)));
        if (loops.Count == 0) diagnostics.Add($"profile-loop-missing:{profile.Name}");
        return loops;
    }

    private static IReadOnlyList<ResolvedProfileSegment2D> BindSegments(ProfileBlock profile, string loopName, string body, IReadOnlyDictionary<string, BoundPath> paths, IReadOnlyDictionary<string, (double X, double Y)> points, IReadOnlyDictionary<string, LineArcProfileCurve2D> guides, List<string> diagnostics)
    {
        if (!body.Contains("|>", StringComparison.Ordinal)) return BindLowLevelSegments(profile, loopName, body, points, guides, diagnostics);
        if (Segment.IsMatch(body))
        {
            diagnostics.Add($"firmament-profile-pipeline-mixed-authoring:{profile.Name}:{loopName}");
            return [];
        }
        var matches = Pipeline.Matches(body).Cast<Match>().ToArray();
        var unconsumed = matches.Aggregate(body, (remaining, pipeline) => remaining.Replace(pipeline.Value, string.Empty, StringComparison.Ordinal));
        if (matches.Length != 1 || unconsumed.Any(character => !char.IsWhiteSpace(character) && character != ';'))
        {
            diagnostics.Add($"firmament-profile-pipeline-expression-required:{profile.Name}:{loopName}");
            return [];
        }
        return BindProfilePipeline(profile, loopName, matches[0], paths, points, guides, diagnostics);
    }

    private static IReadOnlyList<ResolvedProfileSegment2D> BindProfilePipeline(ProfileBlock profile, string loopName, Match match, IReadOnlyDictionary<string, BoundPath> paths, IReadOnlyDictionary<string, (double X, double Y)> points, IReadOnlyDictionary<string, LineArcProfileCurve2D> guides, List<string> diagnostics)
    {
        var stages = ParsePipelineStages(match.Groups["expression"].Value);
        if (stages.Count == 2 && stages[1].Kind == PipelineStageKind.TraceLoop)
        {
            if (!paths.TryGetValue(stages[0].Reference!, out var path))
            {
                diagnostics.Add($"firmament-profile-traceloop-not-loop:{stages[0].Reference}");
                return [];
            }
            if (!IsClosed(path.Steps.SelectMany(step => step.Curves).ToArray()))
            {
                diagnostics.Add($"firmament-profile-traceloop-not-loop:{stages[0].Reference}");
                return [];
            }
            var curves = path.Steps.SelectMany(step => step.Curves.Select((curve, ordinal) => (Step: step, Curve: curve, Ordinal: ordinal))).ToList();
            var wantCounterClockwise = string.Equals(loopName, "Outer", StringComparison.Ordinal);
            var reversedLoop = (SignedArea(curves.Select(x => x.Curve)) > 0) != wantCounterClockwise;
            if (reversedLoop) curves = curves.AsEnumerable().Reverse().Select(x => (x.Step, Reverse(x.Curve), x.Ordinal)).ToList();
            return curves.Select((entry, index) =>
            {
                var inherited = entry.Step.Curves.Count == 1 ? entry.Step.Name : $"{entry.Step.Name}.curve{entry.Ordinal:D2}";
                var name = string.Equals(loopName, "Outer", StringComparison.Ordinal) ? inherited : $"{loopName}.{inherited}";
                return PipelineSegment(profile, loopName, name, entry.Curve, $"concept-path:{path.Name}.{entry.Step.Name}", $"{path.Name}.{entry.Step.Name}", reversedLoop, index, match.Index);
            }).ToArray();
        }
        if (stages.Any(stage => stage.Kind == PipelineStageKind.TraceLoop))
        {
            diagnostics.Add("firmament-pipeline-stage-type:TraceLoop");
            return [];
        }
        var close = stages.LastOrDefault()?.Kind == PipelineStageKind.Close;
        if (stages.Any(stage => stage.Kind == PipelineStageKind.Close && stage != stages[^1]))
        {
            diagnostics.Add("firmament-pipeline-stage-type:Close");
            return [];
        }
        var traceStages = close ? stages.Take(stages.Count - 1).ToArray() : stages.ToArray();
        var result = new List<ResolvedProfileSegment2D>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        (double X, double Y)? first = null;
        (double X, double Y)? current = null;
        string? currentIdentity = null;
        for (var index = 0; index < traceStages.Length; index++)
        {
            var stage = traceStages[index];
            if (stage.Kind != PipelineStageKind.Trace || !guides.TryGetValue(stage.Reference!, out var source))
            {
                diagnostics.Add($"firmament-pipeline-stage-type:{stage.Reference}");
                continue;
            }
            var oriented = OrientPipelineCurve(source, stage.Reverse, current, stage.Reference!, points, diagnostics, out var reversed);
            if (oriented is null) continue;
            var start = Start(oriented); var end = End(oriented);
            first ??= start;
            current = end;
            currentIdentity = DescribeEndpoint(end, points);
            var inheritedName = stage.Reference!.Split('.').Last();
            var name = stage.Alias ?? (string.Equals(loopName, "Outer", StringComparison.Ordinal) ? inheritedName : $"{loopName}.{inheritedName}");
            if (!names.Add(name)) { diagnostics.Add($"firmament-profile-pipeline-identity-collision:{name}"); continue; }
            result.Add(PipelineSegment(profile, loopName, name, oriented, $"concept:{profile.Frame ?? "XY"}.{stage.Reference}", stage.Reference!, reversed, index, match.Index + stage.Offset));
        }
        if (close && first is not null && current is not null && Distance(first.Value, current.Value) > Tolerance)
            diagnostics.Add($"firmament-pipeline-close-open:{loopName}:first={DescribeEndpoint(first.Value, points)}:current={currentIdentity}");
        return result;
    }

    private static BoundPath? BindPathPipeline(string name, string body, IReadOnlyDictionary<string, LineArcProfileCurve2D> guides, List<string> diagnostics)
    {
        var match = Pipeline.Match(body);
        var unconsumed = match.Success ? body.Replace(match.Value, string.Empty, StringComparison.Ordinal) : body;
        if (!match.Success || unconsumed.Any(character => !char.IsWhiteSpace(character) && character != ';'))
        {
            diagnostics.Add($"firmament-path-pipeline-expression-required:{name}");
            return null;
        }
        var stages = ParsePipelineStages(match.Groups["expression"].Value);
        if (stages.Any(stage => stage.Kind is PipelineStageKind.Close or PipelineStageKind.TraceLoop))
        {
            diagnostics.Add($"firmament-pipeline-stage-type:{stages.First(stage => stage.Kind is PipelineStageKind.Close or PipelineStageKind.TraceLoop).Kind}");
            return null;
        }
        var steps = new List<BoundPathStep>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        (double X, double Y)? current = null;
        foreach (var stage in stages)
        {
            if (!guides.TryGetValue(stage.Reference!, out var source)) { diagnostics.Add($"firmament-pipeline-stage-type:{stage.Reference}"); continue; }
            var oriented = OrientPipelineCurve(source, stage.Reverse, current, stage.Reference!, null, diagnostics, out var reversed);
            if (oriented is null) continue;
            var stepName = stage.Alias ?? stage.Reference!.Split('.').Last();
            if (!names.Add(stepName)) { diagnostics.Add($"firmament-profile-pipeline-identity-collision:{stepName}"); continue; }
            var start = Start(oriented); var end = End(oriented);
            steps.Add(new(stepName, oriented is LineArcCircularArc2D ? "ArcTransition" : "Span", $"{name}.{stepName}", $"{name}.{stepName}.End", [oriented], start, end, Heading(start, end), stage.Reference, reversed, steps.Count, $"offset:{match.Index + stage.Offset}"));
            current = end;
        }
        return steps.Count == 0 ? null : new BoundPath(name, steps[0].Start, Heading(steps[0].Start, steps[0].End), steps);
    }

    private static LineArcProfileCurve2D? OrientPipelineCurve(LineArcProfileCurve2D source, bool explicitReverse, (double X, double Y)? current, string reference, IReadOnlyDictionary<string, (double X, double Y)>? points, List<string> diagnostics, out bool reversed)
    {
        reversed = explicitReverse;
        var candidate = explicitReverse ? Reverse(source) : source;
        if (candidate is LineArcFullCircle2D)
        {
            diagnostics.Add($"firmament-profile-pipeline-orientation-ambiguous:{reference}");
            return null;
        }
        if (current is null) return candidate;
        var matchesStart = Distance(current.Value, Start(candidate)) <= Tolerance;
        var matchesEnd = Distance(current.Value, End(candidate)) <= Tolerance;
        if (matchesStart && matchesEnd) { diagnostics.Add($"firmament-profile-pipeline-orientation-ambiguous:{reference}"); return null; }
        if (explicitReverse)
        {
            if (!matchesStart) diagnostics.Add(Disconnected(current.Value, reference, candidate, points));
            return matchesStart ? candidate : null;
        }
        if (matchesStart) return candidate;
        if (matchesEnd) { reversed = true; return Reverse(candidate); }
        diagnostics.Add(Disconnected(current.Value, reference, candidate, points));
        return null;
    }

    private static string Disconnected((double X, double Y) current, string reference, LineArcProfileCurve2D candidate, IReadOnlyDictionary<string, (double X, double Y)>? points) =>
        $"firmament-profile-pipeline-disconnected:{reference}:current={DescribeEndpoint(current, points)}:candidates={DescribeEndpoint(Start(candidate), points)},{DescribeEndpoint(End(candidate), points)}";

    private static string DescribeEndpoint((double X, double Y) point, IReadOnlyDictionary<string, (double X, double Y)>? points) =>
        points?.Where(entry => Distance(entry.Value, point) <= Tolerance).Select(entry => entry.Key).OrderByDescending(x => x.Contains('.')).ThenBy(x => x, StringComparer.Ordinal).FirstOrDefault() ?? $"Point2({point.X:R}mm,{point.Y:R}mm)";

    private static ResolvedProfileSegment2D PipelineSegment(ProfileBlock profile, string loop, string name, LineArcProfileCurve2D geometry, string conceptId, string tracedFrom, bool reversed, int index, int offset) =>
        new(name, geometry, new($"profile:{profile.Name}.{loop}.{name}", conceptId, profile.SourceSpan, $"PipelineTrace({tracedFrom});reversed={reversed.ToString().ToLowerInvariant()};pipelineIndex={index}", profile.Frame ?? "XY", tracedFrom, reversed, index, $"offset:{offset}"));

    private static IReadOnlyList<PipelineStage> ParsePipelineStages(string expression)
    {
        var stages = new List<PipelineStage>();
        var offset = 0;
        foreach (var raw in expression.Split("|>", StringSplitOptions.TrimEntries))
        {
            var localOffset = expression.IndexOf(raw, offset, StringComparison.Ordinal); offset = localOffset + raw.Length;
            if (raw == "Close") stages.Add(new(PipelineStageKind.Close, null, null, false, localOffset));
            else if (raw == "TraceLoop") stages.Add(new(PipelineStageKind.TraceLoop, null, null, false, localOffset));
            else
            {
                var parsed = Regex.Match(raw, @"^(?<reverse>Reverse\s+)?(?<reference>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)(?:\s+As\s+(?<alias>[A-Za-z_]\w*))?$", RegexOptions.CultureInvariant);
                stages.Add(parsed.Success
                    ? new(PipelineStageKind.Trace, parsed.Groups["reference"].Value, parsed.Groups["alias"].Success ? parsed.Groups["alias"].Value : null, parsed.Groups["reverse"].Success, localOffset)
                    : new(PipelineStageKind.Invalid, raw, null, false, localOffset));
            }
        }
        return stages;
    }

    private static bool IsClosed(IReadOnlyList<LineArcProfileCurve2D> curves) => curves.Count > 0 && Distance(Start(curves[0]), End(curves[^1])) <= Tolerance && curves.Zip(curves.Skip(1)).All(pair => Distance(End(pair.First), Start(pair.Second)) <= Tolerance);
    private static double SignedArea(IEnumerable<LineArcProfileCurve2D> curves) => curves.Sum(curve => curve switch
    {
        LineArcLineSegment2D line => (line.Start.X * line.End.Y - line.End.X * line.Start.Y) / 2d,
        LineArcCircularArc2D arc => (arc.Radius * arc.Center.X * (Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians) - Math.Sin(arc.StartAngleRadians)) + arc.Radius * arc.Center.Y * (Math.Cos(arc.StartAngleRadians) - Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians)) + arc.Radius * arc.Radius * arc.SweepAngleRadians) / 2d,
        _ => 0d
    });
    private static LineArcProfileCurve2D Reverse(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => new LineArcLineSegment2D(line.End, line.Start),
        LineArcCircularArc2D arc => new LineArcCircularArc2D(arc.Center, arc.Radius, arc.StartAngleRadians + arc.SweepAngleRadians, -arc.SweepAngleRadians),
        _ => curve
    };

    private static IReadOnlyList<ResolvedProfileSegment2D> BindLowLevelSegments(ProfileBlock profile, string loopName, string body, IReadOnlyDictionary<string, (double X, double Y)> points, IReadOnlyDictionary<string, LineArcProfileCurve2D> guides, List<string> diagnostics)
    {
        foreach (Match raw in Regex.Matches(body, @"\bSegment\s+(?<name>\w+)\s*\{[\s\S]*?\b(?<endpoint>From|To)\s*:\s*(?<value>\[[^\]]*\]|Point2\s*\([^)]*\))", RegexOptions.CultureInvariant))
            diagnostics.Add($"{SegmentEndpointMustReferenceNamedPoint}:{raw.Groups["name"].Value}:{raw.Groups["endpoint"].Value}");
        var segments = new List<ResolvedProfileSegment2D>();
        foreach (Match match in Segment.Matches(body))
        {
            var name = match.Groups["n"].Value;
            if (!points.TryGetValue(match.Groups["from"].Value, out var from) || !points.TryGetValue(match.Groups["to"].Value, out var to)) { diagnostics.Add($"profile-segment-unresolved:{name}"); continue; }
            if (!guides.TryGetValue(match.Groups["trace"].Value, out var guide)) { diagnostics.Add($"profile-guide-missing:{name}:{match.Groups["trace"].Value}"); continue; }
            var geometry = SelectGuide(guide, from, to, match.Groups["sweep"].Value, name, match.Groups["trace"].Value, diagnostics);
            if (geometry is not null) segments.Add(SegmentResult(profile, loopName, name, geometry, $"concept:{profile.Frame ?? "XY"}.{match.Groups["trace"].Value}", $"Trace({match.Groups["trace"].Value})"));
        }
        return segments;
    }

    private static void AddPathLoop(string loopName, bool outer, string pathName, ProfileBlock profile, IReadOnlyDictionary<string, BoundPath> paths, List<ResolvedProfileLoop2D> loops, List<string> diagnostics)
    {
        if (!paths.TryGetValue(pathName, out var path)) { diagnostics.Add($"profile-path-missing:{profile.Name}:{pathName}"); return; }
        loops.Add(new ResolvedProfileLoop2D(loopName, outer, path.Steps.SelectMany(step => step.Curves.Select((curve, ordinal) => SegmentResult(
            profile, loopName,
            outer ? (step.Curves.Count == 1 ? step.Name : $"{step.Name}.curve{ordinal:D2}") : $"{loopName}.{step.Name}.curve{ordinal:D2}",
            curve, $"concept-path:{pathName}.{step.Name}", $"SemanticProfileMIR:{step.Kind}"))).ToArray()));
    }

    private static ResolvedProfileSegment2D SegmentResult(ProfileBlock profile, string loop, string name, LineArcProfileCurve2D geometry, string conceptId, string derivation) => new(name, geometry, new($"profile:{profile.Name}.{loop}.{name}", conceptId, profile.SourceSpan, derivation, profile.Frame ?? "XY"));

    private static LineArcProfileCurve2D? SelectGuide(LineArcProfileCurve2D guide, (double X, double Y) from, (double X, double Y) to, string sweep, string segment, string trace, List<string> diagnostics)
    {
        if (guide is LineArcLineSegment2D line) { if (!OnLine(from, line) || !OnLine(to, line)) diagnostics.Add($"profile-endpoint-not-on-guide:{segment}:{trace}"); return new LineArcLineSegment2D(from, to); }
        if (guide is LineArcFullCircle2D circle)
        {
            if (!OnCircle(from, circle.Center, circle.Radius) || !OnCircle(to, circle.Center, circle.Radius) || string.IsNullOrWhiteSpace(sweep)) { diagnostics.Add($"profile-arc-invalid:{segment}:{trace}"); return null; }
            var start = Math.Atan2(from.Y - circle.Center.Y, from.X - circle.Center.X); var amount = Math.Atan2(to.Y - circle.Center.Y, to.X - circle.Center.X) - start;
            var ccw = sweep == "CounterClockwise"; while (ccw && amount <= 0) amount += 2 * Math.PI; while (!ccw && amount >= 0) amount -= 2 * Math.PI;
            return new LineArcCircularArc2D(circle.Center, circle.Radius, start, amount);
        }
        diagnostics.Add($"profile-guide-missing:{segment}:{trace}"); return null;
    }

    private static ConstructionPlane? ResolveConstructionPlane(string source, string? frame, List<string> diagnostics)
    {
        if (frame is null) return ConstructionPlane.WorldXY;
        var plane = ConstructionPlaneDeclaration.Matches(source).Cast<Match>().SingleOrDefault(x => x.Groups["name"].Value == frame);
        if (plane is null) return ConstructionPlane.WorldXY;
        if (!ConceptIrResolver.TryResolvePlane(source, plane.Groups["trace"].Value, out var conceptPlane, out var traceDiagnostic) || conceptPlane is null) { diagnostics.Add(traceDiagnostic ?? "ConstructionPlaneTraceMissing"); return null; }
        if (!ConstructionPlane.TryTrace("construction:" + frame, conceptPlane, $"offset:{plane.Index}", out var result, out var frameDiagnostic)) diagnostics.Add(frameDiagnostic ?? "ConstructionPlaneFrameInvalid");
        return result;
    }

    private static (double Start, double End, double Height) ResolveExtrude(string source, string profileName, List<string> diagnostics)
    {
        var match = Extrude.Matches(source).Cast<Match>().FirstOrDefault(x => x.Groups["p"].Value == profileName);
        if (match is null || !TryNumber(match.Groups["a"].Value, out var start) || !TryNumber(match.Groups["b"].Value, out var end)) { diagnostics.Add("profile-extrude-missing-or-mismatched"); return default; }
        return (start, end, Math.Abs(end - start));
    }

    private static IEnumerable<ProfileBlock> FindProfiles(string source)
    {
        foreach (var block in FindBlocks(source, @"\bProfile\s+(?<name>[A-Za-z_]\w*)(?:\s+Using\s+(?<frame>[A-Za-z_]\w*))?\s*\{")) yield return new(block.Match.Groups["name"].Value, block.Match.Groups["frame"].Success ? block.Match.Groups["frame"].Value : null, null, block.Body, $"source:{block.Match.Index}");
        foreach (Match match in Regex.Matches(source, @"\bProfile\s+(?<name>[A-Za-z_]\w*)\s+From\s+(?<path>[A-Za-z_]\w*)\b", RegexOptions.CultureInvariant)) yield return new(match.Groups["name"].Value, null, match.Groups["path"].Value, null, $"source:{match.Index}");
    }

    private static IEnumerable<Block> FindBlocks(string source, string headerPattern)
    {
        foreach (Match match in Regex.Matches(source, headerPattern, RegexOptions.CultureInvariant))
        {
            var open = source.IndexOf('{', match.Index, match.Length); var depth = 0; var end = -1;
            for (var index = open; index >= 0 && index < source.Length; index++) { if (source[index] == '{') depth++; else if (source[index] == '}' && --depth == 0) { end = index; break; } }
            if (open >= 0 && end > open) yield return new(match, source[(open + 1)..end], end);
        }
    }

    private static int FindMatchingBrace(string source, int open)
    {
        var depth = 0;
        for (var index = open; index >= 0 && index < source.Length; index++)
        {
            if (source[index] == '{') depth++;
            else if (source[index] == '}' && --depth == 0) return index;
        }
        return -1;
    }

    private static IEnumerable<PathStep> FindPathSteps(string body)
    {
        var entries = new List<PathStep>();
        foreach (var block in FindBlocks(body, @"\b(?<kind>Line|Span|Arc|Chamfer|Step|Notch|Cutback|Tab)\s+(?<name>[A-Za-z_]\w*)\s*\{")) entries.Add(new(block.Match.Groups["kind"].Value, block.Match.Groups["name"].Value, block.Body, block.Match.Index));
        foreach (Match match in Regex.Matches(body, @"\bClose\s+(?<name>[A-Za-z_]\w*)\b", RegexOptions.CultureInvariant)) entries.Add(new("Close", match.Groups["name"].Value, string.Empty, match.Index));
        return entries.OrderBy(x => x.Index);
    }

    private static string? Property(string body, string name)
    {
        // A property expression may contain whitespace (for example P.Length - P.Gap),
        // while legacy compact authoring may put the next Name: property on the same line.
        var match = Regex.Match(body, $@"\b{name}\s*:\s*(?<v>.*?)(?=\s+[A-Za-z_]\w*\s*:|[;\r\n}}]|$)", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["v"].Value.Trim() : null;
    }
    private static bool TryMeasure(string text, string unit, out double value)
    {
        var parser = new BoundedMeasureExpression(text, unit);
        return parser.TryEvaluate(out value);
    }
    private static bool TryNumber(string text, out double value) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    private static (double X, double Y) Advance((double X, double Y) point, double heading, double length) { var radians = DegreesToRadians(heading); return (point.X + length * Math.Cos(radians), point.Y + length * Math.Sin(radians)); }
    private static double DegreesToRadians(double value) => value * Math.PI / 180d;
    private static double Heading((double X, double Y) from, (double X, double Y) to) => Math.Atan2(to.Y - from.Y, to.X - from.X) * 180d / Math.PI;
    private static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static (double X,double Y) Start(LineArcProfileCurve2D curve)=>curve switch{LineArcLineSegment2D line=>line.Start,LineArcCircularArc2D arc=>(arc.Center.X+arc.Radius*Math.Cos(arc.StartAngleRadians),arc.Center.Y+arc.Radius*Math.Sin(arc.StartAngleRadians)),_=>throw new InvalidOperationException()};
    private static (double X,double Y) End(LineArcProfileCurve2D curve)=>curve switch{LineArcLineSegment2D line=>line.End,LineArcCircularArc2D arc=>(arc.Center.X+arc.Radius*Math.Cos(arc.StartAngleRadians+arc.SweepAngleRadians),arc.Center.Y+arc.Radius*Math.Sin(arc.StartAngleRadians+arc.SweepAngleRadians)),_=>throw new InvalidOperationException()};
    private static bool OnLine((double X, double Y) point, LineArcLineSegment2D line) => Math.Abs((line.End.X - line.Start.X) * (point.Y - line.Start.Y) - (line.End.Y - line.Start.Y) * (point.X - line.Start.X)) < 1e-7;
    private static bool OnCircle((double X, double Y) point, (double X, double Y) center, double radius) => Math.Abs(Distance(point, center) - radius) < 1e-7;

    private sealed record Block(Match Match, string Body, int EndIndex);
    private sealed record ProfileBlock(string Name, string? Frame, string? FromPath, string? Body, string SourceSpan);
    private sealed record PathStep(string Kind, string Name, string Body, int Index);
    private sealed record BoundPathStep(string Name, string Kind, string GuideName, string EndpointName, IReadOnlyList<LineArcProfileCurve2D> Curves, (double X, double Y) Start, (double X, double Y) End, double Heading, string? SourceReference = null, bool WasReversed = false, int? PipelineIndex = null, string? SourceRange = null);
    private sealed record BoundPath(string Name, (double X, double Y) Start, double InitialHeading, List<BoundPathStep> Steps);
    private enum PipelineStageKind { Trace, Close, TraceLoop, Invalid }
    private sealed record PipelineStage(PipelineStageKind Kind, string? Reference, string? Alias, bool Reverse, int Offset);

    /// <summary>Small deterministic dimensional expression evaluator used after Template specialization.</summary>
    /// <remarks>Admits only numeric literals, one requested unit, parentheses, and + - * /.</remarks>
    private sealed class BoundedMeasureExpression(string source, string unit)
    {
        private int index;
        public bool TryEvaluate(out double value)
        {
            value = default; index = 0;
            if (!Expression(out var result) || result.Dimension != 1) return false;
            Space();
            if (index != source.Length || !double.IsFinite(result.Value)) return false;
            value = result.Value; return true;
        }
        private bool Expression(out Quantity result)
        {
            if (!Term(out result)) return false;
            while (true)
            {
                Space(); if (index >= source.Length || source[index] is not ('+' or '-')) return true;
                var operation = source[index++]; if (!Term(out var right) || result.Dimension != right.Dimension) return false;
                result = new(operation == '+' ? result.Value + right.Value : result.Value - right.Value, result.Dimension);
            }
        }
        private bool Term(out Quantity result)
        {
            if (!Factor(out result)) return false;
            while (true)
            {
                Space(); if (index >= source.Length || source[index] is not ('*' or '/')) return true;
                var operation = source[index++]; if (!Factor(out var right)) return false;
                var dimension = operation == '*' ? result.Dimension + right.Dimension : result.Dimension - right.Dimension;
                if (dimension is < 0 or > 1 || operation == '/' && Math.Abs(right.Value) <= double.Epsilon) return false;
                result = new(operation == '*' ? result.Value * right.Value : result.Value / right.Value, dimension);
            }
        }
        private bool Factor(out Quantity result)
        {
            Space(); result = default; if (index >= source.Length) return false;
            if (source[index] == '(') { index++; if (!Expression(out result)) return false; Space(); return index < source.Length && source[index++] == ')'; }
            var sign = 1d; if (source[index] is '+' or '-') sign = source[index++] == '-' ? -1d : 1d;
            Space(); var start = index;
            while (index < source.Length && (char.IsDigit(source[index]) || source[index] is '.' or 'e' or 'E' || source[index] is '+' or '-' && index > start && source[index - 1] is 'e' or 'E')) index++;
            if (start == index || !TryNumber(source[start..index], out var number)) return false;
            var dimension = 0; if (source.AsSpan(index).StartsWith(unit, StringComparison.Ordinal)) { index += unit.Length; dimension = 1; }
            result = new(sign * number, dimension); return true;
        }
        private void Space() { while (index < source.Length && char.IsWhiteSpace(source[index])) index++; }
        private readonly record struct Quantity(double Value, int Dimension);
    }
}
