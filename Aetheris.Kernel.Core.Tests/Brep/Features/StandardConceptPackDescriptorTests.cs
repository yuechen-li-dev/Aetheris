using Aetheris.Forge.Abstractions;
using Aetheris.Forge.Standard;

namespace Aetheris.Kernel.Core.Tests.Brep.Features;

public sealed class StandardConceptPackDescriptorTests
{
    [Fact]
    public void ValidStandardConceptPackPassesValidation()
    {
        var result = ForgeDescriptorValidator.Validate(StandardConceptPack.CreatePackage());
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Diagnostics));
    }

    [Fact]
    public void StandardConceptIdsAreStableAndUnique()
    {
        var ids = StandardConceptPack.CreatePackage().Concepts.Select(c => c.ConceptId).ToArray();
        Assert.Equal(["Standard.CNC", "Standard.Hole", "Standard.ShaftHole", "Standard.CounterboreHole", "Standard.CountersinkHole", "Standard.EdgeFinish"], ids);
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void StandardHoleTargetsAirHoleFeatureAsMetadata()
    {
        var package = StandardConceptPack.CreatePackage();
        var holeConcepts = package.Concepts.Where(c => c.ConceptId.Contains("Hole", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(holeConcepts);
        foreach (var concept in holeConcepts)
        {
            var contract = Assert.Single(concept.LoweringContracts);
            Assert.Equal("AirHoleFeature", contract.TargetAirFeatureFamily);
            Assert.Contains("no lowerer is executed", contract.Description, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void StandardCncIsProcessConceptAndHasNoBRepLowering()
    {
        var cnc = StandardConceptPack.CreatePackage().Concepts.Single(c => c.ConceptId == "Standard.CNC");
        Assert.Equal("ManufacturingProcess", cnc.Category);
        Assert.Empty(cnc.LoweringContracts);
        Assert.Contains(cnc.ManufacturingAssumptions!, a => a == "processFamily=CNC/prismatic");
        Assert.DoesNotContain(cnc.ManufacturingAssumptions!, a => a.Contains("BRep", StringComparison.OrdinalIgnoreCase) && !a.Contains("no-brep", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StandardCounterboreAndCountersinkAreDistinctStackedHoleConcepts()
    {
        var package = StandardConceptPack.CreatePackage();
        var counterbore = package.Concepts.Single(c => c.ConceptId == "Standard.CounterboreHole");
        var countersink = package.Concepts.Single(c => c.ConceptId == "Standard.CountersinkHole");

        Assert.Contains(counterbore.Fields, f => f.FieldName == "counterboreDepth" && f.FieldType == ForgeFieldType.Length);
        Assert.Contains(countersink.Fields, f => f.FieldName == "countersinkAngle" && f.FieldType == ForgeFieldType.Angle);
        Assert.Contains(counterbore.CapabilityRequirements, c => c == "Standard.StackedHole.Metadata");
        Assert.Contains(countersink.CapabilityRequirements, c => c == "Standard.StackedHole.Metadata");
        Assert.Contains(counterbore.ValidationRuleIds!, id => id == "standard-counterbore-diameter-not-greater-than-shaft");
        Assert.Contains(countersink.ValidationRuleIds!, id => id == "standard-countersink-diameter-not-greater-than-shaft");
    }

    [Fact]
    public void StandardEdgeFinishIsDescriptorOnlyAndNotUnsafe()
    {
        var package = StandardConceptPack.CreatePackage();
        var edgeFinish = package.Concepts.Single(c => c.ConceptId == "Standard.EdgeFinish");
        Assert.Empty(edgeFinish.LoweringContracts);
        Assert.Contains(edgeFinish.ManufacturingAssumptions!, a => a == "lowering-deferred");
        Assert.Equal(ForgeTrustTier.SemanticDocsOnly, package.RequestedTrustTier);
        Assert.DoesNotContain(package.Capabilities, c => c.Tier == ForgeTrustTier.UnsafeNativeExperimental);
    }

    [Fact]
    public void StandardConceptPackDoesNotRequirePluginExecution()
    {
        var package = StandardConceptPack.CreatePackage();
        Assert.Contains("NoPluginExecution", package.HostRequirements);
        Assert.DoesNotContain(package.HostRequirements, h => h.Contains("plugin", StringComparison.OrdinalIgnoreCase) && h != "NoPluginExecution");
        Assert.All(package.Capabilities, c => Assert.NotEqual(ForgeTrustTier.LoweringProvider, c.Tier));
        Assert.All(package.Capabilities, c => Assert.NotEqual(ForgeTrustTier.MaterializerProvider, c.Tier));
    }
}
