using Aetheris.Forge.Abstractions.FirmamentInterop;

namespace Aetheris.Forge.Standard;

public sealed class CncProcessConcept : IForgeConcept
{
    public ConceptId Id => new("process", "CNC");

    public void Define(ConceptSchemaBuilder schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        schema.RequiredMaterial("material");
        schema.RequiredLength("minimumToolRadius");
    }

    public IEnumerable<FirmamentDiagnostic> Validate(ConceptValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var diagnostics = new List<FirmamentDiagnostic>();

        if (context.TryGetNumeric("minimumToolRadius", FirmamentValueKind.Length, out var minimumToolRadius)
            && minimumToolRadius <= 0)
        {
            diagnostics.Add(StandardConceptValidationHelpers.Fatal(
                "forge.process.cnc.minimum-tool-radius-positive",
                "minimumToolRadius must be greater than zero.",
                "minimumToolRadius"));
        }

        if (!StandardConceptValidationHelpers.HasNonEmptyString(context, "material"))
        {
            diagnostics.Add(StandardConceptValidationHelpers.Fatal(
                "forge.process.cnc.material-required",
                "material is required.",
                "material"));
        }

        return diagnostics;
    }
}
