using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242OrientationSemanticsTests
{
    [Fact]
    public void ImportBody_EdgeCurveSameSenseFalse_ImportsAndFlipsCoedgeReversalState()
    {
        var forward = Step242Importer.ImportBody(BuildSingleTriangleStep(edgeCurveSameSense: true, faceBoundOrientation: true, advancedFaceSameSense: true));
        var reversed = Step242Importer.ImportBody(BuildSingleTriangleStep(edgeCurveSameSense: false, faceBoundOrientation: true, advancedFaceSameSense: true));

        Assert.True(forward.IsSuccess);
        Assert.True(reversed.IsSuccess);

        var forwardCoedge = Assert.Single(forward.Value.Topology.Coedges);
        var reversedCoedge = Assert.Single(reversed.Value.Topology.Coedges);
        Assert.NotEqual(forwardCoedge.IsReversed, reversedCoedge.IsReversed);
    }

    [Fact]
    public void ImportBody_FaceBoundOrientationFalse_FlipsCoedgeReversalState()
    {
        var forward = Step242Importer.ImportBody(BuildSingleTriangleStep(edgeCurveSameSense: true, faceBoundOrientation: true, advancedFaceSameSense: true));
        var reversed = Step242Importer.ImportBody(BuildSingleTriangleStep(edgeCurveSameSense: true, faceBoundOrientation: false, advancedFaceSameSense: true));

        Assert.True(forward.IsSuccess);
        Assert.True(reversed.IsSuccess);

        var forwardCoedge = Assert.Single(forward.Value.Topology.Coedges);
        var reversedCoedge = Assert.Single(reversed.Value.Topology.Coedges);
        Assert.NotEqual(forwardCoedge.IsReversed, reversedCoedge.IsReversed);
    }

    [Fact]
    public void ImportBody_AdvancedFaceSameSenseFalse_FlipsPlaneNormal()
    {
        var forward = Step242Importer.ImportBody(BuildSingleTriangleStep(edgeCurveSameSense: true, faceBoundOrientation: true, advancedFaceSameSense: true));
        var reversed = Step242Importer.ImportBody(BuildSingleTriangleStep(edgeCurveSameSense: true, faceBoundOrientation: true, advancedFaceSameSense: false));

        Assert.True(forward.IsSuccess);
        Assert.True(reversed.IsSuccess);

        var forwardFace = Assert.Single(forward.Value.Topology.Faces);
        var reversedFace = Assert.Single(reversed.Value.Topology.Faces);

        Assert.True(forward.Value.TryGetFaceSurfaceGeometry(forwardFace.Id, out var forwardSurface));
        Assert.True(reversed.Value.TryGetFaceSurfaceGeometry(reversedFace.Id, out var reversedSurface));

        Assert.Equal(1d, forwardSurface!.Plane!.Value.Normal.Z);
        Assert.Equal(-1d, reversedSurface!.Plane!.Value.Normal.Z);
    }

    [Fact]
    public void ImportBody_OrientationTypeMismatch_ReturnsDiagnosticsWithoutThrowing()
    {
        const string invalidOrientation = "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=MANIFOLD_SOLID_BREP('solid',#2);\n#2=CLOSED_SHELL($,(#3));\n#3=ADVANCED_FACE((#4),#5,.T.);\n#4=FACE_OUTER_BOUND($,#6,#7);\n#5=PLANE($,#20);\n#6=EDGE_LOOP($,(#8));\n#7=CARTESIAN_POINT($,(0,0,0));\n#8=ORIENTED_EDGE($,$,$,#9,.T.);\n#9=EDGE_CURVE($,#10,#11,#12,.T.);\n#10=VERTEX_POINT($,#13);\n#11=VERTEX_POINT($,#14);\n#12=LINE($,#13,#15);\n#13=CARTESIAN_POINT($,(0,0,0));\n#14=CARTESIAN_POINT($,(1,0,0));\n#15=VECTOR($,#16,1.0);\n#16=DIRECTION($,(1,0,0));\n#20=AXIS2_PLACEMENT_3D($,#13,#21,#22);\n#21=DIRECTION($,(0,0,1));\n#22=DIRECTION($,(1,0,0));\nENDSEC;\nEND-ISO-10303-21;";

        var import = Record.Exception(() => Step242Importer.ImportBody(invalidOrientation));

        Assert.Null(import);
        var result = Step242Importer.ImportBody(invalidOrientation);
        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Diagnostics);
    }

    private static string BuildSingleTriangleStep(bool edgeCurveSameSense, bool faceBoundOrientation, bool advancedFaceSameSense)
    {
        var edgeSense = edgeCurveSameSense ? ".T." : ".F.";
        var boundOrientation = faceBoundOrientation ? ".T." : ".F.";
        var faceSense = advancedFaceSameSense ? ".T." : ".F.";

        return $"ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=MANIFOLD_SOLID_BREP('solid',#2);\n#2=CLOSED_SHELL($,(#3));\n#3=ADVANCED_FACE((#4),#5,{faceSense});\n#4=FACE_OUTER_BOUND($,#6,{boundOrientation});\n#5=PLANE($,#20);\n#6=EDGE_LOOP($,(#7));\n#7=ORIENTED_EDGE($,$,$,#8,.T.);\n#8=EDGE_CURVE($,#9,#10,#11,{edgeSense});\n#9=VERTEX_POINT($,#12);\n#10=VERTEX_POINT($,#13);\n#11=LINE($,#12,#14);\n#12=CARTESIAN_POINT($,(0,0,0));\n#13=CARTESIAN_POINT($,(1,0,0));\n#14=VECTOR($,#15,1.0);\n#15=DIRECTION($,(1,0,0));\n#20=AXIS2_PLACEMENT_3D($,#12,#21,#22);\n#21=DIRECTION($,(0,0,1));\n#22=DIRECTION($,(1,0,0));\nENDSEC;\nEND-ISO-10303-21;";
    }
}
