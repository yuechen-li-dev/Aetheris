using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Server.Api;

namespace Aetheris.Server.Tests;

public sealed class StepAssemblyDisplayTests
{
    [Fact]
    public void ExportedNestedRepeatedAssembly_BuildsSharedCadmataSceneWithWorldPlacement()
    {
        var body = BrepPrimitives.CreateBox(2, 4, 6).Value;
        double[] identity = [1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1];
        double[] parent = [0,1,0,0, -1,0,0,0, 0,0,1,0, 10,0,0,1];
        double[] first = [1,0,0,0, 0,1,0,0, 0,0,1,0, 3,0,0,1];
        double[] second = [1,0,0,0, 0,1,0,0, 0,0,1,0, 8,0,0,1];
        var source = new Step242AssemblyExportModel("Fixture", "root", [new("part-def", "Shared Part", body)], [
            new("root", "Fixture", null, null, identity),
            new("sub", "Rotated subassembly", "root", null, parent),
            new("first", "First", "sub", "part-def", first),
            new("second", "Second", "sub", "part-def", second)
        ]);
        var step = Step242AssemblyExporter.Export(source);
        Assert.True(step.IsSuccess);
        var imported = Step242AssemblyImporter.Import(step.Value);
        Assert.True(imported.IsSuccess, string.Join("; ", imported.Diagnostics.Select(item => item.Message)));

        var built = AssemblyDisplayService.TryBuildStep(imported.Value, out var packet, out var error);

        Assert.True(built, error);
        Assert.NotNull(packet);
        Assert.Single(packet.Definitions);
        Assert.Equal(4, packet.Occurrences.Count);
        Assert.Equal("sub", packet.Occurrences.Single(item => item.StableId == "first").ParentStableId);
        Assert.Equal("part-def", packet.Occurrences.Single(item => item.StableId == "second").DefinitionStableId);
        Assert.Equal(10, packet.Occurrences.Single(item => item.StableId == "first").WorldTransform[12], 8);
        Assert.Equal(3, packet.Occurrences.Single(item => item.StableId == "first").WorldTransform[13], 8);
        Assert.Equal(8, packet.Occurrences.Single(item => item.StableId == "second").WorldTransform[13], 8);
        Assert.True(packet.Bounds.Maximum[1] > packet.Bounds.Minimum[1]);
    }
}
