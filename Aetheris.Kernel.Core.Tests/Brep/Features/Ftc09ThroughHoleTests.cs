using System.Text.Json;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Import;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Tests.Step242;

namespace Aetheris.Kernel.Core.Tests.Brep.Features;

public sealed class Ftc09ThroughHoleTests
{
    const string Path = "testdata/step242/nist/FTC/nist_ftc_09_asme1_ap242-e1.stp";
    const string Candidate = "through-hole:step-1255+1282";
    static BrepBody Source => Step242Corpus.Body(Path);

    [Fact, Trait("Category", "SlowCorpus")]
    public void SelectedSolid_DoesNotAcquireFacesFromSeparateSurfaceModels()
    {
        var source = Source;
        Assert.Equal(158, source.Topology.Faces.Count());
        Assert.Equal(454, source.Topology.Edges.Count());
        Assert.Equal(300, source.Topology.Vertices.Count());
        Assert.DoesNotContain(source.Bindings.FaceBindings, f => f.SourceStepEntityId == 6064);
        Assert.Equal(ImportQualificationStatus.Qualified, source.ImportQualification!.GeometryStatus);
        Assert.All(source.Topology.Coedges.GroupBy(c => c.EdgeId), g =>
        {
            Assert.Equal(2, g.Count()); Assert.NotEqual(g.First().IsReversed, g.Last().IsReversed);
        });
        var discovery = BrepThroughHoleRevision.Discover(source);
        Assert.True(discovery.Qualified);
        Assert.Equal(59, discovery.Candidates.Count);
        Assert.Equal(25, discovery.Candidates.Count(c => c.CanPlan));
        Assert.All(discovery.Candidates.Where(c => !c.Recognized), c => Assert.NotEmpty(c.Diagnostics));
        var hole = Assert.Single(discovery.Candidates, c => c.Id == Candidate);
        Assert.Equal(1, hole.AxisIndex); Assert.Equal(6.35, hole.Diameter, 6);
        Assert.Equal(3.03784, hole.Depth!.Value, 6);
        // A cylindrical fragment of a slot or countersink cannot masquerade as a round hole.
        foreach (var id in new[]{"through-hole:step-198", "through-hole:step-811+838", "through-hole:step-3558"})
        {
            var unsupported = discovery.Candidates.Single(c => c.Id == id);
            Assert.False(unsupported.CanPlan);
            Assert.False(BrepThroughHoleRevision.PlanRemove(source, id, "Unsafe").IsSuccess);
            Assert.False(BrepThroughHoleRevision.PlanMove(source, id, "Unsafe", new(-70,3.03784,19.1)).IsSuccess);
        }
    }

    [Theory, Trait("Category", "SlowCorpus")]
    [InlineData(false)]
    [InlineData(true)]
    public void MoveOrRemove_PreservesResidualGeometryAndChangesOnlyTheSelectedVoid(bool remove)
    {
        var source = Source; var before = JsonSerializer.Serialize(source.Topology);
        var selected = BrepThroughHoleRevision.Discover(source).Candidates.Single(c => c.Id == Candidate);
        var target = new Point3D(-70, selected.Center!.Value.Y, 19.1);
        var planned = BrepThroughHoleRevision.PlanMove(source, Candidate, "MountHole", target);
        Assert.True(planned.IsSuccess, string.Join(" | ", planned.Diagnostics.Select(d => d.Message)));
        BrepBody body;
        if (remove)
        {
            var removed = BrepThroughHoleRevision.PlanRemove(source, Candidate, "MountHole");
            Assert.True(removed.IsSuccess, string.Join(" | ", removed.Diagnostics.Select(d => d.Message)));
            body = removed.Value.Removal.Body;
            Assert.Equal(156, removed.Value.Removal.FaceCorrespondence.Count);
            Assert.All(removed.Value.Removal.RetiredWallFaces, id => Assert.False(removed.Value.Removal.FaceCorrespondence.ContainsKey(id)));
        }
        else body = planned.Value.Revision.Body;
        Assert.Equal(remove ? 156 : 158, body.Topology.Faces.Count());
        Assert.Equal(remove ? 448 : 454, body.Topology.Edges.Count());
        Assert.Equal(remove ? 296 : 300, body.Topology.Vertices.Count());
        foreach (var face in source.Topology.Faces.Where(f => !selected.WallFaces.Contains(f.Id.Value)))
        {
            Assert.Equal(source.GetFaceSurface(face.Id), body.GetFaceSurface(face.Id));
            Assert.Equal(source.Bindings.GetFaceBinding(face.Id), body.Bindings.GetFaceBinding(face.Id));
            if (face.Id.Value != selected.EntryFace && face.Id.Value != selected.ExitFace)
                Assert.Equal(face.LoopIds, body.Topology.GetFace(face.Id).LoopIds);
        }
        foreach (var edge in source.Topology.Edges.Where(e => body.Topology.Edges.Any(b => b.Id == e.Id)))
        {
            Assert.Equal(edge, body.Topology.GetEdge(edge.Id));
            Assert.Equal(source.GetEdgeCurve(edge.Id), body.GetEdgeCurve(edge.Id));
            Assert.Equal(source.Bindings.GetEdgeBinding(edge.Id), body.Bindings.GetEdgeBinding(edge.Id));
        }
        foreach (var vertex in source.Topology.Vertices.Where(v => body.Topology.Vertices.Any(b => b.Id == v.Id)))
        {
            Assert.True(source.TryGetVertexPoint(vertex.Id, out var oldPoint));
            Assert.True(body.TryGetVertexPoint(vertex.Id, out var newPoint)); Assert.Equal(oldPoint, newPoint);
        }
        var coedges = body.Topology.Coedges.Select(c => c.Id).ToHashSet();
        Assert.All(source.Bindings.PcurveBindings.Where(p => coedges.Contains(p.CoedgeId)), p => Assert.Contains(p, body.Bindings.PcurveBindings));
        Assert.Equal(before, JsonSerializer.Serialize(source.Topology));
        AssertPlateBounds(body);
        // Independent trimmed-support queries witness stock versus an opening.
        // Whole-body ray consensus is Unknown for this mixed-spline corpus body;
        // do not weaken that query or pretend an unknown result proves occupancy.
        AssertSupport(source, selected.Center.Value, false);
        AssertSupport(body, selected.Center.Value, true);
        AssertSupport(body, target, remove);
        Assert.True(BrepPcurveValidator.Validate(body, .001, requireEveryCoedge: true, samples: 1025).IsValid);
        var exported = Step242Exporter.ExportBody(body, new Step242ExportOptions { BrepExportPreflightMode = BrepExportPreflightMode.Enforce });
        Assert.True(exported.IsSuccess, string.Join(" | ", exported.Diagnostics.Select(d => d.Message)));
        var imported = Step242Importer.ImportBody(exported.Value);
        Assert.True(imported.IsSuccess);
        Assert.Equal(ImportQualificationStatus.Qualified, imported.Value.ImportQualification!.GeometryStatus);
        Assert.Equal(body.Topology.Faces.Count(), imported.Value.Topology.Faces.Count());
        AssertPlateBounds(imported.Value);
        AssertSupport(imported.Value, selected.Center.Value, true);
        AssertSupport(imported.Value, target, remove);
    }

