using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Air;

internal enum AirHoleEndConditionKind { ThroughAll, Depth }
internal enum AirHoleLoweringRouteKind { NotLowered, SimpleShaftProfileStackCandidate }

internal sealed record AirHoleFeature(
    string Name,
    string FeatureId,
    string? TargetBodyId,
    AirFaceLocalHolePlacement Placement,
    AirHoleAxis Axis,
    AirHoleShaft Shaft,
    AirHoleEndCondition EndCondition,
    AirProvenance Provenance,
    IReadOnlyList<AirDiagnostic> Diagnostics)
{
    public const string Milestone = "HOLE-X1";
    public const string UnsupportedCounterboreDiagnostic = "hole-x1-counterbore-deferred";
    public const string UnsupportedCountersinkDiagnostic = "hole-x1-countersink-deferred";
    public const string UnsupportedThreadDiagnostic = "hole-x1-thread-deferred";

    public bool IsValid => Diagnostics.All(d => d.Severity != AirDiagnosticSeverity.Error);

    public static AirHoleFeature CreateSimpleShaft(
        string name,
        string featureId,
        string? targetBodyId,
        AirFaceLocalHolePlacement placement,
        AirHoleAxis axis,
        AirHoleShaft shaft,
        AirHoleEndCondition endCondition,
        AirProvenance? provenance = null)
    {
        var diagnostics = Validate(name, featureId, placement, axis, shaft, endCondition).ToArray();
        return new AirHoleFeature(
            name,
            featureId,
            targetBodyId,
            placement,
            axis,
            shaft,
            endCondition,
            provenance ?? DefaultProvenance(name, featureId),
            diagnostics);
    }

    public AirHoleLoweringPlan CreateSimpleShaftLoweringPlan()
    {
        if (!IsValid)
        {
            return new AirHoleLoweringPlan(this, AirHoleLoweringRouteKind.NotLowered, false, "semantic-hole-invalid-before-lowering", Diagnostics);
        }

        return new AirHoleLoweringPlan(
            this,
            AirHoleLoweringRouteKind.SimpleShaftProfileStackCandidate,
            false,
            "hole-x1-preserves-semantic-intent; executable-profile-stack-lowering-deferred",
            [new AirDiagnostic("hole-x1-simple-shaft-lowering-deferred", AirDiagnosticSeverity.Info, "HOLE-X1 records a deterministic simple shaft lowering candidate but does not execute BRep/profile-stack materialization.")]);
    }

    private static AirProvenance DefaultProvenance(string name, string featureId) => new(
        Milestone,
        "Semantic hole AIR scaffold",
        name,
        featureId,
        nameof(AirHoleFeature),
        AirSelectionClass.None,
        AirRuleKind.None,
        "authored/semantic-hole-intent",
        true,
        ["Simple shaft hole only; throughAll and fixed depth end conditions are represented before lower geometry."]);

    private static IEnumerable<AirDiagnostic> Validate(string name, string featureId, AirFaceLocalHolePlacement placement, AirHoleAxis axis, AirHoleShaft shaft, AirHoleEndCondition endCondition)
    {
        if (string.IsNullOrWhiteSpace(name)) yield return Error("hole-x1-name-required", "Hole feature name is required.");
        if (string.IsNullOrWhiteSpace(featureId)) yield return Error("hole-x1-feature-id-required", "Hole feature id is required.");
        if (string.IsNullOrWhiteSpace(placement.EntryFaceName)) yield return Error("hole-x1-entry-face-required", "Face-local hole placement requires an entry face.");
        if (string.IsNullOrWhiteSpace(placement.FrameConvention)) yield return Error("hole-x1-placement-frame-required", "Face-local hole placement requires a frame convention.");
        if (!IsFinite(placement.U) || !IsFinite(placement.V)) yield return Error("hole-x1-placement-center-invalid", "Face-local hole center coordinates must be finite.");
        if (!IsFinite(axis.Direction.X) || !IsFinite(axis.Direction.Y) || !IsFinite(axis.Direction.Z)) yield return Error("hole-x1-axis-invalid", "Hole axis direction must be finite.");
        if (!IsFinite(shaft.Diameter) || shaft.Diameter <= 0d) yield return Error("hole-x1-diameter-invalid", "Simple shaft hole diameter must be greater than zero.");
        if (endCondition is AirHoleEndCondition.Depth depth && (!IsFinite(depth.Value) || depth.Value <= 0d)) yield return Error("hole-x1-depth-invalid", "Depth end condition must be greater than zero.");
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static AirDiagnostic Error(string code, string message) => new(code, AirDiagnosticSeverity.Error, message);
}

internal sealed record AirFaceLocalHolePlacement(string EntryFaceName, double U, double V, string FrameConvention, string? StableFaceSelector = null);
internal sealed record AirHoleAxis(Direction3D Direction, bool DefaultedFromEntryFaceNormal);
internal sealed record AirHoleShaft(double Diameter)
{
    public double Radius => Diameter / 2d;
}

internal abstract record AirHoleEndCondition(AirHoleEndConditionKind Kind)
{
    public sealed record ThroughAll() : AirHoleEndCondition(AirHoleEndConditionKind.ThroughAll);
    public sealed record Depth(double Value) : AirHoleEndCondition(AirHoleEndConditionKind.Depth);
}

internal sealed record AirHoleLoweringPlan(
    AirHoleFeature Feature,
    AirHoleLoweringRouteKind RouteKind,
    bool Executed,
    string Recommendation,
    IReadOnlyList<AirDiagnostic> Diagnostics);
