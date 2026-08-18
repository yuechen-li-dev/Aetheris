using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Geometry;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;
using Aetheris.Surfacing;

var output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "local", "evidence", "geometry", "reasoning-m5"));
Directory.CreateDirectory(output);
var json = new JsonSerializerOptions { WriteIndented = true }; json.Converters.Add(new JsonStringEnumConverter());
var z0 = new Plane3(Point3D.Origin, Direction3D.Create(new(0, 0, 1)));
var t = CurveExpression.T;

var scalar = new Dictionary<string, ContactQueryResult>
{
    ["t"] = ContactQuery.Between(CurveGraph("scalar-t", t), z0),
    ["t2"] = ContactQuery.Between(CurveGraph("scalar-t2", CurveExpression.Power(t, 2)), z0),
    ["t4"] = ContactQuery.Between(CurveGraph("scalar-t4", CurveExpression.Power(t, 4)), z0),
    ["structuralZero"] = ContactQuery.Between(BoundedParametricCurve3.LineSegment("scalar-zero", new(-1, 0, 0), new(1, 0, 0), "M5 evidence"), z0),
    ["singular"] = ContactQuery.Between(BoundedParametricCurve3.Procedural("scalar-singular", new(-1, 1), x => (new(x * x * x, 0, x * x * x * x), new Vector3D(3 * x * x, 0, 4 * x * x * x)), "M5 evidence"), z0)
};
Write("scalar-contact-fixtures.json", new
{
    definition = "g(t)=plane.normal dot (C(t)-plane.origin)",
    exactOrderAdmission = "regular g=0 and first nonzero derivative observed through order 2 under explicit tolerance",
    finiteJetLaw = "t^4 is AtLeast 2 / HigherOrderCandidate, never exact order 4",
    results = scalar
});

var paraboloid = Graph("paraboloid", SurfaceExpression.Add(PowerU(2), PowerV(2)));
var saddle = Graph("saddle", SurfaceExpression.Subtract(PowerU(2), PowerV(2)));
Write("patch-plane-fixtures.json", new
{
    paraboloid = ContactQuery.Between(paraboloid, z0),
    saddle = ContactQuery.Between(saddle, z0),
    saddleGuard = "Whole-domain SignedSide crossing takes precedence over the stationary first jet at the origin.",
    flat = ContactQuery.Between(PlanePatch("flat", Domain()), z0)
});
Write("saddle-zero-gradient-crossing.json", new
{
    fixture = "z=u^2-v^2 against z=0",
    originJet = saddle.EvaluateJet2(0, 0),
    signedSide = SignedSideQuery.Query(saddle, z0, SignedSidePolicy.Certified()),
    contact = ContactQuery.Between(saddle, z0),
    requiredConclusion = ContactClassification.Crossing
});

var plane = PlanePatch("curve-patch-plane", Domain());
Write("curve-patch-fixtures.json", new
{
    transverse = ContactQuery.Between(BoundedParametricCurve3.LineSegment("pierce", new(0, 0, -1), new(0, 0, 1), "M5 evidence"), plane),
    tangentSecondOrderSeparating = ContactQuery.Between(CurveGraph("curve-parabola", CurveExpression.Power(t, 2)), plane),
    secondOrderCompatible = ContactQuery.Between(BoundedParametricCurve3.LineSegment("in-plane", new(-1, 0, 0), new(1, 0, 0), "M5 evidence"), plane),
    orderRule = "No generic integer order is assigned for curve/patch in M5."
});

var horizontal = PlanePatch("horizontal", Domain());
var vertical = VerticalPatch("vertical");
var distinctFlat = PlanePatch("distinct-flat", Domain());
var bowl = Graph("patch-bowl", SurfaceExpression.Add(PowerU(2), PowerV(2)));
var cylinder = Graph("patch-cylinder", PowerU(2));
var rotatedCylinder = Graph("patch-rotated-cylinder", PowerV(2));
Write("patch-patch-fixtures.json", new
{
    transverse = ContactQuery.Between(horizontal, vertical),
    firstOrderCompatibleSecondOrderIncompatible = ContactQuery.Between(horizontal, bowl),
    secondOrderCompatible = ContactQuery.Between(horizontal, distinctFlat),
    directional = ContactQuery.Between(horizontal, cylinder),
    equalPrincipalValuesNotIdentity = new
    {
        curvatureA = CurvatureQuery.Patch(cylinder, 0, 0),
        curvatureB = CurvatureQuery.Patch(rotatedCylinder, 0, 0),
        contact = ContactQuery.Between(cylinder, rotatedCylinder),
        law = "Equal principal-curvature values without aligned directions/forms do not establish second-order compatibility or identity."
    },
    structuralCoincidence = ContactQuery.Between(horizontal, PlanePatch("horizontal", Domain())),
    sameIdentityDifferentDomain = ContactQuery.Between(horizontal, PlanePatch("horizontal", new(new(-.5, .5), new(-.5, .5)))),
    comparison = "Three shared physical tangent directions determine the geometric normal-curvature quadratic form; raw parameter derivatives are not compared."
});

