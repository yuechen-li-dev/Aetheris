using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Brep.Picking;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep;

public sealed class HardeningRegressionCorpusTests
{
    [Fact]
    public void Corpus_Primitives_BoxCylinderSphere_StayConstructible()
    {
        Assert.True(BrepPrimitives.CreateBox(2d, 3d, 4d).IsSuccess);
        Assert.True(BrepPrimitives.CreateCylinder(1d, 5d).IsSuccess);
        Assert.True(BrepPrimitives.CreateSphere(2d).IsSuccess);
    }

    [Fact]
    public void Corpus_Extrude_ValidAndInvalidDepth_AreStable()
    {
        var frame = HardeningRegressionFixtures.CanonicalFrame();
        var good = BrepExtrude.Create(HardeningRegressionFixtures.CanonicalExtrudeProfile(), frame, 3d);
        var bad = BrepExtrude.Create(HardeningRegressionFixtures.CanonicalExtrudeProfile(), frame, 0d);

        Assert.True(good.IsSuccess);
        Assert.False(bad.IsSuccess);
        Assert.Contains(bad.Diagnostics, d => d.Code == KernelDiagnosticCode.InvalidArgument);
    }

    [Fact]
    public void Corpus_Revolve_SupportedAndUnsupported_Subsets_AreStable()
    {
        var frame = HardeningRegressionFixtures.CanonicalFrame();
        var axis = new RevolveAxis3D(frame.Origin, frame.Normal.ToVector());

        var supported = BrepRevolve.Create(HardeningRegressionFixtures.CanonicalRevolveSupportedProfile, frame, axis, System.Math.PI * 2d);
        var unsupported = BrepRevolve.Create(HardeningRegressionFixtures.CanonicalRevolveUnsupportedProfile, frame, axis, System.Math.PI);

        Assert.True(supported.IsSuccess);
        Assert.False(unsupported.IsSuccess);
        Assert.Contains(unsupported.Diagnostics, d => d.Code == KernelDiagnosticCode.NotImplemented);
    }

    [Fact]
    public void Corpus_BoxBoolean_SupportedAndUnsupported_Matrix_AreStable()
    {
        var left = HardeningRegressionFixtures.OverlapLeftBox();
        var overlapRight = HardeningRegressionFixtures.OverlapRightBox();
        var touchingRight = HardeningRegressionFixtures.TouchingOnlyBox();
        var lShapeLeft = HardeningRegressionFixtures.LShapeUnionLeftBox();
        var lShapeRight = HardeningRegressionFixtures.LShapeUnionRightBox();
        var contained = HardeningRegressionFixtures.ContainedBox();

        Assert.True(BrepBoolean.Intersect(left, overlapRight).IsSuccess);
        Assert.False(BrepBoolean.Intersect(left, touchingRight).IsSuccess);
        Assert.True(BrepBoolean.Union(lShapeLeft, lShapeRight).IsSuccess);

        var containmentUnion = BrepBoolean.Union(left, contained);
        Assert.True(containmentUnion.IsSuccess);
    }

    [Fact]
    public void Corpus_TessellationAndPick_FaceEdgeTieOrdering_StaysDeterministic()
    {
        var body = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var tessellation = BrepDisplayTessellator.Tessellate(body).Value;

        var first = BrepPicker.Pick(body, tessellation, HardeningRegressionFixtures.TopDownEdgeTieRay(), PickQueryOptions.Default with { IncludeBackfaces = true, EdgeTolerance = 1e-6d, SortTieTolerance = 1e-5d });
        var second = BrepPicker.Pick(body, tessellation, HardeningRegressionFixtures.TopDownEdgeTieRay(), PickQueryOptions.Default with { IncludeBackfaces = true, EdgeTolerance = 1e-6d, SortTieTolerance = 1e-5d });

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotEmpty(first.Value);
        Assert.Equal(first.Value, second.Value);
        Assert.Equal(SelectionEntityKind.Edge, first.Value[0].EntityKind);
    }
}
