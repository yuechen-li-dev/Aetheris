using System.Diagnostics;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Import;
using Aetheris.Kernel.Core.Step242;
using Rhino.FileIO;
using Rhino.Geometry;

namespace Aetheris.ThreeDm;

public enum ThreeDmStepImportStatus { Qualified, InspectableRecovered, Partial, Unsupported }

public sealed record ThreeDmStepBodyResult(
    int ObjectIndex, Guid SourceId, int SourceFaces, int BoundFaces,
    int SourceTrims, int BoundTrims, int SourcePcurves, int RecoveredPcurves,
    double WorstPcurveResidualMillimetres, bool SourceIsManifold,
    bool BoundIsEnclosed, bool BoundOrientationConsistent,
    bool StepExported, bool StepReimported, string? StepPath,
    double BindingMilliseconds, double ExportMilliseconds,
    IReadOnlyList<string> Diagnostics)
{
    public ThreeDmStepImportStatus Status => !StepReimported
        ? BoundFaces > 0 ? ThreeDmStepImportStatus.Partial : ThreeDmStepImportStatus.Unsupported
        : Adjustments.Count > 0 ? ThreeDmStepImportStatus.InspectableRecovered : ThreeDmStepImportStatus.Qualified;
    public double? MaximumBoundingBoxDriftMillimetres { get; init; }
    public IReadOnlyList<string> StepPaths { get; init; } = [];
    public int ComponentCount { get; init; } = 1;
    public IReadOnlyList<ThreeDmTopologyAdjustment> Adjustments { get; init; } = [];
}

public sealed record ThreeDmStepRecoveryReport(
    string SourcePath, double RecoveryToleranceMillimetres,
    IReadOnlyList<ThreeDmStepBodyResult> Bodies, double SourceReadMilliseconds,
    double SupportStudyMilliseconds, double TotalMilliseconds)
{
    public int QualifiedBodyCount => Bodies.Count(body => body.StepExported && body.StepReimported);
    public int PartialBodyCount => Bodies.Count - QualifiedBodyCount;
    public int BoundFaceCount => Bodies.Sum(body => body.BoundFaces);
    public int BoundTrimCount => Bodies.Sum(body => body.BoundTrims);
    public string? CombinedStepPath { get; init; }
    public bool CombinedStepReimported { get; init; }
    public int CombinedSolidCount { get; init; }
    public double? CombinedBoundingBoxDriftMillimetres { get; init; }
    public IReadOnlyList<string> CombinedDiagnostics { get; init; } = [];
}

