using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.SheetMetal;

namespace Aetheris.Forge.Host;

/// <summary>
/// Language-neutral, serialized invocation boundary over registered public Firmament Templates.
/// It intentionally exposes DTOs and artifacts, never compiler or geometry object graphs.
/// </summary>
public sealed class ForgeProtocolHost
{
    private static readonly UTF8Encoding Utf8 = new(false);
    private readonly object invocationGate = new();
    private readonly IReadOnlyDictionary<string, RegisteredTemplate> templates;

    public ForgeProtocolHost() : this(CreateStandardTemplates()) { }

    private ForgeProtocolHost(IEnumerable<RegisteredTemplate> templates)
    {
        this.templates = templates.OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToDictionary(item => item.Id, StringComparer.Ordinal);
    }

    public ForgeHostInfo GetHostInfo() => new(
        ForgeHostProtocol.Version,
        ForgeHostProtocol.Name,
        AetherisVersion,
        ["ListTemplates", "DescribeTemplate", "InvokeTemplate"],
        "serialized-per-host-instance");

    public ForgeTemplateListResponse ListTemplates() => new(
        ForgeHostProtocol.Version,
        templates.Values.Select(item => new ForgeTemplateSummary(item.Id, item.DisplayName, item.Version)).ToArray());

    public ForgeTemplateDescription? DescribeTemplate(string templateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        if (!templates.TryGetValue(templateId, out var template)) return null;
        return new(
            ForgeHostProtocol.Version,
            template.Id,
            template.DisplayName,
            template.Version,
            template.Documentation,
            template.Metadata.Parameters.Select(parameter => DescribeParameter(template, parameter)).ToArray(),
            template.Artifacts,
            Signature(template.Metadata),
            template.Metadata.TargetKind,
            template.Metadata.Constraints.Select(constraint => new ForgeTemplateConstraintDescription(
                constraint.Name, constraint.Expression)).ToArray());
    }

