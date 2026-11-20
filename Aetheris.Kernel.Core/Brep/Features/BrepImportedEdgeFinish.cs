using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Features;

internal enum ImportedEdgeFinishKind { Chamfer, Fillet }

internal sealed record ImportedEdgeFinishWitness(BrepBody Body, IReadOnlyList<FaceId> FinishFaces,
    IReadOnlyList<FaceId> ModifiedFaces, IReadOnlyList<EdgeId> RetiredEdges, string Strategy = "")
{
    internal FaceId FinishFace => FinishFaces.Single();
}

/// <summary>A local convex edge finish on a qualified closed BRep, independent of authored stock history.</summary>
internal static partial class BrepImportedEdgeFinish
{
    private const double AngularTolerance = 1e-8;

    internal static KernelResult<ImportedEdgeFinishWitness> TryApply(BrepBody source, EdgeId selected,
        ImportedEdgeFinishKind kind, double size, double tolerance = .001, double clearance = .1)
        => TryApply(source, selected, null, kind, size, tolerance, clearance);

    internal static KernelResult<ImportedEdgeFinishWitness> TryApplyBoundary(BrepBody source, FaceId face,
        ImportedEdgeFinishKind kind, double size, double tolerance = .001, double clearance = .1)
        => TryApply(source, null, face, kind, size, tolerance, clearance);

    private sealed record PlanOption(Func<ImportedEdgeFinishWitness>? Emit, int ModifiedFaces, string Evidence);
    private static readonly JudgmentModel<PlanOption, int> Policy = new("imported-edge-finish-v1",
        [new("proved-local-replacement", (proposal, _) =>
            new(proposal.Emit is not null, "local-proof", 0, 0, proposal.Evidence))],
        [new("retained-face-share", 1, (proposal, faceCount) => new(
            1d - (double)proposal.ModifiedFaces / faceCount, proposal.ModifiedFaces, "faces", "Prefer the proved plan that rewrites fewer original faces."))]);

    private static KernelResult<ImportedEdgeFinishWitness> TryApply(BrepBody source, EdgeId? edge, FaceId? face,
        ImportedEdgeFinishKind kind, double size, double tolerance, double clearance)
    {
        try
        {
            BrepLocalEditValidation.RequireQualified(source, tolerance);
            if (!Enum.IsDefined(kind) || !double.IsFinite(size) || size <= tolerance
                || !double.IsFinite(clearance) || clearance <= tolerance)
                throw new ArgumentException("Finish size and clearance must be finite and exceed import uncertainty.");

            var options = new[]
            {
                Propose("straight-edge", () =>
                {
                    if (edge is not { } id) throw new ArgumentException("Select an individual edge for the straight-edge plan.");
                    var region = PlanRegion(source, id, kind, size, tolerance, clearance);
                    return (4, () =>
                    {
                        var (body, finish) = Splice(source, region, tolerance);
                        return new(body, [finish], [region.A.Face, region.B.Face, region.Start.Cap, region.End.Cap],
                            [id, region.Start.A.Edge, region.Start.B.Edge, region.End.A.Edge, region.End.B.Edge]);
                    });
                }),
                Propose("circular-rim", () =>
                {
                    var rim = PlanCircularRim(source, edge, face, kind, size, tolerance, clearance);
                    return (2, () => SpliceCircularRim(source, rim, tolerance));
                }),
                Propose("polygon-rim", () =>
                {
                    var rim = PlanPolygonRim(source, face, kind, size, tolerance, clearance);
                    return (rim.Sides.Length + 1, () => SplicePolygonRim(source, rim, tolerance));
                })
            };
            var decision = Policy.Evaluate(source.Topology.Faces.Count(), options);
            if (decision.Winner is not { } winner)
                throw new ArgumentException(string.Join(" | ", decision.Candidates.Select(candidate =>
                    $"{candidate.Id}: {candidate.Rules.Single().Result.Evidence}")));

            var result = winner.Value.Emit!();
            BrepLocalEditValidation.RequireQualified(result.Body, tolerance);
            return KernelResult<ImportedEdgeFinishWitness>.Success(result with { Strategy = winner.Id });
        }
        catch (Exception error) when (IsPlanFailure(error))
        {
            return KernelResult<ImportedEdgeFinishWitness>.Failure([new(KernelDiagnosticCode.ValidationFailed,
                KernelDiagnosticSeverity.Error, error.Message, "Brep.ImportedEdgeFinish.Region")]);
        }
    }

