using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Prismatic;
using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Continuum.Mirrors;

namespace Aetheris.Continuum.Tests.Backends.Sdf;

public sealed class PrismaticHybridMapDispatchPrototypeTests
{
    private const int Rows = 16;
    private const int Cols = 16;

    [Fact]
    public void EdgePrismaticX8_RectangleInsetHybridDispatch_SelectsConvexCirMirrorAndIsDeterministic()
    {
        var source = PrismaticHybridMapDispatchLab.RectangleInsetSource();

        var first = PrismaticHybridMapDispatchLab.DispatchPrismatic(source, CirMapAnalyzerUse.MapOccupancy, Rows, Cols);
        var second = PrismaticHybridMapDispatchLab.DispatchPrismatic(source, CirMapAnalyzerUse.MapOccupancy, Rows, Cols);

        Assert.Equal(HybridMapBackendKind.CirConvexPolyhedron, first.SelectedBackend);
        Assert.Equal(CirMirrorStatus.MirrorAdmittedExact, first.MirrorAdmission.Status);
        Assert.NotNull(first.MapSummary);
        Assert.Equal(first.StableProjection(), second.StableProjection());
        Assert.Equal("cir-convex-polyhedron", first.MapSummary.BackendSelected);
        Assert.Equal("mirror-admitted-exact", first.MapSummary.MirrorStatus);
        Assert.Equal("map-occupancy", first.MapSummary.RequestedUse);
        Assert.Equal("top", first.MapSummary.View);
        Assert.Equal(Rows, first.MapSummary.Rows);
        Assert.Equal(Cols, first.MapSummary.Cols);
        Assert.Equal(256, first.MapSummary.OccupiedCount);
        Assert.Equal(0, first.MapSummary.EmptyCount);
        Assert.InRange(first.MapSummary.ThicknessMin!.Value, 0.7d, 0.8d);
        Assert.Equal(4d, first.MapSummary.ThicknessMax!.Value, 6);
        Assert.InRange(first.MapSummary.ThicknessAverage!.Value, 3.0d, 3.1d);
        AssertKnownLosses(first);
        Assert.Contains("edge-prismatic-x8-hybrid-map-dispatch-started", first.Diagnostics);
        Assert.Contains("edge-prismatic-x8-prismatic-source-created:rectangle-inset", first.Diagnostics);
        Assert.Contains("edge-prismatic-x8-cir-mirror-admitted-exact:rectangle-inset", first.Diagnostics);
        Assert.Contains("edge-prismatic-x8-backend-selected:cir-convex-polyhedron", first.Diagnostics);
        Assert.Contains("edge-prismatic-x8-map-summary-created:rectangle-inset", first.Diagnostics);
        Assert.Contains("edge-prismatic-x8-no-production-analyzer-behavior-changed", first.Diagnostics);
        Assert.Contains("edge-prismatic-x8-no-default-cli-behavior-changed", first.Diagnostics);
        Assert.Contains("edge-prismatic-x8-no-cir-to-brep-extraction", first.Diagnostics);
    }

    [Fact]
    public void EdgePrismaticX8_TopEdgeChamferHybridDispatch_SelectsConvexCirMirrorAndIsDeterministic()
    {
        var source = PrismaticHybridMapDispatchLab.TopEdgeChamferSource();

        var first = PrismaticHybridMapDispatchLab.DispatchPrismatic(source, CirMapAnalyzerUse.MapOccupancy, Rows, Cols);
        var second = PrismaticHybridMapDispatchLab.DispatchPrismatic(source, CirMapAnalyzerUse.MapOccupancy, Rows, Cols);

        Assert.Equal(HybridMapBackendKind.CirConvexPolyhedron, first.SelectedBackend);
        Assert.Equal(CirMirrorStatus.MirrorAdmittedExact, first.MirrorAdmission.Status);
        Assert.NotNull(first.MapSummary);
        Assert.Equal(first.StableProjection(), second.StableProjection());
        Assert.Equal(256, first.MapSummary.OccupiedCount);
        Assert.Equal(0, first.MapSummary.EmptyCount);
        Assert.InRange(first.MapSummary.ThicknessMin!.Value, 3.3d, 3.4d);
        Assert.Equal(4d, first.MapSummary.ThicknessMax!.Value, 6);
        Assert.InRange(first.MapSummary.ThicknessAverage!.Value, 3.9d, 4.0d);
        AssertKnownLosses(first);
        Assert.Contains("edge-prismatic-x8-prismatic-source-created:top-edge-chamfer", first.Diagnostics);
        Assert.Contains("edge-prismatic-x8-cir-mirror-admitted-exact:top-edge-chamfer", first.Diagnostics);
        Assert.Contains("edge-prismatic-x8-backend-selected:cir-convex-polyhedron", first.Diagnostics);
        Assert.Contains("edge-prismatic-x8-map-summary-created:top-edge-chamfer", first.Diagnostics);
    }

