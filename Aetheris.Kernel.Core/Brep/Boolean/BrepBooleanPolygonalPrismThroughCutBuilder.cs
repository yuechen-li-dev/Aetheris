using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Boolean;

internal static class BrepBooleanPolygonalPrismThroughCutBuilder
{
    public static KernelResult<BrepBody> Build(
        IReadOnlyList<(double X, double Y)> outerFootprint,
        AxisAlignedBoxExtents rootBounds,
        IReadOnlyList<(double X, double Y)> innerFootprint)
    {
        if (outerFootprint.Count < 3 || innerFootprint.Count < 3)
        {
            return KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    KernelDiagnosticSeverity.Error,
                    "Bounded prism-family subtract requires valid outer/inner footprints with at least three vertices.",
                    "BrepBooleanPolygonalPrismThroughCutBuilder.Build")
            ]);
        }

        var zMin = rootBounds.MinZ;
        var zMax = rootBounds.MaxZ;

        var outerBottomPoints = outerFootprint.Select(point => new Point3D(point.X, point.Y, zMin)).ToArray();
        var outerTopPoints = outerFootprint.Select(point => new Point3D(point.X, point.Y, zMax)).ToArray();
        var innerBottomPoints = innerFootprint.Select(point => new Point3D(point.X, point.Y, zMin)).ToArray();
        var innerTopPoints = innerFootprint.Select(point => new Point3D(point.X, point.Y, zMax)).ToArray();

        var builder = new TopologyBuilder();

        var outerBottomVertices = outerBottomPoints.Select(_ => builder.AddVertex()).ToArray();
        var outerTopVertices = outerTopPoints.Select(_ => builder.AddVertex()).ToArray();
        var innerBottomVertices = innerBottomPoints.Select(_ => builder.AddVertex()).ToArray();
        var innerTopVertices = innerTopPoints.Select(_ => builder.AddVertex()).ToArray();

        var outerBottomEdges = BuildEdgeLoop(builder, outerBottomVertices);
        var outerTopEdges = BuildEdgeLoop(builder, outerTopVertices);
        var innerBottomEdges = BuildEdgeLoop(builder, innerBottomVertices);
        var innerTopEdges = BuildEdgeLoop(builder, innerTopVertices);
        var outerVerticalEdges = BuildVerticalEdges(builder, outerBottomVertices, outerTopVertices);
        var innerVerticalEdges = BuildVerticalEdges(builder, innerBottomVertices, innerTopVertices);

        var bottomFace = AddFaceWithLoops(
            builder,
            [.. outerBottomEdges.Select(EdgeUse.Forward)],
            [.. innerBottomEdges.Select(EdgeUse.Reversed)]);

        var topFace = AddFaceWithLoops(
            builder,
            [.. outerTopEdges.Reverse().Select(EdgeUse.Reversed)],
            [.. innerTopEdges.Select(EdgeUse.Forward)]);

        var faces = new List<FaceId> { bottomFace, topFace };
        faces.AddRange(AddSideFaces(builder, outerBottomEdges, outerTopEdges, outerVerticalEdges, reverseInnerOrientation: false));
        faces.AddRange(AddSideFaces(builder, innerBottomEdges, innerTopEdges, innerVerticalEdges, reverseInnerOrientation: true));

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

        BindLoopEdges(outerBottomEdges, outerBottomPoints, AddLinearEdge);
        BindLoopEdges(outerTopEdges, outerTopPoints, AddLinearEdge);
        BindLoopEdges(innerBottomEdges, innerBottomPoints, AddLinearEdge);
        BindLoopEdges(innerTopEdges, innerTopPoints, AddLinearEdge);
        BindVerticalEdges(outerVerticalEdges, outerBottomPoints, outerTopPoints, AddLinearEdge);
        BindVerticalEdges(innerVerticalEdges, innerBottomPoints, innerTopPoints, AddLinearEdge);

        var surfaceId = 1;
        void BindFaceToPlane(FaceId face, Point3D origin, Vector3D normal, Vector3D uAxis)
        {
            var sid = new SurfaceGeometryId(surfaceId++);
            geometry.AddSurface(sid, SurfaceGeometry.FromPlane(new PlaneSurface(origin, Direction3D.Create(normal), Direction3D.Create(uAxis))));
            bindings.AddFaceBinding(new FaceGeometryBinding(face, sid));
        }

        BindFaceToPlane(bottomFace, outerBottomPoints[0], new Vector3D(0d, 0d, -1d), new Vector3D(1d, 0d, 0d));
        BindFaceToPlane(topFace, outerTopPoints[0], new Vector3D(0d, 0d, 1d), new Vector3D(1d, 0d, 0d));

        var faceIndex = 2;
        for (var i = 0; i < outerBottomPoints.Length; i++)
        {
            var next = (i + 1) % outerBottomPoints.Length;
            var edgeDir = outerBottomPoints[next] - outerBottomPoints[i];
            var outwardNormal = Direction3D.Create(new Vector3D(edgeDir.Y, -edgeDir.X, 0d));
            BindFaceToPlane(faces[faceIndex++], outerBottomPoints[i], outwardNormal.ToVector(), new Vector3D(0d, 0d, 1d));
        }

        for (var i = 0; i < innerBottomPoints.Length; i++)
        {
            var next = (i + 1) % innerBottomPoints.Length;
            var edgeDir = innerBottomPoints[next] - innerBottomPoints[i];
            var inwardNormal = Direction3D.Create(new Vector3D(-edgeDir.Y, edgeDir.X, 0d));
            BindFaceToPlane(faces[faceIndex++], innerBottomPoints[i], inwardNormal.ToVector(), new Vector3D(0d, 0d, 1d));
        }

        var vertexPoints = new Dictionary<VertexId, Point3D>();
        for (var i = 0; i < outerBottomVertices.Length; i++)
        {
            vertexPoints[outerBottomVertices[i]] = outerBottomPoints[i];
            vertexPoints[outerTopVertices[i]] = outerTopPoints[i];
        }

        for (var i = 0; i < innerBottomVertices.Length; i++)
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

    private static EdgeId[] BuildEdgeLoop(TopologyBuilder builder, IReadOnlyList<VertexId> vertices)
    {
        var edges = new EdgeId[vertices.Count];
        for (var i = 0; i < vertices.Count; i++)
        {
            edges[i] = builder.AddEdge(vertices[i], vertices[(i + 1) % vertices.Count]);
        }

        return edges;
    }

    private static EdgeId[] BuildVerticalEdges(TopologyBuilder builder, IReadOnlyList<VertexId> bottomVertices, IReadOnlyList<VertexId> topVertices)
    {
        var edges = new EdgeId[bottomVertices.Count];
        for (var i = 0; i < bottomVertices.Count; i++)
        {
            edges[i] = builder.AddEdge(bottomVertices[i], topVertices[i]);
        }

        return edges;
    }

    private static IReadOnlyList<FaceId> AddSideFaces(TopologyBuilder builder, EdgeId[] bottom, EdgeId[] top, EdgeId[] vertical, bool reverseInnerOrientation)
    {
        var faces = new List<FaceId>(bottom.Length);
        for (var i = 0; i < bottom.Length; i++)
        {
            var next = (i + 1) % bottom.Length;
            var uses = reverseInnerOrientation
                ? new[]
                {
                    EdgeUse.Reversed(bottom[i]),
                    EdgeUse.Forward(vertical[i]),
                    EdgeUse.Forward(top[i]),
                    EdgeUse.Reversed(vertical[next]),
                }
                : new[]
                {
                    EdgeUse.Forward(bottom[i]),
                    EdgeUse.Forward(vertical[next]),
                    EdgeUse.Reversed(top[i]),
                    EdgeUse.Reversed(vertical[i]),
                };
            faces.Add(AddFaceWithLoop(builder, uses));
        }

        return faces;
    }

    private static void BindLoopEdges(EdgeId[] edges, Point3D[] points, Action<EdgeId, Point3D, Point3D> bind)
    {
        for (var i = 0; i < edges.Length; i++)
        {
            bind(edges[i], points[i], points[(i + 1) % points.Length]);
        }
    }

    private static void BindVerticalEdges(EdgeId[] edges, Point3D[] bottomPoints, Point3D[] topPoints, Action<EdgeId, Point3D, Point3D> bind)
    {
        for (var i = 0; i < edges.Length; i++)
        {
            bind(edges[i], bottomPoints[i], topPoints[i]);
        }
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
