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

        var result = new Dictionary<string, int[]>(StringComparer.Ordinal);
        foreach (var (target, profile, level, up) in caps)
        {
            var candidates = body.Topology.Faces.Where(face => body.TryGetFaceSurfaceGeometry(face.Id, out var surface)
                    && surface?.Plane is { } plane
                    && Math.Abs(plane.Normal.ToVector().Z - (up ? 1 : -1)) < 1e-8
                    && Math.Abs(plane.Origin.Z - level) < 1e-7)
                .Select(face => face.Id).ToArray();
            if (candidates.Length > 1 && correspondence is not null)
            {
                // Profile walls carry their source profile; a cap belongs to the operation whose walls bound it.
                var wallFaces = correspondence.Descendants
                    .Where(d => d.Face.HasValue && d.Role == SemanticTopologyRole.ExtrusionSideFace
                        && d.SourceStableId.Contains("_" + profile + "_", StringComparison.Ordinal))
                    .Select(d => d.Face!.Value.Value).ToHashSet();
                candidates = candidates.Where(face => Neighbours(body, face).Any(wallFaces.Contains)).ToArray();
            }
            result[target] = candidates.Length == 1 ? [candidates[0].Value] : [];
        }
        return result;
    }

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
    internal static IEnumerable<string> ComposeOperationCapTargets(PrismaticProfileCompositionFeature feature) =>
        feature.Operations.Where(o => o.Intent == PrismaticProfileIntent.Base).SelectMany(o => new[] { $"face({o.Name}.Top)", $"face({o.Name}.Bottom)",
                $"param:{o.Name}.Height", $"param:{o.Name}.Length", $"param:{o.Name}.Width" })
            .Concat((feature.Bosses ?? []).SelectMany(b => new[] { $"face({b.Name}.Top)", $"op:{b.Name}",
                $"param:{b.Name}.Height", $"param:{b.Name}.Diameter", $"param:{b.Name}.Length", $"param:{b.Name}.Width", $"param:{b.Name}.Radius" }))
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
                    && d.Role is not (SemanticTopologyRole.HoleCounterboreWallFace or SemanticTopologyRole.HoleCountersinkWallFace))
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
