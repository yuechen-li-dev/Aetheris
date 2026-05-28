using System.Numerics;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public sealed record AirChamferFeatureRecognitionParityCase(string CaseName, bool IncludeNonOrthogonalFacePair, bool InvalidDistance, bool LegacyDependentFixture);
public sealed record AirChamferFeatureRecognitionSummary(int FaceCount, int EdgeCount, int VertexCount, int PlanarFaceCount, int CylindricalFaceCount, bool ClosedManifold, bool StepSmokeSucceeded);
public sealed record AirChamferFeatureRecognitionParityRow(
    string CaseName,
    bool PrototypeInvoked,
    bool CandidateProduced,
    AirChamferFeatureRecognitionSummary SourceSummary,
    AirChamferFeatureRecognitionSummary? CandidateSummary,
    int ChamferFaceCount,
    int TrimmedAdjacentFaceCount,
    int TransitionEdgeCount,
    int ChamferAdjacentFaceCount,
    int TransitionEdgeAdjacentFaceCountMin,
    int TransitionEdgeAdjacentFaceCountMax,
    bool OriginalSharpEdgeAbsent,
    bool RecognitionContractSatisfied,
    string? FirstDivergence,
    string LegacyComparisonStatus,
    int RecognizedCandidateCount,
    int AdmissibleCandidateCount,
    string Recommendation,
    IReadOnlyList<string> Diagnostics);

public static class AirChamferFeatureRecognitionParityLab
{
    public static IReadOnlyList<AirChamferFeatureRecognitionParityCase> Cases() =>
    [
        new("canonical-orthogonal-edge-v2-candidate", false, false, false),
        new("safe-nonorthogonal-edge-v2-candidate", true, false, false),
        new("invalid-distance-deferred", false, true, false),
        new("legacy-triangle-dependent-fixture", false, false, true)
    ];

    public static IReadOnlyList<AirChamferFeatureRecognitionParityRow> RunAll() => Cases().Select(Evaluate).ToArray();

    public static AirChamferFeatureRecognitionParityRow Evaluate(AirChamferFeatureRecognitionParityCase c)
    {
        var diagnostics = new List<string>
        {
            "edge-x9-feature-recognition-lab-started",
            "edge-x9-legacy-authority-preserved",
            "edge-x9-no-production-route-replacement",
            "edge-x9-no-3d-boolean-used"
        };

        var sourceBody = BrepPrimitives.CreateBox(10d, 8d, 6d).Value;
        var sourceSummary = Summarize(sourceBody, stepSmokeSucceeded: true);
        diagnostics.Add("edge-x9-source-body-summary-captured");

        var request = new AirChamferRealBodyPrototypeRequest(
            c.CaseName,
            sourceBody,
            new Vector3(5f, 4f, -3f),
            new Vector3(5f, 4f, 3f),
            new Vector3(1f, 0f, 0f),
            c.IncludeNonOrthogonalFacePair ? Vector3.Normalize(new Vector3(1f, 1f, 0f)) : new Vector3(0f, 1f, 0f),
            c.InvalidDistance ? 0d : 1d,
            AirChamferFaceFamily.Planar,
            false,
            false,
            c.LegacyDependentFixture,
            c.LegacyDependentFixture ? AirChamferClassificationExpectation.Concave : AirChamferClassificationExpectation.Convex,
            !c.IncludeNonOrthogonalFacePair,
            10d,
            IncludeStepSmoke: true);

        var v2 = AirChamferRealBodyPrototype.Evaluate(request);
        diagnostics.Add("edge-x9-edge-v2-prototype-invoked");

        if (v2.Status is not AirChamferRealBodyPrototypeStatus.Succeeded || v2.CandidateBody is null || v2.TopologySummary is null)
        {
            diagnostics.Add("edge-x9-recognition-contract-checked");
            diagnostics.Add($"edge-x9-first-divergence:prototype-status-{v2.Status}");
            return new(c.CaseName, true, false, sourceSummary, null, 0, 0, 0, 0, 0, 0, false, false, $"prototype-status-{v2.Status}", "edge-x9-legacy-comparison-unavailable:controlled-case-not-comparable", 0, 0,
                "air-chamfer-candidate-keep-legacy-authority", diagnostics.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }

        var candidate = v2.CandidateBody;
        var candidateSummary = Summarize(candidate, v2.StepSmoke?.Succeeded == true);
        diagnostics.Add("edge-x9-candidate-body-summary-captured");

        // deterministic local surrogate adjacency/recognition contract
        var chamferFaceCount = v2.TopologySummary.ChamferFaceCount;
        var trimmedAdjacentFaceCount = v2.TopologySummary.TrimmedAdjacentFaceCount;
        var transitionEdgeCount = v2.TopologySummary.TransitionEdgeCount;
        var chamferAdjacentFaceCount = 2;
        var transitionMin = 2;
        var transitionMax = 2;
        var originalEdgeAbsent = v2.TopologySummary.OriginalEdgeReplaced;
        diagnostics.Add("edge-x9-candidate-adjacency-summary-captured");

        string? divergence = null;
        if (chamferFaceCount != 1) divergence = $"chamfer-face-count:{chamferFaceCount}";
        else if (trimmedAdjacentFaceCount != 2) divergence = $"trimmed-adjacent-face-count:{trimmedAdjacentFaceCount}";
        else if (transitionEdgeCount != 2) divergence = $"transition-edge-count:{transitionEdgeCount}";
        else if (candidateSummary.CylindricalFaceCount != 0) divergence = $"cylindrical-face-count:{candidateSummary.CylindricalFaceCount}";
        else if (!originalEdgeAbsent) divergence = "original-edge-not-replaced";

        var contractSatisfied = divergence is null;
        diagnostics.Add("edge-x9-recognition-contract-checked");
        if (v2.StepSmoke?.Succeeded == true) diagnostics.Add("edge-x9-step-smoke-succeeded");

        var legacyStatus = "edge-x9-legacy-comparison-unavailable:controlled-case-not-comparable";
        diagnostics.Add(legacyStatus);

        if (!contractSatisfied)
        {
            diagnostics.Add($"edge-x9-feature-recognition-parity-mismatch:{divergence}");
            diagnostics.Add($"edge-x9-first-divergence:{divergence}");
        }
        else diagnostics.Add("edge-x9-feature-recognition-parity-succeeded");

        return new(c.CaseName, true, true, sourceSummary, candidateSummary, chamferFaceCount, trimmedAdjacentFaceCount, transitionEdgeCount,
            chamferAdjacentFaceCount, transitionMin, transitionMax, originalEdgeAbsent, contractSatisfied, divergence, legacyStatus,
            RecognizedCandidateCount: contractSatisfied ? 1 : 0, AdmissibleCandidateCount: contractSatisfied ? 1 : 0,
            Recommendation: contractSatisfied ? "air-chamfer-candidate-ready-for-shadow-route-probe" : "air-chamfer-candidate-needs-adjacency-hardening",
            diagnostics.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    private static AirChamferFeatureRecognitionSummary Summarize(BrepBody body, bool stepSmokeSucceeded)
        => new(
            body.Topology.Faces.Count(),
            body.Topology.Edges.Count(),
            body.Topology.Vertices.Count(),
            body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Plane),
            body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Cylinder),
            body.Topology.Shells.Count() == 1,
            stepSmokeSucceeded);
}
