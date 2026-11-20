using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Tests.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep.Features;

public sealed class BrepThroughHoleInsertionTests
{
    [Fact]
    public void ImportedPlateGainsABoreAndKeepsItsExistingTopologyAndGeometry()
    {
        var source = Reopen(BrepPrimitives.CreateBox(40, 30, 8).Value);
        var (entry, exit) = Supports(source);
        var original = Step242Exporter.ExportBody(source).Value;
        var inserted = BrepThroughHoleRevision.TryInsertZ(source, entry, exit, new(-8, 2, 4), 6);
        Assert.True(inserted.IsSuccess, string.Join(" | ", inserted.Diagnostics.Select(item => item.Message)));

        var (body, region, wall) = inserted.Value;
        Assert.InRange((region.Bounds.Min - new Point3D(-11.1, -1.1, -4)).Length, 0, 1e-9);
        Assert.InRange((region.Bounds.Max - new Point3D(-4.9, 5.1, 4)).Length, 0, 1e-9);
        Assert.Equal(3.1, region.ClearanceRadius, 10);
        Assert.Equal(source.Topology.Faces.Count() + 1, body.Topology.Faces.Count());
        Assert.Equal(3, body.GetFaceSurface(wall).Cylinder!.Value.Radius, 10);
        Assert.Null(body.Bindings.GetFaceBinding(wall).SourceStepEntityId);
        foreach (var face in source.Topology.Faces)
        {
            var loops = body.Topology.GetFace(face.Id).LoopIds;
            Assert.Equal(face.LoopIds, loops.Take(face.LoopIds.Count));
            Assert.Equal(face.LoopIds.Count + (face.Id == entry || face.Id == exit ? 1 : 0), loops.Count);
            Assert.Equal(source.Bindings.GetFaceBinding(face.Id), body.Bindings.GetFaceBinding(face.Id));
            Assert.Same(source.GetFaceSurface(face.Id), body.GetFaceSurface(face.Id));
        }
        foreach (var edge in source.Topology.Edges)
        {
            Assert.Equal(edge, body.Topology.GetEdge(edge.Id));
            Assert.Equal(source.Bindings.GetEdgeBinding(edge.Id), body.Bindings.GetEdgeBinding(edge.Id));
            Assert.Same(source.GetEdgeCurve(edge.Id), body.GetEdgeCurve(edge.Id));
        }
        foreach (var loop in source.Topology.Loops) Assert.Equal(loop, body.Topology.GetLoop(loop.Id));
        foreach (var use in source.Topology.Coedges) Assert.Equal(use, body.Topology.GetCoedge(use.Id));
        foreach (var vertex in source.Topology.Vertices)
        {
            Assert.Equal(vertex, body.Topology.GetVertex(vertex.Id));
            Assert.True(source.TryGetVertexPoint(vertex.Id, out var originalPoint));
            Assert.True(body.TryGetVertexPoint(vertex.Id, out var retainedPoint));
            Assert.Equal(originalPoint, retainedPoint);
        }
        foreach (var binding in source.Bindings.PcurveBindings)
            Assert.Equal(binding, body.Bindings.GetPcurveBinding(binding.CoedgeId));
        Assert.Equal(original, Step242Exporter.ExportBody(source).Value);

        var reopened = Reopen(body);
        var cylinder = Assert.Single(reopened.Topology.Faces
            .Select(face => reopened.GetFaceSurface(face.Id).Cylinder).OfType<CylinderSurface>());
        Assert.Equal(3, cylinder.Radius, 10);
        Assert.Equal(-8, cylinder.Origin.X, 10);
        Assert.Equal(2, cylinder.Origin.Y, 10);
    }