    private static JudgmentOption<PlanOption> Propose(string name, Func<(int Faces, Func<ImportedEdgeFinishWitness> Emit)> plan)
    {
        try
        {
            var planned = plan();
            return new(name, new(planned.Emit, planned.Faces, "Local adjacency, clearance and analytic boundaries proved."));
        }
        catch (Exception error)
        {
            if (!IsPlanFailure(error)) throw;
            return new(name, new(null, 0, error.Message));
        }
    }

    private static bool IsPlanFailure(Exception error) =>
        error is ArgumentException or InvalidOperationException or NotSupportedException or KeyNotFoundException;

    private sealed record Support(FaceId Face, Coedge Use, Vector3D Normal);
    private sealed record Spoke(EdgeId Edge, Point3D Cut);
    private sealed record Endpoint(VertexId Corner, FaceId Cap, Coedge Incoming, Spoke A, Spoke B);
    private sealed record Region(EdgeId Selected, Support A, Support B, Endpoint Start, Endpoint End,
        SurfaceGeometry Surface, Circle3Curve? FirstArc, Circle3Curve? LastArc, double Sweep);

    private static Region PlanRegion(BrepBody source, EdgeId selected, ImportedEdgeFinishKind kind,
        double size, double tolerance, double clearance)
    {
        var topology = source.Topology;
        if (!topology.TryGetEdge(selected, out var edge) || edge is null || Curve(source, selected).Line3 is null)
            throw new ArgumentException("Select a straight edge of the imported solid.");
        var uses = topology.Coedges.ToLookup(use => use.EdgeId);
        var owners = topology.Faces.SelectMany(face => face.LoopIds.Select(loop => (loop, face.Id)))
            .ToDictionary(item => item.loop, item => item.Id);
        var edgeUses = uses[selected].ToArray();
        if (edgeUses.Length != 2 || owners[edgeUses[0].LoopId] == owners[edgeUses[1].LoopId])
            throw new ArgumentException("The selected edge must separate two distinct faces.");

        var a = ResolveSupport(edgeUses[0]);
        var b = ResolveSupport(edgeUses[1]);
        var start = Point(source, edge.StartVertexId);
        var end = Point(source, edge.EndVertexId);
        if ((end - start).Length <= clearance + tolerance)
            throw new ArgumentException("The selected edge is too short for the requested clearance.");
        var axis = Direction3D.Create(end - start).ToVector();
        var cross = a.Normal.Cross(b.Normal);
        var cosine = double.Clamp(a.Normal.Dot(b.Normal), -1, 1);
        if (cross.Length <= AngularTolerance || double.Abs(a.Normal.Dot(axis)) > AngularTolerance
            || double.Abs(b.Normal.Dot(axis)) > AngularTolerance)
            throw new ArgumentException("Two distinct planar supports must meet along the selected edge.");

        // The working section is perpendicular to the edge. Offsets are measured on each support.
        var intoA = Direction3D.Create(a.Normal * cosine - b.Normal).ToVector();
        var intoB = Direction3D.Create(b.Normal * cosine - a.Normal).ToVector();
        var setback = kind == ImportedEdgeFinishKind.Fillet ? size * (1 - cosine) / cross.Length : size;
        if (!double.IsFinite(setback) || setback <= tolerance)
            throw new ArgumentException("The finish setback is below import uncertainty.");
        var first = ResolveEndpoint(edge.StartVertexId, start, -axis);
        var last = ResolveEndpoint(edge.EndVertexId, end, axis);
        if (first.Cap == last.Cap)
            throw new ArgumentException("The selected edge needs two different planar endpoint faces.");

        var section = new List<Point3D> { start, first.A.Cut };
        var outward = Direction3D.Create(a.Normal + b.Normal);
        SurfaceGeometry surface;
        Circle3Curve? firstArc = null;
        Circle3Curve? lastArc = null;
        var sweep = 0d;
        if (kind == ImportedEdgeFinishKind.Fillet)
        {
            var offset = -(a.Normal + b.Normal) * (size / (1 + cosine));
            var center = start + offset;
            var normal = Direction3D.Create(cross);
            var radial = Direction3D.Create(a.Normal);
            firstArc = new(center, normal, size, radial);
            lastArc = new(end + offset, normal, size, radial);
            sweep = double.Atan2(cross.Length, cosine);
            surface = SurfaceGeometry.FromCylinder(new(center, Direction3D.Create(axis), size, radial));
            section.Add(center); // The two tangents and the center bound the entire minor arc.
        }
        else
        {
            surface = SurfaceGeometry.FromPlane(new(first.A.Cut, outward, Direction3D.Create(axis)));
        }
        section.Add(first.B.Cut);
        var vertices = section.Concat(section.Select(point => point + (end - start))).ToArray();
        var separationAxes = section.Select((point, index) =>
            Direction3D.Create(axis.Cross(section[(index + 1) % section.Count] - point)).ToVector()).Append(axis);
        var envelope = separationAxes.Select(direction => (direction, range: Project(vertices, start, direction))).ToArray();
        var ownedEdges = new HashSet<EdgeId> { selected, first.A.Edge, first.B.Edge, last.A.Edge, last.B.Edge };
        var ownedFaces = new HashSet<FaceId> { a.Face, b.Face, first.Cap, last.Cap };

        // Neither an unrelated trim on an edited face nor an unrelated face may enter the swept envelope.
        // The projected hulls are conservative and so may refuse some otherwise legal edits.
        foreach (var bound in topology.Edges.Where(candidate => !ownedEdges.Contains(candidate.Id)))
            if (!envelope.Any(slab => slab.range.Clears(CurveRange(source, bound.Id, start, slab.direction), clearance + tolerance)))
                throw new ArgumentException($"The finish region reaches existing boundary edge {bound.Id.Value}. Reduce the size or select a different edge.");
        foreach (var face in topology.Faces.Where(candidate => !ownedFaces.Contains(candidate.Id)))
            if (!envelope.Any(slab => slab.range.Clears(FaceRange(source, face, start, slab.direction), clearance + tolerance)))
                throw new ArgumentException($"The finish region may reach existing face {face.Id.Value}. Reduce the size or select a different edge.");

        return new(selected, a, b, first, last, surface, firstArc, lastArc, sweep);

        Support ResolveSupport(Coedge use)
        {
            var face = owners[use.LoopId];
            RequireOuter(use);
            return new(face, use, Outward(source, face));
        }

        Endpoint ResolveEndpoint(VertexId vertex, Point3D point, Vector3D outwardAxis)
        {
            var atVertex = topology.Edges.Where(candidate => candidate.StartVertexId == vertex || candidate.EndVertexId == vertex).ToArray();
            if (atVertex.Length != 3)
                throw new ArgumentException("Each endpoint must meet exactly three edges.");
            var (aEdge, aCap) = ResolveSpoke(a, vertex);
            var (bEdge, bCap) = ResolveSpoke(b, vertex);
            if (aEdge.Id == bEdge.Id || aCap.LoopId != bCap.LoopId)
                throw new ArgumentException("The two support trims must meet on a single endpoint loop.");
            var cap = owners[aCap.LoopId];
            if (cap == a.Face || cap == b.Face || (Outward(source, cap) - outwardAxis).Length > AngularTolerance)
                throw new ArgumentException("Each endpoint needs an outward planar cap perpendicular to the selected edge.");
            var (incoming, outgoing) = DirectedEnd(aCap) == vertex ? (aCap, bCap) : (bCap, aCap);
            if (DirectedEnd(incoming) != vertex || incoming.NextCoedgeId != outgoing.Id || DirectedEnd(outgoing) == vertex)
                throw new ArgumentException("The endpoint loop does not contain the two consecutive support trims.");
            return new(vertex, cap, incoming, CutSpoke(aEdge, vertex, point, intoA), CutSpoke(bEdge, vertex, point, intoB));
        }

        (Edge Edge, Coedge CapUse) ResolveSpoke(Support support, VertexId corner)
        {
            var adjacent = topology.GetCoedge(DirectedEnd(support.Use) == corner ? support.Use.NextCoedgeId : support.Use.PrevCoedgeId);
            var spoke = topology.GetEdge(adjacent.EdgeId);
            if (spoke.StartVertexId != corner && spoke.EndVertexId != corner || Curve(source, spoke.Id).Line3 is null)
                throw new ArgumentException("Each endpoint needs straight support trims meeting at the selected vertex.");
            var capUse = uses[spoke.Id].Single(use => use.Id != adjacent.Id);
            RequireOuter(capUse);
            return (spoke, capUse);
        }

        Spoke CutSpoke(Edge spoke, VertexId corner, Point3D point, Vector3D intoFace)
        {
            var cut = point + intoFace * setback;
            var other = Point(source, spoke.StartVertexId == corner ? spoke.EndVertexId : spoke.StartVertexId);
            var ray = other - point;
            var direction = Direction3D.Create(ray).ToVector();
            var offset = cut - point;
            var along = offset.Dot(direction);
            if (along <= tolerance || along >= ray.Length - clearance - tolerance || offset.Cross(direction).Length > tolerance)
                throw new ArgumentException("The endpoint trim does not support this convex finish with the requested clearance.");
            return new(spoke.Id, cut);
        }

        void RequireOuter(Coedge use)
        {
            if (source.Bindings.GetFaceBoundaryRoleBinding(use.LoopId).Role != FaceBoundaryRole.Outer)
                throw new ArgumentException("The affected edges must belong to outer face loops.");
        }

        VertexId DirectedEnd(Coedge use)
        {
            var member = topology.GetEdge(use.EdgeId);
            return use.IsReversed ? member.StartVertexId : member.EndVertexId;
        }
    }

