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
    private readonly IReadOnlyList<IHoleRecoveryVariant> _variants = [new ThroughHoleVariant()];
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
