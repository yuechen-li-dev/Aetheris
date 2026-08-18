using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheris.Continuum.Experiments.Attention;

var repository = FindRepositoryRoot(AppContext.BaseDirectory);
var output = Path.Combine(repository, "artifacts", "local", "evidence", "continuum", "attention-e0");
Directory.CreateDirectory(output);
var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
var compact = new JsonSerializerOptions(options) { WriteIndented = false };
var benchmark = AttentionE0Experiments.Run();

WriteJson("benchmark-table.json", benchmark.Methods.Select(WithoutHistory));
WriteJson("residual-histories.json", benchmark.Methods.Select(row => new { row.Method, row.PointsPerAxis, row.ResidualHistory }));
WriteJson("mode-analysis.json", benchmark.ModeAnalysis);
WriteJson("mathematical-contracts.json", benchmark.MathematicalContracts);
WriteJson("interaction-weights.json", new
{
    schema = "aetheris-continuum-attention-e0-interactions-v1",
    operatorName = "rank-8 truncated Dirichlet Green factorization",
    target = "center-adjacent interior token on 16^3 lattice",
    strongestByAbsoluteWeight = benchmark.StrongestInteractions,
});
WriteJson("operator-configurations.json", new
{
    schema = "aetheris-continuum-attention-e0-operators-v1",
    exactOperator = "matrix-free symmetric seven-point -Laplacian; no experimental approximation in residual",
    token = new[] { "interior lattice position", "scalar iterate", "residual", "forcing", "Dirichlet boundary proximity", "constant stencil coefficients" },
    operators = new object[]
    {
        new { name = "compact symmetric", kernel = "delta + 0.12 six-neighbor adjacency", support = "one lattice edge", spd = "|beta| < 1/6" },
        new { name = "screened symmetric", kernel = "exp(-Manhattan lattice distance / 2), separable", support = "global, O(N) recurrence", spd = "positive definite kernel plus positive Jacobi term" },
        new { name = "truncated Green", kernel = "eight analytic Dirichlet sine eigenfunctions", support = "global rank 8, O(8N)", spd = "positive spectral inverse coefficients" },
        new { name = "hierarchical screened", kernel = "2x2x2 average -> screened macro interaction -> constant scatter", support = "global at N/8", spd = "R=(1/8)P^T and SPD macro kernel" },
        new { name = "two-level control", kernel = "same transfers plus eight weighted-Jacobi coarse steps", support = "coarse stencil", spd = "positive polynomial coarse inverse" },
    },
    rejected = new[]
    {
        new { name = "softmax", reason = "row normalization is generally asymmetric, removes inverse-response scale, and has no CG-compatible SPD guarantee" },
        new { name = "dense inverse distance", reason = "O(N^2) application is excluded from production-shaped scaling; screened separable distance is the scalable control" },
    },
});

WriteCsv("benchmark-table.csv", "method,n,unknowns,nonzeros,iterations,final_residual,relative_residual,relative_solution_error,sparse_matvecs,experimental_calls,setup_ms,total_ms,matvec_ms,preconditioner_ms,interaction_ms,hierarchy_ms,memory_bytes",
    benchmark.Methods.Select(row => string.Join(',', Csv(row.Method), row.PointsPerAxis, row.Unknowns, row.Nonzeros, row.Iterations,
        R(row.FinalResidual), R(row.RelativeResidual), R(row.RelativeSolutionError), row.SparseMatvecs, row.ExperimentalOperatorCalls,
        R(row.SetupMilliseconds), R(row.RuntimeMilliseconds), R(row.SparseMatvecMilliseconds), R(row.PreconditionerMilliseconds),
        R(row.InteractionMilliseconds), R(row.HierarchyMilliseconds), row.EstimatedMemoryBytes)));
WriteCsv("residual-histories.csv", "method,n,iteration,absolute_residual,relative_residual",
    benchmark.Methods.SelectMany(row => row.ResidualHistory.Select(sample => string.Join(',', Csv(row.Method), row.PointsPerAxis,
        sample.Iteration, R(sample.AbsoluteResidual), R(sample.RelativeResidual)))));
WriteCsv("interaction-weights.csv", "rank,i,j,k,x,y,z,weight,absolute_weight",
    benchmark.StrongestInteractions.Select(row => string.Join(',', row.Rank, row.I, row.J, row.K, R(row.X), R(row.Y), R(row.Z), R(row.Weight), R(row.AbsoluteWeight))));

var deterministicProjection = new
{
    benchmark.Schema, benchmark.Hypothesis, benchmark.Domain, benchmark.BoundaryConditions, benchmark.ManufacturedSolution,
    benchmark.RelativeTolerance,
    methods = benchmark.Methods.Select(row => new
    {
        row.Method, row.PointsPerAxis, row.Unknowns, row.Nonzeros, row.Iterations, row.FinalResidual,
        row.RelativeResidual, row.RelativeSolutionError, row.SparseMatvecs, row.ExperimentalOperatorCalls,
        row.EstimatedMemoryBytes, row.ResidualHistory,
    }),
    benchmark.ModeAnalysis, benchmark.MathematicalContracts, benchmark.StrongestInteractions,
};
var deterministicJson = JsonSerializer.Serialize(deterministicProjection, compact) + Environment.NewLine;
File.WriteAllText(Path.Combine(output, "deterministic-results.json"), deterministicJson, new UTF8Encoding(false));
WriteJson("deterministic-hashes.json", new
{
    schema = "aetheris-continuum-attention-e0-determinism-v1",
    algorithm = "SHA-256",
    excludes = new[] { "setup and runtime measurements" },
    deterministicResultsSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(deterministicJson))),
});

Console.WriteLine(JsonSerializer.Serialize(new
{
    output,
    rows = benchmark.Methods.Select(row => new { row.Method, row.PointsPerAxis, row.Iterations, row.RelativeResidual, row.RelativeSolutionError, row.RuntimeMilliseconds }),
}, options));
return;

object WithoutHistory(AttentionE0MethodResult row) => row with { ResidualHistory = [] };
void WriteJson(string name, object value) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, options) + Environment.NewLine, new UTF8Encoding(false));
void WriteCsv(string name, string header, IEnumerable<string> rows) => File.WriteAllLines(Path.Combine(output, name), new[] { header }.Concat(rows), new UTF8Encoding(false));
static string R(double value) => value.ToString("R", CultureInfo.InvariantCulture);
static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
static string FindRepositoryRoot(string start)
{
    for (var current = new DirectoryInfo(start); current is not null; current = current.Parent)
        if (File.Exists(Path.Combine(current.FullName, "Aetheris.slnx"))) return current.FullName;
    throw new DirectoryNotFoundException("Could not locate Aetheris.slnx.");
}