    [Fact]
    public void EdgePrismaticX8_PrimitiveBoxDispatch_ReusesCirTapeAndComparesBrepBaseline()
    {
        var node = new CirBoxNode(10d, 6d, 4d);
        var mapRequest = new CirMapPrototypeRequest(CirMapPrototypeView.Top, Rows, Cols, node.Bounds, SamplesPerRay: 384, RootRefinementIterations: 32, Tolerance: 1e-7d);

        var dispatch = PrismaticHybridMapDispatchLab.DispatchPrimitiveBox(node, mapRequest, BrepPrimitives.CreateBox(10d, 6d, 4d).Value);

        Assert.Equal(HybridMapBackendKind.CirTape, dispatch.SelectedBackend);
        Assert.Equal(CirMirrorStatus.MirrorAdmittedExact, dispatch.MirrorAdmission.Status);
        Assert.NotNull(dispatch.MapSummary);
        Assert.Equal("cir-tape", dispatch.MapSummary.BackendSelected);
        Assert.Equal(256, dispatch.MapSummary.OccupiedCount);
        Assert.Equal(0, dispatch.MapSummary.EmptyCount);
        Assert.Contains("edge-prismatic-x8-backend-selected:cir-tape", dispatch.Diagnostics);
        Assert.Contains("edge-prismatic-x8-backend-selected:brep-raycast", dispatch.Diagnostics);
        Assert.Contains("edge-prismatic-x8-primitive-brep-baseline-compared:box", dispatch.Diagnostics);
    }

    [Fact]
    public void EdgePrismaticX8_TopEdgeChamferLossyRequests_RejectWithoutMapSummary()
    {
        var source = PrismaticHybridMapDispatchLab.TopEdgeChamferSource();

        var faceIdentity = PrismaticHybridMapDispatchLab.DispatchPrismatic(source, CirMapAnalyzerUse.FaceIdentity, Rows, Cols);
        var topologyParity = PrismaticHybridMapDispatchLab.DispatchPrismatic(source, CirMapAnalyzerUse.TopologyParity, Rows, Cols);

        AssertLossyRejected(faceIdentity, "face-identity");
        AssertLossyRejected(topologyParity, "topology-parity");
    }

    [Fact]
    public void EdgePrismaticX8_ImportedStepOnlyPrismaticBody_DoesNotInferMirror()
    {
        var dispatch = PrismaticHybridMapDispatchLab.DispatchImportedStepOnly("edge-prismatic-x5-top-edge-chamfer.step", CirMapAnalyzerUse.MapOccupancy, Rows, Cols);

        Assert.Equal(HybridMapBackendKind.Unsupported, dispatch.SelectedBackend);
        Assert.Equal(CirMirrorStatus.MirrorUnavailable, dispatch.MirrorAdmission.Status);
        Assert.Null(dispatch.MapSummary);
        Assert.Equal("prismatic-hybrid-map-mirror-unavailable", dispatch.Recommendation);
        Assert.Contains("edge-prismatic-x8-imported-step-no-mirror", dispatch.Diagnostics);
        Assert.Contains("edge-prismatic-x8-backend-selected:unsupported", dispatch.Diagnostics);
        Assert.Contains("edge-prismatic-x8-mirror-unavailable:edge-prismatic-x5-top-edge-chamfer.step", dispatch.Diagnostics);
    }

