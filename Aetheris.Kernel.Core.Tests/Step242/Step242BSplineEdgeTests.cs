using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242BSplineEdgeTests
{
    [Fact]
    public void Step242_BSplineCurveWithKnots_AsEdgeCurve_ImportsAndSamplesDeterministically()
    {
        const string text = "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n"
            + "#1=MANIFOLD_SOLID_BREP('solid',#2);\n"
            + "#2=CLOSED_SHELL($,(#3));\n"
            + "#3=ADVANCED_FACE((#4),#30,.T.);\n"
            + "#4=FACE_OUTER_BOUND($,#5,.T.);\n"
            + "#5=EDGE_LOOP($,(#6,#7,#8));\n"
            + "#6=ORIENTED_EDGE($,$,$,#9,.T.);\n"
            + "#7=ORIENTED_EDGE($,$,$,#10,.T.);\n"
            + "#8=ORIENTED_EDGE($,$,$,#11,.T.);\n"
            + "#9=EDGE_CURVE($,#12,#13,#20,.T.);\n"
            + "#10=EDGE_CURVE($,#13,#14,#21,.T.);\n"
            + "#11=EDGE_CURVE($,#14,#12,#22,.T.);\n"
            + "#12=VERTEX_POINT($,#40);\n"
            + "#13=VERTEX_POINT($,#41);\n"
            + "#14=VERTEX_POINT($,#42);\n"
            + "#20=B_SPLINE_CURVE_WITH_KNOTS($,2,(#40,#43,#41),.UNSPECIFIED.,.F.,.F.,(3,3),(0.,1.),.UNSPECIFIED.);\n"
            + "#21=LINE($,#41,#50);\n"
            + "#22=LINE($,#42,#51);\n"
            + "#30=PLANE($,#60);\n"
            + "#40=CARTESIAN_POINT($,(0.,0.,0.));\n"
            + "#41=CARTESIAN_POINT($,(2.,0.,0.));\n"
            + "#42=CARTESIAN_POINT($,(0.,2.,0.));\n"
            + "#43=CARTESIAN_POINT($,(1.,0.6,0.));\n"
            + "#50=VECTOR($,#52,1.);\n"
            + "#51=VECTOR($,#53,1.);\n"
            + "#52=DIRECTION($,(-0.7071067811865475,0.7071067811865475,0.));\n"
            + "#53=DIRECTION($,(0.,-1.,0.));\n"
            + "#60=AXIS2_PLACEMENT_3D($,#40,#61,#62);\n"
            + "#61=DIRECTION($,(0.,0.,1.));\n"
            + "#62=DIRECTION($,(1.,0.,0.));\n"
            + "ENDSEC;\nEND-ISO-10303-21;";

        var first = Step242Importer.ImportBody(text);
        Assert.True(first.IsSuccess);

        var second = Step242Importer.ImportBody(text);
        Assert.True(second.IsSuccess);

        var curveA = Assert.Single(first.Value.Geometry.Curves, c => c.Value.Kind == CurveGeometryKind.BSpline3).Value.BSpline3;
        var curveB = Assert.Single(second.Value.Geometry.Curves, c => c.Value.Kind == CurveGeometryKind.BSpline3).Value.BSpline3;
        Assert.NotNull(curveA);
        Assert.NotNull(curveB);
        Assert.Equal(2, curveA.Value.Degree);
        Assert.Equal(0d, curveA.Value.DomainStart);
        Assert.Equal(1d, curveA.Value.DomainEnd);

        var parameters = new[] { 0d, 0.2d, 0.4d, 0.6d, 0.8d, 1d };
        var samplesA = parameters.Select(curveA.Value.Evaluate).ToArray();
        var samplesB = parameters.Select(curveB.Value.Evaluate).ToArray();
        Assert.True(samplesA.SequenceEqual(samplesB));
    }


    [Fact]
    public void Step242_BoundedCurveComplexBspline_AsEdgeCurve_ImportsAndSamplesDeterministically()
    {
        var text = LoadFixture("testdata/step242/handcrafted/edge-trimming/block-full-round.step");

        var first = Step242Importer.ImportBody(text);
        var second = Step242Importer.ImportBody(text);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        var firstBsplines = first.Value.Geometry.Curves
            .Where(c => c.Value.Kind == CurveGeometryKind.BSpline3)
            .Select(c => c.Value.BSpline3!.Value)
            .OrderBy(c => c.Degree)
            .ThenBy(c => c.ControlPoints.Count)
            .ToArray();
        var secondBsplines = second.Value.Geometry.Curves
            .Where(c => c.Value.Kind == CurveGeometryKind.BSpline3)
            .Select(c => c.Value.BSpline3!.Value)
            .OrderBy(c => c.Degree)
            .ThenBy(c => c.ControlPoints.Count)
            .ToArray();

        Assert.Equal(firstBsplines.Length, secondBsplines.Length);
        Assert.NotEmpty(firstBsplines);

        var sampleParameters = new[] { 0d, 0.25d, 0.5d, 0.75d, 1d };
        for (var i = 0; i < firstBsplines.Length; i++)
        {
            var a = firstBsplines[i];
            var b = secondBsplines[i];
            var samplesA = sampleParameters.Select(a.Evaluate).ToArray();
            var samplesB = sampleParameters.Select(b.Evaluate).ToArray();
            Assert.True(samplesA.SequenceEqual(samplesB));
        }
    }


    [Fact]
    public void Step242_NistFtc06_StaysPastBoundedCurveAndSelfIntersectBlockers_Deterministically()
    {
        const string relativePath = "testdata/step242/nist/FTC/nist_ftc_06_asme1_ap242-e2.stp";
        var entry = new Step242CorpusManifestEntry(
            Id: Path.GetFileNameWithoutExtension(relativePath),
            Path: relativePath,
            Group: "nist-bspline-regression",
            Notes: "regression",
            ExpectedFirstDiagnostic: null,
            ExpectHashStableAfterCanonicalization: null,
            ExpectTopologyCounts: null,
            ExpectGeometryKinds: null);

        var first = Step242CorpusManifestRunner.RunOne(entry);
        var second = Step242CorpusManifestRunner.RunOne(entry);

        Assert.DoesNotContain("EDGE_CURVE geometry 'BOUNDED_CURVE' is unsupported.", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.DoesNotContain("B_SPLINE_CURVE_WITH_KNOTS self_intersect", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.Equal(first.FirstDiagnostic.MessagePrefix, second.FirstDiagnostic.MessagePrefix);
        Assert.Equal(first.FirstFailureLayer, second.FirstFailureLayer);
    }

    [Theory]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_06_asme1_ap242-e2.stp")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_11_asme1_ap242-e2.stp")]
    public void Step242_NistTargets_AdvancePastBsplineSelfIntersectBlocker_Deterministically(string relativePath)
    {
        var entry = new Step242CorpusManifestEntry(
            Id: Path.GetFileNameWithoutExtension(relativePath),
            Path: relativePath,
            Group: "nist-bspline-regression",
            Notes: "regression",
            ExpectedFirstDiagnostic: null,
            ExpectHashStableAfterCanonicalization: null,
            ExpectTopologyCounts: null,
            ExpectGeometryKinds: null);

        var first = Step242CorpusManifestRunner.RunOne(entry);
        var second = Step242CorpusManifestRunner.RunOne(entry);

        Assert.DoesNotContain("B_SPLINE_CURVE_WITH_KNOTS self_intersect", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.Equal(first.FirstDiagnostic.MessagePrefix, second.FirstDiagnostic.MessagePrefix);
        Assert.Equal(first.FirstFailureLayer, second.FirstFailureLayer);
        Assert.False(string.IsNullOrWhiteSpace(first.FirstDiagnostic.MessagePrefix));
    }

    private static string LoadFixture(string relativePath)
    {
        var path = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(path);
    }
}
