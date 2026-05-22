using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Brep.Boolean;

public enum HoleFeatureKind
{
    SimpleHole,
    Counterbore,
    Countersink
}

public readonly record struct HoleFeatureRecognition(
    string FeatureId,
    HoleFeatureKind Kind,
    SupportedBooleanHole PrimaryHole,
    SupportedBooleanHole? SecondaryHole = null);

public static class BrepBooleanHoleFeatureRecognition
{
    public static bool TryRecognizeFeature(
        SafeBooleanComposition composition,
        string featureId,
        IReadOnlyDictionary<string, int> opIndexByFeatureId,
        ToleranceContext tolerance,
        out HoleFeatureRecognition recognition,
        out bool isAmbiguous,
        out string reason)
    {
        recognition = default;
        isAmbiguous = false;
        reason = string.Empty;

        var featureHoles = composition.Holes
            .Where(hole => string.Equals(hole.FeatureId, featureId, StringComparison.Ordinal))
            .ToArray();
        if (composition.RootDescriptor.Kind != SafeBooleanRootKind.Box)
        {
            reason = "recognized hole-family auto-PMI is currently bounded to box-root subtract compositions.";
            return false;
        }

        if (featureHoles.Length != 1)
        {
            reason = featureHoles.Length == 0
                ? "feature is not represented as a recognized safe-composition hole."
                : "feature maps to multiple recognized hole segments.";
            isAmbiguous = featureHoles.Length > 1;
            return false;
        }

        var hole = featureHoles[0];
        var supportedFamilies = new List<HoleFeatureRecognition>();
        var participationCount = 0;

        foreach (var candidate in composition.Holes)
        {
            if (string.Equals(candidate.FeatureId, featureId, StringComparison.Ordinal))
            {
                continue;
            }

            if (BrepBooleanCoaxialSubtractStackFamily.TryClassifyPair(
                    composition.OuterBox,
                    hole,
                    candidate,
                    tolerance,
                    out _,
                    out _,
                    featureId))
            {
                participationCount++;
                if (IsCurrentFeatureOwner(featureId, candidate.FeatureId, opIndexByFeatureId))
                {
                    supportedFamilies.Add(new HoleFeatureRecognition(featureId, HoleFeatureKind.Counterbore, hole, candidate));
                }
            }

            if (BrepBooleanCoaxialCountersinkSubtractFamily.TryClassifyPair(
                    composition.OuterBox,
                    hole,
                    candidate,
                    tolerance,
                    out _,
                    out _,
                    featureId))
            {
                participationCount++;
                if (IsCurrentFeatureOwner(featureId, candidate.FeatureId, opIndexByFeatureId))
                {
                    supportedFamilies.Add(new HoleFeatureRecognition(featureId, HoleFeatureKind.Countersink, hole, candidate));
                }
            }
        }

        if (supportedFamilies.Count > 1 || participationCount > 1 && supportedFamilies.Count > 0)
        {
            isAmbiguous = true;
            reason = "feature participates in multiple hole-family pairings.";
            return false;
        }

        if (supportedFamilies.Count == 1)
        {
            recognition = supportedFamilies[0];
            return true;
        }

        if (participationCount > 0)
        {
            reason = "feature is semantically consumed by another hole-family continuation.";
            return false;
        }

        if (hole.Surface.Kind != AnalyticSurfaceKind.Cylinder)
        {
            reason = "feature is not a simple cylindrical hole.";
            return false;
        }

        recognition = new HoleFeatureRecognition(featureId, HoleFeatureKind.SimpleHole, hole);
        return true;
    }

    private static bool IsCurrentFeatureOwner(
        string currentFeatureId,
        string? otherFeatureId,
        IReadOnlyDictionary<string, int> opIndexByFeatureId)
    {
        if (string.IsNullOrWhiteSpace(otherFeatureId))
        {
            return true;
        }

        if (!opIndexByFeatureId.TryGetValue(currentFeatureId, out var currentOp))
        {
            return true;
        }

        if (!opIndexByFeatureId.TryGetValue(otherFeatureId, out var otherOp))
        {
            return true;
        }

        return currentOp >= otherOp;
    }
}
