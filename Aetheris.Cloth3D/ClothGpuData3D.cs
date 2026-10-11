using System.Collections.Immutable;
using System.Numerics;

namespace Aetheris.Cloth3D;

// Stable scalar phase codes, extended by the version 2 contact tape.
public enum ClothPhase3D
{
    Predict = 0,
    Constraints = 1,
    VertexContact = 2,
    TriangleContact = 3,
    Velocity = 4,
    CapturePins = 5,
    OffsetDistances = 6,
    OffsetReduce = 7,
    OffsetBounds = 8,
    OffsetContacts = 9,
}

public readonly record struct ClothDispatch3D(ClothPhase3D Phase, int Start, int Count, bool ResetLambda = false);

/// <summary>Version 2 scalar storage ABI: 16 floats/vertex, then 3 multiplier floats/constraint;
/// 12-float stencil headers plus variable vertex/weight pairs; 32 floats/dispatch header.</summary>
public sealed class ClothGpuData3D
{
    public CompiledCloth3D Plan { get; }
    public float[] Source { get; }
    public ImmutableArray<ClothDispatch3D> Constraints { get; }
    public ImmutableArray<ClothDispatch3D> Triangles { get; }
    public ImmutableArray<ClothDispatch3D> OffsetColors { get; }
    public ImmutableArray<ClothDispatch3D> OffsetReductions { get; }
    public int ContactSourceOffset { get; }
    public int AnchorOffset => Plan.Definition.Positions.Length * 16 + Plan.Constraints.Length * 3;
    public int DistanceOffset => AnchorOffset + Plan.Definition.Positions.Length * 4;
    public int ContactLambdaOffset => DistanceOffset + (Plan.ContactTopology?.Pairs.Length ?? 0)
        + (Plan.ContactTopology?.ReductionNodes.Length ?? 0);

    public ClothGpuData3D(CompiledCloth3D plan)
    {
        Plan = plan;
        int stencilOffset = (plan.Constraints.Length + plan.Definition.Indices.Length / 3) * 12;
        Source = new float[stencilOffset + plan.Constraints.Sum(constraint => constraint.Vertices.Length * 2)];
        var constraints = ImmutableArray.CreateBuilder<ClothDispatch3D>();
        var triangles = ImmutableArray.CreateBuilder<ClothDispatch3D>();
        int index = 0;
        foreach (var color in plan.ConstraintColors)
        {
            constraints.Add(new(ClothPhase3D.Constraints, index, color.Length));
            foreach (int original in color)
            {
                var constraint = plan.Constraints[original];
                int offset = index++ * 12;
                Source[offset] = (int)constraint.Kind;
                for (int local = 0; local < Math.Min(4, constraint.Vertices.Length); local++)
                {
                    Source[offset + 1 + local] = constraint.Vertices[local];
                }
                Source[offset + 5] = constraint.RestLength;
                Source[offset + 6] = constraint.Compliance;
                Source[offset + 7] = stencilOffset;
                Source[offset + 11] = constraint.Vertices.Length;
                for (int local = 0; local < constraint.Vertices.Length; local++)
                {
                    Source[stencilOffset++] = constraint.Vertices[local];
                    Source[stencilOffset++] = constraint.Weights.IsEmpty ? 0 : constraint.Weights[local];
                }
            }
        }
        foreach (var color in plan.TriangleColors)
        {
            triangles.Add(new(ClothPhase3D.TriangleContact, index, color.Length));
            foreach (int triangle in color)
            {
                int offset = index++ * 12;
                Source[offset] = 4;
                for (int local = 0; local < 3; local++)
                {
                    Source[offset + 1 + local] = plan.Definition.Indices[triangle * 3 + local];
                }
            }
        }
        Constraints = constraints.ToImmutable();
        Triangles = triangles.ToImmutable();
        var source = Source.ToList();
        var contactColors = ImmutableArray.CreateBuilder<ClothDispatch3D>();
        var reductions = ImmutableArray.CreateBuilder<ClothDispatch3D>();
        if (plan.ContactTopology is { } topology)
        {
            ContactSourceOffset = source.Count;
            source.AddRange(new float[8]);
            int table = ContactSourceOffset;
            source[table] = source.Count;
            foreach (var edge in topology.Edges)
            {
                source.AddRange(new float[] { edge.A, edge.B, edge.Opposite.Length,
                    edge.Opposite[0], edge.Opposite.Length == 2 ? edge.Opposite[1] : 0 });
            }
            source[table + 1] = source.Count;
            int vertexTable = source.Count;
            source.AddRange(new float[plan.Definition.Positions.Length * 4]);
            for (int vertex = 0; vertex < plan.Definition.Positions.Length; vertex++)
            {
                source[vertexTable + vertex * 4] = source.Count;
                source[vertexTable + vertex * 4 + 1] = topology.Neighbors[vertex].Length;
                source.AddRange(topology.Neighbors[vertex].Select(index => (float)index));
                source[vertexTable + vertex * 4 + 2] = source.Count;
                source[vertexTable + vertex * 4 + 3] = topology.IncidentFaces[vertex].Length;
                source.AddRange(topology.IncidentFaces[vertex].Select(index => (float)index));
            }
            source[table + 2] = source.Count;
            source.AddRange(plan.Definition.Indices.Select(index => (float)index));
            source[table + 3] = source.Count;
            foreach (var pair in topology.Pairs)
            {
                source.Add((int)pair.Kind);
                source.Add(pair.Vertices.Length);
                for (int local = 0; local < 4; local++)
                {
                    source.Add(local < pair.Vertices.Length ? pair.Vertices[local] : 0);
                }
                source.Add(pair.Feature);
                source.Add(0);
            }
            source[table + 4] = source.Count;
            foreach (var node in topology.ReductionNodes)
            {
                source.Add(node.Left);
                source.Add(node.Right);
            }
            source[table + 5] = source.Count;
            source.AddRange(topology.Roots.Select(index => (float)index));
            source[table + 6] = source.Count;
            foreach (var level in topology.ReductionLevels)
            {
                reductions.Add(new(ClothPhase3D.OffsetReduce, source.Count, level.Length));
                source.AddRange(level.Select(index => (float)index));
            }
            source[table + 7] = source.Count;
            foreach (var color in topology.Colors)
            {
                contactColors.Add(new(ClothPhase3D.OffsetContacts, source.Count, color.Length));
                source.AddRange(color.Select(index => (float)index));
            }
        }
        OffsetColors = contactColors.ToImmutable();
        OffsetReductions = reductions.ToImmutable();
        Source = source.ToArray();
    }

