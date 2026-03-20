using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Boolean;

public static class BrepBooleanBoxCylinderHoleBuilder
{
    public static KernelResult<BrepBody> CreateThroughHoleBody(AxisAlignedBoxExtents box, in RecognizedCylinder cylinder)
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
        var topHoleVertex = builder.AddVertex();
        var bottomHoleVertex = builder.AddVertex();
        var seamTopVertex = builder.AddVertex();
        var seamBottomVertex = builder.AddVertex();

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
        var topCircle = builder.AddEdge(topHoleVertex, topHoleVertex);
        var bottomCircle = builder.AddEdge(bottomHoleVertex, bottomHoleVertex);
        var seam = builder.AddEdge(seamTopVertex, seamBottomVertex);

        var bottomOuterLoop = AddLoop(builder, [Forward(e1), Forward(e2), Forward(e3), Forward(e4)]);
        var topOuterLoop = AddLoop(builder, [Forward(e5), Forward(e6), Forward(e7), Forward(e8)]);
        var topInnerLoop = AddLoop(builder, [Forward(topCircle)]);
        var bottomInnerLoop = AddLoop(builder, [Reversed(bottomCircle)]);
        var yMinFace = builder.AddFace([bottomOuterLoop, bottomInnerLoop]);
        var yMaxFace = builder.AddFace([topOuterLoop, topInnerLoop]);

        var xMinFace = builder.AddFace([AddLoop(builder, [Forward(e1), Forward(e10), Reversed(e5), Reversed(e9)])]);
        var xMaxFace = builder.AddFace([AddLoop(builder, [Forward(e2), Forward(e11), Reversed(e6), Reversed(e10)])]);
        var yFarFace = builder.AddFace([AddLoop(builder, [Forward(e3), Forward(e12), Reversed(e7), Reversed(e11)])]);
        var yNearFace = builder.AddFace([AddLoop(builder, [Forward(e4), Forward(e9), Reversed(e8), Reversed(e12)])]);
        var holeFace = builder.AddFace([AddLoop(builder, [Forward(seam), Reversed(topCircle), Reversed(seam), Forward(bottomCircle)])]);

        var shell = builder.AddShell([yMinFace, yMaxFace, xMinFace, xMaxFace, yFarFace, yNearFace, holeFace]);
        builder.AddBody([shell]);

        var geometry = new BrepGeometryStore();
        var width = box.MaxX - box.MinX;
        var depth = box.MaxY - box.MinY;
        var height = box.MaxZ - box.MinZ;

        var p1 = new Point3D(box.MinX, box.MinY, box.MinZ);
        var p2 = new Point3D(box.MaxX, box.MinY, box.MinZ);
        var p3 = new Point3D(box.MaxX, box.MaxY, box.MinZ);
        var p4 = new Point3D(box.MinX, box.MaxY, box.MinZ);
        var p5 = new Point3D(box.MinX, box.MinY, box.MaxZ);
        var p6 = new Point3D(box.MaxX, box.MinY, box.MaxZ);
        var p7 = new Point3D(box.MaxX, box.MaxY, box.MaxZ);
        var p8 = new Point3D(box.MinX, box.MaxY, box.MaxZ);

        var axis = Direction3D.Create(new Vector3D(0d, 0d, 1d));
        var xAxis = Direction3D.Create(new Vector3D(1d, 0d, 0d));
        var yAxis = Direction3D.Create(new Vector3D(0d, 1d, 0d));
        var centerX = cylinder.MinCenter.X;
        var centerY = cylinder.MinCenter.Y;
        var bottomCenter = new Point3D(centerX, centerY, box.MinZ);
        var topCenter = new Point3D(centerX, centerY, box.MaxZ);
        var seamBottomPoint = new Point3D(centerX + cylinder.Radius, centerY, box.MinZ);
        var seamTopPoint = new Point3D(centerX + cylinder.Radius, centerY, box.MaxZ);

        var lineCurves = new[]
        {
            (p1, new Vector3D(width, 0d, 0d)),
            (p2, new Vector3D(0d, depth, 0d)),
            (p3, new Vector3D(-width, 0d, 0d)),
            (p4, new Vector3D(0d, -depth, 0d)),
            (p5, new Vector3D(width, 0d, 0d)),
            (p6, new Vector3D(0d, depth, 0d)),
            (p7, new Vector3D(-width, 0d, 0d)),
            (p8, new Vector3D(0d, -depth, 0d)),
            (p1, new Vector3D(0d, 0d, height)),
            (p2, new Vector3D(0d, 0d, height)),
            (p3, new Vector3D(0d, 0d, height)),
            (p4, new Vector3D(0d, 0d, height)),
            (topCenter, axis.ToVector()),
            (bottomCenter, axis.ToVector()),
            (seamTopPoint, new Vector3D(0d, 0d, box.MinZ - box.MaxZ)),
        };

        for (var i = 0; i < 12; i++)
        {
            geometry.AddCurve(new CurveGeometryId(i + 1), CurveGeometry.FromLine(new Line3Curve(lineCurves[i].Item1, Direction3D.Create(lineCurves[i].Item2))));
        }

