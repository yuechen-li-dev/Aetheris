using System.Globalization;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.StandardLibrary;

/// <summary>Typed boundary from record-shaped authoring fields to a HexBoltSpec.</summary>
public static class HexBoltParameterBinding
{
    public static readonly IReadOnlyList<string> RequiredFields =
    [
        "NominalDiameter", "Length", "HeadAcrossFlats", "HeadHeight", "TopFlatDiameter",
        "TopChamferAngle", "TipChamferLength", "TipDiameter", "ThreadLength",
        "ThreadDesignation", "PropertyClass", "UnderHeadRadius"
    ];

    public static KernelResult<HexBoltSpec> Bind(IReadOnlyDictionary<string, string> fields)
    {
        var missing = RequiredFields.Where(field => !fields.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(value)).ToArray();
        if (missing.Length > 0) return Failure($"Missing required HexBolt parameter(s): {string.Join(", ", missing)}.", "MissingField");

        var allowed = RequiredFields.Append("StableId").ToHashSet(StringComparer.Ordinal);
        var unknown = fields.Keys.Where(field => !allowed.Contains(field)).ToArray();
        if (unknown.Length > 0) return Failure($"Unknown HexBolt parameter(s): {string.Join(", ", unknown)}.", "UnknownField");

        var numericNames = RequiredFields.Where(field => field is not ("ThreadDesignation" or "PropertyClass")).ToArray();
        var values = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var field in numericNames)
        {
            var raw = fields[field].Trim();
            if (raw.EndsWith("mm", StringComparison.Ordinal)) raw = raw[..^2].Trim();
            if (raw.EndsWith("deg", StringComparison.Ordinal)) raw = raw[..^3].Trim();
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return Failure($"HexBolt parameter '{field}' must be a numeric scalar with its declared mm/deg unit.", "InvalidScalar");
            values[field] = value;
        }

        return KernelResult<HexBoltSpec>.Success(new(
            values["NominalDiameter"], values["Length"], values["HeadAcrossFlats"], values["HeadHeight"],
            values["TopFlatDiameter"], values["TopChamferAngle"], values["TipChamferLength"], values["TipDiameter"],
            values["ThreadLength"], Text(fields["ThreadDesignation"]), Text(fields["PropertyClass"]), values["UnderHeadRadius"]));
    }

    public static string Text(string value)
    {
        var text = value.Trim();
        return text.Length >= 2 && text[0] == '"' && text[^1] == '"' ? text[1..^1] : text;
    }

    private static KernelResult<HexBoltSpec> Failure(string message, string code) =>
        KernelResult<HexBoltSpec>.Failure([new KernelDiagnostic(KernelDiagnosticCode.InvalidArgument, KernelDiagnosticSeverity.Error, message, $"StandardLibrary.HexBolt.Parameters.{code}")]);
}
