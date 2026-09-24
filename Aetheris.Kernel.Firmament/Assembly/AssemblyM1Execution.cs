using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Surfacing;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Assembly;

public sealed record AssemblyExecutedGeometry(
    AssemblyGeometryArtifactIr Artifact,
    IReadOnlyDictionary<string, BrepBody> DefinitionBodies,
    IReadOnlyDictionary<string, BrepBody> InstanceBodies);

public sealed record AssemblyM1CompilationResult(
    AssemblyIr? Ir,
    AssemblyExecutedGeometry? Geometry,
    IReadOnlyList<AssemblyDiagnostic> Diagnostics,
    AssemblyPerformanceIr? Performance = null)
{
    public bool IsSuccess => Ir is not null && Geometry is not null && Diagnostics.All(diagnostic => diagnostic.Severity != AssemblyDiagnosticSeverity.Error);
}

/// <summary>
/// Exact part materialization supplied by a domain compiler to the ordinary
/// Assembly pipeline.  This is the cross-profile seam used by Sheet Metal: the
/// Assembly compiler continues to own hierarchy, mates, placement and product
/// export, while the owning part compiler remains authoritative for geometry.
/// </summary>
public sealed record AssemblyPartMaterialization(
    BrepBody Body,
    string SpecializationIdentity,
    IReadOnlyList<SemanticProvenance> Provenance,
    IReadOnlyList<SemanticValue>? Semantics = null);

public delegate AssemblyPartMaterialization? AssemblyPartMaterializer(
    string definitionIdentity,
    string? declarationSource,
    string sourceIdentity,
    IList<AssemblyDiagnostic> diagnostics);

internal sealed record MaterializedAssemblyDefinition(
    string DefinitionIdentity,
    string SpecializationIdentity,
    BrepBody Body,
    IReadOnlyList<SemanticValue> Semantics,
    AssemblyDefinitionArtifactIr Artifact);

