using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Judgment;

namespace Aetheris.Kernel.Firmament.Materializer;

public interface IHoleRecoveryVariant
{
    string Name { get; }
    HoleRecoveryVariantEvaluation Evaluate(FrepMaterializerContext context);
}

public sealed record HoleRecoveryVariantEvaluation(
    string VariantName,
    bool Admissible,
    double Score,
    HoleRecoveryPlan? Plan,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> RejectionReasons,
    IReadOnlyList<string> Diagnostics);

public sealed class HoleRecoveryPolicy : IFrepMaterializerPolicy
{
    private static readonly JudgmentEngine<FrepMaterializerContext> VariantEngine = new();
    private readonly IReadOnlyList<IHoleRecoveryVariant> _variants = [new ThroughHoleVariant(), new CounterboreVariant()];
    public string Name => nameof(HoleRecoveryPolicy);

    public FrepMaterializerPolicyEvaluation Evaluate(FrepMaterializerContext context)
    {
        var traces = _variants.Select(v => v.Evaluate(context)).ToArray();
        var diagnostics = new List<string> {$"{Name} evaluated.", $"Variants evaluated: {traces.Length}."};
        diagnostics.AddRange(traces.SelectMany(t => t.Diagnostics.Select(d => $"variant:{t.VariantName}:{d}")));

        var candidates = traces.Select((t, i) => new JudgmentCandidate<FrepMaterializerContext>(t.VariantName, _ => t.Admissible, _ => t.Score, _ => t.RejectionReasons.Count == 0 ? "variant rejected" : string.Join(" | ", t.RejectionReasons), i)).ToArray();
        var judgment = VariantEngine.Evaluate(context, candidates);
        if (!judgment.IsSuccess)
        {
            var reasons = traces.SelectMany(t => t.RejectionReasons.Select(r => $"{t.VariantName}:{r}")).ToArray();
            diagnostics.Add("No admissible hole variant.");
            return FrepMaterializerPolicyEvaluation.Rejected(Name, traces.SelectMany(t => t.Evidence).Append("semantic-hole-family").ToArray(), diagnostics, reasons);
        }

        var selectedName = judgment.Selection!.Value.Candidate.Name;
        var selected = traces.Single(t => t.VariantName == selectedName);
        var evidence = new List<string> { "semantic-hole-family", $"selected-variant:{selected.VariantName}" };
        evidence.AddRange(selected.Evidence);
        diagnostics.Add($"Selected hole variant: {selected.VariantName}.");
        diagnostics.Add("Hole recovery plan produced.");
        return FrepMaterializerPolicyEvaluation.Admitted(Name, selected.Score, FrepMaterializerCapability.ExactBRep, evidence, diagnostics, selected.Plan);
    }
}

internal sealed class ThroughHoleVariant : IHoleRecoveryVariant
{
    private const double Score = 1000d;
    public string Name => nameof(ThroughHoleVariant);

    public HoleRecoveryVariantEvaluation Evaluate(FrepMaterializerContext context)
    {
        var recognition = CirBoxCylinderRecognizer.Recognize(new CirBoxCylinderRecognizerInput(context.Root, context.ReplayLog, context.SourceLabel));
        if (!recognition.Success || recognition.Value is null)
        {
            return new(Name, false, 0d, null, ["through-hole", "rectangular-box-host"], [recognition.Diagnostic, $"recognizer-reason:{recognition.Reason}"], recognition.Diagnostics);
        }

        var r = recognition.Value;
        var supportsTranslation = r.BoxTranslation != Aetheris.Kernel.Core.Math.Vector3D.Zero || r.CylinderTranslation != Aetheris.Kernel.Core.Math.Vector3D.Zero;
        var evidence = new List<string> { "through-hole", "rectangular-box-host", "cylindrical-profile-segment", "strict-clearance", "z-axis", supportsTranslation ? "translation-wrapper-supported" : "translation-wrapper-not-required" };
        var plan = new HoleRecoveryPlan(HoleHostKind.RectangularBox, HoleAxisKind.Z, HoleKind.Through, HoleDepthKind.Through, HoleEntryFeatureKind.Plain, HoleExitFeatureKind.Plain, r.ThroughLength, r.BoxWidth, r.BoxHeight, r.BoxDepth, r.BoxTranslation, r.CylinderTranslation,
            [new(HoleProfileSegmentKind.Cylindrical, r.CylinderRadius, r.CylinderRadius, 0d, r.ThroughLength)],
            [new(HoleSurfacePatchRole.EntryFace, "Cylinder intersects host entry face with circular profile."), new(HoleSurfacePatchRole.ExitFace, "Cylinder intersects host exit face with circular profile."), new(HoleSurfacePatchRole.HostRetainedPlanarFaces, "Host planar faces are retained after through-hole subtraction."), new(HoleSurfacePatchRole.CylindricalWall, "One cylindrical inner wall patch is expected.")],
            [new(HoleTrimCurveRole.CircularRimTrim, "Circular trims on entry and exit rims are expected.")],
            FrepMaterializerCapability.ExactBRep,
            recognition.Diagnostics);
        evidence.Add(recognition.Reason == CirBoxCylinderRecognitionReason.ReplayMismatch ? "replay-diagnostic" : "replay-consistent");
        return new(Name, true, Score, plan, evidence, Array.Empty<string>(), recognition.Diagnostics);
    }
}


