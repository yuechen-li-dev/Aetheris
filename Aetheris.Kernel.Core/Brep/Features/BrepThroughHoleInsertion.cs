using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Brep.Recipes;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Features;

internal sealed record ThroughHoleInsertionRegion(FaceId Entry, FaceId Exit, ThroughHoleEditBounds Bounds, double ClearanceRadius);
internal sealed record ThroughHoleInsertionWitness(BrepBody Body, ThroughHoleInsertionRegion Region, FaceId Wall);

public static partial class BrepThroughHoleRevision
{
    // Experimental native witness. The selected supports and conservative stock
    // exclusion establish the region before any successor topology is created.
    internal static KernelResult<ThroughHoleInsertionWitness> TryInsertZ(
        BrepBody source, FaceId entry, FaceId exit, Point3D center, double diameter,
        double tolerance = .001, double clearance = .1)
    {
        try
        {
            RequireQualified(source, tolerance);
            if (!Finite(center) || !double.IsFinite(diameter) || diameter <= 2 * tolerance
                || !double.IsFinite(clearance) || clearance <= tolerance)
                throw new ArgumentException("The bore and clearance must exceed import uncertainty.");

            var entryPlane = Surface(source, entry).Plane;
            var exitPlane = Surface(source, exit).Plane;
            if (entryPlane is not { } a || exitPlane is not { } b
                || !IsZNormal(a.Normal.ToVector()) || !IsZNormal(b.Normal.ToVector())
                || double.Abs(center.Z - a.Origin.Z) > Tol)
                throw new ArgumentException("Select two Z-normal planar supports and place the center on the entry plane.");

            var low = double.Min(a.Origin.Z, b.Origin.Z);
            var high = double.Max(a.Origin.Z, b.Origin.Z);
            var lower = a.Origin.Z == low ? entry : exit;
            var upper = lower == entry ? exit : entry;
            if (high - low <= 2 * tolerance || OutwardZ(lower) >= 0 || OutwardZ(upper) <= 0)
                throw new ArgumentException("The supports must bound positive stock thickness and face outward.");

            var radius = diameter / 2;
            var span = radius + clearance;
            var box = new Box(new(center.X - span, center.Y - span, low), new(center.X + span, center.Y + span, high));
            var numeric = new ToleranceContext(tolerance, 1e-9);
            foreach (var (faceId, z) in new[] { (lower, low), (upper, high) })
            {
                var point = new Point3D(center.X, center.Y, z);
                if (!AnalyticPlanarFaceDomain.TryContainsInPlanarFace(source, faceId, point, numeric, default, out var inside) || !inside)
                    throw new ArgumentException("The region center must lie in the material domain of both support faces.");
                if (Edges(source, source.Topology.GetFace(faceId)).Any(edge => BoundaryMayReachRegion(source, edge, point, box, span, tolerance)))
                    throw new ArgumentException("The region reaches an existing support boundary or opening.");
            }
            foreach (var face in source.Topology.Faces.Where(face => face.Id != entry && face.Id != exit))
                if (box.Overlaps(FaceBounds(source, face), tolerance) &&
                    !CylinderClearsRegion(source, face.Id, center, box, span, tolerance))
                    throw new ArgumentException($"The region may intersect unrelated face {face.Id.Value}.");

            // Borrow only the existing recipe's analytic bore, using a temporary
            // coupon whose support stations match the selected stock.
            var midpoint = new Vector3D(center.X, center.Y, low + (high - low) / 2);
            var request = ThroughHoleRecipeRequestBuilder.FromBoxAndZCylinder(
                4 * span, 4 * span, high - low, midpoint, radius, (high - low) + 2 * clearance, midpoint);
            if (!request.IsSuccess) return KernelResult<ThroughHoleInsertionWitness>.Failure(request.Diagnostics);
            var coupon = ThroughHoleConstructionRecipe.Execute(request.Value);
            if (!coupon.IsSuccess) return KernelResult<ThroughHoleInsertionWitness>.Failure(coupon.Diagnostics);
            var (body, wall) = GraftZBore(source, coupon.Value, lower, upper, low, high, tolerance);
            RequireQualified(body, tolerance);
            return KernelResult<ThroughHoleInsertionWitness>.Success(new(body, new(entry, exit, new(box.Min, box.Max), span), wall));
        }
        catch (Exception error) when (IsRecognitionFailure(error))
        {
            return KernelResult<ThroughHoleInsertionWitness>.Failure([new(KernelDiagnosticCode.ValidationFailed,
                KernelDiagnosticSeverity.Error, error.Message, "Brep.ThroughHoleInsertion.Region")]);
        }

        double OutwardZ(FaceId id) => Surface(source, id).Plane!.Value.Normal.Z
            * (source.Bindings.GetFaceBinding(id).Orientation.IsAlignedWithSurface ? 1 : -1);
        static bool IsZNormal(Vector3D normal) => normal.Cross(AxisVector(2)).Length <= Tol;
    }