/// <summary>
/// M1's definition seam: specialize through the ordinary Firmament compiler, then
/// reimport its canonical AP242 exact body. Assembly has no second part generator.
/// </summary>
internal static class AssemblyDefinitionMaterializer
{
    public static MaterializedAssemblyDefinition? TryMaterialize(string definitionIdentity, string? definitionSource, string sourceIdentity, List<AssemblyDiagnostic> diagnostics, FirmamentProjectSnapshot? project = null)
    {
        if (!string.IsNullOrWhiteSpace(definitionSource))
        {
            // Keep the authored SectionChain in its own source document so sibling
            // assembly templates cannot leak unrelated profiles into its binder.
            var sectionFile = System.Text.RegularExpressions.Regex.Match(definitionIdentity,
                "^(?:SectionChainFile|LoftFile)<\\\"(?<path>[^\\\"]+)\\\">$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (sectionFile.Success)
            {
                var requestedPath = sectionFile.Groups["path"].Value;
                string sectionPath;
                string sectionSource;
                if (project is not null)
                {
                    try
                    {
                        sectionPath = FirmamentProjectSnapshot.NormalizePath(requestedPath);
                    }
                    catch (ArgumentException)
                    {
                        diagnostics.Add(new("assembly-profile-invalid-resource-path", $"SectionChain source '{requestedPath}' escapes the project root or uses an invalid path."));
                        return null;
                    }
                    if (!sectionPath.EndsWith(".firmament", StringComparison.OrdinalIgnoreCase))
                    {
                        diagnostics.Add(new("assembly-profile-resource-kind-invalid", $"SectionChain source '{sectionPath}' must be a .firmament document."));
                        return null;
                    }
                    if (!project.TryResolve(sectionPath, out sectionSource!))
                    {
                        diagnostics.Add(new("assembly-profile-unresolved-resource", $"SectionChain source '{sectionPath}' was not found in the project snapshot."));
                        return null;
                    }
                }
                else
                {
                    var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(sourceIdentity)) ?? Directory.GetCurrentDirectory();
                    sectionPath = Path.GetFullPath(Path.Combine(baseDirectory, requestedPath.Replace('/', Path.DirectorySeparatorChar)));
                    if (!File.Exists(sectionPath))
                    {
                        diagnostics.Add(new("assembly-profile-unresolved-resource", $"SectionChain source '{requestedPath}' was not found at '{sectionPath}'."));
                        return null;
                    }
                    sectionSource = File.ReadAllText(sectionPath);
                }
                var section = SectionChainAuthoringParser.Compile(sectionSource);
                if (!section.IsSuccess || section.Materialization?.Body is not { } sectionBody ||
                    section.Materialization.StructureKind != SectionChainStructureKind.ClosedSolid)
                {
                    foreach (var diagnostic in section.Diagnostics)
                        diagnostics.Add(new("assembly-definition-materialization-failed", $"SectionChain source '{sectionPath}': {diagnostic}"));
                    if (section.IsSuccess)
                        diagnostics.Add(new("assembly-definition-materialization-failed", $"SectionChain source '{sectionPath}' must materialize one capped closed solid."));
                    return null;
                }
                var sectionStep = Step242Exporter.ExportBody(sectionBody);
                if (!sectionStep.IsSuccess || sectionStep.Value is null)
                {
                    foreach (var diagnostic in sectionStep.Diagnostics)
                        diagnostics.Add(new("assembly-definition-materialization-failed", $"SectionChain source '{sectionPath}' STEP export failed: {diagnostic.Message}"));
                    return null;
                }
                var sectionImport = Step242Importer.ImportBody(sectionStep.Value);
                if (!sectionImport.IsSuccess || sectionImport.Value is null)
                {
                    foreach (var diagnostic in sectionImport.Diagnostics)
                        diagnostics.Add(new("assembly-definition-reimport-failed", $"SectionChain source '{sectionPath}' STEP reimport failed: {diagnostic.Message}"));
                    return null;
                }
                var sectionHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sectionStep.Value)));
                var sectionStableId = "assembly-definition:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(definitionIdentity)))[..16];
                var sectionProvenance = new[] { new SemanticProvenance("section-chain-source", sectionPath, section.Chain!.StableId, SemanticSourceSpan.Generated(sourceIdentity)) };
                return new(definitionIdentity, "section-chain:" + sectionHash[..16], sectionImport.Value, [],
                    new(sectionStableId, definitionIdentity, "section-chain:" + sectionHash[..16], sectionHash, Metrics(sectionImport.Value), sectionProvenance));
            }
            var gearDocument = GearAuthoring.ParseDefinitions(definitionSource);
            var gear = gearDocument.Gears.SingleOrDefault(candidate => string.Equals(candidate.Name, definitionIdentity, StringComparison.Ordinal));
            if (gear is not null)
            {
                var materialization = GearAuthoring.Materialize(gear, out var gearDiagnostics);
                if (materialization is null)
                {
                    foreach (var diagnostic in gearDiagnostics)
                        diagnostics.Add(new("assembly-definition-materialization-failed", $"Gear definition '{definitionIdentity}' failed Gear-authoritative materialization: {diagnostic}"));
                    return null;
                }
                var step = Step242Exporter.ExportBody(materialization.Body);
                var gearHash = step.IsSuccess
                    ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(step.Value)))
                    : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(definitionIdentity)));
                var gearStableId = "assembly-definition:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(definitionIdentity)))[..16];
                var gearProvenance = new[] { new SemanticProvenance("gear-definition", definitionIdentity, "GearAuthoring.Materialize", SemanticSourceSpan.Generated(sourceIdentity)) };
                return new(definitionIdentity, "gear:" + definitionIdentity, materialization.Body, [],
                    new(gearStableId, definitionIdentity, "gear:" + definitionIdentity, gearHash, Metrics(materialization.Body), gearProvenance));
            }
            // An ordinary template specialization must not see sibling Gear roots:
            // the standalone build route would otherwise select the Gear profile and
            // successfully return that gear for every shaft, support and wheel.
            // Use producer-owned declaration spans; retain offsets for diagnostics.
            if (gearDocument.Gears.Count > 0)
            {
                var ordinaryDeclarations = definitionSource.ToCharArray();
                foreach (var sibling in gearDocument.Gears)
                    for (var i = sibling.SourceSpan.Start; i < sibling.SourceSpan.Start + sibling.SourceSpan.Length; i++)
                        if (ordinaryDeclarations[i] is not ('\r' or '\n')) ordinaryDeclarations[i] = ' ';
                definitionSource = new string(ordinaryDeclarations);
            }
        }
        var externalStep = System.Text.RegularExpressions.Regex.Match(definitionIdentity, "^ExternalStep<\\\"(?<path>[^\\\"]+)\\\">$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (externalStep.Success)
        {
            if (project is not null)
            {
                diagnostics.Add(new("assembly-profile-external-resource-unsupported", "ExternalStep is not a project source document and cannot be resolved from a Firmament project snapshot."));
                return null;
            }
            var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(sourceIdentity)) ?? Directory.GetCurrentDirectory();
            var resolvedPath = Path.GetFullPath(Path.Combine(baseDirectory, externalStep.Groups["path"].Value.Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(resolvedPath))
            {
                diagnostics.Add(new("assembly-profile-unresolved-resource", $"External STEP resource '{externalStep.Groups["path"].Value}' was not found at '{resolvedPath}'."));
                return null;
            }
            var stepText = File.ReadAllText(resolvedPath);
            var externalImport = Step242Importer.ImportBody(stepText);
            if (!externalImport.IsSuccess || externalImport.Value is null)
            {
                foreach (var diagnostic in externalImport.Diagnostics)
                    diagnostics.Add(new("assembly-definition-reimport-failed", $"External STEP definition '{definitionIdentity}' failed import: {diagnostic.Message}"));
                return null;
            }
            var externalHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stepText)));
            var externalStableId = "assembly-definition:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(definitionIdentity)))[..16];
            var externalProvenance = new[] { new SemanticProvenance("imported-step-definition", resolvedPath, externalHash, SemanticSourceSpan.Generated(sourceIdentity)) };
            return new(definitionIdentity, "external-step:" + externalHash[..16], externalImport.Value, [],
                new(externalStableId, definitionIdentity, "external-step:" + externalHash[..16], externalHash, Metrics(externalImport.Value), externalProvenance));
        }
        if (string.IsNullOrWhiteSpace(definitionSource) || !definitionIdentity.Contains('<', StringComparison.Ordinal)) return null;
        // Assembly files keep reusable declarations beside relational assembly syntax.
        // Wrap that declaration catalog in the ordinary V2 Model root for the exact
        // same compiler/build path used by standalone Firmament parts.
        var templateName = definitionIdentity[..definitionIdentity.IndexOf('<')];
        var inspectionDiagnostics = new List<string>();
        var targetKind = FirmamentV2TemplateExpansion.Inspect(definitionSource, inspectionDiagnostics)
            .SingleOrDefault(template => string.Equals(template.Name, templateName, StringComparison.Ordinal))?.TargetKind
            ?? "Struct";
        var application = $"{targetKind} __AssemblyPart = {definitionIdentity}" + Environment.NewLine;
        var module = targetKind == "Model"
            ? definitionSource + Environment.NewLine + application
            : "Model __AssemblyDefinition {" + Environment.NewLine
              + "Units: mm" + Environment.NewLine
              + definitionSource + Environment.NewLine
              + application
              + "}" + Environment.NewLine;
        var build = FirmamentBuildAndExport.CompileSource(module, Path.GetDirectoryName(Path.GetFullPath(sourceIdentity)));
        if (!build.IsSuccess || build.Value is null)
        {
            foreach (var diagnostic in build.Diagnostics)
                diagnostics.Add(new("assembly-definition-materialization-failed", $"Definition '{definitionIdentity}' failed ordinary Firmament materialization: {diagnostic.Message}"));
            return null;
        }
        var import = Step242Importer.ImportBody(build.Value.StepText);
        if (!import.IsSuccess || import.Value is null)
        {
            foreach (var diagnostic in import.Diagnostics)
                diagnostics.Add(new("assembly-definition-reimport-failed", $"Definition '{definitionIdentity}' AP242 reimport failed: {diagnostic.Message}"));
            return null;
        }
        var specialization = build.Value.ConceptIr?.TemplateInstantiations?.LastOrDefault()?.SpecializationIdentity
            ?? "ordinary:" + definitionIdentity;
        var provenance = new List<SemanticProvenance>
        {
            new("Firmament-definition", definitionIdentity, "ordinary Firmament build and canonical AP242 reimport", SemanticSourceSpan.Generated(sourceIdentity)),
            new("template-specialization", specialization, definitionIdentity, SemanticSourceSpan.Generated(sourceIdentity))
        };
        if (build.Value.ConceptIr?.TemplateInstantiations?.LastOrDefault()?.RecordArguments is { Count: > 0 } records)
            provenance.AddRange(records.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair =>
                new SemanticProvenance("static-record", pair.Value.StaticValue, $"{pair.Key}:{pair.Value.RecordType};{pair.Value.Provenance}", SemanticSourceSpan.Generated(sourceIdentity))));
        var semantics = (build.Value.ConceptIr is null ? Array.Empty<SemanticValue>() : SemanticValues(build.Value.ConceptIr, provenance, sourceIdentity))
            .Concat(WindingSemantics(build.Value.WireForm, provenance, sourceIdentity)).ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(build.Value.StepText)));
        var stableId = "assembly-definition:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(definitionIdentity)))[..16];
        var artifact = new AssemblyDefinitionArtifactIr(stableId, definitionIdentity, specialization, hash, Metrics(import.Value), provenance);
        return new(definitionIdentity, specialization, import.Value, semantics, artifact);
    }

    private static IEnumerable<SemanticValue> WindingSemantics(FirmamentWireFormReport? wire,
        IReadOnlyList<SemanticProvenance> provenance, string sourceIdentity)
    {
        foreach (var operation in wire?.Operations ?? [])
        {
            if (operation.AxisDatum is not { } datum) continue;
            var id = operation.StableId + ":winding-datum";
            var o = datum.Origin; var x = datum.Radial; var z = datum.Direction;
            var y = new Vector3D(z[0], z[1], z[2]).Cross(new(x[0], x[1], x[2]));
            var span = SemanticSourceSpan.Generated(sourceIdentity);
            SemanticValue[] members = [
                new(id + ":axis", new("Axis"), [new AxisCapability()],
                    [new ExactAxisBinding(o[0], o[1], o[2], z[0], z[1], z[2], id + ":axis")], exposedName: "Axis"),
                new(id + ":frame", new("DatumFrame"), [new DatumFrameCapability()],
                    [new ExactDatumFrameBinding(o[0], o[1], o[2], x[0], x[1], x[2], y.X, y.Y, y.Z, z[0], z[1], z[2], id + ":frame")], exposedName: "Frame"),
                new(id + ":clear-diameter", new("Length"), [new DimensionalCapability()],
                    [new TolerancedDimensionBinding(datum.ClearDiameterMm, 0, 0, "mm", id + ":clear-diameter")], exposedName: "ClearDiameter")
            ];
            yield return new(id, new("Semantic"), [new AxisCapability(), new DatumFrameCapability(), new DimensionalCapability()],
                exposedMembers: members, provenance: provenance, authoredSourceSpan: span, exposedName: operation.Name);
        }
    }

    private static IReadOnlyList<SemanticValue> SemanticValues(ConceptIrDocument ir, IReadOnlyList<SemanticProvenance> provenance, string sourceIdentity)
    {
        var members = ir.MaterializedStruct.ExposedMembers
            .Where(member => member.Value is ConceptIrAxisValue or ConceptIrPlaneValue or ConceptIrPoint3Value or ConceptIrLengthValue)
            .OrderBy(member => member.Name, StringComparer.Ordinal)
            .Select(member => FromMember(member, provenance, sourceIdentity)).ToArray();
        if (members.Length == 0) return [];
        return [new SemanticValue(
            "template-semantic:" + ir.MaterializedStruct.Name,
            new("Semantic"),
            members.SelectMany(member => member.Capabilities.Values).DistinctBy(capability => capability.GetType()),
            exposedMembers: members,
            provenance: provenance,
            authoredSourceSpan: SemanticSourceSpan.Generated(sourceIdentity),
            exposedName: "Interface")];
    }

    private static SemanticValue FromMember(ConceptIrSemanticMember member, IReadOnlyList<SemanticProvenance> provenance, string sourceIdentity)
    {
        var span = new SemanticSourceSpan(sourceIdentity, member.SourceSpan.Start, member.SourceSpan.Length);
        return member.Value switch
        {
            ConceptIrAxisValue axis => new(member.StableId, new("Axis"), [new AxisCapability()],
                [new ExactAxisBinding(axis.Origin.X, axis.Origin.Y, axis.Origin.Z, axis.Direction.X, axis.Direction.Y, axis.Direction.Z, member.StableId)],
                provenance: provenance, authoredSourceSpan: span, exposedName: member.Name),
            ConceptIrPlaneValue plane => new(member.StableId, new("Plane"), [new PlaneCapability()],
                [new ExactPlaneBinding(plane.Origin.X, plane.Origin.Y, plane.Origin.Z, plane.Normal.X, plane.Normal.Y, plane.Normal.Z, member.StableId)],
                provenance: provenance, authoredSourceSpan: span, exposedName: member.Name),
            ConceptIrPoint3Value point => new(member.StableId, new("Point"), [new PointCapability()],
                [new ExactPointBinding(point.Point.X, point.Point.Y, point.Point.Z, member.StableId)],
                provenance: provenance, authoredSourceSpan: span, exposedName: member.Name),
            ConceptIrLengthValue length => new(member.StableId, new("Length"), [new DimensionalCapability()],
                [new TolerancedDimensionBinding(length.Value, 0, 0, length.Unit, member.StableId)],
                provenance: provenance, authoredSourceSpan: span, exposedName: member.Name),
            _ => throw new InvalidOperationException($"Unsupported assembly semantic member '{member.Name}'.")
        };
    }

    internal static AssemblyGeometryMetricsIr Metrics(BrepBody body)
    {
        var points = body.Topology.Vertices.Select(vertex => body.TryGetVertexPoint(vertex.Id, out var point) ? point : (Point3D?)null).Where(point => point.HasValue).Select(point => point!.Value).ToArray();
        var minimum = points.Length == 0 ? [0d, 0d, 0d] : new[] { points.Min(point => point.X), points.Min(point => point.Y), points.Min(point => point.Z) };
        var maximum = points.Length == 0 ? [0d, 0d, 0d] : new[] { points.Max(point => point.X), points.Max(point => point.Y), points.Max(point => point.Z) };
        return new(body.Topology.Bodies.Count(), body.Topology.Faces.Count(), body.Topology.Edges.Count(), body.Topology.Vertices.Count(), minimum, maximum);
    }
}

