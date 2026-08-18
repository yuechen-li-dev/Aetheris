namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Stable compiler metadata consumed by Forge and generated host bindings.</summary>
public sealed record FirmamentTemplateMetadata(
    string Name,
    string TargetKind,
    IReadOnlyList<FirmamentTemplateParameterMetadata> Parameters,
    IReadOnlyList<FirmamentTemplateConstraintMetadata> Constraints);

public sealed record FirmamentTemplateConstraintMetadata(
    string Name,
    string Expression);

public sealed record FirmamentTemplateParameterMetadata(
    string Name,
    FirmamentTemplateParameterKind Kind,
    string TypeName,
    string? DefaultExpression,
    string? ConstraintConcept);

public sealed record FirmamentTemplateRecordFieldMetadata(
    string Name,
    string TypeName);

public sealed record FirmamentTemplateRecordMetadata(
    string Name,
    IReadOnlyList<FirmamentTemplateRecordFieldMetadata> Fields);

public sealed record FirmamentTemplateEnumMetadata(
    string Name,
    IReadOnlyList<string> Cases);

/// <summary>Authoritative public Template schema extracted from Firmament binder IR.</summary>
public sealed record FirmamentTemplateModuleMetadata(
    IReadOnlyList<FirmamentTemplateMetadata> Templates,
    IReadOnlyList<FirmamentTemplateRecordMetadata> Records,
    IReadOnlyList<FirmamentTemplateEnumMetadata> Enums);

public enum FirmamentTemplateParameterKind
{
    Value = 1,
    Type = 2,
}

/// <summary>
/// Representation-neutral value admitted at the Forge/Firmament boundary. Literal text is
/// canonical Firmament value notation (for example 12mm or true), not arbitrary source.
/// </summary>
public sealed record FirmamentHostArgument(
    string Literal,
    string? RecordType = null,
    IReadOnlyDictionary<string, string>? RecordFields = null);

public sealed record FirmamentHostTemplateExpansion(
    string ExpandedSource,
    string TemplateName,
    string InstanceName,
    string SpecializationIdentity,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> RecordArguments);

public sealed record FirmamentTemplateSourceExpansion(
    string ExpandedSource,
    IReadOnlyList<ConceptIrTemplateInstantiation> Instantiations);

/// <summary>
/// Expands user-authored generic Templates before a domain compiler consumes the
/// concrete declarations. Domain modules use this same typed parser/binder as the
/// canonical Firmament V2 frontend; they do not implement their own substitution.
/// </summary>
public static class FirmamentTemplateSourceCompiler
{
    public static FirmamentTemplateSourceExpansion? Expand(
        string source,
        out IReadOnlyList<string> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(source);
        var collected = new List<string>();
        var expansion = FirmamentV2TemplateExpansion.Expand(source, collected);
        diagnostics = collected.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return expansion is null ? null : new(expansion.Source, expansion.Instantiations);
    }
}

/// <summary>
/// Narrow public seam for Forge. Template declarations are parsed once into compiler IR and host
/// invocations enter the existing typed binder directly; this is not a general source-rewrite API.
/// </summary>
public static class FirmamentTemplateHostBridge
{
    public static IReadOnlyList<FirmamentTemplateMetadata> Inspect(
        string moduleSource,
        out IReadOnlyList<string> diagnostics)
        => InspectModule(moduleSource, out diagnostics).Templates;

    /// <summary>
    /// Inspects templates and their referenced Record/Enum definitions through the same parser
    /// used by host binding. Interop callers therefore never need to parse Firmament source.
    /// </summary>
    public static FirmamentTemplateModuleMetadata InspectModule(
        string moduleSource,
        out IReadOnlyList<string> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(moduleSource);
        var collected = new List<string>();
        var templates = FirmamentV2TemplateExpansion.Inspect(moduleSource, collected)
            .Select(template => new FirmamentTemplateMetadata(
                template.Name,
                template.TargetKind,
                template.Parameters.Select(parameter => new FirmamentTemplateParameterMetadata(
                    parameter.Name,
                    parameter.Kind == "Type" ? FirmamentTemplateParameterKind.Type : FirmamentTemplateParameterKind.Value,
                    parameter.TypeName,
                    parameter.DefaultExpression,
                    parameter.ConstraintConcept)).ToArray(),
                template.Constraints.Select(constraint => new FirmamentTemplateConstraintMetadata(
                    constraint.Name,
                    constraint.Expression)).ToArray()))
            .ToArray();
        var records = FirmamentV2TemplateExpansion.InspectRecords(moduleSource, collected)
            .Select(record => new FirmamentTemplateRecordMetadata(record.Name,
                record.Fields.Select(field => new FirmamentTemplateRecordFieldMetadata(field.Key, field.Value)).ToArray()))
            .ToArray();
        var enums = FirmamentV2TemplateExpansion.InspectEnums(moduleSource)
            .Select(item => new FirmamentTemplateEnumMetadata(item.Key, item.Value.ToArray()))
            .ToArray();
        diagnostics = collected.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return new(templates, records, enums);
    }

    public static FirmamentHostTemplateExpansion? Expand(
        string moduleSource,
        string templateName,
        string instanceName,
        IReadOnlyDictionary<string, FirmamentHostArgument> arguments,
        out IReadOnlyList<string> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(moduleSource);
        ArgumentNullException.ThrowIfNull(arguments);
        var collected = new List<string>();
        Dictionary<string, FirmamentV2TemplateExpansion.HostArgument> hostArguments;
        try
        {
            hostArguments = arguments.ToDictionary(
                pair => pair.Key,
                pair => new FirmamentV2TemplateExpansion.HostArgument(
                    pair.Value.Literal,
                    pair.Value.RecordType,
                    pair.Value.RecordFields),
                StringComparer.Ordinal);
        }
        catch (ArgumentException)
        {
            diagnostics = ["firmament-host-argument-duplicate"];
            return null;
        }
        var expansion = FirmamentV2TemplateExpansion.ExpandHostInvocation(
            moduleSource, templateName, instanceName, hostArguments, collected);
        diagnostics = collected.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (expansion is null) return null;
        var instance = expansion.Instantiations.Single();
        return new FirmamentHostTemplateExpansion(
            expansion.Source,
            templateName,
            instanceName,
            instance.SpecializationIdentity,
            (instance.RecordArguments ?? new Dictionary<string, ConceptIrTemplateRecordArgument>())
                .ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyDictionary<string, string>)pair.Value.Members,
                    StringComparer.Ordinal));
    }
}
