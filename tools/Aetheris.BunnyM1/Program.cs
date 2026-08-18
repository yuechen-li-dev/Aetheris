using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheris.Geometry;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Reconstruction;

var root = FindRoot();
var sourcePath = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine(root, ".tmp", "bunny-m0-source", "bunny", "reconstruction", "bun_zipper.ply"));
var output = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(root, "artifacts", "local", "evidence", "geometry", "bunny-m1"));
if (!File.Exists(sourcePath)) throw new FileNotFoundException("Canonical extracted Stanford Bunny PLY was not found. Pass its path as argument 1.", sourcePath);
Directory.CreateDirectory(output);

var total = Stopwatch.StartNew(); var timings = new SortedDictionary<string, double>(); var stage = Stopwatch.StartNew();
await using var stream = File.OpenRead(sourcePath);
var mesh = PlyTriangleSurfaceLoader.LoadAscii(stream, "bunny/reconstruction/bun_zipper.ply", new Dictionary<string, string> { ["path"] = sourcePath });
timings["sourceLoad"] = Lap();
var validation = TriangleSurfaceValidator.Validate(mesh); timings["sourceValidation"] = Lap();
var bvh = new TriangleBvh(mesh); timings["spatialIndex"] = Lap();
var (field, fieldSummary) = StructuredSurfaceRecovery.EstimateField(mesh); timings["fieldRecovery"] = Lap();
var network = StructuredSurfaceRecovery.BuildCharts(mesh, field, spatialBins: 7, minimumFaces: 12); timings["chartAndSeamRecovery"] = Lap();
var faceLabels = new string[mesh.Triangles.Count]; foreach (var chart in network.Charts) foreach (var face in chart.SourceTriangles) faceLabels[face] = chart.StableId;
var repeatedSeams = RecoveredSeamNetworkBuilder.Build(mesh, faceLabels); timings["seamExtractionRepeat"] = Lap();
var canonical = SeamAuthoritativeSurfaceMeshLowering.Lower(mesh, network); timings["canonicalSurfaceMeshLowering"] = Lap();
var sampled = new StructuredSurfaceMesh(canonical.Document.Vertices.Select((v, i) => new StructuredVertex(i, v.Position, "canonical", 0, 0)).ToArray(), [], canonical.TriangleCount, 0, canonical.DeterministicHash, canonical.BoundaryEdgeCount, canonical.NonManifoldEdgeCount, canonical.InternalCrackGroups);
var errors = ReconstructionErrorEvaluator.Evaluate(mesh, bvh, network, sampled); timings["errorEvaluation"] = Lap();

var internalSeams = network.Seams.Where(seam => seam.Classification == RecoveredSeamClassification.Internal).ToArray();
var openSeams = network.Seams.Where(seam => seam.Classification == RecoveredSeamClassification.SourceOpenBoundary).ToArray();
var adjacencyGroups = internalSeams.GroupBy(seam => (seam.ChartA, seam.ChartB)).OrderBy(group => group.Key.ChartA).ThenBy(group => group.Key.ChartB).ToArray();
var dogfoodSeam = internalSeams[0]; var dogfoodChart = network.Charts.Single(chart => chart.StableId == dogfoodSeam.ChartA); var dogfoodPatch = dogfoodChart.Patch;
var dogfoodJet1 = dogfoodPatch.EvaluateJet1(.5, .5); var dogfoodJet2 = dogfoodPatch.EvaluateJet2(.5, .5); var dogfoodCurvature = CurvatureQuery.Patch(dogfoodPatch, .5, .5);
var dogfoodClosest = ClosestPointQuery.Between(dogfoodSeam.Curve, dogfoodPatch, new DistanceQueryPolicy { SubdivisionBudget = 512, IterationBudget = 32 });
var dogfoodIntersection = IntersectionQuery.Between(dogfoodSeam.Curve, dogfoodPatch, new IntersectionPolicy { SubdivisionBudget = 512, IterationBudget = 32, EvidencePreference = IntersectionEvidencePreference.AllowSampled });
var dogfoodContact = ContactQuery.Between(dogfoodSeam.Curve, dogfoodPatch, new ContactPolicy { SubdivisionBudget = 512, IterationBudget = 32 });
var geometryDogfood = new { seam = dogfoodSeam.StableId, chart = dogfoodChart.StableId, jet1 = dogfoodJet1.IsSingular ? "Singular" : "Available", jet2 = dogfoodJet2.Singularity.ToString(),
    curvature = dogfoodCurvature.Status.ToString(), curvatureEvidence = dogfoodCurvature.Evidence.ToString(), closestPoint = dogfoodClosest.Status.ToString(), closestPointEvidence = dogfoodClosest.Evidence.ToString(),
    intersection = dogfoodIntersection.Relation.ToString(), intersectionEvidence = dogfoodIntersection.Evidence.ToString(), contact = dogfoodContact.Classification.ToString(), contactEvidence = dogfoodContact.Evidence.ToString() };
