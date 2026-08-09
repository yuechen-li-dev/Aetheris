using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Backends.Sdf;

public enum SdfPointClassification
{
    Inside,
    Outside,
    Boundary
}

public sealed record SdfAnalyzerResult(SdfPointClassification Classification, double SignedDistance);

public static class SdfAnalyzer
{
    public static SdfAnalyzerResult ClassifyPoint(SdfNode node, Point3D point, double boundaryTolerance = 1e-6)
    {
        ArgumentNullException.ThrowIfNull(node);
        var value = node.Evaluate(point);
        var classification = double.Abs(value) <= boundaryTolerance
            ? SdfPointClassification.Boundary
            : value < 0d
                ? SdfPointClassification.Inside
                : SdfPointClassification.Outside;
        return new SdfAnalyzerResult(classification, value);
    }

    public static double EstimateVolume(SdfNode node, int resolution) => SdfVolumeEstimator.EstimateVolume(node, resolution);
}
