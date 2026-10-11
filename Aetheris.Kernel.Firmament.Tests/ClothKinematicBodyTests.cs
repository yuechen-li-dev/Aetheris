using System.Collections.Immutable;
using System.Numerics;
using Aetheris.Cloth3D;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ClothKinematicBodyTests
{
    [Fact]
    public void RefitMatchesRebuiltHierarchyAndRejectsInvalidGeometryAtomically()
    {
        // Eighteen triangles exercise internal nodes as well as leaves.
        var grid = ClothDefinition3D.Grid("body", 4, 4, new(1, 1), Vector3.Zero);
        var surface = new ClothTriangleBody3D(grid.Positions, grid.Indices);
        var moved = grid.Positions.Select(point => new Vector3(point.X * 1.2f + .2f,
            .1f * point.X + .3f, point.Z * .8f - .2f)).ToImmutableArray();
        surface.Refit(moved);
        var rebuilt = new ClothTriangleBody3D(moved, grid.Indices);
        Vector3[] queries = [new(.4f, .5f, .2f), new(-1, .1f, -.5f), new(2, 1, 3)];
        foreach (var point in queries)
        {
            var expected = rebuilt.Query(point);
            var actual = surface.Query(point);
            Assert.InRange(Vector3.Distance(expected.Point, actual.Point), 0, 1e-6f);
            Assert.InRange(Vector3.Distance(expected.Normal, actual.Normal), 0, 1e-6f);
            Assert.InRange(MathF.Abs(expected.SignedDistance - actual.SignedDistance), 0, 1e-6f);
        }
        var before = surface.Query(queries[0]);
        var degenerate = moved.ToArray();
        degenerate[grid.Indices[1]] = degenerate[grid.Indices[0]];
        Assert.Throws<ArgumentException>(() => surface.Refit(degenerate));
        Assert.Equal(before, surface.Query(queries[0]));
        Assert.Throws<ArgumentException>(() => surface.Refit(moved.Take(3).ToArray()));
        var nonfinite = moved.ToArray();
        nonfinite[^1] = new(float.NaN, 0, 0);
        Assert.Throws<ArgumentException>(() => surface.Refit(nonfinite));
        Assert.Equal(before, surface.Query(queries[0]));
    }

    [Fact]
    public void MovingContactSamplesExplicitSubstepTimesAndReplaysExactly()
    {
        var plan = ClothDefinition3D.Grid("moving-contact", 2, 2, new(.1f, .1f), Vector3.Zero).Compile();
        using var solver = new ClothSolver3D(plan);
        var body = new MovingPlane();
        var contacts = new ClothContacts3D { BodySurface = body, BodyTimeSeconds = .25 };
        var options = new ClothStepOptions3D { DeltaSeconds = .04f, Substeps = 4, Gravity = Vector3.Zero };
        var before = solver.Capture();
        solver.Step(options, contacts);
        Assert.Equal(5, body.Times.Count);
        for (int index = 0; index < 5; index++)
        {
            Assert.InRange(Math.Abs(body.Times[index] - (.25 + index * .01)), 0, 1e-8);
        }
        var expected = solver.Capture();
        Assert.All(expected.Positions, position => Assert.True(position.Y > .02f));
        solver.Restore(before);
        solver.Step(options, contacts);
        Assert.Equal(expected.Positions.ToArray(), solver.Capture().Positions.ToArray());
        Assert.Equal(expected.Velocities.ToArray(), solver.Capture().Velocities.ToArray());
        Assert.Throws<ArgumentException>(() => solver.Step(options, contacts with { BodyTimeSeconds = double.NaN }));
    }

    private sealed class MovingPlane : IClothKinematicBodySurface3D
    {
        public List<double> Times { get; } = [];
        private float height;

        public void Sample(double seconds)
        {
            Times.Add(seconds);
            height = (float)seconds * .1f;
        }

        public ClothBodySample3D Query(Vector3 position)
        {
            return new(new(position.X, height, position.Z), Vector3.UnitY, position.Y - height);
        }
    }
}
