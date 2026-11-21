using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

public sealed record RecessCageRegion(string Id, double CenterX, double CenterY, double LengthMm, double WidthMm,
    double FloorMm, double LipMm) : MeshSculptOperation(Id);
public sealed record MeshRecessEvidence(string LipRegion, int ReplacedFaces, int BoundaryEdges, double FloorMm, double LipMm);

internal static class SubdivisionRecess
{
    internal static (SubdivisionCage Cage, IReadOnlyDictionary<string, string[]> Regions, MeshRecessEvidence Evidence)
        Apply(MeshSculptState state, RecessCageRegion recess, HashSet<string> pinned)
    {
        if (!new[] { recess.CenterX, recess.CenterY, recess.LengthMm, recess.WidthMm, recess.FloorMm, recess.LipMm }.All(double.IsFinite)
            || recess.LengthMm <= 0 || recess.WidthMm <= 0 || recess.LipMm <= 0)
            throw new InvalidOperationException("mesh-recess-size-invalid: Center, size, floor and lip must be finite, with positive size and lip.");
        var cage = state.Cage;
        var old = cage.Vertices.ToDictionary(v => v.Id, StringComparer.Ordinal);
        var rim = state.SourceRim!;
        // Stay on the upper connected sheet, above the entire parting loop.
        var rimHeight = rim.Vertices.Max(id => old[id].Position.Z);
        if (recess.FloorMm <= rimHeight)
            throw new InvalidOperationException("mesh-recess-floor-invalid: The floor must stay above the locked rim.");
        var selected = new HashSet<int>();
        for (var i = 0; i < cage.Faces.Count; i++)
        {
            var points = cage.Faces[i].Vertices.Select(id => old[id].Position).ToArray();
            if (points.Any(p => p.Z <= rimHeight)) continue;
            var normal = (points[2] - points[0]).Cross(points[3] - points[1]);
            if (normal.Z <= 0) continue;
            if (Math.Abs(points.Average(p => p.X) - recess.CenterX) <= recess.LengthMm / 2 &&
                Math.Abs(points.Average(p => p.Y) - recess.CenterY) <= recess.WidthMm / 2)
            {
                if ((points[1] - points[0]).Cross(points[2] - points[0]).Z <= 0 ||
                    (points[2] - points[0]).Cross(points[3] - points[0]).Z <= 0)
                    throw new InvalidOperationException("mesh-recess-projection-foldover: The selected face folds over in the recess plane.");
                selected.Add(i);
            }
        }
        if (selected.Count == 0)
            throw new InvalidOperationException("mesh-recess-empty: The footprint selects no upward cage faces above the rim.");

        var patch = selected.Order().Select(i => cage.Faces[i]).ToArray();
        var vertices = patch.SelectMany(f => f.Vertices).ToHashSet(StringComparer.Ordinal);
        var allEdges = state.Edges;
        var sharp = cage.Creases.ToHashSet(StringComparer.Ordinal);
        var patchEdges = allEdges.Where(e => e.Faces.Any(selected.Contains)).ToArray();
        if (vertices.Overlaps(pinned) || patchEdges.Any(e => sharp.Contains(e.Id)))
            throw new InvalidOperationException("mesh-recess-protected: The patch meets a protected region or crease.");
        var boundary = patchEdges.Where(e => !e.Faces.All(selected.Contains)).Select(e =>
        {
            var face = cage.Faces[e.Faces.Single(selected.Contains)];
            var a = Enumerable.Range(0, 4).Single(i => face.Vertices[i] == e.A);
            return face.Vertices[(a + 1) % 4] == e.B ? (e.A, e.B) : (e.B, e.A);
        }).ToArray();
        if (vertices.Count - patchEdges.Length + patch.Length != 1 || !HasSingleLoop(boundary))
            throw new InvalidOperationException("mesh-recess-patch-invalid: The footprint must select one disk of faces with a single boundary loop.");
        var ring = boundary.Select(e => e.Item1).ToHashSet(StringComparer.Ordinal);
        if (ring.Any(id => old[id].Position.Z <= recess.FloorMm + 2 * recess.LipMm))
            throw new InvalidOperationException("mesh-recess-lip-invalid: The cut boundary needs clearance for both wall supports above the floor.");
        var removed = vertices.Where(id => !ring.Contains(id)).ToHashSet(StringComparer.Ordinal);
        if (state.AnchorVertices?.Values.Any(removed.Contains) == true)
            throw new InvalidOperationException("mesh-recess-anchor-removed: The patch would remove an authored surface anchor.");

        var lipName = recess.Id;
        if (state.VertexRegions!.ContainsKey(lipName))
            throw new InvalidOperationException($"mesh-recess-region-duplicate: Region '{lipName}' already exists.");
        string Id(string id, string layer) => $"{lipName}_{layer}_{id}";
        var faces = cage.Faces.Where((_, index) => !selected.Contains(index)).ToList();
        var nextVertices = cage.Vertices.Where(v => !removed.Contains(v.Id)).ToList();
        foreach (var source in cage.Vertices.Where(v => vertices.Contains(v.Id)))
        {
            var p = source.Position;
            nextVertices.Add(new(Id(source.Id, "floor"), new(p.X, p.Y, recess.FloorMm), [source.Id]));
            if (!ring.Contains(source.Id)) continue;
            nextVertices.Add(new(Id(source.Id, "lip"), new(p.X, p.Y, p.Z - recess.LipMm), [source.Id]));
            nextVertices.Add(new(Id(source.Id, "wall"), new(p.X, p.Y, recess.FloorMm + recess.LipMm), [source.Id]));
        }
        foreach (var face in patch)
            faces.Add(new(Id(face.Id, "floorFace"), face.Vertices.Select(id => Id(id, "floor")).ToArray(), []));
        foreach (var (a, b) in boundary)
        {
            faces.Add(new(Id(a, "lipFace"), [a, b, Id(b, "lip"), Id(a, "lip")], []));
            faces.Add(new(Id(a, "wallFace"), [Id(a, "lip"), Id(b, "lip"), Id(b, "wall"), Id(a, "wall")], []));
            faces.Add(new(Id(a, "footFace"), [Id(a, "wall"), Id(b, "wall"), Id(b, "floor"), Id(a, "floor")], []));
        }

        var regions = state.VertexRegions.ToDictionary(pair => pair.Key,
            pair => pair.Value.Where(id => !removed.Contains(id)).ToArray(), StringComparer.Ordinal);
        regions.Add(lipName, boundary.SelectMany(e => new[] { e.Item1, Id(e.Item1, "lip") }).ToArray());
        var result = cage with { Faces = faces.ToArray(), Vertices = nextVertices.ToArray() };
        return (result, regions, new(lipName, patch.Length, boundary.Length, recess.FloorMm, recess.LipMm));

        static bool HasSingleLoop((string From, string To)[] edges)
        {
            if (edges.Length < 4) return false;
            var next = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (from, to) in edges)
                if (!next.TryAdd(from, to)) return false;
            var current = edges[0].From;
            for (var i = 0; i < edges.Length; i++)
            {
                if (!next.Remove(current, out var to)) return false;
                current = to;
            }
            return current == edges[0].From;
        }
    }
}
