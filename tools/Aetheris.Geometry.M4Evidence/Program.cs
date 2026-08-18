using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Geometry;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Surfacing;

var output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "local", "evidence", "geometry", "reasoning-m4"));
Directory.CreateDirectory(output);
var json = new JsonSerializerOptions { WriteIndented = true }; json.Converters.Add(new JsonStringEnumConverter());
var z0 = new Plane3(Point3D.Origin, Direction3D.Create(new(0, 0, 1)));

var crossingLine = Line("crossing-line", new(0, 0, -1), new(0, 0, 1));
var disjointLine = Line("disjoint-line", new(-1, 0, 2), new(1, 0, 2));
var t = CurveExpression.T;
var tangentParabola = new BoundedParametricCurve3("tangent-parabola", new(-1, 1), new(
    CurveExpression.Multiply(CurveExpression.Length(1), t), CurveExpression.Length(0),
    CurveExpression.Multiply(CurveExpression.Length(1), CurveExpression.Power(t, 2))), "M4 scientific fixture");
Write("curve-plane-fixtures.json", new
{
    disjoint = IntersectionQuery.Between(disjointLine, z0),
    crossing = IntersectionQuery.Between(crossingLine, z0),
    tangent = IntersectionQuery.Between(tangentParabola, z0),
    structuralZero = IntersectionQuery.Between(Line("structural-zero", new(-1, 0, 0), new(1, 0, 0)), z0),
    unknown = IntersectionQuery.Between(BoundedParametricCurve3.Procedural("first-jet-only", new(-1, 1), x => (new(x, 0, x * x), new Vector3D(1, 0, 2 * x)), "M4 fixture"), z0)
});

var patchAbove = Graph("patch-above", SurfaceExpression.Length(2));
var patchCrossing = Graph("crossing-saddle", SurfaceExpression.Multiply(SurfaceExpression.Length(1), SurfaceExpression.U));
var tangentZ = SurfaceExpression.Add(
    SurfaceExpression.Multiply(SurfaceExpression.Length(1), SurfaceExpression.Power(SurfaceExpression.U, 2)),
    SurfaceExpression.Multiply(SurfaceExpression.Length(1), SurfaceExpression.Power(SurfaceExpression.V, 2)));
var patchTangent = Graph("tangent-paraboloid", tangentZ);
Write("patch-plane-fixtures.json", new
{
    disjoint = IntersectionQuery.Between(patchAbove, z0),
    crossing = IntersectionQuery.Between(patchCrossing, z0),
    tangent = IntersectionQuery.Between(patchTangent, z0),
    toleranceSensitivity = new
    {
        loose = IntersectionQuery.Between(Graph("near-plane-loose", SurfaceExpression.Length(5e-7)), z0),
        tight = IntersectionQuery.Between(Graph("near-plane-tight", SurfaceExpression.Length(5e-7)), z0, new() { LinearTolerance = 1e-8 })
    }
});

var horizontal = PlanePatch("horizontal", 0); var vertical = VerticalPatch("vertical");
Write("curve-patch-fixtures.json", new
{
    separated = IntersectionQuery.Between(disjointLine, horizontal),
    transverse = IntersectionQuery.Between(crossingLine, horizontal),
    nearContact = IntersectionQuery.Between(Line("in-plane", new(-.5, 0, 0), new(.5, 0, 0)), horizontal),
    budgetLimited = IntersectionQuery.Between(crossingLine, horizontal, new IntersectionPolicy { SubdivisionBudget = 16 })
});
Write("patch-patch-fixtures.json", new
{
    separated = IntersectionQuery.Between(horizontal, PlanePatch("parallel-gap", 2)),
    transverse = IntersectionQuery.Between(horizontal, vertical),
    tangentCompatible = IntersectionQuery.Between(horizontal, PlanePatch("compatible", 0)),
    structurallyCoincident = IntersectionQuery.Between(horizontal, PlanePatch("horizontal", 0)),
    numericalNearZero = IntersectionQuery.Between(horizontal, PlanePatch("near", 5e-7)),
    budgetLimited = IntersectionQuery.Between(horizontal, vertical, new IntersectionPolicy { SubdivisionBudget = 16 })
});

