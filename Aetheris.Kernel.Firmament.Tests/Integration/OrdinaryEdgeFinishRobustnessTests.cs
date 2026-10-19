using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests.Integration;

public sealed class OrdinaryEdgeFinishRobustnessTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ConvexPolygonRimsAdmitAcuteObtuseAndSubdividedStraightSupports(bool bottom, bool chamfer)
    {
        foreach (var points in new (double, double)[][] {
            [(0, 0), (30, 0), (12, 20)],
            [(0, 0), (30, 0), (38, 20), (0, 20)],
            [(0, 0), (15, 0), (30, 0), (30, 20), (0, 20)] })
        {
            var profile = Polygon(points);
            var target = Target(profile, bottom);
            var result = chamfer ? ProfileBoundaryChamferPlanner.TryPlan(profile, target, 1) : Fillet(profile, target, 1);
            Assert.True(result.Succeeded, string.Join("\n", result.Diagnostics));
            Check(result.Body!);
            var step = Step242Exporter.ExportBody(result.Body!);
            Assert.True(step.IsSuccess, string.Join("\n", step.Diagnostics.Select(d => d.Message)));
            Check(Step242Importer.ImportBody(step.Value).Value);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvertedInsetIsRejectedInsteadOfEmittingAnInsideOutCap(bool chamfer)
    {
        var profile = Polygon([(0, 0), (30, 0), (30, 4), (0, 4)]);
        var target = Target(profile, false);
        var result = chamfer ? ProfileBoundaryChamferPlanner.TryPlan(profile, target, 3) : Fillet(profile, target, 3);
        Assert.False(result.Succeeded); Assert.Null(result.Body);
        Assert.Contains(result.Diagnostics, d => d.Contains("Inset", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("profile-edgefinish-chimera-fillet.firmament", false)]
    [InlineData("profile-edgefinish-chimera-chamfer.firmament", true)]
    public void MixedTangentLoopBottomUsesTheSameQualifiedSupports(string fixture, bool chamfer)
    {
        var source = File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Regression/CanonicalGeometry/" + fixture));
        var profile = ProfileAuthoringParser.Parse(source).Profile!;
        var target = Target(profile, true);
        var result = chamfer ? ProfileBoundaryChamferPlanner.TryPlan(profile, target, 4) : Fillet(profile, target, 4);
        Assert.True(result.Succeeded, string.Join("\n", result.Diagnostics));
        Check(result.Body!);
        var step = Step242Exporter.ExportBody(result.Body!);
        Assert.True(step.IsSuccess, string.Join("\n", step.Diagnostics.Select(d => d.Message)));
        Check(Step242Importer.ImportBody(step.Value).Value);
    }

    private static ProfileBoundaryChamferPlanResult Fillet(ResolvedProfile2D p, ProfileBoundaryChamferTarget t, double radius)
    {
        var result = ProfileFilletShellPlanner.TryPlan(p, t, radius, radius);
        return new(result.Succeeded, result.Body, result.Correspondence, t, result.Diagnostics);
    }
    private static void Check(BrepBody body)
    {
        var preflight = BrepExportPreflight.Validate(body);
        Assert.True(preflight.IsValid, string.Join("\n", preflight.Diagnostics.Select(d => d.Code + ":" + d.Message + ":" + d.MeasuredDeviation)));
        foreach (var edge in body.Topology.Edges)
        {
            var uses = body.Topology.Coedges.Where(c => c.EdgeId == edge.Id).ToArray();
            Assert.Equal(2, uses.Length); Assert.NotEqual(uses[0].IsReversed, uses[1].IsReversed);
        }
        foreach (var loop in body.Topology.Loops)
        {
            for (var i = 0; i < loop.CoedgeIds.Count; i++)
            {
                var use = body.Topology.GetCoedge(loop.CoedgeIds[i]);
                var next = body.Topology.GetCoedge(loop.CoedgeIds[(i + 1) % loop.CoedgeIds.Count]);
                var edge = body.Topology.GetEdge(use.EdgeId); var nextEdge = body.Topology.GetEdge(next.EdgeId);
                Assert.Equal(use.IsReversed ? edge.StartVertexId : edge.EndVertexId,
                    next.IsReversed ? nextEdge.EndVertexId : nextEdge.StartVertexId);
            }
        }
    }
    private static ResolvedProfile2D Polygon((double X, double Y)[] points) => new("Section", "XY",
        [new("Outer", true, points.Select((point, i) => new ResolvedProfileSegment2D("Edge" + i,
            new LineArcLineSegment2D(point, points[(i + 1) % points.Length]), new("edge:" + i, "fixture", "fixture", "line", "XY"))).ToArray())],
        LocalStartDepth: 0, LocalEndDepth: 8);
    private static ProfileBoundaryChamferTarget Target(ResolvedProfile2D p, bool bottom) => new("edgefinish:Rim", "Body", p.Name, "Outer",
        p.Loops[0].Segments.Select(s => s.Name).ToArray(), bottom ? ProfileBoundaryChamferSide.Bottom : ProfileBoundaryChamferSide.Top,
        ProfileBoundaryChamferChainKind.ClosedLoop, "fixture", "SourceOrder");
}
