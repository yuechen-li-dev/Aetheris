using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Visualization;
using Aetheris.Surfacing;
using Xunit;

namespace Aetheris.Modules.Tests;

public sealed class BSplineControlNetFittingTests
{
    [Fact]
    public void RecoverPolynomialSupportFromIndependentSamplesDeterministically()
    {
        var expected = Surface(0);
        var seed = BSplineControlNetFitting.Copy(expected, expected.ControlPoints.Select(row =>
            (IReadOnlyList<Point3D>)row.Select(point => new Point3D(point.X + 5, point.Y - 8, point.Z + 11)).ToArray()).ToArray());
        var patch = new SplineFitPatch("patch", seed, Enumerable.Range(0, 16).ToArray());
        var samples = Samples(expected, 0);
        var options = new SplineFitOptions(SeedWeight: 1e-10, FairnessWeight: 0);
        var result = BSplineControlNetFitting.Fit([patch], samples, options);
        var repeated = BSplineControlNetFitting.Fit([patch], samples, options);
        Assert.InRange(result.MaximumError, 0, .00001);
        Assert.Equal(result.Patches[0].Surface.ControlPoints.SelectMany(row => row),
            repeated.Patches[0].Surface.ControlPoints.SelectMany(row => row));
        // Independent samples do not coincide with the fitting grid.
        Assert.InRange((result.Patches[0].Surface.Evaluate(2.73, 5.19) - expected.Evaluate(2.73, 5.19)).Length, 0, .00001);
    }

    [Fact]
    public void SharedControlsKeepTheWholeBoundaryExact()
    {
        var first = Surface(0);
        var second = Surface(60);
        var firstIds = Enumerable.Range(0, 16).ToArray();
        var secondIds = Enumerable.Range(12, 16).ToArray();
        var result = BSplineControlNetFitting.Fit(
            [new("first", first, firstIds), new("second", second, secondIds)],
            Samples(first, 0).Concat(Samples(second, 1)).ToArray());
        Assert.Equal(28, result.SharedControlCount);
        for (int index = 0; index <= 100; index++)
        {
            double v = 4 + 2 * index / 100d;
            Assert.Equal(result.Patches[0].Surface.Evaluate(3, v), result.Patches[1].Surface.Evaluate(2, v));
        }
    }

    [Fact]
    public void OpenSheetUsesExactBoundaryCurvesAndPcurvesOnNonUnitDomains()
    {
        var sheet = SurfacePatchBrepMaterializer.MaterializeSheet(Surface(0));
        Assert.Single(sheet.Topology.Faces);
        Assert.Equal(4, sheet.Topology.Edges.Count());
        Assert.True(BrepBindingValidator.Validate(sheet, true).IsSuccess);
        var evidence = BrepPcurveValidator.Validate(sheet, requireEveryCoedge: true);
        Assert.True(evidence.IsValid, string.Join("\n", evidence.Diagnostics));
        Assert.InRange(evidence.MaximumReconstructionDeviation, 0, 1e-8);
        var wire = BrepWireframeSvgRenderer.Render(sheet);
        Assert.Equal(1, wire.Evidence.FacesWithTrimmedIsolines);
        Assert.True(wire.Evidence.IsoPolylineCount > 0);
    }

    [Fact]
    public void ContradictorySharedSeedsAndOutOfDomainSamplesFailByName()
    {
        var first = new SplineFitPatch("first", Surface(0), Enumerable.Range(0, 16).ToArray());
        var second = new SplineFitPatch("second", Surface(60), Enumerable.Range(0, 16).ToArray());
        Assert.StartsWith("spline-fit-shared-control:", Assert.Throws<ArgumentException>(() =>
            BSplineControlNetFitting.Fit([first, second], Samples(first.Surface, 0))).Message);
        Assert.StartsWith("spline-fit-domain:", Assert.Throws<ArgumentException>(() =>
            BSplineControlNetFitting.Fit([first], [new(0, 1.99, 4, new(0, 0, 0))])).Message);
    }

    [Fact]
    public void RationalSupportsFailInsteadOfUsingPolynomialBasisWeights()
    {
        var polynomial = Surface(0);
        var weights = Enumerable.Range(0, 4).Select(row => (IReadOnlyList<double>)
            Enumerable.Range(0, 4).Select(column => 1 + .1 * row * column).ToArray()).ToArray();
        var rational = new BSplineSurfaceWithKnots(3, 3, polynomial.ControlPoints, "UNSPECIFIED", false, false, false,
            [4, 4], [4, 4], [2, 3], [4, 6], "UNSPECIFIED", weights);
        Assert.StartsWith("spline-fit-patch:", Assert.Throws<ArgumentException>(() => BSplineControlNetFitting.Fit(
            [new("rational", rational, Enumerable.Range(0, 16).ToArray())], Samples(rational, 0))).Message);
        Assert.StartsWith("spline-sheet-support:", Assert.Throws<ArgumentException>(() =>
            SurfacePatchBrepMaterializer.MaterializeSheet(rational)).Message);
    }

    private static BSplineSurfaceWithKnots Surface(double offset)
    {
        var controls = Enumerable.Range(0, 4).Select(row => (IReadOnlyList<Point3D>)
            Enumerable.Range(0, 4).Select(column => new Point3D(offset + row * 20, column * 15,
                column * column * 2)).ToArray()).ToArray();
        return new(3, 3, controls, "UNSPECIFIED", false, false, false, [4, 4], [4, 4], [2, 3], [4, 6], "UNSPECIFIED");
    }

    private static SplineFitSample[] Samples(BSplineSurfaceWithKnots surface, int patch)
    {
        return Enumerable.Range(0, 9).SelectMany(row => Enumerable.Range(0, 9).Select(column =>
        {
            double u = 2 + row / 8d;
            double v = 4 + column / 4d;
            return new SplineFitSample(patch, u, v, surface.Evaluate(u, v));
        })).ToArray();
    }
}
