using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.SheetMetal;

public sealed record SheetMetalManufacturingDatum(string Label, string Target);

public sealed record SheetMetalManufacturingDimension(
    string Name,
    string Kind,
    string Target,
    double Value,
    double TolerancePlus,
    double ToleranceMinus,
    int? Quantity);

public sealed record SheetMetalManufacturingGeometricTolerance(
    string Name,
    string Kind,
    string Target,
    double Value,
    IReadOnlyList<string> DatumReferences,
    int? Quantity);

public sealed record SheetMetalManufacturingAnnotation(string Name, string Target, string Text);

public sealed record SheetMetalManufacturingDefinition(
    string ReleaseName,
    string? MaterialSpecification,
    double? ThicknessTolerancePlus,
    double? ThicknessToleranceMinus,
    string? BlankProcess,
    string? FormingProcess,
    double? GeneralCutTolerance,
    double? GeneralFormedTolerance,
    double? GeneralAngularToleranceDegrees,
    IReadOnlyList<SheetMetalManufacturingDatum> Datums,
    IReadOnlyList<SheetMetalManufacturingDimension> Dimensions,
    IReadOnlyList<SheetMetalManufacturingGeometricTolerance> GeometricTolerances,
    IReadOnlyList<SheetMetalManufacturingAnnotation> Annotations)
{
    public static readonly SheetMetalManufacturingDefinition Empty = new(
        "unspecified", null, null, null, null, null, null, null, null, [], [], [], []);

    public IReadOnlyList<Step242SemanticPmi> ToStep242SemanticPmi(SheetMetalPartIr? part = null)
    {
        var result = new List<Step242SemanticPmi>();
        result.AddRange(Datums.Select(datum =>
            new Step242SemanticPmiDatum(datum.Target, "plane", datum.Label, datum.Target)));

        foreach (var dimension in Dimensions)
        {
            if (dimension.Kind.Equals("Diameter", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(new Step242SemanticPmiHole(
                    dimension.Target,
                    dimension.Value,
                    null,
                    "sheet_metal_pattern",
                    dimension.TolerancePlus,
                    dimension.ToleranceMinus,
                    dimension.Quantity));
            }
            else
            {
                result.Add(new Step242SemanticPmiDimension(
                    dimension.Target,
                    dimension.Name,
                    dimension.Kind,
                    dimension.Value,
                    dimension.TolerancePlus,
                    dimension.ToleranceMinus,
                    dimension.Quantity));
            }
        }

        result.AddRange(GeometricTolerances.Select(tolerance =>
            new Step242SemanticPmiGeometricTolerance(
                tolerance.Target,
                tolerance.Name,
                tolerance.Kind,
                tolerance.Value,
                tolerance.DatumReferences,
                tolerance.Quantity)));
        result.AddRange(Annotations.Select(annotation =>
            new Step242SemanticPmiNote(annotation.Name, annotation.Target, annotation.Text)));
        if (part?.FormedBody is null) return result;
        return result.Select(item => item with
        {
            GeometricFaceIds = ResolveTargetFaces(part, item switch
            {
                Step242SemanticPmiDatum datum => datum.Target,
                Step242SemanticPmiNote note => note.Target,
                _ => item.FeatureId
            })
        }).ToArray();
    }

    private static IReadOnlyList<int> ResolveTargetFaces(SheetMetalPartIr part, string target)
    {
        var body = part.FormedBody!;
        var faceIds = new HashSet<int>();
        foreach (var feature in part.Features.Where(feature =>
                     feature.StableId.Equals(target, StringComparison.OrdinalIgnoreCase)
                     || feature.StableId.StartsWith($"{target}[", StringComparison.OrdinalIgnoreCase)))
            AddFeatureFaces(feature);

        if (part.Regions.FirstOrDefault(region => region.StableId.Equals(target, StringComparison.OrdinalIgnoreCase)) is { } region)
            AddRegionFaces(region);
        if (part.Bends.FirstOrDefault(bend => bend.StableId.Equals(target, StringComparison.OrdinalIgnoreCase)) is { } bend
            && part.Regions.FirstOrDefault(region => region.StableId.Equals($"{bend.StableId}Region", StringComparison.OrdinalIgnoreCase)) is { } bendRegion)
            AddRegionFaces(bendRegion);
        return faceIds.OrderBy(id => id).ToArray();

        void AddFeatureFaces(SheetFeatureIr feature)
        {
            var owner = part.Regions.Single(region => region.StableId.Equals(feature.OwningRegionId, StringComparison.OrdinalIgnoreCase));
            if (owner.Plane is null) return;
            var normal = Unit(owner.Plane.Normal);
            var expected = new List<(Point3D Center, double Radius)>();
            if (feature.Kind == SheetFeatureKind.CircularHole && feature.Diameter is { } diameter)
                expected.Add((feature.Center, diameter / 2d));
            else if (feature.ExactContour is { } contour)
                foreach (var arc in contour.OuterLoop.Segments.Select(segment => segment.Geometry).OfType<LineArcCircularArc2D>())
                    expected.Add((owner.Plane.Origin + owner.Plane.UAxis * arc.Center.X + owner.Plane.VAxis * arc.Center.Y, arc.Radius));

            foreach (var face in body.Topology.Faces)
            {
                if (!body.TryGetFaceSurfaceGeometry(face.Id, out var surface) || surface?.Cylinder is not { } cylinder) continue;
                var axis = Unit(cylinder.Axis.ToVector());
                if (Math.Abs(Math.Abs(axis.Dot(normal)) - 1d) > 1e-7) continue;
                if (expected.Any(item => Math.Abs(item.Radius - cylinder.Radius) <= 1e-7 && DistanceToAxis(item.Center, cylinder.Origin, axis) <= 1e-6))
                    faceIds.Add(face.Id.Value);
            }
        }

        void AddRegionFaces(SheetRegionIr region)
        {
            foreach (var face in body.Topology.Faces)
            {
                if (!body.TryGetFaceSurfaceGeometry(face.Id, out var surface) || surface is null) continue;
                if (region.Plane is { } plane && surface.Plane is { } surfacePlane)
                {
                    var normal = Unit(plane.Normal);
                    if (Math.Abs(Math.Abs(surfacePlane.Normal.ToVector().Dot(normal)) - 1d) <= 1e-7
                        && Math.Abs((surfacePlane.Origin - (plane.Origin + normal * (part.Thickness / 2d))).Dot(normal)) <= 1e-6)
                        faceIds.Add(face.Id.Value);
                }
                else if (region.Cylinder is { } reference && surface.Cylinder is { } cylinder)
                {
                    var axis = Unit(reference.AxisDirection);
                    if (Math.Abs(Math.Abs(cylinder.Axis.ToVector().Dot(axis)) - 1d) <= 1e-7
                        && DistanceToAxis(reference.AxisOrigin, cylinder.Origin, axis) <= 1e-6
                        && (Math.Abs(cylinder.Radius - reference.InsideRadius) <= 1e-6 || Math.Abs(cylinder.Radius - (reference.InsideRadius + part.Thickness)) <= 1e-6))
                        faceIds.Add(face.Id.Value);
                }
            }
        }
    }

    private static double DistanceToAxis(Point3D point, Point3D origin, Vector3D unitAxis) =>
        ((point - origin) - unitAxis * (point - origin).Dot(unitAxis)).Length;

    private static Vector3D Unit(Vector3D vector) => vector / vector.Length;
}

internal sealed record SheetMetalManufacturingParseResult(
    SheetMetalManufacturingDefinition Definition,
    IReadOnlyList<SheetMetalDiagnostic> Diagnostics)
{
    public bool IsSuccess => Diagnostics.All(diagnostic => diagnostic.Severity != SheetMetalDiagnosticSeverity.Error);
}

internal static class SheetMetalManufacturingParser
{
    private const RegexOptions Rx = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline;
    private const string Number = @"[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)";

    public static SheetMetalManufacturingParseResult Parse(
        string source,
        string partName,
        AuthoredSheetBaseSpec baseSpec,
        IReadOnlyList<AuthoredSheetFlangeSpec> flanges,
        SheetMetalSemanticLayout semanticLayout)
    {
        var manufacturing = FindNamedBlock(source, "Manufacturing");
        var pmi = FindNamedBlock(source, "Pmi");
        if (manufacturing is null && pmi is null)
        {
            return new(SheetMetalManufacturingDefinition.Empty, []);
        }

        var diagnostics = new List<SheetMetalDiagnostic>();
        if (manufacturing is null)
        {
            diagnostics.Add(Error("A Sheet Metal `Pmi` block requires a `Manufacturing <Release> { ... }` block."));
            return new(SheetMetalManufacturingDefinition.Empty, diagnostics);
        }

        var targets = BuildTargets(partName, baseSpec, flanges, semanticLayout);
        var datums = new List<SheetMetalManufacturingDatum>();
        var dimensions = new List<SheetMetalManufacturingDimension>();
        var tolerances = new List<SheetMetalManufacturingGeometricTolerance>();
        var annotations = new List<SheetMetalManufacturingAnnotation>();

        var materialSpecification = Quoted(manufacturing.Value.Body, "MaterialSpecification");
        var blankProcess = Quoted(manufacturing.Value.Body, "BlankProcess");
        var formingProcess = Quoted(manufacturing.Value.Body, "FormingProcess");
        var thicknessTolerance = PlusMinus(manufacturing.Value.Body, "ThicknessTolerance");
        var generalCutTolerance = Scalar(manufacturing.Value.Body, "GeneralCutTolerance", "mm");
        var generalFormedTolerance = Scalar(manufacturing.Value.Body, "GeneralFormedTolerance", "mm");
        var generalAngularTolerance = Scalar(manufacturing.Value.Body, "GeneralAngularTolerance", "deg");

        if (pmi is not null)
        {
            foreach (var block in ChildBlocks(pmi.Value.Body))
            {
                var target = Token(block.Body, "Target");
                if (target is null)
                {
                    diagnostics.Add(Error($"PMI '{block.Name}' requires a stable Target."));
                    continue;
                }
                if (!targets.Contains(target))
                {
                    diagnostics.Add(Error($"PMI '{block.Name}' targets unknown Sheet Metal identity '{target}'."));
                    continue;
                }

                switch (block.Kind.ToLowerInvariant())
                {
                    case "datumfeature":
                        if (datums.Any(existing => existing.Label.Equals(block.Name, StringComparison.OrdinalIgnoreCase)))
                            diagnostics.Add(Error($"Datum label '{block.Name}' is declared more than once."));
                        else datums.Add(new(block.Name, target));
                        break;
                    case "dimension":
                    {
                        var kind = Token(block.Body, "Kind");
                        var value = Scalar(block.Body, "Value", "mm");
                        var tolerance = PlusMinus(block.Body, "Tolerance");
                        if (kind is null || value is null || value <= 0 || tolerance is null)
                        {
                            diagnostics.Add(Error($"Dimension '{block.Name}' requires Kind, positive Value, and PlusMinus tolerance in millimetres."));
                            break;
                        }
                        dimensions.Add(new(block.Name, kind, target, value.Value, tolerance.Value.Plus, tolerance.Value.Minus, Integer(block.Body, "Quantity")));
                        break;
                    }
                    case "position":
                    {
                        var value = Scalar(block.Body, "Tolerance", "mm");
                        var refs = TokenList(block.Body, "DatumRefs");
                        if (value is null || value <= 0 || refs.Count == 0)
                        {
                            diagnostics.Add(Error($"Position '{block.Name}' requires positive Tolerance and DatumRefs."));
                            break;
                        }
                        tolerances.Add(new(block.Name, "Position", target, value.Value, refs, Integer(block.Body, "Quantity")));
                        break;
                    }
                    case "annotation":
                    {
                        var text = Quoted(block.Body, "Text");
                        if (string.IsNullOrWhiteSpace(text)) diagnostics.Add(Error($"Annotation '{block.Name}' requires non-empty Text."));
                        else annotations.Add(new(block.Name, target, text));
                        break;
                    }
                    default:
                        diagnostics.Add(Error($"Unsupported Sheet Metal PMI record '{block.Kind} {block.Name}'. Supported: DatumFeature, Dimension, Position, Annotation."));
                        break;
                }
            }
        }

        var declaredLabels = datums.Select(datum => datum.Label).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var tolerance in tolerances)
        {
            foreach (var datumReference in tolerance.DatumReferences.Where(reference => !declaredLabels.Contains(reference)))
                diagnostics.Add(Error($"Position '{tolerance.Name}' references unknown datum '{datumReference}'."));
        }

        var definition = new SheetMetalManufacturingDefinition(
            manufacturing.Value.Name,
            materialSpecification,
            thicknessTolerance?.Plus,
            thicknessTolerance?.Minus,
            blankProcess,
            formingProcess,
            generalCutTolerance,
            generalFormedTolerance,
            generalAngularTolerance,
            datums,
            dimensions,
            tolerances,
            annotations);
        return new(definition, diagnostics);
    }

    private static HashSet<string> BuildTargets(
        string partName,
        AuthoredSheetBaseSpec baseSpec,
        IReadOnlyList<AuthoredSheetFlangeSpec> flanges,
        SheetMetalSemanticLayout layout)
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { partName, baseSpec.Name };
        foreach (var flange in flanges)
        {
            targets.Add(flange.Name);
            targets.Add($"{flange.Name}Bend");
        }
        foreach (var pattern in layout.Patterns)
        {
            targets.Add(pattern.Path);
            foreach (var member in pattern.Members) targets.Add(member);
        }
        return targets;
    }

    private static (string Name, string Body)? FindNamedBlock(string source, string keyword)
    {
        var match = Regex.Match(source, $@"\b{Regex.Escape(keyword)}\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{{", Rx);
        return match.Success ? Extract(source, match, match.Groups["name"].Value) : null;
    }

    private static IReadOnlyList<(string Kind, string Name, string Body)> ChildBlocks(string source)
    {
        var result = new List<(string, string, string)>();
        var offset = 0;
        while (offset < source.Length)
        {
            var match = Regex.Match(source[offset..], @"\b(?<kind>DatumFeature|Dimension|Position|Annotation)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", Rx);
            if (!match.Success) break;
            var absolute = new MatchProxy(match, offset);
            var extracted = Extract(source, absolute.Index, absolute.OpenBraceIndex, absolute.Name);
            result.Add((match.Groups["kind"].Value, absolute.Name, extracted.Body));
            offset = extracted.CloseIndex + 1;
        }
        return result;
    }

    private static (string Name, string Body) Extract(string source, Match match, string name)
    {
        var open = source.IndexOf('{', match.Index);
        var extracted = Extract(source, match.Index, open, name);
        return (name, extracted.Body);
    }

    private static (string Body, int CloseIndex) Extract(string source, int matchIndex, int open, string name)
    {
        var depth = 0;
        for (var index = open; index < source.Length; index++)
        {
            if (source[index] == '{') depth++;
            else if (source[index] == '}' && --depth == 0) return (source[(open + 1)..index], index);
        }
        return (source[(open + 1)..], source.Length - 1);
    }

    private readonly record struct MatchProxy(int Index, int OpenBraceIndex, string Name)
    {
        public MatchProxy(Match match, int offset) : this(
            offset + match.Index,
            offset + match.Index + match.Value.LastIndexOf('{'),
            match.Groups["name"].Value) { }
    }

    private static string? Token(string source, string name)
    {
        var match = Regex.Match(source, $@"\b{Regex.Escape(name)}\s*:\s*(?<value>[A-Za-z_][A-Za-z0-9_.\[\]]*)\s*;", Rx);
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static string? Quoted(string source, string name)
    {
        var match = Regex.Match(source, $@"\b{Regex.Escape(name)}\s*:\s*""(?<value>(?:[^""\\]|\\.)*)""\s*;", Rx);
        return match.Success ? Regex.Unescape(match.Groups["value"].Value) : null;
    }

    private static double? Scalar(string source, string name, string unit)
    {
        var match = Regex.Match(source, $@"\b{Regex.Escape(name)}\s*:\s*(?<value>{Number})\s*{Regex.Escape(unit)}\s*;", Rx);
        return match.Success ? double.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture) : null;
    }

    private static (double Plus, double Minus)? PlusMinus(string source, string name)
    {
        var match = Regex.Match(source, $@"\b{Regex.Escape(name)}\s*:\s*PlusMinus\s*\(\s*(?<plus>{Number})\s*mm\s*,\s*(?<minus>{Number})\s*mm\s*\)\s*;", Rx);
        if (!match.Success) return null;
        return (
            double.Parse(match.Groups["plus"].Value, CultureInfo.InvariantCulture),
            double.Parse(match.Groups["minus"].Value, CultureInfo.InvariantCulture));
    }

    private static int? Integer(string source, string name)
    {
        var match = Regex.Match(source, $@"\b{Regex.Escape(name)}\s*:\s*(?<value>[0-9]+)\s*;", Rx);
        return match.Success ? int.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture) : null;
    }

    private static IReadOnlyList<string> TokenList(string source, string name)
    {
        var match = Regex.Match(source, $@"\b{Regex.Escape(name)}\s*:\s*\[(?<value>[^\]]*)\]\s*;", Rx);
        return match.Success
            ? match.Groups["value"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];
    }

    private static SheetMetalDiagnostic Error(string message) =>
        new("sheetmetal-manufacturing-pmi-invalid", SheetMetalDiagnosticSeverity.Error, message);
}