        geometry.AddCurve(new CurveGeometryId(13), CurveGeometry.FromCircle(new Circle3Curve(topCenter, axis, cylinder.Radius, xAxis)));
        geometry.AddCurve(new CurveGeometryId(14), CurveGeometry.FromCircle(new Circle3Curve(bottomCenter, axis, cylinder.Radius, xAxis)));
        geometry.AddCurve(new CurveGeometryId(15), CurveGeometry.FromLine(new Line3Curve(seamTopPoint, Direction3D.Create(lineCurves[14].Item2))));

        geometry.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0d, 0d, box.MinZ), Direction3D.Create(new Vector3D(0d, 0d, -1d)), xAxis)));
        geometry.AddSurface(new SurfaceGeometryId(2), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0d, 0d, box.MaxZ), axis, xAxis)));
        geometry.AddSurface(new SurfaceGeometryId(3), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0d, box.MinY, 0d), Direction3D.Create(new Vector3D(0d, -1d, 0d)), xAxis)));
        geometry.AddSurface(new SurfaceGeometryId(4), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(box.MaxX, 0d, 0d), Direction3D.Create(new Vector3D(1d, 0d, 0d)), yAxis)));
        geometry.AddSurface(new SurfaceGeometryId(5), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0d, box.MaxY, 0d), Direction3D.Create(new Vector3D(0d, 1d, 0d)), Direction3D.Create(new Vector3D(-1d, 0d, 0d)))));
        geometry.AddSurface(new SurfaceGeometryId(6), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(box.MinX, 0d, 0d), Direction3D.Create(new Vector3D(-1d, 0d, 0d)), yAxis)));
        geometry.AddSurface(new SurfaceGeometryId(7), SurfaceGeometry.FromCylinder(new CylinderSurface(bottomCenter, axis, cylinder.Radius, xAxis)));

        var bindings = new BrepBindingModel();
        for (var i = 0; i < 12; i++)
        {
            bindings.AddEdgeBinding(new EdgeGeometryBinding(new EdgeId(i + 1), new CurveGeometryId(i + 1), new ParameterInterval(0d, 1d)));
        }

        bindings.AddEdgeBinding(new EdgeGeometryBinding(topCircle, new CurveGeometryId(13), new ParameterInterval(0d, 2d * double.Pi)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(bottomCircle, new CurveGeometryId(14), new ParameterInterval(0d, 2d * double.Pi)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(seam, new CurveGeometryId(15), new ParameterInterval(0d, height)));

        bindings.AddFaceBinding(new FaceGeometryBinding(yMinFace, new SurfaceGeometryId(1)));
        bindings.AddFaceBinding(new FaceGeometryBinding(yMaxFace, new SurfaceGeometryId(2)));
        bindings.AddFaceBinding(new FaceGeometryBinding(xMinFace, new SurfaceGeometryId(3)));
        bindings.AddFaceBinding(new FaceGeometryBinding(xMaxFace, new SurfaceGeometryId(4)));
        bindings.AddFaceBinding(new FaceGeometryBinding(yFarFace, new SurfaceGeometryId(5)));
        bindings.AddFaceBinding(new FaceGeometryBinding(yNearFace, new SurfaceGeometryId(6)));
        bindings.AddFaceBinding(new FaceGeometryBinding(holeFace, new SurfaceGeometryId(7)));

        var vertexPoints = new Dictionary<VertexId, Point3D>
        {
            [v1] = p1,
            [v2] = p2,
            [v3] = p3,
            [v4] = p4,
            [v5] = p5,
            [v6] = p6,
            [v7] = p7,
            [v8] = p8,
            [topHoleVertex] = seamTopPoint,
            [bottomHoleVertex] = seamBottomPoint,
            [seamTopVertex] = seamTopPoint,
            [seamBottomVertex] = seamBottomPoint,
        };

        var body = new BrepBody(builder.Model, geometry, bindings, vertexPoints);
        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        return validation.IsSuccess
            ? KernelResult<BrepBody>.Success(body, validation.Diagnostics)
            : KernelResult<BrepBody>.Failure(validation.Diagnostics);
    }

    private static LoopId AddLoop(TopologyBuilder builder, IReadOnlyList<EdgeUse> edgeUses)
    {
        var loopId = builder.AllocateLoopId();
        var coedgeIds = new CoedgeId[edgeUses.Count];

        for (var i = 0; i < edgeUses.Count; i++)
        {
            coedgeIds[i] = builder.AllocateCoedgeId();
        }

        for (var i = 0; i < edgeUses.Count; i++)
        {
            var next = coedgeIds[(i + 1) % edgeUses.Count];
            var prev = coedgeIds[(i + edgeUses.Count - 1) % edgeUses.Count];
            builder.AddCoedge(new Coedge(coedgeIds[i], edgeUses[i].EdgeId, loopId, next, prev, edgeUses[i].IsReversed));
        }

        builder.AddLoop(new Loop(loopId, coedgeIds));
        return loopId;
    }

    private readonly record struct EdgeUse(EdgeId EdgeId, bool IsReversed);

    private static EdgeUse Forward(EdgeId edgeId) => new(edgeId, IsReversed: false);

    private static EdgeUse Reversed(EdgeId edgeId) => new(edgeId, IsReversed: true);
}
