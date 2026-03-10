using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242BSplineSurfaceWithKnotsTests
{
    [Fact]
    public void ImportBody_BSplineSurfaceWithKnotsFace_DecodesSurfaceGeometry()
    {
        const string text = "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=MANIFOLD_SOLID_BREP('s',#2);\n#2=CLOSED_SHELL($,(#3));\n#3=ADVANCED_FACE((#4),#5,.T.);\n#4=FACE_OUTER_BOUND($,#6,.T.);\n#5=B_SPLINE_SURFACE_WITH_KNOTS('',3,3,((#100,#101,#102,#103),(#104,#105,#106,#107),(#108,#109,#110,#111),(#112,#113,#114,#115)),.UNSPECIFIED.,.F.,.F.,.F.,(4,4),(4,4),(0.,1.),(0.,1.),.UNSPECIFIED.);\n#6=EDGE_LOOP($,(#10,#11,#12,#13));\n#10=ORIENTED_EDGE($,$,$,#20,.T.);\n#11=ORIENTED_EDGE($,$,$,#21,.T.);\n#12=ORIENTED_EDGE($,$,$,#22,.T.);\n#13=ORIENTED_EDGE($,$,$,#23,.T.);\n#20=EDGE_CURVE($,#30,#31,#40,.T.);\n#21=EDGE_CURVE($,#31,#32,#41,.T.);\n#22=EDGE_CURVE($,#32,#33,#42,.T.);\n#23=EDGE_CURVE($,#33,#30,#43,.T.);\n#30=VERTEX_POINT($,#50);\n#31=VERTEX_POINT($,#51);\n#32=VERTEX_POINT($,#52);\n#33=VERTEX_POINT($,#53);\n#40=LINE($,#50,#80);\n#41=LINE($,#51,#81);\n#42=LINE($,#52,#82);\n#43=LINE($,#53,#83);\n#50=CARTESIAN_POINT($,(0,0,0));\n#51=CARTESIAN_POINT($,(1,0,0));\n#52=CARTESIAN_POINT($,(1,1,0));\n#53=CARTESIAN_POINT($,(0,1,0));\n#80=VECTOR($,#84,1.0);\n#81=VECTOR($,#85,1.0);\n#82=VECTOR($,#86,1.0);\n#83=VECTOR($,#87,1.0);\n#84=DIRECTION($,(1,0,0));\n#85=DIRECTION($,(0,1,0));\n#86=DIRECTION($,(-1,0,0));\n#87=DIRECTION($,(0,-1,0));\n#100=CARTESIAN_POINT($,(0,0,0));\n#101=CARTESIAN_POINT($,(0,0.33,0));\n#102=CARTESIAN_POINT($,(0,0.66,0));\n#103=CARTESIAN_POINT($,(0,1,0));\n#104=CARTESIAN_POINT($,(0.33,0,0));\n#105=CARTESIAN_POINT($,(0.33,0.33,0));\n#106=CARTESIAN_POINT($,(0.33,0.66,0));\n#107=CARTESIAN_POINT($,(0.33,1,0));\n#108=CARTESIAN_POINT($,(0.66,0,0));\n#109=CARTESIAN_POINT($,(0.66,0.33,0));\n#110=CARTESIAN_POINT($,(0.66,0.66,0));\n#111=CARTESIAN_POINT($,(0.66,1,0));\n#112=CARTESIAN_POINT($,(1,0,0));\n#113=CARTESIAN_POINT($,(1,0.33,0));\n#114=CARTESIAN_POINT($,(1,0.66,0));\n#115=CARTESIAN_POINT($,(1,1,0));\nENDSEC;\nEND-ISO-10303-21;";

        var import = Step242Importer.ImportBody(text);

        Assert.True(import.IsSuccess);
        Assert.Contains(import.Value.Geometry.Surfaces, s => s.Value.Kind == SurfaceGeometryKind.BSplineSurfaceWithKnots);

        var export = Step242Exporter.ExportBody(import.Value);
        Assert.True(export.IsSuccess);
        Assert.Contains("B_SPLINE_SURFACE_WITH_KNOTS(", export.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Step242_NistCtc02_BSplineSurfaces_ObservedAsNonRationalWithExpectedDegreeFamilies()
    {
        var path = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), "testdata", "step242", "nist", "CTC", "nist_ctc_02_asme1_ap242-e2.stp");
        var import = Step242Importer.ImportBody(File.ReadAllText(path));
        Assert.True(import.IsSuccess);

        var splineSurfaces = import.Value.Geometry.Surfaces
            .Select(surface => surface.Value)
            .Where(surface => surface.Kind == SurfaceGeometryKind.BSplineSurfaceWithKnots)
            .Select(surface => surface.BSplineSurfaceWithKnots)
            .Where(surface => surface is not null)
            .Select(surface => surface!)
            .ToArray();

        Assert.NotEmpty(splineSurfaces);
        Assert.All(splineSurfaces, surface =>
        {
            Assert.True(surface.ControlPoints.Count >= surface.DegreeU + 1);
            Assert.True(surface.ControlPoints[0].Count >= surface.DegreeV + 1);
            Assert.Equal(surface.KnotMultiplicitiesU.Count, surface.KnotValuesU.Count);
            Assert.Equal(surface.KnotMultiplicitiesV.Count, surface.KnotValuesV.Count);
        });

        var degreePairs = splineSurfaces
            .Select(surface => (surface.DegreeU, surface.DegreeV))
            .Distinct()
            .OrderBy(pair => pair.DegreeU)
            .ThenBy(pair => pair.DegreeV)
            .ToArray();

        Assert.Contains((1, 3), degreePairs);
        Assert.Contains((3, 1), degreePairs);
        Assert.Contains((3, 3), degreePairs);
    }

    [Theory]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_02_asme1_ap242-e2.stp")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_10_asme1_ap242-e2.stp")]
    public void Step242_NistTargets_DoNotFirstFailOnUnsupportedBSplineSurfaceWithKnots(string relativePath)
    {
        var entry = new Step242CorpusManifestEntry(
            Id: Path.GetFileNameWithoutExtension(relativePath),
            Path: relativePath,
            Group: "nist-bspline-surface-regression",
            Notes: "regression",
            ExpectedFirstDiagnostic: null,
            ExpectHashStableAfterCanonicalization: null,
            ExpectTopologyCounts: null,
            ExpectGeometryKinds: null);

        var first = Step242CorpusManifestRunner.RunOne(entry);
        var second = Step242CorpusManifestRunner.RunOne(entry);

        Assert.DoesNotContain("ADVANCED_FACE surface 'B_SPLINE_SURFACE_WITH_KNOTS' is unsupported.", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.Equal(first.FirstFailureLayer, second.FirstFailureLayer);
        Assert.Equal(first.FirstDiagnostic.Source, second.FirstDiagnostic.Source);
        Assert.Equal(first.FirstDiagnostic.MessagePrefix, second.FirstDiagnostic.MessagePrefix);
    }

    [Theory]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_02_asme1_ap242-e2.stp")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_10_asme1_ap242-e2.stp")]
    public void Step242_NistTargets_FirstBlockerIsExplicitAfterBSplineSurfaceProgression(string relativePath)
    {
        var entry = new Step242CorpusManifestEntry(
            Id: Path.GetFileNameWithoutExtension(relativePath),
            Path: relativePath,
            Group: "nist-bspline-surface-regression",
            Notes: "regression",
            ExpectedFirstDiagnostic: null,
            ExpectHashStableAfterCanonicalization: null,
            ExpectTopologyCounts: null,
            ExpectGeometryKinds: null);

        var run = Step242CorpusManifestRunner.RunOne(entry);
        Assert.False(string.IsNullOrWhiteSpace(run.FirstDiagnostic.MessagePrefix));
        if (string.Equals(relativePath, "testdata/step242/nist/CTC/nist_ctc_02_asme1_ap242-e2.stp", StringComparison.Ordinal))
        {
            Assert.DoesNotContain("Unsupported curve kind 'Ellipse3'.", run.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
            Assert.DoesNotContain("sphere tessellation supports only untrimmed sphere faces with zero loops.", run.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_02_asme1_ap242-e2.stp", "", "Audit.None", "No diagnostics.")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp", "tessellator", "", "Face ")]
    [InlineData("testdata/step242/nist/STC/nist_stc_08_asme1_ap242-e3.stp", "exporter", "", "Unsupported surface kind 'Torus'.")]
    public void Step242_TrimmedSphereTargets_AdvancePastOldUntrimmedSphereBlocker_AndRemainDeterministic(
        string relativePath,
        string expectedLayer,
        string expectedSource,
        string expectedMessagePrefix)
    {
        var entry = new Step242CorpusManifestEntry(
            Id: Path.GetFileNameWithoutExtension(relativePath),
            Path: relativePath,
            Group: "nist-sphere-trim-regression",
            Notes: "regression",
            ExpectedFirstDiagnostic: null,
            ExpectHashStableAfterCanonicalization: null,
            ExpectTopologyCounts: null,
            ExpectGeometryKinds: null);

        var first = Step242CorpusManifestRunner.RunOne(entry);
        var second = Step242CorpusManifestRunner.RunOne(entry);

        Assert.DoesNotContain("sphere tessellation supports only untrimmed sphere faces with zero loops.", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        if (!string.IsNullOrEmpty(expectedLayer))
        {
            Assert.Equal(expectedLayer, first.FirstFailureLayer);
        }

        if (!string.IsNullOrEmpty(expectedSource))
        {
            Assert.Equal(expectedSource, first.FirstDiagnostic.Source);
        }

        if (!string.IsNullOrEmpty(expectedMessagePrefix))
        {
            Assert.StartsWith(expectedMessagePrefix, first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        }

        if (string.Equals(relativePath, "testdata/step242/nist/CTC/nist_ctc_02_asme1_ap242-e2.stp", StringComparison.Ordinal))
        {
            Assert.DoesNotContain("Unsupported curve kind 'Ellipse3'.", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
            Assert.DoesNotContain("unsupported surface kind 'BSplineSurfaceWithKnots'", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
            Assert.DoesNotContain("Unsupported surface kind 'Sphere'.", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        }
        Assert.Equal(first.FirstFailureLayer, second.FirstFailureLayer);
        Assert.Equal(first.FirstDiagnostic.Source, second.FirstDiagnostic.Source);
        Assert.Equal(first.FirstDiagnostic.MessagePrefix, second.FirstDiagnostic.MessagePrefix);
    }
}
