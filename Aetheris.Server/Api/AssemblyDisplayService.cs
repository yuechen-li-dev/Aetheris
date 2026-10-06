using System.Diagnostics;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament;
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
        var source = File.ReadAllText(path);
        if (Aetheris.Kernel.Firmament.Scene.SceneAuthoring.HasRoot(source))
        {
            using var session = new Aetheris.Kernel.Firmament.Scene.FirmamentSceneSession();
            var scene = session.CompileFile(path);
            if (!scene.IsSuccess || scene.Scene is null) { error = string.Join(Environment.NewLine, scene.Diagnostics.Select(d => d.Code+": "+d.Message)); return false; }
            packet = FromDisplay(DisplayProjection.Project(scene.Scene));
            return true;
        }
        if (!path.EndsWith(".firmasm", StringComparison.OrdinalIgnoreCase) &&
            !System.Text.RegularExpressions.Regex.IsMatch(source, @"(?m)^\s*Assembly\s+"))
        {
            var part = FirmamentBuildAndExport.CompileSource(source, Path.GetDirectoryName(Path.GetFullPath(path)));
            if (!part.IsSuccess) { error = string.Join(Environment.NewLine, part.Diagnostics.Select(d => d.Message)); return false; }
            var body = part.Value.RuntimeBody;
            if (body is null)
            {
                var imported = Step242Importer.ImportBody(part.Value.StepText);
                body = imported.IsSuccess ? imported.Value : null;
            }
            if (body is null) { error = "display-part-brep-unavailable"; return false; }
            var id = part.Value.Cir?.DefinitionId ?? "part-definition:"+part.Value.ExportedFeatureId;
            var definition = AssemblyDisplayMeshExporter.PrepareDefinition(id, part.Value.ExportedFeatureId, body) with { Cir = part.Value.Cir, Shader = CirShaderArtifactProvider.Resolve(part.Value.Cir), GeometryRevision = part.Value.Cir?.DefinitionId };
            packet = FromDisplay(new("aetheris/display-mesh/1", Path.GetFileNameWithoutExtension(path), "mm", [definition],
                [new("part", Path.GetFileNameWithoutExtension(path), null, id, Transform3D.Identity.ToRowMajor(), part.Value.ExportedFeatureId)]));
            return true;
        }
        var watch = Stopwatch.StartNew();
        var compilation = string.Equals(Path.GetExtension(path), ".firmasm", StringComparison.OrdinalIgnoreCase)
            ? new FirmamentAssemblyDocumentCompiler().CompileFile(path).Compilation
            : new AssemblyM1Pipeline().CompileFile(path);
        if (!compilation.IsSuccess || compilation.Ir is null || compilation.Geometry is null)
        {
            error = string.Join(Environment.NewLine, compilation.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
            return false;
        }
        var display = AssemblyDisplayMeshExporter.Export(compilation);
        var definitions = display.Definitions.Select(LegacyDefinition).ToList();
        var diagnostics = display.Definitions.SelectMany(d => d.Diagnostics.Select(w =>
            new DisplayDiagnosticDto(w.Code, w.Message, null, null, "definition-tessellation", d.Id + ":" + w.FaceId))).ToList();
        var artifactByIdentity = compilation.Geometry.Artifact.Definitions.ToDictionary(item => item.DefinitionIdentity, item => item.StableId, StringComparer.Ordinal);
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
            new(minimum, maximum), diagnostics, new Dictionary<string, double> { ["packetMilliseconds"] = watch.Elapsed.TotalMilliseconds, ["definitionCount"] = definitions.Count, ["moduleDefinitionCount"] = modules.Length, ["occurrenceCount"] = occurrences.Length }, modules,
            display);
        return true;
    }

    private static AssemblyDisplayPacketDto FromDisplay(AssemblyDisplayMeshDocument display)
    {
        var vertices = display.Definitions.ToDictionary(d => d.Id, d => d.Positions, StringComparer.Ordinal);
        var points = display.Occurrences.Where(o => o.DefinitionId is not null).SelectMany(o =>
        {
            var p = vertices[o.DefinitionId!]; var t = Transform3D.FromRowMajor(o.Transform);
            return Enumerable.Range(0, p.Length/3).Select(i => t.Apply(new Point3D(p[i*3], p[i*3+1], p[i*3+2])));
        });
        var boundsPoints = display.MinimumMm is null || display.MaximumMm is null ? points.ToArray() : [];
        var minimum = display.MinimumMm ?? (boundsPoints.Length == 0 ? [0d,0d,0d] : new[] { boundsPoints.Min(p=>p.X), boundsPoints.Min(p=>p.Y), boundsPoints.Min(p=>p.Z) });
        var maximum = display.MaximumMm ?? (boundsPoints.Length == 0 ? [0d,0d,0d] : new[] { boundsPoints.Max(p=>p.X), boundsPoints.Max(p=>p.Y), boundsPoints.Max(p=>p.Z) });
        var occurrences = display.Occurrences.Select(o => new AssemblyDisplayOccurrenceDto(o.Id, o.Path.Split('.').Last(), o.Path, o.ParentId,
            o.DefinitionId, o.Kind ?? "Part", o.Transform, "CompiledDisplay")).ToArray();
        return new("aetheris/cadmata-assembly-display/m3", display.Name, occurrences.Single(o=>o.ParentStableId is null).StableId,
            display.Definitions.Select(LegacyDefinition).ToArray(), occurrences, [], [], new(minimum,maximum), [], new Dictionary<string,double>(), Display: display);
    }

    // Compatibility transport only: geometry and face ranges come from the shared projector.
    private static AssemblyDisplayDefinitionDto LegacyDefinition(AssemblyDisplayMeshDefinition definition)
    {
        var ranges = definition.Ranges ?? [new(0, definition.Indices.Length/3, "face:0", definition.Id)];
        var faces = ranges.Select(range =>
        {
            var indices = definition.Indices.Skip(range.StartTriangle*3).Take(range.TriangleCount*3).ToArray();
            var vertices = indices.Distinct().Order().ToArray();
            var local = vertices.Select((id,index)=>(id,index)).ToDictionary(v=>v.id,v=>v.index);
            var positions = vertices.Select(i=>new Point3Dto(definition.Positions[i*3],definition.Positions[i*3+1],definition.Positions[i*3+2])).ToArray();
            var normals = vertices.Select(i=>new Vector3Dto(definition.Normals[i*3],definition.Normals[i*3+1],definition.Normals[i*3+2])).ToArray();
            return new FacePatchDto(int.Parse(range.FaceId.AsSpan("face:".Length), System.Globalization.CultureInfo.InvariantCulture),
                positions,normals,indices.Select(i=>local[i]).ToArray(),definition.MeshPipeline,null);
        }).ToArray();
        return new(definition.Id,definition.Identity,faces);
    }
}
