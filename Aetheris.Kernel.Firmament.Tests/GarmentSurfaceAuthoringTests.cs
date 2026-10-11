using System.Numerics;
using Aetheris.Cloth3D;
using Aetheris.Humanoid;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Garment;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class GarmentSurfaceAuthoringTests
{
    [Fact]
    public void BodyContourSkirtHasWeldedQuadTopologyAndRuntimeArtifactReplay()
    {
        var body = HumanoidPoseAuthoringTests.Body();
        var source = new GarmentSkirtSurface("test-skirt", HipHeight: .96f, ChestHeight: 1.28f);
        var garment = GarmentSurfaceAuthoring.Skirt(source, body, new()
        {
            WarpCompliance = .001f, WeftCompliance = .004f, DiagonalCompliance = .002f, BendCompliance = .01f,
        });
        var definition = garment.Cloth.Definition;
        Assert.Equal(288, definition.Positions.Length);
        Assert.Equal(256, definition.Surface!.Quads.Length);
        Assert.Equal(ClothSurfaceRest3D.FittedSurface, definition.Surface.Rest);
        Assert.Equal(32, definition.Pins.Length);
        Assert.Empty(definition.Stitches);
        Assert.Equal(new[] { 31, 0, 32, 63 }, definition.Surface.Quads[31].Vertices);
        Assert.All(garment.Cloth.Constraints, constraint =>
            Assert.InRange(Math.Abs(ClothSurfaceMechanics3D.Evaluate(constraint, definition.Positions).Residual), 0, .0001f));
        Assert.Equal(256, garment.Obj().Split('\n').Count(line => line.StartsWith("f ")));
        using var solver = new ClothSolver3D(garment.Cloth);
        var options = new ClothStepOptions3D { Gravity = new(0, 0, -9.81f), Substeps = 2, Iterations = 8 };
        solver.Step(options, new());
        var retained = ClothGarmentArtifact3D.Parse(garment.Artifact(solver.Capture()));
        using var resumed = retained.CreateSolver();
        solver.Step(options, new());
        resumed.Step(options, new());
        Assert.True(solver.Capture().Positions.SequenceEqual(resumed.Capture().Positions));
        Assert.True(solver.Capture().Velocities.SequenceEqual(resumed.Capture().Velocities));
        Assert.Equal(garment.SourceHash, retained.SourceHash);
        Assert.Equal(garment.Cloth.ContentKey, retained.Cloth.ContentKey);
        var variant = GarmentSurfaceAuthoring.Skirt(source with { HemFlare = .15f }, body, definition.Material);
        Assert.NotEqual(garment.SourceHash, variant.SourceHash);
        Assert.NotEqual(garment.Cloth.ContentKey, variant.Cloth.ContentKey);
    }

    [Fact]
    public void ContourExposesTheSameClosedSectionUsedByFigureMeasurements()
    {
        var body = HumanoidPoseAuthoringTests.Body();
        var contour = HumanoidFigureFitting.Contour(body, 960, new Point3D(0, 0, 960));
        Assert.Equal(HumanoidFigureFitting.Section(body, 960, new Point3D(0, 0, 960)), contour.Measurements);
        Assert.Equal(contour.Measurements.Segments, contour.Points.Count);
        double length = 0;
        for (int index = 0; index < contour.Points.Count; index++)
        {
            length += (contour.Points[(index + 1) % contour.Points.Count] - contour.Points[index]).Length;
        }
        Assert.Equal(contour.Measurements.CircumferenceMm, length, 8);
        Assert.All(contour.Points, point => Assert.Equal(960, point.Z));
    }

    [Theory]
    [InlineData(0, .02f)]
    [InlineData(.48f, 0)]
    [InlineData(float.NaN, .02f)]
    public void InvalidDimensionsAreRejectedBeforeFitting(float length, float ease)
    {
        var exception = Assert.Throws<ArgumentException>(() => GarmentSurfaceAuthoring.Skirt(
            new("invalid", Length: length, Ease: ease), HumanoidPoseAuthoringTests.Body(), new()));
        Assert.StartsWith("garment-surface-skirt:", exception.Message);
    }
}
