using System.Text;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242TessellationRobustnessTests
{
    [Fact]
    public void Step242_Tessellate_PlanarFace_WithHole_PreservesHoleDeterministically()
    {
        var text = LoadFixture("testdata/step242/tessellation-robustness/planar-face-with-hole-outer-and-inner.step");

        var firstImport = Step242Importer.ImportBody(text);
        var secondImport = Step242Importer.ImportBody(text);

        Assert.True(firstImport.IsSuccess);
        Assert.True(secondImport.IsSuccess);

        var first = BrepDisplayTessellator.Tessellate(firstImport.Value);
        var second = BrepDisplayTessellator.Tessellate(secondImport.Value);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.DoesNotContain(first.Diagnostics, d => d.Message.Contains("requires exactly one loop", StringComparison.Ordinal));
        Assert.DoesNotContain(first.Diagnostics, d => string.Equals(d.Source, "Viewer.Tessellation.PlanarHolesIgnored", StringComparison.Ordinal));

        var firstPatch = Assert.Single(first.Value.FacePatches);
        var secondPatch = Assert.Single(second.Value.FacePatches);

        Assert.NotEmpty(firstPatch.TriangleIndices);
        Assert.Equal(firstPatch.Positions, secondPatch.Positions);
        Assert.Equal(firstPatch.TriangleIndices, secondPatch.TriangleIndices);
        Assert.All(GetTriangleCentroids(firstPatch), centroid =>
            Assert.False(IsInsideAxisAlignedRectangle(centroid, minX: 3d, minY: 3d, maxX: 7d, maxY: 7d)));
    }

    [Fact]
    public void Step242_Tessellate_BoxTopCapWithCircularHole_PreservesHoleDeterministically()
    {
        var text = LoadFixture("testdata/firmament/exports/boolean_box_cylinder_hole.step");

        var firstImport = Step242Importer.ImportBody(text);
        var secondImport = Step242Importer.ImportBody(text);

        Assert.True(firstImport.IsSuccess);
        Assert.True(secondImport.IsSuccess);

        var first = BrepDisplayTessellator.Tessellate(firstImport.Value);
        var second = BrepDisplayTessellator.Tessellate(secondImport.Value);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        var firstPatch = GetPlanarCapPatch(first.Value, zValue: 6d);
        var secondPatch = GetPlanarCapPatch(second.Value, zValue: 6d);

        Assert.Equal(firstPatch.Positions, secondPatch.Positions);
        Assert.Equal(firstPatch.TriangleIndices, secondPatch.TriangleIndices);
        Assert.All(GetTriangleCentroids(firstPatch), centroid =>
            Assert.True(((centroid.X * centroid.X) + (centroid.Y * centroid.Y)) >= (4d * 4d) - 1e-6d));
    }

    [Fact]
    public void Step242_Tessellate_BoxPlanarFaceWithoutHole_RemainsValid()
    {
        var text = LoadFixture("testdata/firmament/exports/box_basic.step");

        var import = Step242Importer.ImportBody(text);

        Assert.True(import.IsSuccess);

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);

        Assert.True(tessellation.IsSuccess);
        Assert.Equal(6, tessellation.Value.FacePatches.Count);
        Assert.All(tessellation.Value.FacePatches, patch => Assert.NotEmpty(patch.TriangleIndices));
    }

    [Fact]
    public void Step242_Tessellate_UnsupportedComplexPlanarMultiLoop_DoesNotFallbackToOuterLoopFill()
    {
        var text = LoadFixture("testdata/step242/nist/CTC/nist_ctc_02_asme1_ap242-e2.stp");

        var import = Step242Importer.ImportBody(text);

        Assert.True(import.IsSuccess);

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);

        Assert.True(tessellation.IsSuccess);

        var diagnostics = tessellation.Diagnostics
            .Where(d => string.Equals(d.Source, "Viewer.Tessellation.PlanarMultiLoopTriangulationSkipped", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(diagnostics);
        var diagnostic = Assert.Single(diagnostics, d => d.Message.Contains("Face 2 ", StringComparison.Ordinal));
        Assert.Equal(KernelDiagnosticCode.ValidationFailed, diagnostic.Code);
        Assert.Contains("skipping face patch", diagnostic.Message, StringComparison.Ordinal);

        var skippedPatch = Assert.Single(tessellation.Value.FacePatches, patch => patch.FaceId.Value == 2);
        Assert.Empty(skippedPatch.Positions);
        Assert.Empty(skippedPatch.TriangleIndices);
    }

    [Fact]
    public void Step242_Tessellate_PlanarFace_NonConvexSingleLoop_SucceedsAndProducesTriangles()
    {
        var text = LoadFixture("testdata/step242/tessellation-robustness/planar-nonconvex-single-loop.step");

        var import = Step242Importer.ImportBody(text);

        Assert.True(import.IsSuccess);

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);

        Assert.True(tessellation.IsSuccess);

        var facePatch = Assert.Single(tessellation.Value.FacePatches);
        Assert.True(facePatch.TriangleIndices.Count > 0);
        Assert.DoesNotContain(tessellation.Diagnostics, d => d.Message.Contains("requires a convex polygon", StringComparison.Ordinal));
    }


    [Fact]
    public void Step242_Tessellate_PlanarFace_LinePlusArcLoop_Succeeds()
    {
        var text = LoadFixture("testdata/step242/tessellation-robustness/planar-rect-with-filleted-corners.step");

        var import = Step242Importer.ImportBody(text);

        Assert.True(import.IsSuccess);

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);

        Assert.True(tessellation.IsSuccess);
        Assert.DoesNotContain(tessellation.Diagnostics, d => d.Message.Contains("supports only all-line loops or a single circle loop", StringComparison.Ordinal));

        var facePatch = Assert.Single(tessellation.Value.FacePatches);
        Assert.NotEmpty(facePatch.TriangleIndices);
    }

    [Fact]
    public void Step242_Tessellate_PlanarFace_LinePlusBSplineLoop_SucceedsDeterministically()
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

        var firstImport = Step242Importer.ImportBody(text);
        var secondImport = Step242Importer.ImportBody(text);

        Assert.True(firstImport.IsSuccess);
        Assert.True(secondImport.IsSuccess);

        var first = BrepDisplayTessellator.Tessellate(firstImport.Value);
        var second = BrepDisplayTessellator.Tessellate(secondImport.Value);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        var firstPatch = Assert.Single(first.Value.FacePatches);
        var secondPatch = Assert.Single(second.Value.FacePatches);

        Assert.NotEmpty(firstPatch.TriangleIndices);
        Assert.Equal(firstPatch.Positions, secondPatch.Positions);
        Assert.Equal(firstPatch.TriangleIndices, secondPatch.TriangleIndices);
    }

    [Fact]
    public void Step242_Tessellate_PlanarFace_UnsupportedCurve_FailsDeterministically()
    {
        var text = LoadFixture("testdata/step242/tessellation-robustness/planar-loop-unsupported-curve.step");

        var import = Step242Importer.ImportBody(text);

        Assert.True(import.IsSuccess);

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);

        Assert.False(tessellation.IsSuccess);

        var diagnostic = Assert.Single(tessellation.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Viewer.Tessellation.PlanarCurveFlatteningUnsupported", diagnostic.Source);
        Assert.Contains("AETHERIS_PLANAR_UNSUPPORTED_CURVE", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Step242_Tessellate_PlanarFace_DegenerateLoop_FailsDeterministically()
    {
        var text = LoadFixture("testdata/step242/tessellation-robustness/planar-degenerate-loop.step");

        var import = Step242Importer.ImportBody(text);

        Assert.True(import.IsSuccess);

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);

        Assert.False(tessellation.IsSuccess);

        var diagnostic = Assert.Single(tessellation.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.InvalidArgument, diagnostic.Code);
        Assert.Equal("Viewer.Tessellation.PlanarPolygonDegenerate", diagnostic.Source);
    }

    [Fact]
    public void Step242_BlockEdgeFillet_CylindricalFace_TessellatesAsBoundedPatch()
    {
        var text = LoadFixture("testdata/step242/handcrafted/edge-trimming/block-edge-fillet.step");

        var import = Step242Importer.ImportBody(text);

        Assert.True(import.IsSuccess);

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);

        Assert.True(tessellation.IsSuccess);
        Assert.DoesNotContain(tessellation.Diagnostics, d => string.Equals(d.Source, "Viewer.Tessellation.CylinderTrimUnsupported", StringComparison.Ordinal));

        var cylinderFacePatches = GetCylinderFacePatches(import.Value, tessellation.Value);
        Assert.NotEmpty(cylinderFacePatches);

        var boundedPatch = cylinderFacePatches
            .Select(p => ComputeAngularSpan(p.Surface, p.Patch.Positions))
            .FirstOrDefault(span => span < (2d * double.Pi * 0.95d));

        Assert.True(boundedPatch > 0d);
        Assert.True(boundedPatch < (2d * double.Pi * 0.95d));
        Assert.All(cylinderFacePatches, p => Assert.NotEmpty(p.Patch.TriangleIndices));
    }

    [Fact]
    public void Step242_BlockFullRound_CylindricalFace_TessellatesAsBoundedPatch()
    {
        var text = LoadFixture("testdata/step242/handcrafted/edge-trimming/block-full-round.step");

        var import = Step242Importer.ImportBody(text);

        if (!import.IsSuccess)
        {
            var diagnostic = Assert.Single(import.Diagnostics);
            Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
            Assert.Equal("Importer.EntityFamily", diagnostic.Source);
            Assert.Contains("BOUNDED_CURVE", diagnostic.Message, StringComparison.Ordinal);
            return;
        }

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);

        if (!tessellation.IsSuccess)
        {
            var diagnostic = Assert.Single(tessellation.Diagnostics);
            Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
            Assert.Equal("Viewer.Tessellation.CylinderTrimUnsupported", diagnostic.Source);
            return;
        }

        Assert.DoesNotContain(tessellation.Diagnostics, d => string.Equals(d.Source, "Viewer.Tessellation.CylinderTrimUnsupported", StringComparison.Ordinal));
        Assert.DoesNotContain(tessellation.Diagnostics, d => string.Equals(d.Source, "Viewer.Tessellation.CylinderTrimDegenerate", StringComparison.Ordinal));

        var cylinderFacePatches = GetCylinderFacePatches(import.Value, tessellation.Value);
        Assert.NotEmpty(cylinderFacePatches);

        var spans = cylinderFacePatches.Select(p => ComputeAngularSpan(p.Surface, p.Patch.Positions)).ToArray();
        Assert.Contains(spans, span => span > 0d && span < (2d * double.Pi * 0.99d));
    }

    private static IReadOnlyList<(CylinderSurface Surface, DisplayFaceMeshPatch Patch)> GetCylinderFacePatches(
        Core.Brep.BrepBody body,
        DisplayTessellationResult tessellation)
    {
        var patches = new List<(CylinderSurface Surface, DisplayFaceMeshPatch Patch)>();
        foreach (var patch in tessellation.FacePatches)
        {
            if (!body.TryGetFaceSurfaceGeometry(patch.FaceId, out var surface) || surface?.Kind != Core.Geometry.SurfaceGeometryKind.Cylinder)
            {
                continue;
            }

            patches.Add((surface.Cylinder!.Value, patch));
        }

        return patches;
    }

    private static DisplayFaceMeshPatch GetPlanarCapPatch(DisplayTessellationResult tessellation, double zValue)
    {
        return Assert.Single(tessellation.FacePatches, patch =>
            patch.Positions.Count > 0
            && patch.Positions.All(point => double.Abs(point.Z - zValue) <= 1e-6d));
    }

    private static IEnumerable<Point3D> GetTriangleCentroids(DisplayFaceMeshPatch patch)
    {
        for (var i = 0; i < patch.TriangleIndices.Count; i += 3)
        {
            var p0 = patch.Positions[patch.TriangleIndices[i]];
            var p1 = patch.Positions[patch.TriangleIndices[i + 1]];
            var p2 = patch.Positions[patch.TriangleIndices[i + 2]];
            yield return new Point3D(
                (p0.X + p1.X + p2.X) / 3d,
                (p0.Y + p1.Y + p2.Y) / 3d,
                (p0.Z + p1.Z + p2.Z) / 3d);
        }
    }

    private static bool IsInsideAxisAlignedRectangle(Point3D point, double minX, double minY, double maxX, double maxY)
        => point.X > minX && point.X < maxX && point.Y > minY && point.Y < maxY;

    private static double ComputeAngularSpan(CylinderSurface cylinder, IReadOnlyList<Point3D> positions)
    {
        var axis = cylinder.Axis.ToVector();
        var xAxis = cylinder.XAxis.ToVector();
        var yAxis = cylinder.YAxis.ToVector();
        var angles = positions
            .Select(point =>
            {
                var offset = point - cylinder.Origin;
                var radial = offset - (axis * offset.Dot(axis));
                return NormalizeToZeroTwoPi(double.Atan2(radial.Dot(yAxis), radial.Dot(xAxis)));
            })
            .OrderBy(a => a)
            .ToArray();

        var maxGap = 0d;
        for (var i = 0; i < angles.Length; i++)
        {
            var current = angles[i];
            var next = i == angles.Length - 1 ? angles[0] + (2d * double.Pi) : angles[i + 1];
            maxGap = System.Math.Max(maxGap, next - current);
        }

        return (2d * double.Pi) - maxGap;
    }

    private static double NormalizeToZeroTwoPi(double angle)
    {
        var twoPi = 2d * double.Pi;
        var normalized = angle % twoPi;
        if (normalized < 0d)
        {
            normalized += twoPi;
        }

        return normalized;
    }

    private static string LoadFixture(string relativePath)
    {
        var path = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(path, Encoding.UTF8);
    }
}
