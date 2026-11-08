using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Blends;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Tests.Step242;

namespace Aetheris.Kernel.Core.Tests.Brep.Blends;

/// <summary>The rolling-ball certificate: a face is a round when a ball centred a radius behind each of its
/// points touches both anchors; which anchors is a judgment over measured readings.</summary>
public sealed class BlendRecognitionTests
{
    [Theory]
    [InlineData(4, 6, 4)]
    [InlineData(6, 12, 8)]
    [InlineData(8, 12, 6)]
    [InlineData(12, 30, 20)]
    [InlineData(20, 30, 12)]
    public void RoundedPlatonicSolid_EveryEdgeIsAConvexStripBetweenItsTwoFaces_EveryCornerAJoiner(int faces, int edges, int vertices)
    {
        const double radius = 2;
        var body = ConvexStockPrimitives.CreateRegular(faces, 30, radius).Value;
        var plain = ConvexStockPrimitives.CreateRegular(faces, 30).Value;

        var recognized = BlendRecognition.Recognize(body);
        var strips = recognized.Where(f => f.Role == BlendFaceRole.Strip).ToArray();
        Assert.Equal(edges, strips.Length);
        Assert.Equal(vertices, recognized.Count(f => f.Role == BlendFaceRole.Joiner));
        Assert.Equal(faces, recognized.Count(f => f.Role == BlendFaceRole.Primary));
        foreach (var strip in strips)
        {
            var blend = strip.Blend!;
            Assert.Equal(BlendSection.Round, blend.Hypothesis.Section);
            Assert.True(blend.Hypothesis.Convex);
            Assert.Equal(radius, blend.Size, 9);
            Assert.True(blend.Residual < 1e-9, $"face {strip.Face.Value}: residual {blend.Residual}");
            Assert.All(new[] { blend.Hypothesis.AnchorA, blend.Hypothesis.AnchorB }, a => Assert.Equal(BlendFaceRole.Primary, recognized.Single(r => r.Face == a).Role));
        }
        Assert.DoesNotContain(BlendRecognition.Recognize(plain), f => f.Role != BlendFaceRole.Primary);
    }

    [Fact]
    public void RoundedBox_WrongReadingsAreRejectedWithTheirMeasurement()
    {
        var body = ConvexStockPrimitives.CreateRoundedBox(40, 30, 20, 3).Value;
        var strip = BlendRecognition.Recognize(body).First(f => f.Role == BlendFaceRole.Strip);
        var trace = strip.Judgment!;
        Assert.Equal(BlendRecognition.PolicyVersion, trace.PolicyVersion);
        // The same anchors with the ball on the wrong side: refused by the certificate, which says by how much.
        var concave = trace.Candidates.Single(c => c.Id == strip.Blend!.Hypothesis.Id.Replace("convex", "concave"));
        Assert.False(concave.Admissible);
        var residual = concave.Rules.Single(r => r.Id == "residual").Result;
        Assert.False(residual.Passed);
        Assert.True(residual.Measured > 1, $"measured {residual.Measured}");
    }

    [Fact]
    public void SizeLimitIsPolicy_ARoundOverItRefusesNamingItsRadius()
    {
        var body = ConvexStockPrimitives.CreateRoundedBox(40, 30, 20, 3).Value;
        var recognized = BlendRecognition.Recognize(body, new BlendRecognitionOptions { MaxSize = 2 });
        Assert.DoesNotContain(recognized, f => f.Role == BlendFaceRole.Strip);
        var cylinder = recognized.First(f => body.TryGetFaceSurfaceGeometry(f.Face, out var s) && s!.Kind == SurfaceGeometryKind.Cylinder);
        // The reading that certifies (residual passes) is the one refused, and only by the size rule.
        var certified = cylinder.Judgment!.Candidates.Single(c => c.Rules.Single(r => r.Id == "residual").Result.Passed);
        Assert.Equal(["size"], certified.Rules.Where(r => !r.Result.Passed).Select(r => r.Id));
        var size = certified.Rules.Single(r => r.Id == "size").Result;
        Assert.Equal(3, size.Measured, 9);
        Assert.Equal(2, size.Limit);
    }

    /// <summary>Fusion's AP214 export: tori and cylinders certify exactly, and some of its B-spline faces certify as
    /// strips or read as corner patches, which shape-by-kind recognition could not do.</summary>
    [Fact]
    public void So101_CertifiesAnalyticAndFreeformBlends()
    {
        var body = Step242Corpus.Body("testdata/step242/community/so101/Under_arm_SO101.step");
        var recognized = BlendRecognition.Recognize(body);
        var strips = recognized.Where(f => f.Role == BlendFaceRole.Strip).ToArray();
        Assert.True(strips.Length >= 65, $"{strips.Length} strips");
        // Anchors are faces that stay: no strip is read against another blend.
        var roles = recognized.ToDictionary(r => r.Face, r => r.Role);
        Assert.All(strips, s => Assert.True(roles[s.Blend!.Hypothesis.AnchorA] == BlendFaceRole.Primary && roles[s.Blend.Hypothesis.AnchorB] == BlendFaceRole.Primary, $"face {s.Face.Value}: {s.Reason}"));
        Assert.All(strips, s => Assert.True(s.Blend!.Residual <= new BlendRecognitionOptions().Limit(s.Blend.Size)));
        var freeform = recognized.Where(f => body.TryGetFaceSurfaceGeometry(f.Face, out var s) && s!.Kind == SurfaceGeometryKind.BSplineSurfaceWithKnots).ToArray();
        Assert.True(freeform.Count(f => f.Role != BlendFaceRole.Primary) >= 6, string.Join(", ", freeform.Select(f => $"{f.Face.Value}:{f.Role}")));
    }
}
