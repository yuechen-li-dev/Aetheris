namespace Aetheris.CLI.Tests;

using System.Text.Json;

public sealed class FeaCliTests
{
    [Fact]
    public void FeaCommand_RunsCanonicalRealPathAndWritesVerificationPackage()
    {
        var root=FindRoot();var source=Path.Combine(root,"docs","fea","artifacts","m5","plate-with-hole.firmament");var output=Path.Combine(Path.GetTempPath(),"aetheris-fea-cli-"+Guid.NewGuid().ToString("N"));
        try
        {
            var stdout=new StringWriter();var stderr=new StringWriter();
            var exit=CliRunner.Run(["fea",source,"--out-dir",output,"--json"],stdout,stderr);
            Assert.Equal(0,exit);Assert.Empty(stderr.ToString());Assert.Contains("\"converged\": true",stdout.ToString());
            Assert.True(File.Exists(Path.Combine(output,"analysis-ir.json")));Assert.True(File.Exists(Path.Combine(output,"verification.inp")));
        }
        finally { if(Directory.Exists(output))Directory.Delete(output,true); }
    }

    [Fact]
    public void FeaCommand_InlineStepAndLatticeOverride_ReportProductionIdentity()
    {
        var root=FindRoot();var source=Path.Combine(root,"fixtures","FirmamentV2","FEA","inline-step-through-hole.firmament");var output=Path.Combine(Path.GetTempPath(),"aetheris-fea-x1-cli-"+Guid.NewGuid().ToString("N"));
        try
        {
            var stdout=new StringWriter();var stderr=new StringWriter();
            var exit=CliRunner.Run(["fea",source,"--lattice","4,4,3","--out-dir",output,"--json"],stdout,stderr);
            Assert.True(exit==0,$"exit={exit}; stderr={stderr}");Assert.Empty(stderr.ToString());
            using var json=JsonDocument.Parse(stdout.ToString());var rootElement=json.RootElement;
            Assert.Equal("InlineStep",rootElement.GetProperty("analysis").GetProperty("sourceKind").GetString());
            Assert.Equal(4,rootElement.GetProperty("analysis").GetProperty("lattice").GetProperty("countX").GetInt32());
            Assert.Equal("TotalResultantOverSelectedArea",rootElement.GetProperty("analysis").GetProperty("loads")[0].GetProperty("distribution").GetString());
            Assert.True(rootElement.GetProperty("solver").GetProperty("converged").GetBoolean());
            Assert.InRange(rootElement.GetProperty("equilibrium").GetProperty("residualNewton").GetProperty("length").GetDouble(),0,1e-3);
        }
        finally { if(Directory.Exists(output))Directory.Delete(output,true); }
    }

    private static string FindRoot(){var directory=new DirectoryInfo(AppContext.BaseDirectory);while(directory is not null&&!File.Exists(Path.Combine(directory.FullName,"Aetheris.slnx")))directory=directory.Parent;return directory?.FullName??throw new DirectoryNotFoundException();}
}
