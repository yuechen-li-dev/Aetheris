using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Cir;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Sampling;

public enum GeometrySamplePattern
{
    SubcellCenters,
}

public readonly record struct GeometrySample(Point3D Position, ContinuumPointClassification Classification)
{
    public bool IsMaterial => Classification != ContinuumPointClassification.Outside;
}

/// <summary>Geometry-only occupancy evidence. This is not solver quadrature.</summary>
public sealed record GeometrySamplePlan(
    GeometrySamplePattern Pattern,
    int SamplesPerAxis,
    IReadOnlyList<GeometrySample> Samples,
    IReadOnlyList<BoundaryReference> BoundaryCandidates,
    double CoverageEstimate)
{
    public int GeometrySampleCount => Samples.Count;
}

public static class GeometrySampler
{
    public static GeometrySamplePlan Sample(
        IContinuumRegion region,
        BoundingBox3D bounds,
        int samplesPerAxis = 2)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (samplesPerAxis < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(samplesPerAxis));
        }

        var samples = new List<GeometrySample>(samplesPerAxis * samplesPerAxis * samplesPerAxis);
        var dx = (bounds.Max.X - bounds.Min.X) / samplesPerAxis;
        var dy = (bounds.Max.Y - bounds.Min.Y) / samplesPerAxis;
        var dz = (bounds.Max.Z - bounds.Min.Z) / samplesPerAxis;
        var occupiedMeasure = 0d;

        for (var k = 0; k < samplesPerAxis; k++)
        for (var j = 0; j < samplesPerAxis; j++)
        for (var i = 0; i < samplesPerAxis; i++)
        {
            var point = new Point3D(
                bounds.Min.X + ((i + 0.5d) * dx),
                bounds.Min.Y + ((j + 0.5d) * dy),
                bounds.Min.Z + ((k + 0.5d) * dz));
            var classification = region.Classify(point);
            occupiedMeasure += classification switch
            {
                ContinuumPointClassification.Inside => 1d,
                ContinuumPointClassification.Boundary => 0.5d,
                _ => 0d,
            };

            samples.Add(new GeometrySample(point, classification));
        }

        var boundaries = region is IBoundaryReferenceCapability capability
            ? capability.BoundaryCandidates(bounds)
            : [];
        return new GeometrySamplePlan(
            GeometrySamplePattern.SubcellCenters,
            samplesPerAxis,
            samples,
            boundaries,
            occupiedMeasure / samples.Count);
    }
}