var reparameterized = new BoundedParametricPatch3("reparameterized", Domain(), new(
    SurfaceExpression.Multiply(SurfaceExpression.Length(-2), SurfaceExpression.U),
    SurfaceExpression.Multiply(SurfaceExpression.Length(3), SurfaceExpression.V), SurfaceExpression.Length(0)), "M5 evidence");
var ordinaryContact = ContactQuery.Between(Graph("ordinary", SurfaceExpression.Length(0)), distinctFlat);
var reparameterizedContact = ContactQuery.Between(Graph("ordinary-2", SurfaceExpression.Length(0)), reparameterized);
var reversedCurve = new BoundedParametricCurve3("reversed-t2", new(-1, 1), new(
    CurveExpression.Multiply(CurveExpression.Length(-2), t), CurveExpression.Length(0),
    CurveExpression.Multiply(CurveExpression.Length(1), CurveExpression.Power(t, 2))), "M5 evidence");
Write("parameterization-invariance.json", new
{
    patch = new { ordinary = ordinaryContact, reparameterized = reparameterizedContact, invariant = ordinaryContact.Classification == reparameterizedContact.Classification },
    curve = new { ordinary = scalar["t2"], reversedScaled = ContactQuery.Between(reversedCurve, z0), invariant = scalar["t2"].OrderEvidence.Status == ContactQuery.Between(reversedCurve, z0).OrderEvidence.Status },
    principalDirectionsRequired = false
});

var leftFlat = GraphDomain("panel-left-flat", new(new(-1, 0), new(-1, 1)), SurfaceExpression.Length(0));
var rightBreak = GraphDomain("panel-right-break", new(new(0, 1), new(-1, 1)), PowerU(2));
var leftSmooth = GraphDomain("panel-left-smooth", new(new(-1, 0), new(-1, 1)), PowerU(2));
var rightSmooth = GraphDomain("panel-right-smooth", new(new(0, 1), new(-1, 1)), PowerU(2));
Write("panel-g1-g2-dogfood.json", new
{
    g1PassG2Fail = ContactQuery.Between(leftFlat, rightBreak),
    g2Pass = ContactQuery.Between(leftSmooth, rightSmooth),
    mapping = new { G0 = "positional meeting", G1 = "Tangent / first-order compatible", G2 = "SecondOrderCompatible" },
    panelApiReplaced = false,
    engineeringOwner = typeof(PanelNetworkValidator).FullName
});

var body = BrepPrimitives.CreateBox(4, 6, 8).Value; var before = Counts(body);
var firewall = ContactQuery.Between(horizontal, bowl); var after = Counts(body);
Write("topology-firewall.json", new
{
    before,
    after,
    unchanged = before == after,
    firewall.HasTopologyAuthority,
    resultContainsBrep = firewall.GetType().GetProperties().Any(p => p.PropertyType == typeof(BrepBody)),
    law = "ContactQuery is evidence-only and cannot create trims, split topology, reposition geometry, or generate response."
});

Write("api-architecture.json", new
{
    owner = "Aetheris.Geometry",
    matrix = new[] { "Curve-Plane", "Patch-Plane", "Curve-Patch", "Patch-Patch" },
    classifications = Enum.GetNames<ContactClassification>(),
    orderStates = Enum.GetNames<ContactOrderStatus>(),
    evidence = Enum.GetNames<PredicateEvidenceKind>(),
    scopes = Enum.GetNames<ContactEvidenceScope>(),
    policy = ContactPolicy.Default,
    resultFields = typeof(ContactQueryResult).GetProperties().Select(x => x.Name).ToArray(),
    classificationSeparateFromOrder = true,
    topologyAuthority = false,
    higherDerivativeDecision = "No CurveJet3/PatchJet3. Exact scalar order is bounded to 1/2; higher observations remain lower bounds/candidates."
});

