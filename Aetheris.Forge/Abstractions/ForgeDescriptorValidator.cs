using System.Text.RegularExpressions;

namespace Aetheris.Forge.Abstractions;

public static partial class ForgeDescriptorValidator
{
    public static ForgeDescriptorValidationResult Validate(ForgePackageDescriptor package)
    {
        var diagnostics = new List<ForgeDescriptorDiagnostic>();
        ValidatePackage(package, diagnostics);
        return new ForgeDescriptorValidationResult(diagnostics.OrderBy(d => d.Path, StringComparer.Ordinal).ThenBy(d => d.Code, StringComparer.Ordinal).ToArray());
    }

    private static void ValidatePackage(ForgePackageDescriptor package, List<ForgeDescriptorDiagnostic> diagnostics)
    {
        RequireId(package.PackageId, "package", "packageId", diagnostics);
        if (string.IsNullOrWhiteSpace(package.SemanticVersion) || !VersionRegex().IsMatch(package.SemanticVersion))
            Add(diagnostics, "forge.package.invalid-version", "semanticVersion", "Package semanticVersion must be a non-empty semantic version.");
        ValidateTrustTier(package.RequestedTrustTier, "requestedTrustTier", diagnostics);
        Duplicates(package.Concepts.Select(c => c.ConceptId), "forge.package.duplicate-concept-id", "concepts", diagnostics);
        Duplicates(package.Templates.Select(t => t.TemplateId), "forge.package.duplicate-template-id", "templates", diagnostics);
        Duplicates(package.Capabilities.Select(c => c.CapabilityId), "forge.package.duplicate-capability-id", "capabilities", diagnostics);
        ValidateRefs(package.Examples, e => e.ExampleId, e => e.SourcePathOrSnippet, "examples", "example", diagnostics);
        ValidateRefs(package.Fixtures, f => f.FixtureId, f => f.Path, "fixtures", "fixture", diagnostics);
        ValidateRefs(package.LlmGuidance, g => g.GuidanceId, g => g.Path, "llmGuidance", "llm-guidance", diagnostics);

        var conceptIds = package.Concepts.Select(c => c.ConceptId).Where(IsValidId).ToHashSet(StringComparer.Ordinal);
        var capabilityIds = package.Capabilities.Select(c => c.CapabilityId).Where(IsValidId).ToHashSet(StringComparer.Ordinal);
        for (var i = 0; i < package.Capabilities.Count; i++) ValidateCapability(package.Capabilities[i], $"capabilities[{i}]", diagnostics);
        for (var i = 0; i < package.Concepts.Count; i++) ValidateConcept(package.Concepts[i], $"concepts[{i}]", capabilityIds, diagnostics);
        for (var i = 0; i < package.Templates.Count; i++) ValidateTemplate(package.Templates[i], $"templates[{i}]", conceptIds, diagnostics);
    }

    private static void ValidateConcept(ForgeConceptDescriptor concept, string path, HashSet<string> capabilityIds, List<ForgeDescriptorDiagnostic> diagnostics)
    {
        RequireId(concept.ConceptId, "concept", $"{path}.conceptId", diagnostics);
        if (string.IsNullOrWhiteSpace(concept.Category)) Add(diagnostics, "forge.concept.missing-category", $"{path}.category", "Concept category is required.");
        Duplicates(concept.Fields.Select(f => f.FieldName), "forge.concept.duplicate-field-name", $"{path}.fields", diagnostics);
        Duplicates(concept.Diagnostics.Select(d => d.DiagnosticId), "forge.concept.duplicate-diagnostic-id", $"{path}.diagnostics", diagnostics);
        ValidateRefs(concept.Examples, e => e.ExampleId, e => e.SourcePathOrSnippet, $"{path}.examples", "example", diagnostics);
        ValidateRefs(concept.Fixtures, f => f.FixtureId, f => f.Path, $"{path}.fixtures", "fixture", diagnostics);
        ValidateRefs(concept.LlmGuidance, g => g.GuidanceId, g => g.Path, $"{path}.llmGuidance", "llm-guidance", diagnostics);
        for (var i = 0; i < concept.Fields.Count; i++) ValidateField(concept.Fields[i], $"{path}.fields[{i}]", diagnostics);
        for (var i = 0; i < concept.Diagnostics.Count; i++) ValidateDiagnostic(concept.Diagnostics[i], $"{path}.diagnostics[{i}]", diagnostics);
        for (var i = 0; i < concept.LoweringContracts.Count; i++)
        {
            var contract = concept.LoweringContracts[i];
            var cpath = $"{path}.loweringContracts[{i}]";
            RequireId(contract.ContractId, "lowering-contract", $"{cpath}.contractId", diagnostics);
            RequireId(contract.SourceConceptId, "concept", $"{cpath}.sourceConceptId", diagnostics);
            if (string.IsNullOrWhiteSpace(contract.TargetAirFeatureFamily)) Add(diagnostics, "forge.lowering.missing-target", $"{cpath}.targetAirFeatureFamily", "Lowering contract targetAirFeatureFamily is required.");
            foreach (var required in contract.RequiredCapabilities.Where(r => !capabilityIds.Contains(r)))
                Add(diagnostics, "forge.lowering.unknown-capability", $"{cpath}.requiredCapabilities", $"Lowering contract requires undeclared capability '{required}'.");
        }
    }

