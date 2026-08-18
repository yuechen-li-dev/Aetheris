using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;
using Xunit;

namespace Aetheris.SheetMetal.Tests;

public sealed class SheetMetalM3Tests
{
    private static readonly string RepoRoot=FindRepoRoot();

    [Theory]
    [InlineData("m3-l-bracket.firmament",1,0,0)]
    [InlineData("m3-u-channel.firmament",2,0,0)]
    [InlineData("m3-electronics-tray.firmament",4,4,4)]
    public void AuthoredFixtures_IndependentlyProduceManifoldFormedAndStitchedFlatArtifacts(string file,int bends,int cuts,int corners)
    {
        var result=SheetMetalFirmament.CompileFile(Path.Combine(RepoRoot,"fixtures/SheetMetal",file));
        Assert.True(result.IsSuccess,string.Join('\n',result.Diagnostics.Select(d=>d.Message)));var part=result.Part!;var flat=result.FlatPattern!;
        Assert.NotNull(part.FormedBody);Assert.Equal(bends,part.Bends.Count);Assert.Equal(cuts,part.Features.Count);Assert.Equal(corners,part.Corners?.Count??0);
        Assert.True((part.Correspondence?.Count??0)>=1+bends*2+cuts+corners);
        Assert.All(part.Regions,r=>{Assert.Empty(r.Source.FaceIds);Assert.Equal("sole construction authority",r.Source.SourceAuthority);});
        Assert.Equal(FlatPatternStatus.Valid,flat.Status);Assert.Equal(bends,flat.BendLines.Count);Assert.Equal(cuts,flat.CutLoops.Count);Assert.True(flat.Boundary.Count>=4);
        Assert.True(BrepExportPreflight.Validate(part.FormedBody!).IsValid);var formed=Step242Exporter.ExportBody(part.FormedBody!);Assert.True(formed.IsSuccess);Assert.True(Step242Importer.ImportBody(formed.Value).IsSuccess);
        var flatBody=SheetMetalManufacturingArtifacts.BuildFlatBody(part,flat);Assert.True(flatBody.IsSuccess,string.Join('\n',flatBody.Diagnostics.Select(d=>d.Message)));var flatStep=Step242Exporter.ExportBody(flatBody.Body!);Assert.True(flatStep.IsSuccess);Assert.True(Step242Importer.ImportBody(flatStep.Value).IsSuccess);
        var refold=SheetMetalRoundTrip.ValidateReferenceSurface(part,flat);Assert.True(refold.IsWithinTolerance,string.Join('\n',refold.Diagnostics.Select(d=>d.Message)));
        Assert.Equal(flat.DeterministicHash,SheetMetalFlattener.Flatten(part).DeterministicHash);
    }

    [Theory]
    [InlineData("",SheetCornerPolicy.Open,null)]
    [InlineData("Corner: Miter;",SheetCornerPolicy.Mitered,null)]
    [InlineData("Relief: Rectangular;",SheetCornerPolicy.Relief,SheetReliefKind.Rectangular)]
    [InlineData("Relief: Round;",SheetCornerPolicy.RoundRelief,SheetReliefKind.Round)]
    public void AdjacentCornerPolicies_AreTypedAndLowered(string policy,SheetCornerPolicy expected,SheetReliefKind? reliefKind)
    {
        var source=$$"""
        SheetMetal CornerCase { Thickness: 1mm; Base Main { Profile: Rectangle { Width: 40mm; Height: 30mm; }; }
          Flange A { From: Main.Front; Length: 10mm; Angle: 90deg; Radius: 1mm; {{policy}} }
          Flange B { From: Main.Right; Length: 10mm; Angle: 90deg; Radius: 1mm; {{policy}} }
        }
        """;
        var result=SheetMetalFirmament.Compile(source);Assert.True(result.IsSuccess,string.Join('\n',result.Diagnostics.Select(d=>d.Message)));var corner=Assert.Single(result.Part!.Corners!);Assert.Equal(expected,corner.Policy);
        if(reliefKind is null)Assert.Empty(result.Part.Reliefs!);else Assert.Equal(reliefKind,Assert.Single(result.Part.Reliefs!).Kind);
        Assert.True(BrepExportPreflight.Validate(result.Part.FormedBody!).IsValid);Assert.Equal(FlatPatternStatus.Valid,result.FlatPattern!.Status);
    }

