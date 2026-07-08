using Aetheris.Forge.Abstractions.FirmamentInterop;
using Aetheris.Forge.Standard;
using Aetheris.Kernel.Firmament.FirmamentV2.FirmamentInterop;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentV2RuntimeConceptValidationResult(
    FirmamentV2ForgeRuntimeMetadata ForgeRuntime,
    IReadOnlyList<FirmamentV2RuntimeConceptValidationEntry> Concepts,
    IReadOnlyList<PmiObligation> PmiObligations,
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
        => Validate(document, FirmamentV2ForgeRuntimeConfiguration.CreateDefault());

    public static FirmamentV2RuntimeConceptValidationResult Validate(
        FirmamentV2Document? document,
        FirmamentV2ForgeRuntimeConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (document is null)
        {
            return new FirmamentV2RuntimeConceptValidationResult(configuration.Metadata, [], [], []);
        }

        var variables = new FirmamentV2VariablesAdapter(document);
        var conceptResults = new List<FirmamentV2RuntimeConceptValidationEntry>();
        var obligations = new List<PmiObligation>();
        var diagnostics = new List<FirmamentDiagnostic>();

        foreach (var declaration in document.ManufacturingConcepts ?? [])
        {
            ValidateApplication(
                FirmamentV2ConceptApplicationAdapter.Adapt(declaration),
                variables,
                configuration,
                conceptResults,
                obligations,
                diagnostics);
        }

        foreach (var declaration in document.FeatureConcepts ?? [])
        {
            ValidateApplication(
                FirmamentV2ConceptApplicationAdapter.Adapt(declaration),
                variables,
                configuration,
                conceptResults,
                obligations,
                diagnostics);
        }

        return new FirmamentV2RuntimeConceptValidationResult(
            configuration.Metadata,
            conceptResults,
            obligations,
            diagnostics);
    }

    private static void ValidateApplication(
        FirmamentConceptApplicationView application,
        IFirmamentVariables variables,
        FirmamentV2ForgeRuntimeConfiguration configuration,
        ICollection<FirmamentV2RuntimeConceptValidationEntry> conceptResults,
        ICollection<PmiObligation> obligations,
        ICollection<FirmamentDiagnostic> diagnostics)
    {
        if (!configuration.Registry.TryResolve(application.ConceptId, out var concept))
        {
            return;
        }

        var provider = configuration.ConceptProviders.TryGetValue(application.ConceptId, out var providerId)
            ? providerId
            : configuration.Metadata.BuiltInPack;

        var context = new ConceptValidationContext(application, variables);
        var conceptDiagnostics = concept.Validate(context)
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

        if (conceptDiagnostics.Any(diagnostic => diagnostic.Severity == FirmamentDiagnosticSeverity.Fatal))
        {
            return;
        }

        if (concept is not IForgePmiObligationProvider obligationProvider)
        {
            return;
        }

        foreach (var obligation in obligationProvider.GetPmiObligations(context).Distinct())
        {
            obligations.Add(obligation);
        }
    }
}
