using System.Globalization;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Surfacing;

namespace Aetheris.SheetMetal;

public sealed record AuthoredSheetBaseSpec(string Name, double Width, double Depth)
{
    /// <summary>Physical lower-skin origin of the rectangular base in authored coordinates.</summary>
    public Point3D Origin { get; init; } = Point3D.Origin;
}
public sealed record AuthoredSheetFlangeSpec(
    string Name, string ParentRegion, string EdgeName, double Length, double AngleRadians,
    double InsideRadius, SheetBendDirection Direction, SheetCornerPolicy CornerPolicy,
    SheetReliefPolicy ReliefPolicy, double? ReliefWidth = null, double? ReliefDepth = null,
    double? SpanLength = null, double SpanOffset = 0d);
public sealed record AuthoredSheetCutSpec(
    string Name, string RegionName, SheetFeatureKind Kind, double X, double Y,
    double? Diameter, double? Width, double? Length);
public sealed record SheetMetalConstructionSpec(
    string Name, double Thickness, string? Material, double KFactor, AuthoredSheetBaseSpec Base,
    IReadOnlyList<AuthoredSheetFlangeSpec> Flanges, IReadOnlyList<AuthoredSheetCutSpec> Cuts,
    SheetMetalProvenanceCategory Authority, bool LegacySyntax = false, string? SatisfiesConcept = null)
{
    /// <summary>Compile-time semantic layout erased after it resolves exact authored cuts.</summary>
    public SheetMetalSemanticLayout SemanticLayout { get; init; } = SheetMetalSemanticLayout.Empty;
}

/// <summary>
/// Bounded high-level authored Sheet Metal compiler. It deliberately consumes only
/// Firmament declarations; imported/recovered geometry is not accepted by this path.
/// </summary>
internal static class AuthoredSheetMetalCompiler
{
    private const RegexOptions Rx = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline;
    private const string Number = @"[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)";

