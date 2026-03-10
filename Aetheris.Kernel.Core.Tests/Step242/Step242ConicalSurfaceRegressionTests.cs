using Aetheris.Kernel.Core.Step242;
using System.Text.RegularExpressions;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242ConicalSurfaceRegressionTests
{
    [Fact]
    public void Step242_Axis2Placement3D_RefDirectionOmitted_UsesDefaultPlacementDirection()
    {
        const string text = "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n"
            + "#1=MANIFOLD_SOLID_BREP('solid',#2);\n"
            + "#2=CLOSED_SHELL($,(#3));\n"
            + "#3=ADVANCED_FACE((#4),#5,.T.);\n"
            + "#4=FACE_OUTER_BOUND($,#6,.T.);\n"
            + "#5=PLANE($,#20);\n"
            + "#6=EDGE_LOOP($,(#7,#8,#9,#10));\n"
            + "#7=ORIENTED_EDGE($,$,$,#30,.T.);\n"
            + "#8=ORIENTED_EDGE($,$,$,#31,.T.);\n"
            + "#9=ORIENTED_EDGE($,$,$,#32,.T.);\n"
            + "#10=ORIENTED_EDGE($,$,$,#33,.T.);\n"
            + "#30=EDGE_CURVE($,#40,#41,#50,.T.);\n"
            + "#31=EDGE_CURVE($,#41,#42,#51,.T.);\n"
            + "#32=EDGE_CURVE($,#42,#43,#52,.T.);\n"
            + "#33=EDGE_CURVE($,#43,#40,#53,.T.);\n"
            + "#40=VERTEX_POINT($,#60);\n"
            + "#41=VERTEX_POINT($,#61);\n"
            + "#42=VERTEX_POINT($,#62);\n"
            + "#43=VERTEX_POINT($,#63);\n"
            + "#50=LINE($,#60,#70);\n"
            + "#51=LINE($,#61,#71);\n"
            + "#52=LINE($,#62,#72);\n"
            + "#53=LINE($,#63,#73);\n"
            + "#60=CARTESIAN_POINT($,(0.,0.,0.));\n"
            + "#61=CARTESIAN_POINT($,(1.,0.,0.));\n"
            + "#62=CARTESIAN_POINT($,(1.,1.,0.));\n"
            + "#63=CARTESIAN_POINT($,(0.,1.,0.));\n"
            + "#70=VECTOR($,#80,1.);\n"
            + "#71=VECTOR($,#81,1.);\n"
            + "#72=VECTOR($,#82,1.);\n"
            + "#73=VECTOR($,#83,1.);\n"
            + "#80=DIRECTION($,(1.,0.,0.));\n"
            + "#81=DIRECTION($,(0.,1.,0.));\n"
            + "#82=DIRECTION($,(-1.,0.,0.));\n"
            + "#83=DIRECTION($,(0.,-1.,0.));\n"
            + "#20=AXIS2_PLACEMENT_3D('',#60,#21,$);\n"
            + "#21=DIRECTION('',(0.,0.,1.));\n"
            + "ENDSEC;\nEND-ISO-10303-21;";

        var import = Step242Importer.ImportBody(text);
        Assert.True(import.IsSuccess);
    }

    [Fact]
    public void Step242_ConicalSurfaceDecoder_AcceptsZeroRadiusWithRadianSemiAngle()
    {
        const string text = "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n"
            + "#10=CONICAL_SURFACE('',#20,0.,1.029744258676654);\n"
            + "#20=AXIS2_PLACEMENT_3D('',#21,#22,#23);\n"
            + "#21=CARTESIAN_POINT('',(1.,2.,3.));\n"
            + "#22=DIRECTION('',(0.,0.,1.));\n"
            + "#23=DIRECTION('',(1.,0.,0.));\n"
            + "ENDSEC;\nEND-ISO-10303-21;";

        var parse = Step242SubsetParser.Parse(text);
        Assert.True(parse.IsSuccess);

        var coneEntityResult = parse.Value.TryGetEntity(10, "CONICAL_SURFACE");
        Assert.True(coneEntityResult.IsSuccess);

        var cone = Step242SubsetDecoder.ReadConicalSurface(parse.Value, coneEntityResult.Value);
        Assert.True(cone.IsSuccess);

        Assert.Equal(1d, cone.Value.Apex.X, 9);
        Assert.Equal(2d, cone.Value.Apex.Y, 9);
        Assert.Equal(3d, cone.Value.Apex.Z, 9);
        Assert.Equal(1.029744258676654d, cone.Value.SemiAngleRadians, 12);
    }

    [Theory]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp", "", "Audit.None", "No diagnostics.")]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_02_asme1_ap242-e2.stp", "", "Audit.None", "No diagnostics.")]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_03_asme1_ap242-e2.stp", "", "", "")]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_04_asme1_ap242-e1.stp", "exporter", "", "Unsupported surface kind 'Torus'.")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_06_asme1_ap242-e2.stp", "tessellator", "", "Face 12 spherical trim loop must contain at least three coedges. Observed")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_09_asme1_ap242-e1.stp", "", "Audit.None", "No diagnostics.")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_10_asme1_ap242-e2.stp", "tessellator", "", "Face ")]
    [InlineData("testdata/step242/nist/STC/nist_stc_09_asme1_ap242-e3.stp", "", "", "")]
    public void Step242_NistCurvedRevolvedTargets_AdvancePastOldTopologyFamily_AndReportDeterministicNextBlocker(
        string relativePath,
        string expectedLayer,
        string expectedSource,
        string expectedMessagePrefix)
    {
        var entry = new Step242CorpusManifestEntry(
            Id: Path.GetFileNameWithoutExtension(relativePath),
            Path: relativePath,
            Group: "nist-conical-regression",
            Notes: "regression",
            ExpectedFirstDiagnostic: null,
            ExpectHashStableAfterCanonicalization: null,
            ExpectTopologyCounts: null,
            ExpectGeometryKinds: null);

        var first = Step242CorpusManifestRunner.RunOne(entry);
        var second = Step242CorpusManifestRunner.RunOne(entry);

        Assert.DoesNotContain("CONICAL_SURFACE radius", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.DoesNotContain("expected mirrored line uses for this cone/revolved topology", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.DoesNotContain("supports four-coedge torus/revolved loop layouts", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.DoesNotContain("unsupported subfamily 'other (coedges=3, uniqueEdges=3)'", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.DoesNotContain("Unsupported surface kind 'Cylinder'", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.DoesNotContain("Unsupported surface kind 'Cone'", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.DoesNotContain("Unsupported curve kind 'Circle3'", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.DoesNotContain("Unsupported curve kind 'BSpline3'", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.DoesNotContain("Unsupported curve kind 'Ellipse3'.", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
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
            Assert.NotEqual("Viewer.Tessellation.PlanarCurveFlatteningUnsupported", first.FirstDiagnostic.Source);
            Assert.DoesNotContain("planar curve flattening does not support curve kind 'BSpline3'", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
            Assert.DoesNotContain("Unsupported surface kind 'Sphere'.", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        }

        Assert.Equal(first.FirstFailureLayer, second.FirstFailureLayer);
        Assert.Equal(first.FirstDiagnostic.Source, second.FirstDiagnostic.Source);
        Assert.Equal(first.FirstDiagnostic.MessagePrefix, second.FirstDiagnostic.MessagePrefix);
        Assert.False(string.IsNullOrWhiteSpace(first.FirstDiagnostic.MessagePrefix));
    }

    [Theory]
    [InlineData(
        "testdata/step242/nist/CTC/nist_ctc_02_asme1_ap242-e2.stp",
        "",
        "Audit.None",
        "No diagnostics.")]
    [InlineData(
        "testdata/step242/nist/STC/nist_stc_10_asme1_ap242-e2.stp",
        "importer-topology",
        "Importer.LoopRole.InnerDisjointAfterNormalization",
        "Inner loop could not be normalized")]
    public void Step242_NistTargets_AdvancePastCircleTrim_AndKeepDeterministicNextBlocker(
        string relativePath,
        string expectedLayer,
        string expectedSource,
        string expectedMessagePrefix)
    {
        var entry = new Step242CorpusManifestEntry(
            Id: Path.GetFileNameWithoutExtension(relativePath),
            Path: relativePath,
            Group: "nist-circle-trim-regression",
            Notes: "regression",
            ExpectedFirstDiagnostic: null,
            ExpectHashStableAfterCanonicalization: null,
            ExpectTopologyCounts: null,
            ExpectGeometryKinds: null);

        var first = Step242CorpusManifestRunner.RunOne(entry);
        var second = Step242CorpusManifestRunner.RunOne(entry);

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
            Assert.NotEqual("Viewer.Tessellation.PlanarCurveFlatteningUnsupported", first.FirstDiagnostic.Source);
            Assert.DoesNotContain("planar curve flattening does not support curve kind 'BSpline3'", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
            Assert.DoesNotContain("Unsupported surface kind 'Sphere'.", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        }

        Assert.Equal(first.FirstFailureLayer, second.FirstFailureLayer);
        Assert.Equal(first.FirstDiagnostic.Source, second.FirstDiagnostic.Source);
        Assert.Equal(first.FirstDiagnostic.MessagePrefix, second.FirstDiagnostic.MessagePrefix);
    }


    [Fact]
    public void Step242_ConicalSurfaceImport_DegreeContextFromGlobalUnits_NormalizesToRadians()
    {
        var fixturePath = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), "testdata", "step242", "nist", "CTC", "nist_ctc_01_asme1_ap242-e1.stp");
        var source = File.ReadAllText(fixturePath);

        var import = Step242Importer.ImportBody(source);
        Assert.True(import.IsSuccess);

        var cones = import.Value.Geometry.Surfaces
            .Select(surface => surface.Value)
            .Where(surface => surface.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cone)
            .Select(surface => surface.Cone)
            .Where(cone => cone.HasValue)
            .Select(cone => cone!.Value.SemiAngleRadians)
            .ToArray();

        Assert.NotEmpty(cones);
        Assert.Contains(cones, angle => double.Abs(angle - 1.02974425867665d) <= 1e-6d);
    }

    [Fact]
    public void Step242_ConicalSurfaceImport_RadianContextFromGlobalUnits_RemainsRadians()
    {
        var fixturePath = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), "testdata", "step242", "nist", "STC", "nist_stc_09_asme1_ap242-e3.stp");
        var source = File.ReadAllText(fixturePath);

        var import = Step242Importer.ImportBody(source);
        Assert.True(import.IsSuccess);

        var cones = import.Value.Geometry.Surfaces
            .Select(surface => surface.Value)
            .Where(surface => surface.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cone)
            .Select(surface => surface.Cone)
            .Where(cone => cone.HasValue)
            .Select(cone => cone!.Value.SemiAngleRadians)
            .ToArray();

        Assert.NotEmpty(cones);
        Assert.Contains(cones, angle => double.Abs(angle - 0.7853981634d) <= 1e-6d);
    }

    [Fact]
    public void Step242_ConicalSurface_WithNonZeroPlacementRadius_PreservesStoredPlacementSemanticsThroughRoundTrip()
    {
        const string text = "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n"
            + "#1=MANIFOLD_SOLID_BREP('solid',#2);\n"
            + "#2=CLOSED_SHELL($,(#3));\n"
            + "#3=ADVANCED_FACE((#4),#5,.T.);\n"
            + "#4=FACE_OUTER_BOUND($,#6,.T.);\n"
            + "#5=CONICAL_SURFACE($,#20,2.,0.7853981633974483);\n"
            + "#6=EDGE_LOOP($,(#10));\n"
            + "#10=ORIENTED_EDGE($,$,$,#11,.T.);\n"
            + "#11=EDGE_CURVE($,#12,#13,#14,.T.);\n"
            + "#12=VERTEX_POINT($,#30);\n"
            + "#13=VERTEX_POINT($,#31);\n"
            + "#14=LINE($,#30,#40);\n"
            + "#20=AXIS2_PLACEMENT_3D($,#21,#22,#23);\n"
            + "#21=CARTESIAN_POINT($,(0.,0.,5.));\n"
            + "#22=DIRECTION($,(0.,0.,1.));\n"
            + "#23=DIRECTION($,(1.,0.,0.));\n"
            + "#30=CARTESIAN_POINT($,(2.,0.,5.));\n"
            + "#31=CARTESIAN_POINT($,(2.,0.,6.));\n"
            + "#40=VECTOR($,#41,1.);\n"
            + "#41=DIRECTION($,(0.,0.,1.));\n"
            + "ENDSEC;\nEND-ISO-10303-21;";

        var import = Step242Importer.ImportBody(text);
        Assert.True(import.IsSuccess);

        var cone = import.Value.Geometry.Surfaces
            .Select(surface => surface.Value)
            .Where(surface => surface.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cone)
            .Select(surface => surface.Cone)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .Single();

        Assert.Equal(5d, cone.PlacementOrigin.Z, 9);
        Assert.Equal(2d, cone.PlacementRadius, 9);
        Assert.Equal(3d, cone.Apex.Z, 9);

        var export = Step242Exporter.ExportBody(import.Value);
        Assert.True(export.IsSuccess);
        Assert.Contains("CONICAL_SURFACE", export.Value, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"CONICAL_SURFACE\([^,]*,[^,]*,2(?:\.0*)?,", RegexOptions.CultureInvariant), export.Value);
    }

    [Fact]
    public void Step242_NistStc07_AdvancesPastAxis2PlacementRefDirectionFirstBlocker_AndRemainsDeterministic()
    {
        const string relativePath = "testdata/step242/nist/STC/nist_stc_07_asme1_ap242-e3.stp";
        var entry = new Step242CorpusManifestEntry(
            Id: Path.GetFileNameWithoutExtension(relativePath),
            Path: relativePath,
            Group: "nist-axis2-placement-regression",
            Notes: "regression",
            ExpectedFirstDiagnostic: null,
            ExpectHashStableAfterCanonicalization: null,
            ExpectTopologyCounts: null,
            ExpectGeometryKinds: null);

        var first = Step242CorpusManifestRunner.RunOne(entry);
        var second = Step242CorpusManifestRunner.RunOne(entry);

        Assert.DoesNotContain("AXIS2_PLACEMENT_3D ref direction: expected entity reference argument.", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.NotEqual("AXIS2_PLACEMENT_3D axis and ref direction must not be parallel.", first.FirstDiagnostic.MessagePrefix);
        Assert.Equal(first.FirstFailureLayer, second.FirstFailureLayer);
        Assert.Equal(first.FirstDiagnostic.Source, second.FirstDiagnostic.Source);
        Assert.Equal(first.FirstDiagnostic.MessagePrefix, second.FirstDiagnostic.MessagePrefix);
    }

    [Fact]
    public void Step242_Axis2Placement3D_DefaultedRefDirectionParallelToAxis_UsesDeterministicPerpendicularFallback()
    {
        const string text = "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n"
            + "#1=MANIFOLD_SOLID_BREP('solid',#2);\n"
            + "#2=CLOSED_SHELL($,(#3));\n"
            + "#3=ADVANCED_FACE((#4),#5,.T.);\n"
            + "#4=FACE_OUTER_BOUND($,#6,.T.);\n"
            + "#5=PLANE($,#30);\n"
            + "#6=EDGE_LOOP($,(#7));\n"
            + "#7=ORIENTED_EDGE($,$,$,#8,.T.);\n"
            + "#8=EDGE_CURVE($,#9,#9,#10,.T.);\n"
            + "#9=VERTEX_POINT($,#11);\n"
            + "#10=CIRCLE($,#31,1.0);\n"
            + "#11=CARTESIAN_POINT($,(1,0,0));\n"
            + "#30=AXIS2_PLACEMENT_3D($,#12,#13,#14);\n"
            + "#31=AXIS2_PLACEMENT_3D($,#12,#15,$);\n"
            + "#12=CARTESIAN_POINT($,(0,0,0));\n"
            + "#13=DIRECTION($,(0,0,1));\n"
            + "#14=DIRECTION($,(1,0,0));\n"
            + "#15=DIRECTION($,(1,0,0));\n"
            + "ENDSEC;\nEND-ISO-10303-21;";

        var first = Step242Importer.ImportBody(text);
        var second = Step242Importer.ImportBody(text);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        var firstCircle = first.Value.Geometry.Curves
            .Select(curve => curve.Value)
            .Single(curve => curve.Kind == Aetheris.Kernel.Core.Geometry.CurveGeometryKind.Circle3)
            .Circle3!.Value;

        var secondCircle = second.Value.Geometry.Curves
            .Select(curve => curve.Value)
            .Single(curve => curve.Kind == Aetheris.Kernel.Core.Geometry.CurveGeometryKind.Circle3)
            .Circle3!.Value;

        Assert.True(double.Abs(firstCircle.Normal.ToVector().Dot(firstCircle.XAxis.ToVector())) < 1e-12d);
        Assert.True(double.Abs(secondCircle.Normal.ToVector().Dot(secondCircle.XAxis.ToVector())) < 1e-12d);
        Assert.Equal(firstCircle.XAxis.ToVector().X, secondCircle.XAxis.ToVector().X, 12);
        Assert.Equal(firstCircle.XAxis.ToVector().Y, secondCircle.XAxis.ToVector().Y, 12);
        Assert.Equal(firstCircle.XAxis.ToVector().Z, secondCircle.XAxis.ToVector().Z, 12);
    }

    [Fact]
    public void Step242_Axis2Placement3D_ExplicitParallelAxisAndRefDirection_RemainsInvalid()
    {
        const string text = "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n"
            + "#1=MANIFOLD_SOLID_BREP('solid',#2);\n"
            + "#2=CLOSED_SHELL($,(#3));\n"
            + "#3=ADVANCED_FACE((#4),#5,.T.);\n"
            + "#4=FACE_OUTER_BOUND($,#6,.T.);\n"
            + "#5=PLANE($,#30);\n"
            + "#6=EDGE_LOOP($,(#7));\n"
            + "#7=ORIENTED_EDGE($,$,$,#8,.T.);\n"
            + "#8=EDGE_CURVE($,#9,#9,#10,.T.);\n"
            + "#9=VERTEX_POINT($,#11);\n"
            + "#10=CIRCLE($,#31,1.0);\n"
            + "#11=CARTESIAN_POINT($,(1,0,0));\n"
            + "#30=AXIS2_PLACEMENT_3D($,#12,#13,#14);\n"
            + "#31=AXIS2_PLACEMENT_3D($,#12,#15,#16);\n"
            + "#12=CARTESIAN_POINT($,(0,0,0));\n"
            + "#13=DIRECTION($,(0,0,1));\n"
            + "#14=DIRECTION($,(1,0,0));\n"
            + "#15=DIRECTION($,(1,0,0));\n"
            + "#16=DIRECTION($,(1,0,0));\n"
            + "ENDSEC;\nEND-ISO-10303-21;";

        var import = Step242Importer.ImportBody(text);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal("Importer.Geometry.Circle", diagnostic.Source);
        Assert.Equal("AXIS2_PLACEMENT_3D axis and ref direction must not be parallel.", diagnostic.Message);
    }
}
