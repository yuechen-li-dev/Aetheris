namespace Aetheris.CLI.Tests;

public sealed class CadAssistantInspectionTests
{
    [Fact]
    public void ExplicitMissingExecutable_IsReportedAsUnavailableWithoutFallback()
    {
        var artifact = Path.Combine(Path.GetTempPath(), "aetheris-cad-inspection-" + Guid.NewGuid().ToString("N") + ".step");
        File.WriteAllText(artifact, "ISO-10303-21;");
        try
        {
            var result = Aetheris.CLI.CadAssistantInspection.Inspect(
                artifact,
                new Aetheris.CLI.CadAssistantInspectionOptions(artifact + ".missing.exe", TimeSpan.FromMilliseconds(10), Path.GetTempPath()));

            Assert.Equal(Aetheris.CLI.CadAssistantInspectionStatus.Unavailable, result.Status);
            Assert.Null(result.ResolvedExecutablePath);
            Assert.NotEmpty(result.ArtifactSha256);
        }
        finally { File.Delete(artifact); }
    }
}
