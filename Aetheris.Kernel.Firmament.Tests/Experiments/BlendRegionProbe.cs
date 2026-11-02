using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Tests.Experiments;

/// <summary>
/// E0 of blend clean-up: classify faces as primary (large, analytic, extendable) or
/// secondary (blend-like faces between primaries), group secondaries into regions and
/// record what each region touches and what would block its removal. Read-only; no
/// surgery. Experimental evidence, not a production recognizer.
/// </summary>
internal static class BlendRegionProbe
{
    internal sealed record Options(double MaxBlendRadius = 5, double TangentDegrees = 3)
    {
        // A 45° chamfer of size c is c·√2 wide; admit chamfers up to the blend-radius limit.
        public double MaxChamferWidth => MaxBlendRadius * Math.Sqrt(2) + 1e-6;
    }

    internal sealed record FaceReport(int Face, string Kind, string Role, string Reason, double? Radius, double Area,
        int TangentEdges, int SharpEdges, int InnerLoops, bool ConcaveRound);

    internal sealed record RegionReport(int Id, string Shape, int[] Faces, string[] FaceKinds, int[] TangentPrimaries,
        string[] TangentPrimaryKinds, int[] SharpNeighbours, string[] Blockers, int[] FaceBridges, string?[] FaceBlockers);

    internal sealed record Report(string Name, int Faces, int Secondaries, FaceReport[] FaceReports, RegionReport[] Regions,
        Dictionary<int, (float[] Positions, int[] Indices)> Mesh);

    private sealed record EdgeInfo(EdgeId Edge, FaceId[] Faces, bool Tangent, double MaxAngleDegrees);

