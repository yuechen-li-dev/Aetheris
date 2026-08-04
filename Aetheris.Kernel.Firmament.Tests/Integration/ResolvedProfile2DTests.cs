using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests.Integration;

public sealed class ResolvedProfile2DTests
{
    [Fact]
    public void ValidatesNamedCounterClockwiseRectangleAndExtrudesThroughExistingEmitter()
    {
        var profile = Profile("Plate", [(-1d,-1d),(1d,-1d),(1d,1d),(-1d,1d)]);
        var validation = ResolvedProfile2DValidator.Validate(profile);
        Assert.True(validation.IsValid, string.Join("; ", validation.Diagnostics));
        Assert.Equal(4d, validation.SignedArea);
        Assert.Equal(LineArcProfileExtrudeStatus.Succeeded, ResolvedProfile2DValidator.Extrude(profile, 2).Status);
    }

    [Fact]
    public void RejectsSelfIntersectingNamedLoopBeforeEmission()
    {
        var profile = Profile("BowTie", [(-1d,-1d),(1d,1d),(-1d,1d),(1d,-1d)]);
        var validation = ResolvedProfile2DValidator.Validate(profile);
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Diagnostics, d => d.Contains("self-intersection", StringComparison.Ordinal));
        Assert.Equal(LineArcProfileExtrudeStatus.Rejected, ResolvedProfile2DValidator.Extrude(profile, 2).Status);
    }

    private static ResolvedProfile2D Profile(string name, IReadOnlyList<(double X,double Y)> points)
    {
        var segments = points.Select((p,i) => new ResolvedProfileSegment2D($"S{i}", new LineArcLineSegment2D(p, points[(i+1)%points.Count]), new ProfileSegmentProvenance($"profile:{name}.Outer.S{i}", "concept:fixture.Guide", "fixture", "literal-test", "XY"))).ToArray();
        return new ResolvedProfile2D(name, "XY", [new ResolvedProfileLoop2D("Outer", true, segments)]);
    }
}
