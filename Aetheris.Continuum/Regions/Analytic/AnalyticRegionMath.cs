using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Cir;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Regions.Analytic;

internal static class AnalyticRegionMath
{
    public static bool HasPositiveIntersection(BoundingBox3D left, BoundingBox3D right, double tolerance) =>
        double.Min(left.Max.X, right.Max.X) - double.Max(left.Min.X, right.Min.X) > tolerance
        && double.Min(left.Max.Y, right.Max.Y) - double.Max(left.Min.Y, right.Min.Y) > tolerance
        && double.Min(left.Max.Z, right.Max.Z) - double.Max(left.Min.Z, right.Min.Z) > tolerance;

    public static bool Contains(BoundingBox3D outer, BoundingBox3D inner, double tolerance) =>
        inner.Min.X >= outer.Min.X - tolerance && inner.Max.X <= outer.Max.X + tolerance
        && inner.Min.Y >= outer.Min.Y - tolerance && inner.Max.Y <= outer.Max.Y + tolerance
        && inner.Min.Z >= outer.Min.Z - tolerance && inner.Max.Z <= outer.Max.Z + tolerance;

    public static IReadOnlyList<BoundaryReference> BoxBoundaryCandidates(RegionId id, BoundingBox3D box, BoundingBox3D cell)
    {
        var candidates = new List<BoundaryReference>();
        AddIfSpans(candidates, cell.Min.X, cell.Max.X, box.Min.X, new BoundaryReference("analytic", $"{id}:x-min"));
        AddIfSpans(candidates, cell.Min.X, cell.Max.X, box.Max.X, new BoundaryReference("analytic", $"{id}:x-max"));
        AddIfSpans(candidates, cell.Min.Y, cell.Max.Y, box.Min.Y, new BoundaryReference("analytic", $"{id}:y-min"));
        AddIfSpans(candidates, cell.Min.Y, cell.Max.Y, box.Max.Y, new BoundaryReference("analytic", $"{id}:y-max"));
        AddIfSpans(candidates, cell.Min.Z, cell.Max.Z, box.Min.Z, new BoundaryReference("analytic", $"{id}:z-min"));
        AddIfSpans(candidates, cell.Min.Z, cell.Max.Z, box.Max.Z, new BoundaryReference("analytic", $"{id}:z-max"));
        return candidates;
    }

    private static void AddIfSpans(List<BoundaryReference> candidates, double minimum, double maximum, double value, BoundaryReference reference)
    {
        if (minimum <= value && maximum >= value)
        {
            candidates.Add(reference);
        }
    }
}