    internal static Report Run(string name, BrepBody body, Options? options = null)
    {
        options ??= new();
        var tess = BrepDisplayTessellator.Tessellate(body);
        if (!tess.IsSuccess) throw new InvalidOperationException($"{name}: tessellation failed: {string.Join("; ", tess.Diagnostics.Select(d => d.Message))}");
        var patches = tess.Value.FacePatches.GroupBy(p => p.FaceId).ToDictionary(g => g.Key, g => g.ToArray());
        var polylines = tess.Value.EdgePolylines.ToDictionary(p => p.EdgeId);

        var edgeFaces = new Dictionary<EdgeId, HashSet<FaceId>>();
        foreach (var face in body.Topology.Faces)
            foreach (var loop in face.LoopIds)
                foreach (var use in body.Topology.GetLoop(loop).CoedgeIds)
                {
                    var edge = body.Topology.GetCoedge(use).EdgeId;
                    if (!edgeFaces.TryGetValue(edge, out var set)) edgeFaces[edge] = set = [];
                    set.Add(face.Id);
                }

        var edges = edgeFaces.Where(p => p.Value.Count == 2).Select(p =>
        {
            var faces = p.Value.ToArray();
            var angle = polylines.TryGetValue(p.Key, out var line) ? MaxNormalAngle(body, line.Points, faces[0], faces[1], patches) : 180;
            return new EdgeInfo(p.Key, faces, angle <= options.TangentDegrees, angle);
        }).ToDictionary(e => e.Edge);
        var faceEdges = body.Topology.Faces.ToDictionary(f => f.Id,
            f => f.LoopIds.SelectMany(l => body.Topology.GetLoop(l).CoedgeIds).Select(c => body.Topology.GetCoedge(c).EdgeId)
                .Where(edges.ContainsKey).Select(e => edges[e]).Distinct().ToArray());

        var reports = new Dictionary<FaceId, FaceReport>();
        foreach (var face in body.Topology.Faces)
        {
            body.TryGetFaceSurfaceGeometry(face.Id, out var surface);
            var kind = surface?.Kind ?? SurfaceGeometryKind.Plane;
            var mine = faceEdges[face.Id];
            var tangent = mine.Where(e => e.Tangent).ToArray();
            var tangentNeighbours = tangent.Select(e => e.Faces.Single(f => f != face.Id)).Distinct().Count();
            var inner = face.LoopIds.Count(l => body.Bindings.FaceBoundaryRoleBindings.Any(b => b.FaceId == face.Id && b.LoopId == l && b.Role == FaceBoundaryRole.Inner));
            var area = Area(patches.GetValueOrDefault(face.Id) ?? []);
            var radius = kind switch
            {
                SurfaceGeometryKind.Cylinder => surface!.Cylinder!.Value.Radius,
                SurfaceGeometryKind.Torus => surface!.Torus!.Value.MinorRadius,
                SurfaceGeometryKind.Sphere => surface!.Sphere!.Value.Radius,
                _ => (double?)null,
            };
            var concave = kind == SurfaceGeometryKind.Cylinder && IsConcave(surface!.Cylinder!.Value, patches.GetValueOrDefault(face.Id) ?? []);

            // How far the face wraps its own axis (cylinder) or tube (torus). A blend's section is
            // under 180°; a slot end is 180° and a teardrop or hole wall wraps further.
            var sweep = kind switch
            {
                SurfaceGeometryKind.Cylinder => Sweep(patches.GetValueOrDefault(face.Id) ?? [], q => AngleAround(q - surface!.Cylinder!.Value.Origin, surface.Cylinder.Value.Axis.ToVector(), surface.Cylinder.Value.XAxis.ToVector())),
                SurfaceGeometryKind.Torus when surface!.Torus!.Value.MajorRadius > surface.Torus.Value.MinorRadius => Sweep(patches.GetValueOrDefault(face.Id) ?? [], q => TubeAngle(surface!.Torus!.Value, q)),
                _ => (double?)null,
            };
            string role = "primary", reason = "large-or-not-blend-shaped";
            if (sweep is > 178)
            {
                reports[face.Id] = new(face.Id.Value, kind.ToString(), role, $"wraps-{sweep:F0}deg-not-a-blend", radius, area,
                    tangent.Length, mine.Length - tangent.Length, inner, concave);
                continue;
            }
            if (kind is SurfaceGeometryKind.Cylinder or SurfaceGeometryKind.Torus or SurfaceGeometryKind.Sphere or SurfaceGeometryKind.BSplineSurfaceWithKnots)
            {
                if (tangentNeighbours >= 2 && (radius is null || radius <= options.MaxBlendRadius))
                    (role, reason) = ("secondary", radius is null ? "freeform-tangent-bounded" : "round-tangent-bounded");
                else if (tangentNeighbours >= 1 && kind is SurfaceGeometryKind.Sphere or SurfaceGeometryKind.BSplineSurfaceWithKnots
                         && (radius is null || radius <= options.MaxBlendRadius))
                    (role, reason) = ("secondary", "corner-patch-candidate");
                else if (radius is { } r && r <= options.MaxBlendRadius && tangentNeighbours < 2)
                    reason = concave && tangentNeighbours == 0 ? "hole-or-cut-wall" : "round-not-tangent-bounded";
            }
            else if (kind is SurfaceGeometryKind.Plane or SurfaceGeometryKind.Cone && mine.Length >= 3)
            {
                // A chamfer strip: narrow, and leaning against two neighbours along its long sides.
                var length = mine.Select(e => polylines.TryGetValue(e.Edge, out var l) ? Length(l.Points) : 0).OrderByDescending(x => x).Take(2).Sum() / 2;
                var width = length > 0 ? area / length : double.PositiveInfinity;
                var leaning = mine.Count(e => e.MaxAngleDegrees is > 20 and < 70);
                if (width <= options.MaxChamferWidth && leaning >= 2)
                    (role, reason) = ("secondary", "chamfer-strip");
            }
            reports[face.Id] = new(face.Id.Value, kind.ToString(), role, reason, radius, area,
                tangent.Length, mine.Length - tangent.Length, inner, concave);
        }

        // Regions: connected secondaries.
        var parent = reports.Keys.ToDictionary(f => f, f => f);
        FaceId Find(FaceId f) { while (parent[f] != f) f = parent[f] = parent[parent[f]]; return f; }
        foreach (var e in edges.Values)
            if (reports[e.Faces[0]].Role == "secondary" && reports[e.Faces[1]].Role == "secondary")
                parent[Find(e.Faces[0])] = Find(e.Faces[1]);
        var regions = new List<RegionReport>();
        foreach (var group in reports.Keys.Where(f => reports[f].Role == "secondary").GroupBy(Find).OrderBy(g => g.Min(f => f.Value)))
        {
            var members = group.ToHashSet();
            var boundary = members.SelectMany(f => faceEdges[f]).Where(e => !(members.Contains(e.Faces[0]) && members.Contains(e.Faces[1]))).ToArray();
            var tangentPrimaries = boundary.Where(e => e.Tangent).Select(e => members.Contains(e.Faces[0]) ? e.Faces[1] : e.Faces[0]).Distinct().OrderBy(f => f.Value).ToArray();
            var sharp = boundary.Where(e => !e.Tangent).Select(e => members.Contains(e.Faces[0]) ? e.Faces[1] : e.Faces[0]).Distinct().OrderBy(f => f.Value).ToArray();
            bool HoleWall(FaceId f) => reports[f].Reason == "hole-or-cut-wall" || reports[f].Reason.StartsWith("wraps-", StringComparison.Ordinal) && reports[f].ConcaveRound;
            bool Freeform(FaceId f) => reports[f].Role == "primary" && reports[f].Kind == nameof(SurfaceGeometryKind.BSplineSurfaceWithKnots);
            // Per face: what stops this face (not its whole network) from being removed.
            string? FaceBlocker(FaceId f)
            {
                if (reports[f].InnerLoops > 0) return "feature-cut-into-blend";
                foreach (var e in faceEdges[f])
                {
                    var n = e.Faces[0] == f ? e.Faces[1] : e.Faces[0];
                    if (members.Contains(n)) continue;
                    // A break around its own hole (coaxial) is a broken hole edge, not a hole cut across a blend.
                    if (!e.Tangent && HoleWall(n) && !Coaxial(body, f, n)) return $"hole-wall-meets-blend:face {n.Value}";
                    if (Freeform(n)) return $"freeform-neighbour-not-extendable:face {n.Value}";
                }
                return null;
            }
            var blockers = new List<string>();
            var faceBlockers = members.OrderBy(f => f.Value).Select(FaceBlocker).ToArray();
            blockers.AddRange(faceBlockers.Where(b => b is not null)!);
            var isChamfer = members.All(f => reports[f].Reason == "chamfer-strip");
            var anchors = isChamfer ? sharp.Length : tangentPrimaries.Length;
            var shape = isChamfer ? "chamfer" : anchors switch { 2 => "chain", 3 => "corner", < 2 => "underanchored", _ => "complex" };
            // Per member: distinct primaries it is tangent to (2 = strip, 3 = corner, 0-1 = blend meeting blends).
            var bridges = members.OrderBy(f => f.Value).Select(f => faceEdges[f].Where(e => e.Tangent)
                .Select(e => e.Faces[0] == f ? e.Faces[1] : e.Faces[0]).Where(n => reports[n].Role == "primary").Distinct().Count()).ToArray();
            regions.Add(new(regions.Count + 1, shape, members.Select(f => f.Value).Order().ToArray(),
                members.OrderBy(f => f.Value).Select(f => reports[f].Kind).ToArray(),
                tangentPrimaries.Select(f => f.Value).ToArray(), tangentPrimaries.Select(f => reports[f].Kind).ToArray(),
                sharp.Select(f => f.Value).ToArray(), blockers.Distinct().ToArray(), bridges, faceBlockers));
        }

        var mesh = patches.ToDictionary(p => p.Key.Value, p =>
        {
            var positions = new List<float>(); var indices = new List<int>();
            foreach (var patch in p.Value)
            {
                var offset = positions.Count / 3;
                foreach (var v in patch.Positions) { positions.Add((float)v.X); positions.Add((float)v.Y); positions.Add((float)v.Z); }
                indices.AddRange(patch.TriangleIndices.Select(i => i + offset));
            }
            return (positions.ToArray(), indices.ToArray());
        });
        return new(name, reports.Count, reports.Values.Count(r => r.Role == "secondary"),
            reports.Values.OrderBy(r => r.Face).ToArray(), regions.ToArray(), mesh);
    }

