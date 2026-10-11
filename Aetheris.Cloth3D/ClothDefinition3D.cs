using System.Collections.Immutable;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Aetheris.Cloth3D;

public enum ClothBendingModel3D
{
    BoundaryCorrectedVertex,
    QuadraticHinge,
}

/// <summary>Metres, kilograms and seconds. Link compliance is an authoring control,
/// not a measured continuum fabric modulus. Diagonal links approximate shear.</summary>
public sealed record ClothMaterial3D
{
    public float ArealDensity { get; init; } = .2f;
    public float Thickness { get; init; } = .004f;
    public float WarpCompliance { get; init; } = .000001f;
    public float WeftCompliance { get; init; } = .000001f;
    public float DiagonalCompliance { get; init; } = .000004f;
    public float BendCompliance { get; init; } = 1000f;
}

public sealed record ClothDefinition3D(string Id, ImmutableArray<Vector3> Positions,
    ImmutableArray<Vector2> Coordinates, ImmutableArray<int> Indices)
{
    /// <summary>Optional initial arrangement. Positions remains the undeformed rest surface.</summary>
    public ImmutableArray<Vector3> InitialPositions { get; init; } = [];
    /// <summary>Weighted equality constraints between material boundary samples.</summary>
    public ImmutableArray<ClothStitch3D> Stitches { get; init; } = [];
    public ClothMaterial3D Material { get; init; } = new();
    public ImmutableArray<int> Pins { get; init; } = [];
    public ClothBendingModel3D BendingModel { get; init; } = ClothBendingModel3D.BoundaryCorrectedVertex;
    /// <summary>Opt-in exhaustive offset self-contact foundation. The discrete control stays available.</summary>
    public bool OffsetContact { get; init; }

    /// <summary>A flat pattern in XZ. Coordinates retain the material warp/weft axes.</summary>
    public static ClothDefinition3D Grid(string id, int columns, int rows, Vector2 size, Vector3 origin)
    {
        if (columns is < 2 or > 256 || rows is < 2 or > 256 || !float.IsFinite(size.X)
            || !float.IsFinite(size.Y) || size.X <= 0 || size.Y <= 0)
        {
            throw new ArgumentException("Cloth grid requires 2..256 vertices per axis and positive metre dimensions.");
        }
        var positions = ImmutableArray.CreateBuilder<Vector3>(columns * rows);
        var coordinates = ImmutableArray.CreateBuilder<Vector2>(columns * rows);
        var indices = ImmutableArray.CreateBuilder<int>((columns - 1) * (rows - 1) * 6);
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                Vector2 uv = new(size.X * column / (columns - 1), size.Y * row / (rows - 1));
                coordinates.Add(uv);
                positions.Add(origin + new Vector3(uv.X, 0, uv.Y));
                if (row < rows - 1 && column < columns - 1)
                {
                    int a = row * columns + column;
                    indices.AddRange(new int[] { a, a + columns, a + 1, a + 1, a + columns, a + columns + 1 });
                }
            }
        }
        return new(id, positions.MoveToImmutable(), coordinates.MoveToImmutable(), indices.MoveToImmutable());
    }

    public CompiledCloth3D Compile() => ClothCompiler3D.Compile(this);
}

public sealed record ClothStitch3D(string Id, ImmutableArray<int> Vertices,
    ImmutableArray<float> Weights, float Compliance = .00000001f);

public enum ClothConstraintKind3D { Warp, Weft, Diagonal, FlatBend, VertexBend, Stitch }

public sealed record ClothConstraint3D(ClothConstraintKind3D Kind, ImmutableArray<int> Vertices,
    ImmutableArray<float> Weights, float RestLength, float Compliance);

/// <summary>Immutable topology and conflict-free schedules. A color contains disjoint vertex writes.</summary>
public sealed class CompiledCloth3D
{
    public string ContentKey { get; }
    public ClothDefinition3D Definition { get; }
    public ImmutableArray<float> InverseMasses { get; }
    public ImmutableArray<ClothConstraint3D> Constraints { get; }
    public ImmutableArray<ImmutableArray<int>> ConstraintColors { get; }
    public ImmutableArray<ImmutableArray<int>> TriangleColors { get; }
    public ClothContactTopology3D? ContactTopology { get; }

