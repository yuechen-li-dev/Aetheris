using System.Collections.Immutable;
using System.Numerics;

namespace Aetheris.Cloth3D;

public enum ClothSurfaceRest3D
{
    FlatFabric,
    FittedSurface,
}

/// <summary>Counterclockwise four-corner cell. Local metre coordinates allow a welded
/// periodic surface without duplicating physical vertices at its material chart cut.</summary>
public sealed record ClothQuad3D(ImmutableArray<int> Vertices, ImmutableArray<Vector2> Coordinates);

/// <summary>Q1 membrane with 2x2 Gauss integration and objective rest-angle bending.
/// Material compliance is inverse surface stiffness, divided by quadrature area at compile time.
/// This experimental constitutive model is not a calibrated textile material.</summary>
public sealed record ClothQuadSurface3D(ImmutableArray<ClothQuad3D> Quads,
    ClothSurfaceRest3D Rest = ClothSurfaceRest3D.FlatFabric, float GrainRadians = 0)
{
    public static ClothDefinition3D Grid(string id, int columns, int rows, Vector2 size, Vector3 origin)
    {
        var definition = ClothDefinition3D.Grid(id, columns, rows, size, origin);
        var quads = ImmutableArray.CreateBuilder<ClothQuad3D>((columns - 1) * (rows - 1));
        for (int row = 0; row < rows - 1; row++)
        {
            for (int column = 0; column < columns - 1; column++)
            {
                int a = row * columns + column;
                ImmutableArray<int> vertices = [a, a + 1, a + columns + 1, a + columns];
                quads.Add(new(vertices, vertices.Select(index => definition.Coordinates[index]).ToImmutableArray()));
            }
        }
        var surface = new ClothQuadSurface3D(quads.MoveToImmutable());
        return definition with { Surface = surface, Indices = surface.Triangulate() };
    }

    public ImmutableArray<int> Triangulate(bool alternateDiagonal = false)
    {
        var indices = ImmutableArray.CreateBuilder<int>(Quads.Length * 6);
        foreach (var quad in Quads)
        {
            if (quad.Vertices.Length != 4)
            {
                throw new ArgumentException("cloth-quad-arity: A quad needs four vertices.");
            }
            var v = quad.Vertices;
            if (alternateDiagonal)
            {
                indices.AddRange(new int[] { v[0], v[1], v[3], v[1], v[2], v[3] });
            }
            else
            {
                indices.AddRange(new int[] { v[0], v[1], v[2], v[0], v[2], v[3] });
            }
        }
        return indices.MoveToImmutable();
    }
}

