using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Lattice;
using Aetheris.Continuum.Regions.Analytic;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.StandardLibrary;

namespace Aetheris.Continuum.Tests.Lattice;

public sealed class ContinuumM3Tests
{
    [Fact]
    public void ExactTorusQuery_RoundTripsPeriodicParametersAndPrincipalDirections()
    {
        var body = BrepPrimitives.CreateTorus(2d, 0.4d).Value!;
        var face = body.Topology.Faces.Single().Id;
        var query = new ExactBrepBoundaryQuery(body, face, Transform3D.CreateRotationX(0.31d) * Transform3D.CreateRotationZ(0.47d));
        var point = query.Evaluate((2d * double.Pi) - 1e-8d, 1e-8d);
        var recovered = query.RecoverParameters(point);
        var roundTrip = query.Evaluate(recovered.U, recovered.V);
        var curvature = query.PrincipalCurvatures(recovered.U, recovered.V);

        Assert.InRange((roundTrip - point).Length, 0d, 5e-8d);
        Assert.InRange(double.Abs(curvature.DirectionU.Dot(curvature.DirectionV)), 0d, 1e-7d);
        Assert.InRange(double.Abs(double.Abs(curvature.CurvatureV) - 2.5d), 0d, 1e-10d);
        Assert.InRange(double.Abs(ExactBrepBoundaryQuery.UnwrapPeriodic(1e-8d, (2d * double.Pi) - 1e-8d) - (2d * double.Pi + 1e-8d)), 0d, 1e-12d);
    }

    [Fact]
    public void ProductionRootFillet_PreservesFaceIdentityContactsAndMaterialSide()
    {
        var region = Fixture(Transform3D.Identity);
        Assert.True(region.ContactValidation.Passed, string.Join(';', region.ContactValidation.Evidence));
        Assert.Equal(2, region.TorusFaceIds.Count);
        Assert.All(region.TorusFaceIds, id => Assert.Equal("Torus", region.BrepBody.GetFaceSurface(id).Kind.ToString()));
        var support = (BrepTorusBoundarySupport)region.BoundarySupports(new(new(0.05d, 0.55d, 0.05d), new(0.2d, 0.9d, 0.4d))).First();
        var boundary = support.Query.Evaluate(0.7d, 1.25d * double.Pi);
        var material = support.MaterialSideNormal(boundary);
        Assert.NotEqual(ContinuumPointClassification.Outside, region.Classify(boundary + (material * 1e-6d)));
        Assert.Equal(ContinuumPointClassification.Outside, region.Classify(boundary - (material * 1e-6d)));
    }

