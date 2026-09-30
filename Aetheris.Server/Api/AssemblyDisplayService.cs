using System.Diagnostics;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Server.Contracts;

namespace Aetheris.Server.Api;

public static class AssemblyDisplayService
{
    public static bool TryBuildStep(Step242ProductStructure structure, out AssemblyDisplayPacketDto? packet, out string error)
    {
        packet = null; error = string.Empty;
        var watch = Stopwatch.StartNew();
        var definitions = new List<AssemblyDisplayDefinitionDto>();
        foreach (var definition in structure.Definitions.Where(item => item.Geometry is not null).OrderBy(item => item.StableId, StringComparer.Ordinal))
        {
            var mesh = BrepDisplayTessellator.TessellateBounded(definition.Geometry!);
            if (!mesh.IsSuccess)
            {
                error = $"STEP definition '{definition.Name}' (#{definition.ProductDefinitionEntityId}) could not be prepared for display: "
                    + string.Join("; ", mesh.Diagnostics.Select(item => item.Message));
                return false;
            }
            definitions.Add(new(definition.StableId, definition.Name, ApiMappings.ToTessellationResponse(mesh.Value).FacePatches));
        }
        var byDefinition = structure.Definitions.ToDictionary(item => item.StableId, StringComparer.Ordinal);
        var byOccurrence = structure.Occurrences.ToDictionary(item => item.StableId, StringComparer.Ordinal);
        var rootId = "step-root:" + structure.RootDefinitionStableId;
        var world = new Dictionary<string, Transform3D>(StringComparer.Ordinal);
        Transform3D World(Step242ImportedProductOccurrence occurrence)
        {
            if (world.TryGetValue(occurrence.StableId, out var cached)) return cached;
            var local = Transform3D.FromRowMajor(occurrence.LocalTransform);
            return world[occurrence.StableId] = occurrence.ParentStableId is null ? local : local * World(byOccurrence[occurrence.ParentStableId]);
        }
        var occurrences = new List<AssemblyDisplayOccurrenceDto>
        {
            new(rootId, byDefinition.GetValueOrDefault(structure.RootDefinitionStableId)?.Name ?? "ImportedAssembly", rootId,
                null, null, "Assembly", Transform3D.Identity.ToRowMajor(), "ImportedOccurrence")
        };
        var points = new List<Point3D>();
        foreach (var occurrence in structure.Occurrences)
        {
            if (!byDefinition.TryGetValue(occurrence.DefinitionStableId, out var definition))
            {
                error = $"STEP occurrence '{occurrence.Name}' refers to missing definition '{occurrence.DefinitionStableId}'.";
                return false;
            }
            var transform = World(occurrence);
            occurrences.Add(new(occurrence.StableId, occurrence.Name, occurrence.StableId,
                occurrence.ParentStableId ?? rootId, occurrence.DefinitionStableId,
                definition.Geometry is null ? "Assembly" : "Part", transform.ToRowMajor(), "ImportedOccurrence"));
            if (definition.Geometry is null) continue;
            foreach (var vertex in definition.Geometry.Topology.Vertices)
                if (definition.Geometry.TryGetVertexPoint(vertex.Id, out var point)) points.Add(transform.Apply(point));
        }
        var minimum = points.Count == 0 ? new[] { 0d, 0d, 0d } : new[] { points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z) };
        var maximum = points.Count == 0 ? new[] { 0d, 0d, 0d } : new[] { points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z) };
        watch.Stop();
        var performance = new Dictionary<string, double>(structure.Performance ?? new Dictionary<string, double>(), StringComparer.Ordinal)
        {
            ["displayPreparationMilliseconds"] = watch.Elapsed.TotalMilliseconds,
            ["definitionCount"] = definitions.Count,
            ["occurrenceCount"] = occurrences.Count - 1
        };
        packet = new("aetheris/cadmata-assembly-display/m3", occurrences[0].Name, rootId, definitions, occurrences,
            [], [], new(minimum, maximum), [], performance);
        return true;
    }

    public static bool TryBuild(string path, out AssemblyDisplayPacketDto? packet, out string error)
    {
        packet = null; error = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { error = $"Assembly source '{path}' was not found."; return false; }
        var watch = Stopwatch.StartNew();
        var compilation = string.Equals(Path.GetExtension(path), ".firmasm", StringComparison.OrdinalIgnoreCase)
            ? new FirmamentAssemblyDocumentCompiler().CompileFile(path).Compilation
            : new AssemblyM1Pipeline().CompileFile(path);
        if (!compilation.IsSuccess || compilation.Ir is null || compilation.Geometry is null)
        {
            error = string.Join(Environment.NewLine, compilation.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
            return false;
        }
        var definitions = new List<AssemblyDisplayDefinitionDto>();
        var diagnostics = new List<DisplayDiagnosticDto>();
        var artifactByIdentity = compilation.Geometry.Artifact.Definitions.ToDictionary(item => item.DefinitionIdentity, item => item.StableId, StringComparer.Ordinal);
        foreach (var definition in compilation.Geometry.DefinitionBodies.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var tessellation = BrepDisplayTessellator.TessellateBounded(definition.Value);
            if (!tessellation.IsSuccess)
            {
                diagnostics.Add(new("Viewer.Assembly.MissingDefinitionGeometry", $"Definition '{definition.Key}' could not be tessellated.", null, null, "definition-tessellation", "Inspect the definition diagnostic."));
                continue;
            }
            definitions.Add(new(artifactByIdentity[definition.Key], definition.Key, ApiMappings.ToTessellationResponse(tessellation.Value).FacePatches));
        }
        var occurrences = compilation.Ir.Instances.OrderBy(item => item.Path.Segments.Count).ThenBy(item => item.Path.ToString(), StringComparer.Ordinal).Select(instance => new AssemblyDisplayOccurrenceDto(
            instance.StableId, instance.Path.Segments[^1], instance.Path.ToString(), instance.ParentStableId,
            instance.Kind == AssemblyInstanceKind.Part ? artifactByIdentity.GetValueOrDefault(instance.DefinitionIdentity)
                : (compilation.Ir.AssemblyDefinitions ?? []).SingleOrDefault(item => item.DefinitionIdentity == instance.DefinitionIdentity)?.StableId,
            instance.Kind.ToString(), (instance.ResolvedTransform ?? AssemblyTransform.Identity).Matrix, instance.PlacementAuthority.ToString(),
            compilation.Ir.Instances.Where(candidate => candidate.Path.Segments.Count >= instance.Path.Segments.Count
                && candidate.Path.Segments.Take(instance.Path.Segments.Count).SequenceEqual(instance.Path.Segments)).Select(candidate => candidate.StableId).ToArray())).ToArray();
        var mates = compilation.Ir.Mates.Select(mate => new AssemblyDisplayMateDto(mate.StableId, mate.Name, mate.InterfaceStableId,
            mate.Roles.Select(role => $"{role.Role}: {role.ParticipantPath}").ToArray(), mate.ConstraintIds, mate.ValidationStatus)).ToArray();
        var tolerances = compilation.Ir.ToleranceStackups.Select(stack => new AssemblyDisplayToleranceDto(stack.Name, stack.Passed, stack.Nominal, stack.WorstCaseMinimum, stack.WorstCaseMaximum, stack.Unit,
            stack.Contributions.Select(contribution => $"{contribution.OriginInstancePath}: {contribution.RelationStableId}").ToArray(),
            stack.Contributions.SelectMany(contribution => contribution.ExpandedContributors ?? []).Select(item => $"{item.Provenance}: {item.Nominal:G6} {item.Unit} [{item.Nominal + item.LowerTolerance:G6}, {item.Nominal + item.UpperTolerance:G6}]").ToArray())).ToArray();
        var modules = (compilation.Ir.AssemblyDefinitions ?? []).Select(definition => new AssemblyDisplayModuleDefinitionDto(
            definition.StableId, definition.DefinitionIdentity, definition.TemplateName, definition.SpecializationIdentity,
            definition.Provenance.Select(item => $"{item.Stage}:{item.Identity}:{item.Evidence}").ToArray(),
            definition.PublicSemantics.Select(semantic => new AssemblyDisplayPublicSemanticDto(semantic.ExposedName ?? semantic.StableIdentity, semantic.Type.Name,
                semantic.Capabilities.Values.Select(item => item.Name).Order(StringComparer.Ordinal).ToArray(), semantic.Bindings.Select(item => item.Kind).Order(StringComparer.Ordinal).ToArray(),
                semantic.Provenance.LastOrDefault(item => item.Stage == "assembly-expose")?.Evidence)).ToArray(), definition.SolveMilliseconds)).ToArray();
        var metrics = compilation.Geometry.Artifact.Instances.Select(item => item.Metrics).ToArray();
        var minimum = metrics.Length == 0 ? new[] { 0d,0d,0d } : new[] { metrics.Min(item => item.Minimum[0]), metrics.Min(item => item.Minimum[1]), metrics.Min(item => item.Minimum[2]) };
        var maximum = metrics.Length == 0 ? new[] { 0d,0d,0d } : new[] { metrics.Max(item => item.Maximum[0]), metrics.Max(item => item.Maximum[1]), metrics.Max(item => item.Maximum[2]) };
        watch.Stop();
        packet = new("aetheris/cadmata-assembly-display/m3", compilation.Ir.Name, compilation.Ir.RootInstanceStableId, definitions, occurrences, mates, tolerances,
            new(minimum, maximum), diagnostics, new Dictionary<string, double> { ["packetMilliseconds"] = watch.Elapsed.TotalMilliseconds, ["definitionCount"] = definitions.Count, ["moduleDefinitionCount"] = modules.Length, ["occurrenceCount"] = occurrences.Length }, modules);
        return true;
    }
}
