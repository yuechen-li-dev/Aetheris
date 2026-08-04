using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Air;

public sealed class LatticeFillM9Tests
{
    [Fact]
    public void OctetTruss_UsesDeterministicSharedNodesMembersAndBondWitnesses()
    {
        var construction = LatticeFillM9.Construct(CreateFeature());

        Assert.Equal(18, construction.CellDomain.Count);
        Assert.Equal(123, construction.NodeInstances.Count);
        Assert.Equal(300, construction.MemberInstances.Count);
        Assert.NotEmpty(construction.BoundaryIncidents);
        Assert.Equal(construction.BoundaryIncidents.Count, construction.AttachmentWitnesses.Count);
        Assert.All(construction.MemberInstances, member => Assert.True(string.CompareOrdinal(member.StartNodeId, member.EndNodeId) < 0));
        Assert.All(construction.AttachmentWitnesses, witness => Assert.Equal(1.6d, witness.ContactDiameter, 8));
        Assert.Equal(construction.Signature, LatticeFillM9.Construct(CreateFeature()).Signature);
    }

    [Fact]
    public void Validation_RejectsRegionIntersectingTheProtectedThroughHole()
    {
        var feature = CreateFeature() with
        {
            Region = new LatticeFillRegion("Bad", new AxisAlignedBoxExtents(-4, 20, -12, 12, -8, 8), "test")
        };

        var diagnostics = LatticeFillM9.Validate(feature, Host(), 12d, Point3D.Origin);

        Assert.Contains(LatticeFillM9.FillRegionIntersectsVoid, diagnostics);
    }

    [Fact]
    public void Validation_ReportsTypedAdditiveDfmFailures()
    {
        var feature = CreateFeature() with
        {
            StrutRadius = 0.4d,
            AdditiveContext = CreateFeature().AdditiveContext with { MinimumStrutDiameter = 1d, MinimumBondDiameter = 1d }
        };

        var diagnostics = LatticeFillM9.Validate(feature, Host(), 1d, Point3D.Origin);

        Assert.Contains(diagnostics, d => d.StartsWith(LatticeFillM9.MinimumStrutDiameterViolation, StringComparison.Ordinal));
        Assert.Contains(diagnostics, d => d.StartsWith(LatticeFillM9.MinimumBondDiameterViolation, StringComparison.Ordinal));
        Assert.Contains(diagnostics, d => d.StartsWith(LatticeFillM9.MinimumHoleDiameterViolation, StringComparison.Ordinal));
    }

    private static LatticeFillFeature CreateFeature() => new(
        "Body.LightweightCore", "Body",
        new LatticeFillRegion("LightweightCore", new AxisAlignedBoxExtents(10, 34, -12, 12, -8, 8), "test"),
        LatticePatternKind.OctetTruss, 8d, 0.8d, LatticeBoundaryPolicy.Bond,
        new AdditiveManufacturingContext("PolymerPrototype", "FDM", 1.2d, 1d, 1.2d, 2d, "test"),
        new LatticeFillProvenance("test", "LightweightCore", "0:1", []));

    private static AxisAlignedBoxExtents Host() => new(-40, 40, -25, 25, -10, 10);
}
