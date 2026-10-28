using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Math;

// Frozen interval-only control from e1310ce2. Qualification only; never production dispatch.
internal static class IntervalOnlyControl
{
    public static SdfBoundedVolumeResult EstimateBounded(SdfTape tape, SdfBounds bounds, double absoluteError,
        int maximumRegions = 100_001, int maximumDepth = 30)
    {
        ArgumentNullException.ThrowIfNull(tape);
        var enclosingVolume = RegionVolume(bounds);
        if (!double.IsFinite(absoluteError) || absoluteError < 0 || maximumRegions < 1 || maximumDepth < 0
            || !double.IsFinite(enclosingVolume) || enclosingVolume <= 0
            || bounds.SizeX <= 0 || bounds.SizeY <= 0 || bounds.SizeZ <= 0)
            throw new ArgumentException("Bounded volume requires finite positive bounds, a nonnegative error and a positive region budget.");
        var queue = new PriorityQueue<(SdfBounds Region, int Depth), (double Volume, int Order)>();
        double inside = 0, uncertain = 0;
        var visited = 0;
        var padding = 1e-12 * Math.Max(1, new[] { bounds.Min.X, bounds.Min.Y, bounds.Min.Z, bounds.Max.X, bounds.Max.Y, bounds.Max.Z }.Max(Math.Abs));
        void Admit(SdfBounds region, int depth)
        {
            var interval = tape.EvaluateInterval(region);
            var volume = RegionVolume(region);
            visited++;
            if (!double.IsFinite(interval.MinValue) || !double.IsFinite(interval.MaxValue) || interval.MinValue > interval.MaxValue)
                throw new InvalidOperationException("sdf-volume-interval-invalid: The field returned no finite conservative interval.");
            if (interval.MaxValue < -padding) inside += volume;
            else if (interval.MinValue <= padding)
            {
                uncertain += volume;
                if (depth < maximumDepth) queue.Enqueue((region, depth), (-volume, visited));
            }
        }
        Admit(bounds, 0);
        while (uncertain > 2 * absoluteError && queue.Count > 0 && visited + 2 <= maximumRegions)
        {
            var item = queue.Dequeue();
            uncertain -= RegionVolume(item.Region);
            foreach (var child in SplitLongestAxis(item.Region)) Admit(child, item.Depth + 1);
        }
        var roundoff = enclosingVolume * 1e-12;
        var lower = Math.Max(0, inside - roundoff);
        var upper = Math.Min(enclosingVolume, inside + Math.Max(0, uncertain) + roundoff);
        return new(lower, upper, visited, upper - lower > 2 * absoluteError);
    }

    private static double RegionVolume(SdfBounds region) => double.Max(region.SizeX, 0d) * double.Max(region.SizeY, 0d) * double.Max(region.SizeZ, 0d);

    private static IReadOnlyList<SdfBounds> SplitLongestAxis(SdfBounds region)
    {
        var sizeX = region.SizeX;
        var sizeY = region.SizeY;
        var sizeZ = region.SizeZ;

        if (sizeX >= sizeY && sizeX >= sizeZ)
        {
            var mid = (region.Min.X + region.Max.X) * 0.5d;
            return
            [
                new SdfBounds(region.Min, new Point3D(mid, region.Max.Y, region.Max.Z)),
                new SdfBounds(new Point3D(mid, region.Min.Y, region.Min.Z), region.Max),
            ];
        }

        if (sizeY >= sizeX && sizeY >= sizeZ)
        {
            var mid = (region.Min.Y + region.Max.Y) * 0.5d;
            return
            [
                new SdfBounds(region.Min, new Point3D(region.Max.X, mid, region.Max.Z)),
                new SdfBounds(new Point3D(region.Min.X, mid, region.Min.Z), region.Max),
            ];
        }

        var midZ = (region.Min.Z + region.Max.Z) * 0.5d;
        return
        [
            new SdfBounds(region.Min, new Point3D(region.Max.X, region.Max.Y, midZ)),
            new SdfBounds(new Point3D(region.Min.X, region.Min.Y, midZ), region.Max),
        ];
    }

}