public sealed class AssemblyM1Pipeline
{
    public AssemblyM1CompilationResult CompileFile(string path, AssemblyPartMaterializer? domainMaterializer = null)
    {
        var parsed = new AssemblyM0Parser().ParseFile(path);
        return CompileParsed(parsed, domainMaterializer);
    }

    public AssemblyM1CompilationResult Compile(string source, string sourceIdentity = "<memory>", AssemblyPartMaterializer? domainMaterializer = null)
    {
        var parsed = new AssemblyM0Parser().Parse(source, sourceIdentity);
        return CompileParsed(parsed, domainMaterializer);
    }

    public AssemblyM1CompilationResult CompileProject(FirmamentProjectSnapshot project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var profile = project.TryResolve(project.RootDocument, out var root)
            ? FirmamentAssemblyDocumentCompiler.ValidateProfile(root, FirmamentDocumentProfile.Assembly)
            : [];
        if (profile.Any(diagnostic => diagnostic.Severity == AssemblyDiagnosticSeverity.Error))
            return new(null, null, profile);
        var parsed = new AssemblyM0Parser().ParseProject(project);
        if (parsed.Source?.DefinitionSource is { } declarations &&
            System.Text.RegularExpressions.Regex.IsMatch(declarations, @"\bInlineStep\b", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            return new(null, null, [new AssemblyDiagnostic("assembly-profile-external-resource-unsupported",
                "InlineStep requires an external STEP asset and is unsupported in a Firmament project snapshot.")]);
        return CompileParsed(parsed, null, project);
    }

    private static AssemblyM1CompilationResult CompileParsed(AssemblyM0Parser.ParseResult parsed, AssemblyPartMaterializer? domainMaterializer, FirmamentProjectSnapshot? project = null)
    {
        if (!parsed.IsSuccess || parsed.Source is null) return new(null, null, parsed.Diagnostics);
        var diagnostics = parsed.Diagnostics.ToList();
        var materializationWatch = Stopwatch.StartNew();
        var definitions = parsed.Source.Root.Flatten().Where(member => member.Kind == AssemblyInstanceKind.Part)
            .Select(member => member.DefinitionIdentity).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(identity => Materialize(identity, parsed.Source.DefinitionSource, parsed.Source.SourceIdentity, diagnostics, domainMaterializer, project))
            .Where(definition => definition is not null).Cast<MaterializedAssemblyDefinition>().ToDictionary(definition => definition.DefinitionIdentity, StringComparer.Ordinal);
        materializationWatch.Stop();
        var enriched = parsed.Source with { Root = Enrich(parsed.Source.Root, definitions) };
        var compiled = new AssemblyM0Compiler().Compile(enriched, parsed.ElapsedMilliseconds);
        diagnostics.AddRange(compiled.Diagnostics);
        if (compiled.Ir is null) return new(null, null, diagnostics, compiled.Performance);
        var geometryWatch = Stopwatch.StartNew();
        var geometry = Execute(compiled.Ir, definitions, diagnostics, out var validatedIr);
        geometryWatch.Stop();
        var performance = compiled.Performance is null ? null : compiled.Performance with
        {
            DefinitionMaterializationMilliseconds = materializationWatch.Elapsed.TotalMilliseconds,
            GeometryExecutionMilliseconds = geometryWatch.Elapsed.TotalMilliseconds
        };
        return new(validatedIr, geometry, diagnostics, performance);
    }

    private static MaterializedAssemblyDefinition? Materialize(string identity, string? declarations, string sourceIdentity,
        List<AssemblyDiagnostic> diagnostics, AssemblyPartMaterializer? domainMaterializer, FirmamentProjectSnapshot? project)
    {
        if (domainMaterializer?.Invoke(identity, declarations, sourceIdentity, diagnostics) is { } supplied)
        {
            var step = Step242Exporter.ExportBody(supplied.Body);
            var hash = step.IsSuccess
                ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(step.Value)))
                : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity + supplied.SpecializationIdentity)));
            var stableId = "assembly-definition:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..16];
            var artifact = new AssemblyDefinitionArtifactIr(stableId, identity, supplied.SpecializationIdentity, hash,
                AssemblyDefinitionMaterializer.Metrics(supplied.Body), supplied.Provenance);
            return new(identity, supplied.SpecializationIdentity, supplied.Body, supplied.Semantics ?? [], artifact);
        }
        return AssemblyDefinitionMaterializer.TryMaterialize(identity, declarations, sourceIdentity, diagnostics, project);
    }

    private static AssemblyMemberSource Enrich(AssemblyMemberSource member, IReadOnlyDictionary<string, MaterializedAssemblyDefinition> definitions)
    {
        var generated = definitions.TryGetValue(member.DefinitionIdentity, out var definition) ? definition.Semantics : [];
        var provenance = definitions.TryGetValue(member.DefinitionIdentity, out definition)
            ? [.. member.Provenance ?? [], .. definition.Artifact.Provenance]
            : member.Provenance;
        return member with
        {
            Children = member.Children.Select(child => Enrich(child, definitions)).ToArray(),
            ExposedSemantics = [.. member.ExposedSemantics, .. generated],
            Provenance = provenance
        };
    }

    private static AssemblyExecutedGeometry? Execute(AssemblyIr ir, IReadOnlyDictionary<string, MaterializedAssemblyDefinition> definitions, List<AssemblyDiagnostic> diagnostics, out AssemblyIr validatedIr)
    {
        var instances = new Dictionary<string, BrepBody>(StringComparer.Ordinal);
        var instanceArtifacts = new List<AssemblyInstanceGeometryIr>();
        foreach (var instance in ir.Instances.Where(item => item.Kind == AssemblyInstanceKind.Part).OrderBy(item => item.StableId, StringComparer.Ordinal))
        {
            if (!definitions.TryGetValue(instance.DefinitionIdentity, out var definition)) continue;
            if (instance.ResolvedTransform is null)
            {
                diagnostics.Add(new("assembly-geometry-unresolved-transform", $"Part instance '{instance.Path}' has geometry but no resolved transform."));
                continue;
            }
            var transform = Transform3D.FromRowMajor(instance.ResolvedTransform.Matrix);
            var body = FirmasmAssemblyExecutor.TransformBody(definition.Body, transform);
            instances[instance.StableId] = body;
            instanceArtifacts.Add(new(instance.StableId, definition.Artifact.StableId, instance.ResolvedTransform, AssemblyDefinitionMaterializer.Metrics(body)));
        }
        if (definitions.Count == 0 || instanceArtifacts.Count == 0)
        {
            validatedIr = ir;
            diagnostics.Add(new("assembly-geometry-no-materialized-definitions", "No Template-specialized part definitions were available for executable Assembly geometry."));
            return null;
        }
        var residuals = AssemblyWorldQuery.ValidateResiduals(ir, instances.Keys.ToHashSet(StringComparer.Ordinal));
        var byConstraint = residuals.ToDictionary(residual => residual.ConstraintStableId, StringComparer.Ordinal);
        var constraints = ir.PlacementConstraints.Select(constraint => byConstraint.TryGetValue(constraint.StableId, out var residual)
            ? constraint with { Residual = Math.Max(residual.PositionResidualMm, residual.AngularResidualRadians), Status = residual.Passed ? "geometry-validated" : "geometry-residual-failed" }
            : constraint).ToArray();
        foreach (var residual in residuals.Where(item => !item.Passed))
            diagnostics.Add(new("assembly-mate-geometry-residual", $"Constraint '{residual.ConstraintStableId}' residual position={residual.PositionResidualMm:G6}mm angle={residual.AngularResidualRadians:G6}rad."));
        ValidateSolidInterference(ir, instances, diagnostics);
        validatedIr = ir with { Schema = "aetheris/assembly-ir/m1", PlacementConstraints = constraints, Diagnostics = diagnostics };
        var definitionsIr = definitions.Values.Select(definition => definition.Artifact).OrderBy(definition => definition.StableId, StringComparer.Ordinal).ToArray();
        var canonical = JsonSerializer.Serialize(new { definitions = definitionsIr, instances = instanceArtifacts, residuals });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        var artifact = new AssemblyGeometryArtifactIr("aetheris/assembly-geometry/m1", definitionsIr, instanceArtifacts, residuals, hash);
        return new(artifact, definitions.ToDictionary(pair => pair.Key, pair => pair.Value.Body, StringComparer.Ordinal), instances);
    }

    private static void ValidateSolidInterference(AssemblyIr ir, IReadOnlyDictionary<string, BrepBody> instances, List<AssemblyDiagnostic> diagnostics)
    {
        var ordered = instances.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
        var paths = ir.Instances.ToDictionary(instance => instance.StableId, instance => instance.Path.ToString(), StringComparer.Ordinal);
        for (var left = 0; left < ordered.Length - 1; left++)
        for (var right = left + 1; right < ordered.Length; right++)
        {
            var result = BrepSolidInterference.Analyze(ordered[left].Value, ordered[right].Value);
            if (result.Status != BrepSolidInterferenceStatus.Interfering) continue;
            diagnostics.Add(new(
                "assembly-solid-volume-interference",
                $"Part occurrences '{paths[ordered[left].Key]}' and '{paths[ordered[right].Key]}' occupy overlapping solid volume. "
                + $"The assembly is physically invalid and cannot be materialized (proof={result.Evidence}; penetration witness={result.PenetrationWitnessMm:G6}mm; "
                + $"contained tetrahedron={result.WitnessTetrahedronVolumeMm3:G6}mm^3). Face/edge contact is allowed, but positive-volume overlap is not."));
        }
    }
}