internal sealed class CounterboreVariant : IHoleRecoveryVariant
{
    private const double Score = 1100d;
    public string Name => nameof(CounterboreVariant);

    public HoleRecoveryVariantEvaluation Evaluate(FrepMaterializerContext context)
    {
        if (context.Root is not CirSubtractNode outerSubtract)
        {
            return new(Name, false, 0d, null, ["counterbore", "rectangular-box-host"], ["UnsupportedRootNotNestedSubtract"], ["Root must be nested subtract for counterbore."]);
        }

        if (outerSubtract.Left is not CirSubtractNode innerSubtract)
        {
            return new(Name, false, 0d, null, ["counterbore", "rectangular-box-host"], ["UnsupportedCounterboreShape"], ["Counterbore requires Subtract(Subtract(host,small),large) structure."]);
        }

        var hostRec = CirBoxCylinderRecognizer.Recognize(new CirBoxCylinderRecognizerInput(new CirSubtractNode(innerSubtract.Left, innerSubtract.Right), context.ReplayLog, context.SourceLabel));
        if (!hostRec.Success || hostRec.Value is null)
        {
            return new(Name, false, 0d, null, ["counterbore", "rectangular-box-host"], [hostRec.Diagnostic, $"small-recognizer-reason:{hostRec.Reason}"], hostRec.Diagnostics);
        }

        var shallowRec = CirBoxCylinderRecognizer.Recognize(new CirBoxCylinderRecognizerInput(new CirSubtractNode(innerSubtract.Left, outerSubtract.Right), context.ReplayLog, context.SourceLabel));
        if (shallowRec.Success)
        {
            return new(Name, false, 0d, null, ["counterbore", "rectangular-box-host"], ["UnsupportedLargeCylinderThroughFullDepth"], ["Large cylinder spans full host depth and is not a counterbore relief."]);
        }

        if (shallowRec.Reason is not CirBoxCylinderRecognitionReason.UnsupportedNotThroughHole)
        {
            return new(Name, false, 0d, null, ["counterbore", "rectangular-box-host"], [shallowRec.Diagnostic, $"large-recognizer-reason:{shallowRec.Reason}"], shallowRec.Diagnostics);
        }

        if (!TryUnwrapTranslation(outerSubtract.Right, out var largeNode, out var largeTranslation) || largeNode is not CirCylinderNode largeCylinder)
        {
            return new(Name, false, 0d, null, ["counterbore", "rectangular-box-host"], ["UnsupportedLargeTool"], ["Counterbore relief tool must be cylindrical with translation-only transform."]);
        }

        var host = hostRec.Value;
        var tol = Aetheris.Kernel.Core.Numerics.ToleranceContext.Default.Linear;
        if (Math.Abs(largeTranslation.X - host.CylinderTranslation.X) > tol || Math.Abs(largeTranslation.Y - host.CylinderTranslation.Y) > tol)
        {
            return new(Name, false, 0d, null, ["counterbore", "rectangular-box-host"], ["UnsupportedNonCoaxialCylinders"], ["Counterbore relief cylinder must be coaxial with through cylinder in XY."]);
        }

        if (largeCylinder.Radius <= host.CylinderRadius + tol)
        {
            return new(Name, false, 0d, null, ["counterbore", "rectangular-box-host"], ["UnsupportedLargeRadiusNotGreaterThanSmall"], ["Counterbore relief radius must be greater than through radius."]);
        }

        var boxMinZ = host.BoxTranslation.Z - (host.BoxDepth * 0.5d);
        var boxMaxZ = host.BoxTranslation.Z + (host.BoxDepth * 0.5d);
        var largeMinZ = largeTranslation.Z - (largeCylinder.Height * 0.5d);
        var largeMaxZ = largeTranslation.Z + (largeCylinder.Height * 0.5d);
        var touchesEntry = Math.Abs(largeMinZ - boxMinZ) <= tol || Math.Abs(largeMaxZ - boxMaxZ) <= tol;
        if (!touchesEntry)
        {
            return new(Name, false, 0d, null, ["counterbore", "rectangular-box-host"], ["UnsupportedMissingEntryFace"], ["Counterbore relief must enter from a host entry face."]);
        }

        var floorDepth = Math.Min(largeCylinder.Height, host.BoxDepth);
        var evidence = new List<string> { "counterbore", "rectangular-box-host", "coaxial-cylinders", "entry-relief", "through-core", "planned-exact-no-executor" };
        var plan = new HoleRecoveryPlan(
            HoleHostKind.RectangularBox,
            HoleAxisKind.Z,
            HoleKind.Counterbore,
            HoleDepthKind.ThroughWithEntryRelief,
            HoleEntryFeatureKind.Counterbore,
            HoleExitFeatureKind.Plain,
            host.ThroughLength,
            host.BoxWidth,
            host.BoxHeight,
            host.BoxDepth,
            host.BoxTranslation,
            host.CylinderTranslation,
            [
                new(HoleProfileSegmentKind.Cylindrical, largeCylinder.Radius, largeCylinder.Radius, 0d, floorDepth),
                new(HoleProfileSegmentKind.Cylindrical, host.CylinderRadius, host.CylinderRadius, 0d, host.ThroughLength)
            ],
            [
                new(HoleSurfacePatchRole.HostRetainedPlanarFaces, "Host planar faces are retained after counterbore subtraction."),
                new(HoleSurfacePatchRole.CounterboreFloorAnnulus, "Counterbore relief floor annulus is expected."),
                new(HoleSurfacePatchRole.CounterboreWall, "Larger-radius shallow cylindrical wall patch is expected."),
                new(HoleSurfacePatchRole.CylindricalWall, "Smaller-radius through cylindrical wall patch is expected.")
            ],
            [new(HoleTrimCurveRole.CircularRimTrim, "Circular trims at counterbore rim and through core transitions are expected.")],
            FrepMaterializerCapability.ExactBRep,
            ["Counterbore variant plan produced; executor support pending."]);

        return new(Name, true, Score, plan, evidence, Array.Empty<string>(), ["CounterboreVariant admitted canonical nested subtract pattern."]);
    }

