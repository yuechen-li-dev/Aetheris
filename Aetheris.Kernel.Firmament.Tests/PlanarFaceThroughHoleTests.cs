using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class PlanarFaceThroughHoleTests
{
    [Theory]
    [InlineData(16d, -10d, 6d, true)]
    [InlineData(6d, -10d, 6d, false)] // Tangent to the upright: no zero-width wall.
    [InlineData(16d, -2d, 6d, false)]
    public void BracketFoot_PreservesSourceFrameAndBuildsExactThroughCavity(double x, double y, double diameter, bool admitted)
    {
        var fixture = FirmamentCorpusHarness.ReadFixtureText("fixtures/Regression/OrientedComposeHoles/simple-bracket-through-hole.firmament");
        var original = fixture[..fixture.IndexOf("  Modify", StringComparison.Ordinal)] + "}";
        var baseline = FirmamentBuildAndExport.CompileSource(original);
        Assert.True(baseline.IsSuccess, string.Join(";", baseline.Diagnostics.Select(d => d.Message)));
        var host = baseline.Value.RuntimeBody!;
        _ = new GeometrySourceMap(baseline.Value.RuntimeCorrespondence!);
        var target = baseline.Value.RuntimeCorrespondence!.Descendants.Where(d => d.Face is not null && d.FirmamentSelector is not null)
            .Select(d => (Descendant: d, Frame: PlanarFaceThroughHole.Support(host, d.Face!.Value)))
            .Single(d => d.Frame is not null && d.Frame.Normal[2] > .99 && Math.Abs(d.Frame.Origin[2] - 3d) < 1e-8);
        var frame = target.Frame!; var p = new Point3D(x, y, 3d); var relative = p - new Point3D(frame.Origin[0], frame.Origin[1], frame.Origin[2]);
        var u = relative.Dot(new(frame.X[0], frame.X[1], frame.X[2])); var v = relative.Dot(new(frame.Y[0], frame.Y[1], frame.Y[2]));
        var source = original[..original.LastIndexOf('}')] + FormattableString.Invariant($"Modify bracket {{ Hole<Shaft> Mount {{ On: {target.Descendant.FirmamentSelector}; Center: Point2({u}mm, {v}mm); Diameter: {diameter}mm; End: ThroughAll }} }} }}");
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.Equal(admitted, built.IsSuccess);
        if (!admitted) { Assert.Contains(built.Diagnostics, d => d.Message.Contains("clearance", StringComparison.Ordinal)); return; }
        var cylinder = built.Value.RuntimeBody!.Geometry.Surfaces.Single(s => s.Value.Cylinder is not null).Value.Cylinder!.Value;
        Assert.InRange((cylinder.Origin - p).Length, 0d, 1e-8d);
        foreach (var body in new[] { built.Value.RuntimeBody!, Step242Importer.ImportBody(built.Value.StepText).Value })
        {
            var mass = BrepMassProperties.Evaluate(body);
            Assert.True(mass.IsEnclosed); Assert.True(mass.IsOrientationConsistent);
            Assert.InRange(Math.Abs(mass.AbsoluteVolume - (4020d - Math.PI * 9d * 3d)), 0, Math.Max(mass.ErrorBound ?? 0, 1e-6));
        }
        Assert.Contains(built.Value.RuntimeCorrespondence!.Descendants, d => d.Role == SemanticTopologyRole.HoleWallFace);
    }
}
