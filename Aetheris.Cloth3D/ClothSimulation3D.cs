using System.Collections.Immutable;
using System.Numerics;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Cloth3D;

public sealed record ClothStepOptions3D
{
    public float DeltaSeconds { get; init; } = 1f / 60;
    public int Substeps { get; init; } = 8;
    public int Iterations { get; init; } = 4;
    public int ContactIterations { get; init; } = 2;
    public Vector3 Gravity { get; init; } = new(0, -9.81f, 0);
    public float VelocityDamping { get; init; } = 1.5f;
    public float ContactSafetyFactor { get; init; } = .4f;
    public float StitchComplianceScale { get; init; } = 1;
    public float OffsetContactCompliance { get; init; } = .000001f;

    public void Validate()
    {
        ClothValidation3D.Finite(Gravity);
        if (!float.IsFinite(DeltaSeconds) || DeltaSeconds is < .00001f or > .1f
            || Substeps is < 1 or > 64 || Iterations is < 1 or > 64 || ContactIterations is < 1 or > 32
            || !float.IsFinite(VelocityDamping) || VelocityDamping < 0
            || !float.IsFinite(ContactSafetyFactor) || ContactSafetyFactor <= 0 || ContactSafetyFactor >= .5f
            || !float.IsFinite(StitchComplianceScale) || StitchComplianceScale is < 1 or > 100000000
            || !float.IsFinite(OffsetContactCompliance) || OffsetContactCompliance < 0)
        {
            throw new ArgumentException("Invalid bounded cloth step options.");
        }
    }
}

/// <summary>One-way, discrete contact baseline. Plane equation dot(normal,p) >= offset;
/// sphere contact covers vertices AND triangle interiors. No CCD or self-contact claim.</summary>
public sealed record ClothContacts3D
{
    public IClothBodySurface3D? BodySurface { get; init; }
    public float BodyClearance { get; init; }
    public Vector3? PlaneNormal { get; init; }
    public float PlaneOffset { get; init; }
    public Vector3? SphereCenter { get; init; }
    public float SphereRadius { get; init; }

    public void Validate()
    {
        if (PlaneNormal is { } normal)
        {
            ClothValidation3D.Finite(normal);
            if (MathF.Abs(normal.LengthSquared() - 1) > .0001f)
            {
                throw new ArgumentException("Cloth plane requires a unit normal.");
            }
        }
        if (SphereCenter is { } center)
        {
            ClothValidation3D.Finite(center);
            if (!float.IsFinite(SphereRadius) || SphereRadius <= 0)
            {
                throw new ArgumentException("Cloth sphere radius must be positive.");
            }
        }
        if (!float.IsFinite(BodyClearance) || BodyClearance is < 0 or > .03f)
        {
            throw new ArgumentException("Body clearance must be finite and between zero and 30mm.");
        }
        if (!float.IsFinite(PlaneOffset))
        {
            throw new ArgumentException("Nonfinite plane offset.");
        }
    }
}

public sealed record ClothSnapshot3D(string ContentKey, long Tick, ImmutableArray<Vector3> Positions,
    ImmutableArray<Vector3> Velocities, ImmutableArray<Vector3> PinTargets);

public sealed record ClothMetrics3D(float MaximumStretch, float MaximumPinError,
    float MaximumSurfacePenetration, float BendEnergy, bool Finite)
{
    public ClothOffsetMetrics3D? OffsetContact { get; init; }
}

public sealed record ClothOffsetMetrics3D(float MinimumFeatureClearance,
    float MaximumActiveOffsetOverlap, int ActivePairs);

public interface IClothSolver3D : IDisposable
{
    CompiledCloth3D Plan { get; }
    long Tick { get; }
    void SetPin(int vertex, Vector3 target);
    void Step(ClothStepOptions3D options, ClothContacts3D contacts);
    ClothSnapshot3D Capture();
    void Restore(ClothSnapshot3D snapshot);
}

/// <summary>Deterministic CPU reference for the same colored schedule used by Vulkan.
/// Multipliers reset per substep, accumulate across iterations, and are not warm-started.</summary>
public sealed class ClothSolver3D : IClothSolver3D
{
    private readonly Vector3[] positions;
    private readonly Vector3[] velocities;
    private readonly Vector3[] previous;
    private readonly Vector3[] targets;
    private readonly Vector3[] pinStarts;
    private readonly Vector3[] lambda;
    private readonly Vector3[] contactAnchor;
    private float[]? contactBounds;
    private readonly float[] contactLambda;

    public CompiledCloth3D Plan { get; }
    public long Tick { get; private set; }

