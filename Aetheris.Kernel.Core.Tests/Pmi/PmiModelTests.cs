using Aetheris.Kernel.Core.Pmi;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Pmi;

public sealed class PmiModelTests
{
    [Fact]
    public void PmiModel_CanDeclarePlanarDatumFeature()
    {
        var model = PmiModel.Empty("part_main")
            .AddDatum(new PmiDatumFeature(
                "datum:A",
                "A",
                PmiDatumFeatureKind.Planar,
                new PmiPlanarFaceReference("base", "base.top")));

        var datum = Assert.Single(model.DatumFeatures);
        Assert.Equal("A", datum.Label);
        Assert.Equal(PmiDatumFeatureKind.Planar, datum.Kind);
        var planar = Assert.IsType<PmiPlanarFaceReference>(datum.Target);
        Assert.Equal("base.top", planar.Selector);
    }

    [Fact]
    public void PmiModel_CanRepresentLinearDistanceToDatumPlane()
    {
        var model = PmiModel.Empty("part_main")
            .AddDatum(new PmiDatumFeature(
                "datum:A",
                "A",
                PmiDatumFeatureKind.Planar,
                new PmiPlanarFaceReference("base", "base.top")))
            .AddDimension(new PmiDimension(
                "distance:hole_to_A",
                PmiDimensionKind.LinearDistanceToDatum,
                new PmiCylindricalFeatureReference("main_hole", "through_or_blind_cylindrical"),
                new PmiDatumReference("datum:A"),
                12.5d,
                SourceTag: "distance-to-datum"));

        var dimension = Assert.Single(model.Dimensions);
        Assert.Equal(PmiDimensionKind.LinearDistanceToDatum, dimension.Kind);
        Assert.IsType<PmiCylindricalFeatureReference>(dimension.PrimaryReference);
        var datumRef = Assert.IsType<PmiDatumReference>(dimension.SecondaryReference);
        Assert.Equal("datum:A", datumRef.DatumId);
    }

    [Fact]
    public void PmiStep242Adapter_ConvertsPlanarDatumAndDiameterDimension_ToLegacyStepPayload()
    {
        var model = PmiModel.Empty("part_main")
            .AddDatum(new PmiDatumFeature(
                "datum:A",
                "A",
                PmiDatumFeatureKind.Planar,
                new PmiPlanarFaceReference("base", "base.top")))
            .AddDimension(new PmiDimension(
                "diameter:hole_1",
                PmiDimensionKind.Diameter,
                new PmiCylindricalFeatureReference("hole_1", "through_or_blind_cylindrical"),
                null,
                10d,
                SourceTag: "legacy-auto-hole-demo"));

        var converted = PmiStep242Adapter.ToStep242SemanticPmi(model);

        var datum = Assert.IsType<Step242SemanticPmiDatum>(Assert.Single(converted.OfType<Step242SemanticPmiDatum>()));
        Assert.Equal("A", datum.Label);
        Assert.Equal("plane", datum.DatumKind);

        var hole = Assert.IsType<Step242SemanticPmiHole>(Assert.Single(converted.OfType<Step242SemanticPmiHole>()));
        Assert.Equal("hole_1", hole.FeatureId);
        Assert.Equal(10d, hole.Diameter);
    }
}
