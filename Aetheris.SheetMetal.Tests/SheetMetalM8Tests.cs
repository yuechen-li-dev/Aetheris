using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;
using Xunit;

namespace Aetheris.SheetMetal.Tests;

public sealed class SheetMetalM8Tests
{
    private static readonly string RepoRoot=FindRepoRoot();
    private static string CtcStep=>Path.Combine(RepoRoot,"testdata/step242/nist/CTC/nist_ctc_03_asme1_ap242-e2.stp");
    private static string CtcIntent=>Path.Combine(RepoRoot,"docs/modules/sheetmetal/artifacts/m8/ctc03-final.firmament");

    [Fact]
    public void Ctc03_RecoversEveryCircularAndProfileOpeningFromPairedPlanarSkins()
    {
        var recovered=SheetMetalRecognizer.RecognizeStep(CtcStep);var part=Assert.IsType<SheetMetalPartIr>(recovered.Part);
        Assert.Equal(15,part.Features.Count(x=>x.Kind==SheetFeatureKind.CircularHole));
        Assert.Equal(2,part.Features.Count(x=>x.Kind==SheetFeatureKind.Slot));
        Assert.Contains(part.Features,x=>x.Diameter is { } d&&Math.Abs(d-4.7625)<1e-6);
        Assert.Contains(part.Features,x=>x.Diameter is { } d&&Math.Abs(d-50.8)<1e-6);
    }

    [Fact]
    public void Ctc03_FinalSemanticLayout_LowersAllOpeningsAndPartialTabFlangeWithoutSourceStep()
    {
        var isolated=Path.Combine(Path.GetTempPath(),$"ctc03-m8-{Guid.NewGuid():N}.firmament");
        try
        {
            File.Copy(CtcIntent,isolated);var result=SheetMetalFirmament.CompileFile(isolated);
            Assert.True(result.IsSuccess,string.Join('\n',result.Diagnostics.Select(x=>x.Message)));
            Assert.Equal(15,result.Part!.Features.Count(x=>x.Kind==SheetFeatureKind.CircularHole));
            Assert.Equal(2,result.Part.Features.Count(x=>x.Kind==SheetFeatureKind.Slot));
            Assert.Equal(9,result.Spec!.SemanticLayout.Patterns.Count);Assert.Single(result.Spec.SemanticLayout.Tabs);Assert.Equal(2,result.Spec.SemanticLayout.SteppedNotches!.Count);
            Assert.Equal(127,result.Part.Regions.Single(x=>x.StableId=="AngledServiceFlangeBendRegion").Cylinder!.AxisLength,8);
            Assert.NotNull(result.FlatPattern!.ExactBlankContour);Assert.Equal(17,result.FlatPattern.CutLoops.Count);
            Assert.True(BrepExportPreflight.Validate(result.Part.FormedBody!).IsValid);
            var paths=SheetMetalConceptPaths.Inspect(result.Spec,result.Part,result.FlatPattern).Select(x=>x.Path).ToArray();
            Assert.Contains("Ctc03Layout.FrontConnectorRelief",paths);Assert.Contains("Flat.Ctc03Layout.FrontConnectorRelief",paths);
        }
        finally{if(File.Exists(isolated))File.Delete(isolated);}
    }

    [Fact]
    public void Ctc03_FinalSemanticLayout_HasCompleteFeatureParityAtComparisonTolerance()
    {
        var source=SheetMetalRecognizer.RecognizeStep(CtcStep).Part!;var intent=SheetMetalFirmament.CompileFile(CtcIntent).Part!;
        var comparison=SheetMetalIntentComparer.Compare(source,intent);
        Assert.Equal(17,comparison.Features.Count);Assert.All(comparison.Features,x=>Assert.Equal(SheetMetalComparisonStatus.Pass,x.Status));
        Assert.All(comparison.Bends,x=>Assert.Equal(SheetMetalComparisonStatus.Pass,x.Status));
    }

