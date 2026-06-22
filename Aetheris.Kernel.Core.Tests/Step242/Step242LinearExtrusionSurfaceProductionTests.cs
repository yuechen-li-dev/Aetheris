using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242LinearExtrusionSurfaceProductionTests
{
    [Fact]
    public void EllipseDirectrixLinearExtrusion_ExportsReimportsAsExactSweptSurface()
    {
        var body = CreateEllipticStripSurfaceBody();

        var export = Step242Exporter.ExportBody(body, new Step242ExportOptions { ProductName = "RULED-A2 elliptic linear-extrusion strip" });
        Assert.True(export.IsSuccess, string.Join(Environment.NewLine, export.Diagnostics.Select(d => d.Message)));
        Assert.Contains("SURFACE_OF_LINEAR_EXTRUSION", export.Value, StringComparison.Ordinal);
        Assert.Contains("ELLIPSE", export.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("B_SPLINE_SURFACE_WITH_KNOTS", export.Value, StringComparison.Ordinal);

        var artifactPath = WriteArtifact(export.Value);
        Assert.True(File.Exists(artifactPath));

        var reimport = Step242Importer.ImportBody(export.Value);
        Assert.True(reimport.IsSuccess, string.Join(Environment.NewLine, reimport.Diagnostics.Select(d => d.Message)));

        Assert.Single(reimport.Value.Topology.Bodies);
        Assert.Single(reimport.Value.Topology.Shells);
        Assert.Single(reimport.Value.Topology.Faces);
        Assert.Single(reimport.Value.Topology.Loops);
        Assert.Equal(4, reimport.Value.Topology.Edges.Count());
        Assert.Equal(4, reimport.Value.Topology.Vertices.Count());

        var surface = Assert.Single(reimport.Value.Geometry.Surfaces).Value;
        Assert.Equal(SurfaceGeometryKind.LinearExtrusion, surface.Kind);
        Assert.True(surface.LinearExtrusion.HasValue);
        Assert.Equal(CurveGeometryKind.Ellipse3, surface.LinearExtrusion.Value.Directrix.Kind);
    }

    private static BrepBody CreateEllipticStripSurfaceBody()
    {
        var builder = new TopologyBuilder();
        var v0 = builder.AddVertex();
        var v1 = builder.AddVertex();
        var v2 = builder.AddVertex();
        var v3 = builder.AddVertex();

        var e0 = builder.AddEdge(v0, v1);
        var e1 = builder.AddEdge(v1, v2);
        var e2 = builder.AddEdge(v3, v2);
        var e3 = builder.AddEdge(v0, v3);

        var loopId = builder.AllocateLoopId();
        var c0 = builder.AllocateCoedgeId();
        var c1 = builder.AllocateCoedgeId();
        var c2 = builder.AllocateCoedgeId();
        var c3 = builder.AllocateCoedgeId();
        builder.AddCoedge(new Coedge(c0, e0, loopId, c1, c3, false));
        builder.AddCoedge(new Coedge(c1, e1, loopId, c2, c0, false));
        builder.AddCoedge(new Coedge(c2, e2, loopId, c3, c1, true));
        builder.AddCoedge(new Coedge(c3, e3, loopId, c0, c2, true));
        builder.AddLoop(new Loop(loopId, [c0, c1, c2, c3]));

        var faceId = builder.AddFace([loopId]);
        var shellId = builder.AddShell([faceId]);
        builder.AddBody([shellId]);

        var normal = Direction3D.Create(new Vector3D(0, 0, 1));
        var xAxis = Direction3D.Create(new Vector3D(1, 0, 0));
        var zLine = Direction3D.Create(new Vector3D(0, 0, 1));
        var bottomEllipse = new Ellipse3Curve(Point3D.Origin, normal, majorRadius: 4d, minorRadius: 2d, xAxis);
        var topEllipse = new Ellipse3Curve(new Point3D(0, 0, 5), normal, majorRadius: 4d, minorRadius: 2d, xAxis);
        var p0 = bottomEllipse.Evaluate(0d);
        var p1 = bottomEllipse.Evaluate(global::System.Math.PI);
        var p2 = topEllipse.Evaluate(global::System.Math.PI);
        var p3 = topEllipse.Evaluate(0d);

        var geometry = new BrepGeometryStore();
        var surfaceGeometryId = new SurfaceGeometryId(1);
        geometry.AddSurface(surfaceGeometryId, SurfaceGeometry.FromLinearExtrusion(new LinearExtrusionSurface(
            CurveGeometry.FromEllipse(bottomEllipse),
            new Vector3D(0, 0, 5))));
        geometry.AddCurve(new CurveGeometryId(1), CurveGeometry.FromEllipse(bottomEllipse));
        geometry.AddCurve(new CurveGeometryId(2), CurveGeometry.FromLine(new Line3Curve(p1, zLine)));
        geometry.AddCurve(new CurveGeometryId(3), CurveGeometry.FromEllipse(topEllipse));
        geometry.AddCurve(new CurveGeometryId(4), CurveGeometry.FromLine(new Line3Curve(p0, zLine)));

        var bindings = new BrepBindingModel();
        bindings.AddFaceBinding(new FaceGeometryBinding(faceId, surfaceGeometryId));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(e0, new CurveGeometryId(1), new ParameterInterval(0d, global::System.Math.PI)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(e1, new CurveGeometryId(2), new ParameterInterval(0d, 5d)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(e2, new CurveGeometryId(3), new ParameterInterval(0d, global::System.Math.PI)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(e3, new CurveGeometryId(4), new ParameterInterval(0d, 5d)));

        var vertexPoints = new Dictionary<VertexId, Point3D>
        {
            [v0] = p0,
            [v1] = p1,
            [v2] = p2,
            [v3] = p3
        };

        return new BrepBody(builder.Model, geometry, bindings, vertexPoints);
    }

    private static string WriteArtifact(string stepText)
    {
        var path = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), "testdata", "step242", "generated", "ruled-a2", "ellipse-linear-extrusion-production.step");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, stepText);
        return path;
    }
}
