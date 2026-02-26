using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Features;

/// <summary>
/// M10 minimal programmatic extrusion for a single outer polyline profile.
/// </summary>
public static class BrepExtrude
{
    public static KernelResult<BrepBody> Create(PolylineProfile2D profile, ExtrudeFrame3D frame, double depth)
    {
        var diagnostics = new List<KernelDiagnostic>();
        if (!double.IsFinite(depth) || depth <= 0d)
        {
            diagnostics.Add(CreateInvalidArgument("depth must be finite and greater than zero."));
        }

        if (profile is null)
        {
            diagnostics.Add(CreateInvalidArgument("profile must be provided."));
            return KernelResult<BrepBody>.Failure(diagnostics);
        }

        diagnostics.AddRange(PolylineProfile2D.ValidateVertices(profile.Vertices));
        if (diagnostics.Count > 0)
        {
            return KernelResult<BrepBody>.Failure(diagnostics);
        }

        var vertices2D = profile.Vertices;
        var n = vertices2D.Count;
        var builder = new TopologyBuilder();

        var bottomVertices = new VertexId[n];
        var topVertices = new VertexId[n];
        for (var i = 0; i < n; i++)
        {
            bottomVertices[i] = builder.AddVertex();
        }

        for (var i = 0; i < n; i++)
        {
            topVertices[i] = builder.AddVertex();
        }

        var bottomEdges = new EdgeId[n];
        var topEdges = new EdgeId[n];
        var sideEdges = new EdgeId[n];

        for (var i = 0; i < n; i++)
        {
            var next = (i + 1) % n;
            bottomEdges[i] = builder.AddEdge(bottomVertices[i], bottomVertices[next]);
            topEdges[i] = builder.AddEdge(topVertices[i], topVertices[next]);
            sideEdges[i] = builder.AddEdge(bottomVertices[i], topVertices[i]);
        }

        var faces = new List<FaceId>(n + 2)
        {
            AddFaceWithLoop(builder, bottomEdges.Select(EdgeUse.Forward).ToArray()),
            AddFaceWithLoop(builder, topEdges.Select(EdgeUse.Reversed).ToArray()),
        };

        for (var i = 0; i < n; i++)
        {
            var next = (i + 1) % n;
            faces.Add(
                AddFaceWithLoop(
                    builder,
                    [
                        EdgeUse.Forward(bottomEdges[i]),
                        EdgeUse.Forward(sideEdges[next]),
                        EdgeUse.Reversed(topEdges[i]),
                        EdgeUse.Reversed(sideEdges[i]),
                    ]));
        }

        var shell = builder.AddShell(faces);
        builder.AddBody([shell]);

        var geometry = new BrepGeometryStore();
        var bottomPoints = vertices2D.Select(v => frame.ToWorld(v)).ToArray();
        var topPoints = vertices2D.Select(v => frame.ToWorld(v, depth)).ToArray();

        var curveId = 1;
        for (var i = 0; i < n; i++)
        {
            var next = (i + 1) % n;
            geometry.AddCurve(new CurveGeometryId(curveId++), CurveGeometry.FromLine(CreateLine(bottomPoints[i], bottomPoints[next])));
        }

        for (var i = 0; i < n; i++)
        {
            var next = (i + 1) % n;
            geometry.AddCurve(new CurveGeometryId(curveId++), CurveGeometry.FromLine(CreateLine(topPoints[i], topPoints[next])));
        }

        for (var i = 0; i < n; i++)
        {
            geometry.AddCurve(new CurveGeometryId(curveId++), CurveGeometry.FromLine(CreateLine(bottomPoints[i], topPoints[i])));
        }

        var bottomSurfaceId = new SurfaceGeometryId(1);
        var topSurfaceId = new SurfaceGeometryId(2);
        geometry.AddSurface(bottomSurfaceId, SurfaceGeometry.FromPlane(new PlaneSurface(frame.Origin, Direction3D.Create(-frame.Normal.ToVector()), frame.UAxis)));
        geometry.AddSurface(topSurfaceId, SurfaceGeometry.FromPlane(new PlaneSurface(frame.Origin + (frame.Normal.ToVector() * depth), frame.Normal, frame.UAxis)));

        for (var i = 0; i < n; i++)
        {
            var next = (i + 1) % n;
            var edgeDirection = topPoints[next] - topPoints[i];
            var sideNormal = Direction3D.Create(edgeDirection.Cross(frame.Normal.ToVector()));
            geometry.AddSurface(
                new SurfaceGeometryId(i + 3),
                SurfaceGeometry.FromPlane(new PlaneSurface(bottomPoints[i], sideNormal, frame.Normal)));
        }

        var bindings = new BrepBindingModel();
        for (var i = 0; i < n; i++)
        {
            bindings.AddEdgeBinding(new EdgeGeometryBinding(bottomEdges[i], new CurveGeometryId(i + 1), new ParameterInterval(0d, (bottomPoints[(i + 1) % n] - bottomPoints[i]).Length)));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(topEdges[i], new CurveGeometryId(n + i + 1), new ParameterInterval(0d, (topPoints[(i + 1) % n] - topPoints[i]).Length)));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(sideEdges[i], new CurveGeometryId((2 * n) + i + 1), new ParameterInterval(0d, depth)));
        }

        bindings.AddFaceBinding(new FaceGeometryBinding(faces[0], bottomSurfaceId));
        bindings.AddFaceBinding(new FaceGeometryBinding(faces[1], topSurfaceId));
        for (var i = 0; i < n; i++)
        {
            bindings.AddFaceBinding(new FaceGeometryBinding(faces[i + 2], new SurfaceGeometryId(i + 3)));
        }

        var body = new BrepBody(builder.Model, geometry, bindings);
        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        return validation.IsSuccess
            ? KernelResult<BrepBody>.Success(body, validation.Diagnostics)
            : KernelResult<BrepBody>.Failure(validation.Diagnostics);
    }

    private static Line3Curve CreateLine(Point3D start, Point3D end)
    {
        return new Line3Curve(start, Direction3D.Create(end - start));
    }

    private static FaceId AddFaceWithLoop(TopologyBuilder builder, IReadOnlyList<EdgeUse> edgeUses)
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
        return builder.AddFace([loopId]);
    }

    private static KernelDiagnostic CreateInvalidArgument(string message)
        => new(KernelDiagnosticCode.InvalidArgument, KernelDiagnosticSeverity.Error, message);

    private readonly record struct EdgeUse(EdgeId EdgeId, bool IsReversed)
    {
        public static EdgeUse Forward(EdgeId edgeId) => new(edgeId, false);

        public static EdgeUse Reversed(EdgeId edgeId) => new(edgeId, true);
    }
}
