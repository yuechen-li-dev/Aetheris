using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Math;
using Aetheris.Surfacing;

namespace Aetheris.SheetMetal;

/// <summary>Reader for the explicit, loss-aware recovered Sheet Metal Firmament form.</summary>
internal static class RecoveredSheetMetalFirmament
{
    private const RegexOptions Rx = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline;
    private const string Number = @"[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[Ee][+-]?[0-9]+)?";

    public static bool IsRecovered(string source) => Regex.IsMatch(source, "\\bRecoveredRegion\\s+\"", Rx);

    public static SheetMetalAuthoringResult Compile(string source, string sourcePath)
    {
        var header = Regex.Match(source, @"\bSheetMetal\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", Rx);
        if (!header.Success || !Scalar(source, "Thickness", "mm", out var thickness) || thickness <= 0)
            return Failure("Recovered Sheet Metal requires a valid header and positive Thickness.");
        var k = Scalar(source, "KFactor", null, out var parsedK) ? parsedK : .5;
        if (k is < 0 or > 1) return Failure("Recovered Sheet Metal KFactor must be between 0 and 1.");
        var baseRegion = Quoted(source, "BaseRegion"); if (baseRegion is null) return Failure("Recovered Sheet Metal requires BaseRegion.");
        var statusText = Token(source, "RecoveryStatus") ?? nameof(SheetMetalRecognitionStatus.Partial);
        if (!Enum.TryParse<SheetMetalRecognitionStatus>(statusText, true, out var status)) return Failure($"Unknown RecoveryStatus '{statusText}'.");

        var regions = new List<SheetRegionIr>();
        foreach (Match block in Regex.Matches(source, "\\bRecoveredRegion\\s+\"(?<id>[^\"]+)\"\\s*\\{(?<body>.*?)\\}", Rx))
        {
            var id = block.Groups["id"].Value; var body = block.Groups["body"].Value; var kindText = Token(body, "Kind");
            if (!Enum.TryParse<SheetRegionKind>(kindText, true, out var kind)) return Failure($"RecoveredRegion '{id}' has invalid Kind.");
            Scalar(body, "Area", "mm2", out var area); var faces = IntList(body, "SourceFaces"); var boundary = Points(body, "Boundary");
            SheetPlaneReference? plane = null; SheetCylinderReference? cylinder = null;
            if (kind == SheetRegionKind.Planar)
            {
                if (!Point(body, "PlaneOrigin", true, out var origin) || !Vector(body, "PlaneNormal", out var normal) || !Vector(body, "PlaneU", out var u) || !Vector(body, "PlaneV", out var v))
                    return Failure($"Recovered planar region '{id}' is missing its analytic frame.");
                plane = new(origin, normal, u, v, Bool(body, "MaterialPositiveSide", true));
            }
            else if (kind == SheetRegionKind.CylindricalBend)
            {
                if (!Point(body, "AxisOrigin", true, out var origin) || !Vector(body, "AxisDirection", out var axis) || !Scalar(body, "MidRadius", "mm", out var midRadius) || !Scalar(body, "InsideRadius", "mm", out var insideRadius) || !Scalar(body, "AngularSpan", "deg", out var angular) || !Scalar(body, "AxisLength", "mm", out var length))
                    return Failure($"Recovered cylindrical region '{id}' is missing its analytic cylinder values.");
                cylinder = new(origin, axis, midRadius, insideRadius, angular * Math.PI / 180d, length, Bool(body, "MaterialOutside", true));
            }
            regions.Add(new(id, kind, new(DevelopabilityKind.Developable, "recovered analytic support", 0, 0, "Serialized recovered support."), plane, cylinder, boundary, area,
                new("Firmament recovered declaration", "recovered STEP semantics", faces, [], sourcePath), [new(SheetEvidenceKind.Authored, "recovered-region", "Explicit loss-aware Firmament recovery declaration.", SourceFaceIds: faces)]));
        }
        if (regions.Count == 0 || regions.All(r => r.StableId != baseRegion)) return Failure("Recovered Sheet Metal BaseRegion does not name a declared region.");

        var bends = new List<SheetBendIr>();
        foreach (Match block in Regex.Matches(source, "\\bRecoveredBend\\s+\"(?<id>[^\"]+)\"\\s*\\{(?<body>.*?)\\}", Rx))
        {
            var id = block.Groups["id"].Value; var body = block.Groups["body"].Value;
            if (!Point(body, "AxisOrigin", true, out var origin) || !Vector(body, "AxisDirection", out var axis) || !Scalar(body, "Angle", "deg", out var angle) || !Scalar(body, "InsideRadius", "mm", out var radius))
                return Failure($"Recovered bend '{id}' is missing axis, angle, or radius.");
            var between = StringList(body, "Between"); if (between.Count != 2 || between.Any(x => regions.All(r => r.StableId != x))) return Failure($"Recovered bend '{id}' must connect two declared regions.");
            if (!Enum.TryParse<SheetBendDirection>(Token(body, "Direction"), true, out var direction)) direction = SheetBendDirection.Unknown;
            var faces = IntList(body, "SourceFaces"); bends.Add(new(id, origin, axis, angle * Math.PI / 180d, radius, thickness, direction, between[0], between[1], SheetNeutralAxisPolicy.KFactorPolicy(k),
                new("Firmament recovered declaration", "recovered STEP semantics", faces, [], sourcePath), [new(SheetEvidenceKind.Authored, "recovered-bend", "Axis, radius, direction, and region adjacency serialized explicitly.", SourceFaceIds: faces)]));
        }

        var cuts = new List<SheetFeatureIr>();
        foreach (Match block in Regex.Matches(source, "\\bRecoveredCut\\s+\"(?<id>[^\"]+)\"\\s*\\{(?<body>.*?)\\}", Rx))
        {
            var id = block.Groups["id"].Value; var body = block.Groups["body"].Value;
            if (!Enum.TryParse<SheetFeatureKind>(Token(body, "Kind"), true, out var kind)) return Failure($"Recovered cut '{id}' has invalid Kind.");
            var owner = Quoted(body, "On"); if (owner is null || regions.All(r => r.StableId != owner) || !Point(body, "Center", true, out var center)) return Failure($"Recovered cut '{id}' has invalid owner or center.");
            var diameter = Scalar(body, "Diameter", "mm", out var d) ? d : (double?)null; var faces = IntList(body, "SourceFaces");
            cuts.Add(new(id, kind, owner, center, diameter, Points(body, "Boundary"), new("Firmament recovered declaration", "recovered STEP semantics", faces, [], sourcePath),
                [new(SheetEvidenceKind.Authored, "recovered-cut", "Cut classification and reference boundary serialized explicitly.", diameter, SourceFaceIds: faces)]));
        }
        var part = new SheetMetalPartIr(header.Groups["name"].Value, thickness, null, baseRegion, regions, bends, cuts, new(k), status,
            "Firmament recovered Sheet Metal semantics", [new(SheetEvidenceKind.Authored, "recovered-intent", "Loss-aware semantic recovery source; original STEP remains authoritative geometry.")], [], null);
        var flat = SheetMetalFlattener.Flatten(part, new(k));
        return new(flat.Status == FlatPatternStatus.Unsupported ? false : true, null, part, flat, flat.Diagnostics);
    }

