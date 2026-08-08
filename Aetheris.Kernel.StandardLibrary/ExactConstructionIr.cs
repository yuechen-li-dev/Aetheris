using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.StandardLibrary;

public abstract record ExactConstructionNode(string StableId);

public sealed record RegularPrismConstruction(
    string StableId, int SideCount, double AcrossFlats, double Start, double End, double OrientationDegrees = 30d)
    : ExactConstructionNode(StableId)
{
    public double Apothem => AcrossFlats / 2d;
    public double Circumradius => Apothem / Math.Cos(Math.PI / SideCount);
}

public enum PeriodicSplitPolicy { TwoHalfFaces }

public sealed record AxialCylinderConstruction(
    string StableId, double Radius, double Start, double End, PeriodicSplitPolicy SplitPolicy = PeriodicSplitPolicy.TwoHalfFaces)
    : ExactConstructionNode(StableId);

public sealed record AxialFrustumConstruction(
    string StableId, double StartRadius, double EndRadius, double Start, double End,
    PeriodicSplitPolicy SplitPolicy = PeriodicSplitPolicy.TwoHalfFaces)
    : ExactConstructionNode(StableId);

/// <summary>One cone support trimmed by the side planes of a regular prism.</summary>
public sealed record ConePlanarTrimConstruction(
    string StableId, double Apex, double SemiAngleDegrees, double CapPosition, double CapRadius,
    PeriodicSplitPolicy SplitPolicy = PeriodicSplitPolicy.TwoHalfFaces)
    : ExactConstructionNode(StableId);

/// <summary>Material-adding interior shoulder blend, implemented on one shared torus.</summary>
public sealed record ConcaveFilletConstruction(
    string StableId, double Radius, double ShoulderRadius, double Start, double End,
    PeriodicSplitPolicy SplitPolicy = PeriodicSplitPolicy.TwoHalfFaces)
    : ExactConstructionNode(StableId);

public sealed record PlanarCapConstruction(string StableId, double Position, double Radius, bool OutwardPositive)
    : ExactConstructionNode(StableId);

public sealed record AxialSectionStackConstruction(string StableId, IReadOnlyList<ExactConstructionNode> Sections)
    : ExactConstructionNode(StableId);

public enum ConstructionSemanticKind { Part, Region, Face }

/// <summary>Template/family-authored claim over a materializer topology role.</summary>
public sealed record ConstructionSemanticClaim(
    string StableIdPattern, ConstructionSemanticKind Kind, string? TopologyRole = null,
    string? ParentStableId = null, string? Metadata = null);

/// <summary>Geometry-only plan for the proven connected prism/coaxial construction family.</summary>
public sealed record ExactCoaxialConstructionPlan(
    string StableId,
    RegularPrismConstruction Prism,
    ConePlanarTrimConstruction ConePlanarTrim,
    ConcaveFilletConstruction RootBlend,
    AxialCylinderConstruction Cylinder,
    AxialFrustumConstruction EndFrustum,
    PlanarCapConstruction TopCap,
    PlanarCapConstruction EndCap,
    AxialSectionStackConstruction Stack,
    IReadOnlyList<ConstructionSemanticClaim> SemanticClaims,
    IReadOnlyDictionary<string, string> Metadata,
    string DeterministicSignature);

public sealed record ExactConstructionResult(
    BrepBody Body,
    IReadOnlyDictionary<string, IReadOnlyList<FaceId>> FaceGroups,
    IReadOnlyList<ExactConstructionSemanticDescendant> Semantics,
    IReadOnlyDictionary<string, string> Metadata,
    string DeterministicSignature);

/// <summary>Family knowledge and admission live here; the emitted plan contains no HexBoltSpec.</summary>
public static class HexBoltConstructionPlanner
{
    private const double Tolerance = 1e-9d;

