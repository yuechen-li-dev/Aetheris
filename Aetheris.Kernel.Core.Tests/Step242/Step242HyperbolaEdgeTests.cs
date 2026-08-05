using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242HyperbolaEdgeTests
{
    [Fact]
    public void BoundedHyperbolaEdge_ExportsAndReimportsAsExactAnalyticSupport()
    {
        var source = BuildPlanarHyperbolaWire(HyperbolaBranch.PositiveAxisU);
        var exported = Step242Exporter.ExportBody(source, new Step242ExportOptions { BrepExportPreflightMode = BrepExportPreflightMode.Enforce });

        Assert.True(exported.IsSuccess, string.Join(Environment.NewLine, exported.Diagnostics.Select(d => d.Message)));
        Assert.Contains("HYPERBOLA", exported.Value, StringComparison.Ordinal);
        Assert.Contains("TRIMMED_CURVE", exported.Value, StringComparison.Ordinal);

        var imported = Step242Importer.ImportBody(exported.Value);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(d => d.Message)));
        var hyperbola = Assert.Single(imported.Value.Geometry.Curves, pair => pair.Value.Kind == CurveGeometryKind.Hyperbola3).Value.Hyperbola3!.Value;
        Assert.Equal(3d, hyperbola.SemiAxisA, 12);
        Assert.Equal(2d, hyperbola.SemiAxisB, 12);
        Assert.Equal(new Point3D(4d, -5d, 0d), hyperbola.Center);
    }

    [Fact]
    public void NegativeBranch_ExportsTheSamePhysicalBranchWithoutSplineFallback()
    {
        var source = BuildPlanarHyperbolaWire(HyperbolaBranch.NegativeAxisU);
        var exported = Step242Exporter.ExportBody(source, new Step242ExportOptions { BrepExportPreflightMode = BrepExportPreflightMode.Enforce });
        Assert.True(exported.IsSuccess, string.Join(Environment.NewLine, exported.Diagnostics.Select(d => d.Message)));
        var imported = Step242Importer.ImportBody(exported.Value);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(d => d.Message)));

        var sourceEndpoint = source.Geometry.Curves.Single(pair => pair.Value.Kind == CurveGeometryKind.Hyperbola3).Value.Hyperbola3!.Value.Evaluate(0.7d);
        var importedEndpoint = imported.Value.Geometry.Curves.Single(pair => pair.Value.Kind == CurveGeometryKind.Hyperbola3).Value.Hyperbola3!.Value.Evaluate(0.7d);
        Assert.True((sourceEndpoint - importedEndpoint).Length <= 1e-8d);
    }

    [Fact]
    public void BoundedHyperbolaEdge_TessellatesAsDerivedDisplayGeometry()
    {
        var tessellation = BrepDisplayTessellator.Tessellate(BuildPlanarHyperbolaWire(HyperbolaBranch.PositiveAxisU));
        Assert.True(tessellation.IsSuccess, string.Join(Environment.NewLine, tessellation.Diagnostics.Select(d => d.Message)));
        Assert.NotEmpty(tessellation.Value.FacePatches);
    }

    private static BrepBody BuildPlanarHyperbolaWire(HyperbolaBranch branch)
    {
        var z = Direction3D.Create(new Vector3D(0d, 0d, 1d));
        var x = Direction3D.Create(new Vector3D(1d, 0d, 0d));
        var hyperbola = new Hyperbola3Curve(new Point3D(4d, -5d, 0d), z, x, 3d, 2d, branch);
        var first = hyperbola.Evaluate(-0.4d);
        var second = hyperbola.Evaluate(0.7d);
        var third = new Point3D(4d, -8d, 0d);

        var topology = new TopologyModel();
        var vertices = new[] { new VertexId(1), new VertexId(2), new VertexId(3) };
        foreach (var vertex in vertices) topology.AddVertex(new Vertex(vertex));
        topology.AddEdge(new Edge(new EdgeId(1), vertices[0], vertices[1]));
        topology.AddEdge(new Edge(new EdgeId(2), vertices[1], vertices[2]));
        topology.AddEdge(new Edge(new EdgeId(3), vertices[2], vertices[0]));
        for (var i = 0; i < 3; i++) topology.AddCoedge(new Coedge(new CoedgeId(i + 1), new EdgeId(i + 1), new LoopId(1), new CoedgeId((i + 1) % 3 + 1), new CoedgeId((i + 2) % 3 + 1), false));
        topology.AddLoop(new Loop(new LoopId(1), [new CoedgeId(1), new CoedgeId(2), new CoedgeId(3)]));
        topology.AddFace(new Face(new FaceId(1), [new LoopId(1)]));
        topology.AddShell(new Shell(new ShellId(1), [new FaceId(1)]));
        topology.AddBody(new Body(new BodyId(1), [new ShellId(1)]));

        var geometry = new BrepGeometryStore();
        geometry.AddCurve(new CurveGeometryId(1), CurveGeometry.FromHyperbola(hyperbola));
        geometry.AddCurve(new CurveGeometryId(2), CurveGeometry.FromLine(new Line3Curve(second, Direction3D.Create(third - second))));
        geometry.AddCurve(new CurveGeometryId(3), CurveGeometry.FromLine(new Line3Curve(third, Direction3D.Create(first - third))));
        geometry.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromPlane(new PlaneSurface(Point3D.Origin, z, x)));
        var bindings = new BrepBindingModel();
        bindings.AddEdgeBinding(new EdgeGeometryBinding(new EdgeId(1), new CurveGeometryId(1), new ParameterInterval(-0.4d, 0.7d)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(new EdgeId(2), new CurveGeometryId(2), new ParameterInterval(0d, (third - second).Length)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(new EdgeId(3), new CurveGeometryId(3), new ParameterInterval(0d, (first - third).Length)));
        bindings.AddFaceBinding(new FaceGeometryBinding(new FaceId(1), new SurfaceGeometryId(1)));
        return new BrepBody(topology, geometry, bindings, new Dictionary<VertexId, Point3D> { [vertices[0]] = first, [vertices[1]] = second, [vertices[2]] = third });
    }
}
