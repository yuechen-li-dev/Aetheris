using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Features;

internal static partial class BrepImportedEdgeFinish
{
    private sealed record PolygonSide(Coedge CapUse, Coedge SideUse, FaceId Face, Vector3D Normal, Vector3D Along);
    private sealed record PolygonCorner(VertexId Old, EdgeId Spoke, Point3D Shoulder, Point3D Cap,
        Ellipse3Curve? Miter);
    private sealed record PolygonRim(FaceId Cap, PolygonSide[] Sides, PolygonCorner[] Corners, SurfaceGeometry[] Supports);

    private static PolygonRim PlanPolygonRim(BrepBody source, FaceId? selected, ImportedEdgeFinishKind kind,
        double size, double tolerance, double clearance)
    {
        if (selected is not { } cap) throw new ArgumentException("Select a planar face boundary for the polygon plan.");
        var topology = source.Topology;
        var normal = Outward(source, cap);
        var outer = topology.GetFace(cap).LoopIds
            .Single(loop => source.Bindings.GetFaceBoundaryRoleBinding(loop).Role == FaceBoundaryRole.Outer);
        var boundary = topology.GetLoop(outer).CoedgeIds.Select(topology.GetCoedge).ToArray();
        if (boundary.Length < 3 || boundary.Any(use => Curve(source, use.EdgeId).Line3 is null))
            throw new ArgumentException("The polygon plan requires a closed outer loop of straight edges.");

        var incidence = topology.Coedges.ToLookup(use => use.EdgeId);
        var owners = topology.Faces.SelectMany(face => face.LoopIds.Select(loop => (loop, face.Id)))
            .ToDictionary(item => item.loop, item => item.Id);
        var vertices = boundary.Select(use =>
        {
            var edge = topology.GetEdge(use.EdgeId);
            return use.IsReversed ? edge.EndVertexId : edge.StartVertexId;
        }).ToArray();
        if (vertices.Distinct().Count() != vertices.Length)
            throw new ArgumentException("Select a simple outer polygon.");
        var points = vertices.Select(vertex => Point(source, vertex)).ToArray();
        var sides = new PolygonSide[vertices.Length];
        var corners = new PolygonCorner[vertices.Length];
        var supports = new SurfaceGeometry[vertices.Length];
        var selectedEdges = boundary.Select(use => use.EdgeId).ToHashSet();
        var margin = clearance + tolerance;

        for (var i = 0; i < sides.Length; i++)
        {
            var use = boundary[i];
            var support = incidence[use.EdgeId].Single(other => other.Id != use.Id);
            var supportFace = owners[support.LoopId];
            if (supportFace == cap || support.IsReversed == use.IsReversed ||
                source.Bindings.GetFaceBoundaryRoleBinding(support.LoopId).Role != FaceBoundaryRole.Outer)
                throw new ArgumentException("Every polygon edge needs a different exterior planar side.");
            var sideNormal = Outward(source, supportFace);
            var along = Direction3D.Create(points[Next(i)] - points[i]).ToVector();
            if (double.Abs(normal.Dot(sideNormal)) > AngularTolerance || double.Abs(along.Dot(sideNormal)) > AngularTolerance)
                throw new ArgumentException("Polygon rim sides must be perpendicular to the cap.");
            if (points.Any(point => (point - points[i]).Dot(sideNormal) > tolerance))
                throw new ArgumentException("The full cap profile must be convex.");
            sides[i] = new(use, support, supportFace, sideNormal, along);
        }
        if (sides.Select(side => side.Face).Distinct().Count() != sides.Length)
            throw new ArgumentException("Each cap edge needs its own side face. Merge collinear splits before finishing.");

        for (var i = 0; i < corners.Length; i++)
        {
            var point = points[i];
            var corner = vertices[i];
            var previous = sides[(i + sides.Length - 1) % sides.Length];
            var current = sides[i];
            var cosine = double.Clamp(previous.Normal.Dot(current.Normal), -1, 1);
            if (previous.Normal.Cross(current.Normal).Length <= AngularTolerance)
                throw new ArgumentException("Polygon rim corners must have a strict convex turn.");
            var bisector = (previous.Normal + current.Normal) * (size / (1 + cosine));
            var spoke = topology.Edges.SingleOrDefault(edge => !selectedEdges.Contains(edge.Id)
                && (edge.StartVertexId == corner || edge.EndVertexId == corner))
                ?? throw new ArgumentException("Each polygon corner needs one straight axial edge.");
            var spokeFaces = incidence[spoke.Id].Select(use => owners[use.LoopId]).ToHashSet();
            if (Curve(source, spoke.Id).Line3 is not { } line || line.Direction.ToVector().Cross(normal).Length > AngularTolerance
                || !spokeFaces.SetEquals([current.Face, previous.Face])
                || incidence[spoke.Id].Any(use => use.LoopId != current.SideUse.LoopId && use.LoopId != previous.SideUse.LoopId))
                throw new ArgumentException("Each polygon corner must join its two sides on one straight axial edge.");
            var other = Point(source, spoke.StartVertexId == corner ? spoke.EndVertexId : spoke.StartVertexId);
            if ((point - other).Dot(normal) <= size + margin)
                throw new ArgumentException("Reduce the size to leave clearance on every side.");
            var shoulder = point - normal * size;
            var inset = point - bisector;
            Ellipse3Curve? miter = kind == ImportedEdgeFinishKind.Fillet
                ? new(shoulder - bisector, Direction3D.Create(bisector.Cross(normal)), bisector.Length, size, Direction3D.Create(bisector))
                : null;
            corners[i] = new(corner, spoke.Id, shoulder, inset, miter);
        }

        // Each strip bounds one exact ruled/rolled patch and its removed material.
        var envelopes = new (Vector3D Axis, Range Interval)[sides.Length][];
        for (var i = 0; i < sides.Length; i++)
        {
            var side = sides[i];
            var a = corners[i];
            var b = corners[Next(i)];
            if ((b.Cap - a.Cap).Dot(side.Along) <= margin
                || corners.Any(corner => (corner.Cap - points[i]).Dot(side.Normal) > -size + tolerance))
                throw new ArgumentException("Reduce the size to keep the inset rim open and convex.");
            var surfaceAxis = Direction3D.Create(side.Along);
            supports[i] = kind == ImportedEdgeFinishKind.Fillet
                ? SurfaceGeometry.FromCylinder(new(a.Shoulder - side.Normal * size, surfaceAxis, size, Direction3D.Create(side.Normal)))
                : SurfaceGeometry.FromPlane(new(a.Shoulder, Direction3D.Create(normal + side.Normal), surfaceAxis));
            Point3D[] strip = [points[i], points[Next(i)], a.Shoulder, b.Shoulder, a.Cap, b.Cap,
                a.Cap - normal * size, b.Cap - normal * size];
            envelopes[i] = new[] { normal, side.Along, side.Normal }
                .Select(axis => (axis, Project(strip, points[0], axis))).ToArray();
        }

        var ownedEdges = selectedEdges.Concat(corners.Select(corner => corner.Spoke)).ToHashSet();
        var ownedFaces = sides.Select(side => side.Face).Append(cap).ToHashSet();
        foreach (var edge in topology.Edges.Where(edge => !ownedEdges.Contains(edge.Id)))
            if (envelopes.Any(box => !box.Any(slab => slab.Interval.Clears(CurveRange(source, edge.Id, points[0], slab.Axis), margin))))
                throw new ArgumentException($"The polygon rim finish reaches existing boundary edge {edge.Id.Value}. Reduce the size.");
        foreach (var face in topology.Faces.Where(face => !ownedFaces.Contains(face.Id)))
            if (envelopes.Any(box => !box.Any(slab => slab.Interval.Clears(FaceRange(source, face, points[0], slab.Axis), margin))))
                throw new ArgumentException($"The polygon rim finish may reach existing face {face.Id.Value}. Reduce the size.");

        return new(cap, sides, corners, supports);
        int Next(int i) => (i + 1) % sides.Length;
    }

