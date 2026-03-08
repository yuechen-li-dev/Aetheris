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
    public void Step242_Tessellate_PlanarFace_AllowsMultipleLoops_UsesOuterOnly()
    {
        var text = LoadFixture("testdata/step242/tessellation-robustness/planar-face-with-hole-outer-and-inner.step");

        var import = Step242Importer.ImportBody(text);

        Assert.True(import.IsSuccess);

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);

        Assert.True(tessellation.IsSuccess);
        Assert.DoesNotContain(tessellation.Diagnostics, d => d.Message.Contains("requires exactly one loop", StringComparison.Ordinal));

        var facePatch = Assert.Single(tessellation.Value.FacePatches);
        Assert.NotEmpty(facePatch.TriangleIndices);

        var warning = tessellation.Diagnostics.SingleOrDefault(d => string.Equals(d.Source, "Viewer.Tessellation.PlanarHolesIgnored", StringComparison.Ordinal));
        if (warning is not null)
        {
            Assert.Contains("ignored", warning.Message, StringComparison.Ordinal);
        }
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
        Assert.Contains(spans, span => span > double.Pi && span < (2d * double.Pi * 0.99d));
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
