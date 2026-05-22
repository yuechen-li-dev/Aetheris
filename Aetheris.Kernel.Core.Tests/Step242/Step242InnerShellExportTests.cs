using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Picking;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242InnerShellExportTests
{
    [Fact]
    public void ExportBody_SingleShellRegression_StillUsesManifoldSolidBrep()
    {
        var box = BrepPrimitives.CreateBox(6d, 5d, 4d);
        Assert.True(box.IsSuccess);

        var export = Step242Exporter.ExportBody(box.Value);

        Assert.True(export.IsSuccess);
        Assert.Contains("MANIFOLD_SOLID_BREP", export.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("BREP_WITH_VOIDS", export.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportBody_InnerShellRepresentation_ValidatesAndExportsDeterministically()
    {
        var body = CreateSyntheticBodyWithInnerShell();

        var validation = BrepBindingValidator.Validate(body);
        Assert.True(validation.IsSuccess);

        var first = Step242Exporter.ExportBody(body);
        var second = Step242Exporter.ExportBody(body);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value, second.Value);
        Assert.Contains("BREP_WITH_VOIDS", first.Value, StringComparison.Ordinal);
        Assert.Contains("ORIENTED_CLOSED_SHELL", first.Value, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(first.Value, "=CLOSED_SHELL("));
    }

    [Fact]
    public void ExportBody_InnerShellRepresentation_ImportRoundTrip_PreservesShellRepresentation()
    {
        var body = CreateSyntheticBodyWithInnerShell();
        var export = Step242Exporter.ExportBody(body);
        Assert.True(export.IsSuccess);
        Assert.Contains("BREP_WITH_VOIDS", export.Value, StringComparison.Ordinal);

        var import = Step242Importer.ImportBody(export.Value);

        Assert.True(import.IsSuccess, string.Join(Environment.NewLine, import.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
        Assert.NotNull(import.Value.ShellRepresentation);
        Assert.Single(import.Value.ShellRepresentation!.InnerShellIds);
    }

    [Fact]
    public void Subtract_BoxSphereCavity_InnerShellBody_ValidatesExportsAndExternalPickerStaysOnOuterShell()
    {
        var outer = BrepPrimitives.CreateBox(40d, 30d, 12d);
        var inner = BrepPrimitives.CreateSphere(4d);
        Assert.True(outer.IsSuccess);
        Assert.True(inner.IsSuccess);

        var subtract = BrepBoolean.Subtract(outer.Value, inner.Value);
        Assert.True(subtract.IsSuccess);
        Assert.NotNull(subtract.Value.ShellRepresentation);
        Assert.Single(subtract.Value.ShellRepresentation!.InnerShellIds);

        var validation = BrepBindingValidator.Validate(subtract.Value, requireAllEdgeAndFaceBindings: true);
        Assert.True(validation.IsSuccess);

        var export = Step242Exporter.ExportBody(subtract.Value);
        Assert.True(export.IsSuccess);
        Assert.Contains("BREP_WITH_VOIDS", export.Value, StringComparison.Ordinal);

        var tessellation = BrepDisplayTessellator.Tessellate(subtract.Value);
        Assert.True(tessellation.IsSuccess);
        Assert.NotEmpty(tessellation.Value.FacePatches);

        var outerFaceIds = subtract.Value.Topology.GetShell(subtract.Value.ShellRepresentation.OuterShellId).FaceIds.ToHashSet();
        var ray = new Ray3D(
            new Point3D(0d, 0d, 100d),
            Direction3D.Create(new Vector3D(0d, 0d, -1d)));
        var pick = BrepPicker.Pick(subtract.Value, tessellation.Value, ray, PickQueryOptions.Default with { NearestOnly = true });

        Assert.True(pick.IsSuccess);
        var hit = Assert.Single(pick.Value);
        Assert.NotNull(hit.FaceId);
        Assert.Contains(hit.FaceId.Value, outerFaceIds);
    }

    [Fact]
    public void Validate_InnerShellRepresentationRejectsDuplicatedShellRole()
    {
        var box = BrepPrimitives.CreateBox(3d, 3d, 3d);
        Assert.True(box.IsSuccess);

        var shellId = Assert.Single(Assert.Single(box.Value.Topology.Bodies).ShellIds);
        var invalid = new BrepBody(
            box.Value.Topology,
            box.Value.Geometry,
            box.Value.Bindings,
            vertexPoints: null,
            safeBooleanComposition: null,
            shellRepresentation: new BrepBodyShellRepresentation(shellId, [shellId]));

        var validation = BrepBindingValidator.Validate(invalid);

        Assert.False(validation.IsSuccess);
        Assert.Contains(validation.Diagnostics, d => d.Message.Contains("more than once", StringComparison.Ordinal));
    }

    private static BrepBody CreateSyntheticBodyWithInnerShell()
    {
        var outer = BrepPrimitives.CreateBox(10d, 10d, 10d);
        var inner = BrepPrimitives.CreateBox(4d, 4d, 4d);
        Assert.True(outer.IsSuccess);
        Assert.True(inner.IsSuccess);

        const int idOffset = 1000;
        var topology = new TopologyModel();

        foreach (var vertex in outer.Value.Topology.Vertices.OrderBy(v => v.Id.Value))
        {
            topology.AddVertex(vertex);
        }

        foreach (var edge in outer.Value.Topology.Edges.OrderBy(e => e.Id.Value))
        {
            topology.AddEdge(edge);
        }

        foreach (var coedge in outer.Value.Topology.Coedges.OrderBy(c => c.Id.Value))
        {
            topology.AddCoedge(coedge);
        }

        foreach (var loop in outer.Value.Topology.Loops.OrderBy(l => l.Id.Value))
        {
            topology.AddLoop(loop);
        }

        foreach (var face in outer.Value.Topology.Faces.OrderBy(f => f.Id.Value))
        {
            topology.AddFace(face);
        }

        foreach (var shell in outer.Value.Topology.Shells.OrderBy(s => s.Id.Value))
        {
            topology.AddShell(shell);
        }

        var innerVertexMap = inner.Value.Topology.Vertices.ToDictionary(v => v.Id, v => new VertexId(v.Id.Value + idOffset));
        var innerEdgeMap = inner.Value.Topology.Edges.ToDictionary(e => e.Id, e => new EdgeId(e.Id.Value + idOffset));
        var innerLoopMap = inner.Value.Topology.Loops.ToDictionary(l => l.Id, l => new LoopId(l.Id.Value + idOffset));
        var innerCoedgeMap = inner.Value.Topology.Coedges.ToDictionary(c => c.Id, c => new CoedgeId(c.Id.Value + idOffset));
        var innerFaceMap = inner.Value.Topology.Faces.ToDictionary(f => f.Id, f => new FaceId(f.Id.Value + idOffset));
        var innerShellMap = inner.Value.Topology.Shells.ToDictionary(s => s.Id, s => new ShellId(s.Id.Value + idOffset));

        foreach (var vertex in inner.Value.Topology.Vertices.OrderBy(v => v.Id.Value))
        {
            topology.AddVertex(new Vertex(innerVertexMap[vertex.Id]));
        }

        foreach (var edge in inner.Value.Topology.Edges.OrderBy(e => e.Id.Value))
        {
            topology.AddEdge(new Edge(innerEdgeMap[edge.Id], innerVertexMap[edge.StartVertexId], innerVertexMap[edge.EndVertexId]));
        }

        foreach (var coedge in inner.Value.Topology.Coedges.OrderBy(c => c.Id.Value))
        {
            topology.AddCoedge(new Coedge(
                innerCoedgeMap[coedge.Id],
                innerEdgeMap[coedge.EdgeId],
                innerLoopMap[coedge.LoopId],
                innerCoedgeMap[coedge.NextCoedgeId],
                innerCoedgeMap[coedge.PrevCoedgeId],
                coedge.IsReversed));
        }

        foreach (var loop in inner.Value.Topology.Loops.OrderBy(l => l.Id.Value))
        {
            topology.AddLoop(new Loop(innerLoopMap[loop.Id], loop.CoedgeIds.Select(id => innerCoedgeMap[id]).ToArray()));
        }

        foreach (var face in inner.Value.Topology.Faces.OrderBy(f => f.Id.Value))
        {
            topology.AddFace(new Face(innerFaceMap[face.Id], face.LoopIds.Select(id => innerLoopMap[id]).ToArray()));
        }

        foreach (var shell in inner.Value.Topology.Shells.OrderBy(s => s.Id.Value))
        {
            topology.AddShell(new Shell(innerShellMap[shell.Id], shell.FaceIds.Select(id => innerFaceMap[id]).ToArray()));
        }

        var outerShellId = Assert.Single(Assert.Single(outer.Value.Topology.Bodies).ShellIds);
        var innerShellId = innerShellMap[Assert.Single(Assert.Single(inner.Value.Topology.Bodies).ShellIds)];
        topology.AddBody(new Body(new BodyId(1), [outerShellId, innerShellId]));

        var geometry = new BrepGeometryStore();
        foreach (var curve in outer.Value.Geometry.Curves)
        {
            geometry.AddCurve(curve.Key, curve.Value);
        }

        foreach (var surface in outer.Value.Geometry.Surfaces)
        {
            geometry.AddSurface(surface.Key, surface.Value);
        }

        foreach (var curve in inner.Value.Geometry.Curves)
        {
            geometry.AddCurve(new CurveGeometryId(curve.Key.Value + idOffset), curve.Value);
        }

        foreach (var surface in inner.Value.Geometry.Surfaces)
        {
            geometry.AddSurface(new SurfaceGeometryId(surface.Key.Value + idOffset), surface.Value);
        }

        var bindings = new BrepBindingModel();
        foreach (var edgeBinding in outer.Value.Bindings.EdgeBindings.OrderBy(binding => binding.EdgeId.Value))
        {
            bindings.AddEdgeBinding(edgeBinding);
        }

        foreach (var faceBinding in outer.Value.Bindings.FaceBindings.OrderBy(binding => binding.FaceId.Value))
        {
            bindings.AddFaceBinding(faceBinding);
        }

        foreach (var edgeBinding in inner.Value.Bindings.EdgeBindings.OrderBy(binding => binding.EdgeId.Value))
        {
            bindings.AddEdgeBinding(edgeBinding with
            {
                EdgeId = innerEdgeMap[edgeBinding.EdgeId],
                CurveGeometryId = new CurveGeometryId(edgeBinding.CurveGeometryId.Value + idOffset)
            });
        }

        foreach (var faceBinding in inner.Value.Bindings.FaceBindings.OrderBy(binding => binding.FaceId.Value))
        {
            bindings.AddFaceBinding(faceBinding with
            {
                FaceId = innerFaceMap[faceBinding.FaceId],
                SurfaceGeometryId = new SurfaceGeometryId(faceBinding.SurfaceGeometryId.Value + idOffset)
            });
        }

        return new BrepBody(
            topology,
            geometry,
            bindings,
            vertexPoints: null,
            safeBooleanComposition: null,
            shellRepresentation: new BrepBodyShellRepresentation(outerShellId, [innerShellId]));
    }

    private static int CountOccurrences(string text, string token)
    {
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }
}
