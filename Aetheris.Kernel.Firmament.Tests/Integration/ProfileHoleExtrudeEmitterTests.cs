using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests.Integration;

public class ProfileHoleExtrudeEmitterTests
{
    [Theory]
    [MemberData(nameof(ValidCases))]
    public void Emitter_ValidCases_EmitExpectedTopology(object requestObj, int holes)
    {
        var req = (ProfileHoleExtrudeRequest)requestObj;
        var result = ProfileHoleExtrudeEmitter.TryEmit(req);
        Assert.Equal(ProfileHoleExtrudeStatus.Succeeded, result.Status);
        Assert.NotNull(result.Body);
        Assert.Contains("v2-v1-profile-hole-extrude-no-3d-boolean-subtract", result.Diagnostics);

        var body = result.Body!;
        var planar = body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Plane);
        var cyl = body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Cylinder);
        Assert.Equal(6, planar);
        Assert.Equal(holes, cyl);

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
    [MemberData(nameof(InvalidCases))]
    public void Emitter_InvalidCases_RejectBeforeBrep(object requestObj)
    {
        var req = (ProfileHoleExtrudeRequest)requestObj;
        var result = ProfileHoleExtrudeEmitter.TryEmit(req);
        Assert.Equal(ProfileHoleExtrudeStatus.Rejected, result.Status);
        Assert.Null(result.Body);
        Assert.Contains(result.Diagnostics, x => x.StartsWith("v2-v1-profile-hole-extrude-rejected:"));
    }

    public static IEnumerable<object[]> ValidCases()
    {
        yield return [new ProfileHoleExtrudeRequest(20, 20, 10, [new(0, 0, 3)]), 1];
        yield return [new ProfileHoleExtrudeRequest(30, 20, 8, [new(4, 2, 2)]), 1];
        yield return [new ProfileHoleExtrudeRequest(30, 20, 8, [new(-6, 0, 2), new(6, 0, 2)]), 2];
    }

    public static IEnumerable<object[]> InvalidCases()
    {
        yield return [new ProfileHoleExtrudeRequest(20, 20, 10, [new(20, 20, 2)])];
        yield return [new ProfileHoleExtrudeRequest(20, 20, 10, [new(7, 0, 3)])];
        yield return [new ProfileHoleExtrudeRequest(30, 20, 8, [new(-2, 0, 3), new(2, 0, 3)])];
        yield return [new ProfileHoleExtrudeRequest(20, 20, 0, [new(0, 0, 3)])];
        yield return [new ProfileHoleExtrudeRequest(20, 20, 8, [new(0, 0, 0)])];
    }
}