    private readonly record struct Range(double Low, double High)
    {
        internal bool Clears(Range other, double margin) => High + margin < other.Low || other.High + margin < Low;
    }

    private static Range Project(IEnumerable<Point3D> points, Point3D origin, Vector3D axis)
    {
        var values = points.Select(point => (point - origin).Dot(axis)).ToArray();
        return values.Length == 0 ? new(double.NegativeInfinity, double.PositiveInfinity) : new(values.Min(), values.Max());
    }

    private static Range CurveRange(BrepBody body, EdgeId id, Point3D origin, Vector3D axis)
    {
        var curve = Curve(body, id);
        if (body.Bindings.GetEdgeBinding(id).TrimInterval is { } trim)
        {
            if (curve.Line3 is { } line)
                return Project([line.Evaluate(trim.Start), line.Evaluate(trim.End)], origin, axis);
            if (curve.Circle3 is { } circle)
                return PeriodicRange(circle.Center, circle.XAxis.ToVector() * circle.Radius, circle.YAxis.ToVector() * circle.Radius);
            if (curve.Ellipse3 is { } ellipse)
                return PeriodicRange(ellipse.Center, ellipse.XAxis.ToVector() * ellipse.MajorRadius, ellipse.YAxis.ToVector() * ellipse.MinorRadius);

            Range PeriodicRange(Point3D center, Vector3D cosine, Vector3D sine)
            {
                var offset = (center - origin).Dot(axis);
                var x = cosine.Dot(axis);
                var y = sine.Dot(axis);
                var width = double.Hypot(x, y);
                var peak = double.Atan2(y, x);
                var start = x * double.Cos(trim.Start) + y * double.Sin(trim.Start);
                var end = x * double.Cos(trim.End) + y * double.Sin(trim.End);
                return new(offset + (Includes(peak + double.Pi) ? -width : double.Min(start, end)),
                    offset + (Includes(peak) ? width : double.Max(start, end)));

                bool Includes(double angle)
                {
                    var first = trim.Start - AngularTolerance;
                    return angle + double.Tau * double.Ceiling((first - angle) / double.Tau) <= trim.End + AngularTolerance;
                }
            }
        }
        if (curve.BSpline3 is { } spline) return Project(spline.ControlPoints, origin, axis);
        return new(double.NegativeInfinity, double.PositiveInfinity);
    }

