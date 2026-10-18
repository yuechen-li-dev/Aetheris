using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Structural;
using Aetheris.Kernel.Firmament.Drawing;
using Aetheris.Surfacing;
using Aetheris.Collaboration;
namespace Aetheris.Kernel.Firmament.Tests;

public sealed class RemainingDomainAstTests
{
    [Theory]
    [InlineData("heigth: 2mm")]
    [InlineData("Fillet Lost { radius: 1mm }")]
    [InlineData("on: XZ")]
    public void StructuralCannotIgnoreFieldsOrChildren(string intent)
    {
        var source=Read("Structural/simple-structural-frame.firmament");
        var offset=source.IndexOf("Structure ",StringComparison.Ordinal);offset=source.IndexOf('{',offset)+1;
        var result=StructuralAuthoring.Compile(source.Insert(offset,"\n"+intent+"\n"),"structural.firmament");
        Assert.False(result.IsSuccess);Assert.Null(result.StepText);
        Assert.Contains(result.FrontendDiagnostics,d=>d.SourceSpan is not null && d.SourceFile=="structural.firmament");
        Assert.False(FirmamentBuildAndExport.CompileSource(source.Insert(offset,intent)).IsSuccess);
    }
    [Theory]
    [InlineData("heigth: 2mm")]
    [InlineData("Fillet Lost { radius: 1mm }")]
    public void SculptingCannotIgnoreIntent(string intent)
    {
        var source=Read("Sculpting/sculpt-base-housing.firmament");
        var at=source.IndexOf("BodyState ",StringComparison.Ordinal);at=source.IndexOf('{',at)+1;
        var result=SculptingAuthoring.Compile(source.Insert(at,intent+";"),"housing.firmament");
        Assert.False(result.IsSuccess);Assert.Contains(result.Diagnostics,d=>d.SourceSpan is not null && d.SourceFile=="housing.firmament");
    }
    [Fact]
    public void SculptingHeaderPlacementCannotSilentlyChangeThePlane()
    {
        var source=Read("Sculpting/sculpt-base-housing.firmament");
        var at=source.IndexOf("BodyState ",StringComparison.Ordinal);at=source.IndexOf('{',at);
        var result=SculptingAuthoring.Compile(source.Insert(at,"on XZ "),"housing.firmament");
        Assert.False(result.IsSuccess);Assert.Contains(result.Diagnostics,d=>d.Code=="firmament-v2-placement-unsupported");
    }
    [Fact]
    public void StructuralAndSculptingFieldAliasesRetainExactGeometryAndStableIdentity()
    {
        var source=Read("Structural/simple-structural-frame.firmament");
        var first=StructuralAuthoring.Compile(source);var second=StructuralAuthoring.Compile(source.Replace("Width:","width:",StringComparison.Ordinal));
        Assert.True(first.IsSuccess);Assert.True(second.IsSuccess,string.Join(";",second.Diagnostics));Assert.Equal(first.StepText,second.StepText);
        var sculpt=Read("Sculpting/sculpt-base-housing.firmament");
        var a=SculptingAuthoring.Compile(sculpt);var b=SculptingAuthoring.Compile(sculpt.Replace("Size:","size:",StringComparison.Ordinal));
        Assert.True(a.IsSuccess);Assert.True(b.IsSuccess);Assert.Equal(a.OutputState!.StateId,b.OutputState!.StateId);
        Assert.Equal(SculptStepExporter.Export(a.OutputState,a.ModelName).Step,SculptStepExporter.Export(b.OutputState,b.ModelName).Step);
    }
    [Fact]
    public void ScopedStructuralTableSchemaCannotBeBypassed()
    {
        var source=Read("Structural/welded-workbench.firmament");
        var at=source.IndexOf("Record StructuralMemberRow",StringComparison.Ordinal);at=source.IndexOf('{',at)+1;
        var result=StructuralAuthoring.Compile(source.Insert(at,"extraIntent: String;"));
        Assert.False(result.IsSuccess);Assert.Contains(result.Diagnostics,d=>d.StartsWith("firmament-template-table-missing-column",StringComparison.Ordinal));
    }
    [Theory]
    [InlineData("Orientation: Landscape","orientation: Diagonal")]
    [InlineData("Direction: +Z","direction: [0,0,0]")]
    [InlineData("HiddenLines: VisibleOnly","hiddenLines: Gibberish")]
    [InlineData("PMI: []","pmi: []; heigth: 2mm")]
    [InlineData("Source: Item","source: Item; Source: Item")]
    public void DrawingUnknownOrInvalidIntentRejectsBeforeArtifacts(string before,string after)
    {
        var directory=Path.Combine(Root(),"artifacts","local","remaining-domain-tests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,"drawing.firmament");File.WriteAllText(path,Read("Drawings/bearing-block-production-drawing.firmament").Replace(before,after,StringComparison.Ordinal));
        var result=FirmamentDrawingCompiler.Compile(path,directory);Assert.False(result.IsSuccess);Assert.Null(result.Artifacts);Assert.Empty(Directory.GetFiles(directory,"*.pdf"));
    }
    [Fact]
    public void DrawingAcronymsHaveOneModernSpellingAndKeepHistoricalAliases()
    {
        Assert.Equal("pmi",FirmamentSourceSpelling.PreferredField("PMI"));Assert.Equal("bom",FirmamentSourceSpelling.PreferredField("BOM"));Assert.Equal("uvBounds",FirmamentSourceSpelling.PreferredField("UVBounds"));
        Assert.True(Aetheris.Kernel.Firmament.FirmamentV2.Syntax.FirmamentSyntaxFields.SameField("pMI","PMI"));
        Assert.True(Aetheris.Kernel.Firmament.FirmamentV2.Syntax.FirmamentSyntaxFields.SameField("pmi","PMI"));
    }
    [Fact]
    public void SculptingEditorAnalysisReportsSyntaxWithoutClaimingEngineeringAdmission()
    {
        var source=Read("Sculpting/sculpt-base-housing.firmament");
        var report=FirmamentLanguageAnalysisService.Analyze(source,"housing.firmament","1");
        Assert.DoesNotContain(report.Diagnostics,d=>d.Severity=="error");
        Assert.Contains(report.Diagnostics,d=>d.Code=="firmament-language-domain-context-required" && d.Start>0);
        var broken=source[..source.LastIndexOf('}')];
        var errors=FirmamentLanguageAnalysisService.Analyze(broken,"housing.firmament","2");
        Assert.Contains(errors.Diagnostics,d=>d.Code.StartsWith("firmament-syntax-",StringComparison.Ordinal) && d.Severity=="error");
    }

    private static string Read(string path)=>File.ReadAllText(Path.Combine(Root(),"fixtures","Canonical",path));
    private static string Root(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d is not null&&!File.Exists(Path.Combine(d.FullName,"Aetheris.slnx")))d=d.Parent;return d!.FullName;}
}
