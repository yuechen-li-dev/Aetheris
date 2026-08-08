using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Brep;
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

public sealed record ExactCoaxialPartDefinition(
    ExactCoaxialPartRecipe Recipe,
    BrepBody Body,
    ExactConstructionSemanticModel Semantics,
    string DeterministicSignature,
    ExactCoaxialConstructionPlan ConstructionPlan);

public static class ExactCoaxialPartBuilder
{
    public static KernelResult<ExactCoaxialPartDefinition> Create(ExactCoaxialPartRecipe recipe)
    {
        var plan = Plan(recipe);
        if (!plan.IsSuccess) return KernelResult<ExactCoaxialPartDefinition>.Failure(plan.Diagnostics);
        var emitted = ExactConstructionMaterializer.Materialize(plan.Value);
        return emitted.IsSuccess
            ? KernelResult<ExactCoaxialPartDefinition>.Success(new(recipe, emitted.Value.Body,
                new(plan.Value.StableId, emitted.Value.Semantics, emitted.Value.Metadata), emitted.Value.DeterministicSignature, plan.Value))
            : KernelResult<ExactCoaxialPartDefinition>.Failure(emitted.Diagnostics);
    }

    public static KernelResult<ExactCoaxialConstructionPlan> Plan(ExactCoaxialPartRecipe recipe)
    {
        if (recipe.PolygonSides < 3 || recipe.AcrossFlats <= 0d || recipe.PrismAxialHeight <= 0d
            || recipe.TopFlatDiameter <= 0d || recipe.TopConeAngleDegrees <= 0d || recipe.TopConeAngleDegrees >= 90d
            || recipe.BlendRadius < 0d || recipe.ShaftDiameter <= 0d || recipe.AxialLength <= 0d
            || recipe.EndChamferLength <= 0d || recipe.EndChamferLength >= recipe.AxialLength || recipe.EndDiameter <= 0d
            || recipe.EndDiameter >= recipe.ShaftDiameter)
            return KernelResult<ExactCoaxialConstructionPlan>.Failure([new KernelDiagnostic(KernelDiagnosticCode.InvalidArgument,
                KernelDiagnosticSeverity.Error, "Exact coaxial construction dimensions are outside the admitted bounded family.", "ExactCoaxialPart.Admission")]);
        var apothem = recipe.AcrossFlats / 2d;
        var circumradius = apothem / Math.Cos(Math.PI / recipe.PolygonSides);
        var capRadius = recipe.TopFlatDiameter / 2d;
        if (capRadius >= apothem)
            return KernelResult<ExactCoaxialConstructionPlan>.Failure([new KernelDiagnostic(KernelDiagnosticCode.InvalidArgument,
                KernelDiagnosticSeverity.Error, "Top cap must lie inside the regular prism apothem.", "ExactCoaxialPart.ConePlanarTrim")]);
        var semiAngle = 90d - recipe.TopConeAngleDegrees;
        var slope = Math.Tan(semiAngle * Math.PI / 180d);
        var apex = -recipe.PrismAxialHeight - capRadius / slope;
        var prismEnd = apex + circumradius / slope;
        var prism = new RegularPrismConstruction("regular-prism", recipe.PolygonSides, recipe.AcrossFlats, 0d, prismEnd,
            180d / recipe.PolygonSides);
        var trim = new ConePlanarTrimConstruction("cone-planar-trim", apex, semiAngle, -recipe.PrismAxialHeight, capRadius);
        var blend = new ConcaveFilletConstruction("root-fillet", recipe.BlendRadius,
            recipe.ShaftDiameter / 2d + recipe.BlendRadius, 0d, recipe.BlendRadius);
        var cylinder = new AxialCylinderConstruction("cylinder", recipe.ShaftDiameter / 2d, recipe.BlendRadius,
            recipe.AxialLength - recipe.EndChamferLength);
        var frustum = new AxialFrustumConstruction("end-frustum", recipe.ShaftDiameter / 2d, recipe.EndDiameter / 2d,
            recipe.AxialLength - recipe.EndChamferLength, recipe.AxialLength);
        var top = new PlanarCapConstruction("top-cap", -recipe.PrismAxialHeight, capRadius, false);
        var end = new PlanarCapConstruction("end-cap", recipe.AxialLength, recipe.EndDiameter / 2d, true);
        ExactConstructionNode[] sections = [prism, trim, top, blend, cylinder, frustum, end];
        var id = recipe.StableId;
        ConstructionSemanticClaim[] claims =
        [
            new(id, ConstructionSemanticKind.Part), new(id + ".Head", ConstructionSemanticKind.Region, ParentStableId: id),
            new(id + ".Head.TopChamfer", ConstructionSemanticKind.Region, ParentStableId: id + ".Head"),
            new(id + ".Head.TopFlat", ConstructionSemanticKind.Face, "TopCap", id + ".Head"),
            new(id + ".Head.UnderHead", ConstructionSemanticKind.Face, "Shoulder", id + ".Head"),
            new(id + ".Shank", ConstructionSemanticKind.Region, ParentStableId: id),
            new(id + ".ThreadRegion", ConstructionSemanticKind.Region, ParentStableId: id,
                Metadata: $"{recipe.SemanticDesignation};length={recipe.SemanticAxialRegionLength:R}mm;material-geometry=Cylinder"),
            new(id + ".TipChamfer", ConstructionSemanticKind.Region, ParentStableId: id),
            new(id + ".TipFace", ConstructionSemanticKind.Face, "EndCap", id),
            new(id + ".Head.Side[{i}]", ConstructionSemanticKind.Face, "PrismSides", id + ".Head"),
            new(id + ".Head.TopChamfer.Face[{i}]", ConstructionSemanticKind.Face, "ConePlanarTrim", id + ".Head.TopChamfer"),
            new(id + ".Shank.Face[{i}]", ConstructionSemanticKind.Face, "Cylinder", id + ".Shank"),
            new(id + ".ThreadRegion.Face[{i}]", ConstructionSemanticKind.Face, "Cylinder", id + ".ThreadRegion"),
            new(id + ".TipChamfer.Face[{i}]", ConstructionSemanticKind.Face, "EndFrustum", id + ".TipChamfer"),
            new(id + ".Head.UnderHeadBlend.Face[{i}]", ConstructionSemanticKind.Face, "RootBlend", id + ".Head")
        ];
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["NominalDiameter"] = $"{recipe.ShaftDiameter:R}mm", ["ThreadLength"] = $"{recipe.SemanticAxialRegionLength:R}mm",
            ["ThreadDesignation"] = recipe.SemanticDesignation, ["PropertyClass"] = recipe.Grade,
            ["ThreadGeometry"] = "deferred-semantic-cylinder"
        };
        var signatureSource = string.Join("|", new[] { recipe.ShaftDiameter, recipe.AxialLength, recipe.AcrossFlats,
            recipe.PrismAxialHeight, recipe.TopFlatDiameter, recipe.TopConeAngleDegrees, recipe.EndChamferLength,
            recipe.EndDiameter, recipe.SemanticAxialRegionLength, recipe.BlendRadius }.Select(x => x.ToString("R", CultureInfo.InvariantCulture)))
            + $"|{recipe.SemanticDesignation}|{recipe.Grade}";
        var signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signatureSource))).ToLowerInvariant();
        return KernelResult<ExactCoaxialConstructionPlan>.Success(new(id, prism, trim, blend, cylinder, frustum, top, end,
            new AxialSectionStackConstruction("coaxial-stack", sections), claims, metadata, signature));
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
