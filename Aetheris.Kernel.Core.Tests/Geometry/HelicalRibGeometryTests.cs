using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Geometry;

public sealed class HelicalRibGeometryTests
{
    private static readonly Direction3D Z = Direction3D.Create(new Vector3D(0, 0, 1));
    private static readonly Direction3D X = Direction3D.Create(new Vector3D(1, 0, 0));

    private static HelicalRibParameters Witness(double span = 30d, double pitch = 1.25d,
        double crestRadius = 4d, double rootWidth = 1.1d) => new(
        Point3D.Origin, Z, X, 0d, span + rootWidth,
        3.2d, crestRadius, pitch, rootWidth / 2d,
        span + rootWidth / 2d, rootWidth, 0.1d);

    [Fact]
    public void OneAndManyTurnsHaveExactFrameProfileAndDerivedLead()
    {
        foreach (var span in new[] { 1.25d, 30d })
        {
            var result = HelicalRibGeometry.Create(Witness(span));
            Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics));
            var rib = result.Value;
            Assert.Equal(span / 1.25d, rib.Turns, 12);
            Assert.Equal(1.25d, rib.LeadMm);
            Assert.Equal(0.15d, rib.MinimumAdjacentTurnClearanceMm, 12);

            foreach (var role in Enum.GetValues<HelicalRibBoundaryRole>())
            {
                var helix = rib.Boundary(role);
                var first = helix.Evaluate(helix.StartAngleRadians);
                var last = helix.Evaluate(helix.EndAngleRadians);
                Assert.Equal(span, last.Z - first.Z, 10);
                Assert.Equal(helix.RadiusMm, double.Hypot(first.X, first.Y), 10);
                Assert.Equal(helix.RadiusMm, double.Hypot(last.X, last.Y), 10);
            }
            var mid = rib.Parameters.StartAngleRadians + (rib.EndAngleRadians - rib.Parameters.StartAngleRadians) * 0.37d;
            Assert.Equal(rib.Boundary(HelicalRibBoundaryRole.LeadingCrest).Evaluate(mid),
                rib.Evaluate(HelicalRibSideRole.Crest, mid, 0d));
            Assert.Equal(rib.Boundary(HelicalRibBoundaryRole.TrailingCrest).Evaluate(mid),
                rib.Evaluate(HelicalRibSideRole.Crest, mid, 1d));
            Assert.Equal(4, rib.CapCorners(false).Count);
        }
    }

    [Fact]
    public void PitchLengthDepthAndRigidFrameChangeGeometryDeterministically()
    {
        var original = HelicalRibGeometry.Create(Witness()).Value;
        var faster = HelicalRibGeometry.Create(Witness(pitch: 1.5d)).Value;
        var longer = HelicalRibGeometry.Create(Witness(span: 35d)).Value;
        var deeper = HelicalRibGeometry.Create(Witness(crestRadius: 4.2d)).Value;
        Assert.Equal(24d, original.Turns);
        Assert.Equal(20d, faster.Turns);
        Assert.Equal(28d, longer.Turns);
        Assert.Equal(4.2d, deeper.Boundary(HelicalRibBoundaryRole.LeadingCrest).RadiusMm);
        Assert.Equal(3.2d, deeper.Boundary(HelicalRibBoundaryRole.LeadingRoot).RadiusMm);

        var transformed = Witness() with {
            AxisOrigin = new Point3D(7d, -4d, 2d),
            Axis = Direction3D.Create(new Vector3D(1d, 0d, 0d)),
            StartRadial = Direction3D.Create(new Vector3D(0d, 1d, 0d))
        };
        var rotated = HelicalRibGeometry.Create(transformed).Value;
        var p = rotated.Boundary(HelicalRibBoundaryRole.LeadingRoot).Evaluate(rotated.Parameters.StartAngleRadians);
        Assert.Equal(7d + transformed.AxialStartMm - transformed.RootWidthMm / 2d, p.X, 10);
        Assert.Equal(-4d + 3.2d, p.Y, 10);
        Assert.Equal(2d, p.Z, 10);
    }

    [Theory]
    [InlineData(0d, 30d, 1.1d, "pitch")]
    [InlineData(-1d, 30d, 1.1d, "pitch")]
    [InlineData(1.25d, 0d, 1.1d, "span")]
    [InlineData(1.25d, 30d, 1.25d, "adjacent-turn-overlap")]
    public void InvalidIntentHasSpecificDiagnostic(double pitch, double span, double rootWidth, string code)
    {
        var p = Witness(span, pitch, rootWidth: rootWidth);
        var result = HelicalRibGeometry.Create(p);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains($"helical-rib-{code}", StringComparison.Ordinal));
    }

    [Fact]
    public void OutOfSupportAndInvertedDepthAreRejected()
    {
        var outside = HelicalRibGeometry.Create(Witness() with { SupportAxialMaxMm = 30d });
        var inverted = HelicalRibGeometry.Create(Witness(crestRadius: 3.2d));
        Assert.Contains(outside.Diagnostics, d => d.Message.Contains("support-extent", StringComparison.Ordinal));
        Assert.Contains(inverted.Diagnostics, d => d.Message.Contains("radii", StringComparison.Ordinal));
    }

    [Fact]
    public void PeriodicAngularWrapPreservesRadialPositionAndAdvancesOnePitch()
    {
        var rib = HelicalRibGeometry.Create(Witness()).Value;
        var helix = rib.Boundary(HelicalRibBoundaryRole.TrailingCrest);
        var first = helix.Evaluate(helix.StartAngleRadians);
        var next = helix.Evaluate(helix.StartAngleRadians + 2d * double.Pi);
        Assert.InRange(double.Abs(first.X - next.X), 0d, 1e-12d);
        Assert.InRange(double.Abs(first.Y - next.Y), 0d, 1e-12d);
        Assert.Equal(1.25d, next.Z - first.Z, 10);
    }

    [Fact]
    public void NonRationalBoundaryRealizationIsCertifiedAndRepeatable()
    {
        var rib = HelicalRibGeometry.Create(Witness()).Value;
        var helix = rib.Boundary(HelicalRibBoundaryRole.LeadingCrest);
        var first = HelicalRibBoundaryRealizer.Realize(helix);
        var second = HelicalRibBoundaryRealizer.Realize(helix);
        Assert.True(first.IsSuccess, string.Join("; ", first.Diagnostics));
        Assert.True(second.IsSuccess);
        Assert.InRange(first.Value.CertifiedDeviationBoundMm, 0d, 1e-6d);
        Assert.Equal(first.Value.Curve.ControlPoints, second.Value.Curve.ControlPoints);
        Assert.True(first.Value.SegmentCount > 20);
        for (var i = 0; i <= 80; i++)
        {
            var angle = helix.StartAngleRadians + (helix.EndAngleRadians - helix.StartAngleRadians) * i / 80d;
            var deviation = (first.Value.Curve.Evaluate(angle) - helix.Evaluate(angle)).Length;
            Assert.InRange(deviation, 0d, first.Value.CertifiedDeviationBoundMm);
        }
    }

    [Fact]
    public void AllThreeRuledHelicalSidesRemainWithinCertifiedSurfaceBound()
    {
        var rib = HelicalRibGeometry.Create(Witness()).Value;
        foreach (var role in Enum.GetValues<HelicalRibSideRole>())
        {
            var realized = HelicalRibSurfaceRealizer.Realize(rib, role);
            Assert.True(realized.IsSuccess, string.Join("; ", realized.Diagnostics));
            Assert.InRange(realized.Value.CertifiedDeviationBoundMm, 0d, 1e-6d);
            Assert.False(realized.Value.Surface.IsRational);
            for (var i = 0; i <= 40; i++)
            {
                var angle = rib.Parameters.StartAngleRadians
                    + (rib.EndAngleRadians - rib.Parameters.StartAngleRadians) * i / 40d;
                foreach (var v in new[] { 0d, 0.3d, 1d })
                {
                    var error = (realized.Value.Surface.Evaluate(angle, v) - rib.Evaluate(role, angle, v)).Length;
                    Assert.InRange(error, 0d, realized.Value.CertifiedDeviationBoundMm);
                }
            }
        }
    }
}