    [Fact]
    public void EdgePrismaticX8_InvalidNonConvexPrismaticSource_RejectsWithoutMapSummary()
    {
        var dispatch = PrismaticHybridMapDispatchLab.DispatchPrismatic(PrismaticHybridMapDispatchLab.NonConvexSource(), CirMapAnalyzerUse.MapOccupancy, Rows, Cols);

        Assert.Equal(HybridMapBackendKind.Unsupported, dispatch.SelectedBackend);
        Assert.Equal(CirMirrorStatus.MirrorRejectedUnsupportedAtom, dispatch.MirrorAdmission.Status);
        Assert.Null(dispatch.MapSummary);
        Assert.Equal("prismatic-hybrid-map-needs-mirror-hardening", dispatch.Recommendation);
        Assert.Contains("edge-prismatic-x8-mirror-unavailable:non-convex", dispatch.Diagnostics);
        Assert.Contains("edge-prismatic-x8-backend-selected:unsupported", dispatch.Diagnostics);
    }

    private static void AssertLossyRejected(HybridMapDispatchResult dispatch, string request)
    {
        Assert.Equal(HybridMapBackendKind.Unsupported, dispatch.SelectedBackend);
        Assert.Equal(CirMirrorStatus.MirrorRejectedLossyForRequest, dispatch.MirrorAdmission.Status);
        Assert.Null(dispatch.MapSummary);
        Assert.Equal("prismatic-hybrid-map-lossy-request-rejected", dispatch.Recommendation);
        Assert.Contains($"edge-prismatic-x8-mirror-rejected-lossy-for-request:{request}", dispatch.Diagnostics);
        Assert.Contains("edge-prismatic-x8-backend-selected:unsupported", dispatch.Diagnostics);
    }

    private static void AssertKnownLosses(HybridMapDispatchResult dispatch)
    {
        Assert.Contains("face identity lost", dispatch.KnownLosses);
        Assert.Contains("loop identity lost", dispatch.KnownLosses);
        Assert.Contains("split-face lineage lost", dispatch.KnownLosses);
        Assert.Contains("feature role labels lost", dispatch.KnownLosses);
        Assert.Contains("topology parity unavailable", dispatch.KnownLosses);
        Assert.Contains("edge-prismatic-x8-loss-face-identity", dispatch.Diagnostics);
        Assert.Contains("edge-prismatic-x8-loss-split-face-lineage", dispatch.Diagnostics);
        Assert.Contains("edge-prismatic-x8-loss-topology-parity", dispatch.Diagnostics);
    }
}

internal enum HybridMapBackendKind
{
    CirConvexPolyhedron,
    CirTape,
    BrepRaycast,
    Unsupported,
}

internal sealed record GeneratedPrismaticMapSource(
    string CaseLabel,
    IReadOnlyList<PrismaticSection> Sections,
    PrismaticCorrespondenceMap? Correspondence = null);

internal sealed record HybridMapSummary(
    string BackendSelected,
    string MirrorStatus,
    string RequestedUse,
    string View,
    int Rows,
    int Cols,
    int OccupiedCount,
    int EmptyCount,
    double? ThicknessMin,
    double? ThicknessMax,
    double? ThicknessAverage,
    CirBounds Bounds,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> KnownLosses);

internal sealed record HybridMapDispatchResult(
    HybridMapBackendKind SelectedBackend,
    CirMirrorAdmissionResult MirrorAdmission,
    CirMapAnalyzerUse RequestedUse,
    HybridMapSummary? MapSummary,
    IReadOnlyList<string> KnownLosses,
    IReadOnlyList<string> Diagnostics,
    string Recommendation)
{
    public string StableProjection() => string.Join("|", new[]
    {
        SelectedBackend.ToString(),
        MirrorAdmission.StatusText,
        RequestedUse.ToString(),
        MapSummary?.ToString() ?? "no-map",
        string.Join(",", KnownLosses),
        string.Join(",", Diagnostics),
        Recommendation,
    });
}

