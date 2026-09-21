using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242OcctCylinderRecoveryTests
{
    [Fact]
    public void ImportBody_OcctRod_RecoversAnalyticCylinderFacesFromRationalBsplineSurfaces()
    {
        var import = Step242Importer.ImportBody(ReadFixture("testdata/step242/OCCT/rod.step"));

        Assert.True(import.IsSuccess);
        Assert.Equal(2, import.Value.Geometry.Surfaces.Count(surface => surface.Value.Kind == SurfaceGeometryKind.Cylinder));
        Assert.Equal(0, import.Value.Geometry.Surfaces.Count(surface => surface.Value.Kind == SurfaceGeometryKind.BSplineSurfaceWithKnots));
    }

    [Fact]
    public void ImportBody_OcctBolt001_RecoversAnalyticCylinderFacesFromRationalBsplineSurfaces()
    {
        var import = Step242Importer.ImportBody(ReadFixture("testdata/step242/OCCT/bolt001.step"));

        Assert.True(import.IsSuccess);
        Assert.Equal(4, import.Value.Geometry.Surfaces.Count(surface => surface.Value.Kind == SurfaceGeometryKind.Cylinder));
        Assert.Equal(0, import.Value.Geometry.Surfaces.Count(surface => surface.Value.Kind == SurfaceGeometryKind.BSplineSurfaceWithKnots));
    }

    /// <summary>
    /// A polynomial cubic through the corners of a half-circle looks like a cylinder to any test that only reads the
    /// control net, but it bulges 2.5 mm away from one. Recovery has to measure the surface it would replace, not the
    /// shape of its net, or it silently swaps a blend for a primitive that is nowhere near it.
    /// </summary>
    [Fact]
    public void BsplineRecoveryLane_PolynomialApproximationOfAHalfCircle_IsRejectedInsteadOfSnappedToACylinder()
    {
        var decision = Step242BsplineSurfaceRecoveryLane.Decide(CreateCylinderLikeSurface());

        Assert.Equal("reject", decision.CandidateName);
        Assert.Null(decision.RecoveredSurface);
        Assert.Contains("exact", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BsplineRecoveryLane_ExactRationalCylinder_UsesJudgmentToSelectAnalyticCylinder()
    {
        var decision = Step242BsplineSurfaceRecoveryLane.Decide(CreateExactRationalCylinder(radius: 5d, height: 10d));

        Assert.Equal("analytic_cylinder", decision.CandidateName);
        Assert.NotNull(decision.RecoveredSurface);
        Assert.Equal(SurfaceGeometryKind.Cylinder, decision.RecoveredSurface!.Kind);
        Assert.Equal(5d, decision.RecoveredSurface.Cylinder!.Value.Radius, 9);
    }

    /// <summary>Quarter circle as a rational quadratic - the exact NURBS arc every exporter writes - swept along z.</summary>
    internal static BSplineSurfaceWithKnots CreateExactRationalCylinder(double radius, double height)
    {
        var weight = double.Sqrt(0.5d);
        IReadOnlyList<Point3D> Ring(double z) =>
        [
            new Point3D(radius, 0d, z),
            new Point3D(radius, radius, z),
            new Point3D(0d, radius, z)
        ];

        return new BSplineSurfaceWithKnots(
            degreeU: 1,
            degreeV: 2,
            controlPoints: [Ring(0d), Ring(height)],
            surfaceForm: "CYLINDRICAL_SURF",
            uClosed: false,
            vClosed: false,
            selfIntersect: false,
            knotMultiplicitiesU: [2, 2],
            knotMultiplicitiesV: [3, 3],
            knotValuesU: [0d, 1d],
            knotValuesV: [0d, 1d],
            knotSpec: "PIECEWISE_BEZIER_KNOTS",
            weights: [[1d, weight, 1d], [1d, weight, 1d]]);
    }

    private static BSplineSurfaceWithKnots CreateCylinderLikeSurface()
    {
        var rowTop = new[]
        {
            new Point3D(-5d, 0d, 10d),
            new Point3D(-5d, -10d, 10d),
            new Point3D(5d, -10d, 10d),
            new Point3D(5d, 0d, 10d)
        };

        var rowBottom = rowTop.Select(point => new Point3D(point.X, point.Y, 0d)).ToArray();

        return new BSplineSurfaceWithKnots(
            degreeU: 1,
            degreeV: 3,
            controlPoints: [rowTop, rowBottom],
            surfaceForm: "UNSPECIFIED",
            uClosed: false,
            vClosed: false,
            selfIntersect: false,
            knotMultiplicitiesU: [2, 2],
            knotMultiplicitiesV: [4, 4],
            knotValuesU: [0d, 1d],
            knotValuesV: [0d, 1d],
            knotSpec: "PIECEWISE_BEZIER_KNOTS");
    }

    private static string ReadFixture(string relativePath)
    {
        var absolutePath = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(absolutePath);
    }
}