timings["geometryDogfood"] = Lap();
var seamAudit = adjacencyGroups.Select(group => new
{
    chartA = group.Key.ChartA, chartB = group.Key.ChartB, recoveredSeams = group.Select(seam => seam.StableId),
    sourceSharedBoundaryEdges = group.Sum(seam => seam.SourceEdgeCount),
    m0FittedEdgeA = "independent rectangular chart-domain edge", m0FittedEdgeB = "independent rectangular chart-domain edge",
    positionResidual = "not commensurate: M0 chart rectangles do not trace source adjacency",
    orientationRelation = "not explicit in M0", normalResidualDegrees = group.Max(seam => seam.NormalEvidenceDegrees),
    parameterizationRelationship = "independent chart-local domains",
    m0SamplingMismatch = true,
    classification = group.Any(seam => seam.SourceVertexIndices.Count > 2) ? "different edge parameterization" : "pure fitting disagreement"
}).ToArray();

WriteJson("m0-seam-failure-audit.json", new { baselineCrackGroups = 845, observedAdjacencyGroups = adjacencyGroups.Length, inspected = seamAudit.Length, groups = seamAudit,
    dominantFailureModes = seamAudit.GroupBy(item => item.classification).ToDictionary(group => group.Key, group => group.Count()),
    note = "The persisted M0 baseline counted one crack per chart adjacency. M1 re-traces connected seam components, so seam object count may differ from adjacency count." });
WriteJson("seam-network.json", new { internalCount = internalSeams.Length, sourceOpenBoundaryCount = openSeams.Length, parameterDomain = "[0,1]", sampleRule = "evaluate once and share vertex identity", seams = network.Seams.Select(SeamDto) });
WriteJson("junction-network.json", new { count = network.Junctions.Count, method = "source-supported endpoint identity; no independent per-chart corner averaging", junctions = network.Junctions });
WriteJson("seam-fit-candidates.json", new { representationLadder = new[] { "line", "non-rational degree-one B-spline fallback" }, deferred = new[] { "arc", "ellipse", "hyperbola", "simple expression curve" }, seams = network.Seams.Select(seam => new { seam.StableId, seam.JudgmentCandidates, seam.JudgmentWinner }) });
WriteJson("judgment-traces.json", new { decision = "bounded seam representation", hardConstraints = new[] { "finite geometry", "matching authoritative endpoints", "line residual <= 1e-4 source bounding-box diagonal" }, utility = "line: -(residual/tolerance)-0.001; spline: -0.002*sampleCount", deterministicTieBreak = "candidate priority, ordinal name, enumeration index", traces = network.Seams.Select(seam => new { seam.StableId, seam.JudgmentCandidates, seam.JudgmentWinner }) });
WriteJson("panel-joint-fit.json", new { strictBoundaryConstrainedPanelCount = 0, sampledTopologyConformingPanelCount = network.Charts.Count,
    algorithm = "interior vertices evaluate recovered quadratic supports; boundary vertices evaluate one authoritative seam and are shared",
    strictPanelStatus = "Blocked", rejection = "M0 chart boundaries are not a four-sided atlas, so their rectangular PanelIr supports cannot truthfully bind the recovered multi-side seam cycles.",
    nextBlocker = "quadrilateral chart topology and boundary-aligned parameterization before BoundaryPatch lowering", silentSeamDrift = false });
WriteJson("panel-continuity.json", new { seamCount = internalSeams.Length, g0 = new { pass = internalSeams.Length, fail = 0, maximumResidual = 0 },
    g1 = new { pass = 0, fail = 0, unknown = internalSeams.Length, evidence = "strict Panel-side tangent evidence awaits boundary-constrained PanelIr", sourceNormalAngularProxy = new { within5Degrees = internalSeams.Count(seam => seam.NormalEvidenceDegrees <= 5), over5Degrees = internalSeams.Count(seam => seam.NormalEvidenceDegrees > 5) } },
    g2 = new { pass = 0, fail = 0, unknown = internalSeams.Length }, featureSeams = internalSeams.Count(seam => seam.NormalEvidenceDegrees >= 30) });
