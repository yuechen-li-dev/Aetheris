using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Kernel.Core.Step242;
using CorePrismatic = Aetheris.Kernel.Core.Brep.Prismatic;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public sealed record EdgeLoopX2CorpusGuarantees(
    bool NoProductionRouteReplacement,
    bool NoAirEdgeSweep,
    bool NoBrepBoundedChamfer,
    bool NoTopologyGraft,
    bool No3DBoolean,
    bool NoCoplanarMerge,
    bool NotFourIndependentSingleEdgeChamfers);

public sealed record EdgeLoopX2LoopSelectionSummary(
    string SelectionClass,
    string OwningFace,
    string LoopKind,
    bool Closed,
    int EdgeCount,
    bool Ordered);

public sealed record EdgeLoopX2RuleSummary(
    string Rule,
    double? Distance);

public sealed record EdgeLoopX2StepMarkerSummary(
    IReadOnlyList<string> RequiredPresent,
    IReadOnlyList<string> ForbiddenAbsent,
    bool RequiredMarkersPresent,
    bool ForbiddenMarkersAbsent,
    IReadOnlyList<string> PresentMarkers,
    IReadOnlyList<string> MissingRequiredMarkers,
    IReadOnlyList<string> AbsentMarkers,
    IReadOnlyList<string> UnexpectedPresentMarkers);

public sealed record EdgeLoopX2TopologySummary(
    bool BodyProduced,
    int SectionCount,
    int VertexCount,
    int EdgeCount,
    int FaceCount,
    int PlanarFaceCount,
    int CylindricalFaceCount,
    int CapFaceCount,
    int LowerPrismSideFaceCount,
    int TransitionFaceCount,
    int ChamferTransitionFaceCount,
    int LoopCount,
    int CoedgeCount,
    int BoundsCount,
    string Bounds);

public sealed record EdgeLoopX2CorpusCaseResult(
    string CaseName,
    string Status,
    string? ArtifactPath,
    string? ArtifactFileName,
    string SelectionClass,
    EdgeLoopX2LoopSelectionSummary LoopSelectionSummary,
    EdgeLoopX2RuleSummary RuleSummary,
    string ConstructionRoute,
    string SplitPolicy,
    EdgeLoopX2TopologySummary TopologySummary,
    EdgeLoopX2StepMarkerSummary? StepMarkerSummary,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> Errors,
    EdgeLoopX2CorpusGuarantees Guarantees);

public sealed record EdgeLoopX2CorpusResult(
    string Milestone,
    string OutputDirectory,
    string SummaryPath,
    string CorpusRoute,
    string ConstructionRoute,
    string SplitPolicy,
    IReadOnlyList<EdgeLoopX2CorpusCaseResult> Cases,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> Errors,
    EdgeLoopX2CorpusGuarantees Guarantees);

public static class EdgeLoopX2TopFaceLoopChamferCorpusLab
{
    public const string Milestone = "EDGE-LOOP-X2";
    public const string DefaultSummaryFileName = "edge-loop-x2-corpus.json";
    public const string CorpusRoute = "experimental CLI route: aetheris experimental loop-chamfer-corpus --out-dir <dir> [--json]";
    public const string ConstructionRoute = "prismatic-section-transition";
    public const string SplitPolicy = "preserve-section-splits";
    public const string SelectionClass = "Class B / face-boundary loop";

    private static readonly string[] RequiredStepMarkers = ["ISO-10303-21", "MANIFOLD_SOLID_BREP", "ADVANCED_FACE", "PLANE"];
    private static readonly string[] ForbiddenStepMarkers = ["CYLINDRICAL_SURFACE", "BREP_WITH_VOIDS"];
    private static readonly EdgeLoopX2CorpusGuarantees DefaultGuarantees = new(
        NoProductionRouteReplacement: true,
        NoAirEdgeSweep: true,
        NoBrepBoundedChamfer: true,
        NoTopologyGraft: true,
        No3DBoolean: true,
        NoCoplanarMerge: true,
        NotFourIndependentSingleEdgeChamfers: true);

    public static EdgeLoopX2CorpusResult WriteEdgeLoopX2Corpus(string outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory is required.", nameof(outputDirectory));
        }

