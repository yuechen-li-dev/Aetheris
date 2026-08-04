using Aetheris.Kernel.Core.Air.BRepPlan;

namespace Aetheris.Kernel.Core.Air;

internal enum ChamferCornerConvexity { Convex, Concave, Unknown }
internal enum ChamferCornerMaterialSide { RetainInterior, RetainExterior, Ambiguous }
internal enum ChamferCornerRule { UniformEqualDistance, Asymmetric, NonUniform }
internal enum ChamferCornerTopologyKind { OpenChain, ClosedLoop, Junction }
internal enum ChamferCornerSourceProvenance { GeneratedHistoryKnown, RecoveredTopology, ExplicitConceptWitness }
internal enum ChamferCornerSupportSurfaceFamily { Plane, Cylinder, Cone, Other }

// These are the only policies for which this repository contains either a modern exact
// construction or a concrete legacy geometry experiment. They are deliberately not a
// catalogue of every corner treatment a CAD kernel might someday support.
internal enum ChamferCornerPolicy
{
    SectionTransitionJunction,
    PlanarEdgePairCut,
    PlanarTriangularCut,
    ExplicitWitness,
}

internal enum ChamferCornerSelectionMode { Direct, Utility, Error }

internal enum ChamferCornerAdmissionFailure
{
    None,
    UnsupportedValence,
    UnsupportedSurfaceCombination,
    UnsupportedChamferRule,
    UnsupportedHistory,
    InvalidMaterialSide,
    DistanceNotAdmissible,
    SelfIntersection,
    OpenReplacementRegion,
    MissingRetainedRegion,
    MissingReplacementRegion,
    NonManifoldTopology,
    ExactConstructionUnavailable,
    MissingAuthoritativeBRepPlan,
    PolicyDoesNotMatchClassification,
}

internal sealed record ChamferCornerConstructionWitness(
    string WitnessId,
    string ConstructionKind,
    ChamferCornerPolicy Policy,
    AirBRepPlan Plan,
    ChamferCornerSourceProvenance Provenance);

/// <summary>
/// Construction-facing facts for one selected-edge junction. IDs are AIR/construction
/// identities, never raw B-rep topology IDs and never Firmament syntax.
/// </summary>
internal sealed record ChamferCornerContext(
    string CornerId,
    ChamferCornerConvexity Convexity,
    int IncidentSelectedEdgeCount,
    int VertexValence,
    IReadOnlyList<ChamferCornerSupportSurfaceFamily> SupportSurfaces,
    ChamferCornerMaterialSide MaterialSide,
    ChamferCornerRule Rule,
    ChamferCornerTopologyKind TopologyKind,
    bool HasConstructionHistory,
    bool IsSymmetric,
    ChamferCornerSourceProvenance SourceProvenance,
    bool DistanceAdmissible,
    bool NonSelfIntersecting,
    bool HasClosedReplacementRegion,
    bool HasRetainedRegionOwnership,
    bool HasReplacementRegionOwnership,
    bool PreservesManifoldTopology,
    bool ExactConstructionAvailable,
    ChamferCornerConstructionWitness? AvailableWitness = null);

internal sealed record ChamferCornerCandidateEvidence(
    ChamferCornerPolicy Policy,
    bool Admitted,
    ChamferCornerAdmissionFailure Failure,
    string Reason,
    double? UtilityScore = null,
    IReadOnlyDictionary<string, double>? UtilityConsiderations = null);

internal sealed record ChamferCornerConstruction(
    ChamferCornerPolicy Policy,
    ChamferCornerSelectionMode SelectionMode,
    IReadOnlyList<ChamferCornerCandidateEvidence> Candidates,
    ChamferCornerConstructionWitness Witness,
    ChamferCornerSourceProvenance Provenance);

