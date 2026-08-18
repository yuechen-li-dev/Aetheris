using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Core.Step242;
using Xunit;

namespace Aetheris.SheetMetal.Tests;

public sealed class SheetMetalM11Tests
{
    private const string ExplicitCoupon="""
        SheetMetal TerminationCoupon {
          Thickness: 1mm;
          Base Deck { Profile: Rectangle { Width: 80mm; Height: 50mm; }; }
          Flange Wall { From: Deck.Front; Height: 16mm; Angle: 90deg; Radius: 2mm; Direction: Up;
            StartTermination: Rounded; StartTerminationRadius: 2mm;
            EndTermination: Trimmed; EndTerminationSetback: 2mm; EndTerminationDepth: 1.5mm;
          }
        }
        """;

    [Fact]
    public void Explicit_terminations_are_stable_sheet_metal_semantics_with_profile_delta_lowering()
    {
        var result=SheetMetalFirmament.Compile(ExplicitCoupon);
        Assert.True(result.IsSuccess,string.Join('\n',result.Diagnostics.Select(x=>x.Message)));
        var bend=Assert.Single(result.Part!.Bends);
        Assert.Equal("WallBend.StartTermination",bend.StartTermination!.StableId);
        Assert.Equal(SheetBendTerminationTreatment.Rounded,bend.StartTermination.ResolvedTreatment);
        Assert.Equal(SheetBendTerminationTreatment.Trimmed,bend.EndTermination!.ResolvedTreatment);
        Assert.NotNull(bend.StartTermination.LoweredProfileDelta);
        Assert.Contains(bend.StartTermination.LoweredProfileDelta!.Members,x=>x.Kind==SemanticProfileDeltaMemberKind.Round);
        Assert.True(BrepExportPreflight.Validate(result.Part.FormedBody!).IsValid);
        Assert.Equal(FlatPatternStatus.Valid,result.FlatPattern!.Status);
        Assert.NotNull(result.FlatPattern.ExactBlankContour);
        var paths=SheetMetalConceptPaths.Inspect(result.Spec!,result.Part,result.FlatPattern).Select(x=>x.Path).ToArray();
        Assert.Contains("WallBend.StartTermination",paths);
        Assert.Contains("WallBend.EndTermination.Finish",paths);
        Assert.Equal(SheetMetalDfmStatus.Pass,SheetMetalDfm.Evaluate(result.Part,result.FlatPattern).Overall);
        var formedStep=Step242Exporter.ExportBody(result.Part.FormedBody!);Assert.True(formedStep.IsSuccess);Assert.True(Step242Importer.ImportBody(formedStep.Value).IsSuccess);
        var flatBody=SheetMetalManufacturingArtifacts.BuildFlatBody(result.Part,result.FlatPattern);Assert.True(flatBody.IsSuccess,string.Join('\n',flatBody.Diagnostics.Select(x=>x.Message)));
        var flatStep=Step242Exporter.ExportBody(flatBody.Body!);Assert.True(flatStep.IsSuccess);Assert.True(Step242Importer.ImportBody(flatStep.Value).IsSuccess);
    }

    [Fact]
    public void Auto_is_bounded_and_natural_remains_explicit()
    {
        const string source="""
            SheetMetal AutoCoupon {
              Thickness: 1mm;
              Base Deck { Profile: Rectangle { Width: 80mm; Height: 50mm; }; }
              Flange Wall { From: Deck.Front; Height: 16mm; Angle: 90deg; Radius: 2mm;
                StartTermination: Auto; EndTermination: Natural;
              }
            }
            """;
        var result=SheetMetalFirmament.Compile(source);
        Assert.True(result.IsSuccess,string.Join('\n',result.Diagnostics.Select(x=>x.Message)));
        var bend=Assert.Single(result.Part!.Bends);
        Assert.True(bend.StartTermination!.IsPolicyDerived);
        Assert.Equal(SheetBendTerminationTreatment.Rounded,bend.StartTermination.ResolvedTreatment);
        Assert.Equal(SheetBendTerminationTreatment.Natural,bend.EndTermination!.ResolvedTreatment);
        Assert.Null(bend.EndTermination.LoweredProfileDelta);
    }

