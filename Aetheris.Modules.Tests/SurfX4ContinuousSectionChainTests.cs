using Aetheris.Kernel.Core.Step242;
using Aetheris.Surfacing;
using Xunit;

namespace Aetheris.Modules.Tests;

public sealed class SurfX4ContinuousSectionChainTests
{
    [Fact]
    public void ErgonomicFlagshipIsVerifiedG1AndExportsWithoutRationalSurfaces()
    {
        var chain = SectionChainTemplates.ErgonomicFairingG1();
        var result = SectionChainMaterializer.Materialize(chain);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Equal(SectionChainContinuity.G1, chain.Continuity);
        Assert.Equal(3, result.SmoothSelection!.CandidateCount);
        Assert.True(result.ContinuityEvidence!.MaximumPositionError < 1e-10d);
        Assert.True(result.ContinuityEvidence.MaximumTangentPlaneAngleDegrees < 1e-3d);
        Assert.All(result.Body!.Geometry.Surfaces.Where(pair => pair.Value.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.BSplineSurfaceWithKnots),
            pair => Assert.NotNull(pair.Value.BSplineSurfaceWithKnots));
        var step = Step242Exporter.ExportBody(result.Body);
        Assert.True(step.IsSuccess, string.Join(Environment.NewLine, step.Diagnostics.Select(item => item.Message)));
        Assert.DoesNotContain("RATIONAL_B_SPLINE_SURFACE", step.Value, StringComparison.Ordinal);
        Assert.True(Step242Importer.ImportBody(step.Value).IsSuccess);
    }

    [Fact]
    public void ExplicitRuledRemainsG0AndMiddleEditHasBoundedG1Stencil()
    {
        var g0 = SectionChainTemplates.ErgonomicFairing("g0", smooth: false);
        Assert.True(SectionChainMaterializer.Materialize(g0).IsSuccess);
        Assert.Equal(SectionChainContinuity.G0, g0.Continuity);

        var g1 = SectionChainTemplates.ErgonomicFairingG1();
        var replacement = g1.Sections[3] with { Frame = g1.Sections[3].Frame with { Origin = g1.Sections[3].Frame.Origin + new Aetheris.Kernel.Core.Math.Vector3D(0, 1, 0) } };
        var edit = SectionChainEditor.ReplaceSection(g1, replacement).Delta;
        Assert.Equal(["PalmFront", "Rise", "Peak"], edit.RecomputedTangentFields);
        Assert.Equal(["Front->PalmFront", "PalmFront->Rise", "Rise->Peak", "Peak->PalmRear"], edit.RebuiltTransitions);
        Assert.Contains("Nose->Front", edit.PreservedTransitions);
        Assert.Contains("PalmRear->Rear", edit.PreservedTransitions);
    }

    [Fact]
    public void SameSemanticInputProducesIdenticalStep()
    {
        var chain = SectionChainTemplates.ErgonomicFairingG1();
        var first = Step242Exporter.ExportBody(SectionChainMaterializer.Materialize(chain).Body!).Value;
        var second = Step242Exporter.ExportBody(SectionChainMaterializer.Materialize(chain).Body!).Value;
        Assert.Equal(first, second);
    }
}
