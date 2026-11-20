using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Features;

internal static partial class BrepImportedEdgeFinish
{
    private sealed record CircularRim(EdgeId Selected, EdgeId WallSeam, VertexId Corner,
        FaceId Cap, FaceId Wall, Coedge CapUse, Coedge WallUse, Circle3Curve CapCircle,
        Circle3Curve ShoulderCircle, CurveGeometry Transition, ParameterInterval TransitionTrim, SurfaceGeometry Surface);

    private static CircularRim PlanCircularRim(BrepBody source, EdgeId? selected, FaceId? requestedCap,
        ImportedEdgeFinishKind kind, double size, double tolerance, double clearance)
    {
        var topology = source.Topology;
        if (requestedCap is { } face)
        {
            var outer = topology.GetFace(face).LoopIds
                .Single(loop => source.Bindings.GetFaceBoundaryRoleBinding(loop).Role == FaceBoundaryRole.Outer);
            var boundary = topology.GetLoop(outer).CoedgeIds;
            if (boundary.Count != 1) throw new ArgumentException("Boundary needs one complete circular edge. Polygon and split rims require a different join plan.");
            selected = topology.GetCoedge(boundary[0]).EdgeId;
        }

        if (selected is not { } id || !topology.TryGetEdge(id, out var edge) || edge is null
            || edge.StartVertexId != edge.EndVertexId || Curve(source, id).Circle3 is not { } circle)
            throw new ArgumentException("Select one complete circular rim.");
        var binding = source.Bindings.GetEdgeBinding(id);
        if (binding.TrimInterval is not { } trim || double.Abs(trim.End - trim.Start - double.Tau) > AngularTolerance)
            throw new ArgumentException("The circle trim must cover one full turn.");

        var uses = topology.Coedges.Where(use => use.EdgeId == id).ToArray();
        var owners = topology.Faces.SelectMany(item => item.LoopIds.Select(loop => (loop, item.Id))).ToDictionary(item => item.loop, item => item.Id);
        if (uses.Length != 2) throw new ArgumentException("The circle must have two face uses.");
        var capUse = uses.SingleOrDefault(use => Surface(source, owners[use.LoopId]).Plane is not null)
            ?? throw new ArgumentException("The circle needs a planar cap.");
        var wallUse = uses.Single(use => use.Id != capUse.Id);
        var cap = owners[capUse.LoopId];
        var wall = owners[wallUse.LoopId];
        if (requestedCap is { } required && required != cap
            || source.Bindings.GetFaceBoundaryRoleBinding(capUse.LoopId).Role != FaceBoundaryRole.Outer
            || topology.GetLoop(capUse.LoopId).CoedgeIds.Count != 1)
            throw new ArgumentException("Select the outer rim of its planar cap.");
        if (Surface(source, wall).Cylinder is not { } cylinder || !source.Bindings.GetFaceBinding(wall).Orientation.IsAlignedWithSurface)
            throw new ArgumentException("The cap must meet an exterior cylindrical wall.");
        var axis = Outward(source, cap);
        var normal = Direction3D.Create(axis);
        if (circle.Normal.ToVector().Cross(axis).Length > AngularTolerance
            || cylinder.Axis.ToVector().Cross(axis).Length > AngularTolerance
            || (circle.Center - cylinder.Origin).Cross(axis).Length > tolerance
            || double.Abs(circle.Radius - cylinder.Radius) > tolerance
            || Surface(source, cap).Plane is not { } plane || double.Abs((circle.Center - plane.Origin).Dot(axis)) > tolerance)
            throw new ArgumentException("The cap and cylinder must share a perpendicular, coaxial rim.");

        var seam = topology.Edges.SingleOrDefault(candidate => candidate.Id != id
            && (candidate.StartVertexId == edge.StartVertexId || candidate.EndVertexId == edge.StartVertexId))
            ?? throw new ArgumentException("The circular rim needs one axial seam.");
        var seamUses = topology.Coedges.Where(use => use.EdgeId == seam.Id).ToArray();
        if (Curve(source, seam.Id).Line3 is not { } seamLine || seamLine.Direction.ToVector().Cross(axis).Length > AngularTolerance
            || seamUses.Length != 2 || seamUses.Any(use => use.LoopId != wallUse.LoopId))
            throw new ArgumentException("The circular rim needs two uses of its axial seam on the same wall.");
        var margin = tolerance + clearance;
        var other = Point(source, seam.StartVertexId == edge.StartVertexId ? seam.EndVertexId : seam.StartVertexId);
        var corner = Point(source, edge.StartVertexId);
        if ((corner - other).Dot(axis) <= size + margin || circle.Radius - size <= margin)
            throw new ArgumentException("Reduce the size to leave clearance on the cylinder and cap.");
        if (kind == ImportedEdgeFinishKind.Fillet && circle.Radius - 2 * size <= tolerance)
            throw new ArgumentException("The fillet needs a ring torus. Reduce the radius.");

        var radial = Direction3D.Create((corner - circle.Center) - axis * (corner - circle.Center).Dot(axis));
        var side = axis.Cross(radial.ToVector());
        var shoulder = circle.Center - axis * size;
        var low = circle.Radius - size;
        var ringOrientation = Direction3D.Create(circle.Normal.ToVector() * (binding.OrientedEdgeSense ? 1 : -1));

        foreach (var candidate in topology.Edges.Where(item => item.Id != id && item.Id != seam.Id))
            if (!Clears(direction => CurveRange(source, candidate.Id, circle.Center, direction)))
                throw new ArgumentException($"The rim finish reaches existing boundary edge {candidate.Id.Value}. Reduce the size.");
        foreach (var candidate in topology.Faces.Where(item => item.Id != cap && item.Id != wall))
            if (!Clears(direction => FaceRange(source, candidate, circle.Center, direction)))
                throw new ArgumentException($"The rim finish may reach existing face {candidate.Id.Value}. Reduce the size.");

        var capRing = new Circle3Curve(circle.Center, ringOrientation, low, radial);
        var shoulderRing = new Circle3Curve(shoulder, ringOrientation, circle.Radius, radial);
        if (kind == ImportedEdgeFinishKind.Chamfer)
        {
            var start = capRing.Evaluate(0);
            var delta = shoulderRing.Evaluate(0) - start;
            return new(id, seam.Id, edge.StartVertexId, cap, wall, capUse, wallUse, capRing, shoulderRing,
                CurveGeometry.FromLine(new(start, Direction3D.Create(delta))), new(0, delta.Length),
                SurfaceGeometry.FromCone(new(circle.Center, Direction3D.Create(-axis), low, double.Pi / 4, radial)));
        }

        var sectionCenter = shoulder + radial.ToVector() * low;
        var arc = new Circle3Curve(sectionCenter, Direction3D.Create(side), size, normal);
        return new(id, seam.Id, edge.StartVertexId, cap, wall, capUse, wallUse, capRing, shoulderRing,
            CurveGeometry.FromCircle(arc), new(0, double.Pi / 2),
            SurfaceGeometry.FromTorus(new(shoulder, normal, low, size, radial)));

        bool Clears(Func<Vector3D, Range> project)
        {
            if (new Range(-size, 0).Clears(project(axis), margin)) return true;
            var x = project(radial.ToVector());
            var y = project(side);
            var outer = double.Hypot(double.Max(double.Abs(x.Low), double.Abs(x.High)), double.Max(double.Abs(y.Low), double.Abs(y.High)));
            if (outer + margin < low) return true;
            var inner = double.Hypot(double.Max(0, double.Max(x.Low, -x.High)), double.Max(0, double.Max(y.Low, -y.High)));
            return inner > circle.Radius + margin;
        }
    }

