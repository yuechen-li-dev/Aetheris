using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Lowering;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentPlacementAnchorSemanticsTests
{
    [Fact]
    public void SharedAnchor_TopFace_MatchesProduction()
    {
        var compile = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/w2_cylinder_root_blind_bore_semantic.firmament"));
        var baseBody = compile.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives.Single(p => p.FeatureId == "flange").Body;
        var bounds = baseBody.ComputeBounds()!.Value;

        Assert.True(FirmamentPlacementAnchorSemantics.TryResolveAuthoredFaceAnchorFromBounds("top_face", bounds.Min, bounds.Max, out var anchor));
        Assert.Equal(bounds.Max.Z, anchor.Z, 6);
        Assert.Equal((bounds.Min.X + bounds.Max.X) * 0.5d, anchor.X, 6);
        Assert.Equal((bounds.Min.Y + bounds.Max.Y) * 0.5d, anchor.Y, 6);
    }
}

internal static class BrepBodyBoundsExtensions
{
    public static BoundingBox3D? ComputeBounds(this Aetheris.Kernel.Core.Brep.BrepBody body)
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