    public float[] State(ClothSnapshot3D snapshot)
    {
        ClothState3D.ValidateSnapshot(Plan, snapshot);
        float[] state = new float[Plan.ContactTopology is null ? AnchorOffset
            : ContactLambdaOffset + Plan.ContactTopology.Pairs.Length];
        for (int vertex = 0; vertex < snapshot.Positions.Length; vertex++)
        {
            int offset = vertex * 16;
            WriteVector(state, offset, snapshot.Positions[vertex]);
            WriteVector(state, offset + 6, snapshot.Velocities[vertex]);
            state[offset + 9] = Plan.InverseMasses[vertex];
            WriteVector(state, offset + 10, snapshot.PinTargets[vertex]);
        }
        return state;
    }

    public ClothSnapshot3D Snapshot(ReadOnlySpan<float> state, long tick)
    {
        int count = Plan.Definition.Positions.Length;
        if (state.Length < count * 16)
        {
            throw new ArgumentException("Truncated cloth GPU state.");
        }
        var positions = ImmutableArray.CreateBuilder<Vector3>(count);
        var velocities = ImmutableArray.CreateBuilder<Vector3>(count);
        var targets = ImmutableArray.CreateBuilder<Vector3>(count);
        for (int vertex = 0; vertex < count; vertex++)
        {
            positions.Add(ReadVector(state, vertex * 16));
            velocities.Add(ReadVector(state, vertex * 16 + 6));
            targets.Add(ReadVector(state, vertex * 16 + 10));
        }
        var snapshot = new ClothSnapshot3D(Plan.ContentKey, tick, positions.MoveToImmutable(),
            velocities.MoveToImmutable(), targets.MoveToImmutable());
        ClothState3D.ValidateSnapshot(Plan, snapshot);
        return snapshot;
    }

    public float[] Header(ClothStepOptions3D options, ClothContacts3D contacts, ClothDispatch3D dispatch, float fraction)
    {
        float h = options.DeltaSeconds / options.Substeps;
        float[] header = new float[32];
        if (!Enum.IsDefined(dispatch.Phase))
        {
            throw new ArgumentOutOfRangeException(nameof(dispatch));
        }
        header[0] = (int)dispatch.Phase;
        header[1] = dispatch.Count;
        header[2] = dispatch.Start;
        header[3] = Plan.Definition.Positions.Length;
        header[4] = h;
        WriteVector(header, 5, options.Gravity);
        header[8] = MathF.Exp(-options.VelocityDamping * h);
        header[9] = dispatch.ResetLambda ? 1 : 0;
        header[10] = fraction;
        header[31] = options.StitchComplianceScale;
        header[11] = Plan.Definition.Material.Thickness * .5f;
        if (contacts.PlaneNormal is { } normal)
        {
            header[12] = 1;
            WriteVector(header, 13, normal);
            header[16] = contacts.PlaneOffset;
        }
        if (contacts.SphereCenter is { } sphere)
        {
            header[17] = 1;
            WriteVector(header, 18, sphere);
            header[21] = contacts.SphereRadius;
        }
        if (Plan.ContactTopology is { } topology)
        {
            header[22] = topology.Pairs.Length;
            header[23] = ContactSourceOffset;
            header[24] = AnchorOffset;
            header[25] = DistanceOffset;
            header[26] = ContactLambdaOffset;
            header[27] = Plan.Definition.Material.Thickness;
            header[28] = options.ContactSafetyFactor;
            header[29] = options.OffsetContactCompliance;
            header[30] = 1;
        }
        return header;
    }

    private static Vector3 ReadVector(ReadOnlySpan<float> values, int offset) => new(values[offset], values[offset + 1], values[offset + 2]);
    private static void WriteVector(float[] values, int offset, Vector3 vector)
    {
        values[offset] = vector.X;
        values[offset + 1] = vector.Y;
        values[offset + 2] = vector.Z;
    }
}
