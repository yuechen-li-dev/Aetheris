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


public sealed record AirChamferStepMarkerExpectationSummary(
    IReadOnlyList<string> RequiredPresent,
    IReadOnlyList<string> ForbiddenAbsent,
    bool RequiredPresentSatisfied,
    bool ForbiddenAbsentSatisfied,
    AirChamferStepArtifactMarkerSummary Markers);

public sealed record AirChamferStepCorpusCaseResult(
    string CaseName,
    string Status,
    string? ArtifactPath,
    string? ArtifactFileName,
    string CandidatePath,
    string Route,
    AirChamferStepMarkerExpectationSummary? StepMarkerSummary,
    AirChamferRealBodyTopologySummary? TopologySummary,
    IReadOnlyList<string> Diagnostics,
    bool LegacyAuthorityPreserved,
    bool ProductionOutputChanged,
    bool NoProductionRouteReplacement,
    bool No3DBooleanUsed,
    IReadOnlyList<string> Errors);

public sealed record AirChamferStepCorpusResult(
    string CorpusVersion,
    string Milestone,
    string OutputDirectory,
    string SummaryPath,
    string CandidatePath,
    string Route,
    bool LegacyAuthorityPreserved,
    bool ProductionOutputChanged,
    bool NoProductionRouteReplacement,
    bool No3DBooleanUsed,
    IReadOnlyList<AirChamferStepCorpusCaseResult> Cases,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> Errors);

public static class AirChamferStepArtifactLab
{
    public const string DefaultArtifactFileName = "edge-x10-airchamfer-cube-one-edge.step";
    public const string DefaultCorpusSummaryFileName = "edge-x11-airchamfer-corpus.json";
    public const string ExperimentalRoute = "experimental-cli-airchamfer-cube";
    public const string ExperimentalCorpusRoute = "experimental-cli-airchamfer-corpus";
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


    public static AirChamferStepCorpusResult WriteEdgeX11Corpus(string outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory is required.", nameof(outputDirectory));
        }

        var fullDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(fullDirectory);

        var diagnostics = new List<string>
        {
            "edge-x11-airchamfer-corpus-started",
            "edge-x11-legacy-authority-preserved",
            "edge-x11-no-production-route-replacement",
            "edge-x11-no-3d-boolean-used"
        };

        var cases = new[]
        {
            RunCorpusCase(
                fullDirectory,
                "canonical",
                "edge-x11-airchamfer-cube-canonical.step",
                1d,
                invalidEdge: false,
                missingFace: false,
                faceFamily: AirChamferFaceFamily.Planar,
                edgeChain: false,
                cornerChain: false,
                legacyDependency: false,
                nonOrthogonal: false),
            RunCorpusCase(
                fullDirectory,
                "nonorthogonal",
                "edge-x11-airchamfer-cube-nonorthogonal.step",
                1d,
                invalidEdge: false,
                missingFace: false,
                faceFamily: AirChamferFaceFamily.Planar,
                edgeChain: false,
                cornerChain: false,
                legacyDependency: false,
                nonOrthogonal: true),
            RunCorpusCase(
                fullDirectory,
                "invalid-distance",
                artifactFileName: null,
                distance: 0d,
                invalidEdge: false,
                missingFace: false,
                faceFamily: AirChamferFaceFamily.Planar,
                edgeChain: false,
                cornerChain: false,
                legacyDependency: false,
                nonOrthogonal: false),
            RunCorpusCase(
                fullDirectory,
                "triangle-legacy-dependent",
                artifactFileName: null,
                distance: 1d,
                invalidEdge: false,
                missingFace: false,
                faceFamily: AirChamferFaceFamily.Planar,
                edgeChain: false,
                cornerChain: false,
                legacyDependency: true,
                nonOrthogonal: false)
        };

        foreach (var c in cases)
        {
            diagnostics.AddRange(c.Diagnostics.Where(d => d.StartsWith("edge-x11-", StringComparison.Ordinal)));
        }

        diagnostics.Add("edge-x11-json-summary-written");
        var summaryPath = Path.Combine(fullDirectory, DefaultCorpusSummaryFileName);
        var result = new AirChamferStepCorpusResult(
            "EDGE-X11",
            "EDGE-X11",
            fullDirectory,
            summaryPath,
            CandidatePath,
            ExperimentalCorpusRoute,
            LegacyAuthorityPreserved: true,
            ProductionOutputChanged: false,
            NoProductionRouteReplacement: true,
            No3DBooleanUsed: true,
            cases,
            diagnostics.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            cases.SelectMany(c => c.Errors).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray());

