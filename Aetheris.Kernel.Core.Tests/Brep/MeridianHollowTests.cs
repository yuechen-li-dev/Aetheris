using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Geometry.Planar;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Brep;

public sealed class MeridianHollowTests
{
    private static PlanarLine L(double r0, double z0, double r1, double z1) => new((r0, z0), (r1, z1));
    private static PlanarSpan[] Cylinder(double r, double h) => [L(0, 0, r, 0), L(r, 0, r, h), L(r, h, 0, h), L(0, h, 0, 0)];

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void CylinderWallsAreExactAtEveryOpening(bool top, bool bottom)
    {
        const double r = 20, h = 50, t = 2;
        var wall = MeridianHollow.Wall(Cylinder(r, h), t, top, bottom);
        Assert.True(wall.IsSuccess, string.Join("; ", wall.Diagnostics.Select(d => d.Message)));
        var shell = BrepRevolvedShell.Build(wall.Value, RevolutionFrame.WorldZ);
        Assert.True(shell.IsSuccess, string.Join("; ", shell.Diagnostics.Select(d => d.Message)));
        var closedCaps = (top ? 0 : 1) + (bottom ? 0 : 1);
        var expected = double.Pi * (r * r * h - (r - t) * (r - t) * (h - closedCaps * t));
        AssertSolid(shell.Value.Body, expected);
        // Outer wall, inner wall, one rim per opening, two faces per closed end.
        Assert.Equal(2 + (top ? 1 : 2) + (bottom ? 1 : 2), shell.Value.Body.Topology.Faces.Count());
    }

    [Fact]
    public void BlendedBottomBecomesConcentricTori()
    {
        PlanarSpan[] cup = [L(0, 0, 12, 0), new PlanarArc((12, 8), 8, -double.Pi / 2, double.Pi / 2), L(20, 8, 20, 40), L(20, 40, 0, 40), L(0, 40, 0, 0)];
        var wall = MeridianHollow.Wall(cup, 2, openTop: true, openBottom: false);
        Assert.True(wall.IsSuccess);
        var arcs = wall.Value.OfType<PlanarArc>().OrderByDescending(a => a.Radius).ToArray();
        Assert.Equal([8d, 6d], arcs.Select(a => a.Radius));
        Assert.All(arcs, arc => Assert.Equal((12d, 8d), arc.Center));
        var shell = BrepRevolvedShell.Build(wall.Value, RevolutionFrame.WorldZ);
        Assert.True(shell.IsSuccess);
        Assert.Equal(2, shell.Value.Body.Geometry.Surfaces.Count(s => s.Value.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Torus));
    }

    [Fact]
    public void ClosedVesselsAndApexesAreRefused()
    {
        Assert.Contains(MeridianHollow.Wall(Cylinder(20, 50), 2, false, false).Diagnostics, d => d.Message.StartsWith("hollow-closed-vessel-unsupported", StringComparison.Ordinal));
        var cone = MeridianHollow.Wall([L(0, 0, 30, 40), L(30, 40, 0, 40), L(0, 40, 0, 0)], 1.5, true, false);
        Assert.True(cone.IsSuccess);
        Assert.Contains(BrepRevolvedShell.Build(cone.Value, RevolutionFrame.WorldZ).Diagnostics, d => d.Message.StartsWith("revolve-shell-apex-unsupported", StringComparison.Ordinal));
    }

    private static void AssertSolid(BrepBody body, double expectedVolume)
    {
        var step = Step242Exporter.ExportBody(body);
        Assert.True(step.IsSuccess);
        var imported = Step242Importer.ImportBody(step.Value);
        Assert.True(imported.IsSuccess);
        var enclosure = BrepMassProperties.EvaluateEnclosure(imported.Value);
        Assert.True(enclosure.IsEnclosed && enclosure.IsOrientationConsistent, string.Join(" | ", enclosure.Diagnostics));
        var volume = BrepMassProperties.Evaluate(imported.Value, new(0.01d, double.Pi / 180d, 1d, MaximumSegments: 512)).AbsoluteVolume;
        Assert.InRange(volume, expectedVolume * (1 - 1e-3), expectedVolume * (1 + 1e-3));
    }
}
