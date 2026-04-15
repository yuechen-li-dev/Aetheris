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

    [Fact]
    public void BsplineRecoveryLane_NonRationalCylinderLikeSurface_ExplicitlyRejectsRecovery()
    {
        var decision = Step242BsplineSurfaceRecoveryLane.Decide(
            CreateNonRationalSurfaceEntity(),
            CreateCylinderLikeSurface());

        Assert.Equal("reject", decision.CandidateName);
        Assert.Null(decision.RecoveredSurface);
        Assert.Contains("not a rational", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BsplineRecoveryLane_RationalCylinderLikeSurface_UsesJudgmentToSelectAnalyticCylinder()
    {
        var decision = Step242BsplineSurfaceRecoveryLane.Decide(
            CreateRationalSurfaceEntity(),
            CreateCylinderLikeSurface());

        Assert.Equal("analytic_cylinder", decision.CandidateName);
        Assert.NotNull(decision.RecoveredSurface);
        Assert.Equal(SurfaceGeometryKind.Cylinder, decision.RecoveredSurface!.Kind);
    }

    private static Step242ParsedEntity CreateRationalSurfaceEntity()
    {
        var constructors = new[]
        {
            new Step242EntityConstructor("B_SPLINE_SURFACE_WITH_KNOTS", []),
            new Step242EntityConstructor("RATIONAL_B_SPLINE_SURFACE", [new Step242ListValue([])]),
            new Step242EntityConstructor("SURFACE", [])
        };

        return new Step242ParsedEntity(1, new Step242ComplexEntityInstance(constructors));
    }

    private static Step242ParsedEntity CreateNonRationalSurfaceEntity()
    {
        return new Step242ParsedEntity(
            1,
            new Step242SimpleEntityInstance(new Step242EntityConstructor("B_SPLINE_SURFACE_WITH_KNOTS", [])));
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
