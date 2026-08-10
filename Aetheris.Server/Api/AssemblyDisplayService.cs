using System.Diagnostics;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Server.Contracts;

namespace Aetheris.Server.Api;

public static class AssemblyDisplayService
{
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
            instance.Kind == AssemblyInstanceKind.Part ? artifactByIdentity.GetValueOrDefault(instance.DefinitionIdentity) : null,
            instance.Kind.ToString(), (instance.ResolvedTransform ?? AssemblyTransform.Identity).Matrix, instance.PlacementAuthority.ToString())).ToArray();
        var mates = compilation.Ir.Mates.Select(mate => new AssemblyDisplayMateDto(mate.StableId, mate.Name, mate.InterfaceStableId,
            mate.Roles.Select(role => $"{role.Role}: {role.ParticipantPath}").ToArray(), mate.ConstraintIds, mate.ValidationStatus)).ToArray();
        var tolerances = compilation.Ir.ToleranceStackups.Select(stack => new AssemblyDisplayToleranceDto(stack.Name, stack.Passed, stack.Nominal, stack.WorstCaseMinimum, stack.WorstCaseMaximum, stack.Unit,
            stack.Contributions.Select(contribution => $"{contribution.OriginInstancePath}: {contribution.RelationStableId}").ToArray())).ToArray();
        var metrics = compilation.Geometry.Artifact.Instances.Select(item => item.Metrics).ToArray();
        var minimum = metrics.Length == 0 ? new[] { 0d,0d,0d } : new[] { metrics.Min(item => item.Minimum[0]), metrics.Min(item => item.Minimum[1]), metrics.Min(item => item.Minimum[2]) };
        var maximum = metrics.Length == 0 ? new[] { 0d,0d,0d } : new[] { metrics.Max(item => item.Maximum[0]), metrics.Max(item => item.Maximum[1]), metrics.Max(item => item.Maximum[2]) };
        watch.Stop();
        packet = new("aetheris/cadmata-assembly-display/m2", compilation.Ir.Name, compilation.Ir.RootInstanceStableId, definitions, occurrences, mates, tolerances,
            new(minimum, maximum), diagnostics, new Dictionary<string, double> { ["packetMilliseconds"] = watch.Elapsed.TotalMilliseconds, ["definitionCount"] = definitions.Count, ["occurrenceCount"] = occurrences.Length });
        return true;
    }
}
