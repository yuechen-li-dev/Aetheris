using Aetheris.Kernel.Firmament;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2StandaloneCubicLatticeTests
{
    [Fact]
    public void Parser_AdmitsStandaloneMaterialBoundsCubicTrussWithoutHost()
    {
        var parsed = FirmamentV2Parser.Parse(Source());

        Assert.True(parsed.IsSuccess, string.Join(", ", parsed.Diagnostics));
        var fill = Assert.Single(parsed.Document!.StandaloneLatticeFills!);
        Assert.Equal("Domain", fill.Name);
        Assert.Equal("CubicTruss", fill.Pattern);
        Assert.Equal([3, 3, 3], [fill.CellsX, fill.CellsY, fill.CellsZ]);
        Assert.Equal(26.4d, fill.Region.Size[0], 8);
        Assert.Empty(parsed.Document.Solids);
    }

    [Fact]
    public void Build_EmitsOneVerifiedAnalyticCubicLatticeBody()
    {
        var sourcePath = Path.Combine(Path.GetTempPath(), $"aetheris-m9r-{Guid.NewGuid():N}.firmament");
        var stepPath = Path.ChangeExtension(sourcePath, ".step");
        try
        {
            File.WriteAllText(sourcePath, Source());
            var build = FirmamentBuildAndExport.Run(sourcePath, stepPath);

            Assert.True(build.IsSuccess, string.Join(", ", build.Diagnostics.Select(diagnostic => diagnostic.Message)));
            var lattice = Assert.IsType<FirmamentStandaloneLatticeReport>(build.Value.Export.Lattice);
            Assert.Equal(64, lattice.Nodes);
            Assert.Equal(144, lattice.Members);
            Assert.Equal(288, lattice.Seams);
            Assert.Equal(208, lattice.Faces);
            Assert.Equal(64, lattice.SphericalFaces);
            Assert.Equal(144, lattice.CylindricalFaces);
            Assert.True(lattice.AuthoritativePlan);
            Assert.Equal(lattice.AnalyticVolume, lattice.BrepVolume, 8);
            Assert.Contains("SPHERICAL_SURFACE", build.Value.Export.StepText, StringComparison.Ordinal);
            Assert.Contains("CYLINDRICAL_SURFACE", build.Value.Export.StepText, StringComparison.Ordinal);
            Assert.Contains("TRIMMED_CURVE", build.Value.Export.StepText, StringComparison.Ordinal);
            Assert.True(Step242Importer.ImportBody(build.Value.Export.StepText).IsSuccess);
        }
        finally
        {
            if (File.Exists(sourcePath)) File.Delete(sourcePath);
            if (File.Exists(stepPath)) File.Delete(stepPath);
        }
    }

    [Theory]
    [InlineData("nodeRadius: 1.1mm", "node-radius-too-small-for-struts")]
    [InlineData("strutRadius: 0.4mm", "minimum-strut-diameter-violation")]
    public void Build_RejectsInvalidStandaloneDfmBeforeEmission(string replacement, string expectedDiagnostic)
    {
        var sourcePath = Path.Combine(Path.GetTempPath(), $"aetheris-m9r-invalid-{Guid.NewGuid():N}.firmament");
        var stepPath = Path.ChangeExtension(sourcePath, ".step");
        try
        {
            var source = replacement.StartsWith("nodeRadius", StringComparison.Ordinal)
                ? Source().Replace("nodeRadius: 1.2mm", replacement, StringComparison.Ordinal)
                : Source().Replace("strutRadius: 0.8mm", replacement, StringComparison.Ordinal);
            File.WriteAllText(sourcePath, source);
            var build = FirmamentBuildAndExport.Run(sourcePath, stepPath);

            Assert.False(build.IsSuccess);
            Assert.Contains(build.Diagnostics, diagnostic => diagnostic.Message.Contains(expectedDiagnostic, StringComparison.Ordinal));
            Assert.False(File.Exists(stepPath));
        }
        finally
        {
            if (File.Exists(sourcePath)) File.Delete(sourcePath);
            if (File.Exists(stepPath)) File.Delete(stepPath);
        }
    }

    private static string Source() => """
        model CubicLatticeSample {
            units mm
            template<Additive> PolymerLattice {
                concept MinimumStrutDiameter: 1.0mm
                concept MinimumNodeDiameter: 2.0mm
                concept MinimumFeatureSpacing: 0.5mm
            }
            region Domain { box { size: [26.4mm, 26.4mm, 26.4mm] } }
            fill Domain {
                pattern: CubicTruss {
                    cells: [3, 3, 3]
                    cellSize: 8mm
                    strutRadius: 0.8mm
                    nodeRadius: 1.2mm
                }
                placement: MaterialBounds
            }
        }
        """;
}
