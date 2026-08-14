using Aetheris.Kernel.Core.Math;
using Xunit;

namespace Aetheris.Reconstruction.Tests;

public sealed class FastSurfaceReconstructionTests
{
    [Fact]
    public void Fast_open_sheet_is_crack_free_quad_and_preserves_boundary()
    {
        var result = SurfaceReconstruction.Remesh(Mesh(
            [new(0,0,0), new(1,0,0), new(1,1,.1), new(0,1,.1)],
            [new(0,1,2), new(0,2,3)], "open-sheet"));

        Assert.Equal(ReconstructionStatus.Success, result.Status);
        Assert.NotNull(result.Mesh); Assert.NotNull(result.Quality); Assert.NotNull(result.Statistics);
        Assert.Equal(1, result.Statistics.Topology.Quads);
        Assert.Equal(0, result.Statistics.Topology.Triangles);
        Assert.Equal(1, result.Statistics.Topology.BoundaryLoops);
        Assert.Equal(0, result.Statistics.Topology.InternalCracks);
        Assert.Equal(0, result.Statistics.Topology.NonManifoldEdges);
        Assert.All(result.Mesh!.Patches, patch => Assert.Equal(
            Aetheris.Kernel.Core.Brep.Tessellation.SurfaceMeshSupportKind.BoundedParametricPatch, patch.Support.Kind));
    }

    [Fact]
    public void Fast_closed_sphereish_and_creased_box_are_generic_and_mostly_quads()
    {
        var sphere = SurfaceReconstruction.Remesh(Octahedron());
        var box = SurfaceReconstruction.Remesh(Box());

        Assert.Equal(ReconstructionStatus.Success, sphere.Status);
        Assert.Equal(0, sphere.Statistics!.Topology.BoundaryLoops);
        Assert.Equal(100, sphere.Statistics.Topology.QuadPercentage);
        Assert.Equal(ReconstructionStatus.Success, box.Status);
        Assert.Equal(0, box.Statistics!.Topology.BoundaryLoops);
        Assert.True(box.Statistics.Topology.QuadPercentage >= 95);
        Assert.Contains(box.Structure!.QuadRegions, c => c.FieldAlignmentDegrees >= 0);
    }

    [Fact]
    public void Fast_is_deterministic_and_correspondence_cache_invalidates_only_one_region()
    {
        var source = Octahedron(); var first = SurfaceReconstruction.Remesh(source); var second = SurfaceReconstruction.Remesh(source);
        Assert.Equal(first.Statistics!.DeterministicHash, second.Statistics!.DeterministicHash);
        Assert.True(first.Correspondences.HitCount > 0); Assert.True(first.Correspondences.ProjectionCallCount > 0);
        var before = first.Correspondences.Count;
        var region = first.Correspondences.Entries.First().TargetRegionId;
        var expected = first.Correspondences.Entries.Count(x => x.TargetRegionId == region);
        Assert.Equal(expected, first.Correspondences.InvalidateRegion(region));
        Assert.Equal(before - expected, first.Correspondences.Count);
    }

    [Fact]
    public void Fast_returns_typed_unsupported_result_for_nonmanifold_input()
    {
        var source = Mesh([new(0,0,0),new(1,0,0),new(0,1,0),new(0,-1,0),new(0,0,1)],
            [new(0,1,2),new(1,0,3),new(0,1,4)], "nonmanifold");
        var result = SurfaceReconstruction.Remesh(source);
        Assert.Equal(ReconstructionStatus.Unsupported, result.Status);
        Assert.Null(result.Mesh);
        Assert.Contains(result.Diagnostics, d => d.Code == ReconstructionDiagnosticCode.UnsupportedTopology);
    }

    private static TriangleSurfaceMesh Octahedron() => Mesh(
        [new(1,0,0),new(-1,0,0),new(0,1,0),new(0,-1,0),new(0,0,1),new(0,0,-1)],
        [new(4,0,2),new(4,2,1),new(4,1,3),new(4,3,0),new(5,2,0),new(5,1,2),new(5,3,1),new(5,0,3)], "sphere-ish");

    private static TriangleSurfaceMesh Box() => Mesh(
        [new(-1,-1,-1),new(1,-1,-1),new(1,1,-1),new(-1,1,-1),new(-1,-1,1),new(1,-1,1),new(1,1,1),new(-1,1,1)],
        [new(0,2,1),new(0,3,2),new(4,5,6),new(4,6,7),new(0,1,5),new(0,5,4),new(1,2,6),new(1,6,5),new(2,3,7),new(2,7,6),new(3,0,4),new(3,4,7)], "creased-box");

    private static TriangleSurfaceMesh Mesh(Point3D[] vertices, Triangle[] triangles, string id)
        => new(vertices, triangles, null, id, id + "-hash", new Dictionary<string, string>());
}
