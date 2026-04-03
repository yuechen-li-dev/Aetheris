using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

public sealed class DisplayPreparationFallbackBuilderTests
{
    [Fact]
    public void Build_SingleFaceBsplineBody_UsesAcceptedScaffoldPatch()
    {
        var body = ImportRepresentativeBsplineBody();

        var result = DisplayPreparationFallbackBuilder.Build(body);

        Assert.True(result.IsSuccess);
        var patch = Assert.Single(result.Value.FacePatches);
        Assert.Equal(DisplayFaceMeshSource.BsplineUvScaffold, patch.Source);
        Assert.Null(patch.ScaffoldRejectionReason);
    }

    [Fact]
    public void Build_StrictScaffoldThreshold_RejectedAndFallsBackToTessellator()
    {
        var body = ImportRepresentativeBsplineBody();

        var result = DisplayPreparationFallbackBuilder.Build(
            body,
            tessellationOptions: null,
            scaffoldOptions: new DisplayPreparationBsplineScaffoldOptions(
                USegments: 12,
                VSegments: 12,
                Acceptance: new BsplineUvGridScaffoldAcceptanceThresholds(
                    MaxTriangleDensityRatioVsFallback: 0.01d)));

        Assert.True(result.IsSuccess);
        var patch = Assert.Single(result.Value.FacePatches);
        Assert.Equal(DisplayFaceMeshSource.Tessellator, patch.Source);
        Assert.Equal(BsplineUvGridScaffoldRejectionReason.TooDenseVsFallback.ToString(), patch.ScaffoldRejectionReason);
    }

    [Fact]
    public void Build_UnsupportedBody_StaysOnExistingFallbackWithoutScaffoldAttempt()
    {
        var body = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;

        var result = DisplayPreparationFallbackBuilder.Build(body);

        Assert.True(result.IsSuccess);
        Assert.All(result.Value.FacePatches, patch =>
        {
            Assert.Equal(DisplayFaceMeshSource.Tessellator, patch.Source);
            Assert.Null(patch.ScaffoldRejectionReason);
        });
    }

    [Fact]
    public void Build_AcceptedScaffoldCase_IsDeterministic()
    {
        var body = ImportRepresentativeBsplineBody();

        var first = DisplayPreparationFallbackBuilder.Build(body);
        var second = DisplayPreparationFallbackBuilder.Build(body);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(CreateSignature(first.Value), CreateSignature(second.Value));
    }

    private static string CreateSignature(DisplayTessellationResult result)
        => string.Join(
            "|",
            result.FacePatches.Select(patch =>
                $"{patch.FaceId.Value}:{patch.Source}:{patch.ScaffoldRejectionReason}:{patch.Positions.Count}:{patch.TriangleIndices.Count}:{string.Join(',', patch.TriangleIndices.Take(24))}"));

    private static BrepBody ImportRepresentativeBsplineBody()
    {
        const string text = "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=MANIFOLD_SOLID_BREP('s',#2);\n#2=CLOSED_SHELL($,(#3));\n#3=ADVANCED_FACE((#4),#5,.T.);\n#4=FACE_OUTER_BOUND($,#6,.T.);\n#5=B_SPLINE_SURFACE_WITH_KNOTS('',3,3,((#100,#101,#102,#103),(#104,#105,#106,#107),(#108,#109,#110,#111),(#112,#113,#114,#115)),.UNSPECIFIED.,.F.,.F.,.F.,(4,4),(4,4),(0.,1.),(0.,1.),.UNSPECIFIED.);\n#6=EDGE_LOOP($,(#10,#11,#12,#13));\n#10=ORIENTED_EDGE($,$,$,#20,.T.);\n#11=ORIENTED_EDGE($,$,$,#21,.T.);\n#12=ORIENTED_EDGE($,$,$,#22,.T.);\n#13=ORIENTED_EDGE($,$,$,#23,.T.);\n#20=EDGE_CURVE($,#30,#31,#40,.T.);\n#21=EDGE_CURVE($,#31,#32,#41,.T.);\n#22=EDGE_CURVE($,#32,#33,#42,.T.);\n#23=EDGE_CURVE($,#33,#30,#43,.T.);\n#30=VERTEX_POINT($,#50);\n#31=VERTEX_POINT($,#51);\n#32=VERTEX_POINT($,#52);\n#33=VERTEX_POINT($,#53);\n#40=LINE($,#50,#80);\n#41=LINE($,#51,#81);\n#42=LINE($,#52,#82);\n#43=LINE($,#53,#83);\n#50=CARTESIAN_POINT($,(0,0,0));\n#51=CARTESIAN_POINT($,(1,0,0));\n#52=CARTESIAN_POINT($,(1,1,0));\n#53=CARTESIAN_POINT($,(0,1,0));\n#80=VECTOR($,#84,1.0);\n#81=VECTOR($,#85,1.0);\n#82=VECTOR($,#86,1.0);\n#83=VECTOR($,#87,1.0);\n#84=DIRECTION($,(1,0,0));\n#85=DIRECTION($,(0,1,0));\n#86=DIRECTION($,(-1,0,0));\n#87=DIRECTION($,(0,-1,0));\n#100=CARTESIAN_POINT($,(0,0,0));\n#101=CARTESIAN_POINT($,(0,0.33,0));\n#102=CARTESIAN_POINT($,(0,0.66,0));\n#103=CARTESIAN_POINT($,(0,1,0));\n#104=CARTESIAN_POINT($,(0.33,0,0));\n#105=CARTESIAN_POINT($,(0.33,0.33,0));\n#106=CARTESIAN_POINT($,(0.33,0.66,0));\n#107=CARTESIAN_POINT($,(0.33,1,0));\n#108=CARTESIAN_POINT($,(0.66,0,0));\n#109=CARTESIAN_POINT($,(0.66,0.33,0));\n#110=CARTESIAN_POINT($,(0.66,0.66,0));\n#111=CARTESIAN_POINT($,(0.66,1,0));\n#112=CARTESIAN_POINT($,(1,0,0));\n#113=CARTESIAN_POINT($,(1,0.33,0));\n#114=CARTESIAN_POINT($,(1,0.66,0));\n#115=CARTESIAN_POINT($,(1,1,0));\nENDSEC;\nEND-ISO-10303-21;";
        var import = Step242Importer.ImportBody(text);
        Assert.True(import.IsSuccess);
        return import.Value;
    }
}
