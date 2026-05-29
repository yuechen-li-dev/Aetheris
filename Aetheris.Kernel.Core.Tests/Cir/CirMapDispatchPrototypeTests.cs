using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Cir.Mirrors;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Cir;

public sealed class CirMapDispatchPrototypeTests
{
    private const int Rows = 17;
    private const int Cols = 19;
    private const double ThicknessTolerance = 0.075d;

    public static TheoryData<string, int, CirNode, BrepBody, CirMapPrototypeView> PrimitiveViews => new()
    {
        { "box", (int)CirMirrorAtomKind.BoxPrimitive, new CirBoxNode(10d, 6d, 4d), BrepPrimitives.CreateBox(10d, 6d, 4d).Value, CirMapPrototypeView.Top },
        { "box", (int)CirMirrorAtomKind.BoxPrimitive, new CirBoxNode(10d, 6d, 4d), BrepPrimitives.CreateBox(10d, 6d, 4d).Value, CirMapPrototypeView.Front },
        { "cylinder", (int)CirMirrorAtomKind.CylinderPrimitive, new CirCylinderNode(3d, 8d), BrepPrimitives.CreateCylinder(3d, 8d).Value, CirMapPrototypeView.Top },
        { "cylinder", (int)CirMirrorAtomKind.CylinderPrimitive, new CirCylinderNode(3d, 8d), BrepPrimitives.CreateCylinder(3d, 8d).Value, CirMapPrototypeView.Front },
        { "sphere", (int)CirMirrorAtomKind.SpherePrimitive, new CirSphereNode(3d), BrepPrimitives.CreateSphere(3d).Value, CirMapPrototypeView.Top },
        { "sphere", (int)CirMirrorAtomKind.SpherePrimitive, new CirSphereNode(3d), BrepPrimitives.CreateSphere(3d).Value, CirMapPrototypeView.Front },
    };

    [Theory]
    [MemberData(nameof(PrimitiveViews))]
    public void CirMapX2_PrimitiveMapOccupancy_SelectsCirTapeAndComparesBrepBaseline(string source, int atomKindValue, CirNode node, BrepBody body, CirMapPrototypeView view)
    {
        var request = new CirMapPrototypeRequest(view, Rows, Cols, node.Bounds, SamplesPerRay: 384, RootRefinementIterations: 32, Tolerance: 1e-7d);
        var dispatch = CirMapDispatchPrototype.Dispatch(CreateRequest(source, (CirMirrorAtomKind)atomKindValue, CirMapAnalyzerUse.MapOccupancy), CirMapAnalyzerUse.MapOccupancy, node, request, body, ThicknessTolerance);

        Assert.Equal(CirMapBackendKind.CirTape, dispatch.SelectedBackend);
        Assert.Equal(CirMirrorStatus.MirrorAdmittedExact, dispatch.MirrorAdmission.Status);
        Assert.Equal("cir-map-dispatch-ready-for-primitive-lab", dispatch.Recommendation);
        Assert.NotNull(dispatch.MapResult);
        Assert.NotNull(dispatch.BrepBaselineComparison);
        Assert.True(dispatch.BrepBaselineComparison.ParitySucceeded);
        Assert.Equal(dispatch.BrepBaselineComparison.BrepSummary.HitSamples, dispatch.MapResult.Summary.HitSamples);
        Assert.Equal(dispatch.BrepBaselineComparison.BrepSummary.EmptySamples, dispatch.MapResult.Summary.EmptySamples);
        Assert.InRange(dispatch.BrepBaselineComparison.ThicknessMinDelta!.Value, 0d, ThicknessTolerance);
        Assert.InRange(dispatch.BrepBaselineComparison.ThicknessMaxDelta!.Value, 0d, ThicknessTolerance);
        Assert.InRange(dispatch.BrepBaselineComparison.ThicknessAverageDelta!.Value, 0d, ThicknessTolerance);
        Assert.Contains("cir-map-x2-dispatch-started", dispatch.Diagnostics);
        Assert.Contains($"cir-map-x2-mirror-admission-requested:{source}", dispatch.Diagnostics);
        Assert.Contains($"cir-map-x2-mirror-admitted-exact:{source}", dispatch.Diagnostics);
        Assert.Contains("cir-map-x2-backend-selected:cir-tape", dispatch.Diagnostics);
        Assert.Contains($"cir-map-x2-brep-baseline-compared:{source}", dispatch.Diagnostics);
        Assert.Contains($"cir-map-x2-map-parity-succeeded:{source}", dispatch.Diagnostics);
        Assert.Contains("cir-map-x2-no-production-analyzer-behavior-changed", dispatch.Diagnostics);
    }

