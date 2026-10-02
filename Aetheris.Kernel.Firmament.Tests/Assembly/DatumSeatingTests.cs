using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class DatumSeatingTests
{
    private const string Source = """
        Concept Struct Layout {
          Plane Deck { Origin: [0mm,0mm,20mm]; Normal: [0,0,1]; Up: [0,1,0] }
          DatumFrame Base { On: Deck; At: [0mm,0mm]; X: [1,0] }
          DatumFrame Hardware { On: Deck; At: [4mm,0mm]; X: [1,0] }
        }
        Template<H: Length> Struct Pad {
          Expose { Semantic Bottom { DatumFrame Frame = [0mm,0mm,0mm] x [1,0,0] y [0,1,0] z [0,0,1]; }
                   Semantic Top { DatumFrame Frame = [0mm,0mm,H] x [1,0,0] y [0,1,0] z [0,0,1]; } }
          Rect2 R { Center: [0mm,0mm]; Size: [20mm,20mm] }
          Profile P { Loop Outer { R |> TraceLoop } }
          Extrude Body { Profile: P From: 0mm To: H }
        }
        Interface<Fixed> Seating { Datum: Layout.Deck; Members: [Test.Base.Top,Test.Hardware.Bottom] }
        Assembly Test {
          <Assembly Test><Part Base = Pad<H:2mm>></Part><Part Hardware = Pad<H:1mm>></Part></Assembly>
          Anchor: Test;
          Mate BaseOnDeck: Seating { Member: Test.Base.Top; At: Layout.Base; Orientation: SameDirection; Support: true; }
          Mate HardwareOnDeck: Seating { Member: Test.Hardware.Bottom; At: Layout.Hardware; Orientation: SameDirection; }
        }
        """;

    [Fact]
    public void ConceptPlaneDrivesBothMaterialPartsWithoutSyntheticProductOccurrences()
    {
        using var session = new FirmamentCompilationSession();
        var first = session.Compile(Source);
        Assert.True(first.IsSuccess, string.Join("\n", first.Diagnostics.Select(d => d.Message)));
        Assert.Equal(3, first.Ir!.Instances.Count);
        Assert.All(first.Geometry!.Artifact.DatumSeats!, s => Assert.True(s.Passed));
        Assert.Equal(18, first.Ir.Instances.Single(i => i.Path.ToString() == "Test.Base").ResolvedTransform!.Matrix[14], 6);
        Assert.Equal(20, first.Ir.Instances.Single(i => i.Path.ToString() == "Test.Hardware").ResolvedTransform!.Matrix[14], 6);
        var shifted = session.Compile(Source.Replace("0mm,0mm,20mm", "0mm,0mm,25mm"));
        Assert.True(shifted.IsSuccess, string.Join("\n", shifted.Diagnostics.Select(d => d.Message)));
        Assert.Equal(2, shifted.Reuse!.ReusedDefinitions);
        Assert.All(shifted.Geometry!.Artifact.DatumSeats!, s => Assert.True(s.Passed));
        Assert.Equal(25, shifted.Ir!.Instances.Single(i => i.Path.ToString() == "Test.Hardware").ResolvedTransform!.Matrix[14], 6);
    }

    [Theory]
    [InlineData("Members: [Test.Base.Top,Test.Hardware.Bottom]", "Members: [Test.Base.Top]", "assembly-datum-member-outside-contract")]
    [InlineData("On: Deck", "On: Missing", "assembly-datum-plane-unresolved")]
    [InlineData("X: [1,0]", "X: [0,0]", "assembly-datum-invalid-direction")]
    [InlineData("At: Layout.Hardware", "At: Layout.Deck", "assembly-datum-target-invalid")]
    [InlineData("Orientation: SameDirection", "Orientation: Unknown", "assembly-datum-orientation-invalid")]
    [InlineData("Up: [0,1,0]", "Up: [0,0,1]", "assembly-frame-invalid-basis")]
    public void InvalidContractsFailClosed(string from, string to, string code)
    {
        var result = new AssemblyM1Pipeline().Compile(Source.Replace(from, to));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == code);
    }

    [Fact]
    public void FrameWithoutMaterialSeatAndSeatOutsideSupportAreRejected()
    {
        var fake = new AssemblyM1Pipeline().Compile(Source.Replace("From: 0mm", "From: -1mm"));
        Assert.False(fake.IsSuccess);
        Assert.Contains(fake.Diagnostics, d => d.Code == "assembly-datum-material-seat-missing");
        var outside = new AssemblyM1Pipeline().Compile(Source.Replace("At: [4mm,0mm]", "At: [40mm,0mm]"));
        Assert.False(outside.IsSuccess);
        Assert.Contains(outside.Diagnostics, d => d.Code == "assembly-datum-support-contact-missing");
    }

    [Fact]
    public void DuplicatePlacementAuthorityIsRejected()
    {
        var source = Source.Replace("<Part Hardware = Pad<H:1mm>></Part>", "<Part Hardware = Pad<H:1mm>> Placement { From: Origin; To: World; } </Part>");
        var result = new AssemblyM1Pipeline().Compile(source);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == "assembly-placement-authority-conflict");
    }

    [Fact]
    public void PartOwnedConceptGeometryIsNotConsumedByAssemblyLayoutBinding()
    {
        var source = Source.Replace("Template<H: Length> Struct Pad {", "Template<H: Length> Struct Pad { Concept Struct Private { Plane PrivatePlane { Origin: [0mm,0mm,0mm]; Normal: [0,0,1]; Up: [0,1,0] } }");
        var parsed = new AssemblyM0Parser().Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics.Select(d => d.Message)));
        Assert.Contains("Concept Struct Private", parsed.Source!.DefinitionSource);
        Assert.DoesNotContain("Concept Struct Layout", parsed.Source.DefinitionSource);
    }

    [Fact]
    public void GuitarBridgeAndPickupsHaveMaterialContactWithOneConceptDeck()
    {
        var result = new AssemblyM1Pipeline().CompileFile(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm"));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        var evidence = result.Geometry!.Artifact.DatumSeats!;
        Assert.Equal(4, evidence.Count);
        Assert.All(evidence, e => { Assert.True(e.Passed); Assert.NotEmpty(e.MaterialFaces); Assert.Equal("GuitarLayout.HardwareDeck", e.Datum); });
        var bridge = result.Ir!.Instances.Single(i => i.Path.ToString() == "GuitarX0.Bridge.Bridge");
        Assert.Equal(53, bridge.ResolvedTransform!.Matrix[14], 6);
        Assert.Contains("H:10mm", bridge.DefinitionIdentity);
        Assert.Equal(63, result.Geometry.Artifact.Instances.Single(i => i.InstanceStableId == bridge.StableId).Metrics.Maximum[2], 6);
    }
}
