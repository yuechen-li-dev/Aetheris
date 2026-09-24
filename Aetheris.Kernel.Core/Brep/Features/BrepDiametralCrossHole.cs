using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Features;

/// <summary>
/// Constructive solid-cylinder through-diameter hole. The host seam is relocated
/// to the equidistant clear sector between openings; the cutter seam has two
/// face-local UV uses on one geometric edge. Each opening is two half-branch
/// edges because strict preflight does not admit a non-conic self-loop edge.
/// </summary>
public static class BrepDiametralCrossHole
{
    public static KernelResult<BrepBody> Build(
        CylinderSurface stock, double length, Direction3D cutterAxis,
        double cutterRadius, double axialPosition,
        ToleranceContext? tolerance = null)
    {
        var tol = tolerance ?? ToleranceContext.Default;
        if (!double.IsFinite(length) || length <= 0d || !double.IsFinite(cutterRadius) || cutterRadius <= 0d
            || !double.IsFinite(axialPosition))
            return Failure("Length, cutter radius and axial position must be finite; length and radius must be positive.",
                "Brep.CrossHole.InvalidDimension");
        if (axialPosition - cutterRadius <= tol.Linear || length - axialPosition - cutterRadius <= tol.Linear)
            return Failure("The entire cross-hole must clear both stock ends.", "Brep.CrossHole.EndCollision");

        var z = stock.Axis.ToVector();
        var x = cutterAxis.ToVector();
        if (double.Abs(z.Dot(x)) > tol.Angular)
            return Failure("Cutter axis must be perpendicular to the stock axis.", "Brep.CrossHole.ObliqueAxis");
        var seamDirection = Direction3D.Create(z.Cross(x));
        var y = seamDirection.ToVector();
        var bottom = stock.Origin;
        var top = bottom + z * length;
        var center = bottom + z * axialPosition;
        var host = new CylinderSurface(bottom, stock.Axis, stock.Radius, seamDirection);
        var tool = new CylinderSurface(center, cutterAxis, cutterRadius, seamDirection);
        var full = new ParameterInterval(0d, 2d * double.Pi);
        var first = new ParameterInterval(0d, double.Pi);
        var second = new ParameterInterval(double.Pi, 2d * double.Pi);
        var positive = CylinderCylinderIntersectionCurve.Create(host, tool, center,
            CylinderIntersectionBranch.PositiveCutterAxis, full, tolerance: tol);
        if (!positive.IsSuccess) return KernelResult<BrepBody>.Failure(positive.Diagnostics);
        var negative = CylinderCylinderIntersectionCurve.Create(host, tool, center,
            CylinderIntersectionBranch.NegativeCutterAxis, full, tolerance: tol);
        if (!negative.IsSuccess) return KernelResult<BrepBody>.Failure(negative.Diagnostics);
        var positiveEdge = CylinderIntersectionCurveRealizer.Realize(positive.Value, tol);
        if (!positiveEdge.IsSuccess) return KernelResult<BrepBody>.Failure(positiveEdge.Diagnostics);
        var negativeEdge = CylinderIntersectionCurveRealizer.Realize(negative.Value, tol);
        if (!negativeEdge.IsSuccess) return KernelResult<BrepBody>.Failure(negativeEdge.Diagnostics);
        var positiveUv = CylinderIntersectionPcurveRealizer.Realize(positiveEdge.Value, tol);
        if (!positiveUv.IsSuccess) return KernelResult<BrepBody>.Failure(positiveUv.Diagnostics);
        var negativeUv = CylinderIntersectionPcurveRealizer.Realize(negativeEdge.Value, tol);
        if (!negativeUv.IsSuccess) return KernelResult<BrepBody>.Failure(negativeUv.Diagnostics);

        var builder = new TopologyBuilder();
        var points = new Dictionary<VertexId, Point3D>();
        VertexId Vertex(Point3D point)
        {
            var id = builder.AddVertex();
            points.Add(id, point);
            return id;
        }
        var bottomSeamVertex = Vertex(bottom + y * stock.Radius);
        var topSeamVertex = Vertex(top + y * stock.Radius);
        var pos0 = Vertex(positive.Value.Evaluate(0d));
        var posPi = Vertex(positive.Value.Evaluate(double.Pi));
        var neg0 = Vertex(negative.Value.Evaluate(0d));
        var negPi = Vertex(negative.Value.Evaluate(double.Pi));

        var hostSeam = builder.AddEdge(bottomSeamVertex, topSeamVertex);
        var bottomRim = builder.AddEdge(bottomSeamVertex, bottomSeamVertex);
        var topRim = builder.AddEdge(topSeamVertex, topSeamVertex);
        var posFirst = builder.AddEdge(pos0, posPi);
        var posSecond = builder.AddEdge(posPi, pos0);
        var negFirst = builder.AddEdge(neg0, negPi);
        var negSecond = builder.AddEdge(negPi, neg0);
        var toolSeam = builder.AddEdge(pos0, neg0);

        var hostOuter = AddLoop(builder, [new(hostSeam), new(topRim), new(hostSeam, true), new(bottomRim, true)]);
        var hostPositiveOpening = AddLoop(builder, [new(posSecond, true), new(posFirst, true)]);
        var hostNegativeOpening = AddLoop(builder, [new(negFirst), new(negSecond)]);
        var hostFace = builder.AddFace([hostOuter.Id, hostPositiveOpening.Id, hostNegativeOpening.Id]);
        var bottomCap = AddLoop(builder, [new(bottomRim)]);
        var bottomFace = builder.AddFace([bottomCap.Id]);
        var topCap = AddLoop(builder, [new(topRim, true)]);
        var topFace = builder.AddFace([topCap.Id]);
        var wall = AddLoop(builder, [new(posFirst), new(posSecond), new(toolSeam),
            new(negSecond, true), new(negFirst, true), new(toolSeam, true)]);
        var wallFace = builder.AddFace([wall.Id]);
        var shell = builder.AddShell([hostFace, bottomFace, topFace, wallFace]);
        builder.AddBody([shell]);

        var geometry = new BrepGeometryStore();
        geometry.AddCurve(new CurveGeometryId(1), CurveGeometry.FromLine(new Line3Curve(points[bottomSeamVertex], stock.Axis)));
        geometry.AddCurve(new CurveGeometryId(2), CurveGeometry.FromCircle(new Circle3Curve(bottom, stock.Axis, stock.Radius, seamDirection)));
        geometry.AddCurve(new CurveGeometryId(3), CurveGeometry.FromCircle(new Circle3Curve(top, stock.Axis, stock.Radius, seamDirection)));
        geometry.AddCurve(new CurveGeometryId(4), CurveGeometry.FromCertifiedIntersection(positiveEdge.Value));
        geometry.AddCurve(new CurveGeometryId(5), CurveGeometry.FromCertifiedIntersection(negativeEdge.Value));
        var seamLength = (points[neg0] - points[pos0]).Length;
        geometry.AddCurve(new CurveGeometryId(6), CurveGeometry.FromLine(new Line3Curve(points[pos0], Direction3D.Create(points[neg0] - points[pos0]))));
        geometry.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromCylinder(host));
        geometry.AddSurface(new SurfaceGeometryId(2), SurfaceGeometry.FromPlane(new PlaneSurface(bottom, Direction3D.Create(-z), seamDirection)));
        geometry.AddSurface(new SurfaceGeometryId(3), SurfaceGeometry.FromPlane(new PlaneSurface(top, stock.Axis, seamDirection)));
        geometry.AddSurface(new SurfaceGeometryId(4), SurfaceGeometry.FromCylinder(tool));