    [Fact]
    public void EnvelopeRejectsBreakoutAndExistingBoresButAllowsASeparatedSecondBore()
    {
        var blank = Reopen(BrepPrimitives.CreateBox(40, 30, 8).Value);
        var (entry, exit) = Supports(blank);
        var breakout = BrepThroughHoleRevision.TryInsertZ(blank, entry, exit, new(18, 0, 4), 6);
        Assert.False(breakout.IsSuccess);
        Assert.Contains(breakout.Diagnostics, item => item.Message.Contains("support boundary"));
        var tangent = BrepThroughHoleRevision.TryInsertZ(blank, entry, exit, new(17, 0, 4), 6);
        Assert.False(tangent.IsSuccess);
        var outside = BrepThroughHoleRevision.TryInsertZ(blank, entry, exit, new(23, 0, 4), 6);
        Assert.False(outside.IsSuccess);
        Assert.Contains(outside.Diagnostics, item => item.Message.Contains("material domain"));
        var first = BrepThroughHoleRevision.TryInsertZ(blank, entry, exit, new(-8, 0, 4), 6);
        Assert.True(first.IsSuccess, string.Join(" | ", first.Diagnostics.Select(item => item.Message)));
        var stock = Reopen(first.Value.Body);
        (entry, exit) = Supports(stock);
        var original = Step242Exporter.ExportBody(stock).Value;
        var collision = BrepThroughHoleRevision.TryInsertZ(stock, entry, exit, new(-3, 0, 4), 6);
        Assert.False(collision.IsSuccess);
        Assert.Contains(collision.Diagnostics, item => item.Message.Contains("support boundary or opening"));
        Assert.Equal(original, Step242Exporter.ExportBody(stock).Value);
        // The axis-aligned envelope overlaps the first bore's bounds in both
        // radial coordinates. The two circular footprints still have clearance.
        var separated = BrepThroughHoleRevision.TryInsertZ(stock, entry, exit, new(-3.6, 4.4, 4), 6);
        Assert.True(separated.IsSuccess, string.Join(" | ", separated.Diagnostics.Select(item => item.Message)));
        var reopened = Reopen(separated.Value.Body);
        Assert.Equal(2, reopened.Topology.Faces.Count(face => reopened.GetFaceSurface(face.Id).Cylinder is not null));
    }

    [Fact]
    public void ImportedRoundStockAdmitsAConcentricBoreAndRejectsInsufficientRimClearance()
    {
        var stock = Step242Corpus.Body("testdata/step242/handcrafted/baseline/cylinder.step");
        var stations = stock.Topology.Faces.Select(face => stock.GetFaceSurface(face.Id).Plane)
            .OfType<PlaneSurface>().Select(plane => plane.Origin.Z).Order().ToArray();
        var entry = SupportAt(stock, stations[^1]);
        var exit = SupportAt(stock, stations[0]);
        var witness = BrepThroughHoleRevision.TryInsertZ(stock, entry, exit, new(0, 0, stations[^1]), .2);
        Assert.True(witness.IsSuccess, string.Join(" | ", witness.Diagnostics.Select(item => item.Message)));
        var reopened = Reopen(witness.Value.Body);
        var radii = reopened.Topology.Faces.Select(face => reopened.GetFaceSurface(face.Id).Cylinder)
            .OfType<CylinderSurface>().Select(cylinder => cylinder.Radius).Order().ToArray();
        Assert.Collection(radii, radius => Assert.Equal(.1, radius, 10), radius => Assert.Equal(.5, radius, 10));

        var nearRim = BrepThroughHoleRevision.TryInsertZ(stock, entry, exit, new(.35, 0, stations[^1]), .2);
        Assert.False(nearRim.IsSuccess);
        Assert.Contains(nearRim.Diagnostics, item => item.Message.Contains("support boundary"));
    }

    private static (FaceId Entry, FaceId Exit) Supports(BrepBody body) =>
        (SupportAt(body, 4), SupportAt(body, -4));

    private static FaceId SupportAt(BrepBody body, double z) => body.Topology.Faces.Single(face =>
        body.GetFaceSurface(face.Id).Plane is { } plane && double.Abs(plane.Normal.Z) > .99 && double.Abs(plane.Origin.Z - z) < 1e-8).Id;

    private static BrepBody Reopen(BrepBody body)
    {
        var exported = Step242Exporter.ExportBody(body, new Step242ExportOptions { BrepExportPreflightMode = BrepExportPreflightMode.Enforce });
        Assert.True(exported.IsSuccess, string.Join(" | ", exported.Diagnostics.Select(item => item.Message)));
        var imported = Step242Importer.ImportBody(exported.Value);
        Assert.True(imported.IsSuccess, string.Join(" | ", imported.Diagnostics.Select(item => item.Message)));
        return imported.Value;
    }
}
