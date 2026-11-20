using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

public sealed class SeamlessBandTessellationTests
{
    // A cylinder wall bounded by two full circles and no seam edge (how many CAD systems write hole walls and bosses).
    private const string SeamlessCylinderBand = "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=MANIFOLD_SOLID_BREP('solid',#2);\n#2=CLOSED_SHELL($,(#3));\n#3=ADVANCED_FACE((#4,#5),#6,.T.);\n#4=FACE_OUTER_BOUND($,#7,.T.);\n#5=FACE_BOUND($,#8,.T.);\n#6=CYLINDRICAL_SURFACE($,#9,5.0);\n#7=EDGE_LOOP($,(#10));\n#8=EDGE_LOOP($,(#11));\n#9=AXIS2_PLACEMENT_3D($,#12,#13,#14);\n#10=ORIENTED_EDGE($,$,$,#20,.T.);\n#11=ORIENTED_EDGE($,$,$,#21,.F.);\n#20=EDGE_CURVE($,#30,#30,#40,.T.);\n#21=EDGE_CURVE($,#31,#31,#41,.T.);\n#30=VERTEX_POINT($,#50);\n#31=VERTEX_POINT($,#51);\n#40=CIRCLE($,#9,5.0);\n#41=CIRCLE($,#15,5.0);\n#15=AXIS2_PLACEMENT_3D($,#16,#13,#14);\n#50=CARTESIAN_POINT($,(5,0,0));\n#51=CARTESIAN_POINT($,(5,0,3));\n#12=CARTESIAN_POINT($,(0,0,0));\n#13=DIRECTION($,(0,0,1));\n#14=DIRECTION($,(1,0,0));\n#16=CARTESIAN_POINT($,(0,0,3));\nENDSEC;\nEND-ISO-10303-21;";