    private static ImportedEdgeFinishWitness SplicePolygonRim(BrepBody source, PolygonRim plan, double tolerance)
    {
        var patch = new Patch(source, tolerance, plan.Corners.Select(corner => corner.Old).ToHashSet());
        var count = plan.Corners.Length;
        var high = plan.Corners.Select(corner => patch.Vertex(corner.Cap)).ToArray();
        var low = plan.Corners.Select(corner => patch.Vertex(corner.Shoulder)).ToArray();
        var miters = plan.Corners.Select((corner, i) => corner.Miter is { } ellipse
            ? patch.Edge(low[i], high[i], CurveGeometry.FromEllipse(ellipse), new(0, double.Pi / 2))
            : patch.Line(low[i], high[i])).ToArray();
        var shortened = plan.Corners.Select((corner, i) => (corner.Spoke, New: patch.Shorten(corner.Spoke, corner.Old, low[i])))
            .ToDictionary(item => item.Spoke, item => item.New);
        var capEdges = high.Select((vertex, i) => patch.Line(vertex, high[(i + 1) % count])).ToArray();
        patch.Loop(plan.Sides[0].CapUse.LoopId, plan.Cap, capEdges.Select(edge => new Use(edge, false)).ToArray());
        var finishFaces = new FaceId[count];
        for (var i = 0; i < count; i++)
        {
            var next = (i + 1) % count;
            var side = plan.Sides[i];
            var shoulder = patch.Line(low[i], low[next]);
            var uses = source.Topology.GetLoop(side.SideUse.LoopId).CoedgeIds.Select(source.Topology.GetCoedge)
                .Select(use => use.Id == side.SideUse.Id ? new Use(shoulder, true)
                    : shortened.TryGetValue(use.EdgeId, out var trimmed) ? new Use(trimmed, use.IsReversed)
                    : new Use(use.EdgeId, use.IsReversed, use.Id)).ToArray();
            patch.Loop(side.SideUse.LoopId, side.Face, uses);
            finishFaces[i] = patch.Finish(plan.Supports[i],
                [new(capEdges[i], true), new(miters[i], true), new(shoulder, false), new(miters[next], false)]);
        }
        var retired = plan.Sides.Select(side => side.CapUse.EdgeId).Concat(shortened.Keys).ToArray();
        return new(patch.Build(retired), finishFaces, [plan.Cap, .. plan.Sides.Select(side => side.Face)], retired);
    }
}
