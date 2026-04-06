using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Boolean;

public static class BrepBooleanBoxPrismThroughCutBuilder
{
    public static KernelResult<BrepBody> Build(
        AxisAlignedBoxExtents box,
        IReadOnlyList<(double X, double Y)> footprint,
        ToleranceContext _)
    {
        if (footprint.Count < 3)
        {
            return KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error,
                    "Bounded prism-family subtract requires a valid prism footprint with at least three vertices.",
                    "BrepBooleanBoxPrismThroughCutBuilder.Build")
            ]);
        }

        var zMin = box.MinZ;
        var zMax = box.MaxZ;

        var outerBottomPoints = new[]
        {
            new Point3D(box.MinX, box.MinY, zMin),
            new Point3D(box.MaxX, box.MinY, zMin),
            new Point3D(box.MaxX, box.MaxY, zMin),
            new Point3D(box.MinX, box.MaxY, zMin),
        };
        var outerTopPoints = outerBottomPoints.Select(point => new Point3D(point.X, point.Y, zMax)).ToArray();

        var n = footprint.Count;
        var innerBottomPoints = footprint.Select(point => new Point3D(point.X, point.Y, zMin)).ToArray();
        var innerTopPoints = footprint.Select(point => new Point3D(point.X, point.Y, zMax)).ToArray();

        var builder = new TopologyBuilder();

        var outerBottomVertices = outerBottomPoints.Select(_ => builder.AddVertex()).ToArray();
        var outerTopVertices = outerTopPoints.Select(_ => builder.AddVertex()).ToArray();
        var innerBottomVertices = innerBottomPoints.Select(_ => builder.AddVertex()).ToArray();
        var innerTopVertices = innerTopPoints.Select(_ => builder.AddVertex()).ToArray();

        var outerBottomEdges = new EdgeId[4];
        var outerTopEdges = new EdgeId[4];
        var outerVerticalEdges = new EdgeId[4];
        for (var i = 0; i < 4; i++)
        {
            var next = (i + 1) % 4;
            outerBottomEdges[i] = builder.AddEdge(outerBottomVertices[i], outerBottomVertices[next]);
            outerTopEdges[i] = builder.AddEdge(outerTopVertices[i], outerTopVertices[next]);
            outerVerticalEdges[i] = builder.AddEdge(outerBottomVertices[i], outerTopVertices[i]);
        }

        var innerBottomEdges = new EdgeId[n];
        var innerTopEdges = new EdgeId[n];
        var innerVerticalEdges = new EdgeId[n];
        for (var i = 0; i < n; i++)
        {
            var next = (i + 1) % n;
            innerBottomEdges[i] = builder.AddEdge(innerBottomVertices[i], innerBottomVertices[next]);
            innerTopEdges[i] = builder.AddEdge(innerTopVertices[i], innerTopVertices[next]);
            innerVerticalEdges[i] = builder.AddEdge(innerBottomVertices[i], innerTopVertices[i]);
        }

        var bottomFace = AddFaceWithLoops(builder,
            [EdgeUse.Forward(outerBottomEdges[0]), EdgeUse.Forward(outerBottomEdges[1]), EdgeUse.Forward(outerBottomEdges[2]), EdgeUse.Forward(outerBottomEdges[3])],
            [.. innerBottomEdges.Select(EdgeUse.Reversed)]);

        var topFace = AddFaceWithLoops(builder,
            [EdgeUse.Reversed(outerTopEdges[3]), EdgeUse.Reversed(outerTopEdges[2]), EdgeUse.Reversed(outerTopEdges[1]), EdgeUse.Reversed(outerTopEdges[0])],
            [.. innerTopEdges.Select(EdgeUse.Forward)]);

        var faces = new List<FaceId>(n + 6)
        {
            bottomFace,
            topFace,
            AddFaceWithLoop(builder, [EdgeUse.Forward(outerBottomEdges[0]), EdgeUse.Forward(outerVerticalEdges[1]), EdgeUse.Reversed(outerTopEdges[0]), EdgeUse.Reversed(outerVerticalEdges[0])]),
            AddFaceWithLoop(builder, [EdgeUse.Forward(outerBottomEdges[1]), EdgeUse.Forward(outerVerticalEdges[2]), EdgeUse.Reversed(outerTopEdges[1]), EdgeUse.Reversed(outerVerticalEdges[1])]),
            AddFaceWithLoop(builder, [EdgeUse.Forward(outerBottomEdges[2]), EdgeUse.Forward(outerVerticalEdges[3]), EdgeUse.Reversed(outerTopEdges[2]), EdgeUse.Reversed(outerVerticalEdges[2])]),
            AddFaceWithLoop(builder, [EdgeUse.Forward(outerBottomEdges[3]), EdgeUse.Forward(outerVerticalEdges[0]), EdgeUse.Reversed(outerTopEdges[3]), EdgeUse.Reversed(outerVerticalEdges[3])]),
        };

        for (var i = 0; i < n; i++)
        {
            var next = (i + 1) % n;
            faces.Add(AddFaceWithLoop(builder,
            [
                EdgeUse.Reversed(innerBottomEdges[i]),
                EdgeUse.Forward(innerVerticalEdges[i]),
                EdgeUse.Forward(innerTopEdges[i]),
                EdgeUse.Reversed(innerVerticalEdges[next]),
            ]));
        }

        var shell = builder.AddShell(faces);
        builder.AddBody([shell]);

        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();

        var curveId = 1;
        void AddLinearEdge(EdgeId edgeId, Point3D start, Point3D end)
        {
            var direction = end - start;
            geometry.AddCurve(new CurveGeometryId(curveId), CurveGeometry.FromLine(new Line3Curve(start, Direction3D.Create(direction))));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(edgeId, new CurveGeometryId(curveId), new ParameterInterval(0d, direction.Length)));
            curveId++;
        }

        for (var i = 0; i < 4; i++)
        {
            AddLinearEdge(outerBottomEdges[i], outerBottomPoints[i], outerBottomPoints[(i + 1) % 4]);
            AddLinearEdge(outerTopEdges[i], outerTopPoints[i], outerTopPoints[(i + 1) % 4]);
            AddLinearEdge(outerVerticalEdges[i], outerBottomPoints[i], outerTopPoints[i]);
        }

        for (var i = 0; i < n; i++)
        {
            var next = (i + 1) % n;
            AddLinearEdge(innerBottomEdges[i], innerBottomPoints[i], innerBottomPoints[next]);
            AddLinearEdge(innerTopEdges[i], innerTopPoints[i], innerTopPoints[next]);
            AddLinearEdge(innerVerticalEdges[i], innerBottomPoints[i], innerTopPoints[i]);
        }

        var surfaceId = 1;
        void BindFaceToPlane(FaceId face, Point3D origin, Vector3D normal, Vector3D uAxis)
        {
            var sid = new SurfaceGeometryId(surfaceId++);
            geometry.AddSurface(sid, SurfaceGeometry.FromPlane(new PlaneSurface(origin, Direction3D.Create(normal), Direction3D.Create(uAxis))));
            bindings.AddFaceBinding(new FaceGeometryBinding(face, sid));
        }

        BindFaceToPlane(bottomFace, outerBottomPoints[0], new Vector3D(0d, 0d, -1d), new Vector3D(1d, 0d, 0d));
        BindFaceToPlane(topFace, outerTopPoints[0], new Vector3D(0d, 0d, 1d), new Vector3D(1d, 0d, 0d));

        BindFaceToPlane(faces[2], new Point3D(box.MinX, box.MinY, zMin), new Vector3D(0d, -1d, 0d), new Vector3D(1d, 0d, 0d));
        BindFaceToPlane(faces[3], new Point3D(box.MaxX, box.MinY, zMin), new Vector3D(1d, 0d, 0d), new Vector3D(0d, 1d, 0d));
        BindFaceToPlane(faces[4], new Point3D(box.MaxX, box.MaxY, zMin), new Vector3D(0d, 1d, 0d), new Vector3D(-1d, 0d, 0d));
        BindFaceToPlane(faces[5], new Point3D(box.MinX, box.MaxY, zMin), new Vector3D(-1d, 0d, 0d), new Vector3D(0d, -1d, 0d));

        for (var i = 0; i < n; i++)
        {
            var next = (i + 1) % n;
            var edgeDir = innerBottomPoints[next] - innerBottomPoints[i];
            var inwardNormal = Direction3D.Create(new Vector3D(edgeDir.Y, -edgeDir.X, 0d));
            BindFaceToPlane(faces[6 + i], innerBottomPoints[i], inwardNormal.ToVector(), new Vector3D(0d, 0d, 1d));
        }

        var vertexPoints = new Dictionary<VertexId, Point3D>();
        for (var i = 0; i < 4; i++)
        {
            vertexPoints[outerBottomVertices[i]] = outerBottomPoints[i];
            vertexPoints[outerTopVertices[i]] = outerTopPoints[i];
        }

        for (var i = 0; i < n; i++)
        {
            vertexPoints[innerBottomVertices[i]] = innerBottomPoints[i];
            vertexPoints[innerTopVertices[i]] = innerTopPoints[i];
        }

        var body = new BrepBody(builder.Model, geometry, bindings, vertexPoints);
        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        return validation.IsSuccess
            ? KernelResult<BrepBody>.Success(body, validation.Diagnostics)
            : KernelResult<BrepBody>.Failure(validation.Diagnostics);
    }

    private static FaceId AddFaceWithLoops(TopologyBuilder builder, IReadOnlyList<EdgeUse> outerLoop, IReadOnlyList<EdgeUse> innerLoop)
    {
        var outerLoopId = AddLoop(builder, outerLoop);
        var innerLoopId = AddLoop(builder, innerLoop);
        return builder.AddFace([outerLoopId, innerLoopId]);
    }

    private static FaceId AddFaceWithLoop(TopologyBuilder builder, IReadOnlyList<EdgeUse> edgeUses)
    {
        var loopId = AddLoop(builder, edgeUses);
        return builder.AddFace([loopId]);
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

    private readonly record struct EdgeUse(EdgeId EdgeId, bool IsReversed)
    {
        public static EdgeUse Forward(EdgeId edgeId) => new(edgeId, false);
        public static EdgeUse Reversed(EdgeId edgeId) => new(edgeId, true);
    }
}
