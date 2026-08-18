using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Surfacing;

var output = args.Length == 1 ? Path.GetFullPath(args[0]) : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "local", "evidence", "geometry", "reasoning-m0"));
Directory.CreateDirectory(output);
var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
options.Converters.Add(new JsonStringEnumConverter());
var plane = new Plane3(Point3D.Origin, Direction3D.Create(new Vector3D(0, 0, 1)));

BoundedParametricPatch3 Graph(string id, SurfaceScalarExpression z, string provenance = "geometry-m0:calibration") =>
    new(id, new(new(-1, 1), new(-1, 1)), new(
        SurfaceExpression.Multiply(SurfaceExpression.Length(20), SurfaceExpression.U),
        SurfaceExpression.Multiply(SurfaceExpression.Length(15), SurfaceExpression.V), z), provenance);

var square = SurfaceExpression.Multiply(SurfaceExpression.Length(2), SurfaceExpression.Power(SurfaceExpression.U, 2));
var safe = Graph("calibration:safe-clearance", SurfaceExpression.Add(SurfaceExpression.Length(1), square));
var tangent = Graph("calibration:tangent-contact", square);
var failing = Graph("calibration:crossing", SurfaceExpression.Multiply(SurfaceExpression.Length(3), SurfaceExpression.U));
var cadSource = new ParametricSurfaceIr("cad:mold-clearance-panel", SurfaceConstructionKind.ParametricSurface,
    new(new(-1, 1), new(-1, 1)), new(
        SurfaceExpression.Multiply(SurfaceExpression.Length(50), SurfaceExpression.U),
        SurfaceExpression.Multiply(SurfaceExpression.Length(30), SurfaceExpression.V),
        SurfaceExpression.Add(SurfaceExpression.Length(8), SurfaceExpression.Multiply(SurfaceExpression.Length(1), SurfaceExpression.Power(SurfaceExpression.U, 2)))),
    "cad:mold-tooling-clearance");
var cadPanel = PanelFactory.FromParametric(cadSource).Panel ?? throw new InvalidOperationException("CAD calibration Panel failed to construct.");

Run("safe", safe);
Run("tangent", tangent);
Run("failing", failing);
Run("cad-panel-clearance", cadPanel.AuthoredPatch);

void Run(string name, BoundedParametricPatch3 patch)
{
    var sampled = Measure(() => SignedSideQuery.Query(patch, plane, SignedSidePolicy.Sampled(1e-9, 17, 17)));
    var certified = Measure(() => SignedSideQuery.Query(patch, plane, SignedSidePolicy.Certified(1e-9, 10, 4096)));
    var repeat = SignedSideQuery.Query(patch, plane, SignedSidePolicy.Certified(1e-9, 10, 4096));
    var sampledJson = JsonSerializer.Serialize(sampled.Result, options);
    var certifiedJson = JsonSerializer.Serialize(certified.Result, options);
    var repeatJson = JsonSerializer.Serialize(repeat, options);
    var report = new
    {
        milestone = "AETHERIS-GEOMETRY-REASONING-M0",
        fixture = name,
        authoredGeometry = new { patch.StableId, patch.Provenance, representation = patch.Representation.ToString(), patch.Domain },
        plane,
        sampled = sampled.Result,
        certified = certified.Result,
        performance = new { sampledMilliseconds = sampled.Elapsed.TotalMilliseconds, certifiedMilliseconds = certified.Elapsed.TotalMilliseconds },
        determinism = new { stable = certifiedJson == repeatJson, certifiedQuerySha256 = Hash(certifiedJson), repeatQuerySha256 = Hash(repeatJson), sampledQuerySha256 = Hash(sampledJson) }
    };
    File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(report, options).Replace("\r\n", "\n"), new UTF8Encoding(false));
}

static (T Result, TimeSpan Elapsed) Measure<T>(Func<T> action)
{
    var watch = Stopwatch.StartNew(); var result = action(); watch.Stop(); return (result, watch.Elapsed);
}

static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
