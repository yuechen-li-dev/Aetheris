using System.Text.Json;
using Aetheris.CLI;
using Aetheris.Kernel.Core.Step242;
using Aetheris.SheetMetal;
using Xunit;

namespace Aetheris.CLI.Tests;

public sealed class SheetMetalCliTests
{
    private static readonly string RepoRoot=FindRepoRoot();

    [Fact]
    public void InspectCtc03_ReportsRecoveredManufacturingSemantics()
    {
        var output=new StringWriter();var error=new StringWriter();var input=Path.Combine(RepoRoot,"testdata/step242/nist/CTC/nist_ctc_03_asme1_ap242-e2.stp");
        var exit=CliRunner.Run(["sheetmetal","inspect",input,"--json"],output,error);Assert.Equal(0,exit);Assert.Empty(error.ToString());
        using var json=JsonDocument.Parse(output.ToString());var root=json.RootElement;Assert.True(root.GetProperty("success").GetBoolean());Assert.Equal("Partial",root.GetProperty("recognitionStatus").GetString());Assert.Equal(7,root.GetProperty("sheetMetal").GetProperty("bends").GetArrayLength());Assert.Equal(17,root.GetProperty("sheetMetal").GetProperty("cuts").GetArrayLength());Assert.Equal("Partial",root.GetProperty("flatPattern").GetProperty("status").GetString());
    }

