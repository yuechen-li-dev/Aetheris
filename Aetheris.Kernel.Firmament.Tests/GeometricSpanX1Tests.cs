using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class GeometricSpanX1Tests
{
    [Fact]
    public void LineSpan_IsAParentPreservingView_AndPipelinesWithoutHelperGeometry()
    {
        var source = """
            Rect2 Stock { Center: [50mm, 20mm]; Size: [100mm, 40mm] }
            Point2 MountLeft { Position: [30mm, 0mm] } Point2 MountRight { Position: [70mm, 0mm] }
            Point2 UpperRight { Position: [70mm, 20mm] } Point2 UpperLeft { Position: [30mm, 20mm] }
            Line2 Rise { From: MountRight; To: UpperRight } Line2 Top { From: UpperRight; To: UpperLeft } Line2 Down { From: UpperLeft; To: MountLeft }
            Span<Line> MountingEdge { On: Stock.Bottom; From: MountLeft; To: MountRight }
            Profile Plate { Loop Outer { MountingEdge |> Rise |> Top |> Down |> Close } }
            Extrude Solid { Profile: Plate; From: 0mm; To: 2mm }
            """;

        var span = Assert.Single(ProfileAuthoringParser.InspectGeometricSpans(source).Spans);
        Assert.Equal(("MountingEdge", "Stock.Bottom", "Line", "Forward"), (span.SpanId, span.ParentId, span.ParentType, span.Orientation));
        Assert.Equal(40d, span.Length!.Value, 9);
        Assert.Contains("parent:Stock.Bottom", span.Provenance, StringComparison.Ordinal);

        var parsed = ProfileAuthoringParser.Parse(source);
        Assert.Empty(parsed.Diagnostics);
        Assert.Contains("MountingEdge", parsed.Profile!.Loops.Single().Segments.Select(x => x.Provenance.TracedFrom));
        Assert.True(ResolvedProfile2DValidator.Validate(parsed.Profile).IsValid);
    }

    [Fact]
    public void ArcSpan_HasNativeParameterDomain_AndExplicitReverseDoesNotCopyCarrier()
    {
        var source = """
            Point2 C { Position: [0mm, 0mm] } Point2 A { Position: [10mm, 0mm] } Point2 B { Position: [0mm, 10mm] }
            Circle2 Guide { Center: C; Radius: 10mm }
            Span<Arc> Quarter { On: Guide; From: A; To: B; Sweep: CounterClockwise; Orientation: Reverse }
            """;
        var span = Assert.Single(ProfileAuthoringParser.InspectGeometricSpans(source).Spans);
        Assert.Equal("Arc", span.ParentType); Assert.Equal("Reverse", span.Orientation);
        Assert.StartsWith("NativeRadians:", span.Domain, StringComparison.Ordinal);
        Assert.Equal(Math.PI * 5d, span.Length!.Value, 9);
        Assert.IsType<LineArcCircularArc2D>(span.Geometry);
    }

    [Theory]
    [InlineData("Span<Material> Bad { On: Stock.Bottom; From: A; To: B }", "firmament-span-type-invalid:Material")]
    [InlineData("Span<Line> Bad { On: Stock.Bottom; From: A; To: A }", "firmament-span-zero-length:Bad")]
    [InlineData("Span<Line> Bad { On: Stock.Bottom; From: A; To: C }", "profile-endpoint-not-on-guide:Bad:Stock.Bottom")]
    public void InvalidCurveSpans_AreTyped(string declaration, string expected)
    {
        var inspection = ProfileAuthoringParser.InspectGeometricSpans($$"""
            Point2 A { Position: [0mm, 0mm] } Point2 B { Position: [10mm, 0mm] } Point2 C { Position: [0mm, 2mm] }
            Line2 Base { From: A; To: B } Rect2 Stock { Center: [5mm, 2mm]; Size: [10mm, 4mm] }
            {{declaration}}
            """);
        Assert.Contains(expected, inspection.Diagnostics);
    }

    [Fact]
    public void PlaneSpan_PreservesPlaneAndBoundaryIdentity_WithoutCreatingAFace()
    {
        var source = """
            Construction Plane MountPlane { Trace: Plate.TopPlane }
            Profile MountBoundary { Loop Outer { } }
            Span<Plane> MountArea { On: MountPlane; Boundary: MountBoundary }
            """;
        var span = Assert.Single(ProfileAuthoringParser.InspectGeometricSpans(source).Spans);
        Assert.Equal(("Plane", "MountPlane", "MountBoundary"), (span.SpanType, span.ParentId, span.BoundaryProfile));
        Assert.Null(span.Geometry); Assert.Null(span.Length);
    }
}
