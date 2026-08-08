using System.Diagnostics;
using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class CadmataViewTests
{
    [Fact]
    public void DirectStep_UsesPackagedDiscovery_AndHandsOffAbsolutePathWithSpaces()
    {
        using var fixture = ViewFixture.Create("direct space.step");
        var packaged = fixture.AddPackagedCadmata();
        var launcher = new RecordingLauncher();

        var exitCode = Run(["view", Path.GetRelativePath(Directory.GetCurrentDirectory(), fixture.StepPath)], fixture, launcher, out var stdout, out var stderr);

        Assert.Equal(0, exitCode);
        Assert.Empty(stderr);
        Assert.Equal(packaged, launcher.ExecutablePath);
        Assert.Equal(fixture.StepPath, launcher.StepPath);
        Assert.Contains("Opened direct space.step in Cadmata", stdout, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.ChangeExtension(fixture.StepPath, ".firmament")));
    }

    [Fact]
    public void Firmament_BuildsAdjacentStepBeforeLaunch()
    {
        using var fixture = ViewFixture.CreateFirmament("plate source.firmament");
        fixture.AddPackagedCadmata();
        var launcher = new RecordingLauncher();

        var exitCode = Run(["view", fixture.SourcePath!], fixture, launcher, out var stdout, out var stderr);

        Assert.Equal(0, exitCode);
        Assert.Empty(stderr);
        Assert.True(File.Exists(fixture.StepPath));
        Assert.Equal(fixture.StepPath, launcher.StepPath);
        Assert.Contains("Built plate source.firmament", stdout, StringComparison.Ordinal);
        Assert.Contains("STEP: " + fixture.StepPath, stdout, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("nested/part.stp")]
    [InlineData("nested/part.step")]
    public void StepExtensionsAndNestedPathsLaunchDirectly(string relativeName)
    {
        using var fixture = ViewFixture.Create(relativeName.Replace('/', Path.DirectorySeparatorChar));
        fixture.AddPackagedCadmata();
        var launcher = new RecordingLauncher();

        var exitCode = Run(["view", fixture.StepPath], fixture, launcher, out _, out var stderr);

        Assert.Equal(0, exitCode);
        Assert.Empty(stderr);
        Assert.Equal(fixture.StepPath, launcher.StepPath);
    }

    [Fact]
    public void ConfiguredOverrideWinsAndJsonReportsStableHandoffFields()
    {
        using var fixture = ViewFixture.Create("plate.step");
        var configured = Path.Combine(fixture.Root, "custom", "Cadmata custom.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(configured)!);
        File.WriteAllText(configured, "placeholder");
        var launcher = new RecordingLauncher();

        var exitCode = Run(["view", fixture.StepPath, "--cadmata-path", configured, "--json"], fixture, launcher, out var stdout, out var stderr);

        Assert.Equal(0, exitCode);
        Assert.Empty(stderr);
        Assert.Equal(configured, launcher.ExecutablePath);
        using var json = JsonDocument.Parse(stdout);
        Assert.True(json.RootElement.GetProperty("launched").GetBoolean());
        Assert.Equal(fixture.StepPath, json.RootElement.GetProperty("stepPath").GetString());
        Assert.Equal(configured, json.RootElement.GetProperty("cadmataPath").GetString());
        Assert.True(json.RootElement.GetProperty("processId").GetInt32() > 0);
    }

    [Fact]
    public void MissingCadmataAndLaunchFailureAreReadable()
    {
        using var fixture = ViewFixture.Create("plate.step");
        var missing = Path.Combine(fixture.Root, "missing.exe");

        var missingCode = Run(["view", fixture.StepPath, "--cadmata-path", missing], fixture, new RecordingLauncher(), out _, out var missingError);
        Assert.Equal(1, missingCode);
        Assert.Contains("Cadmata was not found", missingError, StringComparison.Ordinal);

        var configured = fixture.AddPackagedCadmata();
        var failingCode = Run(["view", fixture.StepPath, "--cadmata-path", configured], fixture, new RecordingLauncher(new InvalidOperationException("launch denied")), out _, out var launchError);
        Assert.Equal(1, failingCode);
        Assert.Contains("Could not open", launchError, StringComparison.Ordinal);
        Assert.Contains("launch denied", launchError, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFailurePreventsLaunch()
    {
        using var fixture = ViewFixture.CreateFirmament("bad.firmament", "Model Broken {");
        fixture.AddPackagedCadmata();
        var launcher = new RecordingLauncher();

        var exitCode = Run(["view", fixture.SourcePath!], fixture, launcher, out _, out var stderr);

        Assert.Equal(1, exitCode);
        Assert.Null(launcher.StepPath);
        Assert.Contains("build failed", stderr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DiscoveryOrderPrefersCanonicalEnvironmentThenCompatibilityThenPackage()
    {
        using var fixture = ViewFixture.Create("plate.step");
        var canonical = fixture.Touch("canonical.exe");
        var compatibility = fixture.Touch("compat.exe");
        var packaged = fixture.AddPackagedCadmata();
        var values = new Dictionary<string, string?>
        {
            ["AETHERIS_CADMATA_PATH"] = canonical,
            ["AETHERIS_CAD_ASSISTANT_PATH"] = compatibility,
            ["PATH"] = string.Empty,
        };

        var canonicalResult = Aetheris.CLI.CadmataDiscovery.Resolve(null, fixture.CliBase, key => values.GetValueOrDefault(key));
        Assert.Equal(canonical, canonicalResult!.Path);

        values["AETHERIS_CADMATA_PATH"] = null;
        var compatibilityResult = Aetheris.CLI.CadmataDiscovery.Resolve(null, fixture.CliBase, key => values.GetValueOrDefault(key));
        Assert.Equal(compatibility, compatibilityResult!.Path);

        values["AETHERIS_CAD_ASSISTANT_PATH"] = null;
        var packagedResult = Aetheris.CLI.CadmataDiscovery.Resolve(null, fixture.CliBase, key => values.GetValueOrDefault(key));
        Assert.Equal(packaged, packagedResult!.Path);
    }

    private static int Run(string[] args, ViewFixture fixture, Aetheris.CLI.ICadmataProcessLauncher launcher, out string stdout, out string stderr)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var result = Aetheris.CLI.CliRunner.Run(args, output, error, launcher, fixture.CliBase);
        stdout = output.ToString();
        stderr = error.ToString();
        return result;
    }

    private sealed class RecordingLauncher(Exception? failure = null) : Aetheris.CLI.ICadmataProcessLauncher
    {
        public string? ExecutablePath { get; private set; }
        public string? StepPath { get; private set; }

        public Process? Launch(string executablePath, string stepPath)
        {
            ExecutablePath = executablePath;
            StepPath = stepPath;
            if (failure is not null) throw failure;
            return Process.GetCurrentProcess();
        }
    }

    private sealed class ViewFixture : IDisposable
    {
        private ViewFixture(string root, string workingDirectory, string cliBase, string stepPath, string? sourcePath)
            => (Root, WorkingDirectory, CliBase, StepPath, SourcePath) = (root, workingDirectory, cliBase, stepPath, sourcePath);

        public string Root { get; }
        public string WorkingDirectory { get; }
        public string CliBase { get; }
        public string StepPath { get; }
        public string? SourcePath { get; }

        public static ViewFixture Create(string relativeStep)
        {
            var root = Path.Combine(Path.GetTempPath(), "Aetheris view tests " + Guid.NewGuid().ToString("N"));
            var working = Path.Combine(root, "user directory");
            var cliBase = Path.Combine(root, "bundle");
            var step = Path.GetFullPath(Path.Combine(working, relativeStep));
            Directory.CreateDirectory(Path.GetDirectoryName(step)!);
            Directory.CreateDirectory(cliBase);
            File.WriteAllText(step, "ISO-10303-21;");
            return new ViewFixture(root, working, cliBase, step, null);
        }

        public static ViewFixture CreateFirmament(string name, string? source = null)
        {
            var fixture = Create(Path.ChangeExtension(name, ".step"));
            var sourcePath = Path.Combine(fixture.WorkingDirectory, name);
            File.WriteAllText(sourcePath, source ?? """
                Model Plate {
                    Units: mm
                    Box Body { Size: [10mm, 20mm, 3mm] }
                }
                """);
            File.Delete(fixture.StepPath);
            return new ViewFixture(fixture.Root, fixture.WorkingDirectory, fixture.CliBase, fixture.StepPath, sourcePath);
        }

        public string AddPackagedCadmata()
        {
            var path = Path.Combine(CliBase, "cadmata", "Cadmata.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "placeholder");
            return path;
        }

        public string Touch(string name)
        {
            var path = Path.Combine(Root, name);
            File.WriteAllText(path, "placeholder");
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
