using Aetheris.Kernel.Core.Step242;
using Xunit;

namespace Aetheris.SheetMetal.Tests;

public sealed class RecognizedImportRecoveryM1Tests
{
    private static readonly string RepoRoot=FindRepoRoot();
    private static string Ctc=>Path.Combine(RepoRoot,"testdata/step242/nist/CTC/nist_ctc_03_asme1_ap242-e2.stp");

    [Fact]
    public void Ctc03_DetectionCandidatesRequireAnExplicitValidatedRecognitionPlan()
    {
        var detection=SheetMetalRecognizer.RecognizeStep(Ctc);var model=RecognizedSheetMetalRecovery.FromDetection(detection);
        Assert.Equal(7,model.Bends.Count);Assert.All(model.Bends,x=>Assert.Equal(RecognizedBendStatus.Candidate,x.Status));
        var plan=RecognizedSheetMetalRecovery.CreateAutomaticPlan(model);Assert.Equal(7,plan.Bends.Count(x=>x.Status==RecognizedBendStatus.Recognized));
        var renamed=plan with { Bends=plan.Bends.Select((x,i)=>i==0?x with{Name="FrontWallBend"}:x).ToArray(),Authority="engineer/LLM assertion checked against imported geometry" };
        var validation=RecognizedSheetMetalRecovery.ValidatePlan(model,renamed);Assert.True(validation.IsValid,string.Join('\n',validation.Diagnostics.Select(x=>x.Message)));
        Assert.Contains(validation.Model!.Bends,x=>x.Name=="FrontWallBend"&&x.Status==RecognizedBendStatus.Recognized&&x.Geometry.Source.FaceIds.Count==2);
    }

    [Fact]
    public void RecognitionPlan_RejectsAClaimWithoutCylindricalSourceEvidence()
    {
        var model=RecognizedSheetMetalRecovery.FromDetection(SheetMetalRecognizer.RecognizeStep(Ctc));var plan=RecognizedSheetMetalRecovery.CreateAutomaticPlan(model);
        plan=plan with { Bends=[..plan.Bends,new("invented-bend","Impossible",RecognizedBendStatus.Recognized)] };
        var validation=RecognizedSheetMetalRecovery.ValidatePlan(model,plan);Assert.False(validation.IsValid);
        Assert.Contains(validation.Diagnostics,x=>x.Code==SheetMetalDiagnosticCodes.RecognitionAssertionInvalid&&x.Message.Contains("no machine-detected cylindrical support",StringComparison.Ordinal));
    }

    [Fact]
    public void Ctc03_SourceUnfold_PreservesAllExactRegionLoopsCutsBendsAndProvenanceDeterministically()
    {
        var detection=SheetMetalRecognizer.RecognizeStep(Ctc);var model=RecognizedSheetMetalRecovery.FromDetection(detection);var plan=RecognizedSheetMetalRecovery.CreateAutomaticPlan(model);
        var first=RecoveredSourceFlattener.Flatten(model,plan);var second=RecoveredSourceFlattener.Flatten(model,plan);
        Assert.NotEqual(FlatPatternStatus.Unsupported,first.Status);Assert.Equal(7,first.BendLines.Count);Assert.Equal(17,first.InnerContours.Count);
        Assert.All(first.Regions,x=>Assert.NotNull(x.ExactContour));Assert.All(first.InnerContours,x=>Assert.NotNull(x.ExactContour));
        Assert.Contains(first.Regions.SelectMany(x=>x.ExactContour!.OuterLoop.Segments),x=>x.Geometry is Aetheris.Kernel.Firmament.Materializer.LineArcCircularArc2D);
        Assert.True(first.SourceProvenance.Count>=100);Assert.Equal(first.DeterministicHash,second.DeterministicHash);
        Assert.Equal(RecoveredFlatReferenceKind.GeometricMidSurface,first.ReferenceKind);Assert.Equal(.5,RecoveredSourceFlattener.ToFlatPattern(first).Policy.KFactor);
        Assert.Equal(FlatPatternStatus.Valid,first.Status);Assert.NotNull(first.OuterAndInnerContours);
    }

    [Fact]
    public void NonCtcUChannel_StepOnlyRoundTripRecognizesAndUnfoldsWithoutNativeConstructionAuthority()
    {
        var authored=SheetMetalFirmament.CompileFile(Path.Combine(RepoRoot,"fixtures/FirmamentV2/SheetMetal/simple-u-channel.firmament"));Assert.True(authored.IsSuccess);
        var exported=Step242Exporter.ExportBody(authored.Part!.FormedBody!);Assert.True(exported.IsSuccess);
        var imported=Step242Importer.ImportBody(exported.Value);Assert.True(imported.IsSuccess);
        var detection=SheetMetalRecognizer.Recognize(imported.Value,"generic-u-channel.step");var model=RecognizedSheetMetalRecovery.FromDetection(detection);var plan=RecognizedSheetMetalRecovery.CreateAutomaticPlan(model);
        var recovered=RecoveredSourceFlattener.Flatten(model,plan);Assert.Equal(2,recovered.BendLines.Count);Assert.Equal(FlatPatternStatus.Valid,recovered.Status);Assert.NotNull(recovered.OuterAndInnerContours);
        Assert.All(model.Bends,x=>Assert.Equal(RecognizedBendStatus.Candidate,x.Status));Assert.All(plan.Bends,x=>Assert.Equal(RecognizedBendStatus.Recognized,x.Status));
        Assert.All(recovered.Regions.Where(x=>x.Kind==SheetRegionKind.Planar),x=>Assert.NotNull(x.ExactContour));
    }

    private static string FindRepoRoot(){var dir=new DirectoryInfo(AppContext.BaseDirectory);while(dir is not null&&!File.Exists(Path.Combine(dir.FullName,"Aetheris.slnx")))dir=dir.Parent;return dir?.FullName??throw new InvalidOperationException("Repo root not found.");}
}
