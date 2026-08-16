using Aetheris.Kernel.Firmament.FirmamentV2;
using Xunit;

namespace Aetheris.SheetMetal.Tests;

public sealed class SheetMetalM12Tests
{
    [Fact]
    public void Semantic_compare_localizes_a_non_ctc_profile_delta_parameter_mismatch()
    {
        static string Coupon(double depth)=>$$"""
            ProfileDelta ServiceRecess {
              On: Wall.Outer; Anchor: CenteredAt 50mm; Side: Inward;
              Level Carrier { Offset: 0mm; } Level Deep { Offset: {{depth}}mm; }
              Transition LeadIn { Kind: Diagonal; Run: 5mm; To: Deep; }
              Span Land { Run: 20mm; At: Deep; }
              Transition LeadOut { Kind: Diagonal; Run: 5mm; To: Carrier; }
            }
            SheetMetal LocalCompareCoupon {
              Thickness: 1mm;
              Base Deck { Profile: Rectangle { Width: 100mm; Height: 60mm; }; }
              Flange Wall { From: Deck.Front; Height: 20mm; Angle: 90deg; Radius: 2mm; }
            }
            """;
        var reference=SheetMetalFirmament.Compile(Coupon(5));var perturbed=SheetMetalFirmament.Compile(Coupon(7));
        Assert.True(reference.IsSuccess,string.Join(';',reference.Diagnostics.Select(x=>x.Message)));Assert.True(perturbed.IsSuccess,string.Join(';',perturbed.Diagnostics.Select(x=>x.Message)));
        var recovered=Reference(reference.Part!,reference.FlatPattern!);var report=SemanticSheetMetalComparer.CompareFlat(recovered,perturbed.Part!,perturbed.FlatPattern!,sourcePart:reference.Part);
        Assert.Equal(SheetMetalComparisonStatus.NeedsReview,report.Status);
        Assert.Contains(report.Targets,x=>x.SemanticPath=="Wall.ServiceRecess.Land"&&x.Status==SemanticGeometryComparisonStatus.NeedsReview&&x.Classification==SemanticGeometryDifferenceClassification.ParameterMismatch);
        Assert.Equal(report.DeterministicHash,SemanticSheetMetalComparer.CompareFlat(recovered,perturbed.Part!,perturbed.FlatPattern!,sourcePart:reference.Part).DeterministicHash);
    }

    [Fact]
    public void Ctc_semantic_compare_proves_openings_and_emits_four_individual_termination_results()
    {
        var root=FindRepoRoot();var step=Path.Combine(root,"testdata/step242/nist/CTC/nist_ctc_03_asme1_ap242-e2.stp");var nativePath=Path.Combine(root,"docs/modules/sheetmetal/artifacts/m8/ctc03-final.firmament");
        var detection=SheetMetalRecognizer.RecognizeStep(step);Assert.NotNull(detection.Part);var model=RecognizedSheetMetalRecovery.FromDetection(detection);var plan=RecognizedSheetMetalRecovery.CreateAutomaticPlan(model);var recovered=RecoveredSourceFlattener.Flatten(model,plan);var native=SheetMetalFirmament.CompileFile(nativePath);Assert.True(native.IsSuccess);
        var report=SemanticSheetMetalComparer.CompareFlat(recovered,native.Part!,native.FlatPattern!,sourcePart:model.DetectedPart);
        var terminations=report.Targets.Where(x=>x.GeometryKind==SemanticGeometryTargetKind.BendTermination).ToArray();Assert.Equal(4,terminations.Length);Assert.Equal(4,terminations.Select(x=>x.SemanticPath).Distinct().Count());
        var openings=report.Targets.Where(x=>x.GeometryKind==SemanticGeometryTargetKind.Opening).ToArray();Assert.Equal(17,openings.Length);Assert.All(openings,x=>Assert.Equal(SemanticGeometryComparisonStatus.Pass,x.Status));
        Assert.Contains(report.Targets,x=>x.SemanticPath=="RightWall.RightWallServiceProfile.LeadIn");Assert.Contains(report.Targets,x=>x.GeometryKind==SemanticGeometryTargetKind.AttachmentPath);
    }

    private static RecoveredFlatReference Reference(SheetMetalPartIr part,SheetMetalFlatPatternIr flat)
    {
        var plan=new SheetMetalRecognitionPlan("authored-reference-plan","authored",part.Thickness,part.BaseRegionId,RecoveredFlatReferenceKind.GeometricMidSurface,[],"test authored authority","authored");
        return new("authored-reference",RecoveredFlatReferenceKind.GeometricMidSurface,part.BaseRegionId,"XY",flat.ExactBlankContour,flat.Regions2D,flat.CutLoops,flat.BendLines,flat.SourceToFlatMappings,[],plan,flat.Bounds,flat.Status,[],RecoveredContourAcceptance.Exact,[],null,flat.DeterministicHash,TimeSpan.Zero,TimeSpan.Zero,TimeSpan.Zero);
    }
    private static string FindRepoRoot(){var dir=new DirectoryInfo(AppContext.BaseDirectory);while(dir is not null&&!File.Exists(Path.Combine(dir.FullName,"Aetheris.slnx")))dir=dir.Parent;return dir?.FullName??throw new InvalidOperationException("Repo root not found.");}
}
