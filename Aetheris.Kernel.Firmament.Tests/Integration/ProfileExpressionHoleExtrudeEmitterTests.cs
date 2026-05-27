using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests.Integration;

public class ProfileExpressionHoleExtrudeEmitterTests
{
    [Theory]
    [MemberData(nameof(SuccessCases))]
    public void Success_cases_emit_expected_topology_and_step(object requestObj, int holeCount)
    {
        var request = (ProfileExpressionHoleExtrudeRequest)requestObj;
        var result = ProfileExpressionHoleExtrudeEmitter.TryEmit(request);
        Assert.Equal(ProfileExpressionHoleExtrudeStatus.Succeeded, result.Status);
        Assert.NotNull(result.Body);
        Assert.Contains("v2-v3-profile-expression-frontdoor-attempted", result.Diagnostics);
        Assert.Contains("v2-v3-profile-expression-normalized", result.Diagnostics);
        Assert.Contains("v2-v3-no-3d-boolean-used", result.Diagnostics);

        var body = result.Body!;
        var planar = body.Geometry.Surfaces.Count(x => x.Value.Kind == SurfaceGeometryKind.Plane);
        var cyl = body.Geometry.Surfaces.Count(x => x.Value.Kind == SurfaceGeometryKind.Cylinder);
        Assert.Equal(6, planar);
        Assert.Equal(holeCount, cyl);

        var step = Step242Exporter.ExportBody(body);
        Assert.True(step.IsSuccess);
        var text = step.Value!;
        Assert.Contains("ISO-10303-21", text);
        Assert.Contains("MANIFOLD_SOLID_BREP", text);
        Assert.Contains("ADVANCED_FACE", text);
        Assert.Contains("PLANE", text);
        Assert.Contains("CYLINDRICAL_SURFACE", text);
        Assert.DoesNotContain("BREP_WITH_VOIDS", text);
    }

    [Theory]
    [MemberData(nameof(RejectedCases))]
    public void Invalid_cases_reject_before_brep(object requestObj, string expected)
    {
        var request = (ProfileExpressionHoleExtrudeRequest)requestObj;
        var result = ProfileExpressionHoleExtrudeEmitter.TryEmit(request);
        Assert.Equal(ProfileExpressionHoleExtrudeStatus.Rejected, result.Status);
        Assert.Null(result.Body);
        Assert.Contains(result.Diagnostics, x => x == $"v2-v3-profile-expression-rejected:{expected}");
        Assert.DoesNotContain("v2-v3-profile-hole-extrude-succeeded", result.Diagnostics);
    }

    [Theory]
    [MemberData(nameof(DeferredCases))]
    public void Deferred_cases_stop_before_emission(object requestObj)
    {
        var request = (ProfileExpressionHoleExtrudeRequest)requestObj;
        var result = ProfileExpressionHoleExtrudeEmitter.TryEmit(request);
        Assert.Equal(ProfileExpressionHoleExtrudeStatus.Deferred, result.Status);
        Assert.Null(result.Body);
        Assert.DoesNotContain("v2-v3-profile-hole-extrude-succeeded", result.Diagnostics);
    }

    public static IEnumerable<object[]> SuccessCases()
    {
        yield return [new ProfileExpressionHoleExtrudeRequest(new ProfileDifferenceExpr2D(new ProfileRectangleExpr2D(0, 0, 20, 20), [new ProfileCircleExpr2D(0, 0, 3)]), 10), 1];
        yield return [new ProfileExpressionHoleExtrudeRequest(new ProfileDifferenceExpr2D(new ProfileRectangleExpr2D(0, 0, 30, 20), [new ProfileCircleExpr2D(5, 2, 2)]), 8), 1];
        yield return [new ProfileExpressionHoleExtrudeRequest(new ProfileDifferenceExpr2D(new ProfileRectangleExpr2D(0, 0, 30, 20), [new ProfileCircleExpr2D(-5, 0, 2), new ProfileCircleExpr2D(5, 0, 2)]), 8), 2];
    }

    public static IEnumerable<object[]> RejectedCases()
    {
        yield return [new ProfileExpressionHoleExtrudeRequest(new ProfileDifferenceExpr2D(new ProfileRectangleExpr2D(0, 0, 20, 20), [new ProfileCircleExpr2D(20, 20, 2)]), 10), "circle-outside-rectangle"];
        yield return [new ProfileExpressionHoleExtrudeRequest(new ProfileDifferenceExpr2D(new ProfileRectangleExpr2D(0, 0, 20, 20), [new ProfileCircleExpr2D(7, 0, 3)]), 10), "circle-touches-boundary"];
        yield return [new ProfileExpressionHoleExtrudeRequest(new ProfileDifferenceExpr2D(new ProfileRectangleExpr2D(0, 0, 30, 20), [new ProfileCircleExpr2D(0, 0, 3), new ProfileCircleExpr2D(4, 0, 3)]), 8), "circles-overlap-touch"];
        yield return [new ProfileExpressionHoleExtrudeRequest(new ProfileDifferenceExpr2D(new ProfileRectangleExpr2D(0, 0, 20, 20), [new ProfileCircleExpr2D(0, 0, 0)]), 10), "invalid-circle"];
        yield return [new ProfileExpressionHoleExtrudeRequest(new ProfileDifferenceExpr2D(new ProfileRectangleExpr2D(0, 0, 0, 20), [new ProfileCircleExpr2D(0, 0, 2)]), 10), "invalid-rectangle"];
        yield return [new ProfileExpressionHoleExtrudeRequest(new ProfileDifferenceExpr2D(new ProfileRectangleExpr2D(0, 0, 20, 20), [new ProfileCircleExpr2D(0, 0, 2)]), 0), "emitter-validation-failure"];
        yield return [new ProfileExpressionHoleExtrudeRequest(new ProfileUnionExpr2D([new ProfileRectangleExpr2D(0, 0, 10, 10), new ProfileRectangleExpr2D(0, 0, 8, 8)]), 10), "unsupported-operation"];
    }

    public static IEnumerable<object[]> DeferredCases()
    {
        yield return [new ProfileExpressionHoleExtrudeRequest(new ProfileDifferenceExpr2D(new ProfileRectangleExpr2D(0, 0, 20, 20), [new ProfileCapsuleExpr2D(0, 0, 10, 2)]), 10)];
        yield return [new ProfileExpressionHoleExtrudeRequest(new ProfileDifferenceExpr2D(new ProfileRectangleExpr2D(0, 0, 20, 20), [new ProfileRectangleExpr2D(5, 0, 2, 40)]), 10)];
    }
}