/// <summary>
/// Exhaustive structural classification plus hard geometric admission. No utility engine
/// is called because the currently proven modern fixture set never leaves two admitted,
/// authoritative constructions for the same context.
/// </summary>
internal static class ChamferCornerPolicyResolver
{
    public static ChamferLoweringResult<ChamferCornerConstruction> Resolve(ChamferCornerContext context)
    {
        var policies = EnumeratePolicies(context);
        if (policies.Count == 0)
            return Error(ChamferLoweringErrorKind.NoCandidatePolicy, "chamfer-corner-no-candidate-policy", "No corner policy matches the typed corner classification.", context, []);

        var evidence = policies.Select(policy => Admit(context, policy)).ToArray();
        var admitted = evidence.Where(candidate => candidate.Admitted).ToArray();
        if (admitted.Length == 0)
            return Error(ErrorKind(evidence), ErrorCode(evidence), "No candidate has an exact, hard-valid Construction AIR/BRepPlan witness.", context, evidence);

        if (admitted.Length > 1)
            return Error(ChamferLoweringErrorKind.AmbiguousWithoutPreference, "chamfer-corner-ambiguous-without-preference", "Several exact corner constructions are admitted, but no evidence-backed preference model is registered.", context, evidence);

        var selected = admitted[0];
        var witness = context.AvailableWitness!;
        return ChamferLoweringResult<ChamferCornerConstruction>.Ok(new(
            selected.Policy,
            ChamferCornerSelectionMode.Direct,
            evidence,
            witness,
            witness.Provenance));
    }

    private static IReadOnlyList<ChamferCornerPolicy> EnumeratePolicies(ChamferCornerContext context)
    {
        if (context.SourceProvenance == ChamferCornerSourceProvenance.ExplicitConceptWitness)
            return [ChamferCornerPolicy.ExplicitWitness];

        return (context.TopologyKind, context.Convexity, context.IncidentSelectedEdgeCount, context.VertexValence) switch
        {
            (ChamferCornerTopologyKind.ClosedLoop, ChamferCornerConvexity.Convex, 2, 3) => [ChamferCornerPolicy.SectionTransitionJunction],
            (ChamferCornerTopologyKind.Junction, ChamferCornerConvexity.Convex, 2, 3) => [ChamferCornerPolicy.PlanarEdgePairCut],
            (ChamferCornerTopologyKind.Junction, ChamferCornerConvexity.Convex, 3, 3) => [ChamferCornerPolicy.PlanarTriangularCut],
            (ChamferCornerTopologyKind.Junction, ChamferCornerConvexity.Concave, _, _) => [ChamferCornerPolicy.ExplicitWitness],
            _ => [],
        };
    }

    private static ChamferCornerCandidateEvidence Admit(ChamferCornerContext context, ChamferCornerPolicy policy)
    {
        ChamferCornerCandidateEvidence Reject(ChamferCornerAdmissionFailure failure, string reason) => new(policy, false, failure, reason);

        if (context.VertexValence is < 2 or > 3)
            return Reject(ChamferCornerAdmissionFailure.UnsupportedValence, "Only bounded valence-two loop corners and valence-three junction experiments exist.");
        if (context.SupportSurfaces.Count == 0 || context.SupportSurfaces.Any(surface => surface != ChamferCornerSupportSurfaceFamily.Plane))
            return Reject(ChamferCornerAdmissionFailure.UnsupportedSurfaceCombination, "The current corner evidence is planar-only.");
        if (context.Rule != ChamferCornerRule.UniformEqualDistance)
            return Reject(ChamferCornerAdmissionFailure.UnsupportedChamferRule, "No asymmetric or nonuniform exact corner construction is registered.");
        if (policy != ChamferCornerPolicy.ExplicitWitness && !context.HasConstructionHistory)
            return Reject(ChamferCornerAdmissionFailure.UnsupportedHistory, "Automatic corner construction currently requires generated construction history.");
        if (policy == ChamferCornerPolicy.SectionTransitionJunction && !context.IsSymmetric)
            return Reject(ChamferCornerAdmissionFailure.PolicyDoesNotMatchClassification, "The section-transition control fixture is symmetric.");
        if (context.MaterialSide == ChamferCornerMaterialSide.Ambiguous)
            return Reject(ChamferCornerAdmissionFailure.InvalidMaterialSide, "Material side must be established before policy selection.");
        if (!context.DistanceAdmissible)
            return Reject(ChamferCornerAdmissionFailure.DistanceNotAdmissible, "Chamfer distance violates the local feature envelope.");
        if (!context.NonSelfIntersecting)
            return Reject(ChamferCornerAdmissionFailure.SelfIntersection, "Candidate replacement intersects itself or retained material.");
        if (!context.HasClosedReplacementRegion)
            return Reject(ChamferCornerAdmissionFailure.OpenReplacementRegion, "Replacement ownership does not form a closed region.");
        if (!context.HasRetainedRegionOwnership)
            return Reject(ChamferCornerAdmissionFailure.MissingRetainedRegion, "Retained face-region ownership is missing.");
        if (!context.HasReplacementRegionOwnership)
            return Reject(ChamferCornerAdmissionFailure.MissingReplacementRegion, "Replacement face-region ownership is missing.");
        if (!context.PreservesManifoldTopology)
            return Reject(ChamferCornerAdmissionFailure.NonManifoldTopology, "The bounded topology invariant is not proven.");
        if (!context.ExactConstructionAvailable)
            return Reject(ChamferCornerAdmissionFailure.ExactConstructionUnavailable, "No exact construction is available for this policy.");
        if (context.AvailableWitness is null || context.AvailableWitness.Policy != policy || context.AvailableWitness.Plan is null)
            return Reject(ChamferCornerAdmissionFailure.MissingAuthoritativeBRepPlan, "A concrete authoritative BRepPlan witness is required; legacy direct-BRep output is insufficient.");

        return new(policy, true, ChamferCornerAdmissionFailure.None, "All hard invariants and the authoritative BRepPlan witness are present.");
    }

