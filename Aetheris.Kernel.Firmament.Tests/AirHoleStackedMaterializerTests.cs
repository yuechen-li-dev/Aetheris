using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class AirHoleStackedMaterializerTests
{
    [Fact]
    public void CounterboreThroughAll_MaterializesAsOwnedSemanticStack()
    {
        var feature = Counterbore(new AirHoleEndCondition.ThroughAll(), counterboreDepth: 2);
        var result = AirHoleSimpleShaftMaterializer.Execute(feature, Host());

        Assert.True(result.Succeeded, string.Join("\n", result.Diagnostics));
        Assert.Same(feature, result.Plan!.SemanticFeature);
        Assert.Equal(AirHoleStackKind.Counterbore, result.Plan.StackKind);
        Assert.Equal([AirHoleStackComponentKind.Counterbore, AirHoleStackComponentKind.Shaft], result.Plan.StackComponentRoles);
        Assert.Contains(result.Plan.ProfileStackSpec.Layers, l => l.Role.Contains("counterbore-entry") && l.InnerCircleRadius == 4);
        Assert.Contains(result.Plan.ProfileStackSpec.Layers, l => l.Role.Contains("shaft") && l.InnerCircleRadius == 2);
        Assert.Contains(result.Diagnostics, d => d.Contains("semantic AirHoleFeature", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Diagnostics, d => d.Contains("CylinderCut", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CounterboreDepth_MaterializesDeterministicStackSpans()
    {
        var result = AirHoleSimpleShaftMaterializer.Execute(Counterbore(new AirHoleEndCondition.Depth(4), 1.5), Host());
        Assert.True(result.Succeeded, string.Join("\n", result.Diagnostics));
        Assert.Contains(result.Plan!.ProfileStackSpec.Layers, l => l.Role.Contains("shaft") && l.ZMin == 1 && l.ZMax == 5);
        Assert.Contains(result.Plan.ProfileStackSpec.Layers, l => l.Role.Contains("counterbore-entry") && l.ZMin == 3.5 && l.ZMax == 5);
    }

    [Fact]
    public void CounterboreValidation_RejectsInvalidStackDeterministically()
    {
        Assert.Contains(Counterbore(new AirHoleEndCondition.Depth(4), 1, diameter: 4).Diagnostics, d => d.Code == "hole-x3-counterbore-diameter-invalid");
        Assert.Contains(Counterbore(new AirHoleEndCondition.Depth(4), 0).Diagnostics, d => d.Code == "hole-x3-counterbore-depth-invalid");
        Assert.Contains(Counterbore(new AirHoleEndCondition.Depth(1), 2).Diagnostics, d => d.Code == "hole-x3-counterbore-depth-exceeds-shaft-span");
    }

    [Fact]
    public void CountersinkThroughAll_MaterializesConicalEntryAndOwnedShaftStack()
    {
        var result = AirHoleSimpleShaftMaterializer.Execute(Countersink(new AirHoleEndCondition.ThroughAll(), 90), Host());
        Assert.True(result.Succeeded, string.Join("\n", result.Diagnostics));
        Assert.Equal(AirHoleStackKind.Countersink, result.Plan!.StackKind);
        Assert.Equal([AirHoleStackComponentKind.Countersink, AirHoleStackComponentKind.Shaft], result.Plan.StackComponentRoles);
        Assert.Contains(result.Plan.ProfileStackSpec.Layers, l => l.Role.Contains("countersink-entry") && l.InnerCircleRadius == 4 && l.TopInnerCircleRadius == 2);
        var step = Step242Exporter.ExportBody(result.Body!);
        Assert.True(step.IsSuccess);
        Assert.Contains("CONICAL_SURFACE", step.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void CountersinkDepth_MaterializesDeterministicStackSpans()
    {
        var result = AirHoleSimpleShaftMaterializer.Execute(Countersink(new AirHoleEndCondition.Depth(4), 90), Host());
        Assert.True(result.Succeeded, string.Join("\n", result.Diagnostics));
        Assert.Contains(result.Plan!.ProfileStackSpec.Layers, l => l.Role.Contains("shaft") && l.ZMin == 1 && l.ZMax == 5);
        Assert.Contains(result.Plan.ProfileStackSpec.Layers, l => l.Role.Contains("countersink-entry") && Math.Abs(l.ZMin - 3) < 1e-9 && l.ZMax == 5);
    }

    [Fact]
    public void CountersinkValidation_RejectsInvalidStackDeterministically()
    {
        Assert.Contains(Countersink(new AirHoleEndCondition.Depth(4), 90, entryDiameter: 4).Diagnostics, d => d.Code == "hole-x3-countersink-diameter-invalid");
        Assert.Contains(Countersink(new AirHoleEndCondition.Depth(4), 0).Diagnostics, d => d.Code == "hole-x3-countersink-angle-invalid");
        Assert.Contains(Countersink(new AirHoleEndCondition.Depth(1), 90).Diagnostics, d => d.Code == "hole-x3-countersink-depth-exceeds-shaft-span");
    }

    private static AirHoleSimpleShaftHost Host() => new(20, 20, -5, 5);

    private static AirHoleFeature Counterbore(AirHoleEndCondition endCondition, double counterboreDepth, double diameter = 8) => AirHoleFeature.CreateCounterbore(
        "cb", "hole-cb", "body-main", new AirFaceLocalHolePlacement("top", 0, 0, "face(top):u=+X,v=+Y"),
        new AirHoleAxis(Direction3D.Create(new Vector3D(0, 0, 1)), true), new AirHoleShaft(4), endCondition, new AirHoleCounterboreComponent(diameter, counterboreDepth));

    private static AirHoleFeature Countersink(AirHoleEndCondition endCondition, double angle, double entryDiameter = 8) => AirHoleFeature.CreateCountersink(
        "cs", "hole-cs", "body-main", new AirFaceLocalHolePlacement("top", 0, 0, "face(top):u=+X,v=+Y"),
        new AirHoleAxis(Direction3D.Create(new Vector3D(0, 0, 1)), true), new AirHoleShaft(4), endCondition, new AirHoleCountersinkComponent(entryDiameter, angle));
}
