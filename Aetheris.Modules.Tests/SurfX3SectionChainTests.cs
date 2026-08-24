using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Surfacing;
using Xunit;

namespace Aetheris.Modules.Tests;

public sealed class SurfX3SectionChainTests
{
    [Fact]
    public void FlagshipMaterializesEightOrderedSectionsAsOneDeterministicClosedBody()
    {
        var first = SectionChainMaterializer.Materialize(SectionChainTemplates.ErgonomicFairing());
        var second = SectionChainMaterializer.Materialize(SectionChainTemplates.ErgonomicFairing());

        Assert.True(first.IsSuccess, string.Join("; ", first.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        Assert.Equal(8, first.Chain.Sections.Count);
        Assert.Equal(7, first.Transitions.Count);
        Assert.Equal(SectionChainStructureKind.ClosedSolid, first.StructureKind);
        Assert.Equal(30, first.Body!.Topology.Faces.Count());
        Assert.All(first.Body.Topology.Coedges.GroupBy(coedge => coedge.EdgeId), uses => Assert.Equal(2, uses.Count()));
        Assert.NotNull(first.Pcurves);
        Assert.Equal(first.Body.Topology.Coedges.Count(), first.Pcurves!.PcurveCount);
        Assert.True(first.Pcurves.LoopClosureValid);
        Assert.True(first.SelfIntersection!.Passed);

        var exportedA = Step242Exporter.ExportBody(first.Body);
        var exportedB = Step242Exporter.ExportBody(second.Body!);
        Assert.True(exportedA.IsSuccess, string.Join("; ", exportedA.Diagnostics.Select(item => item.Message)));
        Assert.Equal(Hash(exportedA.Value), Hash(exportedB.Value));
        Assert.Contains("B_SPLINE_SURFACE_WITH_KNOTS", exportedA.Value);
        Assert.DoesNotContain("RATIONAL_B_SPLINE_SURFACE", exportedA.Value);
        Assert.True(Step242Importer.ImportBody(exportedA.Value).IsSuccess);
    }

    [Fact]
    public void MixedLineToSplineSpanUsesExactNonRationalRuledSurface()
    {
        var result = SectionChainMaterializer.Materialize(SectionChainTemplates.ErgonomicFairing());
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(item => item.Message)));
        var mixedTransition = result.Transitions[1];
        Assert.All(mixedTransition.Surfaces, surface =>
        {
            Assert.Equal(SurfaceGeometryKind.BSplineSurfaceWithKnots, surface.SurfaceClass);
            Assert.Equal(SurfaceMaterializationKind.ExactPolynomialBSpline, surface.MaterializationKind);
        });
    }

