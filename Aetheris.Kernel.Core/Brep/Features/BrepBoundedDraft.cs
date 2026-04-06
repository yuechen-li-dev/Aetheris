using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Features;

[Flags]
public enum BrepBoundedDraftFaces
{
    None = 0,
    XMin = 1,
    XMax = 2,
    YMin = 4,
    YMax = 8
}

/// <summary>
/// M4 bounded manufacturing draft for axis-aligned box roots only.
/// Supports planar side-face draft against world +Z pull with a uniform inward angle.
/// </summary>
public static class BrepBoundedDraft
{
    public static KernelResult<BrepBody> DraftAxisAlignedBoxSideFaces(
        AxisAlignedBoxExtents box,
        double draftAngleDegrees,
        BrepBoundedDraftFaces faces)
    {
        var diagnostics = new List<KernelDiagnostic>();

        if (faces == BrepBoundedDraftFaces.None)
        {
            diagnostics.Add(Failure("Bounded draft requires at least one explicit side face in {x_min,x_max,y_min,y_max}.", "firmament.draft-bounded"));
            return KernelResult<BrepBody>.Failure(diagnostics);
        }

        if (!double.IsFinite(draftAngleDegrees) || draftAngleDegrees <= 0d)
        {
            diagnostics.Add(Failure("Bounded draft angle must be a finite value greater than 0 degrees.", "firmament.draft-bounded"));
            return KernelResult<BrepBody>.Failure(diagnostics);
        }

        var height = box.MaxZ - box.MinZ;
        if (!double.IsFinite(height) || height <= 0d)
        {
            diagnostics.Add(Failure("Bounded draft source box must have positive height.", "firmament.draft-bounded"));
            return KernelResult<BrepBody>.Failure(diagnostics);
        }

        var radians = draftAngleDegrees * (System.Math.PI / 180d);
        var inset = System.Math.Tan(radians) * height;

        var topMinX = box.MinX + (faces.HasFlag(BrepBoundedDraftFaces.XMin) ? inset : 0d);
        var topMaxX = box.MaxX - (faces.HasFlag(BrepBoundedDraftFaces.XMax) ? inset : 0d);
        var topMinY = box.MinY + (faces.HasFlag(BrepBoundedDraftFaces.YMin) ? inset : 0d);
        var topMaxY = box.MaxY - (faces.HasFlag(BrepBoundedDraftFaces.YMax) ? inset : 0d);

        if (topMaxX <= topMinX || topMaxY <= topMinY)
        {
            diagnostics.Add(Failure("Bounded draft collapses top profile (angle too large for selected faces and box height).", "firmament.draft-bounded"));
            return KernelResult<BrepBody>.Failure(diagnostics);
        }

        return CreateLoftedRectangularPrism(
            box.MinX,
            box.MaxX,
            box.MinY,
            box.MaxY,
            box.MinZ,
            topMinX,
            topMaxX,
            topMinY,
            topMaxY,
            box.MaxZ);
    }