    private static bool BoundaryMayReachRegion(BrepBody body, EdgeId edge, Point3D center, Box envelope,
        double clearanceRadius, double tolerance)
    {
        if (!envelope.Overlaps(Bounds(CurveHull(body, edge).ToArray()), tolerance)) return false;

        var curve = Curve(body, edge);
        var trim = body.Bindings.GetEdgeBinding(edge).TrimInterval!.Value;
        var limit = clearanceRadius + tolerance;
        if (curve.Line3 is { } line)
        {
            var start = line.Evaluate(trim.Start);
            var direction = line.Evaluate(trim.End) - start;
            var offset = center - start;
            var lengthSquared = direction.X * direction.X + direction.Y * direction.Y;
            var fraction = lengthSquared == 0 ? 0 : double.Clamp(
                (offset.X * direction.X + offset.Y * direction.Y) / lengthSquared, 0, 1);
            return !(RadialDistance(center, start + direction * fraction) > limit);
        }

        if (curve.Circle3 is { } circle)
        {
            var start = double.Min(trim.Start, trim.End);
            var end = double.Max(trim.Start, trim.End);
            var offset = center - circle.Center;
            var u = offset.Dot(circle.XAxis.ToVector());
            var v = offset.Dot(circle.YAxis.ToVector());
            var angle = double.Atan2(v, u);
            // Include the closest radial point if it lies on this periodic trim.
            // Otherwise the closest point is an endpoint. Extend the angular
            // interval by import uncertainty before deciding to use endpoints.
            var slack = tolerance / circle.Radius;
            var first = start - slack;
            var radialParameter = angle + double.Tau * double.Ceiling((first - angle) / double.Tau);
            var distance = radialParameter <= end + slack
                ? double.Hypot(double.Hypot(u, v) - circle.Radius, offset.Dot(circle.Normal.ToVector()))
                : double.Min((center - circle.Evaluate(start)).Length, (center - circle.Evaluate(end)).Length);
            // Bound how much of the 3D distance can be vertical. This also
            // covers imported circles whose normals carry rounding-scale tilt.
            var verticalReach = double.Abs(offset.Z) + circle.Radius * double.Hypot(circle.XAxis.Z, circle.YAxis.Z);
            return !(distance > double.Hypot(limit, verticalReach));
        }

        return true;
    }

    private static bool CylinderClearsRegion(BrepBody body, FaceId face, Point3D center, Box envelope,
        double clearanceRadius, double tolerance)
    {
        if (Surface(body, face).Cylinder is not { } cylinder) return false;

        var axis = cylinder.Axis;
        var radialGap = double.Abs((center - cylinder.Origin).Cross(axis.ToVector()).Length - cylinder.Radius);
        // Varying Z moves the proposed axis by at most this much in the
        // cylinder's radial plane. The entire surface is outside the region
        // if that drift and the horizontal clearance cannot close the gap.
        var axialReach = double.Max(double.Abs(envelope.Min.Z - center.Z), double.Abs(envelope.Max.Z - center.Z));
        var radialDrift = (axialReach + tolerance) * double.Hypot(axis.X, axis.Y);
        return radialGap > clearanceRadius + tolerance + radialDrift;
    }

    private static double RadialDistance(Point3D a, Point3D b) => double.Hypot(a.X - b.X, a.Y - b.Y);