internal static class ClothQuadCompiler3D
{
    public static List<ClothConstraint3D> Compile(ClothDefinition3D definition, float[] masses)
    {
        var surface = definition.Surface!;
        if (!Enum.IsDefined(surface.Rest) || !float.IsFinite(surface.GrainRadians)
            || surface.Quads.IsDefaultOrEmpty || surface.Quads.Length > 16384 || definition.OffsetContact)
        {
            throw new ArgumentException("cloth-quad-surface: Require bounded cells, valid rest/grain and no offset self-contact.");
        }
        ValidateTriangles(definition, surface);
        Array.Clear(masses);
        var constraints = new List<ClothConstraint3D>();
        var edges = new SortedDictionary<(int A, int B), List<(int A, int B, int C, int D)>>();
        float gauss = 1 / MathF.Sqrt(3);
        float cosine = MathF.Cos(surface.GrainRadians);
        float sine = MathF.Sin(surface.GrainRadians);
        foreach (var quad in surface.Quads)
        {
            var v = quad.Vertices;
            for (int side = 0; side < 4; side++)
            {
                int a = v[side];
                int b = v[(side + 1) % 4];
                var key = (Math.Min(a, b), Math.Max(a, b));
                if (!edges.TryGetValue(key, out var uses))
                {
                    edges.Add(key, uses = []);
                }
                uses.Add((a, b, v[(side + 2) % 4], v[(side + 3) % 4]));
                if (uses.Count > 2 || (uses.Count == 2 && uses[0].A != b))
                {
                    throw new ArgumentException("cloth-quad-topology: Nonmanifold or inconsistent winding.");
                }
            }
            Vector3 firstNormal = Vector3.Zero;
            foreach (float eta in new[] { -gauss, gauss })
            {
                foreach (float xi in new[] { -gauss, gauss })
                {
                    float[] r = [-(1 - eta) / 4, (1 - eta) / 4, (1 + eta) / 4, -(1 + eta) / 4];
                    float[] s = [-(1 - xi) / 4, -(1 + xi) / 4, (1 + xi) / 4, (1 - xi) / 4];
                    Vector2 ur = Vector2.Zero;
                    Vector2 us = Vector2.Zero;
                    Vector3 xr = Vector3.Zero;
                    Vector3 xs = Vector3.Zero;
                    for (int local = 0; local < 4; local++)
                    {
                        ur += quad.Coordinates[local] * r[local];
                        us += quad.Coordinates[local] * s[local];
                        xr += definition.Positions[v[local]] * r[local];
                        xs += definition.Positions[v[local]] * s[local];
                    }
                    float determinant = ur.X * us.Y - ur.Y * us.X;
                    Vector3 normal = Vector3.Cross(xr, xs);
                    float area = surface.Rest == ClothSurfaceRest3D.FlatFabric ? determinant : normal.Length();
                    if (!float.IsFinite(determinant) || determinant <= 1e-10f || !float.IsFinite(area)
                        || area <= 1e-10f || normal.LengthSquared() <= 1e-20f
                        || (firstNormal != Vector3.Zero && Vector3.Dot(firstNormal, normal) <= 0))
                    {
                        throw new ArgumentException("cloth-quad-jacobian: Singular, inverted or folded rest/material cell.");
                    }
                    firstNormal = normal;
                    float[] u = new float[4];
                    float[] w = new float[4];
                    for (int local = 0; local < 4; local++)
                    {
                        u[local] = (r[local] * us.Y - s[local] * ur.Y) / determinant;
                        w[local] = (-r[local] * us.X + s[local] * ur.X) / determinant;
                    }
                    if (surface.Rest == ClothSurfaceRest3D.FittedSurface)
                    {
                        Vector3 fu = Sum(u);
                        Vector3 fv = Sum(w);
                        float a = fu.Length();
                        float b = Vector3.Dot(fu, fv) / a;
                        float c = Vector3.Cross(fu, fv).Length() / a;
                        for (int local = 0; local < 4; local++)
                        {
                            w[local] = (w[local] - u[local] * b / a) / c;
                            u[local] /= a;
                        }
                    }
                    float[] shape = [(1 - xi) * (1 - eta) / 4, (1 + xi) * (1 - eta) / 4,
                        (1 + xi) * (1 + eta) / 4, (1 - xi) * (1 + eta) / 4];
                    for (int local = 0; local < 4; local++)
                    {
                        float old = u[local];
                        u[local] = cosine * old + sine * w[local];
                        w[local] = -sine * old + cosine * w[local];
                        masses[v[local]] += area * definition.Material.ArealDensity * shape[local];
                    }
                    Add(ClothConstraintKind3D.SurfaceWarp, definition.Material.WarpCompliance);
                    Add(ClothConstraintKind3D.SurfaceWeft, definition.Material.WeftCompliance);
                    Add(ClothConstraintKind3D.SurfaceShear, definition.Material.DiagonalCompliance);

                    Vector3 Sum(float[] weights)
                    {
                        Vector3 value = Vector3.Zero;
                        for (int local = 0; local < 4; local++)
                        {
                            value += definition.Positions[v[local]] * weights[local];
                        }
                        return value;
                    }

                    void Add(ClothConstraintKind3D kind, float compliance)
                    {
                        if (!float.IsFinite(compliance / area) || u.Any(value => !float.IsFinite(value))
                            || w.Any(value => !float.IsFinite(value)))
                        {
                            throw new ArgumentException("cloth-quad-conditioning: Nonfinite material operator.");
                        }
                        constraints.Add(new(kind, v, u.ToImmutableArray(), 0, compliance / area)
                        {
                            TransverseWeights = w.ToImmutableArray(),
                        });
                    }
                }
            }
        }
        foreach (var uses in edges.Values.Where(uses => uses.Count == 2))
        {
            var first = uses[0];
            var second = uses[1];
            ImmutableArray<int> vertices = [first.A, first.B, first.C, first.D, second.C, second.D];
            Vector3 a = definition.Positions[first.A];
            Vector3 b = definition.Positions[first.B];
            Vector3 c = (definition.Positions[first.C] + definition.Positions[first.D]) * .5f;
            Vector3 d = (definition.Positions[second.C] + definition.Positions[second.D]) * .5f;
            float rest = surface.Rest == ClothSurfaceRest3D.FittedSurface
                ? ClothSurfaceMechanics3D.Angle(a, b, c, d) : 0;
            Vector3 edge = b - a;
            float width = (Vector3.Cross(c - a, edge).Length() + Vector3.Cross(d - a, edge).Length()) / edge.Length();
            float compliance = definition.Material.BendCompliance * width / edge.Length();
            if (!float.IsFinite(rest) || !float.IsFinite(width) || width <= 0 || !float.IsFinite(compliance))
            {
                throw new ArgumentException("cloth-quad-bend: Singular hinge or nonfinite rest/compliance.");
            }
            constraints.Add(new(ClothConstraintKind3D.SurfaceBend, vertices, [], rest, compliance));
        }
        if (masses.Any(mass => !float.IsFinite(mass) || mass <= 0 || !float.IsFinite(1 / mass)))
        {
            throw new ArgumentException("cloth-quad-mass: Unused or numerically massless vertices.");
        }
        return constraints;
    }

