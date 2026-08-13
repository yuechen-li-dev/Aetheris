using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheris.Geometry;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;
using Aetheris.Piping;
using Aetheris.Surfacing;

var output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "geometry", "artifacts", "reasoning-m1"));
Directory.CreateDirectory(output);
var x = Direction3D.Create(new Vector3D(1, 0, 0));
var z = Direction3D.Create(new Vector3D(0, 0, 1));
var line = BoundedParametricCurve3.LineSegment("evidence:line", Point3D.Origin, new(10, 0, 0), "evidence", "fixture");
var circle = BoundedParametricCurve3.FromCurveGeometry("evidence:circle", CurveGeometry.FromCircle(new(Point3D.Origin, z, 5, x)), 0, 2 * double.Pi, "evidence", "fixture");
var t = CurveExpression.T; var mm = CurveExpression.Length(1);
var helix = new BoundedParametricCurve3("evidence:helix", new(0, 2 * double.Pi),
    new(CurveExpression.Multiply(mm, CurveExpression.Cos(t)), CurveExpression.Multiply(mm, CurveExpression.Sin(t)), CurveExpression.Multiply(mm, t)), "evidence", "fixture");
var panel = PanelFactory.FromRuled(RuledCanopyTemplate.Create("evidence-panel", 20, 10, 2)).Panel!;
var route = PipeRouteLowering.Lower(StandardPipeElbowTemplate.Create("evidence-route", 4, 10, 8)).Ir!;

var snapshot = new
{
    curves = new[] { Snapshot(line, .5), Snapshot(circle, double.Pi / 2), Snapshot(helix, double.Pi) },
    panel = panel.BoundaryEdges.Select(edge => Snapshot(edge.AuthoredCurve, edge.AuthoredCurve.Domain.Minimum)).ToArray(),
    piping = route.CenterlineCurves.Select(curve => Snapshot(curve, curve.Domain.Minimum)).ToArray()
};
var options = new JsonSerializerOptions { WriteIndented = true };
var json = JsonSerializer.Serialize(snapshot, options) + Environment.NewLine;
File.WriteAllText(Path.Combine(output, "deterministic-evaluations.json"), json, new UTF8Encoding(false));
var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
File.WriteAllText(Path.Combine(output, "deterministic-hashes.json"), JsonSerializer.Serialize(new
{
    algorithm = "SHA-256", artifact = "deterministic-evaluations.json", sha256 = hash,
    invariant = "Same runtime inputs preserve identities, domains, provenance, ordered pieces, evaluations, and first jets."
}, options) + Environment.NewLine, new UTF8Encoding(false));

const int iterations = 100_000;
var measurements = new[]
{
    Measure("curve-evaluate", iterations, i => line.Evaluate((i % 1000) / 100d)),
    Measure("curve-first-jet", iterations, i => circle.EvaluateJet1((i % 1000) * 2 * double.Pi / 1000)),
    Measure("expression-first-jet", iterations, i => helix.EvaluateJet1((i % 1000) * 2 * double.Pi / 1000)),
    Measure("panel-edge-adapter", iterations, i => panel.BoundaryEdges[i % 4].AuthoredCurve.EvaluateJet1(panel.BoundaryEdges[i % 4].AuthoredCurve.Domain.Minimum)),
    Measure("piping-route-adapter", iterations, i => route.CenterlineCurves[i % 3].EvaluateJet1(route.CenterlineCurves[i % 3].Domain.Minimum))
};
var performance = "# M1 performance evidence\n\nDebug net10.0, Stopwatch wall time, 100,000 operations per case. These are smoke measurements, not optimization claims.\n\n| Case | elapsed ms | ns/op |\n|---|---:|---:|\n" +
    string.Join("\n", measurements.Select(m => $"| {m.Name} | {m.ElapsedMs:F3} | {m.NanosecondsPerOperation:F1} |")) + "\n";
File.WriteAllText(Path.Combine(output, "performance.md"), performance, new UTF8Encoding(false));
Console.WriteLine(JsonSerializer.Serialize(new { output, hash, measurements }, options));

static object Snapshot(BoundedParametricCurve3 curve, double parameter)
{
    var jet = curve.EvaluateJet1(parameter);
    return new { identity = curve.StableId, curve.Domain, curve.Provenance, curve.Representation, curve.NativeFamily, curve.IsPeriodic, parameter, jet.Point, jet.Derivative, jet.UnitTangent, jet.Singularity };
}

static Measurement Measure(string name, int iterations, Action<int> operation)
{
    for (var i = 0; i < 1000; i++) operation(i);
    var stopwatch = Stopwatch.StartNew();
    for (var i = 0; i < iterations; i++) operation(i);
    stopwatch.Stop();
    return new(name, stopwatch.Elapsed.TotalMilliseconds, stopwatch.Elapsed.TotalNanoseconds / iterations);
}

sealed record Measurement(string Name, double ElapsedMs, double NanosecondsPerOperation);
