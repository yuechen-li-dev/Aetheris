using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament;

public static partial class FirmamentBuildAndExport
{
    /// <summary>
    /// PMI targets that name a Compose operation's planar cap: <c>face(op.Top)</c> for Base and
    /// Boss, <c>face(op.Bottom)</c> for Base and <c>face(op.Floor)</c> for Pocket. A cap is the
    /// planar face at the operation's level whose normal points out of the material. When more
    /// than one face sits at that level, only faces bounded by the operation's own profile walls
    /// are kept. A target that does not resolve to exactly one face maps to no faces, so the
    /// exporter's existing unresolved-target diagnostics reject it rather than guess.
    /// </summary>
    internal static IReadOnlyDictionary<string, int[]> ComposeOperationCapFaces(
        PrismaticProfileCompositionFeature feature, BrepBody body, SemanticTopologyCorrespondence? correspondence)
    {
        var caps = new List<(string Target, string Profile, double Level, bool Up)>();
        foreach (var operation in feature.Operations.Where(o => o.Intent == PrismaticProfileIntent.Base))
        {
            caps.Add(($"face({operation.Name}.Top)", operation.ProfileReference, operation.To, true));
            caps.Add(($"face({operation.Name}.Bottom)", operation.ProfileReference, operation.From, false));
        }
        foreach (var boss in feature.Bosses ?? []) caps.Add(($"face({boss.Name}.Top)", boss.ProfileReference, boss.To, true));
        foreach (var pocket in feature.Pockets ?? []) caps.Add(($"face({pocket.Name}.Floor)", pocket.ProfileReference, pocket.From, true));
        foreach (var offset in feature.MaterialOffsets ?? [])
            caps.Add(offset.Intent == PrismaticProfileIntent.Add ? ($"face({offset.Name}.Top)", offset.ProfileReference, offset.To, true)
                : ($"face({offset.Name}.Floor)", offset.ProfileReference, offset.From, true));
        // Plain Add and Remove operations: the top of added material, the floor of a removal that stops in material.
        foreach (var operation in feature.Operations.Where(o => o.Intent == PrismaticProfileIntent.Add && caps.All(c => c.Profile != o.ProfileReference) && o.SemanticFeatureId is null).ToArray())
        {
            // Added material shows its top, its bottom, or both (a lug hung below the stock shows only its bottom).
            caps.Add(($"face({operation.Name}.Top)", operation.ProfileReference, operation.To, true));
            caps.Add(($"face({operation.Name}.Bottom)", operation.ProfileReference, operation.From, false));
        }
        foreach (var operation in feature.Operations.Where(o => o.Intent == PrismaticProfileIntent.Remove && caps.All(c => c.Profile != o.ProfileReference) && o.SemanticFeatureId is null).ToArray())
            caps.Add(($"face({operation.Name}.Floor)", operation.ProfileReference, operation.From, true));

        var result = new Dictionary<string, int[]>(StringComparer.Ordinal);
        var (origin, axis) = ComposeAxis(feature);
        foreach (var (target, profile, level, up) in caps)
        {
            var candidates = body.Topology.Faces.Where(face => body.TryGetFaceSurfaceGeometry(face.Id, out var surface)
                    && surface?.Plane is { } plane
                    && Math.Abs(plane.Normal.ToVector().Dot(axis) - (up ? 1 : -1)) < 1e-8
                    && Math.Abs((plane.Origin - origin).Dot(axis) - level) < 1e-7)
                .Select(face => face.Id).ToArray();
            if (candidates.Length > 1 && correspondence is not null)
            {
                // Profile walls carry their source profile; a cap belongs to the operation whose walls bound it.
                var wallFaces = Walls(body, correspondence, profile);
                candidates = candidates.Where(face => Neighbours(body, face).Any(wallFaces.Contains)).ToArray();
            }
            result[target] = candidates.Length == 1 ? [candidates[0].Value] : [];
        }
        return result;
    }

