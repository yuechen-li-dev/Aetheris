using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

internal sealed record StateBackedDifferentialResult(bool Success, Point3D BrepMin, Point3D BrepMax, Point3D CirMin, Point3D CirMax, double BoundsMaxAbsDelta, double? CirEstimatedVolume);

internal static class NativeGeometryStateDifferentialHelper
{
    public static StateBackedDifferentialResult CompareBoundsAndVolumeFromState(NativeGeometryState state)
    {
        Assert.NotNull(state.MaterializedBody);
        Assert.Equal(CirMirrorStatus.Available, state.CirMirror.Status);
        Assert.NotNull(state.CirMirror.Summary);

        var (min, max) = GetBounds(state.MaterializedBody!);
        var cir = state.CirMirror.Summary!;
        var deltas = new[]
        {
            Math.Abs(min.X - cir.Min.X), Math.Abs(min.Y - cir.Min.Y), Math.Abs(min.Z - cir.Min.Z),
            Math.Abs(max.X - cir.Max.X), Math.Abs(max.Y - cir.Max.Y), Math.Abs(max.Z - cir.Max.Z)
        };

        return new StateBackedDifferentialResult(deltas.Max() < 1.0d, min, max, cir.Min, cir.Max, deltas.Max(), cir.EstimatedVolume);
    }

    private static (Point3D Min, Point3D Max) GetBounds(BrepBody body)
    {
        var points = body.Topology.Vertices
            .Select(v => body.TryGetVertexPoint(v.Id, out var p) ? p : (Point3D?)null)
            .Where(p => p is not null)
            .Select(p => p!.Value)
            .ToArray();

        return (
            new Point3D(points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z)),
            new Point3D(points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z)));
    }
}
