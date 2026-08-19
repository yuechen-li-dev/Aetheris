using Aetheris.Kernel.Firmament.Assembly;
using Xunit;

namespace Aetheris.SheetMetal.Tests;

public sealed class SheetMetalM7Tests
{
    private static EnclosureProductSpec Canonical => new(160, 110, 36, 1.2, 1.2, 1.5, 1, 9);

    [Fact]
    public void NetworkAppliance_UsesRegisteredSemanticFramesAndCorrectedPlacement()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "fixtures/Compatibility/Firmasm/SheetMetal/network-appliance-product-m7.firmasm"));
        var product = EnclosureProductFamilies.Compile(source, Canonical, "m7-network-appliance-product.firmasm");
        var definition = Assert.Single(product.Assembly.Ir!.AssemblyDefinitions!);
        var solution = Assert.Single(definition.LocalDatumMateSolutions!);
        Assert.Equal(DatumOrientationRelation.OpposedDirection, solution.Orientation);
        Assert.Equal(6, solution.ConstrainedDegreesOfFreedom);
        var lid = product.Assembly.Ir.Instances.Single(item => item.Path.Segments.Last() == "Lid");
        Assert.Equal(PlacementAuthority.MateDerived, lid.PlacementAuthority);
        Assert.Equal(-1, lid.ResolvedTransform!.Matrix[12], 6);
        Assert.Equal(-1, lid.ResolvedTransform.Matrix[13], 6);
        Assert.Equal(41.4, lid.ResolvedTransform.Matrix[14], 5);
        Assert.Contains(definition.LocalDatums!, item => item.SemanticPath.EndsWith("Body.Datums.LidSeat", StringComparison.Ordinal));
        Assert.Contains(definition.LocalDatums!, item => item.SemanticPath.EndsWith("Lid.Datums.BodySeat", StringComparison.Ordinal));
        if (Environment.GetEnvironmentVariable("AETHERIS_M7_ARTIFACTS") is { } artifactDirectory)
            product.Export(artifactDirectory);
    }

    [Fact]
    public void M6ReferenceFixture_ReproducesDetachedUnderregisteredClosure()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "fixtures/Compatibility/Firmasm/SheetMetal/network-appliance-product-m6.firmasm"));
        var product = EnclosureProductFamilies.Compile(source, Canonical, "m6-reference-network-appliance.firmasm");
        var definition = Assert.Single(product.Assembly.Ir!.AssemblyDefinitions!);
        Assert.Empty(definition.LocalDatumMateSolutions ?? []);
        var localLid = definition.LocalInstances.Single(item => item.Path.Segments.Last() == "Lid");
        Assert.Equal(36, localLid.ResolvedTransform!.Matrix[14], 6);
        var bodyGeometry = product.Assembly.Geometry!.Artifact.Instances.Single(item => item.InstanceStableId.EndsWith(".Body", StringComparison.Ordinal));
        Assert.Equal(41.4, bodyGeometry.Metrics.Maximum[2], 6);
        Assert.Equal(5.4, bodyGeometry.Metrics.Maximum[2] - localLid.ResolvedTransform.Matrix[14], 6);
    }

    [Fact]
    public void ManufacturingVariation_ChangesFitWithoutChangingNominalGeometry()
    {
        var baseline = EnclosureProductFamilies.MakeEnclosureProduct(Canonical, "BaselineFit");
        var coated = EnclosureProductFamilies.MakeEnclosureProduct(Canonical with
        {
            ProductionVariation = new(CoatingThickness: .25, CoatingThicknessTolerance: .05)
        }, "CoatedFit");
        Assert.Equal(FitClassification.GuaranteedClearance, baseline.Fit.VariationEnvelopeState);
        Assert.Equal(FitClassification.PossibleInterference, coated.Fit.VariationEnvelopeState);
        Assert.Equal(baseline.Assembly.Geometry!.Artifact.Definitions.Select(item => item.StepSha256).Order(), coated.Assembly.Geometry!.Artifact.Definitions.Select(item => item.StepSha256).Order());
        Assert.Contains(coated.Fit.DominantContributions, item => item.Source.Contains("coated", StringComparison.Ordinal));
    }

    [Fact]
    public void FitClassification_CoversGuaranteedClearancePossibleAndGuaranteedInterference()
    {
        var clear = EnclosureProductFamilies.MakeEnclosureProduct(Canonical with
        {
            LidClearance = 2,
            ProductionVariation = new(.05, .02, .1, .02, 0, 0)
        }, "LooseFit");
        var possible = EnclosureProductFamilies.MakeEnclosureProduct(Canonical with
        {
            ProductionVariation = new(CoatingThickness: .25, CoatingThicknessTolerance: .05)
        }, "RiskFit");
        var interference = EnclosureProductFamilies.MakeEnclosureProduct(Canonical with
        {
            LidClearance = .2,
            ProductionVariation = new(.05, .02, .1, .02, 1, .05)
        }, "InterferenceFit");
        Assert.Equal(FitClassification.GuaranteedClearance, clear.Fit.VariationEnvelopeState);
        Assert.Equal(FitClassification.PossibleInterference, possible.Fit.VariationEnvelopeState);
        Assert.Equal(FitClassification.GuaranteedInterference, interference.Fit.VariationEnvelopeState);
        Assert.True(interference.Fit.MaximumPenetrationMm > 0);
    }

    private static string Root() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent; return directory?.FullName ?? throw new InvalidOperationException(); }
}
