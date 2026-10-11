using System.Collections.Immutable;
using System.Numerics;

namespace Aetheris.Cloth3D;

/// <summary>Better Bending Eq.39-40: each row of (L + N) / sqrt(barycentric area).
/// The normal derivative N cancels the spurious membrane force on free boundaries.
/// Rest-flat QB(PL), not an intrinsic triangulation or a director shell.</summary>
internal static class ClothVertexBending3D
{
    public static IEnumerable<ClothConstraint3D> Compile(ClothDefinition3D definition)
    {
        int count = definition.Positions.Length;
        var rows = Enumerable.Range(0, count).Select(_ => new SortedDictionary<int, double>()).ToArray();
        double[] areas = new double[count];
        var edges = new SortedDictionary<(int A, int B), List<int>>();
        for (int face = 0; face < definition.Indices.Length; face += 3)
        {
            int a = definition.Indices[face];
            int b = definition.Indices[face + 1];
            int c = definition.Indices[face + 2];
            double area = Vector3.Cross(definition.Positions[b] - definition.Positions[a],
                definition.Positions[c] - definition.Positions[a]).Length() * .5;
            areas[a] += area / 3;
            areas[b] += area / 3;
            areas[c] += area / 3;
            AddEdge(a, b, c);
            AddEdge(b, c, a);
            AddEdge(c, a, b);
        }
        foreach (var (edge, opposite) in edges)
        {
            double cotangent = opposite.Sum(vertex => Cotangent(edge.A, vertex, edge.B));
            Add(edge.A, edge.B, cotangent * .5);
            Add(edge.A, edge.A, -cotangent * .5);
            Add(edge.B, edge.A, cotangent * .5);
            Add(edge.B, edge.B, -cotangent * .5);
            if (opposite.Count == 1)
            {
                int interior = opposite[0];
                double atA = Cotangent(interior, edge.A, edge.B) * .5;
                double atB = Cotangent(edge.A, edge.B, interior) * .5;
                foreach (int boundary in new[] { edge.A, edge.B })
                {
                    Add(boundary, edge.B, atA);
                    Add(boundary, edge.A, atB);
                    Add(boundary, interior, -atA - atB);
                }
            }
        }
        for (int vertex = 0; vertex < count; vertex++)
        {
            var entries = rows[vertex].Where(entry => Math.Abs(entry.Value) > 1e-12).ToArray();
            if (entries.Length == 0)
            {
                continue;
            }
            if (entries.Length > 32)
            {
                throw new NotSupportedException("AUR-CLOTH-003: Bending supports at most 32 vertices per compiled row; remesh high-valence vertices.");
            }
            double scale = 1 / Math.Sqrt(areas[vertex]);
            var weights = entries.Select(entry => (float)(entry.Value * scale)).ToImmutableArray();
            if (weights.Any(weight => !float.IsFinite(weight)))
            {
                throw new ArgumentException("Nonfinite vertex bending operator.");
            }
            yield return new(ClothConstraintKind3D.VertexBend,
                entries.Select(entry => entry.Key).ToImmutableArray(), weights, 0,
                definition.Material.BendCompliance);
        }

        void Add(int row, int column, double value)
        {
            rows[row].TryGetValue(column, out double current);
            rows[row][column] = current + value;
        }

        void AddEdge(int a, int b, int opposite)
        {
            var key = (Math.Min(a, b), Math.Max(a, b));
            if (!edges.TryGetValue(key, out var vertices))
            {
                vertices = [];
                edges.Add(key, vertices);
            }
            vertices.Add(opposite);
        }

        double Cotangent(int a, int center, int b)
        {
            Vector3 first = definition.Positions[a] - definition.Positions[center];
            Vector3 second = definition.Positions[b] - definition.Positions[center];
            return Vector3.Dot(first, second) / (double)Vector3.Cross(first, second).Length();
        }
    }
}
