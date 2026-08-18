using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Continuum.Experiments;

const int warmupRuns = 5;
const int measuredRuns = 20;
for (var i = 0; i < warmupRuns; i++)
{
    _ = ContinuumM1Experiments.Run();
}

var measured = new List<ContinuumM1Benchmark>(measuredRuns);
for (var i = 0; i < measuredRuns; i++)
{
    measured.Add(ContinuumM1Experiments.Run());
}

var benchmark = ApplyAverageTimings(measured[0], measured);
var options = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Converters = { new JsonStringEnumConverter() },
};
var compactOptions = new JsonSerializerOptions
{
    WriteIndented = false,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Converters = { new JsonStringEnumConverter() },
};
var repository = FindRepositoryRoot(AppContext.BaseDirectory);
var output = Path.Combine(repository, "artifacts", "local", "evidence", "continuum", "m1");
Directory.CreateDirectory(output);

var m0Summary = benchmark.M0CylindricalHoleBaselines.Select(row => new
{
    row.Resolution,
    dimensions = new[] { row.Grid.Lattice.CountX, row.Grid.Lattice.CountY, row.Grid.Lattice.CountZ },
    row.TotalCells,
    row.CutCells,
    row.GeometrySamples,
    row.EstimatedVolume,
    row.ExactVolume,
    row.RelativeVolumeError,
});
Write("benchmark-summary.json", new
{
    benchmark.Schema,
    warmedRuns = warmupRuns,
    measuredRuns,
    cylindricalHole = ResultProjection(benchmark.CylindricalHole),
    obliquePlane = ResultProjection(benchmark.ObliquePlane),
    m0CylindricalHoleBaselines = m0Summary,
});
Write("cylindrical-hole-results.json", ResultProjection(benchmark.CylindricalHole));
Write("oblique-plane-control-results.json", ResultProjection(benchmark.ObliquePlane));
WriteCompact("per-cell-diagnostics.json", new
{
    schema = "aetheris-continuum-m1-per-cell-v1",
    cylindricalHole = benchmark.CylindricalHole.PerCellDiagnostics,
    obliquePlane = benchmark.ObliquePlane.PerCellDiagnostics,
});

var best = benchmark.CylindricalHole.Strategies.MinBy(row => row.RelativeVolumeError)!;
Write("fixed-vs-fine-comparison.json", new
{
    schema = "aetheris-continuum-m1-fixed-vs-fine-v1",
    fixedMediumBest = best,
    m0 = m0Summary,
    fixedMediumUsesFractionOfFineCells = (double)best.TotalCells / benchmark.M0CylindricalHoleBaselines[^1].TotalCells,
    fixedMediumBeatsFineVolumeError = best.RelativeVolumeError < benchmark.M0CylindricalHoleBaselines[^1].RelativeVolumeError,
});

var deterministicProjection = new
{
    benchmark.Schema,
    cylinder = Project(benchmark.CylindricalHole),
    plane = Project(benchmark.ObliquePlane),
    m0 = benchmark.M0CylindricalHoleBaselines.Select(row => new { row.Resolution, row.TotalCells, row.CutCells, row.GeometrySamples, row.EstimatedVolume, row.ExactVolume }),
};
var deterministicJson = JsonSerializer.Serialize(deterministicProjection, compactOptions);
File.WriteAllText(Path.Combine(output, "deterministic-geometry.json"), deterministicJson + Environment.NewLine, new UTF8Encoding(false));
Write("deterministic-hashes.json", new
{
    schema = "aetheris-continuum-m1-determinism-v1",
    algorithm = "SHA-256",
    excludes = new[] { "runtime timings" },
    deterministicGeometrySha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(deterministicJson + Environment.NewLine))),
    repeatedRunCount = measuredRuns,
    repeatedGeometryWasIdentical = measured.All(item => JsonSerializer.Serialize(new { item.Schema, cylinder = Project(item.CylindricalHole), plane = Project(item.ObliquePlane), m0 = item.M0CylindricalHoleBaselines.Select(row => new { row.Resolution, row.TotalCells, row.CutCells, row.GeometrySamples, row.EstimatedVolume, row.ExactVolume }) }, compactOptions) == deterministicJson),
});

Console.WriteLine(JsonSerializer.Serialize(new
{
    output,
    best = new { best.Strategy, best.RelativeVolumeError, best.RelativeBoundaryAreaError, best.TotalCells, best.CutCells, best.RawRequestedSamples, best.UniqueExactGeometryQueries, best.ReuseRatio, best.Timings },
}, options));
return;

object Project(ContinuumM1FixtureResult fixture) => new
{
    fixture.Fixture,
    lattice = new { fixture.Lattice.CountX, fixture.Lattice.CountY, fixture.Lattice.CountZ, fixture.Lattice.TotalCellCount },
    fixture.InsideCells,
    fixture.OutsideCells,
    fixture.CutCells,
    strategies = fixture.Strategies.Select(row => row with { Timings = new ContinuumM1Timings(0d, 0d, 0d, 0d, 0d) }),
    fixture.PerCellDiagnostics,
};

object ResultProjection(ContinuumM1FixtureResult fixture) => new
{
    fixture.Fixture,
    lattice = new
    {
        fixture.Lattice.CountX,
        fixture.Lattice.CountY,
        fixture.Lattice.CountZ,
        fixture.Lattice.TotalCellCount,
        fixture.Lattice.CellSize,
    },
    fixture.InsideCells,
    fixture.OutsideCells,
    fixture.CutCells,
    fixture.Strategies,
};

void Write(string file, object value)
{
    var json = JsonSerializer.Serialize(value, options);
    File.WriteAllText(Path.Combine(output, file), json + Environment.NewLine, new UTF8Encoding(false));
}

void WriteCompact(string file, object value)
{
    var json = JsonSerializer.Serialize(value, compactOptions);
    File.WriteAllText(Path.Combine(output, file), json + Environment.NewLine, new UTF8Encoding(false));
}

static ContinuumM1Benchmark ApplyAverageTimings(ContinuumM1Benchmark source, IReadOnlyList<ContinuumM1Benchmark> runs) => source with
{
    CylindricalHole = AverageFixture(source.CylindricalHole, runs.Select(run => run.CylindricalHole).ToArray()),
    ObliquePlane = AverageFixture(source.ObliquePlane, runs.Select(run => run.ObliquePlane).ToArray()),
};

static ContinuumM1FixtureResult AverageFixture(ContinuumM1FixtureResult source, IReadOnlyList<ContinuumM1FixtureResult> runs) => source with
{
    Strategies = source.Strategies.Select(row => row with
    {
        Timings = Average(runs.Select(run => run.Strategies.Single(candidate => candidate.Strategy == row.Strategy).Timings).ToArray()),
    }).ToArray(),
};

static ContinuumM1Timings Average(IReadOnlyList<ContinuumM1Timings> values) => new(
    values.Average(value => value.ClassificationMilliseconds),
    values.Average(value => value.BoundaryMapConstructionMilliseconds),
    values.Average(value => value.GeometrySamplingMilliseconds),
    values.Average(value => value.AggregationMilliseconds),
    values.Average(value => value.TotalMilliseconds));

static string FindRepositoryRoot(string start)
{
    var directory = new DirectoryInfo(start);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx")))
    {
        directory = directory.Parent;
    }

    return directory?.FullName ?? throw new InvalidOperationException("Could not locate Aetheris.slnx.");
}
