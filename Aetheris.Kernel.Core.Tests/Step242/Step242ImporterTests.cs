using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242ImporterTests
{
    [Fact]
    public void ImportBody_KnownGoodM22SubsetText_ReturnsValidatedBody()
    {
        var fixtureText = CreateM22BoxFixtureText();

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
        var malformed = "ISO-10303-21; DATA; #1=MANIFOLD_SOLID_BREP('bad',#2) ENDSEC; END-ISO-10303-21;";

        var import = Step242Importer.ImportBody(malformed);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.InvalidArgument, diagnostic.Code);
        Assert.Equal(KernelDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Parser", diagnostic.Source);
    }

    [Fact]
    public void ImportBody_EdgeCurveSameSenseFalse_ReturnsNotImplementedDiagnostic()
    {
        var stepText = """
ISO-10303-21;
HEADER;
ENDSEC;
DATA;
#1=CARTESIAN_POINT($,(0,0,0));
#2=VERTEX_POINT($,#1);
#3=CARTESIAN_POINT($,(1,0,0));
#4=VERTEX_POINT($,#3);
#5=CARTESIAN_POINT($,(0,0,0));
#6=DIRECTION($,(1,0,0));
#7=VECTOR($,#6,1);
#8=LINE($,#5,#7);
#9=EDGE_CURVE($,#2,#4,#8,.F.);
#10=ORIENTED_EDGE($,$,$,#9,.T.);
#11=EDGE_LOOP($,(#10));
#12=FACE_OUTER_BOUND($,#11,.T.);
#13=CARTESIAN_POINT($,(0,0,0));
#14=DIRECTION($,(0,0,1));
#15=DIRECTION($,(1,0,0));
#16=AXIS2_PLACEMENT_3D($,#13,#14,#15);
#17=PLANE($,#16);
#18=ADVANCED_FACE((#12),#17,.T.);
#19=CLOSED_SHELL($,(#18));
#20=MANIFOLD_SOLID_BREP('x',#19);
ENDSEC;
END-ISO-10303-21;
""";

        var import = Step242Importer.ImportBody(stepText);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Entity:9", diagnostic.Source);
        Assert.Contains("same_sense=.F.", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportBody_MultipleBrepRoots_ReturnsDeterministicDiagnostic()
    {
        var fixture = CreateM22BoxFixtureText();
        var duplicateRoot = fixture.Replace(
            "ENDSEC;\nEND-ISO-10303-21;",
            "#999=MANIFOLD_SOLID_BREP('other',#19);\nENDSEC;\nEND-ISO-10303-21;",
            StringComparison.Ordinal);

        var import = Step242Importer.ImportBody(duplicateRoot);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.ValidationFailed, diagnostic.Code);
        Assert.Equal(KernelDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Step242Importer.ValidateRoots", diagnostic.Source);
        Assert.Contains("multiple B-rep roots", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#999", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportBody_UnsupportedEntityInParseableText_ReturnsNotImplementedDiagnostic()
    {
        var unsupported = "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=SPHERICAL_SURFACE($,#2,1.0);\n#2=AXIS2_PLACEMENT_3D($,#3,#4,#5);\n#3=CARTESIAN_POINT($,(0,0,0));\n#4=DIRECTION($,(0,0,1));\n#5=DIRECTION($,(1,0,0));\nENDSEC;\nEND-ISO-10303-21;";

        var import = Step242Importer.ImportBody(unsupported);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal(KernelDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Entity:1", diagnostic.Source);
        Assert.Contains("unsupported", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateM22BoxFixtureText()
    {
        var boxResult = BrepPrimitives.CreateBox(2d, 2d, 2d);
        Assert.True(boxResult.IsSuccess);

        var export = Step242Exporter.ExportBody(boxResult.Value);
        Assert.True(export.IsSuccess);
        return export.Value;
    }
}
