using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Cir;

public enum CirPointClassification
{
    Inside,
    Outside,
    Boundary
}

public sealed record CirAnalyzerResult(CirPointClassification Classification, double SignedDistance);

public static class CirAnalyzer
{
    public static CirAnalyzerResult ClassifyPoint(CirNode node, Point3D point, double boundaryTolerance = 1e-6)
    {
        ArgumentNullException.ThrowIfNull(node);
        var value = node.Evaluate(point);
        var classification = double.Abs(value) <= boundaryTolerance
            ? CirPointClassification.Boundary
            : value < 0d
                ? CirPointClassification.Inside
                : CirPointClassification.Outside;
        return new CirAnalyzerResult(classification, value);
    }

    public static double EstimateVolume(CirNode node, int resolution) => CirVolumeEstimator.EstimateVolume(node, resolution);
}
