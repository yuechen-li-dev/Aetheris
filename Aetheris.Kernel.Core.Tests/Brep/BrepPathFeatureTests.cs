using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Brep;

public sealed class BrepPathFeatureTests
{
    private static readonly BrepMassPropertiesOptions Accuracy = new(.002, double.Pi / 180, .01, 64, 128);
    [Theory]
    [InlineData(0, 8, 0, "dimensions")]
    [InlineData(3, 8, 30, "draft-collapse")]
    [InlineData(3, 8, -1, "dimensions")]
    [InlineData(100, 8, 0, "support")]
    public void UnconstructibleRibDoesNotProduceABody(double thickness, double height, double draft, string code)
    {
        var feature = new PlanarPathFeature("Web", true, PathSectionKind.Rectangular,
            new(PlanarGuideKind.Line, new(-10,0), new(10,0), default), thickness, height, draft);
        var rejected = BrepPlanarPathFeature.BuildBox(40, 30, 5, [feature]);
        Assert.False(rejected.IsSuccess); Assert.Contains(rejected.Diagnostics, d => d.Message.Contains(code, StringComparison.Ordinal));
    }
    [Fact]
    public void CutsCannotRemoveTheirFloorOrCrossEachOther()
    {
        var deep = new PlanarPathFeature("Cut", false, PathSectionKind.Rectangular,
            new(PlanarGuideKind.Line, new(-10,0), new(10,0), default), 3, 5);
        Assert.False(BrepPlanarPathFeature.BuildBox(40,30,5,[deep]).IsSuccess);
        Assert.False(BrepCircumferentialGroove.BuildCylinder(15,40,[new("Bore", PathSectionKind.Rectangular,4,5,0,true)],5).IsSuccess);
        Assert.False(BrepCircumferentialGroove.BuildCylinder(15,40,[
            new("A", PathSectionKind.Rectangular,4,1,0),new("B",PathSectionKind.Rectangular,4,1,1)]).IsSuccess);
        Assert.False(BrepCircumferentialGroove.BuildCylinder(15,40,[new("Unknown",(PathSectionKind)1234,4,1,0)]).IsSuccess);
    }
    [Theory]
    [InlineData(false, false, PathSectionKind.Rectangular)]
    [InlineData(false, false, PathSectionKind.Semicircular)]
    [InlineData(true, false, PathSectionKind.Rectangular)]
    [InlineData(true, false, PathSectionKind.Semicircular)]
    [InlineData(true, true, PathSectionKind.Rectangular)]
    [InlineData(true, true, PathSectionKind.Semicircular)]
    public void CylinderGrooveRetainsTheStockAndCaps(bool hollow, bool inner, PathSectionKind section)
    {
        var built = BrepCircumferentialGroove.BuildCylinder(15, 40,
            [new("Seal", section, 4, 2, 0, inner)], hollow ? 5 : null);
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => d.Message)));
        var mass = BrepMassProperties.Evaluate(built.Value.Body, Accuracy);
        var expected = double.Pi * 15 * 15 * 40 - (hollow ? double.Pi * 10 * 10 * 35 : 0) + built.Value.MaterialVolumeDelta;
        Assert.True(mass.IsEnclosed && mass.IsOrientationConsistent, string.Join("; ", mass.Diagnostics));
        Assert.InRange(mass.SignedVolume, expected * .999, expected * 1.001);
        var stockVolume = expected - built.Value.MaterialVolumeDelta;
        var deltaTolerance = double.Abs(built.Value.MaterialVolumeDelta) * .03;
        Assert.InRange(mass.SignedVolume - stockVolume, built.Value.MaterialVolumeDelta - deltaTolerance, built.Value.MaterialVolumeDelta + deltaTolerance);
        var step = Step242Exporter.ExportBody(built.Value.Body, new Step242ExportOptions { BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute });
        Assert.True(step.IsSuccess, string.Join("; ", step.Diagnostics.Select(d => d.Message)));
        var again = Step242Importer.ImportBody(step.Value);
        Assert.True(again.IsSuccess, string.Join("; ", again.Diagnostics.Select(d => d.Message)));
        var after = BrepMassProperties.Evaluate(again.Value, Accuracy);
        Assert.True(after.IsEnclosed && after.IsOrientationConsistent);
        Assert.InRange(after.SignedVolume / mass.SignedVolume, .999, 1.001);
    }
    [Theory]
    [InlineData(PlanarGuideKind.Line, true, PathSectionKind.Rectangular, false)]
    [InlineData(PlanarGuideKind.Line, true, PathSectionKind.Rectangular, true)]
    [InlineData(PlanarGuideKind.Arc, true, PathSectionKind.Rectangular, false)]
    [InlineData(PlanarGuideKind.Circle, true, PathSectionKind.Rectangular, false)]
    [InlineData(PlanarGuideKind.Line, false, PathSectionKind.Rectangular, false)]
    [InlineData(PlanarGuideKind.Line, false, PathSectionKind.Rectangular, true)]
    [InlineData(PlanarGuideKind.Arc, false, PathSectionKind.Rectangular, false)]
    [InlineData(PlanarGuideKind.Circle, false, PathSectionKind.Rectangular, false)]
    [InlineData(PlanarGuideKind.Line, false, PathSectionKind.Semicircular, false)]
    [InlineData(PlanarGuideKind.Line, false, PathSectionKind.Semicircular, true)]
    [InlineData(PlanarGuideKind.Arc, false, PathSectionKind.Semicircular, false)]
    [InlineData(PlanarGuideKind.Circle, false, PathSectionKind.Semicircular, false)]
    public void LocalFeatureIsOneClosedAnalyticBody(PlanarGuideKind kind, bool add, PathSectionKind section, bool round)
    {
        var guide = new PlanarFeatureGuide(kind, new(-10, 0), new(10, 0), new(0, 0), 10, 0, double.Pi / 2);
        var feature = new PlanarPathFeature("Feature", add, section, guide, 4, section == PathSectionKind.Semicircular ? 2 : add ? 8 : 2, add ? 2 : 0, round);
        var built = BrepPlanarPathFeature.BuildBox(60, 50, 10, [feature]);
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => d.Message)));
        var body = built.Value.Body;
        var mass = BrepMassProperties.Evaluate(body, Accuracy);
        Assert.True(mass.IsEnclosed && mass.IsOrientationConsistent, string.Join("; ", mass.Diagnostics));
        Assert.InRange(mass.SignedVolume, (30000 + built.Value.MaterialVolumeDelta) * .995, (30000 + built.Value.MaterialVolumeDelta) * 1.005);
        // Bound the feature's effect, not just the much larger stock volume:
        // omitting a small channel must not fit inside the test tolerance.
        var deltaTolerance = double.Abs(built.Value.MaterialVolumeDelta) * .02;
        Assert.InRange(mass.SignedVolume - 30000, built.Value.MaterialVolumeDelta - deltaTolerance, built.Value.MaterialVolumeDelta + deltaTolerance);
        Assert.All(body.Topology.Edges, edge => Assert.Equal(2, body.Topology.Coedges.Count(c => c.EdgeId == edge.Id)));
        if (kind == PlanarGuideKind.Line && section == PathSectionKind.Semicircular && !round)
            Assert.False(BrepMassProperties.TryEvaluateExactVolume(body, out _)); // Must not masquerade as a convex quarter-cylinder fillet.
        Assert.All(body.Geometry.Surfaces, s => Assert.Null(s.Value.BSplineSurfaceWithKnots));
        var exported = Step242Exporter.ExportBody(body, new Step242ExportOptions { BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute });
        Assert.True(exported.IsSuccess, string.Join("; ", exported.Diagnostics.Select(d => d.Message)));
        var imported = Step242Importer.ImportBody(exported.Value);
        Assert.True(imported.IsSuccess, string.Join("; ", imported.Diagnostics.Select(d => d.Message)));
        var again = BrepMassProperties.Evaluate(imported.Value, Accuracy);
        Assert.True(again.IsEnclosed && again.IsOrientationConsistent, string.Join("; ", again.Diagnostics));
        Assert.InRange(again.SignedVolume / mass.SignedVolume, .999, 1.001);
    }
}
