using Aetheris.Kernel.Core.Brep.Prismatic;
using Aetheris.Continuum.Mirrors;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Tests.Backends.Sdf;

public sealed class CirPrismaticConvexPolyhedronMirrorTests
{
    [Fact]
    public void CirPrismaticX2_RectangleInsetMirror_AdmitsClassifiesAndSummarizesDeterministically()
    {
        var first = CirPrismaticMirrorBuilder.BuildFromSections("rectangle-inset", RectangleInsetSections());
        var second = CirPrismaticMirrorBuilder.BuildFromSections("rectangle-inset", RectangleInsetSections());

        Assert.True(first.Succeeded, string.Join(Environment.NewLine, first.Diagnostics));
        Assert.Equal(CirMirrorStatus.MirrorAdmittedExact, first.Admission.Status);
        Assert.True(first.Admission.Supports(CirMirrorCapability.PointContainment));
        Assert.True(first.Admission.Supports(CirMirrorCapability.MapOccupancy));
        AssertKnownLosses(first.Admission);

        var mirror = first.Mirror!;
        Assert.Equal(6, mirror.HalfSpaces.Count);
        AssertInside(mirror, new Point3D(0d, 0d, 2d));
        AssertOutside(mirror, new Point3D(8d, 0d, 2d));
        AssertOutside(mirror, new Point3D(0d, 8d, 2d));
        AssertInside(mirror, new Point3D(4.5d, 0d, 0.25d));
        AssertOutside(mirror, new Point3D(4.5d, 0d, 3.75d));

        var summary = mirror.CreateTopViewSummary();
        var repeatedSummary = second.Mirror!.CreateTopViewSummary();
        AssertStableSummary(summary, repeatedSummary);
        Assert.Equal(16, summary.Rows);
        Assert.Equal(16, summary.Cols);
        Assert.Equal(256, summary.OccupiedCount);
        Assert.Equal(0, summary.EmptyCount);
        Assert.InRange(summary.ThicknessMin!.Value, 0.7d, 0.8d);
        Assert.Equal(4d, summary.ThicknessMax!.Value, 6);
        Assert.InRange(summary.ThicknessAverage!.Value, 3.0d, 3.1d);
        Assert.Contains("cir-prismatic-x2-map-summary-created:rectangle-inset", summary.Diagnostics);
        Assert.Contains("cir-prismatic-x2-no-production-analyzer-behavior-changed", first.Diagnostics);
        Assert.Contains("cir-prismatic-x2-no-cir-to-brep-extraction", first.Diagnostics);
    }

    [Fact]
    public void CirPrismaticX2_TopEdgeChamferMirror_AdmitsClassifiesAndSummarizesDeterministically()
    {
        var request = new PrismaticTopEdgeChamferRequest(10d, 6d, 4d, 1d);
        var first = CirPrismaticMirrorBuilder.BuildFromSections("top-edge-chamfer", PrismaticTopEdgeChamferPrototype.CreateSectionStack(request));
        var second = CirPrismaticMirrorBuilder.BuildFromSections("top-edge-chamfer", PrismaticTopEdgeChamferPrototype.CreateSectionStack(request));

        Assert.True(first.Succeeded, string.Join(Environment.NewLine, first.Diagnostics));
        Assert.Equal(CirMirrorStatus.MirrorAdmittedExact, first.Admission.Status);
        Assert.True(first.Admission.Supports(CirMirrorCapability.PointContainment));
        Assert.True(first.Admission.Supports(CirMirrorCapability.MapOccupancy));
        AssertKnownLosses(first.Admission);

        var mirror = first.Mirror!;
        Assert.Equal(10, mirror.HalfSpaces.Count);
        AssertInside(mirror, new Point3D(4.5d, 0d, 2.5d));
        AssertOutside(mirror, new Point3D(4.5d, 0d, 3.75d));
        AssertInside(mirror, new Point3D(4.25d, 0d, 3.25d));
        AssertOutside(mirror, new Point3D(4.75d, 0d, 3.75d));
        AssertInside(mirror, new Point3D(0d, 0d, 2d));

        var summary = mirror.CreateTopViewSummary();
        var repeatedSummary = second.Mirror!.CreateTopViewSummary();
        AssertStableSummary(summary, repeatedSummary);
        Assert.Equal(256, summary.OccupiedCount);
        Assert.Equal(0, summary.EmptyCount);
        Assert.InRange(summary.ThicknessMin!.Value, 3.3d, 3.4d);
        Assert.Equal(4d, summary.ThicknessMax!.Value, 6);
        Assert.InRange(summary.ThicknessAverage!.Value, 3.9d, 4.0d);
        Assert.Contains("cir-prismatic-x2-map-summary-created:top-edge-chamfer", summary.Diagnostics);
        Assert.Contains("cir-prismatic-x2-halfspace-count:10", first.Diagnostics);
    }

