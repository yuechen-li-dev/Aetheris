using System.Numerics;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Cloth3D;

public readonly record struct ClothContactQuery3D(float Distance, Vector3 Normal,
    Vector4 Weights, bool Active);

/// <summary>OGC feature blocks, complete clearance queries and conservative trust regions.
/// Normal response uses a C2 matched quadratic/logarithmic distance potential in XPBD.
/// Friction and two-way rigid-body coupling are separate, unimplemented capabilities.</summary>
public static class ClothOffsetContact3D
{
    public const float ClearanceFloor = .000001f;

    public static ClothContactQuery3D Query(ClothContactTopology3D topology,
        ClothContactPair3D pair, IReadOnlyList<Vector3> positions)
    {
        var vertices = pair.Vertices;
        Vector3 a = positions[vertices[0]];
        Vector3 b = positions[vertices[1]];
        Vector3 difference;
        Vector4 weights;
        bool active;
        switch (pair.Kind)
        {
            case ClothContactFeature3D.Face:
                var closest = TriangleSurface3D.ClosestPoint(a, b,
                    positions[vertices[2]], positions[vertices[3]]);
                var barycentric = closest.Barycentric;
                difference = a - closest.Point;
                weights = new(1, -barycentric.X, -barycentric.Y, -barycentric.Z);
                active = barycentric.X > 0 && barycentric.Y > 0 && barycentric.Z > 0;
                break;
            case ClothContactFeature3D.Edge:
                Vector3 c = positions[vertices[2]];
                float t = SegmentParameter(a, b, c);
                Vector3 point = Vector3.Lerp(b, c, t);
                difference = a - point;
                weights = new(1, t - 1, -t, 0);
                active = t > 0 && t < 1;
                foreach (int opposite in topology.Edges[pair.Feature].Opposite)
                {
                    Vector3 foot = b + (c - b) * LineParameter(positions[opposite], b, c);
                    active &= Vector3.Dot(a - foot, foot - positions[opposite]) >= 0;
                }
                break;
            case ClothContactFeature3D.Vertex:
                difference = a - b;
                weights = new(1, -1, 0, 0);
                active = VertexBlock(vertices[1], a, topology, positions)
                    && VertexBlock(vertices[0], b, topology, positions);
                break;
            case ClothContactFeature3D.EdgePair:
                Vector3 d = positions[vertices[3]];
                Vector3 third = positions[vertices[2]];
                var parameters = SegmentParameters(a, b, third, d);
                Vector3 firstPoint = Vector3.Lerp(a, b, parameters.X);
                Vector3 secondPoint = Vector3.Lerp(third, d, parameters.Y);
                difference = firstPoint - secondPoint;
                weights = new(1 - parameters.X, parameters.X, parameters.Y - 1, -parameters.Y);
                active = EndpointBlock(vertices[0], vertices[1], parameters.X, secondPoint, topology, positions)
                    && EndpointBlock(vertices[2], vertices[3], parameters.Y, firstPoint, topology, positions);
                float cross = Vector3.Cross(b - a, d - third).LengthSquared();
                active &= cross > 1e-8f * (b - a).LengthSquared() * (d - third).LengthSquared();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(pair));
        }
        float distance = difference.Length();
        Vector3 normal = distance > 0 ? difference / distance : Vector3.Zero;
        return new(distance, normal, weights, active);
    }

    public static Vector2 SegmentParameters(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        Vector3 first = b - a;
        Vector3 second = d - c;
        Vector3 offset = a - c;
        float aa = first.LengthSquared();
        float bb = Vector3.Dot(first, second);
        float cc = second.LengthSquared();
        float dd = Vector3.Dot(first, offset);
        float ee = Vector3.Dot(second, offset);
        float determinant = aa * cc - bb * bb;
        float s = 0;
        if (aa <= 1e-20f)
        {
            return new(0, SegmentParameter(a, c, d));
        }
        if (cc <= 1e-20f)
        {
            return new(SegmentParameter(c, a, b), 0);
        }
        if (determinant > 1e-8f * aa * cc)
        {
            s = Math.Clamp((bb * ee - cc * dd) / determinant, 0, 1);
        }
        float t = (bb * s + ee) / cc;
        if (t < 0)
        {
            t = 0;
            s = Math.Clamp(-dd / aa, 0, 1);
        }
        else if (t > 1)
        {
            t = 1;
            s = Math.Clamp((bb - dd) / aa, 0, 1);
        }
        return new(s, t);
    }

    public static Vector2 Potential(float distance, float radius)
    {
        if (distance >= radius)
        {
            return Vector2.Zero;
        }
        if (distance <= 0)
        {
            throw new ArgumentException("Offset potential requires positive separation.");
        }
        float threshold = radius * .5f;
        if (distance >= threshold)
        {
            float residual = radius - distance;
            return new(residual, -1);
        }
        // Match value, slope AND curvature at r/2. The logarithmic coefficient
        // is r^2/4; using the printed Eq.19 literally would not match its Eq.18.
        float coefficient = radius * radius * .25f;
        float energy = coefficient * (.5f - MathF.Log(distance / threshold));
        float constraint = MathF.Sqrt(2 * energy);
        return new(constraint, -coefficient / (distance * constraint));
    }

    public static float[] Bounds(CompiledCloth3D plan, IReadOnlyList<Vector3> anchor,
        ClothContacts3D contacts, float safetyFactor)
    {
        var topology = plan.ContactTopology ?? throw new ArgumentException("Offset contact was not compiled.");
        float[] clearances = Enumerable.Repeat(1e6f, anchor.Count).ToArray();
        foreach (var pair in topology.Pairs)
        {
            if (pair.Kind is not (ClothContactFeature3D.Face or ClothContactFeature3D.EdgePair))
            {
                continue;
            }
            float distance = Query(topology, pair, anchor).Distance;
            foreach (int vertex in pair.Vertices)
            {
                clearances[vertex] = MathF.Min(clearances[vertex], distance);
            }
        }
        float halfThickness = plan.Definition.Material.Thickness * .5f;
        for (int vertex = 0; vertex < anchor.Count; vertex++)
        {
            if (contacts.PlaneNormal is { } normal)
            {
                float gap = Vector3.Dot(normal, anchor[vertex]) - contacts.PlaneOffset - halfThickness;
                clearances[vertex] = MathF.Min(clearances[vertex], gap);
            }
            if (contacts.SphereCenter is { } sphere)
            {
                foreach (int face in topology.IncidentFaces[vertex])
                {
                    var indices = plan.Definition.Indices;
                    var closest = TriangleSurface3D.ClosestPoint(sphere, anchor[indices[face * 3]],
                        anchor[indices[face * 3 + 1]], anchor[indices[face * 3 + 2]]);
                    float gap = Vector3.Distance(sphere, closest.Point) - contacts.SphereRadius - halfThickness;
                    clearances[vertex] = MathF.Min(clearances[vertex], gap);
                }
            }
            clearances[vertex] = safetyFactor * MathF.Max(0, clearances[vertex] - ClearanceFloor);
        }
        return clearances;
    }

    public static Vector3 Clip(Vector3 proposed, Vector3 anchor, float bound)
    {
        Vector3 movement = proposed - anchor;
        float length = movement.Length();
        if (length > bound)
        {
            return anchor + movement * (bound / length);
        }
        return proposed;
    }

    public static void ValidateInitial(CompiledCloth3D plan, ClothSnapshot3D state, ClothContacts3D contacts)
    {
        var topology = plan.ContactTopology ?? throw new ArgumentException("Offset contact was not compiled.");
        var positions = state.Positions;
        foreach (var pair in topology.Pairs)
        {
            if (pair.Kind is ClothContactFeature3D.Face or ClothContactFeature3D.EdgePair
                && Query(topology, pair, positions).Distance <= ClearanceFloor * 2)
            {
                throw new ArgumentException("AUR-CLOTH-006: Offset contact needs an intersection-free initial state with positive clearance.");
            }
        }
        // Distances at isolated VF/EE features do not detect an edge already piercing
        // a face interior. Test every nonincident segment/facet before trusting bounds.
        foreach (var edge in topology.Edges)
        {
            for (int face = 0; face < plan.Definition.Indices.Length; face += 3)
            {
                int a = plan.Definition.Indices[face];
                int b = plan.Definition.Indices[face + 1];
                int c = plan.Definition.Indices[face + 2];
                if (edge.A == a || edge.A == b || edge.A == c || edge.B == a || edge.B == b || edge.B == c)
                {
                    continue;
                }
                Vector3 normal = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
                float start = Vector3.Dot(positions[edge.A] - positions[a], normal);
                float end = Vector3.Dot(positions[edge.B] - positions[a], normal);
                if (start * end < 0)
                {
                    Vector3 point = Vector3.Lerp(positions[edge.A], positions[edge.B], start / (start - end));
                    var closest = TriangleSurface3D.ClosestPoint(point, positions[a], positions[b], positions[c]);
                    if (Vector3.Distance(point, closest.Point) <= ClearanceFloor)
                    {
                        throw new ArgumentException("AUR-CLOTH-006: An initial cloth edge pierces a nonincident face.");
                    }
                }
            }
        }
        if (Bounds(plan, positions, contacts, .4f).Any(bound => bound <= 0))
        {
            throw new ArgumentException("AUR-CLOTH-006: Initial cloth overlaps a collider or lacks positive clearance.");
        }
    }

    private static bool EndpointBlock(int a, int b, float t, Vector3 other,
        ClothContactTopology3D topology, IReadOnlyList<Vector3> positions)
    {
        if (t <= 0)
        {
            return VertexBlock(a, other, topology, positions);
        }
        if (t >= 1)
        {
            return VertexBlock(b, other, topology, positions);
        }
        return true;
    }

    private static bool VertexBlock(int vertex, Vector3 point,
        ClothContactTopology3D topology, IReadOnlyList<Vector3> positions)
    {
        foreach (int neighbor in topology.Neighbors[vertex])
        {
            if (Vector3.Dot(point - positions[vertex], positions[vertex] - positions[neighbor]) < 0)
            {
                return false;
            }
        }
        return true;
    }

    private static float SegmentParameter(Vector3 point, Vector3 a, Vector3 b)
        => Math.Clamp(LineParameter(point, a, b), 0, 1);

    private static float LineParameter(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 edge = b - a;
        float length = edge.LengthSquared();
        return length > 1e-20f ? Vector3.Dot(point - a, edge) / length : 0;
    }
}
