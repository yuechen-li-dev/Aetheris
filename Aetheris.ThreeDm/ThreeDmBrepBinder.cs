using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;
using Rhino.Geometry;
using KernelPoint = Aetheris.Kernel.Core.Math.Point3D;

namespace Aetheris.ThreeDm;

public sealed record ThreeDmBoundBody(
    int ObjectIndex, Guid SourceId, BrepBody? Body, int SourceFaces, int SourceTrims,
    int BoundFaces, int BoundTrims, int SourcePcurves, int RecoveredPcurves,
    double WorstPcurveResidualMillimetres, IReadOnlyList<string> Diagnostics)
{
    public bool IsQualified => Body is not null && Diagnostics.Count == 0;
    public IReadOnlyList<ThreeDmTopologyAdjustment> Adjustments { get; init; } = [];
    public IReadOnlyDictionary<VertexId, double> SourceVertexTolerancesMillimetres { get; init; } =
        new Dictionary<VertexId, double>();
}

public sealed record ThreeDmTopologyAdjustment(
    string Kind, int SourceIndex, double BeforeMillimetres, double AfterMillimetres,
    double AllowedMillimetres, string Explanation);

/// <summary>Import-only topology bridge. Source edge and trim indices remain in diagnostics.</summary>
public static class ThreeDmBrepBinder
{
    public static ThreeDmBoundBody Bind(Brep source, int objectIndex, Guid sourceId,
        double millimetresPerUnit, double recoveryToleranceMillimetres)
    {
        var diagnostics = new List<string>();
        var builder = new TopologyBuilder();
        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var vertexPoints = new Dictionary<VertexId, KernelPoint>();
        var vertices = new Dictionary<int, VertexId>();
        var edges = new Dictionary<int, EdgeId>();
        var faces = new List<FaceId>();
        var boundTrims = 0;
        var sourcePcurves = 0;
        var adjustments = new List<ThreeDmTopologyAdjustment>();
        var endpointCandidates = new Dictionary<int, List<KernelPoint>>();
        var vertexTolerances = new Dictionary<VertexId, double>();
        var singularTransitions = new HashSet<(CoedgeId Current, CoedgeId Next)>();

        try
        {
            foreach (var vertex in source.Vertices)
            {
                var id = builder.AddVertex();
                vertices.Add(vertex.VertexIndex, id);
                vertexPoints.Add(id, ToPoint(vertex.Location, millimetresPerUnit));
                vertexTolerances.Add(id, double.IsFinite(vertex.Tolerance)
                    ? double.Min(1d, double.Max(0d, vertex.Tolerance * millimetresPerUnit)) : 0d);
            }
            foreach (var edge in source.Edges)
            {
                if (edge.StartVertex is null || edge.EndVertex is null)
                    return Failed($"object:{objectIndex}/edge:{edge.EdgeIndex}: endpoint vertex missing.");
                var id = builder.AddEdge(vertices[edge.StartVertex.VertexIndex], vertices[edge.EndVertex.VertexIndex]);
                edges.Add(edge.EdgeIndex, id);
                var curve = BindCurve(edge, sourceId, millimetresPerUnit, recoveryToleranceMillimetres,
                    out var interval, out var reason);
                if (curve is null) return Failed($"object:{objectIndex}/edge:{edge.EdgeIndex}: {reason}");
                var geometryId = new CurveGeometryId(edge.EdgeIndex + 1);
                geometry.AddCurve(geometryId, curve);
                bindings.AddEdgeBinding(new(id, geometryId, interval));
                AddEndpoint(edge.StartVertex.VertexIndex, EvaluateCurve(curve, interval.Start));
                AddEndpoint(edge.EndVertex.VertexIndex, EvaluateCurve(curve, interval.End));
            }
            foreach (var vertex in source.Vertices)
            {
                if (!endpointCandidates.TryGetValue(vertex.VertexIndex, out var candidates) || candidates.Count == 0) continue;
                var id = vertices[vertex.VertexIndex];
                var original = vertexPoints[id];
                var before = candidates.Max(point => (point - original).Length);
                if (before <= 1e-6d) continue;
                var anchor = candidates[0];
                var after = candidates.Max(point => (point - anchor).Length);
                var sourceLimit = double.IsFinite(vertex.Tolerance) && vertex.Tolerance > 0
                    ? vertex.Tolerance * millimetresPerUnit : recoveryToleranceMillimetres;
                // Source vertex tolerance is topology evidence, distinct from spline recovery error.
                // Cap it locally; never change the kernel or authored tolerance.
                var allowed = double.Max(recoveryToleranceMillimetres, double.Min(1d, sourceLimit));
                if (before > allowed || after > 1e-6d) continue;
                vertexPoints[id] = anchor;
                adjustments.Add(new("SourceVertexToCoincidentEdgeEndpoints", vertex.VertexIndex,
                    before, after, allowed, "All incident bound edge endpoints agree; source vertex is within its declared tolerance."));
            }

            foreach (var face in source.Faces)
            {
                var surface = BindSurface(source.Surfaces[face.SurfaceIndex], sourceId, face.FaceIndex, millimetresPerUnit,
                    recoveryToleranceMillimetres, out var reason);
                if (surface is null) return Failed($"object:{objectIndex}/face:{face.FaceIndex}: {reason}");
                var loops = new List<LoopId>();
                var pendingPcurves = new List<(CoedgeId Id, BrepTrim Trim)>();
                foreach (var loop in face.Loops)
                {
                    if (loop.Trims.Count == 0) return Failed($"object:{objectIndex}/face:{face.FaceIndex}/loop:{loop.LoopIndex}: empty loop.");
                    foreach (var trim in loop.Trims.Where(trim => trim.Edge is null))
                    {
                        if (trim.TrimType != BrepTrimType.Singular || trim.StartVertex is null ||
                            trim.StartVertex.VertexIndex != trim.EndVertex?.VertexIndex || trim.TrimCurve is null)
                            return Failed($"object:{objectIndex}/face:{face.FaceIndex}/loop:{loop.LoopIndex}/trim:{trim.TrimIndex}: edgeless trim is not a qualified singular vertex use.");
                        var vertex = trim.StartVertex;
                        var original = ToPoint(vertex.Location, millimetresPerUnit);
                        var domain = trim.Domain;
                        var worst = 0d;
                        for (var sample = 0; sample <= 32; sample++)
                        {
                            var uv = trim.PointAt(domain.T0 + (domain.T1 - domain.T0) * sample / 32d);
                            var lifted = ToPoint(source.Surfaces[face.SurfaceIndex].PointAt(uv.X, uv.Y), millimetresPerUnit);
                            worst = double.Max(worst, (lifted - original).Length);
                        }
                        // Rhino's declared vertex tolerance is source topology evidence, capped locally.
                        var allowed = double.Max(recoveryToleranceMillimetres,
                            double.Min(1d, double.IsFinite(vertex.Tolerance) ? double.Max(0d, vertex.Tolerance * millimetresPerUnit) : 0d));
                        if (!double.IsFinite(worst) || worst > allowed)
                            return Failed($"object:{objectIndex}/face:{face.FaceIndex}/loop:{loop.LoopIndex}/trim:{trim.TrimIndex}: singular lift {worst:G9} mm exceeds source-local bound {allowed:G9} mm.");
                        adjustments.Add(new("RhinoSingularTrimCollapsedToVertex", trim.TrimIndex, worst, 0d,
                            allowed, $"Face {face.FaceIndex}, loop {loop.LoopIndex}, source vertex {vertex.VertexIndex}; no 3D edge exists."));
                    }
                    var edgeTrims = loop.Trims.Where(trim => trim.Edge is not null).ToArray();
                    if (edgeTrims.Length == 0)
                        return Failed($"object:{objectIndex}/face:{face.FaceIndex}/loop:{loop.LoopIndex}: standalone singular vertex loop requires a supported collapsed analytic surface.");
                    for (var i = 0; i < edgeTrims.Length; i++)
                    {
                        var previous = edgeTrims[i];
                        var next = edgeTrims[(i + 1) % edgeTrims.Length];
                        if (previous.EndVertex?.VertexIndex != next.StartVertex?.VertexIndex)
                            return Failed($"object:{objectIndex}/face:{face.FaceIndex}/loop:{loop.LoopIndex}/trim:{previous.TrimIndex}: chain ends at vertex {previous.EndVertex?.VertexIndex}, next trim {next.TrimIndex} starts at {next.StartVertex?.VertexIndex}.");
                    }
                    var loopId = builder.AllocateLoopId();
                    var coedges = edgeTrims.Select(_ => builder.AllocateCoedgeId()).ToArray();
                    var edgePositions = Enumerable.Range(0, loop.Trims.Count).Where(index => loop.Trims[index].Edge is not null).ToArray();
                    for (var i = 0; i < edgePositions.Length; i++)
                        if ((edgePositions[(i + 1) % edgePositions.Length] - edgePositions[i] + loop.Trims.Count) % loop.Trims.Count != 1)
                            singularTransitions.Add((coedges[i], coedges[(i + 1) % coedges.Length]));
                    for (var i = 0; i < coedges.Length; i++)
                    {
                        var trim = edgeTrims[i];
                        builder.AddCoedge(new Coedge(coedges[i], edges[trim.Edge!.EdgeIndex], loopId,
                            coedges[(i + 1) % coedges.Length], coedges[(i + coedges.Length - 1) % coedges.Length],
                            trim.IsReversed()));
                        pendingPcurves.Add((coedges[i], trim));
                        boundTrims++;
                    }
                    boundTrims += loop.Trims.Count - edgeTrims.Length;
                    builder.AddLoop(new Loop(loopId, coedges));
                    loops.Add(loopId);
                }
                var faceId = builder.AddFace(loops);
                faces.Add(faceId);
                var surfaceId = new SurfaceGeometryId(face.FaceIndex + 1);
                geometry.AddSurface(surfaceId, surface);
                bindings.AddFaceBinding(new FaceGeometryBinding(faceId, surfaceId, !face.OrientationIsReversed));
                for (var i = 0; i < loops.Count; i++)
                {
                    var role = face.Loops[i].LoopType == BrepLoopType.Inner ? FaceBoundaryRole.Inner : FaceBoundaryRole.Outer;
                    bindings.AddFaceBoundaryRoleBinding(new(faceId, loops[i], role));
                }
                foreach (var (coedgeId, trim) in pendingPcurves)
                {
                    if (surface.BSplineSurfaceWithKnots is null || trim.TrimCurve is null || trim.Edge is null)
                        continue;
                    var edgeBinding = bindings.GetEdgeBinding(edges[trim.Edge.EdgeIndex]);
                    if (edgeBinding.TrimInterval is not { } interval) continue;
                    var retained = TrySourcePcurve(trim, geometry.GetCurve(edgeBinding.CurveGeometryId),
                        surface.BSplineSurfaceWithKnots, interval, millimetresPerUnit,
                        recoveryToleranceMillimetres, out var residual);
                    if (retained is null) continue;
                    bindings.AddPcurveBinding(new(coedgeId, faceId, surfaceId, retained,
                        Qualification: new(PcurveBindingOrigin.SourceValidated, residual,
                            recoveryToleranceMillimetres, 1025, "RhinoTrimSamples", surface.Kind.ToString())));
                    sourcePcurves++;
                }
            }
            var shell = builder.AddShell(faces);
            builder.AddBody([shell]);
            var body = new BrepBody(builder.Model, geometry, bindings, vertexPoints);
            var graph = BrepBindingValidator.Validate(body);
            if (!graph.IsSuccess)
                return Failed($"object:{objectIndex}: {string.Join("; ", graph.Diagnostics.Select(item => item.Message))}");
            var recovered = BrepPcurveRecovery.Populate(builder.Model, geometry, bindings,
                tolerance: recoveryToleranceMillimetres);
            var pcurveEvidence = BrepPcurveValidator.Validate(body, recoveryToleranceMillimetres, requireEveryCoedge: true,
                sourceTopology: new(vertexTolerances, singularTransitions));
            if (!recovered.IsSuccess || !pcurveEvidence.IsValid)
                return Failed($"object:{objectIndex}: pcurve binding: {string.Join("; ", recovered.Diagnostics.Select(item => item.Message).Concat(pcurveEvidence.Diagnostics))}",
                    body, recovered.Count - sourcePcurves, recovered.MaximumResidual);
            return new(objectIndex, sourceId, body, source.Faces.Count, source.Trims.Count,
                faces.Count, boundTrims, sourcePcurves, recovered.Count - sourcePcurves,
                recovered.MaximumResidual, diagnostics)
                { Adjustments = adjustments, SourceVertexTolerancesMillimetres = vertexTolerances };
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException or KeyNotFoundException)
        {
            return Failed($"object:{objectIndex}: {exception.GetType().Name}: {exception.Message}");
        }

        ThreeDmBoundBody Failed(string reason, BrepBody? body = null, int recoveredPcurves = 0, double worstResidual = 0)
        {
            diagnostics.Add(reason);
            return new(objectIndex, sourceId, body, source.Faces.Count, source.Trims.Count,
                faces.Count, boundTrims, sourcePcurves, recoveredPcurves, worstResidual, diagnostics)
                { Adjustments = adjustments, SourceVertexTolerancesMillimetres = vertexTolerances };
        }
        void AddEndpoint(int vertexIndex, KernelPoint point)
        {
            if (!endpointCandidates.TryGetValue(vertexIndex, out var points)) endpointCandidates[vertexIndex] = points = [];
            points.Add(point);
        }
    }