    [Fact]
    public void TorusOffsetMap_UsesAnisotropicCertificateAndStructuredIntegrationDeterministically()
    {
        var region = Fixture(Transform3D.CreateRotationY(0.23d) * Transform3D.CreateRotationZ(0.41d));
        var lattice = new LatticeSpec(region.Bounds, 24, 24, 24);
        var index = lattice.Indices().Where(i =>
        {
            var b = lattice.CellBounds(i); if (!region.IsRootFilletCandidate(b)) return false;
            var center = new Point3D((b.Min.X + b.Max.X) * 0.5d, (b.Min.Y + b.Max.Y) * 0.5d, (b.Min.Z + b.Max.Z) * 0.5d);
            var s = (BrepTorusBoundarySupport)region.BoundarySupports(b).First(); var p = s.Query.Project(center);
            return p.X >= b.Min.X && p.X <= b.Max.X && p.Y >= b.Min.Y && p.Y <= b.Max.Y && p.Z >= b.Min.Z && p.Z <= b.Max.Z;
        }).MinBy(i =>
        {
            var b = lattice.CellBounds(i); var center = new Point3D((b.Min.X + b.Max.X) * 0.5d, (b.Min.Y + b.Max.Y) * 0.5d, (b.Min.Z + b.Max.Z) * 0.5d);
            var s = (BrepTorusBoundarySupport)region.BoundarySupports(b).First();
            var v = ExactBrepBoundaryQuery.UnwrapPeriodic(s.Query.RecoverParameters(s.Query.Project(center)).V, 1.25d * double.Pi);
            return double.Abs(v - (1.25d * double.Pi));
        });
        var bounds = lattice.CellBounds(index);
        var support = (BrepTorusBoundarySupport)region.BoundarySupports(bounds).First();
        var policy = new BoundaryOffsetMapErrorPolicy(0.002d, 1d, 24);
        var resolution = support.ChooseResolution(bounds, policy);
        var runtime = support.Build(index, bounds, resolution.U, resolution.V, policy, new(), false);
        var validated = support.Validate(runtime, policy);
        var first = BoundaryOffsetMap3DIntegrator.IntegrateStructured(validated, bounds);
        var second = BoundaryOffsetMap3DIntegrator.IntegrateStructured(validated, bounds);

        Assert.NotEqual(resolution.U, resolution.V);
        Assert.Equal(BoundaryMapCertificateDecision.Acceptable, runtime.Approximation.RuntimeCertificate!.Decision);
        Assert.True(validated.Approximation.IsAccepted);
        Assert.Equal(first.Estimate, second.Estimate);
        Assert.Equal(first.Diagnostics, second.Diagnostics);
        Assert.Equal(first.Footprint.Vertices, second.Footprint.Vertices);
        Assert.InRange(first.Estimate.OccupancyFraction, 0d, 1d);
        Assert.True(first.Footprint.Vertices.Count >= 4);
        Assert.True(first.Diagnostics.ThicknessEvaluations < 50000);
    }

    [Fact]
    public void StructuredIntegrator_PreservesSphereVolumeAndAreaAgainstDenseControl()
    {
        var region = new BrepSphereContinuumRegion(new RegionId("m3-sphere"), 1d, Transform3D.CreateTranslation(new(0.047d, -0.031d, 0.023d)));
        var lattice = new LatticeSpec(new(new(-1.4d, -1.4d, -1.4d), new(1.4d, 1.4d, 1.4d)), 16, 16, 16);
        var index = lattice.Indices().First(i => ContinuumGridClassifier.ClassifyCell(region, lattice.CellBounds(i)) == CellClassification.Cut);
        var bounds = lattice.CellBounds(index); var support = (BrepSphereBoundarySupport)region.BoundarySupports(bounds).Single();
        var policy = new BoundaryOffsetMapErrorPolicy(0.00005d, 0.15d, 24); var r = support.ChooseResolution(bounds, policy);
        var map = support.Build(index, bounds, r.U, r.V, policy, new(), false);
        var structured = BoundaryOffsetMap3DIntegrator.IntegrateStructured(map, bounds).Estimate;
        var dense = BoundaryOffsetMap3DIntegrator.IntegrateDenseOracle(map, bounds, 96, 8);
        Assert.InRange(double.Abs(structured.OccupancyFraction - dense.OccupancyFraction), 0d, 0.002d);
        Assert.InRange(double.Abs(structured.BoundaryArea - dense.BoundaryArea), 0d, 0.002d);
    }

    private static BrepTorusRootFilletContinuumRegion Fixture(Transform3D transform)
    {
        const double r = 0.25d; const double shaftRadius = 0.75d;
        var recipe = new ExactCoaxialPartRecipe("m3-fixture", 12, 2.8d, 0.5d, 1d, 30d, r, 2d * shaftRadius,
            1.5d, 0.2d, 1.2d, 0.7d, "M3", "test");
        var plan = ExactCoaxialPartBuilder.Plan(recipe).Value!;
        var result = ExactConstructionMaterializer.Materialize(plan).Value!;
        return new(new("m3-root"), result.Body, result.FaceGroups["RootBlend"], shaftRadius + r, r, 1.4d, 0.5d, 1.5d, transform);
    }
}
