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
    private readonly IReadOnlyList<IHoleRecoveryVariant> _variants = [new ThroughHoleVariant(), new BlindHoleVariant(), new CounterboreVariant(), new CountersinkVariant()];
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

internal sealed class CountersinkVariant : IHoleRecoveryVariant
{
    private const double Score = 1050d;
    public string Name => nameof(CountersinkVariant);

    public HoleRecoveryVariantEvaluation Evaluate(FrepMaterializerContext context)
    {
        var diagnostics = new List<string>
        {
            "CountersinkVariant evaluated."
        };

        if (context.Root is not CirSubtractNode outer || outer.Left is not CirSubtractNode inner)
        {
            diagnostics.Add("not countersink shape: requires Subtract(Subtract(Box,Cylinder),Cone). ");
            return new(Name, false, 0d, null, ["countersink", "rectangular-box-host"], ["UnsupportedCountersinkShape"], diagnostics);
        }

        var hostRec = CirBoxCylinderRecognizer.Recognize(new CirBoxCylinderRecognizerInput(new CirSubtractNode(inner.Left, inner.Right), context.ReplayLog, context.SourceLabel));
        if (!hostRec.Success || hostRec.Value is null)
        {
            return new(Name, false, 0d, null, ["countersink", "rectangular-box-host"], [hostRec.Diagnostic, $"host-recognizer-reason:{hostRec.Reason}"], hostRec.Diagnostics);
        }

        if (!CounterboreVariant.TryUnwrapTranslation(outer.Right, out var coneNode, out var coneTranslation) || coneNode is not CirConeNode cone)
        {
            diagnostics.Add("missing cone primitive / unexpected tool kind.");
            return new(Name, false, 0d, null, ["countersink", "rectangular-box-host"], ["UnsupportedMissingConePrimitiveOrTransform"], diagnostics);
        }
        diagnostics.Add("cone primitive detected.");

        var host = hostRec.Value;
        var tol = Aetheris.Kernel.Core.Numerics.ToleranceContext.Default.Linear;
        if (Math.Abs(coneTranslation.X - host.CylinderTranslation.X) > tol || Math.Abs(coneTranslation.Y - host.CylinderTranslation.Y) > tol)
        {
            diagnostics.Add("cone/cylinder not coaxial in XY.");
            return new(Name, false, 0d, null, ["countersink", "rectangular-box-host"], ["UnsupportedNonCoaxialConeCylinder"], diagnostics);
        }
        diagnostics.Add("cone/cylinder coaxial in XY.");

        var boxMinZ = host.BoxTranslation.Z - (host.BoxDepth * 0.5d);
        var boxMaxZ = host.BoxTranslation.Z + (host.BoxDepth * 0.5d);
        var coneMinZ = coneTranslation.Z - (cone.Height * 0.5d);
        var coneMaxZ = coneTranslation.Z + (cone.Height * 0.5d);
        var touchesTop = Math.Abs(coneMaxZ - boxMaxZ) <= tol;
        var touchesBottom = Math.Abs(coneMinZ - boxMinZ) <= tol;
        if (!touchesTop && !touchesBottom)
        {
            diagnostics.Add("cone does not touch entry face.");
            return new(Name, false, 0d, null, ["countersink", "rectangular-box-host"], ["UnsupportedConeMissingEntryFace"], diagnostics);
        }

        if (touchesTop && touchesBottom)
        {
            diagnostics.Add("cone through full depth unsupported.");
            return new(Name, false, 0d, null, ["countersink", "rectangular-box-host"], ["UnsupportedConeThroughFullDepth"], diagnostics);
        }

        var entryFromTop = touchesTop;
        var entryRadius = entryFromTop ? cone.TopRadius : cone.BottomRadius;
        var transitionRadius = entryFromTop ? cone.BottomRadius : cone.TopRadius;
        if (entryRadius <= transitionRadius + tol)
        {
            diagnostics.Add("cone radius ordering invalid for entry side.");
            return new(Name, false, 0d, null, ["countersink", "rectangular-box-host"], ["UnsupportedConeRadiusOrderingInvalid"], diagnostics);
        }

        if (Math.Abs(transitionRadius - host.CylinderRadius) > tol)
        {
            diagnostics.Add("cone transition radius incompatible with cylinder radius.");
            return new(Name, false, 0d, null, ["countersink", "rectangular-box-host"], ["UnsupportedTransitionRadiusMismatch"], diagnostics);
        }

        var coneDepth = cone.Height;
        if (coneDepth >= host.BoxDepth - tol)
        {
            diagnostics.Add("cone through full depth.");
            return new(Name, false, 0d, null, ["countersink", "rectangular-box-host"], ["UnsupportedConeThroughFullDepth"], diagnostics);
        }

        var halfW = host.BoxWidth * 0.5d;
        var halfH = host.BoxHeight * 0.5d;
        var dx = coneTranslation.X - host.BoxTranslation.X;
        var dy = coneTranslation.Y - host.BoxTranslation.Y;
        if ((dx + halfW - entryRadius) <= tol || (halfW - dx - entryRadius) <= tol || (dy + halfH - entryRadius) <= tol || (halfH - dy - entryRadius) <= tol)
        {
            diagnostics.Add("cone max radius tangent/grazing/oversized.");
            return new(Name, false, 0d, null, ["countersink", "rectangular-box-host"], ["UnsupportedConeClearance"], diagnostics);
        }

        var depthKind = hostRec.Value.ThroughLength >= host.BoxDepth - tol ? HoleDepthKind.ThroughWithEntryRelief : HoleDepthKind.BlindWithEntryRelief;
        var exitKind = depthKind == HoleDepthKind.ThroughWithEntryRelief ? HoleExitFeatureKind.Plain : HoleExitFeatureKind.ClosedBottom;
        diagnostics.Add($"entry side detected: {(entryFromTop ? "top(+Z)" : "bottom(-Z)")}.");
        diagnostics.Add("cone radius/order validated.");
        diagnostics.Add("transition radius compatible.");
        diagnostics.Add($"countersink depth computed: {coneDepth:R}.");
        diagnostics.Add("Countersink plan produced.");

        var plan = new HoleRecoveryPlan(HoleHostKind.RectangularBox, HoleAxisKind.Z, HoleKind.Countersink, depthKind, HoleEntryFeatureKind.Countersink, exitKind,
            host.ThroughLength, host.BoxWidth, host.BoxHeight, host.BoxDepth, host.BoxTranslation, host.CylinderTranslation,
            [new(HoleProfileSegmentKind.Conical, entryRadius, transitionRadius, 0d, coneDepth), new(HoleProfileSegmentKind.Cylindrical, host.CylinderRadius, host.CylinderRadius, 0d, host.ThroughLength)],
            [new(HoleSurfacePatchRole.HostRetainedPlanarFaces, "Host planar faces are retained after countersink subtraction."), new(HoleSurfacePatchRole.CountersinkWall, "Conical countersink wall patch is expected."), new(HoleSurfacePatchRole.CylindricalWall, "Cylindrical continuation wall patch is expected.")],
            [new(HoleTrimCurveRole.CircularRimTrim, "Circular entry/transition trims are expected.")], FrepMaterializerCapability.ExactBRep, diagnostics.ToArray());

        return new(
            Name,
            true,
            Score,
            plan,
            ["countersink", "rectangular-box-host", "cone-primitive", "coaxial-cone-cylinder", "entry-relief", "semantic-profile-stack"],
            Array.Empty<string>(),
            diagnostics);
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




internal sealed class BlindHoleVariant : IHoleRecoveryVariant
{
    private const double Score = 900d;
    public string Name => nameof(BlindHoleVariant);

    public HoleRecoveryVariantEvaluation Evaluate(FrepMaterializerContext context)
    {
        var recognition = CirBoxCylinderRecognizer.Recognize(new CirBoxCylinderRecognizerInput(context.Root, context.ReplayLog, context.SourceLabel));
        if (recognition.Success)
        {
            var throughDiagnostics = new List<string>(recognition.Diagnostics)
            {
                "Blind-hole variant evaluated and rejected: recognizer admitted through-hole span.",
                "through-hole rejected as not blind."
            };
            return new(Name, false, 0d, null, ["blind-hole", "rectangular-box-host"], ["UnsupportedThroughHole", "NotBlindThroughSpan"], throughDiagnostics);
        }

        if (recognition.Reason is not CirBoxCylinderRecognitionReason.UnsupportedNotThroughHole)
        {
            return new(Name, false, 0d, null, ["blind-hole", "rectangular-box-host"], [recognition.Diagnostic, $"recognizer-reason:{recognition.Reason}"], recognition.Diagnostics);
        }

        if (!TryBuildBlindPlan(context.Root, out var plan, out var diagnostics, out var rejection))
        {
            return new(Name, false, 0d, null, ["blind-hole", "rectangular-box-host"], [rejection], diagnostics);
        }

        var evidence = new List<string> { "blind-hole", "rectangular-box-host", "cylindrical-profile-segment", "strict-clearance", "z-axis", "closed-bottom" };
        return new(Name, true, Score, plan, evidence, Array.Empty<string>(), diagnostics);
    }

    private static bool TryBuildBlindPlan(CirNode root, out HoleRecoveryPlan plan, out List<string> diagnostics, out string rejection)
    {
        diagnostics = ["BlindHoleVariant evaluated."];
        rejection = string.Empty;
        plan = null!;
        if (root is not CirSubtractNode subtract)
        {
            rejection = "UnsupportedRootNotSubtract";
            diagnostics.Add("Blind-hole requires Subtract(Box, Cylinder). ");
            return false;
        }

        if (subtract.Left is CirSubtractNode or CirUnionNode or CirIntersectNode || subtract.Right is CirSubtractNode or CirUnionNode or CirIntersectNode)
        {
            rejection = "UnsupportedNestedOrComposite";
            diagnostics.Add("Nested/composite booleans are unsupported for blind-hole V8.");
            return false;
        }

        if (!CounterboreVariant.TryUnwrapTranslation(subtract.Left, out var left, out var hostT) || left is not CirBoxNode box)
        {
            rejection = "UnsupportedHostNotBoxOrTransform";
            diagnostics.Add("Blind-hole host must be box with translation-only transform.");
            diagnostics.Add("unsupported transform rejected.");
            return false;
        }

        if (!CounterboreVariant.TryUnwrapTranslation(subtract.Right, out var right, out var toolT) || right is not CirCylinderNode cyl)
        {
            rejection = "UnsupportedToolNotCylinderOrTransform";
            diagnostics.Add("Blind-hole tool must be cylinder with translation-only transform.");
            diagnostics.Add("unsupported transform rejected.");
            return false;
        }

        var tol = Aetheris.Kernel.Core.Numerics.ToleranceContext.Default.Linear;
        var boxMinZ = hostT.Z - (box.Depth * 0.5d);
        var boxMaxZ = hostT.Z + (box.Depth * 0.5d);
        var cylMinZ = toolT.Z - (cyl.Height * 0.5d);
        var cylMaxZ = toolT.Z + (cyl.Height * 0.5d);

        var entersTop = Math.Abs(cylMaxZ - boxMaxZ) <= tol;
        var entersBottom = Math.Abs(cylMinZ - boxMinZ) <= tol;
        diagnostics.Add($"translated geometry normalized: hostT=({hostT.X:R},{hostT.Y:R},{hostT.Z:R}), toolT=({toolT.X:R},{toolT.Y:R},{toolT.Z:R}).");
        if (!entersTop && !entersBottom)
        {
            rejection = "UnsupportedMissingEntryFace";
            diagnostics.Add("Blind-hole cylinder must intersect exactly one entry face.");
            diagnostics.Add("entry face not reached; blind-hole rejected.");
            return false;
        }

        if (entersTop && entersBottom)
        {
            rejection = "UnsupportedThroughFullDepth";
            diagnostics.Add("Blind-hole cylinder intersects both entry faces (through-hole). ");
            diagnostics.Add("through-hole rejected as not blind.");
            return false;
        }

        var bottomInside = entersTop ? (cylMinZ > boxMinZ + tol) : (cylMaxZ < boxMaxZ - tol);
        if (!bottomInside)
        {
            rejection = "UnsupportedBottomOutsideHost";
            diagnostics.Add("Blind-hole bottom cap is outside host bounds.");
            diagnostics.Add("opposite face reached or exceeded; blind-hole rejected as near-through or through.");
            return false;
        }

        var halfW = box.Width * 0.5d;
        var halfH = box.Height * 0.5d;
        var dx = toolT.X - hostT.X;
        var dy = toolT.Y - hostT.Y;
        var cxm = dx + halfW - cyl.Radius;
        var cxp = halfW - dx - cyl.Radius;
        var cym = dy + halfH - cyl.Radius;
        var cyp = halfH - dy - cyl.Radius;
        if (cxm <= tol || cxp <= tol || cym <= tol || cyp <= tol)
        {
            rejection = "UnsupportedTangentOrOutsideClearance";
            diagnostics.Add("Blind-hole cylinder radius must satisfy strict XY clearance.");
            diagnostics.Add("tangent/grazing radius rejected.");
            return false;
        }

        var depth = entersTop ? (boxMaxZ - cylMinZ) : (cylMaxZ - boxMinZ);
        diagnostics.Add($"Blind entry face detected: {(entersTop ? "top(+Z)" : "bottom(-Z)")}.");
        diagnostics.Add("entry face reached.");
        diagnostics.Add("blind bottom inside host.");
        diagnostics.Add("opposite face not reached.");
        diagnostics.Add($"Blind depth computed: {depth:R}.");
        diagnostics.Add("Blind-hole plan produced.");

        plan = new HoleRecoveryPlan(HoleHostKind.RectangularBox, HoleAxisKind.Z, HoleKind.Blind, HoleDepthKind.Blind, HoleEntryFeatureKind.Plain, HoleExitFeatureKind.ClosedBottom,
            depth, box.Width, box.Height, box.Depth, hostT, toolT,
            [new(HoleProfileSegmentKind.Cylindrical, cyl.Radius, cyl.Radius, 0d, depth)],
            [new(HoleSurfacePatchRole.EntryFace, "Cylinder intersects one host entry face with circular profile."),
             new(HoleSurfacePatchRole.HostRetainedPlanarFaces, "Host planar faces are retained after blind-hole subtraction."),
             new(HoleSurfacePatchRole.CylindricalWall, "One cylindrical blind wall patch is expected."),
             new(HoleSurfacePatchRole.BlindBottomCap, "One circular blind bottom cap patch is expected.")],
            [new(HoleTrimCurveRole.CircularRimTrim, "Circular trims on entry rim and blind bottom are expected.")],
            FrepMaterializerCapability.ExactBRep,
            diagnostics.ToArray());
        return true;
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

    internal static bool TryUnwrapTranslation(Aetheris.Kernel.Core.Cir.CirNode node, out Aetheris.Kernel.Core.Cir.CirNode unwrapped, out Aetheris.Kernel.Core.Math.Vector3D translation)
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