    public ClothSolver3D(CompiledCloth3D plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        Plan = plan;
        positions = (plan.Definition.InitialPositions.IsEmpty
            ? plan.Definition.Positions : plan.Definition.InitialPositions).ToArray();
        previous = new Vector3[positions.Length];
        velocities = new Vector3[positions.Length];
        targets = positions.ToArray();
        pinStarts = new Vector3[positions.Length];
        lambda = new Vector3[plan.Constraints.Length];
        contactAnchor = new Vector3[positions.Length];
        contactLambda = new float[plan.ContactTopology?.Pairs.Length ?? 0];
    }

    public void SetPin(int vertex, Vector3 target)
    {
        ClothState3D.ValidatePin(Plan, vertex, target);
        targets[vertex] = target;
    }

    public void Step(ClothStepOptions3D options, ClothContacts3D contacts)
    {
        ClothState3D.ValidateStep(Plan, options, contacts);
        if (Plan.ContactTopology is not null)
        {
            ClothOffsetContact3D.ValidateInitial(Plan, Capture(), contacts);
        }
        float h = options.DeltaSeconds / options.Substeps;
        float damping = MathF.Exp(-options.VelocityDamping * h);
        positions.CopyTo(pinStarts, 0);
        for (int substep = 0; substep < options.Substeps; substep++)
        {
            Array.Clear(lambda);
            Array.Clear(contactLambda);
            RefreshBounds(options, contacts);
            float fraction = (substep + 1f) / options.Substeps;
            for (int vertex = 0; vertex < positions.Length; vertex++)
            {
                previous[vertex] = positions[vertex];
                if (Plan.InverseMasses[vertex] == 0)
                {
                    positions[vertex] = Vector3.Lerp(pinStarts[vertex], targets[vertex], fraction);
                }
                else
                {
                    velocities[vertex] += options.Gravity * h;
                    positions[vertex] += velocities[vertex] * h;
                }
            }
            LimitPositions();
            for (int iteration = 0; iteration < options.Iterations; iteration++)
            {
                RefreshBounds(options, contacts);
                foreach (var color in Plan.ConstraintColors)
                {
                    foreach (int index in color)
                    {
                        ProjectConstraint(index, h, options.StitchComplianceScale);
                    }
                }
                for (int contact = 0; contact < options.ContactIterations; contact++)
                {
                    ProjectContacts(contacts);
                    ProjectOffsetContacts(options, h);
                }
            }
            for (int vertex = 0; vertex < positions.Length; vertex++)
            {
                velocities[vertex] = (positions[vertex] - previous[vertex]) * (damping / h);
            }
        }
        Tick++;
    }

    private void RefreshBounds(ClothStepOptions3D options, ClothContacts3D contacts)
    {
        if (Plan.ContactTopology is null)
        {
            return;
        }
        positions.CopyTo(contactAnchor, 0);
        contactBounds = ClothOffsetContact3D.Bounds(Plan, contactAnchor, contacts, options.ContactSafetyFactor);
    }

    private void LimitPositions()
    {
        if (contactBounds is null)
        {
            return;
        }
        for (int vertex = 0; vertex < positions.Length; vertex++)
        {
            positions[vertex] = ClothOffsetContact3D.Clip(positions[vertex], contactAnchor[vertex], contactBounds[vertex]);
        }
    }

    private void Move(int vertex, Vector3 correction)
    {
        Vector3 proposed = positions[vertex] + correction;
        positions[vertex] = contactBounds is null ? proposed
            : ClothOffsetContact3D.Clip(proposed, contactAnchor[vertex], contactBounds[vertex]);
    }

    private void ProjectOffsetContacts(ClothStepOptions3D options, float h)
    {
        if (Plan.ContactTopology is not { } topology)
        {
            return;
        }
        float alpha = options.OffsetContactCompliance / (h * h);
        foreach (var color in topology.Colors)
        {
            foreach (int index in color)
            {
                var pair = topology.Pairs[index];
                var query = ClothOffsetContact3D.Query(topology, pair, positions);
                if (!query.Active || query.Distance >= Plan.Definition.Material.Thickness)
                {
                    contactLambda[index] = 0;
                    continue;
                }
                Vector2 potential = ClothOffsetContact3D.Potential(query.Distance, Plan.Definition.Material.Thickness);
                float movable = 0;
                for (int local = 0; local < pair.Vertices.Length; local++)
                {
                    float weight = query.Weights[local] * potential.Y;
                    movable += Plan.InverseMasses[pair.Vertices[local]] * weight * weight;
                }
                if (movable <= 1e-20f)
                {
                    continue;
                }
                float increment = (-potential.X - alpha * contactLambda[index]) / (movable + alpha);
                contactLambda[index] += increment;
                for (int local = 0; local < pair.Vertices.Length; local++)
                {
                    int vertex = pair.Vertices[local];
                    Move(vertex, query.Normal * (Plan.InverseMasses[vertex] * query.Weights[local] * potential.Y * increment));
                }
            }
        }
    }

