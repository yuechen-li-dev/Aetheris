using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;
using CorePrismatic = Aetheris.Kernel.Core.Brep.Prismatic;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public sealed record PrismaticCorpusGuarantees(
    bool NoProductionRouteReplacement,
    bool NoAirEdgeSweep,
    bool NoBrepBoundedChamfer,
    bool NoTopologyGraft,
    bool No3DBoolean,
    bool NoCoplanarMerge);

public sealed record PrismaticCorpusStepMarkerSummary(
    IReadOnlyList<string> RequiredPresent,
    IReadOnlyList<string> ForbiddenAbsent,
    bool RequiredMarkersPresent,
    bool ForbiddenMarkersAbsent,
    IReadOnlyList<string> PresentMarkers,
    IReadOnlyList<string> MissingRequiredMarkers,
    IReadOnlyList<string> AbsentMarkers,
    IReadOnlyList<string> UnexpectedPresentMarkers);

public sealed record PrismaticCorpusTopologySummary(
    bool BodyProduced,
    int SectionCount,
    int VertexCount,
    int EdgeCount,
    int FaceCount,
    int PlanarFaceCount,
    int CylindricalFaceCount,
    int TransitionFaceCount,
    int CapFaceCount,
    int LoopCount,
    int CoedgeCount,
    int? LowerPrismSideFaceCount,
    int? ChamferTransitionFaceCount,
    string Bounds);

public sealed record PrismaticCorpusCaseResult(
    string CaseName,
    string Status,
    string? ArtifactPath,
    string? ArtifactFileName,
    string Route,
    string TransitionRoute,
    string EmitterComponentName,
    string SplitPolicy,
    PrismaticCorpusTopologySummary TopologySummary,
    PrismaticCorpusStepMarkerSummary? StepMarkerSummary,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> Errors,
    PrismaticCorpusGuarantees Guarantees);

public sealed record PrismaticSectionTransitionCorpusResult(
    string Milestone,
    string OutputDirectory,
    string SummaryPath,
    string Route,
    string TransitionRoute,
    string EmitterComponentName,
    string SplitPolicy,
    IReadOnlyList<PrismaticCorpusCaseResult> Cases,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> Errors,
    PrismaticCorpusGuarantees Guarantees);

public static class PrismaticSectionTransitionCorpusLab
{
    public const string Milestone = "EDGE-PRISMATIC-X5";
    public const string DefaultSummaryFileName = "edge-prismatic-x5-corpus.json";
    public const string ExperimentalRoute = "experimental";
    public const string TransitionRoute = "prismatic-section-transition";
    public const string EmitterComponentName = "PrismaticSectionTransitionEmitter";
    public const string SplitPolicy = "preserve-section-splits";

    private static readonly string[] RequiredStepMarkers = ["ISO-10303-21", "MANIFOLD_SOLID_BREP", "ADVANCED_FACE", "PLANE"];
    private static readonly string[] ForbiddenStepMarkers = ["CYLINDRICAL_SURFACE", "BREP_WITH_VOIDS"];
    private static readonly PrismaticCorpusGuarantees DefaultGuarantees = new(
        NoProductionRouteReplacement: true,
        NoAirEdgeSweep: true,
        NoBrepBoundedChamfer: true,
        NoTopologyGraft: true,
        No3DBoolean: true,
        NoCoplanarMerge: true);

    public static PrismaticSectionTransitionCorpusResult WriteEdgePrismaticX5Corpus(string outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory is required.", nameof(outputDirectory));
        }

        var fullDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(fullDirectory);
        var diagnostics = new List<string>
        {
            "edge-prismatic-x5-corpus-started",
            "edge-prismatic-x5-no-production-route-replacement",
            "edge-prismatic-x5-no-air-edge-sweep-used",
            "edge-prismatic-x5-no-brep-bounded-chamfer-used",
            "edge-prismatic-x5-no-topology-graft-used",
            "edge-prismatic-x5-no-3d-boolean-used",
            "edge-prismatic-x5-no-coplanar-merge-used",
        };

