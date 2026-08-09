namespace Aetheris.Continuum.Experiments.Attention;

/// <summary>
/// Matrix-free seven-point discretization of -Laplacian on the interior nodes of
/// the unit cube with homogeneous Dirichlet boundary conditions.
/// </summary>
public sealed class PoissonSystem
{
    public PoissonSystem(int pointsPerAxis)
    {
        if (pointsPerAxis < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(pointsPerAxis));
        }

        PointsPerAxis = pointsPerAxis;
        UnknownCount = checked(pointsPerAxis * pointsPerAxis * pointsPerAxis);
        Spacing = 1d / (pointsPerAxis + 1d);
        InverseSpacingSquared = 1d / (Spacing * Spacing);
        Diagonal = 6d * InverseSpacingSquared;
        NonzeroCount = checked(UnknownCount + (6 * (pointsPerAxis - 1) * pointsPerAxis * pointsPerAxis));
    }

    public int PointsPerAxis { get; }
    public int UnknownCount { get; }
    public int NonzeroCount { get; }
    public double Spacing { get; }
    public double InverseSpacingSquared { get; }
    public double Diagonal { get; }

    public static PoissonProblem CreateManufactured(int pointsPerAxis)
    {
        var system = new PoissonSystem(pointsPerAxis);
        var forcing = new double[system.UnknownCount];
        var exact = new double[system.UnknownCount];
        var n = system.PointsPerAxis;
        var h = system.Spacing;

        for (var k = 0; k < n; k++)
        for (var j = 0; j < n; j++)
        for (var i = 0; i < n; i++)
        {
            var x = (i + 1d) * h;
            var y = (j + 1d) * h;
            var z = (k + 1d) * h;
            var px = x * (1d - x);
            var py = y * (1d - y);
            var pz = z * (1d - z);
            var index = system.Flatten(i, j, k);
            exact[index] = px * py * pz;
            forcing[index] = 2d * ((py * pz) + (px * pz) + (px * py));
        }

        return new PoissonProblem(system, forcing, exact,
            "u=x(1-x)y(1-y)z(1-z); f=2[py*pz+px*pz+px*py]");
    }

    public void Apply(ReadOnlySpan<double> source, Span<double> destination)
    {
        if (source.Length != UnknownCount || destination.Length != UnknownCount)
        {
            throw new ArgumentException("Vector length must match the Poisson system.");
        }

        var n = PointsPerAxis;
        var scale = InverseSpacingSquared;
        for (var k = 0; k < n; k++)
        for (var j = 0; j < n; j++)
        for (var i = 0; i < n; i++)
        {
            var index = Flatten(i, j, k);
            var value = 6d * source[index];
            if (i > 0) value -= source[index - 1];
            if (i + 1 < n) value -= source[index + 1];
            if (j > 0) value -= source[index - n];
            if (j + 1 < n) value -= source[index + n];
            if (k > 0) value -= source[index - (n * n)];
            if (k + 1 < n) value -= source[index + (n * n)];
            destination[index] = value * scale;
        }
    }

    public IReadOnlyList<PoissonEntry> Row(int i, int j, int k)
    {
        var row = Flatten(i, j, k);
        var entries = new List<PoissonEntry>(7) { new(row, row, Diagonal) };
        Add(i - 1, j, k); Add(i + 1, j, k);
        Add(i, j - 1, k); Add(i, j + 1, k);
        Add(i, j, k - 1); Add(i, j, k + 1);
        return entries;

        void Add(int ni, int nj, int nk)
        {
            if ((uint)ni < (uint)PointsPerAxis && (uint)nj < (uint)PointsPerAxis && (uint)nk < (uint)PointsPerAxis)
            {
                entries.Add(new PoissonEntry(row, Flatten(ni, nj, nk), -InverseSpacingSquared));
            }
        }
    }

    public int Flatten(int i, int j, int k)
    {
        if ((uint)i >= (uint)PointsPerAxis || (uint)j >= (uint)PointsPerAxis || (uint)k >= (uint)PointsPerAxis)
        {
            throw new ArgumentOutOfRangeException(nameof(i));
        }

        return ((k * PointsPerAxis) + j) * PointsPerAxis + i;
    }
}

public sealed record PoissonProblem(PoissonSystem System, double[] Forcing, double[] ExactSolution, string ManufacturedSolution);
public readonly record struct PoissonEntry(int Row, int Column, double Value);
