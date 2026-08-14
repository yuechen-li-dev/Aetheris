using System.Text;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;
using Xunit;

namespace Aetheris.Reconstruction.Tests;

public sealed class ReconstructionTests
{
    [Fact]
    public void Ply_loader_and_validation_preserve_open_boundary_as_evidence()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("""
ply
format ascii 1.0
element vertex 4
property float x
property float y
property float z
property float confidence
element face 2
property list uchar int vertex_indices
end_header
0 0 0 1
1 0 0 1
1 1 0 1
0 1 0 1
3 0 1 2
3 0 2 3
"""));
        var mesh = PlyTriangleSurfaceLoader.LoadAscii(stream, "unit-square"); var report = TriangleSurfaceValidator.Validate(mesh);
        Assert.Equal(4, report.VertexCount); Assert.Equal(2, report.TriangleCount); Assert.Equal(4, report.BoundaryEdgeCount); Assert.Equal(1, report.BoundaryLoopCount); Assert.Equal(1, report.ConnectedComponents); Assert.True(report.OrientationConsistent);
    }

    [Fact]
    public void Bvh_returns_nearest_point_and_bounds_candidates_deterministically()
    {
        var mesh = Square(); var bvh = new TriangleBvh(mesh); var hit = bvh.Nearest(new(.25, .25, 2));
        Assert.Equal(2, hit.Distance, 12); Assert.Equal(0, hit.Point.Z); Assert.Contains(hit.TriangleIndex, bvh.Query(new(new(0, 0, -.1), new(.6, .6, .1))));
    }

    [Fact]
    public void Recovery_builds_bounded_second_jet_panels_and_quad_mesh()
    {
        var mesh = Grid(5); var (field, summary) = StructuredSurfaceRecovery.EstimateField(mesh); var charts = StructuredSurfaceRecovery.BuildCharts(mesh, field, spatialBins: 1, minimumFaces: 2); var output = PanelSurfaceMeshLowering.Lower(charts, 3);
        var chart = Assert.Single(charts.Charts); Assert.Equal("Accepted", chart.Status); Assert.True(chart.Patch.SupportsSecondJet); Assert.True(chart.Patch.EvaluateJet2(.5, .5).IsRegular); Assert.Equal(9, output.Quads.Count); Assert.Equal(0, output.TriangleCount); Assert.Equal(summary.SampleCount, summary.KnownDirectionCount);
        var panel = RecoveredPanelMaterializer.Materialize(chart); Assert.True(panel.IsSuccess); Assert.NotNull(panel.Panel);
    }

    [Fact]
    public void Recovery_hash_is_stable()
    {
        var mesh = Grid(4); var a = Run(mesh); var b = Run(mesh); Assert.Equal(a.DeterministicHash, b.DeterministicHash);
        static StructuredSurfaceMesh Run(TriangleSurfaceMesh m) { var (f, _) = StructuredSurfaceRecovery.EstimateField(m); return PanelSurfaceMeshLowering.Lower(StructuredSurfaceRecovery.BuildCharts(m, f, 1, 2), 4); }
    }

    [Fact]
    public void Two_chart_fixture_has_one_internal_seam_authority_with_opposite_side_orientation()
    {
        var mesh = Square();
        var network = RecoveredSeamNetworkBuilder.Build(mesh, ["chart-a", "chart-b"]);
        var seam = Assert.Single(network.Seams, item => item.Classification == RecoveredSeamClassification.Internal);

        Assert.Equal("chart-a", seam.ChartA);
        Assert.Equal("chart-b", seam.ChartB);
        Assert.Equal(RecoveredSeamOrientation.SameDirection, seam.LeftOrientation);
        Assert.Equal(RecoveredSeamOrientation.ReversedDirection, seam.RightOrientation);
        Assert.Equal(0, seam.G0Residual);
        Assert.Equal(0, seam.ParameterStart);
        Assert.Equal(1, seam.ParameterEnd);
        Assert.Equal(2, network.Junctions.Count);
        Assert.All(network.Junctions, junction => Assert.Contains(seam.StableId, junction.IncidentSeamIds));
    }

    [Fact]
    public void Source_open_boundary_is_retained_and_is_not_an_internal_crack()
    {
        var mesh = Square(); var (field, _) = StructuredSurfaceRecovery.EstimateField(mesh);
        var charts = StructuredSurfaceRecovery.BuildCharts(mesh, field, spatialBins: 1, minimumFaces: 1);
        var canonical = SeamAuthoritativeSurfaceMeshLowering.Lower(mesh, charts);

        Assert.Equal(0, canonical.InternalCrackGroups);
        Assert.Equal(1, canonical.IntentionalOpenBoundaryLoops);
        Assert.Equal(4, canonical.BoundaryEdgeCount);
        Assert.Equal(0, canonical.NonManifoldEdgeCount);
        Assert.Equal(1, canonical.QuadCount);
        Assert.Equal(0, canonical.TriangleCount);
        Assert.Equal("Pass", canonical.ValidationStatus);
        Assert.True(SurfaceMeshIrValidator.TryValidate(canonical.Document, out _));
        Assert.All(charts.Seams, seam => Assert.Equal(RecoveredSeamClassification.SourceOpenBoundary, seam.Classification));
    }

    [Fact]
    public void Seam_judgment_rejects_line_when_bounded_residual_is_exceeded_and_is_stable()
    {
        var vertices = new Point3D[]
        {
            new(0,0,0), new(1,0,0), new(2,0,0),
            new(0,1,0), new(1,1,.2), new(2,1,0),
            new(0,2,0), new(1,2,0), new(2,2,0)
        };
        var triangles = new Triangle[]
        {
            new(0,1,4), new(0,4,3), new(3,4,7), new(3,7,6),
            new(1,2,5), new(1,5,4), new(4,5,8), new(4,8,7)
        };
        var mesh = new TriangleSurfaceMesh(vertices, triangles, null, "bent-two-chart", "fixture", new Dictionary<string,string>());
        var labels = new[] { "left", "left", "left", "left", "right", "right", "right", "right" };
        var first = RecoveredSeamNetworkBuilder.Build(mesh, labels);
        var second = RecoveredSeamNetworkBuilder.Build(mesh, labels);
        var seam = Assert.Single(first.Seams, item => item.Classification == RecoveredSeamClassification.Internal);

        Assert.Equal(RecoveredSeamRepresentation.NonRationalBSpline, seam.RepresentationKind);
        Assert.Equal("NonRationalBSpline", seam.JudgmentWinner);
        Assert.False(seam.JudgmentCandidates.Single(candidate => candidate.Representation == "Line").Admissible);
        Assert.Equal(seam.StableId, second.Seams.Single(item => item.Classification == RecoveredSeamClassification.Internal).StableId);
        Assert.Equal(seam.JudgmentWinner, second.Seams.Single(item => item.StableId == seam.StableId).JudgmentWinner);
    }

    private static TriangleSurfaceMesh Square() => new([new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0)], [new(0, 1, 2), new(0, 2, 3)], null, "square", "fixture", new Dictionary<string, string>());
    private static TriangleSurfaceMesh Grid(int n)
    {
        var p = new List<Point3D>(); for (var j = 0; j < n; j++) for (var i = 0; i < n; i++) p.Add(new(i / (double)(n - 1), j / (double)(n - 1), .02 * i * j)); var t = new List<Triangle>(); for (var j = 0; j < n - 1; j++) for (var i = 0; i < n - 1; i++) { var a = j * n + i; t.Add(new(a, a + 1, a + n + 1)); t.Add(new(a, a + n + 1, a + n)); } return new(p, t, null, "grid", "fixture", new Dictionary<string, string>());
    }
}