    private static Range FaceRange(BrepBody body, Face face, Point3D origin, Vector3D axis)
    {
        var surface = Surface(body, face.Id);
        var edges = face.LoopIds.SelectMany(id => body.Topology.GetLoop(id).CoedgeIds)
            .Select(id => body.Topology.GetCoedge(id).EdgeId).Distinct().ToArray();
        if (surface.Plane is not null) return BoundaryRange(origin, axis);
        if (surface.Cylinder is { } cylinder)
            return AxialRange(cylinder.Origin, cylinder.Axis.ToVector(), _ => cylinder.Radius);
        if (surface.Cone is { } cone)
            return AxialRange(cone.Apex, cone.Axis.ToVector(),
                span => double.Max(double.Abs(span.Low), double.Abs(span.High)) * double.Tan(cone.SemiAngleRadians),
                includeApex: true);
        if (surface.Sphere is { } sphere) return Around(sphere.Center, sphere.Radius);
        if (surface.Torus is { } torus)
            return Around(torus.Center, torus.MajorRadius * torus.Axis.ToVector().Cross(axis).Length + torus.MinorRadius);
        if (surface.BSplineSurfaceWithKnots is { } spline &&
            (spline.Weights is null || spline.Weights.SelectMany(row => row).All(weight => double.IsFinite(weight) && weight > 0)))
            return Project(spline.ControlPoints.SelectMany(row => row), origin, axis);
        return new(double.NegativeInfinity, double.PositiveInfinity);

        Range Around(Point3D center, double width)
        {
            var projection = (center - origin).Dot(axis);
            return new(projection - width, projection + width);
        }

        Range AxialRange(Point3D center, Vector3D along, Func<Range, double> radius, bool includeApex = false)
        {
            var span = BoundaryRange(center, along);
            if (!double.IsFinite(span.Low) || !double.IsFinite(span.High)) return span;
            if (includeApex) span = new(double.Min(span.Low, 0), double.Max(span.High, 0));
            var stations = Project([center + along * span.Low, center + along * span.High], origin, axis);
            var width = radius(span) * along.Cross(axis).Length;
            return new(stations.Low - width, stations.High + width);
        }

        Range BoundaryRange(Point3D reference, Vector3D direction)
        {
            if (edges.Length == 0) return new(double.NegativeInfinity, double.PositiveInfinity);
            var bounds = edges.Select(id => CurveRange(body, id, reference, direction)).ToArray();
            return new(bounds.Min(range => range.Low), bounds.Max(range => range.High));
        }
    }

    private static Point3D Point(BrepBody body, VertexId id) => body.TryGetVertexPoint(id, out var point)
        ? point : throw new ArgumentException($"Missing vertex point {id.Value}.");
    private static CurveGeometry Curve(BrepBody body, EdgeId id) => body.Geometry.GetCurve(body.Bindings.GetEdgeBinding(id).CurveGeometryId);
    private static SurfaceGeometry Surface(BrepBody body, FaceId id) => body.Geometry.GetSurface(body.Bindings.GetFaceBinding(id).SurfaceGeometryId);
    private static Vector3D Outward(BrepBody body, FaceId id)
    {
        var plane = Surface(body, id).Plane ?? throw new ArgumentException($"Face {id.Value} needs a planar support.");
        return plane.Normal.ToVector() * (body.Bindings.GetFaceBinding(id).Orientation.IsAlignedWithSurface ? 1 : -1);
    }
}