    static void AssertSupport(BrepBody body, Point3D point, bool expectedMaterial)
    {
        foreach (var y in new[] { 0d, point.Y })
        {
            var support = body.Topology.Faces.Where(f => body.GetFaceSurface(f.Id).Plane is { } p
                && System.Math.Abs(p.Origin.Y - y) < 1e-6 && System.Math.Abs(p.Normal.Y) > .99)
                .MaxBy(f => f.LoopIds.Count)!;
            Assert.True(AnalyticPlanarFaceDomain.TryContainsInPlanarFace(body, support.Id, point with { Y = y },
                Core.Numerics.ToleranceContext.Default, default, out var material));
            Assert.Equal(expectedMaterial, material);
        }
    }

    static void AssertPlateBounds(BrepBody body)
    {
        var points = body.Topology.Vertices.Select(v => { Assert.True(body.TryGetVertexPoint(v.Id, out var p)); return p; }).ToArray();
        Assert.Equal(-95.25, points.Min(p => p.X), 7); Assert.Equal(95.25, points.Max(p => p.X), 7);
        Assert.Equal(0, points.Min(p => p.Y), 7); Assert.Equal(3.03784, points.Max(p => p.Y), 7);
        Assert.Equal(-139.7, points.Min(p => p.Z), 7); Assert.Equal(139.7, points.Max(p => p.Z), 7);
    }

    [Fact, Trait("Category", "SlowCorpus")]
    public void UnprovedDestinationsAndIncompleteSelections_AreRejected()
    {
        var source = Source; var before = JsonSerializer.Serialize(source.Topology);
        var candidates = BrepThroughHoleRevision.Discover(source).Candidates;
        var hole = candidates.Single(c => c.Id == Candidate);
        var occupied = candidates.First(c => c.CanPlan && c.Id != Candidate).Center!.Value;
        Assert.False(BrepThroughHoleRevision.PlanMove(source, Candidate, "Occupied", occupied).IsSuccess);
        Assert.False(BrepThroughHoleRevision.PlanMove(source, Candidate, "OffPlane", new(-70, 4, 19.1)).IsSuccess);
        Assert.False(BrepThroughHoleRevision.PlanMove(source, Candidate, "Outside", new(1000, hole.Center!.Value.Y, 19.1)).IsSuccess);
        var plan = BrepThroughHoleRevision.PlanRemove(source, Candidate, "Valid").Value;
        Assert.False(BrepThroughHoleRevision.Remove(source, plan.Request with { WallFaces = [plan.Request.WallFaces[0]] }).IsSuccess);
        Assert.Equal(before, JsonSerializer.Serialize(source.Topology));
    }
}
