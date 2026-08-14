using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class PlanarContour2Tests
{
    [Fact]
    public void BoundedIntersectionAndSplit_RetainAnalyticKinds()
    {
        var horizontal = new LineArcLineSegment2D((0, 0), (10, 0));
        var vertical = new LineArcLineSegment2D((5, -5), (5, 5));
        var lineHit = Assert.Single(ProfileArrangementBuilder.IntersectBounded(horizontal, vertical).Intersections);
        Assert.Equal((5d, 0d), lineHit.Point);
        Assert.Equal(.5d, lineHit.FirstParameter, 12);

        var arc = new LineArcCircularArc2D((5, 0), 3, Math.PI, Math.PI);
        Assert.Equal(2, ProfileArrangementBuilder.IntersectBounded(horizontal, arc).Intersections.Count);
        var split = ProfileArrangementBuilder.SplitBounded(arc, .5d);
        Assert.All(split, curve => Assert.IsType<LineArcCircularArc2D>(curve));
        Assert.Equal(Math.PI / 2d, Assert.IsType<LineArcCircularArc2D>(split[0]).SweepAngleRadians, 12);
    }

    [Fact]
    public void ArcArcIntersection_IsBoundedByAuthoredSweeps()
    {
        var a = new LineArcCircularArc2D((0, 0), 5, 0, Math.PI);
        var b = new LineArcCircularArc2D((6, 0), 5, Math.PI, -Math.PI);
        var result = ProfileArrangementBuilder.IntersectBounded(a, b);
        Assert.Single(result.Intersections);
        Assert.False(result.IsCoincident);
    }

    [Fact]
    public void ConvexAndConcaveLineChains_OffsetWithExplicitSide()
    {
        var square = PlanarContourKernel.FromPolygon("Square", "XY", [(0, 0), (10, 0), (10, 10), (0, 10)], "test");
        var inset = PlanarContourKernel.Offset(square, 1, PlanarOffsetSide.Left);
        Assert.True(inset.Succeeded, string.Join('\n', inset.Diagnostics.Select(x => x.Message)));
        Assert.True(PlanarContourKernel.Validate(inset.Contour!).IsValid);

        var concave = PlanarContourKernel.FromPolygon("Concave", "XY", [(0, 0), (6, 0), (6, 2), (2, 2), (2, 6), (0, 6)], "test");
        var concaveInset = PlanarContourKernel.Offset(concave, .5, PlanarOffsetSide.Left);
        Assert.True(concaveInset.Succeeded, string.Join('\n', concaveInset.Diagnostics.Select(x => x.Message)));
    }

    [Fact]
    public void CollapsedArcOffset_IsTypedRejection()
    {
        var provenance = new ProfileSegmentProvenance("arc", "Circle", "test", "test", "XY");
        var contour = new PlanarContour2("Circle", "XY", new("Outer", true,
        [
            new("a", new LineArcCircularArc2D((0, 0), 1, 0, Math.PI), provenance),
            new("b", new LineArcCircularArc2D((0, 0), 1, Math.PI, Math.PI), provenance with { StableId = "arc-b" })
        ]), [], "test");
        var result = PlanarContourKernel.Offset(contour, 1, PlanarOffsetSide.Left);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, x => x.Code == "planar-offset-collapsed-arc");
    }

    [Fact]
    public void OuterAndInnerWinding_AreValidated()
    {
        var clockwise = PlanarContourKernel.FromPolygon("ClockwiseNormalized", "XY", [(0, 0), (0, 4), (4, 4), (4, 0)], "test");
        Assert.True(PlanarContourKernel.Validate(clockwise).IsValid); // factory normalizes outer authority
        var reversed = clockwise with { OuterLoop = clockwise.OuterLoop with { Segments = clockwise.OuterLoop.Segments.Reverse().Select(Reverse).ToArray() } };
        var validation = PlanarContourKernel.Validate(reversed);
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Diagnostics, x => x.Code == "planar-contour-winding");

        static PlanarContourSegment2 Reverse(PlanarContourSegment2 segment) => segment.Geometry is LineArcLineSegment2D line
            ? segment with { Geometry = new LineArcLineSegment2D(line.End, line.Start) }
            : throw new NotSupportedException();
    }
}