    private static KernelPoint EvaluateCurve(CurveGeometry curve, double parameter) => curve.Kind switch
    {
        CurveGeometryKind.Line3 => curve.Line3!.Value.Evaluate(parameter),
        CurveGeometryKind.Circle3 => curve.Circle3!.Value.Evaluate(parameter),
        CurveGeometryKind.BSpline3 => curve.BSpline3!.Value.Evaluate(parameter),
        _ => throw new NotSupportedException($"3DM bound curve {curve.Kind} has no endpoint evaluator.")
    };

    private static CurveGeometry? BindCurve(BrepEdge edge, Guid sourceId, double scale, double tolerance,
        out ParameterInterval interval, out string reason)
    {
        var domain = edge.Domain;
        interval = new(domain.T0, domain.T1);
        reason = string.Empty;
        if (edge.EdgeCurve.IsLinear())
        {
            var start = ToPoint(edge.PointAt(domain.T0), scale);
            var end = ToPoint(edge.PointAt(domain.T1), scale);
            if (!Direction3D.TryCreate(end - start, out var direction))
            { reason = "linear edge has coincident endpoints."; return null; }
            interval = new(0, (end - start).Length);
            return CurveGeometry.FromLine(new Line3Curve(start, direction));
        }
        if (edge.EdgeCurve.TryGetCircle(out var circle) || edge.EdgeCurve.TryGetArc(out var arc) && (circle = new Circle(arc.Plane, arc.Radius)).IsValid)
        {
            var start = edge.PointAt(domain.T0);
            var normal = circle.Normal;
            var radial = start - circle.Center;
            radial.Unitize();
            var tangent = Vector3d.CrossProduct(normal, radial);
            if (tangent * edge.TangentAt(domain.T0) < 0) normal = -normal;
            var geometry = new Circle3Curve(ToPoint(circle.Center, scale), ToDirection(normal),
                circle.Radius * scale, ToDirection(radial));
            var sweep = 2d * double.Pi;
            if (edge.StartVertex?.VertexIndex != edge.EndVertex?.VertexIndex)
            {
                var endRadial = edge.PointAt(domain.T1) - circle.Center;
                var midRadial = edge.PointAt((domain.T0 + domain.T1) * 0.5d) - circle.Center;
                var yAxis = Vector3d.CrossProduct(normal, radial);
                static double PositiveAngle(Vector3d x, Vector3d y, Vector3d vector)
                {
                    var angle = double.Atan2(vector * y, vector * x);
                    return angle < 0 ? angle + 2d * double.Pi : angle;
                }
                sweep = PositiveAngle(radial, yAxis, endRadial);
                if (PositiveAngle(radial, yAxis, midRadial) > sweep + 1e-8d) sweep += 2d * double.Pi;
            }
            interval = new(0, sweep);
            return CurveGeometry.FromCircle(geometry);
        }
        var source = edge.EdgeCurve.ToNurbsCurve();
        var polynomial = ThreeDmSplineRecovery.SourceCurve(source, scale);
        if (!source.IsRational) return CurveGeometry.FromBSpline(polynomial);
        var weights = Enumerable.Range(0, source.Points.Count).Select(i => source.Points[i].Weight).ToArray();
        if (!BSplineCurveRationalReduction.TryReduce(polynomial, weights, tolerance,
            out var reduced, out var deviation, out reason)) return null;
        interval = new(reduced.DomainStart, reduced.DomainEnd);
        return CurveGeometry.FromRecoveredBSpline(reduced,
            new SplineRecoveryProvenance("3DM_RATIONAL_EDGE", "AdaptiveCubic", tolerance,
                deviation, true, "Rhino analytic probes did not qualify.",
                SourceEntityIdentity: $"3dm:{sourceId}/edge:{edge.EdgeIndex}"));
    }