    public static IReadOnlyList<HexBoltAdmissionDiagnostic> Validate(HexBoltSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var diagnostics = new List<HexBoltAdmissionDiagnostic>();
        foreach (var (name, value) in new (string Name, double Value)[]
        {
            (nameof(spec.NominalDiameter), spec.NominalDiameter), (nameof(spec.Length), spec.Length),
            (nameof(spec.HeadAcrossFlats), spec.HeadAcrossFlats), (nameof(spec.HeadHeight), spec.HeadHeight),
            (nameof(spec.TopFlatDiameter), spec.TopFlatDiameter), (nameof(spec.TipDiameter), spec.TipDiameter)
        })
            if (!double.IsFinite(value) || value <= Tolerance)
                diagnostics.Add(new(HexBoltAdmissionCode.NonFiniteOrNonPositiveDimension, name, $"{name} must be finite and positive."));

        if (!double.IsFinite(spec.TopChamferAngle) || spec.TopChamferAngle <= Tolerance || spec.TopChamferAngle >= 90d - Tolerance)
            diagnostics.Add(new(HexBoltAdmissionCode.NonFiniteOrNonPositiveDimension, nameof(spec.TopChamferAngle), "TopChamferAngle must be in (0, 90) degrees."));
        if (!double.IsFinite(spec.ThreadLength) || spec.ThreadLength < 0d || spec.ThreadLength > spec.Length + Tolerance)
            diagnostics.Add(new(HexBoltAdmissionCode.ThreadLengthOutsideShank, nameof(spec.ThreadLength), "ThreadLength must lie in [0, Length]."));
        if (!double.IsFinite(spec.UnderHeadRadius) || spec.UnderHeadRadius < 0d || spec.UnderHeadRadius >= spec.NominalDiameter / 2d)
            diagnostics.Add(new(HexBoltAdmissionCode.UnderHeadRadiusInvalid, nameof(spec.UnderHeadRadius), "UnderHeadRadius must be non-negative and smaller than the shank radius."));
        if (string.IsNullOrWhiteSpace(spec.ThreadDesignation) || string.IsNullOrWhiteSpace(spec.PropertyClass))
            diagnostics.Add(new(HexBoltAdmissionCode.EmptySemanticMetadata, nameof(spec.ThreadDesignation), "ThreadDesignation and PropertyClass must be non-empty semantic metadata."));
        if (diagnostics.Count > 0) return diagnostics;

        var apothem = spec.HeadAcrossFlats / 2d;
        var circumradius = spec.HeadAcrossFlats / Math.Sqrt(3d);
        var topRadius = spec.TopFlatDiameter / 2d;
        if (topRadius >= apothem - Tolerance)
            diagnostics.Add(new(HexBoltAdmissionCode.TopFlatOutsideHex, nameof(spec.TopFlatDiameter), "The top-flat circle must lie strictly inside the hex apothem."));
        var coneSlope = 1d / Math.Tan(spec.TopChamferAngle * Math.PI / 180d);
        if ((circumradius - topRadius) / coneSlope >= spec.HeadHeight - Tolerance)
            diagnostics.Add(new(HexBoltAdmissionCode.TopChamferConsumesHead, nameof(spec.HeadHeight), "The cone/hex corner intersection must remain above the under-head plane with non-zero side remnants."));
        if (!double.IsFinite(spec.TipChamferLength) || spec.TipChamferLength <= Tolerance || spec.TipChamferLength >= spec.Length - spec.UnderHeadRadius - Tolerance
            || spec.TipDiameter >= spec.NominalDiameter - Tolerance)
            diagnostics.Add(new(HexBoltAdmissionCode.TipChamferInvalid, nameof(spec.TipChamferLength), "Tip chamfer must have positive axial length, leave a cylindrical shank, and reduce the tip diameter."));
        return diagnostics;
    }

    public static HexBoltDerivedDimensions Derive(HexBoltSpec spec)
    {
        var apothem = spec.HeadAcrossFlats / 2d; var circumradius = spec.HeadAcrossFlats / Math.Sqrt(3d);
        var topRadius = spec.TopFlatDiameter / 2d; var semiAngle = 90d - spec.TopChamferAngle;
        var slope = Math.Tan(semiAngle * Math.PI / 180d); var apex = -spec.HeadHeight - topRadius / slope;
        return new(apothem, circumradius, topRadius, semiAngle, apex, apex + apothem / slope,
            apex + circumradius / slope, spec.Length - spec.TipChamferLength);
    }