    private void ProjectConstraint(int index, float h, float stitchComplianceScale)
    {
        var constraint = Plan.Constraints[index];
        float compliance = constraint.Kind == ClothConstraintKind3D.Stitch
            ? constraint.Compliance * stitchComplianceScale : constraint.Compliance;
        float alpha = compliance / (h * h);
        if (constraint.Kind is not (ClothConstraintKind3D.FlatBend or ClothConstraintKind3D.VertexBend or ClothConstraintKind3D.Stitch))
        {
            int a = constraint.Vertices[0];
            int b = constraint.Vertices[1];
            float wa = Plan.InverseMasses[a];
            float wb = Plan.InverseMasses[b];
            Vector3 difference = positions[a] - positions[b];
            float length = difference.Length();
            if (length < 1e-10f || wa + wb == 0)
            {
                return;
            }
            float delta = (-(length - constraint.RestLength) - alpha * lambda[index].X) / (wa + wb + alpha);
            lambda[index].X += delta;
            Vector3 correction = difference * (delta / length);
            Move(a, correction * wa);
            Move(b, -correction * wb);
            return;
        }
        Vector3 curvature = Vector3.Zero;
        float denominator = alpha;
        float movable = 0;
        for (int local = 0; local < constraint.Vertices.Length; local++)
        {
            int vertex = constraint.Vertices[local];
            float weight = constraint.Weights[local];
            curvature += positions[vertex] * weight;
            float term = Plan.InverseMasses[vertex] * weight * weight;
            denominator += term;
            movable += term;
        }
        if (movable <= 1e-20f)
        {
            return;
        }
        Vector3 increment = (-curvature - alpha * lambda[index]) / denominator;
        lambda[index] += increment;
        for (int local = 0; local < constraint.Vertices.Length; local++)
        {
            int vertex = constraint.Vertices[local];
            Move(vertex, increment * (Plan.InverseMasses[vertex] * constraint.Weights[local]));
        }
    }

