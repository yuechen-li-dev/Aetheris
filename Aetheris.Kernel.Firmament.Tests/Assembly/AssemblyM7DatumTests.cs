using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class AssemblyM7DatumTests
{
    [Fact]
    public void GenericDatumFrameMate_ResolvesAllSixDegreesOfFreedom()
    {
        const string source = """
            Interface RegisteredFrame {
              Role Moving requires DatumFrameCapable;
              Role Fixed requires DatumFrameCapable;
              Lower FrameCoincident Moving Fixed SameDirection;
            }
            Assembly Fixture {
              <Assembly Fixture>
                <Part Plate = PlatePart> Semantic Mount { DatumFrame Frame = [10,20,30] x [1,0,0] y [0,1,0] z [0,0,1]; } </Part>
                <Part Bracket = BracketPart> Semantic Mount { DatumFrame Frame = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1]; } </Part>
              </Assembly>
              Anchor: Fixture.Plate.Mount;
              Mate BracketMount: RegisteredFrame { Moving: Fixture.Bracket.Mount; Fixed: Fixture.Plate.Mount; }
            }
            """;
        var result = Compile(source);
        Assert.True(result.IsSuccess, Evidence(result));
        var placement = result.Ir!.Placements.Single(item => item.InstanceStableId.EndsWith(":Fixture.Bracket", StringComparison.Ordinal));
        Assert.Equal(PlacementStatus.Resolved, placement.Status);
        Assert.Empty(placement.FreeTranslations); Assert.Empty(placement.FreeRotations);
        Assert.Equal(10, placement.Transform!.Matrix[12], 6);
        Assert.Equal(20, placement.Transform.Matrix[13], 6);
        Assert.Equal(30, placement.Transform.Matrix[14], 6);
        var solution = Assert.Single(result.Ir.DatumMateSolutions!);
        Assert.Equal(6, solution.ConstrainedDegreesOfFreedom);
        Assert.Equal("resolved", solution.Status);
    }

    [Fact]
    public void MissingDatumPath_FailsBeforeGeometry()
    {
        var result = Compile(PairSource("Missing", includeSecondMate: false));
        Assert.Contains(result.Diagnostics, item => item.Code == AssemblyM0Compiler.InvalidParticipant);
    }

    [Fact]
    public void NonOrthonormalDatumFrame_IsRejectedByParser()
    {
        const string source = "Assembly Bad { <Assembly Bad><Part P = Block> Semantic D { DatumFrame F = [0,0,0] x [1,0,0] y [1,0,0] z [0,0,1]; } </Part></Assembly> Anchor: Bad.P.D; }";
        var parsed = new AssemblyM0Parser().Parse(source);
        Assert.Contains(parsed.Diagnostics, item => item.Code == "assembly-datum-frame-invalid");
    }

    [Fact]
    public void ConflictingDatumFrames_AreTypedOverconstraint()
    {
        var result = Compile(PairSource("Frame", includeSecondMate: true));
        Assert.Contains(result.Diagnostics, item => item.Code == AssemblyM0Compiler.Overconstrained);
        Assert.Contains(result.Ir!.Placements, item => item.Status == PlacementStatus.Overconstrained);
    }

    private static string PairSource(string fixedMember, bool includeSecondMate) => $$"""
        Interface RegisteredFrame {
          Role Moving requires DatumFrameCapable;
          Role Fixed requires DatumFrameCapable;
          Lower FrameCoincident Moving.Frame Fixed.{{fixedMember}} SameDirection;
        }
        Interface DirectFrame {
          Role Moving requires DatumFrameCapable;
          Role Fixed requires DatumFrameCapable;
          Lower FrameCoincident Moving Fixed SameDirection;
        }
        Assembly Pair {
          <Assembly Pair>
            <Part Fixed = Block>
              Semantic Fixed { DatumFrame Frame = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1]; }
              Semantic Other { DatumFrame Frame = [10,0,0] x [1,0,0] y [0,1,0] z [0,0,1]; }
            </Part>
            <Part Moving = Block> Semantic Moving { DatumFrame Frame = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1]; } </Part>
          </Assembly>
          Anchor: Pair.Fixed.Fixed;
          Mate First: RegisteredFrame { Moving: Pair.Moving.Moving; Fixed: Pair.Fixed.Fixed; }
          {{(includeSecondMate ? "Mate Second: DirectFrame { Moving: Pair.Moving.Moving.Frame; Fixed: Pair.Fixed.Other.Frame; }" : string.Empty)}}
        }
        """;

    private static AssemblyCompilationResult Compile(string source)
    {
        var parsed = new AssemblyM0Parser().Parse(source);
        Assert.NotNull(parsed.Source);
        return new AssemblyM0Compiler().Compile(parsed.Source!);
    }

    private static string Evidence(AssemblyCompilationResult result) => string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Code + ": " + item.Message));
}