    private static double MaxNormalAngle(BrepBody body, IReadOnlyList<Point3D> line, FaceId a, FaceId b, Dictionary<FaceId, DisplayFaceMeshPatch[]> patches)
    {
        if (line.Count < 2) return 180;
        var worst = 0d;
        // Interior samples only: endpoints sit on vertices where three faces meet.
        for (var i = 1; i <= 5; i++)
        {
            var p = At(line, i / 6d);
            var na = Normal(body, a, p, patches.GetValueOrDefault(a) ?? []);
            var nb = Normal(body, b, p, patches.GetValueOrDefault(b) ?? []);
            if (na is null || nb is null) return 180;
            var cos = Math.Clamp(na.Value.Dot(nb.Value) / (na.Value.Length * nb.Value.Length), -1, 1);
            worst = Math.Max(worst, Math.Acos(cos) * 180 / Math.PI);
        }
        return worst;
    }

    /// <summary>Exact surface normal at the point, oriented outward by the face's display mesh.</summary>
    private static Vector3D? Normal(BrepBody body, FaceId face, Point3D p, DisplayFaceMeshPatch[] patches)
    {
        if (!body.TryGetFaceSurfaceGeometry(face, out var s) || s is null) return null;
        Vector3D? n = s.Kind switch
        {
            SurfaceGeometryKind.Plane => s.Plane!.Value.Normal.ToVector(),
            SurfaceGeometryKind.Cylinder => Radial(p - s.Cylinder!.Value.Origin, s.Cylinder.Value.Axis.ToVector()),
            SurfaceGeometryKind.Sphere => p - s.Sphere!.Value.Center,
            SurfaceGeometryKind.Torus => TorusNormal(s.Torus!.Value, p),
            SurfaceGeometryKind.Cone => ConeNormal(s.Cone!.Value, p),
            _ => null,
        };
        var mesh = NearestMeshNormal(patches, p);
        if (n is null) return mesh;
        if (mesh is { } m && n.Value.Dot(m) < 0) n = n.Value * -1;
        return n;
    }