    private void ProjectContacts(ClothContacts3D contacts)
    {
        float thickness = Plan.Definition.Material.Thickness * .5f;
        for (int vertex = 0; vertex < positions.Length; vertex++)
        {
            if (Plan.InverseMasses[vertex] == 0)
            {
                continue;
            }
            if (contacts.BodySurface is { } body)
            {
                var sample = body.Query(positions[vertex]);
                float clearance = thickness + contacts.BodyClearance;
                if (sample.SignedDistance < clearance)
                {
                    Move(vertex, sample.Normal * (clearance - sample.SignedDistance));
                }
            }
            if (contacts.PlaneNormal is { } normal)
            {
                float gap = Vector3.Dot(normal, positions[vertex]) - contacts.PlaneOffset - thickness;
                if (gap < 0)
                {
                    Move(vertex, -normal * gap);
                }
            }
            if (contacts.SphereCenter is { } center)
            {
                Vector3 delta = positions[vertex] - center;
                float distance = delta.Length();
                float radius = contacts.SphereRadius + thickness;
                if (distance < radius)
                {
                    Vector3 outward = distance > 1e-10f ? delta / distance : Vector3.UnitY;
                    Move(vertex, center + outward * radius - positions[vertex]);
                }
            }
        }
        if (contacts.BodySurface is { } bodySurface)
        {
            foreach (var color in Plan.TriangleColors)
            {
                foreach (int triangle in color)
                {
                    int a = Plan.Definition.Indices[triangle * 3];
                    int b = Plan.Definition.Indices[triangle * 3 + 1];
                    int c = Plan.Definition.Indices[triangle * 3 + 2];
                    Vector3 center = (positions[a] + positions[b] + positions[c]) / 3;
                    float radius = MathF.Max(Vector3.Distance(center, positions[a]),
                        MathF.Max(Vector3.Distance(center, positions[b]), Vector3.Distance(center, positions[c])));
                    // A positive nearest-surface distance beyond the containing sphere
                    // certifies this triangle is outside the static contact neighborhood.
                    if (bodySurface.Query(center).SignedDistance > radius + thickness + contacts.BodyClearance) continue;
                    foreach (Vector3 barycentric in ClothBodyContact3D.Samples)
                    {
                        Vector3 point = positions[a] * barycentric.X + positions[b] * barycentric.Y + positions[c] * barycentric.Z;
                        var sample = bodySurface.Query(point);
                        float penetration = thickness + contacts.BodyClearance - sample.SignedDistance;
                        if (penetration <= 0) continue;
                        float denominator = Plan.InverseMasses[a] * barycentric.X * barycentric.X
                            + Plan.InverseMasses[b] * barycentric.Y * barycentric.Y
                            + Plan.InverseMasses[c] * barycentric.Z * barycentric.Z;
                        if (denominator <= 1e-20f) continue;
                        Vector3 correction = sample.Normal * (penetration / denominator);
                        Move(a, correction * (Plan.InverseMasses[a] * barycentric.X));
                        Move(b, correction * (Plan.InverseMasses[b] * barycentric.Y));
                        Move(c, correction * (Plan.InverseMasses[c] * barycentric.Z));
                    }
                }
            }
        }
        if (contacts.SphereCenter is not { } sphere)
        {
            return;
        }
        foreach (var color in Plan.TriangleColors)
        {
            foreach (int triangle in color)
            {
                int a = Plan.Definition.Indices[triangle * 3];
                int b = Plan.Definition.Indices[triangle * 3 + 1];
                int c = Plan.Definition.Indices[triangle * 3 + 2];
                var closest = TriangleSurface3D.ClosestPoint(sphere, positions[a], positions[b], positions[c]);
                Vector3 delta = closest.Point - sphere;
                float distance = delta.Length();
                float penetration = contacts.SphereRadius + thickness - distance;
                if (penetration <= 0)
                {
                    continue;
                }
                Vector3 barycentric = closest.Barycentric;
                float denominator = Plan.InverseMasses[a] * barycentric.X * barycentric.X
                    + Plan.InverseMasses[b] * barycentric.Y * barycentric.Y
                    + Plan.InverseMasses[c] * barycentric.Z * barycentric.Z;
                if (denominator <= 1e-20f)
                {
                    continue;
                }
                Vector3 normal = distance > 1e-10f ? delta / distance : Vector3.UnitY;
                Vector3 correction = normal * (penetration / denominator);
                Move(a, correction * (Plan.InverseMasses[a] * barycentric.X));
                Move(b, correction * (Plan.InverseMasses[b] * barycentric.Y));
                Move(c, correction * (Plan.InverseMasses[c] * barycentric.Z));
            }
        }
    }

    public ClothSnapshot3D Capture() => new(Plan.ContentKey, Tick, positions.ToImmutableArray(),
        velocities.ToImmutableArray(), targets.ToImmutableArray());

    public void Restore(ClothSnapshot3D snapshot)
    {
        ClothState3D.ValidateSnapshot(Plan, snapshot);
        if (Plan.ContactTopology is not null)
        {
            ClothOffsetContact3D.ValidateInitial(Plan, snapshot, new());
        }
        snapshot.Positions.CopyTo(positions);
        snapshot.Velocities.CopyTo(velocities);
        snapshot.PinTargets.CopyTo(targets);
        Tick = snapshot.Tick;
    }

    public void Dispose() { }
}

