using Aetheris.Forge.Abstractions;

namespace Aetheris.Forge.Examples;

public static class StandardHoleForgeDescriptor
{
    public static ForgePackageDescriptor CreatePackage() => new(
        PackageId: "Aetheris.Standard",
        DisplayName: "Aetheris Standard Semantic Concepts",
        SemanticVersion: "0.1.0",
        Vendor: "Aetheris",
        Description: "Descriptor-only fixture for standard semantic CAD concepts.",
        RequestedTrustTier: ForgeTrustTier.SemanticDocsOnly,
        HostRequirements: ["Aetheris.Forge.Abstractions"],
        Concepts: [CreateConcept()],
        Templates: [],
        Capabilities: [new ForgeCapabilityDescriptor("Standard.Hole.LoweringMetadata", ForgeTrustTier.LoweringProvider, "Metadata describing the allowed Standard.Hole AIR lowering target; no lowerer is executed.", ["AirHoleFeature"])],
        Examples: [new ForgeExampleDescriptor("standard-hole-minimal", "hole(entryFace: face, center: uv, shaftDiameter: 5mm, endCondition: through)", "Minimal Standard.Hole descriptor source snippet.")],
        Fixtures: [new ForgeFixtureDescriptor("standard-hole-validation-fixture", "fixtures/forge/standard-hole.json", "Descriptor validation fixture path; existence is not required by FORGE-X1.")],
        LlmGuidance: [new ForgeLlmGuidanceDescriptor("standard-hole-llm-guidance", "docs/llm/standard-hole.md", "Guidance link for authoring Standard.Hole semantics.")]);

    public static ForgeConceptDescriptor CreateConcept() => new(
        ConceptId: "Standard.Hole",
        DisplayName: "Standard Hole",
        Category: "Hole",
        Description: "Descriptor-only semantic concept for a simple hole. This does not change Standard Library behavior.",
        Fields:
        [
            new ForgeFieldDescriptor("entryFace", ForgeFieldType.FaceSelector, true, null, "Face receiving the hole."),
            new ForgeFieldDescriptor("center", ForgeFieldType.FaceLocalPoint2D, true, null, "Face-local hole center."),
            new ForgeFieldDescriptor("shaftDiameter", ForgeFieldType.Length, true, "Length", "Nominal shaft diameter."),
            new ForgeFieldDescriptor("endCondition", ForgeFieldType.HoleEndCondition, true, null, "Through, blind, or other descriptor-level end condition metadata.")
        ],
        Diagnostics:
        [
            new ForgeDiagnosticDescriptor("standard-hole-missing-entry-face", ForgeDiagnosticSeverity.Error, "Standard.Hole requires an entry face.", "Raised when entryFace metadata is absent."),
            new ForgeDiagnosticDescriptor("standard-hole-invalid-center", ForgeDiagnosticSeverity.Error, "Standard.Hole center must be valid on the entry face.", "Raised when center metadata is invalid."),
            new ForgeDiagnosticDescriptor("standard-hole-invalid-diameter", ForgeDiagnosticSeverity.Error, "Standard.Hole shaftDiameter must be positive.", "Raised when shaftDiameter metadata is invalid."),
            new ForgeDiagnosticDescriptor("standard-hole-invalid-end-condition", ForgeDiagnosticSeverity.Error, "Standard.Hole endCondition is unsupported.", "Raised when endCondition metadata is invalid.")
        ],
        CapabilityRequirements: ["Standard.Hole.LoweringMetadata"],
        LoweringContracts:
        [
            new ForgeLoweringContractDescriptor("Standard.Hole.ToAirHoleFeature", "Standard.Hole", "AirHoleFeature", ["Standard.Hole.LoweringMetadata"], "Metadata contract permitting Standard.Hole to describe AirHoleFeature lowering targets without executing lowering.")
        ],
        Examples: [],
        Fixtures: [],
        LlmGuidance: [],
        ManufacturingAssumptions: ["Descriptor scaffold only; no standards-table or tap/thread geometry assumptions are encoded."],
        DerivedFields: [],
        ValidationRuleIds: ["standard-hole-missing-entry-face", "standard-hole-invalid-center", "standard-hole-invalid-diameter", "standard-hole-invalid-end-condition"]);
}
