using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;
using Xunit;

namespace Aetheris.SheetMetal.Tests;

public sealed class SheetMetalM1Tests
{
    private static readonly string RepoRoot=FindRepoRoot();

    [Fact]
    public void KFactorAllowance_UsesNeutralAxisRadius()
    {
        var policy=new SheetMetalFlattenPolicy(.42);
        Assert.Equal(2.63,policy.NeutralRadius(2,1.5),10);
        Assert.Equal(Math.PI/2*2.63,policy.BendAllowance(Math.PI/2,2,1.5),10);
    }

    [Fact]
    public void AuthoredChannel_ProducesExactFormedBrepAndManufacturingFlatPattern()
    {
        var path=Path.Combine(RepoRoot,"fixtures/SheetMetal/simple-u-channel.firmament");
        var result=SheetMetalFirmament.CompileFile(path);
        Assert.True(result.IsSuccess,string.Join("\n",result.Diagnostics.Select(d=>d.Message)));
        var part=Assert.IsType<SheetMetalPartIr>(result.Part);var flat=Assert.IsType<SheetMetalFlatPatternIr>(result.FlatPattern);
        Assert.Equal(1.5,part.Thickness);Assert.Equal(2,part.Bends.Count);Assert.Equal(2,part.Features.Count);Assert.Equal(FlatPatternStatus.Valid,flat.Status);
        Assert.Equal(2,flat.BendLines.Count);Assert.Equal(2,flat.CutLoops.Count);Assert.NotNull(part.FormedBody);
        var preflight=BrepExportPreflight.Validate(part.FormedBody!);Assert.True(preflight.IsValid,string.Join("\n",preflight.Diagnostics.Select(d=>d.Message)));
        var step=Step242Exporter.ExportBody(part.FormedBody!);Assert.True(step.IsSuccess,string.Join("\n",step.Diagnostics.Select(d=>d.Message)));Assert.True(Step242Importer.ImportBody(step.Value).IsSuccess);
        var first=SheetMetalFlattener.Flatten(part);Assert.Equal(flat.DeterministicHash,first.DeterministicHash);
        var roundTrip=SheetMetalRoundTrip.ValidateReferenceSurface(part,flat);Assert.True(roundTrip.IsWithinTolerance,string.Join("\n",roundTrip.Diagnostics.Select(d=>d.Message)));
        Assert.All(flat.BendLines,b=>Assert.Equal(Math.PI/2*(2+.42*1.5),b.BendAllowance,10));
    }

    [Fact]
    public void Ctc03_RecoversToleranceBoundedThicknessAndBendEvidenceDeterministically()
    {
        var path=Path.Combine(RepoRoot,"testdata/step242/nist/CTC/nist_ctc_03_asme1_ap242-e2.stp");
        var first=SheetMetalRecognizer.RecognizeStep(path);var second=SheetMetalRecognizer.RecognizeStep(path);
        Assert.True(first.Thickness.IsPlausible,string.Join("\n",first.Diagnostics.Select(d=>d.Message)));Assert.True(first.Part is not null,$"t={first.Thickness.NominalThickness}; planar={first.Thickness.SourcePairs.Count(p=>p.Family=="Planar")}; closestPlanar={string.Join(",",first.Thickness.SourcePairs.Where(p=>p.Family=="Planar").OrderBy(p=>p.Residual).Take(10).Select(p=>$"{p.FaceA}-{p.FaceB}:{p.Separation}/r{p.Residual}"))}; admitted={string.Join(",",first.Thickness.SourcePairs.Where(p=>p.Admitted).Select(p=>$"{p.Family}:{p.FaceA}-{p.FaceB}:{p.Separation}"))}; diagnostics={string.Join(" | ",first.Diagnostics.Select(d=>d.Message))}");
        Assert.Equal(15,first.Part!.Regions.Count);Assert.Equal(7,first.Part.Bends.Count);Assert.Equal(17,first.Part.Features.Count);Assert.Equal(first.Part.StableId,second.Part!.StableId);
        var flat1=SheetMetalFlattener.Flatten(first.Part);var flat2=SheetMetalFlattener.Flatten(second.Part);Assert.Equal(flat1.DeterministicHash,flat2.DeterministicHash);Assert.NotEqual(FlatPatternStatus.Unsupported,flat1.Status);
        var validation=SheetMetalFlatPatternValidation.Validate(flat1);Assert.True(validation.Finite);Assert.True(validation.LoopsClosed);Assert.Empty(validation.Overlaps);
        var flatBody=SheetMetalManufacturingArtifacts.BuildFlatBody(first.Part,flat1);Assert.True(flatBody.IsSuccess,string.Join("\n",flatBody.Diagnostics.Select(d=>d.Message)));Assert.NotNull(flatBody.Body);
        var flatStep=Step242Exporter.ExportBody(flatBody.Body!);Assert.True(flatStep.IsSuccess,string.Join("\n",flatStep.Diagnostics.Select(d=>d.Message)));Assert.True(Step242Importer.ImportBody(flatStep.Value).IsSuccess);
        var recoveredSource=SheetMetalManufacturingArtifacts.WriteRecoveredFirmament(first.Part,path);var recovered=SheetMetalFirmament.Compile(recoveredSource,"recovered-ctc03.firmament");Assert.True(recovered.IsSuccess,string.Join("\n",recovered.Diagnostics.Select(d=>d.Message)));
        Assert.Equal(first.Part.Bends.Count,recovered.Part!.Bends.Count);Assert.Equal(first.Part.Features.Count,recovered.Part.Features.Count);Assert.True(flat1.DeterministicHash==recovered.FlatPattern!.DeterministicHash,$"expected={flat1.DeterministicHash} bounds={flat1.Bounds}; actual={recovered.FlatPattern.DeterministicHash} bounds={recovered.FlatPattern.Bounds}; expected bends={string.Join('|',flat1.BendLines.Select(b=>$"{b.BendId}:{b.Start}:{b.End}:{b.BendAllowance:R}"))}; actual bends={string.Join('|',recovered.FlatPattern.BendLines.Select(b=>$"{b.BendId}:{b.Start}:{b.End}:{b.BendAllowance:R}"))}");
    }