    [Fact]
    public void CirMapX2_BoxFaceIdentityRequest_RejectsCirAsLossy()
    {
        var node = new CirBoxNode(10d, 6d, 4d);
        var request = new CirMapPrototypeRequest(CirMapPrototypeView.Top, Rows, Cols, node.Bounds, SamplesPerRay: 128, RootRefinementIterations: 24, Tolerance: 1e-7d);

        var dispatch = CirMapDispatchPrototype.Dispatch(CreateRequest("box", CirMirrorAtomKind.BoxPrimitive, CirMapAnalyzerUse.FaceIdentity), CirMapAnalyzerUse.FaceIdentity, node, request);

        Assert.Equal(CirMapBackendKind.Unsupported, dispatch.SelectedBackend);
        Assert.Equal(CirMirrorStatus.MirrorRejectedLossyForRequest, dispatch.MirrorAdmission.Status);
        Assert.Null(dispatch.MapResult);
        Assert.Equal("cir-map-dispatch-lossy-request-rejected", dispatch.Recommendation);
        Assert.Contains("cir-map-x2-mirror-rejected-lossy-for-request:face-identity", dispatch.Diagnostics);
        Assert.Contains("cir-map-x2-backend-selected:unsupported", dispatch.Diagnostics);
    }

    [Fact]
    public void CirMapX2_BoxTopologyParityRequest_RejectsCirAsLossy()
    {
        var node = new CirBoxNode(10d, 6d, 4d);
        var request = new CirMapPrototypeRequest(CirMapPrototypeView.Top, Rows, Cols, node.Bounds, SamplesPerRay: 128, RootRefinementIterations: 24, Tolerance: 1e-7d);

        var dispatch = CirMapDispatchPrototype.Dispatch(CreateRequest("box", CirMirrorAtomKind.BoxPrimitive, CirMapAnalyzerUse.TopologyParity), CirMapAnalyzerUse.TopologyParity, node, request);

        Assert.Equal(CirMapBackendKind.Unsupported, dispatch.SelectedBackend);
        Assert.Equal(CirMirrorStatus.MirrorRejectedLossyForRequest, dispatch.MirrorAdmission.Status);
        Assert.Null(dispatch.MapResult);
        Assert.Equal("cir-map-dispatch-lossy-request-rejected", dispatch.Recommendation);
        Assert.Contains("cir-map-x2-mirror-rejected-lossy-for-request:topology-parity", dispatch.Diagnostics);
        Assert.Contains("cir-map-x2-backend-selected:unsupported", dispatch.Diagnostics);
    }