    [Fact]
    public void CylinderBoundedByTwoFullCircles_IsTessellatedAsTheStripBetweenThem()
    {
        var body = Step242Importer.ImportBody(SeamlessCylinderBand);
        Assert.True(body.IsSuccess, string.Join(" | ", body.Diagnostics.Select(d => d.Message)));

        var result = BrepDisplayTessellator.Tessellate(body.Value);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Diagnostics, d => d.Source == "Viewer.Tessellation.TrimEvaluationFailed");
        var patch = Assert.Single(result.Value.FacePatches);
        Assert.NotEmpty(patch.TriangleIndices);
        Assert.Equal(2d * double.Pi * 5d * 3d, SurfaceArea(patch), 0);
    }

    [Fact]
    public void ConeBetweenCircleAndObliqueEllipse_KeepsTheWholeBand()
    {
        var body = Step242Importer.ImportBody(ObliqueCone(8d, ".F."));
        Assert.True(body.IsSuccess, string.Join(" | ", body.Diagnostics.Select(d => d.Message)));
        var face = Assert.Single(body.Value.Topology.Faces, face => body.Value.GetFaceSurface(face.Id).Kind == SurfaceGeometryKind.Cone);
        Assert.Equal(2, face.LoopIds.Count);

        var result = BrepDisplayTessellator.Tessellate(body.Value);
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        var patch = Assert.Single(result.Value.FacePatches, patch => patch.FaceId == face.Id);
        Assert.NotEmpty(patch.TriangleIndices);
        Assert.Equal(8d, patch.Positions.Max(point => point.Z), 6);
        Assert.InRange(patch.Positions.Min(point => point.Z), 2d - 1e-6d, 2.1d);
        Assert.All(patch.Positions, point =>
        {
            Assert.True(point.Z >= 3d + 0.5d * point.X - 0.05d);
            Assert.Equal(point.Z, double.Sqrt(point.X * point.X + point.Y * point.Y), 8);
        });
    }

    [Theory]
    [InlineData(4d, ".F.")]
    [InlineData(8d, ".T.")]
    public void ConeBetweenInvalidFullTurnBounds_IsRefused(double upperZ, string upperDirection)
    {
        var imported = Step242Importer.ImportBody(ObliqueCone(upperZ, upperDirection));

        Assert.False(imported.IsSuccess);
        Assert.Contains(imported.Diagnostics, d => d.Source == "Importer.LoopRole.ConeNonNormalizableDegenerateProjection");
    }

    // The cone has r=z. Its lower edge is the ellipse formed by z=3+x/2.
    private static string ObliqueCone(double upperZ, string upperDirection) => FormattableString.Invariant($"""
        ISO-10303-21;
        HEADER;
        ENDSEC;
        DATA;
        #1=MANIFOLD_SOLID_BREP('',#2);
        #2=CLOSED_SHELL('',(#3,#35,#36));
        #3=ADVANCED_FACE('',(#5,#4),#6,.T.);
        #4=FACE_BOUND('',#7,.T.);
        #5=FACE_BOUND('',#8,.T.);
        #6=CONICAL_SURFACE('',#9,0.,{double.Pi / 4d:R});
        #7=EDGE_LOOP('',(#10));
        #8=EDGE_LOOP('',(#11));
        #9=AXIS2_PLACEMENT_3D('',#12,#13,#14);
        #10=ORIENTED_EDGE('',*,*,#20,{upperDirection});
        #11=ORIENTED_EDGE('',*,*,#21,.T.);
        #12=CARTESIAN_POINT('',(0.,0.,0.));
        #13=DIRECTION('',(0.,0.,1.));
        #14=DIRECTION('',(1.,0.,0.));
        #15=AXIS2_PLACEMENT_3D('',#16,#13,#14);
        #16=CARTESIAN_POINT('',(0.,0.,{upperZ:R}));
        #17=AXIS2_PLACEMENT_3D('',#18,#19,#22);
        #18=CARTESIAN_POINT('',(2.,0.,4.));
        #19=DIRECTION('',(-1.,0.,2.));
        #20=EDGE_CURVE('',#30,#30,#40,.T.);
        #21=EDGE_CURVE('',#31,#31,#41,.T.);
        #22=DIRECTION('',(2.,0.,1.));
        #30=VERTEX_POINT('',#50);
        #31=VERTEX_POINT('',#51);
        #35=ADVANCED_FACE('',(#52),#54,.T.);
        #36=ADVANCED_FACE('',(#53),#55,.F.);
        #40=CIRCLE('',#15,{upperZ:R});
        #41=ELLIPSE('',#17,{double.Sqrt(20d):R},{double.Sqrt(12d):R});
        #50=CARTESIAN_POINT('',({upperZ:R},0.,{upperZ:R}));
        #51=CARTESIAN_POINT('',(6.,0.,6.));
        #52=FACE_OUTER_BOUND('',#56,.T.);
        #53=FACE_OUTER_BOUND('',#57,.T.);
        #54=PLANE('',#15);
        #55=PLANE('',#17);
        #56=EDGE_LOOP('',(#58));
        #57=EDGE_LOOP('',(#59));
        #58=ORIENTED_EDGE('',*,*,#20,.T.);
        #59=ORIENTED_EDGE('',*,*,#21,.F.);
        ENDSEC;
        END-ISO-10303-21;
        """);

    [Fact]
    public void EarCutFallback_TriangulatesPolygonWithHole_CounterClockwiseAboutTheNormal()
    {
        var plane = new PlaneSurface(Point3D.Origin, Direction3D.Create(new Vector3D(0d, 0d, 1d)), Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        var outer = new[] { new Point3D(0, 0, 0), new Point3D(10, 0, 0), new Point3D(10, 10, 0), new Point3D(0, 10, 0) };
        IReadOnlyList<Point3D> hole = [new Point3D(3, 3, 0), new Point3D(3, 7, 0), new Point3D(7, 7, 0), new Point3D(7, 3, 0)];

        Assert.True(BrepDisplayTessellator.TryEarCutPlanarLoops(plane, outer, [hole], out var points, out var indices));

        var signedArea = 0d;
        for (var i = 0; i + 2 < indices.Count; i += 3)
        {
            var a = points[indices[i]];
            var b = points[indices[i + 1]];
            var c = points[indices[i + 2]];
            var cross = ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));
            Assert.True(cross > 0d, "triangle is not counter-clockwise about +Z");
            signedArea += cross * 0.5d;
        }

        Assert.Equal(100d - 16d, signedArea, 9);
    }

    private static double SurfaceArea(DisplayFaceMeshPatch patch)
    {
        var area = 0d;
        for (var i = 0; i + 2 < patch.TriangleIndices.Count; i += 3)
        {
            var a = patch.Positions[patch.TriangleIndices[i]];
            var b = patch.Positions[patch.TriangleIndices[i + 1]];
            var c = patch.Positions[patch.TriangleIndices[i + 2]];
            area += (b - a).Cross(c - a).Length * 0.5d;
        }

        return area;
    }
}
