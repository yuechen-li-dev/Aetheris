using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>
/// Ordinary CAD rim finish on a circular segment (arc plus chord). The cap is
/// the intersection of the inset circle and inset half-plane. Adjacent finish
/// supports share their actual intersection, never a guessed tangent meridian.
/// Chamfer seams are exact quadratic parabolas; fillet seams are certified
/// cubic representations of the analytic cylinder/torus intersection.
/// </summary>
public static class CircularSegmentEdgeFinishMaterializer
{
    public static ProfileBoundaryChamferPlanResult TryMaterialize(ResolvedProfile2D profile,
        ProfileBoundaryChamferTarget target, ProfileEdgeFinishMixedShellPlan plan)
    {
        ProfileBoundaryChamferPlanResult Fail(string message) => new(false, null, null, target, [message]);
        if (plan.JunctionDecision is not { Strategy: "CircularSegmentMiter", CircularSegment: { } segment })
            return Fail("ProfileEdgeFinishCircularSegmentPlanRequired");
        if (plan.Target != target || target.ChainKind != ProfileBoundaryChamferChainKind.ClosedLoop)
            return Fail("ProfileEdgeFinishCircularSegmentTargetMismatch");
        if (target.ReflexJunctionStyle != ProfileReflexJunctionStyle.ToroidalRolling)
            return Fail("ProfileBoundaryFilletReflexJunctionStyleRequiresReflexPair");
        var loop = profile.Loops.SingleOrDefault(l => l.Name == target.LoopId);
        if (loop is null || loop.Segments.Count != 2 || !target.SegmentIds.SequenceEqual(loop.Segments.Select(s => s.Name)))
            return Fail("ProfileEdgeFinishCircularSegmentProfileMismatch");
        var frame = profile.EffectiveConstructionPlane;
        var arc = (LineArcCircularArc2D)loop.Segments[segment.ArcIndex].Geometry;
        var size = plan.FinishSize;
        var round = plan.FinishKind == ProfileEdgeFinishKind.Fillet;
        var direction = target.Side == ProfileBoundaryChamferSide.Top ? 1d : -1d;
        var capDepth = direction > 0 ? profile.LocalEndDepth ?? 1 : profile.LocalStartDepth ?? -1;
        var baseDepth = direction > 0 ? profile.LocalStartDepth ?? -1 : profile.LocalEndDepth ?? 1;
        var transition = capDepth - direction * size;
        var capOut = Direction3D.Create(frame.AxisZ.ToVector() * direction);
        var reverseFaces = direction * segment.WindingSign < 0;
        var tangent = Direction3D.Create(frame.ToWorldDirection(new(segment.Tangent.X, segment.Tangent.Y, 0)));
        var inward = Direction3D.Create(frame.ToWorldDirection(new(segment.Inward.X, segment.Inward.Y, 0)));
        var r = segment.Radius; var k = segment.ChordDistance;
        var capX = Math.Sqrt((r - size) * (r - size) - (k + size) * (k + size));
        var builder = new TopologyBuilder();
        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var points = new Dictionary<VertexId, Point3D>();
        var descendants = new List<SemanticTopologyDescendant>();
        var nextCurve = 1; var nextSurface = 1;
        var lower = new VertexId[2]; var middle = new VertexId[2]; var upper = new VertexId[2];
        var bottomEdges = new EdgeId[2]; var middleEdges = new EdgeId[2]; var topEdges = new EdgeId[2];
        var verticalEdges = new EdgeId[2]; var seams = new EdgeId[2];
        var capPoints = new (double X, double Y)[2];
        var maxDeviation = 0d;

        VertexId Vertex(Point3D point)
        {
            var vertex = builder.AddVertex(); points.Add(vertex, point); return vertex;
        }
        EdgeId Edge(VertexId a, VertexId b, CurveGeometry curve, ParameterInterval trim, bool sameSense = true)
        {
            var edge = builder.AddEdge(a, b);
            var id = new CurveGeometryId(nextCurve++); geometry.AddCurve(id, curve);
            bindings.AddEdgeBinding(new(edge, id, trim, sameSense)); return edge;
        }
        EdgeId Line(VertexId a, VertexId b) => Edge(a, b,
            CurveGeometry.FromLine(new(points[a], Direction3D.Create(points[b] - points[a]))),
            new(0, (points[b] - points[a]).Length));
        (EdgeId Edge, bool Reverse) Use(EdgeId edge, VertexId start)
            => (edge, builder.Model.Edges.Single(e => e.Id == edge).StartVertexId != start);
        void Face(string name, IReadOnlyList<(EdgeId Edge, bool Reverse)> sourceUses, SurfaceGeometry surface,
            SemanticTopologyRole role, string source)
        {
            // A digon with an arc and a chord is a legitimate face boundary.
            var uses = reverseFaces ? sourceUses.Reverse().Select(u => (u.Edge, Reverse: !u.Reverse)).ToArray() : sourceUses.ToArray();
            var id = builder.AllocateLoopId(); var coedges = uses.Select(_ => builder.AllocateCoedgeId()).ToArray();
            for (var i = 0; i < uses.Length; i++)
                builder.AddCoedge(new(coedges[i], uses[i].Edge, id, coedges[(i + 1) % uses.Length],
                    coedges[(i + uses.Length - 1) % uses.Length], uses[i].Reverse));
            builder.AddLoop(new Loop(id, coedges)); var face = builder.AddFace([id]);
            var surfaceId = new SurfaceGeometryId(nextSurface++); geometry.AddSurface(surfaceId, surface);
            bindings.AddFaceBinding(new(face, surfaceId, true));
            descendants.Add(new($"{target.StableId}:{name}", "Face", role, source, Face: face, ParentStableId: target.StableId));
        }
        EdgeId ProfileEdge(int i, double depth, bool inset, VertexId a, VertexId b)
        {
            if (i == segment.LineIndex) return Line(a, b);
            var start = inset ? capPoints[i] : Start(arc);
            var end = inset ? capPoints[1 - i] : End(arc);
            var angleA = Math.Atan2(start.Y - arc.Center.Y, start.X - arc.Center.X);
            var angleB = Math.Atan2(end.Y - arc.Center.Y, end.X - arc.Center.X);
            if (arc.SweepAngleRadians > 0) { while (angleB <= angleA) angleB += 2 * Math.PI; }
            else { while (angleB >= angleA) angleB -= 2 * Math.PI; }
            return Edge(a, b, CurveGeometry.FromCircle(new(frame.ToWorld(arc.Center, depth), frame.AxisZ,
                inset ? r - size : r, frame.AxisX)), new(Math.Min(angleA, angleB), Math.Max(angleA, angleB)), arc.SweepAngleRadians > 0);
        }

        try
        {
            for (var i = 0; i < 2; i++)
            {
                var sourcePoint = Start(loop.Segments[i].Geometry);
                var x = (sourcePoint.X - segment.Center.X) * segment.Tangent.X + (sourcePoint.Y - segment.Center.Y) * segment.Tangent.Y;
                var sign = Math.Sign(x);
                capPoints[i] = (segment.Center.X + sign * capX * segment.Tangent.X + (k + size) * segment.Inward.X,
                    segment.Center.Y + sign * capX * segment.Tangent.Y + (k + size) * segment.Inward.Y);
                lower[i] = Vertex(frame.ToWorld(sourcePoint, baseDepth));
                middle[i] = Vertex(frame.ToWorld(sourcePoint, transition));
                upper[i] = Vertex(frame.ToWorld(capPoints[i], capDepth));
                verticalEdges[i] = Line(lower[i], middle[i]);
                var miter = CircularSegmentFinishMiter.Create(segment, frame, transition, direction, size, sign, round);
                maxDeviation = Math.Max(maxDeviation, miter.DeviationBound);
                seams[i] = Edge(middle[i], upper[i], miter.Curve, miter.Trim);
                descendants.Add(new($"{target.StableId}:miter:{loop.Segments[i].Name}.Start", "Edge", SemanticTopologyRole.ComposeTransition,
                    $"profile-junction:{loop.Name}.{loop.Segments[i].Name}.Start", Edge: seams[i], ParentStableId: target.StableId,
                    GeometryPreview: round ? "CylinderTorusIntersection:CertifiedCubic" : "PlaneConeIntersection:ExactQuadratic"));
            }
            for (var i = 0; i < 2; i++)
            {
                var next = 1 - i;
                bottomEdges[i] = ProfileEdge(i, baseDepth, false, lower[i], lower[next]);
                middleEdges[i] = ProfileEdge(i, transition, false, middle[i], middle[next]);
                topEdges[i] = ProfileEdge(i, capDepth, true, upper[i], upper[next]);
            }
            var loopSource = $"profile:{profile.Name}.{loop.Name}";
            Face("bottom-cap", [Use(bottomEdges[1], lower[0]), Use(bottomEdges[0], lower[1])],
                SurfaceGeometry.FromPlane(new(frame.ToWorld((0d, 0d), baseDepth), Direction3D.Create(-capOut.ToVector()), frame.AxisX)),
                SemanticTopologyRole.BottomFaceBoundaryLoop, loopSource);
            Face("top-cap", [Use(topEdges[0], upper[0]), Use(topEdges[1], upper[1])],
                SurfaceGeometry.FromPlane(new(frame.ToWorld((0d, 0d), capDepth), capOut, frame.AxisX)),
                SemanticTopologyRole.TopFaceBoundaryLoop, loopSource);
            for (var i = 0; i < 2; i++)
            {
                var next = 1 - i;
                var source = loop.Segments[i].Provenance.StableId;
                var side = i == segment.ArcIndex
                    ? SurfaceGeometry.FromCylinder(new(frame.ToWorld(segment.Center, baseDepth), frame.AxisZ, r, frame.AxisX))
                    : SurfaceGeometry.FromPlane(new(points[lower[i]], Direction3D.Create(-inward.ToVector()), tangent));
                Face($"side:{loop.Segments[i].Name}", [Use(bottomEdges[i], lower[i]), Use(verticalEdges[next], lower[next]),
                    Use(middleEdges[i], middle[next]), Use(verticalEdges[i], middle[i])], side, SemanticTopologyRole.ExtrusionSideFace, source);
                var patch = plan.OrderedPatches[i] switch
                {
                    CylindricalFilletPatch cylinder => SurfaceGeometry.FromCylinder(cylinder.Surface),
                    ToroidalFilletPatch torus => SurfaceGeometry.FromTorus(torus.Surface),
                    PlanarChamferPatch plane => SurfaceGeometry.FromPlane(plane.Surface),
                    ConicalChamferPatch cone => SurfaceGeometry.FromCone(cone.Surface),
                    _ => throw new InvalidOperationException("ProfileEdgeFinishCircularSegmentPatchMismatch")
                };
                Face($"finish:{loop.Segments[i].Name}", [Use(middleEdges[i], middle[i]), Use(seams[next], middle[next]),
                    Use(topEdges[i], upper[next]), Use(seams[i], upper[i])], patch,
                    round ? SemanticTopologyRole.FilletSurface : SemanticTopologyRole.EdgeFinishReplacementFace, source);
            }
            var shell = builder.AddShell(builder.Model.Faces.Select(f => f.Id).ToArray()); builder.AddBody([shell]);
            var body = new BrepBody(builder.Model, geometry, bindings, points);
            var check = BrepBindingValidator.Validate(body, true);
            if (!check.IsSuccess) return Fail("ProfileEdgeFinishCircularSegmentBindingInvalid");
            var correspondence = new SemanticTopologyCorrespondence(target.HostBodyId, descendants,
                ["ResolvedProfile2D", "CircularSegmentMiter", "SharedIntersectionEdges", "JudgmentBeforeEmission"]);
            return new(true, body, correspondence, target,
                ["ProfileEdgeFinishCircularSegmentMiter", $"MiterDeviationBoundMm={maxDeviation:R}"])
                { PlanningEvidence = new(plan.JunctionDecision, maxDeviation) };
        }
        catch (InvalidOperationException e) { return Fail(e.Message); }
    }