    [Theory]
    [InlineData("prismatic-section-transition", (int)CirMirrorAtomKind.PrismaticSectionTransition, (int)CirMirrorStatus.MirrorRejectedUnsupportedAtom)]
    [InlineData("profile-authored-chamfer", (int)CirMirrorAtomKind.ProfileAuthoredVerticalChamfer, (int)CirMirrorStatus.MirrorUnavailable)]
    public void CirMapX2_UnavailableSources_DoNotSelectCir(string source, int atomKindValue, int expectedStatusValue)
    {
        var bounds = new CirBounds(new Point3D(-1d, -1d, -1d), new Point3D(1d, 1d, 1d));
        var request = new CirMapPrototypeRequest(CirMapPrototypeView.Top, Rows, Cols, bounds, SamplesPerRay: 128, RootRefinementIterations: 24, Tolerance: 1e-7d);

        var dispatch = CirMapDispatchPrototype.Dispatch(CreateRequest(source, (CirMirrorAtomKind)atomKindValue, CirMapAnalyzerUse.MapOccupancy), CirMapAnalyzerUse.MapOccupancy, node: null, request);

        Assert.Equal(CirMapBackendKind.Unsupported, dispatch.SelectedBackend);
        Assert.Equal((CirMirrorStatus)expectedStatusValue, dispatch.MirrorAdmission.Status);
        Assert.Null(dispatch.MapResult);
        Assert.Equal("cir-map-dispatch-mirror-unavailable", dispatch.Recommendation);
        Assert.Contains($"cir-map-x2-mirror-unavailable:{source}", dispatch.Diagnostics);
        Assert.Contains("cir-map-x2-no-prismatic-mirror-used", dispatch.Diagnostics);
        Assert.Contains("cir-map-x2-backend-selected:unsupported", dispatch.Diagnostics);
    }

    [Fact]
    public void CirMapX2_RepeatedDispatchesProduceStableProjection()
    {
        var node = new CirSphereNode(3d);
        var request = new CirMapPrototypeRequest(CirMapPrototypeView.Front, Rows, Cols, node.Bounds, SamplesPerRay: 384, RootRefinementIterations: 32, Tolerance: 1e-7d);

        var first = CirMapDispatchPrototype.Dispatch(CreateRequest("sphere", CirMirrorAtomKind.SpherePrimitive, CirMapAnalyzerUse.MapOccupancy), CirMapAnalyzerUse.MapOccupancy, node, request, BrepPrimitives.CreateSphere(3d).Value, ThicknessTolerance);
        var second = CirMapDispatchPrototype.Dispatch(CreateRequest("sphere", CirMirrorAtomKind.SpherePrimitive, CirMapAnalyzerUse.MapOccupancy), CirMapAnalyzerUse.MapOccupancy, node, request, BrepPrimitives.CreateSphere(3d).Value, ThicknessTolerance);

        Assert.Equal(first.StableProjection(), second.StableProjection());
    }

    private static CirMirrorAdmission CreateRequest(string source, CirMirrorAtomKind atomKind, CirMapAnalyzerUse use) =>
        new(
            CirMirrorSourceRepresentationKind.Air,
            source,
            atomKind,
            CirMapDispatchPrototype.CapabilityForUse(use),
            SourceIdOrLabel: source,
            DiagnosticsLabel: "cir-map-x2");
}

internal enum CirMapAnalyzerUse
{
    MapOccupancy,
    FaceIdentity,
    TopologyParity,
    PointContainment,
    SectionSampling,
}

internal enum CirMapBackendKind
{
    CirTape,
    CirNode,
    BrepRaycastBaseline,
    Unsupported,
}

internal sealed record CirMapBackendCandidate(CirMapBackendKind Kind, bool Admissible, string Reason);

internal sealed record CirMapBaselineComparison(
    CirMapPrototypeSummary CirSummary,
    CirMapPrototypeSummary BrepSummary,
    bool ParitySucceeded,
    double? ThicknessMinDelta,
    double? ThicknessMaxDelta,
    double? ThicknessAverageDelta);

internal sealed record CirMapDispatchResult(
    CirMapBackendKind SelectedBackend,
    CirMirrorAdmissionResult MirrorAdmission,
    IReadOnlyList<CirMapBackendCandidate> Candidates,
    CirMapPrototypeResult? MapResult,
    CirMapBaselineComparison? BrepBaselineComparison,
    IReadOnlyList<string> Diagnostics,
    string Recommendation)
{
    public string StableProjection() => string.Join("|", new[]
    {
        SelectedBackend.ToString(),
        MirrorAdmission.StatusText,
        Recommendation,
        MapResult?.Summary.ToString() ?? "no-map",
        BrepBaselineComparison?.ToString() ?? "no-baseline",
        string.Join(",", Diagnostics),
    });
}