    private static SurfaceGeometry? BindSurface(Surface source, Guid sourceId, int faceIndex,
        double scale, double tolerance, out string reason)
    {
        reason = string.Empty;
        if (source.TryGetPlane(out var plane))
            return SurfaceGeometry.FromPlane(new Aetheris.Kernel.Core.Geometry.Surfaces.PlaneSurface(ToPoint(plane.Origin, scale),
                ToDirection(plane.Normal), ToDirection(plane.XAxis)));
        if (source.TryGetCylinder(out var cylinder))
            return SurfaceGeometry.FromCylinder(new CylinderSurface(ToPoint(cylinder.Center, scale),
                ToDirection(cylinder.Axis), cylinder.Radius * scale, ToDirection(cylinder.BasePlane.XAxis)));
        if (source.TryGetCone(out var cone) && double.Abs(cone.Height) > 1e-12d)
            return SurfaceGeometry.FromCone(new ConeSurface(ToPoint(cone.ApexPoint, scale),
                ToDirection(cone.Axis), double.Atan(double.Abs(cone.Radius / cone.Height)),
                ToDirection(cone.Plane.XAxis)));
        if (source.TryGetSphere(out var sphere))
            return SurfaceGeometry.FromSphere(new SphereSurface(ToPoint(sphere.Center, scale),
                ToDirection(sphere.EquatorialPlane.Normal), sphere.Radius * scale,
                ToDirection(sphere.EquatorialPlane.XAxis)));
        if (source.TryGetTorus(out var torus))
            return SurfaceGeometry.FromTorus(new TorusSurface(ToPoint(torus.Plane.Origin, scale),
                ToDirection(torus.Plane.Normal), torus.MajorRadius * scale,
                torus.MinorRadius * scale, ToDirection(torus.Plane.XAxis)));
        var nurbs = source.ToNurbsSurface();
        var spline = ThreeDmSplineRecovery.SourceSurface(nurbs, scale);
        if (!spline.IsRational) return SurfaceGeometry.FromBSplineSurfaceWithKnots(spline);
        if (!BSplineSurfaceRationalReduction.TryReduce(spline, tolerance,
            out var reduced, out var deviation, out reason) || reduced is null) return null;
        return SurfaceGeometry.FromRecoveredBSplineSurfaceWithKnots(reduced,
            new SplineRecoveryProvenance("3DM_RATIONAL_FACE", "AdaptiveGreville", tolerance,
                deviation, true, "Rhino analytic probes did not qualify.",
                SourceEntityIdentity: $"3dm:{sourceId}/face:{faceIndex}"));
    }

