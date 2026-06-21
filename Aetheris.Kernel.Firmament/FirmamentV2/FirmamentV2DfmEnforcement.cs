using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

internal static class FirmamentV2DfmEnforcement
{
    public static KernelResult<object> Validate(FirmamentV2Document document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var diagnostics = new List<KernelDiagnostic>();
        foreach (var template in document.Templates ?? [])
        {
            if (!string.Equals(template.Process, "CNC", StringComparison.Ordinal)) continue;
            foreach (var concept in template.Concepts.Where(c => string.Equals(c.Name, "minimumToolRadius", StringComparison.Ordinal)))
            {
                if (!string.Equals(concept.Unit, document.Units, StringComparison.Ordinal))
                {
                    diagnostics.Add(Error($"{FirmamentV2Parser.DfmConceptUnitMismatch}: template '{template.Name}' concept '{concept.Name}' expects length unit '{document.Units}' but found '{concept.Unit ?? "<unitless>"}' in '{concept.RawValue}'."));
                    continue;
                }

                foreach (var (featureName, radius) in EnumerateHoleRadii(document))
                {
                    if (radius < concept.NumericValue)
                    {
                        diagnostics.Add(Error($"{FirmamentV2Parser.DfmMinimumToolRadiusViolation}: template '{template.Name}' concept '{concept.Name}' requires minimum tool radius {concept.NumericValue:0.###}{document.Units}; feature '{featureName}' has radius {radius:0.###}{document.Units}."));
                    }
                }
            }
        }

        return diagnostics.Count == 0
            ? KernelResult<object>.Success(new object())
            : KernelResult<object>.Failure(diagnostics);
    }

    private static IEnumerable<(string FeatureName, double Radius)> EnumerateHoleRadii(FirmamentV2Document document)
    {
        foreach (var modify in document.ModifyBlocks ?? [])
        foreach (var hole in modify.SemanticHoles)
        {
            yield return ($"{modify.TargetSolid}.{hole.Name}.shaft", hole.ShaftDiameter / 2d);
            if (hole.CounterboreDiameter is { } cb) yield return ($"{modify.TargetSolid}.{hole.Name}.counterbore", cb / 2d);
            if (hole.CountersinkDiameter is { } cs) yield return ($"{modify.TargetSolid}.{hole.Name}.countersink", cs / 2d);
        }
    }

    private static KernelDiagnostic Error(string message) =>
        new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message, "FirmamentV2.DfmEnforcement");
}