internal static class PrismaticHybridMapDispatchLab
{
    private static readonly string[] KnownLossDescriptions =
    [
        "face identity lost",
        "loop identity lost",
        "split-face lineage lost",
        "feature role labels lost",
        "topology parity unavailable",
    ];

    private static readonly string[] LossDiagnostics =
    [
        "edge-prismatic-x8-loss-face-identity",
        "edge-prismatic-x8-loss-loop-identity",
        "edge-prismatic-x8-loss-split-face-lineage",
        "edge-prismatic-x8-loss-feature-role-labels",
        "edge-prismatic-x8-loss-topology-parity",
    ];

    public static HybridMapDispatchResult DispatchPrismatic(GeneratedPrismaticMapSource source, CirMapAnalyzerUse requestedUse, int rows, int cols)
    {
        var token = Normalize(source.CaseLabel);
        var diagnostics = StartDiagnostics();
        diagnostics.Add($"edge-prismatic-x8-prismatic-source-created:{token}");
        diagnostics.Add($"edge-prismatic-x8-cir-mirror-admission-requested:{token}");

        var mirrorResult = CirPrismaticMirrorBuilder.BuildFromSections(token, source.Sections, source.Correspondence);
        diagnostics.AddRange(mirrorResult.Diagnostics);

        if (requestedUse is CirMapAnalyzerUse.FaceIdentity or CirMapAnalyzerUse.TopologyParity)
        {
            var requestToken = UseToken(requestedUse);
            var admission = mirrorResult.Mirror?.RejectLossyRequest(ToPrismaticRequest(requestedUse))
                ?? CirConvexPolyhedronMirror.CreateAdmission(token, CirMirrorStatus.MirrorRejectedLossyForRequest, CirMirrorCapability.None, diagnostics);
            diagnostics.Add($"edge-prismatic-x8-mirror-rejected-lossy-for-request:{requestToken}");
            diagnostics.AddRange(LossDiagnostics);
            diagnostics.Add("edge-prismatic-x8-backend-selected:unsupported");
            AddNoChangeDiagnostics(diagnostics);
            return new HybridMapDispatchResult(
                HybridMapBackendKind.Unsupported,
                admission,
                requestedUse,
                null,
                KnownLossDescriptions,
                StableDiagnostics(diagnostics),
                "prismatic-hybrid-map-lossy-request-rejected");
        }

        if (!mirrorResult.Succeeded || mirrorResult.Mirror is null)
        {
            diagnostics.Add($"edge-prismatic-x8-mirror-unavailable:{token}");
            diagnostics.Add("edge-prismatic-x8-backend-selected:unsupported");
            AddNoChangeDiagnostics(diagnostics);
            return new HybridMapDispatchResult(
                HybridMapBackendKind.Unsupported,
                mirrorResult.Admission,
                requestedUse,
                null,
                KnownLossDescriptions,
                StableDiagnostics(diagnostics),
                "prismatic-hybrid-map-needs-mirror-hardening");
        }

        if (mirrorResult.Admission.Status == CirMirrorStatus.MirrorAdmittedExact &&
            mirrorResult.Admission.Supports(CirMirrorCapability.MapOccupancy) &&
            requestedUse == CirMapAnalyzerUse.MapOccupancy)
        {
            diagnostics.Add($"edge-prismatic-x8-cir-mirror-admitted-exact:{token}");
            diagnostics.Add("edge-prismatic-x8-backend-selected:cir-convex-polyhedron");
            diagnostics.AddRange(LossDiagnostics);
            var mirrorSummary = mirrorResult.Mirror.CreateTopViewSummary(rows, cols);
            diagnostics.Add($"edge-prismatic-x8-map-summary-created:{token}");
            AddNoChangeDiagnostics(diagnostics);
            var summaryDiagnostics = mirrorSummary.Diagnostics.Concat(diagnostics).Distinct(StringComparer.Ordinal).ToArray();
            var summary = new HybridMapSummary(
                "cir-convex-polyhedron",
                mirrorResult.Admission.StatusText,
                UseToken(requestedUse),
                mirrorSummary.View,
                mirrorSummary.Rows,
                mirrorSummary.Cols,
                mirrorSummary.OccupiedCount,
                mirrorSummary.EmptyCount,
                mirrorSummary.ThicknessMin,
                mirrorSummary.ThicknessMax,
                mirrorSummary.ThicknessAverage,
                mirrorSummary.Bounds,
                summaryDiagnostics,
                KnownLossDescriptions);
            return new HybridMapDispatchResult(
                HybridMapBackendKind.CirConvexPolyhedron,
                mirrorResult.Admission,
                requestedUse,
                summary,
                KnownLossDescriptions,
                StableDiagnostics(diagnostics),
                "prismatic-hybrid-map-ready-for-experimental-cli");
        }

        diagnostics.Add($"edge-prismatic-x8-mirror-unavailable:{token}");
        diagnostics.Add("edge-prismatic-x8-backend-selected:unsupported");
        AddNoChangeDiagnostics(diagnostics);
        return new HybridMapDispatchResult(
            HybridMapBackendKind.Unsupported,
            mirrorResult.Admission,
            requestedUse,
            null,
            KnownLossDescriptions,
            StableDiagnostics(diagnostics),
            "prismatic-hybrid-map-mirror-unavailable");
    }