    private static KernelResult<BrepBody> CreateLoftedRectangularPrism(
        double bottomMinX,
        double bottomMaxX,
        double bottomMinY,
        double bottomMaxY,
        double bottomZ,
        double topMinX,
        double topMaxX,
        double topMinY,
        double topMaxY,
        double topZ)
    {
        var builder = new TopologyBuilder();

        var bottomVertices = new VertexId[4];
        var topVertices = new VertexId[4];
        for (var i = 0; i < 4; i++)
        {
            bottomVertices[i] = builder.AddVertex();
            topVertices[i] = builder.AddVertex();
        }

        var bottomEdges = new EdgeId[4];
        var topEdges = new EdgeId[4];
        var sideEdges = new EdgeId[4];
        for (var i = 0; i < 4; i++)
        {
            var next = (i + 1) % 4;
            bottomEdges[i] = builder.AddEdge(bottomVertices[i], bottomVertices[next]);
            topEdges[i] = builder.AddEdge(topVertices[i], topVertices[next]);
            sideEdges[i] = builder.AddEdge(bottomVertices[i], topVertices[i]);
        }

        var faces = new List<FaceId>(6)
        {
            AddFaceWithLoop(builder, bottomEdges.Select(EdgeUse.Forward).ToArray()),
            AddFaceWithLoop(builder, topEdges.Select(EdgeUse.Reversed).ToArray())
        };

        for (var i = 0; i < 4; i++)
        {
            var next = (i + 1) % 4;
            faces.Add(AddFaceWithLoop(
                builder,
                [
                    EdgeUse.Forward(bottomEdges[i]),
                    EdgeUse.Forward(sideEdges[next]),
                    EdgeUse.Reversed(topEdges[i]),
                    EdgeUse.Reversed(sideEdges[i])
                ]));
        }

        var shell = builder.AddShell(faces);
        builder.AddBody([shell]);

        var b = new[]
        {
            new Point3D(bottomMinX, bottomMinY, bottomZ),
            new Point3D(bottomMaxX, bottomMinY, bottomZ),
            new Point3D(bottomMaxX, bottomMaxY, bottomZ),
            new Point3D(bottomMinX, bottomMaxY, bottomZ)
        };

        var t = new[]
        {
            new Point3D(topMinX, topMinY, topZ),
            new Point3D(topMaxX, topMinY, topZ),
            new Point3D(topMaxX, topMaxY, topZ),
            new Point3D(topMinX, topMaxY, topZ)
        };

        var geometry = new BrepGeometryStore();
        var curveId = 1;
        for (var i = 0; i < 4; i++)
        {
            var next = (i + 1) % 4;
            geometry.AddCurve(new CurveGeometryId(curveId++), CurveGeometry.FromLine(new Line3Curve(b[i], Direction3D.Create(b[next] - b[i]))));
        }

        for (var i = 0; i < 4; i++)
        {
            var next = (i + 1) % 4;
            geometry.AddCurve(new CurveGeometryId(curveId++), CurveGeometry.FromLine(new Line3Curve(t[i], Direction3D.Create(t[next] - t[i]))));
        }

        for (var i = 0; i < 4; i++)
        {
            geometry.AddCurve(new CurveGeometryId(curveId++), CurveGeometry.FromLine(new Line3Curve(b[i], Direction3D.Create(t[i] - b[i]))));
        }

        geometry.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromPlane(new PlaneSurface(b[0], Direction3D.Create(new Vector3D(0d, 0d, -1d)), Direction3D.Create(new Vector3D(1d, 0d, 0d)))));
        geometry.AddSurface(new SurfaceGeometryId(2), SurfaceGeometry.FromPlane(new PlaneSurface(t[0], Direction3D.Create(new Vector3D(0d, 0d, 1d)), Direction3D.Create(new Vector3D(1d, 0d, 0d)))));

        for (var i = 0; i < 4; i++)
        {
            var next = (i + 1) % 4;
            var edgeDir = t[next] - t[i];
            var sideDir = t[i] - b[i];
            var normal = Direction3D.Create(edgeDir.Cross(sideDir));
            geometry.AddSurface(new SurfaceGeometryId(3 + i), SurfaceGeometry.FromPlane(new PlaneSurface(b[i], normal, Direction3D.Create(edgeDir))));
        }

        var bindings = new BrepBindingModel();
        for (var i = 0; i < 4; i++)
        {
            bindings.AddEdgeBinding(new EdgeGeometryBinding(bottomEdges[i], new CurveGeometryId(i + 1), new ParameterInterval(0d, (b[(i + 1) % 4] - b[i]).Length)));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(topEdges[i], new CurveGeometryId(5 + i), new ParameterInterval(0d, (t[(i + 1) % 4] - t[i]).Length)));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(sideEdges[i], new CurveGeometryId(9 + i), new ParameterInterval(0d, (t[i] - b[i]).Length)));
        }

        for (var i = 0; i < faces.Count; i++)
        {
            bindings.AddFaceBinding(new FaceGeometryBinding(faces[i], new SurfaceGeometryId(i + 1)));
        }

        var body = new BrepBody(builder.Model, geometry, bindings);
        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        return validation.IsSuccess
            ? KernelResult<BrepBody>.Success(body, validation.Diagnostics)
            : KernelResult<BrepBody>.Failure(validation.Diagnostics);
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

    private static KernelDiagnostic Failure(string message, string source)
        => new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message, Source: source);

    private readonly record struct EdgeUse(EdgeId EdgeId, bool IsReversed)
    {
        public static EdgeUse Forward(EdgeId edgeId) => new(edgeId, false);
        public static EdgeUse Reversed(EdgeId edgeId) => new(edgeId, true);
    }
}