    [Fact]
    public void FlattenAuthored_WritesDeterministicSvgWithBendAndCutLayers()
    {
        var temp=Path.Combine(Path.GetTempPath(),$"aetheris-sheetmetal-{Guid.NewGuid():N}.svg");
        try
        {
            var output=new StringWriter();var error=new StringWriter();var input=Path.Combine(RepoRoot,"fixtures/Canonical/SheetMetal/u-channel.firmament");
            var exit=CliRunner.Run(["sheetmetal","flatten",input,"--output",temp,"--k-factor","0.42","--json"],output,error);Assert.Equal(0,exit);Assert.Empty(error.ToString());
            var svg=File.ReadAllText(temp);Assert.Contains("id=\"bend-lines\"",svg);Assert.Contains("id=\"bend-labels\" stroke=\"none\"",svg);Assert.Contains("id=\"cut-contours\"",svg);Assert.Contains("MountA",svg);Assert.Contains("Up 90°",svg);Assert.DoesNotContain("x1=\"35.66\" y1=\"42\" x2=\"35.66\" y2=\"42\"",svg);
            using var report=JsonDocument.Parse(output.ToString());Assert.Equal("5052-H32 Aluminum",report.RootElement.GetProperty("material").GetProperty("authored").GetString());
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
    }

    [Fact]
    public void BuildAuthoredSheetMetal_UsesRealStepExportPath()
    {
        var temp=Path.Combine(Path.GetTempPath(),$"aetheris-sheetmetal-{Guid.NewGuid():N}.step");
        try
        {
            var output=new StringWriter();var error=new StringWriter();var input=Path.Combine(RepoRoot,"fixtures/Canonical/SheetMetal/u-channel.firmament");
            var exit=CliRunner.Run(["build",input,"--output",temp,"--json"],output,error);Assert.Equal(0,exit);Assert.Empty(error.ToString());var step=File.ReadAllText(temp);Assert.Contains("MANIFOLD_SOLID_BREP",step);Assert.Contains("ADVANCED_FACE",step);
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
    }

    [Fact]
    public void BuildAuthoredSheetMetal_ReportsCanonicalHoleAsGeneratedFeature()
    {
        var temp=Path.Combine(Path.GetTempPath(),$"aetheris-sheetmetal-hole-{Guid.NewGuid():N}.step");
        try
        {
            var output=new StringWriter();var error=new StringWriter();
            var input=Path.Combine(RepoRoot,"fixtures/Canonical/SheetMetal/l-bracket-with-hole.firmament");
            var exit=CliRunner.Run(["build",input,"--output",temp,"--json"],output,error);
            Assert.Equal(0,exit);Assert.Empty(error.ToString());
            using var report=JsonDocument.Parse(output.ToString());
            Assert.Equal(1,report.RootElement.GetProperty("part").GetProperty("features").GetInt32());
            Assert.Equal("5052-H32 Aluminum",report.RootElement.GetProperty("part").GetProperty("material").GetString());
            var analysis=StepAnalyzer.Analyze(temp);
            Assert.Equal(3,analysis.Summary.SurfaceFamilies["cylinder"]);
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
    }

    [Fact]
    public void BuildAuthoredSheetMetal_RejectsModelDomainHoleSyntaxInsteadOfIgnoringIntent()
    {
        var canonical=File.ReadAllText(Path.Combine(RepoRoot,"fixtures/Canonical/SheetMetal/l-bracket-with-hole.firmament"));
        var source=canonical.Replace("Hole Mount", "Hole<Shaft> Mount", StringComparison.Ordinal);
        var dir=Path.Combine(Path.GetTempPath(),$"aetheris-sheetmetal-domain-{Guid.NewGuid():N}");Directory.CreateDirectory(dir);
        try
        {
            var input=Path.Combine(dir,"wrong-domain.firmament");File.WriteAllText(input,source);
            var output=new StringWriter();var error=new StringWriter();
            var exit=CliRunner.Run(["build",input,"--json"],output,error);
            Assert.Equal(1,exit);Assert.Empty(error.ToString());
            Assert.Contains("sheetmetal-hole-domain-syntax",output.ToString(),StringComparison.Ordinal);
            Assert.Contains("Hole Mount",output.ToString(),StringComparison.Ordinal);
        }
        finally{Directory.Delete(dir,true);}
    }

    [Fact]
    public void BuildAuthoredSheetMetal_ExplainsModelDomainPmiSyntax()
    {
        var source=File.ReadAllText(Path.Combine(RepoRoot,"fixtures/Canonical/SheetMetal/l-bracket-with-hole.firmament"));
        source=source.Insert(source.LastIndexOf('}'),"\n    Pmi { Datum A { Target: face(-Z) } }\n");
        var dir=Path.Combine(Path.GetTempPath(),$"aetheris-sheetmetal-pmi-domain-{Guid.NewGuid():N}");Directory.CreateDirectory(dir);
        try
        {
            var input=Path.Combine(dir,"wrong-pmi-domain.firmament");File.WriteAllText(input,source);
            var output=new StringWriter();var error=new StringWriter();
            var exit=CliRunner.Run(["build",input,"--json"],output,error);
            Assert.Equal(1,exit);Assert.Empty(error.ToString());
            Assert.Contains("sheetmetal-pmi-domain-syntax",output.ToString(),StringComparison.Ordinal);
            Assert.Contains("DatumFeature A",output.ToString(),StringComparison.Ordinal);
        }
        finally{Directory.Delete(dir,true);}
    }

    [Fact]
    public void ValidateRoutesModuleShapedSheetMetalThroughDomainCompiler()
    {
        var output=new StringWriter();var error=new StringWriter();var input=Path.Combine(RepoRoot,"docs/development/milestones/modules/sheetmetal/artifacts/m8/ctc03-final.firmament");
        var exit=CliRunner.Run(["validate",input,"--json"],output,error);
        Assert.Equal(0,exit);Assert.Empty(error.ToString());
        using var json=JsonDocument.Parse(output.ToString());var validation=json.RootElement.GetProperty("sheetMetalValidation");
        Assert.Equal("valid",validation.GetProperty("status").GetString());Assert.Equal(7,validation.GetProperty("summary").GetProperty("bends").GetInt32());Assert.True(validation.GetProperty("summary").GetProperty("exactBlank").GetBoolean());
    }

    [Fact]
    public void FlattenCtc03_WritesReimportableStepAndRecompilableRecoveredFirmament()
    {
        var dir=Path.Combine(Path.GetTempPath(),$"aetheris-sheetmetal-{Guid.NewGuid():N}");Directory.CreateDirectory(dir);
        try
        {
            var step=Path.Combine(dir,"ctc03-flat.step");var firmament=Path.Combine(dir,"ctc03-recovered.firmament");var svg=Path.Combine(dir,"ctc03-flat.svg");
            var output=new StringWriter();var error=new StringWriter();var input=Path.Combine(RepoRoot,"testdata/step242/nist/CTC/nist_ctc_03_asme1_ap242-e2.stp");
            var exit=CliRunner.Run(["sheetmetal","flatten",input,"--step",step,"--firmament",firmament,"--svg",svg,"--json"],output,error);
            Assert.Equal(0,exit);Assert.Empty(error.ToString());Assert.True(Step242Importer.ImportBody(File.ReadAllText(step)).IsSuccess);Assert.True(SheetMetalFirmament.CompileFile(firmament).IsSuccess);Assert.Contains("id=\"bend-labels\" stroke=\"none\"",File.ReadAllText(svg));
            using var report=JsonDocument.Parse(output.ToString());Assert.Equal(Path.GetFullPath(step),report.RootElement.GetProperty("flatPattern").GetProperty("step").GetString());
        }
        finally{Directory.Delete(dir,true);}
    }

    [Fact]
    public void RecoverAndCompare_ProvideLlmBriefAndLocalizedResiduals()
    {
        var dir=Path.Combine(Path.GetTempPath(),$"aetheris-sheetmetal-m2-{Guid.NewGuid():N}");Directory.CreateDirectory(dir);
        try
        {
            var source=Path.Combine(RepoRoot,"testdata/step242/nist/CTC/nist_ctc_03_asme1_ap242-e2.stp");var intent=Path.Combine(RepoRoot,"docs/development/milestones/modules/sheetmetal/artifacts/m2/ctc03-idiomatic.firmament");
            var output=new StringWriter();var error=new StringWriter();var recover=CliRunner.Run(["sheetmetal","recover",source,"--out-dir",dir,"--json"],output,error);
            Assert.Equal(0,recover);Assert.Empty(error.ToString());Assert.Contains("Ambiguities:",File.ReadAllText(Path.Combine(dir,"reconstruction-brief.md")));Assert.True(File.Exists(Path.Combine(dir,"recovery-summary.json")));
            output.GetStringBuilder().Clear();var compare=CliRunner.Run(["sheetmetal","compare",source,intent,"--json"],output,error);Assert.Equal(2,compare);Assert.Empty(error.ToString());
            using var report=JsonDocument.Parse(output.ToString());Assert.False(report.RootElement.GetProperty("success").GetBoolean());Assert.Equal("Fail",report.RootElement.GetProperty("comparison").GetProperty("status").GetString());Assert.Equal(7,report.RootElement.GetProperty("comparison").GetProperty("bends").GetArrayLength());Assert.Contains(report.RootElement.GetProperty("comparison").GetProperty("diagnostics").EnumerateArray(),x=>x.GetString()!.Contains("Feature count mismatch: source 17, intent 2",StringComparison.Ordinal));
        }
        finally{Directory.Delete(dir,true);}
    }

    [Fact]
    public void RecognizeThenRecoverFlat_WritesInspectableSourceDerivedArtifacts()
    {
        var dir=Path.Combine(Path.GetTempPath(),$"aetheris-recognized-flat-{Guid.NewGuid():N}");Directory.CreateDirectory(dir);
        try
        {
            var source=Path.Combine(RepoRoot,"testdata/step242/nist/CTC/nist_ctc_03_asme1_ap242-e2.stp");var plan=Path.Combine(dir,"recognition-plan.json");var output=new StringWriter();var error=new StringWriter();
            var recognize=CliRunner.Run(["sheetmetal","recognize",source,"--plan",plan,"--json"],output,error);Assert.Equal(0,recognize);Assert.Empty(error.ToString());Assert.True(File.Exists(plan));
            using(var report=JsonDocument.Parse(output.ToString())){Assert.Equal(7,report.RootElement.GetProperty("bends").GetArrayLength());Assert.All(report.RootElement.GetProperty("bends").EnumerateArray(),x=>Assert.Equal("Recognized",x.GetProperty("acceptedStatus").GetString()));}
            output.GetStringBuilder().Clear();var recover=CliRunner.Run(["sheetmetal","recover-flat",source,"--recognition-plan",plan,"--out-dir",dir,"--json"],output,error);Assert.Equal(0,recover);Assert.Empty(error.ToString());
            var reference=Path.Combine(dir,"recovered-flat.json");Assert.True(File.Exists(reference));Assert.True(File.Exists(Path.Combine(dir,"recovered-flat.svg")));
            using var flat=JsonDocument.Parse(File.ReadAllText(reference));Assert.Equal("aetheris.recovered-flat-reference.v2",flat.RootElement.GetProperty("schema").GetString());Assert.Equal("RecoveredWithRepairs",flat.RootElement.GetProperty("contourAcceptance").GetString());Assert.Equal(3,flat.RootElement.GetProperty("junctionRepairs").GetArrayLength());Assert.Equal(17,flat.RootElement.GetProperty("cuts").GetArrayLength());Assert.Equal(7,flat.RootElement.GetProperty("bendLines").GetArrayLength());Assert.True(flat.RootElement.GetProperty("sourceProvenance").GetArrayLength()>=100);
            output.GetStringBuilder().Clear();var native=Path.Combine(RepoRoot,"docs/development/milestones/modules/sheetmetal/artifacts/m8/ctc03-final.firmament");var compare=CliRunner.Run(["sheetmetal","compare-flat",reference,native,"--semantic","--json"],output,error);Assert.Equal(0,compare);Assert.Empty(error.ToString());
            using var semantic=JsonDocument.Parse(output.ToString());var local=semantic.RootElement.GetProperty("semanticComparison");Assert.True(local.GetProperty("targets").GetArrayLength()>60);Assert.Equal(4,local.GetProperty("targets").EnumerateArray().Count(x=>x.GetProperty("geometryKind").GetString()=="BendTermination"));
        }
        finally{Directory.Delete(dir,true);}
    }

    private static string FindRepoRoot(){var dir=new DirectoryInfo(AppContext.BaseDirectory);while(dir is not null&&!File.Exists(Path.Combine(dir.FullName,"Aetheris.slnx")))dir=dir.Parent;return dir?.FullName??throw new InvalidOperationException("Repo root not found.");}
}
