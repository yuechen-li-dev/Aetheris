using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;
using Xunit;

namespace Aetheris.SheetMetal.Tests;

public sealed class RecoveredContourStitchingM2Tests
{
    private static readonly string RepoRoot=FindRepoRoot();
    private static string Ctc=>Path.Combine(RepoRoot,"testdata/step242/nist/CTC/nist_ctc_03_asme1_ap242-e2.stp");

    [Fact]
    public void Ctc03_RecoveryCollapsesExactlyThreePointTangentMicroClosuresIntoAValidAnalyticBlank()
    {
        var model=RecognizedSheetMetalRecovery.FromDetection(SheetMetalRecognizer.RecognizeStep(Ctc));
        var recovered=RecoveredSourceFlattener.Flatten(model,RecognizedSheetMetalRecovery.CreateAutomaticPlan(model));
        Assert.Equal(FlatPatternStatus.Valid,recovered.Status);Assert.Equal(RecoveredContourAcceptance.RecoveredWithRepairs,recovered.ContourAcceptance);
        Assert.NotNull(recovered.OuterAndInnerContours);Assert.True(PlanarContourKernel.Validate(recovered.OuterAndInnerContours).IsValid);
        Assert.Equal(17,recovered.OuterAndInnerContours.InnerLoops.Count);Assert.Equal(7,recovered.BendLines.Count);
        Assert.Equal(3,recovered.JunctionRepairs.Count);Assert.All(recovered.JunctionRepairs,x=>
        {
            Assert.Equal(RecoveryJunctionKind.PointTangentContinuation,x.Kind);Assert.Equal(RecoveryRepairConfidence.Strong,x.Confidence);
            Assert.InRange(x.MaximumDisplacement,0,RecoveryContourStitcher.JunctionTolerance);
        });
        Assert.Equal(3,recovered.StitchSummary!.AmbiguousJunctionCount);Assert.Empty(recovered.StitchSummary.Rejections);
        Assert.Contains(recovered.OuterAndInnerContours.OuterLoop.Segments,x=>x.Geometry is LineArcCircularArc2D);
    }

    [Fact]
    public void SameProfileVendorGap_IsClusteredLocally_WithoutPolygonizingComplementaryArcs()
    {
        var p=Provenance("vendor-loop");
        ArrangementFragment2D F(string id,LineArcProfileCurve2D geometry)=>new(id,
            new(id,"region",PrismaticProfileIntent.Add,"vendor-loop","outer",id,geometry,p with{StableId=id}),0,1,geometry,true,true);
        const double noise=1e-6;
        var fragments=new[]
        {
            F("bottom",new LineArcLineSegment2D((0,0),(10,0))),
            F("right",new LineArcLineSegment2D((10+noise,0),(10,10))),
            F("top",new LineArcLineSegment2D((10,10),(0,10))),
            F("left",new LineArcLineSegment2D((0,10),(0,0)))
        };
        var arrangement=new ProfileArrangement2D("XY",fragments.Select(x=>x.Source).ToArray(),[],fragments,[],0,[],TimeSpan.Zero,TimeSpan.Zero,TimeSpan.Zero,TimeSpan.Zero);
        var result=RecoveryContourStitcher.Stitch(arrangement,"noisy-vendor-square");
        Assert.NotNull(result.Contour);Assert.Equal(RecoveredContourAcceptance.RecoveredWithRepairs,result.Acceptance);
        Assert.Single(result.Repairs);Assert.Equal(RecoveryJunctionKind.WithinToleranceEndpointMatch,result.Repairs[0].Kind);
        Assert.True(PlanarContourKernel.Validate(result.Contour).IsValid);

        var arcs=new LineArcProfileCurve2D[]
        {
            new LineArcCircularArc2D((0,0),5,0,Math.PI),
            new LineArcCircularArc2D((0,0),5,Math.PI,Math.PI)
        };
        var arcFragments=arcs.Select((x,i)=>F($"arc-{i}",x)).ToArray();
        var arcArrangement=new ProfileArrangement2D("XY",arcFragments.Select(x=>x.Source).ToArray(),[],arcFragments,[],0,[],TimeSpan.Zero,TimeSpan.Zero,TimeSpan.Zero,TimeSpan.Zero);
        var circle=RecoveryContourStitcher.Stitch(arcArrangement,"complementary-arcs");
        Assert.NotNull(circle.Contour);Assert.Equal(2,circle.Contour.OuterLoop.Segments.Count);
        Assert.NotEqual(circle.Contour.OuterLoop.Segments[0].Geometry,circle.Contour.OuterLoop.Segments[1].Geometry);
    }

    [Fact]
    public void SignedZeroPerturbedDumbStep_StillRecognizesAndRecoversGenericUChannel()
    {
        var source=SheetMetalFirmament.CompileFile(Path.Combine(RepoRoot,"fixtures/SheetMetal/simple-u-channel.firmament"));Assert.True(source.IsSuccess);
        var exported=Step242Exporter.ExportBody(source.Part!.FormedBody!);Assert.True(exported.IsSuccess);
        // A vendor-style text perturbation changes numeric spelling only; native
        // Firmament source is deliberately absent from the recovery call below.
        var dirty=exported.Value.Replace("(0.,", "(-0.,",StringComparison.Ordinal);
        var path=Path.Combine(Path.GetTempPath(),$"aetheris-noisy-u-{Guid.NewGuid():N}.step");
        try
        {
            File.WriteAllText(path,dirty);var detection=SheetMetalRecognizer.RecognizeStep(path);Assert.NotNull(detection.Part);
            var model=RecognizedSheetMetalRecovery.FromDetection(detection);var recovered=RecoveredSourceFlattener.Flatten(model,RecognizedSheetMetalRecovery.CreateAutomaticPlan(model));
            Assert.Equal(FlatPatternStatus.Valid,recovered.Status);Assert.NotNull(recovered.OuterAndInnerContours);Assert.Equal(2,recovered.BendLines.Count);
        }
        finally{File.Delete(path);}
    }

    private static ProfileSegmentProvenance Provenance(string id)=>new(id,id,"test","vendor-noise recovery fixture","XY");
    private static string FindRepoRoot(){var dir=new DirectoryInfo(AppContext.BaseDirectory);while(dir is not null&&!File.Exists(Path.Combine(dir.FullName,"Aetheris.slnx")))dir=dir.Parent;return dir?.FullName??throw new InvalidOperationException("Repo root not found.");}
}