    public static KernelResult<ExactCoaxialConstructionPlan> Plan(HexBoltSpec spec, string stableId = "HexBolt")
    {
        var admission = Validate(spec);
        if (admission.Count > 0)
            return KernelResult<ExactCoaxialConstructionPlan>.Failure(admission.Select(d => new KernelDiagnostic(
                KernelDiagnosticCode.InvalidArgument, KernelDiagnosticSeverity.Error, d.Message,
                $"StandardLibrary.HexBolt.{d.Code}:{d.Field}")));

        var d = Derive(spec);
        var prism = new RegularPrismConstruction("head-prism", 6, spec.HeadAcrossFlats, 0d, d.TopConeCornerX);
        var trim = new ConePlanarTrimConstruction("head-cone-plane-trims", d.TopConeApexX, d.TopConeSemiAngleDegrees, -spec.HeadHeight, d.TopFlatRadius);
        var blend = new ConcaveFilletConstruction("under-head-root", spec.UnderHeadRadius,
            spec.NominalDiameter / 2d + spec.UnderHeadRadius, 0d, spec.UnderHeadRadius);
        var cylinder = new AxialCylinderConstruction("shank", spec.NominalDiameter / 2d, spec.UnderHeadRadius, d.TipChamferStartX);
        var frustum = new AxialFrustumConstruction("tip-chamfer", spec.NominalDiameter / 2d, spec.TipDiameter / 2d, d.TipChamferStartX, spec.Length);
        var topCap = new PlanarCapConstruction("top-cap", -spec.HeadHeight, d.TopFlatRadius, false);
        var endCap = new PlanarCapConstruction("end-cap", spec.Length, spec.TipDiameter / 2d, true);
        ExactConstructionNode[] sections = [prism, trim, topCap, blend, cylinder, frustum, endCap];
        ConstructionSemanticClaim[] claims =
        [
            new(stableId, ConstructionSemanticKind.Part),
            new(stableId + ".Head", ConstructionSemanticKind.Region, ParentStableId: stableId),
            new(stableId + ".Head.TopChamfer", ConstructionSemanticKind.Region, ParentStableId: stableId + ".Head"),
            new(stableId + ".Head.TopFlat", ConstructionSemanticKind.Face, "TopCap", stableId + ".Head"),
            new(stableId + ".Head.UnderHead", ConstructionSemanticKind.Face, "Shoulder", stableId + ".Head"),
            new(stableId + ".Shank", ConstructionSemanticKind.Region, ParentStableId: stableId),
            new(stableId + ".ThreadRegion", ConstructionSemanticKind.Region, ParentStableId: stableId,
                Metadata: $"{spec.ThreadDesignation};length={spec.ThreadLength:R}mm;material-geometry=Cylinder"),
            new(stableId + ".TipChamfer", ConstructionSemanticKind.Region, ParentStableId: stableId),
            new(stableId + ".TipFace", ConstructionSemanticKind.Face, "EndCap", stableId),
            new(stableId + ".Head.Side[{i}]", ConstructionSemanticKind.Face, "PrismSides", stableId + ".Head"),
            new(stableId + ".Head.TopChamfer.Face[{i}]", ConstructionSemanticKind.Face, "ConePlanarTrim", stableId + ".Head.TopChamfer"),
            new(stableId + ".Shank.Face[{i}]", ConstructionSemanticKind.Face, "Cylinder", stableId + ".Shank"),
            new(stableId + ".ThreadRegion.Face[{i}]", ConstructionSemanticKind.Face, "Cylinder", stableId + ".ThreadRegion"),
            new(stableId + ".TipChamfer.Face[{i}]", ConstructionSemanticKind.Face, "EndFrustum", stableId + ".TipChamfer"),
            new(stableId + ".Head.UnderHeadBlend.Face[{i}]", ConstructionSemanticKind.Face, "RootBlend", stableId + ".Head")
        ];
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["NominalDiameter"] = $"{spec.NominalDiameter:R}mm", ["ThreadLength"] = $"{spec.ThreadLength:R}mm",
            ["ThreadDesignation"] = spec.ThreadDesignation, ["PropertyClass"] = spec.PropertyClass,
            ["ThreadGeometry"] = "deferred-semantic-cylinder"
        };
        var signatureSource = string.Join("|", new[] { spec.NominalDiameter, spec.Length, spec.HeadAcrossFlats, spec.HeadHeight,
            spec.TopFlatDiameter, spec.TopChamferAngle, spec.TipChamferLength, spec.TipDiameter, spec.ThreadLength, spec.UnderHeadRadius }
            .Select(x => x.ToString("R", CultureInfo.InvariantCulture))) + $"|{spec.ThreadDesignation}|{spec.PropertyClass}";
        var signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signatureSource))).ToLowerInvariant();
        return KernelResult<ExactCoaxialConstructionPlan>.Success(new(stableId, prism, trim, blend, cylinder, frustum, topCap, endCap,
            new AxialSectionStackConstruction("coaxial-stack", sections), claims, metadata, signature));
    }

    public static HexBoltDefinition Wrap(HexBoltSpec spec, ExactCoaxialConstructionPlan plan, ExactConstructionResult result) =>
        new(spec, Derive(spec), result.Body, new(plan.StableId, result.Semantics, result.Metadata), result.DeterministicSignature);
}

public static class ExactConstructionMaterializer
{
    public static KernelResult<ExactConstructionResult> Materialize(ExactCoaxialConstructionPlan plan) =>
        CoaxialConstructionMaterializer.Materialize(plan);
}

/// <summary>Compatibility facade over the same generic plan and materializer used by Firmament.</summary>
public static class HexBoltBuilder
{
    public static IReadOnlyList<HexBoltAdmissionDiagnostic> Validate(HexBoltSpec spec) => HexBoltConstructionPlanner.Validate(spec);
    public static HexBoltDerivedDimensions Derive(HexBoltSpec spec) => HexBoltConstructionPlanner.Derive(spec);
    public static KernelResult<HexBoltDefinition> Create(HexBoltSpec spec, string bodyStableId = "HexBolt")
    {
        var plan = HexBoltConstructionPlanner.Plan(spec, bodyStableId);
        if (!plan.IsSuccess) return KernelResult<HexBoltDefinition>.Failure(plan.Diagnostics);
        var emitted = ExactConstructionMaterializer.Materialize(plan.Value);
        return emitted.IsSuccess
            ? KernelResult<HexBoltDefinition>.Success(HexBoltConstructionPlanner.Wrap(spec, plan.Value, emitted.Value))
            : KernelResult<HexBoltDefinition>.Failure(emitted.Diagnostics);
    }
}
