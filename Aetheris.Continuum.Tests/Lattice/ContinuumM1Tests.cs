using System.Text.Json;
using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Experiments;
using Aetheris.Continuum.Lattice;
using Aetheris.Continuum.Regions.Analytic;
using Aetheris.Continuum.Sampling;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Tests.Lattice;

public sealed class ContinuumM1Tests
{
    [Fact]
    public void CylinderOffsetMap_UsesExactDeterministicFrameAndIndependentFidelityMetrics()
    {
        var region = Cylinder();
        var lattice = CylinderLattice();
        var cell = lattice.Indices().First(index => ContinuumGridClassifier.ClassifyCell(region, lattice.CellBounds(index)) == CellClassification.Cut);
        var support = ((IBoundaryOffsetMapCapability)region).BoundarySupports(lattice.CellBounds(cell)).Single();
        var policy = new BoundaryOffsetMapErrorPolicy(MaximumPositionError: 0.02d, MaximumNormalAngleDegrees: 1d);
        var map2 = support.CreateOffsetMap(cell, lattice.CellBounds(cell), 2, policy);
        var map4 = support.CreateOffsetMap(cell, lattice.CellBounds(cell), 4, policy);
        var again = support.CreateOffsetMap(cell, lattice.CellBounds(cell), 4, policy);

        Assert.Equal(region.CylindricalWallReference, map4.SourceBoundary);
        Assert.Equal(map4.LocalFrame, again.LocalFrame);
        Assert.Equal(map4.Samples, again.Samples);
        Assert.Equal(81, map4.Approximation.IndependentValidationPointCount);
        Assert.True(map4.Approximation.MaximumPositionError < map2.Approximation.MaximumPositionError);
        Assert.True(map4.Approximation.MaximumNormalAngleDegrees < map2.Approximation.MaximumNormalAngleDegrees);
        Assert.Equal(1d, map4.LocalFrame.Normal.Length, 12);
        Assert.Equal(0d, map4.LocalFrame.Normal.Dot(map4.LocalFrame.TangentU), 12);
        Assert.All(map4.Samples, sample => Assert.True(sample.Normal!.Value.Length > 0.999999999d));
    }

    [Fact]
    public void HierarchicalPattern_ReusesAllEightBaseQueriesInRegularFourPattern()
    {
        var region = Cylinder();
        var bounds = CylinderLattice().CellBounds(new CellIndex(4, 6, 1));
        var cache = new GeometryQueryCache();
        var coarse = HierarchicalGeometrySampler.SampleNestedBase2(region, bounds, cache);
        var fine = HierarchicalGeometrySampler.RefineToRegular4(region, bounds, cache);

        Assert.Equal(8, coarse.RawRequestedSamples);
        Assert.Equal(8, coarse.UniqueExactQueries);
        Assert.Equal(64, fine.RawRequestedSamples);
        Assert.Equal(56, fine.UniqueExactQueries);
        Assert.Equal(8, fine.ReusedSamples);
        Assert.All(coarse.Plan.Samples, sample => Assert.Contains(sample.Position, fine.Plan.Samples.Select(candidate => candidate.Position)));
    }

    [Fact]
    public void FixedMediumCylinder_SmarterBoundaryBeatsM0FineWithoutChangingCells()
    {
        var fixture = ContinuumM1Experiments.RunCylinder();
        var best = fixture.Strategies.Single(row => row.Strategy == "offset-map-selective-msaa");
        var map4 = fixture.Strategies.Single(row => row.Strategy == "offset-map-4x4");
        var fine = ContinuumM0Experiments.CylindricalHole(32);

        Assert.Equal((16, 16, 4, 1024), (fixture.Lattice.CountX, fixture.Lattice.CountY, fixture.Lattice.CountZ, fixture.Lattice.TotalCellCount));
        Assert.Equal(112, fixture.CutCells);
        Assert.InRange(best.SelectivelyRefinedCells, 1, fixture.CutCells - 1);
        Assert.True(best.RelativeVolumeError < fine.RelativeVolumeError);
        Assert.True(best.RelativeVolumeError < map4.RelativeVolumeError);
        Assert.True(best.RelativeBoundaryAreaError < map4.RelativeBoundaryAreaError);
        Assert.True(best.ReusedSamples > 0);
        Assert.Equal(best.RawRequestedSamples, best.UniqueExactGeometryQueries + best.ReusedSamples);
        Assert.Equal(8192 / 8, best.TotalCells);
        Assert.All(fixture.Strategies, row => Assert.Equal(1024, row.TotalCells));
    }