    public static SheetMetalAuthoringResult Compile(string source, string sourcePath)
    {
        var parseStarted=Stopwatch.GetTimestamp();
        var diagnostics = new List<SheetMetalDiagnostic>();
        var header = Regex.Match(source, @"\bSheetMetal\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:\s*:\s*(?<concept>[A-Za-z_][A-Za-z0-9_]*))?\s*\{", Rx);
        if (!header.Success) return Fail("Expected `SheetMetal <Name> { ... }`.");
        if (!Scalar(source, "Thickness", "mm", out var thickness) || thickness <= 0)
            return Fail("Thickness must be a positive millimetre value.");
        var material = Quoted(source, "Material");
        var k = Scalar(source, "KFactor", null, out var parsedK) ? parsedK : SheetMetalFlattenPolicy.Default.KFactor;
        if (k is < 0 or > 1) return Fail("KFactor must be between zero and one.");

        AuthoredSheetBaseSpec? baseSpec = null;
        var legacyBase = Regex.Match(source, $@"\bBase\s*:\s*Rectangle\s*\(\s*(?<w>{Number})\s*mm\s*,\s*(?<d>{Number})\s*mm\s*\)\s*;", Rx);
        if (legacyBase.Success) baseSpec = new("Base", Num(legacyBase, "w"), Num(legacyBase, "d"));
        else
        {
            var named = Blocks(source, "Base").SingleOrDefault();
            if (named is not null && Scalar(named.Body, "Width", "mm", out var width) &&
                (Scalar(named.Body, "Height", "mm", out var depth) || Scalar(named.Body, "Depth", "mm", out depth)))
                baseSpec = new AuthoredSheetBaseSpec(named.Name, width, depth) { Origin = Point3(named.Body, "Origin") ?? Point3D.Origin };
        }
        if (baseSpec is null || baseSpec.Width <= 0 || baseSpec.Depth <= 0)
            return Fail("A positive rectangular base is required (`Base: Rectangle(w, d)` or named `Base <Name> { Profile: Rectangle { Width; Height; } }`).");

        var flanges = new List<AuthoredSheetFlangeSpec>();
        foreach (var block in Blocks(source, "Flange"))
        {
            var from = Regex.Match(block.Body, @"\bFrom\s*:\s*(?<region>[A-Za-z_][A-Za-z0-9_]*)\.(?<edge>[A-Za-z_][A-Za-z0-9_]*)\s*;", Rx);
            var hasLength = Scalar(block.Body, "Length", "mm", out var length) || Scalar(block.Body, "Height", "mm", out length);
            var hasRadius = Scalar(block.Body, "InsideRadius", "mm", out var radius) || Scalar(block.Body, "Radius", "mm", out radius);
            if (!from.Success || !hasLength || !Scalar(block.Body, "Angle", "deg", out var degrees) || !hasRadius)
                return Fail($"Flange '{block.Name}' requires From, Length/Height, Angle, and InsideRadius/Radius.");
            var parentName=from.Groups["region"].Value;var authoredMember=from.Groups["edge"].Value;
            if(parentName.Equals(baseSpec.Name,StringComparison.OrdinalIgnoreCase)&&authoredMember.Equals("Center",StringComparison.OrdinalIgnoreCase))
                return Fail($"`{parentName}.Center` has capability PointCapable; `Flange.From` requires FlangeAttachable SheetEdge. Available public edge members: Front, Right, Rear, Left.","sheetmetal-incompatible-edge-capability");
            var available=parentName.Equals(baseSpec.Name,StringComparison.OrdinalIgnoreCase)?SheetMetalConceptPaths.AvailableMembers("SheetRegion.Rectangle"):SheetMetalConceptPaths.AvailableMembers("SheetFlange");
            var allowed=parentName.Equals(baseSpec.Name,StringComparison.OrdinalIgnoreCase)
                ? new[]{"Front","Right","Rear","Left"}.Contains(authoredMember,StringComparer.OrdinalIgnoreCase)
                : new[]{"Outer","Top"}.Contains(authoredMember,StringComparer.OrdinalIgnoreCase);
            if(!allowed)return Fail($"`{parentName}.{authoredMember}` is not a FlangeAttachable public member. Available public members: {string.Join(", ",available)}.","sheetmetal-concept-member-not-exposed");
            var direction = Token(block.Body, "Direction")?.Equals("Down", StringComparison.OrdinalIgnoreCase) == true ? SheetBendDirection.Down : SheetBendDirection.Up;
            var corner = Token(block.Body, "Corner")?.ToLowerInvariant() switch
            {
                "miter" or "mitered" => SheetCornerPolicy.Mitered,
                "relief" => SheetCornerPolicy.Relief,
                _ => SheetCornerPolicy.Open
            };
            var reliefToken = Token(block.Body, "Relief")?.ToLowerInvariant();
            var relief = reliefToken switch { "auto" => SheetReliefPolicy.Auto, "rectangular" or "rectangle" => SheetReliefPolicy.Rectangular, "round" => SheetReliefPolicy.Round, _ => SheetReliefPolicy.None };
            Scalar(block.Body, "ReliefWidth", "mm", out var reliefWidth);
            Scalar(block.Body, "ReliefDepth", "mm", out var reliefDepth);
            var hasSpan=Scalar(block.Body, "Span", "mm", out var spanLength);
            var hasSpanOffset=Scalar(block.Body, "SpanOffset", "mm", out var spanOffset);
            if(radius<0||degrees is <=0 or >=180)return Fail($"Flange '{block.Name}' has an invalid radius or bend angle.");
            if(length<=radius+thickness)return Fail($"Flange '{block.Name}' length {length:G6} mm is not greater than inside radius + thickness ({radius+thickness:G6} mm). Increase `{block.Name}` Height/Length above {radius+thickness:G6} mm.",legacyBase.Success?"sheetmetal-firmament-invalid":SheetMetalDiagnosticCodes.FlangeBelowMinimum);
            if(hasSpan&&spanLength<=0)return Fail($"Flange '{block.Name}' Span must be positive when supplied.","sheetmetal-flange-span-invalid");
            if(hasSpanOffset&&!hasSpan)return Fail($"Flange '{block.Name}' SpanOffset requires Span.","sheetmetal-flange-span-invalid");
            flanges.Add(new(block.Name, parentName, NormalizeEdge(authoredMember), length,
                degrees * Math.PI / 180d, radius, direction, corner, relief,
                reliefWidth > 0 ? reliefWidth : null, reliefDepth > 0 ? reliefDepth : null,
                spanLength > 0 ? spanLength : null, spanOffset));
        }
        if (flanges.Count == 0) return Fail("At least one Flange is required.");
        var duplicate = flanges.GroupBy(f => (f.ParentRegion.ToLowerInvariant(), f.EdgeName.ToLowerInvariant())).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            return Fail($"Multiple flanges target {duplicate.First().ParentRegion}.{duplicate.First().EdgeName}.", SheetMetalDiagnosticCodes.DuplicateFlange);
        if (flanges.Select(f => f.Name).Append(baseSpec.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != flanges.Count + 1)
            return Fail("Base and flange names must be unique.", SheetMetalDiagnosticCodes.ImpossibleTopology);
        foreach(Match extension in Regex.Matches(source,@"\bExtend\s+SheetMetal\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{",Rx))
            if(!extension.Groups["name"].Value.Equals(header.Groups["name"].Value,StringComparison.Ordinal))
                return Fail($"Sheet Metal extension targets '{extension.Groups["name"].Value}', but the specialized part is '{header.Groups["name"].Value}'.","sheetmetal-extension-target-mismatch");

        var cuts = new List<AuthoredSheetCutSpec>();
        foreach (var block in Blocks(source, "Hole"))
        {
            var on = Token(block.Body, "On");
            var at = Point(block.Body, "Center");
            if (on is null || at is null || !Scalar(block.Body, "Diameter", "mm", out var diameter) || diameter <= 0)
                return Fail($"Hole '{block.Name}' requires On, Center, and positive Diameter.");
            cuts.Add(new(block.Name, on, SheetFeatureKind.CircularHole, at.Value.X, at.Value.Y, diameter, null, null));
        }
        foreach (var block in Blocks(source, "Cut"))
        {
            var on = Token(block.Body, "On");
            var at = Point(block.Body, "At") ?? Point(block.Body, "Center");
            var circle = Regex.IsMatch(block.Body, @"\bProfile\s*:\s*Circle\b", Rx);
            var slot = Regex.IsMatch(block.Body, @"\bProfile\s*:\s*(?:Slot|Rectangle)\b", Rx);
            if (on is null || at is null || (!circle && !slot)) return Fail($"Cut '{block.Name}' requires On, At, and a Circle/Slot/Rectangle profile.");
            if (circle)
            {
                if (!Scalar(block.Body, "Diameter", "mm", out var diameter) || diameter <= 0) return Fail($"Cut '{block.Name}' requires positive Diameter.");
                cuts.Add(new(block.Name, on, SheetFeatureKind.CircularHole, at.Value.X, at.Value.Y, diameter, null, null));
            }
            else
            {
                if (!Scalar(block.Body, "Width", "mm", out var cutWidth) || !Scalar(block.Body, "Length", "mm", out var cutLength) || cutWidth <= 0 || cutLength <= 0)
                    return Fail($"Cut '{block.Name}' requires positive Width and Length.");
                cuts.Add(new(block.Name, on, SheetFeatureKind.Slot, at.Value.X, at.Value.Y, null, cutWidth, cutLength));
            }
        }

        var semanticStarted=Stopwatch.GetTimestamp();
        var semanticLayout=SheetMetalSemanticLayoutParser.Parse(source,baseSpec,flanges,cuts);
        var semanticMs=Stopwatch.GetElapsedTime(semanticStarted).TotalMilliseconds;
        if(!semanticLayout.IsSuccess)return new(false,null,null,null,semanticLayout.Diagnostics);
        cuts.AddRange(semanticLayout.GeneratedCuts);
        var duplicateCut=cuts.GroupBy(x=>x.Name,StringComparer.Ordinal).FirstOrDefault(x=>x.Count()>1);
        if(duplicateCut is not null)return Fail($"Semantic feature path '{duplicateCut.Key}' is declared more than once.","sheetmetal-semantic-duplicate-feature");

        var authority = Regex.IsMatch(source, @"\bIntent\s*:\s*Reconstructed\s*;", Rx)
            ? SheetMetalProvenanceCategory.Reconstructed : SheetMetalProvenanceCategory.Authored;
        var concept=header.Groups["concept"].Success?header.Groups["concept"].Value:null;
        var spec = new SheetMetalConstructionSpec(header.Groups["name"].Value, thickness, material, k, baseSpec, flanges, cuts, authority, legacyBase.Success,concept)
        { SemanticLayout=semanticLayout.Layout };
        if(concept is not null&&SheetMetalConceptContracts.Validate(source,spec) is { } conformanceFailure)
            return Fail(conformanceFailure.Message,conformanceFailure.Code);
        var parseMs=Stopwatch.GetElapsedTime(parseStarted).TotalMilliseconds;var formedStarted=Stopwatch.GetTimestamp();var lowered = AuthoredSheetMetalLowering.Lower(spec, sourcePath);var formedMs=Stopwatch.GetElapsedTime(formedStarted).TotalMilliseconds;
        if (!lowered.IsSuccess || lowered.Part is null) return new(false, spec, null, null, lowered.Diagnostics);
        var flatStarted=Stopwatch.GetTimestamp();var flat = SheetMetalFlattener.Flatten(lowered.Part);var flatMs=Stopwatch.GetElapsedTime(flatStarted).TotalMilliseconds;
        diagnostics.AddRange(lowered.Diagnostics); diagnostics.AddRange(flat.Diagnostics);
        return new(flat.Status is not FlatPatternStatus.Unsupported and not FlatPatternStatus.Overlapping, spec, lowered.Part, flat, diagnostics,new(parseMs,formedMs,flatMs,semanticMs));

        SheetMetalAuthoringResult Fail(string message, string code = "sheetmetal-firmament-invalid") =>
            new(false, null, null, null, [new(code, SheetMetalDiagnosticSeverity.Error, message)]);
    }

    private sealed record Block(string Name, string Body);
    private static IReadOnlyList<Block> Blocks(string source, string keyword)
    {
        var result = new List<Block>();
        var matches = Regex.Matches(source, $@"\b{Regex.Escape(keyword)}\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{{", Rx);
        foreach (Match match in matches)
        {
            var open = source.IndexOf('{', match.Index); var depth = 0; var close = -1;
            for (var i = open; i < source.Length; i++) { if (source[i] == '{') depth++; else if (source[i] == '}' && --depth == 0) { close = i; break; } }
            if (close > open) result.Add(new(match.Groups["name"].Value, source[(open + 1)..close]));
        }
        return result;
    }
    private static bool Scalar(string text, string name, string? unit, out double value)
    {
        var suffix = unit is null ? "" : @"\s*" + Regex.Escape(unit);
        var m = Regex.Match(text, $@"\b{Regex.Escape(name)}\s*:\s*(?<v>{Number}){suffix}\s*;", Rx);
        value = m.Success ? Num(m, "v") : 0; return m.Success;
    }
    private static (double X, double Y)? Point(string text, string name)
    {
        var m = Regex.Match(text, $@"\b{Regex.Escape(name)}\s*:\s*\(\s*(?<x>{Number})\s*mm\s*,\s*(?<y>{Number})\s*mm\s*\)\s*;", Rx);
        return m.Success ? (Num(m, "x"), Num(m, "y")) : null;
    }
    private static Point3D? Point3(string text,string name)
    {
        var m=Regex.Match(text,$@"\b{Regex.Escape(name)}\s*:\s*\(\s*(?<x>{Number})\s*mm\s*,\s*(?<y>{Number})\s*mm\s*,\s*(?<z>{Number})\s*mm\s*\)\s*;",Rx);
        return m.Success?new Point3D(Num(m,"x"),Num(m,"y"),Num(m,"z")):null;
    }
    private static string? Token(string text, string name) { var m = Regex.Match(text, $@"\b{Regex.Escape(name)}\s*:\s*(?<v>[A-Za-z_][A-Za-z0-9_]*)\s*;", Rx); return m.Success ? m.Groups["v"].Value : null; }
    private static string? Quoted(string text, string name) { var m = Regex.Match(text, $"\\b{Regex.Escape(name)}\\s*:\\s*\"(?<v>[^\"]+)\"\\s*;", Rx); return m.Success ? m.Groups["v"].Value : null; }
    private static double Num(Match m, string group) => double.Parse(m.Groups[group].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static string NormalizeEdge(string edge) => edge.Equals("Top", StringComparison.OrdinalIgnoreCase) ? "Outer" : edge;
}

internal sealed record AuthoredSheetLoweringResult(bool IsSuccess, SheetMetalPartIr? Part, IReadOnlyList<SheetMetalDiagnostic> Diagnostics);

internal static class AuthoredSheetMetalLowering
{
    private sealed record EdgeFrame(Point3D A, Point3D B, Vector3D Outward);
    private sealed record RegionFrame(string Name, Point3D A, Point3D B, Vector3D Outward, Vector3D Normal, double Length)
    {
        public Point3D OuterA => A + Outward * Length;
        public Point3D OuterB => B + Outward * Length;
    }
    private sealed record Patch(string Id, SheetRegionKind Kind, IReadOnlyList<Point3D> Positive, IReadOnlyList<Point3D> Negative,
        SurfaceGeometry PositiveSurface, SurfaceGeometry NegativeSurface, Point3D? CylinderCenter = null, Vector3D? CylinderAxis = null);

    public static AuthoredSheetLoweringResult Lower(SheetMetalConstructionSpec spec, string sourcePath)
    {
        var diagnostics = new List<SheetMetalDiagnostic>();
        var source = new SheetSourceBinding("Firmament declaration", "sole construction authority", [], [], sourcePath);
        var evidence = new[] { new SheetEvidence(SheetEvidenceKind.Authored, "source-independent-construction", "All dimensions, adjacency, cuts, corners, and bend policy come from this Firmament source.") };
        var regions = new List<SheetRegionIr>(); var bends = new List<SheetBendIr>(); var features = new List<SheetFeatureIr>();
        var corners = new List<SheetMetalCornerIr>(); var reliefs = new List<SheetMetalReliefIr>(); var correspondence = new List<SheetMetalCorrespondence>();
        var patches = new List<Patch>(); var frames = new Dictionary<string, RegionFrame>(StringComparer.OrdinalIgnoreCase);
        var stableRegions = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) { [spec.Base.Name] = spec.LegacySyntax ? "region-base" : spec.Base.Name };
        foreach (var flange in spec.Flanges) stableRegions[flange.Name] = spec.LegacySyntax ? $"region-{flange.EdgeName.ToLowerInvariant()}-flange" : flange.Name;
        var t = spec.Thickness; var z = new Vector3D(0, 0, 1); var x = new Vector3D(1, 0, 0); var y = new Vector3D(0, 1, 0);
        var w = spec.Base.Width; var d = spec.Base.Depth; var mid = t / 2d;var origin=spec.Base.Origin;
        var baseEdges = new Dictionary<string, EdgeFrame>(StringComparer.OrdinalIgnoreCase)
        {
            ["Front"] = new(origin+new Vector3D(0,0,mid),origin+new Vector3D(w,0,mid),-y), ["Right"] = new(origin+new Vector3D(w,0,mid),origin+new Vector3D(w,d,mid),x),
            ["Rear"] = new(origin+new Vector3D(w,d,mid),origin+new Vector3D(0,d,mid),y), ["Left"] = new(origin+new Vector3D(0,d,mid),origin+new Vector3D(0,0,mid),-x)
        };

        var baseBoundary = new List<Point3D>();
        foreach (var edgeName in new[] { "Front", "Right", "Rear", "Left" })
        {
            var edge = baseEdges[edgeName]; var flange = spec.Flanges.FirstOrDefault(f => f.ParentRegion.Equals(spec.Base.Name, StringComparison.OrdinalIgnoreCase) && f.EdgeName.Equals(edgeName, StringComparison.OrdinalIgnoreCase));
            var previous = Previous(edgeName); var next = Next(edgeName);
            var atStart = spec.Flanges.Any(f => f.ParentRegion.Equals(spec.Base.Name, StringComparison.OrdinalIgnoreCase) && f.EdgeName.Equals(previous, StringComparison.OrdinalIgnoreCase));
            var atEnd = spec.Flanges.Any(f => f.ParentRegion.Equals(spec.Base.Name, StringComparison.OrdinalIgnoreCase) && f.EdgeName.Equals(next, StringComparison.OrdinalIgnoreCase));
            var edgeLength = (edge.B - edge.A).Length;
            var gapStart = flange is null || !atStart ? 0 : CornerGap(flange, spec.Flanges.First(f => f.ParentRegion.Equals(spec.Base.Name, StringComparison.OrdinalIgnoreCase) && f.EdgeName.Equals(previous, StringComparison.OrdinalIgnoreCase)), t);
            var gapEnd = flange is null || !atEnd ? 0 : CornerGap(flange, spec.Flanges.First(f => f.ParentRegion.Equals(spec.Base.Name, StringComparison.OrdinalIgnoreCase) && f.EdgeName.Equals(next, StringComparison.OrdinalIgnoreCase)), t);
            if(flange is not null)
            {
                // A child-region Profile corner owns its end outline explicitly.  The
                // generic manufacturing relief policy remains available at the shared
                // base corner, but must not silently shorten this authored flange edge.
                if((spec.SemanticLayout.Corners??[]).Any(c=>c.Region.Equals(flange.Name,StringComparison.OrdinalIgnoreCase)&&(c.Corner.Equals("OuterEnd",StringComparison.OrdinalIgnoreCase)||c.Corner.Equals("RootStart",StringComparison.OrdinalIgnoreCase))))gapStart=0;
                if((spec.SemanticLayout.Corners??[]).Any(c=>c.Region.Equals(flange.Name,StringComparison.OrdinalIgnoreCase)&&(c.Corner.Equals("OuterStart",StringComparison.OrdinalIgnoreCase)||c.Corner.Equals("RootEnd",StringComparison.OrdinalIgnoreCase))))gapEnd=0;
            }
            if (gapStart + gapEnd >= edgeLength - 1e-8) return Failure($"Corner trims consume base edge {spec.Base.Name}.{edgeName}.", SheetMetalDiagnosticCodes.ImpossibleTopology);
            var axis = Unit(edge.B - edge.A); var a = edge.A + axis * gapStart; var b = edge.B - axis * gapEnd;
            if (baseBoundary.Count == 0 || !Same(baseBoundary[^1], edge.A)) baseBoundary.Add(edge.A);
            if (!Same(a, edge.A)) baseBoundary.Add(a); if (!Same(b, a)) baseBoundary.Add(b); if (!Same(edge.B, b)) baseBoundary.Add(edge.B);
            if (flange is not null) baseEdges[edgeName] = new(a, b, edge.Outward);
        }
        baseBoundary = CleanLoop(baseBoundary);
        var basePlane = new SheetPlaneReference(origin+new Vector3D(0,0,mid), z, x, y, true);
        var baseId=stableRegions[spec.Base.Name];
        regions.Add(new(baseId, SheetRegionKind.Planar, Developable("Authored planar base."), basePlane, null, baseBoundary, w * d, source, evidence));
        patches.Add(PlanePatch(baseId, SheetRegionKind.Planar, baseBoundary, z, t));
        correspondence.Add(new(spec.Base.Name, "Region", baseId, $"flat-{baseId}"));

        var pending = new List<AuthoredSheetFlangeSpec>(spec.Flanges); var guard = 0;
        while (pending.Count > 0 && guard++ <= spec.Flanges.Count)
        {
            var progressed = false;
            foreach (var flange in pending.ToArray())
            {
                EdgeFrame? parentEdge = null; Vector3D parentNormal;
                if (flange.ParentRegion.Equals(spec.Base.Name, StringComparison.OrdinalIgnoreCase) && baseEdges.TryGetValue(flange.EdgeName, out var be)) { parentEdge = be; parentNormal = z; }
                else if (frames.TryGetValue(flange.ParentRegion, out var pf) && flange.EdgeName.Equals("Outer", StringComparison.OrdinalIgnoreCase)) { parentEdge = new(pf.OuterA, pf.OuterB, pf.Outward); parentNormal = pf.Normal; }
                else continue;
                progressed = true; pending.Remove(flange);
                var edge = parentEdge; var axis = Unit(edge.B - edge.A);
                if(flange.SpanLength is { } span)
                {
                    var available=(edge.B-edge.A).Length;if(span>available+1e-8||Math.Abs(flange.SpanOffset)>(available-span)/2d+1e-8)
                        return Failure($"Flange '{flange.Name}' Span {span:G9} mm with offset {flange.SpanOffset:G9} mm does not fit parent edge length {available:G9} mm.","sheetmetal-flange-span-invalid");
                    var spanCenter=Mid(edge.A,edge.B)+axis*flange.SpanOffset;edge=new(spanCenter-axis*(span/2d),spanCenter+axis*(span/2d),edge.Outward);
                }
                var sign = flange.Direction == SheetBendDirection.Down ? -1d : 1d;
                var rMid = flange.InsideRadius + t / 2d; var angle = flange.AngleRadians;
                var tangentA = edge.A + edge.Outward * (rMid * Math.Sin(angle)) + parentNormal * (sign * rMid * (1 - Math.Cos(angle)));
                var tangentB = edge.B + edge.Outward * (rMid * Math.Sin(angle)) + parentNormal * (sign * rMid * (1 - Math.Cos(angle)));
                var direction = Unit(edge.Outward * Math.Cos(angle) + parentNormal * (sign * Math.Sin(angle)));
                var normal = Unit(parentNormal * Math.Cos(angle) - edge.Outward * (sign * Math.Sin(angle)));
                var frame = new RegionFrame(flange.Name, tangentA, tangentB, direction, normal, flange.Length); frames[flange.Name] = frame;
                var tabs=spec.SemanticLayout.Tabs.Where(x=>x.Region.Equals(flange.Name,StringComparison.OrdinalIgnoreCase)&&x.Edge.Equals("Outer",StringComparison.OrdinalIgnoreCase)).ToArray();
                var steppedNotches=(spec.SemanticLayout.SteppedNotches??[]).Where(x=>x.Region.Equals(flange.Name,StringComparison.OrdinalIgnoreCase)&&x.Edge.Equals("Outer",StringComparison.OrdinalIgnoreCase)).ToArray();
                var cornerPrograms=(spec.SemanticLayout.Corners??[]).Where(x=>x.Region.Equals(flange.Name,StringComparison.OrdinalIgnoreCase)).ToArray();
                var rootStartProgram=cornerPrograms.SingleOrDefault(x=>x.Corner.Equals("RootStart",StringComparison.OrdinalIgnoreCase));
                var rootEndProgram=cornerPrograms.SingleOrDefault(x=>x.Corner.Equals("RootEnd",StringComparison.OrdinalIgnoreCase));
                var outerStartProgram=cornerPrograms.SingleOrDefault(x=>x.Corner.Equals("OuterStart",StringComparison.OrdinalIgnoreCase));
                var outerEndProgram=cornerPrograms.SingleOrDefault(x=>x.Corner.Equals("OuterEnd",StringComparison.OrdinalIgnoreCase));
                ResolvedSemanticCornerProfileIr? ResolveCorner(SheetMetalSemanticCornerProfile? program)
                {
                    if(program is null)return null;
                    var length=(edge.B-edge.A).Length;var height=flange.Length;
                    SemanticCornerOperationIr operation=program.Operation.ToUpperInvariant() switch
                    {
                        "CHAMFER"=>new SemanticCornerChamferIr(program.OperationPath.Split('.').Last(),program.OperationPath,program.SetbackA,program.SetbackB,"SheetMetalSemanticLayout"),
                        "CUTBACK"=>new SemanticCornerCutbackIr(program.OperationPath.Split('.').Last(),program.OperationPath,program.SetbackA,program.SetbackB,"SheetMetalSemanticLayout"),
                        "TAPER"=>new SemanticCornerTaperIr(program.OperationPath.Split('.').Last(),program.OperationPath,program.SetbackA,program.SetbackB,"SheetMetalSemanticLayout"),
                        "NOTCHCORNER"=>new SemanticCornerNotchIr(program.OperationPath.Split('.').Last(),program.OperationPath,program.SetbackA,program.SetbackB,"SheetMetalSemanticLayout"),
                        _=>throw new InvalidOperationException()
                    };
                    var request=program.Corner.ToUpperInvariant() switch
                    {
                        "ROOTEND"=>new SemanticCornerProfileIr(program.CornerPath,program.CornerPath,$"{flange.Name}.Root",$"{flange.Name}.EndB",new(0,0),new(length,0),new(length,height),operation,$"{program.CornerPath}[u,v]","Sheet Metal Profile corner"),
                        "OUTERSTART"=>new SemanticCornerProfileIr(program.CornerPath,program.CornerPath,$"{flange.Name}.EndB",$"{flange.Name}.Outer",new(length,0),new(length,height),new(0,height),operation,$"{program.CornerPath}[u,v]","Sheet Metal Profile corner"),
                        "OUTEREND"=>new SemanticCornerProfileIr(program.CornerPath,program.CornerPath,$"{flange.Name}.Outer",$"{flange.Name}.EndA",new(length,height),new(0,height),new(0,0),operation,$"{program.CornerPath}[u,v]","Sheet Metal Profile corner"),
                        "ROOTSTART"=>new SemanticCornerProfileIr(program.CornerPath,program.CornerPath,$"{flange.Name}.EndA",$"{flange.Name}.Root",new(0,height),new(0,0),new(length,0),operation,$"{program.CornerPath}[u,v]","Sheet Metal Profile corner"),
                        _=>throw new InvalidOperationException()
                    };
                    var resolved=SemanticCornerProfileResolver.Resolve(request);
                    if(!resolved.IsSuccess)throw new InvalidOperationException($"CornerProfile '{program.CornerPath}' failed: {string.Join("; ",resolved.Diagnostics)}");
                    return resolved.Corner;
                }
                ResolvedSemanticCornerProfileIr? rootStartCorner;ResolvedSemanticCornerProfileIr? rootEndCorner;ResolvedSemanticCornerProfileIr? outerStartCorner;ResolvedSemanticCornerProfileIr? outerEndCorner;
                try{rootStartCorner=ResolveCorner(rootStartProgram);rootEndCorner=ResolveCorner(rootEndProgram);outerStartCorner=ResolveCorner(outerStartProgram);outerEndCorner=ResolveCorner(outerEndProgram);}
                catch(InvalidOperationException ex){return Failure(ex.Message,"sheetmetal-corner-profile-invalid");}
                var ownerLength=(edge.B-edge.A).Length;
                var outerAttachment=new SemanticEdgeProfileIr($"{flange.Name}.Outer",$"{flange.Name}.Outer",new(ownerLength,flange.Length),new(0,flange.Length),
                    tabs.Select(tab=>(SemanticEdgeFragmentIr)new SemanticEdgeTabIr(tab.Path.Split('.').Last(),tab.Path,
                        new(SemanticEdgeAnchorKind.CenteredAt,ownerLength-tab.Center),tab.Width,tab.Extension,-1,"SheetMetalSemanticLayout"))
                    .Concat(steppedNotches.Select(notch=>(SemanticEdgeFragmentIr)new SemanticEdgeSteppedNotchIr(notch.Path.Split('.').Last(),notch.Path,
                        new(SemanticEdgeAnchorKind.CenteredAt,ownerLength-notch.Center),notch.Width,notch.Depth,notch.ShoulderDepth,notch.OuterChamfer,notch.InnerChamfer,-notch.Side,"SheetMetalSemanticLayout"))).ToArray(),
                    "SheetRegionLocal[u=OuterB->OuterA,v=material-left]","Sheet Metal semantic edge attachment");
                SemanticEdgeProfileResolution Edge(string edgeName,SemanticProfilePoint start,SemanticProfilePoint end,IReadOnlyList<SemanticEdgeFragmentIr> fragments,ResolvedSemanticCornerProfileIr? startCorner,ResolvedSemanticCornerProfileIr? endCorner)
                    =>SemanticEdgeProfileResolver.Resolve(new($"{flange.Name}.{edgeName}",$"{flange.Name}.{edgeName}",start,end,fragments,$"{flange.Name}.{edgeName}[u,v]","Sheet Metal profile edge"),
                        new(startCorner?.EdgeBConsumption??0,endCorner?.EdgeAConsumption??0,startCorner?.Source.CornerPath,endCorner?.Source.CornerPath));
                var rootEdge=Edge("Root",new(0,0),new(ownerLength,0),[],rootStartCorner,rootEndCorner);
                var endBEdge=Edge("EndB",new(ownerLength,0),new(ownerLength,flange.Length),[],rootEndCorner,outerStartCorner);
                var composed=SemanticEdgeProfileResolver.Resolve(outerAttachment,new(outerStartCorner?.EdgeBConsumption??0,outerEndCorner?.EdgeAConsumption??0,outerStartCorner?.Source.CornerPath,outerEndCorner?.Source.CornerPath));
                var endAEdge=Edge("EndA",new(0,flange.Length),new(0,0),[],outerEndCorner,rootStartCorner);
                var edgeFailures=new[]{rootEdge,endBEdge,composed,endAEdge}.Where(x=>!x.IsSuccess).SelectMany(x=>x.Diagnostics).ToArray();
                if(edgeFailures.Length>0)return Failure($"Profile edge composition for '{flange.Name}' failed: {string.Join("; ",edgeFailures)}","sheetmetal-edge-profile-invalid");
                Point3D Map((double X,double Y) point)=>tangentA+axis*point.X+direction*point.Y;
                var points=new List<Point3D>();
                void Add(Point3D point){if(points.Count==0||(point-points[^1]).Length>1e-8)points.Add(point);}
                void AddEdge(SemanticEdgeProfileResolution profile)
                {
                    var descendants=profile.Profile!.OrderedMembers.SelectMany(member=>member.CurveDescendants).ToArray();
                    if(descendants.Length==0)return;
                    Add(Map(((LineArcLineSegment2D)descendants[0].Geometry).Start));
                    foreach(var descendant in descendants)Add(Map(((LineArcLineSegment2D)descendant.Geometry).End));
                }
                void AddCorner(ResolvedSemanticCornerProfileIr? corner)
                {
                    if(corner is null)return;
                    Add(Map((corner.EdgeAEndpoint.X,corner.EdgeAEndpoint.Y)));
                    foreach(var descendant in corner.CurveDescendants)Add(Map(((LineArcLineSegment2D)descendant.Geometry).End));
                }
                AddEdge(rootEdge);AddCorner(rootEndCorner);AddEdge(endBEdge);AddCorner(outerStartCorner);AddEdge(composed);AddCorner(outerEndCorner);AddEdge(endAEdge);AddCorner(rootStartCorner);
                if(points.Count>1&&(points[^1]-points[0]).Length<=1e-8)points.RemoveAt(points.Count-1);
                IReadOnlyList<Point3D> boundary=points;
                foreach(var fragment in composed.Profile!.OrderedMembers.Where(member=>!member.IsGeneratedCarrier))
                    correspondence.Add(new(fragment.StableId,"EdgeFragment",fragment.StableId,$"flat-{fragment.StableId}"));
                foreach(var corner in new[]{rootStartCorner,rootEndCorner,outerStartCorner,outerEndCorner}.OfType<ResolvedSemanticCornerProfileIr>())
                    correspondence.Add(new(corner.Source.CornerPath,"ProfileCorner",corner.Source.CornerPath,$"flat-{corner.Source.CornerPath}"));
                var plane = new SheetPlaneReference(tangentA, normal, axis, direction, true);
                var flangeId=stableRegions[flange.Name];
                var fragmentArea=tabs.Sum(x=>x.Width*x.Extension)-steppedNotches.Sum(x=>x.Width*x.Depth);
                regions.Add(new(flangeId, SheetRegionKind.Planar, Developable("Authored planar flange."), plane, null, boundary, (edge.B-edge.A).Length * flange.Length+fragmentArea, source, evidence));
                patches.Add(PlanePatch(flangeId, SheetRegionKind.Planar, boundary, normal, t));

                var bendId = spec.LegacySyntax ? $"bend-{flange.EdgeName.ToLowerInvariant()}" : $"{flange.Name}Bend"; var bendRegionId = spec.LegacySyntax ? $"region-{flange.EdgeName.ToLowerInvariant()}-bend" : $"{flange.Name}BendRegion"; var centerA = edge.A + parentNormal * (sign * rMid); var center = Mid(centerA, edge.B + parentNormal * (sign * rMid));
                var cylinder = new SheetCylinderReference(center, axis, rMid, flange.InsideRadius, angle, (edge.B-edge.A).Length, sign > 0);
                var bendBoundary = new[] { edge.A, edge.B, tangentB, tangentA };
                regions.Add(new(bendRegionId, SheetRegionKind.CylindricalBend, Developable("Authored analytic cylindrical bend."), null, cylinder, bendBoundary, angle * rMid * cylinder.AxisLength, source, evidence));
                bends.Add(new(bendId, center, axis, angle, flange.InsideRadius, t, flange.Direction, stableRegions[flange.ParentRegion], flangeId, SheetNeutralAxisPolicy.KFactorPolicy(spec.KFactor), source,
                    [new(SheetEvidenceKind.Authored, "bend-intent", "Axis, angle, inside radius, direction, and adjacency derive from the high-level flange declaration.")]));
                patches.Add(BendPatch(bendRegionId, edge, parentNormal, flange, t));
                correspondence.Add(new(flange.Name, "Region", flangeId, $"flat-{flangeId}"));
                correspondence.Add(new(bendId, "Bend", bendRegionId, $"flat-{bendId}"));
            }
            if (!progressed) break;
        }
        if (pending.Count > 0) return Failure($"Flange graph is disconnected or references unsupported edges: {string.Join(", ", pending.Select(f => $"{f.Name}->{f.ParentRegion}.{f.EdgeName}"))}.", SheetMetalDiagnosticCodes.DisconnectedGraph);

        AddCorners();
        foreach (var cut in spec.Cuts)
        {
            SheetPlaneReference? plane; IReadOnlyList<Point3D> boundary;
            if (cut.RegionName.Equals(spec.Base.Name, StringComparison.OrdinalIgnoreCase)) { plane = basePlane; boundary = baseBoundary; }
            else { var region = regions.FirstOrDefault(r => r.StableId.Equals(stableRegions.GetValueOrDefault(cut.RegionName,cut.RegionName), StringComparison.OrdinalIgnoreCase)); plane = region?.Plane; boundary = region?.Boundary3D ?? []; }
            if (plane is null) return Failure($"Cut '{cut.Name}' references unknown planar region '{cut.RegionName}'.", SheetMetalDiagnosticCodes.FeatureMappingFailure);
            var local=boundary.Select(p=>(U:(p-plane.Origin).Dot(plane.UAxis),V:(p-plane.Origin).Dot(plane.VAxis))).ToArray();var hu=(cut.Diameter??cut.Length)!.Value/2;var hv=(cut.Diameter??cut.Width)!.Value/2;
            if(local.Length<3||cut.X-hu<=local.Min(p=>p.U)+1e-8||cut.X+hu>=local.Max(p=>p.U)-1e-8||cut.Y-hv<=local.Min(p=>p.V)+1e-8||cut.Y+hv>=local.Max(p=>p.V)-1e-8)
                return Failure($"Cut '{cut.Name}' reaches or crosses the boundary/bend zone of region '{cut.RegionName}'.",SheetMetalDiagnosticCodes.CutCrossesBend);
            var center = plane.Origin + plane.UAxis * cut.X + plane.VAxis * cut.Y;
            IReadOnlyList<Point3D> loop = cut.Kind == SheetFeatureKind.CircularHole ? [] : RectangleLoop(center, plane.UAxis, plane.VAxis, cut.Length!.Value, cut.Width!.Value);
            features.Add(new(cut.Name, cut.Kind, stableRegions.GetValueOrDefault(cut.RegionName,cut.RegionName), center, cut.Diameter, loop, source, evidence));
            correspondence.Add(new(cut.Name, "Cut", cut.Name, cut.Name));
        }

        var bodyResult = AuthoredSheetBrepEmitter.Emit(patches, spec, regions, features);
        if (!bodyResult.IsSuccess || bodyResult.Value is null)
            return Failure("Formed BRep stitching failed: " + string.Join("; ", bodyResult.Diagnostics.Select(d => d.Message)), SheetMetalDiagnosticCodes.FormedBodyInvalid);
        var preflight = BrepExportPreflight.Validate(bodyResult.Value);
        if (!preflight.IsValid) return Failure("Formed BRep failed validation: " + string.Join("; ", preflight.Diagnostics.Select(d => d.Message)), SheetMetalDiagnosticCodes.FormedBodyInvalid);
        var status = SheetMetalRecognitionStatus.Complete;
        var authorityText = spec.Authority == SheetMetalProvenanceCategory.Reconstructed ? "Reconstructed Firmament engineering intent" : "Authored Firmament engineering intent";
        var part = new SheetMetalPartIr($"sheetmetal-{spec.Name}", t, spec.Material, baseId, regions, bends, features, new(spec.KFactor), status,
            $"{authorityText}; source-independent exact formed lowering.", [new(SheetEvidenceKind.Authored, "construction-authority", authorityText)], diagnostics, bodyResult.Value,
            corners, reliefs, correspondence, SheetFlangeLengthMode.TangentToEdge);
        return new(true, part, diagnostics);

        void AddCorners()
        {
            var edgeOrder = new[] { "Front", "Right", "Rear", "Left" };
            for (var i = 0; i < edgeOrder.Length; i++)
            {
                var a = spec.Flanges.FirstOrDefault(f => f.ParentRegion.Equals(spec.Base.Name, StringComparison.OrdinalIgnoreCase) && f.EdgeName.Equals(edgeOrder[i], StringComparison.OrdinalIgnoreCase));
                var b = spec.Flanges.FirstOrDefault(f => f.ParentRegion.Equals(spec.Base.Name, StringComparison.OrdinalIgnoreCase) && f.EdgeName.Equals(edgeOrder[(i+1)%4], StringComparison.OrdinalIgnoreCase));
                if (a is null || b is null) continue;
                var reliefOwner=a.ReliefPolicy!=SheetReliefPolicy.None?a:b.ReliefPolicy!=SheetReliefPolicy.None?b:null;
                var policy = a.CornerPolicy == SheetCornerPolicy.Mitered && b.CornerPolicy == SheetCornerPolicy.Mitered ? SheetCornerPolicy.Mitered :
                    reliefOwner?.ReliefPolicy==SheetReliefPolicy.Round?SheetCornerPolicy.RoundRelief:
                    reliefOwner is not null?SheetCornerPolicy.RectangularRelief:SheetCornerPolicy.Open;
                var id = $"corner-{a.Name}-{b.Name}"; string? reliefId = null;
                if (policy is SheetCornerPolicy.RectangularRelief or SheetCornerPolicy.RoundRelief)
                {
                    reliefId = $"relief-{a.Name}-{b.Name}"; var owner = reliefOwner!;
                    var width = owner.ReliefWidth ?? Math.Max(t, 0.5 * t); var depth = owner.ReliefDepth ?? owner.InsideRadius + t;
                    var kind = owner.ReliefPolicy == SheetReliefPolicy.Round ? SheetReliefKind.Round : SheetReliefKind.Rectangular;
                    reliefs.Add(new(reliefId, kind, spec.Base.Name, id, width, depth, kind == SheetReliefKind.Round ? width/2 : null, owner.ReliefWidth is null || owner.ReliefDepth is null, source,
                        [new(SheetEvidenceKind.Derived, "automatic-relief", "Auto relief width=max(thickness, 0.5*thickness); depth=inside radius+thickness.", width)]));
                    correspondence.Add(new(reliefId, "Relief", reliefId, reliefId));
                }
                corners.Add(new(id, a.Name, b.Name, spec.Base.Name, $"{edgeOrder[i]}-{edgeOrder[(i+1)%4]}", policy, reliefId, source, evidence));
                correspondence.Add(new(id, "Corner", id, reliefId ?? id));
            }
        }
        AuthoredSheetLoweringResult Failure(string message, string code) => new(false, null, [new(code, SheetMetalDiagnosticSeverity.Error, message)]);
    }

    private static Patch PlanePatch(string id, SheetRegionKind kind, IReadOnlyList<Point3D> mid, Vector3D normal, double thickness)
    {
        var n = Unit(normal); var positive = mid.Select(p => p + n * (thickness/2)).ToArray(); var negative = mid.Select(p => p - n * (thickness/2)).ToArray();
        var u = Unit(mid[1]-mid[0]);
        return new(id, kind, positive, negative, SurfaceGeometry.FromPlane(new PlaneSurface(positive[0], Direction3D.Create(n), Direction3D.Create(u))), SurfaceGeometry.FromPlane(new PlaneSurface(negative[0], Direction3D.Create(-n), Direction3D.Create(u))));
    }
    private static Patch BendPatch(string id, EdgeFrame edge, Vector3D parentNormal, AuthoredSheetFlangeSpec flange, double thickness)
    {
        var axis = Unit(edge.B-edge.A); var sign = flange.Direction == SheetBendDirection.Down ? -1d : 1d; var rMid = flange.InsideRadius + thickness/2; var a = flange.AngleRadians;
        Point3D Ref(Point3D q, double theta) => q + edge.Outward*(rMid*Math.Sin(theta)) + parentNormal*(sign*rMid*(1-Math.Cos(theta)));
        Vector3D Normal(double theta) => Unit(parentNormal*Math.Cos(theta)-edge.Outward*(sign*Math.Sin(theta)));
        var p0a=Ref(edge.A,0);var p0b=Ref(edge.B,0);var p1a=Ref(edge.A,a);var p1b=Ref(edge.B,a);var n0=Normal(0);var n1=Normal(a);
        var positive=new[]{p0a+n0*thickness/2,p0b+n0*thickness/2,p1b+n1*thickness/2,p1a+n1*thickness/2};
        var negative=new[]{p0a-n0*thickness/2,p0b-n0*thickness/2,p1b-n1*thickness/2,p1a-n1*thickness/2};
        var center=edge.A+parentNormal*(sign*rMid);var positiveRadius=(positive[0]-center).Length;var negativeRadius=(negative[0]-center).Length;
        var refPos=Direction3D.Create(positive[0]-center);var refNeg=Direction3D.Create(negative[0]-center);
        return new(id,SheetRegionKind.CylindricalBend,positive,negative,
            SurfaceGeometry.FromCylinder(new CylinderSurface(center,Direction3D.Create(axis),positiveRadius,refPos)),
            SurfaceGeometry.FromCylinder(new CylinderSurface(center,Direction3D.Create(axis),negativeRadius,refNeg)),center,axis);
    }
    private static DevelopabilityEvidence Developable(string note)=>new(DevelopabilityKind.Developable,"authored analytic construction",0,0,note);
    private static IReadOnlyList<Point3D> RectangleLoop(Point3D c,Vector3D u,Vector3D v,double length,double width)=>[c-u*(length/2)-v*(width/2),c+u*(length/2)-v*(width/2),c+u*(length/2)+v*(width/2),c-u*(length/2)+v*(width/2)];
    private static string Previous(string edge)=>edge switch{"Front"=>"Left","Right"=>"Front","Rear"=>"Right",_=>"Rear"};
    private static string Next(string edge)=>edge switch{"Front"=>"Right","Right"=>"Rear","Rear"=>"Left",_=>"Front"};
    private static double CornerGap(AuthoredSheetFlangeSpec a,AuthoredSheetFlangeSpec b,double t)=>a.CornerPolicy==SheetCornerPolicy.Mitered&&b.CornerPolicy==SheetCornerPolicy.Mitered?Math.Max(a.InsideRadius,b.InsideRadius)+t/2:Math.Max(Math.Max(a.InsideRadius,b.InsideRadius)+t, a.ReliefWidth??0);
    private static List<Point3D> CleanLoop(IEnumerable<Point3D> p){var r=new List<Point3D>();foreach(var q in p)if(r.Count==0||!Same(r[^1],q))r.Add(q);if(r.Count>1&&Same(r[0],r[^1]))r.RemoveAt(r.Count-1);return r;}
    private static bool Same(Point3D a,Point3D b)=>(a-b).Length<1e-8;
    private static Vector3D Unit(Vector3D v)=>v.TryNormalize(out var n)?n:throw new InvalidOperationException("Zero-length authored direction.");
    private static Point3D Mid(Point3D a,Point3D b)=>new((a.X+b.X)/2,(a.Y+b.Y)/2,(a.Z+b.Z)/2);

    private static class AuthoredSheetBrepEmitter
    {
        private readonly record struct Use(EdgeId Edge,bool Reverse);
        private sealed record EdgeGeometry(CurveGeometry Curve,ParameterInterval Interval);
        private sealed record CutBoundary(SheetFeatureIr Feature,SheetPlaneReference Plane,IReadOnlyList<Point3D> Positive,IReadOnlyList<Point3D> Negative,IReadOnlyList<EdgeId> PositiveEdges,IReadOnlyList<EdgeId> NegativeEdges,bool Circle);
        private readonly record struct Key(long X,long Y,long Z){public static Key Of(Point3D p)=>new(Q(p.X),Q(p.Y),Q(p.Z));private static long Q(double v)=>(long)Math.Round(v*1e8);}
        private readonly record struct EdgeKey(Key A,Key B){public static EdgeKey Of(Point3D a,Point3D b){var ka=Key.Of(a);var kb=Key.Of(b);return Compare(ka,kb)<=0?new(ka,kb):new(kb,ka);}private static int Compare(Key a,Key b){var x=a.X.CompareTo(b.X);if(x!=0)return x;var y=a.Y.CompareTo(b.Y);return y!=0?y:a.Z.CompareTo(b.Z);}}

        public static KernelResult<BrepBody> Emit(IReadOnlyList<Patch> patches,SheetMetalConstructionSpec spec,IReadOnlyList<SheetRegionIr> regions,IReadOnlyList<SheetFeatureIr> features)
        {
            var b=new TopologyBuilder();var g=new BrepGeometryStore();var bindings=new BrepBindingModel();var points=new Dictionary<VertexId,Point3D>();
            var vertices=new Dictionary<Key,VertexId>();var edges=new Dictionary<EdgeKey,EdgeId>();var edgeGeometry=new Dictionary<EdgeId,EdgeGeometry>();var useCount=new Dictionary<EdgeId,int>();var faces=new List<FaceId>();
            var boundaryCandidates=new List<(EdgeId Positive,EdgeId Negative,Point3D PA,Point3D PB,Point3D NA,Point3D NB)>();
            var cutsByRegion=new Dictionary<string,List<CutBoundary>>(StringComparer.Ordinal);
            VertexId Vertex(Point3D p){var key=Key.Of(p);if(vertices.TryGetValue(key,out var id))return id;id=b.AddVertex();vertices[key]=id;points[id]=p;return id;}
            EdgeId Edge(Point3D a,Point3D q,EdgeGeometry? geometry=null){var key=EdgeKey.Of(a,q);if(edges.TryGetValue(key,out var id))return id;id=b.AddEdge(Vertex(a),Vertex(q));edges[key]=id;edgeGeometry[id]=geometry??Line(a,q);return id;}
            Use DirectedEdge(Point3D a,Point3D q,EdgeGeometry? geometry=null){var id=Edge(a,q,geometry);useCount[id]=useCount.GetValueOrDefault(id)+1;var topology=b.Model.GetEdge(id);return new(id,topology.StartVertexId==Vertex(a));}
            Use ExistingEdge(EdgeId id,Point3D start){useCount[id]=useCount.GetValueOrDefault(id)+1;return new(id,b.Model.GetEdge(id).StartVertexId==Vertex(start));}
            FaceId Face(IReadOnlyList<IReadOnlyList<Use>> loops,SurfaceGeometry surface,bool same=true){var ids=new List<LoopId>();foreach(var uses in loops){var lid=b.AllocateLoopId();var co=uses.Select(_=>b.AllocateCoedgeId()).ToArray();for(var i=0;i<co.Length;i++)b.AddCoedge(new(co[i],uses[i].Edge,lid,co[(i+1)%co.Length],co[(i+co.Length-1)%co.Length],!uses[i].Reverse));b.AddLoop(new Loop(lid,co));ids.Add(lid);}var face=b.AddFace(ids);var sid=new SurfaceGeometryId(g.Surfaces.Count()+1);g.AddSurface(sid,surface);bindings.AddFaceBinding(new(face,sid,same));faces.Add(face);return face;}

            BuildCutBoundaries();
            foreach(var patch in patches)
            {
                AddSkin(patch,true,patch.Positive,patch.PositiveSurface);AddSkin(patch,false,patch.Negative.Reverse().ToArray(),patch.NegativeSurface);
                var count=patch.Positive.Count;for(var i=0;i<count;i++)
                {
                    var j=(i+1)%count;var pg=CurveFor(patch,true,i,patch.Positive[i],patch.Positive[j]);var ng=CurveFor(patch,false,i,patch.Negative[i],patch.Negative[j]);
                    var pe=Edge(patch.Positive[i],patch.Positive[j],pg);var ne=Edge(patch.Negative[i],patch.Negative[j],ng);
                    boundaryCandidates.Add((pe,ne,patch.Positive[i],patch.Positive[j],patch.Negative[i],patch.Negative[j]));
                }
            }
            foreach(var candidate in boundaryCandidates)
            {
                if(useCount.GetValueOrDefault(candidate.Positive)!=1||useCount.GetValueOrDefault(candidate.Negative)!=1)continue;
                var loop=new[]{DirectedEdge(candidate.PA,candidate.PB),DirectedEdge(candidate.PB,candidate.NB),DirectedEdge(candidate.NB,candidate.NA),DirectedEdge(candidate.NA,candidate.PA)};
                var u=candidate.PB-candidate.PA;var v=candidate.NA-candidate.PA;var normal=Unit(u.Cross(v));Face([loop],SurfaceGeometry.FromPlane(new PlaneSurface(candidate.PA,Direction3D.Create(normal),Direction3D.Create(Unit(u)))));
            }
            AddCutWalls();
            foreach(var (edge,geometry) in edgeGeometry.OrderBy(x=>x.Key.Value)){var cid=new CurveGeometryId(g.Curves.Count()+1);g.AddCurve(cid,geometry.Curve);bindings.AddEdgeBinding(new(edge,cid,geometry.Interval));}
            var shell=b.AddShell(faces);b.AddBody([shell]);var body=new BrepBody(b.Model,g,bindings,points);var validation=BrepExportPreflight.Validate(body);
            if(!validation.IsValid)return KernelResult<BrepBody>.Failure(validation.Diagnostics.Where(x=>x.Severity==BrepExportPreflightSeverity.Error).Select(x=>new Aetheris.Kernel.Core.Diagnostics.KernelDiagnostic(Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed,Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error,$"{x.Code}: {x.Message}",x.Context)).ToArray());
            return KernelResult<BrepBody>.Success(body);

            void AddSkin(Patch patch,bool positive,IReadOnlyList<Point3D> loop,SurfaceGeometry surface)
            {
                var uses=new List<Use>();for(var i=0;i<loop.Count;i++){var j=(i+1)%loop.Count;var originalIndex=positive?i:(patch.Negative.Count-2-i+patch.Negative.Count)%patch.Negative.Count;uses.Add(DirectedEdge(loop[i],loop[j],CurveFor(patch,positive,originalIndex,loop[i],loop[j])));}
                var loops=new List<IReadOnlyList<Use>>{uses};
                if(cutsByRegion.TryGetValue(patch.Id,out var patchCuts))foreach(var cut in patchCuts)
                {
                    var points=positive?cut.Positive.Reverse().ToArray():cut.Negative;var cutEdges=positive?cut.PositiveEdges:cut.NegativeEdges;
                    if(cut.Circle)loops.Add([ExistingEdge(cutEdges[0],points[0])]);
                    else{var cutUses=new List<Use>();for(var i=0;i<points.Count;i++){var original=positive?(points.Count-2-i+points.Count)%points.Count:i;cutUses.Add(ExistingEdge(cutEdges[original],points[i]));}loops.Add(cutUses);}
                }
                Face(loops,surface,positive);
            }
            EdgeGeometry CurveFor(Patch patch,bool positive,int index,Point3D a,Point3D q)
            {
                if(patch.Kind!=SheetRegionKind.CylindricalBend||index is 0 or 2)return Line(a,q);
                var center=patch.CylinderCenter!.Value;var axis=Unit(patch.CylinderAxis!.Value);var endpointCenter=ProjectToAxis(a,center,axis);var radius=(a-endpointCenter).Length;var va=a-endpointCenter;var vq=q-ProjectToAxis(q,center,axis);var signed=Math.Atan2(axis.Dot(Unit(va).Cross(Unit(vq))),Unit(va).Dot(Unit(vq)));if(signed<0){axis=-axis;signed=-signed;}var circle=new Circle3Curve(endpointCenter,Direction3D.Create(axis),radius,Direction3D.Create(va));return new(CurveGeometry.FromCircle(circle),new(0,Math.Max(signed,1e-12)));
            }
            void BuildCutBoundaries()
            {
                foreach(var feature in features)
                {
                    var region=regions.First(r=>r.StableId==feature.OwningRegionId);var plane=region.Plane!;var n=Unit(plane.Normal);var posCenter=feature.Center+n*spec.Thickness/2;var negCenter=feature.Center-n*spec.Thickness/2;
                    IReadOnlyList<Point3D> positive;IReadOnlyList<Point3D> negative;IReadOnlyList<EdgeId> positiveEdges;IReadOnlyList<EdgeId> negativeEdges;var circle=false;
                    if(feature.Kind==SheetFeatureKind.CircularHole&&feature.Diameter is { } diameter)
                    {
                        circle=true;var radius=diameter/2;var p0=posCenter+plane.UAxis*radius;var n0=negCenter+plane.UAxis*radius;var pv=Vertex(p0);var nv=Vertex(n0);var pe=b.AddEdge(pv,pv);var ne=b.AddEdge(nv,nv);var pc=new Circle3Curve(posCenter,Direction3D.Create(n),radius,Direction3D.Create(plane.UAxis));var nc=new Circle3Curve(negCenter,Direction3D.Create(n),radius,Direction3D.Create(plane.UAxis));edgeGeometry[pe]=new(CurveGeometry.FromCircle(pc),new(0,2*Math.PI));edgeGeometry[ne]=new(CurveGeometry.FromCircle(nc),new(0,2*Math.PI));positive=[p0];negative=[n0];positiveEdges=[pe];negativeEdges=[ne];
                    }
                    else
                    {
                        positive=feature.Boundary3D.Select(p=>p+n*spec.Thickness/2).ToArray();negative=feature.Boundary3D.Select(p=>p-n*spec.Thickness/2).ToArray();
                        positiveEdges=Enumerable.Range(0,positive.Count).Select(i=>Edge(positive[i],positive[(i+1)%positive.Count])).ToArray();negativeEdges=Enumerable.Range(0,negative.Count).Select(i=>Edge(negative[i],negative[(i+1)%negative.Count])).ToArray();
                    }
                    var cut=new CutBoundary(feature,plane,positive,negative,positiveEdges,negativeEdges,circle);if(!cutsByRegion.TryGetValue(feature.OwningRegionId,out var list))cutsByRegion[feature.OwningRegionId]=list=[];list.Add(cut);
                }
            }
            void AddCutWalls()
            {
                foreach(var cut in cutsByRegion.Values.SelectMany(x=>x))
                {
                    if(cut.Circle)
                    {
                        var radius=cut.Feature.Diameter!.Value/2;var loops=new IReadOnlyList<Use>[] { [ExistingEdge(cut.PositiveEdges[0],cut.Positive[0])], [ExistingEdge(cut.NegativeEdges[0],cut.Negative[0])] };
                        Face(loops,SurfaceGeometry.FromCylinder(new CylinderSurface(cut.Feature.Center-cut.Plane.Normal*(spec.Thickness/2),Direction3D.Create(Unit(cut.Plane.Normal)),radius,Direction3D.Create(Unit(cut.Plane.UAxis)))),false);
                    }
                    else for(var i=0;i<cut.Positive.Count;i++)
                    {
                        var j=(i+1)%cut.Positive.Count;var loop=new[]{ExistingEdge(cut.PositiveEdges[i],cut.Positive[i]),DirectedEdge(cut.Positive[j],cut.Negative[j]),ExistingEdge(cut.NegativeEdges[i],cut.Negative[j]),DirectedEdge(cut.Negative[i],cut.Positive[i])};var u=cut.Positive[j]-cut.Positive[i];var v=cut.Negative[i]-cut.Positive[i];var normal=Unit(u.Cross(v));Face([loop],SurfaceGeometry.FromPlane(new PlaneSurface(cut.Positive[i],Direction3D.Create(normal),Direction3D.Create(Unit(u)))));
                    }
                }
            }
            EdgeGeometry Line(Point3D a,Point3D q){var d=q-a;return new(CurveGeometry.FromLine(new Line3Curve(a,Direction3D.Create(d))),new(0,d.Length));}
            Point3D ProjectToAxis(Point3D p,Point3D origin,Vector3D axis)=>origin+axis*((p-origin).Dot(axis));
        }
    }
}
