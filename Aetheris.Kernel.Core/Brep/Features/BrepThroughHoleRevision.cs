using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Features;

public sealed record ThroughHoleEditBounds(Point3D Min, Point3D Max);
public sealed record ThroughHoleRevisionRequest(string FeatureId, IReadOnlyList<FaceId> WallFaces,
    FaceId EntryFace, Point3D Center, double Diameter, ThroughHoleEditBounds Within,
    double ImportToleranceMillimetres = 1e-6);
public sealed record ThroughHoleRevisionResult(BrepBody Body, string FeatureId,
    IReadOnlyList<FaceId> RetiredWallFaces, IReadOnlyList<FaceId> WallFaces,
    FaceId EntryFace, FaceId ExitFace, Point3D Center, double Diameter, double Depth,
    ThroughHoleEditBounds Within, IReadOnlyDictionary<FaceId, FaceId> FaceCorrespondence,
    int PreservedFaces, int PreservedEdges, double ImportToleranceMillimetres);

/// <summary>Atomic, same-size through-hole replacement in a certified constant-stock
/// prism. It retains the residual BRep and constructs new analytic hole geometry.
/// The first admitted family is Z-axis, isolated, round and through two planar supports.</summary>
public static class BrepThroughHoleRevision
{
    const double Tol = 1e-6;
    sealed record Region(HashSet<FaceId> Walls, (Face Face, Loop Loop)[] Openings,
        HashSet<EdgeId> Edges, HashSet<VertexId> Vertices, CylinderSurface Cylinder, double LowZ, double HighZ);
    sealed record Box(Point3D Min, Point3D Max)
    {
        public bool Overlaps(Box other, double uncertainty) => Min.X <= other.Max.X + uncertainty && Max.X >= other.Min.X - uncertainty
            && Min.Y <= other.Max.Y + uncertainty && Max.Y >= other.Min.Y - uncertainty
            && Min.Z <= other.Max.Z + uncertainty && Max.Z >= other.Min.Z - uncertainty;
    }

