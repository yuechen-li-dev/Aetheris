using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class FirmamentProjectSnapshotTests
{
    [Fact]
    public void PathsAreNormalizedAndCannotEscapeTheProject()
    {
        var project = new FirmamentProjectSnapshot("./main.firmasm",
            [KeyValuePair.Create("parts/./shade.firmament", "source")]);
        Assert.Equal("main.firmasm", project.RootDocument);
        Assert.True(project.TryResolve("parts/shade.firmament", out var source));
        Assert.Equal("source", source);
        Assert.Throws<ArgumentException>(() => new FirmamentProjectSnapshot("main.firmasm",
            [KeyValuePair.Create("parts/shade.firmament", "a"), KeyValuePair.Create("parts/./shade.firmament", "b")]));
        Assert.Throws<ArgumentException>(() => FirmamentProjectSnapshot.NormalizePath("../secret.firmament"));
        Assert.Throws<ArgumentException>(() => FirmamentProjectSnapshot.NormalizePath("C:/secret.firmament"));
    }

    [Fact]
    public void IncludeCycleIsDiagnosedWithoutFilesystemAccess()
    {
        var project = new FirmamentProjectSnapshot("main.firmasm", [
            KeyValuePair.Create("main.firmasm", "Include \"parts/a.firmament\";\nAssembly Root { <Assembly Root></Assembly> Anchor: Root; }"),
            KeyValuePair.Create("parts/a.firmament", "Include \"parts/b.firmament\";"),
            KeyValuePair.Create("parts/b.firmament", "Include \"parts/a.firmament\";")]);
        var result = new AssemblyM1Pipeline().CompileProject(project);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "assembly-include-cycle" &&
            diagnostic.Message.Contains("parts/a.firmament -> parts/b.firmament -> parts/a.firmament", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingIncludeNamesTheRequestedProjectPath()
    {
        var project = new FirmamentProjectSnapshot("main.firmasm", [
            KeyValuePair.Create("main.firmasm", "Include \"parts/missing.firmament\";\nAssembly Root { <Assembly Root></Assembly> Anchor: Root; }")]);
        var result = new AssemblyM1Pipeline().CompileProject(project);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "assembly-include-file-not-found" &&
            diagnostic.Message.Contains("parts/missing.firmament", StringComparison.Ordinal));
    }

    [Fact]
    public void ProjectSnapshotRejectsExternalStepBeforeItCanReadDisk()
    {
        var project = new FirmamentProjectSnapshot("main.firmasm", [
            KeyValuePair.Create("main.firmasm", "Assembly Root { <Assembly Root><Part External = ExternalStep<\"C:/secret.step\">></Part></Assembly> Anchor: Root; }")]);
        var result = new AssemblyM1Pipeline().CompileProject(project);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "assembly-profile-external-resource-unsupported");
    }

    [Fact]
    public void LampShadeBuildsFromSnapshotWithoutDiskRelativeToTheAssembly()
    {
        var lamp = File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/ThreeDm/lamp-visible-intent.firmament"));
        var shade = File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/ThreeDm/lamp-shade-loft.firmament"));
        var project = new FirmamentProjectSnapshot("main.firmasm", [
            KeyValuePair.Create("main.firmasm", lamp),
            KeyValuePair.Create("lamp-shade-loft.firmament", shade)]);
        var result = new AssemblyM1Pipeline().CompileProject(project);
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        Assert.Equal(7, result.Geometry!.InstanceBodies.Count);
    }
}