    private static PcurveGeometry? TrySourcePcurve(BrepTrim trim, CurveGeometry boundCurve,
        BSplineSurfaceWithKnots recovered, ParameterInterval interval, double scale,
        double tolerance, out double maximum)
    {
        maximum = 0;
        var uv = new SurfaceParameterPoint[1025];
        var domain = trim.Domain;
        for (var i = 0; i < uv.Length; i++)
        {
            var fraction = i / 1024d;
            var edge = trim.Edge!;
            var edgeDomain = edge.Domain;
            var edgeFraction = fraction;
            if (boundCurve.Circle3 is { } circle)
            {
                var angle = interval.Start + (interval.End - interval.Start) * fraction;
                var target = circle.Evaluate(angle);
                var targetSource = new Point3d(target.X / scale, target.Y / scale, target.Z / scale);
                edgeFraction = fraction == 1d ? 1d : ClosestParameterFraction(edge, targetSource);
            }
            var sourceFraction = trim.IsReversed() ? 1d - edgeFraction : edgeFraction;
            var point = trim.PointAt(domain.T0 + (domain.T1 - domain.T0) * sourceFraction);
            uv[i] = new(point.X, point.Y);
            var edgePoint = boundCurve.Circle3 is { } boundCircle
                ? boundCircle.Evaluate(interval.Start + (interval.End - interval.Start) * fraction)
                : ToPoint(edge.PointAt(edgeDomain.T0 + (edgeDomain.T1 - edgeDomain.T0) * fraction), scale);
            var lifted = recovered.Evaluate(point.X, point.Y);
            var residual = (lifted - edgePoint).Length;
            maximum = double.Max(maximum, residual);
            if (!double.IsFinite(maximum) || maximum > tolerance) return null;
        }
        return PcurveGeometry.Polyline(interval, uv);
    }

