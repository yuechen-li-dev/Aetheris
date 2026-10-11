using System.Numerics;
using Aetheris.Cloth3D;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Garment;
using Aetheris.Surfacing;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class GarmentSplineAuthoringTests
{
    [Fact]
    public void EditableSplineRoundTripsAndLowersToWeldedStressFreeQuads()
    {
        var body = HumanoidPoseAuthoringTests.Body();
        var fit = GarmentSplineAuthoring.FitTubeTop(new("tube"), body);
        var garment = GarmentSplineAuthoring.Lower(fit.Source, new());
        var restored = GarmentSplineAuthoring.Lower(GarmentSplineAuthoring.ParseSource(
            GarmentSplineAuthoring.SerializeSource(fit.Source)), new());
        Assert.Equal(garment.SourceHash, restored.SourceHash);
        Assert.Equal(garment.Cloth.ContentKey, restored.Cloth.ContentKey);
        Assert.True(fit.FittedRmsMm < fit.SeedRmsMm);
        var definition = garment.Cloth.Definition;
        Assert.Equal(624, definition.Positions.Length);
        Assert.Equal(576, definition.Surface!.Quads.Length);
        Assert.Equal(48, definition.Pins.Length);
        Assert.Equal(ClothSurfaceRest3D.FittedSurface, definition.Surface.Rest);
        Assert.Equal(new[] { 47, 0, 48, 95 }, definition.Surface.Quads[47].Vertices);
        Assert.All(garment.Cloth.Constraints, constraint => Assert.InRange(
            Math.Abs(ClothSurfaceMechanics3D.Evaluate(constraint, definition.Positions).Residual), 0, .0001f));
        using var solver = new ClothSolver3D(garment.Cloth);
        var options = new ClothStepOptions3D { Gravity = new(0, 0, -9.81f), Substeps = 2, Iterations = 4 };
        solver.Step(options, new());
        var retained = ClothGarmentArtifact3D.Parse(garment.Artifact(solver.Capture()));
        using var resumed = retained.CreateSolver();
        solver.Step(options, new());
        resumed.Step(options, new());
        Assert.True(solver.Capture().Positions.SequenceEqual(resumed.Capture().Positions));
        Assert.True(solver.Capture().Velocities.SequenceEqual(resumed.Capture().Velocities));
    }

    [Fact]
    public void LocalControlEditChangesOnlyItsSupportAndRetainsSeams()
    {
        var source = GarmentSplineAuthoring.FitTubeTop(new("editable"), HumanoidPoseAuthoringTests.Body()).Source;
        var controls = source.Patches[0].Surface.ControlPoints.Select(row => row.ToArray()).ToArray();
        controls[3][1] += new Vector3D(0, 8, 0);
        var editedPatch = source.Patches[0] with
        {
            Surface = BSplineControlNetFitting.Copy(source.Patches[0].Surface, controls),
        };
        var edited = source with { Patches = new[] { editedPatch, source.Patches[1] } };
        var before = GarmentSplineAuthoring.Lower(source, new());
        var after = GarmentSplineAuthoring.Lower(edited, new());
        Assert.NotEqual(before.SourceHash, after.SourceHash);
        Assert.NotEqual(before.Cloth.ContentKey, after.Cloth.ContentKey);
        Assert.NotEqual(source.Patches[0].Surface.Evaluate(.5, .4), edited.Patches[0].Surface.Evaluate(.5, .4));
        Assert.Equal(source.Patches[1].Surface.Evaluate(.5, .4), edited.Patches[1].Surface.Evaluate(.5, .4));
        Assert.Equal(before.Cloth.Definition.Positions.Skip(24).Take(24), after.Cloth.Definition.Positions.Skip(24).Take(24));
    }

    [Fact]
    public void BreakingASharedBoundaryFailsBeforeMeshCompilation()
    {
        var source = GarmentSplineAuthoring.FitTubeTop(new("bad-seam"), HumanoidPoseAuthoringTests.Body()).Source;
        var controls = source.Patches[0].Surface.ControlPoints.Select(row => row.ToArray()).ToArray();
        controls[0][0] += new Vector3D(1, 0, 0);
        var edited = source with
        {
            Patches = new[] { source.Patches[0] with
                { Surface = BSplineControlNetFitting.Copy(source.Patches[0].Surface, controls) }, source.Patches[1] },
        };
        Assert.StartsWith("garment-spline-seam:", Assert.Throws<ArgumentException>(() =>
            GarmentSplineAuthoring.Lower(edited, new())).Message);
    }

    [Theory]
    [InlineData(47, .08f)]
    [InlineData(48, float.NaN)]
    [InlineData(48, .3f)]
    public void InvalidGuideDefinitionsHaveNamedDiagnostics(int columns, float underarmDrop)
    {
        Assert.StartsWith("garment-spline-guides:", Assert.Throws<ArgumentException>(() =>
            GarmentSplineAuthoring.FitTubeTop(new("invalid", Columns: columns, UnderarmDrop: underarmDrop),
                HumanoidPoseAuthoringTests.Body())).Message);
    }
}