    /// <summary>The composition's build direction and the point its levels are measured from.</summary>
    private static (Aetheris.Kernel.Core.Math.Point3D Origin, Aetheris.Kernel.Core.Math.Vector3D Axis) ComposeAxis(PrismaticProfileCompositionFeature feature) =>
        feature.Placement.TryResolveConstructionPlane(out var frame, out _) && frame is not null
            ? (frame.Origin, frame.AxisZ.ToVector()) : (Aetheris.Kernel.Core.Math.Point3D.Origin, new Aetheris.Kernel.Core.Math.Vector3D(0, 0, 1));

    private static IEnumerable<int> Neighbours(BrepBody body, FaceId face)
    {
        var edges = body.Topology.GetFace(face).LoopIds.SelectMany(id => body.Topology.GetLoop(id).CoedgeIds)
            .Select(id => body.Topology.GetCoedge(id).EdgeId).ToHashSet();
        return body.Topology.Faces.Where(other => other.Id != face && other.LoopIds
                .SelectMany(id => body.Topology.GetLoop(id).CoedgeIds)
                .Any(id => edges.Contains(body.Topology.GetCoedge(id).EdgeId)))
            .Select(other => other.Id.Value);
    }

    /// <summary>The cap selectors, dimensionable parameters (<c>param:</c>) and locatable features
    /// (<c>op:</c>) a Compose body admits in <c>Pmi</c>.</summary>
    internal static IEnumerable<string> ComposeOperationCapTargets(PrismaticProfileCompositionFeature feature)
    {
        var targets = CapTargets(feature).ToArray();
        // Every operation that names a cap also owns the faces derived from it (face(stock.TopInside)).
        return targets.Concat(targets.Where(t => t.StartsWith("face(", StringComparison.Ordinal)).Select(t => "face-owner:" + t[5..t.IndexOf('.')]).Distinct());
    }

    private static IEnumerable<string> CapTargets(PrismaticProfileCompositionFeature feature) =>
        feature.Operations.Where(o => o.Intent == PrismaticProfileIntent.Base).SelectMany(o => new[] { $"face({o.Name}.Top)", $"face({o.Name}.Bottom)",
                $"param:{o.Name}.Height", $"param:{o.Name}.Length", $"param:{o.Name}.Width" })
            .Concat((feature.Bosses ?? []).SelectMany(b => new[] { $"face({b.Name}.Top)", $"op:{b.Name}",
                $"param:{b.Name}.Height", $"param:{b.Name}.Diameter", $"param:{b.Name}.Length", $"param:{b.Name}.Width", $"param:{b.Name}.Radius" }))
            .Concat((feature.MaterialOffsets ?? []).Select(o => o.Intent == PrismaticProfileIntent.Add ? $"face({o.Name}.Top)" : $"face({o.Name}.Floor)"))
            .Concat(feature.Operations.Where(o => o.Intent == PrismaticProfileIntent.Add && o.SemanticFeatureId is null).SelectMany(o => new[] { $"face({o.Name}.Top)", $"face({o.Name}.Bottom)" }))
            .Concat(feature.Operations.Where(o => o.Intent == PrismaticProfileIntent.Remove && o.SemanticFeatureId is null).Select(o => $"face({o.Name}.Floor)"))
            .Concat((feature.Pockets ?? []).SelectMany(p => new[] { $"face({p.Name}.Floor)", $"op:{p.Name}",
                $"param:{p.Name}.Depth", $"param:{p.Name}.Diameter", $"param:{p.Name}.Length", $"param:{p.Name}.Width", $"param:{p.Name}.Radius" }));
}

/// <summary>A construction parameter projected into PMI: its value and the faces it measures.</summary>
internal sealed record ComposeProjection(string Kind, double Value, int[] Faces, (int[] From, int[] To)? Ends = null, int Count = 1);

