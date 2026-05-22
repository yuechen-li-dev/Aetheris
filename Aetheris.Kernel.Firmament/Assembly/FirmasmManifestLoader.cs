using System.Text.Json;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Firmament.Assembly;

public sealed class FirmasmManifestLoader
{
    private static readonly HashSet<string> AllowedTopLevelSections = new(StringComparer.Ordinal)
    {
        "manifest",
        "assembly",
        "parts",
        "instances",
    };

    public KernelResult<FirmasmLoadedAssembly> LoadFromFile(string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            return Failure<FirmasmLoadedAssembly>("Manifest path must be provided.");
        }

        if (!File.Exists(manifestPath))
        {
            return Failure<FirmasmLoadedAssembly>($"Assembly manifest '{manifestPath}' was not found.");
        }

        if (!string.Equals(Path.GetExtension(manifestPath), ".firmasm", StringComparison.OrdinalIgnoreCase))
        {
            return Failure<FirmasmLoadedAssembly>($"Assembly manifest '{manifestPath}' must use the '.firmasm' extension.");
        }

        string sourceText;
        try
        {
            sourceText = File.ReadAllText(manifestPath);
        }
        catch (Exception ex)
        {
            return Failure<FirmasmLoadedAssembly>($"Assembly manifest '{manifestPath}' could not be read: {ex.Message}");
        }

        var parseResult = Parse(sourceText);
        if (!parseResult.IsSuccess)
        {
            return KernelResult<FirmasmLoadedAssembly>.Failure(parseResult.Diagnostics);
        }

