using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aetheris.CLI;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Import;
using Aetheris.Kernel.Core.Step242;

// One specimen per process: the outer runner contains hangs and allocation failures.
if (args.Length is < 2 or > 4 || args.Length == 3 && args[2] is not ("--probe-pcurves" or "--probe-spheres") || args.Length == 4 && args[2] != "--face-step")
    throw new ArgumentException("Usage: Aetheris.StepLegacyCorpus <source.step> <output-directory> [--probe-pcurves | --probe-spheres | --face-step <entity-id>]");
int? debugFaceStepId = args.Length == 4 ? int.Parse(args[3], CultureInfo.InvariantCulture) : null;
var source = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(output);
var json = new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
var watch = Stopwatch.StartNew();
var text = File.ReadAllText(source);
var schema = Regex.Match(text, @"FILE_SCHEMA\s*\(\s*\((.*?)\)\s*\)", RegexOptions.Singleline).Groups[1].Value;
var diagnostics = new List<KernelDiagnostic>();
var bodies = new List<(string Id, BrepBody Body)>();
var instances = new List<(string Definition, Transform3D World)>();
object? structureEvidence = null;
string? failure = null;
var parsed = Step242SubsetParser.Parse(text);
var entities = parsed.IsSuccess ? parsed.Value.Entities.ToArray() : [];
var assembly = entities.Any(e => e.Name == "NEXT_ASSEMBLY_USAGE_OCCURRENCE");
try
{
    if (assembly)
    {
        var imported = Step242AssemblyImporter.Import(text);
        diagnostics.AddRange(imported.Diagnostics);
        if (imported.IsSuccess)
        {
            var structure = imported.Value;
            bodies.AddRange(structure.Definitions.Where(d => d.Geometry is not null).Select(d => (d.StableId, d.Geometry!)));
            var byId = structure.Occurrences.ToDictionary(o => o.StableId);
            var worlds = new Dictionary<string, Transform3D>();
            Transform3D World(string id)
            {
                if (worlds.TryGetValue(id, out var cached)) return cached;
                var occurrence = byId[id];
                var local = Transform3D.FromRowMajor(occurrence.LocalTransform);
                return worlds[id] = occurrence.ParentStableId is null ? local : local * World(occurrence.ParentStableId);
            }
            instances.AddRange(structure.Occurrences.Select(o => (o.DefinitionStableId, World(o.StableId))));
            structureEvidence = new { structure.RootDefinitionStableId, Definitions = structure.Definitions.Select(d => new { d.StableId, d.Name, d.RigidRootEntityId }), structure.Occurrences, structure.Provenance };
        }
        else failure = "Assembly import failed";
    }
    else
    {
        var imported = Step242Importer.ImportRigidRoots(text);
        diagnostics.AddRange(imported.Diagnostics);
        if (imported.IsSuccess)
        {
            bodies.AddRange(imported.Value.Select(r => (r.StepEntityId.ToString(CultureInfo.InvariantCulture), r.Body)));
            instances.AddRange(bodies.Select(b => (b.Id, Transform3D.Identity)));
        }
        else failure = "Rigid-root import failed";
    }
}
catch (Exception ex) { failure = ex.ToString(); }
var importMilliseconds = watch.Elapsed.TotalMilliseconds;
var reports = new List<object>();
var patches = new List<object>();
var displayedFaces = 0;
var boundFaces = 0;
var qualifications = new List<BrepImportQualificationReport>();
var normalization = new List<object>();
foreach (var (id, body) in bodies)
{
    try
    {
        object? probe = null;
        if (args.Length == 3 && args[2] == "--probe-pcurves")
        {
            var recovered = BrepPcurveRecovery.Populate(body.Topology, body.Geometry, body.Bindings, tolerance: 1e-3);
            probe = new { Recovery = recovered, Evidence = BrepPcurveValidator.Validate(body, 1e-3, requireEveryCoedge: true) };
        }
        var summary = StepAnalyzer.AnalyzeImportedBody(body, source).Summary;
        var displayOptions = DisplayTessellationOptions.ForViewport(body);
        var displayWatch = Stopwatch.StartNew();
        var mesh = BrepDisplayTessellator.TessellateBoundedPartial(body, displayOptions, TimeSpan.FromSeconds(30));
        var displayMilliseconds = displayWatch.Elapsed.TotalMilliseconds;
        var visible = mesh.FacePatches.Where(p => p.TriangleIndices.Count > 0).Select(p => p.FaceId).Distinct().ToHashSet();
        var missing = body.Topology.Faces.Where(f => !visible.Contains(f.Id)).Select(f => f.Id.Value).ToArray();
        var qualification = BrepImportQualification.WithDisplay(body, mesh);
        qualifications.Add(qualification);
        boundFaces += summary.FaceCount;
        displayedFaces += visible.Count;
        var rejectedDisplayFaces = body.Topology.Faces.Where(f => !visible.Contains(f.Id)
            || body.Bindings.GetFaceBinding(f.Id).SourceStepEntityId == debugFaceStepId
            || args.Length == 3 && args[2] == "--probe-spheres" && body.GetFaceSurface(f.Id).Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Sphere).Select(f => new {
            FaceId = f.Id.Value, StepEntityId = body.Bindings.GetFaceBinding(f.Id).SourceStepEntityId,
            Surface = body.Geometry.Surfaces.Single(s => s.Key == body.Bindings.GetFaceBinding(f.Id).SurfaceGeometryId).Value.Kind.ToString(),
            Support = body.Geometry.Surfaces.Single(s => s.Key == body.Bindings.GetFaceBinding(f.Id).SurfaceGeometryId).Value,
            Orientation = body.Bindings.GetFaceBinding(f.Id).Orientation,
            FaceBinding = body.Bindings.GetFaceBinding(f.Id),
            TriangulationInput = body.Geometry.Surfaces.Single(s => s.Key == body.Bindings.GetFaceBinding(f.Id).SurfaceGeometryId).Value.BSplineSurfaceWithKnots is not null
                ? SplineTrimInput(body, f.Id) : null,
            TriangulationOutput = mesh.FacePatches.Where(p => p.FaceId == f.Id)
                .Select(p => new { p.Positions, p.TriangleIndices }),
            Loops = f.LoopIds.Select(l => new { LoopId = l.Value, body.Topology.GetLoop(l).Kind,
                Coedges = body.Topology.GetLoop(l).CoedgeIds.Select(c => {
                    var coedge = body.Topology.GetCoedge(c); var edge = body.Topology.GetEdge(coedge.EdgeId);
                    body.TryGetVertexPoint(edge.StartVertexId, out var start); body.TryGetVertexPoint(edge.EndVertexId, out var end);
                    var hasPcurve = body.Bindings.TryGetPcurveBinding(c, out var pc);
                    return new { CoedgeId = c.Value, EdgeId = edge.Id.Value, coedge.IsReversed, Start = start, End = end,
                        EdgeBinding = body.Bindings.GetEdgeBinding(edge.Id), PcurveSameSense = hasPcurve ? pc.SameSense : (bool?)null,
                        Curve = body.GetEdgeCurve(edge.Id), body.Bindings.GetEdgeBinding(edge.Id).TrimInterval,
                        EdgeSamples = DebugEdgeSamples(body, edge.Id),
                        ProjectedEdgeSamples = DebugProjectedEdgeSamples(body, f.Id, edge.Id),
                        PcurveQualification = hasPcurve ? pc.Qualification : null,
                        Pcurve = hasPcurve ? pc.Pcurve : null,
                        PcurveSamples = hasPcurve ? Enumerable.Range(0, 33).Select(i => pc.Pcurve.Evaluate(
                            i == 32 ? pc.Pcurve.Domain.End : pc.Pcurve.Domain.Start + i / 32d * (pc.Pcurve.Domain.End - pc.Pcurve.Domain.Start))) : null,
                        PcurveStart = hasPcurve ? pc.Pcurve.Evaluate(pc.Pcurve.Domain.Start) : (Aetheris.Kernel.Core.Brep.SurfaceParameterPoint?)null,
                        PcurveEnd = hasPcurve ? pc.Pcurve.Evaluate(pc.Pcurve.Domain.End) : (Aetheris.Kernel.Core.Brep.SurfaceParameterPoint?)null };
                }) })
        });
        reports.Add(new { Id = id, Summary = summary, Qualification = qualification, DisplayedFaces = visible.Count,
            DisplayMilliseconds = displayMilliseconds, DisplayOptions = displayOptions,
            TriangleCount = mesh.FacePatches.Sum(p => p.TriangleIndices.Count / 3),
            MissingDisplayFaceIds = missing, RejectedDisplayFaces = rejectedDisplayFaces, mesh.FaceDiagnostics, PcurveProbe = probe });
        foreach (var instance in instances.Where(i => i.Definition == id))
            foreach (var patch in mesh.FacePatches)
                if (debugFaceStepId is null || body.Bindings.GetFaceBinding(patch.FaceId).SourceStepEntityId == debugFaceStepId)
                patches.Add(new { Definition = id, FaceId = patch.FaceId.Value, Positions = patch.Positions.Select(p => { var w = instance.World.Apply(p); return new[] { w.X, w.Y, w.Z }; }), patch.TriangleIndices,
                    QuadCells = patch.Cells?.OfType<QuadCell>().Select(cell => cell.VertexIds).ToArray() });
        if (qualification.GeometryStatus == ImportQualificationStatus.Qualified && args.Length == 2)
        {
            try
            {
                // The corpus uses the importer-qualified engineering recovery budget,
                // never a display tolerance. Exact geometry keeps the stricter trim
                // budget; recovered ordinary parts remain bounded by 0.1 mm.
                var normalizationTolerance = double.Min(.1d, double.Max(qualification.PcurveToleranceMillimetres,
                    body.Geometry.Curves.Select(c => c.Value.RecoveryProvenance?.RecoveryToleranceMillimetres ?? 0d)
                        .Concat(body.Geometry.Surfaces.Select(s => s.Value.RecoveryProvenance?.RecoveryToleranceMillimetres ?? 0d)).DefaultIfEmpty(0d).Max()));
                var exported = Step242Exporter.ExportBody(body, new Step242ExportOptions {
                    BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute,
                    BrepExportPreflightMode = BrepExportPreflightMode.Enforce,
                    ImportedRecoveryToleranceMillimetres = normalizationTolerance,
                    ImportedSourceVertexTolerancesMillimetres = body.Topology.Vertices.ToDictionary(v => v.Id,
                        _ => normalizationTolerance)
                });
                if (!exported.IsSuccess) normalization.Add(new { Id = id, Success = false, Diagnostics = exported.Diagnostics });
                else
                {
                    File.WriteAllText(Path.Combine(output, $"normalized-{id}.step"), exported.Value);
                    var reimport = Step242Importer.ImportBody(exported.Value);
                    if (!reimport.IsSuccess) normalization.Add(new { Id = id, Success = false, Diagnostics = reimport.Diagnostics });
                    else
                    {
                        var after = StepAnalyzer.AnalyzeImportedBody(reimport.Value, "normalized").Summary;
                        var beforeCounts = new[] { summary.BodyCount, summary.ShellCount, summary.FaceCount, summary.EdgeCount, summary.VertexCount };
                        var afterCounts = new[] { after.BodyCount, after.ShellCount, after.FaceCount, after.EdgeCount, after.VertexCount };
                        var boundsDeviation = summary.BoundingBox is { } a && after.BoundingBox is { } b
                            ? double.Max((a.Min - b.Min).Length, (a.Max - b.Max).Length) : double.PositiveInfinity;
                        // Serialization reallocates IDs and can rebuild curve parameter
                        // origins. Match geometric endpoints, then compare directed
                        // fractions in each curve's own native trim domain.
                        var restoredEdges = EdgeSamples(reimport.Value).ToList();
                        var deviation = 0d;
                        var matched = 0;
                        foreach (var original in EdgeSamples(body))
                        {
                            // Rounded coordinate keys can straddle a quantization
                            // boundary even when the vertices agree well within the
                            // comparison budget. Compare actual endpoint distances.
                            var candidates = restoredEdges.Where(candidate =>
                                ((original.Start - candidate.Start).Length <= normalizationTolerance && (original.End - candidate.End).Length <= normalizationTolerance) ||
                                ((original.Start - candidate.End).Length <= normalizationTolerance && (original.End - candidate.Start).Length <= normalizationTolerance)).ToArray();
                            if (candidates.Length == 0) { deviation = double.PositiveInfinity; break; }
                            var distances = candidates.Select(candidate => {
                                var forward = original.Points.Zip(candidate.Points).Max(pair => (pair.First - pair.Second).Length);
                                var reverse = original.Points.Zip(candidate.Points.Reverse()).Max(pair => (pair.First - pair.Second).Length);
                                return (Candidate: candidate, Distance: double.Min(forward, reverse));
                            }).OrderBy(d => d.Distance).ToArray();
                            deviation = double.Max(deviation, distances[0].Distance);
                            restoredEdges.Remove(distances[0].Candidate);
                            matched++;
                        }
                        var success = beforeCounts.SequenceEqual(afterCounts) && boundsDeviation <= normalizationTolerance && deviation <= normalizationTolerance
                            && after.ImportQualification?.GeometryStatus == ImportQualificationStatus.Qualified;
                        normalization.Add(new { Id = id, Success = success, BeforeCounts = beforeCounts, AfterCounts = afterCounts,
                            BoundsDeviationMillimetres = boundsDeviation, SampledEdgeDeviationMillimetres = double.IsFinite(deviation) ? deviation : (double?)null,
                            MatchedEdges = matched, EngineeringToleranceMillimetres = normalizationTolerance,
                            NonRationalStep = !exported.Value.Contains("RATIONAL_B_SPLINE", StringComparison.Ordinal),
                            ExportRecovery = exported.Diagnostics.Where(d => d.Source == "Step242.ProductionNormalization"),
                            ComparisonBasis = "33 samples per edge; endpoint-matched, native parameter fractions; interiors not sampled", After = after });
                    }
                }
            }
            catch (Exception ex) { normalization.Add(new { Id = id, Success = false, Failure = ex.ToString() }); }
        }
    }
    catch (Exception ex) { reports.Add(new { Id = id, Failure = ex.ToString() }); }
}
var parsedFaces = entities.Count(e => e.Name == "ADVANCED_FACE");
// Inspectable remains a candidate, never a claim that visual review/normalization passed.
var status = bodies.Count == 0 ? "Failed" : qualifications.Count != bodies.Count || boundFaces != parsedFaces ? "Degraded"
    : qualifications.Select(q => q.Status).DefaultIfEmpty(ImportQualificationStatus.Failed).Max().ToString();