public static partial class FirmamentBuildAndExport
{
    /// <summary>
    /// Resolve <c>op.Param</c> on a composed body. Values come from the built geometry (walls,
    /// caps, arcs), so a projected dimension cannot disagree with what was made.
    /// Unknown or unmeasurable parameters return null and the PMI record rejects.
    /// </summary>
    internal static ComposeProjection? ProjectComposeParameter(PrismaticProfileCompositionFeature feature, BrepBody body,
        SemanticTopologyCorrespondence? correspondence, string target)
    {
        var dot = target.LastIndexOf('.');
        if (dot <= 0) return null;
        var owner = target[..dot]; var parameter = target[(dot + 1)..];
        var baseOp = feature.Operations.FirstOrDefault(o => o.Intent == PrismaticProfileIntent.Base && o.Name == owner);
        var boss = (feature.Bosses ?? []).FirstOrDefault(b => b.Name == owner);
        var pocket = (feature.Pockets ?? []).FirstOrDefault(p => p.Name == owner);
        if (baseOp is null && boss is null && pocket is null) return ProjectEdgeFinish(body, parameter);
        var profile = baseOp?.ProfileReference ?? boss?.ProfileReference ?? pocket!.ProfileReference;
        var (from, to) = baseOp is not null ? (baseOp.From, baseOp.To) : boss is not null ? (boss.From, boss.To) : (pocket!.From, pocket.To);
        var walls = Walls(body, correspondence, profile);
        int[] Caps(double level, bool up, bool adjacent) => body.Topology.Faces.Where(f => body.TryGetFaceSurfaceGeometry(f.Id, out var s)
                && s?.Plane is { } plane && Math.Abs(plane.Normal.ToVector().Z - (up ? 1 : -1)) < 1e-8 && Math.Abs(plane.Origin.Z - level) < 1e-7
                && (!adjacent || Neighbours(body, f.Id).Any(walls.Contains)))
            .Select(f => f.Id.Value).ToArray();
        switch (parameter)
        {
            case "Height" when baseOp is not null:
            {
                var bottom = Caps(from, false, false); var top = Caps(to, true, false);
                return bottom.Length == 0 || top.Length == 0 ? null : new("thickness", to - from, [.. bottom, .. top], (bottom, top));
            }
            case "Height" when boss is not null:
            {
                var support = Caps(from, true, true); var top = Caps(to, true, true);
                return support.Length == 0 || top.Length == 0 ? null : new("height", to - from, [.. support, .. top], (support, top));
            }
            case "Depth" when pocket is not null:
            {
                var rim = Caps(to, true, true); var floor = Caps(from, true, true);
                return rim.Length == 0 || floor.Length == 0 ? null : new("depth", to - from, [.. rim, .. floor], (rim, floor));
            }
            case "Length" or "Width":
            {
                // Length runs along X and Width along Y, between the profile's opposite straight walls.
                var axis = parameter == "Length" ? new Aetheris.Kernel.Core.Math.Vector3D(1, 0, 0) : new Aetheris.Kernel.Core.Math.Vector3D(0, 1, 0);
                var planar = walls.Select(id => (Id: id, Plane: body.GetFaceSurface(new FaceId(id)).Plane))
                    .Where(w => w.Plane is { } p && Math.Abs(Math.Abs(p.Normal.ToVector().Dot(axis)) - 1) < 1e-8)
                    .Select(w => (w.Id, Offset: (w.Plane!.Value.Origin - Aetheris.Kernel.Core.Math.Point3D.Origin).Dot(axis))).ToArray();
                if (planar.Length < 2) return null;
                var min = planar.Min(p => p.Offset); var max = planar.Max(p => p.Offset);
                var low = planar.Where(p => Math.Abs(p.Offset - min) < 1e-7).Select(p => p.Id).ToArray();
                var high = planar.Where(p => Math.Abs(p.Offset - max) < 1e-7).Select(p => p.Id).ToArray();
                return max - min < 1e-9 ? null : new(parameter.ToLowerInvariant(), max - min, [.. low, .. high], (low, high));
            }
            case "Radius" or "Diameter":
            {
                var arcs = walls.Select(id => (Id: id, Cylinder: body.GetFaceSurface(new FaceId(id)).Cylinder)).Where(w => w.Cylinder is not null).ToArray();
                if (arcs.Length == 0) return null;
                var radii = arcs.Select(a => Math.Round(a.Cylinder!.Value.Radius, 9)).Distinct().ToArray();
                if (radii.Length != 1) return null;
                var axes = arcs.Select(a => (Math.Round(a.Cylinder!.Value.Origin.X, 6), Math.Round(a.Cylinder!.Value.Origin.Y, 6))).Distinct().Count();
                // A diameter needs one full round wall; a radius is the corner arcs, counted per corner.
                if (parameter == "Diameter" && (axes != 1 || arcs.Any(a => body.GetFaceSurface(new FaceId(a.Id)).Cylinder is null))
                    || parameter == "Diameter" && arcs.Length != walls.Count) return null;
                return parameter == "Diameter" ? new("diameter", 2 * radii[0], arcs.Select(a => a.Id).ToArray())
                    : new("radius", radii[0], arcs.Select(a => a.Id).ToArray(), Count: axes);
            }
        }
        return null;
    }

