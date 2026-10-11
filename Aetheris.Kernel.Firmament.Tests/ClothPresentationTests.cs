using System.Collections.Immutable;
using System.Numerics;
using Aetheris.Cloth3D;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ClothPresentationTests
{
    [Fact]
    public void RefinementPreservesCutBoundariesAndDoesNotChangeSimulation()
    {
        var plan = ClothDefinition3D.Grid("display", 3, 3, new(1, 1), Vector3.Zero).Compile();
        using var solver = new ClothSolver3D(plan);
        var snapshot = solver.Capture();
        snapshot = snapshot with { Positions = snapshot.Positions.SetItem(4, new(.5f, .2f, .5f)) };
        var presentation = ClothPresentationBinding3D.Compile(plan, 2);
        var originalPositions = snapshot.Positions;
        var mesh = presentation.Evaluate(snapshot);
        Assert.Equal(plan.Definition.Indices.Length * 16, mesh.Indices.Length);
        Assert.Equal(9, snapshot.Positions.Length);
        Assert.Equal(24, plan.Definition.Indices.Length);
        Assert.InRange(mesh.Positions[4].Y, 0, .199f);
        foreach (int vertex in new[] { 0, 1, 2, 3, 5, 6, 7, 8 })
        {
            Assert.Equal(snapshot.Positions[vertex], mesh.Positions[vertex]);
        }
        // No refined vertex can move beyond the convex hull of its simulation support.
        foreach (var stencil in presentation.Stencils)
        {
            Assert.InRange(MathF.Abs(stencil.Weights.Sum() - 1), 0, 1e-6f);
            Assert.All(stencil.Weights, weight => Assert.InRange(weight, 0, 1));
        }
        Assert.Equal(originalPositions, snapshot.Positions);
        Assert.Equal(plan.ContentKey, presentation.SourceContentKey);
    }

    [Fact]
    public void PresentationCommutesWithRigidMotionAndRejectsAnotherTopology()
    {
        var plan = ClothDefinition3D.Grid("motion", 4, 4, new(1, 1), Vector3.Zero).Compile();
        using var solver = new ClothSolver3D(plan);
        var snapshot = solver.Capture();
        var presentation = ClothPresentationBinding3D.Compile(plan, 1);
        var first = presentation.Evaluate(snapshot);
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, .7f);
        Vector3 translation = new(.3f, -.2f, .8f);
        var moved = snapshot with
        {
            Positions = snapshot.Positions.Select(point => Vector3.Transform(point, rotation) + translation).ToImmutableArray(),
        };
        var second = presentation.Evaluate(moved);
        for (int vertex = 0; vertex < first.Positions.Length; vertex++)
        {
            Vector3 expected = Vector3.Transform(first.Positions[vertex], rotation) + translation;
            Assert.InRange(Vector3.Distance(expected, second.Positions[vertex]), 0, 1e-6f);
        }
        Assert.Throws<ArgumentException>(() => presentation.Evaluate(snapshot with { ContentKey = "other" }));
        Assert.Throws<ArgumentOutOfRangeException>(() => ClothPresentationBinding3D.Compile(plan, 3));
        var large = ClothDefinition3D.Grid("budget", 100, 100, new(1, 1), Vector3.Zero).Compile();
        Assert.Contains("cloth-presentation-budget", Assert.Throws<ArgumentException>(
            () => ClothPresentationBinding3D.Compile(large, 2)).Message);
    }
}
