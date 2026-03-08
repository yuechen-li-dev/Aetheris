using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Picking;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242Tier1GeometryTests
{
    [Fact]
    public void ImportBody_CircleEdgeSubset_ImportsValidatesAndTessellates()
    {
        var import = Step242Importer.ImportBody(BuildCylinderSideFaceStepText());

        Assert.True(import.IsSuccess);
        var validation = BrepBindingValidator.Validate(import.Value, requireAllEdgeAndFaceBindings: true);
        Assert.True(validation.IsSuccess);

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);
        Assert.True(tessellation.IsSuccess);

        var ray = new Ray3D(new Point3D(0d, 1.5d, 0.5d), Direction3D.Create(new Vector3D(0d, -1d, 0d)));
        var pick = BrepPicker.Pick(import.Value, tessellation.Value, ray, PickQueryOptions.Default with { NearestOnly = true });
        Assert.True(pick.IsSuccess);
        Assert.NotEmpty(pick.Value);
    }

    [Fact]
    public void ImportBody_CircleFullLoopTrim_ProducesFullCircleInterval()
    {
        var import = Step242Importer.ImportBody(BuildSphereFaceStepText());

        Assert.True(import.IsSuccess);
        Assert.True(import.Value.Bindings.TryGetEdgeBinding(new Aetheris.Kernel.Core.Topology.EdgeId(2), out var trim));
        Assert.NotNull(trim.TrimInterval);
        Assert.Equal(0d, trim.TrimInterval!.Value.Start, 8);
        Assert.Equal(2d * double.Pi, trim.TrimInterval.Value.End, 8);
    }

    [Fact]
    public void ImportBody_CircleTrimProjectionFailure_ReturnsDeterministicDiagnostic()
    {
        var import = Step242Importer.ImportBody(BuildInvalidCircleTrimStepText());

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.ValidationFailed, diagnostic.Code);
        Assert.StartsWith("Importer.Geometry.CircleTrim", diagnostic.Source, StringComparison.Ordinal);
        Assert.StartsWith("Unable to project circular trim point", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportBody_CylindricalSurfaceFace_ImportsAndTessellates()
    {
        var import = Step242Importer.ImportBody(BuildCylinderSideFaceStepText());

        Assert.True(import.IsSuccess);
        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);
        Assert.True(tessellation.IsSuccess);
        Assert.NotEmpty(tessellation.Value.FacePatches);
    }

    [Fact]
    public void ImportBody_ConicalSurfaceFace_ImportsAndTessellates()
    {
        var import = Step242Importer.ImportBody(BuildConeSideFaceStepText());

        Assert.True(import.IsSuccess);
        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);
        Assert.True(tessellation.IsSuccess);
        Assert.NotEmpty(tessellation.Value.FacePatches);
    }

    [Fact]
    public void ImportBody_SphericalSurfaceFace_ImportsAndPickSucceeds()
    {
        var import = Step242Importer.ImportBody(BuildSphereOnlyStepText());

        Assert.True(import.IsSuccess);
        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);
        Assert.True(tessellation.IsSuccess);

        var ray = new Ray3D(new Point3D(0d, 0d, 3d), Direction3D.Create(new Vector3D(0d, 0d, -1d)));
        var pick = BrepPicker.Pick(import.Value, tessellation.Value, ray, PickQueryOptions.Default with { NearestOnly = true });
        Assert.True(pick.IsSuccess);
        Assert.NotEmpty(pick.Value);
    }

    private static string BuildCylinderSideFaceStepText() =>
        "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=MANIFOLD_SOLID_BREP('solid',#2);\n#2=CLOSED_SHELL($,(#3));\n#3=ADVANCED_FACE((#4),#5,.T.);\n#4=FACE_OUTER_BOUND($,#6,.T.);\n#5=CYLINDRICAL_SURFACE($,#30,1.0);\n#6=EDGE_LOOP($,(#7,#8,#9,#10));\n#7=ORIENTED_EDGE($,$,$,#11,.T.);\n#8=ORIENTED_EDGE($,$,$,#12,.T.);\n#9=ORIENTED_EDGE($,$,$,#13,.T.);\n#10=ORIENTED_EDGE($,$,$,#14,.T.);\n#11=EDGE_CURVE($,#15,#16,#17,.T.);\n#12=EDGE_CURVE($,#16,#16,#18,.T.);\n#13=EDGE_CURVE($,#16,#15,#19,.T.);\n#14=EDGE_CURVE($,#15,#15,#20,.T.);\n#15=VERTEX_POINT($,#21);\n#16=VERTEX_POINT($,#22);\n#17=LINE($,#21,#23);\n#18=CIRCLE($,#31,1.0);\n#19=LINE($,#22,#24);\n#20=CIRCLE($,#30,1.0);\n#21=CARTESIAN_POINT($,(1,0,0));\n#22=CARTESIAN_POINT($,(1,0,1));\n#23=VECTOR($,#25,1.0);\n#24=VECTOR($,#26,1.0);\n#25=DIRECTION($,(0,0,1));\n#26=DIRECTION($,(0,0,-1));\n#30=AXIS2_PLACEMENT_3D($,#27,#28,#29);\n#31=AXIS2_PLACEMENT_3D($,#32,#28,#29);\n#32=CARTESIAN_POINT($,(0,0,1));\n#27=CARTESIAN_POINT($,(0,0,0));\n#28=DIRECTION($,(0,0,1));\n#29=DIRECTION($,(1,0,0));\nENDSEC;\nEND-ISO-10303-21;";

    private static string BuildConeSideFaceStepText() =>
        "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=MANIFOLD_SOLID_BREP('solid',#2);\n#2=CLOSED_SHELL($,(#3));\n#3=ADVANCED_FACE((#4),#5,.T.);\n#4=FACE_OUTER_BOUND($,#6,.T.);\n#5=CONICAL_SURFACE($,#30,1.0,0.7853981633974483);\n#6=EDGE_LOOP($,(#7,#8,#9,#10));\n#7=ORIENTED_EDGE($,$,$,#11,.T.);\n#8=ORIENTED_EDGE($,$,$,#12,.T.);\n#9=ORIENTED_EDGE($,$,$,#13,.T.);\n#10=ORIENTED_EDGE($,$,$,#14,.T.);\n#11=EDGE_CURVE($,#15,#16,#17,.T.);\n#12=EDGE_CURVE($,#16,#16,#18,.T.);\n#13=EDGE_CURVE($,#16,#15,#19,.T.);\n#14=EDGE_CURVE($,#15,#15,#20,.T.);\n#15=VERTEX_POINT($,#21);\n#16=VERTEX_POINT($,#22);\n#17=LINE($,#21,#23);\n#18=CIRCLE($,#31,2.0);\n#19=LINE($,#22,#24);\n#20=CIRCLE($,#30,1.0);\n#21=CARTESIAN_POINT($,(1,0,1));\n#22=CARTESIAN_POINT($,(2,0,2));\n#23=VECTOR($,#25,1.0);\n#24=VECTOR($,#26,1.0);\n#25=DIRECTION($,(1,0,1));\n#26=DIRECTION($,(-1,0,-1));\n#30=AXIS2_PLACEMENT_3D($,#27,#28,#29);\n#31=AXIS2_PLACEMENT_3D($,#32,#28,#29);\n#32=CARTESIAN_POINT($,(0,0,2));\n#27=CARTESIAN_POINT($,(0,0,1));\n#28=DIRECTION($,(0,0,1));\n#29=DIRECTION($,(1,0,0));\nENDSEC;\nEND-ISO-10303-21;";

    private static string BuildSphereOnlyStepText() =>
        "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=MANIFOLD_SOLID_BREP('solid',#2);\n#2=CLOSED_SHELL($,(#3));\n#3=ADVANCED_FACE((),#4,.T.);\n#4=SPHERICAL_SURFACE($,#10,1.0);\n#10=AXIS2_PLACEMENT_3D($,#11,#12,#13);\n#11=CARTESIAN_POINT($,(0,0,0));\n#12=DIRECTION($,(0,0,1));\n#13=DIRECTION($,(1,0,0));\nENDSEC;\nEND-ISO-10303-21;";

    private static string BuildSphereFaceStepText() => BuildCylinderSideFaceStepText();

    private static string BuildInvalidCircleTrimStepText() =>
        "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=MANIFOLD_SOLID_BREP('solid',#2);\n#2=CLOSED_SHELL($,(#3));\n#3=ADVANCED_FACE((#4),#5,.T.);\n#4=FACE_OUTER_BOUND($,#6,.T.);\n#5=PLANE($,#30);\n#6=EDGE_LOOP($,(#7));\n#7=ORIENTED_EDGE($,$,$,#8,.T.);\n#8=EDGE_CURVE($,#9,#10,#11,.T.);\n#9=VERTEX_POINT($,#12);\n#10=VERTEX_POINT($,#13);\n#11=CIRCLE($,#31,1.0);\n#12=CARTESIAN_POINT($,(2,0,0));\n#13=CARTESIAN_POINT($,(0,2,0));\n#30=AXIS2_PLACEMENT_3D($,#14,#15,#16);\n#31=AXIS2_PLACEMENT_3D($,#14,#15,#16);\n#14=CARTESIAN_POINT($,(0,0,0));\n#15=DIRECTION($,(0,0,1));\n#16=DIRECTION($,(1,0,0));\nENDSEC;\nEND-ISO-10303-21;";
}
