using Aetheris.Forge.Abstractions.FirmamentInterop;

namespace Aetheris.Forge.Standard;

public sealed class CountersinkHoleConcept : IForgeConcept
{
    public ConceptId Id => new("hole", "Countersink");

    public void Define(ConceptSchemaBuilder schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        schema.RequiredTarget("target");
        schema.RequiredLength("diameter");
        schema.RequiredLength("countersinkDiameter");
        schema.RequiredAngle("angle");
    }

    public IEnumerable<FirmamentDiagnostic> Validate(ConceptValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var diagnostics = new List<FirmamentDiagnostic>();

        var hasDiameter = StandardConceptValidationHelpers.TryGetPositiveLength(
            context,
            "diameter",
            "forge.hole.countersink.diameter-positive",
            "diameter must be greater than zero.",
            diagnostics,
            out var diameter);

        var hasCountersinkDiameter = StandardConceptValidationHelpers.TryGetPositiveLength(
            context,
            "countersinkDiameter",
            "forge.hole.countersink.countersink-diameter-positive",
            "countersinkDiameter must be greater than zero.",
            diagnostics,
            out var countersinkDiameter);

        if (hasDiameter && hasCountersinkDiameter && countersinkDiameter <= diameter)
        {
            diagnostics.Add(StandardConceptValidationHelpers.Fatal(
                "forge.hole.countersink.diameter-order",
                "countersinkDiameter must be greater than diameter.",
                "countersinkDiameter"));
        }

        if (context.TryGetNumeric("angle", FirmamentValueKind.Angle, out var angle)
            && (angle <= 0 || angle >= 180))
        {
            diagnostics.Add(StandardConceptValidationHelpers.Fatal(
                "forge.hole.countersink.angle-range",
                "angle must be greater than 0 degrees and less than 180 degrees.",
                "angle"));
        }

        if (!StandardConceptValidationHelpers.HasTarget(context, "target"))
        {
            diagnostics.Add(StandardConceptValidationHelpers.Fatal(
                "forge.hole.countersink.target-required",
                "target is required.",
                "target"));
        }

        StandardConceptValidationHelpers.AddToleranceRecommendation(
            context,
            "diameter",
            "forge.hole.countersink.diameter-tolerance-recommended",
            "diameter should include tolerance evidence.",
            diagnostics);

        StandardConceptValidationHelpers.AddToleranceRecommendation(
            context,
            "countersinkDiameter",
            "forge.hole.countersink.diameter-tolerance-recommended",
            "countersinkDiameter should include tolerance evidence.",
            diagnostics);

        return diagnostics;
    }
}
