using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Features;

/// <summary>
/// M11 minimal programmatic revolve.
/// Supported subset: one open line segment profile (two vertices) with strictly positive radius at both endpoints.
/// Revolve angle is currently full-turn only (2*pi radians).
/// </summary>
public static class BrepRevolve
{
    public static KernelResult<BrepBody> Create(
        IReadOnlyList<ProfilePoint2D> profileVertices,
        ExtrudeFrame3D frame,
        RevolveAxis3D axis,
        double angleRadians = 2d * double.Pi)
    {
        var diagnostics = ValidateInputs(profileVertices, frame, axis, angleRadians, out var axisDirection, out var seamReferenceDirection);
        if (diagnostics.Count > 0)
        {
            return KernelResult<BrepBody>.Failure(diagnostics);
        }

        var p0 = profileVertices[0];
        var p1 = profileVertices[1];

        var center0 = axis.Origin + (axisDirection.ToVector() * p0.Y);
        var center1 = axis.Origin + (axisDirection.ToVector() * p1.Y);
        var rim0 = center0 + (seamReferenceDirection.ToVector() * p0.X);
        var rim1 = center1 + (seamReferenceDirection.ToVector() * p1.X);
        var seamLength = (rim1 - rim0).Length;

        var builder = new TopologyBuilder();
        var seamStart = builder.AddVertex();
        var seamEnd = builder.AddVertex();
        var cap0Vertex = builder.AddVertex();
        var cap1Vertex = builder.AddVertex();

        var seamEdge = builder.AddEdge(seamStart, seamEnd);
        var cap0Edge = builder.AddEdge(cap0Vertex, cap0Vertex);
        var cap1Edge = builder.AddEdge(cap1Vertex, cap1Vertex);

        // Seam strategy: represent periodic side surface with one explicit seam edge used twice in the side loop.
        var sideFace = AddFaceWithLoop(
            builder,
            [
                EdgeUse.Forward(seamEdge),
                EdgeUse.Forward(cap1Edge),
                EdgeUse.Reversed(seamEdge),
                EdgeUse.Reversed(cap0Edge),
            ]);

        var cap0Face = AddFaceWithLoop(builder, [EdgeUse.Forward(cap0Edge)]);
        var cap1Face = AddFaceWithLoop(builder, [EdgeUse.Reversed(cap1Edge)]);

        var shell = builder.AddShell([sideFace, cap0Face, cap1Face]);
        builder.AddBody([shell]);

        var geometry = new BrepGeometryStore();
        geometry.AddCurve(new CurveGeometryId(1), CurveGeometry.FromLine(new Line3Curve(rim0, Direction3D.Create(rim1 - rim0))));
        geometry.AddCurve(new CurveGeometryId(2), CurveGeometry.FromCircle(new Circle3Curve(center0, axisDirection, p0.X, seamReferenceDirection)));
        geometry.AddCurve(new CurveGeometryId(3), CurveGeometry.FromCircle(new Circle3Curve(center1, axisDirection, p1.X, seamReferenceDirection)));

        var isCylinder = double.Abs(p0.X - p1.X) <= 1e-12d;
        if (isCylinder)
        {
            geometry.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromCylinder(new CylinderSurface(center0, axisDirection, p0.X, seamReferenceDirection)));
        }
        else
        {
            var slope = (p1.X - p0.X) / (p1.Y - p0.Y);
            var apexY = p0.Y - (p0.X / slope);
            var apex = axis.Origin + (axisDirection.ToVector() * apexY);
            var semiAngle = double.Atan(double.Abs(slope));
            var coneAxis = slope > 0d ? axisDirection : Direction3D.Create(-axisDirection.ToVector());
            geometry.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromCone(new ConeSurface(apex, coneAxis, semiAngle, seamReferenceDirection)));
        }

        geometry.AddSurface(new SurfaceGeometryId(2), SurfaceGeometry.FromPlane(new PlaneSurface(center0, Direction3D.Create(-axisDirection.ToVector()), seamReferenceDirection)));
        geometry.AddSurface(new SurfaceGeometryId(3), SurfaceGeometry.FromPlane(new PlaneSurface(center1, axisDirection, seamReferenceDirection)));

        var bindings = new BrepBindingModel();
        bindings.AddEdgeBinding(new EdgeGeometryBinding(seamEdge, new CurveGeometryId(1), new ParameterInterval(0d, seamLength)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(cap0Edge, new CurveGeometryId(2), new ParameterInterval(0d, 2d * double.Pi)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(cap1Edge, new CurveGeometryId(3), new ParameterInterval(0d, 2d * double.Pi)));

        bindings.AddFaceBinding(new FaceGeometryBinding(sideFace, new SurfaceGeometryId(1)));
        bindings.AddFaceBinding(new FaceGeometryBinding(cap0Face, new SurfaceGeometryId(2)));
        bindings.AddFaceBinding(new FaceGeometryBinding(cap1Face, new SurfaceGeometryId(3)));

        var body = new BrepBody(builder.Model, geometry, bindings);
        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        return validation.IsSuccess
            ? KernelResult<BrepBody>.Success(body, validation.Diagnostics)
            : KernelResult<BrepBody>.Failure(validation.Diagnostics);
    }

    private static List<KernelDiagnostic> ValidateInputs(
        IReadOnlyList<ProfilePoint2D>? profileVertices,
        ExtrudeFrame3D frame,
        RevolveAxis3D axis,
        double angleRadians,
        out Direction3D axisDirection,
        out Direction3D seamReferenceDirection)
    {
        var diagnostics = new List<KernelDiagnostic>();
        axisDirection = default;
        seamReferenceDirection = default;

        if (profileVertices is null)
        {
            diagnostics.Add(CreateInvalidArgument("profileVertices must be provided."));
            return diagnostics;
        }

        if (!Direction3D.TryCreate(axis.Direction, out axisDirection))
        {
            diagnostics.Add(CreateInvalidArgument("axis direction must be finite and non-zero."));
        }

        if (!double.IsFinite(angleRadians) || angleRadians <= 0d)
        {
            diagnostics.Add(CreateInvalidArgument("angleRadians must be finite and greater than zero."));
        }
        else if (double.Abs(angleRadians - (2d * double.Pi)) > 1e-12d)
        {
            diagnostics.Add(CreateNotImplemented("M11 supports full revolve only (angleRadians must be 2*pi)."));
        }

        if (profileVertices.Count != 2)
        {
            diagnostics.Add(CreateNotImplemented("M11 supports only a two-point line segment profile."));
            return diagnostics;
        }

        for (var i = 0; i < profileVertices.Count; i++)
        {
            var point = profileVertices[i];
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
            {
                diagnostics.Add(CreateInvalidArgument("profile coordinates must be finite."));
                return diagnostics;
            }

            if (point.X <= 0d)
            {
                diagnostics.Add(CreateInvalidArgument("profile radius (X) must be greater than zero; axis-touching/crossing profiles are unsupported in M11."));
                return diagnostics;
            }
        }

        if (double.Abs(profileVertices[1].Y - profileVertices[0].Y) <= 1e-12d)
        {
            diagnostics.Add(CreateInvalidArgument("profile segment must span non-zero axis distance (Y coordinates must differ)."));
        }

        if (diagnostics.Count > 0)
        {
            return diagnostics;
        }

        var projectedReference = frame.UAxis.ToVector() - (axisDirection.ToVector() * frame.UAxis.ToVector().Dot(axisDirection.ToVector()));
        if (!Direction3D.TryCreate(projectedReference, out seamReferenceDirection))
        {
            diagnostics.Add(CreateInvalidArgument("frame.UAxis must not be parallel to the revolve axis."));
        }

        return diagnostics;
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

    private static KernelDiagnostic CreateNotImplemented(string message)
        => new(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, message);

    private readonly record struct EdgeUse(EdgeId EdgeId, bool IsReversed)
    {
        public static EdgeUse Forward(EdgeId edgeId) => new(edgeId, false);

        public static EdgeUse Reversed(EdgeId edgeId) => new(edgeId, true);
    }
}
