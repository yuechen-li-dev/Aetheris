using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Step242;
using Xunit;

namespace Aetheris.SheetMetal.Tests;

/// <summary>
/// Every face of a formed sheet faces out of the material: skins away from the sheet, walls away from the region their
/// contour encloses (into a cut), whatever way round the authored contour runs. The importer derives orientation from
/// geometry alone, so its answer must equal the same_sense the lowering wrote.
/// </summary>
public sealed class SheetMetalFaceOrientationTests
{
    [Theory]
    [InlineData("l-bracket")]
    [InlineData("l-bracket-with-hole")]
    [InlineData("u-channel")]
    [InlineData("four-wall-tray-multiple-openings")]
    [InlineData("network-appliance-enclosure")]
    public void FormedFacesBindTheirOutwardNormal(string fixture)
    {
        var compiled = SheetMetalFirmament.CompileFile(Path.Combine(RepoRoot(), $"fixtures/Canonical/SheetMetal/{fixture}.firmament"));
        Assert.True(compiled.IsSuccess, string.Join("\n", compiled.Diagnostics.Select(d => d.Message)));
        var step = Step242Exporter.ExportBody(compiled.Part!.FormedBody!);
        Assert.True(step.IsSuccess);
        Assert.DoesNotContain(step.Diagnostics, d => d.Message.StartsWith("step-export-face-normal-inverted", StringComparison.Ordinal));
        var imported = Step242Importer.ImportBody(step.Value);
        Assert.True(imported.IsSuccess);
        var written = Regex.Matches(step.Value, @"ADVANCED_FACE\([^;]*,\.(T|F)\.\);").Select(m => m.Groups[1].Value == "T").ToArray();
        var faces = imported.Value.Topology.Faces.OrderBy(f => f.Id.Value).ToArray();
        Assert.Equal(written.Length, faces.Length);
        for (var i = 0; i < faces.Length; i++)
            Assert.True(written[i] == imported.Value.Bindings.GetFaceBinding(faces[i].Id).Orientation.IsAlignedWithSurface, $"face {i} was written facing into the material");
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory!.FullName;
    }
}