/// <summary>Local 3DM-to-STEP qualification route; each source BRep fails independently.</summary>
public static class ThreeDmStepRecovery
{
    public static ThreeDmStepRecoveryReport Run(string path, string outputDirectory,
        double recoveryToleranceMillimetres = 0.1d, int? selectedObjectIndex = null,
        bool emitCombinedStep = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        if (!double.IsFinite(recoveryToleranceMillimetres) || recoveryToleranceMillimetres <= 0)
            throw new ArgumentOutOfRangeException(nameof(recoveryToleranceMillimetres));
        if (emitCombinedStep && selectedObjectIndex.HasValue)
            throw new ArgumentException("Combined STEP requires the complete source file.", nameof(selectedObjectIndex));
        var total = Stopwatch.StartNew();
        var study = Stopwatch.StartNew();
        var support = ThreeDmRecovery.Analyze(path, recoveryToleranceMillimetres);
        study.Stop();
        var read = Stopwatch.StartNew();
        using var file = File3dm.Read(Path.GetFullPath(path))
            ?? throw new InvalidDataException("OpenNURBS could not read the 3DM file.");
        read.Stop();
        Directory.CreateDirectory(outputDirectory);
        var results = new List<ThreeDmStepBodyResult>();
        var objectIndex = 0;
        foreach (var item in file.Objects)
        {
            if (selectedObjectIndex.HasValue && objectIndex != selectedObjectIndex.Value)
            { objectIndex++; continue; }
            if (item.Geometry is not Brep source) { objectIndex++; continue; }
            var components = ConnectedFaceComponents(source);
            Brep[] parts = components.Count == 1
                ? [source]
                : components.Select(faces => source.DuplicateSubBrep(faces)).ToArray();
            var partResults = parts.Select((part, index) => RecoverPart(part, objectIndex,
                item.Attributes.ObjectId, components.Count == 1 ? null : index, outputDirectory,
                support.MillimetresPerSourceUnit, recoveryToleranceMillimetres)).ToArray();
            var paths = partResults.Select(result => result.StepPath).OfType<string>().ToArray();
            results.Add(new(objectIndex, item.Attributes.ObjectId, source.Faces.Count,
                partResults.Sum(result => result.BoundFaces), source.Trims.Count,
                partResults.Sum(result => result.BoundTrims), partResults.Sum(result => result.SourcePcurves),
                partResults.Sum(result => result.RecoveredPcurves),
                partResults.Max(result => result.WorstPcurveResidualMillimetres), source.IsManifold,
                partResults.All(result => result.BoundIsEnclosed),
                partResults.All(result => result.BoundOrientationConsistent),
                partResults.All(result => result.StepExported),
                partResults.All(result => result.StepReimported), paths.FirstOrDefault(),
                partResults.Sum(result => result.BindingMilliseconds),
                partResults.Sum(result => result.ExportMilliseconds),
                partResults.SelectMany(result => result.Diagnostics).ToArray())
            {
                StepPaths = paths,
                ComponentCount = parts.Length,
                Adjustments = partResults.SelectMany(result => result.Adjustments).ToArray(),
                MaximumBoundingBoxDriftMillimetres = partResults.All(result => result.MaximumBoundingBoxDriftMillimetres.HasValue)
                    ? partResults.Max(result => result.MaximumBoundingBoxDriftMillimetres!.Value) : null
            });
            objectIndex++;
        }
        CombinedArtifact? combined = null;
        if (emitCombinedStep)
            combined = BuildCombinedArtifact(file, results, outputDirectory,
                support.MillimetresPerSourceUnit, recoveryToleranceMillimetres);
        total.Stop();
        return new(Path.GetFullPath(path), recoveryToleranceMillimetres, results,
            read.Elapsed.TotalMilliseconds, study.Elapsed.TotalMilliseconds, total.Elapsed.TotalMilliseconds)
        {
            CombinedStepPath = combined?.Path,
            CombinedStepReimported = combined?.Reimported ?? false,
            CombinedSolidCount = combined?.SolidCount ?? 0,
            CombinedBoundingBoxDriftMillimetres = combined?.BoundingBoxDriftMillimetres,
            CombinedDiagnostics = combined?.Diagnostics ?? []
        };
    }

    private sealed record CombinedArtifact(string? Path, bool Reimported, int SolidCount,
        double? BoundingBoxDriftMillimetres, IReadOnlyList<string> Diagnostics);

