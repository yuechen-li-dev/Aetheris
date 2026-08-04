namespace Aetheris.CLI.Tests;

public sealed class RoundedBoxCliPipelineTests
{
    [Fact]
    public void RoundedBoxCliBuild_ExportsAndReportsToroidalTopBoundaryFillet()
    {
        var root = Path.Combine(Path.GetTempPath(), $"aetheris-rounded-box-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var sourcePath = Path.Combine(root, "enclosure.firmament");
        var stepPath = Path.Combine(root, "enclosure.step");
        File.WriteAllText(sourcePath, """
            Struct Enclosure {
                RoundedBox Body { Size: [80mm, 50mm, 20mm] CornerRadius: 8mm }
                Modify Body { EdgeFinish TopRound { Face: +Z Target: Boundary Kind: Fillet Radius: 1mm } }
            }
            """);
        try
        {
            var stdout = new StringWriter(); var stderr = new StringWriter();
            var exit = CliRunner.Run(["build", sourcePath, "--out", stepPath, "--json"], stdout, stderr);

            Assert.Equal(0, exit);
            Assert.True(File.Exists(stepPath), stderr.ToString());
            Assert.Contains("\"toroidalCornerFaces\": 4", stdout.ToString(), StringComparison.Ordinal);
            Assert.Contains("TOROIDAL_SURFACE", File.ReadAllText(stepPath), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
