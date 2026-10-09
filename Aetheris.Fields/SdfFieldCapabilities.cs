namespace Aetheris.Continuum.Backends.Sdf;

[Flags]
public enum SdfFieldCapabilities
{
    None = 0,
    SignCorrectOccupancy = 1,
    ConservativeIntervals = 2,
    ExactEuclideanSignedDistance = 4,
    Gradient = 8,
}

/// <summary>Computes promises preserved by a concrete SDF expression. Bounds for intersections are conservative, never advertised as tight.</summary>
public static class SdfCapabilityAnalyzer
{
    public static SdfFieldCapabilities Analyze(SdfNode node) => node switch
    {
        SdfBoxNode or SdfCylinderNode or SdfConeNode or SdfSphereNode or SdfTorusNode =>
            SdfFieldCapabilities.SignCorrectOccupancy | SdfFieldCapabilities.ConservativeIntervals |
            SdfFieldCapabilities.ExactEuclideanSignedDistance | SdfFieldCapabilities.Gradient,
        SdfTransformNode transformed => Transform(Analyze(transformed.Child), transformed.Transform.IsRigid()),
        SdfUnionNode union => Composition(Analyze(union.Left), Analyze(union.Right)),
        SdfIntersectNode intersection => Composition(Analyze(intersection.Left), Analyze(intersection.Right)),
        SdfSubtractNode subtraction => Composition(Analyze(subtraction.Left), Analyze(subtraction.Right)),
        _ => SdfFieldCapabilities.None,
    };

    private static SdfFieldCapabilities Transform(SdfFieldCapabilities child, bool rigid) => rigid
        ? child
        : child & ~SdfFieldCapabilities.ExactEuclideanSignedDistance;

    private static SdfFieldCapabilities Composition(SdfFieldCapabilities left, SdfFieldCapabilities right)
    {
        var shared = left & right;
        return shared & ~SdfFieldCapabilities.ExactEuclideanSignedDistance;
    }
}
