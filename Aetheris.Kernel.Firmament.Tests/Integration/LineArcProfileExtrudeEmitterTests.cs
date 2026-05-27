using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests.Integration;

public class LineArcProfileExtrudeEmitterTests
{
    [Theory]
    [MemberData(nameof(ValidCases))]
    public void EmitsExpectedTopology(string name, object reqObj, int planar, int cyl)
    {
        var req = (LineArcProfileExtrudeRequest)reqObj;
        var result = LineArcProfileExtrudeEmitter.TryEmit(req);
        Assert.Equal(LineArcProfileExtrudeStatus.Succeeded, result.Status);
        Assert.Contains("v2-v4-no-3d-boolean-used", result.Diagnostics);
        var body = Assert.IsType<Aetheris.Kernel.Core.Brep.BrepBody>(result.Body);
        Assert.Equal(planar + cyl, body.Topology.Faces.Count());
        var step = Step242Exporter.ExportBody(body);
        Assert.True(step.IsSuccess, name);
        var txt = step.Value!;
        Assert.Contains("ISO-10303-21", txt, StringComparison.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP", txt, StringComparison.Ordinal);
        Assert.DoesNotContain("BREP_WITH_VOIDS", txt, StringComparison.Ordinal);
        if (cyl > 0) Assert.Contains("CYLINDRICAL_SURFACE", txt, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsInvalidHeight()
    {
        var req = new LineArcProfileExtrudeRequest([RectOuter(20, 10)], 0);
        var result = LineArcProfileExtrudeEmitter.TryEmit(req);
        Assert.Equal(LineArcProfileExtrudeStatus.Rejected, result.Status);
        Assert.Null(result.Body);
        Assert.Contains(result.Diagnostics, x => x.Contains("invalid height", StringComparison.Ordinal));
    }

    public static IEnumerable<object[]> ValidCases()
    {
        yield return ["rectangle", new LineArcProfileExtrudeRequest([RectOuter(20, 10)], 5), 6, 0];
        yield return ["rectangle-circle", new LineArcProfileExtrudeRequest([RectOuter(20, 20), new LineArcProfileLoop2D([new LineArcFullCircle2D((0, 0), 3)], true)], 10), 6, 1];
        yield return ["rectangle-two-circles", new LineArcProfileExtrudeRequest([RectOuter(30, 20), new LineArcProfileLoop2D([new LineArcFullCircle2D((-5, 0), 2)], true), new LineArcProfileLoop2D([new LineArcFullCircle2D((5, 0), 2)], true)], 8), 6, 2];
        yield return ["rectangle-slot", new LineArcProfileExtrudeRequest([RectOuter(30, 20), HorizontalSlot(0, 0, 12, 2)], 8), 8, 2];
    }

    private static LineArcProfileLoop2D RectOuter(double w, double h)
    {
        var hw = w / 2d; var hh = h / 2d;
        return new([
            new LineArcLineSegment2D((-hw,-hh),(hw,-hh)),
            new LineArcLineSegment2D((hw,-hh),(hw,hh)),
            new LineArcLineSegment2D((hw,hh),(-hw,hh)),
            new LineArcLineSegment2D((-hw,hh),(-hw,-hh))], false);
    }

    private static LineArcProfileLoop2D HorizontalSlot(double cx, double cy, double len, double r)
    {
        var dx = (len / 2d) - r;
        return new([
            new LineArcLineSegment2D((cx - dx, cy + r), (cx + dx, cy + r)),
            new LineArcCircularArc2D((cx + dx, cy), r, Math.PI / 2d, -Math.PI),
            new LineArcLineSegment2D((cx + dx, cy - r), (cx - dx, cy - r)),
            new LineArcCircularArc2D((cx - dx, cy), r, -Math.PI / 2d, -Math.PI)
        ], true);
    }
}
