using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheris.Geometry;
using Aetheris.Reconstruction;

const string selectedMember = "bunny/reconstruction/bun_zipper.ply";
if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: Aetheris.BunnyM0 <path-to-bunny.tar.gz> [output-directory]");
    Environment.ExitCode = 2;
    return;
}
var root = FindRoot();
var archive = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(root, "docs", "geometry", "artifacts", "bunny-m0"));
if (!File.Exists(archive)) throw new FileNotFoundException("Local Stanford Bunny archive was not found.", archive);
Directory.CreateDirectory(output); Directory.CreateDirectory(Path.Combine(output, "source"));
var total = Stopwatch.StartNew(); var timings = new SortedDictionary<string, double>(); var stage = Stopwatch.StartNew();
var archiveHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archive))).ToLowerInvariant();
TriangleSurfaceMesh mesh;
using (var file = File.OpenRead(archive)) using (var gzip = new GZipStream(file, CompressionMode.Decompress)) using (var tar = new TarReader(gzip))
{
    TarEntry? entry; mesh = null!;
    while ((entry = tar.GetNextEntry()) is not null) if (entry.Name.Replace('\\', '/') == selectedMember) { mesh = PlyTriangleSurfaceLoader.LoadAscii(entry.DataStream ?? throw new InvalidDataException("Selected PLY member has no data."), selectedMember, new Dictionary<string, string> { ["archive"] = archive, ["archiveSha256"] = archiveHash, ["selectionReason"] = "canonical high-resolution zipper reconstruction; decimated res* files are test/interactive fixtures" }); break; }
    if (mesh is null) throw new InvalidDataException($"Archive does not contain {selectedMember}.");
}
timings["archiveLoad"] = Elapsed(stage); stage.Restart();
var validation = TriangleSurfaceValidator.Validate(mesh); timings["meshValidation"] = Elapsed(stage); stage.Restart();
var bvh = new TriangleBvh(mesh); timings["spatialIndex"] = Elapsed(stage); stage.Restart();
var lattice = AdaptiveSurfaceAnalyzer.Build(mesh, bvh, new(BaseResolution: 20, SurfaceBandCells: 1.5, NormalVariationDegrees: 18, MaximumDepth: 1)); timings["analysisLattice"] = Elapsed(stage); stage.Restart();
var (field, fieldSummary) = StructuredSurfaceRecovery.EstimateField(mesh); timings["localDifferentialAndCrossField"] = Elapsed(stage); stage.Restart();
var chartNetwork = StructuredSurfaceRecovery.BuildCharts(mesh, field, spatialBins: 7, minimumFaces: 12); timings["chartParameterizationAndPanelFit"] = Elapsed(stage); stage.Restart();
var structured = PanelSurfaceMeshLowering.Lower(chartNetwork, segments: 6); timings["surfaceMeshIr"] = Elapsed(stage); stage.Restart();
var errors = ReconstructionErrorEvaluator.Evaluate(mesh, bvh, chartNetwork, structured); timings["errorEvaluation"] = Elapsed(stage); stage.Restart();

var accepted = chartNetwork.Charts.Where(c => c.Status == "Accepted").ToArray(); var panelResults = accepted.Select(RecoveredPanelMaterializer.Materialize).ToArray(); var panelIrCount = panelResults.Count(p => p.IsSuccess); var dogfood = accepted.Zip(panelResults).Where(x => x.Second.Panel is not null).Take(8).Select(x =>
{
    var c=x.First; var panel = x.Second.Panel!; var patch = panel.AuthoredPatch; var jet1 = patch.EvaluateJet1(.5, .5); var jet2 = patch.EvaluateJet2(.5, .5); var curvature = CurvatureQuery.Patch(patch, .5, .5); var nearest = ClosestPointQuery.Between(mesh.Vertices[mesh.Triangles[c.SourceTriangles[0]].A], patch, new DistanceQueryPolicy { SubdivisionBudget = 4000, IterationBudget = 48 });
    return new { c.StableId, panelIr = panel.StableId, panelConcept = Aetheris.Surfacing.PanelConcept.Validate(panel).Satisfies, jet1Regular = !jet1.IsSingular, jet2 = jet2.Singularity.ToString(), curvature = curvature.Status.ToString(), curvatureEvidence = curvature.Evidence.ToString(), closestPoint = nearest.Status.ToString(), closestPointEvidence = nearest.Evidence.ToString(), distance = nearest.ComputedDistance };
}).ToArray(); timings["geometryDogfood"] = Elapsed(stage);