    public static HybridMapDispatchResult DispatchPrimitiveBox(CirBoxNode node, CirMapPrototypeRequest request, BrepBody? brepBaseline = null)
    {
        var diagnostics = StartDiagnostics();
        diagnostics.Add("edge-prismatic-x8-cir-mirror-admission-requested:box");
        var dispatch = CirMapDispatchPrototype.Dispatch(
            new CirMirrorAdmission(
                CirMirrorSourceRepresentationKind.Air,
                "box",
                CirMirrorAtomKind.BoxPrimitive,
                CirMirrorCapability.MapOccupancy,
                SourceIdOrLabel: "box",
                DiagnosticsLabel: "edge-prismatic-x8"),
            CirMapAnalyzerUse.MapOccupancy,
            node,
            request,
            brepBaseline);

        diagnostics.AddRange(dispatch.Diagnostics);
        diagnostics.Add("edge-prismatic-x8-cir-mirror-admitted-exact:box");
        diagnostics.Add("edge-prismatic-x8-backend-selected:cir-tape");
        if (brepBaseline is not null)
        {
            diagnostics.Add("edge-prismatic-x8-backend-selected:brep-raycast");
            diagnostics.Add("edge-prismatic-x8-primitive-brep-baseline-compared:box");
        }

        AddNoChangeDiagnostics(diagnostics);
        var sourceSummary = dispatch.MapResult!.Summary;
        var summary = new HybridMapSummary(
            "cir-tape",
            dispatch.MirrorAdmission.StatusText,
            "map-occupancy",
            request.View.ToString().ToLowerInvariant(),
            request.Rows,
            request.Cols,
            sourceSummary.HitSamples,
            sourceSummary.EmptySamples,
            sourceSummary.ThicknessMin,
            sourceSummary.ThicknessMax,
            sourceSummary.ThicknessAverage,
            request.Bounds,
            StableDiagnostics(diagnostics),
            KnownLossDescriptions);
        return new HybridMapDispatchResult(
            HybridMapBackendKind.CirTape,
            dispatch.MirrorAdmission,
            CirMapAnalyzerUse.MapOccupancy,
            summary,
            KnownLossDescriptions,
            StableDiagnostics(diagnostics),
            dispatch.Recommendation);
    }