    /// <summary>An equal-distance chamfer: its distance from the inclined face's vertical extent and angle.</summary>
    private static ComposeProjection? ProjectEdgeFinish(BrepBody body, string parameter)
    {
        if (parameter != "Distance") return null;
        var faces = body.Topology.Faces.Select(f => (f.Id, Plane: body.TryGetFaceSurfaceGeometry(f.Id, out var s) ? s?.Plane : null))
            .Where(f => f.Plane is { } p && Math.Abs(p.Normal.ToVector().Z) is > .05 and < .95).ToArray();
        if (faces.Length == 0) return null;
        var normal = faces[0].Plane!.Value.Normal.ToVector();
        if (Math.Abs(Math.Abs(normal.Z) - Math.Sqrt(.5)) > 1e-6) return null; // only 45° chamfers are measured here
        var points = body.Topology.GetFace(faces[0].Id).LoopIds.SelectMany(id => body.Topology.GetLoop(id).CoedgeIds)
            .Select(id => body.Topology.GetEdge(body.Topology.GetCoedge(id).EdgeId))
            .SelectMany(e => new[] { e.StartVertexId, e.EndVertexId }).Distinct()
            .Select(v => body.TryGetVertexPoint(v, out var p) ? p.Z : double.NaN).Where(double.IsFinite).ToArray();
        return points.Length < 2 ? null : new("chamfer", points.Max() - points.Min(), faces.Select(f => f.Id.Value).ToArray());
    }

    private static HashSet<int> Walls(BrepBody body, SemanticTopologyCorrespondence? correspondence, string profile) =>
        (correspondence?.Descendants ?? [])
            .Where(d => d.Face.HasValue && d.Role == SemanticTopologyRole.ExtrusionSideFace
                && (d.SourceStableId.Contains("_" + profile + "_", StringComparison.Ordinal) || d.SourceStableId.StartsWith("profile:" + profile + ".", StringComparison.Ordinal)))
            .Select(d => d.Face!.Value.Value).ToHashSet();

