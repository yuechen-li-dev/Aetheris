using System.Collections.Immutable;
using System.Numerics;
using Aetheris.Cloth3D;
using Aetheris.Humanoid;

namespace Aetheris.Kernel.Firmament.Garment;

public sealed record GarmentDrapeResult(ClothSnapshot3D Snapshot, ClothMetrics3D Metrics,
    float MaximumSeamGap, float InitialSeamGap, bool ReplayExact, string BodyId, string BodyTopology,
    IReadOnlyList<GarmentContactOutlier> ContactOutliers, IReadOnlyList<GarmentStretchOutlier> StretchOutliers);

public sealed record GarmentContactOutlier(int Vertex, string Panel, bool Pinned, float Penetration, float[] Position);
public sealed record GarmentStretchOutlier(int A, int B, float Stretch, float RestLength);

public static class GarmentDraping
{
    public static ClothTriangleBody3D BodySurface(HumanoidGameplayBody body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var positions = body.Surface.Vertices.Select(vertex => new Vector3(
            (float)vertex.Position.X, (float)vertex.Position.Y, (float)vertex.Position.Z) * .001f).ToImmutableArray();
        var outer = body.Surface.Components.Where(component => component.Kind == HumanoidComponentKind.OuterSkin)
            .SelectMany(component => component.FaceIndices).Distinct().Order().ToArray();
        if (outer.Length == 0)
        {
            throw new InvalidDataException("garment-body-skin-missing: Body needs its declared OuterSkin component.");
        }
        var indices = outer.SelectMany(index =>
        {
            var face = body.Surface.Faces[index];
            return new[] { face.A, face.B, face.C };
        }).ToImmutableArray();
        return new(positions, indices);
    }

    /// <summary>Static Rest-pose authoring qualification on the shared CPU solver.
    /// Explicit pins are retained garment supports. No animation, friction, CCD or self-contact claim.</summary>
    public static GarmentDrapeResult Settle(CompiledGarment garment, HumanoidGameplayBody body, int ticks = 60)
    {
        if (ticks is < 1 or > 600)
        {
            throw new ArgumentOutOfRangeException(nameof(ticks));
        }
        if (garment.Source.Figure is null || garment.Source.Pose != "Rest")
        {
            throw new InvalidOperationException("garment-drape-binding-required: Declare Drape { figure: Anatolia; pose: Rest; } and supply its body artifact.");
        }
        var contacts = new ClothContacts3D { BodySurface = BodySurface(body), BodyClearance = garment.Source.BodyClearance };
        var options = new ClothStepOptions3D
        {
            Gravity = new(0, 0, -9.81f),
            Substeps = 8,
            Iterations = 24,
            ContactIterations = 1,
            VelocityDamping = 8,
        };
        using var solver = new ClothSolver3D(garment.Cloth);
        var initial = solver.Capture();
        float initialGap = garment.MaximumSeamGap(initial);
        // Sew with gravity disabled and a decreasing compliance, then settle.
        // This keeps the initial closing impulse from dominating garment motion.
        for (int stage = 0; stage < 30; stage++)
        {
            float scale = MathF.Pow(10, 6 * (1 - stage / 29f));
            solver.Step(options with { Gravity = Vector3.Zero, StitchComplianceScale = scale }, contacts);
        }
        for (int tick = 0; tick < ticks; tick++)
        {
            solver.Step(options, contacts);
        }
        var final = solver.Capture();
        var beforeReplay = final;
        solver.Step(options, contacts);
        var expected = solver.Capture();
        solver.Restore(beforeReplay);
        solver.Step(options, contacts);
        var replayed = solver.Capture();
        bool replay = expected.Positions.SequenceEqual(replayed.Positions)
            && expected.Velocities.SequenceEqual(replayed.Velocities) && expected.Tick == replayed.Tick;
        var pins = garment.Cloth.Definition.Pins.ToHashSet();
        var contactOutliers = final.Positions.Select((position, index) => new GarmentContactOutlier(index,
            garment.Panels.Single(panel => index >= panel.VertexStart && index < panel.VertexStart + panel.VertexCount).Identity,
            pins.Contains(index), garment.Cloth.Definition.Material.Thickness * .5f + contacts.BodyClearance - contacts.BodySurface!.Query(position).SignedDistance,
            [position.X, position.Y, position.Z])).Where(item => item.Penetration > .001f)
            .OrderByDescending(item => item.Penetration).Take(8).ToArray();
        var stretchOutliers = garment.Cloth.Constraints.Where(constraint => constraint.Vertices.Length == 2
            && constraint.Weights.IsEmpty).Select(constraint => new GarmentStretchOutlier(constraint.Vertices[0], constraint.Vertices[1],
                Vector3.Distance(final.Positions[constraint.Vertices[0]], final.Positions[constraint.Vertices[1]]) / constraint.RestLength - 1,
                constraint.RestLength)).OrderByDescending(item => item.Stretch).Take(8).ToArray();
        return new(final, ClothState3D.Measure(garment.Cloth, final, contacts), garment.MaximumSeamGap(final),
            initialGap, replay, body.Id, body.Surface.ConnectivityHash, contactOutliers, stretchOutliers);
    }
}