    private static bool Scalar(string text, string name, string? unit, out double value)
    {
        var suffix = unit is null ? "" : @"\s*" + Regex.Escape(unit); var match = Regex.Match(text, @"\b" + Regex.Escape(name) + @"\s*:\s*(?<v>" + Number + ")" + suffix + @"\s*;", Rx);
        value = match.Success ? double.Parse(match.Groups["v"].Value, NumberStyles.Float, CultureInfo.InvariantCulture) : 0; return match.Success;
    }
    private static bool Point(string text, string name, bool units, out Point3D value) { var tuple = Tuple(text, name, units); value = tuple is null ? default : new(tuple.Value.X, tuple.Value.Y, tuple.Value.Z); return tuple is not null; }
    private static bool Vector(string text, string name, out Vector3D value) { var tuple = Tuple(text, name, false); value = tuple is null ? default : new(tuple.Value.X, tuple.Value.Y, tuple.Value.Z); return tuple is not null; }
    private static (double X, double Y, double Z)? Tuple(string text, string name, bool units)
    {
        var u = units ? @"\s*mm" : ""; var m = Regex.Match(text, @"\b" + Regex.Escape(name) + @"\s*:\s*\(\s*(?<x>" + Number + ")" + u + @"\s*,\s*(?<y>" + Number + ")" + u + @"\s*,\s*(?<z>" + Number + ")" + u + @"\s*\)\s*;", Rx);
        return m.Success ? (N(m, "x"), N(m, "y"), N(m, "z")) : null;
    }
    private static IReadOnlyList<Point3D> Points(string text, string name)
    {
        var list = Regex.Match(text, @"\b" + Regex.Escape(name) + @"\s*:\s*\[(?<v>.*?)\]\s*;", Rx); if (!list.Success) return [];
        return Regex.Matches(list.Groups["v"].Value, @"\(\s*(?<x>" + Number + @")\s*mm\s*,\s*(?<y>" + Number + @")\s*mm\s*,\s*(?<z>" + Number + @")\s*mm\s*\)", Rx).Cast<Match>().Select(m => new Point3D(N(m, "x"), N(m, "y"), N(m, "z"))).ToArray();
    }
    private static IReadOnlyList<int> IntList(string text, string name) { var m = Regex.Match(text, @"\b" + Regex.Escape(name) + @"\s*:\s*\[(?<v>.*?)\]\s*;", Rx); return m.Success ? Regex.Matches(m.Groups["v"].Value, @"\d+").Select(x => int.Parse(x.Value, CultureInfo.InvariantCulture)).ToArray() : []; }
    private static IReadOnlyList<string> StringList(string text, string name) { var m = Regex.Match(text, @"\b" + Regex.Escape(name) + @"\s*:\s*\[(?<v>.*?)\]\s*;", Rx); return m.Success ? Regex.Matches(m.Groups["v"].Value, "\"(?<v>[^\"]+)\"").Cast<Match>().Select(x => x.Groups["v"].Value).ToArray() : []; }
    private static string? Quoted(string text, string name) { var m = Regex.Match(text, @"\b" + Regex.Escape(name) + "\\s*:\\s*\"(?<v>[^\"]+)\"\\s*;", Rx); return m.Success ? m.Groups["v"].Value : null; }
    private static string? Token(string text, string name) { var m = Regex.Match(text, @"\b" + Regex.Escape(name) + @"\s*:\s*(?<v>[A-Za-z_][A-Za-z0-9_]*)\s*;", Rx); return m.Success ? m.Groups["v"].Value : null; }
    private static bool Bool(string text, string name, bool fallback) => bool.TryParse(Token(text, name), out var value) ? value : fallback;
    private static double N(Match m, string group) => double.Parse(m.Groups[group].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static SheetMetalAuthoringResult Failure(string message) => new(false, null, null, null, [new("sheetmetal-firmament-recovery-invalid", SheetMetalDiagnosticSeverity.Error, message)]);
}