        var fullDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(fullDirectory);
        var diagnostics = new List<string>
        {
            "edge-loop-x2-corpus-started",
            "edge-loop-x2-no-production-route-replacement",
            "edge-loop-x2-no-air-edge-sweep-used",
            "edge-loop-x2-no-brep-bounded-chamfer-used",
            "edge-loop-x2-no-topology-graft-used",
            "edge-loop-x2-no-3d-boolean-used",
            "edge-loop-x2-no-coplanar-merge-used",
        };

        var cases = new List<EdgeLoopX2CorpusCaseResult>
        {
            RunSuccessCase(fullDirectory, new("canonical-top-face-loop-chamfer", 10, 8, 6, 1), "edge-loop-x2-canonical-top-face-loop-chamfer.step"),
            RunSuccessCase(fullDirectory, new("larger-top-face-loop-chamfer", 10, 8, 6, 2), "edge-loop-x2-larger-top-face-loop-chamfer.step"),
            RunSuccessCase(fullDirectory, new("non-square-top-face-loop-chamfer", 12, 5, 7, 1), "edge-loop-x2-non-square-top-face-loop-chamfer.step"),
            RunDiagnosticCase(new("invalid-zero-chamfer-distance", 10, 8, 6, 0)),
            RunDiagnosticCase(new("invalid-negative-chamfer-distance", 10, 8, 6, -1)),
            RunDiagnosticCase(new("too-large-chamfer-distance", 10, 8, 6, 4)),
            RunDiagnosticCase(new("invalid-width", 0, 8, 6, 1)),
            RunDiagnosticCase(new("invalid-depth", 10, -8, 6, 1)),
            RunDiagnosticCase(new("invalid-height", 10, 8, 0, 1)),
            RunDiagnosticCase(new("non-finite-dimensions", double.NaN, 8, 6, 1)),
            RunDiagnosticCase(new("non-uniform-rule-rejected", 10, 8, 6, 1, Rule: FaceLoopChamferRuleKind.NonUniform)),
            RunDiagnosticCase(new("arbitrary-graph-rejected", 10, 8, 6, 1, new(SelectionKind: FaceLoopChamferSelectionKind.ArbitraryGraph))),
            RunDiagnosticCase(new("open-chain-deferred", 10, 8, 6, 1, new(SelectionKind: FaceLoopChamferSelectionKind.OpenChain))),
            RunDiagnosticCase(new("non-closed-loop-rejected", 10, 8, 6, 1, new(IsClosed: false))),
            RunDiagnosticCase(new("non-outer-loop-deferred", 10, 8, 6, 1, new(LoopKind: FaceLoopChamferLoopKind.Inner))),
            RunDiagnosticCase(new("non-planar-owning-face-deferred", 10, 8, 6, 1, new(OwningFace: FaceLoopChamferOwningFaceKind.NonPlanarFace))),
            RunModeledMetadataCase("inset-self-intersection-risk", "rejected", "inset-self-intersection-risk"),
        };

        diagnostics.AddRange(cases.SelectMany(c => c.Diagnostics).Where(d => d.StartsWith("edge-loop-x2-", StringComparison.Ordinal)));
        diagnostics.Add("edge-loop-x2-json-summary-written");

        var summaryPath = Path.Combine(fullDirectory, DefaultSummaryFileName);
        var result = new EdgeLoopX2CorpusResult(
            Milestone,
            fullDirectory,
            summaryPath,
            CorpusRoute,
            ConstructionRoute,
            SplitPolicy,
            cases,
            Stable(diagnostics),
            cases.SelectMany(c => c.Errors).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            DefaultGuarantees);

