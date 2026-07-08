using Aetheris.Forge.Abstractions.FirmamentInterop;

namespace Aetheris.Forge.Standard;

public sealed class ShaftHoleConcept : IForgeConcept, IForgePmiObligationProvider
{
    public ConceptId Id => new("hole", "Shaft");

    public void Define(ConceptSchemaBuilder schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        schema.RequiredTarget("target");
        schema.RequiredLength("diameter");
    }

    public IEnumerable<FirmamentDiagnostic> Validate(ConceptValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var diagnostics = new List<FirmamentDiagnostic>();

        StandardConceptValidationHelpers.TryGetPositiveLength(
            context,
            "diameter",
            "forge.hole.shaft.diameter-positive",
            "diameter must be greater than zero.",
            diagnostics,
            out _);

        if (!StandardConceptValidationHelpers.HasTarget(context, "target"))
        {
            diagnostics.Add(StandardConceptValidationHelpers.Fatal(
                "forge.hole.shaft.target-required",
                "target is required.",
                "target"));
        }

        StandardConceptValidationHelpers.AddToleranceRecommendation(
            context,
            "diameter",
            "forge.hole.shaft.diameter-tolerance-recommended",
            "diameter should include tolerance evidence.",
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