File.WriteAllText(Path.Combine(output, "mesh.json"), JsonSerializer.Serialize(patches, json));
File.WriteAllText(Path.Combine(output, "diagnostics.json"), JsonSerializer.Serialize(new {
    Filename = Path.GetFileName(source), Sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(source))),
    Bytes = new FileInfo(source).Length, Schema = schema, SourceMillimetresPerUnit = parsed.IsSuccess ? parsed.Value.SourceMillimetresPerUnit : (double?)null,
    ProductCount = entities.Count(e => e.Name == "PRODUCT"), AssemblyUsageCount = entities.Count(e => e.Name == "NEXT_ASSEMBLY_USAGE_OCCURRENCE"),
    RigidRootCount = entities.Count(e => e.Name is "MANIFOLD_SOLID_BREP" or "BREP_WITH_VOIDS"),
    ImportedDefinitions = bodies.Count, Occurrences = instances.Count, ParsedFaces = parsedFaces, BoundFaces = boundFaces, DisplayedFaces = displayedFaces,
    ImportStatus = status, ImportMilliseconds = importMilliseconds, TotalMilliseconds = watch.Elapsed.TotalMilliseconds,
    Failure = failure, Diagnostics = diagnostics, Bodies = reports, Structure = structureEvidence,
    VisualReview = "pending", Normalization = normalization
}, json));
Console.WriteLine($"{Path.GetFileName(source)}: {status}; faces {boundFaces}/{parsedFaces}, displayed {displayedFaces}; {importMilliseconds:F0}ms");

