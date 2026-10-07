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
        var edges = Assert.Single(packet.Definitions).EdgePolylines!;
        Assert.Equal(body.Topology.Edges.Count(), edges.Count);
        Assert.Equal(imported.Value.Definitions.Single(d => d.Geometry is not null).Geometry!.Topology.Edges.Select(e => e.Id.Value).Order(),
            edges.Select(e => e.EdgeId).Order());
        var geometry = imported.Value.Definitions.Single(d => d.Geometry is not null).Geometry!;
        Assert.Equal(geometry.Topology.Faces.Count(), packet.Definitions[0].FaceSources!.Count);
        foreach (var sourceFace in packet.Definitions[0].FaceSources!)
        {
            var faceId = new Aetheris.Kernel.Core.Topology.FaceId(sourceFace.FaceId);
            Assert.Equal(geometry.Bindings.GetFaceBinding(faceId).SourceStepEntityId, sourceFace.SourceStepEntityId);
            Assert.Equal(geometry.GetLoopIds(faceId).SelectMany(geometry.GetCoedgeIds)
                .Select(id => geometry.Topology.GetCoedge(id).EdgeId.Value).Distinct().Order(), sourceFace.EdgeIds.Order());
        }
        Assert.Equal(4, packet.Occurrences.Count);
        Assert.Equal("sub", packet.Occurrences.Single(item => item.StableId == "first").ParentStableId);
        Assert.Equal("part-def", packet.Occurrences.Single(item => item.StableId == "second").DefinitionStableId);
        Assert.Equal(10, packet.Occurrences.Single(item => item.StableId == "first").WorldTransform[12], 8);
        Assert.Equal(3, packet.Occurrences.Single(item => item.StableId == "first").WorldTransform[13], 8);
        Assert.Equal(8, packet.Occurrences.Single(item => item.StableId == "second").WorldTransform[13], 8);
        Assert.True(packet.Bounds.Maximum[1] > packet.Bounds.Minimum[1]);
    }
}
