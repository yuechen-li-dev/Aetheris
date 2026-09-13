using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class WindingAxisMateTests
{
    [Theory]
    [InlineData(6)]
    [InlineData(4.5)]
    public void WindingDatumMatesActualPitchedCoilAxisToStem(double pitch)
    {
        var fixture = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/Assembly/coil-on-stem.firmament");
        var source = File.ReadAllText(fixture).Replace("P: 6mm", "P: " + pitch.ToString(System.Globalization.CultureInfo.InvariantCulture) + "mm", StringComparison.Ordinal);
        var result = new AssemblyM1Pipeline().Compile(source, fixture);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        var coil = result.Ir!.Instances.Single(i => i.Path.ToString() == "GuidedSpring.Coil");
        var datum = coil.SemanticRoot.ExposedMembers["Winding"].ExposedMembers["Axis"];
        Assert.True(datum.TryBinding<ExactAxisBinding>(out var local));
        Assert.Equal(9, local.OriginY, 10);
        Assert.InRange(local.DirectionX, .07, .12); // Wire tangent is not the helix axis.
        var world = Assert.IsType<ExactAxisBinding>(AssemblyWorldQuery.Resolve(result.Ir, datum.StableIdentity));
        Assert.InRange(double.Abs(world.OriginX) + double.Abs(world.OriginY), 0, 1e-12);
        Assert.InRange(double.Abs(world.DirectionX) + double.Abs(world.DirectionY), 0, 1e-12);
        Assert.Equal(1, world.DirectionZ, 12);
        Assert.Equal(2, result.Geometry!.Artifact.MateResiduals.Count);
        Assert.All(result.Geometry.Artifact.MateResiduals, r => Assert.True(r.Passed));
        Assert.Single(result.Ir.FitResults);
        // Fit classification uses the compiler-derived clear diameter, not the part origin.
        var invalid = new AssemblyM1Pipeline().Compile(source.Replace("Diameter = 8mm", "Diameter = 17mm", StringComparison.Ordinal), fixture);
        Assert.False(Assert.Single(invalid.Ir!.FitResults).Compatible);
        var wrongAxis = new AssemblyM1Pipeline().Compile(source.Replace("-> [0,0,1]", "-> [1,0,0]", StringComparison.Ordinal), fixture);
        Assert.False(wrongAxis.IsSuccess);
        Assert.Contains(wrongAxis.Diagnostics, d => d.Code == "assembly-mate-geometry-residual");
    }
}