    private static void ValidateTriangles(ClothDefinition3D definition, ClothQuadSurface3D surface)
    {
        if (definition.Indices.Length != surface.Quads.Length * 6)
        {
            throw new ArgumentException("cloth-quad-triangulation: Require two display triangles per cell.");
        }
        for (int cell = 0; cell < surface.Quads.Length; cell++)
        {
            var quad = surface.Quads[cell];
            if (quad is null || quad.Vertices.IsDefault || quad.Coordinates.IsDefault
                || quad.Vertices.Length != 4 || quad.Coordinates.Length != 4
                || quad.Vertices.Distinct().Count() != 4
                || quad.Vertices.Any(index => index < 0 || index >= definition.Positions.Length)
                || quad.Coordinates.Any(point => !float.IsFinite(point.X) || !float.IsFinite(point.Y)))
            {
                throw new ArgumentException("cloth-quad-cell: Require four distinct valid vertices and finite coordinates.");
            }
            var v = quad.Vertices;
            int[] first = [v[0], v[1], v[2], v[0], v[2], v[3]];
            int[] second = [v[0], v[1], v[3], v[1], v[2], v[3]];
            var actual = definition.Indices.AsSpan(cell * 6, 6);
            if (!actual.SequenceEqual(first) && !actual.SequenceEqual(second))
            {
                throw new ArgumentException("cloth-quad-triangulation: Display triangles must lower their corresponding cell.");
            }
        }
    }

    public static void WriteIdentity(BinaryWriter writer, ClothQuadSurface3D surface)
    {
        writer.Write("cloth/quad-metric/v1");
        writer.Write((int)surface.Rest);
        writer.Write(surface.GrainRadians);
        writer.Write(surface.Quads.Length);
        foreach (var quad in surface.Quads)
        {
            for (int local = 0; local < 4; local++)
            {
                writer.Write(quad.Vertices[local]);
                writer.Write(quad.Coordinates[local].X);
                writer.Write(quad.Coordinates[local].Y);
            }
        }
    }
}
