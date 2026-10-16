using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Brep.Features;

public sealed class TopBoundaryFilletThroughHoleTests
{
    [Theory]
    [InlineData(60d, 40d, 10d, 1d)]
    [InlineData(18d, 30d, 7d, 2d)]
    public void FilletBand_HasExactCylinderIntersectionsAndTangentSupportContacts(double w, double d, double h, double r)
    {
        var result = CombinedTopBoundaryEdgeFinishThroughHoleBuilder.Build(Plan(w, d, h, r));
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Check(result.Value, 10, 4);
        var body = result.Value;
        var cylinders = body.Geometry.Surfaces.Select(x => x.Value).Where(s => s.Kind == SurfaceGeometryKind.Cylinder).Select(s => s.Cylinder!.Value).ToArray();
        foreach (var binding in body.Bindings.EdgeBindings)
        {
            var curve = body.Geometry.GetCurve(binding.CurveGeometryId);
            if (curve.Kind != CurveGeometryKind.Ellipse3) continue;
            var ellipse = curve.Ellipse3!.Value;
            var edge = body.Topology.GetEdge(binding.EdgeId);
            Assert.True(body.TryGetVertexPoint(edge.StartVertexId, out var start));
            Assert.True(body.TryGetVertexPoint(edge.EndVertexId, out var end));
            Assert.InRange((ellipse.Evaluate(0) - start).Length, 0, 1e-9);
            Assert.InRange((ellipse.Evaluate(System.Math.PI / 2) - end).Length, 0, 1e-9);
            foreach (var t in new[] { 0d, .2, .7, System.Math.PI / 2 })
            {
                var p = ellipse.Evaluate(t);
                Assert.Equal(2, cylinders.Count(c => System.Math.Abs(Radial(p - c.Origin, c.Axis.ToVector()).Length - r) < 1e-9));
            }
        }
        // Each cylinder is tangent to its vertical support at the shoulder and
        // to +Z at the top. The junction between cylinders is a sharp ellipse.
        foreach (var c in cylinders)
        {
            Assert.Equal(h - r, c.Origin.Z, 9);
            Assert.InRange(System.Math.Abs(c.XAxis.Z), 0, 1e-9);
            Assert.InRange(System.Math.Abs(c.Axis.Z), 0, 1e-9);
        }
        var step = Step242Exporter.ExportBody(body, new Step242ExportOptions { BrepExportPreflightMode = BrepExportPreflightMode.Enforce });
        Assert.True(step.IsSuccess, string.Join("; ", step.Diagnostics.Select(x => x.Message)));
        var imported = Step242Importer.ImportBody(step.Value!);
        Assert.True(imported.IsSuccess);
        Check(imported.Value, 10, 4);
    }

    [Fact]
    public void AnalyticVolume_AgreesWithIndependentSectionIntegrationAndHoleRemoval()
    {
        var plan = Plan(60, 40, 10, 2) with { Holes = [new("shaft", -10, 0, 2), new("counterbore", 10, 0, 2, 4, 3)] };
        const int n = 100_000;
        var dz = plan.FinishSize / n;
        var volume = plan.Width * plan.Depth * (plan.ZMax - plan.FinishSize);
        for (var i = 0; i <= n; i++)
        {
            var t = i * dz;
            var inset = plan.FinishSize - System.Math.Sqrt(System.Math.Max(0, plan.FinishSize * plan.FinishSize - t * t));
            volume += (plan.Width - 2 * inset) * (plan.Depth - 2 * inset) * dz * (i is 0 or n ? .5 : 1);
        }
        volume -= System.Math.PI * (4 * 10 + 4 * 10 + 12 * 3);
        Assert.InRange(System.Math.Abs(plan.AnalyticVolume - volume), 0, 1e-4);
        var body = CombinedTopBoundaryEdgeFinishThroughHoleBuilder.Build(plan);
        Assert.True(body.IsSuccess);
        Check(body.Value, 14, 7);
    }

    [Theory]
    [InlineData(0d, 0d, 0d)]
    [InlineData(0d, 0d, 10d)]
    [InlineData(0d, 0d, double.NaN)]
    [InlineData(25d, 0d, 2d)] // tangent to the inset top face
    [InlineData(26d, 0d, 2d)] // crosses the fillet band
    public void InvalidOrInteractingIntent_CannotProduceABody(double x, double y, double radius)
    {
        var plan = Plan(60, 40, 10, radius) with { Holes = [new("bore", x, y, 3)] };
        Assert.False(CombinedTopBoundaryEdgeFinishThroughHoleBuilder.Build(plan).IsSuccess);
    }

    private static void Check(BrepBody body, int faces, int cylinders)
    {
        Assert.Equal(faces, body.Topology.Faces.Count());
        Assert.Equal(cylinders, body.Geometry.Surfaces.Count(x => x.Value.Kind == SurfaceGeometryKind.Cylinder));
        Assert.Equal(4, body.Geometry.Curves.Count(x => x.Value.Kind == CurveGeometryKind.Ellipse3));
        var preflight = BrepExportPreflight.Validate(body);
        Assert.True(preflight.IsValid, string.Join("; ", preflight.Diagnostics.Select(x => x.Message)));
    }

    private static Vector3D Radial(Vector3D delta, Vector3D axis) => delta - axis * delta.Dot(axis);
    private static CombinedTopBoundaryEdgeFinishThroughHolePlan Plan(double w, double d, double h, double r) =>
        new("finish-plan", "host-plan", ["EdgeFinish"], w, d, 0, h, r, [], TopBoundaryEdgeFinishKind.Fillet);
}