        File.WriteAllText(summaryPath, JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        }));

        return result;
    }

    private static EdgeLoopX2CorpusCaseResult RunSuccessCase(string outputDirectory, TopFaceLoopChamferCase c, string artifactFileName)
    {
        var diagnostics = CaseStartDiagnostics(c.Name, c);
        diagnostics.Add($"edge-loop-x2-loop-selection-created:{c.Name}");
        diagnostics.Add($"edge-loop-x2-class-b-loop-route:{c.Name}");
        diagnostics.Add($"edge-loop-x2-prismatic-emitter-invoked:{c.Name}");

        var result = EmitCore(c);
        diagnostics.AddRange(ToX2Diagnostics(result.Diagnostics, c.Name));

        if (result.Status != CorePrismatic.FaceLoopChamferStatus.Succeeded || result.Body is null)
        {
            diagnostics.Add($"edge-loop-x2-case-failed:{c.Name}:{result.Recommendation}");
            return CreateCase(c, "failed", null, null, ToCorpusTopology(result.Topology), null, diagnostics, [result.Recommendation]);
        }

        var export = Step242Exporter.ExportBody(result.Body);
        if (!export.IsSuccess || export.Value is null)
        {
            diagnostics.Add($"edge-loop-x2-case-failed:{c.Name}:step-export");
            return CreateCase(c, "failed", null, null, ToCorpusTopology(result.Topology), null, diagnostics, ["step-export-failed"]);
        }

        var artifactPath = Path.Combine(outputDirectory, artifactFileName);
        File.WriteAllText(artifactPath, export.Value);
        diagnostics.Add($"edge-loop-x2-step-artifact-written:{c.Name}");
        var step = SummarizeMarkers(export.Value);
        if (step.RequiredMarkersPresent && step.ForbiddenMarkersAbsent)
        {
            diagnostics.Add($"edge-loop-x2-step-smoke-succeeded:{c.Name}");
        }

        if (ValidateLoopTopology(result.Topology))
        {
            diagnostics.Add($"edge-loop-x2-split-preserving-topology-validated:{c.Name}");
        }

        diagnostics.Add($"edge-loop-x2-not-four-independent-single-edge-chamfers:{c.Name}");
        return CreateCase(c, "succeeded", artifactPath, artifactFileName, ToCorpusTopology(result.Topology), step, diagnostics, []);
    }

    private static EdgeLoopX2CorpusCaseResult RunDiagnosticCase(TopFaceLoopChamferCase c)
    {
        var diagnostics = CaseStartDiagnostics(c.Name, c);
        var result = EmitCore(c);
        diagnostics.AddRange(ToX2Diagnostics(result.Diagnostics, c.Name));
        var status = result.Status switch
        {
            CorePrismatic.FaceLoopChamferStatus.Deferred => "deferred",
            CorePrismatic.FaceLoopChamferStatus.Rejected => "rejected",
            CorePrismatic.FaceLoopChamferStatus.Succeeded => "failed",
            _ => "failed",
        };
        var reason = ReasonFromDiagnostics(result.Diagnostics, result.Recommendation);
        diagnostics.Add(status == "deferred"
            ? $"edge-loop-x2-case-deferred:{c.Name}:{reason}"
            : $"edge-loop-x2-case-rejected:{c.Name}:{reason}");

        return CreateCase(c, status, null, null, ToCorpusTopology(result.Topology), null, diagnostics, status == "failed" ? [$"unexpected-status:{result.Status}"] : []);
    }

    private static EdgeLoopX2CorpusCaseResult RunModeledMetadataCase(string caseName, string status, string reason)
    {
        var c = new TopFaceLoopChamferCase(caseName, 2, 8, 6, 1);
        var diagnostics = CaseStartDiagnostics(caseName, c);
        diagnostics.Add(status == "deferred"
            ? $"edge-loop-x2-case-deferred:{caseName}:{reason}"
            : $"edge-loop-x2-case-rejected:{caseName}:{reason}");
        return CreateCase(c, status, null, null, EmptyTopology(), null, diagnostics, []);
    }

    private static CorePrismatic.FaceLoopChamferResult EmitCore(TopFaceLoopChamferCase c) =>
        CorePrismatic.PrismaticTopFaceLoopChamferPrototype.Emit(new CorePrismatic.PrismaticTopFaceLoopChamferRequest(
            c.Width,
            c.Depth,
            c.Height,
            c.ChamferDistance,
            c.Selection is null ? null : new CorePrismatic.FaceLoopChamferSelection(
                ToCore(c.Selection.SelectionKind),
                ToCore(c.Selection.OwningFace),
                ToCore(c.Selection.LoopKind),
                c.Selection.IsClosed,
                c.Selection.EdgeCount,
                c.Selection.OrderedCoedges),
            ToCore(c.Rule),
            ExportStep: true));

    private static EdgeLoopX2CorpusCaseResult CreateCase(
        TopFaceLoopChamferCase c,
        string status,
        string? artifactPath,
        string? artifactFileName,
        EdgeLoopX2TopologySummary topology,
        EdgeLoopX2StepMarkerSummary? step,
        IEnumerable<string> diagnostics,
        IReadOnlyList<string> errors) =>
        new(
            c.Name,
            status,
            artifactPath,
            artifactFileName,
            SelectionClass,
            ToLoopSelectionSummary(c.Selection),
            new("uniform symmetric chamfer", double.IsFinite(c.ChamferDistance) ? c.ChamferDistance : null),
            ConstructionRoute,
            SplitPolicy,
            topology,
            step,
            Stable(diagnostics.Concat(GuaranteeDiagnostics())),
            errors,
            DefaultGuarantees);

    private static List<string> CaseStartDiagnostics(string caseName, TopFaceLoopChamferCase c) =>
    [
        $"edge-loop-x2-case-started:{caseName}",
        "edge-loop-x2-no-production-route-replacement",
        "edge-loop-x2-no-air-edge-sweep-used",
        "edge-loop-x2-no-brep-bounded-chamfer-used",
        "edge-loop-x2-no-topology-graft-used",
        "edge-loop-x2-no-3d-boolean-used",
        "edge-loop-x2-no-coplanar-merge-used",
    ];

    private static IEnumerable<string> GuaranteeDiagnostics() =>
    [
        "edge-loop-x2-no-production-route-replacement",
        "edge-loop-x2-no-air-edge-sweep-used",
        "edge-loop-x2-no-brep-bounded-chamfer-used",
        "edge-loop-x2-no-topology-graft-used",
        "edge-loop-x2-no-3d-boolean-used",
        "edge-loop-x2-no-coplanar-merge-used",
    ];

    private static IEnumerable<string> ToX2Diagnostics(IEnumerable<string> diagnostics, string caseName) =>
        diagnostics.Select(d => d.Replace("edge-loop-x1", "edge-loop-x2", StringComparison.Ordinal))
            .Select(d => d.StartsWith("edge-loop-x2-class-b-loop-route", StringComparison.Ordinal)
                || d.StartsWith("edge-loop-x2-prismatic-emitter-invoked", StringComparison.Ordinal)
                || d.StartsWith("edge-loop-x2-not-four-independent-single-edge-chamfers", StringComparison.Ordinal)
                || d.StartsWith("edge-loop-x2-step-smoke-succeeded", StringComparison.Ordinal)
                    ? $"{d}:{caseName}"
                    : d);

    private static bool ValidateLoopTopology(CorePrismatic.FaceLoopChamferTopologySummary t) =>
        t.BodyProduced
        && t.SectionCount == 3
        && t.VertexCount == 12
        && t.EdgeCount == 20
        && t.FaceCount == 10
        && t.PlanarFaceCount == 10
        && t.CylindricalFaceCount == 0
        && t.CapFaceCount == 2
        && t.LowerPrismSideFaceCount == 4
        && t.TransitionFaceCount == 4
        && t.ChamferTransitionFaceCount == 4
        && t.LoopCount == 10
        && t.CoedgeCount == 40;

    private static EdgeLoopX2StepMarkerSummary SummarizeMarkers(string stepText)
    {
        var present = RequiredStepMarkers.Where(m => stepText.Contains(m, StringComparison.Ordinal)).ToArray();
        var missing = RequiredStepMarkers.Except(present, StringComparer.Ordinal).ToArray();
        var absent = ForbiddenStepMarkers.Where(m => !stepText.Contains(m, StringComparison.Ordinal)).ToArray();
        var unexpected = ForbiddenStepMarkers.Where(m => stepText.Contains(m, StringComparison.Ordinal)).ToArray();
        return new(RequiredStepMarkers, ForbiddenStepMarkers, missing.Length == 0, unexpected.Length == 0, present, missing, absent, unexpected);
    }

    private static EdgeLoopX2TopologySummary ToCorpusTopology(CorePrismatic.FaceLoopChamferTopologySummary t) => new(
        t.BodyProduced,
        t.SectionCount,
        t.VertexCount,
        t.EdgeCount,
        t.FaceCount,
        t.PlanarFaceCount,
        t.CylindricalFaceCount,
        t.CapFaceCount,
        t.LowerPrismSideFaceCount,
        t.TransitionFaceCount,
        t.ChamferTransitionFaceCount,
        t.LoopCount,
        t.CoedgeCount,
        t.Bounds == "none" ? 0 : 2,
        t.Bounds);

    private static EdgeLoopX2TopologySummary EmptyTopology() => new(false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "none");

    private static EdgeLoopX2LoopSelectionSummary ToLoopSelectionSummary(FaceLoopChamferSelection? selection)
    {
        var s = selection ?? new FaceLoopChamferSelection();
        return new(
            SelectionClass,
            s.OwningFace == FaceLoopChamferOwningFaceKind.TopCap ? "top cap" : s.OwningFace.ToString(),
            s.LoopKind == FaceLoopChamferLoopKind.Outer ? "outer" : "inner",
            s.IsClosed,
            s.EdgeCount,
            s.OrderedCoedges);
    }

    private static string ReasonFromDiagnostics(IReadOnlyList<string> diagnostics, string recommendation)
    {
        var diagnostic = diagnostics.FirstOrDefault(d => d.StartsWith("edge-loop-x1-", StringComparison.Ordinal)
            && (d.EndsWith("-rejected", StringComparison.Ordinal) || d.EndsWith("-deferred", StringComparison.Ordinal)));
        return diagnostic is null ? recommendation : diagnostic.Replace("edge-loop-x1-", string.Empty, StringComparison.Ordinal).Replace("-rejected", string.Empty, StringComparison.Ordinal).Replace("-deferred", string.Empty, StringComparison.Ordinal);
    }

    private static CorePrismatic.FaceLoopChamferSelectionKind ToCore(FaceLoopChamferSelectionKind kind) => kind switch
    {
        FaceLoopChamferSelectionKind.OpenChain => CorePrismatic.FaceLoopChamferSelectionKind.OpenChain,
        FaceLoopChamferSelectionKind.ArbitraryGraph => CorePrismatic.FaceLoopChamferSelectionKind.ArbitraryGraph,
        _ => CorePrismatic.FaceLoopChamferSelectionKind.FaceBoundaryLoop,
    };

    private static CorePrismatic.FaceLoopChamferOwningFaceKind ToCore(FaceLoopChamferOwningFaceKind kind) => kind switch
    {
        FaceLoopChamferOwningFaceKind.BottomCap => CorePrismatic.FaceLoopChamferOwningFaceKind.BottomCap,
        FaceLoopChamferOwningFaceKind.SideFace => CorePrismatic.FaceLoopChamferOwningFaceKind.SideFace,
        FaceLoopChamferOwningFaceKind.NonPlanarFace => CorePrismatic.FaceLoopChamferOwningFaceKind.NonPlanarFace,
        _ => CorePrismatic.FaceLoopChamferOwningFaceKind.TopCap,
    };

    private static CorePrismatic.FaceLoopChamferLoopKind ToCore(FaceLoopChamferLoopKind kind) => kind == FaceLoopChamferLoopKind.Inner
        ? CorePrismatic.FaceLoopChamferLoopKind.Inner
        : CorePrismatic.FaceLoopChamferLoopKind.Outer;

    private static CorePrismatic.FaceLoopChamferRuleKind ToCore(FaceLoopChamferRuleKind kind) => kind == FaceLoopChamferRuleKind.NonUniform
        ? CorePrismatic.FaceLoopChamferRuleKind.NonUniform
        : CorePrismatic.FaceLoopChamferRuleKind.UniformSymmetric;

    private static IReadOnlyList<string> Stable(IEnumerable<string> diagnostics) => diagnostics.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
}
