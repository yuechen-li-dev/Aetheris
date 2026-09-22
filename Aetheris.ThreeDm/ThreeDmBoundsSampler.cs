using Rhino.Geometry;

namespace Aetheris.ThreeDm;

/// <summary>
/// A bounded envelope of trimmed edge samples. Rhino's BRep bounding box can
/// include much larger, untrimmed support surfaces; this sample is useful for
/// component navigation but is not a certified surface extrema search.
/// </summary>
internal static class ThreeDmBoundsSampler
{
    public static BoundingBox Sample(Brep brep)
    {
        var minX = double.PositiveInfinity; var minY = double.PositiveInfinity; var minZ = double.PositiveInfinity;
        var maxX = double.NegativeInfinity; var maxY = double.NegativeInfinity; var maxZ = double.NegativeInfinity;
        foreach (var edge in brep.Edges)
        {
            var domain = edge.Domain;
            const int count = 129;
            for (var i = 0; i < count; i++)
            {
                var point = edge.PointAt(domain.T0 + (domain.T1 - domain.T0) * i / (count - 1d));
                if (!point.IsValid) continue;
                minX = double.Min(minX, point.X); minY = double.Min(minY, point.Y); minZ = double.Min(minZ, point.Z);
                maxX = double.Max(maxX, point.X); maxY = double.Max(maxY, point.Y); maxZ = double.Max(maxZ, point.Z);
            }
        }
        return double.IsFinite(minX)
            ? new BoundingBox(new Point3d(minX, minY, minZ), new Point3d(maxX, maxY, maxZ))
            : brep.GetBoundingBox(true);
    }
}
