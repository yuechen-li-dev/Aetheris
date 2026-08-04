using Aetheris.Kernel.Firmament;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class RoundedBoxFirmamentTests
{
    [Fact]
    public void DirectRoundedBoxSyntax_PreservesPrimitiveAndTopFilletAsSeparateStages()
    {
        var source = """
            Struct Enclosure {
                RoundedBox Body { Size: [120mm, 80mm, 18mm] CornerRadius: 12mm }
                Modify Body { EdgeFinish TopRound { Face: +Z Target: Boundary Kind: Fillet Radius: 2mm } }
            }
            """;
        var path = Path.Combine(Path.GetTempPath(), $"aetheris-rounded-box-{Guid.NewGuid():N}.firmament");
        try
        {
            File.WriteAllText(path, source);
            var result = FirmamentBuildAndExport.Run(path, Path.ChangeExtension(path, ".step"));
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value.Export.RoundedBox);
            Assert.Equal("RoundedBoxFeature", result.Value.Export.RoundedBox!.Primitive.FeatureAir);
            Assert.Equal("RoundedRectangleProfile -> LinearSweep", result.Value.Export.RoundedBox.Primitive.ConstructionAir);
            Assert.Equal(4, result.Value.Export.RoundedBox.Primitive.CylindricalCornerWallFaces);
            Assert.Equal(4, result.Value.Export.RoundedBox.EdgeFinish!.ToroidalCornerFaces);
            Assert.True(result.Value.Export.RoundedBox.Geometry.AnalyticVolume > 0d);
            Assert.Equal("valid", result.Value.Export.RoundedBox.Geometry.Preflight);
            Assert.True(result.Value.Export.RoundedBox.Step.ReimportSucceeded);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            var step = Path.ChangeExtension(path, ".step"); if (File.Exists(step)) File.Delete(step);
        }
    }
}
