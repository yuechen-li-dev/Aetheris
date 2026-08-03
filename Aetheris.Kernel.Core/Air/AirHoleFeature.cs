using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Air;

internal enum AirHoleEndConditionKind { ThroughAll, Depth }
internal enum AirHoleLoweringRouteKind { NotLowered, SimpleShaftProfileStackCandidate, StackedProfileStackCandidate }
internal enum AirHoleStackKind { SimpleShaft, Counterbore, Countersink }
internal enum AirHoleStackComponentKind { Shaft, Counterbore, Countersink }

internal sealed record AirHoleFeature(
    string Name,
    string FeatureId,
    string? TargetBodyId,
    AirFaceLocalHolePlacement Placement,
    AirHoleAxis Axis,
    AirHoleShaft Shaft,
    AirHoleEndCondition EndCondition,
    AirHoleStack Stack,
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
            AirHoleStack.SimpleShaft(shaft, endCondition),
            provenance ?? DefaultProvenance(name, featureId),
            diagnostics);
    }


    public static AirHoleFeature CreateCounterbore(
        string name,
        string featureId,
        string? targetBodyId,
        AirFaceLocalHolePlacement placement,
        AirHoleAxis axis,
        AirHoleShaft shaft,
        AirHoleEndCondition endCondition,
        AirHoleCounterboreComponent counterbore,
        AirProvenance? provenance = null)
    {
        var stack = AirHoleStack.Counterbore(counterbore, new AirHoleShaftComponent(shaft.Diameter, endCondition));
        var diagnostics = Validate(name, featureId, placement, axis, shaft, endCondition).Concat(ValidateStack(stack, shaft, endCondition)).ToArray();
        return new AirHoleFeature(name, featureId, targetBodyId, placement, axis, shaft, endCondition, stack, provenance ?? DefaultProvenance(name, featureId), diagnostics);
    }

    public static AirHoleFeature CreateCountersink(
        string name,
        string featureId,
        string? targetBodyId,
        AirFaceLocalHolePlacement placement,
        AirHoleAxis axis,
        AirHoleShaft shaft,
        AirHoleEndCondition endCondition,
        AirHoleCountersinkComponent countersink,
        AirProvenance? provenance = null)
    {
        var stack = AirHoleStack.Countersink(countersink, new AirHoleShaftComponent(shaft.Diameter, endCondition));
        var diagnostics = Validate(name, featureId, placement, axis, shaft, endCondition).Concat(ValidateStack(stack, shaft, endCondition)).ToArray();
        return new AirHoleFeature(name, featureId, targetBodyId, placement, axis, shaft, endCondition, stack, provenance ?? DefaultProvenance(name, featureId), diagnostics);
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
        ["Semantic shaft/counterbore/countersink hole intent; throughAll and fixed depth end conditions are represented before lower geometry."]);

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

    private static IEnumerable<AirDiagnostic> ValidateStack(AirHoleStack stack, AirHoleShaft shaft, AirHoleEndCondition endCondition)
    {
        if (stack.Kind == AirHoleStackKind.Counterbore)
        {
            var cb = stack.Components.OfType<AirHoleCounterboreComponent>().Single();
            if (!IsFinite(cb.Diameter) || cb.Diameter <= shaft.Diameter) yield return Error("hole-x3-counterbore-diameter-invalid", "Counterbore diameter must be finite and greater than shaft diameter.");
            if (!IsFinite(cb.Depth) || cb.Depth <= 0d) yield return Error("hole-x3-counterbore-depth-invalid", "Counterbore depth must be finite and greater than zero.");
            if (endCondition is AirHoleEndCondition.Depth d && IsFinite(cb.Depth) && cb.Depth > d.Value) yield return Error("hole-x3-counterbore-depth-exceeds-shaft-span", "Counterbore depth must not exceed bounded shaft depth.");
        }
        if (stack.Kind == AirHoleStackKind.Countersink)
        {
            var cs = stack.Components.OfType<AirHoleCountersinkComponent>().Single();
            if (!IsFinite(cs.EntryDiameter) || cs.EntryDiameter <= shaft.Diameter) yield return Error("hole-x3-countersink-diameter-invalid", "Countersink entry diameter must be finite and greater than shaft diameter.");
            if (!IsFinite(cs.AngleDegrees) || cs.AngleDegrees <= 0d || cs.AngleDegrees >= 180d) yield return Error("hole-x3-countersink-angle-invalid", "Countersink angle must be finite and between 0 and 180 degrees.");
            var sinkDepth = cs.DerivedDepthForShaft(shaft);
            if (!IsFinite(sinkDepth) || sinkDepth <= 0d) yield return Error("hole-x3-countersink-depth-invalid", "Countersink derived depth must be finite and greater than zero.");
            if (IsFinite(sinkDepth) && endCondition is AirHoleEndCondition.Depth d && sinkDepth > d.Value) yield return Error("hole-x3-countersink-depth-exceeds-shaft-span", "Countersink derived depth must not exceed bounded shaft depth.");
        }
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static AirDiagnostic Error(string code, string message) => new(code, AirDiagnosticSeverity.Error, message);
}

internal sealed record AirResolvedPoint3PlacementSource(
    double X,
    double Y,
    double Z,
    string StableId,
    string SourceMember,
    int? Ordinal,
    string PlacementFace,
    double PlaneDistance,
    string SourceSpan);

internal sealed record AirFaceLocalHolePlacement(
    string EntryFaceName,
    double U,
    double V,
    string FrameConvention,
    string? StableFaceSelector = null,
    AirResolvedPoint3PlacementSource? ResolvedPoint3 = null);
internal sealed record AirHoleAxis(Direction3D Direction, bool DefaultedFromEntryFaceNormal);
internal sealed record AirHoleShaft(double Diameter)
{
    public double Radius => Diameter / 2d;
}

internal abstract record AirHoleStackComponent(AirHoleStackComponentKind Kind);
internal sealed record AirHoleShaftComponent(double Diameter, AirHoleEndCondition EndCondition) : AirHoleStackComponent(AirHoleStackComponentKind.Shaft)
{
    public double Radius => Diameter / 2d;
}
internal sealed record AirHoleCounterboreComponent(double Diameter, double Depth) : AirHoleStackComponent(AirHoleStackComponentKind.Counterbore)
{
    public double Radius => Diameter / 2d;
}
internal sealed record AirHoleCountersinkComponent(double EntryDiameter, double AngleDegrees) : AirHoleStackComponent(AirHoleStackComponentKind.Countersink)
{
    public double EntryRadius => EntryDiameter / 2d;
    public double DerivedDepthForShaft(AirHoleShaft shaft) => (EntryRadius - shaft.Radius) / System.Math.Tan((AngleDegrees / 2d) * System.Math.PI / 180d);
}
internal sealed record AirHoleStack(AirHoleStackKind Kind, IReadOnlyList<AirHoleStackComponent> Components)
{
    public static AirHoleStack SimpleShaft(AirHoleShaft shaft, AirHoleEndCondition endCondition) => new(AirHoleStackKind.SimpleShaft, [new AirHoleShaftComponent(shaft.Diameter, endCondition)]);
    public static AirHoleStack Counterbore(AirHoleCounterboreComponent counterbore, AirHoleShaftComponent shaft) => new(AirHoleStackKind.Counterbore, [counterbore, shaft]);
    public static AirHoleStack Countersink(AirHoleCountersinkComponent countersink, AirHoleShaftComponent shaft) => new(AirHoleStackKind.Countersink, [countersink, shaft]);
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