    internal CompiledCloth3D(string contentKey, ClothDefinition3D definition,
        ImmutableArray<float> inverseMasses, ImmutableArray<ClothConstraint3D> constraints,
        ImmutableArray<ImmutableArray<int>> constraintColors,
        ImmutableArray<ImmutableArray<int>> triangleColors)
    {
        ContentKey = contentKey;
        Definition = definition;
        InverseMasses = inverseMasses;
        Constraints = constraints;
        ConstraintColors = constraintColors;
        TriangleColors = triangleColors;
        ContactTopology = definition.OffsetContact ? ClothContactTopology3D.Compile(definition) : null;
    }
}

internal static class ClothCompiler3D
{
    public static CompiledCloth3D Compile(ClothDefinition3D definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Id);
        var positions = definition.Positions;
        var indices = definition.Indices;
        var material = definition.Material;
        ArgumentNullException.ThrowIfNull(material);
        if (!Enum.IsDefined(definition.BendingModel))
        {
            throw new ArgumentException("Unknown cloth bending model.");
        }
        if (positions.IsDefault || positions.Length is < 3 or > 65536 || definition.Coordinates.IsDefault
            || positions.Length != definition.Coordinates.Length || indices.IsDefaultOrEmpty || indices.Length % 3 != 0)
        {
            throw new ArgumentException("Cloth requires positions, matching material coordinates and indexed triangles.");
        }
        if (definition.InitialPositions.IsDefault || (!definition.InitialPositions.IsEmpty
            && definition.InitialPositions.Length != positions.Length) || definition.Stitches.IsDefault)
        {
            throw new ArgumentException("Initial arrangement must match rest vertices; stitches cannot be default.");
        }
        foreach (Vector3 position in definition.InitialPositions)
        {
            ClothValidation3D.Finite(position);
        }
        var stitchIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stitch in definition.Stitches)
        {
            if (string.IsNullOrWhiteSpace(stitch.Id) || !stitchIds.Add(stitch.Id)
                || stitch.Vertices.IsDefault || stitch.Weights.IsDefault
                || stitch.Vertices.Length is < 2 or > 4 || stitch.Vertices.Length != stitch.Weights.Length
                || stitch.Vertices.Distinct().Count() != stitch.Vertices.Length
                || stitch.Vertices.Any(vertex => vertex < 0 || vertex >= positions.Length)
                || stitch.Weights.Any(weight => !float.IsFinite(weight))
                || MathF.Abs(stitch.Weights.Sum()) > 1e-6f
                || MathF.Abs(stitch.Weights.Where(weight => weight > 0).Sum() - 1) > 1e-6f
                || !float.IsFinite(stitch.Compliance) || stitch.Compliance < 0)
            {
                throw new ArgumentException("Invalid stitch identity, stencil, barycentric weights or compliance.");
            }
        }
        foreach (Vector3 position in positions)
        {
            ClothValidation3D.Finite(position);
        }
        foreach (Vector2 uv in definition.Coordinates)
        {
            if (!float.IsFinite(uv.X) || !float.IsFinite(uv.Y))
            {
                throw new ArgumentException("Nonfinite cloth coordinates.");
            }
        }
        foreach (float value in new[] { material.ArealDensity, material.Thickness, material.WarpCompliance,
            material.WeftCompliance, material.DiagonalCompliance, material.BendCompliance })
        {
            if (!float.IsFinite(value) || value < 0)
            {
                throw new ArgumentException("Cloth material must be finite and nonnegative.");
            }
        }
        if (material.ArealDensity <= 0 || material.Thickness <= 0)
        {
            throw new ArgumentException("Density and thickness must be positive.");
        }
        if (definition.Pins.IsDefault || definition.Pins.Distinct().Count() != definition.Pins.Length
            || definition.Pins.Any(pin => pin < 0 || pin >= positions.Length))
        {
            throw new ArgumentException("Invalid or repeated cloth pin.");
        }
        float[] masses = new float[positions.Length];
        var edges = new SortedDictionary<(int A, int B), List<int>>();
        var uniqueTriangles = new HashSet<(int, int, int)>();
        var triangleVertices = new List<ImmutableArray<int>>();
        for (int triangle = 0; triangle < indices.Length; triangle += 3)
        {
            int a = indices[triangle];
            int b = indices[triangle + 1];
            int c = indices[triangle + 2];
            if (a < 0 || b < 0 || c < 0 || a >= positions.Length || b >= positions.Length || c >= positions.Length
                || a == b || a == c || b == c)
            {
                throw new ArgumentException("Invalid cloth triangle index.");
            }
            int[] sorted = [a, b, c];
            Array.Sort(sorted);
            if (!uniqueTriangles.Add((sorted[0], sorted[1], sorted[2])))
            {
                throw new ArgumentException("Duplicate cloth triangle.");
            }
            float area = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]).Length() * .5f;
            if (!float.IsFinite(area) || area <= 1e-10f)
            {
                throw new ArgumentException("Degenerate cloth rest triangle.");
            }
            Vector2 materialAb = definition.Coordinates[b] - definition.Coordinates[a];
            Vector2 materialAc = definition.Coordinates[c] - definition.Coordinates[a];
            float materialArea = materialAb.X * materialAc.Y - materialAb.Y * materialAc.X;
            if (!float.IsFinite(materialArea) || MathF.Abs(materialArea) <= 1e-12f)
            {
                throw new ArgumentException("Degenerate cloth material-coordinate triangle.");
            }
            float mass = area * material.ArealDensity / 3;
            masses[a] += mass;
            masses[b] += mass;
            masses[c] += mass;
            triangleVertices.Add([a, b, c]);
            Edge(a, b, c);
            Edge(b, c, a);
            Edge(c, a, b);
        }
        if (masses.Any(mass => !float.IsFinite(mass) || mass <= 0 || !float.IsFinite(1 / mass)))
        {
            throw new ArgumentException("Cloth has unused or numerically massless vertices.");
        }
        var constraints = new List<ClothConstraint3D>();
        foreach (var (edge, opposite) in edges)
        {
            Vector2 direction = definition.Coordinates[edge.B] - definition.Coordinates[edge.A];
            if (direction.LengthSquared() < 1e-14f)
            {
                throw new ArgumentException("Coincident material coordinates on cloth edge.");
            }
            ClothConstraintKind3D kind = ClothConstraintKind3D.Diagonal;
            float compliance = material.DiagonalCompliance;
            if (MathF.Abs(direction.Y) <= 1e-6f * direction.Length())
            {
                kind = ClothConstraintKind3D.Warp;
                compliance = material.WarpCompliance;
            }
            else if (MathF.Abs(direction.X) <= 1e-6f * direction.Length())
            {
                kind = ClothConstraintKind3D.Weft;
                compliance = material.WeftCompliance;
            }
            constraints.Add(new(kind, [edge.A, edge.B], [], Vector3.Distance(positions[edge.A], positions[edge.B]), compliance));
            if (opposite.Count == 2)
            {
                var hinge = Bend(edge.A, edge.B, opposite[0], opposite[1]);
                if (definition.BendingModel == ClothBendingModel3D.QuadraticHinge)
                {
                    constraints.Add(hinge);
                }
            }
        }
        if (definition.BendingModel == ClothBendingModel3D.BoundaryCorrectedVertex)
        {
            constraints.AddRange(ClothVertexBending3D.Compile(definition));
        }
        foreach (var stitch in definition.Stitches)
        {
            constraints.Add(new(ClothConstraintKind3D.Stitch, stitch.Vertices, stitch.Weights, 0, stitch.Compliance));
        }
        var pinned = definition.Pins.ToHashSet();
        var inverseMasses = masses.Select((mass, index) => pinned.Contains(index) ? 0 : 1 / mass).ToImmutableArray();
        string key = Identity();
        return new(key, definition, inverseMasses, constraints.ToImmutableArray(),
            Color(constraints.Select(constraint => constraint.Vertices)), Color(triangleVertices));

        void Edge(int a, int b, int opposite)
        {
            var key = (Math.Min(a, b), Math.Max(a, b));
            if (!edges.TryGetValue(key, out var neighbors))
            {
                edges.Add(key, neighbors = []);
            }
            neighbors.Add(opposite);
            if (neighbors.Count > 2)
            {
                throw new ArgumentException("Nonmanifold cloth edge.");
            }
        }

        ClothConstraint3D Bend(int a, int b, int c, int d)
        {
            Vector3 pa = positions[a];
            Vector3 pb = positions[b];
            Vector3 pc = positions[c];
            Vector3 pd = positions[d];
            float doubleArea1 = Vector3.Cross(pa - pc, pb - pc).Length();
            float doubleArea2 = Vector3.Cross(pa - pd, pb - pd).Length();
            float ac = Vector3.Dot(pb - pa, pc - pa) / doubleArea1;
            float bc = Vector3.Dot(pa - pb, pc - pb) / doubleArea1;
            float ad = Vector3.Dot(pb - pa, pd - pa) / doubleArea2;
            float bd = Vector3.Dot(pa - pb, pd - pb) / doubleArea2;
            float factor = MathF.Sqrt(6 / (doubleArea1 + doubleArea2));
            // Flat-rest quadratic hinge: E = 1/2 |sum(k_i * x_i)|^2.
            // Its vector components are solved directly, preserving quadratic energy
            // rather than squaring that energy into another constraint potential.
            float[] weights = [-(bc + bd) * factor, -(ac + ad) * factor,
                (ac + bc) * factor, (ad + bd) * factor];
            if (weights.Any(weight => !float.IsFinite(weight)))
            {
                throw new ArgumentException("Numerically ill-conditioned cloth rest hinge.");
            }
            // Classify rest planarity geometrically. The weighted residual scales with
            // inverse edge length and can suffer cancellation even on a planar mesh.
            Vector3 normal1 = Vector3.Cross(pb - pa, pc - pa) / doubleArea1;
            Vector3 normal2 = Vector3.Cross(pb - pa, pd - pa) / doubleArea2;
            // Opposite vertices must lie on opposite sides of the shared edge.
            // Parallel normals alone would also admit a fully folded-back, overlapping hinge.
            if ((normal1 + normal2).Length() > .0001f)
            {
                throw new NotSupportedException("AUR-CLOTH-001: The quadratic baseline requires locally flat rest hinges; shaped garments need a rest-curvature model.");
            }
            return new(ClothConstraintKind3D.FlatBend, [a, b, c, d], weights.ToImmutableArray(), 0, material.BendCompliance);
        }

        string Identity()
        {
            using var bytes = new MemoryStream();
            using var writer = new BinaryWriter(bytes, Encoding.UTF8, true);
            writer.Write("cloth/material-rest-xpbd/v3");
            writer.Write((int)definition.BendingModel);
            writer.Write(definition.OffsetContact);
            writer.Write(definition.Id);
            writer.Write(positions.Length);
            foreach (Vector3 p in positions)
            {
                writer.Write(p.X);
                writer.Write(p.Y);
                writer.Write(p.Z);
            }
            foreach (Vector2 uv in definition.Coordinates)
            {
                writer.Write(uv.X);
                writer.Write(uv.Y);
            }
            writer.Write(indices.Length);
            foreach (int index in indices)
            {
                writer.Write(index);
            }
            writer.Write(definition.Pins.Length);
            foreach (int pin in definition.Pins)
            {
                writer.Write(pin);
            }
            writer.Write(definition.InitialPositions.Length);
            foreach (Vector3 p in definition.InitialPositions)
            {
                writer.Write(p.X);
                writer.Write(p.Y);
                writer.Write(p.Z);
            }
            writer.Write(definition.Stitches.Length);
            foreach (var stitch in definition.Stitches)
            {
                writer.Write(stitch.Id);
                writer.Write(stitch.Compliance);
                writer.Write(stitch.Vertices.Length);
                for (int i = 0; i < stitch.Vertices.Length; i++)
                {
                    writer.Write(stitch.Vertices[i]);
                    writer.Write(stitch.Weights[i]);
                }
            }
            writer.Write(material.ArealDensity);
            writer.Write(material.Thickness);
            writer.Write(material.WarpCompliance);
            writer.Write(material.WeftCompliance);
            writer.Write(material.DiagonalCompliance);
            writer.Write(material.BendCompliance);
            writer.Flush();
            return Convert.ToHexString(SHA256.HashData(bytes.ToArray()));
        }
    }

    internal static ImmutableArray<ImmutableArray<int>> Color(IEnumerable<ImmutableArray<int>> stencils)
    {
        var vertices = new List<HashSet<int>>();
        var colors = new List<List<int>>();
        int index = 0;
        foreach (var stencil in stencils)
        {
            int color = 0;
            for (; color < colors.Count; color++)
            {
                if (!stencil.Any(vertices[color].Contains))
                {
                    break;
                }
            }
            if (color == colors.Count)
            {
                colors.Add([]);
                vertices.Add([]);
            }
            colors[color].Add(index++);
            vertices[color].UnionWith(stencil);
        }
        return colors.Select(color => color.ToImmutableArray()).ToImmutableArray();
    }
}

internal static class ClothValidation3D
{
    public static void Finite(Vector3 value)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
        {
            throw new ArgumentException("Nonfinite cloth vector.");
        }
    }
}