    public static KernelResult<ThroughHoleRevisionResult> Execute(BrepBody source, ThroughHoleRevisionRequest request)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            if (string.IsNullOrWhiteSpace(request.FeatureId)) throw new ArgumentException("A stable replacement feature name is required.");
            RequireQualified(source, request.ImportToleranceMillimetres);
            if (request.WallFaces.Count == 0 || request.WallFaces.Distinct().Count() != request.WallFaces.Count)
                throw new ArgumentException("Select the complete distinct cylindrical wall set.");
            var region = Recognize(source, request.WallFaces[0]);
            if (!region.Walls.SetEquals(request.WallFaces))
                throw new ArgumentException("The selected faces do not equal the complete connected cylindrical wall group.");
            if (region.Openings.SingleOrDefault(o => o.Face.Id == request.EntryFace).Face is not { } entry)
                throw new ArgumentException("The entry face must be one of the proved planar supports.");
            if (!double.IsFinite(request.Diameter) || request.Diameter <= 0 || System.Math.Abs(request.Diameter / 2 - region.Cylinder.Radius) > Tol)
                throw new ArgumentException("This revision admits unchanged diameter only; resizing is unsupported.");
            if (!Finite(request.Center) || System.Math.Abs(request.Center.Z - Surface(source, entry.Id).Plane!.Value.Origin.Z) > Tol)
                throw new ArgumentException("The world-space center must lie on the selected entry plane.");
            var box = new Box(request.Within.Min, request.Within.Max);
            if (!Finite(box.Min) || !Finite(box.Max) || box.Min.X >= box.Max.X || box.Min.Y >= box.Max.Y
                || System.Math.Abs(box.Min.Z - region.LowZ) > Tol || System.Math.Abs(box.Max.Z - region.HighZ) > Tol)
                throw new ArgumentException("The edit envelope must be finite and span exactly the two support planes.");
            var r = request.Diameter / 2;
            foreach (var center in new[] { region.Cylinder.Origin, request.Center })
                if (center.X - r <= box.Min.X + Tol || center.X + r >= box.Max.X - Tol || center.Y - r <= box.Min.Y + Tol || center.Y + r >= box.Max.Y - Tol)
                    throw new ArgumentException("The edit envelope must contain both complete holes with positive clearance.");
            var authorized = region.Walls.Concat(region.Openings.Select(o => o.Face.Id)).ToHashSet();
            var conflicts = source.Topology.Faces.Where(f => !authorized.Contains(f.Id) && box.Overlaps(FaceBounds(source, f), System.Math.Max(Tol, request.ImportToleranceMillimetres))).Select(f => f.Id.Value).ToArray();
            if (conflicts.Length > 0)
                throw new ArgumentException($"The stock envelope may intersect unrelated boundary faces: {string.Join(',', conflicts)}. Narrow the operation or choose another destination.");
            var patch = MaterializeHolePatch(source, region, request.Center, r);
            var suppressed = Suppress(source, region);
            RequireQualified(suppressed, request.ImportToleranceMillimetres);
            var body = SuppressThenInsert(source, suppressed, patch, region);
            RequireQualified(body, request.ImportToleranceMillimetres);
            var maximum = source.Topology.Faces.Max(f => f.Id.Value);
            var map = source.Topology.Faces.ToDictionary(f => f.Id, f => region.Walls.Contains(f.Id) ? new FaceId(maximum + f.Id.Value) : f.Id);
            var walls = region.Walls.OrderBy(id => id.Value).Select(id => map[id]).ToArray();
            var exit = region.Openings.Single(o => o.Face.Id != entry.Id).Face.Id;
            return KernelResult<ThroughHoleRevisionResult>.Success(new(body, request.FeatureId,
                region.Walls.OrderBy(id => id.Value).ToArray(), walls, entry.Id, exit, request.Center, request.Diameter,
                region.HighZ - region.LowZ, request.Within, map, source.Topology.Faces.Count() - region.Walls.Count,
                source.Topology.Edges.Count() - region.Edges.Count, request.ImportToleranceMillimetres));
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or KeyNotFoundException or NotSupportedException)
        {
            return KernelResult<ThroughHoleRevisionResult>.Failure([new(KernelDiagnosticCode.ValidationFailed,
                KernelDiagnosticSeverity.Error, "firmament-imported-hole-revision-rejected: " + error.Message,
                "Brep.ThroughHoleRevision")]);
        }
    }

    static bool Finite(Point3D p) => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z);
    static void RequireQualified(BrepBody body, double tolerance)
    {
        if (!double.IsFinite(tolerance) || tolerance <= 0 || tolerance > .1)
            throw new ArgumentException("Import tolerance must be positive, finite and at most 0.1 mm.");
        if (!TopologyGraphValidator.Validate(body.Topology).IsSuccess || body.Topology.Bodies.Count() != 1 || body.Topology.Shells.Count() != 1
            || body.Topology.Coedges.GroupBy(c => c.EdgeId).Any(g => g.Count() != 2 || g.First().IsReversed == g.Last().IsReversed))
            throw new ArgumentException("A single closed oriented manifold body is required.");
        var pcurves = BrepPcurveValidator.Validate(body, tolerance, requireEveryCoedge: true, samples: 257);
        if (!pcurves.IsValid || pcurves.MaximumReconstructionDeviation > tolerance || !BrepExportPreflight.Validate(body, tolerance).IsValid)
            throw new ArgumentException("The imported or revised body is not qualified at the requested tolerance; required trim or boundary checks failed.");
    }
    static Region Recognize(BrepBody body, FaceId seed)
    {
        var cylinder = Surface(body, seed).Cylinder ?? throw new ArgumentException("Seed must be cylindrical.");
        if (cylinder.Axis.ToVector().Cross(new Vector3D(0, 0, 1)).Length > Tol) throw new ArgumentException("Only Z-axis holes are admitted.");
        var incidence = body.Topology.Faces.SelectMany(f => Edges(body, f).Select(e => (Edge: e, Face: f.Id)))
            .GroupBy(x => x.Edge).ToDictionary(g => g.Key, g => g.Select(x => x.Face).Distinct().ToArray());
        var walls = new HashSet<FaceId> { seed };
        var queue = new Queue<FaceId>(); queue.Enqueue(seed);
        while (queue.TryDequeue(out var id))
        {
            foreach (var adjacent in Edges(body, body.Topology.GetFace(id)).SelectMany(e => incidence[e]).Distinct())
            {
                if (walls.Contains(adjacent) || Surface(body, adjacent).Cylinder is not { } other) continue;
                var d = other.Origin - cylinder.Origin;
                if (System.Math.Abs(other.Radius - cylinder.Radius) > Tol || other.Axis.ToVector().Cross(cylinder.Axis.ToVector()).Length > Tol
                    || (d - cylinder.Axis.ToVector() * d.Dot(cylinder.Axis.ToVector())).Length > Tol) continue;
                walls.Add(adjacent); queue.Enqueue(adjacent);
            }
        }
        if (walls.Any(id => body.Bindings.GetFaceBinding(id).Orientation.IsAlignedWithSurface)) throw new ArgumentException("Not an inward cylindrical void wall.");
        var wallEdges = walls.SelectMany(id => Edges(body, body.Topology.GetFace(id))).ToHashSet();
        var openings = new List<(Face Face, Loop Loop)>();
        foreach (var face in body.Topology.Faces.Where(f => !walls.Contains(f.Id)))
            foreach (var loop in face.LoopIds.Select(body.Topology.GetLoop))
            {
                var edges = loop.CoedgeIds.Select(id => body.Topology.GetCoedge(id).EdgeId).ToArray();
                if (!edges.Any(wallEdges.Contains)) continue;
                if (!edges.All(wallEdges.Contains) || Surface(body, face.Id).Plane is not { } plane
                    || plane.Normal.ToVector().Cross(new Vector3D(0, 0, 1)).Length > Tol
                    || body.Bindings.GetFaceBoundaryRoleBinding(loop.Id).Role != FaceBoundaryRole.Inner)
                    throw new ArgumentException("Void boundary is not two isolated circular inner loops on Z-normal planes.");
                var angularCoverage = 0d;
                foreach (var edge in edges)
                {
                    var circle = Curve(body, edge).Circle3 ?? throw new ArgumentException("Non-circular opening.");
                    var offset = circle.Center - cylinder.Origin;
                    if (System.Math.Abs(circle.Radius - cylinder.Radius) > Tol || circle.Normal.ToVector().Cross(new Vector3D(0, 0, 1)).Length > Tol
                        || System.Math.Sqrt(offset.X * offset.X + offset.Y * offset.Y) > Tol) throw new ArgumentException("Non-coaxial opening.");
                    var trim = body.Bindings.GetEdgeBinding(edge).TrimInterval ?? throw new ArgumentException("Missing arc trim.");
                    angularCoverage += System.Math.Abs(trim.End - trim.Start);
                }
                if (System.Math.Abs(angularCoverage - 2 * double.Pi) > Tol) throw new ArgumentException("Opening does not cover a complete circle.");
                openings.Add((face, loop));
            }
        if (openings.Count != 2 || openings.Select(o => o.Face.Id).Distinct().Count() != 2) throw new ArgumentException("Expected two distinct planar supports.");
        var z = openings.Select(o => Surface(body, o.Face.Id).Plane!.Value.Origin.Z).Order().ToArray();
        if (z[1] - z[0] <= Tol) throw new ArgumentException("Zero depth.");
        foreach (var id in walls)
        {
            var arcs = Edges(body, body.Topology.GetFace(id)).Where(e => Curve(body, e).Circle3 is not null).ToArray();
            if (arcs.Length != 2 || arcs.Select(e => Curve(body, e).Circle3!.Value.Center.Z).Order().Zip(z).Any(p => System.Math.Abs(p.First - p.Second) > Tol))
                throw new ArgumentException("Wall patches must span the full same depth.");
        }
        foreach (var edge in wallEdges)
        {
            if (incidence[edge].Length != 2) throw new ArgumentException("Non-manifold wall boundary.");
            if (Curve(body, edge).Line3 is { } line && line.Direction.ToVector().Cross(new Vector3D(0, 0, 1)).Length > Tol) throw new ArgumentException("Non-axial seam.");
            if (Curve(body, edge).Kind is not (CurveGeometryKind.Line3 or CurveGeometryKind.Circle3)) throw new ArgumentException("Unsupported wall edge.");
        }
        var vertices = wallEdges.SelectMany(e => { var edge = body.Topology.GetEdge(e); return new[] { edge.StartVertexId, edge.EndVertexId }; }).ToHashSet();
        return new(walls, openings.ToArray(), wallEdges, vertices, cylinder, z[0], z[1]);
    }

    static BrepBody MaterializeHolePatch(BrepBody source, Region region, Point3D center, double radius)
    {
        var changedCurves = region.Edges.Select(e => source.Bindings.GetEdgeBinding(e).CurveGeometryId).ToHashSet();
        var changedSurfaces = region.Walls.Select(f => source.Bindings.GetFaceBinding(f).SurfaceGeometryId).ToHashSet();
        if (source.Bindings.EdgeBindings.Any(b => changedCurves.Contains(b.CurveGeometryId) && !region.Edges.Contains(b.EdgeId))
            || source.Bindings.FaceBindings.Any(b => changedSurfaces.Contains(b.SurfaceGeometryId) && !region.Walls.Contains(b.FaceId)))
            throw new ArgumentException("Shared geometry outside edit region requires copy-on-write IDs.");
        var geometry = new BrepGeometryStore();
        foreach (var (id, curve) in source.Geometry.Curves) geometry.AddCurve(id, changedCurves.Contains(id) ? MaterializeCurve(curve, region.Cylinder.Origin, center, radius) : curve);
        foreach (var (id, surface) in source.Geometry.Surfaces)
        {
            var c = surface.Cylinder;
            geometry.AddSurface(id, changedSurfaces.Contains(id) ? SurfaceGeometry.FromCylinder(new(new Point3D(center.X, center.Y, c!.Value.Origin.Z), c.Value.Axis, radius, c.Value.XAxis)) : surface);
        }
        var bindings = new BrepBindingModel();
        foreach (var b in source.Bindings.EdgeBindings) bindings.AddEdgeBinding(b);
        foreach (var b in source.Bindings.FaceBindings) bindings.AddFaceBinding(b);
        foreach (var b in source.Bindings.FaceBoundaryRoleBindings) bindings.AddFaceBoundaryRoleBinding(b);
        foreach (var b in source.Bindings.VertexLoopParameterBindings) bindings.AddVertexLoopParameterBinding(b);
        foreach (var b in source.Bindings.PcurveBindings)
        {
            var edge = source.Topology.GetCoedge(b.CoedgeId).EdgeId;
            if (!region.Edges.Contains(edge)) { bindings.AddPcurveBinding(b); continue; }
            var pcurve = b.Pcurve;
            if (!region.Walls.Contains(b.FaceId))
            {
                var plane = Surface(source, b.FaceId).Plane!.Value;
                var circle = geometry.GetCurve(source.Bindings.GetEdgeBinding(edge).CurveGeometryId).Circle3!.Value;
                var d = circle.Center - plane.Origin;
                pcurve = PcurveGeometry.Ellipse(b.Pcurve.Domain, new(d.Dot(plane.UAxis.ToVector()), d.Dot(plane.VAxis.ToVector())),
                    new(circle.Radius * circle.XAxis.ToVector().Dot(plane.UAxis.ToVector()), circle.Radius * circle.XAxis.ToVector().Dot(plane.VAxis.ToVector())),
                    new(circle.Radius * circle.YAxis.ToVector().Dot(plane.UAxis.ToVector()), circle.Radius * circle.YAxis.ToVector().Dot(plane.VAxis.ToVector())));
            }
            bindings.AddPcurveBinding(b with
            {
                Pcurve = pcurve,
                SourceStepPcurveEntityId = null,
                SourceStepCurveEntityId = null,
                SourceStepSurfaceEntityId = null,
                Qualification = null
            });
        }
        var points = Points(source);
        foreach (var id in region.Vertices)
        {
            var p = points[id]; var d = p - region.Cylinder.Origin;
            var length = System.Math.Sqrt(d.X * d.X + d.Y * d.Y);
            points[id] = new(center.X + radius * d.X / length, center.Y + radius * d.Y / length, p.Z);
        }
        return new(source.Topology.Clone(), geometry, bindings, points, null, source.ShellRepresentation);
    }

    static BrepBody Suppress(BrepBody source, Region region)
    {
        var removedLoops = region.Walls.SelectMany(id => source.Topology.GetFace(id).LoopIds).Concat(region.Openings.Select(o => o.Loop.Id)).ToHashSet();
        var topology = new TopologyModel();
        foreach (var v in source.Topology.Vertices.Where(v => !region.Vertices.Contains(v.Id))) topology.AddVertex(v);
        foreach (var e in source.Topology.Edges.Where(e => !region.Edges.Contains(e.Id))) topology.AddEdge(e);
        foreach (var c in source.Topology.Coedges.Where(c => !removedLoops.Contains(c.LoopId))) topology.AddCoedge(c);
        foreach (var l in source.Topology.Loops.Where(l => !removedLoops.Contains(l.Id))) topology.AddLoop(l);
        foreach (var f in source.Topology.Faces.Where(f => !region.Walls.Contains(f.Id))) topology.AddFace(f with { LoopIds = f.LoopIds.Where(id => !removedLoops.Contains(id)).ToArray() });
        foreach (var s in source.Topology.Shells) topology.AddShell(s with { FaceIds = s.FaceIds.Where(id => !region.Walls.Contains(id)).ToArray() });
        foreach (var b in source.Topology.Bodies) topology.AddBody(b);
        return Assemble(source, topology, Points(source).Where(p => !region.Vertices.Contains(p.Key)).ToDictionary(), [], [], [], []);
    }

    static BrepBody SuppressThenInsert(BrepBody original, BrepBody suppressed, BrepBody moved, Region region)
    {
        var wallLoops = region.Walls.SelectMany(id => original.Topology.GetFace(id).LoopIds).ToHashSet();
        var changedLoops = wallLoops.Concat(region.Openings.Select(o => o.Loop.Id)).ToHashSet();
        var vMap = region.Vertices.ToDictionary(id => id, id => new VertexId(original.Topology.Vertices.Max(v => v.Id.Value) + id.Value));
        var eMap = region.Edges.ToDictionary(id => id, id => new EdgeId(original.Topology.Edges.Max(v => v.Id.Value) + id.Value));
        var lMap = changedLoops.ToDictionary(id => id, id => new LoopId(original.Topology.Loops.Max(v => v.Id.Value) + id.Value));
        var cMap = original.Topology.Coedges.Where(c => changedLoops.Contains(c.LoopId)).ToDictionary(c => c.Id, c => new CoedgeId(original.Topology.Coedges.Max(v => v.Id.Value) + c.Id.Value));
        var fMap = region.Walls.ToDictionary(id => id, id => new FaceId(original.Topology.Faces.Max(v => v.Id.Value) + id.Value));
        var topology = new TopologyModel();
        foreach (var v in moved.Topology.Vertices) topology.AddVertex(v with { Id = vMap.GetValueOrDefault(v.Id, v.Id) });
        foreach (var e in moved.Topology.Edges) topology.AddEdge(e with { Id = eMap.GetValueOrDefault(e.Id, e.Id), StartVertexId = vMap.GetValueOrDefault(e.StartVertexId, e.StartVertexId), EndVertexId = vMap.GetValueOrDefault(e.EndVertexId, e.EndVertexId) });
        foreach (var c in moved.Topology.Coedges) topology.AddCoedge(c with { Id = cMap.GetValueOrDefault(c.Id, c.Id), EdgeId = eMap.GetValueOrDefault(c.EdgeId, c.EdgeId), LoopId = lMap.GetValueOrDefault(c.LoopId, c.LoopId), NextCoedgeId = cMap.GetValueOrDefault(c.NextCoedgeId, c.NextCoedgeId), PrevCoedgeId = cMap.GetValueOrDefault(c.PrevCoedgeId, c.PrevCoedgeId) });
        foreach (var l in moved.Topology.Loops) topology.AddLoop(l with { Id = lMap.GetValueOrDefault(l.Id, l.Id), CoedgeIds = l.CoedgeIds.Select(id => cMap.GetValueOrDefault(id, id)).ToArray() });
        foreach (var f in suppressed.Topology.Faces)
        {
            var opening = region.Openings.SingleOrDefault(o => o.Face.Id == f.Id);
            topology.AddFace(opening.Face is null ? f : f with { LoopIds = f.LoopIds.Append(lMap[opening.Loop.Id]).ToArray() });
        }
        foreach (var f in moved.Topology.Faces.Where(f => region.Walls.Contains(f.Id))) topology.AddFace(f with { Id = fMap[f.Id], LoopIds = f.LoopIds.Select(id => lMap[id]).ToArray() });
        foreach (var s in original.Topology.Shells) topology.AddShell(s with { FaceIds = s.FaceIds.Select(id => fMap.GetValueOrDefault(id, id)).ToArray() });
        foreach (var b in original.Topology.Bodies) topology.AddBody(b);
        var points = Points(moved).ToDictionary(p => vMap.GetValueOrDefault(p.Key, p.Key), p => p.Value);
        return Assemble(moved, topology, points, eMap, cMap, lMap, fMap);
    }

    static BrepBody Assemble(BrepBody geometrySource, TopologyModel topology, Dictionary<VertexId, Point3D> points,
        Dictionary<EdgeId, EdgeId> eMap, Dictionary<CoedgeId, CoedgeId> cMap, Dictionary<LoopId, LoopId> lMap, Dictionary<FaceId, FaceId> fMap)
    {
        var edges = topology.Edges.Select(e => e.Id).ToHashSet(); var faces = topology.Faces.Select(f => f.Id).ToHashSet();
        var loops = topology.Loops.Select(l => l.Id).ToHashSet(); var coedges = topology.Coedges.Select(c => c.Id).ToHashSet();
        var bindings = new BrepBindingModel();
        foreach (var b in geometrySource.Bindings.EdgeBindings) { var id = eMap.GetValueOrDefault(b.EdgeId, b.EdgeId); if (edges.Contains(id)) bindings.AddEdgeBinding(b with { EdgeId = id }); }
        foreach (var b in geometrySource.Bindings.FaceBindings) { var id = fMap.GetValueOrDefault(b.FaceId, b.FaceId); if (faces.Contains(id)) bindings.AddFaceBinding(b with { FaceId = id, SourceStepEntityId = fMap.ContainsKey(b.FaceId) ? null : b.SourceStepEntityId }); }
        foreach (var b in geometrySource.Bindings.FaceBoundaryRoleBindings) { var id = lMap.GetValueOrDefault(b.LoopId, b.LoopId); if (loops.Contains(id)) bindings.AddFaceBoundaryRoleBinding(b with { LoopId = id, FaceId = fMap.GetValueOrDefault(b.FaceId, b.FaceId), SourceStepEntityId = lMap.ContainsKey(b.LoopId) ? null : b.SourceStepEntityId }); }
        foreach (var b in geometrySource.Bindings.PcurveBindings) { var id = cMap.GetValueOrDefault(b.CoedgeId, b.CoedgeId); if (coedges.Contains(id)) bindings.AddPcurveBinding(b with { CoedgeId = id, FaceId = fMap.GetValueOrDefault(b.FaceId, b.FaceId) }); }
        foreach (var b in geometrySource.Bindings.VertexLoopParameterBindings)
            if (loops.Contains(b.LoopId) && faces.Contains(b.FaceId) && points.ContainsKey(b.VertexId)) bindings.AddVertexLoopParameterBinding(b);
        var geometry = new BrepGeometryStore();
        foreach (var id in bindings.EdgeBindings.Select(b => b.CurveGeometryId).Distinct()) geometry.AddCurve(id, geometrySource.Geometry.GetCurve(id));
        foreach (var id in bindings.FaceBindings.Select(b => b.SurfaceGeometryId).Distinct()) geometry.AddSurface(id, geometrySource.Geometry.GetSurface(id));
        return new(topology, geometry, bindings, points, null, geometrySource.ShellRepresentation);
    }

    static Box FaceBounds(BrepBody body, Face face)
    {
        var points = Edges(body, face).SelectMany(e => CurveHull(body, e)).ToArray();
        if (points.Length == 0) throw new ArgumentException("Unbounded face.");
        if (Surface(body, face.Id).Plane is not null) return Bounds(points);
        if (Surface(body, face.Id).Cylinder is { } c)
        {
            var values = points.Select(p => (p - c.Origin).Dot(c.Axis.ToVector())).ToArray();
            return AxialBounds(c.Origin, c.Axis.ToVector(), values.Min(), values.Max(), c.Radius);
        }
        if (Surface(body, face.Id).Cone is { } cone)
        {
            var values = points.Select(p => (p - cone.Apex).Dot(cone.Axis.ToVector())).ToArray();
            return AxialBounds(cone.Apex, cone.Axis.ToVector(), values.Min(), values.Max(), System.Math.Max(System.Math.Abs(values.Min()), System.Math.Abs(values.Max())) * System.Math.Tan(cone.SemiAngleRadians));
        }
        throw new ArgumentException($"No conservative support bound for {Surface(body, face.Id).Kind}.");
    }

    static Box AxialBounds(Point3D origin, Vector3D axis, double low, double high, double radius)
    {
        var a = origin + axis * low; var b = origin + axis * high;
        var x = radius * System.Math.Sqrt(System.Math.Max(0, 1 - axis.X * axis.X));
        var y = radius * System.Math.Sqrt(System.Math.Max(0, 1 - axis.Y * axis.Y));
        var z = radius * System.Math.Sqrt(System.Math.Max(0, 1 - axis.Z * axis.Z));
        return new(new(System.Math.Min(a.X, b.X) - x, System.Math.Min(a.Y, b.Y) - y, System.Math.Min(a.Z, b.Z) - z), new(System.Math.Max(a.X, b.X) + x, System.Math.Max(a.Y, b.Y) + y, System.Math.Max(a.Z, b.Z) + z));
    }

    static IEnumerable<Point3D> CurveHull(BrepBody body, EdgeId id)
    {
        var c = Curve(body, id);
        var interval = body.Bindings.GetEdgeBinding(id).TrimInterval ?? throw new ArgumentException("Missing finite trim.");
        if (c.Line3 is { } line) return [line.Evaluate(interval.Start), line.Evaluate(interval.End)];
        if (c.Circle3 is { } circle)
        {
            var n = circle.Normal;
            var x = circle.Radius * System.Math.Sqrt(System.Math.Max(0, 1 - n.X * n.X));
            var y = circle.Radius * System.Math.Sqrt(System.Math.Max(0, 1 - n.Y * n.Y));
            var z = circle.Radius * System.Math.Sqrt(System.Math.Max(0, 1 - n.Z * n.Z));
            // All eight corners are necessary for conservative projection onto
            // oblique cylinder/cone axes; two opposite corners can cancel terms.
            return new[] { -x, x }.SelectMany(dx => new[] { -y, y }.SelectMany(dy => new[] { -z, z }.Select(dz => circle.Center + new Vector3D(dx, dy, dz))));
        }
        if (c.BSpline3 is { } spline) return spline.ControlPoints;
        throw new ArgumentException($"Unsupported conservative curve hull {c.Kind}.");
    }

    static Box Bounds(Point3D[] p) => new(new(p.Min(v => v.X), p.Min(v => v.Y), p.Min(v => v.Z)), new(p.Max(v => v.X), p.Max(v => v.Y), p.Max(v => v.Z)));
    static CurveGeometry MaterializeCurve(CurveGeometry c, Point3D oldCenter, Point3D center, double radius)
    {
        if (c.Circle3 is { } circle) return CurveGeometry.FromCircle(new(new(center.X, center.Y, circle.Center.Z), circle.Normal, radius, circle.XAxis));
        if (c.Line3 is { } line)
        {
            var d = line.Origin - oldCenter; var length = System.Math.Sqrt(d.X * d.X + d.Y * d.Y);
            return CurveGeometry.FromLine(new(new(center.X + radius * d.X / length, center.Y + radius * d.Y / length, line.Origin.Z), line.Direction));
        }
        throw new NotSupportedException("Only circular rims and axial seams are admitted.");
    }
    static IEnumerable<EdgeId> Edges(BrepBody b, Face f) => f.LoopIds.SelectMany(id => b.Topology.GetLoop(id).CoedgeIds).Select(id => b.Topology.GetCoedge(id).EdgeId).Distinct();
    static SurfaceGeometry Surface(BrepBody b, FaceId id) { b.TryGetFaceSurfaceGeometry(id, out var s); return s ?? throw new ArgumentException("Missing surface."); }
    static CurveGeometry Curve(BrepBody b, EdgeId id) { b.TryGetEdgeCurveGeometry(id, out var c); return c ?? throw new ArgumentException("Missing curve."); }
    static Dictionary<VertexId, Point3D> Points(BrepBody b) => b.Topology.Vertices.ToDictionary(v => v.Id, v => b.TryGetVertexPoint(v.Id, out var p) ? p : throw new ArgumentException("Missing vertex."));
}
