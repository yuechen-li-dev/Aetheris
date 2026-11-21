using Aetheris.Geometry;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

internal static class SubdivisionStrokeReflection
{
    // The match is bounded in model millimetres. Every partner must be unique
    // and reciprocal. This tolerance does not authorize snapping the cage.
    private const double ToleranceMm = 1e-6;

    internal static Point3D Reflect(Point3D p, Plane3 plane) =>
        p - plane.Normal.ToVector() * (2 * plane.SignedDistance(p));

    internal static Dictionary<string, string> Match(IReadOnlyList<CageVertex> vertices, Plane3 plane)
    {
        var cells = vertices.ToLookup(v => Cell(v.Position));
        var matched = new Dictionary<string, string>(vertices.Count, StringComparer.Ordinal);
        foreach (var vertex in vertices)
        {
            var target = Reflect(vertex.Position, plane);
            var (x, y, z) = Cell(target);
            string? partner = null;
            for (var dx = -1; dx <= 1; dx++)
                for (var dy = -1; dy <= 1; dy++)
                    for (var dz = -1; dz <= 1; dz++)
                        foreach (var candidate in cells[(x + dx, y + dy, z + dz)])
                        {
                            if ((candidate.Position - target).Length > ToleranceMm) continue;
                            if (partner is not null && partner != candidate.Id)
                                throw Invalid(vertex.Id, "ambiguous");
                            partner = candidate.Id;
                        }
            matched.Add(vertex.Id, partner ?? throw Invalid(vertex.Id, "missing"));
        }
        foreach (var (id, partner) in matched)
            if (matched[partner] != id) throw Invalid(id, "nonreciprocal");
        return matched;

        static (double X, double Y, double Z) Cell(Point3D p) =>
            (Math.Floor(p.X / ToleranceMm), Math.Floor(p.Y / ToleranceMm), Math.Floor(p.Z / ToleranceMm));
        static InvalidOperationException Invalid(string id, string reason) =>
            new($"mesh-stroke-mirror-correspondence: Reflected partner for '{id}' is {reason} within {ToleranceMm:G} mm.");
    }

    internal static Dictionary<string, string> MatchCage(SubdivisionCage cage, IReadOnlyList<CageEdge> edges,
        IReadOnlySet<string> pinned, Plane3 plane)
    {
        var pairs = Match(cage.Vertices, plane);
        var rings = cage.Faces.Select(f => Ring(f.Vertices[0], f.Vertices[1], f.Vertices[2], f.Vertices[3])).ToHashSet();
        foreach (var face in cage.Faces)
        {
            var v = face.Vertices;
            if (!rings.Contains(Ring(pairs[v[0]], pairs[v[3]], pairs[v[2]], pairs[v[1]])))
                throw new InvalidOperationException("mesh-stroke-mirror-topology: Reflected cage quads must match with reversed winding.");
        }

        var sharp = cage.Creases.ToHashSet(StringComparer.Ordinal);
        foreach (var edge in edges.Where(e => sharp.Contains(e.Id)))
            if (!sharp.Contains(SubdivisionSculptor.EdgeId(pairs[edge.A], pairs[edge.B])))
                throw new InvalidOperationException("mesh-stroke-mirror-topology: Reflected crease edges must match.");
        if (pinned.Any(id => !pinned.Contains(pairs[id])))
            throw new InvalidOperationException("mesh-stroke-mirror-preserve: The rim and preserved regions must be symmetric across the plane.");
        return pairs;

        static (string, string, string, string) Ring(string a, string b, string c, string d)
        {
            var vertices = new[] { a, b, c, d };
            var first = 0;
            for (var i = 1; i < vertices.Length; i++)
                if (StringComparer.Ordinal.Compare(vertices[i], vertices[first]) < 0) first = i;
            return (vertices[first], vertices[(first + 1) % 4], vertices[(first + 2) % 4], vertices[(first + 3) % 4]);
        }
    }
}