    /// <summary>
    /// A location from an axis face (its extreme support plane) to a hole axis or to the
    /// mid-plane of a boss or pocket between its walls facing that direction.
    /// </summary>
    internal static ComposeProjection? ProjectComposeLocation(PrismaticProfileCompositionFeature feature, BrepBody body,
        SemanticTopologyCorrespondence? correspondence, IReadOnlyDictionary<string, string> holeFeatures, string solid, string from, string to, bool basic)
    {
        var port = from switch { "face(+X)" => "plus_x_face", "face(-X)" => "minus_x_face", "face(+Y)" => "plus_y_face", "face(-Y)" => "minus_y_face", _ => null };
        if (port is null) return null;
        var datum = Execution.FirmamentSelectorResolver.ResolveSupportPlaneFaces(body, $"{solid}.{port}").Select(f => f.Value).ToArray();
        if (datum.Length == 0) return null;
        var plane = body.GetFaceSurface(new FaceId(datum[0])).Plane!.Value;
        var n = plane.Normal.ToVector();
        var origin = (plane.Origin - Aetheris.Kernel.Core.Math.Point3D.Origin).Dot(n);
        int[] targetFaces; double coordinate;
        if (holeFeatures.TryGetValue(to, out var featureId))
        {
            targetFaces = (correspondence?.Descendants ?? []).Where(d => d.Face.HasValue && (d.SourceStableId == featureId || d.ParentStableId == featureId)
                    && d.Role is not (SemanticTopologyRole.HoleCounterboreWallFace or SemanticTopologyRole.HoleCountersinkWallFace or SemanticTopologyRole.HoleCounterboreShoulderFace or SemanticTopologyRole.HoleDrillPointFace))
                .Select(d => d.Face!.Value.Value).Distinct().ToArray();
            var cylinder = targetFaces.Select(id => body.GetFaceSurface(new FaceId(id)).Cylinder).FirstOrDefault(c => c is not null);
            if (cylinder is null || Math.Abs(cylinder.Value.Axis.ToVector().Dot(n)) > 1e-8) return null;
            coordinate = (cylinder.Value.Origin - Aetheris.Kernel.Core.Math.Point3D.Origin).Dot(n);
        }
        else
        {
            var profile = (feature.Bosses ?? []).FirstOrDefault(b => b.Name == to)?.ProfileReference ?? (feature.Pockets ?? []).FirstOrDefault(p => p.Name == to)?.ProfileReference;
            if (profile is null) return null;
            var walls = Walls(body, correspondence, profile);
            var facing = walls.Select(id => (Id: id, Plane: body.GetFaceSurface(new FaceId(id)).Plane))
                .Where(w => w.Plane is { } p && Math.Abs(Math.Abs(p.Normal.ToVector().Dot(n)) - 1) < 1e-8).ToArray();
            if (facing.Length < 2)
            {
                // A round boss or pocket is located by its axis.
                var round = walls.Select(id => body.GetFaceSurface(new FaceId(id)).Cylinder).Where(c => c is not null).Select(c => c!.Value).ToArray();
                if (round.Length == 0 || round.Select(c => Math.Round((c.Origin - Aetheris.Kernel.Core.Math.Point3D.Origin).Dot(n), 7)).Distinct().Count() != 1) return null;
                var axisName0 = from[5..^1];
                return new($"location {axisName0}{(basic ? " basic" : "")}", Math.Abs((round[0].Origin - Aetheris.Kernel.Core.Math.Point3D.Origin).Dot(n) - origin),
                    [.. datum, .. walls], (datum, walls.ToArray()));
            }
            var offsets = facing.Select(w => (w.Plane!.Value.Origin - Aetheris.Kernel.Core.Math.Point3D.Origin).Dot(n)).ToArray();
            coordinate = (offsets.Min() + offsets.Max()) / 2; // to the feature's centre plane
            targetFaces = facing.Select(w => w.Id).ToArray();
        }
        var axisName = from[5..^1];
        return new($"location {axisName}{(basic ? " basic" : "")}", Math.Abs(coordinate - origin), [.. datum, .. targetFaces], (datum, targetFaces));
    }
}

