using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class NestedOccurrenceFrameTests
{
    [Fact]
    public void ParentPlacementAndInternalMatePreserveNestedWorldTransforms()
    {
        var source = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/fixed-nested-usd.firmament");
        var compiled = new AssemblyM1Pipeline().CompileFile(source);
        Assert.True(compiled.IsSuccess, string.Join("\n", compiled.Diagnostics));
        var first = Assert.Single(compiled.Ir!.Instances, i => i.Path.ToString() == "FixedNested.Module.First");
        var second = Assert.Single(compiled.Ir.Instances, i => i.Path.ToString() == "FixedNested.Module.Second");
        Assert.Equal(40, first.ResolvedTransform!.Matrix[12], 9);
        Assert.Equal(10, first.ResolvedTransform.Matrix[13], 9);
        Assert.Equal(3, first.ResolvedTransform.Matrix[14], 9);
        Assert.Equal(40 + 60 / Math.Sqrt(2), second.ResolvedTransform!.Matrix[12], 9);
        Assert.Equal(10 + 60 / Math.Sqrt(2), second.ResolvedTransform.Matrix[13], 9);
        Assert.True(AssemblyIrAp242Exporter.Export(compiled).IsSuccess);
    }

    [Fact]
    public void TranslatedNestedMembersComposeOnceUnderARotatedParent()
    {
        var source=FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/nested-occurrence-frames.firmament");
        var compiled=new AssemblyM1Pipeline().CompileFile(source);
        Assert.True(compiled.IsSuccess,string.Join("\n",compiled.Diagnostics.Select(d=>d.Message)));
        var tip=compiled.Ir!.Instances.Single(i=>i.Path.ToString()=="NestedFrames.Rotated.Offset.Tip");
        // Rz(90) * ([30,0,40] + [5,7,11]) + [100,200,300].
        Assert.Equal(93,tip.ResolvedTransform!.Matrix[12],9);
        Assert.Equal(235,tip.ResolvedTransform.Matrix[13],9);
        Assert.Equal(351,tip.ResolvedTransform.Matrix[14],9);
        var mesh=AssemblyDisplayMeshExporter.Export(compiled);
        Assert.Equal(tip.ResolvedTransform.Matrix,mesh.Occurrences.Single(i=>i.Id==tip.StableId).Transform);
        Assert.Equal(2,mesh.Definitions.Count);
        Assert.True(AssemblyIrAp242Exporter.Export(compiled).IsSuccess);
    }
}
