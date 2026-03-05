using System.Text;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242ImporterTests
{
    [Fact]
    public void ImportBody_KnownGoodM22SubsetText_ReturnsValidatedBody()
    {
        var fixtureText = Step242FixtureCorpus.CanonicalBoxGolden;

        var import = Step242Importer.ImportBody(fixtureText);

        Assert.True(import.IsSuccess);
        var validation = BrepBindingValidator.Validate(import.Value, requireAllEdgeAndFaceBindings: true);
        Assert.True(validation.IsSuccess);

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);
        Assert.True(tessellation.IsSuccess);
        Assert.NotEmpty(tessellation.Value.FacePatches);
    }

    [Fact]
    public void ExportImportRoundTrip_BoxSubset_PreservesBasicTopologyInvariants()
    {
        var boxResult = BrepPrimitives.CreateBox(4d, 6d, 8d);
        Assert.True(boxResult.IsSuccess);

        var export = Step242Exporter.ExportBody(boxResult.Value);
        Assert.True(export.IsSuccess);

        var import = Step242Importer.ImportBody(export.Value);

        Assert.True(import.IsSuccess);
        Assert.Equal(boxResult.Value.Topology.Vertices.Count(), import.Value.Topology.Vertices.Count());
        Assert.Equal(boxResult.Value.Topology.Edges.Count(), import.Value.Topology.Edges.Count());
        Assert.Equal(boxResult.Value.Topology.Faces.Count(), import.Value.Topology.Faces.Count());

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);
        Assert.True(tessellation.IsSuccess);
    }

    [Fact]
    public void ImportBody_MalformedStep_ReturnsDiagnosticWithoutThrowing()
    {
        var malformed = Step242FixtureCorpus.MalformedMissingParen;

        var import = Step242Importer.ImportBody(malformed);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.InvalidArgument, diagnostic.Code);
        Assert.Equal(KernelDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.StartsWith("Parser", diagnostic.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportBody_UnsupportedEntityInParseableText_ReturnsNotImplementedDiagnostic()
    {
        var unsupported = Step242FixtureCorpus.UnsupportedToroidalSurface;

        var import = Step242Importer.ImportBody(unsupported);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal(KernelDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Importer.EntityFamily", diagnostic.Source);
        Assert.Contains("unsupported", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImportBody_MissingSolidRoot_ReturnsDeterministicTopologyRootDiagnostic()
    {
        const string noRoot = "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=CARTESIAN_POINT($,(0,0,0));\nENDSEC;\nEND-ISO-10303-21;";

        var import = Step242Importer.ImportBody(noRoot);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Importer.TopologyRoot", diagnostic.Source);
        Assert.StartsWith("Missing MANIFOLD_SOLID_BREP", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportBody_MultipleSolidRoots_ReturnsDeterministicSingleSolidDiagnostic()
    {
        const string multiRoot = "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=MANIFOLD_SOLID_BREP('a',#3);\n#2=MANIFOLD_SOLID_BREP('b',#3);\n#3=CLOSED_SHELL($,());\nENDSEC;\nEND-ISO-10303-21;";

        var import = Step242Importer.ImportBody(multiRoot);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Importer.SingleSolid", diagnostic.Source);
        Assert.StartsWith("Multiple MANIFOLD_SOLID_BREP", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportBody_DegenerateDirection_ReturnsDiagnosticWithoutThrowing()
    {
        const string degenerateDirection = "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=MANIFOLD_SOLID_BREP('solid',#2);\n#2=CLOSED_SHELL($,(#3));\n#3=ADVANCED_FACE((#4),#5,.T.);\n#4=FACE_OUTER_BOUND($,#6,.T.);\n#5=PLANE($,#20);\n#6=EDGE_LOOP($,(#7));\n#7=ORIENTED_EDGE($,$,$,#8,.T.);\n#8=EDGE_CURVE($,#9,#10,#11,.T.);\n#9=VERTEX_POINT($,#12);\n#10=VERTEX_POINT($,#13);\n#11=LINE($,#12,#14);\n#12=CARTESIAN_POINT($,(0,0,0));\n#13=CARTESIAN_POINT($,(1,0,0));\n#14=VECTOR($,#15,1.0);\n#15=DIRECTION($,(1,0,0));\n#20=AXIS2_PLACEMENT_3D($,#12,#21,#22);\n#21=DIRECTION($,(0,0,0));\n#22=DIRECTION($,(1,0,0));\nENDSEC;\nEND-ISO-10303-21;";

        var import = Step242Importer.ImportBody(degenerateDirection);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Importer.Geometry.Direction", diagnostic.Source);
        Assert.StartsWith("Degenerate direction vector", diagnostic.Message, StringComparison.Ordinal);
    }


    [Fact]
    public void Step242_AdvancedFace_Bounds_SingleAggregate_ConsumesCorrectly()
    {
        var text = LoadFixture("testdata/step242/syntax-robustness/advanced-face-bounds-single-aggregate.step");

        var import = Step242Importer.ImportBody(text);

        Assert.True(import.IsSuccess);
        Assert.DoesNotContain(import.Diagnostics, d => d.Message.Contains("ADVANCED_FACE surface: expected entity reference or inline entity constructor.", StringComparison.Ordinal));
    }

    [Fact]
    public void Step242_AdvancedFace_Bounds_MultiAggregate_ConsumesCorrectly()
    {
        var text = LoadFixture("testdata/step242/syntax-robustness/advanced-face-bounds-multi-aggregate.step");

        var import = Step242Importer.ImportBody(text);

        Assert.DoesNotContain(import.Diagnostics, d => d.Message.Contains("ADVANCED_FACE surface: expected entity reference or inline entity constructor.", StringComparison.Ordinal));
        Assert.DoesNotContain(import.Diagnostics, d => string.Equals(d.Source, "Importer.StepSyntax.AdvancedFaceBounds", StringComparison.Ordinal));
    }

    [Fact]
    public void Step242_FreeCAD_Repro_ImportsPastAdvancedFaceBounds()
    {
        var text = LoadFixture("testdata/step242/syntax-robustness/freecad-pad-repro.step");

        var import = Step242Importer.ImportBody(text);

        Assert.True(import.IsSuccess);
        Assert.DoesNotContain(import.Diagnostics, d => d.Message.Contains("ADVANCED_FACE surface", StringComparison.Ordinal));
    }

    [Fact]
    public void Step242_AdvancedFace_Surface_AllowsInlinePlaneConstructor()
    {
        var text = LoadFixture("testdata/step242/syntax-robustness/advanced-face-inline-plane.step");

        var parseResult = Step242SubsetParser.Parse(text);
        Assert.True(parseResult.IsSuccess);

        var import = Step242Importer.ImportBody(text);

        Assert.True(import.IsSuccess);
        Assert.Single(import.Value.Topology.Faces);
        Assert.DoesNotContain(import.Diagnostics, d => d.Message.Contains("ADVANCED_FACE surface: expected entity reference argument.", StringComparison.Ordinal));
    }

    [Fact]
    public void Step242_AdvancedFace_Surface_AllowsEntityRefPlaneConstructor()
    {
        var text = LoadFixture("testdata/step242/syntax-robustness/advanced-face-ref-plane.step");

        var parseResult = Step242SubsetParser.Parse(text);
        Assert.True(parseResult.IsSuccess);

        var import = Step242Importer.ImportBody(text);

        Assert.True(import.IsSuccess);
        Assert.Single(import.Value.Topology.Faces);
    }

    [Fact]
    public void Step242_AdvancedFace_Surface_AllowsInlineCylindricalSurfaceConstructor()
    {
        var text = LoadFixture("testdata/step242/syntax-robustness/advanced-face-inline-cylinder.step");

        var parseResult = Step242SubsetParser.Parse(text);
        Assert.True(parseResult.IsSuccess);

        var import = Step242Importer.ImportBody(text);

        Assert.DoesNotContain(import.Diagnostics, d => d.Source is not null && d.Source.StartsWith("Parser", StringComparison.Ordinal));
        Assert.DoesNotContain(import.Diagnostics, d => string.Equals(d.Source, "Importer.StepSyntax.InlineEntity", StringComparison.Ordinal));

        if (!import.IsSuccess)
        {
            Assert.NotEmpty(import.Diagnostics);
            return;
        }

        Assert.Contains(import.Value.Geometry.Surfaces, s => s.Value.Kind == SurfaceGeometryKind.Cylinder);
    }

    [Fact]
    public void Step242_AdvancedFace_Surface_RejectsInlineCylinderMalformedArgs()
    {
        var text = LoadFixture("testdata/step242/syntax-robustness/advanced-face-inline-cylinder-malformed.step");

        var parseResult = Step242SubsetParser.Parse(text);
        Assert.True(parseResult.IsSuccess);

        var import = Step242Importer.ImportBody(text);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Importer.StepSyntax.InlineEntity", diagnostic.Source);
        Assert.StartsWith("Inline ADVANCED_FACE.surface constructor 'CYLINDRICAL_SURFACE' has unsupported argument shape.", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Step242_AdvancedFace_Surface_AllowsInlineConicalSurfaceConstructor()
    {
        var text = LoadFixture("testdata/step242/syntax-robustness/advanced-face-inline-cone-valid.step");

        var parseResult = Step242SubsetParser.Parse(text);
        Assert.True(parseResult.IsSuccess);

        var import = Step242Importer.ImportBody(text);

        Assert.DoesNotContain(import.Diagnostics, d => d.Source is not null && d.Source.StartsWith("Parser", StringComparison.Ordinal));
        Assert.DoesNotContain(import.Diagnostics, d => string.Equals(d.Source, "Importer.StepSyntax.InlineEntity", StringComparison.Ordinal));

        if (!import.IsSuccess)
        {
            Assert.NotEmpty(import.Diagnostics);
            return;
        }

        Assert.Contains(import.Value.Geometry.Surfaces, s => s.Value.Kind == SurfaceGeometryKind.Cone);
    }

    [Fact]
    public void Step242_AdvancedFace_Surface_RejectsInlineConicalSurface_MalformedArgs()
    {
        var text = LoadFixture("testdata/step242/syntax-robustness/advanced-face-inline-cone-malformed.step");

        var parseResult = Step242SubsetParser.Parse(text);
        Assert.True(parseResult.IsSuccess);

        var import = Step242Importer.ImportBody(text);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Importer.StepSyntax.InlineEntity", diagnostic.Source);
        Assert.StartsWith("Inline ADVANCED_FACE.surface constructor 'CONICAL_SURFACE' has unsupported argument shape.", diagnostic.Message, StringComparison.Ordinal);
    }


    [Fact]
    public void Step242_CurveSampler_Circle3_SamplesArcDeterministically()
    {
        var circle = new Circle3Curve(Point3D.Origin, Direction3D.Create(new Vector3D(0d, 0d, 1d)), 2d, Direction3D.Create(new Vector3D(1d, 0d, 0d)));

        var forward = CurveSampler.SampleCircleArc(circle, 0d, double.Pi / 2d);
        var second = CurveSampler.SampleCircleArc(circle, 0d, double.Pi / 2d);
        var reversed = CurveSampler.SampleCircleArc(circle, double.Pi / 2d, -double.Pi / 2d);

        Assert.Equal(forward.Count, second.Count);
        Assert.Equal(13, forward.Count);
        Assert.True(((forward[0] - circle.Evaluate(0d)).LengthSquared) < 1e-12d);
        Assert.True(((forward[^1] - circle.Evaluate(double.Pi / 2d)).LengthSquared) < 1e-12d);

        for (var i = 0; i < forward.Count; i++)
        {
            Assert.True(((forward[i] - second[i]).LengthSquared) < 1e-12d);
            Assert.True(((forward[i] - reversed[^(i + 1)]).LengthSquared) < 1e-12d);
        }
    }

    [Fact]
    public void Step242_0430200200_Import_NoCircle3LineOnlyError()
    {
        var text = LoadFixture("testdata/step242/tessellation-robustness/planar-rect-with-filleted-corners.step");

        var import = Step242Importer.ImportBody(text);
        Assert.True(import.IsSuccess);

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);
        Assert.True(tessellation.IsSuccess);

        Assert.DoesNotContain(import.Diagnostics, d => d.Message.Contains("Circle3", StringComparison.Ordinal));
        Assert.DoesNotContain(import.Diagnostics, d => d.Message.Contains("line edges only", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(tessellation.Diagnostics, d => d.Message.Contains("Circle3", StringComparison.Ordinal));
        Assert.DoesNotContain(tessellation.Diagnostics, d => d.Message.Contains("line edges only", StringComparison.OrdinalIgnoreCase));
    }

    private static string LoadFixture(string relativePath)
    {
        var path = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(path, Encoding.UTF8);
    }

}