internal static class CirMapDispatchPrototype
{
    public static CirMapDispatchResult Dispatch(
        CirMirrorAdmission mirrorRequest,
        CirMapAnalyzerUse requestedUse,
        CirNode? node,
        CirMapPrototypeRequest mapRequest,
        BrepBody? brepBaselineBody = null,
        double summaryTolerance = 0.075d)
    {
        var source = mirrorRequest.SourceRoute;
        var diagnostics = new List<string>
        {
            "cir-map-x2-dispatch-started",
            $"cir-map-x2-mirror-admission-requested:{source}",
        };

        var admissionRequest = mirrorRequest with { RequestedCapabilities = CapabilityForUse(requestedUse) };
        var admission = CirMirrorAdmissionService.Admit(admissionRequest);
        diagnostics.AddRange(admission.Diagnostics.Select(MapAdmissionDiagnostic));

        if (admission.Status == CirMirrorStatus.MirrorRejectedLossyForRequest || requestedUse is CirMapAnalyzerUse.FaceIdentity or CirMapAnalyzerUse.TopologyParity)
        {
            diagnostics.Add($"cir-map-x2-mirror-rejected-lossy-for-request:{UseToken(requestedUse)}");
            diagnostics.Add("cir-map-x2-backend-selected:unsupported");
            diagnostics.Add("cir-map-x2-no-production-analyzer-behavior-changed");
            return new CirMapDispatchResult(
                CirMapBackendKind.Unsupported,
                admission,
                [new CirMapBackendCandidate(CirMapBackendKind.CirTape, false, "lossy-request-rejected")],
                null,
                null,
                diagnostics,
                "cir-map-dispatch-lossy-request-rejected");
        }

        if (admission.Status == CirMirrorStatus.MirrorAdmittedExact && admission.Supports(CirMirrorCapability.MapOccupancy) && requestedUse == CirMapAnalyzerUse.MapOccupancy && node is not null)
        {
            diagnostics.Add($"cir-map-x2-mirror-admitted-exact:{source}");
            diagnostics.Add("cir-map-x2-backend-selected:cir-tape");
            var map = CirMapPrototype.Evaluate(CirTapeLowerer.Lower(node), node.Bounds, source, mapRequest);
            CirMapBaselineComparison? comparison = null;
            var recommendation = "cir-map-dispatch-ready-for-primitive-lab";

            if (brepBaselineBody is not null)
            {
                var baseline = CirMapPrototype.EvaluateBrepBaseline(brepBaselineBody, source, mapRequest);
                comparison = Compare(map.Summary, baseline.Summary, summaryTolerance);
                diagnostics.Add($"cir-map-x2-brep-baseline-compared:{source}");
                if (comparison.ParitySucceeded)
                {
                    diagnostics.Add($"cir-map-x2-map-parity-succeeded:{source}");
                }
                else
                {
                    diagnostics.Add($"cir-map-x2-map-parity-warning:{source}:summary-mismatch");
                    recommendation = "cir-map-dispatch-needs-tape-hardening";
                }
            }

            diagnostics.Add("cir-map-x2-no-prismatic-mirror-used");
            diagnostics.Add("cir-map-x2-no-production-analyzer-behavior-changed");
            return new CirMapDispatchResult(
                CirMapBackendKind.CirTape,
                admission,
                [new CirMapBackendCandidate(CirMapBackendKind.CirTape, true, "mirror-admitted-exact-map-occupancy")],
                map,
                comparison,
                diagnostics,
                recommendation);
        }

        diagnostics.Add($"cir-map-x2-mirror-unavailable:{source}");
        diagnostics.Add("cir-map-x2-no-prismatic-mirror-used");
        diagnostics.Add("cir-map-x2-backend-selected:unsupported");
        diagnostics.Add("cir-map-x2-no-production-analyzer-behavior-changed");
        return new CirMapDispatchResult(
            CirMapBackendKind.Unsupported,
            admission,
            [new CirMapBackendCandidate(CirMapBackendKind.CirTape, false, admission.StatusText)],
            null,
            null,
            diagnostics,
            "cir-map-dispatch-mirror-unavailable");
    }

