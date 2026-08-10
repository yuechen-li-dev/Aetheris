using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Firmament.Assembly;

public sealed record FirmasmComponentPackageItem(string DefinitionStableId, string RelativePath, string Sha256);
public sealed record FirmasmImportPackageResult(string FirmasmPath, string ManifestPath, Step242ProductStructure ProductStructure, IReadOnlyList<FirmasmComponentPackageItem> Components, string FirmasmSha256);

public static class Step242FirmasmPackageImporter
{
    public static KernelResult<FirmasmImportPackageResult> Import(string sourceStepPath, string outputDirectory)
    {
        var structure = Step242AssemblyImporter.Import(File.ReadAllText(sourceStepPath));
        if (!structure.IsSuccess) return KernelResult<FirmasmImportPackageResult>.Failure(structure.Diagnostics);
        var output = Path.GetFullPath(outputDirectory); var componentsDirectory = Path.Combine(output, "components");
        Directory.CreateDirectory(componentsDirectory);
        var componentItems = new List<FirmasmComponentPackageItem>();
        var componentByDefinition = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (definition, index) in structure.Value.Definitions.Where(item => item.Geometry is not null).OrderBy(item => item.StableId, StringComparer.Ordinal).Select((item, index) => (item, index)))
        {
            var relative = $"components/{index + 1:D4}-{Identifier(definition.Name).ToLowerInvariant()}.step";
            var export = Step242Exporter.ExportBody(definition.Geometry!, options: new Step242ExportOptions { ProductId = definition.StableId, ProductName = definition.Name });
            if (!export.IsSuccess) return KernelResult<FirmasmImportPackageResult>.Failure(export.Diagnostics);
            var path = Path.Combine(output, relative.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllText(path, export.Value, new UTF8Encoding(false));
            var hash = Sha(export.Value); componentItems.Add(new(definition.StableId, relative, hash)); componentByDefinition[definition.StableId] = relative;
        }
        var source = GenerateSource(structure.Value, componentByDefinition, Path.GetFileName(sourceStepPath));
        var firmasmPath = Path.Combine(output, Identifier(Path.GetFileNameWithoutExtension(sourceStepPath)) + ".firmasm");
        File.WriteAllText(firmasmPath, source, new UTF8Encoding(false));
        var manifestPath = Path.Combine(output, "component-package.json");
        var manifest = new
        {
            schema = "aetheris/firmasm-component-package/m2",
            sourceStep = Path.GetFileName(sourceStepPath),
            firmasm = Path.GetFileName(firmasmPath),
            normalization = structure.Diagnostics.Any(diagnostic => diagnostic.Source == "Importer.Assembly.MultiplicityNormalization") ? "flat-assembly-from-ambiguous-multiplicity" : "explicit-ap242-occurrence-hierarchy",
            definitionCount = componentItems.Count,
            occurrenceCount = structure.Value.Occurrences.Count,
            components = componentItems
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        return KernelResult<FirmasmImportPackageResult>.Success(new(firmasmPath, manifestPath, structure.Value, componentItems, Sha(source)), structure.Diagnostics);
    }

    public static string GenerateSource(Step242ProductStructure structure, IReadOnlyDictionary<string, string> componentByDefinition, string provenance)
    {
        var definitions = structure.Definitions.ToDictionary(item => item.StableId, StringComparer.Ordinal);
        var occurrences = structure.Occurrences.ToDictionary(item => item.StableId, StringComparer.Ordinal);
        var children = structure.Occurrences.GroupBy(item => item.ParentStableId).ToDictionary(group => group.Key ?? string.Empty, group => group.OrderBy(item => item.StableId, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        var rootName = "ImportedAssembly";
        var builder = new StringBuilder();
        builder.AppendLine($"// Imported from {provenance}; occurrence transforms are interchange evidence, not semantic Mates.");
        builder.AppendLine("// Multiplicity without trustworthy foreign hierarchy is intentionally normalized to a flat Assembly.");
        builder.AppendLine($"Assembly {rootName} {{");
        builder.AppendLine($"  <Assembly {rootName}>");
        foreach (var occurrence in children.GetValueOrDefault(string.Empty, [])) Emit(occurrence, 4);
        builder.AppendLine("  </Assembly>");
        builder.AppendLine($"  Anchor: {rootName};");
        builder.AppendLine("}");
        return builder.ToString();

        void Emit(Step242ImportedProductOccurrence occurrence, int indent)
        {
            var padding = new string(' ', indent); var id = Identifier(occurrence.StableId);
            var definition = definitions[occurrence.DefinitionStableId];
            var nested = definition.Geometry is null;
            if (nested)
            {
                builder.AppendLine($"{padding}<Assembly {id}>");
                builder.AppendLine($"{padding}  // STEP occurrence #{occurrence.StepEntityId}; Placement authority: ImportedOccurrence.");
                builder.AppendLine($"{padding}  Placement ImportedOccurrence = [{string.Join(", ", occurrence.LocalTransform.Select(Number))}];");
                foreach (var child in children.GetValueOrDefault(occurrence.StableId, [])) Emit(child, indent + 2);
                builder.AppendLine($"{padding}</Assembly>");
                return;
            }
            builder.AppendLine($"{padding}<Part {id} = ExternalStep<\"{componentByDefinition[occurrence.DefinitionStableId]}\">>");
            builder.AppendLine($"{padding}  // STEP occurrence #{occurrence.StepEntityId}; Placement authority: ImportedOccurrence.");
            builder.AppendLine($"{padding}  Placement ImportedOccurrence = [{string.Join(", ", occurrence.LocalTransform.Select(Number))}];");
            builder.AppendLine($"{padding}</Part>");
        }
    }

    private static string Identifier(string value)
    {
        var result = Regex.Replace(value, "[^A-Za-z0-9_]", "_");
        if (result.Length == 0) result = "Imported";
        return char.IsLetter(result[0]) || result[0] == '_' ? result : "_" + result;
    }
    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string Sha(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public static class AssemblyIrAp242Exporter
{
    public static KernelResult<string> Export(AssemblyM1CompilationResult compilation)
    {
        if (!compilation.IsSuccess || compilation.Ir is null || compilation.Geometry is null)
            return Failure("AssemblyIR must be successfully materialized before AP242 product-structure export.");
        var ir = compilation.Ir;
        var artifactByIdentity = compilation.Geometry.Artifact.Definitions.ToDictionary(item => item.DefinitionIdentity, item => item.StableId, StringComparer.Ordinal);
        var definitions = compilation.Geometry.DefinitionBodies.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new Step242AssemblyDefinition(artifactByIdentity[pair.Key], pair.Key, pair.Value))
            .Concat((ir.AssemblyDefinitions ?? []).Select(definition => new Step242AssemblyDefinition(definition.StableId, definition.DefinitionIdentity, null)))
            .OrderBy(definition => definition.StableId, StringComparer.Ordinal).ToArray();
        var byId = ir.Instances.ToDictionary(item => item.StableId, StringComparer.Ordinal);
        var assemblyDefinitionByIdentity = (ir.AssemblyDefinitions ?? []).ToDictionary(item => item.DefinitionIdentity, item => item.StableId, StringComparer.Ordinal);
        var occurrences = ir.Instances.OrderBy(item => item.Path.Segments.Count).ThenBy(item => item.StableId, StringComparer.Ordinal).Select(instance =>
        {
            var world = instance.ResolvedTransform ?? AssemblyTransform.Identity;
            IReadOnlyList<double> local = world.Matrix;
            if (instance.ParentStableId is not null && byId[instance.ParentStableId].ResolvedTransform is { } parentWorld)
            {
                var parent = Transform3D.FromRowMajor(parentWorld.Matrix);
                local = (Transform3D.FromRowMajor(world.Matrix) * parent.Inverse()).ToRowMajor();
            }
            return new Step242AssemblyOccurrence(instance.StableId, instance.Path.Segments.Last(), instance.ParentStableId,
                instance.Kind == AssemblyInstanceKind.Part ? artifactByIdentity[instance.DefinitionIdentity]
                    : instance.IsEncapsulatedDefinition ? assemblyDefinitionByIdentity[instance.DefinitionIdentity] : null, local);
        }).ToArray();
        return Step242AssemblyExporter.Export(new(ir.Name, ir.RootInstanceStableId, definitions, occurrences));
    }

    private static KernelResult<string> Failure(string message) => KernelResult<string>.Failure([
        new(Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error, message, "Exporter.AssemblyIR")]);
}
