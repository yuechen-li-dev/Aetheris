using Aetheris.Kernel.Core.Geometry.Surfaces;

namespace Aetheris.Kernel.Core.Step242;

internal enum Step242BsplineRuledDirection
{
    None,
    U,
    V,
    Both,
}

internal enum Step242BsplineRuledExactness
{
    None,
    ExactRuled,
}

internal readonly record struct Step242BsplineRuledClassification(
    bool IsRuledCandidate,
    Step242BsplineRuledDirection RulingDirection,
    bool IsBilinearPatch,
    Step242BsplineRuledExactness Exactness,
    string Reason);

internal static class Step242BsplineRuledClassifier
{
    public static Step242BsplineRuledClassification Classify(BSplineSurfaceWithKnots surface)
    {
        var ruledInU = surface.DegreeU == 1;
        var ruledInV = surface.DegreeV == 1;
        var direction = (ruledInU, ruledInV) switch
        {
            (true, true) => Step242BsplineRuledDirection.Both,
            (true, false) => Step242BsplineRuledDirection.U,
            (false, true) => Step242BsplineRuledDirection.V,
            _ => Step242BsplineRuledDirection.None
        };

        var isRuledCandidate = direction != Step242BsplineRuledDirection.None;
        var isBilinearPatch = ruledInU && ruledInV;
        var exactness = isRuledCandidate
            ? Step242BsplineRuledExactness.ExactRuled
            : Step242BsplineRuledExactness.None;

        var reason = direction switch
        {
            Step242BsplineRuledDirection.Both => "Degrees (1,1) form an exact bilinear ruled patch.",
            Step242BsplineRuledDirection.U => "Degree 1 in U marks an exact ruled candidate in the U direction.",
            Step242BsplineRuledDirection.V => "Degree 1 in V marks an exact ruled candidate in the V direction.",
            _ => "Both spline degrees exceed 1, so ruled exactness cannot be concluded from degree alone."
        };

        return new Step242BsplineRuledClassification(
            isRuledCandidate,
            direction,
            isBilinearPatch,
            exactness,
            reason);
    }
}
