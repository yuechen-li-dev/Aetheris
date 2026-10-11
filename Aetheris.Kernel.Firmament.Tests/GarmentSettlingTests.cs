using System.Collections.Immutable;
using System.Numerics;
using Aetheris.Cloth3D;
using Aetheris.Kernel.Firmament.Garment;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class GarmentSettlingTests
{
    [Theory]
    [InlineData(false, 45, "FrameBudget")]
    [InlineData(true, 30, "Settled")]
    public void RealDrapeStopsOnlyAfterStableMotionAndAcceptableResiduals(bool earlyStop, int completed, string reason)
    {
        var garment = SupportedSheet(false);
        var settings = new GarmentDrapeSettings { Substeps = 1, Iterations = 2, StopWhenSettled = earlyStop };
        var result = GarmentDraping.SettleOnSurface(garment, new GroundPlane(), "test", "plane", 45, settings: settings);
        Assert.Equal(completed, result.CompletedSettlingTicks);
        Assert.Equal(reason, result.StopReason);
        Assert.Equal(30 + completed, result.Snapshot.Tick);
        Assert.Equal(settings, result.Settings);
        Assert.True(result.ReplayExact);
        Assert.True(GarmentDrapeLimits.Accept(result.Metrics, result.MaximumSeamGap));
        Assert.Equal(6, result.Samples.Count(sample => sample.Phase == "sewing"));
        Assert.Equal(completed / 5, result.Samples.Count(sample => sample.Phase == "settling"));
        Assert.All(result.Samples, sample =>
        {
            Assert.Equal(0, sample.MaximumSpeed);
            Assert.True(sample.Settled);
        });
    }

    [Fact]
    public void StationaryButStretchedClothCannotTriggerAnEarlySuccess()
    {
        var garment = SupportedSheet(true);
        var result = GarmentDraping.SettleOnSurface(garment, new GroundPlane(), "test", "plane", 40,
            settings: new() { Substeps = 1, Iterations = 2, StopWhenSettled = true });
        Assert.Equal(40, result.CompletedSettlingTicks);
        Assert.Equal("FrameBudget", result.StopReason);
        Assert.InRange(result.Metrics.MaximumStretch, .99f, 1.01f);
        Assert.Equal(result.Metrics.MaximumStretch, result.StretchOutliers[0].Stretch);
        Assert.Equal("sheet", result.StretchOutliers[0].Panel);
        Assert.Equal(2, result.StretchOutliers[0].MaterialA.Length);
        Assert.False(GarmentDrapeLimits.Accept(result.Metrics, result.MaximumSeamGap));
        Assert.All(result.Samples, sample =>
        {
            Assert.Equal(0, sample.MaximumSpeed);
            Assert.False(sample.Settled);
        });
    }

    [Theory]
    [InlineData(0, 24, .01f)]
    [InlineData(65, 24, .01f)]
    [InlineData(8, 0, .01f)]
    [InlineData(8, 65, .01f)]
    [InlineData(8, 24, 0f)]
    [InlineData(8, 24, float.NaN)]
    [InlineData(8, 24, .2f)]
    public void InvalidSettingsStopBeforeSimulation(int substeps, int iterations, float speed)
    {
        var settings = new GarmentDrapeSettings { Substeps = substeps, Iterations = iterations, StationarySpeed = speed };
        Assert.Throws<ArgumentException>(() => GarmentDraping.SettleOnSurface(
            SupportedSheet(false), new GroundPlane(), "test", "plane", settings: settings));
    }

    [Fact]
    public void LimitsKeepTheExistingStrainSeamAndContactThresholds()
    {
        var metrics = new ClothMetrics3D(.15f, 0, .005f, 0, true);
        Assert.True(GarmentDrapeLimits.Accept(metrics, .01f));
        Assert.False(GarmentDrapeLimits.Accept(metrics with { MaximumStretch = .1501f }, .01f));
        Assert.False(GarmentDrapeLimits.Accept(metrics with { MaximumSurfacePenetration = .0051f }, .01f));
        Assert.False(GarmentDrapeLimits.Accept(metrics, .0101f));
        Assert.False(GarmentDrapeLimits.Accept(metrics with { Finite = false }, .01f));
        Assert.False(GarmentDrapeLimits.Accept(metrics, float.NaN));
    }

    private static CompiledGarment SupportedSheet(bool stretch)
    {
        const string source = """
            schema Garment
            Rect2 blank { center: [0mm, 0mm]; size: [100mm, 100mm]; }
            Profile cut { from: blank; }
            Garment test {
              Fabric cotton { thickness: 2mm; arealDensity: 0.2; }
              Panel sheet { profile: cut; origin: [0mm, 0mm, 1000mm]; }
            }
            """;
        var garment = GarmentCompiler.Compile(source).Garment!;
        var definition = garment.Cloth.Definition with
        {
            Pins = Enumerable.Range(0, garment.Cloth.Definition.Positions.Length).ToImmutableArray(),
            InitialPositions = garment.Cloth.Definition.InitialPositions.Select(point =>
                stretch ? new Vector3(point.X * 2, point.Y * 2, point.Z * 2) : point).ToImmutableArray(),
        };
        return garment with { Cloth = definition.Compile() };
    }

    private sealed class GroundPlane : IClothBodySurface3D
    {
        public ClothBodySample3D Query(Vector3 point) => new(new(point.X, point.Y, 0), Vector3.UnitZ, point.Z);
    }
}
