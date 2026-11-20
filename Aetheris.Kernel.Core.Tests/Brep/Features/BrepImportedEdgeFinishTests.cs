using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Tests.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep.Features;

public sealed class BrepImportedEdgeFinishTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FinishesAFreeCadBlockEdgeAndReopensItInStep(bool fillet)
    {
        var source = Step242Corpus.Body("testdata/step242/handcrafted/baseline/block-basic.step");
        var selected = source.Topology.Edges.First().Id;
        VerifyFinish(source, selected, fillet, .1, .02);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FinishesAnObliqueConvexEdgeOnRotatedNonconvexStock(bool fillet)
    {
        var (source, frame) = RotatedStock();
        var selected = EdgeAt(source, frame.ToWorld(new(16, 7)), frame.ToWorld(new(16, 7), 9));
        var witness = VerifyFinish(source, selected, fillet, 1.2, .15);

        // Check the actual setback and tangent normals for a non-right dihedral.
        var supportNormals = witness.ModifiedFaces.Take(2).Select(face =>
        {
            var binding = source.Bindings.GetFaceBinding(face);
            var normal = source.GetFaceSurface(face).Plane!.Value.Normal.ToVector();
            return normal * (binding.Orientation.IsAlignedWithSurface ? 1 : -1);
        }).ToArray();
        var cosine = supportNormals[0].Dot(supportNormals[1]);
        Assert.InRange(double.Abs(cosine), .1, .99);
        var oldCorner = frame.ToWorld(new(16, 7));
        var expected = fillet ? 1.2 * (1 - cosine) / double.Sqrt(1 - cosine * cosine) : 1.2;
        var finishLoop = witness.Body.Topology.GetLoop(witness.Body.Topology.GetFace(witness.FinishFace).LoopIds.Single());
        var finishVertices = finishLoop.CoedgeIds.Select(witness.Body.Topology.GetCoedge)
            .Select(use => witness.Body.Topology.GetEdge(use.EdgeId).StartVertexId).Distinct()
            .Select(id => Position(witness.Body, id));
        foreach (var point in finishVertices)
        {
            var delta = point - oldCorner;
            var axial = frame.Normal.ToVector();
            Assert.InRange(delta.Cross(axial).Length, expected - 1e-8, expected + 1e-8);
        }
        if (fillet)
        {
            var cylinder = witness.Body.GetFaceSurface(witness.FinishFace).Cylinder!.Value;
            var arcs = finishLoop.CoedgeIds.Select(witness.Body.Topology.GetCoedge)
                .Select(use => witness.Body.GetEdgeCurve(use.EdgeId).Circle3).OfType<Circle3Curve>();
            foreach (var arc in arcs)
            {
                var sweep = double.Acos(cosine);
                var startNormal = (arc.Evaluate(0) - arc.Center) / cylinder.Radius;
                var endNormal = (arc.Evaluate(sweep) - arc.Center) / cylinder.Radius;
                Assert.InRange((startNormal - supportNormals[0]).Length, 0, 1e-8);
                Assert.InRange((endNormal - supportNormals[1]).Length, 0, 1e-8);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreservesAClearHoleAndRefusesAFinishThatReachesIt(bool fillet)
    {
        var blank = Reopen(BrepPrimitives.CreateBox(40, 30, 8).Value);
        var entry = FaceAt(4);
        var exit = FaceAt(-4);
        var drilled = BrepThroughHoleRevision.TryInsertZ(blank, entry, exit, new(15, 0, 4), 4);
        Assert.True(drilled.IsSuccess, string.Join(" | ", drilled.Diagnostics.Select(item => item.Message)));
        var source = Reopen(drilled.Value.Body);
        var selected = EdgeAt(source, new(20, -15, 4), new(20, 15, 4));
        var existingWall = Assert.Single(source.Topology.Faces, face => source.GetFaceSurface(face.Id).Cylinder is not null);
        var witness = VerifyFinish(source, selected, fillet, 1, .1);
        Assert.Same(existingWall, witness.Body.Topology.GetFace(existingWall.Id));
        Assert.Same(source.GetFaceSurface(existingWall.Id), witness.Body.GetFaceSurface(existingWall.Id));
        var reopened = Reopen(witness.Body);
        Assert.Contains(reopened.Topology.Faces, face => reopened.GetFaceSurface(face.Id).Cylinder is { } cylinder
            && double.Abs(cylinder.Radius - 2) < 1e-9 && double.Abs(cylinder.Origin.X - 15) < 1e-9);
        var oppositeEdge = EdgeAt(reopened, new(-20, -15, 4), new(-20, 15, 4));
        VerifyFinish(reopened, oppositeEdge, !fillet, 1, .1);

        var before = Step242Exporter.ExportBody(source).Value;
        foreach (var conflictingSize in new[] { 2.95, 4.0 })
        {
            var blocked = BrepImportedEdgeFinish.TryApply(source, selected, Kind(fillet), conflictingSize);
            Assert.False(blocked.IsSuccess);
            Assert.Contains(blocked.Diagnostics, item => item.Message.Contains("reaches existing boundary edge"));
        }
        Assert.Equal(before, Step242Exporter.ExportBody(source).Value);

        FaceId FaceAt(double z) => blank.Topology.Faces.Single(face => blank.GetFaceSurface(face.Id).Plane is { } plane
            && double.Abs(plane.Normal.Z) > .99 && double.Abs(plane.Origin.Z - z) < 1e-8).Id;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FinishesRotatedConvexPolygonAndRetainsABoreInSeparateStock(bool fillet)
    {
        var (rotated, _) = RotatedStock(convex: true);
        var cap = rotated.Topology.Faces.Single(face => rotated.GetFaceSurface(face.Id).Plane is { } plane
            && (plane.Origin - new Point3D(30, -12, 4)).Dot(new Vector3D(1, 2, 3)) > 30
            && double.Abs(plane.Normal.ToVector().Dot(Direction3D.Create(new Vector3D(1, 2, 3)).ToVector())) > .9999).Id;
        var rotatedRim = BrepImportedEdgeFinish.TryApplyBoundary(rotated, cap, Kind(fillet), 1.2);
        Assert.True(rotatedRim.IsSuccess, string.Join(" | ", rotatedRim.Diagnostics.Select(item => item.Message)));
        Assert.Equal("polygon-rim", rotatedRim.Value.Strategy);
        Assert.Equal(5, rotatedRim.Value.FinishFaces.Count);
        Assert.Equal(rotated.Topology.Faces.Count() + 5, Reopen(rotatedRim.Value.Body).Topology.Faces.Count());

        var blank = Reopen(BrepPrimitives.CreateBox(40, 30, 8).Value);
        var drilled = BrepThroughHoleRevision.TryInsertZ(blank, Cap(blank, 4), Cap(blank, -4), new(15, 0, 4), 4);
        Assert.True(drilled.IsSuccess, string.Join(" | ", drilled.Diagnostics.Select(item => item.Message)));
        var source = Reopen(drilled.Value.Body);
        var top = Cap(source, 4);
        var retained = Assert.Single(source.Topology.Faces, face => source.GetFaceSurface(face.Id).Cylinder is not null);
        var finish = BrepImportedEdgeFinish.TryApplyBoundary(source, top, Kind(fillet), 1);
        Assert.True(finish.IsSuccess, string.Join(" | ", finish.Diagnostics.Select(item => item.Message)));
        Assert.Same(retained, finish.Value.Body.Topology.GetFace(retained.Id));
        Assert.Same(source.GetFaceSurface(retained.Id), finish.Value.Body.GetFaceSurface(retained.Id));
        Assert.Contains(Reopen(finish.Value.Body).Topology.Faces, face => face.LoopIds.Count == 2);
        var tooClose = BrepImportedEdgeFinish.TryApplyBoundary(source, top, Kind(fillet), 2.95);
        Assert.False(tooClose.IsSuccess);
        Assert.Contains(tooClose.Diagnostics, item => item.Message.Contains("polygon rim finish reaches"));

        static FaceId Cap(BrepBody body, double z) => body.Topology.Faces.Single(face => body.GetFaceSurface(face.Id).Plane is { } plane
            && double.Abs(plane.Normal.Z) > .99 && double.Abs(plane.Origin.Z - z) < 1e-8).Id;
    }

    [Fact]
    public void RefusesTheReentrantEdgeOfTheSameStock()
    {
        var (source, frame) = RotatedStock();
        var selected = EdgeAt(source, frame.ToWorld(new(8, 6)), frame.ToWorld(new(8, 6), 9));
        foreach (var kind in Enum.GetValues<ImportedEdgeFinishKind>())
        {
            var result = BrepImportedEdgeFinish.TryApply(source, selected, kind, 1);
            Assert.False(result.IsSuccess);
            Assert.Contains(result.Diagnostics, item => item.Message.Contains("convex finish"));
        }
    }

    [Fact]
    public void RefusesAnObliqueEndpointUntilItsTransitionIsQualified()
    {
        var (source, frame) = RotatedStock();
        var selected = EdgeAt(source, frame.ToWorld(new(14, 0)), frame.ToWorld(new(16, 7)));
        var result = BrepImportedEdgeFinish.TryApply(source, selected, ImportedEdgeFinishKind.Fillet, 1);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, item => item.Message.Contains("perpendicular to the selected edge"));
    }

    private static ImportedEdgeFinishWitness VerifyFinish(BrepBody source, EdgeId edge, bool fillet, double size, double clearance)
    {
        var original = Step242Exporter.ExportBody(source).Value;
        var result = BrepImportedEdgeFinish.TryApply(source, edge, Kind(fillet), size, clearance: clearance);
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(item => item.Message)));
        var witness = result.Value;
        var body = witness.Body;
        Assert.Equal(4, witness.ModifiedFaces.Distinct().Count());
        Assert.Equal(5, witness.RetiredEdges.Distinct().Count());
        Assert.Contains(edge, witness.RetiredEdges);
        Assert.Equal(source.Topology.Faces.Count() + 1, body.Topology.Faces.Count());
        Assert.Equal(source.Topology.Edges.Count() + 3, body.Topology.Edges.Count());
        Assert.Equal(source.Topology.Vertices.Count() + 2, body.Topology.Vertices.Count());

        foreach (var oldEdge in source.Topology.Edges)
        {
            if (witness.RetiredEdges.Contains(oldEdge.Id))
            {
                Assert.False(body.Topology.TryGetEdge(oldEdge.Id, out _));
                Assert.False(body.Bindings.TryGetEdgeBinding(oldEdge.Id, out _));
                continue;
            }
            Assert.Same(oldEdge, body.Topology.GetEdge(oldEdge.Id));
            Assert.Equal(source.Bindings.GetEdgeBinding(oldEdge.Id), body.Bindings.GetEdgeBinding(oldEdge.Id));
            Assert.Same(source.GetEdgeCurve(oldEdge.Id), body.GetEdgeCurve(oldEdge.Id));
        }
        foreach (var face in source.Topology.Faces)
        {
            Assert.Same(face, body.Topology.GetFace(face.Id));
            Assert.Equal(source.Bindings.GetFaceBinding(face.Id), body.Bindings.GetFaceBinding(face.Id));
            Assert.Same(source.GetFaceSurface(face.Id), body.GetFaceSurface(face.Id));
        }
        foreach (var bound in source.Bindings.PcurveBindings)
            if (body.Topology.TryGetCoedge(bound.CoedgeId, out _))
                Assert.Same(bound.Pcurve, body.Bindings.GetPcurveBinding(bound.CoedgeId).Pcurve);

        Assert.Equal(original, Step242Exporter.ExportBody(source).Value);
        var successor = Reopen(body);
        Assert.Equal(body.Topology.Faces.Count(), successor.Topology.Faces.Count());
        Assert.Equal(fillet ? SurfaceGeometryKind.Cylinder : SurfaceGeometryKind.Plane, body.GetFaceSurface(witness.FinishFace).Kind);
        if (fillet)
        {
            var cylinder = body.GetFaceSurface(witness.FinishFace).Cylinder!.Value;
            Assert.Equal(size, cylinder.Radius, 10);
            Assert.Equal(2, body.Topology.GetLoop(body.Topology.GetFace(witness.FinishFace).LoopIds.Single()).CoedgeIds
                .Select(body.Topology.GetCoedge).Count(use => body.GetEdgeCurve(use.EdgeId).Circle3 is not null));
            Assert.Contains(successor.Topology.Faces, face => successor.GetFaceSurface(face.Id).Cylinder is { } wall
                && double.Abs(wall.Radius - size) < 1e-9);
        }
        return witness;
    }

    private static ImportedEdgeFinishKind Kind(bool fillet) => fillet ? ImportedEdgeFinishKind.Fillet : ImportedEdgeFinishKind.Chamfer;

    private static (BrepBody Body, ExtrudeFrame3D Frame) RotatedStock(bool convex = false)
    {
        var normal = Direction3D.Create(new Vector3D(1, 2, 3));
        var reference = Direction3D.Create(new Vector3D(2, -1, 0));
        var frame = new ExtrudeFrame3D(new(30, -12, 4), normal, reference);
        var profile = PolylineProfile2D.Create(convex
            ? [new(0, 0), new(14, 0), new(16, 7), new(10, 11), new(0, 11)]
            : [new(0, 0), new(14, 0), new(16, 7), new(10, 11), new(8, 6), new(6, 11), new(0, 11)]).Value;
        var prism = BrepExtrude.Create(profile, frame, 9);
        Assert.True(prism.IsSuccess, string.Join(" | ", prism.Diagnostics.Select(item => item.Message)));
        return (Reopen(prism.Value), frame);
    }

    private static EdgeId EdgeAt(BrepBody body, Point3D a, Point3D b)
    {
        return body.Topology.Edges.Single(edge =>
            (Near(edge.StartVertexId, a) && Near(edge.EndVertexId, b)) || (Near(edge.StartVertexId, b) && Near(edge.EndVertexId, a))).Id;

        bool Near(VertexId id, Point3D point) => (Position(body, id) - point).Length < 1e-8;
    }

    private static Point3D Position(BrepBody body, VertexId id)
    {
        Assert.True(body.TryGetVertexPoint(id, out var point));
        return point;
    }

    private static BrepBody Reopen(BrepBody body)
    {
        var exported = Step242Exporter.ExportBody(body,
            new Step242ExportOptions { BrepExportPreflightMode = BrepExportPreflightMode.Enforce });
        Assert.True(exported.IsSuccess, string.Join(" | ", exported.Diagnostics.Select(item => item.Message)));
        var imported = Step242Importer.ImportBody(exported.Value);
        Assert.True(imported.IsSuccess, string.Join(" | ", imported.Diagnostics.Select(item => item.Message)));
        return imported.Value;
    }
}
