using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Lowering;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentCirDifferentialAnalysisTests
{
    [Fact]
    public void CIRvsBRep_BoxMinusCylinder_VolumeComparison()
    {
        var fixture = "testdata/firmament/examples/boolean_box_cylinder_hole.firmament";
        var compile = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText(fixture));
        var cir = FirmamentCirLowerer.Lower(compile.Compilation.Value.PrimitiveLoweringPlan!);
        Assert.True(cir.IsSuccess);

        var brep = compile.Compilation.Value.PrimitiveExecutionResult!.ExecutedBooleans.Single(b => b.FeatureId == "hole").Body;
        var cirVolume = CirAnalyzer.EstimateVolume(cir.Value.Root, 72);
        var brepVolume = EstimateBrepVolume(brep, 72);
        Assert.True(brepVolume.HasValue && brepVolume.Value > 1e-9d);
        Assert.InRange(Math.Abs(cirVolume - brepVolume.Value) / brepVolume.Value, 0d, 0.08d);
    }

    [Fact]
    public void CIRvsBRep_SemanticPlacementFixture_Comparison_Passes()
    {
        var fixture = "testdata/firmament/examples/w2_cylinder_root_blind_bore_semantic.firmament";
        var compile = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText(fixture));
        var cir = FirmamentCirLowerer.Lower(compile.Compilation.Value.PrimitiveLoweringPlan!);
        Assert.True(cir.IsSuccess);

        var brep = compile.Compilation.Value.PrimitiveExecutionResult!.ExecutedBooleans.Single(b => b.FeatureId == "blind_bore").Body;
        var cirBounds = cir.Value.Root.Bounds;
        var cirVolume = CirAnalyzer.EstimateVolume(cir.Value.Root, 64);
        var brepVolume = EstimateBrepVolume(brep, 64);
        if (brepVolume.HasValue && brepVolume.Value > 1e-9d)
        {
            Assert.InRange(Math.Abs(cirVolume - brepVolume.Value) / brepVolume.Value, 0d, 0.2d);
        }

        var probes = new[] { new Point3D(0d, 0d, 5d), new Point3D(30d, 0d, 0d), new Point3D(0d, 0d, -20d) };
        foreach (var probe in probes)
        {
            var cirClass = CirAnalyzer.ClassifyPoint(cir.Value.Root, probe).Classification;
            var brepResult = BrepSpatialQueries.ClassifyPoint(brep, probe);
            if (brepResult.IsSuccess && brepResult.Value != PointContainment.Unknown)
            {
                Assert.Equal(cirClass == CirPointClassification.Inside, brepResult.Value == PointContainment.Inside);
            }
        }
    }

    private static double? EstimateBrepVolume(BrepBody body, int resolution)
    {
        var bounds = ComputeBoundsFromVertices(body);
        if (!bounds.HasValue)
        {
            return null;
        }

        var value = bounds.Value;
        var dx = (value.Max.X - value.Min.X) / resolution;
        var dy = (value.Max.Y - value.Min.Y) / resolution;
        var dz = (value.Max.Z - value.Min.Z) / resolution;
        var cell = dx * dy * dz;
        var inside = 0;
        for (var ix = 0; ix < resolution; ix++)
        for (var iy = 0; iy < resolution; iy++)
        for (var iz = 0; iz < resolution; iz++)
        {
            var p = new Point3D(value.Min.X + (ix + 0.5d) * dx, value.Min.Y + (iy + 0.5d) * dy, value.Min.Z + (iz + 0.5d) * dz);
            var result = BrepSpatialQueries.ClassifyPoint(body, p);
            if (result.IsSuccess && result.Value == PointContainment.Inside)
            {
                inside++;
            }
        }

        return inside * cell;
    }

    private static BoundingBox3D? ComputeBoundsFromVertices(BrepBody body)
    {
        var points = body.Topology.Vertices
            .Select(v => body.TryGetVertexPoint(v.Id, out var p) ? p : (Point3D?)null)
            .Where(p => p.HasValue)
            .Select(p => p!.Value)
            .ToArray();
        if (points.Length == 0)
        {
            return null;
        }

        return new BoundingBox3D(
            new Point3D(points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z)),
            new Point3D(points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z)));
    }
}
