using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ConceptDerivationTests
{
    private const string Profiles = """
        Model Test {
          Units: mm
          Rect2 Rectangle { Center: [0mm,0mm]; Size: [4mm,2mm] }
          Profile Original { Loop Outer { Rectangle |> TraceLoop } }
          Concept Struct Layout {
            Deck: Plane { Origin: [0mm,0mm,7mm]; Normal: [0,0,1]; Up: [0,1,0] }
            Curve2 Outline { From: Original; On: Deck; Translate: [10mm,20mm]; Rotate: 90deg }
          }
          Profile Derived Using Layout { Loop Outer { Outline |> TraceLoop } }
          Extrude Body { Profile: Derived From: 0mm To: 3mm }
        }
        """;

    [Fact]
    public void RigidBoundaryPlacementPreservesSourceAndUsesRealProfileExtrusion()
    {
        var original = ProfileAuthoringParser.ResolveNamedProfile(Profiles, "Original", out var originalErrors);
        var derived = ProfileAuthoringParser.ResolveNamedProfile(Profiles, "Derived", out var errors);
        Assert.Empty(originalErrors); Assert.Empty(errors); Assert.NotNull(original); Assert.NotNull(derived);
        Assert.Equal(8, ResolvedProfile2DValidator.Validate(derived).SignedArea, 8);
        Assert.Equal(7, derived.EffectiveConstructionPlane.Origin.Z);
        var lines = derived.Loops[0].Segments.Select(s => Assert.IsType<LineArcLineSegment2D>(s.Geometry)).ToArray();
        Assert.Equal(9, lines.Min(l => Math.Min(l.Start.X,l.End.X)), 8);
        Assert.Equal(11, lines.Max(l => Math.Max(l.Start.X,l.End.X)), 8);
        Assert.Equal(18, lines.Min(l => Math.Min(l.Start.Y,l.End.Y)), 8);
        Assert.Equal(22, lines.Max(l => Math.Max(l.Start.Y,l.End.Y)), 8);
        Assert.All(derived.Loops[0].Segments, s => { Assert.Equal("concept-curve:Layout.Outline", s.Provenance.ConceptStableId); Assert.NotNull(s.Provenance.TracedFrom); });
        var parsed = ProfileAuthoringParser.Parse(Profiles);
        Assert.Empty(parsed.Diagnostics);
        Assert.Equal(LineArcProfileExtrudeStatus.Succeeded, ResolvedProfile2DValidator.Extrude(parsed.Profile!, parsed.Height).Status);
        var compiled = FirmamentBuildAndExport.CompileSource(Profiles);
        Assert.True(compiled.IsSuccess, string.Join("\n", compiled.Diagnostics));
    }

    [Theory]
    [InlineData("From: Original", "From: Derived", "concept-profile-dependency-cycle")]
    [InlineData("From: Original", "From: Missing", "profile-source-missing-profile")]
    [InlineData("On: Deck", "On: Missing", "Plane")]
    [InlineData("Rotate: 90deg", "Rotate: 90mm", "concept-profile-unit-invalid")]
    [InlineData("Rotate: 90deg", "Rotate: 90deg; Rotate: 0deg", "concept-profile-duplicate-field")]
    [InlineData("Rotate: 90deg", "Rotate: 90deg; Scale: -1", "concept-profile-scale-invalid")]
    [InlineData("Translate: [10mm,20mm]", "Translate: [10mm,20mm,30mm]", "concept-profile-vector-invalid")]
    public void InvalidBoundaryDerivationsFailClosed(string from, string to, string diagnostic)
    {
        Assert.Null(ProfileAuthoringParser.ResolveNamedProfile(Profiles.Replace(from,to), "Derived", out var errors));
        Assert.Contains(errors, e => e.Contains(diagnostic, StringComparison.Ordinal));
    }

    private const string Assembly = """
        Concept Struct Layout {
          Plane Deck { From: Test.Base.Top.Frame; Offset: 2mm; Clocking: 90deg }
          DatumFrame Mount { On: Deck; At: [4mm,0mm]; X: [1,0] }
        }
        Template<H: Length> Struct Pad {
          Expose { Semantic Bottom { DatumFrame Frame = [0mm,0mm,0mm] x [1,0,0] y [0,1,0] z [0,0,1]; }
                   Semantic Top { DatumFrame Frame = [0mm,0mm,H] x [1,0,0] y [0,1,0] z [0,0,1]; } }
          Rect2 R { Center: [0mm,0mm]; Size: [20mm,20mm] }
          Profile P { Loop Outer { R |> TraceLoop } }
          Extrude Body { Profile: P From: 0mm To: H }
        }
        Interface<Fixed> Seating { Datum: Layout.Deck; Members: [Test.Hardware.Bottom] }
        Assembly Test {
          <Assembly Test>
            <Part Base = Pad<H:10mm>> Placement { From: Origin; To: World; TranslateLocal: [0mm,0mm,30mm]; } </Part>
            <Part Hardware = Pad<H:1mm>></Part>
          </Assembly>
          Anchor: Test;
          Mate HardwareOnDeck: Seating { Member: Test.Hardware.Bottom; At: Layout.Mount; Orientation: SameDirection; }
        }
        """;

    [Fact]
    public void PublishedOccurrenceFrameDrivesOffsetClockingAndCachedPlacement()
    {
        using var session = new FirmamentCompilationSession();
        var first = session.Compile(Assembly);
        Assert.True(first.IsSuccess, string.Join("\n",first.Diagnostics));
        var matrix = first.Ir!.Instances.Single(i => i.Path.ToString() == "Test.Hardware").ResolvedTransform!.Matrix;
        Assert.Equal(0, matrix[12], 8); Assert.Equal(4, matrix[13], 8); Assert.Equal(42, matrix[14], 8);
        Assert.Equal(0, matrix[0], 8); Assert.Equal(1, matrix[1], 8);
        var edited = session.Compile(Assembly.Replace("Offset: 2mm", "Offset: 3mm"));
        Assert.True(edited.IsSuccess, string.Join("\n",edited.Diagnostics));
        Assert.Equal(2, edited.Reuse!.ReusedDefinitions);
        Assert.Equal(43, edited.Ir!.Instances.Single(i => i.Path.ToString() == "Test.Hardware").ResolvedTransform!.Matrix[14], 8);
        var nonPlanar = session.Compile(Assembly.Replace("TranslateLocal: [0mm,0mm,30mm];", "TranslateLocal: [0mm,0mm,30mm]; RotateLocal: { Axis: Y; Angle: 90deg; }"));
        Assert.True(nonPlanar.IsSuccess, string.Join("\n",nonPlanar.Diagnostics));
        var tilted = nonPlanar.Ir!.Instances.Single(i => i.Path.ToString() == "Test.Hardware").ResolvedTransform!.Matrix;
        Assert.Equal(12, tilted[12], 8); Assert.Equal(4, tilted[13], 8); Assert.Equal(30, tilted[14], 8);
        Assert.Equal(1, tilted[8], 8); Assert.Equal(0, tilted[10], 8);
        Assert.Equal(2, nonPlanar.Reuse!.ReusedDefinitions);
        Assert.All(nonPlanar.Geometry!.Artifact.DatumSeats!, s => Assert.True(s.Passed));
    }

    [Theory]
    [InlineData("From: Test.Base.Top.Frame", "From: Test.Hardware.Bottom.Frame", "assembly-datum-source-placement-dependent")]
    [InlineData("From: Test.Base.Top.Frame", "From: Test.Base.Missing.Frame", "assembly-datum-source-unresolved")]
    [InlineData("From: Test.Base.Top.Frame", "From: Layout.Deck", "assembly-datum-dependency-cycle")]
    public void InvalidDatumSourcesFailClosed(string from, string to, string code)
    {
        var result = new AssemblyM1Pipeline().Compile(Assembly.Replace(from,to));
        Assert.False(result.IsSuccess); Assert.Contains(result.Diagnostics, d => d.Code == code);
    }

    [Fact]
    public void DuplicateDerivedPlaneProducesDiagnosticRatherThanThrowing()
    {
        var source = Assembly.Replace("DatumFrame Mount", "Plane Deck { From: Test.Base.Top.Frame; Offset: 2mm; Clocking: 90deg } DatumFrame Mount");
        var result = new AssemblyM1Pipeline().Compile(source);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == "assembly-datum-duplicate");
    }

    [Fact]
    public void SharedGuitarOutlineEditsInvalidateExactlyItsThreeConsumers()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm");
        var documents = Directory.GetFiles(Path.GetDirectoryName(path)!,"*.firmament").Append(path).ToDictionary(p => Path.GetFileName(p),File.ReadAllText);
        using var session = new FirmamentCompilationSession();
        var first = session.CompileProject(new("guitar.firmasm",documents));
        Assert.True(first.IsSuccess, string.Join("\n",first.Diagnostics)); Assert.Equal(19, first.Ir!.SourceDependencies!.Count);
        Assert.Single(first.Ir.SourceDependencies, d => d.IsRoot);
        Assert.Equal(9, first.Geometry!.Artifact.Definitions.Sum(d => d.Provenance.Count(p => p.Stage == "concept-boundary-placement")));
        documents["body-outline.firmament"] = documents["body-outline.firmament"].Replace("115mm,-195mm", "115.1mm,-195mm");
        var edited = session.CompileProject(new("guitar.firmasm",documents));
        Assert.True(edited.IsSuccess, string.Join("\n",edited.Diagnostics));
        Assert.Equal(3, edited.Reuse!.RebuiltDefinitions); Assert.Equal(50, edited.Reuse.ReusedDefinitions);
        Assert.All(edited.Geometry!.Artifact.DatumSeats!, s => Assert.True(s.Passed));
        foreach (var identity in new[] { "CarvedMaple", "MahoganyBack", "IvoryBinding" })
            Assert.NotEqual(first.Geometry!.Artifact.Definitions.Single(d => d.DefinitionIdentity.Contains(identity)).StepSha256,
                edited.Geometry.Artifact.Definitions.Single(d => d.DefinitionIdentity.Contains(identity)).StepSha256);
    }

    [Theory]
    [InlineData(false, "assembly-include-file-not-found")]
    [InlineData(true, "assembly-include-cycle")]
    public void FileBackedProfileIncludesFailClosed(bool cycle, string code)
    {
        var project = new FirmamentProjectSnapshot("main.firmasm", new Dictionary<string,string> {
            ["main.firmasm"] = "Assembly Test { <Assembly Test><Part Body = SectionChainFile<\"part.firmament\">></Part></Assembly> Anchor: Test; }",
            ["part.firmament"] = "Include \"boundary.firmament\";\nModel Test { Units: mm }",
            ["boundary.firmament"] = cycle ? "Include \"part.firmament\";" : "Include \"missing.firmament\";"
        });
        var result = new AssemblyM1Pipeline().CompileProject(project);
        Assert.False(result.IsSuccess); Assert.Contains(result.Diagnostics, d => d.Code == code);
    }
}
