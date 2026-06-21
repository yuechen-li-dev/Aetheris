namespace Aetheris.Forge.Abstractions;

public enum ForgeTrustTier
{
    SemanticDocsOnly = 1,
    ValidationDerivation = 2,
    LoweringProvider = 3,
    MaterializerProvider = 4,
    UnsafeNativeExperimental = 5,
}

public enum ForgeDiagnosticSeverity
{
    Info = 1,
    Warning = 2,
    Error = 3,
}

public enum ForgeFieldType
{
    Length = 1,
    Angle = 2,
    Integer = 3,
    Real = 4,
    Boolean = 5,
    String = 6,
    FaceSelector = 7,
    FaceLocalPoint2D = 8,
    HoleEndCondition = 9,
    SemanticReference = 10,
}

public sealed record ForgePackageDescriptor(
    string PackageId,
    string DisplayName,
    string SemanticVersion,
    string Vendor,
    string Description,
    ForgeTrustTier RequestedTrustTier,
    IReadOnlyList<string> HostRequirements,
    IReadOnlyList<ForgeConceptDescriptor> Concepts,
    IReadOnlyList<ForgeTemplateDescriptor> Templates,
    IReadOnlyList<ForgeCapabilityDescriptor> Capabilities,
    IReadOnlyList<ForgeExampleDescriptor> Examples,
    IReadOnlyList<ForgeFixtureDescriptor> Fixtures,
    IReadOnlyList<ForgeLlmGuidanceDescriptor> LlmGuidance);

public sealed record ForgeConceptDescriptor(
    string ConceptId,
    string DisplayName,
    string Category,
    string Description,
    IReadOnlyList<ForgeFieldDescriptor> Fields,
    IReadOnlyList<ForgeDiagnosticDescriptor> Diagnostics,
    IReadOnlyList<string> CapabilityRequirements,
    IReadOnlyList<ForgeLoweringContractDescriptor> LoweringContracts,
    IReadOnlyList<ForgeExampleDescriptor> Examples,
    IReadOnlyList<ForgeFixtureDescriptor> Fixtures,
    IReadOnlyList<ForgeLlmGuidanceDescriptor> LlmGuidance,
    IReadOnlyList<string>? ManufacturingAssumptions = null,
    IReadOnlyList<string>? DerivedFields = null,
    IReadOnlyList<string>? ValidationRuleIds = null);

public sealed record ForgeTemplateDescriptor(
    string TemplateId,
    string DisplayName,
    string Description,
    IReadOnlyList<ForgeFieldDescriptor> Parameters,
    IReadOnlyList<string> ConceptRequirements,
    IReadOnlyList<string> ExpandsToConceptIds,
    IReadOnlyList<ForgeDiagnosticDescriptor> Diagnostics,
    IReadOnlyList<ForgeExampleDescriptor> Examples,
    IReadOnlyList<ForgeFixtureDescriptor> Fixtures,
    IReadOnlyList<ForgeLlmGuidanceDescriptor> LlmGuidance);

public sealed record ForgeFieldDescriptor(
    string FieldName,
    ForgeFieldType FieldType,
    bool Required,
    string? QuantityKind,
    string Description,
    string? DefaultValue = null,
    IReadOnlyList<string>? AllowedValues = null);

public sealed record ForgeDiagnosticDescriptor(
    string DiagnosticId,
    ForgeDiagnosticSeverity Severity,
    string MessageTemplate,
    string Description);

public sealed record ForgeCapabilityDescriptor(
    string CapabilityId,
    ForgeTrustTier Tier,
    string Description,
    IReadOnlyList<string> RequiredHostFeatures);

public sealed record ForgeLoweringContractDescriptor(
    string ContractId,
    string SourceConceptId,
    string TargetAirFeatureFamily,
    IReadOnlyList<string> RequiredCapabilities,
    string Description);

public sealed record ForgeExampleDescriptor(string ExampleId, string SourcePathOrSnippet, string Description);
public sealed record ForgeFixtureDescriptor(string FixtureId, string Path, string Description);
public sealed record ForgeLlmGuidanceDescriptor(string GuidanceId, string Path, string Description);

public sealed record ForgeDescriptorDiagnostic(string Code, string Path, string Message);

public sealed record ForgeDescriptorValidationResult(IReadOnlyList<ForgeDescriptorDiagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0;
}