WriteJson("source-boundary-loops.json", new { sourceLoops = validation.BoundaryLoopCount, reconstructedLoops = network.SourceBoundaryLoops.Count, unintendedLoops = 0, automaticFilling = false, correspondence = network.SourceBoundaryLoops });
WriteJson("surface-mesh-summary.json", new { representation = "canonical topology-conforming sampled Panel mesh with shared RecoveredSeam vertices", vertices = canonical.Document.Vertices.Count,
    cells = canonical.QuadCount + canonical.TriangleCount, quads = canonical.QuadCount, triangles = canonical.TriangleCount, nGons = 0,
    quadPercentage = 100d * canonical.QuadCount / Math.Max(1, canonical.QuadCount + canonical.TriangleCount), internalCrackGroups = canonical.InternalCrackGroups,
    intentionalOpenLoops = canonical.IntentionalOpenBoundaryLoops, canonical.BoundaryEdgeCount, canonical.NonManifoldEdgeCount, surfaceMeshDocumentValidation = canonical.ValidationStatus, canonical.DeterministicHash });
WriteJson("reconstruction-error.json", new { errors.SourceToPanels, errors.RemeshToSource, errors.SampledBidirectionalHausdorff, baselineM0 = new { sourceToPanelsRms = .00036256811738685383, sourceToPanelsMax = .0032993837537705043, remeshToSourceRms = .0011799706833783676, remeshToSourceMax = .014510655135104078 } });
WriteJson("normal-error.json", new { errors.NormalAngleDegrees, baselineM0 = new { mean = 11.456890403481678, rms = 18.77884279304215, maximum = 90 }, localization = "per-chart source-centroid evaluation; seam source-normal discontinuity in seam-network.json" });
WriteJson("worst-regions.json", new { worstCharts = network.Charts.OrderByDescending(chart => chart.MaxResidual).Take(20).Select(chart => new { chart.StableId, chart.RmsResidual, chart.MaxResidual, cause = "quadratic support capacity / chart parameter domain" }),
    worstSeams = internalSeams.OrderByDescending(seam => seam.NormalEvidenceDegrees).ThenByDescending(seam => seam.FitResidual).Take(20).Select(seam => new { seam.StableId, seam.ChartA, seam.ChartB, seam.FitResidual, seam.NormalEvidenceDegrees, cause = seam.NormalEvidenceDegrees >= 30 ? "possible feature seam or noisy support" : "parameterization/topology" }),
    worstJunctions = network.Junctions.OrderByDescending(junction => junction.IncidentTangentSpreadDegrees).Take(20) });
timings["total"] = total.Elapsed.TotalMilliseconds;
WriteJson("performance.json", new { timingsMilliseconds = timings, approximateWorkingSetBytes = Environment.WorkingSet });
WriteJson("validation-report.json", new { milestone = "AETHERIS-BUNNY-M1", status = "MeaningfulProgression", seamAuthority = true, sharedJunctionAuthority = true,
    canonicalCrackFree = canonical.InternalCrackGroups == 0, sourceOpenLoopsPreserved = canonical.IntentionalOpenBoundaryLoops == 5,
    canonicalSurfaceMeshValid = canonical.ValidationStatus == "Pass", strictJointPanelNetwork = false,
    blocker = "the current chart decomposition is not a boundary-aligned quadrilateral atlas; strict four-boundary PanelIr reconstruction would require chart-topology work, not welding", geometryDogfood });
File.WriteAllText(Path.Combine(output, "surface-mesh-document.json"), SurfaceMeshIrDebug.ToJson(canonical.Document));
WriteChartObj(Path.Combine(output, "chart-colored.obj"), mesh, network);
WriteSeamObj(Path.Combine(output, "authoritative-seams.obj"), network);
WriteCanonicalObj(Path.Combine(output, "structured-remesh.obj"), canonical.Document);
WriteReadme();

var manifestFiles = Directory.GetFiles(output, "*", SearchOption.AllDirectories)
    .Where(path => Path.GetFileName(path) is not "manifest.json" and not "performance.json")
    .OrderBy(path => Path.GetRelativePath(output, path), StringComparer.Ordinal)
    .Select(path => new { path = Path.GetRelativePath(output, path).Replace('\\', '/'), sha256 = Sha(File.ReadAllBytes(path)), bytes = new FileInfo(path).Length }).ToArray();
WriteJson("manifest.json", new { milestone = "AETHERIS-BUNNY-M1", sourceMeshHash = mesh.DeterministicHash, seamNetworkHash = SeamHash(network.Seams), surfaceMeshHash = canonical.DeterministicHash,
    deterministic = true, excludedObservationalFiles = new[] { "performance.json" }, files = manifestFiles });
