namespace Aetheris.CLI.Tests;

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

    private static string FindRoot(){var directory=new DirectoryInfo(AppContext.BaseDirectory);while(directory is not null&&!File.Exists(Path.Combine(directory.FullName,"Aetheris.slnx")))directory=directory.Parent;return directory?.FullName??throw new DirectoryNotFoundException();}
}