        var bindings = new BrepBindingModel();
        bindings.AddEdgeBinding(new EdgeGeometryBinding(hostSeam, new CurveGeometryId(1), new ParameterInterval(0d, length)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(bottomRim, new CurveGeometryId(2), full));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(topRim, new CurveGeometryId(3), full));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(posFirst, new CurveGeometryId(4), first));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(posSecond, new CurveGeometryId(4), second));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(negFirst, new CurveGeometryId(5), first));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(negSecond, new CurveGeometryId(5), second));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(toolSeam, new CurveGeometryId(6), new ParameterInterval(0d, seamLength)));
        bindings.AddFaceBinding(new FaceGeometryBinding(hostFace, new SurfaceGeometryId(1)));
        bindings.AddFaceBinding(new FaceGeometryBinding(bottomFace, new SurfaceGeometryId(2)));
        bindings.AddFaceBinding(new FaceGeometryBinding(topFace, new SurfaceGeometryId(3)));
        bindings.AddFaceBinding(new FaceGeometryBinding(wallFace, new SurfaceGeometryId(4), IsAlignedWithSurface: false));
        bindings.AddFaceBoundaryRoleBinding(new FaceBoundaryRoleBinding(hostFace, hostOuter.Id, FaceBoundaryRole.Outer));
        bindings.AddFaceBoundaryRoleBinding(new FaceBoundaryRoleBinding(hostFace, hostPositiveOpening.Id, FaceBoundaryRole.Inner));
        bindings.AddFaceBoundaryRoleBinding(new FaceBoundaryRoleBinding(hostFace, hostNegativeOpening.Id, FaceBoundaryRole.Inner));
        bindings.AddFaceBoundaryRoleBinding(new FaceBoundaryRoleBinding(bottomFace, bottomCap.Id, FaceBoundaryRole.Outer));
        bindings.AddFaceBoundaryRoleBinding(new FaceBoundaryRoleBinding(topFace, topCap.Id, FaceBoundaryRole.Outer));
        bindings.AddFaceBoundaryRoleBinding(new FaceBoundaryRoleBinding(wallFace, wall.Id, FaceBoundaryRole.Outer));

        void Bind(CoedgeId id, FaceId face, SurfaceGeometryId surface, PcurveGeometry pcurve)
            => bindings.AddPcurveBinding(new CoedgePcurveBinding(id, face, surface, pcurve));
        Bind(hostOuter.Coedges[0], hostFace, new SurfaceGeometryId(1), PcurveGeometry.Line(new ParameterInterval(0d, length), new(0d, 0d), new(0d, length)));
        Bind(hostOuter.Coedges[1], hostFace, new SurfaceGeometryId(1), PcurveGeometry.Line(full, new(0d, length), new(2d * double.Pi, length)));
        Bind(hostOuter.Coedges[2], hostFace, new SurfaceGeometryId(1), PcurveGeometry.Line(new ParameterInterval(0d, length), new(2d * double.Pi, 0d), new(2d * double.Pi, length)));
        Bind(hostOuter.Coedges[3], hostFace, new SurfaceGeometryId(1), PcurveGeometry.Line(full, new(0d, 0d), new(2d * double.Pi, 0d)));
        Bind(hostPositiveOpening.Coedges[0], hostFace, new SurfaceGeometryId(1), PcurveGeometry.Polynomial(second, positiveUv.Value.Host));
        Bind(hostPositiveOpening.Coedges[1], hostFace, new SurfaceGeometryId(1), PcurveGeometry.Polynomial(first, positiveUv.Value.Host));
        Bind(hostNegativeOpening.Coedges[0], hostFace, new SurfaceGeometryId(1), PcurveGeometry.Polynomial(first, negativeUv.Value.Host));
        Bind(hostNegativeOpening.Coedges[1], hostFace, new SurfaceGeometryId(1), PcurveGeometry.Polynomial(second, negativeUv.Value.Host));
        Bind(bottomCap.Coedges[0], bottomFace, new SurfaceGeometryId(2), PcurveGeometry.Circle(full, new(0d, 0d), stock.Radius, -stock.Radius));
        Bind(topCap.Coedges[0], topFace, new SurfaceGeometryId(3), PcurveGeometry.Circle(full, new(0d, 0d), stock.Radius, stock.Radius));
        Bind(wall.Coedges[0], wallFace, new SurfaceGeometryId(4), PcurveGeometry.Polynomial(first, positiveUv.Value.Tool));
        Bind(wall.Coedges[1], wallFace, new SurfaceGeometryId(4), PcurveGeometry.Polynomial(second, positiveUv.Value.Tool));
        Bind(wall.Coedges[2], wallFace, new SurfaceGeometryId(4), PcurveGeometry.Line(new ParameterInterval(0d, seamLength), new(2d * double.Pi, (points[pos0] - center).Dot(x)), new(2d * double.Pi, (points[neg0] - center).Dot(x))));
        Bind(wall.Coedges[3], wallFace, new SurfaceGeometryId(4), PcurveGeometry.Polynomial(second, negativeUv.Value.Tool));
        Bind(wall.Coedges[4], wallFace, new SurfaceGeometryId(4), PcurveGeometry.Polynomial(first, negativeUv.Value.Tool));
        Bind(wall.Coedges[5], wallFace, new SurfaceGeometryId(4), PcurveGeometry.Line(new ParameterInterval(0d, seamLength), new(0d, (points[pos0] - center).Dot(x)), new(0d, (points[neg0] - center).Dot(x))));

        var body = new BrepBody(builder.Model, geometry, bindings, points);
        var bindingValidation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        if (!bindingValidation.IsSuccess) return KernelResult<BrepBody>.Failure(bindingValidation.Diagnostics);
        var pcurveValidation = BrepPcurveValidator.Validate(body, tol.Linear, requireEveryCoedge: true, samples: 129);
        if (!pcurveValidation.IsValid)
            return Failure(string.Join("; ", pcurveValidation.Diagnostics), "Brep.CrossHole.PcurveValidation");
        var preflight = BrepExportPreflight.Validate(body);
        if (!preflight.IsValid)
            return Failure(string.Join("; ", preflight.Diagnostics.Where(d => d.Severity == BrepExportPreflightSeverity.Error)
                .Select(d => $"{d.Code}: {d.Message}")), "Brep.CrossHole.Preflight");
        return KernelResult<BrepBody>.Success(body);
    }

    private static (LoopId Id, CoedgeId[] Coedges) AddLoop(TopologyBuilder builder, IReadOnlyList<Use> uses)
    {
        var loop = builder.AllocateLoopId();
        var coedges = uses.Select(_ => builder.AllocateCoedgeId()).ToArray();
        for (var i = 0; i < uses.Count; i++)
            builder.AddCoedge(new Coedge(coedges[i], uses[i].Edge, loop,
                coedges[(i + 1) % uses.Count], coedges[(i + uses.Count - 1) % uses.Count], uses[i].Reversed));
        builder.AddLoop(new Loop(loop, coedges));
        return (loop, coedges);
    }

    private static KernelResult<BrepBody> Failure(string message, string source)
        => KernelResult<BrepBody>.Failure([
            new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message, source)]);

    private readonly record struct Use(EdgeId Edge, bool Reversed = false);
}
