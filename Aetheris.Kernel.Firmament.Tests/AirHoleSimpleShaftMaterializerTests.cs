using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class AirHoleSimpleShaftMaterializerTests
{
    [Fact]
    public void ThroughAllSimpleShaftHole_MaterializesAndPreservesSemanticParentage()
    {
        var feature = ValidFeature(new AirHoleEndCondition.ThroughAll());
        var result = AirHoleSimpleShaftMaterializer.Execute(feature, Host());

        Assert.True(result.Succeeded, string.Join("\n", result.Diagnostics));
        Assert.NotNull(result.Body);
        Assert.NotNull(result.Plan);
        var correspondence = Assert.IsType<SemanticTopologyCorrespondence>(result.Correspondence);
        Assert.Contains(correspondence.Descendants, x => x.Role == SemanticTopologyRole.HoleEntryLoop);
        Assert.Contains(correspondence.Descendants, x => x.Role == SemanticTopologyRole.HoleExitLoop);
        Assert.Contains(correspondence.Descendants, x => x.Role == SemanticTopologyRole.HoleWallFace);
        Assert.Same(feature, result.Plan.SemanticFeature);
        Assert.Equal("hole-001", result.Plan.SemanticFeatureId);
        Assert.Equal(nameof(AirHoleFeature), result.Plan.SemanticSourceKind);
        Assert.Equal(AirHoleEndConditionKind.ThroughAll, result.Plan.EndConditionKind);
        Assert.Equal(1.5, result.Plan.CenterU);
        Assert.Equal(-1, result.Plan.CenterV);
        Assert.Equal(1.5, result.Plan.ProfileStackSpec.CenterX);
        Assert.Equal(-1, result.Plan.ProfileStackSpec.CenterY);
        Assert.Equal(-5, result.Plan.CutZMin);
        Assert.Equal(5, result.Plan.CutZMax);
        Assert.Contains(result.Diagnostics, d => d.Contains("semantic-parent featureId=hole-001", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Contains("ThroughHoleConstructionRecipe", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Contains("no Boolean or temporary tool BRep", StringComparison.Ordinal));
        Assert.Contains("ThroughHoleConstructionRecipe", correspondence.ProvenanceChain);
        Assert.Contains("BrepSurgery", correspondence.ProvenanceChain);
        Assert.DoesNotContain("ProfileStackExtrudeSpec", correspondence.ProvenanceChain);
        Assert.DoesNotContain(result.Diagnostics, d => d.Contains("CylinderCut", StringComparison.OrdinalIgnoreCase));

        var step = Step242Exporter.ExportBody(result.Body!);
        Assert.True(step.IsSuccess);
        Assert.Contains("CYLINDRICAL_SURFACE", step.Value, StringComparison.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP", step.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void DepthSimpleShaftHole_MaterializesBoundedBlindSpanAndPreservesSemanticParentage()
    {
        var feature = ValidFeature(new AirHoleEndCondition.Depth(3));
        var result = AirHoleSimpleShaftMaterializer.Execute(feature, Host());

        Assert.True(result.Succeeded, string.Join("\n", result.Diagnostics));
        Assert.NotNull(result.Plan);
        Assert.Same(feature, result.Plan.SemanticFeature);
        Assert.Equal(AirHoleEndConditionKind.Depth, result.Plan.EndConditionKind);
        Assert.Equal(2, result.Plan.CutZMin);
        Assert.Equal(5, result.Plan.CutZMax);
        Assert.Contains(result.Plan.ProfileStackSpec.Layers, l => !l.InnerCircleRadius.HasValue && l.Role.Contains("solid-before-blind-depth", StringComparison.Ordinal));
        Assert.Contains(result.Plan.ProfileStackSpec.Layers, l => l.InnerCircleRadius == 2 && l.Role.Contains("hole-001", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Contains("ProfileStackExtrudeSpec is lowering furniture", StringComparison.Ordinal));
    }

    [Fact]
    public void UnsupportedPlacement_RejectsDeterministicallyWithoutAnonymousCylinderFallback()
    {
        var feature = AirHoleFeature.CreateSimpleShaft(
            "mount-hole",
            "hole-unsupported",
            "body-main",
            new AirFaceLocalHolePlacement("curved-wall", 0, 0, "face(curved-wall):unsupported", "face(curved-wall)"),
            new AirHoleAxis(Direction3D.Create(new Vector3D(1, 0, 0)), false),
            new AirHoleShaft(4),
            new AirHoleEndCondition.ThroughAll());

        var result = AirHoleSimpleShaftMaterializer.Execute(feature, Host());

        Assert.Equal(AirHoleSimpleShaftMaterializationStatus.UnsupportedPlacement, result.Status);
        Assert.Null(result.Body);
        Assert.Null(result.Plan);
        Assert.Contains(result.Diagnostics, d => d.Contains("only planar top/+Z and bottom/-Z", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Diagnostics, d => d.Contains("CylinderCut", StringComparison.OrdinalIgnoreCase));
    }

    private static AirHoleSimpleShaftHost Host() => new(20, 20, -5, 5);

    private static AirHoleFeature ValidFeature(AirHoleEndCondition endCondition) => AirHoleFeature.CreateSimpleShaft(
        "mount-hole",
        "hole-001",
        "body-main",
        new AirFaceLocalHolePlacement("top", 1.5, -1, "face(top):u=+X,v=+Y", "face(top)"),
        new AirHoleAxis(Direction3D.Create(new Vector3D(0, 0, 1)), true),
        new AirHoleShaft(4),
        endCondition);
}