public static class AssemblyWorldQuery
{
    public static SemanticBinding Resolve(AssemblyIr ir, string semanticValueId)
    {
        var instance = ir.Instances.FirstOrDefault(candidate => Flatten(candidate.SemanticRoot).Any(value => value.StableIdentity == semanticValueId))
            ?? throw new KeyNotFoundException($"Semantic value '{semanticValueId}' has no owning assembly instance.");
        if (instance.ResolvedTransform is null) throw new InvalidOperationException($"Instance '{instance.Path}' has no resolved world transform.");
        var value = Flatten(instance.SemanticRoot).Single(candidate => candidate.StableIdentity == semanticValueId);
        var transform = Transform3D.FromRowMajor(instance.ResolvedTransform.Matrix);
        if (value.TryBinding<ExactAxisBinding>(out var axis))
        {
            var origin = transform.Apply(new Point3D(axis.OriginX, axis.OriginY, axis.OriginZ));
            var direction = transform.Apply(new Vector3D(axis.DirectionX, axis.DirectionY, axis.DirectionZ));
            return new ExactAxisBinding(origin.X, origin.Y, origin.Z, direction.X, direction.Y, direction.Z, axis.AxisStableId + ":world:" + instance.StableId);
        }
        if (value.TryBinding<ExactPlaneBinding>(out var plane))
        {
            var origin = transform.Apply(new Point3D(plane.OriginX, plane.OriginY, plane.OriginZ));
            var normal = transform.Apply(new Vector3D(plane.NormalX, plane.NormalY, plane.NormalZ));
            return new ExactPlaneBinding(origin.X, origin.Y, origin.Z, normal.X, normal.Y, normal.Z, plane.PlaneStableId + ":world:" + instance.StableId);
        }
        if (value.TryBinding<ExactDatumFrameBinding>(out var frame))
        {
            var origin = transform.Apply(new Point3D(frame.OriginX, frame.OriginY, frame.OriginZ));
            var x = transform.Apply(new Vector3D(frame.XAxisX, frame.XAxisY, frame.XAxisZ));
            var y = transform.Apply(new Vector3D(frame.YAxisX, frame.YAxisY, frame.YAxisZ));
            var z = transform.Apply(new Vector3D(frame.ZAxisX, frame.ZAxisY, frame.ZAxisZ));
            return new ExactDatumFrameBinding(origin.X, origin.Y, origin.Z, x.X, x.Y, x.Z, y.X, y.Y, y.Z, z.X, z.Y, z.Z,
                frame.FrameStableId + ":world:" + instance.StableId);
        }
        if (value.TryBinding<ExactPointBinding>(out var point))
        {
            var world = transform.Apply(new Point3D(point.X, point.Y, point.Z));
            return new ExactPointBinding(world.X, world.Y, world.Z, point.PointStableId + ":world:" + instance.StableId);
        }
        if (value.TryBinding<ExactBrepBodyBinding>(out var body))
            return new ExactBrepBodyBinding(FirmasmAssemblyExecutor.TransformBody(body.Body, transform), body.BodyStableId + ":world:" + instance.StableId);
        throw new InvalidOperationException($"Semantic value '{semanticValueId}' has no world-queryable exact binding.");
    }

