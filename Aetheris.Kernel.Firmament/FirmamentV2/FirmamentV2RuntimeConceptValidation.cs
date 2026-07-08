using Aetheris.Forge.Abstractions.FirmamentInterop;
using Aetheris.Forge.Standard;
using Aetheris.Kernel.Firmament.FirmamentV2.FirmamentInterop;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentV2RuntimeConceptValidationResult(
    string Provider,
    IReadOnlyList<FirmamentV2RuntimeConceptValidationEntry> Concepts,
    IReadOnlyList<FirmamentDiagnostic> Diagnostics);

public sealed record FirmamentV2RuntimeConceptValidationEntry(
    FirmamentConceptApplicationKind Kind,
    string? Name,
    string Family,
    string Concept,
    string Provider,
    IReadOnlyList<FirmamentDiagnostic> Diagnostics)
{
    public string Status => Diagnostics.Any(diagnostic => diagnostic.Severity == FirmamentDiagnosticSeverity.Fatal)
        ? "invalid"
        : "valid";
}

public static class FirmamentV2RuntimeConceptValidation
{
    public static FirmamentV2RuntimeConceptValidationResult Validate(FirmamentV2Document? document)
    {
        if (document is null)
        {
            return new FirmamentV2RuntimeConceptValidationResult(string.Empty, [], []);
        }

        var registry = new ForgeConceptRegistry();
        var pack = new StandardForgeRuntimeConceptPack();
        pack.Register(registry);

        var variables = new FirmamentV2VariablesAdapter(document);
        var conceptResults = new List<FirmamentV2RuntimeConceptValidationEntry>();
        var diagnostics = new List<FirmamentDiagnostic>();

        foreach (var declaration in document.ManufacturingConcepts ?? [])
        {
            ValidateApplication(
                FirmamentV2ConceptApplicationAdapter.Adapt(declaration),
                variables,
                pack.Id,
                registry,
                conceptResults,
                diagnostics);
        }

        foreach (var declaration in document.FeatureConcepts ?? [])
        {
            ValidateApplication(
                FirmamentV2ConceptApplicationAdapter.Adapt(declaration),
                variables,
                pack.Id,
                registry,
                conceptResults,
                diagnostics);
        }

        return new FirmamentV2RuntimeConceptValidationResult(
            pack.Id,
            conceptResults,
            diagnostics);
    }

    private static void ValidateApplication(
        FirmamentConceptApplicationView application,
        IFirmamentVariables variables,
        string provider,
        IForgeRegistry registry,
        ICollection<FirmamentV2RuntimeConceptValidationEntry> conceptResults,
        ICollection<FirmamentDiagnostic> diagnostics)
    {
        if (!registry.TryResolve(application.ConceptId, out var concept))
        {
            return;
        }

        var conceptDiagnostics = concept.Validate(new ConceptValidationContext(application, variables))
            .Distinct()
            .ToArray();

        conceptResults.Add(new FirmamentV2RuntimeConceptValidationEntry(
            application.Kind,
            application.Name,
            application.ConceptId.Family,
            application.ConceptId.Concept,
            provider,
            conceptDiagnostics));

        foreach (var diagnostic in conceptDiagnostics)
        {
            diagnostics.Add(diagnostic);
        }
    }
}