    private static KernelPoint ToPoint(Point3d source, double scale) =>
        new(source.X * scale, source.Y * scale, source.Z * scale);

    private static double ClosestParameterFraction(BrepEdge edge, Point3d target)
    {
        var domain = edge.Domain;
        var bestIndex = 0;
        var best = double.PositiveInfinity;
        for (var i = 0; i <= 64; i++)
        {
            var parameter = domain.T0 + (domain.T1 - domain.T0) * i / 64d;
            var distance = edge.PointAt(parameter).DistanceToSquared(target);
            if (distance < best) { best = distance; bestIndex = i; }
        }
        var lower = double.Max(0, bestIndex - 1) / 64d;
        var upper = double.Min(64, bestIndex + 1) / 64d;
        for (var i = 0; i < 24; i++)
        {
            var left = lower + (upper - lower) / 3d;
            var right = upper - (upper - lower) / 3d;
            var leftPoint = edge.PointAt(domain.T0 + (domain.T1 - domain.T0) * left);
            var rightPoint = edge.PointAt(domain.T0 + (domain.T1 - domain.T0) * right);
            if (leftPoint.DistanceToSquared(target) < rightPoint.DistanceToSquared(target)) upper = right;
            else lower = left;
        }
        return (lower + upper) / 2d;
    }
    private static Direction3D ToDirection(Vector3d source) =>
        Direction3D.Create(new Vector3D(source.X, source.Y, source.Z));
}
