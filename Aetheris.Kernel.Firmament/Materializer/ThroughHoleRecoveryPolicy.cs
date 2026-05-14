namespace Aetheris.Kernel.Firmament.Materializer;

public sealed class ThroughHoleRecoveryPolicy : IFrepMaterializerPolicy
{
    private const double SemanticThroughHoleScore = 1000d;

    public string Name => nameof(ThroughHoleRecoveryPolicy);

    public FrepMaterializerPolicyEvaluation Evaluate(FrepMaterializerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var recognition = CirBoxCylinderRecognizer.Recognize(new CirBoxCylinderRecognizerInput(context.Root, context.ReplayLog, context.SourceLabel));
        if (!recognition.Success || recognition.Value is null)
        {
            return FrepMaterializerPolicyEvaluation.Rejected(
                Name,
                evidence: ["semantic-through-hole", $"recognizer-reason:{recognition.Reason}"],
                diagnostics: recognition.Diagnostics,
                rejectionReasons: [recognition.Diagnostic, $"recognizer-reason:{recognition.Reason}"]);
        }

        var recognized = recognition.Value;
        var evidence = new List<string>
        {
            "semantic-through-hole",
            "rectangular-box-host",
            "cylindrical-tool",
            "through-hole",
            "strict-clearance"
        };

        var supportsTranslation = recognized.BoxTranslation != Aetheris.Kernel.Core.Math.Vector3D.Zero
            || recognized.CylinderTranslation != Aetheris.Kernel.Core.Math.Vector3D.Zero;
        evidence.Add(supportsTranslation ? "translation-wrapper-supported" : "translation-wrapper-not-required");
        evidence.Add(recognition.Reason == CirBoxCylinderRecognitionReason.ReplayMismatch ? "replay-diagnostic" : "replay-consistent");
        evidence.Add("expected-patches:entry-exit-planar,cylindrical-wall");
        evidence.Add("expected-trims:circular-rim");

        var plan = new ThroughHoleRecoveryPlan(
            ThroughHoleHostKind.RectangularBox,
            ThroughHoleToolKind.Cylindrical,
            ThroughHoleProfileKind.Circular,
            ThroughHoleAxisKind.Z,
            recognized.ThroughLength,
            recognized.CylinderRadius,
            recognized.BoxWidth,
            recognized.BoxHeight,
            recognized.BoxDepth,
            recognized.BoxTranslation,
            recognized.CylinderTranslation,
            [
                new ThroughHoleSurfaceParticipation(ThroughHoleSurfaceRole.EntryFace, "Cylinder intersects host entry face with circular profile."),
                new ThroughHoleSurfaceParticipation(ThroughHoleSurfaceRole.ExitFace, "Cylinder intersects host exit face with circular profile.")
            ],
            [
                new ThroughHoleExpectedPatch(ThroughHoleSurfaceRole.HostRetainedPlanarFaces, "Host planar faces are retained after through-hole subtraction."),
                new ThroughHoleExpectedPatch(ThroughHoleSurfaceRole.CylindricalWall, "One cylindrical inner wall patch is expected.")
            ],
            [new ThroughHoleExpectedTrim(ThroughHoleTrimRole.CircularRimTrim, "Circular trims on entry and exit rims are expected.")],
            FrepMaterializerCapability.ExactBRep,
            recognition.Diagnostics);

        return FrepMaterializerPolicyEvaluation.Admitted(
            Name,
            SemanticThroughHoleScore,
            FrepMaterializerCapability.ExactBRep,
            evidence,
            recognition.Diagnostics,
            plan);
    }
}
