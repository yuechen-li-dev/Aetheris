using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Lattice;
using Aetheris.Continuum.Regions.Analytic;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Tests.Lattice;

public sealed class ContinuumM2Tests
{
    [Fact]
    public void BrepSphereCutCell_RetainsExactFaceIdentityAndCirMaterialSideAgreement()
    {
        var transform = Transform3D.CreateRotationX(0.37d) * Transform3D.CreateRotationY(0.61d)
            * Transform3D.CreateTranslation(new Vector3D(0.047d, -0.031d, 0.023d));
        var region = new BrepSphereContinuumRegion(new RegionId("m2-test"), 1d, transform);

        Assert.Equal("brep", region.BoundaryReference.SourceRepresentation);
        Assert.Equal(region.FaceId.Value.ToString(), region.BoundaryReference.ExactBrepFaceId);
        Assert.Equal(SurfaceGeometryKind.Sphere, region.BrepBody.GetFaceSurface(region.FaceId).Kind);
        var boundary = region.ExactQuery.Project(new Point3D(2d, 0.2d, -0.1d));
        var inward = -region.ExactQuery.OutwardNormal(boundary);
        Assert.NotEqual(ContinuumPointClassification.Outside, region.Classify(boundary + (inward * 1e-6d)));
        Assert.Equal(ContinuumPointClassification.Outside, region.Classify(boundary - (inward * 1e-6d)));
    }

    [Fact]
    public void RuntimeMap_UsesAnisotropicEngineeringCertificate_AndOracleHasNoFalseAccept()
    {
        var region = new BrepSphereContinuumRegion(new RegionId("m2-map"), 1d,
            Transform3D.CreateRotationZ(0.29d) * Transform3D.CreateTranslation(new Vector3D(0.047d, -0.031d, 0.023d)));
        var lattice = new LatticeSpec(new BoundingBox3D(new Point3D(-1.4d, -1.4d, -1.4d), new Point3D(1.4d, 1.4d, 1.4d)), 16, 16, 16);
        var index = lattice.Indices().First(i => ContinuumGridClassifier.ClassifyCell(region, lattice.CellBounds(i)) == CellClassification.Cut);
        var bounds = lattice.CellBounds(index);
        var support = (BrepSphereBoundarySupport)region.BoundarySupports(bounds).Single();
        var policy = new BoundaryOffsetMapErrorPolicy(0.00005d, 0.15d, 24);
        var resolution = support.ChooseResolution(bounds, policy);
        var cache = new BoundaryEvaluationCache();
        var runtime = support.Build(index, bounds, resolution.U, resolution.V, policy, cache, runOracle: false);
        var certified = support.Validate(runtime, policy);

        Assert.Equal(0, runtime.Approximation.IndependentValidationPointCount);
        Assert.Equal(0, runtime.Approximation.RuntimeCertificate!.ExactQueryCount);
        Assert.Equal(BoundaryMapCertificateDecision.Acceptable, runtime.Approximation.RuntimeCertificate.Decision);
        Assert.True(certified.Approximation.IsAccepted);
        Assert.True(certified.Approximation.IndependentValidationPointCount > runtime.Samples.Count);
        Assert.NotNull(runtime.SourceBoundary.ExactBrepFaceId);
        Assert.InRange(double.Abs(runtime.LocalFrame.Normal.Dot(runtime.LocalFrame.TangentU)), 0d, 1e-10d);
        Assert.InRange(double.Abs(runtime.LocalFrame.Normal.Dot(runtime.LocalFrame.TangentV)), 0d, 1e-10d);

        _ = support.Build(index, bounds, resolution.U, resolution.V, policy, cache, runOracle: false);
        Assert.True(cache.Hits >= runtime.Samples.Count);
    }

    [Fact]
    public void ExactBrepSphereQuery_RecoversParametersAndNormalsWithoutMeshEvidence()
    {
        var region = new BrepSphereContinuumRegion(new RegionId("m2-query"), 2d,
            Transform3D.CreateRotationX(0.23d) * Transform3D.CreateRotationY(0.47d));
        const double u = 1.2d;
        const double v = -0.4d;
        var point = region.ExactQuery.Evaluate(u, v);
        var recovered = region.ExactQuery.RecoverParameters(point);
        var roundTrip = region.ExactQuery.Evaluate(recovered.U, recovered.V);

        Assert.InRange((roundTrip - point).Length, 0d, 2e-7d);
        Assert.InRange(region.ExactQuery.ExactFaceNormal(u, v).Length, 0.999999d, 1.000001d);
    }
}
