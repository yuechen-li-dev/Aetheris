using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep;

public sealed class BoxBrepFixtureTests
{
    [Fact]
    public void ManualBoxBrep_CanBeConstructedAndValidated()
    {
        var body = BuildUnitBoxBrep();

        var result = BrepBindingValidator.Validate(body);

        Assert.True(result.IsSuccess);
        Assert.True(body.TryGetEdgeCurveGeometry(new EdgeId(1), out var edgeCurve));
        Assert.Equal(CurveGeometryKind.Line3, edgeCurve!.Kind);

        Assert.True(body.TryGetFaceSurfaceGeometry(new FaceId(1), out var faceSurface));
        Assert.Equal(SurfaceGeometryKind.Plane, faceSurface!.Kind);
    }

    private static BrepBody BuildUnitBoxBrep()
    {
        var builder = new TopologyBuilder();

        var v1 = builder.AddVertex();
        var v2 = builder.AddVertex();
        var v3 = builder.AddVertex();
        var v4 = builder.AddVertex();
        var v5 = builder.AddVertex();
        var v6 = builder.AddVertex();
        var v7 = builder.AddVertex();
        var v8 = builder.AddVertex();

        var e1 = builder.AddEdge(v1, v2);
        var e2 = builder.AddEdge(v2, v3);
        var e3 = builder.AddEdge(v3, v4);
        var e4 = builder.AddEdge(v4, v1);
        var e5 = builder.AddEdge(v5, v6);
        var e6 = builder.AddEdge(v6, v7);
        var e7 = builder.AddEdge(v7, v8);
        var e8 = builder.AddEdge(v8, v5);
        var e9 = builder.AddEdge(v1, v5);
        var e10 = builder.AddEdge(v2, v6);
        var e11 = builder.AddEdge(v3, v7);
        var e12 = builder.AddEdge(v4, v8);

        var f1 = AddFaceWithLoop(builder, [e1, e2, e3, e4]);
        var f2 = AddFaceWithLoop(builder, [e5, e6, e7, e8]);
        var f3 = AddFaceWithLoop(builder, [e1, e10, e5, e9]);
        var f4 = AddFaceWithLoop(builder, [e2, e11, e6, e10]);
        var f5 = AddFaceWithLoop(builder, [e3, e12, e7, e11]);
        var f6 = AddFaceWithLoop(builder, [e4, e9, e8, e12]);

        var shell = builder.AddShell([f1, f2, f3, f4, f5, f6]);
        builder.AddBody([shell]);

        var geometry = new BrepGeometryStore();
        AddLineCurve(geometry, 1, Point3D.Origin, new Vector3D(1d, 0d, 0d));
        AddLineCurve(geometry, 2, new Point3D(1d, 0d, 0d), new Vector3D(0d, 1d, 0d));
        AddLineCurve(geometry, 3, new Point3D(1d, 1d, 0d), new Vector3D(-1d, 0d, 0d));
        AddLineCurve(geometry, 4, new Point3D(0d, 1d, 0d), new Vector3D(0d, -1d, 0d));
        AddLineCurve(geometry, 5, new Point3D(0d, 0d, 1d), new Vector3D(1d, 0d, 0d));
        AddLineCurve(geometry, 6, new Point3D(1d, 0d, 1d), new Vector3D(0d, 1d, 0d));
        AddLineCurve(geometry, 7, new Point3D(1d, 1d, 1d), new Vector3D(-1d, 0d, 0d));
        AddLineCurve(geometry, 8, new Point3D(0d, 1d, 1d), new Vector3D(0d, -1d, 0d));
        AddLineCurve(geometry, 9, Point3D.Origin, new Vector3D(0d, 0d, 1d));
        AddLineCurve(geometry, 10, new Point3D(1d, 0d, 0d), new Vector3D(0d, 0d, 1d));
        AddLineCurve(geometry, 11, new Point3D(1d, 1d, 0d), new Vector3D(0d, 0d, 1d));
        AddLineCurve(geometry, 12, new Point3D(0d, 1d, 0d), new Vector3D(0d, 0d, 1d));

        geometry.AddSurface(new SurfaceGeometryId(1), PlaneAt(new Point3D(0d, 0d, 0d), new Vector3D(0d, 0d, -1d), new Vector3D(1d, 0d, 0d)));
        geometry.AddSurface(new SurfaceGeometryId(2), PlaneAt(new Point3D(0d, 0d, 1d), new Vector3D(0d, 0d, 1d), new Vector3D(1d, 0d, 0d)));
        geometry.AddSurface(new SurfaceGeometryId(3), PlaneAt(new Point3D(0d, 0d, 0d), new Vector3D(0d, -1d, 0d), new Vector3D(1d, 0d, 0d)));
        geometry.AddSurface(new SurfaceGeometryId(4), PlaneAt(new Point3D(1d, 0d, 0d), new Vector3D(1d, 0d, 0d), new Vector3D(0d, 1d, 0d)));
        geometry.AddSurface(new SurfaceGeometryId(5), PlaneAt(new Point3D(0d, 1d, 0d), new Vector3D(0d, 1d, 0d), new Vector3D(-1d, 0d, 0d)));
        geometry.AddSurface(new SurfaceGeometryId(6), PlaneAt(new Point3D(0d, 0d, 0d), new Vector3D(-1d, 0d, 0d), new Vector3D(0d, 1d, 0d)));

        var bindings = new BrepBindingModel();
        for (var edgeIndex = 1; edgeIndex <= 12; edgeIndex++)
        {
            bindings.AddEdgeBinding(new EdgeGeometryBinding(new EdgeId(edgeIndex), new CurveGeometryId(edgeIndex), new ParameterInterval(0d, 1d)));
        }

        for (var faceIndex = 1; faceIndex <= 6; faceIndex++)
        {
            bindings.AddFaceBinding(new FaceGeometryBinding(new FaceId(faceIndex), new SurfaceGeometryId(faceIndex)));
        }

        return new BrepBody(builder.Model, geometry, bindings);
    }

    private static FaceId AddFaceWithLoop(TopologyBuilder builder, IReadOnlyList<EdgeId> edges)
    {
        var loopId = builder.AllocateLoopId();
        var coedgeIds = new CoedgeId[edges.Count];

        for (var i = 0; i < edges.Count; i++)
        {
            coedgeIds[i] = builder.AllocateCoedgeId();
        }

        for (var i = 0; i < edges.Count; i++)
        {
            var next = coedgeIds[(i + 1) % edges.Count];
            var prev = coedgeIds[(i + edges.Count - 1) % edges.Count];
            builder.AddCoedge(new Coedge(coedgeIds[i], edges[i], loopId, next, prev, false));
        }

        builder.AddLoop(new Loop(loopId, coedgeIds));
        return builder.AddFace([loopId]);
    }

    private static void AddLineCurve(BrepGeometryStore geometry, int id, Point3D origin, Vector3D direction)
    {
        geometry.AddCurve(new CurveGeometryId(id), CurveGeometry.FromLine(new Line3Curve(origin, Direction3D.Create(direction))));
    }

    private static SurfaceGeometry PlaneAt(Point3D origin, Vector3D normal, Vector3D uAxis)
    {
        return SurfaceGeometry.FromPlane(new PlaneSurface(origin, Direction3D.Create(normal), Direction3D.Create(uAxis)));
    }
}
