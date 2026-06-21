using Aetheris.Forge.Abstractions;
using Aetheris.Forge.Examples;

namespace Aetheris.Kernel.Core.Tests.Brep.Features;

public sealed class ForgeDescriptorValidationTests
{
    [Fact]
    public void StandardHolePackageDescriptor_IsValid()
    {
        var result = ForgeDescriptorValidator.Validate(StandardHoleForgeDescriptor.CreatePackage());
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Diagnostics));
    }

    [Fact]
    public void MissingPackageId_FailsDeterministically()
    {
        var package = StandardHoleForgeDescriptor.CreatePackage() with { PackageId = "" };
        AssertCodes(package, "forge.package.missing-id");
    }

    [Fact]
    public void DuplicateConceptIds_FailDeterministically()
    {
        var concept = StandardHoleForgeDescriptor.CreateConcept();
        var package = StandardHoleForgeDescriptor.CreatePackage() with { Concepts = [concept, concept] };
        AssertCodes(package, "forge.package.duplicate-concept-id");
    }

    [Fact]
    public void MissingConceptId_FailsDeterministically()
    {
        var concept = StandardHoleForgeDescriptor.CreateConcept() with { ConceptId = "" };
        var package = StandardHoleForgeDescriptor.CreatePackage() with { Concepts = [concept] };
        AssertCodes(package, "forge.concept.missing-id");
    }

    [Fact]
    public void DuplicateFieldNames_FailDeterministically()
    {
        var field = new ForgeFieldDescriptor("entryFace", ForgeFieldType.FaceSelector, true, null, "Duplicate test field.");
        var concept = StandardHoleForgeDescriptor.CreateConcept() with { Fields = [field, field] };
        var package = StandardHoleForgeDescriptor.CreatePackage() with { Concepts = [concept] };
        AssertCodes(package, "forge.concept.duplicate-field-name");
    }

    [Fact]
    public void MissingDiagnosticId_FailsDeterministically()
    {
        var diagnostic = new ForgeDiagnosticDescriptor("", ForgeDiagnosticSeverity.Error, "message", "description");
        var concept = StandardHoleForgeDescriptor.CreateConcept() with { Diagnostics = [diagnostic] };
        var package = StandardHoleForgeDescriptor.CreatePackage() with { Concepts = [concept] };
        AssertCodes(package, "forge.diagnostic.missing-id");
    }

    [Fact]
    public void UnknownTemplateReferencedConcept_FailsDeterministically()
    {
        var template = new ForgeTemplateDescriptor(
            "Standard.HoleTemplate",
            "Hole Template",
            "Template references a missing concept for validation coverage.",
            Parameters: [],
            ConceptRequirements: ["Standard.MissingConcept"],
            ExpandsToConceptIds: [],
            Diagnostics: [],
            Examples: [],
            Fixtures: [],
            LlmGuidance: []);
        var package = StandardHoleForgeDescriptor.CreatePackage() with { Templates = [template] };
        AssertCodes(package, "forge.template.unknown-concept-id");
    }

    [Fact]
    public void LoweringContractMetadata_TargetsAirHoleFeatureWithoutExecution()
    {
        var contract = StandardHoleForgeDescriptor.CreateConcept().LoweringContracts.Single();
        Assert.Equal("AirHoleFeature", contract.TargetAirFeatureFamily);
        Assert.Equal("Standard.Hole.ToAirHoleFeature", contract.ContractId);
    }

    [Fact]
    public void TrustTierNamesAndValues_AreStableAndValidated()
    {
        Assert.Equal(1, (int)ForgeTrustTier.SemanticDocsOnly);
        Assert.Equal(2, (int)ForgeTrustTier.ValidationDerivation);
        Assert.Equal(3, (int)ForgeTrustTier.LoweringProvider);
        Assert.Equal(4, (int)ForgeTrustTier.MaterializerProvider);
        Assert.Equal(5, (int)ForgeTrustTier.UnsafeNativeExperimental);

        var package = StandardHoleForgeDescriptor.CreatePackage() with { RequestedTrustTier = (ForgeTrustTier)99 };
        AssertCodes(package, "forge.trust-tier.invalid");
    }

    private static void AssertCodes(ForgePackageDescriptor package, params string[] expectedCodes)
    {
        var result = ForgeDescriptorValidator.Validate(package);
        var codes = result.Diagnostics.Select(d => d.Code).ToArray();
        Assert.False(result.IsValid);
        Assert.Equal(codes.Order(StringComparer.Ordinal), codes);
        foreach (var expectedCode in expectedCodes)
            Assert.Contains(expectedCode, codes);
    }
}
