using System.Globalization;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.StandardLibrary;

/// <summary>
/// Domain-neutral bounded recipe for one connected coaxial analytic part made from
/// a regular prism, an exact cone/plane trim, a toroidal transition, a cylindrical
/// span, and a terminal conical frustum.  The recipe is compiler machinery; names
/// and engineering meaning are supplied by the Firmament Template.
/// </summary>
public sealed record ExactCoaxialPartRecipe(
    string StableId,
    int PolygonSides,
    double AcrossFlats,
    double PrismAxialHeight,
    double TopFlatDiameter,
    double TopConeAngleDegrees,
    double BlendRadius,
    double ShaftDiameter,
    double AxialLength,
    double EndChamferLength,
    double EndDiameter,
    double SemanticAxialRegionLength,
    string SemanticDesignation,
    string Grade);

public static class ExactCoaxialPartBuilder
{
    public static KernelResult<HexBoltDefinition> Create(ExactCoaxialPartRecipe recipe)
    {
        if (recipe.PolygonSides != 6)
            return KernelResult<HexBoltDefinition>.Failure([new KernelDiagnostic(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error,
                "ExactCoaxialPart currently admits six-sided regular prisms; the construction node remains polygon-generic and diagnoses the bounded backend limit.", "ExactCoaxialPart.RegularPolygonPrism.Sides")]);
        var spec = new HexBoltSpec(recipe.ShaftDiameter, recipe.AxialLength, recipe.AcrossFlats, recipe.PrismAxialHeight,
            recipe.TopFlatDiameter, recipe.TopConeAngleDegrees, recipe.EndChamferLength, recipe.EndDiameter,
            recipe.SemanticAxialRegionLength, recipe.SemanticDesignation, recipe.Grade, recipe.BlendRadius);
        return HexBoltBuilder.Create(spec, recipe.StableId);
    }

    public static KernelResult<ExactCoaxialPartRecipe> Bind(IReadOnlyDictionary<string, string> fields)
    {
        string Text(string name) => Unquote(fields[name]);
        bool Number(string name, string suffix, out double value)
        {
            value = double.NaN; var raw = fields.GetValueOrDefault(name, string.Empty);
            return raw.EndsWith(suffix, StringComparison.Ordinal) && double.TryParse(raw[..^suffix.Length], NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
        }
        var required = new[] { "StableId", "Sides", "AcrossFlats", "AxialHeight", "FlatDiameter", "ConeAngle", "BlendRadius", "ShaftDiameter", "AxialLength", "EndChamferLength", "EndDiameter", "SemanticAxialRegionLength", "Designation", "Grade" };
        var missing = required.Where(name => !fields.ContainsKey(name)).ToArray();
        if (missing.Length > 0) return Failure("missing-field:" + string.Join(",", missing));
        if (!int.TryParse(fields["Sides"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sides)
            || !Number("AcrossFlats", "mm", out var acrossFlats) || !Number("AxialHeight", "mm", out var height)
            || !Number("FlatDiameter", "mm", out var flat) || !Number("ConeAngle", "deg", out var angle)
            || !Number("BlendRadius", "mm", out var blend) || !Number("ShaftDiameter", "mm", out var diameter)
            || !Number("AxialLength", "mm", out var length) || !Number("EndChamferLength", "mm", out var endLength)
            || !Number("EndDiameter", "mm", out var endDiameter) || !Number("SemanticAxialRegionLength", "mm", out var semanticLength))
            return Failure("invalid-scalar");
        return KernelResult<ExactCoaxialPartRecipe>.Success(new(Text("StableId"), sides, acrossFlats, height, flat, angle, blend, diameter, length,
            endLength, endDiameter, semanticLength, Text("Designation"), Text("Grade")));
    }

    private static string Unquote(string value) => value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
    private static KernelResult<ExactCoaxialPartRecipe> Failure(string detail) => KernelResult<ExactCoaxialPartRecipe>.Failure([
        new KernelDiagnostic(KernelDiagnosticCode.InvalidArgument, KernelDiagnosticSeverity.Error, "Invalid ExactCoaxialPart recipe: " + detail, "ExactCoaxialPart.Binding")]);
}
