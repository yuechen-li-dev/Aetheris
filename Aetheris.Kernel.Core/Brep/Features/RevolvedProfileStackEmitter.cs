using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Features;

internal sealed record RevolvedProfileTopologyPlan(
    IReadOnlyList<ProfilePoint2D> Profile,
    ExtrudeFrame3D Frame,
    RevolveAxis3D Axis,
    int ExpectedVertexCount,
    int ExpectedEdgeCount,
    int ExpectedFaceCount,
    int ExpectedLoopCount,
    int ExpectedCoedgeCount,
    string DeterministicSignature);

internal sealed record RevolvedProfileStackResult(
    BrepBody? Body,
    int VertexCount,
    int EdgeCount,
    int FaceCount,
    IReadOnlyList<string> Diagnostics)
{
    public bool Succeeded => Body is not null;
}

/// <summary>
/// Exact full-revolution materializer for a bounded, open, piecewise-linear radial profile.
/// Each profile segment becomes one analytic cylindrical or conical face; profile corners remain
/// explicit circular topology. This is construction geometry, not a chamfer feature opcode.
/// </summary>
internal static class RevolvedProfileStackEmitter
{
    private const double Tol = 1e-12;

    public static KernelResult<RevolvedProfileTopologyPlan> Plan(
        IReadOnlyList<ProfilePoint2D> profile,
        ExtrudeFrame3D frame,
        RevolveAxis3D axis)
    {
        var errors = Validate(profile, frame, axis, out _, out _);
        if (errors.Count != 0) return KernelResult<RevolvedProfileTopologyPlan>.Failure(errors);

        var n = profile.Count;
        var signatureText = string.Join(";", profile.Select(p => FormattableString.Invariant($"{p.X:R},{p.Y:R}")));
        var signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signatureText)));
        return KernelResult<RevolvedProfileTopologyPlan>.Success(new(
            profile.ToArray(), frame, axis,
            // A periodic rim and its generating seam meet at the same topology vertex.
            // Keeping separate coincident vertices creates disconnected face loops.
            ExpectedVertexCount: n,
            ExpectedEdgeCount: (n - 1) + n,
            ExpectedFaceCount: (n - 1) + 2,
            ExpectedLoopCount: (n - 1) + 2,
            ExpectedCoedgeCount: ((n - 1) * 4) + 2,
            DeterministicSignature: signature));
    }

    public static RevolvedProfileStackResult Emit(RevolvedProfileTopologyPlan plan)
    {
        var validation = Validate(plan.Profile, plan.Frame, plan.Axis, out var axisDirection, out var radialDirection);
        if (validation.Count != 0)
            return new(null, 0, 0, 0, validation.Select(d => d.Message).ToArray());

        var profile = plan.Profile;
        var centers = profile.Select(p => plan.Axis.Origin + axisDirection.ToVector() * p.Y).ToArray();
        var rims = profile.Select((p, i) => centers[i] + radialDirection.ToVector() * p.X).ToArray();
        var builder = new TopologyBuilder();
        var seamVertices = profile.Select(_ => builder.AddVertex()).ToArray();
        var seamEdges = Enumerable.Range(0, profile.Count - 1).Select(i => builder.AddEdge(seamVertices[i], seamVertices[i + 1])).ToArray();
        var circleEdges = Enumerable.Range(0, profile.Count).Select(i => builder.AddEdge(seamVertices[i], seamVertices[i])).ToArray();

        var sideFaces = new FaceId[profile.Count - 1];
        for (var i = 0; i < sideFaces.Length; i++)
            sideFaces[i] = AddFaceWithLoop(builder,
            [
                EdgeUse.Forward(seamEdges[i]),
                EdgeUse.Forward(circleEdges[i + 1]),
                EdgeUse.Reversed(seamEdges[i]),
                EdgeUse.Reversed(circleEdges[i]),
            ]);
        var bottomFace = AddFaceWithLoop(builder, [EdgeUse.Forward(circleEdges[0])]);
        var topFace = AddFaceWithLoop(builder, [EdgeUse.Reversed(circleEdges[^1])]);
        var shell = builder.AddShell([.. sideFaces, bottomFace, topFace]);
        builder.AddBody([shell]);

        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var curveId = 1;
        for (var i = 0; i < seamEdges.Length; i++)
        {
            var id = new CurveGeometryId(curveId++);
            geometry.AddCurve(id, CurveGeometry.FromLine(new Line3Curve(rims[i], Direction3D.Create(rims[i + 1] - rims[i]))));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(seamEdges[i], id, new ParameterInterval(0, (rims[i + 1] - rims[i]).Length)));
        }
        for (var i = 0; i < circleEdges.Length; i++)
        {
            var id = new CurveGeometryId(curveId++);
            geometry.AddCurve(id, CurveGeometry.FromCircle(new Circle3Curve(centers[i], axisDirection, profile[i].X, radialDirection)));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(circleEdges[i], id, new ParameterInterval(0, 2 * double.Pi)));
        }

        var surfaceId = 1;
        for (var i = 0; i < sideFaces.Length; i++)
        {
            var id = new SurfaceGeometryId(surfaceId++);
            var a = profile[i];
            var b = profile[i + 1];
            if (System.Math.Abs(a.X - b.X) <= Tol)
            {
                geometry.AddSurface(id, SurfaceGeometry.FromCylinder(new CylinderSurface(centers[i], axisDirection, a.X, radialDirection)));
            }
            else
            {
                var slope = (b.X - a.X) / (b.Y - a.Y);
                var apexY = a.Y - a.X / slope;
                var apex = plan.Axis.Origin + axisDirection.ToVector() * apexY;
                var coneAxis = slope > 0 ? axisDirection : Direction3D.Create(-axisDirection.ToVector());
                geometry.AddSurface(id, SurfaceGeometry.FromCone(new ConeSurface(apex, coneAxis, System.Math.Atan(System.Math.Abs(slope)), radialDirection)));
            }
            bindings.AddFaceBinding(new FaceGeometryBinding(sideFaces[i], id));
        }
        var bottomSurface = new SurfaceGeometryId(surfaceId++);
        geometry.AddSurface(bottomSurface, SurfaceGeometry.FromPlane(new PlaneSurface(centers[0], Direction3D.Create(-axisDirection.ToVector()), radialDirection)));
        bindings.AddFaceBinding(new FaceGeometryBinding(bottomFace, bottomSurface));
        var topSurface = new SurfaceGeometryId(surfaceId);
        geometry.AddSurface(topSurface, SurfaceGeometry.FromPlane(new PlaneSurface(centers[^1], axisDirection, radialDirection)));
        bindings.AddFaceBinding(new FaceGeometryBinding(topFace, topSurface));

        var vertexPoints = new Dictionary<VertexId, Point3D>();
        for (var i = 0; i < profile.Count; i++)
        {
            vertexPoints[seamVertices[i]] = rims[i];
        }
        var body = new BrepBody(builder.Model, geometry, bindings, vertexPoints);
        var bindingValidation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        if (!bindingValidation.IsSuccess)
            return new(null, 0, 0, 0, bindingValidation.Diagnostics.Select(d => d.Message).ToArray());

        var vertices = body.Topology.Vertices.Count();
        var edges = body.Topology.Edges.Count();
        var faces = body.Topology.Faces.Count();
        if (vertices != plan.ExpectedVertexCount || edges != plan.ExpectedEdgeCount || faces != plan.ExpectedFaceCount)
            return new(null, vertices, edges, faces, ["revolved-profile-materialization-diverged-from-authoritative-plan"]);
        return new(body, vertices, edges, faces,
        [
            "revolved-profile-authoritative-topology-plan-consumed",
            "revolved-profile-analytic-cylinder-and-cone-surfaces-emitted",
            "revolved-profile-section-splits-preserved",
        ]);
    }

    private static List<KernelDiagnostic> Validate(
        IReadOnlyList<ProfilePoint2D>? profile,
        ExtrudeFrame3D frame,
        RevolveAxis3D axis,
        out Direction3D axisDirection,
        out Direction3D radialDirection)
    {
        var diagnostics = new List<KernelDiagnostic>();
        axisDirection = default;
        radialDirection = default;
        if (profile is null || profile.Count < 2)
            diagnostics.Add(Error("Revolved profile requires at least two points."));
        if (!Direction3D.TryCreate(axis.Direction, out axisDirection))
            diagnostics.Add(Error("Revolved profile axis must be finite and non-zero."));
        if (profile is not null && profile.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y) || p.X <= Tol))
            diagnostics.Add(Error("Revolved profile points require finite positive radii and finite axial coordinates."));
        if (profile is not null && profile.Zip(profile.Skip(1)).Any(pair => pair.Second.Y <= pair.First.Y + Tol))
            diagnostics.Add(Error("Revolved profile axial coordinates must be strictly increasing."));
        if (profile is not null && profile.Zip(profile.Skip(1)).Any(pair => System.Math.Abs(pair.Second.X - pair.First.X) > Tol && System.Math.Abs(pair.Second.Y - pair.First.Y) <= Tol))
            diagnostics.Add(Error("Revolved profile does not admit radial-only segments."));
        if (Direction3D.TryCreate(axis.Direction, out axisDirection))
        {
            var projected = frame.UAxis.ToVector() - axisDirection.ToVector() * frame.UAxis.ToVector().Dot(axisDirection.ToVector());
            if (!Direction3D.TryCreate(projected, out radialDirection))
                diagnostics.Add(Error("Revolved profile frame U axis must not be parallel to its revolution axis."));
        }
        return diagnostics;
    }

    private static FaceId AddFaceWithLoop(TopologyBuilder builder, IReadOnlyList<EdgeUse> uses)
    {
        var loop = builder.AllocateLoopId();
        var coedges = uses.Select(_ => builder.AllocateCoedgeId()).ToArray();
        for (var i = 0; i < uses.Count; i++)
            builder.AddCoedge(new Coedge(coedges[i], uses[i].EdgeId, loop, coedges[(i + 1) % uses.Count], coedges[(i + uses.Count - 1) % uses.Count], uses[i].IsReversed));
        builder.AddLoop(new Loop(loop, coedges));
        return builder.AddFace([loop]);
    }

    private static KernelDiagnostic Error(string message) => new(KernelDiagnosticCode.InvalidArgument, KernelDiagnosticSeverity.Error, message);
    private readonly record struct EdgeUse(EdgeId EdgeId, bool IsReversed)
    {
        public static EdgeUse Forward(EdgeId edge) => new(edge, false);
        public static EdgeUse Reversed(EdgeId edge) => new(edge, true);
    }
}
