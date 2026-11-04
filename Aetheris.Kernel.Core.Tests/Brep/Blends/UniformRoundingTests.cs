using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Blends;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Tests.Step242;

namespace Aetheris.Kernel.Core.Tests.Brep.Blends;

/// <summary>"Fillet everything" as an opening: erode by the radius, sweep the ball back. Checked against the
/// dedicated regular-stock builder, and read back by the rolling-ball certificate.</summary>
public sealed class UniformRoundingTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(20)]
    public void RegularSolids_MatchTheDedicatedBuilder(int faces)
    {
        const double radius = 2;
        var rounded = UniformRounding.RoundAllEdges(ConvexStockPrimitives.CreateRegular(faces, 30).Value, radius);
        Assert.True(rounded.IsSuccess, string.Join("; ", rounded.Diagnostics.Select(d => d.Message)));
        var expected = ConvexStockPrimitives.CreateRegular(faces, 30, radius).Value;
        AssertSameSolid(expected, rounded.Value);
    }

    [Fact]
    public void Box_MatchesTheRoundedBox()
    {
        var rounded = UniformRounding.RoundAllEdges(ConvexStockPrimitives.CreateHull(BoxCorners(40, 30, 20)).Value, 3);
        Assert.True(rounded.IsSuccess, string.Join("; ", rounded.Diagnostics.Select(d => d.Message)));
        AssertSameSolid(ConvexStockPrimitives.CreateRoundedBox(40, 30, 20, 3).Value, rounded.Value);
    }

    /// <summary>A square pyramid's apex has four faces: the eroded planes meet there only by the pyramid's symmetry
    /// in general, so the erosion is the half-spaces' intersection, not three planes per vertex. The rounded result
    /// reads back, by the certificate alone, as one strip per edge at the radius and one corner per vertex.</summary>
    [Fact]
    public void SquarePyramid_EveryEdgeAStripEveryVertexACorner()
    {
        var pyramid = ConvexStockPrimitives.CreateHull([new(-10, -10, 0), new(10, -10, 0), new(10, 10, 0), new(-10, 10, 0), new(0, 0, 12)]).Value;
        Assert.Equal(5, pyramid.Topology.Faces.Count());
        var rounded = UniformRounding.RoundAllEdges(pyramid, 1.5);
        Assert.True(rounded.IsSuccess, string.Join("; ", rounded.Diagnostics.Select(d => d.Message)));
        AssertReadsBack(rounded.Value, faces: 5, edges: 8, vertices: 5, radius: 1.5);
        Assert.True(BrepMassProperties.Evaluate(rounded.Value).AbsoluteVolume < BrepMassProperties.Evaluate(pyramid).AbsoluteVolume);
    }

    /// <summary>A prism cut by a skewed plane: no symmetry, unequal dihedral angles, three faces at every vertex, so
    /// the erosion keeps the body's structure and the rounding has one strip per edge and one corner per vertex.</summary>
    [Fact]
    public void SkewCutPrism_EveryEdgeAStripEveryVertexACorner()
    {
        Point3D[] bottom = [new(0, 0, 0), new(24, 0, 0), new(28, 14, 0), new(6, 17, 0)];
        double Top(Point3D p) => 10 + 0.25 * p.X + 0.15 * p.Y;
        var hull = ConvexStockPrimitives.CreateHull([.. bottom, .. bottom.Select(p => new Point3D(p.X, p.Y, Top(p)))]).Value;
        var plain = hull.Topology;
        Assert.All(plain.Vertices, v => Assert.Equal(3, plain.Edges.Count(e => e.StartVertexId == v.Id || e.EndVertexId == v.Id)));
        var rounded = UniformRounding.RoundAllEdges(hull, 1);
        Assert.True(rounded.IsSuccess, string.Join("; ", rounded.Diagnostics.Select(d => d.Message)));
        AssertReadsBack(rounded.Value, plain.Faces.Count(), plain.Edges.Count(), plain.Vertices.Count(), 1);
    }

    /// <summary>Where four or more faces meet without symmetry, the faces moved in by the radius do not meet at one
    /// point: the vertex opens into short edges, each its own strip. That is what a ball rolled over the body does.</summary>
    [Fact]
    public void IrregularHighValenceVertices_OpenIntoShortStrips()
    {
        var hull = ConvexStockPrimitives.CreateHull([new(0, 0, 0), new(30, 0, 0), new(26, 18, 0), new(3, 15, 0), new(4, 3, 9), new(22, 2, 11), new(19, 13, 8)]).Value;
        Assert.Contains(hull.Topology.Vertices, v => hull.Topology.Edges.Count(e => e.StartVertexId == v.Id || e.EndVertexId == v.Id) > 3);
        var rounded = UniformRounding.RoundAllEdges(hull, 1);
        Assert.True(rounded.IsSuccess, string.Join("; ", rounded.Diagnostics.Select(d => d.Message)));
        var body = rounded.Value;
        int Count(Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind kind) => body.Topology.Faces.Count(f => body.TryGetFaceSurfaceGeometry(f.Id, out var g) && g!.Kind == kind);
        Assert.True(Count(Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cylinder) > hull.Topology.Edges.Count());
        AssertReadsBack(body, hull.Topology.Faces.Count(), Count(Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cylinder), Count(Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Sphere), 1);
    }

    [Fact]
    public void RadiusTooLarge_RefusesNamingTheLargestThatKeepsEveryFace()
    {
        var pyramid = ConvexStockPrimitives.CreateHull([new(-10, -10, 0), new(10, -10, 0), new(10, 10, 0), new(-10, 10, 0), new(0, 0, 12)]).Value;
        var refused = UniformRounding.RoundAllEdges(pyramid, 50);
        Assert.False(refused.IsSuccess);
        var message = refused.Diagnostics.Single().Message;
        var fit = double.Parse(System.Text.RegularExpressions.Regex.Match(message, @"(?:keeps every face is|fits is under) ([0-9.Ee+-]+) mm").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(fit > 0 && fit < 12, message);
        Assert.True(UniformRounding.RoundAllEdges(pyramid, fit * 0.99).IsSuccess, $"{fit * 0.99} should fit: {message}");
        Assert.False(UniformRounding.RoundAllEdges(pyramid, fit * 1.01).IsSuccess);
    }

    [Fact]
    public void SplineAndEllipticalEdges_RefuseByName()
    {
        // Fusion's SO-101 export has both.
        var refused = UniformRounding.RoundAllEdges(Step242Corpus.Body("testdata/step242/community/so101/Under_arm_SO101.step"), 1);
        Assert.False(refused.IsSuccess);
        var message = refused.Diagnostics.Single().Message;
        Assert.StartsWith("uniform-rounding-spline-edge-unsupported", message);
        Assert.Contains("does not support spline or elliptical edges", message);
        Assert.Contains("spline", message);
        Assert.Contains("elliptical", message);
    }

    /// <summary>The local rolling ball on the body's own faces against the opening, where both apply: convex
    /// bodies bounded by planes with three faces at every corner. Same faces, edges, corners and points.</summary>
    [Theory]
    [InlineData("box")]
    [InlineData("tetrahedron")]
    [InlineData("dodecahedron")]
    [InlineData("skew-prism")]
    public void LocalRounding_MatchesTheOpening(string name)
    {
        Point3D[] bottom = [new(0, 0, 0), new(24, 0, 0), new(28, 14, 0), new(6, 17, 0)];
        var body = name switch
        {
            "box" => ConvexStockPrimitives.CreateHull(BoxCorners(40, 30, 20)).Value,
            "tetrahedron" => ConvexStockPrimitives.CreateRegular(4, 30).Value,
            "dodecahedron" => ConvexStockPrimitives.CreateRegular(12, 20).Value,
            _ => ConvexStockPrimitives.CreateHull([.. bottom, .. bottom.Select(p => new Point3D(p.X, p.Y, 10 + 0.25 * p.X + 0.15 * p.Y))]).Value,
        };
        var opening = UniformRounding.RoundAllEdges(body, 1.5);
        var local = LocalRounding.Round(body, 1.5);
        Assert.True(local.Body is not null, local.Refusal?.Message);
        AssertSameSolid(opening.Value, local.Body!, volumeDigits: null);
        var (v0, v1) = (BrepMassProperties.Evaluate(opening.Value).AbsoluteVolume, BrepMassProperties.Evaluate(local.Body!).AbsoluteVolume);
        Assert.True(double.Abs(v1 - v0) <= 1e-4 * v0, $"volume {v1} vs {v0}");
    }

    /// <summary>A cylinder's two rims are circles: each rounds to a torus around the axis (major R - r), the caps
    /// and the side shrink onto the tori's contact circles, and the side's seam runs between them.</summary>
    [Fact]
    public void Cylinder_EachRimATorus()
    {
        var rounded = UniformRounding.RoundAllEdges(BrepPrimitives.CreateCylinder(10, 20).Value, 2);
        Assert.True(rounded.IsSuccess, string.Join("; ", rounded.Diagnostics.Select(d => d.Message)));
        var body = rounded.Value;
        Assert.True(BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true).IsSuccess);
        var tori = body.Topology.Faces.Select(f => body.TryGetFaceSurfaceGeometry(f.Id, out var g) ? g!.Torus : null).Where(t => t is not null).Select(t => t!.Value).ToArray();
        Assert.Equal(2, tori.Length);
        Assert.All(tori, t => { Assert.Equal(8, t.MajorRadius, 9); Assert.Equal(2, t.MinorRadius, 9); Assert.Equal(8, double.Abs(t.Center.Z), 9); });
        Assert.Equal((5, 7), (body.Topology.Faces.Count(), body.Topology.Edges.Count()));
        // The caps keep a disc of radius R - r; the side runs between z = ±(h/2 - r).
        Assert.All(body.Topology.Vertices, v => { body.TryGetVertexPoint(v.Id, out var p); Assert.True(double.Abs(double.Hypot(p.X, p.Y) - 8) < 1e-9 && double.Abs(double.Abs(p.Z) - 10) < 1e-9 || double.Abs(double.Hypot(p.X, p.Y) - 10) < 1e-9 && double.Abs(double.Abs(p.Z) - 8) < 1e-9); });
        AssertRoundTrips(body);
        var strips = BlendRecognition.Recognize(body).Where(r => r.Role == BlendFaceRole.Strip).ToArray();
        Assert.Equal(2, strips.Length);
        Assert.All(strips, s => Assert.Equal(2, s.Blend!.Size, 9));
    }

    [Fact]
    public void Cylinder_RadiusAsLargeAsTheCylinder_Refuses()
    {
        var refused = UniformRounding.RoundAllEdges(BrepPrimitives.CreateCylinder(10, 40).Value, 12);
        var message = refused.Diagnostics.Single().Message;
        Assert.StartsWith("uniform-rounding-radius-too-large", message);
        var fit = double.Parse(System.Text.RegularExpressions.Regex.Match(message, @"fits is under ([0-9.Ee+-]+) mm").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(fit, 4.9, 10);
        Assert.True(UniformRounding.RoundAllEdges(BrepPrimitives.CreateCylinder(10, 40).Value, fit * 0.99).IsSuccess);
    }

    private static Point3D[] BoxCorners(double x, double y, double z) =>
        [.. from a in new[] { -1, 1 } from b in new[] { -1, 1 } from c in new[] { -1, 1 } select new Point3D(a * x / 2, b * y / 2, c * z / 2)];

    private static void AssertRoundTrips(BrepBody body)
    {
        var step = Aetheris.Kernel.Core.Step242.Step242Exporter.ExportBody(body);
        Assert.True(step.IsSuccess, string.Join("; ", step.Diagnostics.Select(d => d.Message)));
        var back = Aetheris.Kernel.Core.Step242.Step242Importer.ImportBody(step.Value);
        Assert.True(back.IsSuccess, string.Join("; ", back.Diagnostics.Select(d => d.Message)));
        Assert.Equal(body.Topology.Faces.Count(), back.Value.Topology.Faces.Count());
        Assert.Equal(Points(body), Points(back.Value));
    }

    private static void AssertReadsBack(BrepBody body, int faces, int edges, int vertices, double radius)
    {
        Assert.True(BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true).IsSuccess);
        var read = BlendRecognition.Recognize(body);
        var strips = read.Where(r => r.Role == BlendFaceRole.Strip).ToArray();
        Assert.Equal(edges, strips.Length);
        Assert.All(strips, s => { Assert.Equal(radius, s.Blend!.Size, 9); Assert.True(s.Blend.Hypothesis.Convex); });
        Assert.Equal(vertices, read.Count(r => r.Role == BlendFaceRole.Joiner));
        Assert.Equal(faces, read.Count(r => r.Role == BlendFaceRole.Primary));
    }

    private static void AssertSameSolid(BrepBody expected, BrepBody actual, int? volumeDigits = 6)
    {
        Assert.True(BrepBindingValidator.Validate(actual, requireAllEdgeAndFaceBindings: true).IsSuccess);
        Assert.Equal(expected.Topology.Faces.Count(), actual.Topology.Faces.Count());
        Assert.Equal(expected.Topology.Edges.Count(), actual.Topology.Edges.Count());
        Assert.Equal(expected.Topology.Vertices.Count(), actual.Topology.Vertices.Count());
        Assert.Equal(Points(expected), Points(actual));
        if (volumeDigits is { } digits) Assert.Equal(BrepMassProperties.Evaluate(expected).AbsoluteVolume, BrepMassProperties.Evaluate(actual).AbsoluteVolume, digits);
    }

    private static string[] Points(BrepBody body) => body.Topology.Vertices
        .Select(v => { body.TryGetVertexPoint(v.Id, out var p); return $"{double.Round(p.X, 6) + 0.0:F6},{double.Round(p.Y, 6) + 0.0:F6},{double.Round(p.Z, 6) + 0.0:F6}"; })
        .Order(StringComparer.Ordinal).ToArray();
}