        var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath)) ?? Directory.GetCurrentDirectory();
        var loadPartsResult = LoadPartSources(parseResult.Value.Parts, sourceDirectory);
        if (!loadPartsResult.IsSuccess)
        {
            return KernelResult<FirmasmLoadedAssembly>.Failure(loadPartsResult.Diagnostics);
        }

        return KernelResult<FirmasmLoadedAssembly>.Success(
            new FirmasmLoadedAssembly(
                SourcePath: Path.GetFullPath(manifestPath),
                Manifest: parseResult.Value,
                LoadedParts: loadPartsResult.Value));
    }

    public KernelResult<FirmasmManifest> Parse(string sourceText)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return Failure<FirmasmManifest>("Assembly manifest source text must be provided.");
        }

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(sourceText);
            root = document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            return Failure<FirmasmManifest>($"Assembly manifest is not valid JSON: {ex.Message}");
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return Failure<FirmasmManifest>("Top-level .firmasm manifest must be an object.");
        }

        var unknownTopLevel = root.EnumerateObject()
            .Select(property => property.Name)
            .FirstOrDefault(name => !AllowedTopLevelSections.Contains(name));

        if (unknownTopLevel is not null)
        {
            return Failure<FirmasmManifest>($"Unknown top-level section '{unknownTopLevel}' in .firmasm manifest.");
        }

        if (!TryGetObject(root, "manifest", out var manifestSection, out var manifestDiagnostic))
        {
            return Failure<FirmasmManifest>(manifestDiagnostic);
        }

        if (!TryGetObject(root, "assembly", out var assemblySection, out var assemblyDiagnostic))
        {
            return Failure<FirmasmManifest>(assemblyDiagnostic);
        }

        if (!TryGetObject(root, "parts", out var partsSection, out var partsDiagnostic))
        {
            return Failure<FirmasmManifest>(partsDiagnostic);
        }

        if (!TryGetArray(root, "instances", out var instancesSection, out var instancesDiagnostic))
        {
            return Failure<FirmasmManifest>(instancesDiagnostic);
        }

        var versionResult = ParseRequiredString(manifestSection, "manifest", "version");
        if (!versionResult.IsSuccess)
        {
            return KernelResult<FirmasmManifest>.Failure(versionResult.Diagnostics);
        }

        var nameResult = ParseRequiredString(assemblySection, "assembly", "name");
        if (!nameResult.IsSuccess)
        {
            return KernelResult<FirmasmManifest>.Failure(nameResult.Diagnostics);
        }

        var unitsResult = ParseRequiredString(assemblySection, "assembly", "units");
        if (!unitsResult.IsSuccess)
        {
            return KernelResult<FirmasmManifest>.Failure(unitsResult.Diagnostics);
        }

        var partsResult = ParseParts(partsSection);
        if (!partsResult.IsSuccess)
        {
            return KernelResult<FirmasmManifest>.Failure(partsResult.Diagnostics);
        }

        var instancesResult = ParseInstances(instancesSection, partsResult.Value.Keys);
        if (!instancesResult.IsSuccess)
        {
            return KernelResult<FirmasmManifest>.Failure(instancesResult.Diagnostics);
        }

        return KernelResult<FirmasmManifest>.Success(
            new FirmasmManifest(
                ManifestVersion: versionResult.Value,
                Assembly: new FirmasmAssemblyMetadata(nameResult.Value, unitsResult.Value),
                Parts: partsResult.Value,
                Instances: instancesResult.Value));
    }

    private static KernelResult<IReadOnlyDictionary<string, FirmasmLoadedPartSource>> LoadPartSources(
        IReadOnlyDictionary<string, FirmasmPartDefinition> parts,
        string sourceDirectory)
    {
        var loaded = new Dictionary<string, FirmasmLoadedPartSource>(StringComparer.Ordinal);

        foreach (var (partName, part) in parts)
        {
            var resolvedPath = Path.GetFullPath(Path.Combine(sourceDirectory, part.Source));
            if (!File.Exists(resolvedPath))
            {
                return Failure<IReadOnlyDictionary<string, FirmasmLoadedPartSource>>(
                    $"Part '{partName}' source '{part.Source}' was not found at '{resolvedPath}'.");
            }

            switch (part.Kind)
            {
                case FirmasmPartKind.Firmament:
                    loaded[partName] = new FirmasmLoadedNativeFirmamentPart(part.Source, resolvedPath);
                    break;
                case FirmasmPartKind.Step:
                {
                    string stepText;
                    try
                    {
                        stepText = File.ReadAllText(resolvedPath);
                    }
                    catch (Exception ex)
                    {
                        return Failure<IReadOnlyDictionary<string, FirmasmLoadedPartSource>>(
                            $"STEP part '{partName}' at '{resolvedPath}' could not be read: {ex.Message}");
                    }

                    var importResult = Step242Importer.ImportBody(stepText);
                    if (!importResult.IsSuccess)
                    {
                        var reason = importResult.Diagnostics.FirstOrDefault()?.Message ?? "unknown STEP import failure";
                        return Failure<IReadOnlyDictionary<string, FirmasmLoadedPartSource>>(
                            $"STEP part '{partName}' at '{resolvedPath}' is unsupported or ambiguous for .firmasm bounded contract: {reason}");
                    }

                    loaded[partName] = new FirmasmLoadedOpaqueStepPart(part.Source, resolvedPath, importResult.Value);
                    break;
                }
                default:
                    return Failure<IReadOnlyDictionary<string, FirmasmLoadedPartSource>>($"Part '{partName}' uses unsupported kind '{part.Kind}'.");
            }
        }

        return KernelResult<IReadOnlyDictionary<string, FirmasmLoadedPartSource>>.Success(loaded);
    }

    private static KernelResult<IReadOnlyDictionary<string, FirmasmPartDefinition>> ParseParts(JsonElement partsSection)
    {
        var parsed = new Dictionary<string, FirmasmPartDefinition>(StringComparer.Ordinal);

        foreach (var property in partsSection.EnumerateObject())
        {
            var partName = property.Name;
            if (!parsed.TryAdd(partName, default!))
            {
                return Failure<IReadOnlyDictionary<string, FirmasmPartDefinition>>($"Duplicate part definition '{partName}' is not allowed.");
            }

            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                return Failure<IReadOnlyDictionary<string, FirmasmPartDefinition>>($"Part '{partName}' must be an object.");
            }

            RejectUnknownFieldsOrReturn(property.Value, new HashSet<string>(StringComparer.Ordinal) { "kind", "source" }, $"Part '{partName}'", out var unknownField);
            if (unknownField is not null)
            {
                return Failure<IReadOnlyDictionary<string, FirmasmPartDefinition>>(unknownField);
            }

            var kindResult = ParseRequiredString(property.Value, $"parts.{partName}", "kind");
            if (!kindResult.IsSuccess)
            {
                return KernelResult<IReadOnlyDictionary<string, FirmasmPartDefinition>>.Failure(kindResult.Diagnostics);
            }

            var sourceResult = ParseRequiredString(property.Value, $"parts.{partName}", "source");
            if (!sourceResult.IsSuccess)
            {
                return KernelResult<IReadOnlyDictionary<string, FirmasmPartDefinition>>.Failure(sourceResult.Diagnostics);
            }

            if (!TryParsePartKind(kindResult.Value, out var kind))
            {
                return Failure<IReadOnlyDictionary<string, FirmasmPartDefinition>>(
                    $"Part '{partName}' kind '{kindResult.Value}' is not supported. Allowed kinds: 'firmament', 'step'.");
            }

            parsed[partName] = new FirmasmPartDefinition(kind, sourceResult.Value);
        }

        if (parsed.Count == 0)
        {
            return Failure<IReadOnlyDictionary<string, FirmasmPartDefinition>>("Section 'parts' must contain at least one part definition.");
        }

        return KernelResult<IReadOnlyDictionary<string, FirmasmPartDefinition>>.Success(parsed);
    }

    private static KernelResult<IReadOnlyList<FirmasmInstanceDefinition>> ParseInstances(
        JsonElement instancesSection,
        IEnumerable<string> knownParts)
    {
        var knownPartSet = new HashSet<string>(knownParts, StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var instances = new List<FirmasmInstanceDefinition>();

        foreach (var item in instancesSection.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                return Failure<IReadOnlyList<FirmasmInstanceDefinition>>("Each instances entry must be an object.");
            }

            RejectUnknownFieldsOrReturn(item, new HashSet<string>(StringComparer.Ordinal) { "id", "part", "transform" }, "Instance entry", out var unknownField);
            if (unknownField is not null)
            {
                return Failure<IReadOnlyList<FirmasmInstanceDefinition>>(unknownField);
            }

            var idResult = ParseRequiredString(item, "instance", "id");
            if (!idResult.IsSuccess)
            {
                return KernelResult<IReadOnlyList<FirmasmInstanceDefinition>>.Failure(idResult.Diagnostics);
            }

            var partResult = ParseRequiredString(item, "instance", "part");
            if (!partResult.IsSuccess)
            {
                return KernelResult<IReadOnlyList<FirmasmInstanceDefinition>>.Failure(partResult.Diagnostics);
            }

            if (!ids.Add(idResult.Value))
            {
                return Failure<IReadOnlyList<FirmasmInstanceDefinition>>($"Duplicate instance id '{idResult.Value}' is not allowed.");
            }

            if (!knownPartSet.Contains(partResult.Value))
            {
                return Failure<IReadOnlyList<FirmasmInstanceDefinition>>(
                    $"Instance '{idResult.Value}' references unknown part '{partResult.Value}'.");
            }

            if (!TryGetObject(item, "transform", out var transformSection, out var transformDiagnostic))
            {
                return Failure<IReadOnlyList<FirmasmInstanceDefinition>>(transformDiagnostic);
            }

            var transformResult = ParseTransform(transformSection);
            if (!transformResult.IsSuccess)
            {
                return KernelResult<IReadOnlyList<FirmasmInstanceDefinition>>.Failure(transformResult.Diagnostics);
            }

            instances.Add(new FirmasmInstanceDefinition(idResult.Value, partResult.Value, transformResult.Value));
        }

        if (instances.Count == 0)
        {
            return Failure<IReadOnlyList<FirmasmInstanceDefinition>>("Section 'instances' must contain at least one instance.");
        }

        return KernelResult<IReadOnlyList<FirmasmInstanceDefinition>>.Success(instances);
    }

    private static KernelResult<FirmasmRigidTransform> ParseTransform(JsonElement transformSection)
    {
        RejectUnknownFieldsOrReturn(transformSection, new HashSet<string>(StringComparer.Ordinal) { "translate", "rotate_deg_xyz" }, "Instance transform", out var unknownField);
        if (unknownField is not null)
        {
            return Failure<FirmasmRigidTransform>(unknownField);
        }

        var translateResult = ParseNumericVector(transformSection, "translate", expectedCount: 3, "transform");
        if (!translateResult.IsSuccess)
        {
            return KernelResult<FirmasmRigidTransform>.Failure(translateResult.Diagnostics);
        }

        IReadOnlyList<double>? rotate = null;
        if (transformSection.TryGetProperty("rotate_deg_xyz", out _))
        {
            var rotateResult = ParseNumericVector(transformSection, "rotate_deg_xyz", expectedCount: 3, "transform");
            if (!rotateResult.IsSuccess)
            {
                return KernelResult<FirmasmRigidTransform>.Failure(rotateResult.Diagnostics);
            }

            rotate = rotateResult.Value;
        }

        return KernelResult<FirmasmRigidTransform>.Success(new FirmasmRigidTransform(translateResult.Value, rotate));
    }

    private static KernelResult<string> ParseRequiredString(JsonElement section, string sectionName, string fieldName)
    {
        if (!section.TryGetProperty(fieldName, out var element))
        {
            return Failure<string>($"Missing required field '{fieldName}' in section '{sectionName}'.");
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return Failure<string>($"Field '{fieldName}' in section '{sectionName}' must be a string.");
        }

        var value = element.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return Failure<string>($"Field '{fieldName}' in section '{sectionName}' must not be blank.");
        }

        return KernelResult<string>.Success(value);
    }

    private static KernelResult<IReadOnlyList<double>> ParseNumericVector(
        JsonElement section,
        string fieldName,
        int expectedCount,
        string sectionName)
    {
        if (!section.TryGetProperty(fieldName, out var element))
        {
            return Failure<IReadOnlyList<double>>($"Missing required field '{fieldName}' in section '{sectionName}'.");
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return Failure<IReadOnlyList<double>>($"Field '{fieldName}' in section '{sectionName}' must be an array of {expectedCount} numbers.");
        }

        var values = new List<double>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number)
            {
                return Failure<IReadOnlyList<double>>($"Field '{fieldName}' in section '{sectionName}' must contain numeric values only.");
            }

            values.Add(item.GetDouble());
        }

        if (values.Count != expectedCount)
        {
            return Failure<IReadOnlyList<double>>(
                $"Field '{fieldName}' in section '{sectionName}' must contain exactly {expectedCount} numeric values.");
        }

        return KernelResult<IReadOnlyList<double>>.Success(values);
    }

    private static bool TryGetObject(JsonElement root, string sectionName, out JsonElement section, out string diagnostic)
    {
        section = default;
        diagnostic = string.Empty;

        if (!root.TryGetProperty(sectionName, out section))
        {
            diagnostic = $"Missing required section '{sectionName}'.";
            return false;
        }

        if (section.ValueKind != JsonValueKind.Object)
        {
            diagnostic = $"Section '{sectionName}' must be an object.";
            return false;
        }

        return true;
    }

    private static bool TryGetArray(JsonElement root, string sectionName, out JsonElement section, out string diagnostic)
    {
        section = default;
        diagnostic = string.Empty;

        if (!root.TryGetProperty(sectionName, out section))
        {
            diagnostic = $"Missing required section '{sectionName}'.";
            return false;
        }

        if (section.ValueKind != JsonValueKind.Array)
        {
            diagnostic = $"Section '{sectionName}' must be an array.";
            return false;
        }

        return true;
    }

    private static void RejectUnknownFieldsOrReturn(
        JsonElement section,
        IReadOnlySet<string> allowedFields,
        string sectionName,
        out string? unknownFieldDiagnostic)
    {
        unknownFieldDiagnostic = section.EnumerateObject()
            .Select(property => property.Name)
            .FirstOrDefault(name => !allowedFields.Contains(name));

        if (unknownFieldDiagnostic is not null)
        {
            unknownFieldDiagnostic = $"{sectionName} contains unknown field '{unknownFieldDiagnostic}'.";
        }
    }

    private static bool TryParsePartKind(string value, out FirmasmPartKind kind)
    {
        switch (value)
        {
            case "firmament":
                kind = FirmasmPartKind.Firmament;
                return true;
            case "step":
                kind = FirmasmPartKind.Step;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static KernelResult<T> Failure<T>(string message)
    {
        return KernelResult<T>.Failure([
            new KernelDiagnostic(
                KernelDiagnosticCode.ValidationFailed,
                KernelDiagnosticSeverity.Error,
                message,
                Source: "firmasm")
        ]);
    }
}
