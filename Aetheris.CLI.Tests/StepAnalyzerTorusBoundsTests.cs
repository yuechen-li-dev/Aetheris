using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

public sealed class StepAnalyzerTorusBoundsTests
{
    [Fact]
    public void Analyze_ReimportedFullTorus_ReportsAnalyticBoundsInsteadOfSeamVertexBounds()
    {
        var torus = BrepPrimitives.CreateTorus(10d, 3d);
        Assert.True(torus.IsSuccess);

        var export = Step242Exporter.ExportBody(torus.Value);
        Assert.True(export.IsSuccess);

        var path = Path.Combine(Path.GetTempPath(), $"aetheris-torus-bounds-{Guid.NewGuid():N}.step");
        try
        {
            File.WriteAllText(path, export.Value);

            var analysis = StepAnalyzer.Analyze(path);

            Assert.NotNull(analysis.Summary.BoundingBox);
            Assert.Equal(-13d, analysis.Summary.BoundingBox!.Value.Min.X, 9);
            Assert.Equal(-3d, analysis.Summary.BoundingBox.Value.Min.Y, 9);
            Assert.Equal(-13d, analysis.Summary.BoundingBox.Value.Min.Z, 9);
            Assert.Equal(13d, analysis.Summary.BoundingBox.Value.Max.X, 9);
            Assert.Equal(3d, analysis.Summary.BoundingBox.Value.Max.Y, 9);
            Assert.Equal(13d, analysis.Summary.BoundingBox.Value.Max.Z, 9);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