    [Theory]
    [InlineData("EqualSize", "10mm", "11mm", "sheetmetal-semantic-equal-size")]
    [InlineData("EqualPitch", "10mm", "10mm", "sheetmetal-semantic-equal-pitch")]
    [InlineData("Mirror", "10mm", "10mm", "sheetmetal-semantic-mirror")]
    public void SemanticContracts_RejectStatedEngineeringContradictions(string kind,string firstDiameter,string secondDiameter,string code)
    {
        var secondX=kind=="EqualPitch"?"41mm":kind=="Mirror"?"81mm":"40mm";
        var extra=kind=="EqualPitch"?"Axis: X; Pitch: 20mm;":kind=="Mirror"?"About: Panel.CenterX;":"";
        var source=$$"""
        SheetMetal Contradiction { Thickness: 1.5mm; Base Panel { Profile: Rectangle { Width: 100mm; Height: 60mm; }; }
          Flange Lip { From: Panel.Rear; Height: 15mm; Angle: 90deg; Radius: 2mm; }
          Hole Left { On: Panel; Center: (20mm, 20mm); Diameter: {{firstDiameter}}; }
          Hole Right { On: Panel; Center: ({{secondX}}, 20mm); Diameter: {{secondDiameter}}; }
          Require StatedIntent { Kind: {{kind}}; Members: [Left, Right]; {{extra}} }
        }
        """;
        var result=SheetMetalFirmament.Compile(source);Assert.False(result.IsSuccess);Assert.Contains(result.Diagnostics,x=>x.Code==code);
    }

    [Fact]
    public void SemanticLayout_GeneralizesToNonCtcPanelAndRetainsStablePaths()
    {
        var path=Path.Combine(RepoRoot,"fixtures/FirmamentV2/SheetMetal/m8-semantic-panel.firmament");var first=SheetMetalFirmament.CompileFile(path);var second=SheetMetalFirmament.CompileFile(path);
        Assert.True(first.IsSuccess,string.Join('\n',first.Diagnostics.Select(x=>x.Message)));Assert.Equal(4,first.Part!.Features.Count);
        var paths=SheetMetalConceptPaths.Inspect(first.Spec!,first.Part,first.FlatPattern).Select(x=>x.Path).ToArray();
        Assert.Contains("PanelLayout.MountingHoles[0]",paths);Assert.Contains("PanelLayout.MountingHoles[3]",paths);
        Assert.Equal(first.FlatPattern!.DeterministicHash,second.FlatPattern!.DeterministicHash);
    }

    [Theory]
    [InlineData("Span: 0mm;")]
    [InlineData("SpanOffset: 1mm;")]
    public void PartialEdgeFlange_RejectsIncompleteOrDegenerateSpanIntent(string spanIntent)
    {
        var source=$$"""
        SheetMetal InvalidSpan {
          Thickness: 1mm;
          Base Panel { Profile: Rectangle { Width: 100mm; Height: 60mm; }; }
          Flange Lip { From: Panel.Rear; Height: 15mm; {{spanIntent}} Angle: 90deg; Radius: 2mm; }
        }
        """;
        var result=SheetMetalFirmament.Compile(source);Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics,x=>x.Code=="sheetmetal-flange-span-invalid");
    }

    [Fact]
    public void Ctc03_FinalArtifacts_ExportAndReimportAsClosedBodies()
    {
        var result=SheetMetalFirmament.CompileFile(CtcIntent);Assert.True(result.IsSuccess);
        var formed=Step242Exporter.ExportBody(result.Part!.FormedBody!);Assert.True(formed.IsSuccess);Assert.True(Step242Importer.ImportBody(formed.Value).IsSuccess);
        var flatBody=SheetMetalManufacturingArtifacts.BuildFlatBody(result.Part,result.FlatPattern!);Assert.True(flatBody.IsSuccess,string.Join('\n',flatBody.Diagnostics.Select(x=>x.Message)));
        var flat=Step242Exporter.ExportBody(flatBody.Body!);Assert.True(flat.IsSuccess);Assert.True(Step242Importer.ImportBody(flat.Value).IsSuccess);
    }

    private static string FindRepoRoot(){var dir=new DirectoryInfo(AppContext.BaseDirectory);while(dir is not null&&!File.Exists(Path.Combine(dir.FullName,"Aetheris.slnx")))dir=dir.Parent;return dir?.FullName??throw new InvalidOperationException("Repo root not found.");}
}