    private static CombinedArtifact BuildCombinedArtifact(File3dm source, IReadOnlyList<ThreeDmStepBodyResult> results,
        string outputDirectory, double scale, double tolerance)
    {
        if (results.Count == 0 || results.Any(result => !result.StepReimported))
            return new(null, false, 0, null, ["Combined STEP requires every selected source BRep to reimport successfully."]);
        var paths = results.SelectMany(result => result.StepPaths).ToArray();
        var importPolicy = new ImportPolicy(RecoveryToleranceMillimetres: tolerance,
            AllowBoundedNearCoincidentInnerLoop: true);
        var identity = new double[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
        var definitions = new List<Step242AssemblyDefinition>(paths.Length);
        var occurrences = new List<Step242AssemblyOccurrence>(paths.Length + 1)
            { new("root", "Recovered 3DM reference", null, null, identity) };
        foreach (var path in paths)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var body = Step242Importer.ImportBody(File.ReadAllText(path), importPolicy);
            if (!body.IsSuccess)
                return new(null, false, definitions.Count, null,
                    [$"Combined STEP input {name}: {string.Join("; ", body.Diagnostics.Select(diagnostic => diagnostic.Message))}"]);
            definitions.Add(new($"def:{name}", name, body.Value));
            occurrences.Add(new(name, name, "root", $"def:{name}", identity));
        }
        var step = Step242AssemblyExporter.Export(new("Recovered 3DM reference", "root", definitions, occurrences));
        if (!step.IsSuccess)
            return new(null, false, 0, null,
                [$"Combined STEP export: {string.Join("; ", step.Diagnostics.Select(diagnostic => diagnostic.Message))}"]);
        var combinedPath = Path.GetFullPath(Path.Combine(outputDirectory, "recovered-assembly.step"));
        File.WriteAllText(combinedPath, step.Value);
        var reimported = Step242AssemblyImporter.Import(step.Value, importPolicy);
        if (!reimported.IsSuccess)
            return new(combinedPath, false, 0, null,
                [$"Combined STEP reimport: {string.Join("; ", reimported.Diagnostics.Select(diagnostic => diagnostic.Message))}"]);
        var solids = reimported.Value.Definitions.Where(definition => definition.Geometry is not null).ToArray();
        if (solids.Length != paths.Length || reimported.Value.Occurrences.Count != paths.Length ||
            solids.Any(solid =>
            {
                var mass = BrepMassProperties.Evaluate(solid.Geometry!);
                return !mass.IsEnclosed || !mass.IsOrientationConsistent;
            }))
            return new(combinedPath, false, solids.Length, null,
                ["Combined STEP reimport changed the solid/occurrence count or enclosure/orientation."]);
        var drift = CombinedBoundingBoxDrift(source, solids.Select(solid => solid.Geometry!).ToArray(), scale);
        if (!double.IsFinite(drift) || drift > tolerance)
            return new(combinedPath, false, solids.Length, drift,
                [$"Combined STEP bounding-box drift {drift:G9} mm exceeds {tolerance:G9} mm."]);
        return new(combinedPath, true, solids.Length, drift, []);
    }

    private static double CombinedBoundingBoxDrift(File3dm source, IReadOnlyList<BrepBody> solids, double scale)
    {
        var sourcePoints = source.Objects.Select(item => item.Geometry).OfType<Brep>()
            .SelectMany(brep => brep.Edges).SelectMany(edge => Enumerable.Range(0, 129).Select(i =>
            {
                var domain = edge.Domain;
                var point = edge.PointAt(domain.T0 + (domain.T1 - domain.T0) * i / 128d);
                return (X: point.X * scale, Y: point.Y * scale, Z: point.Z * scale);
            })).ToArray();
        var targetPoints = solids.SelectMany(solid => solid.Topology.Edges.SelectMany(edge =>
        {
            var binding = solid.Bindings.GetEdgeBinding(edge.Id);
            var curve = solid.Geometry.GetCurve(binding.CurveGeometryId);
            var interval = binding.TrimInterval!.Value;
            return Enumerable.Range(0, 129).Select(i =>
            {
                var parameter = interval.Start + (interval.End - interval.Start) * i / 128d;
                var point = curve.Kind switch
                {
                    CurveGeometryKind.Line3 => curve.Line3!.Value.Evaluate(parameter),
                    CurveGeometryKind.Circle3 => curve.Circle3!.Value.Evaluate(parameter),
                    CurveGeometryKind.BSpline3 => curve.BSpline3!.Value.Evaluate(parameter),
                    CurveGeometryKind.Ellipse3 => curve.Ellipse3!.Value.Evaluate(parameter),
                    CurveGeometryKind.Hyperbola3 => curve.Hyperbola3!.Value.Evaluate(parameter),
                    _ => throw new NotSupportedException($"Assembly bounding-box sampling cannot evaluate {curve.Kind}.")
                };
                return (point.X, point.Y, point.Z);
            });
        })).ToArray();
        static double[] Bounds((double X, double Y, double Z)[] points) =>
            [points.Min(point => point.X), points.Max(point => point.X),
             points.Min(point => point.Y), points.Max(point => point.Y),
             points.Min(point => point.Z), points.Max(point => point.Z)];
        return Bounds(sourcePoints).Zip(Bounds(targetPoints), (a, b) => double.Abs(a - b)).Max();
    }