    public static IReadOnlyList<AssemblyMateResidualIr> ValidateResiduals(AssemblyIr ir, IReadOnlySet<string> materializedInstanceIds, double positionToleranceMm = 1e-8, double angularToleranceRadians = 1e-8)
    {
        var result = new List<AssemblyMateResidualIr>();
        foreach (var constraint in ir.PlacementConstraints.OrderBy(item => item.StableId, StringComparer.Ordinal))
        {
            var owners = ir.Instances.Where(instance => Flatten(instance.SemanticRoot).Any(value => value.StableIdentity == constraint.FirstSemanticValueId || value.StableIdentity == constraint.SecondSemanticValueId)).ToArray();
            if (owners.Length != 2 || owners.Any(owner => !materializedInstanceIds.Contains(owner.StableId))) continue;
            var first = Resolve(ir, constraint.FirstSemanticValueId);
            var second = Resolve(ir, constraint.SecondSemanticValueId);
            var (position, angle) = Residual(constraint.Kind, first, second, constraint.OffsetMm, constraint.Orientation);
            result.Add(new(constraint.StableId, constraint.Kind, position, angle, position <= positionToleranceMm && angle <= angularToleranceRadians, "world exact semantic binding over materialized BRep instance"));
        }
        return result;
    }

    private static (double Position, double Angle) Residual(PlacementConstraintKind kind, SemanticBinding first, SemanticBinding second, double offset, DatumOrientationRelation orientation)
    {
        if (first is ExactAxisBinding a && second is ExactAxisBinding b)
        {
            var ad = Unit(new(a.DirectionX, a.DirectionY, a.DirectionZ)); var bd = Unit(new(b.DirectionX, b.DirectionY, b.DirectionZ));
            var delta = new Vector3D(a.OriginX - b.OriginX, a.OriginY - b.OriginY, a.OriginZ - b.OriginZ);
            var cross = Cross(delta, bd);
            return (cross.Length, UndirectedAngle(ad, bd));
        }
        if (first is ExactPlaneBinding p && second is ExactPlaneBinding q)
        {
            var pn = Unit(new(p.NormalX, p.NormalY, p.NormalZ)); var qn = Unit(new(q.NormalX, q.NormalY, q.NormalZ));
            var delta = new Vector3D(p.OriginX - q.OriginX, p.OriginY - q.OriginY, p.OriginZ - q.OriginZ);
            return (Math.Abs(delta.Dot(qn) - offset), UndirectedAngle(pn, qn));
        }
        if (first is ExactPointBinding x && second is ExactPointBinding y)
        {
            var delta = new Vector3D(x.X - y.X, x.Y - y.Y, x.Z - y.Z);
            return (Math.Abs(delta.Length - offset), 0);
        }
        if (first is ExactDatumFrameBinding f && second is ExactDatumFrameBinding g)
        {
            var delta = new Vector3D(f.OriginX - g.OriginX, f.OriginY - g.OriginY, f.OriginZ - g.OriginZ);
            var fx = Unit(new(f.XAxisX, f.XAxisY, f.XAxisZ)); var gx = Unit(new(g.XAxisX, g.XAxisY, g.XAxisZ));
            var fz = Unit(new(f.ZAxisX, f.ZAxisY, f.ZAxisZ)); var gz = Unit(new(g.ZAxisX, g.ZAxisY, g.ZAxisZ));
            var expectedZDot = orientation == DatumOrientationRelation.OpposedDirection ? -1d : 1d;
            return (delta.Length, Math.Max(DirectedAngle(fx, gx), DirectedAngle(fz, gz * expectedZDot)));
        }
        return (double.PositiveInfinity, double.PositiveInfinity);
    }

    // atan2 retains precision near aligned axes; acos(dot) turns rounding at 1 into
    // a spurious ~1e-8 rad residual even for identical oblique datum frames.
    private static double DirectedAngle(Vector3D a, Vector3D b) => double.Atan2(Cross(a, b).Length, a.Dot(b));
    private static double UndirectedAngle(Vector3D a, Vector3D b) => double.Atan2(Cross(a, b).Length, double.Abs(a.Dot(b)));
    private static Vector3D Unit(Vector3D value) => value / value.Length;
    private static Vector3D Cross(Vector3D a, Vector3D b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    private static IEnumerable<SemanticValue> Flatten(SemanticValue root)
    {
        yield return root;
        foreach (var child in root.ExposedMembers.Values.SelectMany(Flatten)) yield return child;
    }
}

internal static class AssemblyMemberSourceExtensions
{
    public static IEnumerable<AssemblyMemberSource> Flatten(this AssemblyMemberSource root)
    {
        yield return root;
        foreach (var child in root.Children.SelectMany(Flatten)) yield return child;
    }
}
