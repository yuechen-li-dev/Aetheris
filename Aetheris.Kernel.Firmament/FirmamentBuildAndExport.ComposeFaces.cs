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

    /// <summary>The cap selector spellings a Compose body admits in <c>Pmi</c>.</summary>
    internal static IEnumerable<string> ComposeOperationCapTargets(PrismaticProfileCompositionFeature feature) =>
        feature.Operations.Where(o => o.Intent == PrismaticProfileIntent.Base).SelectMany(o => new[] { $"face({o.Name}.Top)", $"face({o.Name}.Bottom)" })
            .Concat((feature.Bosses ?? []).Select(b => $"face({b.Name}.Top)"))
            .Concat((feature.Pockets ?? []).Select(p => $"face({p.Name}.Floor)"));
}
