using System.Text;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242RuledSurfaceSkeletonTests
{
    [Fact]
    public void LinearExtrusionLineProbe_ImportsExportsAndReimportsAsLinearExtrusion()
    {
        var text = LoadProbe("surface-of-linear-extrusion-line.step");

        var import = Step242Importer.ImportBody(text);
        Assert.True(import.IsSuccess);

        var surface = Assert.Single(import.Value.Geometry.Surfaces).Value;
        Assert.Equal(SurfaceGeometryKind.LinearExtrusion, surface.Kind);
        Assert.True(surface.LinearExtrusion.HasValue);
        var linearExtrusion = surface.LinearExtrusion.Value;
        Assert.Equal(CurveGeometryKind.Line3, linearExtrusion.Directrix.Kind);

        var export = Step242Exporter.ExportBody(import.Value);
        Assert.True(export.IsSuccess);
        Assert.Contains("SURFACE_OF_LINEAR_EXTRUSION", export.Value, StringComparison.Ordinal);

        var roundTrip = Step242Importer.ImportBody(export.Value);
        Assert.True(roundTrip.IsSuccess);
        Assert.Equal(SurfaceGeometryKind.LinearExtrusion, Assert.Single(roundTrip.Value.Geometry.Surfaces).Value.Kind);
    }

    [Fact]
    public void SurfaceOfRevolutionLineProbe_ImportsExportsAndReimportsAsSurfaceOfRevolution()
    {
        var text = LoadProbe("surface-of-revolution-line.step");

        var import = Step242Importer.ImportBody(text);
        Assert.True(import.IsSuccess);

        var surface = Assert.Single(import.Value.Geometry.Surfaces).Value;
        Assert.Equal(SurfaceGeometryKind.SurfaceOfRevolution, surface.Kind);
        Assert.True(surface.SurfaceOfRevolution.HasValue);
        var revolution = surface.SurfaceOfRevolution.Value;
        Assert.Equal(CurveGeometryKind.Line3, revolution.Directrix.Kind);

        var export = Step242Exporter.ExportBody(import.Value);
        Assert.True(export.IsSuccess);
        Assert.Contains("SURFACE_OF_REVOLUTION", export.Value, StringComparison.Ordinal);

        var roundTrip = Step242Importer.ImportBody(export.Value);
        Assert.True(roundTrip.IsSuccess);
        Assert.Equal(SurfaceGeometryKind.SurfaceOfRevolution, Assert.Single(roundTrip.Value.Geometry.Surfaces).Value.Kind);
    }

    [Fact]
    public void Degree11BilinearProbe_IsClassifiedAsExactRuledAndRoundTripsAsBspline()
    {
        var text = LoadProbe("bspline-degree-1-1-bilinear.step");

        var import = Step242Importer.ImportBody(text);
        Assert.True(import.IsSuccess);

        var surface = Assert.Single(import.Value.Geometry.Surfaces).Value;
        Assert.Equal(SurfaceGeometryKind.BSplineSurfaceWithKnots, surface.Kind);

        var bspline = Assert.IsType<BSplineSurfaceWithKnots>(surface.BSplineSurfaceWithKnots);
        Assert.Equal(1, bspline.DegreeU);
        Assert.Equal(1, bspline.DegreeV);

        var classification = Step242BsplineRuledClassifier.Classify(bspline);
        Assert.True(classification.IsRuledCandidate);
        Assert.True(classification.IsBilinearPatch);
        Assert.Equal(Step242BsplineRuledDirection.Both, classification.RulingDirection);
        Assert.Equal(Step242BsplineRuledExactness.ExactRuled, classification.Exactness);
        Assert.DoesNotContain(import.Diagnostics, diagnostic => diagnostic.Message.Contains("approximation", StringComparison.OrdinalIgnoreCase));

        var export = Step242Exporter.ExportBody(import.Value);
        Assert.True(export.IsSuccess);
        Assert.Contains("B_SPLINE_SURFACE_WITH_KNOTS", export.Value, StringComparison.Ordinal);

        var roundTrip = Step242Importer.ImportBody(export.Value);
        Assert.True(roundTrip.IsSuccess);

        var roundTripSurface = Assert.Single(roundTrip.Value.Geometry.Surfaces).Value;
        var roundTripSpline = Assert.IsType<BSplineSurfaceWithKnots>(roundTripSurface.BSplineSurfaceWithKnots);
        Assert.Equal(1, roundTripSpline.DegreeU);
        Assert.Equal(1, roundTripSpline.DegreeV);
    }

    private static string LoadProbe(string fileName)
    {
        var path = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), "testdata", "step242", "probes", fileName);
        return File.ReadAllText(path, Encoding.UTF8);
    }
}
