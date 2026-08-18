namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>
/// Adapts canonical typed policy structs and the historical lowercase template syntax
/// to one DFM-facing representation. Canonical policy structs take precedence whenever
/// they are present; lowercase templates remain a compatibility input only.
/// </summary>
internal static class FirmamentManufacturingPolicyResolver
{
    internal sealed record Policy(
        string Name,
        string Process,
        IReadOnlyDictionary<string, ConceptIrValue> Members,
        string Source);

    public static IReadOnlyList<Policy> Resolve(FirmamentV2Document document, string process)
    {
        var contract = process.ToUpperInvariant() switch
        {
            "CNC" => "CncManufacturingPolicy",
            "FDM" => "FdmManufacturingPolicy",
            "ADDITIVE" => "AdditiveManufacturingPolicy",
            "SHEETMETAL" or "SHEET METAL" => "SheetMetalManufacturingPolicy",
            _ => process + "ManufacturingPolicy",
        };

        var canonical = (document.ConceptIr?.Structs ?? [])
            .Where(instance => instance.Satisfies.Contains(contract, StringComparer.Ordinal))
            .Select(instance => new Policy(instance.Name, process, instance.Members, $"Concept Struct {instance.Name}: {contract}"))
            .ToArray();
        if (canonical.Length > 0) return canonical;

        return (document.Templates ?? [])
            .Where(template => string.Equals(template.Process, process, StringComparison.OrdinalIgnoreCase))
            .Select(template => new Policy(
                template.Name,
                template.Process,
                template.Concepts.ToDictionary(
                    concept => concept.Name,
                    concept => (ConceptIrValue)new ConceptIrLengthValue(
                        $"legacy-template:{template.Name}.{concept.Name}",
                        concept.NumericValue,
                        concept.Unit ?? string.Empty,
                        $"template<{template.Process}> {template.Name}"),
                    StringComparer.OrdinalIgnoreCase),
                $"template<{template.Process}> {template.Name}"))
            .ToArray();
    }

    public static bool TryLength(Policy policy, string member, string units, out double value)
    {
        var pair = policy.Members.FirstOrDefault(candidate => string.Equals(candidate.Key, member, StringComparison.OrdinalIgnoreCase));
        if (pair.Value is ConceptIrLengthValue length && string.Equals(length.Unit, units, StringComparison.OrdinalIgnoreCase))
        {
            value = length.Value;
            return true;
        }

        value = double.NaN;
        return false;
    }
}
