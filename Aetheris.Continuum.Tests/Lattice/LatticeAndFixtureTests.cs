using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Diagnostics;
using Aetheris.Continuum.Experiments;
using Aetheris.Continuum.Lattice;
using Aetheris.Continuum.Regions.Analytic;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Tests.Lattice;

public sealed class LatticeAndFixtureTests
{
    [Fact]
    public void CirContract_DoesNotRequireSignedDistance()
    {
        IContinuumRegion region = new AxisAlignedBoxRegion(
            new RegionId("box"),
            new BoundingBox3D(new Point3D(-1d, -1d, -1d), new Point3D(1d, 1d, 1d)));

        Assert.True(region.Contains(Point3D.Origin));
        Assert.False(region is IExactEuclideanSignedDistanceCapability);
    }

    [Fact]
    public void SdfBackend_AdaptsPreservedNodeAndTapeToCirCapabilities()
    {
        var sdfRegion = new SdfContinuumRegion(new RegionId("sphere"), new SdfSphereNode(1d));
        IContinuumRegion region = sdfRegion;

        Assert.Equal(ContinuumPointClassification.Inside, region.Classify(Point3D.Origin));
        Assert.IsAssignableFrom<IImplicitFieldCapability>(region);
        Assert.True(sdfRegion.Capabilities.HasFlag(SdfFieldCapabilities.ExactEuclideanSignedDistance));
        Assert.IsAssignableFrom<IGradientCapability>(region);
        Assert.IsAssignableFrom<IBoundsClassificationCapability>(region);
    }

    [Fact]
    public void Lattice_UsesDeterministicRowMajorCellIndexingAndBounds()
    {
        var spec = new LatticeSpec(
            new BoundingBox3D(Point3D.Origin, new Point3D(4d, 2d, 1d)),
            4,
            2,
            1);

        var index = new CellIndex(3, 1, 0);
        Assert.Equal(7, index.Flatten(spec));
        Assert.Equal(new Vector3D(1d, 1d, 1d), spec.CellSize);
        Assert.Equal(new Point3D(3.5d, 1.5d, 0.5d), spec.CellCenter(index));
    }

    [Fact]
    public void AlignedBox_ProducesOnlyInsideAndOutsideCells()
    {
        var result = ContinuumM0Experiments.Box();

        Assert.Equal(64, result.TotalCells);
        Assert.Equal(8, result.Grid.InsideCellCount);
        Assert.Equal(56, result.Grid.OutsideCellCount);
        Assert.Equal(0, result.CutCells);
        Assert.Equal(8d, result.EstimatedVolume, 12);
    }

    [Fact]
    public void AngledPlane_ProducesExplicitCutCellsAndExactSymmetricCoverage()
    {
        var result = ContinuumM0Experiments.AngledPlane();

        Assert.True(result.Grid.InsideCellCount > 0);
        Assert.True(result.Grid.OutsideCellCount > 0);
        Assert.True(result.CutCells > 0);
        Assert.Equal(result.ExactVolume, result.EstimatedVolume, 12);
        Assert.All(result.Grid.CutCells, cut => Assert.Contains(cut.BoundaryReferences, boundary => boundary.SourceId.EndsWith("oblique-plane", StringComparison.Ordinal)));
    }

    [Fact]
    public void CylindricalHole_RefinementReducesVolumeErrorWithoutChangingRegularTopology()
    {
        var results = ContinuumM0Experiments.CylindricalHoleConvergence();

        Assert.Equal([8, 16, 32], results.Select(result => result.Resolution));
        Assert.All(results, result => Assert.True(result.CutCells > 0));
        Assert.All(results, result => Assert.True(result.Grid.InsideCellCount > result.CutCells));
        Assert.True(results[^1].RelativeVolumeError < results[0].RelativeVolumeError);
        Assert.Equal(results.Select(result => result.Grid.Lattice.CellSize.X), results.Select(result => result.Grid.Lattice.CellSize.Y));
    }

    [Fact]
    public void GridDiagnostics_ExposeDimensionsStatesCutIndicesAndCoverageDeterministically()
    {
        var result = ContinuumM0Experiments.CylindricalHole(8);
        var first = ContinuumGridDiagnostics.ToJson(result.Grid);
        var second = ContinuumGridDiagnostics.ToJson(result.Grid);

        Assert.Equal(first, second);
        Assert.Contains("\"cutCells\"", first, StringComparison.Ordinal);
        Assert.Contains("\"geometrySamples\"", first, StringComparison.Ordinal);
        Assert.Contains("\"OccupancyEstimate\"", first, StringComparison.Ordinal);
    }
}
