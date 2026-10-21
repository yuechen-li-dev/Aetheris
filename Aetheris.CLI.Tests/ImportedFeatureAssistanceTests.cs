using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class ImportedFeatureAssistanceTests
{
    [Fact]
    public void InspectSelectPlanValidateBuild_RequiresNoAuthoredFaceIdsOrBounds()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx"))) root = root.Parent;
        var input = Path.Combine(root!.FullName, "testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp");
        var directory = Path.Combine(root.FullName, "artifacts/local/imported-feature-assistance/tests", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(directory, "revision.firmament");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["imported-features", "inspect", input, "--import-tolerance", ".01", "--json"], stdout, stderr));
        using var inventory = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("InspectionOnly", inventory.RootElement.GetProperty("admission").GetString());
        var candidates = inventory.RootElement.GetProperty("inspection").GetProperty("discovery").GetProperty("candidates").EnumerateArray();
        // Simulate selecting the upper-left diameter-25 through-hole from measured
        // facts, without knowing a STEP entity or BRep ordinal in advance.
        var selected = Assert.Single(candidates, c => c.GetProperty("canPlan").GetBoolean()
            && System.Math.Abs(c.GetProperty("diameter").GetDouble() - 25) < 1e-6
            && c.GetProperty("center").GetProperty("x").GetDouble() < 0
            && c.GetProperty("center").GetProperty("y").GetDouble() > 0);
        var id = selected.GetProperty("id").GetString()!;
        stdout.GetStringBuilder().Clear();
        Assert.Equal(0, CliRunner.Run(["imported-features", "plan", input, "--candidate", id, "--as", "MountHole", "--offset", "290", "0",
            "--geometry-only", "--import-tolerance", ".01", "--source-out", source, "--json"], stdout, stderr));
        Assert.Empty(stderr.ToString()); Assert.True(File.Exists(source));
        Assert.True(File.Exists(Path.Combine(directory,"firmament.lock.toml")));
        Assert.DoesNotContain("textSha256:",File.ReadAllText(source));
        using var plan = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("Built", plan.RootElement.GetProperty("plan").GetProperty("admission").GetString());
        Assert.Equal(.1, plan.RootElement.GetProperty("plan").GetProperty("authoringIncrementMillimetres").GetDouble());
        Assert.Contains("diameter: 25mm", File.ReadAllText(source));
        Assert.Equal(115, plan.RootElement.GetProperty("plan").GetProperty("revision").GetProperty("preservedFaces").GetInt32());
        stdout.GetStringBuilder().Clear();
        Assert.Equal(0, CliRunner.Run(["validate", source, "--json"], stdout, stderr));
        using var validation = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("Built", validation.RootElement.GetProperty("firmamentV2Validation").GetProperty("validationLevel").GetString());
        stdout.GetStringBuilder().Clear();
        Assert.Equal(0, CliRunner.Run(["build", source, "--output", Path.Combine(directory,"revision.step"), "--json"], stdout, stderr));
        using var build = JsonDocument.Parse(stdout.ToString());
        Assert.Equal(1, build.RootElement.GetProperty("featureCount").GetInt32());
        Assert.Single(build.RootElement.GetProperty("pmiExportEvidence").GetProperty("diameter").EnumerateArray());
        var blocked = Path.Combine(directory, "occupied.firmament");
        stdout.GetStringBuilder().Clear();
        Assert.Equal(1, CliRunner.Run(["imported-features", "plan", input, "--candidate", id, "--as", "MountHole", "--offset", "320", "0",
            "--geometry-only", "--import-tolerance", ".01", "--source-out", blocked, "--json"], stdout, stderr));
        Assert.False(File.Exists(blocked));
        using var refusal = JsonDocument.Parse(stdout.ToString());
        Assert.Contains(refusal.RootElement.GetProperty("diagnostics").EnumerateArray(),
            d => d.GetProperty("code").GetString() == "firmament-imported-hole-revision-rejected");
        // A byte change is enough to invalidate a bound source even when its
        // nominal geometry is unchanged. Updating a hash cannot rebind faces.
        var localInput = Path.Combine(directory,"changed.step");
        File.WriteAllText(localInput,File.ReadAllText(input));
        var localSource = Path.Combine(directory,"local.firmament");
        var isolated = Path.Combine(directory,"surgery"); Directory.CreateDirectory(isolated);
        localSource = Path.Combine(isolated,"local.firmament");
        File.WriteAllText(localSource,File.ReadAllText(source).Replace(Path.GetRelativePath(directory,input).Replace('\\','/'),"../changed.step"));
        Assert.Equal(0,CliRunner.Run(["lock",localSource,"--json"],new StringWriter(),stderr));
        var lockPath = Path.Combine(isolated,"firmament.lock.toml"); var receipt = File.ReadAllText(lockPath);
        File.AppendAllText(localInput,"\n");
        stdout.GetStringBuilder().Clear();
        Assert.Equal(1,CliRunner.Run(["lock",localSource,"--update","--json"],stdout,stderr));
        Assert.Contains("firmament-lock-rebind-required",stdout.ToString());
        Assert.Equal(receipt,File.ReadAllText(lockPath));
    }
}