    public ForgeTemplateInvocationResult InvokeTemplate(
        string templateId,
        ForgeTemplateInvocationRequest request,
        string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        var started = Stopwatch.GetTimestamp();
        var version = templates.TryGetValue(templateId, out var known) ? known.Version : string.Empty;
        ForgeTemplateInvocationResult Failure(params ForgeProtocolDiagnostic[] diagnostics) => new(
            false, Identity(templateId, version, null), diagnostics, [], Stopwatch.GetElapsedTime(started).TotalMilliseconds);

        if (request.ProtocolVersion != ForgeHostProtocol.Version)
            return Failure(Error("forge-host-protocol-version-unsupported",
                $"Protocol version {request.ProtocolVersion} is unsupported; this host accepts version {ForgeHostProtocol.Version}."));
        if (known is null)
            return Failure(Error("forge-host-template-not-found", $"Public template '{templateId}' was not found.", templateId));

        lock (invocationGate)
        {
            var requestedArtifacts = (request.Artifacts is null || request.Artifacts.Count == 0
                    ? new[] { ForgeArtifactKind.StepAp242 }
                    : request.Artifacts.Distinct().ToArray())
                .OrderBy(item => item).ToArray();
            var unsupported = requestedArtifacts.Except(known.Artifacts).ToArray();
            if (unsupported.Length > 0)
                return Failure(unsupported.Select(item => Error("forge-host-artifact-unsupported",
                    $"Template '{templateId}' does not declare artifact '{item}'.", item.ToString())).ToArray());

            var diagnostics = new List<ForgeProtocolDiagnostic>();
            var hostArguments = BindArguments(known, request.Arguments ?? new Dictionary<string, JsonElement>(), diagnostics);
            if (diagnostics.Any(item => item.Severity == ForgeProtocolDiagnosticSeverity.Error))
                return Failure(diagnostics.ToArray());

            var expansion = FirmamentTemplateHostBridge.Expand(
                known.Source, known.Metadata.Name, "InteropPart", hostArguments, out var bindingDiagnostics);
            diagnostics.AddRange(bindingDiagnostics.Select(FromFirmamentDiagnostic));
            if (expansion is null || diagnostics.Any(item => item.Severity == ForgeProtocolDiagnosticSeverity.Error))
                return Failure(diagnostics.ToArray());

            SheetMetalAuthoringResult? sheetMetalCompilation = null;
            FirmamentStepExportResult? nativeCompilation = null;
            if (known.NativeFirmament)
            {
                var compiled = FirmamentBuildAndExport.CompileSource(expansion.ExpandedSource);
                diagnostics.AddRange(compiled.Diagnostics.Select(item => new ForgeProtocolDiagnostic(item.Code.ToString(),
                    item.Severity == Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error ? ForgeProtocolDiagnosticSeverity.Error : ForgeProtocolDiagnosticSeverity.Warning,
                    item.Message, item.Source, "FirmamentV2")));
                if (!compiled.IsSuccess) return Failure(diagnostics.ToArray());
                nativeCompilation = compiled.Value;
            }
            else
            {
                sheetMetalCompilation = SheetMetalFirmament.Compile(expansion.ExpandedSource, $"forge-host:{known.Id}");
                diagnostics.AddRange(sheetMetalCompilation.Diagnostics.Select(FromSheetMetalDiagnostic));
                if (!sheetMetalCompilation.IsSuccess || sheetMetalCompilation.Part?.FormedBody is null || sheetMetalCompilation.FlatPattern is null)
                    return Failure(diagnostics.ToArray());

                var dfm = SheetMetalDfm.Evaluate(sheetMetalCompilation.Part, sheetMetalCompilation.FlatPattern);
                diagnostics.AddRange(dfm.Findings.Where(item => item.Status is SheetMetalDfmStatus.Warning or SheetMetalDfmStatus.Fail)
                    .Select(item => new ForgeProtocolDiagnostic(item.RuleId,
                        item.Status == SheetMetalDfmStatus.Fail ? ForgeProtocolDiagnosticSeverity.Error : ForgeProtocolDiagnosticSeverity.Warning,
                        item.Message, item.SubjectId, "SheetMetalDfm")));
                if (diagnostics.Any(item => item.Severity == ForgeProtocolDiagnosticSeverity.Error))
                    return Failure(diagnostics.ToArray());
            }

            try
            {
                var root = Path.GetFullPath(outputDirectory);
                Directory.CreateDirectory(root);
                var artifacts = new List<ForgeArtifact>();
                foreach (var kind in requestedArtifacts)
                {
                    var (name, contentType, content) = nativeCompilation is null
                        ? GenerateArtifact(kind, sheetMetalCompilation!)
                        : GenerateNativeArtifact(kind, nativeCompilation);
                    var path = Path.Combine(root, name);
                    var resolved = Path.GetFullPath(path);
                    if (!string.Equals(Path.GetDirectoryName(resolved), root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Generated artifact path escaped the requested output directory.");
                    var bytes = Utf8.GetBytes(content);
                    File.WriteAllBytes(resolved, bytes);
                    artifacts.Add(new(kind, name, contentType, bytes.LongLength,
                        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), name));
                }
                return new(true, Identity(templateId, known.Version, expansion.SpecializationIdentity),
                    diagnostics, artifacts, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(Error("forge-host-artifact-write-failed", exception.Message, outputDirectory));
                return Failure(diagnostics.ToArray());
            }
            catch (InvalidOperationException exception)
            {
                diagnostics.Add(Error("forge-host-artifact-generation-failed", exception.Message, templateId, "AetherisLowering"));
                return Failure(diagnostics.ToArray());
            }
        }
    }

    private static IReadOnlyList<RegisteredTemplate> CreateStandardTemplates()
    {
        var source = SheetMetalTemplateLibrary.Source;
        var module = FirmamentTemplateHostBridge.InspectModule(source, out var diagnostics);
        if (diagnostics.Count > 0)
            throw new InvalidOperationException("Standard Firmament template catalog is invalid: " + string.Join("; ", diagnostics));
        var version = "1+" + Convert.ToHexString(SHA256.HashData(Utf8.GetBytes(source)))[..12].ToLowerInvariant();
        var sheetMetal = module.Templates.Where(item => string.Equals(item.TargetKind, "SheetMetal", StringComparison.Ordinal))
            .Select(item => new RegisteredTemplate(
                "Standard.SheetMetal." + item.Name,
                Humanize(item.Name),
                version,
                $"Standard production Firmament Sheet Metal template '{item.Name}'. Its schema is derived from the embedded authoritative module.",
                source,
                item,
                module.Records.ToDictionary(record => record.Name, StringComparer.Ordinal),
                module.Enums.ToDictionary(value => value.Name, StringComparer.Ordinal),
                [ForgeArtifactKind.StepAp242, ForgeArtifactKind.FlatStep, ForgeArtifactKind.Svg], false))
            .ToArray();
        var productSource = PaperclipTemplateLibrary.Source;
        var products = FirmamentTemplateHostBridge.InspectModule(productSource, out var productDiagnostics);
        if (productDiagnostics.Count > 0)
            throw new InvalidOperationException("Standard Products Firmament template catalog is invalid: " + string.Join("; ", productDiagnostics));
        var productVersion = "1+" + Convert.ToHexString(SHA256.HashData(Utf8.GetBytes(productSource)))[..12].ToLowerInvariant();
        var paperclip = products.Templates.Where(item => string.Equals(item.Name, "PaperclipTemplate", StringComparison.Ordinal))
            .Select(item => new RegisteredTemplate(PaperclipTemplateLibrary.TemplateId, "Paperclip", productVersion,
                "A bounded parametric office paperclip compiled from a semantic planar path and constant circular Sweep.",
                productSource, item, products.Records.ToDictionary(record => record.Name, StringComparer.Ordinal),
                products.Enums.ToDictionary(value => value.Name, StringComparer.Ordinal), [ForgeArtifactKind.StepAp242], true));
        return sheetMetal.Concat(paperclip).ToArray();
    }

    private static ForgeTemplateParameterDescription DescribeParameter(RegisteredTemplate template, FirmamentTemplateParameterMetadata parameter)
    {
        var fields = template.Records.TryGetValue(parameter.TypeName, out var record)
            ? record.Fields.Select(field => DescribeValue(template, field.Name, field.TypeName, true, null)).ToArray()
            : null;
        return DescribeValue(template, parameter.Name, parameter.TypeName, parameter.DefaultExpression is null,
            parameter.DefaultExpression, fields,
            parameter.Kind == FirmamentTemplateParameterKind.Type ? "type" : fields is null ? "value" : "record",
            parameter.ConstraintConcept);
    }

    private static ForgeTemplateParameterDescription DescribeValue(RegisteredTemplate template, string name, string type,
        bool required, string? defaultValue, IReadOnlyList<ForgeTemplateParameterDescription>? fields = null,
        string category = "value", string? constraint = null)
    {
        var dimension = type switch { "Length" => "length", "Angle" => "angle", _ => null };
        var unit = type switch { "Length" => "mm", "Angle" => "deg", _ => null };
        var allowed = template.Enums.TryGetValue(type, out var enumeration) ? enumeration.Cases : null;
        return new(name, PublicType(type, fields is not null), required, defaultValue, dimension, unit, allowed, fields, category, constraint);
    }

    private static string Signature(FirmamentTemplateMetadata template) =>
        template.Name + "<" + string.Join(", ", template.Parameters.Select(parameter =>
            parameter.Kind == FirmamentTemplateParameterKind.Type
                ? $"type {parameter.Name} satisfies {parameter.ConstraintConcept}"
                : $"{parameter.Name}: {parameter.TypeName}" + (parameter.DefaultExpression is null ? string.Empty : $" = {parameter.DefaultExpression}"))) + ">";

    private static IReadOnlyDictionary<string, FirmamentHostArgument> BindArguments(
        RegisteredTemplate template,
        IReadOnlyDictionary<string, JsonElement> supplied,
        ICollection<ForgeProtocolDiagnostic> diagnostics)
    {
        var result = new Dictionary<string, FirmamentHostArgument>(StringComparer.Ordinal);
        var parameters = template.Metadata.Parameters;
        FirmamentTemplateRecordMetadata? onlyRecord = null;
        var projectedRecord = parameters.Count == 1 && template.Records.TryGetValue(parameters[0].TypeName, out onlyRecord);
        if (projectedRecord && !TryGet(supplied, parameters[0].Name, out _))
        {
            BindRecordFields(parameters[0].Name, parameters[0].TypeName, onlyRecord!, supplied, result, template, diagnostics);
            return result;
        }

        foreach (var pair in supplied)
        {
            var parameter = parameters.FirstOrDefault(item => string.Equals(item.Name, pair.Key, StringComparison.OrdinalIgnoreCase));
            if (parameter is null)
            {
                result[pair.Key] = new(pair.Value.GetRawText());
                continue;
            }
            if (template.Records.TryGetValue(parameter.TypeName, out var record))
                BindRecord(parameter.Name, parameter.TypeName, record, pair.Value, result, template, diagnostics);
            else if (TryLiteral(parameter.TypeName, pair.Value, template, out var literal, out var message))
                result[parameter.Name] = new(literal);
            else diagnostics.Add(Error("forge-host-argument-transport-type", message, parameter.Name));
        }
        return result;
    }

    private static void BindRecord(string parameterName, string recordType, FirmamentTemplateRecordMetadata record,
        JsonElement value, IDictionary<string, FirmamentHostArgument> result, RegisteredTemplate template,
        ICollection<ForgeProtocolDiagnostic> diagnostics)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(Error("forge-host-argument-transport-type",
                $"Parameter '{parameterName}' expects a JSON object for Firmament Record '{recordType}'.", parameterName));
            return;
        }
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        BindRecordFields(parameterName, recordType, record,
            value.EnumerateObject().ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal),
            result, template, diagnostics);
    }

    private static void BindRecordFields(string parameterName, string recordType, FirmamentTemplateRecordMetadata record,
        IEnumerable<KeyValuePair<string, JsonElement>> values, IDictionary<string, FirmamentHostArgument> result,
        RegisteredTemplate template, ICollection<ForgeProtocolDiagnostic> diagnostics)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in values)
        {
            var field = record.Fields.FirstOrDefault(item => string.Equals(item.Name, property.Key, StringComparison.OrdinalIgnoreCase));
            if (field is null)
            {
                fields[property.Key] = property.Value.GetRawText();
                continue;
            }
            if (TryLiteral(field.TypeName, property.Value, template, out var literal, out var message)) fields[field.Name] = literal;
            else diagnostics.Add(Error("forge-host-argument-transport-type", message, parameterName + "." + field.Name));
        }
        result[parameterName] = new(string.Empty, recordType, fields);
    }

    private static bool TryLiteral(string type, JsonElement value, RegisteredTemplate template, out string literal, out string message)
    {
        literal = string.Empty;
        message = string.Empty;
        switch (type)
        {
            case "Length": case "Angle":
                if (value.ValueKind == JsonValueKind.String)
                {
                    literal = Regex.Replace(value.GetString()!.Trim(), @"(?<=\d)\s+(?=[A-Za-z])", string.Empty,
                        RegexOptions.CultureInvariant);
                    return true;
                }
                break;
            case "int":
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _)) { literal = value.GetRawText(); return true; }
                break;
            case "float":
                if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number))
                { literal = number.ToString("R", CultureInfo.InvariantCulture); return true; }
                break;
            case "bool":
                if (value.ValueKind is JsonValueKind.True or JsonValueKind.False) { literal = value.GetBoolean() ? "true" : "false"; return true; }
                break;
            case "String":
                if (value.ValueKind == JsonValueKind.String) { literal = value.GetRawText(); return true; }
                break;
            default:
                if (template.Enums.ContainsKey(type) && value.ValueKind == JsonValueKind.String)
                { literal = value.GetString()!; return true; }
                if (value.ValueKind == JsonValueKind.String) { literal = value.GetString()!; return true; }
                break;
        }
        message = $"Firmament value of type '{type}' has incompatible JSON kind '{value.ValueKind}'.";
        return false;
    }

    private static (string Name, string ContentType, string Content) GenerateArtifact(ForgeArtifactKind kind, SheetMetalAuthoringResult result)
    {
        if (kind == ForgeArtifactKind.StepAp242)
        {
            var exported = Step242Exporter.ExportBody(result.Part!.FormedBody!, new Step242ExportOptions { ProductName = "InteropPart" });
            if (!exported.IsSuccess) throw new InvalidOperationException("STEP AP242 export failed: " + string.Join("; ", exported.Diagnostics.Select(item => item.Message)));
            return ("part.step", "model/step", exported.Value);
        }
        if (kind == ForgeArtifactKind.FlatStep)
        {
            var flat = SheetMetalManufacturingArtifacts.BuildFlatBody(result.Part!, result.FlatPattern!);
            if (!flat.IsSuccess || flat.Body is null) throw new InvalidOperationException("Flat STEP lowering failed: " + string.Join("; ", flat.Diagnostics.Select(item => item.Message)));
            var exported = Step242Exporter.ExportBody(flat.Body, new Step242ExportOptions { ProductName = "InteropPart flat pattern" });
            if (!exported.IsSuccess) throw new InvalidOperationException("Flat STEP export failed: " + string.Join("; ", exported.Diagnostics.Select(item => item.Message)));
            return ("part.flat.step", "model/step", exported.Value);
        }
        if (kind == ForgeArtifactKind.Svg)
            return ("part.flat.svg", "image/svg+xml", SheetMetalSvgRenderer.Render(result.FlatPattern!));
        throw new InvalidOperationException($"Artifact kind '{kind}' has no generator.");
    }

    private static (string Name, string ContentType, string Content) GenerateNativeArtifact(ForgeArtifactKind kind, FirmamentStepExportResult result) =>
        kind == ForgeArtifactKind.StepAp242
            ? ("paperclip.step", "model/step", result.StepText)
            : throw new InvalidOperationException($"Native Firmament artifact kind '{kind}' has no generator.");

    private static ForgeProtocolDiagnostic FromFirmamentDiagnostic(string value)
    {
        var parts = value.Split(':', 3);
        return Error(parts[0], value, parts.Length > 1 ? parts[1] : null, "FirmamentBinder");
    }

    private static ForgeProtocolDiagnostic FromSheetMetalDiagnostic(SheetMetalDiagnostic value) => new(
        value.Code,
        value.Severity switch
        {
            SheetMetalDiagnosticSeverity.Information => ForgeProtocolDiagnosticSeverity.Info,
            SheetMetalDiagnosticSeverity.Warning => ForgeProtocolDiagnosticSeverity.Warning,
            _ => ForgeProtocolDiagnosticSeverity.Error,
        },
        value.Message,
        value.SourceFaceIds is null ? null : string.Join(",", value.SourceFaceIds),
        "SheetMetalLowering");

    private static ForgeProtocolDiagnostic Error(string code, string message, string? target = null, string? source = null) =>
        new(code, ForgeProtocolDiagnosticSeverity.Error, message, target, source);

    private static ForgeInvocationIdentity Identity(string template, string version, string? specialization) =>
        new(ForgeHostProtocol.Version, template, version, AetherisVersion, specialization);

    private static bool TryGet(IReadOnlyDictionary<string, JsonElement> values, string name, out JsonElement value)
    {
        foreach (var pair in values)
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)) { value = pair.Value; return true; }
        value = default;
        return false;
    }

    private static string PublicType(string type, bool record) => type switch
    {
        "int" => "integer", "float" => "number", "bool" => "boolean", "String" => "string",
        _ when record => "record", _ => type,
    };

    private static string Humanize(string value) => Regex.Replace(value, "(?<=[a-z0-9])(?=[A-Z])", " ", RegexOptions.CultureInvariant);

    private static string AetherisVersion =>
        typeof(ForgeProtocolHost).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(ForgeProtocolHost).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    private sealed record RegisteredTemplate(
        string Id,
        string DisplayName,
        string Version,
        string Documentation,
        string Source,
        FirmamentTemplateMetadata Metadata,
        IReadOnlyDictionary<string, FirmamentTemplateRecordMetadata> Records,
        IReadOnlyDictionary<string, FirmamentTemplateEnumMetadata> Enums,
        IReadOnlyList<ForgeArtifactKind> Artifacts,
        bool NativeFirmament);
}