    private static void ValidateTemplate(ForgeTemplateDescriptor template, string path, HashSet<string> conceptIds, List<ForgeDescriptorDiagnostic> diagnostics)
    {
        RequireId(template.TemplateId, "template", $"{path}.templateId", diagnostics);
        Duplicates(template.Parameters.Select(p => p.FieldName), "forge.template.duplicate-parameter-name", $"{path}.parameters", diagnostics);
        Duplicates(template.Diagnostics.Select(d => d.DiagnosticId), "forge.template.duplicate-diagnostic-id", $"{path}.diagnostics", diagnostics);
        foreach (var id in template.ConceptRequirements.Concat(template.ExpandsToConceptIds).Where(id => !conceptIds.Contains(id)))
            Add(diagnostics, "forge.template.unknown-concept-id", path, $"Template references unknown same-package concept '{id}'.");
        for (var i = 0; i < template.Parameters.Count; i++) ValidateField(template.Parameters[i], $"{path}.parameters[{i}]", diagnostics);
        for (var i = 0; i < template.Diagnostics.Count; i++) ValidateDiagnostic(template.Diagnostics[i], $"{path}.diagnostics[{i}]", diagnostics);
    }

    private static void ValidateField(ForgeFieldDescriptor field, string path, List<ForgeDescriptorDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(field.FieldName)) Add(diagnostics, "forge.field.missing-name", $"{path}.fieldName", "Field name is required.");
        if (!Enum.IsDefined(field.FieldType)) Add(diagnostics, "forge.field.invalid-type", $"{path}.fieldType", "Field type is invalid.");
        if (field.Required && !string.IsNullOrWhiteSpace(field.DefaultValue)) Add(diagnostics, "forge.field.required-with-default", path, "Required fields must not declare defaultValue metadata.");
    }

    private static void ValidateDiagnostic(ForgeDiagnosticDescriptor descriptor, string path, List<ForgeDescriptorDiagnostic> diagnostics)
    {
        RequireId(descriptor.DiagnosticId, "diagnostic", $"{path}.diagnosticId", diagnostics);
        if (!Enum.IsDefined(descriptor.Severity)) Add(diagnostics, "forge.diagnostic.invalid-severity", $"{path}.severity", "Diagnostic severity is invalid.");
        if (string.IsNullOrWhiteSpace(descriptor.MessageTemplate)) Add(diagnostics, "forge.diagnostic.missing-message-template", $"{path}.messageTemplate", "Diagnostic messageTemplate is required.");
    }

    private static void ValidateCapability(ForgeCapabilityDescriptor capability, string path, List<ForgeDescriptorDiagnostic> diagnostics)
    {
        RequireId(capability.CapabilityId, "capability", $"{path}.capabilityId", diagnostics);
        ValidateTrustTier(capability.Tier, $"{path}.tier", diagnostics);
    }

    private static void ValidateTrustTier(ForgeTrustTier tier, string path, List<ForgeDescriptorDiagnostic> diagnostics)
    { if (!Enum.IsDefined(tier)) Add(diagnostics, "forge.trust-tier.invalid", path, "Trust tier must be one of the declared Forge trust tiers."); }

    private static void ValidateRefs<T>(IReadOnlyList<T> refs, Func<T,string> id, Func<T,string> path, string basePath, string kind, List<ForgeDescriptorDiagnostic> diagnostics)
    {
        Duplicates(refs.Select(id), $"forge.{kind}.duplicate-id", basePath, diagnostics);
        for (var i = 0; i < refs.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(id(refs[i]))) Add(diagnostics, $"forge.{kind}.missing-id", $"{basePath}[{i}]", $"{kind} id is required.");
            if (string.IsNullOrWhiteSpace(path(refs[i]))) Add(diagnostics, $"forge.{kind}.missing-path", $"{basePath}[{i}]", $"{kind} path or snippet is required.");
        }
    }

    private static void Duplicates(IEnumerable<string> values, string code, string path, List<ForgeDescriptorDiagnostic> diagnostics)
    {
        foreach (var value in values.Where(v => !string.IsNullOrWhiteSpace(v)).GroupBy(v => v, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).Order(StringComparer.Ordinal))
            Add(diagnostics, code, path, $"Duplicate id/name '{value}'.");
    }

    private static void RequireId(string value, string kind, string path, List<ForgeDescriptorDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value)) Add(diagnostics, $"forge.{kind}.missing-id", path, $"{kind} id is required.");
        else if (!IsValidId(value)) Add(diagnostics, $"forge.{kind}.invalid-id", path, $"{kind} id '{value}' is invalid.");
    }

    private static bool IsValidId(string value) => IdRegex().IsMatch(value);
    private static void Add(List<ForgeDescriptorDiagnostic> diagnostics, string code, string path, string message) => diagnostics.Add(new ForgeDescriptorDiagnostic(code, path, message));
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9]*(?:[.-][A-Za-z][A-Za-z0-9]*)*$")]
    private static partial Regex IdRegex();
    [GeneratedRegex("^\\d+\\.\\d+\\.\\d+(?:[-+][0-9A-Za-z.-]+)?$")]
    private static partial Regex VersionRegex();
}