    private static (double X, double Y) Start(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => line.Start,
        LineArcCircularArc2D arc => (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians)),
        _ => throw new InvalidOperationException("ProfileEdgeFinishCircularSegmentCurveRequired")
    };
    private static (double X, double Y) End(LineArcCircularArc2D arc) =>
        (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians));
}

internal sealed record CircularSegmentFinishMiter(CurveGeometry Curve, ParameterInterval Trim, double DeviationBound)
{
    internal static CircularSegmentFinishMiter Create(CircularSegmentEdgeFinishPlan segment,
        ConstructionPlane frame, double transition, double direction, double size, double sign, bool round)
    {
        var r = segment.Radius; var k = segment.ChordDistance;
        Point3D Point(double x, double inset, double rise) => frame.ToWorld(
            (segment.Center.X + sign * x * segment.Tangent.X + (k + inset) * segment.Inward.X,
             segment.Center.Y + sign * x * segment.Tangent.Y + (k + inset) * segment.Inward.Y), transition + direction * rise);
        Vector3D Vector(double x, double inset, double rise) => frame.ToWorldDirection(new(
            sign * x * segment.Tangent.X + inset * segment.Inward.X,
            sign * x * segment.Tangent.Y + inset * segment.Inward.Y, direction * rise));
        var a = r * r - k * k - 2 * (r + k) * size;
        var b = 2 * (r + k) * size;
        if (!round)
        {
            // Parameter x is linear; d=(R²-k²-x²)/(2(R+k)) is quadratic.
            // A degree-two Bezier therefore represents the parabola exactly.
            var x0 = Math.Sqrt(r * r - k * k); var x1 = Math.Sqrt(a); var dx = x1 - x0;
            var p0 = Point(x0, 0, 0); var p2 = Point(x1, size, size);
            var derivative = Vector(dx, -x0 * dx / (r + k), -x0 * dx / (r + k));
            var spline = new BSpline3Curve(2, [p0, p0 + derivative * .5, p2], [3, 3], [0, 1], "UNSPECIFIED", false, false, "UNSPECIFIED");
            return new(CurveGeometry.FromBSpline(spline), new(0, 1), 0);
        }
        Point3D Evaluate(double t) => Point(Math.Sqrt(a + b * Math.Cos(t)), size * (1 - Math.Cos(t)), size * Math.Sin(t));
        Vector3D Derivative(double t) => Vector(-b * Math.Sin(t) / (2 * Math.Sqrt(a + b * Math.Cos(t))), size * Math.Sin(t), size * Math.Cos(t));
        // For x=sqrt(g), |g^(n)| <= b and g >= a > 0. Chain rule gives
        // this global fourth-derivative bound. y,z each contribute at most size.
        var fourthX = 15 * Math.Pow(b, 4) / (16 * Math.Pow(a, 3.5))
            + 9 * Math.Pow(b, 3) / (4 * Math.Pow(a, 2.5))
            + 7 * b * b / (4 * Math.Pow(a, 1.5)) + b / (2 * Math.Sqrt(a));
        var fourth = Math.Sqrt(fourthX * fourthX + 2 * size * size);
        var origin = frame.ToWorld(segment.Center, transition);
        var scale = Math.Max(1, Math.Max(Math.Max(Math.Abs(origin.X), Math.Abs(origin.Y)), Math.Max(Math.Abs(origin.Z), r + size)));
        var allowance = 256 * 2.2204460492503131e-16 * scale;
        var budget = ToleranceContext.Default.Linear - allowance;
        if (budget <= 0 || !double.IsFinite(fourth)) throw new InvalidOperationException("ProfileEdgeFinishMiterNumericToleranceExhausted");
        var count = 1;
        double Bound(int n) => fourth * Math.Pow(Math.PI / (2 * n), 4) / 384;
        while (Bound(count) > budget && count < 16384) count *= 2;
        if (Bound(count) > budget) throw new InvalidOperationException("ProfileEdgeFinishMiterRefinementLimit");
        var controls = new List<Point3D>(4 * count); var knots = new List<double>(); var multiplicities = new List<int>();
        knots.Add(0); multiplicities.Add(4);
        for (var i = 0; i < count; i++)
        {
            var start = Math.PI * i / (2 * count); var end = Math.PI * (i + 1) / (2 * count); var h = end - start;
            var p0 = Evaluate(start); var p3 = Evaluate(end);
            controls.Add(p0); controls.Add(p0 + Derivative(start) * (h / 3));
            controls.Add(p3 - Derivative(end) * (h / 3)); controls.Add(p3);
            knots.Add(end); multiplicities.Add(4);
        }
        var curve = new BSpline3Curve(3, controls, multiplicities, knots, "UNSPECIFIED", false, false, "UNSPECIFIED");
        return new(CurveGeometry.FromBSpline(curve), new(0, Math.PI / 2), Bound(count) + allowance);
    }
}