    [Fact]
    public void BendDirectionAndAngle_AreGeneralAndDeterministic()
    {
        static string Source(string direction,double angle)=>$$"""
        SheetMetal BendCase { Thickness: 1mm; KFactor: 0.4; Base Main { Profile: Rectangle { Width: 30mm; Height: 20mm; }; }
          Flange Wall { From: Main.Right; Length: 12mm; Angle: {{angle}}deg; Radius: 1mm; Direction: {{direction}}; }
        }
        """;
        var up=SheetMetalFirmament.Compile(Source("Up",90));var down=SheetMetalFirmament.Compile(Source("Down",90));var angled=SheetMetalFirmament.Compile(Source("Up",45));
        Assert.True(up.IsSuccess);Assert.True(down.IsSuccess);Assert.True(angled.IsSuccess);
        var upZ=up.Part!.Regions.Single(r=>r.StableId=="Wall").Boundary3D.Average(p=>p.Z);var downZ=down.Part!.Regions.Single(r=>r.StableId=="Wall").Boundary3D.Average(p=>p.Z);
        Assert.True(upZ>0);Assert.True(downZ<0);Assert.Equal(45,angled.Part!.Bends.Single().BendAngleRadians*180/Math.PI,9);
        Assert.NotEqual(up.FlatPattern!.DeterministicHash,down.FlatPattern!.DeterministicHash);
    }

    [Fact]
    public void GraphAndCutFailures_AreTyped()
    {
        var duplicate="""
        SheetMetal Bad { Thickness: 1mm; Base Main { Profile: Rectangle { Width: 30mm; Height: 20mm; }; }
          Flange A { From: Main.Left; Length: 8mm; Angle: 90deg; Radius: 1mm; }
          Flange B { From: Main.Left; Length: 8mm; Angle: 90deg; Radius: 1mm; }
        }
        """;
        var crossing="""
        SheetMetal BadCut { Thickness: 1mm; Base Main { Profile: Rectangle { Width: 30mm; Height: 20mm; }; }
          Flange A { From: Main.Left; Length: 8mm; Angle: 90deg; Radius: 1mm; }
          Hole H { On: Main; Center: (1mm, 10mm); Diameter: 4mm; }
        }
        """;
        var a=SheetMetalFirmament.Compile(duplicate);Assert.False(a.IsSuccess);Assert.Contains(a.Diagnostics,d=>d.Code==SheetMetalDiagnosticCodes.DuplicateFlange);
        var b=SheetMetalFirmament.Compile(crossing);Assert.False(b.IsSuccess);Assert.Contains(b.Diagnostics,d=>d.Code==SheetMetalDiagnosticCodes.CutCrossesBend);
    }

    [Fact]
    public void Ctc03_ReconstructionCompilesWithNoEvidenceProviderOrSourceStep()
    {
        var path=Path.Combine(RepoRoot,"docs/development/milestones/modules/sheetmetal/artifacts/m2/ctc03-idiomatic.firmament");var source=File.ReadAllText(path);
        Assert.DoesNotContain("EvidenceSource",source);Assert.DoesNotContain("FromEvidence",source);
        var isolatedPath=Path.Combine(Path.GetTempPath(),$"ctc03-{Guid.NewGuid():N}.firmament");
        try
        {
            File.WriteAllText(isolatedPath,source);var result=SheetMetalFirmament.CompileFile(isolatedPath);
            Assert.True(result.IsSuccess,string.Join('\n',result.Diagnostics.Select(d=>d.Message)));Assert.NotNull(result.Part!.FormedBody);Assert.Equal(7,result.Part.Bends.Count);Assert.Equal(2,result.Part.Features.Count);Assert.Equal(FlatPatternStatus.Valid,result.FlatPattern!.Status);
            Assert.Contains("source-independent",result.Part.Provenance,StringComparison.OrdinalIgnoreCase);Assert.Equal(15,result.Part.Regions.Count);
        }
        finally{if(File.Exists(isolatedPath))File.Delete(isolatedPath);}
    }

    private static string FindRepoRoot(){var dir=new DirectoryInfo(AppContext.BaseDirectory);while(dir is not null&&!File.Exists(Path.Combine(dir.FullName,"Aetheris.slnx")))dir=dir.Parent;return dir?.FullName??throw new InvalidOperationException("Repo root not found.");}
}