public static partial class FirmamentBuildAndExport
{
    /// <summary>
    /// Gives every face of a composed body that the construction can name a stable selector, so
    /// inspection, picking and PMI address it the same way:
    /// <list type="bullet">
    /// <item>a profile side split by section levels keeps one <c>face(Body.Side("…"))</c> when its pieces are coplanar;</item>
    /// <item>a curved side made of one face is named like a planar one;</item>
    /// <item>hole walls are <c>face(H.Wall)</c>, counterbore and countersink entries <c>face(H.Counterbore)</c> and
    /// <c>face(H.Countersink)</c>, and a counterbore's step <c>face(H.Shoulder)</c>;</item>
    /// <item>operation caps are <c>face(op.Top)</c>, <c>face(op.Bottom)</c> and <c>face(pocket.Floor)</c>;</item>
    /// <item>a feature that owns a profile side (a pocket, boss or slot wall) carries the side's name;</item>
    /// <item>a hollow body's cavity floors and ceilings are the insides of the caps they follow: <c>face(stock.TopInside)</c>.</item>
    /// </list>
    /// Faces nothing names stay runtime-only. Selectors are added, never moved: a face that already has one keeps it.
    /// </summary>
    internal static SemanticTopologyCorrespondence NameComposeFaces(SemanticTopologyCorrespondence correspondence, BrepBody body,
        PrismaticProfileCompositionFeature feature, string? finishName = null)
    {
        var list = correspondence.Descendants.ToList();
        var live = body.Topology.Faces.Select(f => f.Id).ToHashSet();
        void Name(int index, string selector) => list[index] = list[index] with { FirmamentSelector = selector, Addressability = SemanticTopologyAddressability.DerivedStable };
        Aetheris.Kernel.Core.Geometry.Surfaces.PlaneSurface? PlaneOf(FaceId face) =>
            body.TryGetFaceSurfaceGeometry(face, out var surface) ? surface?.Plane : null;
        // Faces on one surface: one plane, or one cylinder (same axis line and radius).
        bool SameSurface(FaceId a, FaceId b)
        {
            if (!body.TryGetFaceSurfaceGeometry(a, out var x) || !body.TryGetFaceSurfaceGeometry(b, out var y) || x is null || y is null) return false;
            if (x.Plane is { } p && y.Plane is { } q)
            {
                var n = p.Normal.ToVector();
                return q.Normal.ToVector().Dot(n) > 1 - 1e-9 && Math.Abs((q.Origin - p.Origin).Dot(n)) < 1e-7;
            }
            if (x.Cylinder is { } c && y.Cylinder is { } e)
            {
                var axis = c.Axis.ToVector(); var d = e.Origin - c.Origin;
                return Math.Abs(Math.Abs(e.Axis.ToVector().Dot(axis)) - 1) < 1e-9 && Math.Abs(c.Radius - e.Radius) < 1e-7 && (d - axis * d.Dot(axis)).Length < 1e-7;
            }
            return false;
        }
        // A wall runs along the build direction: a plane square to it, or a cylinder whose axis follows it.
        var build = ComposeAxis(feature).Axis;
        bool Upright(FaceId face) => body.TryGetFaceSurfaceGeometry(face, out var surface) && surface is not null
            && (surface.Plane is { } p ? Math.Abs(p.Normal.ToVector().Dot(build)) < 1e-9 : surface.Cylinder is { } c ? Math.Abs(Math.Abs(c.Axis.ToVector().Dot(build)) - 1) < 1e-9 : false);

        // Profile sides: one name per source for its upright faces when they lie on one surface. A finish that cuts the
        // side leaves a second, inclined surface on the same source: that is the finish's side.
        var planPrefix = $"plan:{feature.Name}:";
        foreach (var group in list.Select((d, i) => (D: d, I: i))
                     .Where(x => x.D.Face is { } f && live.Contains(f) && x.D.Role == SemanticTopologyRole.ExtrusionSideFace
                         && x.D.StableId.StartsWith(planPrefix, StringComparison.Ordinal))
                     .GroupBy(x => x.D.SourceStableId, StringComparer.Ordinal))
        {
            var faces = group.Select(x => x.D.Face!.Value).Distinct().ToArray();
            var source = System.Text.Json.JsonSerializer.Serialize(group.Key);
            var upright = faces.Where(Upright).ToArray(); var inclined = faces.Except(upright).ToArray();
            void NameAll(IReadOnlyList<FaceId> members, string selector)
            {
                if (members.Count == 0 || members.Any(f => !SameSurface(members[0], f))) return;
                foreach (var item in group.Where(x => members.Contains(x.D.Face!.Value) && x.D.FirmamentSelector is null)) Name(item.I, selector);
            }
            if (faces.Length == 1) { NameAll(faces, $"face({feature.Name}.Side({source}))"); continue; }
            NameAll(upright, $"face({feature.Name}.Side({source}))");
            if (finishName is not null) NameAll(inclined, $"face({finishName}.Side({source}))");
        }

        // Holes: the feature names its walls; a counterbore's step is the face between its two walls.
        var holeNames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var hole in feature.ShaftHoles ?? []) holeNames[hole.StableId] = hole.Name;
        foreach (var hole in feature.CounterboreHoles ?? []) holeNames[hole.StableId] = hole.Name;
        foreach (var hole in feature.CountersinkHoles ?? []) holeNames[hole.StableId] = hole.Name;
        foreach (var drill in feature.ConstructionPlaneBlindDrills ?? []) { holeNames[drill.StableId] = drill.Name; holeNames[$"hole:{feature.Name}.{drill.Name}"] = drill.Name; }
        for (var i = 0; i < list.Count; i++)
        {
            var d = list[i];
            if (d.Face is not { } face || !live.Contains(face) || d.IsTopologyAlias || d.FirmamentSelector is not null
                || !holeNames.TryGetValue(d.SourceStableId, out var hole)) continue;
            var port = d.Role switch
            {
                SemanticTopologyRole.HoleWallFace or SemanticTopologyRole.HoleCounterboreShaftWallFace => "Wall",
                SemanticTopologyRole.HoleCounterboreWallFace => "Counterbore",
                SemanticTopologyRole.HoleCountersinkWallFace => "Countersink",
                SemanticTopologyRole.HoleDrillPointFace => "Tip",
                _ => null
            };
            if (port is not null) Name(i, $"face({hole}.{port})");
        }
        foreach (var hole in feature.CounterboreHoles ?? [])
        {
            HashSet<int> Walls(SemanticTopologyRole role) => list.Where(d => d.SourceStableId == hole.StableId && d.Role == role && d.Face.HasValue)
                .Select(d => d.Face!.Value.Value).ToHashSet();
            var bore = Walls(SemanticTopologyRole.HoleCounterboreWallFace); var shaft = Walls(SemanticTopologyRole.HoleCounterboreShaftWallFace);
            var shoulder = body.Topology.Faces.Where(f => PlaneOf(f.Id) is not null && !bore.Contains(f.Id.Value) && !shaft.Contains(f.Id.Value))
                .Select(f => f.Id).Where(f => { var n = Neighbours(body, f).ToArray(); return n.Any(bore.Contains) && n.Any(shaft.Contains); }).ToArray();
            if (shoulder.Length == 1) Claim(shoulder[0], $"face({hole.Name}.Shoulder)", hole.StableId, SemanticTopologyRole.HoleCounterboreShoulderFace);
        }

