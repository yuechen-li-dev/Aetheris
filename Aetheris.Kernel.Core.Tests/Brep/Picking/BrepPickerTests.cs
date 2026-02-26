using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Picking;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep.Picking;

public sealed class BrepPickerTests
{
    [Fact]
    public void Pick_FaceHitOnBox_ReturnsFaceContractWithPointNormalAndDistance()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var tessellation = BrepDisplayTessellator.Tessellate(box).Value;
        var ray = new Ray3D(new Point3D(0d, 0d, 3d), Direction3D.Create(new Vector3D(0d, 0d, -1d)));

        var result = BrepPicker.Pick(box, tessellation, ray);

        Assert.True(result.IsSuccess);
        var firstFaceHit = Assert.Single(result.Value.Where(h => h.EntityKind == SelectionEntityKind.Face));
        Assert.Equal(new FaceId(2), firstFaceHit.FaceId);
        Assert.Equal(2d, firstFaceHit.T, 8);
        Assert.Equal(0d, firstFaceHit.Point.X, 8);
        Assert.Equal(0d, firstFaceHit.Point.Y, 8);
        Assert.Equal(1d, firstFaceHit.Point.Z, 8);
        Assert.True(firstFaceHit.Normal.HasValue);
        Assert.Equal(1d, firstFaceHit.Normal.Value.Z, 8);
    }

    [Fact]
    public void Pick_RayMiss_ReturnsSuccessfulEmptyHitList()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var tessellation = BrepDisplayTessellator.Tessellate(box).Value;
        var ray = new Ray3D(new Point3D(0d, 0d, 3d), Direction3D.Create(new Vector3D(0d, 1d, 0d)));

        var result = BrepPicker.Pick(box, tessellation, ray);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public void Pick_NearestOnly_IsDeterministic()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var ray = new Ray3D(new Point3D(0d, 0d, 3d), Direction3D.Create(new Vector3D(0d, 0d, -1d)));
        var options = PickQueryOptions.Default with { NearestOnly = true };

        var first = BrepPicker.Pick(box, ray, pickOptions: options);
        var second = BrepPicker.Pick(box, ray, pickOptions: options);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Single(first.Value);
        Assert.Single(second.Value);
        Assert.Equal(first.Value[0], second.Value[0]);
    }

    [Fact]
    public void Pick_BackfaceIsCulledByDefault_AndCanBeEnabled()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var tessellation = BrepDisplayTessellator.Tessellate(box).Value;
        var ray = new Ray3D(new Point3D(0d, 0d, 0d), Direction3D.Create(new Vector3D(0d, 0d, 1d)));

        var culled = BrepPicker.Pick(box, tessellation, ray);
        var includeBackfaces = BrepPicker.Pick(box, tessellation, ray, PickQueryOptions.Default with { IncludeBackfaces = true });

        Assert.True(culled.IsSuccess);
        Assert.True(includeBackfaces.IsSuccess);
        Assert.Empty(culled.Value.Where(h => h.EntityKind == SelectionEntityKind.Face));
        Assert.NotEmpty(includeBackfaces.Value.Where(h => h.EntityKind == SelectionEntityKind.Face));
    }

    [Fact]
    public void Pick_RayNearEdgePolyline_ReturnsEdgeHit()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var tessellation = BrepDisplayTessellator.Tessellate(box).Value;
        var ray = new Ray3D(new Point3D(1.01d, -1d, 0d), Direction3D.Create(new Vector3D(0d, 1d, 0d)));

        var result = BrepPicker.Pick(box, tessellation, ray, PickQueryOptions.Default with { EdgeTolerance = 0.02d });

        Assert.True(result.IsSuccess);
        var edgeHits = result.Value.Where(h => h.EntityKind == SelectionEntityKind.Edge).ToArray();
        Assert.NotEmpty(edgeHits);
        var edgeHit = edgeHits[0];
        Assert.True(edgeHit.EdgeId.HasValue);
        Assert.Equal(1.01d, edgeHit.Point.X, 3);
        Assert.Equal(-1d, edgeHit.Point.Y, 3);
        Assert.Equal(0d, edgeHit.T, 8);
    }

    [Fact]
    public void Pick_EdgeToleranceControlsEdgeHits()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var tessellation = BrepDisplayTessellator.Tessellate(box).Value;
        var ray = new Ray3D(new Point3D(1.01d, -1d, 0d), Direction3D.Create(new Vector3D(0d, 1d, 0d)));

        var result = BrepPicker.Pick(box, tessellation, ray, PickQueryOptions.Default with { EdgeTolerance = 0.005d });

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Where(h => h.EntityKind == SelectionEntityKind.Edge));
    }

    [Fact]
    public void Pick_MultipleEdgeCandidates_AreSortedDeterministicallyByT()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var tessellation = BrepDisplayTessellator.Tessellate(box).Value;
        var ray = new Ray3D(new Point3D(1d, -2d, 1d), Direction3D.Create(new Vector3D(0d, 1d, 0d)));

        var options = PickQueryOptions.Default with { IncludeBackfaces = true, EdgeTolerance = 1e-6d };
        var first = BrepPicker.Pick(box, tessellation, ray, options);
        var second = BrepPicker.Pick(box, tessellation, ray, options);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        var firstEdges = first.Value.Where(h => h.EntityKind == SelectionEntityKind.Edge).ToArray();
        var secondEdges = second.Value.Where(h => h.EntityKind == SelectionEntityKind.Edge).ToArray();
        Assert.NotEmpty(firstEdges);
        Assert.Equal(firstEdges, secondEdges);
    }

    [Fact]
    public void Pick_FaceEdgeTie_UsesEdgeFirstOrdering_AndNearestOnlyMatches()
    {
        var body = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var tessellation = BrepDisplayTessellator.Tessellate(body).Value;
        var ray = new Ray3D(new Point3D(1d, 1d, 3d), Direction3D.Create(new Vector3D(0d, 0d, -1d)));

        var allHits = BrepPicker.Pick(body, tessellation, ray, PickQueryOptions.Default with { IncludeBackfaces = true, EdgeTolerance = 1e-6d, SortTieTolerance = 1e-5d });
        var nearest = BrepPicker.Pick(body, tessellation, ray, PickQueryOptions.Default with { IncludeBackfaces = true, EdgeTolerance = 1e-6d, SortTieTolerance = 1e-5d, NearestOnly = true });

        Assert.True(allHits.IsSuccess);
        Assert.True(nearest.IsSuccess);
        Assert.NotEmpty(allHits.Value);
        Assert.Equal(SelectionEntityKind.Edge, allHits.Value[0].EntityKind);
        Assert.Single(nearest.Value);
        Assert.Equal(SelectionEntityKind.Edge, nearest.Value[0].EntityKind);
    }

    [Fact]
    public void Pick_EmptyTessellation_ReturnsInvalidArgumentDiagnostic()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var empty = new DisplayTessellationResult([], []);
        var ray = new Ray3D(new Point3D(0d, 0d, 3d), Direction3D.Create(new Vector3D(0d, 0d, -1d)));

        var result = BrepPicker.Pick(box, empty, ray);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == KernelDiagnosticCode.InvalidArgument);
    }

    [Fact]
    public void Pick_InvalidOptions_ReturnFailureWithoutThrowing()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var tessellation = BrepDisplayTessellator.Tessellate(box).Value;
        var ray = new Ray3D(new Point3D(0d, 0d, 3d), Direction3D.Create(new Vector3D(0d, 0d, -1d)));

        var result = BrepPicker.Pick(box, tessellation, ray, PickQueryOptions.Default with { EdgeTolerance = -1d });

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == KernelDiagnosticCode.InvalidArgument);
    }
}
