using Aetheris.Forge.Abstractions.FirmamentInterop;

namespace Aetheris.Kernel.Firmament.FirmamentV2.FirmamentInterop;

public sealed class FirmamentV2VariablesAdapter : IFirmamentVariables
{
    private readonly IReadOnlyList<FirmamentVariable> all;
    private readonly IReadOnlyDictionary<string, FirmamentVariable> byName;

    public FirmamentV2VariablesAdapter(FirmamentV2Document document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var variables = new List<FirmamentVariable>();
        foreach (var boundLet in document.BoundLets ?? [])
        {
            variables.Add(FirmamentV2InteropValueAdapter.AdaptVariable(boundLet.Name, boundLet));
        }

        foreach (var record in document.BoundLetRecords ?? [])
        {
            foreach (var field in record.Fields.Values.OrderBy(field => field.Name, StringComparer.Ordinal))
            {
                variables.Add(FirmamentV2InteropValueAdapter.AdaptVariable($"{record.Name}.{field.Name}", field));
            }
        }

        all = variables.OrderBy(variable => variable.Name, StringComparer.Ordinal).ToArray();
        byName = all.ToDictionary(variable => variable.Name, StringComparer.Ordinal);
    }

    public IReadOnlyList<FirmamentVariable> All => all;

    public bool TryGet(string name, out FirmamentValue value)
    {
        if (byName.TryGetValue(name, out var variable))
        {
            value = variable.Value;
            return true;
        }

        value = null!;
        return false;
    }

    public FirmamentValue GetRequired(string name)
    {
        if (TryGet(name, out var value))
        {
            return value;
        }

        throw new KeyNotFoundException($"Firmament variable '{name}' was not found.");
    }
}
