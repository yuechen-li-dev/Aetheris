using System.Numerics;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public sealed record AirChamferStepArtifactMarkerSummary(
    bool HasIso,
    bool HasManifoldSolidBrep,
    bool HasAdvancedFace,
    bool HasPlane,
    bool HasCylindricalSurface,
    bool HasBrepWithVoids);

public sealed record AirChamferStepArtifactResult(
    bool Succeeded,
    string OutputPath,
    string ArtifactFileName,
    string Route,
    string CandidatePath,
    AirChamferShadowCandidateStatus ShadowCandidateStatus,
    AirChamferStepArtifactMarkerSummary MarkerSummary,
    AirChamferRealBodyTopologySummary? TopologySummary,
    IReadOnlyList<string> Diagnostics,
    string? Error);

public static class AirChamferStepArtifactLab
{
    public const string DefaultArtifactFileName = "edge-x10-airchamfer-cube-one-edge.step";
    public const string ExperimentalRoute = "experimental-cli-airchamfer-cube";
    public const string CandidatePath = "AirChamferShadowRoute->AirChamferRealBodyPrototype";

    public static AirChamferStepArtifactResult WriteControlledCubeOneEdgeStep(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path is required.", nameof(outputPath));
        }

        var fullPath = Path.GetFullPath(outputPath);
        var diagnostics = new List<string>
        {
            "edge-x10-airchamfer-step-artifact-started",
            "edge-x10-cli-export-path-used",
            "edge-x10-air-chamfer-shadow-route-invoked",
            "edge-x10-legacy-authority-preserved",
            "edge-x10-no-production-route-replacement",
            "edge-x10-no-3d-boolean-used"
        };

        var sourceBody = BrepPrimitives.CreateBox(10d, 8d, 6d).Value;
        var report = AirChamferShadowRoute.Evaluate(new AirChamferShadowRouteRequest(
            "edge-x10-controlled-cube-one-edge-step-artifact",
            sourceBody,
            new Vector3(5f, 4f, -3f),
            new Vector3(5f, 4f, 3f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 1f, 0f),
            1d,
            AirChamferFaceFamily.Planar,
            IsEdgeChain: false,
            IsCornerChain: false,
            LegacyDependency: false,
            AirChamferClassificationExpectation.Convex,
            IsOrthogonalFacePair: true,
            ReferenceEnvelope: 10d,
            IncludeStepSmoke: true));

        diagnostics.AddRange(report.Diagnostics);

        if (!report.ShadowCandidateProduced || report.ShadowCandidateBody is null)
        {
            diagnostics.Add($"edge-x10-candidate-body-not-created:{report.ShadowCandidateStatus}");
            return CreateFailure(fullPath, report.ShadowCandidateStatus, report.TopologySummary, diagnostics, "AirChamfer shadow route did not produce a candidate body.");
        }

        diagnostics.Add("edge-x10-candidate-body-created");
        var export = Step242Exporter.ExportBody(report.ShadowCandidateBody);
        if (!export.IsSuccess)
        {
            diagnostics.Add("edge-x10-step-artifact-write-failed:step-export");
            return CreateFailure(fullPath, report.ShadowCandidateStatus, report.TopologySummary, diagnostics, "STEP export failed for the AirChamfer candidate body.");
        }

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, export.Value);
        diagnostics.Add("edge-x10-step-artifact-written");

        var markers = SummarizeMarkers(export.Value);
        var smokeSucceeded = markers.HasIso
            && markers.HasManifoldSolidBrep
            && markers.HasAdvancedFace
            && markers.HasPlane
            && !markers.HasCylindricalSurface
            && !markers.HasBrepWithVoids;

        if (smokeSucceeded)
        {
            diagnostics.Add("edge-x10-step-smoke-succeeded");
        }
        else
        {
            diagnostics.Add("edge-x10-step-smoke-failed:marker-validation");
        }

        return new AirChamferStepArtifactResult(
            smokeSucceeded,
            fullPath,
            Path.GetFileName(fullPath),
            ExperimentalRoute,
            CandidatePath,
            report.ShadowCandidateStatus,
            markers,
            report.TopologySummary,
            diagnostics.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            smokeSucceeded ? null : "STEP marker validation failed for the AirChamfer candidate artifact.");
    }

    private static AirChamferStepArtifactResult CreateFailure(
        string fullPath,
        AirChamferShadowCandidateStatus status,
        AirChamferRealBodyTopologySummary? topology,
        List<string> diagnostics,
        string error)
        => new(
            false,
            fullPath,
            Path.GetFileName(fullPath),
            ExperimentalRoute,
            CandidatePath,
            status,
            new AirChamferStepArtifactMarkerSummary(false, false, false, false, false, false),
            topology,
            diagnostics.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            error);

    private static AirChamferStepArtifactMarkerSummary SummarizeMarkers(string stepText)
        => new(
            stepText.Contains("ISO-10303-21", StringComparison.Ordinal),
            stepText.Contains("MANIFOLD_SOLID_BREP", StringComparison.Ordinal),
            stepText.Contains("ADVANCED_FACE", StringComparison.Ordinal),
            stepText.Contains("PLANE", StringComparison.Ordinal),
            stepText.Contains("CYLINDRICAL_SURFACE", StringComparison.Ordinal),
            stepText.Contains("BREP_WITH_VOIDS", StringComparison.Ordinal));
}