    private static ThreeDmStepBodyResult RecoverPart(Brep source, int objectIndex, Guid sourceId,
        int? componentIndex, string outputDirectory, double millimetresPerUnit, double recoveryToleranceMillimetres)
    {
            var label = componentIndex is int index ? $"object:{objectIndex}/component:{index}" : $"object:{objectIndex}";
            var bindingTimer = Stopwatch.StartNew();
            var bound = ThreeDmBrepBinder.Bind(source, objectIndex, sourceId,
                millimetresPerUnit, recoveryToleranceMillimetres);
            bindingTimer.Stop();
            var errors = bound.Diagnostics.ToList();
            var enclosed = false;
            var oriented = false;
            var exported = false;
            var reimported = false;
            double? boundingBoxDrift = null;
            string? stepPath = null;
            var exportTimer = new Stopwatch();
            if (bound.IsQualified && bound.Body is { } body)
            {
                var mass = BrepMassProperties.Evaluate(body);
                enclosed = mass.IsEnclosed;
                oriented = mass.IsOrientationConsistent;
                if (!enclosed || !oriented)
                    errors.Add($"{label}: bound shell failed enclosure/orientation: {string.Join("; ", mass.Topology.Messages)}");
                else
                {
                    exportTimer.Start();
                    // STEP EDGE_LOOP has no mixed singular-trim use. Keep the qualified UV
                    // bindings in the import body, but let STEP consumers rebind those loops.
                    var step = Step242Exporter.ExportBody(body, new Step242ExportOptions
                    {
                        ProductName = componentIndex is int component ? $"3dm-object-{objectIndex:D3}-component-{component:D2}" : $"3dm-object-{objectIndex:D3}",
                        BrepExportPreflightMode = BrepExportPreflightMode.Enforce,
                        BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute,
                        ImportedRecoveryToleranceMillimetres = recoveryToleranceMillimetres,
                        ImportedSourceVertexTolerancesMillimetres = bound.SourceVertexTolerancesMillimetres,
                        EmitQualifiedPcurves = !bound.Adjustments.Any(adjustment => adjustment.Kind == "RhinoSingularTrimCollapsedToVertex")
                    });
                    if (step.IsSuccess)
                    {
                        exported = true;
                        var name = componentIndex is int partIndex ? $"object-{objectIndex:D3}-component-{partIndex:D2}.step" : $"object-{objectIndex:D3}.step";
                        stepPath = Path.GetFullPath(Path.Combine(outputDirectory, name));
                        File.WriteAllText(stepPath, step.Value);
                        var imported = Step242Importer.ImportBody(step.Value, new ImportPolicy(
                            RecoveryToleranceMillimetres: recoveryToleranceMillimetres,
                            AllowBoundedNearCoincidentInnerLoop: true));
                        reimported = imported.IsSuccess;
                        if (!reimported)
                            errors.Add($"{label}: STEP reimport: {string.Join("; ", imported.Diagnostics.Select(d => d.Message))}");
                        else
                        {
                            var reimportMass = BrepMassProperties.Evaluate(imported.Value);
                            if (!reimportMass.IsEnclosed || !reimportMass.IsOrientationConsistent)
                            {
                                reimported = false;
                                errors.Add($"{label}: STEP reimport shell is not enclosed/oriented: {string.Join("; ", reimportMass.Topology.Messages)}");
                            }
                            else
                            {
                                boundingBoxDrift = MaximumBoundingBoxDrift(source, imported.Value,
                                    millimetresPerUnit);
                                if (!double.IsFinite(boundingBoxDrift.Value) ||
                                    boundingBoxDrift.Value > recoveryToleranceMillimetres)
                                {
                                    reimported = false;
                                    errors.Add($"{label}: source/STEP sampled bounding-box drift {boundingBoxDrift.Value:G6} mm exceeds {recoveryToleranceMillimetres:G6} mm.");
                                }
                            }
                        }
                    }
                    else errors.Add($"{label}: STEP export: {string.Join("; ", step.Diagnostics.Select(d => d.Message))}");
                    exportTimer.Stop();
                }
            }
            return new(objectIndex, sourceId, source.Faces.Count, bound.BoundFaces,
                source.Trims.Count, bound.BoundTrims, bound.SourcePcurves, bound.RecoveredPcurves,
                bound.WorstPcurveResidualMillimetres, source.IsManifold,
                enclosed, oriented, exported, reimported, stepPath,
                bindingTimer.Elapsed.TotalMilliseconds, exportTimer.Elapsed.TotalMilliseconds, errors)
                { MaximumBoundingBoxDriftMillimetres = boundingBoxDrift,
                  StepPaths = stepPath is null ? [] : [stepPath], Adjustments = bound.Adjustments };
    }

