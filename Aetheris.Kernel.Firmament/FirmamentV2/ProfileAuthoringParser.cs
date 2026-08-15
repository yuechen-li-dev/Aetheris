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
    private static readonly Regex Extrude = new(@"\bExtrude\s+\w+\s*\{\s*Profile\s*:\s*(?<p>\w+)\s*;?\s*From\s*:\s*(?<a>[-+.\deE]+)mm\s*;?\s*To\s*:\s*(?<b>[-+.\deE]+)mm", RegexOptions.Singleline | RegexOptions.CultureInvariant);

    public static bool IsProfileSource(string source) => Regex.IsMatch(source, @"\bProfile\s+[A-Za-z_]\w*", RegexOptions.CultureInvariant);

    public static IReadOnlyList<ConceptPathInspection> InspectConceptPaths(string source)
    {
        var diagnostics = new List<string>();
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
        var authoredProfiles = FindProfiles(source).Where(candidate => candidate.FromPath is not null).ToArray();
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

    private static void AddOrdinaryGuides(string source, Dictionary<string, (double X, double Y)> points, Dictionary<string, LineArcProfileCurve2D> guides, List<string> diagnostics)
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
    }

    private static IReadOnlyDictionary<string, BoundPath> BindPaths(string source, Dictionary<string, (double X, double Y)> points, Dictionary<string, LineArcProfileCurve2D> guides, List<string> diagnostics)
    {
        var result = new Dictionary<string, BoundPath>(StringComparer.Ordinal);
        foreach (var block in FindBlocks(source, @"\bConcept\s+Path\s+(?<name>[A-Za-z_]\w*)\s*\{"))
        {
            var name = block.Match.Groups["name"].Value;
            if (!result.TryAdd(name, default!)) { diagnostics.Add($"concept-path-duplicate:{name}"); continue; }
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
        foreach (Match rectangle in Rect.Matches(source))
        {
            var name = rectangle.Groups["n"].Value;
            var isProfileSource = Regex.IsMatch(source, $@"\bProfile\s+[A-Za-z_]\w*\s+From\s+{Regex.Escape(name)}\b", RegexOptions.CultureInvariant);
            if (!isProfileSource && !programs.Any(x => x.Match.Groups["owner"].Value == name)) continue;
            if (!TryNumber(rectangle.Groups["w"].Value, out var width) || !TryNumber(rectangle.Groups["h"].Value, out var height) ||
                !TryNumber(rectangle.Groups["x"].Value, out var cx) || !TryNumber(rectangle.Groups["y"].Value, out var cy)) continue;
            if (result.ContainsKey(name)) { diagnostics.Add($"profile-edge-owner-name-collision:{name}"); continue; }
            var bl = (X: cx - width / 2, Y: cy - height / 2); var br = (X: cx + width / 2, Y: cy - height / 2);
            var tr = (X: cx + width / 2, Y: cy + height / 2); var tl = (X: cx - width / 2, Y: cy + height / 2);
            var path = new BoundPath(name, bl, 0, []);
            AddEdge("Bottom", bl, br); AddEdge("Right", br, tr); AddEdge("Top", tr, tl); AddEdge("Left", tl, bl);
            result[name] = path;

            void AddEdge(string edge, (double X, double Y) start, (double X, double Y) end)
            {
                var matching = programs.Where(x => x.Match.Groups["owner"].Value == name && x.Match.Groups["edge"].Value == edge).ToArray();
                if (matching.Length == 0)
                {
                    path.Steps.Add(new(edge, "Span", $"{name}.{edge}", $"{name}.{edge}.End", [new LineArcLineSegment2D(start, end)], start, end, Heading(start, end)));
                    return;
                }
                if (matching.Length > 1) { diagnostics.Add($"semantic-edge-duplicate-program:{name}.{edge}"); return; }
                var fragments = new List<SemanticEdgeFragmentIr>();
                foreach (var step in FindPathSteps(matching[0].Body).Where(x => x.Kind is "Chamfer" or "Step" or "Notch" or "Cutback" or "Tab"))
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
                var resolution = SemanticEdgeProfileResolver.Resolve(new($"{name}.{edge}", $"{name}.{edge}", new(start.X, start.Y), new(end.X, end.Y), fragments, $"{name}.{edge}[u,v]", $"EdgeProfile {name}.{edge}"));
                if (!resolution.IsSuccess) { diagnostics.AddRange(resolution.Diagnostics); return; }
                foreach (var member in resolution.Profile!.OrderedMembers)
                {
                    var first = ((LineArcLineSegment2D)member.CurveDescendants.First().Geometry).Start; var last = ((LineArcLineSegment2D)member.CurveDescendants.Last().Geometry).End;
                    path.Steps.Add(new($"{edge}.{member.Name}", member.Kind, $"{name}.{edge}.{member.Name}", $"{name}.{edge}.{member.Name}.End", member.CurveDescendants.Select(x => x.Geometry).ToArray(), first, last, Heading(first, last)));
                }
            }
        }
        foreach (var program in programs.Where(p => !result.ContainsKey(p.Match.Groups["owner"].Value)))
            diagnostics.Add($"semantic-edge-owner-path-missing:{program.Match.Groups["owner"].Value}.{program.Match.Groups["edge"].Value}");
        foreach (var program in programs.Where(p => result.ContainsKey(p.Match.Groups["owner"].Value) && p.Match.Groups["edge"].Value is not ("Bottom" or "Right" or "Top" or "Left")))
            diagnostics.Add($"semantic-edge-owner-member-missing:{program.Match.Groups["owner"].Value}.{program.Match.Groups["edge"].Value}:available=Bottom,Right,Top,Left");
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
            loops.Add(new ResolvedProfileLoop2D(loopName, string.Equals(loopName, "Outer", StringComparison.Ordinal), BindLowLevelSegments(profile, loopName, loop.Body, points, guides, diagnostics)));
        }
        // The first Profile frontend permitted direct segments without an explicit Loop.
        if (loops.Count == 0 && !string.IsNullOrWhiteSpace(profile.Body))
            loops.Add(new ResolvedProfileLoop2D("Outer", true, BindLowLevelSegments(profile, "Outer", profile.Body, points, guides, diagnostics)));
        if (loops.Count == 0) diagnostics.Add($"profile-loop-missing:{profile.Name}");
        return loops;
    }

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
            if (open >= 0 && end > open) yield return new(match, source[(open + 1)..end]);
        }
    }

    private static IEnumerable<PathStep> FindPathSteps(string body)
    {
        var entries = new List<PathStep>();
        foreach (var block in FindBlocks(body, @"\b(?<kind>Line|Span|Arc|Chamfer|Step|Notch|Cutback|Tab)\s+(?<name>[A-Za-z_]\w*)\s*\{")) entries.Add(new(block.Match.Groups["kind"].Value, block.Match.Groups["name"].Value, block.Body, block.Match.Index));
        foreach (Match match in Regex.Matches(body, @"\bClose\s+(?<name>[A-Za-z_]\w*)\b", RegexOptions.CultureInvariant)) entries.Add(new("Close", match.Groups["name"].Value, string.Empty, match.Index));
        return entries.OrderBy(x => x.Index);
    }

    private static string? Property(string body, string name) { var match = Regex.Match(body, $@"\b{name}\s*:\s*(?<v>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)?|[-+.\deE]+(?:mm|deg)?)", RegexOptions.CultureInvariant); return match.Success ? match.Groups["v"].Value : null; }
    private static bool TryMeasure(string text, string unit, out double value) { value = default; var match = Regex.Match(text, $@"^(?<v>[-+.\deE]+){unit}$", RegexOptions.CultureInvariant); return match.Success && TryNumber(match.Groups["v"].Value, out value); }
    private static bool TryNumber(string text, out double value) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    private static (double X, double Y) Advance((double X, double Y) point, double heading, double length) { var radians = DegreesToRadians(heading); return (point.X + length * Math.Cos(radians), point.Y + length * Math.Sin(radians)); }
    private static double DegreesToRadians(double value) => value * Math.PI / 180d;
    private static double Heading((double X, double Y) from, (double X, double Y) to) => Math.Atan2(to.Y - from.Y, to.X - from.X) * 180d / Math.PI;
    private static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static bool OnLine((double X, double Y) point, LineArcLineSegment2D line) => Math.Abs((line.End.X - line.Start.X) * (point.Y - line.Start.Y) - (line.End.Y - line.Start.Y) * (point.X - line.Start.X)) < 1e-7;
    private static bool OnCircle((double X, double Y) point, (double X, double Y) center, double radius) => Math.Abs(Distance(point, center) - radius) < 1e-7;

    private sealed record Block(Match Match, string Body);
    private sealed record ProfileBlock(string Name, string? Frame, string? FromPath, string? Body, string SourceSpan);
    private sealed record PathStep(string Kind, string Name, string Body, int Index);
    private sealed record BoundPathStep(string Name, string Kind, string GuideName, string EndpointName, IReadOnlyList<LineArcProfileCurve2D> Curves, (double X, double Y) Start, (double X, double Y) End, double Heading);
    private sealed record BoundPath(string Name, (double X, double Y) Start, double InitialHeading, List<BoundPathStep> Steps);
}