        File.WriteAllText(summaryPath, System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        }));

        return result;
    }

    private static AirChamferStepCorpusCaseResult RunCorpusCase(
        string outputDirectory,
        string caseName,
        string? artifactFileName,
        double distance,
        bool invalidEdge,
        bool missingFace,
        AirChamferFaceFamily faceFamily,
        bool edgeChain,
        bool cornerChain,
        bool legacyDependency,
        bool nonOrthogonal)
    {
        var diagnostics = new List<string>
        {
            $"edge-x11-case-started:{caseName}",
            "edge-x11-legacy-authority-preserved",
            "edge-x11-no-production-route-replacement",
            "edge-x11-no-3d-boolean-used"
        };

        var sourceBody = BrepPrimitives.CreateBox(10d, 8d, 6d).Value;
        var edgeStart = new Vector3(5f, 4f, -3f);
        var edgeEnd = invalidEdge ? edgeStart : new Vector3(5f, 4f, 3f);
        Vector3? faceA = new(1f, 0f, 0f);
        Vector3? faceB = missingFace ? null : (nonOrthogonal ? Vector3.Normalize(new Vector3(1f, 1f, 0f)) : new Vector3(0f, 1f, 0f));
        var classification = edgeChain || cornerChain || legacyDependency
            ? AirChamferClassificationExpectation.Concave
            : AirChamferClassificationExpectation.Convex;

        var report = AirChamferShadowRoute.Evaluate(new AirChamferShadowRouteRequest(
            $"edge-x11-{caseName}",
            sourceBody,
            edgeStart,
            edgeEnd,
            faceA,
            faceB,
            distance,
            faceFamily,
            edgeChain,
            cornerChain,
            legacyDependency,
            classification,
            !nonOrthogonal,
            ReferenceEnvelope: 10d,
            IncludeStepSmoke: true));

        diagnostics.AddRange(report.Diagnostics);

        if (!report.ShadowCandidateProduced || report.ShadowCandidateBody is null)
        {
            var status = report.ShadowCandidateStatus is AirChamferShadowCandidateStatus.Deferred or AirChamferShadowCandidateStatus.FallbackLegacy
                ? "deferred"
                : report.ShadowCandidateStatus is AirChamferShadowCandidateStatus.Rejected
                    ? "rejected"
                    : "failed";
            var reason = report.ShadowCandidateStatus is AirChamferShadowCandidateStatus.FallbackLegacy
                ? "legacy-dependent-fallback"
                : report.AirChamferDecision;
            diagnostics.Add(status == "rejected"
                ? $"edge-x11-case-rejected:{caseName}:{reason}"
                : $"edge-x11-case-deferred:{caseName}:{reason}");

            return new AirChamferStepCorpusCaseResult(
                caseName,
                status,
                ArtifactPath: null,
                ArtifactFileName: null,
                CandidatePath,
                ExperimentalCorpusRoute,
                StepMarkerSummary: null,
                report.TopologySummary,
                diagnostics.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                LegacyAuthorityPreserved: true,
                ProductionOutputChanged: false,
                NoProductionRouteReplacement: true,
                No3DBooleanUsed: true,
                Array.Empty<string>());
        }

        if (string.IsNullOrWhiteSpace(artifactFileName))
        {
            diagnostics.Add($"edge-x11-case-deferred:{caseName}:no-artifact-filename");
            return new AirChamferStepCorpusCaseResult(
                caseName,
                "deferred",
                ArtifactPath: null,
                ArtifactFileName: null,
                CandidatePath,
                ExperimentalCorpusRoute,
                StepMarkerSummary: null,
                report.TopologySummary,
                diagnostics.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                LegacyAuthorityPreserved: true,
                ProductionOutputChanged: false,
                NoProductionRouteReplacement: true,
                No3DBooleanUsed: true,
                Array.Empty<string>());
        }

        var export = Step242Exporter.ExportBody(report.ShadowCandidateBody);
        if (!export.IsSuccess)
        {
            diagnostics.Add($"edge-x11-case-deferred:{caseName}:step-export-failed");
            return new AirChamferStepCorpusCaseResult(
                caseName,
                "failed",
                ArtifactPath: null,
                ArtifactFileName: null,
                CandidatePath,
                ExperimentalCorpusRoute,
                StepMarkerSummary: null,
                report.TopologySummary,
                diagnostics.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                LegacyAuthorityPreserved: true,
                ProductionOutputChanged: false,
                NoProductionRouteReplacement: true,
                No3DBooleanUsed: true,
                new[] { "step-export-failed" });
        }

        var fullPath = Path.Combine(outputDirectory, artifactFileName);
        File.WriteAllText(fullPath, export.Value);
        diagnostics.Add($"edge-x11-step-artifact-written:{caseName}");

        var markers = SummarizeMarkers(export.Value);
        var stepSummary = CreateExpectationSummary(markers);
        if (stepSummary.RequiredPresentSatisfied && stepSummary.ForbiddenAbsentSatisfied)
        {
            diagnostics.Add($"edge-x11-step-smoke-succeeded:{caseName}");
        }
        else
        {
            diagnostics.Add($"edge-x11-step-smoke-failed:{caseName}:marker-validation");
        }

        return new AirChamferStepCorpusCaseResult(
            caseName,
            stepSummary.RequiredPresentSatisfied && stepSummary.ForbiddenAbsentSatisfied ? "succeeded" : "failed",
            fullPath,
            artifactFileName,
            CandidatePath,
            ExperimentalCorpusRoute,
            stepSummary,
            report.TopologySummary,
            diagnostics.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            LegacyAuthorityPreserved: true,
            ProductionOutputChanged: false,
            NoProductionRouteReplacement: true,
            No3DBooleanUsed: true,
            stepSummary.RequiredPresentSatisfied && stepSummary.ForbiddenAbsentSatisfied ? Array.Empty<string>() : new[] { "step-marker-validation-failed" });
    }

    private static AirChamferStepMarkerExpectationSummary CreateExpectationSummary(AirChamferStepArtifactMarkerSummary markers)
        => new(
            new[] { "ISO-10303-21", "MANIFOLD_SOLID_BREP", "ADVANCED_FACE", "PLANE" },
            new[] { "CYLINDRICAL_SURFACE", "BREP_WITH_VOIDS" },
            markers.HasIso && markers.HasManifoldSolidBrep && markers.HasAdvancedFace && markers.HasPlane,
            !markers.HasCylindricalSurface && !markers.HasBrepWithVoids,
            markers);

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