        var cases = new List<PrismaticCorpusCaseResult>
        {
            RunPrismaticCase(fullDirectory, "rectangle-inset", "edge-prismatic-x5-rectangle-inset.step", RectangleSection(0, 10, 8), RectangleSection(1, 8, 6), PrismaticCorrespondenceMap.Identity(4)),
            RunTopEdgeChamferCase(fullDirectory),
            RunPrismaticCase(fullDirectory, "pentagon-scaled", "edge-prismatic-x5-pentagon-scaled.step", RegularPolygonSection(0, 5, 5), RegularPolygonSection(2, 4, 5), PrismaticCorrespondenceMap.Identity(5)),
            RunPrismaticCase(fullDirectory, "hexagon-scaled", "edge-prismatic-x5-hexagon-scaled.step", RegularPolygonSection(0, 6, 6), RegularPolygonSection(2, 4.5, 6), PrismaticCorrespondenceMap.Identity(6)),
            RunPrismaticCase(fullDirectory, "pentagon-asymmetric", "edge-prismatic-x5-pentagon-asymmetric.step",
                new PrismaticSection(0, [(-4, -2), (1, -3), (5, 0), (2, 3.5), (-3, 2.5)]),
                new PrismaticSection(2, [(-3.25, -2.35), (1.75, -3.35), (5.75, -0.35), (2.75, 3.15), (-2.25, 2.15)]),
                PrismaticCorrespondenceMap.Identity(5)),
            RunDiagnosticCase("mismatched-vertex-count", [RectangleSection(0, 10, 8), RegularPolygonSection(1, 5, 5)], PrismaticCorrespondenceMap.Identity(4)),
            RunDiagnosticCase("non-increasing-sections", [RectangleSection(0, 10, 8), RectangleSection(0, 8, 6)], PrismaticCorrespondenceMap.Identity(4)),
            RunDiagnosticCase("invalid-self-intersecting-profile", [new PrismaticSection(0, [(0, 0), (2, 2), (0, 2), (2, 0)]), RectangleSection(1, 8, 6)], PrismaticCorrespondenceMap.Identity(4)),
            RunDiagnosticCase("holes-deferred", [RectangleSection(0, 10, 8) with { HasHoles = true }, RectangleSection(1, 8, 6) with { HasHoles = true }], PrismaticCorrespondenceMap.Identity(4)),
            RunDiagnosticCase("arcs-deferred", [RectangleSection(0, 10, 8) with { HasArcs = true }, RectangleSection(1, 8, 6) with { HasArcs = true }], PrismaticCorrespondenceMap.Identity(4)),
            RunDiagnosticCase("multiple-loops-deferred", [RectangleSection(0, 10, 8) with { OuterLoopCount = 2 }, RectangleSection(1, 8, 6) with { OuterLoopCount = 2 }], PrismaticCorrespondenceMap.Identity(4)),
            RunDiagnosticCase("missing-correspondence", [RectangleSection(0, 10, 8), RectangleSection(1, 8, 6)], null),
            RunDiagnosticCase("non-identity-correspondence", [RectangleSection(0, 10, 8), RectangleSection(1, 8, 6)], new PrismaticCorrespondenceMap([1, 2, 3, 0])),
        };

        diagnostics.AddRange(cases.SelectMany(c => c.Diagnostics).Where(d => d.StartsWith("edge-prismatic-x5-", StringComparison.Ordinal)));
        diagnostics.Add("edge-prismatic-x5-json-summary-written");

        var summaryPath = Path.Combine(fullDirectory, DefaultSummaryFileName);
        var result = new PrismaticSectionTransitionCorpusResult(
            Milestone,
            fullDirectory,
            summaryPath,
            ExperimentalRoute,
            TransitionRoute,
            EmitterComponentName,
            SplitPolicy,
            cases,
            Stable(diagnostics),
            cases.SelectMany(c => c.Errors).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            DefaultGuarantees);

