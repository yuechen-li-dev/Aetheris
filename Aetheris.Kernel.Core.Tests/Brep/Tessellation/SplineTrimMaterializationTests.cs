using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

public sealed class SplineTrimMaterializationTests
{
    [Fact]
    public void TwoCurvedCoedges_EncloseFiniteSplinePatchInNativeDomain()
    {
        var body = CreateLens();
        var result = BrepDisplayTessellator.Tessellate(body);
        Assert.True(result.IsSuccess, string.Join(";", result.Diagnostics.Select(d => d.Message)));
        var patch = Assert.Single(result.Value.FacePatches); Assert.NotEmpty(patch.TriangleIndices);
        Assert.All(patch.Positions, p => { Assert.InRange(p.X, 0, 2); Assert.InRange(p.Y, -.501, .501); Assert.Equal(0, p.Z, 9); });
    }

    [Fact]
    public void SubBudgetLens_WithCollapsedRecoveredPcurves_DisplaysActualBoundaryWithoutChangingBindings()
    {
        var body = CreateLens(.001);
        var face = Assert.Single(body.Topology.Faces);
        var surfaceId = body.Bindings.GetFaceBinding(face.Id).SurfaceGeometryId;
        foreach (var coedge in body.Topology.Coedges)
        {
            var pc = coedge.Id.Value == 1 ? PcurveGeometry.Line(new(7, 9), new(2, 12), new(4, 12))
                : PcurveGeometry.Line(new(7, 9), new(4, 12), new(2, 12));
            body.Bindings.AddPcurveBinding(new(coedge.Id, face.Id, surfaceId, pc,
                Qualification: new(PcurveBindingOrigin.RecoveredSpline, .0005, .001, 67, "Line", "BSplineSurfaceWithKnots")));
        }
        var bindings = body.Bindings.PcurveBindings.ToArray();
        var before = BrepPcurveValidator.Validate(body, .001, requireEveryCoedge: true);
        Assert.True(before.IsValid, string.Join(";", before.Diagnostics));
        var result = BrepDisplayTessellator.Tessellate(body);
        Assert.True(result.IsSuccess);
        var patch = Assert.Single(result.Value.FacePatches);
        Assert.NotEmpty(patch.TriangleIndices);
        Assert.Contains(patch.Positions, p => p.Y > .0002);
        Assert.Contains(patch.Positions, p => p.Y < -.0002);
        Assert.Equal(bindings, body.Bindings.PcurveBindings.ToArray());
        var after = BrepPcurveValidator.Validate(body, .001, requireEveryCoedge: true);
        Assert.True(after.IsValid);
        Assert.Equal(before.MaximumReconstructionDeviation, after.MaximumReconstructionDeviation);
    }

    private static BrepBody CreateLens(double height = 1)
    {
        var builder = new TopologyBuilder(); var geometry = new BrepGeometryStore(); var bindings = new BrepBindingModel();
        var a = builder.AddVertex(); var b = builder.AddVertex(); var loop = builder.AllocateLoopId();
        var c0 = builder.AllocateCoedgeId(); var c1 = builder.AllocateCoedgeId();
        var curves = new[] { new[] { new Point3D(0, 0, 0), new Point3D(1, height, 0), new Point3D(2, 0, 0) },
            new[] { new Point3D(2, 0, 0), new Point3D(1, -height, 0), new Point3D(0, 0, 0) } };
        var uses = new[] { c0, c1 };
        for (var i = 0; i < 2; i++)
        {
            var edge = builder.AddEdge(i == 0 ? a : b, i == 0 ? b : a); var id = new CurveGeometryId(i + 1);
            geometry.AddCurve(id, CurveGeometry.FromBSpline(new BSpline3Curve(2, curves[i], [3, 3], [7d, 9d], "UNSPECIFIED", false, false, "UNSPECIFIED")));
            bindings.AddEdgeBinding(new(edge, id, new ParameterInterval(7, 9)));
            builder.AddCoedge(new(uses[i], edge, loop, uses[1 - i], uses[1 - i], false));
        }
        builder.AddLoop(new Loop(loop, uses)); var face = builder.AddFace([loop]); builder.AddBody([builder.AddShell([face])]);
        var support = new BSplineSurfaceWithKnots(1, 1,
            [new[] { new Point3D(0, -height, 0), new Point3D(0, height, 0) }, new[] { new Point3D(2, -height, 0), new Point3D(2, height, 0) }],
            "UNSPECIFIED", false, false, false, [2, 2], [2, 2], [2d, 4d], [10d, 14d], "UNSPECIFIED");
        var surface = new SurfaceGeometryId(1); geometry.AddSurface(surface, SurfaceGeometry.FromBSplineSurfaceWithKnots(support));
        bindings.AddFaceBinding(new(face, surface));
        return new BrepBody(builder.Model, geometry, bindings, new Dictionary<VertexId, Point3D> { [a] = curves[0][0], [b] = curves[1][0] });
    }