var panel = PanelFactory.FromParametric(new ParametricSurfaceIr("panel", SurfaceConstructionKind.ParametricSurface,
    new(new(-1, 1), new(-1, 1)), GraphExpression(SurfaceExpression.Length(3)), "cad:M4-panel")).Panel!;
var panelGap = PanelFactory.FromParametric(new ParametricSurfaceIr("panel-gap", SurfaceConstructionKind.ParametricSurface,
    new(new(-1, 1), new(-1, 1)), GraphExpression(SurfaceExpression.Length(5)), "cad:M4-panel-gap")).Panel!;
var panelCrossing = PanelFactory.FromParametric(new ParametricSurfaceIr("panel-crossing", SurfaceConstructionKind.ParametricSurface,
    new(new(-1, 1), new(-1, 1)), GraphExpression(SurfaceExpression.Multiply(SurfaceExpression.Length(1), SurfaceExpression.U)), "cad:M4-panel-crossing")).Panel!;
var panelTangent = PanelFactory.FromParametric(new ParametricSurfaceIr("panel-tangent", SurfaceConstructionKind.ParametricSurface,
    new(new(-1, 1), new(-1, 1)), GraphExpression(tangentZ), "cad:M4-panel-tangent")).Panel!;
Write("panel-cad-dogfood.json", new
{
    panelPlane = IntersectionQuery.Between(panel.AuthoredPatch, z0),
    panelPanelGap = IntersectionQuery.Between(panel.AuthoredPatch, panelGap.AuthoredPatch),
    panelPlaneCrossing = IntersectionQuery.Between(panelCrossing.AuthoredPatch, z0),
    panelPlaneTangent = IntersectionQuery.Between(panelTangent.AuthoredPatch, z0)
});

var body = BrepPrimitives.CreateBox(4, 6, 8).Value;
var before = Counts(body); var firewallQuery = IntersectionQuery.Between(crossingLine, horizontal); var after = Counts(body);
Write("topology-firewall-proof.json", new
{
    before, after, unchanged = before == after, queryRelation = firewallQuery.Relation,
    witnessesAreAuthoritativeTrims = firewallQuery.WitnessesAreAuthoritativeTrims,
    resultContainsBrep = firewallQuery.GetType().GetProperties().Any(p => p.PropertyType == typeof(BrepBody)),
    law = "Generic numerical intersection may establish geometric evidence, but it does not author semantic topology."
});
var cone = new ConeSurface(Point3D.Origin, Direction3D.Create(new(1, 0, 0)), double.Pi / 4, Direction3D.Create(new(0, 0, 1)));
var coneSection = TransverseConePlaneIntersection.IntersectWorldZ(cone, 2);
Write("bounded-constructive-regression.json", new { success = coneSection.IsSuccess, resultFamily = coneSection.Value.GetType().Name, unchangedRoute = "TransverseConePlaneIntersection.IntersectWorldZ", genericQueryReplacement = false });

Write("api-architecture.json", new
{
    owner = "Aetheris.Geometry", matrix = new[] { "Curve-Plane", "Patch-Plane", "Curve-Patch", "Patch-Patch" },
    relationStates = Enum.GetNames<IntersectionRelation>(), evidenceClasses = Enum.GetNames<PredicateEvidenceKind>(),
    policy = IntersectionPolicy.Default,
    resultFields = typeof(IntersectionResult).GetProperties().Select(p => p.Name).ToArray(),
    witnessFields = typeof(IntersectionWitness).GetProperties().Select(p => p.Name).ToArray(),
    witnessAuthority = "EvidenceOnly; NonAuthoritative; NonExportableAsTrim",
    futureSeam = "ContactClassification / ContactOrder (not implemented)"
});