var operations = new (string Name, Func<ContactQueryResult> Run)[]
{
    ("curve-plane", () => ContactQuery.Between(CurveGraph("perf-t2", CurveExpression.Power(t, 2)), z0)),
    ("patch-plane", () => ContactQuery.Between(paraboloid, z0)),
    ("curve-patch", () => ContactQuery.Between(CurveGraph("perf-cp", CurveExpression.Power(t, 2)), plane)),
    ("patch-patch", () => ContactQuery.Between(horizontal, bowl))
};
var timings = new List<object>();
foreach (var operation in operations)
{
    for (var i = 0; i < 3; i++) operation.Run(); const int count = 25; var watch = Stopwatch.StartNew(); ContactQueryResult? last = null;
    for (var i = 0; i < count; i++) last = operation.Run(); watch.Stop();
    timings.Add(new
    {
        operation.Name,
        iterations = count,
        elapsedMilliseconds = watch.Elapsed.TotalMilliseconds,
        nanosecondsPerQuery = watch.Elapsed.TotalNanoseconds / count,
        last!.Classification,
        last.Statistics
    });
}
Write("performance.json", new { note = "Calibration only; queries reuse M4 candidates and no optimization campaign was performed.", operations = timings });

var deterministic = operations.OrderBy(x => x.Name, StringComparer.Ordinal).Select(operation =>
{
    var first = JsonSerializer.Serialize(operation.Run(), json); var second = JsonSerializer.Serialize(operation.Run(), json);
    return new
    {
        operation.Name,
        stable = first == second,
        sha256 = Sha(first),
        classification = operation.Run().Classification,
        order = operation.Run().OrderEvidence,
        witnessCount = operation.Run().Witnesses.Count
    };
}).ToArray();
Write("deterministic-hashes.json", new { algorithm = "SHA-256", deterministic });
Console.WriteLine(JsonSerializer.Serialize(new { output, generated = Directory.GetFiles(output).Select(Path.GetFileName).Order().ToArray() }, json));

void Write(string name, object value) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, json) + Environment.NewLine, new UTF8Encoding(false));
string Sha(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
TopologyCounts Counts(BrepBody b) => new(b.Topology.Vertices.Count(), b.Topology.Edges.Count(), b.Topology.Faces.Count(), b.Geometry.Curves.Count(), b.Geometry.Surfaces.Count());
ParametricDomain Domain() => new(new(-1, 1), new(-1, 1));
SurfaceScalarExpression PowerU(int exponent) => SurfaceExpression.Multiply(SurfaceExpression.Length(1), SurfaceExpression.Power(SurfaceExpression.U, exponent));
SurfaceScalarExpression PowerV(int exponent) => SurfaceExpression.Multiply(SurfaceExpression.Length(1), SurfaceExpression.Power(SurfaceExpression.V, exponent));
BoundedParametricCurve3 CurveGraph(string id, SurfaceScalarExpression z) => new(id, new(-1, 1), new(
    CurveExpression.Multiply(CurveExpression.Length(1), CurveExpression.T), CurveExpression.Length(0), CurveExpression.Multiply(CurveExpression.Length(1), z)), "M5 evidence");
BoundedParametricPatch3 Graph(string id, SurfaceScalarExpression z) => GraphDomain(id, Domain(), z);
BoundedParametricPatch3 GraphDomain(string id, ParametricDomain domain, SurfaceScalarExpression z) => new(id, domain, new(
    SurfaceExpression.Multiply(SurfaceExpression.Length(1), SurfaceExpression.U), SurfaceExpression.Multiply(SurfaceExpression.Length(1), SurfaceExpression.V), z), "M5 evidence");
BoundedParametricPatch3 PlanePatch(string id, ParametricDomain domain) => BoundedParametricPatch3.Procedural(id, domain,
    (u, v) => new(new(u, v, 0), new(1, 0, 0), new(0, 1, 0), Direction3D.Create(new(0, 0, 1)), false),
    (u, v) => new(new(u, v, 0), new(1, 0, 0), new(0, 1, 0), Vector3D.Zero, Vector3D.Zero, Vector3D.Zero, DifferentialSingularityKind.Regular), "M5 evidence");
BoundedParametricPatch3 VerticalPatch(string id) => BoundedParametricPatch3.Procedural(id, Domain(),
    (u, v) => new(new(u, 0, v), new(1, 0, 0), new(0, 0, 1), Direction3D.Create(new(0, -1, 0)), false),
    (u, v) => new(new(u, 0, v), new(1, 0, 0), new(0, 0, 1), Vector3D.Zero, Vector3D.Zero, Vector3D.Zero, DifferentialSingularityKind.Regular), "M5 evidence");

internal sealed record TopologyCounts(int Vertices, int Edges, int Faces, int Curves, int Surfaces);