    private static IReadOnlyList<int[]> ConnectedFaceComponents(Brep source)
    {
        var neighbors = Enumerable.Range(0, source.Faces.Count).ToDictionary(index => index, _ => new HashSet<int>());
        foreach (var group in source.Trims.Where(trim => trim.Edge is not null && trim.Face is not null)
            .GroupBy(trim => trim.Edge!.EdgeIndex))
        {
            var incident = group.Select(trim => trim.Face!.FaceIndex).Distinct().ToArray();
            foreach (var a in incident) foreach (var b in incident)
                if (a != b) neighbors[a].Add(b);
        }
        var unvisited = neighbors.Keys.ToHashSet();
        var components = new List<int[]>();
        while (unvisited.Count > 0)
        {
            var queue = new Queue<int>(); queue.Enqueue(unvisited.Min());
            var component = new List<int>();
            while (queue.Count > 0)
            {
                var face = queue.Dequeue();
                if (!unvisited.Remove(face)) continue;
                component.Add(face);
                foreach (var neighbor in neighbors[face].Order()) if (unvisited.Contains(neighbor)) queue.Enqueue(neighbor);
            }
            components.Add(component.Order().ToArray());
        }
        return components;
    }

    private static double MaximumBoundingBoxDrift(Brep source, BrepBody reimported, double scale)
    {
        var sourcePoints = source.Edges.SelectMany(edge => Enumerable.Range(0, 129).Select(i =>
        {
            var domain = edge.Domain;
            var point = edge.PointAt(domain.T0 + (domain.T1 - domain.T0) * i / 128d);
            return (X: point.X * scale, Y: point.Y * scale, Z: point.Z * scale);
        })).ToArray();
        var targetPoints = reimported.Topology.Edges.SelectMany(edge =>
        {
            var binding = reimported.Bindings.GetEdgeBinding(edge.Id);
            var curve = reimported.Geometry.GetCurve(binding.CurveGeometryId);
            var interval = binding.TrimInterval!.Value;
            return Enumerable.Range(0, 129).Select(i =>
            {
                var parameter = interval.Start + (interval.End - interval.Start) * i / 128d;
                var point = curve.Kind switch
                {
                    CurveGeometryKind.Line3 => curve.Line3!.Value.Evaluate(parameter),
                    CurveGeometryKind.Circle3 => curve.Circle3!.Value.Evaluate(parameter),
                    CurveGeometryKind.BSpline3 => curve.BSpline3!.Value.Evaluate(parameter),
                    CurveGeometryKind.Ellipse3 => curve.Ellipse3!.Value.Evaluate(parameter),
                    CurveGeometryKind.Hyperbola3 => curve.Hyperbola3!.Value.Evaluate(parameter),
                    _ => throw new NotSupportedException($"Bounding-box sampling cannot evaluate {curve.Kind}.")
                };
                return (point.X, point.Y, point.Z);
            });
        }).ToArray();
        static double[] Bounds((double X, double Y, double Z)[] points) =>
            [points.Min(point => point.X), points.Max(point => point.X),
             points.Min(point => point.Y), points.Max(point => point.Y),
             points.Min(point => point.Z), points.Max(point => point.Z)];
        return Bounds(sourcePoints).Zip(Bounds(targetPoints), (a, b) => double.Abs(a - b)).Max();
    }
}