    [Fact]
    public void CirPrismaticX2_LossyTopologyRequestsRejectWithoutMapSummaryClaim()
    {
        var result = CirPrismaticMirrorBuilder.BuildFromSections("rectangle-inset", RectangleInsetSections());
        var mirror = result.Mirror!;

        var faceIdentity = mirror.RejectLossyRequest(CirPrismaticMirrorRequestKind.FaceIdentity);
        var topologyParity = mirror.RejectLossyRequest(CirPrismaticMirrorRequestKind.TopologyParity);

        Assert.Equal(CirMirrorStatus.MirrorRejectedLossyForRequest, faceIdentity.Status);
        Assert.False(faceIdentity.Supports(CirMirrorCapability.MapOccupancy));
        Assert.Contains("cir-prismatic-x2-mirror-rejected-lossy-for-request:face-identity", faceIdentity.Diagnostics);
        Assert.Contains("cir-prismatic-x2-loss-face-identity", faceIdentity.Diagnostics);

        Assert.Equal(CirMirrorStatus.MirrorRejectedLossyForRequest, topologyParity.Status);
        Assert.False(topologyParity.Supports(CirMirrorCapability.MapOccupancy));
        Assert.Contains("cir-prismatic-x2-mirror-rejected-lossy-for-request:topology-parity", topologyParity.Diagnostics);
        Assert.Contains("cir-prismatic-x2-loss-topology-parity", topologyParity.Diagnostics);
    }

    [Fact]
    public void CirPrismaticX2_NonConvexSectionRejectsDeterministically()
    {
        var sections = new[]
        {
            new PrismaticSection(0d, [(0d, 0d), (3d, 0d), (1d, 1d), (3d, 3d), (0d, 3d)]),
            new PrismaticSection(2d, [(0d, 0d), (3d, 0d), (1d, 1d), (3d, 3d), (0d, 3d)]),
        };

        var result = CirPrismaticMirrorBuilder.BuildFromSections("non-convex", sections);

        Assert.False(result.Succeeded);
        Assert.Equal(CirMirrorStatus.MirrorRejectedUnsupportedAtom, result.Status);
        Assert.Contains("cir-prismatic-x2-mirror-rejected-unsupported:non-convex-or-clockwise-section", result.Diagnostics);
    }

    private static IReadOnlyList<PrismaticSection> RectangleInsetSections() =>
    [
        new PrismaticSection(0d, [(-5d, -3d), (5d, -3d), (5d, 3d), (-5d, 3d)]),
        new PrismaticSection(4d, [(-4d, -2d), (4d, -2d), (4d, 2d), (-4d, 2d)]),
    ];

    private static void AssertStableSummary(CirPrismaticMirrorSummary expected, CirPrismaticMirrorSummary actual)
    {
        Assert.Equal(expected.Rows, actual.Rows);
        Assert.Equal(expected.Cols, actual.Cols);
        Assert.Equal(expected.View, actual.View);
        Assert.Equal(expected.OccupiedCount, actual.OccupiedCount);
        Assert.Equal(expected.EmptyCount, actual.EmptyCount);
        Assert.Equal(expected.ThicknessMin, actual.ThicknessMin);
        Assert.Equal(expected.ThicknessMax, actual.ThicknessMax);
        Assert.Equal(expected.ThicknessAverage, actual.ThicknessAverage);
        Assert.Equal(expected.Bounds, actual.Bounds);
        Assert.Equal(expected.Diagnostics, actual.Diagnostics);
    }

    private static void AssertKnownLosses(CirMirrorAdmissionResult admission)
    {
        Assert.True(admission.HasLoss(CirMirrorLossFlags.FaceIdentityLost));
        Assert.True(admission.HasLoss(CirMirrorLossFlags.LoopIdentityLost));
        Assert.True(admission.HasLoss(CirMirrorLossFlags.SplitFaceLineageLost));
        Assert.True(admission.HasLoss(CirMirrorLossFlags.FeatureRoleLabelsLost));
        Assert.True(admission.HasLoss(CirMirrorLossFlags.ExactTopologyUnavailable));
    }

    private static void AssertInside(CirConvexPolyhedronMirror mirror, Point3D point)
    {
        var classification = mirror.Classify(point);
        Assert.True(
            classification is CirConvexPointClassification.Inside or CirConvexPointClassification.Boundary,
            $"Expected inside but found {classification} with violation {mirror.Evaluate(point)} at {point}.");
    }

    private static void AssertOutside(CirConvexPolyhedronMirror mirror, Point3D point)
    {
        Assert.Equal(CirConvexPointClassification.Outside, mirror.Classify(point));
    }
}