    [Fact]
    public void PlaneControl_OffsetMapAddsNoPositionNormalVolumeOrAreaError()
    {
        var fixture = ContinuumM1Experiments.RunPlane();
        var map = fixture.Strategies.Single(row => row.Strategy == "offset-map-2x2");

        Assert.Equal(0d, map.RelativeVolumeError, 12);
        Assert.Equal(0d, map.RelativeBoundaryAreaError, 12);
        Assert.Equal(0d, map.MaximumPositionError!.Value, 12);
        Assert.Equal(0d, map.MaximumNormalAngleDegrees!.Value, 12);
        Assert.Equal(0, map.SelectivelyRefinedCells);
    }

    [Fact]
    public void AnalyticReferences_AreIndependentAndRecoverGlobalCylinderMeasures()
    {
        var region = Cylinder();
        var lattice = CylinderLattice();
        var cut = lattice.Indices().Where(index => ContinuumGridClassifier.ClassifyCell(region, lattice.CellBounds(index)) == CellClassification.Cut).ToArray();
        var arcArea = cut.Sum(index => AnalyticContinuumReferences.CylinderArcLength(lattice.CellBounds(index), region.HoleCenter, region.HoleRadius)
            * lattice.CellSize.Z);

        Assert.Equal(region.ExactVolume, AnalyticContinuumReferences.BlockWithCylindricalHoleVolume(region.Bounds, region.HoleRadius), 12);
        Assert.Equal(region.ExactCylindricalBoundaryArea, arcArea, 10);
        var projected = AnalyticContinuumReferences.ProjectCylinder(new Point3D(2d, 0d, 0.2d), region.HoleCenter, region.HoleRadius);
        Assert.Equal(new Point3D(1d, 0d, 0.2d), projected);
        Assert.Equal(new Vector3D(1d, 0d, 0d), AnalyticContinuumReferences.CylinderMaterialSideNormal(projected, region.HoleCenter));
    }

    [Fact]
    public void GeometryMetrics_AreByteDeterministicWhenRuntimeIsExcluded()
    {
        var first = DeterministicProjection(ContinuumM1Experiments.Run());
        var second = DeterministicProjection(ContinuumM1Experiments.Run());
        Assert.Equal(first, second);
    }

    private static string DeterministicProjection(ContinuumM1Benchmark benchmark) => JsonSerializer.Serialize(new
    {
        cylinder = benchmark.CylindricalHole.Strategies.Select(WithoutTime),
        cylinderCells = benchmark.CylindricalHole.PerCellDiagnostics,
        plane = benchmark.ObliquePlane.Strategies.Select(WithoutTime),
        planeCells = benchmark.ObliquePlane.PerCellDiagnostics,
    });

    private static ContinuumM1StrategyResult WithoutTime(ContinuumM1StrategyResult row) =>
        row with { Timings = new ContinuumM1Timings(0d, 0d, 0d, 0d, 0d) };

    private static BlockWithCylindricalHoleRegion Cylinder()
    {
        var bounds = new BoundingBox3D(new Point3D(-2d, -2d, -0.5d), new Point3D(2d, 2d, 0.5d));
        return new BlockWithCylindricalHoleRegion(new RegionId("test-cylinder"), bounds, 1d);
    }

    private static LatticeSpec CylinderLattice() => new(
        new BoundingBox3D(new Point3D(-2d, -2d, -0.5d), new Point3D(2d, 2d, 0.5d)), 16, 16, 4);
}