    private static ImportedEdgeFinishWitness SpliceCircularRim(BrepBody source, CircularRim region, double tolerance)
    {
        var patch = new Patch(source, tolerance, [region.Corner]);
        var a = patch.Vertex(region.CapCircle.Evaluate(0));
        var b = patch.Vertex(region.ShoulderCircle.Evaluate(0));
        var capRing = patch.Edge(a, a, CurveGeometry.FromCircle(region.CapCircle), new(0, double.Tau));
        var shoulderRing = patch.Edge(b, b, CurveGeometry.FromCircle(region.ShoulderCircle), new(0, double.Tau));
        var transition = patch.Edge(a, b, region.Transition, region.TransitionTrim);
        var shortened = patch.Shorten(region.WallSeam, region.Corner, b);
        patch.Loop(region.CapUse.LoopId, region.Cap, [new(capRing, region.CapUse.IsReversed)]);
        patch.Loop(region.WallUse.LoopId, region.Wall, source.Topology.GetLoop(region.WallUse.LoopId).CoedgeIds
            .Select(source.Topology.GetCoedge)
            .Select(use => use.EdgeId == region.Selected ? new Use(shoulderRing, use.IsReversed)
                : use.EdgeId == region.WallSeam ? new Use(shortened, use.IsReversed) : new Use(use.EdgeId, use.IsReversed, use.Id)).ToArray());
        var finish = patch.Finish(region.Surface,
            [new(capRing, !region.CapUse.IsReversed), new(transition, false),
             new(shoulderRing, !region.WallUse.IsReversed), new(transition, true)]);
        return new(patch.Build([region.Selected, region.WallSeam]), [finish], [region.Cap, region.Wall], [region.Selected, region.WallSeam]);
    }
}
