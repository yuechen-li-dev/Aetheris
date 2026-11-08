using System.Text.Json;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

/// <summary><c>EdgeFinish&lt;Fillet&gt; { where: A meets B }</c>: the edges where two Compose operations meet, rounded by
/// one ball, with exact cylinders along straight edges and quarter-ellipse mitres where they turn a corner.</summary>
public sealed class JunctionFilletCliTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void HexBossFootIsSixCylindersMitredByEllipses()
    {
        var body = BuildAndImport("fixtures/Regression/JunctionFillet/hex-boss-foot.firmament");
        var cylinders = body.Topology.Faces.Select(f => body.TryGetFaceSurface(f.Id, out var s) ? s?.Cylinder : null).OfType<Aetheris.Kernel.Core.Geometry.Surfaces.CylinderSurface>().ToArray();
        Assert.Equal(6, cylinders.Length);
        foreach (var cylinder in cylinders)
        {
            Assert.Equal(5d, cylinder.Radius, 9);
            // Each axis runs 5 mm above the plate and 5 mm out from a hex flat (50 mm from the centre).
            var axis = cylinder.Axis.ToVector();
            Assert.Equal(0d, axis.Z, 9);
            var origin = cylinder.Origin;
            Assert.Equal(5d, origin.Z, 9);
            var normal = new Aetheris.Kernel.Core.Math.Vector3D(axis.Y, -axis.X, 0);
            Assert.Equal(55d, double.Abs(normal.Dot(origin - Aetheris.Kernel.Core.Math.Point3D.Origin)), 9);
        }
        var ellipses = body.Topology.Edges.Count(e => body.Bindings.TryGetEdgeBinding(e.Id, out var b) && body.Geometry.TryGetCurve(b.CurveGeometryId, out var c) && c?.Kind == CurveGeometryKind.Ellipse3);
        Assert.Equal(6, ellipses);
        Assert.Equal(19, body.Topology.Faces.Count());
    }

    [Fact]
    public void FilletAddsTheMitredPrismVolume()
    {
        var filleted = Build("fixtures/Regression/JunctionFillet/hex-boss-foot.firmament");
        var plain = Build("fixtures/Regression/JunctionFillet/hex-boss-foot.firmament", s => s.Replace("EdgeFinish<Fillet> HexFoot { where: Hex meets Stock; radius: 5mm }", "", StringComparison.Ordinal));
        var result = StepShapeComparison.Compare(plain, filleted);
        // The concave fillet's cross-section, r²(1 - π/4), swept along its centroid's path, which the mitres keep
        // straight between the bisector planes.
        var r = 5d; var side = 100d / Math.Sqrt(3);
        var area = r * r * (1 - Math.PI / 4);
        var centroid = (r * r * r / 2 - Math.PI * r * r / 4 * (r - 4 * r / (3 * Math.PI))) / area;
        var expected = 6 * area * (side + 2 * centroid * Math.Tan(Math.PI / 6));
        Assert.Equal(expected, result.Overlap.CandidateVolume - result.Overlap.ReferenceVolume, expected * 0.02);
        Assert.Empty(result.Carriers.MissingFromCandidate);
        Assert.Equal(6, result.Carriers.ExtraInCandidate.Count);
    }

    [Theory]
    [InlineData("unknown-operand.firmament", "'Plinth' is not an operation of Compose Body; it has Stock, Hex")]
    [InlineData("unknown-role.firmament", "'role Pad' is not a role of Compose Body")]
    [InlineData("chamfer-kind.firmament", "'where' selects edges for EdgeFinish<Fillet> only")]
    [InlineData("where-not-junction.firmament", "write where: A meets B")]
    [InlineData("too-large.firmament", "the largest that fits is under 19.28")]
    public void UnsupportedJunctionsAreRefused(string fixture, string expected)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = CliRunner.Run(["validate", Path.Combine(RepoRoot, "fixtures/Invalid/JunctionFillet", fixture), "--json"], stdout, stderr);
        Assert.NotEqual(0, exit);
        using var document = JsonDocument.Parse(stdout.ToString());
        var report = document.RootElement.TryGetProperty("firmamentV2Validation", out var nested) ? nested : document.RootElement;
        var messages = report.GetProperty("diagnostics").EnumerateArray().Select(d => d.GetProperty("message").GetString()).ToArray();
        Assert.Contains(messages, m => m!.Contains(expected, StringComparison.Ordinal));
    }

    private static BrepBody BuildAndImport(string fixture)
    {
        var import = Step242Importer.ImportBody(File.ReadAllText(Build(fixture)));
        Assert.True(import.IsSuccess, string.Join("; ", import.Diagnostics.Select(d => d.Message)));
        return import.Value;
    }

    private static string Build(string fixture, Func<string, string>? edit = null)
    {
        var directory = Path.Combine(Path.GetTempPath(), "aetheris-junction-fillet", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var source = Path.Combine(RepoRoot, fixture);
        if (edit is not null)
        {
            var copy = Path.Combine(directory, Path.GetFileName(fixture));
            File.WriteAllText(copy, edit(File.ReadAllText(source)));
            source = copy;
        }
        var step = Path.Combine(directory, Path.GetFileNameWithoutExtension(fixture) + ".step");
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        Assert.True(CliRunner.Run(["build", source, "--out", step], stdout, stderr) == 0, $"{fixture}: {stderr}{stdout}");
        return step;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