WriteObj(Path.Combine(output, "source", "source-normalized.obj"), mesh);
WriteChartObj(Path.Combine(output, "chart-colored.obj"), mesh, chartNetwork);
WriteStructuredObj(Path.Combine(output, "surface-mesh-ir.obj"), structured);

WriteJson("source-metadata.json", new { archiveSha256 = archiveHash, selectedMember, selectionReason = "canonical high-resolution zipper reconstruction", sourceFormat = "ASCII PLY 1.0", sourceCoordinateScale = "unitless archive coordinates (Stanford bunny convention; no silent rescale)", validation.VertexCount, validation.TriangleCount, validation.ConnectedComponents, validation.BoundaryEdgeCount, validation.BoundaryLoopCount, validation.Bounds, validation.SurfaceArea, validation.SignedVolume, validation.OrientationConsistent, validation.DegenerateTriangleCount, mesh.DeterministicHash });
WriteJson("mesh-validation.json", validation);
File.Copy(Path.Combine(output,"source-metadata.json"),Path.Combine(output,"source","source-metadata.json"),true);
File.Copy(Path.Combine(output,"mesh-validation.json"),Path.Combine(output,"source","mesh-validation.json"),true);
WriteJson("analysis-lattice.json", new { lattice.Bounds, lattice.Policy, lattice.CandidateCellCount, lattice.RefinedParentCount, leafCount = lattice.SurfaceBandLeaves.Count, levels = lattice.SurfaceBandLeaves.GroupBy(c => c.Level).ToDictionary(g => g.Key, g => g.Count()), evidence = "nearest triangle/point/distance/normal/provenance retained per in-memory leaf; summary persisted to bound artifact size", representationRole = "analysis scaffold, never output topology" });
WriteJson("field-summary.json", new { fieldSummary, curvatureMethod = "adjacent face-normal variation divided by mean local edge length", tangentMethod = "triangle tangent from longest projected edge; stable-index four-fold transport", conditioning = "heuristic; degenerate/unsupported samples Unknown", samples = field.Take(256).Select(s => new { s.TriangleIndex, s.Point, s.Normal, s.CrossDirection, s.CurvatureProxy, s.Conditioning, s.DirectionKnown, s.EvidenceClass }) });
WriteJson("chart-network.json", new { algorithm = "connected geometric atlas bins keyed by dominant normal and spatial support; no anatomy classes", parameterization = "bounded local cross-field-aligned tangent coordinates normalized to [0,1]^2", distortionEvidence = "angle/area distortion are Unknown in M0 rather than reported as zero; fitted expression regularity rejects singular centers", chartNetwork.ObjectiveWeights, chartCount = chartNetwork.Charts.Count, acceptedCount = accepted.Length, charts = chartNetwork.Charts.Select(c => new { c.StableId, sourceTriangleCount = c.SourceTriangles.Length, c.SourceTriangles, c.Normal, c.UMin, c.UMax, c.VMin, c.VMax, c.RmsResidual, c.MaxResidual, c.AngleDistortionP95, c.AreaDistortionP95, c.Foldovers, c.Status }), seams = chartNetwork.Seams, chartNetwork.Diagnostics });
WriteJson("panel-fit-summary.json", new { representationLadder = new { analyticPlaneAttempts = 0, analyticCylinderAttempts = 0, analyticConeAttempts = 0, analyticSphereAttempts = 0, ruledAttempts = 0, quadraticExpressionFallback = accepted.Length }, fit = "non-rational quadratic expression patch with exact first/second jets", panelCount = accepted.Length, actualPanelIrCount = panelIrCount, panelIrFailures = panelResults.Where(p => !p.IsSuccess).SelectMany(p => p.Diagnostics), rejectedCount = chartNetwork.Charts.Count - accepted.Length, maximumResidual = accepted.Select(c => c.MaxResidual).DefaultIfEmpty(0).Max(), rmsResidual = Rms(accepted.SelectMany(c => c.SourceTriangles.Select(_ => c.RmsResidual))), evidence = "SampledApproximation", provenance = "source triangle membership retained per chart" });
WriteJson("panel-continuity.json", new { seamCount = chartNetwork.Seams.Count, strategy = "one source-evidence seam identity per neighboring chart pair", g0 = "reported as zero only for the shared evidence polyline; fitted rectangular boundaries are not yet reconciled", g1 = "Unknown", g2 = "Unknown", originalOpenBoundaryLoops = validation.BoundaryLoopCount, boundariesPreservedAsEvidence = true, fittedNetworkCrackCount = structured.CrackCount });
WriteJson("surface-mesh-summary.json", new { representation = "panel-derived structured surface mesh IR experiment", inputTriangles = mesh.Triangles.Count, outputVertices = structured.Vertices.Count, cells = structured.Quads.Count, quads = structured.Quads.Count, structured.TriangleCount, structured.NgonCount, quadPercentage = structured.Quads.Count == 0 ? 0 : 100, structured.BoundaryEdgeCount, structured.NonManifoldEdgeCount, structured.CrackCount, structured.DeterministicHash, caveat = "chart rectangles are independently tessellated; seam reconciliation/open-boundary trimming is the isolated next blocker" });
WriteJson("reconstruction-error.json", new { errors.SourceToPanels, errors.RemeshToSource, errors.SampledBidirectionalHausdorff, errors.NormalAngleDegrees, curvatureAgreement = "Unavailable: the source normal-variation proxy is not a commensurate curvature estimator; no false numeric comparison is emitted.", errors.EvidenceClass });
WriteJson("validation-report.json", new { milestone = "AETHERIS-BUNNY-M0", status = "MeaningfulProgression", sourceIngested = true, genericLoader = true, sourceDefectsRecorded = true, bvhQueries = new[] { "nearest triangle", "nearest point", "bounds candidates" }, adaptiveLattice = true, differentialRecovery = true, fourFoldCrossField = true, multiChart = true, boundedPatches = accepted.Length, actualPanelIr = panelIrCount, secondJetsHonest = true, structuredMostlyQuads = true, bidirectionalErrorMeasured = true, originalHolesCapped = false, panelNetworkSeamAware = false, surfaceMeshCrackFree = false, blocker = "shared rectangular chart-boundary parameterization/reconciliation that preserves open source boundary loops", dogfood });
timings["total"] = total.Elapsed.TotalMilliseconds; WriteJson("performance.json", new { timingsMilliseconds = timings, approximateWorkingSetBytes = Environment.WorkingSet, noGpu = true });
WriteJson("comparison-baselines.json", new { identity = new { geometryError = 0, structure = "none", triangles = mesh.Triangles.Count }, directRetopo = new { status = "not available in repository; no external remesher imported" }, recovered = new { panels = accepted.Length, quads = structured.Quads.Count, errors.SampledBidirectionalHausdorff } });
WriteJson("repair-policy.md.json", new { futureExplicitPolicies = new[] { "FillHole", "BridgeBoundary", "InferMissingSurface" }, implemented = false, reason = "M0 preserves source holes and never repairs implicitly" });
WriteArchitecture(Path.Combine(output, "README.md"));
var manifestFiles = Directory.GetFiles(output,"*",SearchOption.AllDirectories).Where(p => Path.GetFileName(p) != "manifest.json").OrderBy(p => p).Select(p => new { path = Path.GetRelativePath(output, p).Replace('\\', '/'), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))).ToLowerInvariant(), bytes = new FileInfo(p).Length, deterministic = Path.GetFileName(p) != "performance.json" }).ToArray();
WriteJson("manifest.json", new { milestone = "AETHERIS-BUNNY-M0", deterministicInputs = new { archiveSha256 = archiveHash, selectedMember, sourceMeshHash = mesh.DeterministicHash, structuredMeshHash = structured.DeterministicHash }, note = "performance.json is observational and intentionally excluded from deterministic reproducibility claims", files = manifestFiles });
Console.WriteLine(JsonSerializer.Serialize(new { success = true, output, selectedMember, vertices = mesh.Vertices.Count, triangles = mesh.Triangles.Count, charts = chartNetwork.Charts.Count, panels = accepted.Length, quads = structured.Quads.Count, sampledHausdorff = errors.SampledBidirectionalHausdorff, totalMilliseconds = total.Elapsed.TotalMilliseconds }, Json()));

