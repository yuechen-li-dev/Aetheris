using System.Collections.Immutable;
using System.Numerics;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Cloth3D;

public readonly record struct ClothBodySample3D(Vector3 Point, Vector3 Normal, float SignedDistance);

public interface IClothBodySurface3D
{
    ClothBodySample3D Query(Vector3 position);
}

/// <summary>Immutable pose snapshot with a nearest-triangle BVH. Outward triangle winding
/// is a caller contract. This is a discrete oriented surface query, not a watertight SDF or CCD.</summary>
public sealed class ClothTriangleBody3D : IClothBodySurface3D
{
    private readonly Vector3[] positions;
    private readonly Vector3[] normals;
    private readonly int[] indices;
    private readonly int[] triangles;
    private readonly List<Node> nodes = [];

    private sealed record Node(Vector3 Minimum, Vector3 Maximum, int Start, int Count, int Left, int Right);

    public ClothTriangleBody3D(ImmutableArray<Vector3> positions, ImmutableArray<int> indices)
    {
        if (positions.IsDefaultOrEmpty || indices.IsDefaultOrEmpty || indices.Length % 3 != 0
            || indices.Any(index => index < 0 || index >= positions.Length))
        {
            throw new ArgumentException("Body surface requires finite vertices and indexed outward triangles.");
        }
        foreach (var position in positions)
        {
            ClothValidation3D.Finite(position);
        }
        this.positions = positions.ToArray();
        this.indices = indices.ToArray();
        normals = new Vector3[positions.Length];
        for (int face = 0; face < indices.Length; face += 3)
        {
            int a = indices[face];
            int b = indices[face + 1];
            int c = indices[face + 2];
            Vector3 normal = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
            if (normal.LengthSquared() <= 1e-20f)
            {
                throw new ArgumentException("Body surface has a degenerate triangle.");
            }
            normals[a] += normal;
            normals[b] += normal;
            normals[c] += normal;
        }
        for (int vertex = 0; vertex < normals.Length; vertex++)
        {
            if (normals[vertex].LengthSquared() > 1e-20f)
            {
                normals[vertex] = Vector3.Normalize(normals[vertex]);
            }
        }
        triangles = Enumerable.Range(0, indices.Length / 3).ToArray();
        Build(0, triangles.Length);
    }

    public ClothBodySample3D Query(Vector3 position)
    {
        ClothValidation3D.Finite(position);
        float bestDistance = float.PositiveInfinity;
        int bestTriangle = -1;
        TriangleProximity3D closest = default;
        Visit(0);
        int offset = bestTriangle * 3;
        Vector3 normal = normals[indices[offset]] * closest.Barycentric.X
            + normals[indices[offset + 1]] * closest.Barycentric.Y
            + normals[indices[offset + 2]] * closest.Barycentric.Z;
        if (normal.LengthSquared() <= 1e-20f)
        {
            normal = Vector3.Cross(positions[indices[offset + 1]] - positions[indices[offset]],
                positions[indices[offset + 2]] - positions[indices[offset]]);
        }
        normal = Vector3.Normalize(normal);
        Vector3 delta = position - closest.Point;
        float distance = MathF.Sqrt(bestDistance);
        float sign = Vector3.Dot(delta, normal) < 0 ? -1 : 1;
        // The oriented pseudonormal chooses the side; the geometric nearest-point
        // direction resolves contact. Interpolated normals are not distance gradients.
        if (distance > 1e-10f)
        {
            normal = delta * (sign / distance);
        }
        return new(closest.Point, normal, distance * sign);

        void Visit(int nodeIndex)
        {
            var node = nodes[nodeIndex];
            if (DistanceToBox(position, node.Minimum, node.Maximum) > bestDistance)
            {
                return;
            }
            if (node.Count > 0)
            {
                for (int i = node.Start; i < node.Start + node.Count; i++)
                {
                    int triangle = triangles[i];
                    int face = triangle * 3;
                    var candidate = TriangleSurface3D.ClosestPoint(position, positions[indices[face]],
                        positions[indices[face + 1]], positions[indices[face + 2]]);
                    float distance = Vector3.DistanceSquared(position, candidate.Point);
                    if (distance < bestDistance || (distance == bestDistance && triangle < bestTriangle))
                    {
                        bestDistance = distance;
                        bestTriangle = triangle;
                        closest = candidate;
                    }
                }
                return;
            }
            var left = nodes[node.Left];
            var right = nodes[node.Right];
            if (DistanceToBox(position, left.Minimum, left.Maximum)
                <= DistanceToBox(position, right.Minimum, right.Maximum))
            {
                Visit(node.Left);
                Visit(node.Right);
            }
            else
            {
                Visit(node.Right);
                Visit(node.Left);
            }
        }
    }

    private int Build(int start, int count)
    {
        Vector3 minimum = new(float.PositiveInfinity);
        Vector3 maximum = new(float.NegativeInfinity);
        for (int i = start; i < start + count; i++)
        {
            for (int corner = 0; corner < 3; corner++)
            {
                var point = positions[indices[triangles[i] * 3 + corner]];
                minimum = Vector3.Min(minimum, point);
                maximum = Vector3.Max(maximum, point);
            }
        }
        int nodeIndex = nodes.Count;
        nodes.Add(new(minimum, maximum, start, count, -1, -1));
        if (count <= 8)
        {
            return nodeIndex;
        }
        Vector3 size = maximum - minimum;
        int axis = 0;
        if (size.Y > size.X)
        {
            axis = 1;
        }
        if (size.Z > size[axis])
        {
            axis = 2;
        }
        Array.Sort(triangles, start, count, Comparer<int>.Create((a, b) =>
        {
            float ca = Center(a)[axis];
            float cb = Center(b)[axis];
            int comparison = ca.CompareTo(cb);
            return comparison == 0 ? a.CompareTo(b) : comparison;
        }));
        int left = Build(start, count / 2);
        int right = Build(start + count / 2, count - count / 2);
        nodes[nodeIndex] = new(minimum, maximum, start, 0, left, right);
        return nodeIndex;
    }

    private Vector3 Center(int triangle)
    {
        int face = triangle * 3;
        return (positions[indices[face]] + positions[indices[face + 1]] + positions[indices[face + 2]]) / 3;
    }

    private static float DistanceToBox(Vector3 point, Vector3 minimum, Vector3 maximum)
    {
        return Vector3.DistanceSquared(point, Vector3.Clamp(point, minimum, maximum));
    }
}