static object? DebugProjectedEdgeSamples(BrepBody body, Aetheris.Kernel.Core.Topology.FaceId face, Aetheris.Kernel.Core.Topology.EdgeId edge)
{
    if (!body.TryGetFaceSurface(face, out var support) || support?.BSplineSurfaceWithKnots is not { } spline) return null;
    SurfaceParameterPoint? previous = null;
    return DebugEdgeSamples(body, edge).Select(point => {
        var result = BrepPcurveRecovery.ProjectSplinePoint(spline, point, previous, 1e-8);
        previous = result.Uv;
        return new { result.Success, result.Uv, result.Residual };
    }).ToArray();
}

static IEnumerable<(Point3D Start, Point3D End, Point3D[] Points)> EdgeSamples(BrepBody body)
{
    foreach (var edge in body.Topology.Edges)
    {
        body.TryGetVertexPoint(edge.StartVertexId, out var a); body.TryGetVertexPoint(edge.EndVertexId, out var b);
        var binding = body.Bindings.GetEdgeBinding(edge.Id);
        var curve = body.GetEdgeCurve(edge.Id);
        var interval = binding.TrimInterval!.Value;
        var points = Enumerable.Range(0, 33).Select(index => {
            var fraction = binding.OrientedEdgeSense ? index / 32d : 1d - index / 32d;
            return BrepPcurveValidator.Evaluate(curve, interval.Start + (interval.End - interval.Start) * fraction)
                ?? throw new NotSupportedException($"Edge sample evaluator unavailable for {curve.Kind}");
        }).ToArray();
        yield return (a, b, points);
    }
}

static object SplineTrimInput(BrepBody body, Aetheris.Kernel.Core.Topology.FaceId face)
{
    var input = BrepDisplayTessellator.InspectSplineTrim(body, face);
    return new { input.IsSuccess, input.Diagnostics,
        Loops = input.IsSuccess ? input.Value.Select(l => l.Select(p => new { p.U, p.V }).ToArray()).ToArray() : null };
}

static Point3D[] DebugEdgeSamples(BrepBody body, Aetheris.Kernel.Core.Topology.EdgeId edge)
{
    if (body.Bindings.GetEdgeBinding(edge).TrimInterval is not { } interval) return [];
    var curve = body.GetEdgeCurve(edge);
    return Enumerable.Range(0, 65).Select(i => BrepPcurveValidator.Evaluate(curve,
        i == 64 ? interval.End : interval.Start + i / 64d * (interval.End - interval.Start)))
        .Where(p => p.HasValue).Select(p => p!.Value).ToArray();
}