        File.WriteAllText(summaryPath, System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        }));

        return result;
    }

    private static PrismaticCorpusCaseResult RunPrismaticCase(string outputDirectory, string caseName, string artifactFileName, PrismaticSection bottom, PrismaticSection top, PrismaticCorrespondenceMap correspondence)
    {
        var diagnostics = CaseStartDiagnostics(caseName);
        diagnostics.Add($"edge-prismatic-x5-prismatic-emitter-invoked:{caseName}");
        var request = new CorePrismatic.PrismaticSectionTransitionRequest(
            [ToCore(bottom), ToCore(top)],
            new CorePrismatic.PrismaticCorrespondenceMap(correspondence.VertexMap),
            new CorePrismatic.PrismaticSectionTransitionOptions(RunStepSmoke: true, TraceLabel: caseName));
        var result = CorePrismatic.PrismaticSectionTransitionEmitter.Emit(request);
        diagnostics.AddRange(result.Diagnostics);

        if (result.Status != CorePrismatic.PrismaticSectionTransitionStatus.Succeeded || result.Body is null)
        {
            diagnostics.Add($"edge-prismatic-x5-case-failed:{caseName}:{result.Recommendation}");
            return CreateCase(caseName, "failed", null, null, ToCorpusTopology(result.Topology), null, diagnostics, [result.Recommendation]);
        }

        var export = Step242Exporter.ExportBody(result.Body);
        if (!export.IsSuccess || export.Value is null)
        {
            diagnostics.Add($"edge-prismatic-x5-case-failed:{caseName}:step-export");
            return CreateCase(caseName, "failed", null, null, ToCorpusTopology(result.Topology), null, diagnostics, ["step-export-failed"]);
        }

        var artifactPath = Path.Combine(outputDirectory, artifactFileName);
        File.WriteAllText(artifactPath, export.Value);
        diagnostics.Add($"edge-prismatic-x5-step-artifact-written:{caseName}");
        var step = SummarizeMarkers(export.Value);
        if (step.RequiredMarkersPresent && step.ForbiddenMarkersAbsent)
        {
            diagnostics.Add($"edge-prismatic-x5-step-smoke-succeeded:{caseName}");
        }

        if (ValidateSplitTopology(result.Topology, bottom.OuterLoop.Count, 2))
        {
            diagnostics.Add($"edge-prismatic-x5-split-preserving-topology-validated:{caseName}");
        }

        return CreateCase(caseName, "succeeded", artifactPath, artifactFileName, ToCorpusTopology(result.Topology), step, diagnostics, []);
    }

    private static PrismaticCorpusCaseResult RunTopEdgeChamferCase(string outputDirectory)
    {
        const string caseName = "top-edge-chamfer";
        const string artifactFileName = "edge-prismatic-x5-top-edge-chamfer.step";
        var diagnostics = CaseStartDiagnostics(caseName);
        diagnostics.Add($"edge-prismatic-x5-prismatic-emitter-invoked:{caseName}");
        var row = PrismaticTopEdgeChamferLab.Run(new PrismaticTopEdgeChamferCase("canonical-top-pos-x-edge", 10, 8, 6, 1));
        diagnostics.AddRange(row.Diagnostics);
        var emitted = PrismaticTopEdgeChamferLab.TryEmitBody(new PrismaticTopEdgeChamferCase("canonical-top-pos-x-edge", 10, 8, 6, 1));
        if (!row.Succeeded || emitted.Body is null)
        {
            diagnostics.Add($"edge-prismatic-x5-case-failed:{caseName}:{row.Recommendation}");
            return CreateCase(caseName, "failed", null, null, ToCorpusTopology(row.Topology), null, diagnostics, [row.Recommendation]);
        }

        var export = Step242Exporter.ExportBody(emitted.Body);
        if (!export.IsSuccess || export.Value is null)
        {
            diagnostics.Add($"edge-prismatic-x5-case-failed:{caseName}:step-export");
            return CreateCase(caseName, "failed", null, null, ToCorpusTopology(row.Topology), null, diagnostics, ["step-export-failed"]);
        }

        var artifactPath = Path.Combine(outputDirectory, artifactFileName);
        File.WriteAllText(artifactPath, export.Value);
        diagnostics.Add($"edge-prismatic-x5-step-artifact-written:{caseName}");
        var step = SummarizeMarkers(export.Value);
        if (step.RequiredMarkersPresent && step.ForbiddenMarkersAbsent)
        {
            diagnostics.Add($"edge-prismatic-x5-step-smoke-succeeded:{caseName}");
        }

        if (row.Topology.VertexCount == 12 && row.Topology.EdgeCount == 20 && row.Topology.FaceCount == 10 && row.Topology.PlanarFaceCount == 10 && row.Topology.CylindricalFaceCount == 0 && row.Topology.TransitionFaceCount == 4 && row.Topology.ChamferTransitionFaceCount == 1 && row.Topology.LoopCount == 10 && row.Topology.CoedgeCount == 40)
        {
            diagnostics.Add($"edge-prismatic-x5-split-preserving-topology-validated:{caseName}");
        }

        return CreateCase(caseName, "succeeded", artifactPath, artifactFileName, ToCorpusTopology(row.Topology), step, diagnostics, []);
    }

    private static PrismaticCorpusCaseResult RunDiagnosticCase(string caseName, IReadOnlyList<PrismaticSection> sections, PrismaticCorrespondenceMap? correspondence)
    {
        var diagnostics = CaseStartDiagnostics(caseName);
        var request = new CorePrismatic.PrismaticSectionTransitionRequest(
            sections.Select(ToCore).ToArray(),
            correspondence is null ? null : new CorePrismatic.PrismaticCorrespondenceMap(correspondence.VertexMap),
            new CorePrismatic.PrismaticSectionTransitionOptions(RunStepSmoke: true, TraceLabel: caseName));
        var result = CorePrismatic.PrismaticSectionTransitionEmitter.Emit(request);
        diagnostics.AddRange(result.Diagnostics);
        var status = result.Status switch
        {
            CorePrismatic.PrismaticSectionTransitionStatus.Deferred => "deferred",
            CorePrismatic.PrismaticSectionTransitionStatus.Rejected => "rejected",
            CorePrismatic.PrismaticSectionTransitionStatus.Succeeded => "failed",
            _ => "failed",
        };
        var reason = ReasonFromDiagnostics(result.Diagnostics, result.Recommendation);
        diagnostics.Add(status == "deferred"
            ? $"edge-prismatic-x5-case-deferred:{caseName}:{reason}"
            : $"edge-prismatic-x5-case-rejected:{caseName}:{reason}");

        return CreateCase(caseName, status, null, null, ToCorpusTopology(result.Topology), null, diagnostics, status == "failed" ? [$"unexpected-status:{result.Status}"] : []);
    }

    private static PrismaticCorpusCaseResult CreateCase(
        string caseName,
        string status,
        string? artifactPath,
        string? artifactFileName,
        PrismaticCorpusTopologySummary topology,
        PrismaticCorpusStepMarkerSummary? step,
        IEnumerable<string> diagnostics,
        IReadOnlyList<string> errors) =>
        new(
            caseName,
            status,
            artifactPath,
            artifactFileName,
            ExperimentalRoute,
            TransitionRoute,
            EmitterComponentName,
            SplitPolicy,
            topology,
            step,
            Stable(diagnostics.Concat(GuaranteeDiagnostics())),
            errors,
            DefaultGuarantees);

    private static List<string> CaseStartDiagnostics(string caseName) =>
    [
        $"edge-prismatic-x5-case-started:{caseName}",
        "edge-prismatic-x5-no-production-route-replacement",
        "edge-prismatic-x5-no-air-edge-sweep-used",
        "edge-prismatic-x5-no-brep-bounded-chamfer-used",
        "edge-prismatic-x5-no-topology-graft-used",
        "edge-prismatic-x5-no-3d-boolean-used",
        "edge-prismatic-x5-no-coplanar-merge-used",
    ];

    private static IEnumerable<string> GuaranteeDiagnostics() =>
    [
        "edge-prismatic-x5-no-production-route-replacement",
        "edge-prismatic-x5-no-air-edge-sweep-used",
        "edge-prismatic-x5-no-brep-bounded-chamfer-used",
        "edge-prismatic-x5-no-topology-graft-used",
        "edge-prismatic-x5-no-3d-boolean-used",
        "edge-prismatic-x5-no-coplanar-merge-used",
    ];

    private static bool ValidateSplitTopology(CorePrismatic.PrismaticTransitionTopologySummary topology, int n, int sectionCount) =>
        topology.SectionCount == sectionCount
        && topology.VertexCount == sectionCount * n
        && topology.EdgeCount == (sectionCount * n) + ((sectionCount - 1) * n)
        && topology.FaceCount == 2 + ((sectionCount - 1) * n)
        && topology.PlanarFaceCount == topology.FaceCount
        && topology.CylindricalFaceCount == 0
        && topology.TransitionFaceCount == (sectionCount - 1) * n
        && topology.CapFaceCount == 2
        && topology.LoopCount == topology.FaceCount
        && topology.CoedgeCount == (2 * n) + (4 * (sectionCount - 1) * n);

    private static PrismaticCorpusStepMarkerSummary SummarizeMarkers(string stepText)
    {
        var present = RequiredStepMarkers.Where(m => stepText.Contains(m, StringComparison.Ordinal)).ToArray();
        var missing = RequiredStepMarkers.Except(present, StringComparer.Ordinal).ToArray();
        var absent = ForbiddenStepMarkers.Where(m => !stepText.Contains(m, StringComparison.Ordinal)).ToArray();
        var unexpected = ForbiddenStepMarkers.Where(m => stepText.Contains(m, StringComparison.Ordinal)).ToArray();
        return new(RequiredStepMarkers, ForbiddenStepMarkers, missing.Length == 0, unexpected.Length == 0, present, missing, absent, unexpected);
    }

    private static PrismaticCorpusTopologySummary ToCorpusTopology(CorePrismatic.PrismaticTransitionTopologySummary t) => new(
        t.BodyProduced,
        t.SectionCount,
        t.VertexCount,
        t.EdgeCount,
        t.FaceCount,
        t.PlanarFaceCount,
        t.CylindricalFaceCount,
        t.TransitionFaceCount,
        t.CapFaceCount,
        t.LoopCount,
        t.CoedgeCount,
        LowerPrismSideFaceCount: null,
        ChamferTransitionFaceCount: null,
        t.Bounds);

    private static PrismaticCorpusTopologySummary ToCorpusTopology(PrismaticTopEdgeChamferTopologySummary t) => new(
        t.BodyProduced,
        SectionCount: 3,
        t.VertexCount,
        t.EdgeCount,
        t.FaceCount,
        t.PlanarFaceCount,
        t.CylindricalFaceCount,
        t.TransitionFaceCount,
        CapFaceCount: 2,
        t.LoopCount,
        t.CoedgeCount,
        t.LowerPrismSideFaceCount,
        t.ChamferTransitionFaceCount,
        t.Bounds);

    private static string ReasonFromDiagnostics(IReadOnlyList<string> diagnostics, string recommendation)
    {
        var requestDiagnostic = diagnostics.FirstOrDefault(d => d.StartsWith("edge-prismatic-v1-request-rejected:", StringComparison.Ordinal) || d.StartsWith("edge-prismatic-v1-request-deferred:", StringComparison.Ordinal));
        if (requestDiagnostic is not null)
        {
            return requestDiagnostic[(requestDiagnostic.LastIndexOf(':') + 1)..];
        }

        return recommendation;
    }

    private static CorePrismatic.PrismaticSection ToCore(PrismaticSection section) => new(section.Z, section.OuterLoop, section.HasHoles, section.HasArcs, section.OuterLoopCount);

    private static PrismaticSection RectangleSection(double z, double width, double depth)
    {
        var x = width * 0.5d;
        var y = depth * 0.5d;
        return new(z, [(-x, -y), (x, -y), (x, y), (-x, y)]);
    }

    private static PrismaticSection RegularPolygonSection(double z, double radius, int vertices)
    {
        var points = Enumerable.Range(0, vertices)
            .Select(i =>
            {
                var a = ((Math.PI * 2d) * i / vertices) - (Math.PI * 0.5d);
                return (X: Math.Cos(a) * radius, Y: Math.Sin(a) * radius);
            })
            .ToArray();
        return new(z, points);
    }

    private static IReadOnlyList<string> Stable(IEnumerable<string> diagnostics) => diagnostics.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
}