    [Fact]
    public void PlanarLinePairsAreRecognizedAsAnalyticPlanes()
    {
        var result = SectionChainMaterializer.Materialize(SectionChainTemplates.TwoProfileRuled());
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(item => item.Message)));
        Assert.All(result.Transitions.SelectMany(transition => transition.Surfaces),
            surface => Assert.Equal(SurfaceGeometryKind.Plane, surface.SurfaceClass));
    }

    [Fact]
    public void TwistIsFrameDrivenAndDeterministic()
    {
        var first = SectionChainMaterializer.Materialize(SectionChainTemplates.TwistWitness());
        var second = SectionChainMaterializer.Materialize(SectionChainTemplates.TwistWitness());
        Assert.True(first.IsSuccess, string.Join("; ", first.Diagnostics.Select(item => item.Message)));
        Assert.Equal(first.Transitions.Select(Canonical), second.Transitions.Select(Canonical));
        Assert.NotEqual(first.Chain.Sections[0].Frame.XAxis, first.Chain.Sections[^1].Frame.XAxis);
    }

    [Fact]
    public void ReplacingInteriorSectionRebuildsOnlyAdjacentTransitions()
    {
        var source = SectionChainTemplates.ErgonomicFairing();
        var old = source.Sections[3];
        var replacement = old with { Frame = old.Frame with { Origin = old.Frame.Origin + new Vector3D(0, 2, 0) } };
        var edited = SectionChainEditor.ReplaceSection(source, replacement);

        Assert.Equal(["PalmFront->Rise", "Rise->Peak"], edited.Delta.RebuiltTransitions);
        Assert.Equal(5, edited.Delta.PreservedTransitions.Count);
        Assert.Equal(["StartTermination", "EndTermination"], edited.Delta.PreservedTerminations);
        Assert.True(SectionChainMaterializer.Materialize(edited.Chain).IsSuccess);
    }

    [Fact]
    public void MissingSemanticCorrespondenceAndClockwiseProfileAreActionableFailures()
    {
        var source = SectionChainTemplates.TwoProfileRuled();
        var renamedSpans = source.Sections[1].Profile.Spans.Select((span, index) => span with { SpanId = $"Other{index}" }).ToArray();
        var missing = source with
        {
            Sections = [source.Sections[0], source.Sections[1] with { Profile = source.Sections[1].Profile with { Spans = renamedSpans, SeamSpanId = "Other0" } }],
            Correspondence = []
        };
        var result = SectionChainMaterializer.Materialize(missing);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, item => item.Code == "section-chain-correspondence-missing");

        var clockwise = source.Sections[0].Profile.Spans.Reverse().Select(span => span.Curve is SectionProfileCurve.Line line
            ? span with { Curve = new SectionProfileCurve.Line(line.End, line.Start) }
            : span).ToArray();
        var inverted = source with
        {
            Sections = [source.Sections[0] with { Profile = source.Sections[0].Profile with { Spans = clockwise, SeamSpanId = clockwise[0].SpanId } }, source.Sections[1]],
            Correspondence = []
        };
        var orientation = SectionChainMaterializer.Materialize(inverted);
        Assert.Contains(orientation.Diagnostics, item => item.Code == "section-chain-profile-orientation-mismatch");
    }

    [Fact]
    public void ExtremeTwistThatCollapsesRuledJacobiansIsRejected()
    {
        var source = SectionChainTemplates.TwoProfileRuled();
        var top = source.Sections[1];
        var invertedFrame = SectionFrame.Create(top.Frame.Origin, new Vector3D(-1, 0, 0), new Vector3D(0, -1, 0));
        var invalid = source with { Sections = [source.Sections[0], top with { Frame = invertedFrame }] };
        var result = SectionChainMaterializer.Materialize(invalid);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, item => item.Code == "section-chain-transition-foldover");
    }

    [Fact]
    public void CappedBodyPassesBindingAndStepPreflight()
    {
        var result = SectionChainMaterializer.Materialize(SectionChainTemplates.ErgonomicFairing());
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(item => item.Message)));
        Assert.True(BrepBindingValidator.Validate(result.Body!, true).IsSuccess);
        var preflight = BrepExportPreflight.Validate(result.Body!);
        Assert.True(preflight.IsValid, string.Join("; ", preflight.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
    }

    [Fact]
    public void NonNeighborTransitionCrossingFailsWithTypedConservativeEvidence()
    {
        var seed = SectionChainTemplates.TwoProfileRuled().Sections[0];
        var origins = new[] { 0d, 20d, 0d, 20d };
        var sections = origins.Select((z, index) => seed with
        {
            SectionId = $"S{index}", Frame = seed.Frame with { Origin = new Point3D(0, 0, z) },
            Profile = seed.Profile with { StableId = $"S{index}.Profile" }
        }).ToArray();
        var maps = Enumerable.Range(0, 3).Select(index => new AdjacentSectionCorrespondence($"S{index}", $"S{index + 1}",
            seed.Profile.Spans.Select(span => new SectionSpanCorrespondence(span.SpanId, span.SpanId)).ToArray())).ToArray();
        var result = SectionChainMaterializer.Materialize(new("self-crossing", sections, maps, SectionTransitionPolicy.Ruled, SectionTermination.Cap, SectionTermination.Cap));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, item => item.Code == "section-chain-self-intersection" && item.Message.Contains("S0->S1", StringComparison.Ordinal));
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static string Canonical(SectionTransitionEvidence transition) => $"{transition.TransitionId}|" + string.Join(';',
        transition.Surfaces.Select(surface => $"{surface.SpanId}:{surface.SurfaceClass}:{surface.MaterializationKind}"));
}