    private static (BrepBody Body, FaceId Wall) GraftZBore(BrepBody stock, BrepBody coupon,
        FaceId lower, FaceId upper, double low, double high, double tolerance)
    {
        var wall = coupon.Topology.Faces.Single(face => Surface(coupon, face.Id).Cylinder is not null);
        var wallEdges = Edges(coupon, wall).ToHashSet();
        var wallId = new FaceId(stock.Topology.Faces.Max(item => item.Id.Value) + 1);
        var loopOwners = wall.LoopIds.ToDictionary(id => id, _ => wallId);
        var supportLoops = new Dictionary<FaceId, LoopId>();
        foreach (var (target, station) in new[] { (lower, low), (upper, high) })
        {
            var loop = coupon.Topology.Faces.Where(face => Surface(coupon, face.Id).Plane is { } plane
                    && double.Abs(plane.Normal.Z) > .99 && double.Abs(plane.Origin.Z - station) <= Tol)
                .SelectMany(face => face.LoopIds).Select(coupon.Topology.GetLoop)
                .Single(loop => loop.CoedgeIds.All(id => wallEdges.Contains(coupon.Topology.GetCoedge(id).EdgeId)));
            supportLoops.Add(target, loop.Id);
            loopOwners.Add(loop.Id, target);
        }

        var topology = new TopologyModel();
        foreach (var vertex in stock.Topology.Vertices) topology.AddVertex(vertex);
        foreach (var edge in stock.Topology.Edges) topology.AddEdge(edge);
        foreach (var use in stock.Topology.Coedges) topology.AddCoedge(use);
        foreach (var loop in stock.Topology.Loops) topology.AddLoop(loop);
        var vertexOffset = stock.Topology.Vertices.Max(item => item.Id.Value);
        var edgeOffset = stock.Topology.Edges.Max(item => item.Id.Value);
        var useOffset = stock.Topology.Coedges.Max(item => item.Id.Value);
        var loopOffset = stock.Topology.Loops.Max(item => item.Id.Value);
        VertexId VertexIdFor(VertexId id) => new(vertexOffset + id.Value);
        EdgeId EdgeIdFor(EdgeId id) => new(edgeOffset + id.Value);
        CoedgeId UseIdFor(CoedgeId id) => new(useOffset + id.Value);
        LoopId LoopIdFor(LoopId id) => new(loopOffset + id.Value);

        var vertices = Points(stock);
        var donorPoints = Points(coupon);
        var geometry = stock.Geometry.Clone();
        var bindings = stock.Bindings.Clone();
        var curveOffset = stock.Geometry.Curves.Max(item => item.Key.Value);
        var donorVertices = wallEdges.Select(coupon.Topology.GetEdge)
            .SelectMany(edge => new[] { edge.StartVertexId, edge.EndVertexId }).Distinct();
        foreach (var id in donorVertices)
        {
            topology.AddVertex(new(VertexIdFor(id)));
            vertices.Add(VertexIdFor(id), donorPoints[id]);
        }
        foreach (var id in wallEdges)
        {
            var edge = coupon.Topology.GetEdge(id);
            topology.AddEdge(new(EdgeIdFor(id), VertexIdFor(edge.StartVertexId), VertexIdFor(edge.EndVertexId)));
            var binding = coupon.Bindings.GetEdgeBinding(id);
            var curveId = new CurveGeometryId(curveOffset + binding.CurveGeometryId.Value);
            if (!geometry.TryGetCurve(curveId, out _)) geometry.AddCurve(curveId, coupon.Geometry.GetCurve(binding.CurveGeometryId));
            bindings.AddEdgeBinding(binding with { EdgeId = EdgeIdFor(id), CurveGeometryId = curveId, SourceStepEntityId = null });
        }
        foreach (var (loopId, owner) in loopOwners)
        {
            var loop = coupon.Topology.GetLoop(loopId);
            foreach (var use in loop.CoedgeIds.Select(coupon.Topology.GetCoedge))
                topology.AddCoedge(new(UseIdFor(use.Id), EdgeIdFor(use.EdgeId), LoopIdFor(use.LoopId),
                    UseIdFor(use.NextCoedgeId), UseIdFor(use.PrevCoedgeId), use.IsReversed));
            topology.AddLoop(new(LoopIdFor(loopId), loop.CoedgeIds.Select(UseIdFor).ToArray()));
            bindings.AddFaceBoundaryRoleBinding(new(owner, LoopIdFor(loopId),
                owner == wallId ? FaceBoundaryRole.Outer : FaceBoundaryRole.Inner));
        }
        foreach (var face in stock.Topology.Faces)
            topology.AddFace(supportLoops.TryGetValue(face.Id, out var loop)
                ? face with { LoopIds = face.LoopIds.Append(LoopIdFor(loop)).ToArray() } : face);
        topology.AddFace(new(wallId, wall.LoopIds.Select(LoopIdFor).ToArray()));
        foreach (var shell in stock.Topology.Shells)
            topology.AddShell(shell with { FaceIds = shell.FaceIds.Append(wallId).ToArray() });
        foreach (var body in stock.Topology.Bodies) topology.AddBody(body);

        var surfaceId = new SurfaceGeometryId(stock.Geometry.Surfaces.Max(item => item.Key.Value) + 1);
        geometry.AddSurface(surfaceId, Surface(coupon, wall.Id));
        bindings.AddFaceBinding(new(wallId, surfaceId, ResolvedFaceOrientation.Opposed));
        // Only build trims for the new uses. Retained trims can have their own
        // parameter sense and qualification, which the complete-body gate checks.
        foreach (var (loopId, owner) in loopOwners)
        {
            var supportId = bindings.GetFaceBinding(owner).SurfaceGeometryId;
            var support = geometry.GetSurface(supportId);
            foreach (var use in topology.GetLoop(LoopIdFor(loopId)).CoedgeIds.Select(topology.GetCoedge))
            {
                var edge = bindings.GetEdgeBinding(use.EdgeId);
                var curve = geometry.GetCurve(edge.CurveGeometryId);
                if (edge.TrimInterval is not { } interval
                    || BrepPcurveRecovery.TryBuildAtEdgeParameter(support, curve, interval, tolerance) is not { } trim)
                    throw new ArgumentException($"The new trim on support {owner.Value} did not qualify.");
                bindings.AddPcurveBinding(new(use.Id, owner, supportId, trim));
            }
        }
        return (new(topology, geometry, bindings, vertices, shellRepresentation: stock.ShellRepresentation), wallId);
    }
}