var operations = new (string Name, Func<IntersectionResult> Run)[]
{
    ("curve-plane", () => IntersectionQuery.Between(crossingLine, z0)),
    ("patch-plane", () => IntersectionQuery.Between(patchCrossing, z0)),
    ("curve-patch", () => IntersectionQuery.Between(crossingLine, horizontal)),
    ("patch-patch", () => IntersectionQuery.Between(horizontal, vertical))
};
var performance = new List<object>();
foreach (var operation in operations)
{
    for (var i = 0; i < 3; i++) operation.Run(); const int count = 25; var watch = Stopwatch.StartNew();
    IntersectionResult? last = null; for (var i = 0; i < count; i++) last = operation.Run(); watch.Stop();
    performance.Add(new { operation.Name, iterations = count, elapsedMilliseconds = watch.Elapsed.TotalMilliseconds,
        nanosecondsPerQuery = watch.Elapsed.TotalNanoseconds / count, last!.Statistics });
}
Write("performance.json", new { note = "Calibration only; no optimization campaign.", operations = performance });

var deterministicCases = new Dictionary<string, Func<IntersectionResult>>
{
    ["curve-plane"] = () => IntersectionQuery.Between(crossingLine, z0), ["patch-plane"] = () => IntersectionQuery.Between(patchCrossing, z0),
    ["curve-patch"] = () => IntersectionQuery.Between(crossingLine, horizontal), ["patch-patch"] = () => IntersectionQuery.Between(horizontal, vertical)
};
var reports = deterministicCases.OrderBy(x => x.Key, StringComparer.Ordinal).Select(pair =>
{
    var first = JsonSerializer.Serialize(pair.Value(), json); var second = JsonSerializer.Serialize(pair.Value(), json);
    return new { name = pair.Key, stable = first == second, sha256 = Sha(first) };
}).ToArray();
Write("deterministic-hashes.json", new { algorithm = "SHA-256", reports });
Console.WriteLine(JsonSerializer.Serialize(new { output, generated = Directory.GetFiles(output).Select(Path.GetFileName).Order().ToArray() }, json));

void Write(string name, object value) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, json) + Environment.NewLine, new UTF8Encoding(false));
string Sha(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
TopologyCounts Counts(BrepBody b) => new(b.Topology.Vertices.Count(), b.Topology.Edges.Count(), b.Topology.Faces.Count(), b.Geometry.Curves.Count(), b.Geometry.Surfaces.Count());
BoundedParametricCurve3 Line(string id, Point3D a, Point3D b) => BoundedParametricCurve3.LineSegment(id, a, b, "M4 evidence");
SurfacePointExpression GraphExpression(SurfaceScalarExpression z) => new(SurfaceExpression.Multiply(SurfaceExpression.Length(1), SurfaceExpression.U), SurfaceExpression.Multiply(SurfaceExpression.Length(1), SurfaceExpression.V), z);
BoundedParametricPatch3 Graph(string id, SurfaceScalarExpression z) => new(id, new(new(-1, 1), new(-1, 1)), GraphExpression(z), "M4 evidence");
BoundedParametricPatch3 PlanePatch(string id, double z) => BoundedParametricPatch3.Procedural(id, new(new(-1, 1), new(-1, 1)),
    (u, v) => new(new(u, v, z), new(1, 0, 0), new(0, 1, 0), Direction3D.Create(new(0, 0, 1)), false),
    (u, v) => new(new(u, v, z), new(1, 0, 0), new(0, 1, 0), new(0, 0, 0), new(0, 0, 0), new(0, 0, 0), DifferentialSingularityKind.Regular), "M4 evidence");
BoundedParametricPatch3 VerticalPatch(string id) => BoundedParametricPatch3.Procedural(id, new(new(-1, 1), new(-1, 1)),
    (u, v) => new(new(u, 0, v), new(1, 0, 0), new(0, 0, 1), Direction3D.Create(new(0, -1, 0)), false), "M4 evidence");

internal sealed record TopologyCounts(int Vertices, int Edges, int Faces, int Curves, int Surfaces);
