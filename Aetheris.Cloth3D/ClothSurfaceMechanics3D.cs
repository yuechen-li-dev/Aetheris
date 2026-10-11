using System.Collections.Immutable;
using System.Numerics;

namespace Aetheris.Cloth3D;

public sealed record ClothSurfaceEvaluation3D(float Residual, ImmutableArray<Vector3> Gradients);

public static class ClothSurfaceMechanics3D
{
    public static ClothSurfaceEvaluation3D Evaluate(ClothConstraint3D constraint, ImmutableArray<Vector3> positions)
    {
        if (!IsSurface(constraint.Kind))
        {
            throw new ArgumentException("Require a compiled surface constraint.");
        }
        Span<Vector3> gradients = stackalloc Vector3[6];
        float residual = Evaluate(constraint, positions.AsSpan(), gradients);
        return new(residual, gradients[..constraint.Vertices.Length].ToArray().ToImmutableArray());
    }

    internal static bool IsSurface(ClothConstraintKind3D kind)
    {
        return kind is ClothConstraintKind3D.SurfaceWarp or ClothConstraintKind3D.SurfaceWeft
            or ClothConstraintKind3D.SurfaceShear or ClothConstraintKind3D.SurfaceBend;
    }

    internal static float Evaluate(ClothConstraint3D constraint, ReadOnlySpan<Vector3> positions,
        Span<Vector3> gradients)
    {
        gradients.Clear();
        var vertices = constraint.Vertices;
        if (constraint.Kind == ClothConstraintKind3D.SurfaceBend)
        {
            Vector3 a = positions[vertices[0]];
            Vector3 b = positions[vertices[1]];
            Vector3 c = (positions[vertices[2]] + positions[vertices[3]]) * .5f;
            Vector3 d = (positions[vertices[4]] + positions[vertices[5]]) * .5f;
            Vector3 edge = b - a;
            Vector3 first = Vector3.Cross(edge, c - a);
            Vector3 second = Vector3.Cross(d - a, edge);
            float length = edge.Length();
            if (length < 1e-8f || first.LengthSquared() < 1e-16f || second.LengthSquared() < 1e-16f)
            {
                return 0;
            }
            Vector3 gc = -first * (length / first.LengthSquared());
            Vector3 gd = -second * (length / second.LengthSquared());
            float tc = Vector3.Dot(c - a, edge) / edge.LengthSquared();
            float td = Vector3.Dot(d - a, edge) / edge.LengthSquared();
            gradients[0] = -(1 - tc) * gc - (1 - td) * gd;
            gradients[1] = -tc * gc - td * gd;
            gradients[2] = gradients[3] = gc * .5f;
            gradients[4] = gradients[5] = gd * .5f;
            return Wrap(Angle(a, b, c, d) - constraint.RestLength);
        }
        Vector3 u = Vector3.Zero;
        Vector3 v = Vector3.Zero;
        for (int local = 0; local < 4; local++)
        {
            u += positions[vertices[local]] * constraint.Weights[local];
            v += positions[vertices[local]] * constraint.TransverseWeights[local];
        }
        for (int local = 0; local < 4; local++)
        {
            gradients[local] = constraint.Kind switch
            {
                ClothConstraintKind3D.SurfaceWarp => u * constraint.Weights[local],
                ClothConstraintKind3D.SurfaceWeft => v * constraint.TransverseWeights[local],
                ClothConstraintKind3D.SurfaceShear => v * constraint.Weights[local] + u * constraint.TransverseWeights[local],
                _ => throw new ArgumentException("Unknown surface constraint."),
            };
        }
        return constraint.Kind switch
        {
            ClothConstraintKind3D.SurfaceWarp => .5f * (u.LengthSquared() - 1),
            ClothConstraintKind3D.SurfaceWeft => .5f * (v.LengthSquared() - 1),
            ClothConstraintKind3D.SurfaceShear => Vector3.Dot(u, v),
            _ => throw new ArgumentException("Unknown surface constraint."),
        };
    }

    internal static float Angle(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        Vector3 edge = Vector3.Normalize(b - a);
        Vector3 first = Vector3.Normalize(Vector3.Cross(b - a, c - a));
        Vector3 second = Vector3.Normalize(Vector3.Cross(d - a, b - a));
        return MathF.Atan2(Vector3.Dot(edge, Vector3.Cross(first, second)), Vector3.Dot(first, second));
    }

    private static float Wrap(float angle)
    {
        if (angle > MathF.PI)
        {
            return angle - 2 * MathF.PI;
        }
        if (angle < -MathF.PI)
        {
            return angle + 2 * MathF.PI;
        }
        return angle;
    }
}