    [Fact]
    public void Auto_refuses_when_no_safe_treatment_fits_and_corner_conflict_names_both_operations()
    {
        const string refused="""
            SheetMetal UnsafeCoupon {
              Thickness: 1mm;
              Base Deck { Profile: Rectangle { Width: 8mm; Height: 8mm; }; }
              Flange Wall { From: Deck.Front; Height: 3.1mm; Angle: 90deg; Radius: 2mm; StartTermination: Auto; StartTerminationRadius: 4mm; }
            }
            """;
        var rejected=SheetMetalFirmament.Compile(refused);
        Assert.False(rejected.IsSuccess);
        Assert.Contains(rejected.Diagnostics,x=>x.Code=="sheetmetal-bend-termination-auto-refused"&&x.Message.Contains("WallBend.StartTermination",StringComparison.Ordinal));

        const string conflict="""
            Concept Struct Layout { CornerProfile Wall.RootStart { Round Manual { Radius: 2mm; } } }
            SheetMetal ConflictCoupon {
              Thickness: 1mm;
              Base Deck { Profile: Rectangle { Width: 80mm; Height: 50mm; }; }
              Flange Wall { From: Deck.Front; Height: 16mm; Angle: 90deg; Radius: 2mm; StartTermination: Rounded; StartTerminationRadius: 2mm; }
            }
            """;
        var conflicted=SheetMetalFirmament.Compile(conflict);
        Assert.False(conflicted.IsSuccess);
        Assert.Contains(conflicted.Diagnostics,x=>x.Code=="sheetmetal-bend-termination-conflict"&&x.Message.Contains("WallBend.StartTermination",StringComparison.Ordinal)&&x.Message.Contains("Layout.Wall.RootStart",StringComparison.Ordinal));

        const string deltaConflict="""
            ProfileDelta ManualRootTrim { On: Wall.Root; Anchor: FromStart 0mm; Side: Inward; Level Carrier { Offset: 0mm; } Level Cut { Offset: 1mm; } Transition Enter { Kind: Diagonal; Run: 1mm; To: Cut; } Transition Exit { Kind: Step; To: Carrier; } }
            SheetMetal DeltaConflictCoupon { Thickness: 1mm; Base Deck { Profile: Rectangle { Width: 80mm; Height: 50mm; }; } Flange Wall { From: Deck.Front; Height: 16mm; Angle: 90deg; Radius: 2mm; StartTermination: Trimmed; StartTerminationSetback: 1mm; } }
            """;
        var deltaConflicted=SheetMetalFirmament.Compile(deltaConflict);
        Assert.False(deltaConflicted.IsSuccess);
        Assert.Contains(deltaConflicted.Diagnostics,x=>x.Code=="sheetmetal-bend-termination-profile-delta-conflict"&&x.Message.Contains("ManualRootTrim",StringComparison.Ordinal)&&x.Message.Contains("WallBend.StartTermination",StringComparison.Ordinal));
    }

    [Fact]
    public void Ctc03_owns_four_front_rear_bend_terminations_without_regression()
    {
        var root=FindRepoRoot();var path=Path.Combine(root,"docs/development/milestones/modules/sheetmetal/artifacts/m8/ctc03-final.firmament");
        var result=SheetMetalFirmament.CompileFile(path);
        Assert.True(result.IsSuccess,string.Join('\n',result.Diagnostics.Select(x=>x.Message)));
        var terminations=result.Part!.Bends.SelectMany(x=>new[]{x.StartTermination,x.EndTermination}).OfType<SheetBendTerminationIr>().ToArray();
        Assert.Equal(4,terminations.Length);Assert.All(terminations,x=>Assert.Equal(SheetBendTerminationTreatment.Trimmed,x.ResolvedTreatment));
        Assert.All(terminations,x=>Assert.Equal(8.89,x.Setback,6));
        Assert.Equal(new[]{"FrontWallBend.EndTermination","FrontWallBend.StartTermination","RearWallBend.EndTermination","RearWallBend.StartTermination"},terminations.Select(x=>x.StableId).Order().ToArray());
        Assert.Equal(7,result.Part.Bends.Count);Assert.Equal(17,result.Part.Features.Count);
        Assert.Equal(FlatPatternStatus.Valid,result.FlatPattern!.Status);Assert.NotNull(result.FlatPattern.ExactBlankContour);
        Assert.True(BrepExportPreflight.Validate(result.Part.FormedBody!).IsValid);
        Assert.Contains("source-independent",result.Part.Provenance,StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SheetMetalDfmStatus.Pass,SheetMetalDfm.Evaluate(result.Part,result.FlatPattern).Overall);
    }

    private static string FindRepoRoot(){var dir=new DirectoryInfo(AppContext.BaseDirectory);while(dir is not null&&!File.Exists(Path.Combine(dir.FullName,"Aetheris.slnx")))dir=dir.Parent;return dir?.FullName??throw new InvalidOperationException("Repo root not found.");}
}