    private static (Point3D Origin, Vector3D Axis)? AxisOf(BrepBody body, FaceId face)
    {
        if (!body.TryGetFaceSurfaceGeometry(face, out var s) || s is null) return null;
        return s.Kind switch
        {
            SurfaceGeometryKind.Cylinder => (s.Cylinder!.Value.Origin, s.Cylinder.Value.Axis.ToVector()),
            SurfaceGeometryKind.Cone => (s.Cone!.Value.Apex, s.Cone.Value.Axis.ToVector()),
            SurfaceGeometryKind.Torus => (s.Torus!.Value.Center, s.Torus.Value.Axis.ToVector()),
            _ => null,
        };
    }

    private static bool Coaxial(BrepBody body, FaceId a, FaceId b)
    {
        if (AxisOf(body, a) is not { } x || AxisOf(body, b) is not { } y) return false;
        if (Math.Abs(Math.Abs(x.Axis.Dot(y.Axis)) / (x.Axis.Length * y.Axis.Length) - 1) > 1e-6) return false;
        var offset = Radial(y.Origin - x.Origin, x.Axis * (1 / x.Axis.Length));
        return offset.Length < 1e-4;
    }

    private static double? Sweep(DisplayFaceMeshPatch[] patches, Func<Point3D, double> angle)
    {
        var angles = patches.SelectMany(p => p.Positions).Select(angle).Select(a => (a * 180 / Math.PI + 360) % 360).Order().ToArray();
        if (angles.Length < 3) return null;
        var gap = angles.Zip(angles.Skip(1)).Select(p => p.Second - p.First).Append(angles[0] + 360 - angles[^1]).Max();
        return 360 - gap;
    }