    [Fact]
    public void RectangularSpline_WithRepeatedInteriorTurning_UsesKnotAwareAdaptiveRefinement()
    {
        var net = Enumerable.Range(0, 2).Select(row => (IReadOnlyList<Point3D>)Enumerable.Range(0, 33)
            .Select(i => new Point3D(row, i / 32d, i % 2 == 0 ? 0 : 1)).ToArray()).ToArray();
        var support = new BSplineSurfaceWithKnots(1, 1, net, "UNSPECIFIED", false, false, false,
            [2, 2], Enumerable.Range(0, 33).Select(i => i == 0 || i == 32 ? 2 : 1).ToArray(),
            [2d, 4d], Enumerable.Range(0, 33).Select(i => 10d + i / 320d).ToArray(), "UNSPECIFIED");
        var patch = TrimmedSurfaceTessellator.Tessellate(new FaceId(1),
            [new[] { (2d, 10d), (4d, 10d), (4d, 10.1d), (2d, 10.1d) }], support.Evaluate,
            (_, _) => new Vector3D(0, 0, 1), DisplayTessellationOptions.Default,
            2, 4, 10, 10.1, Warning, surfaceKind: SurfaceGeometryKind.BSplineSurfaceWithKnots, splineSupport: support);
        Assert.True(patch.IsSuccess); Assert.Empty(patch.Diagnostics);
        var p = patch.Value;
        for (var i = 0; i < p.TriangleIndices.Count; i += 3)
        {
            var points = p.TriangleIndices.Skip(i).Take(3).Select(n => p.Positions[n]).ToArray();
            var center = new Point3D(points.Average(x => x.X), points.Average(x => x.Y), points.Average(x => x.Z));
            Assert.InRange((support.Evaluate(2 + 2 * center.X, 10 + .1 * center.Y) - center).Length, 0, .051);
        }
    }

    [Fact]
    public void RectangularSpline_RetainsPhysicalQuadsBeforeFinalTriangleLowering()
    {
        var support = new BSplineSurfaceWithKnots(1, 1,
            [new[] { new Point3D(0, 0, 0), new Point3D(0, 1, 0) }, new[] { new Point3D(20, 0, 0), new Point3D(20, 1, 0) }],
            "UNSPECIFIED", false, false, false, [2, 2], [2, 2], [0d, 1d], [0d, 1d], "UNSPECIFIED");
        var patch = SplineRectangleTessellator.Tessellate(new FaceId(1), support, 0, 1, 0, 1,
            (_, _) => new Vector3D(0, 0, 1), DisplayTessellationOptions.Default, null, 65_536, out var limited);
        Assert.NotNull(patch); Assert.False(limited); Assert.NotNull(patch.Cells);
        Assert.Equal(patch.Cells.Count * 6, patch.TriangleIndices.Count);
        Assert.All(patch.Cells, cell =>
        {
            Assert.IsType<QuadCell>(cell);
            var points = cell.VertexIds.Select(i => patch.Positions[i]).ToArray();
            var edges = Enumerable.Range(0, 4).Select(i => (points[(i + 1) % 4] - points[i]).Length).ToArray();
            Assert.InRange(edges.Max() / edges.Min(), 1, 4);
        });
        // Full rectangle remains covered; balancing changes display sampling only.
        double area = 0;
        for (var i = 0; i < patch.TriangleIndices.Count; i += 3)
        {
            var a = patch.Positions[patch.TriangleIndices[i]];
            var b = patch.Positions[patch.TriangleIndices[i + 1]];
            var c = patch.Positions[patch.TriangleIndices[i + 2]];
            area += (b - a).Cross(c - a).Length * .5d;
        }
        Assert.Equal(20d, area, 8);
    }

    [Fact]
    public void ClosedSplineTrim_UsesNativePeriodAndOriginAtSeam()
    {
        var row = new[] { new Point3D(1, 0, 0), new Point3D(0, 1, 0), new Point3D(-1, 0, 0), new Point3D(0, -1, 0), new Point3D(1, 0, 0) };
        var surface = new BSplineSurfaceWithKnots(1, 1, row.Select(p => (IReadOnlyList<Point3D>)new[] { p, p + new Vector3D(0, 0, 1) }).ToArray(),
            "UNSPECIFIED", true, false, false, [2, 1, 1, 1, 2], [2, 2], [2d, 4d, 6d, 8d, 10d], [3d, 7d], "UNSPECIFIED");
        var points = new[] { (2d, 3d), (9d, 3d), (6d, 3d), (6d, 7d), (9d, 7d), (10d, 7d) };
        var loop = Assert.Single(BrepDisplayTessellator.NormalizeClosedSplineLoops(surface, [points]));
        Assert.All(loop, p => Assert.InRange(p.U, 6, 10));
        for (var i = 0; i < points.Length; i++)
            Assert.InRange((surface.Evaluate(points[i].Item1, points[i].Item2) - surface.Evaluate(loop[i].U, loop[i].V)).Length, 0, 1e-10);
    }

    [Fact]
    public void CollapsedPcurveLoop_ReportsZeroAreaWithoutInventingTriangles()
    {
        var result = TrimmedSurfaceTessellator.Tessellate(new FaceId(1),
            [new[] { (0d, 0d), (.5d, .5d), (1d, 1d) }], (u, v) => new Point3D(u, v, 0),
            (_, _) => new Vector3D(0, 0, 1), DisplayTessellationOptions.Default, 0, 1, 0, 1, Warning);
        Assert.True(result.IsSuccess); Assert.Empty(result.Value.TriangleIndices);
        Assert.Contains(result.Diagnostics, d => d.Source == "Viewer.Tessellation.TrimEvaluationFailed"
            && d.Message.Contains("zero triangles") && d.Message.Contains("signed UV area=0"));
    }

    [Fact]
    public void AdaptiveBudgetExhaustion_IsExplicitInsteadOfSilentFidelitySuccess()
    {
        var mesh = BoundaryConformingTrimTessellator.TryTessellate(new FaceId(1),
            [new() { (0, 0), (1, 0), (1, 1), (0, 1) }], 0,
            (u, v) => new Point3D(u, v, double.Sin(15 * u) * double.Sin(15 * v)),
            (_, _) => new Vector3D(0, 0, 1), .01, .01, DisplayTessellationOptions.Default, null,
            out var incomplete, refineRectangles: true, maximumTriangles: 32);
        Assert.NotNull(mesh); Assert.True(incomplete);
    }

    private static KernelDiagnostic Warning(string message, string source) => new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Warning, message, source);
}
