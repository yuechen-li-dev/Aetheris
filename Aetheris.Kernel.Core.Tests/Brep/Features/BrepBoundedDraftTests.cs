using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Tests.Brep.Features;

public sealed class BrepBoundedDraftTests
{
    [Fact]
    public void DraftAxisAlignedBoxSideFaces_Succeeds_ForBoundedFaces()
    {
        var box = new AxisAlignedBoxExtents(-20d, 20d, -10d, 10d, 0d, 20d);

        var result = BrepBoundedDraft.DraftAxisAlignedBoxSideFaces(
            box,
            draftAngleDegrees: 2d,
            faces: BrepBoundedDraftFaces.XMin | BrepBoundedDraftFaces.XMax);

        Assert.True(result.IsSuccess);
        Assert.Equal(6, result.Value.Topology.Faces.Count());

        var topFace = result.Value.Topology.Faces
            .Select(face => (face, surface: result.Value.Geometry.GetSurface(result.Value.Bindings.FaceBindings.Single(binding => binding.FaceId == face.Id).SurfaceGeometryId)))
            .Single(pair => pair.surface.Kind == SurfaceGeometryKind.Plane
                && ToleranceMath.AlmostEqual(pair.surface.Plane!.Value.Normal.Z, 1d, ToleranceContext.Default));

        Assert.NotNull(topFace.face);
    }

    [Fact]
    public void DraftAxisAlignedBoxSideFaces_Rejects_TooLargeAngle()
    {
        var box = new AxisAlignedBoxExtents(-5d, 5d, -5d, 5d, 0d, 50d);

        var result = BrepBoundedDraft.DraftAxisAlignedBoxSideFaces(
            box,
            draftAngleDegrees: 20d,
            faces: BrepBoundedDraftFaces.XMin | BrepBoundedDraftFaces.XMax | BrepBoundedDraftFaces.YMin | BrepBoundedDraftFaces.YMax);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("collapses top profile", StringComparison.Ordinal));
    }
}