    private static bool TryUnwrapTranslation(Aetheris.Kernel.Core.Cir.CirNode node, out Aetheris.Kernel.Core.Cir.CirNode unwrapped, out Aetheris.Kernel.Core.Math.Vector3D translation)
    {
        unwrapped = node;
        translation = Aetheris.Kernel.Core.Math.Vector3D.Zero;
        while (unwrapped is Aetheris.Kernel.Core.Cir.CirTransformNode transformNode)
        {
            var origin = transformNode.Transform.Apply(Aetheris.Kernel.Core.Math.Point3D.Origin);
            var x = transformNode.Transform.Apply(new Aetheris.Kernel.Core.Math.Point3D(1d, 0d, 0d));
            var y = transformNode.Transform.Apply(new Aetheris.Kernel.Core.Math.Point3D(0d, 1d, 0d));
            var z = transformNode.Transform.Apply(new Aetheris.Kernel.Core.Math.Point3D(0d, 0d, 1d));
            const double eps = 1e-9d;
            if (!NearlyEqual(x - origin, new Aetheris.Kernel.Core.Math.Vector3D(1d, 0d, 0d), eps)
                || !NearlyEqual(y - origin, new Aetheris.Kernel.Core.Math.Vector3D(0d, 1d, 0d), eps)
                || !NearlyEqual(z - origin, new Aetheris.Kernel.Core.Math.Vector3D(0d, 0d, 1d), eps))
            {
                unwrapped = node;
                translation = Aetheris.Kernel.Core.Math.Vector3D.Zero;
                return false;
            }

            translation += origin - Aetheris.Kernel.Core.Math.Point3D.Origin;
            unwrapped = transformNode.Child;
        }

        return true;
    }

    private static bool NearlyEqual(Aetheris.Kernel.Core.Math.Vector3D a, Aetheris.Kernel.Core.Math.Vector3D b, double eps)
        => Math.Abs(a.X - b.X) <= eps && Math.Abs(a.Y - b.Y) <= eps && Math.Abs(a.Z - b.Z) <= eps;
}

internal static class ThroughHoleRecoveryPlanAdapter
{
    internal static bool TryConvert(HoleRecoveryPlan plan, out ThroughHoleRecoveryPlan? through)
    {
        through = null;
        if (plan.HostKind != HoleHostKind.RectangularBox || plan.Axis != HoleAxisKind.Z || plan.HoleKind != HoleKind.Through || plan.DepthKind != HoleDepthKind.Through || plan.ProfileStack.Count != 1 || plan.ProfileStack[0].SegmentKind != HoleProfileSegmentKind.Cylindrical)
        {
            return false;
        }

        var seg = plan.ProfileStack[0];
        through = new ThroughHoleRecoveryPlan(ThroughHoleHostKind.RectangularBox, ThroughHoleToolKind.Cylindrical, ThroughHoleProfileKind.Circular, ThroughHoleAxisKind.Z, plan.ThroughLength, seg.RadiusStart, plan.HostSizeX, plan.HostSizeY, plan.HostSizeZ, plan.HostTranslation, plan.ToolTranslation,
            [new(ThroughHoleSurfaceRole.EntryFace, "Adapted from HoleRecoveryPlan."), new(ThroughHoleSurfaceRole.ExitFace, "Adapted from HoleRecoveryPlan.")],
            [new(ThroughHoleSurfaceRole.HostRetainedPlanarFaces, "Adapted from HoleRecoveryPlan."), new(ThroughHoleSurfaceRole.CylindricalWall, "Adapted from HoleRecoveryPlan.")],
            [new(ThroughHoleTrimRole.CircularRimTrim, "Adapted from HoleRecoveryPlan.")], plan.Capability, plan.Diagnostics);
        return true;
    }
}