    private static double AngleAround(Vector3D offset, Vector3D axis, Vector3D x)
    {
        var y = axis.Cross(x);
        var r = Radial(offset, axis);
        return Math.Atan2(r.Dot(y), r.Dot(x));
    }

    private static double TubeAngle(Core.Geometry.Surfaces.TorusSurface t, Point3D p)
    {
        var axis = t.Axis.ToVector();
        var radial = Radial(p - t.Center, axis);
        var outward = radial * (1 / radial.Length);
        var tube = p - (t.Center + outward * t.MajorRadius);
        return Math.Atan2(tube.Dot(axis), tube.Dot(outward));
    }

    private static Vector3D Radial(Vector3D offset, Vector3D axis) => offset - axis * offset.Dot(axis);

    private static Vector3D TorusNormal(Core.Geometry.Surfaces.TorusSurface t, Point3D p)
    {
        var axis = t.Axis.ToVector();
        var radial = Radial(p - t.Center, axis);
        var tube = t.Center + radial * (t.MajorRadius / radial.Length);
        return p - tube;
    }

    private static Vector3D ConeNormal(Core.Geometry.Surfaces.ConeSurface c, Point3D p)
    {
        var axis = c.Axis.ToVector();
        var v = p - c.Apex; var h = v.Dot(axis);
        var radial = Radial(v, axis); radial = radial * (1 / radial.Length);
        return radial * Math.Cos(c.SemiAngleRadians) - axis * (Math.Sin(c.SemiAngleRadians) * Math.Sign(h));
    }

    private static Vector3D? NearestMeshNormal(DisplayFaceMeshPatch[] patches, Point3D p)
    {
        Vector3D? best = null; var bestDistance = double.PositiveInfinity;
        foreach (var patch in patches)
            for (var i = 0; i < patch.Positions.Count; i++)
            {
                var d = (patch.Positions[i] - p).Length;
                if (d < bestDistance) { bestDistance = d; best = patch.Normals[i]; }
            }
        return best;
    }

    private static Point3D At(IReadOnlyList<Point3D> line, double t)
    {
        var total = Length(line); var target = total * t; var run = 0d;
        for (var i = 1; i < line.Count; i++)
        {
            var step = (line[i] - line[i - 1]).Length;
            if (run + step >= target && step > 0) return line[i - 1] + (line[i] - line[i - 1]) * ((target - run) / step);
            run += step;
        }
        return line[^1];
    }

    private static double Length(IReadOnlyList<Point3D> line) => line.Zip(line.Skip(1)).Sum(p => (p.Second - p.First).Length);

    private static double Area(IEnumerable<DisplayFaceMeshPatch> patches) => patches.Sum(patch =>
        Enumerable.Range(0, patch.TriangleIndices.Count / 3).Sum(t =>
        {
            var a = patch.Positions[patch.TriangleIndices[3 * t]]; var b = patch.Positions[patch.TriangleIndices[3 * t + 1]]; var c = patch.Positions[patch.TriangleIndices[3 * t + 2]];
            return (b - a).Cross(c - a).Length / 2;
        }));

    private static bool IsConcave(Core.Geometry.Surfaces.CylinderSurface cylinder, DisplayFaceMeshPatch[] patches)
    {
        // Material outside the cylinder (a hole or cut wall): the outward face normal points to the axis.
        foreach (var patch in patches)
            for (var i = 0; i < patch.Positions.Count; i += Math.Max(1, patch.Positions.Count / 8))
            {
                var axis = cylinder.Axis.ToVector();
                var offset = patch.Positions[i] - cylinder.Origin;
                var radial = offset - axis * offset.Dot(axis);
                return patch.Normals[i].Dot(radial) < 0;
            }
        return false;
    }
}
