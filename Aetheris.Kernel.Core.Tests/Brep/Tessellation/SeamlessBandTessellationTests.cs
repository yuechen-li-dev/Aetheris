using Aetheris.Kernel.Core.Brep.Tessellation;
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
