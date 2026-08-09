using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Lattice;

public readonly record struct CellIndex(int I, int J, int K)
{
    public int Flatten(LatticeSpec spec) => ((K * spec.CountY) + J) * spec.CountX + I;
}

public sealed record LatticeSpec
{
    public LatticeSpec(BoundingBox3D bounds, int countX, int countY, int countZ)
    {
        if (countX < 1 || countY < 1 || countZ < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(countX), "All lattice dimensions must be positive.");
        }

        Bounds = bounds;
        CountX = countX;
        CountY = countY;
        CountZ = countZ;
    }

    public BoundingBox3D Bounds { get; }
    public int CountX { get; }
    public int CountY { get; }
    public int CountZ { get; }
    public int TotalCellCount => checked(CountX * CountY * CountZ);
    public Vector3D CellSize => new(
        (Bounds.Max.X - Bounds.Min.X) / CountX,
        (Bounds.Max.Y - Bounds.Min.Y) / CountY,
        (Bounds.Max.Z - Bounds.Min.Z) / CountZ);

    public BoundingBox3D CellBounds(CellIndex index)
    {
        Validate(index);
        var size = CellSize;
        var min = new Point3D(
            Bounds.Min.X + (index.I * size.X),
            Bounds.Min.Y + (index.J * size.Y),
            Bounds.Min.Z + (index.K * size.Z));
        return new BoundingBox3D(min, min + size);
    }

    public Point3D CellCenter(CellIndex index)
    {
        var bounds = CellBounds(index);
        return new Point3D(
            (bounds.Min.X + bounds.Max.X) * 0.5d,
            (bounds.Min.Y + bounds.Max.Y) * 0.5d,
            (bounds.Min.Z + bounds.Max.Z) * 0.5d);
    }

    public IEnumerable<CellIndex> Indices()
    {
        for (var k = 0; k < CountZ; k++)
        for (var j = 0; j < CountY; j++)
        for (var i = 0; i < CountX; i++)
        {
            yield return new CellIndex(i, j, k);
        }
    }

    private void Validate(CellIndex index)
    {
        if (index.I < 0 || index.I >= CountX || index.J < 0 || index.J >= CountY || index.K < 0 || index.K >= CountZ)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