        // A feature that owns a named profile side carries that name.
        for (var i = 0; i < list.Count; i++)
        {
            var d = list[i];
            if (d.Face is not { } face || d.IsTopologyAlias || d.FirmamentSelector is not null) continue;
            var alias = list.FirstOrDefault(a => a.IsTopologyAlias && a.Face == face && a.FirmamentSelector is not null);
            if (alias is not null) Name(i, alias.FirmamentSelector!);
        }

        // Caps of Base, Boss and Pocket operations.
        var sources = feature.Operations.Where(o => o.Intent == PrismaticProfileIntent.Base)
            .ToDictionary(o => o.Name, o => o.SemanticFeatureId ?? $"compose:{feature.Name}:{o.Name}", StringComparer.Ordinal);
        foreach (var boss in feature.Bosses ?? []) sources[boss.Name] = boss.StableId;
        foreach (var pocket in feature.Pockets ?? []) sources[pocket.Name] = pocket.StableId;
        foreach (var offset in feature.MaterialOffsets ?? []) sources[offset.Name] = offset.StableId;
        var interim = correspondence with { Descendants = list.ToArray() };
        foreach (var (target, faces) in ComposeOperationCapFaces(feature, body, interim))
        {
            if (faces.Length != 1) continue;
            var owner = target[5..target.IndexOf('.')];
            Claim(new FaceId(faces[0]), target, sources.GetValueOrDefault(owner, $"compose:{feature.Name}:{owner}"), SemanticTopologyRole.Unknown);
        }
        // A hollow body's cavity follows the exterior at constant wall: each cavity floor or ceiling is the inside of the
        // nearest exterior cap facing the other way, and takes its name with "Inside" added (face(stock.TopInside)).
        var cavityWalls = list.Where(d => d.Face.HasValue && d.SourceStableId.StartsWith("hollow:", StringComparison.Ordinal)).Select(d => d.Face!.Value.Value).ToHashSet();
        if (cavityWalls.Count > 0)
        {
            var (start, _) = ComposeAxis(feature);
            double Level(Aetheris.Kernel.Core.Geometry.Surfaces.PlaneSurface p) => (p.Origin - start).Dot(build);
            var capsNamed = list.Where(d => d.Face is { } f && !d.IsTopologyAlias && d.FirmamentSelector is { } sel
                    && (sel.EndsWith(".Top)", StringComparison.Ordinal) || sel.EndsWith(".Bottom)", StringComparison.Ordinal) || sel.EndsWith(".Floor)", StringComparison.Ordinal)) && PlaneOf(f) is not null)
                .Select(d => (Selector: d.FirmamentSelector!, Source: d.SourceStableId, Plane: PlaneOf(d.Face!.Value)!.Value)).ToArray();
            var inner = body.Topology.Faces.Where(f => PlaneOf(f.Id) is { } p && Math.Abs(Math.Abs(p.Normal.ToVector().Dot(build)) - 1) < 1e-8
                    && Neighbours(body, f.Id).ToArray() is { Length: > 0 } n && n.All(cavityWalls.Contains))
                .Select(f => (Face: f.Id, Plane: PlaneOf(f.Id)!.Value)).ToArray();
            var matches = inner.Select(c =>
            {
                var up = c.Plane.Normal.ToVector().Dot(build) > 0; var level = Level(c.Plane);
                var outside = capsNamed.Where(x => x.Plane.Normal.ToVector().Dot(build) > 0 != up && (up ? Level(x.Plane) < level : Level(x.Plane) > level))
                    .OrderBy(x => Math.Abs(Level(x.Plane) - level)).ToArray();
                return (c.Face, Cap: outside.Length == 0 ? default : outside[0], Gap: outside.Length == 0 ? double.NaN : Math.Abs(Level(outside[0].Plane) - level));
            }).ToArray();
            if (matches.Length > 0 && matches.All(m => double.IsFinite(m.Gap) && Math.Abs(m.Gap - matches[0].Gap) < 1e-7)
                && matches.Select(m => m.Cap.Selector).Distinct().Count() == matches.Length)
                foreach (var (face, cap, _) in matches) Claim(face, cap.Selector[..^1] + "Inside)", cap.Source, SemanticTopologyRole.Unknown);
        }
        return correspondence with { Descendants = list.ToArray() };

        // Names a face: on its owner when it has one, else as a new owner. A face is never owned twice.
        void Claim(FaceId face, string selector, string source, SemanticTopologyRole role)
        {
            var owner = list.FindIndex(d => d.Face == face && !d.IsTopologyAlias);
            if (owner >= 0) { if (list[owner].FirmamentSelector is null) Name(owner, selector); return; }
            list.Add(new($"compose:{feature.Name}:face:{face.Value}", "Face", role, source, Face: face, ParentStableId: source,
                FirmamentSelector: selector, Addressability: SemanticTopologyAddressability.DerivedStable));
        }
    }
}