    [Fact]
    public void InvalidAuthoredRadius_IsTypedRejection()
    {
        var source="""
        SheetMetal Bad { Thickness: 2mm; Base: Rectangle(20mm, 20mm);
          Flange L { From: Base.Left; Length: 4mm; Angle: 90deg; InsideRadius: 3mm; }
          Flange R { From: Base.Right; Length: 4mm; Angle: 90deg; InsideRadius: 3mm; }
        }
        """;
        var result=SheetMetalFirmament.Compile(source);Assert.False(result.IsSuccess);Assert.Contains(result.Diagnostics,d=>d.Code=="sheetmetal-firmament-invalid");
    }

    [Fact]
    public void FlatOverlapDetection_RejectsPositiveAreaIntersectionButAllowsTouching()
    {
        FlatRegion2D Rect(string id,double x0,double x1)=>new(id,id,SheetRegionKind.Planar,[new(x0,0),new(x1,0),new(x1,10),new(x0,10)],"test");
        Assert.Single(SheetMetalFlatPatternValidation.FindOverlaps([Rect("a",0,10),Rect("b",5,15)]));
        Assert.Empty(SheetMetalFlatPatternValidation.FindOverlaps([Rect("a",0,10),Rect("b",10,20)]));
    }

    [Fact]
    public void AuthoredFormedBody_IsRecognizedByTheSameImportedBrepSystem()
    {
        var authored=SheetMetalFirmament.CompileFile(Path.Combine(RepoRoot,"fixtures/SheetMetal/simple-u-channel.firmament"));
        var recognized=SheetMetalRecognizer.Recognize(authored.Part!.FormedBody!,"authored-roundtrip.step");
        Assert.True(recognized.Thickness.IsPlausible);Assert.Equal(1.5,recognized.Thickness.NominalThickness!.Value,8);Assert.NotNull(recognized.Part);Assert.Equal(2,recognized.Part!.Bends.Count);
    }

    [Fact]
    public void DoubleCurvatureSolid_IsTypedUnsupportedRatherThanFlattened()
    {
        var sphere=Aetheris.Kernel.Core.Brep.BrepPrimitives.CreateSphere(10);Assert.True(sphere.IsSuccess);
        var result=SheetMetalRecognizer.Recognize(sphere.Value,"sphere.step");Assert.Null(result.Part);Assert.False(result.Thickness.IsPlausible);Assert.Contains(result.Diagnostics,d=>d.Code==SheetMetalDiagnosticCodes.NonConstantThickness);
    }

    [Fact]
    public void DisconnectedSheetGraph_ProducesPartialFlatWithDiagnostic()
    {
        var authored=SheetMetalFirmament.CompileFile(Path.Combine(RepoRoot,"fixtures/SheetMetal/simple-u-channel.firmament"));var part=authored.Part!;
        var orphan=part.Regions.First(r=>r.StableId=="region-left-flange") with { StableId="region-orphan" };
        var disconnected=part with { Regions=[..part.Regions,orphan] };
        var flat=SheetMetalFlattener.Flatten(disconnected);Assert.Equal(FlatPatternStatus.Partial,flat.Status);Assert.Contains(flat.Diagnostics,d=>d.Code==SheetMetalDiagnosticCodes.DisconnectedGraph);
    }

    [Fact]
    public void InvalidFlattenPolicyRadius_IsRejected()
        => Assert.Throws<ArgumentOutOfRangeException>(()=>new SheetMetalFlattenPolicy(.5).BendAllowance(Math.PI/2,-1,1));

    private static string FindRepoRoot(){var dir=new DirectoryInfo(AppContext.BaseDirectory);while(dir is not null&&!File.Exists(Path.Combine(dir.FullName,"Aetheris.slnx")))dir=dir.Parent;return dir?.FullName??throw new InvalidOperationException("Repo root not found.");}
}
