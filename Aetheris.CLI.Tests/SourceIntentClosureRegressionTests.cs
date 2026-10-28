using Aetheris.CLI;

namespace Aetheris.CLI.Tests;

/// <summary>
/// Regressions from the 2.0.0 release fuzz pass: each case was silently accepted (validate exit 0)
/// before the fix. Source that the binder would ignore or that is outside the finite range must be
/// rejected with a diagnostic naming the problem.
/// </summary>
public sealed class SourceIntentClosureRegressionTests
{
    [Theory]
    // Non-finite numeric literals and the NaN/Infinity identifiers.
    [InlineData("Pipeline/line-arc-profile.firmament", "Line Bottom { length: 30mm }", "Line Bottom { length: 1e309mm }", "firmament-number-not-finite")]
    [InlineData("Pipeline/line-arc-profile.firmament", "Arc Right { Radius: 8mm;", "Arc Right { Radius: NaN;", "firmament-number-not-finite")]
    // Concept Path segment bodies (also used by circular sweep paths).
    [InlineData("Pipeline/line-arc-profile.firmament", "Arc Right { Radius: 8mm;", "Arc Right { Radiuz: 8mm;", "concept-path-field-unknown")]
    [InlineData("Features/Sweep/circular-line-sweep.firmament", "Line Stock { length: 40mm }", "Line Stock { length: 40mm; zzUnknownField: 3mm }", "concept-path-field-unknown")]
    // Pmi block fields.
    [InlineData("Templates/nested-template-pmi.firmament", "        Pmi {", "        Pmi { zzUnknownField: 3mm;", "firmament-v2-pmi-unknown-field")]
    // Profile body fields (From remains the derived-boundary form).
    [InlineData("Profiles/rect2-profile-extrusion.firmament", "Profile MainPlate using Rect2Layout {", "Profile MainPlate using Rect2Layout { zzUnknownField: 3mm;", "Profile:zzUnknownField")]
    // Modify targeting a composed body.
    [InlineData("Learn/03-machined-block.firmament", "Modify body {", "Modify body { zzUnknownField: 3mm;", "Modify:zzUnknownField")]
    // Hole<Shaft> inside a materialized Template struct.
    [InlineData("Templates/cnc-dfm-policy.firmament", "Hole<Shaft> Probe {", "Hole<Shaft> Probe { zzUnknownField: 3mm;", "Hole:zzUnknownField")]
    public void MutatedCanonicalSource_IsRejected(string fixture, string anchor, string replacement, string expectedCode)
    {
        var original = File.ReadAllText(Path.Combine(RepoRoot(), "fixtures", "Canonical", fixture));
        Assert.Contains(anchor, original, StringComparison.Ordinal);
        Assert.Equal(0, Validate(original, fixture).ExitCode);

        var index = original.IndexOf(anchor, StringComparison.Ordinal);
        var mutated = original[..index] + replacement + original[(index + anchor.Length)..];
        var (exitCode, output) = Validate(mutated, fixture);

        Assert.NotEqual(0, exitCode);
        Assert.Contains(expectedCode, output, StringComparison.Ordinal);
    }

    private static (int ExitCode, string Output) Validate(string source, string fixture)
    {
        // These specimens are self-contained. Keep generated mutations out of the
        // canonical tree, which other parallel tests enumerate as durable source.
        var directory = Path.Combine(RepoRoot(), "artifacts", "local", "source-intent-closure");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(fixture)}-{Guid.NewGuid():N}.firmament");
        File.WriteAllText(path, source);
        try
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var exitCode = CliRunner.Run(["validate", path], stdout, stderr);
            return (exitCode, stdout.ToString() + stderr);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
