using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class StepLoopOrientationTests
{
    [Fact]
    public void ExtrudedBoxCapsWindAboutTheirOutwardNormals()
        => AssertEachEdgeUsedOnceEachWay(Step242Exporter.ExportBody(BrepPrimitives.CreateBox(80, 50, 25).Value).Value);

    [Fact]
    public void BuildersWithInconsistentLoopsExportConsistentOnes()
    {
        // The box-and-hole builder winds both caps and their holes about +Z; the writer derives the direction.
        var box = new AxisAlignedBoxExtents(-20, 20, -15, 15, 0, 6);
        var axis = Direction3D.Create(new Vector3D(0, 0, 1));
        var cylinder = new RecognizedCylinder(new Point3D(0, 0, 0), axis, 4, 0, 6);
        var hole = new SupportedBooleanHole("Hole", new AnalyticSurface(AnalyticSurfaceKind.Cylinder, Cylinder: cylinder), 0, 0,
            new Point3D(0, 0, 0), new Point3D(0, 0, 6), axis, Direction3D.Create(new Vector3D(1, 0, 0)), 4, 4, SupportedBooleanHoleSpanKind.Through, 0, 6);
        var body = BrepBooleanBoxCylinderHoleBuilder.BuildComposition(new SafeBooleanComposition(box, [hole]), ToleranceContext.Default);
        Assert.True(body.IsSuccess, string.Join(" | ", body.Diagnostics.Select(d => d.Message)));
        var step = Step242Exporter.ExportBody(body.Value!);
        Assert.True(step.IsSuccess);
        AssertEachEdgeUsedOnceEachWay(step.Value);
        Assert.DoesNotContain(step.Diagnostics, d => d.Message.StartsWith("step-export-face-normal-inverted", StringComparison.Ordinal));
    }

    private static void AssertEachEdgeUsedOnceEachWay(string step)
    {
        var uses = Regex.Matches(step, @"ORIENTED_EDGE\(\$,\$,\$,#(\d+),\.(T|F)\.\)")
            .GroupBy(m => m.Groups[1].Value, m => m.Groups[2].Value);
        Assert.NotEmpty(uses);
        Assert.All(uses, edge => Assert.Equal(["F", "T"], edge.Order(StringComparer.Ordinal)));
    }
}
