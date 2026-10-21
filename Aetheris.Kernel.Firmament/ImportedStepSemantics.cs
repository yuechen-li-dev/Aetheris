using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Firmament;

/// <summary>Detect annotation families before a geometry-only adapter can discard them.
/// This is not a foreign PMI interpreter or a promise of annotation passthrough.</summary>
internal static class ImportedStepSemantics
{
    internal static bool HasAnnotations(string text)
    {
        if (Step242SemanticPmiInspector.Inspect(text).Items.Count > 0) return true;
        var parsed = Step242SubsetParser.Parse(text);
        return parsed.IsSuccess && parsed.Value.Entities.Any(e => e.Name.EndsWith("_TOLERANCE", StringComparison.Ordinal)
            || e.Name.StartsWith("ANNOTATION_", StringComparison.Ordinal)
            || new[] { "DIMENSIONAL_SIZE", "DIMENSIONAL_LOCATION", "GEOMETRIC_TOLERANCE", "DATUM_FEATURE" }
                .Any(kind => Step242SubsetDecoder.TryGetConstructor(e.Instance, kind) is not null));
    }
}
