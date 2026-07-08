using Aetheris.Forge.Abstractions.FirmamentInterop;

namespace Aetheris.Forge.Standard;

public sealed class CounterboreHoleConcept : IForgeConcept, IForgePmiObligationProvider
{
    public ConceptId Id => new("hole", "Counterbore");

    public void Define(ConceptSchemaBuilder schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        schema.RequiredTarget("target");
        schema.RequiredLength("diameter");
        schema.RequiredLength("counterboreDiameter");
        schema.RequiredLength("counterboreDepth");
    }

    public IEnumerable<FirmamentDiagnostic> Validate(ConceptValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var diagnostics = new List<FirmamentDiagnostic>();

        var hasDiameter = StandardConceptValidationHelpers.TryGetPositiveLength(
            context,
            "diameter",
            "forge.hole.counterbore.diameter-positive",
            "diameter must be greater than zero.",
            diagnostics,
            out var diameter);

        var hasCounterboreDiameter = StandardConceptValidationHelpers.TryGetPositiveLength(
            context,
            "counterboreDiameter",
            "forge.hole.counterbore.counterbore-diameter-positive",
            "counterboreDiameter must be greater than zero.",
            diagnostics,
            out var counterboreDiameter);

        StandardConceptValidationHelpers.TryGetPositiveLength(
            context,
            "counterboreDepth",
            "forge.hole.counterbore.counterbore-depth-positive",
            "counterboreDepth must be greater than zero.",
            diagnostics,
            out _);

        if (hasDiameter && hasCounterboreDiameter && counterboreDiameter < diameter)
        {
            diagnostics.Add(StandardConceptValidationHelpers.Fatal(
                "forge.hole.counterbore.diameter-order",
                "counterboreDiameter must be greater than or equal to diameter.",
                "counterboreDiameter"));
        }

        if (!StandardConceptValidationHelpers.HasTarget(context, "target"))
        {
            diagnostics.Add(StandardConceptValidationHelpers.Fatal(
                "forge.hole.counterbore.target-required",
                "target is required.",
                "target"));
        }

        StandardConceptValidationHelpers.AddToleranceRecommendation(
            context,
            "diameter",
            "forge.hole.counterbore.diameter-tolerance-recommended",
            "diameter should include tolerance evidence.",
            diagnostics);

        StandardConceptValidationHelpers.AddToleranceRecommendation(
            context,
            "counterboreDiameter",
            "forge.hole.counterbore.diameter-tolerance-recommended",
            "counterboreDiameter should include tolerance evidence.",
            diagnostics);

        return diagnostics;
    }

    public IEnumerable<PmiObligation> GetPmiObligations(ConceptValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return
        [
            new PmiObligation(
                "diameter",
                Id,
                context.Application.Name,
                context.TryGetTargetSource("target", out var targetSource) ? targetSource : null,
                "diameter",
                FirmamentDiagnosticSeverity.Warning)
        ];
    }
}