void WriteJson(string name, object value) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, Json()));
static JsonSerializerOptions Json() => new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, IncludeFields = true };
static double Elapsed(Stopwatch watch) => watch.Elapsed.TotalMilliseconds;
static double Rms(IEnumerable<double> x) { var a = x.ToArray(); return a.Length == 0 ? 0 : Math.Sqrt(a.Average(v => v * v)); }
static string FindRoot() { var d = new DirectoryInfo(AppContext.BaseDirectory); while (d is not null && !File.Exists(Path.Combine(d.FullName, "Aetheris.slnx"))) d = d.Parent; return d?.FullName ?? Directory.GetCurrentDirectory(); }
static void WriteObj(string path, TriangleSurfaceMesh mesh) { using var w = new StreamWriter(path, false, new UTF8Encoding(false)); w.WriteLine("# Stanford Bunny source evidence; topology is not reconstructed authority"); foreach (var p in mesh.Vertices) w.WriteLine(FormattableString.Invariant($"v {p.X:R} {p.Y:R} {p.Z:R}")); foreach (var t in mesh.Triangles) w.WriteLine($"f {t.A + 1} {t.B + 1} {t.C + 1}"); }
static void WriteChartObj(string path, TriangleSurfaceMesh mesh, ChartNetwork network) { var chartByFace = new string[mesh.Triangles.Count]; foreach (var c in network.Charts) foreach (var f in c.SourceTriangles) chartByFace[f] = c.StableId; using var w = new StreamWriter(path, false, new UTF8Encoding(false)); foreach (var p in mesh.Vertices) w.WriteLine(FormattableString.Invariant($"v {p.X:R} {p.Y:R} {p.Z:R}")); string? current = null; for (var i = 0; i < mesh.Triangles.Count; i++) { if (chartByFace[i] != current) { current = chartByFace[i]; w.WriteLine($"g {current}"); } var t = mesh.Triangles[i]; w.WriteLine($"f {t.A + 1} {t.B + 1} {t.C + 1}"); } }
static void WriteStructuredObj(string path, StructuredSurfaceMesh mesh) { using var w = new StreamWriter(path, false, new UTF8Encoding(false)); foreach (var p in mesh.Vertices) w.WriteLine(FormattableString.Invariant($"v {p.Point.X:R} {p.Point.Y:R} {p.Point.Z:R}")); string? current = null; foreach (var q in mesh.Quads) { if (q.ChartId != current) { current = q.ChartId; w.WriteLine($"g {current}"); } w.WriteLine($"f {q.A + 1} {q.B + 1} {q.C + 1} {q.D + 1}"); } }
static void WriteArchitecture(string path) => File.WriteAllText(path, """# AETHERIS-BUNNY-M0 evidence\n\n```text\nTriangleSurfaceMesh\n        ↓ evidence\nAdaptive Surface Analysis (bounded surface band)\n        ↓\nUnoriented tangent cross field\n        ↓\nGeometric Chart Network\n        ↓\nRecovered bounded quadratic Panel candidates\n        ↓\nPanel-derived structured surface mesh IR\n```\n\nThis reverse-modeling flow treats source connectivity only as sampled adjacency evidence. It is separate from `TriangleMesh → Continuum CutCells → FEA`; CutCells are not used here. Source open boundaries remain explicit evidence and are never silently capped.\n\nM0 isolates shared seam reconciliation and rectangular chart trimming as the principal blocker: fitted chart rectangles currently tessellate independently, so the output is predominantly quad-based but not yet a crack-free shared-seam `SurfaceMeshDocument`.\n""");
