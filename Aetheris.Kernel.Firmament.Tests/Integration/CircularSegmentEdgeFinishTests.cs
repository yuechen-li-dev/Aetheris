using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests.Integration;

public sealed class CircularSegmentEdgeFinishTests
{
    [Theory]
    [InlineData("fillet")]
    [InlineData("chamfer")]
    public void RealSourceCompilesWithFinishRatherThanUnchangedExtrusion(string kind)
    {
        var source = Source(kind);
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join("\n", built.Diagnostics.Select(d => d.Message)));
        var imported = Step242Importer.ImportBody(built.Value.StepText);
        Assert.True(imported.IsSuccess, string.Join("\n", imported.Diagnostics.Select(d => d.Message)));
        AssertBody(imported.Value);
        Assert.Contains(imported.Value.Geometry.Surfaces, s => s.Value.Kind ==
            (kind == "fillet" ? SurfaceGeometryKind.Torus : SurfaceGeometryKind.Cone));
        Assert.Equal(built.Value.StepText, FirmamentBuildAndExport.CompileSource(source).Value.StepText);
        var plain = source[..source.IndexOf("    Modify", StringComparison.Ordinal)] + "}\n";
        var unmodified = FirmamentBuildAndExport.CompileSource(plain);
        Assert.True(unmodified.IsSuccess);
        Assert.NotEqual(unmodified.Value.StepText, built.Value.StepText);
        Assert.Equal(4, Step242Importer.ImportBody(unmodified.Value.StepText).Value.Topology.Faces.Count());
    }

    [Theory]
    [InlineData(false, false, false, 180d)]
    [InlineData(true, false, false, 180d)]
    [InlineData(false, true, false, 180d)]
    [InlineData(true, true, true, 180d)]
    [InlineData(false, false, true, 120d)]
    [InlineData(true, true, true, 240d)]
    public void BothFinishesRetainSharedIncidenceAndActualSupportCurves(bool clockwise, bool bottom, bool placed, double sweep)
    {
        foreach (var kind in new[] { ProfileEdgeFinishKind.Fillet, ProfileEdgeFinishKind.Chamfer })
        {
            var (profile, target) = Profile(clockwise, bottom, placed, sweep);
            var mixed = ProfileEdgeFinishMixedShellPlanner.TryPlan(profile, target, kind, 1);
            Assert.True(mixed.Succeeded, string.Join("\n", mixed.Diagnostics));
            Assert.Equal("CircularSegmentMiter", mixed.Plan!.JunctionDecision!.Strategy);
            Assert.Single(mixed.Plan.JunctionDecision.Judgment.Candidates, c => c.Admissible);
            if (kind == ProfileEdgeFinishKind.Chamfer)
            {
                var normal = Assert.Single(mixed.Plan.OrderedPatches.OfType<PlanarChamferPatch>()).Surface.Normal;
                var capAxis = profile.EffectiveConstructionPlane.AxisZ.ToVector() * (bottom ? -1 : 1);
                Assert.True(normal.ToVector().Dot(capAxis) > 0); // Outward support, as well as on-surface trims.
            }
            var result = CircularSegmentEdgeFinishMaterializer.TryMaterialize(profile, target, mixed.Plan);
            Assert.True(result.Succeeded, string.Join("\n", result.Diagnostics));
            var body = result.Body!;
            AssertBody(body);
            Assert.Equal(2, result.Correspondence!.Descendants.Count(d => d.Kind == "Edge" && d.GeometryPreview!.Contains("Intersection", StringComparison.Ordinal)));
            Assert.All(body.Topology.Vertices, vertex =>
            {
                Assert.True(body.TryGetVertexPoint(vertex.Id, out var point));
                var local = profile.EffectiveConstructionPlane.ToLocal(point);
                Assert.InRange(local.Z, -1e-7, 8 + 1e-7);
            });
            var step = Step242Exporter.ExportBody(body);
            Assert.True(step.IsSuccess, string.Join("\n", step.Diagnostics.Select(d => d.Message)));
            AssertBody(Step242Importer.ImportBody(step.Value).Value);
            AssertMiterGeometry(profile, result, kind, bottom);
        }
    }

    [Theory]
    [InlineData(5d)]
    [InlineData(6d)]
    [InlineData(8d)]
    public void OversizeFinishesRejectBeforeAllocatingBody(double size)
    {
        var (profile, target) = Profile(false, false, false, 180);
        foreach (var kind in new[] { ProfileEdgeFinishKind.Fillet, ProfileEdgeFinishKind.Chamfer })
        {
            var planned = ProfileEdgeFinishMixedShellPlanner.TryPlan(profile, target, kind, size);
            Assert.False(planned.Succeeded);
            Assert.Null(planned.Plan);
            Assert.Contains(planned.Diagnostics, d => d.Contains(size == 8 ? "Thickness" : "InsetCollapse", StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData("fillet")]
    [InlineData("chamfer")]
    public void PreferredAndCompatibilityFieldsProduceIdenticalStep(string kind)
    {
        var source = Source(kind);
        var preferred = FirmamentBuildAndExport.CompileSource(source);
        var compatibility = FirmamentBuildAndExport.CompileSource(FirmamentSourceSpelling.Normalize(source));
        Assert.True(preferred.IsSuccess, string.Join("\n", preferred.Diagnostics.Select(d => d.Message)));
        Assert.True(compatibility.IsSuccess, string.Join("\n", compatibility.Diagnostics.Select(d => d.Message)));
        Assert.Equal(preferred.Value.StepText, compatibility.Value.StepText);
    }

    [Theory]
    [InlineData(-200)]
    [InlineData(200)]
    public void TranslatingTheSketchCannotReverseItsMaterialSide(int y)
    {
        foreach (var kind in new[] { "fillet", "chamfer" })
        {
            var source = Source(kind).Replace("Point2(10mm, 0mm)", $"Point2(10mm, {y}mm)");
            var built = FirmamentBuildAndExport.CompileSource(source);
            Assert.True(built.IsSuccess, string.Join("\n", built.Diagnostics.Select(d => d.Message)));
            var body = Step242Importer.ImportBody(built.Value.StepText).Value;
            AssertBody(body);
            if (kind == "fillet")
            {
                var torus = Assert.Single(body.Geometry.Surfaces, s => s.Value.Kind == SurfaceGeometryKind.Torus).Value.Torus!.Value;
                Assert.Equal(9, torus.MajorRadius);
                Assert.Equal(y, torus.Center.Y, 7);
            }
        }
    }

    private static void AssertMiterGeometry(ResolvedProfile2D profile, ProfileBoundaryChamferPlanResult result,
        ProfileEdgeFinishKind kind, bool bottom)
    {
        var source = (LineArcCircularArc2D)profile.Loops[0].Segments[0].Geometry;
        var line = (LineArcLineSegment2D)profile.Loops[0].Segments[1].Geometry;
        var dx = line.End.X - line.Start.X; var dy = line.End.Y - line.Start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy); var winding = Math.Sign(source.SweepAngleRadians);
        var nx = -dy / length * winding; var ny = dx / length * winding;
        var k = (line.Start.X - source.Center.X) * nx + (line.Start.Y - source.Center.Y) * ny;
        foreach (var seam in result.Correspondence!.Descendants.Where(d => d.Kind == "Edge"))
        {
            var binding = result.Body!.Bindings.GetEdgeBinding(seam.Edge!.Value);
            var curve = result.Body.Geometry.GetCurve(binding.CurveGeometryId).BSpline3!.Value;
            Assert.Equal(kind == ProfileEdgeFinishKind.Fillet ? 3 : 2, curve.Degree);
            for (var i = 0; i <= 512; i++)
            {
                var p = profile.EffectiveConstructionPlane.ToLocal(curve.Evaluate(curve.DomainStart + (curve.DomainEnd - curve.DomainStart) * i / 512));
                var rise = bottom ? 1 - p.Z : p.Z - 7;
                var inset = kind == ProfileEdgeFinishKind.Fillet ? 1 - Math.Sqrt(Math.Max(0, 1 - rise * rise)) : rise;
                var radial = Math.Sqrt((p.X - source.Center.X) * (p.X - source.Center.X) + (p.Y - source.Center.Y) * (p.Y - source.Center.Y));
                var lineDistance = (p.X - source.Center.X) * nx + (p.Y - source.Center.Y) * ny - k;
                // Comparing implicit support equations avoids the sqrt conditioning
                // at the horizontal cap endpoint of a fillet.
                if (kind == ProfileEdgeFinishKind.Fillet)
                {
                    Assert.InRange(Math.Abs((1 - lineDistance) * (1 - lineDistance) + rise * rise - 1), 0, 5e-6);
                    Assert.InRange(Math.Abs((radial - 9) * (radial - 9) + rise * rise - 1), 0, 5e-6);
                }
                else
                {
                    Assert.InRange(Math.Abs(lineDistance - inset), 0, 1e-9);
                    Assert.InRange(Math.Abs(radial - (10 - inset)), 0, 1e-9);
                }
            }
        }
    }

    private static void AssertBody(BrepBody body)
    {
        Assert.Single(body.Topology.Bodies); Assert.Single(body.Topology.Shells);
        Assert.Equal(6, body.Topology.Faces.Count()); Assert.Equal(10, body.Topology.Edges.Count()); Assert.Equal(6, body.Topology.Vertices.Count());
        var preflight = BrepExportPreflight.Validate(body);
        Assert.True(preflight.IsValid, string.Join("\n", preflight.Diagnostics.Select(d => d.Message + ":" + d.MeasuredDeviation)));
        foreach (var edge in body.Topology.Edges)
        {
            var uses = body.Topology.Coedges.Where(c => c.EdgeId == edge.Id).ToArray();
            Assert.Equal(2, uses.Length); Assert.NotEqual(uses[0].IsReversed, uses[1].IsReversed);
        }
    }

    private static (ResolvedProfile2D, ProfileBoundaryChamferTarget) Profile(bool clockwise, bool bottom, bool placed, double sweep)
    {
        var signedSweep = (clockwise ? -1 : 1) * sweep * Math.PI / 180;
        var arc = new LineArcCircularArc2D((0, 0), 10, 0, signedSweep);
        var line = new LineArcLineSegment2D((10 * Math.Cos(signedSweep), 10 * Math.Sin(signedSweep)), (10, 0));
        var frame = placed ? ConstructionPlane.WorldXY with { Origin = new(20, 30, 40),
            AxisY = Direction3D.Create(new(0, 0, 1)), AxisZ = Direction3D.Create(new(0, -1, 0)) } : ConstructionPlane.WorldXY;
        var profile = new ResolvedProfile2D("Section", "XY", [new("Outer", true,
            [new("Round", arc, new("round", "Outline", "fixture", "arc", "XY")),
             new("Flat", line, new("flat", "Outline", "fixture", "chord", "XY"))])], frame, 0, 8);
        var target = new ProfileBoundaryChamferTarget("edgefinish:Rim", "Body", "Section", "Outer", ["Round", "Flat"],
            bottom ? ProfileBoundaryChamferSide.Bottom : ProfileBoundaryChamferSide.Top,
            ProfileBoundaryChamferChainKind.ClosedLoop, "fixture", "SourceOrder");
        return (profile, target);
    }

    private static string Source(string kind) => File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath(
        $"fixtures/Regression/EdgeFinishRobustness/d-profile-{kind}.firmament"));
}