    private static ChamferLoweringErrorKind ErrorKind(IReadOnlyList<ChamferCornerCandidateEvidence> evidence)
    {
        var failures = evidence.Select(candidate => candidate.Failure).ToHashSet();
        if (failures.Contains(ChamferCornerAdmissionFailure.UnsupportedValence)) return ChamferLoweringErrorKind.UnsupportedValence;
        if (failures.Contains(ChamferCornerAdmissionFailure.UnsupportedSurfaceCombination)) return ChamferLoweringErrorKind.UnsupportedSurfaceCombination;
        if (failures.Contains(ChamferCornerAdmissionFailure.UnsupportedChamferRule)) return ChamferLoweringErrorKind.UnsupportedSelection;
        if (failures.Contains(ChamferCornerAdmissionFailure.UnsupportedHistory)) return ChamferLoweringErrorKind.UnsupportedHistory;
        if (failures.Contains(ChamferCornerAdmissionFailure.InvalidMaterialSide)) return ChamferLoweringErrorKind.InvalidMaterialSide;
        if (failures.Contains(ChamferCornerAdmissionFailure.SelfIntersection)) return ChamferLoweringErrorKind.SelfIntersection;
        if (failures.Contains(ChamferCornerAdmissionFailure.MissingRetainedRegion)) return ChamferLoweringErrorKind.MissingRetainedRegion;
        if (failures.Contains(ChamferCornerAdmissionFailure.MissingReplacementRegion) || failures.Contains(ChamferCornerAdmissionFailure.OpenReplacementRegion)) return ChamferLoweringErrorKind.MissingReplacementRegion;
        return ChamferLoweringErrorKind.ConstructionWitnessRequired;
    }

    private static string ErrorCode(IReadOnlyList<ChamferCornerCandidateEvidence> evidence) =>
        evidence.Any(candidate => candidate.Failure == ChamferCornerAdmissionFailure.MissingAuthoritativeBRepPlan)
            ? "chamfer-corner-construction-witness-required:authoritative-brep-plan"
            : $"chamfer-corner-hard-admission-rejected:{evidence[0].Failure}";

    private static ChamferLoweringResult<ChamferCornerConstruction> Error(
        ChamferLoweringErrorKind kind,
        string code,
        string message,
        ChamferCornerContext context,
        IReadOnlyList<ChamferCornerCandidateEvidence> evidence) =>
        ChamferLoweringResult<ChamferCornerConstruction>.Err(new(
            kind,
            code,
            message,
            "FeatureAIR->CornerPolicy->ConstructionAIR",
            [
                $"corner={context.CornerId}",
                $"classification={context.Convexity}/{context.TopologyKind}/selected:{context.IncidentSelectedEdgeCount}/valence:{context.VertexValence}",
                .. evidence.Select(candidate => $"candidate={candidate.Policy};admitted={candidate.Admitted};failure={candidate.Failure};reason={candidate.Reason}"),
            ]));
}