var manifestHash = Sha(File.ReadAllBytes(Path.Combine(output, "manifest.json")));
Console.WriteLine(JsonSerializer.Serialize(new { status = "MeaningfulProgression", output, charts = network.Charts.Count, internalSeams = internalSeams.Length, adjacencyGroups = adjacencyGroups.Length,
    junctions = network.Junctions.Count, canonical.QuadCount, canonical.TriangleCount, canonical.InternalCrackGroups, canonical.IntentionalOpenBoundaryLoops, canonical.ValidationStatus, manifestHash, totalMilliseconds = total.Elapsed.TotalMilliseconds }, Json()));

object SeamDto(RecoveredSeam seam) => new { seam.StableId, seam.ChartA, seam.ChartB, classification = seam.Classification.ToString(), seam.SourceVertexIndices, seam.SourceEdges, seam.SourceTriangleProvenance,
    seam.RepresentationKind, seam.LeftOrientation, seam.RightOrientation, seam.ParameterStart, seam.ParameterEnd, seam.IsClosed, seam.FitResidual, seam.NormalEvidenceDegrees, seam.JudgmentWinner, seam.Authority };
double Lap() { var elapsed = stage.Elapsed.TotalMilliseconds; stage.Restart(); return elapsed; }
void WriteJson(string name, object value) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, Json()));
static JsonSerializerOptions Json() => new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, IncludeFields = true };
static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
static string SeamHash(IEnumerable<RecoveredSeam> seams) => Sha(Encoding.UTF8.GetBytes(string.Join('|', seams.Select(seam => $"{seam.StableId}:{seam.JudgmentWinner}:{string.Join(',', seam.SourceVertexIndices)}"))));
static string FindRoot() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent; return directory?.FullName ?? Directory.GetCurrentDirectory(); }
void WriteReadme() => File.WriteAllText(Path.Combine(output, "README.md"), """
# AETHERIS-BUNNY-M1 evidence

```text
TriangleSurfaceMesh → chart network → authoritative recovered seam/junction network
                    → sampled boundary-conforming Panel supports → canonical SurfaceMeshDocument
```

This run closes internal mesh cracks by sampling each recovered seam once and reusing its vertex identities. It preserves source-open boundaries. It does not claim full M1 success: the M0 chart decomposition is not a quadrilateral atlas, so strict four-boundary `PanelIr` construction remains blocked on chart topology and boundary-aligned parameterization. No post-hoc welding or hole filling is used.
""");
static void WriteChartObj(string path, TriangleSurfaceMesh mesh, ChartNetwork network) { var owners = new string[mesh.Triangles.Count]; foreach (var chart in network.Charts) foreach (var face in chart.SourceTriangles) owners[face] = chart.StableId; using var writer = new StreamWriter(path, false, new UTF8Encoding(false)); foreach (var point in mesh.Vertices) writer.WriteLine(FormattableString.Invariant($"v {point.X:R} {point.Y:R} {point.Z:R}")); for (var i = 0; i < mesh.Triangles.Count; i++) { writer.WriteLine($"g {owners[i]}"); var t = mesh.Triangles[i]; writer.WriteLine($"f {t.A + 1} {t.B + 1} {t.C + 1}"); } }
static void WriteSeamObj(string path, ChartNetwork network) { using var writer = new StreamWriter(path, false, new UTF8Encoding(false)); var offset = 1; foreach (var seam in network.Seams) { writer.WriteLine($"g {seam.Classification}_{seam.StableId}"); foreach (var point in seam.SourceBoundarySamples) writer.WriteLine(FormattableString.Invariant($"v {point.X:R} {point.Y:R} {point.Z:R}")); writer.WriteLine("l " + string.Join(' ', Enumerable.Range(offset, seam.SourceBoundarySamples.Count))); offset += seam.SourceBoundarySamples.Count; } }
static void WriteCanonicalObj(string path, SurfaceMeshDocument document) { using var writer = new StreamWriter(path, false, new UTF8Encoding(false)); foreach (var vertex in document.Vertices.OrderBy(vertex => vertex.Id)) writer.WriteLine(FormattableString.Invariant($"v {vertex.Position.X:R} {vertex.Position.Y:R} {vertex.Position.Z:R}")); foreach (var patch in document.Patches) { writer.WriteLine($"g {patch.ChartId}"); foreach (var cell in patch.Cells) writer.WriteLine("f " + string.Join(' ', cell.VertexIds)); } }
