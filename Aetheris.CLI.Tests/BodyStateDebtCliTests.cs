using System.Text.Json;
using Aetheris.CLI;

namespace Aetheris.CLI.Tests;

public sealed class BodyStateDebtCliTests
{
    [Theory]
    [InlineData("box", true)]
    [InlineData("cylinder", true)]
    [InlineData("profile", true)]
    [InlineData("explicit-pmi", true)]
    [InlineData("counterbore-chamfer", true)]
    [InlineData("plain-offset", true)]
    [InlineData("second-hole-after-add", true)]
    [InlineData("unknown-field", false)]
    [InlineData("unsupported-cylinder-edit", false)]
    [InlineData("history-drop", false)]
    public void ValidateBuildAndInspectAgreeOnActualAdmission(string name, bool accepted)
    {
        var path = Path.Combine(Root(), "fixtures/Regression/BodyStateDebt", name + ".firmament");
        var directory = Path.Combine(Root(), "artifacts/local/tests/body-state-debt", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var output = Path.Combine(directory, "body.step");
        try
        {
            foreach (var command in new[] { "validate", "build", "inspect" })
            {
                var stdout = new StringWriter(); var stderr = new StringWriter();
                string[] args = command == "build" ? [command, path, "--out", output, "--json"] : [command, path, "--json"];
                Assert.True(CliRunner.Run(args, stdout, stderr) == (accepted ? 0 : 1), command + ": " + stdout + stderr);
                using var json = JsonDocument.Parse(stdout.ToString());
                var report = command == "validate" ? json.RootElement.GetProperty("firmamentV2Validation") : json.RootElement;
                if (!accepted)
                    Assert.All(report.GetProperty("diagnostics").EnumerateArray(), d =>
                    { Assert.False(string.IsNullOrWhiteSpace(d.GetProperty("message").GetString())); Assert.Equal(Path.GetFullPath(path), Path.GetFullPath(d.GetProperty("sourceFile").GetString()!)); });
            }
            Assert.Equal(accepted, File.Exists(output));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("Sculpting/sculpted-housing")]
    [InlineData("Structural/square-tube-member")]
    [InlineData("PlasticShell/plastic-shell-basic")]
    public void CommonScalarRequirementsAreAdmittedAndEnforced(string fixture)
    {
        var original = File.ReadAllText(Path.Combine(Root(), "fixtures/Canonical", fixture + ".firmament"));
        var requirements = File.ReadAllText(Path.Combine(Root(), "fixtures/Regression/BodyStateDebt/requirements.fragment.firmament"));
        var directory = Path.Combine(Root(), "artifacts/local/tests/domain-requirements", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "source.firmament");
        var output = Path.Combine(directory, "part.step");
        try
        {
            // Compare the same authoritative product before and after a true requirement.
            File.WriteAllText(path, original);
            Assert.Equal(0, CliRunner.Run(["build", path, "--out", output, "--json"], new StringWriter(), new StringWriter()));
            var expectedStep = File.ReadAllText(output);
            foreach (var accepted in new[] { true, false })
            {
                File.Delete(output);
                File.WriteAllText(path, (accepted ? requirements : requirements.Replace("Positive(1mm)", "Positive(-1mm)")) + original);
                foreach (var command in new[] { "validate", "build" })
                {
                    var stdout = new StringWriter();
                    string[] args = command == "build" ? [command, path, "--out", output, "--json"] : [command, path, "--json"];
                    Assert.Equal(accepted ? 0 : 1, CliRunner.Run(args, stdout, new StringWriter()));
                    if (!accepted) Assert.Contains("firmament-concept-requirement", stdout.ToString());
                }
                Assert.Equal(accepted, File.Exists(output));
                if (accepted) Assert.Equal(expectedStep, File.ReadAllText(output));
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("Sculpting/sculpted-housing")]
    [InlineData("PlasticShell/plastic-shell-basic")]
    public void StaticOnlyOptionCannotSilentlyRunTheGeometryCompiler(string fixture)
    {
        var path = Path.Combine(Root(), "fixtures/Canonical", fixture + ".firmament");
        var stdout = new StringWriter();
        Assert.Equal(1, CliRunner.Run(["validate", path, "--parse-only", "--json"], stdout, new StringWriter()));
        using var json = JsonDocument.Parse(stdout.ToString());
        var report = json.RootElement.GetProperty("firmamentV2Validation");
        Assert.Equal("not-validated", report.GetProperty("status").GetString());
        Assert.Equal("NotValidated", report.GetProperty("validationLevel").GetString());
        Assert.Equal("firmament-validation-option-unsupported", report.GetProperty("diagnostics")[0].GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("Structural/square-tube-member")]
    [InlineData("Garment/skirt")]
    public void ExistingStaticDomainAnalyzersRemainAvailable(string fixture)
    {
        var path = Path.Combine(Root(), "fixtures/Canonical", fixture + ".firmament");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        Assert.True(CliRunner.Run(["validate", path, "--parse-only", "--json"], stdout, stderr) == 0, stdout + stderr.ToString());
        using var json = JsonDocument.Parse(stdout.ToString());
        var report = json.RootElement.GetProperty("firmamentV2Validation");
        Assert.Equal("parsed-not-built", report.GetProperty("status").GetString());
        Assert.Equal("ParsedNotBuilt", report.GetProperty("validationLevel").GetString());
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory!.FullName;
    }
}
