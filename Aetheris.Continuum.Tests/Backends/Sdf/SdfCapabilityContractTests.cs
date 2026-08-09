using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Tests.Backends.Sdf;

public sealed class SdfCapabilityContractTests
{
    [Fact]
    public void PrimitiveAndRigidTransformRetainExactEuclideanDistance()
    {
        var primitive=new SdfSphereNode(2d);
        var rigid=new SdfTransformNode(primitive,Transform3D.CreateRotationY(.37d)*Transform3D.CreateTranslation(new(2,3,4)));
        Assert.True(SdfCapabilityAnalyzer.Analyze(primitive).HasFlag(SdfFieldCapabilities.ExactEuclideanSignedDistance));
        Assert.True(SdfCapabilityAnalyzer.Analyze(rigid).HasFlag(SdfFieldCapabilities.ExactEuclideanSignedDistance));
    }

    [Fact]
    public void NonRigidTransformAndCompositionDoNotAdvertiseExactDistance()
    {
        var scaled=new SdfTransformNode(new SdfSphereNode(1d),Transform3D.CreateScale(new Vector3D(2,1,1)));
        var composed=new SdfUnionNode(new SdfSphereNode(1d),new SdfTransformNode(new SdfSphereNode(1d),Transform3D.CreateTranslation(new(1,0,0))));
        Assert.False(SdfCapabilityAnalyzer.Analyze(scaled).HasFlag(SdfFieldCapabilities.ExactEuclideanSignedDistance));
        Assert.False(SdfCapabilityAnalyzer.Analyze(composed).HasFlag(SdfFieldCapabilities.ExactEuclideanSignedDistance));
        Assert.True(SdfCapabilityAnalyzer.Analyze(scaled).HasFlag(SdfFieldCapabilities.SignCorrectOccupancy));
        Assert.True(SdfCapabilityAnalyzer.Analyze(composed).HasFlag(SdfFieldCapabilities.ConservativeIntervals));
    }

    [Fact]
    public void IntersectionBoundsAreConservativeRatherThanClaimedTight()
    {
        var intersection=new SdfIntersectNode(new SdfBoxNode(2,2,2),new SdfTransformNode(new SdfBoxNode(2,2,2),Transform3D.CreateTranslation(new(10,0,0))));
        Assert.True(intersection.Bounds.SizeX>2d);
        Assert.True(SdfCapabilityAnalyzer.Analyze(intersection).HasFlag(SdfFieldCapabilities.ConservativeIntervals));
        Assert.False(SdfCapabilityAnalyzer.Analyze(intersection).HasFlag(SdfFieldCapabilities.ExactEuclideanSignedDistance));
    }
}