public static class ClothState3D
{
    public static void ValidateStep(CompiledCloth3D plan, ClothStepOptions3D options, ClothContacts3D contacts)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(contacts);
        options.Validate();
        contacts.Validate();
        float h = options.DeltaSeconds / options.Substeps;
        if (plan.Constraints.Any(constraint => !float.IsFinite(constraint.Compliance / (h * h))))
        {
            throw new ArgumentException("Cloth compliance and timestep exceed finite solver arithmetic.");
        }
        if (!float.IsFinite(options.OffsetContactCompliance / (h * h)))
        {
            throw new ArgumentException("Offset contact compliance exceeds finite solver arithmetic.");
        }
    }

    public static void ValidatePin(CompiledCloth3D plan, int vertex, Vector3 target)
    {
        ClothValidation3D.Finite(target);
        if (vertex < 0 || vertex >= plan.InverseMasses.Length || plan.InverseMasses[vertex] != 0)
        {
            throw new ArgumentException("Pin commands require an authored pinned vertex.");
        }
    }

    public static void ValidateSnapshot(CompiledCloth3D plan, ClothSnapshot3D snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        int count = plan.Definition.Positions.Length;
        if (snapshot.ContentKey != plan.ContentKey || snapshot.Tick < 0 || snapshot.Positions.IsDefault
            || snapshot.Velocities.IsDefault || snapshot.PinTargets.IsDefault || snapshot.Positions.Length != count
            || snapshot.Velocities.Length != count || snapshot.PinTargets.Length != count)
        {
            throw new ArgumentException("Cloth snapshot belongs to another topology/material or has invalid state dimensions.");
        }
        foreach (Vector3 value in snapshot.Positions)
        {
            ValidateVector(value);
        }
        foreach (Vector3 value in snapshot.Velocities)
        {
            ValidateVector(value);
        }
        foreach (Vector3 value in snapshot.PinTargets)
        {
            ValidateVector(value);
        }
    }

    private static void ValidateVector(Vector3 value) => ClothValidation3D.Finite(value);

    public static ClothMetrics3D Measure(CompiledCloth3D plan, ClothSnapshot3D snapshot, ClothContacts3D contacts)
    {
        ValidateSnapshot(plan, snapshot);
        contacts.Validate();
        var positions = snapshot.Positions;
        float stretch = 0;
        float bending = 0;
        foreach (var constraint in plan.Constraints)
        {
            if (constraint.Kind is not (ClothConstraintKind3D.FlatBend or ClothConstraintKind3D.VertexBend or ClothConstraintKind3D.Stitch))
            {
                float length = Vector3.Distance(positions[constraint.Vertices[0]], positions[constraint.Vertices[1]]);
                stretch = MathF.Max(stretch, length / constraint.RestLength - 1);
            }
            else
            {
                Vector3 curvature = Vector3.Zero;
                for (int local = 0; local < constraint.Vertices.Length; local++)
                {
                    curvature += positions[constraint.Vertices[local]] * constraint.Weights[local];
                }
                if (constraint.Kind != ClothConstraintKind3D.Stitch)
                {
                    bending += .5f * curvature.LengthSquared();
                }
            }
        }
        float pinError = 0;
        foreach (int vertex in plan.Definition.Pins)
        {
            pinError = MathF.Max(pinError, Vector3.Distance(positions[vertex], snapshot.PinTargets[vertex]));
        }
        float penetration = 0;
        float halfThickness = plan.Definition.Material.Thickness * .5f;
        if (contacts.PlaneNormal is { } normal)
        {
            foreach (Vector3 position in positions)
            {
                penetration = MathF.Max(penetration, contacts.PlaneOffset + halfThickness - Vector3.Dot(normal, position));
            }
        }
        if (contacts.SphereCenter is { } sphere)
        {
            for (int triangle = 0; triangle < plan.Definition.Indices.Length; triangle += 3)
            {
                var indices = plan.Definition.Indices;
                var closest = TriangleSurface3D.ClosestPoint(sphere, positions[indices[triangle]],
                    positions[indices[triangle + 1]], positions[indices[triangle + 2]]);
                penetration = MathF.Max(penetration, contacts.SphereRadius + halfThickness - Vector3.Distance(sphere, closest.Point));
            }
        }
        if (contacts.BodySurface is { } body)
        {
            foreach (Vector3 position in positions)
            {
                penetration = MathF.Max(penetration, halfThickness + contacts.BodyClearance - body.Query(position).SignedDistance);
            }
            for (int face = 0; face < plan.Definition.Indices.Length; face += 3)
            {
                int a = plan.Definition.Indices[face];
                int b = plan.Definition.Indices[face + 1];
                int c = plan.Definition.Indices[face + 2];
                foreach (Vector3 barycentric in ClothBodyContact3D.Samples)
                {
                    Vector3 point = positions[a] * barycentric.X + positions[b] * barycentric.Y + positions[c] * barycentric.Z;
                    penetration = MathF.Max(penetration, halfThickness + contacts.BodyClearance - body.Query(point).SignedDistance);
                }
            }
        }
        ClothOffsetMetrics3D? offsetMetrics = null;
        if (plan.ContactTopology is { } topology)
        {
            float clearance = 1e6f;
            float overlap = 0;
            int active = 0;
            foreach (var pair in topology.Pairs)
            {
                var query = ClothOffsetContact3D.Query(topology, pair, positions);
                if (pair.Kind is ClothContactFeature3D.Face or ClothContactFeature3D.EdgePair)
                {
                    clearance = MathF.Min(clearance, query.Distance);
                }
                if (query.Active && query.Distance < plan.Definition.Material.Thickness)
                {
                    overlap = MathF.Max(overlap, plan.Definition.Material.Thickness - query.Distance);
                    active++;
                }
            }
            offsetMetrics = new(clearance, overlap, active);
        }
        return new(stretch, pinError, penetration, bending,
            float.IsFinite(stretch) && float.IsFinite(bending)) { OffsetContact = offsetMetrics };
    }
}
