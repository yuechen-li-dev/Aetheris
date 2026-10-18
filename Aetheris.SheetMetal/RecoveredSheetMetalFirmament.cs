using System.Globalization;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using static Aetheris.SheetMetal.RecoverySyntax;
using Aetheris.Kernel.Core.Math;
using Aetheris.Surfacing;

namespace Aetheris.SheetMetal;

/// <summary>Reader for the explicit, loss-aware recovered Sheet Metal Firmament form.</summary>
internal static class RecoveredSheetMetalFirmament
{
    public static bool IsRecovered(string source)=>FirmamentSyntaxTraversal.Declarations(FirmamentSyntaxParser.Parse(source).Document).Any(d=>d.Type.Name.Text=="RecoveredRegion");

    public static SheetMetalAuthoringResult Compile(string source, string sourcePath)
    {
        var root=Root(source,sourcePath,out var syntaxDiagnostics);
        if(root is null)return new(false,null,null,null,syntaxDiagnostics);
        root.Take("RecoverySource");
        if(!Scalar(root,"Thickness","mm",out var thickness)||thickness<=0)return Failure("Recovered Sheet Metal requires positive Thickness.");
        var k = Scalar(root, "KFactor", null, out var parsedK) ? parsedK : .5;
        if (k is < 0 or > 1) return Failure("Recovered Sheet Metal KFactor must be between 0 and 1.");
        var baseRegion = Quoted(root, "BaseRegion"); if (baseRegion is null) return Failure("Recovered Sheet Metal requires BaseRegion.");
        var statusText = Token(root, "RecoveryStatus") ?? nameof(SheetMetalRecognitionStatus.Partial);
        if (!Enum.TryParse<SheetMetalRecognitionStatus>(statusText, true, out var status)) return Failure($"Unknown RecoveryStatus '{statusText}'.");

        var regions = new List<SheetRegionIr>();
        foreach (var block in root.Children("RecoveredRegion"))
        {
            var id = block.Name; var body = block.Body; var kindText = Token(body, "Kind");
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
        foreach (var block in root.Children("RecoveredBend"))
        {
            var id = block.Name; var body = block.Body;
            if (!Point(body, "AxisOrigin", true, out var origin) || !Vector(body, "AxisDirection", out var axis) || !Scalar(body, "Angle", "deg", out var angle) || !Scalar(body, "InsideRadius", "mm", out var radius))
                return Failure($"Recovered bend '{id}' is missing axis, angle, or radius.");
            var between = StringList(body, "Between"); if (between.Count != 2 || between.Any(x => regions.All(r => r.StableId != x))) return Failure($"Recovered bend '{id}' must connect two declared regions.");
            var directionText=Token(body,"Direction");
            if (!Enum.TryParse<SheetBendDirection>(directionText, true, out var direction)){direction=SheetBendDirection.Unknown;if(directionText is not null)body.Invalid("Direction");}
            var faces = IntList(body, "SourceFaces"); bends.Add(new(id, origin, axis, angle * Math.PI / 180d, radius, thickness, direction, between[0], between[1], SheetNeutralAxisPolicy.KFactorPolicy(k),
                new("Firmament recovered declaration", "recovered STEP semantics", faces, [], sourcePath), [new(SheetEvidenceKind.Authored, "recovered-bend", "Axis, radius, direction, and region adjacency serialized explicitly.", SourceFaceIds: faces)]));
        }

        var cuts = new List<SheetFeatureIr>();
        foreach (var block in root.Children("RecoveredCut"))
        {
            var id = block.Name; var body = block.Body;
            if (!Enum.TryParse<SheetFeatureKind>(Token(body, "Kind"), true, out var kind)) return Failure($"Recovered cut '{id}' has invalid Kind.");
            var owner = Quoted(body, "On"); if (owner is null || regions.All(r => r.StableId != owner) || !Point(body, "Center", true, out var center)) return Failure($"Recovered cut '{id}' has invalid owner or center.");
            var diameter = Scalar(body, "Diameter", "mm", out var d) ? d : (double?)null; var faces = IntList(body, "SourceFaces");
            cuts.Add(new(id, kind, owner, center, diameter, Points(body, "Boundary"), new("Firmament recovered declaration", "recovered STEP semantics", faces, [], sourcePath),
                [new(SheetEvidenceKind.Authored, "recovered-cut", "Cut classification and reference boundary serialized explicitly.", diameter, SourceFaceIds: faces)]));
        }
        if(regions.Select(r=>r.StableId).Distinct().Count()!=regions.Count || bends.Select(b=>b.StableId).Distinct().Count()!=bends.Count || cuts.Select(c=>c.StableId).Distinct().Count()!=cuts.Count)
            return Failure("Recovered identities must be unique within each region, bend and cut family.");
        var closed=Close(root);if(closed.Count>0)return new(false,null,null,null,closed);
        var part = new SheetMetalPartIr(root.Name, thickness, null, baseRegion, regions, bends, cuts, new(k), status,
            "Firmament recovered Sheet Metal semantics", [new(SheetEvidenceKind.Authored, "recovered-intent", "Loss-aware semantic recovery source; original STEP remains authoritative geometry.")], [], null);
        var flat = SheetMetalFlattener.Flatten(part, new(k));
        return new(flat.Status == FlatPatternStatus.Unsupported ? false : true, null, part, flat, flat.Diagnostics);
    }

    private static SheetMetalAuthoringResult Failure(string message) => new(false, null, null, null, [new("sheetmetal-firmament-recovery-invalid", SheetMetalDiagnosticSeverity.Error, message)]);
}
