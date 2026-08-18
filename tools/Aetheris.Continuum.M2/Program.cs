using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Continuum.Experiments;

var options = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Converters = { new JsonStringEnumConverter() },
};
var compact = new JsonSerializerOptions(options) { WriteIndented = false };
_ = ContinuumM2Experiments.Run();
var first = ContinuumM2Experiments.Run();
var second = ContinuumM2Experiments.Run();
var repository = FindRepositoryRoot(AppContext.BaseDirectory);
var output = Path.Combine(repository, "artifacts", "local", "evidence", "continuum", "m2");
Directory.CreateDirectory(output);

Write("benchmark-summary.json", first);
Write("primary-curved-fixture-diagnostics.json", new
{
    schema = "aetheris-continuum-m2-cell-diagnostics-v1",
    fixture = "exact BRep sphere",
    orientation = first.OrientationMatrix[0].Orientation,
    cells = first.OrientationMatrix[0].Diagnostics,
});
Write("orientation-matrix.json", first.OrientationMatrix.Select(row => new
{
    row.Orientation, row.RotationXDegrees, row.RotationYDegrees, row.RotationZDegrees,
    row.Result.CutCells, row.Result.MapResolutionDistribution, row.Result.RelativeVolumeError,
    row.Result.RelativeBoundaryAreaError, row.Result.MaximumPositionError, row.Result.RmsPositionError,
    row.Result.MaximumNormalErrorDegrees, row.Result.Timings.RuntimeExcludingOracleMilliseconds,
}));
Write("curvature-matrix.json", first.CurvatureMatrix);
Write("runtime-vs-oracle-cost.json", first.GeometricRefinementLadder.Where(row => row.EstimatedBoundaryArea.HasValue).Select(row => new
{
    row.Strategy, row.RuntimeCertificateQueries, row.OracleValidationQueries, row.CertificateOracleAgreements,
    row.FalseAccepts, row.FalseRefines, row.Timings.RuntimeExcludingOracleMilliseconds,
    row.Timings.OracleValidationMilliseconds, row.Timings.ExperimentalIncludingOracleMilliseconds,
}));
Write("m1-cost-audit.json", new
{
    schema = "aetheris-continuum-m2-m1-cost-audit-v1",
    selectiveRawRequests = 45120,
    independentValidationRequests = 32192,
    validationSharePercent = 100d * 32192d / 45120d,
    operations = new[]
    {
        new { operation = "cell classification", runtime = true, experimentalOnly = false, reusable = true, replacement = "conservative CIR bounds" },
        new { operation = "map node construction", runtime = true, experimentalOnly = false, reusable = true, replacement = "none; exact support samples define derived map" },
        new { operation = "dense independent map validation", runtime = false, experimentalOnly = true, reusable = false, replacement = "support Hessian/normal-variation engineering bound" },
        new { operation = "nested geometry sampling", runtime = true, experimentalOnly = false, reusable = true, replacement = "selective conservative occupancy disagreement" },
        new { operation = "aggregation", runtime = true, experimentalOnly = false, reusable = false, replacement = "none" },
    },
});

var projection1 = Projection(first);
var projection2 = Projection(second);
var json1 = JsonSerializer.Serialize(projection1, compact) + Environment.NewLine;
var json2 = JsonSerializer.Serialize(projection2, compact) + Environment.NewLine;
File.WriteAllText(Path.Combine(output, "deterministic-geometry.json"), json1, new UTF8Encoding(false));
Write("deterministic-hashes.json", new
{
    schema = "aetheris-continuum-m2-determinism-v1",
    algorithm = "SHA-256",
    excludes = new[] { "runtime timings" },
    repeatedRunCount = 2,
    repeatedGeometryWasIdentical = json1 == json2,
    deterministicGeometrySha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json1))),
});

Console.WriteLine(JsonSerializer.Serialize(new
{
    output,
    first.FixedLatticeBeatsFineGridVolumeError,
    bestFixed = first.GeometricRefinementLadder.MinBy(row => row.RelativeVolumeError),
    fine = first.FineGridReference,
}, options));
return;

object Projection(ContinuumM2Benchmark value) => new
{
    value.Schema,
    ladder = value.GeometricRefinementLadder.Select(WithoutTime),
    orientations = value.OrientationMatrix.Select(row => new { row.Orientation, row.RotationXDegrees, row.RotationYDegrees, row.RotationZDegrees, result = WithoutTime(row.Result), row.Diagnostics }),
    curvature = value.CurvatureMatrix.Select(row => new { row.Radius, result = WithoutTime(row.Result) }),
    fine = WithoutTime(value.FineGridReference), value.FixedLatticeBeatsFineGridVolumeError,
    value.CirBrepConsistencyPassed, value.ExactBrepFace, value.SupportKind,
};

ContinuumM2StrategyResult WithoutTime(ContinuumM2StrategyResult row) => row with
{
    Timings = new ContinuumM2Timings(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
};

void Write(string name, object value) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, options) + Environment.NewLine, new UTF8Encoding(false));

static string FindRepositoryRoot(string start)
{
    for (var current = new DirectoryInfo(start); current is not null; current = current.Parent)
        if (File.Exists(Path.Combine(current.FullName, "Aetheris.slnx"))) return current.FullName;
    throw new DirectoryNotFoundException("Could not locate Aetheris.slnx.");
}
