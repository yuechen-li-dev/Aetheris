using Aetheris.Kernel.Core.Math;
using Xunit;

namespace Aetheris.SheetMetal.Tests;

public sealed class SheetMetalM2Tests
{
    private static string RepoRoot=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../"));
    private static string Ctc=>Path.Combine(RepoRoot,"testdata/step242/nist/CTC/nist_ctc_03_asme1_ap242-e2.stp");
    private static string Intent=>Path.Combine(RepoRoot,"docs/modules/sheetmetal/artifacts/m2/ctc03-idiomatic.firmament");

    [Fact]
    public void Recovery_SeparatesEvidenceDraftAndBrief_WithBoundedNominals()
    {
        var recognized=SheetMetalRecognizer.RecognizeStep(Ctc);var result=SheetMetalIntentRecovery.Recover(recognized);
        Assert.Equal(SheetMetalProvenanceCategory.Recovered,result.Draft.Provenance);
        Assert.Contains(result.Evidence.NominalCandidates,n=>n.Quantity=="Thickness"&&Math.Abs(n.ProposedNominal-1.905)<1e-10&&n.Confidence==SheetMetalIntentConfidence.StrongCandidate);
        Assert.Contains(result.Evidence.NominalCandidates,n=>n.Quantity=="BendAngle"&&n.ProposedNominal==90);
        Assert.Contains(result.Evidence.GroupingCandidates,g=>g.Kind=="RepeatedBendPolicy");
        Assert.NotEmpty(result.Evidence.Corners);Assert.All(result.Evidence.Corners,c=>Assert.Equal(SheetCornerKind.Unknown,c.Kind));
        Assert.Empty(result.Evidence.Reliefs); // CTC vent slots are far from bend axes; do not misclassify them as reliefs.
        Assert.Contains("Ambiguities:",result.ReconstructionBrief);
        Assert.DoesNotContain("Boundary:",result.ReconstructionBrief);
    }

    [Fact]
    public void HistoricalIdiomaticCtc_CompilesButNowHonestlyFailsRecoveredHoleParity()
    {
        var source=SheetMetalRecognizer.RecognizeStep(Ctc).Part!;var authored=SheetMetalFirmament.CompileFile(Intent);
        Assert.True(authored.IsSuccess,string.Join('\n',authored.Diagnostics.Select(d=>d.Message)));Assert.Equal("MainDeck",authored.Part!.BaseRegionId);
        Assert.Contains(authored.Part.Bends,b=>b.StableId=="FrontWallBend");Assert.Contains(authored.Part.Features,f=>f.StableId=="VentSlotLeft");
        var comparison=SheetMetalIntentComparer.Compare(source,authored.Part);
        Assert.Equal(SheetMetalComparisonStatus.Fail,comparison.Status);Assert.True(comparison.SourceToIntent.Maximum>0);Assert.All(comparison.Bends,b=>Assert.Equal(SheetMetalComparisonStatus.Pass,b.Status));
        Assert.Contains(comparison.Diagnostics,x=>x.Contains("Feature count mismatch: source 17, intent 2",StringComparison.Ordinal));Assert.Equal(SheetMetalComparisonStatus.Fail,comparison.FlatPattern.Status);
    }

    [Fact]
    public void Comparer_LocalizesWrongBendAndWrongCut()
    {
        var source=SheetMetalRecognizer.RecognizeStep(Ctc).Part!;var intent=SheetMetalFirmament.CompileFile(Intent).Part!;
        var wrongBend=intent with{Bends=intent.Bends.Select((b,i)=>i==0?b with{BendAngleRadians=b.BendAngleRadians+5*Math.PI/180}:b).ToArray()};
        var bendReport=SheetMetalIntentComparer.Compare(source,wrongBend);Assert.Equal(SheetMetalComparisonStatus.Fail,bendReport.Status);Assert.Contains(bendReport.Bends,b=>b.IntentBendId==intent.Bends[0].StableId&&b.BendAngleResidualDegrees>4.9);
        var wrongCut=intent with{Features=intent.Features.Select((f,i)=>i==0?f with{Center=f.Center+new Vector3D(5,0,0)}:f).ToArray()};
        var cutReport=SheetMetalIntentComparer.Compare(source,wrongCut);Assert.Equal(SheetMetalComparisonStatus.Fail,cutReport.Status);Assert.Contains(cutReport.Features,f=>f.Status==SheetMetalComparisonStatus.Fail&&f.CenterResidual>4.9);
    }

    [Fact]
    public void OrderedConcaveSourceBoundary_IsNotReplacedByConvexHull()
    {
        var polygon=new[]{new SheetPoint2(0,0),new SheetPoint2(4,0),new SheetPoint2(4,4),new SheetPoint2(2,2),new SheetPoint2(0,4)};
        var normalized=SheetMetalFlattener.NormalizeSourcePolygon(polygon);Assert.Equal(5,normalized.Count);Assert.Equal(new SheetPoint2(2,2),normalized[3]);
    }
}