    public static CirMirrorCapability CapabilityForUse(CirMapAnalyzerUse use) => use switch
    {
        CirMapAnalyzerUse.MapOccupancy => CirMirrorCapability.MapOccupancy,
        CirMapAnalyzerUse.FaceIdentity => CirMirrorCapability.FaceIdentity,
        CirMapAnalyzerUse.TopologyParity => CirMirrorCapability.TopologyParity,
        CirMapAnalyzerUse.PointContainment => CirMirrorCapability.PointContainment,
        CirMapAnalyzerUse.SectionSampling => CirMirrorCapability.SectionSampling,
        _ => CirMirrorCapability.None,
    };

    private static CirMapBaselineComparison Compare(CirMapPrototypeSummary cir, CirMapPrototypeSummary brep, double tolerance)
    {
        var minDelta = Delta(cir.ThicknessMin, brep.ThicknessMin);
        var maxDelta = Delta(cir.ThicknessMax, brep.ThicknessMax);
        var averageDelta = Delta(cir.ThicknessAverage, brep.ThicknessAverage);
        var paritySucceeded = cir.HitSamples == brep.HitSamples &&
            cir.EmptySamples == brep.EmptySamples &&
            DeltaWithinTolerance(minDelta, tolerance) &&
            DeltaWithinTolerance(maxDelta, tolerance) &&
            DeltaWithinTolerance(averageDelta, tolerance);

        return new CirMapBaselineComparison(cir, brep, paritySucceeded, minDelta, maxDelta, averageDelta);
    }

    private static double? Delta(double? left, double? right) => left is null || right is null ? null : double.Abs(left.Value - right.Value);

    private static bool DeltaWithinTolerance(double? delta, double tolerance) => delta is null || delta.Value <= tolerance;

    private static string MapAdmissionDiagnostic(string diagnostic)
    {
        if (diagnostic.StartsWith("air-cir-x1-mirror-admitted-exact:", StringComparison.Ordinal))
        {
            return diagnostic.Replace("air-cir-x1-mirror-admitted-exact:", "cir-map-x2-mirror-admitted-exact:", StringComparison.Ordinal);
        }

        if (diagnostic.StartsWith("air-cir-x1-mirror-unavailable:", StringComparison.Ordinal))
        {
            return diagnostic.Replace("air-cir-x1-mirror-unavailable:", "cir-map-x2-mirror-unavailable:", StringComparison.Ordinal);
        }

        if (diagnostic.StartsWith("air-cir-x1-mirror-rejected-unsupported-atom:", StringComparison.Ordinal))
        {
            return diagnostic.Replace("air-cir-x1-mirror-rejected-unsupported-atom:", "cir-map-x2-mirror-unavailable:", StringComparison.Ordinal);
        }

        if (diagnostic.StartsWith("air-cir-x1-mirror-rejected-lossy-for-request:", StringComparison.Ordinal))
        {
            return diagnostic.Replace("air-cir-x1-mirror-rejected-lossy-for-request:", "cir-map-x2-mirror-rejected-lossy-for-request:", StringComparison.Ordinal);
        }

        return diagnostic;
    }

    private static string UseToken(CirMapAnalyzerUse use) => use switch
    {
        CirMapAnalyzerUse.MapOccupancy => "map-occupancy",
        CirMapAnalyzerUse.FaceIdentity => "face-identity",
        CirMapAnalyzerUse.TopologyParity => "topology-parity",
        CirMapAnalyzerUse.PointContainment => "point-containment",
        CirMapAnalyzerUse.SectionSampling => "section-sampling",
        _ => "unknown",
    };
}