    public static HybridMapDispatchResult DispatchImportedStepOnly(string sourceLabel, CirMapAnalyzerUse requestedUse, int rows, int cols)
    {
        var token = Normalize(sourceLabel);
        var diagnostics = StartDiagnostics();
        diagnostics.Add("edge-prismatic-x8-imported-step-no-mirror");
        diagnostics.Add($"edge-prismatic-x8-cir-mirror-admission-requested:{token}");
        var admission = CirMirrorAdmissionService.Admit(new CirMirrorAdmission(
            CirMirrorSourceRepresentationKind.Step,
            token,
            CirMirrorAtomKind.Unknown,
            CirMapDispatchPrototype.CapabilityForUse(requestedUse),
            SourceIdOrLabel: token,
            DiagnosticsLabel: "edge-prismatic-x8"));
        diagnostics.AddRange(admission.Diagnostics);
        diagnostics.Add($"edge-prismatic-x8-mirror-unavailable:{token}");
        diagnostics.Add("edge-prismatic-x8-backend-selected:unsupported");
        AddNoChangeDiagnostics(diagnostics);
        return new HybridMapDispatchResult(
            HybridMapBackendKind.Unsupported,
            admission,
            requestedUse,
            null,
            KnownLossDescriptions,
            StableDiagnostics(diagnostics),
            "prismatic-hybrid-map-mirror-unavailable");
    }

    public static GeneratedPrismaticMapSource RectangleInsetSource() => new(
        "rectangle-inset",
        [
            new PrismaticSection(0d, [(-5d, -3d), (5d, -3d), (5d, 3d), (-5d, 3d)]),
            new PrismaticSection(4d, [(-4d, -2d), (4d, -2d), (4d, 2d), (-4d, 2d)]),
        ]);

    public static GeneratedPrismaticMapSource TopEdgeChamferSource() => new(
        "top-edge-chamfer",
        PrismaticTopEdgeChamferPrototype.CreateSectionStack(new PrismaticTopEdgeChamferRequest(10d, 6d, 4d, 1d)));

    public static GeneratedPrismaticMapSource NonConvexSource() => new(
        "non-convex",
        [
            new PrismaticSection(0d, [(0d, 0d), (3d, 0d), (1d, 1d), (3d, 3d), (0d, 3d)]),
            new PrismaticSection(2d, [(0d, 0d), (3d, 0d), (1d, 1d), (3d, 3d), (0d, 3d)]),
        ]);

    private static List<string> StartDiagnostics() =>
    [
        "edge-prismatic-x8-hybrid-map-dispatch-started",
    ];

    private static void AddNoChangeDiagnostics(List<string> diagnostics)
    {
        diagnostics.Add("edge-prismatic-x8-no-production-analyzer-behavior-changed");
        diagnostics.Add("edge-prismatic-x8-no-default-cli-behavior-changed");
        diagnostics.Add("edge-prismatic-x8-no-cir-to-brep-extraction");
    }

    private static CirPrismaticMirrorRequestKind ToPrismaticRequest(CirMapAnalyzerUse requestedUse) => requestedUse switch
    {
        CirMapAnalyzerUse.FaceIdentity => CirPrismaticMirrorRequestKind.FaceIdentity,
        CirMapAnalyzerUse.TopologyParity => CirPrismaticMirrorRequestKind.TopologyParity,
        CirMapAnalyzerUse.MapOccupancy => CirPrismaticMirrorRequestKind.MapOccupancy,
        _ => CirPrismaticMirrorRequestKind.PointContainment,
    };

    private static string UseToken(CirMapAnalyzerUse use) => use switch
    {
        CirMapAnalyzerUse.MapOccupancy => "map-occupancy",
        CirMapAnalyzerUse.FaceIdentity => "face-identity",
        CirMapAnalyzerUse.TopologyParity => "topology-parity",
        CirMapAnalyzerUse.PointContainment => "point-containment",
        CirMapAnalyzerUse.SectionSampling => "section-sampling",
        _ => "unknown",
    };

    private static string Normalize(string value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant().Replace(' ', '-');

    private static IReadOnlyList<string> StableDiagnostics(IEnumerable<string> diagnostics) =>
        diagnostics.Distinct(StringComparer.Ordinal).ToArray();
}
