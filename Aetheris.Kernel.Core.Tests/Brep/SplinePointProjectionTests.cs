using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Brep;

/// <summary>
/// Inverse projection of a point that is not on the surface (a tolerant edge's gap): the result is the foot point,
/// in the interior and on the domain boundary, found by the projection's own convergence rather than a pattern search.
/// </summary>
public sealed class SplinePointProjectionTests
{
    private static BSplineSurfaceWithKnots Patch() => new(3, 2,
        Enumerable.Range(0, 4).Select(i => (IReadOnlyList<Point3D>)Enumerable.Range(0, 3)
            .Select(j => new Point3D(10 + 15 * i + .2 * j * j, -7 + 9 * j + .3 * i * i, 4 + .7 * i * j)).ToArray()).ToArray(),
        "UNSPECIFIED", false, false, false, [4, 4], [3, 3], [-4d, 3d], [2d, 7d], "UNSPECIFIED");

    private static Vector3D Normal(BSplineSurfaceWithKnots patch, double u, double v)
    {
        const double h = 1e-6;
        var du = patch.Evaluate(u + h, v) - patch.Evaluate(u - h, v);
        var dv = patch.Evaluate(u, v + h) - patch.Evaluate(u, v - h);
        var normal = du.Cross(dv);
        return normal * (1d / normal.Length);
    }

    /// <summary>The smallest distance over a dense grid, refined around its best cell: an independent foot point.</summary>
    private static double BruteForceDistance(BSplineSurfaceWithKnots patch, Point3D target)
    {
        var best = double.PositiveInfinity; var bestU = 0d; var bestV = 0d;
        var (u0, u1, v0, v1) = (patch.DomainStartU, patch.DomainEndU, patch.DomainStartV, patch.DomainEndV);
        for (var round = 0; round < 12; round++)
        {
            for (var i = 0; i <= 40; i++) for (var j = 0; j <= 40; j++)
            {
                var u = u0 + (u1 - u0) * i / 40d; var v = v0 + (v1 - v0) * j / 40d;
                var d = (patch.Evaluate(u, v) - target).Length;
                if (d < best) { best = d; bestU = u; bestV = v; }
            }
            var su = (u1 - u0) / 20d; var sv = (v1 - v0) / 20d;
            (u0, u1) = (double.Max(patch.DomainStartU, bestU - su), double.Min(patch.DomainEndU, bestU + su));
            (v0, v1) = (double.Max(patch.DomainStartV, bestV - sv), double.Min(patch.DomainEndV, bestV + sv));
        }
        return best;
    }

    [Theory]
    [InlineData(-1.3, 4.1, 5e-6)]
    [InlineData(0.7, 2.9, 4e-4)]
    [InlineData(2.2, 6.3, 1e-3)]
    public void OffSurfaceInteriorTarget_ProjectsToItsFootPoint(double u, double v, double gap)
    {
        var patch = Patch();
        var target = patch.Evaluate(u, v) + Normal(patch, u, v) * gap;
        var seed = new SurfaceParameterPoint(u + .05, v - .05);
        var result = BrepPcurveRecovery.ProjectSplinePoint(patch, target, seed, 1e-2);
        Assert.True(result.Success);
        Assert.Equal(gap, result.Residual, 9);
        Assert.True(double.Abs(result.Uv.U - u) <= 1e-6 && double.Abs(result.Uv.V - v) <= 1e-6, $"{result.Uv} vs ({u}, {v})");
    }

    [Theory]
    [InlineData(true, 4.4)]
    [InlineData(false, -2.5)]
    public void TargetBeyondTheDomain_ProjectsToTheBoundaryFootPoint(bool beyondU, double along)
    {
        var patch = Patch();
        // Outside the parameter rectangle: the closest point is a constrained minimum on one boundary.
        var edge = beyondU ? patch.Evaluate(patch.DomainEndU, along) : patch.Evaluate(along, patch.DomainStartV);
        var outward = beyondU
            ? patch.Evaluate(patch.DomainEndU, along) - patch.Evaluate(patch.DomainEndU - 1e-3, along)
            : patch.Evaluate(along, patch.DomainStartV) - patch.Evaluate(along, patch.DomainStartV + 1e-3);
        var target = edge + outward * (2e-3 / outward.Length);
        var result = BrepPcurveRecovery.ProjectSplinePoint(patch, target, new SurfaceParameterPoint(beyondU ? 2.5 : along + .1, beyondU ? along - .1 : 2.4), 1e-2);
        Assert.True(result.Success);
        if (beyondU) Assert.Equal(patch.DomainEndU, result.Uv.U); else Assert.Equal(patch.DomainStartV, result.Uv.V);
        Assert.True(result.Residual <= BruteForceDistance(patch, target) + 1e-10, $"{result.Residual} vs brute force");
    }

    [Fact]
    public void DistantSeed_StillReachesTheTarget()
    {
        // A seed from the far corner of the patch, as a carried seed can be after a jump along a trace.
        var patch = Patch();
        var target = patch.Evaluate(2.6, 6.6);
        var result = BrepPcurveRecovery.ProjectSplinePoint(patch, target, new SurfaceParameterPoint(-3.9, 2.1), 1e-6);
        Assert.True(result.Success, $"residual {result.Residual}");
        Assert.True(result.Residual <= 1e-6);
    }
}
