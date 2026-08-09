using Aetheris.Continuum.Lattice;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Boundaries;

/// <summary>A derived bilinear local graph. The source boundary remains geometry authority.</summary>
public sealed class SampledBoundaryOffsetMap : IBoundaryOffsetMap
{
    private readonly BoundaryOffsetSample[,] _grid;

    public SampledBoundaryOffsetMap(
        CellIndex cellIndex,
        BoundaryReference sourceBoundary,
        BoundaryLocalFrame localFrame,
        BoundaryMapDomain domain,
        BoundaryOffsetSample[,] grid,
        BoundaryApproximationMetadata approximation)
    {
        CellIndex = cellIndex;
        SourceBoundary = sourceBoundary;
        LocalFrame = localFrame;
        Domain = domain;
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        Approximation = approximation;
        Samples = Flatten(grid);
        Validate();
    }

    public CellIndex CellIndex { get; }
    public BoundaryReference SourceBoundary { get; }
    public BoundaryLocalFrame LocalFrame { get; }
    public IReadOnlyList<BoundaryOffsetSample> Samples { get; }
    public BoundaryMapDomain Domain { get; }
    public BoundaryApproximationMetadata Approximation { get; }

    public BoundaryMapEvaluation Evaluate(double u, double v)
    {
        if (u < Domain.MinimumU - 1e-12d || u > Domain.MaximumU + 1e-12d
            || v < Domain.MinimumV - 1e-12d || v > Domain.MaximumV + 1e-12d)
        {
            throw new ArgumentOutOfRangeException(nameof(u), "Boundary-map coordinates are outside the local domain.");
        }

        var (i, tu) = Coordinate(u, Domain.MinimumU, Domain.MaximumU, _grid.GetLength(0));
        var (j, tv) = Coordinate(v, Domain.MinimumV, Domain.MaximumV, _grid.GetLength(1));
        var a = _grid[i, j];
        var b = _grid[i + 1, j];
        var c = _grid[i, j + 1];
        var d = _grid[i + 1, j + 1];
        var offset = Bilinear(a.Offset, b.Offset, c.Offset, d.Offset, tu, tv);
        var normal = InterpolateNormal(a.Normal, b.Normal, c.Normal, d.Normal, tu, tv);
        var position = LocalFrame.Origin
            + (LocalFrame.TangentU * u)
            + (LocalFrame.TangentV * v)
            + (LocalFrame.Normal * offset);
        return new BoundaryMapEvaluation(position, normal, offset);
    }

    internal SampledBoundaryOffsetMap WithApproximation(BoundaryApproximationMetadata approximation) =>
        new(CellIndex, SourceBoundary, LocalFrame, Domain, _grid, approximation);

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(SourceBoundary.SourceRepresentation) || string.IsNullOrWhiteSpace(SourceBoundary.SourceId))
        {
            throw new InvalidOperationException("BoundaryOffsetMap requires a valid BoundaryReference.");
        }

        if (_grid.GetLength(0) < 2 || _grid.GetLength(1) < 2
            || _grid.GetLength(0) != Approximation.ResolutionU || _grid.GetLength(1) != Approximation.ResolutionV)
        {
            throw new InvalidOperationException("BoundaryOffsetMap sample dimensions are inconsistent.");
        }

        if (!Finite(Domain.MinimumU) || !Finite(Domain.MaximumU) || !Finite(Domain.MinimumV) || !Finite(Domain.MaximumV)
            || Domain.MinimumU >= Domain.MaximumU || Domain.MinimumV >= Domain.MaximumV)
        {
            throw new InvalidOperationException("BoundaryOffsetMap has an invalid local domain.");
        }

        var f = LocalFrame;
        if (!Finite(f.Origin) || !Unit(f.Normal) || !Unit(f.TangentU) || !Unit(f.TangentV)
            || double.Abs(f.Normal.Dot(f.TangentU)) > 1e-9d
            || double.Abs(f.Normal.Dot(f.TangentV)) > 1e-9d
            || double.Abs(f.TangentU.Dot(f.TangentV)) > 1e-9d)
        {
            throw new InvalidOperationException("BoundaryOffsetMap local frame is not finite and orthonormal.");
        }

        foreach (var sample in Samples)
        {
            if (!Finite(sample.U) || !Finite(sample.V) || !Finite(sample.Offset) || sample.Normal is not { } n || !Unit(n))
            {
                throw new InvalidOperationException("BoundaryOffsetMap contains an invalid offset or normal sample.");
            }
        }

        if (!Finite(Approximation.MaximumPositionError) || !Finite(Approximation.MaximumNormalAngleDegrees)
            || Approximation.MaximumPositionError < 0d || Approximation.MaximumNormalAngleDegrees < 0d)
        {
            throw new InvalidOperationException("BoundaryOffsetMap has invalid approximation metadata.");
        }
    }

    private static IReadOnlyList<BoundaryOffsetSample> Flatten(BoundaryOffsetSample[,] grid)
    {
        var values = new List<BoundaryOffsetSample>(grid.Length);
        for (var j = 0; j < grid.GetLength(1); j++)
        for (var i = 0; i < grid.GetLength(0); i++)
        {
            values.Add(grid[i, j]);
        }

        return values;
    }

    private static (int Index, double T) Coordinate(double value, double minimum, double maximum, int count)
    {
        var scaled = double.Clamp((value - minimum) / (maximum - minimum), 0d, 1d) * (count - 1);
        var index = int.Min((int)double.Floor(scaled), count - 2);
        return (index, scaled - index);
    }

    private static Vector3D InterpolateNormal(Vector3D? a, Vector3D? b, Vector3D? c, Vector3D? d, double u, double v)
    {
        var x = Bilinear(a!.Value.X, b!.Value.X, c!.Value.X, d!.Value.X, u, v);
        var y = Bilinear(a.Value.Y, b.Value.Y, c.Value.Y, d.Value.Y, u, v);
        var z = Bilinear(a.Value.Z, b.Value.Z, c.Value.Z, d.Value.Z, u, v);
        var candidate = new Vector3D(x, y, z);
        if (!candidate.TryNormalize(out var normalized))
        {
            throw new InvalidOperationException("Interpolated boundary normal is degenerate.");
        }

        return normalized;
    }

    private static double Bilinear(double a, double b, double c, double d, double u, double v) =>
        ((1d - u) * (1d - v) * a) + (u * (1d - v) * b) + ((1d - u) * v * c) + (u * v * d);

    private static bool Unit(Vector3D value) => Finite(value) && double.Abs(value.Length - 1d) <= 1e-9d;
    private static bool Finite(Point3D value) => Finite(value.X) && Finite(value.Y) && Finite(value.Z);
    private static bool Finite(Vector3D value) => Finite(value.X) && Finite(value.Y) && Finite(value.Z);
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
