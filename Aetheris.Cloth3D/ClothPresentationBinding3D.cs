using System.Collections.Immutable;
using System.Numerics;

namespace Aetheris.Cloth3D;

public sealed record ClothPresentationStencil3D(ImmutableArray<int> Vertices, ImmutableArray<float> Weights);
public sealed record ClothPresentationMesh3D(ImmutableArray<Vector3> Positions, ImmutableArray<int> Indices);

/// <summary>Author-time Loop subdivision stencils bound to one simulation topology.
/// Every material boundary is retained exactly as a piecewise linear curve, including
/// sewn boundaries. This is presentation only: it neither corrects nor certifies contact.</summary>
public sealed class ClothPresentationBinding3D
{
    private readonly CompiledCloth3D plan;
    public string SourceContentKey => plan.ContentKey;
    public ImmutableArray<ClothPresentationStencil3D> Stencils { get; }
    public ImmutableArray<int> Indices { get; }
    public ImmutableArray<Vector2> Coordinates { get; }
    public int Levels { get; }

    private ClothPresentationBinding3D(CompiledCloth3D plan, int levels,
        ImmutableArray<ClothPresentationStencil3D> stencils, ImmutableArray<int> indices)
    {
        this.plan = plan;
        Stencils = stencils;
        Indices = indices;
        Levels = levels;
        Coordinates = stencils.Select(stencil =>
        {
            Vector2 coordinate = Vector2.Zero;
            for (int index = 0; index < stencil.Vertices.Length; index++)
            {
                coordinate += plan.Definition.Coordinates[stencil.Vertices[index]] * stencil.Weights[index];
            }
            return coordinate;
        }).ToImmutableArray();
    }

    public static ClothPresentationBinding3D Compile(CompiledCloth3D plan, int levels = 2)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (levels is < 0 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(levels), "cloth-presentation-levels: Use 0..2 bounded levels.");
        }
        var stencils = Enumerable.Range(0, plan.Definition.Positions.Length)
            .Select(vertex => new ClothPresentationStencil3D([vertex], [1])).ToImmutableArray();
        var indices = plan.Definition.Indices;
        for (int level = 0; level < levels; level++)
        {
            var edges = new SortedDictionary<(int A, int B), List<int>>();
            var neighbors = Enumerable.Range(0, stencils.Length).Select(_ => new SortedSet<int>()).ToArray();
            for (int face = 0; face < indices.Length; face += 3)
            {
                int a = indices[face];
                int b = indices[face + 1];
                int c = indices[face + 2];
                Edge(a, b, c);
                Edge(b, c, a);
                Edge(c, a, b);
            }
            if (stencils.Length + edges.Count > 65536 || indices.Length > 3 * 65536)
            {
                throw new ArgumentException("cloth-presentation-budget: Refinement exceeds 65536 vertices or 262144 triangles.");
            }
            var boundary = edges.Where(edge => edge.Value.Count == 1)
                .SelectMany(edge => new[] { edge.Key.A, edge.Key.B }).ToHashSet();
            var next = ImmutableArray.CreateBuilder<ClothPresentationStencil3D>(stencils.Length + edges.Count);
            for (int vertex = 0; vertex < stencils.Length; vertex++)
            {
                if (boundary.Contains(vertex))
                {
                    next.Add(stencils[vertex]);
                    continue;
                }
                int valence = neighbors[vertex].Count;
                if (valence < 3)
                {
                    throw new ArgumentException("cloth-presentation-topology: An interior vertex needs at least three neighbors.");
                }
                float inner = .375f + .25f * MathF.Cos(2 * MathF.PI / valence);
                float beta = (.625f - inner * inner) / valence;
                var terms = new List<(int Vertex, float Weight)> { (vertex, 1 - valence * beta) };
                terms.AddRange(neighbors[vertex].Select(other => (other, beta)));
                next.Add(Blend(stencils, terms));
            }
            var midpoints = new Dictionary<(int A, int B), int>();
            foreach (var (edge, opposite) in edges)
            {
                midpoints.Add(edge, next.Count);
                if (opposite.Count == 1)
                {
                    next.Add(Blend(stencils, [(edge.A, .5f), (edge.B, .5f)]));
                }
                else
                {
                    next.Add(Blend(stencils, [(edge.A, .375f), (edge.B, .375f),
                        (opposite[0], .125f), (opposite[1], .125f)]));
                }
            }
            var refined = ImmutableArray.CreateBuilder<int>(indices.Length * 4);
            for (int face = 0; face < indices.Length; face += 3)
            {
                int a = indices[face];
                int b = indices[face + 1];
                int c = indices[face + 2];
                int ab = Midpoint(a, b);
                int bc = Midpoint(b, c);
                int ca = Midpoint(c, a);
                refined.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            stencils = next.MoveToImmutable();
            indices = refined.MoveToImmutable();

            void Edge(int a, int b, int opposite)
            {
                var key = (Math.Min(a, b), Math.Max(a, b));
                if (!edges.TryGetValue(key, out var adjacent))
                {
                    edges.Add(key, adjacent = []);
                }
                adjacent.Add(opposite);
                if (adjacent.Count > 2)
                {
                    throw new ArgumentException("cloth-presentation-topology: Nonmanifold edge.");
                }
                neighbors[a].Add(b);
                neighbors[b].Add(a);
            }

            int Midpoint(int a, int b)
            {
                return midpoints[(Math.Min(a, b), Math.Max(a, b))];
            }
        }
        return new(plan, levels, stencils, indices);
    }

    public ClothPresentationMesh3D Evaluate(ClothSnapshot3D snapshot)
    {
        ClothState3D.ValidateSnapshot(plan, snapshot);
        var positions = ImmutableArray.CreateBuilder<Vector3>(Stencils.Length);
        foreach (var stencil in Stencils)
        {
            Vector3 point = Vector3.Zero;
            for (int index = 0; index < stencil.Vertices.Length; index++)
            {
                point += snapshot.Positions[stencil.Vertices[index]] * stencil.Weights[index];
            }
            ClothValidation3D.Finite(point);
            positions.Add(point);
        }
        return new(positions.MoveToImmutable(), Indices);
    }

    private static ClothPresentationStencil3D Blend(ImmutableArray<ClothPresentationStencil3D> source,
        IEnumerable<(int Vertex, float Weight)> terms)
    {
        var weights = new SortedDictionary<int, float>();
        foreach (var (vertex, weight) in terms)
        {
            var stencil = source[vertex];
            for (int index = 0; index < stencil.Vertices.Length; index++)
            {
                int original = stencil.Vertices[index];
                weights[original] = weights.GetValueOrDefault(original) + weight * stencil.Weights[index];
            }
        }
        return new(weights.Keys.ToImmutableArray(), weights.Values.ToImmutableArray());
    }
}
