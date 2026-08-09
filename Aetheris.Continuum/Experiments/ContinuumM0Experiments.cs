using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Lattice;
using Aetheris.Continuum.Regions.Analytic;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Experiments;

public sealed record ContinuumExperimentResult(
    string Name,
    int Resolution,
    ContinuumGridResult Grid,
    double ExactVolume,
    double RelativeVolumeError,
    double? ExactBoundaryArea = null,
    double? EstimatedBoundaryArea = null)
{
    public int TotalCells => Grid.Lattice.TotalCellCount;
    public int CutCells => Grid.CutCellCount;
    public int GeometrySamples => Grid.GeometrySampleCount;
    public double EstimatedVolume => Grid.EstimatedOccupiedVolume;
}

public static class ContinuumM0Experiments
{
    public static ContinuumExperimentResult Box(int cellsPerAxis = 4)
    {
        var region = new AxisAlignedBoxRegion(
            new RegionId("m0-box"),
            new BoundingBox3D(new Point3D(-1d, -1d, -1d), new Point3D(1d, 1d, 1d)));
        var lattice = new LatticeSpec(
            new BoundingBox3D(new Point3D(-2d, -2d, -2d), new Point3D(2d, 2d, 2d)),
            cellsPerAxis,
            cellsPerAxis,
            cellsPerAxis);
        return Result("axis-aligned-box", cellsPerAxis, ContinuumGridClassifier.Classify(region, lattice, 2), region.ExactVolume, region.ExactBoundaryArea);
    }

    public static ContinuumExperimentResult AngledPlane(int cellsPerAxis = 8, int samplesPerAxis = 2)
    {
        var bounds = new BoundingBox3D(new Point3D(-1d, -1d, -1d), new Point3D(1d, 1d, 1d));
        var region = new ObliqueHalfSpaceRegion(new RegionId("m0-angled-plane"), bounds, new Vector3D(1d, 1d, 0d), 0d);
        var lattice = new LatticeSpec(bounds, cellsPerAxis, cellsPerAxis, cellsPerAxis);
        var exactVolume = 4d;
        return Result("angled-plane", cellsPerAxis, ContinuumGridClassifier.Classify(region, lattice, samplesPerAxis), exactVolume);
    }

    public static ContinuumExperimentResult CylindricalHole(int planarResolution, int samplesPerAxis = 4)
    {
        if (planarResolution < 4 || planarResolution % 4 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(planarResolution), "Resolution must be >= 4 and divisible by four so cells remain cubic.");
        }

        var bounds = new BoundingBox3D(new Point3D(-2d, -2d, -0.5d), new Point3D(2d, 2d, 0.5d));
        var region = new BlockWithCylindricalHoleRegion(new RegionId("m0-cylindrical-hole"), bounds, 1d);
        var lattice = new LatticeSpec(bounds, planarResolution, planarResolution, planarResolution / 4);
        return Result("cylindrical-hole", planarResolution, ContinuumGridClassifier.Classify(region, lattice, samplesPerAxis), region.ExactVolume, region.ExactBoundaryArea);
    }

    public static IReadOnlyList<ContinuumExperimentResult> CylindricalHoleConvergence() =>
        [CylindricalHole(8), CylindricalHole(16), CylindricalHole(32)];

    private static ContinuumExperimentResult Result(
        string name,
        int resolution,
        ContinuumGridResult grid,
        double exactVolume,
        double? exactBoundaryArea = null)
    {
        var relativeError = double.Abs(grid.EstimatedOccupiedVolume - exactVolume) / exactVolume;
        return new ContinuumExperimentResult(name, resolution, grid, exactVolume, relativeError, exactBoundaryArea);
    }
}
