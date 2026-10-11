using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using Aetheris.Cloth3D;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ClothQuadSurfaceTests
{
    [Fact]
    public void GrainResponseIsContinuousAndAffineEnergyIsResolutionIndependent()
    {
        var baseline = Patch(3);
        var deformed = Deform(baseline.Positions);
        float energy = Energy(baseline.Compile(), deformed);
        Assert.True(energy > 0);
        var rotated = baseline with { Surface = baseline.Surface! with { GrainRadians = .01f * MathF.PI / 180 } };
        float rotatedEnergy = Energy(rotated.Compile(), deformed);
        Assert.InRange(MathF.Abs(rotatedEnergy / energy - 1), 0, .001f);
        foreach (int side in new[] { 5, 9 })
        {
            var refined = Patch(side);
            Assert.InRange(MathF.Abs(Energy(refined.Compile(), Deform(refined.Positions)) / energy - 1), 0, .0001f);
            var plan = refined.Compile();
            Assert.InRange(MathF.Abs(plan.InverseMasses.Sum(mass => 1 / mass) - .2f), 0, 1e-6f);
        }
        var halfTurn = baseline with { Surface = baseline.Surface! with { GrainRadians = MathF.PI / 2 } };
        Assert.True(MathF.Abs(Energy(halfTurn.Compile(), deformed) / energy - 1) > .1f);
        Assert.DoesNotContain(baseline.Compile().Constraints, item => item.Kind is
            ClothConstraintKind3D.Warp or ClothConstraintKind3D.Weft or ClothConstraintKind3D.Diagonal);
    }

    [Fact]
    public void RenderingDiagonalDoesNotChangeMassesConstraintsOrSolverEvolution()
    {
        var definition = Patch(4);
        var alternate = definition with { Indices = definition.Surface!.Triangulate(true) };
        var first = definition.Compile();
        var second = alternate.Compile();
        Assert.True(first.InverseMasses.SequenceEqual(second.InverseMasses));
        Assert.Equal(first.Constraints.Length, second.Constraints.Length);
        for (int index = 0; index < first.Constraints.Length; index++)
        {
            var a = first.Constraints[index];
            var b = second.Constraints[index];
            Assert.Equal(a.Kind, b.Kind);
            Assert.Equal(a.Compliance, b.Compliance);
            Assert.Equal(a.RestLength, b.RestLength);
            Assert.True(a.Vertices.SequenceEqual(b.Vertices));
            Assert.True(a.Weights.SequenceEqual(b.Weights));
            Assert.True(a.TransverseWeights.SequenceEqual(b.TransverseWeights));
        }
        using var solverA = new ClothSolver3D(first);
        using var solverB = new ClothSolver3D(second);
        solverA.Restore(solverA.Capture() with { Positions = Deform(definition.Positions) });
        solverB.Restore(solverB.Capture() with { Positions = Deform(definition.Positions) });
        for (int tick = 0; tick < 8; tick++)
        {
            solverA.Step(new() { Gravity = Vector3.Zero }, new());
            solverB.Step(new() { Gravity = Vector3.Zero }, new());
        }
        Assert.True(solverA.Capture().Positions.SequenceEqual(solverB.Capture().Positions));
        Assert.True(solverA.Capture().Velocities.SequenceEqual(solverB.Capture().Velocities));
    }

    [Fact]
    public void FittedCurvedRestIsStressFreeAndCommutesWithRigidMotion()
    {
        var definition = Curved();
        var plan = definition.Compile();
        foreach (var constraint in plan.Constraints)
        {
            Assert.InRange(MathF.Abs(ClothSurfaceMechanics3D.Evaluate(constraint, definition.Positions).Residual), 0, 2e-6f);
        }
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.Normalize(new(1, 2, 3)), .8f);
        var moved = definition.Positions.Select(point => Vector3.Transform(point, rotation) + new Vector3(.4f, -.2f, .3f)).ToImmutableArray();
        foreach (var constraint in plan.Constraints)
        {
            Assert.InRange(MathF.Abs(ClothSurfaceMechanics3D.Evaluate(constraint, moved).Residual), 0, 1e-5f);
        }
        using var solver = new ClothSolver3D(plan);
        for (int tick = 0; tick < 8; tick++)
        {
            solver.Step(new() { Gravity = Vector3.Zero }, new());
        }
        float drift = solver.Capture().Positions.Zip(definition.Positions, Vector3.Distance).Max();
        Assert.InRange(drift, 0, 1e-5f);
        var flat = definition with { Surface = definition.Surface! with { Rest = ClothSurfaceRest3D.FlatFabric } };
        Assert.True(Energy(flat.Compile(), definition.Positions) > .01f);
    }

    [Fact]
    public void AnalyticalGradientsMatchFiniteDifferencesForEverySurfaceConstraintKind()
    {
        var definition = Curved();
        var positions = Deform(definition.Positions).SetItem(3, definition.Positions[3] + new Vector3(.01f, .03f, -.02f));
        var plan = definition.Compile();
        foreach (var constraint in plan.Constraints.GroupBy(item => item.Kind).Select(group => group.First()))
        {
            var evaluation = ClothSurfaceMechanics3D.Evaluate(constraint, positions);
            for (int local = 0; local < constraint.Vertices.Length; local++)
            {
                int vertex = constraint.Vertices[local];
                for (int axis = 0; axis < 3; axis++)
                {
                    Vector3 offset = axis switch { 0 => Vector3.UnitX, 1 => Vector3.UnitY, _ => Vector3.UnitZ };
                    const float epsilon = .0001f;
                    float plus = ClothSurfaceMechanics3D.Evaluate(constraint,
                        positions.SetItem(vertex, positions[vertex] + offset * epsilon)).Residual;
                    float minus = ClothSurfaceMechanics3D.Evaluate(constraint,
                        positions.SetItem(vertex, positions[vertex] - offset * epsilon)).Residual;
                    float numeric = (plus - minus) / (2 * epsilon);
                    float analytical = Vector3.Dot(evaluation.Gradients[local], offset);
                    Assert.True(MathF.Abs(numeric - analytical) < .003f,
                        $"{constraint.Kind} vertex {vertex} axis {axis}: analytic {analytical}, numeric {numeric}");
                }
            }
        }
    }

    [Fact]
    public void SurfaceTransportAndInvalidGeometryAreExplicit()
    {
        var definition = Curved();
        var json = new JsonSerializerOptions { IncludeFields = true };
        var restored = JsonSerializer.Deserialize<ClothDefinition3D>(JsonSerializer.Serialize(definition, json), json)!;
        Assert.Equal(definition.Compile().ContentKey, restored.Compile().ContentKey);
        Assert.Throws<ArgumentException>(() => (definition with { OffsetContact = true }).Compile());
        Assert.Throws<ArgumentException>(() => (definition with { Surface = definition.Surface! with { GrainRadians = float.NaN } }).Compile());
        var invalid = definition.Surface!.Quads[0] with { Coordinates = [new(0, 0), new(0, 1), new(1, 1), new(1, 0)] };
        Assert.Throws<ArgumentException>(() => (definition with
        {
            Surface = definition.Surface with { Quads = definition.Surface.Quads.SetItem(0, invalid) },
        }).Compile());
        Assert.Throws<ArgumentException>(() => (definition with { Indices = definition.Indices.SetItem(0, 10) }).Compile());
    }

    private static ClothDefinition3D Patch(int side)
    {
        return ClothQuadSurface3D.Grid("patch", side, side, new(1, 1), Vector3.Zero) with
        {
            Material = new() { WarpCompliance = .001f, WeftCompliance = .004f, DiagonalCompliance = .002f, BendCompliance = .01f },
        };
    }

    private static ClothDefinition3D Curved()
    {
        var definition = Patch(5);
        return definition with
        {
            Positions = definition.Positions.Select(point => point with { Y = .1f * MathF.Sin(MathF.PI * point.X) }).ToImmutableArray(),
            Surface = definition.Surface! with { Rest = ClothSurfaceRest3D.FittedSurface },
        };
    }

    private static ImmutableArray<Vector3> Deform(ImmutableArray<Vector3> positions)
    {
        return positions.Select(point => new Vector3(point.X * 1.1f + .03f * point.Z, point.Y, point.Z * .98f)).ToImmutableArray();
    }

    private static float Energy(CompiledCloth3D plan, ImmutableArray<Vector3> positions)
    {
        return plan.Constraints.Sum(constraint =>
        {
            float residual = ClothSurfaceMechanics3D.Evaluate(constraint, positions).Residual;
            return .5f * residual * residual / constraint.Compliance;
        });
    }
}
