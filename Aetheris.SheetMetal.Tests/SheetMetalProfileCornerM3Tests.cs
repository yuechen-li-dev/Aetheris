using Aetheris.Kernel.Core.Brep;
using Xunit;

namespace Aetheris.SheetMetal.Tests;

public sealed class SheetMetalProfileCornerM3Tests
{
    [Fact]
    public void GenericSheetFlange_UsesSharedProfileCornersInFormedAndFlatCorrespondence()
    {
        const string source = """
            Concept Struct Layout {
              CornerProfile Lip.OuterStart { Chamfer RightChamfer { Setback: 6mm; } }
              CornerProfile Lip.OuterEnd { NotchCorner LeftStep { SetbackA: 5mm; SetbackB: 4mm; } }
            }
            SheetMetal Panel {
              Thickness: 1mm;
              Base Deck { Profile: Rectangle { Width: 100mm; Height: 60mm; }; }
              Flange Lip { From: Deck.Front; Height: 20mm; Angle: 90deg; Radius: 2mm; Direction: Up; Relief: Auto; }
            }
            """;

        var result = SheetMetalFirmament.Compile(source);

        Assert.True(result.IsSuccess, string.Join('\n', result.Diagnostics.Select(x => x.Message)));
        var lip = result.Part!.Regions.Single(x => x.StableId == "Lip");
        Assert.Equal(7, lip.Boundary3D.Count);
        Assert.True(BrepExportPreflight.Validate(result.Part.FormedBody!).IsValid);
        var paths = SheetMetalConceptPaths.Inspect(result.Spec!, result.Part, result.FlatPattern).Select(x => x.Path).ToArray();
        Assert.Contains("Layout.Lip.OuterStart", paths);
        Assert.Contains("Layout.Lip.OuterStart.RightChamfer", paths);
        Assert.Contains("Flat.Layout.Lip.OuterStart.RightChamfer", paths);
    }

    [Fact]
    public void CornerAndOuterEdgeFragmentConflictNamesBothSemanticOwnersBeforeBrepConstruction()
    {
        const string source = """
            Concept Struct Layout {
              Tab EndTab { On: Lip.Outer; Center: 10mm; Width: 10mm; Extension: 3mm; }
              CornerProfile Lip.OuterEnd { Chamfer EndChamfer { Setback: 20mm; } }
            }
            SheetMetal Panel {
              Thickness: 1mm;
              Base Deck { Profile: Rectangle { Width: 100mm; Height: 60mm; }; }
              Flange Lip { From: Deck.Front; Height: 25mm; Angle: 90deg; Radius: 2mm; Direction: Up; Relief: Auto; }
            }
            """;

        var result = SheetMetalFirmament.Compile(source);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, x => x.Code == "sheetmetal-edge-profile-invalid"
            && x.Message.Contains("Layout.Lip.OuterEnd", StringComparison.Ordinal)
            && x.Message.Contains("Layout.EndTab", StringComparison.Ordinal));
        Assert.Null(result.Part);
    }

    [Fact]
    public void Ctc03_ProfileCornersMateriallyImproveM2FormedAndFlatResidualsWithoutFeatureRegression()
    {
        var root = FindRepoRoot();
        var step = Path.Combine(root, "testdata/step242/nist/CTC/nist_ctc_03_asme1_ap242-e2.stp");
        var intent = Path.Combine(root, "docs/modules/sheetmetal/artifacts/m8/ctc03-final.firmament");
        var source = SheetMetalRecognizer.RecognizeStep(step).Part!;
        var authored = SheetMetalFirmament.CompileFile(intent);

        Assert.True(authored.IsSuccess, string.Join('\n', authored.Diagnostics.Select(x => x.Message)));
        Assert.Equal(12, authored.Spec!.SemanticLayout.Corners!.Count);
        var comparison = SheetMetalIntentComparer.Compare(source, authored.Part!);
        Assert.All(comparison.Bends, x => Assert.Equal(SheetMetalComparisonStatus.Pass, x.Status));
        Assert.All(comparison.Features, x => Assert.Equal(SheetMetalComparisonStatus.Pass, x.Status));
        Assert.True(comparison.SourceToIntent.Rms < 10.614040, $"formed RMS was {comparison.SourceToIntent.Rms:R}");
        Assert.True(comparison.FlatPattern.Contour.Rms < 12.038486, $"flat RMS was {comparison.FlatPattern.Contour.Rms:R}");
    }

    private static string FindRepoRoot(){var dir=new DirectoryInfo(AppContext.BaseDirectory);while(dir is not null&&!File.Exists(Path.Combine(dir.FullName,"Aetheris.slnx")))dir=dir.Parent;return dir?.FullName??throw new InvalidOperationException("Repo root not found.");}
}
