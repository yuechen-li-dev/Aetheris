using Aetheris.Continuum.Boundaries;
using Aetheris.Forge.Abstractions;
using Aetheris.Forge.Extensions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.FEA.Abaqus;
using Aetheris.FEA.Analysis;
using Aetheris.FEA.Mechanics;

namespace Aetheris.Forge.Sdk;

public sealed record ForgeDiagnostic(
    string Code,
    ForgeDiagnosticSeverity Severity,
    string Message,
    string? Source = null,
    string? CapabilityId = null);

public sealed record ForgeTemplateParameter(
    string Name,
    string TypeName,
    bool IsTypeParameter,
    string? DefaultValue,
    string? ConstraintConcept);

public sealed record ForgeTemplateMetadata(
    string ModuleName,
    string Name,
    string TargetKind,
    IReadOnlyList<ForgeTemplateParameter> Parameters,
    string GeneratedBindingName);

public sealed record ForgeCapabilityEvidence(
    string CapabilityId,
    string CapabilityVersion,
    string ExtensionId,
    string ExtensionVersion,
    string OutputClassification,
    IReadOnlyList<string> LoweringTargets);

public sealed record ForgeProvenanceEntry(string Stage, string Identity, string Evidence);

public sealed record ForgeCirEvidence(
    CirBrepAssociation Association,
    BrepCirConsistencyResult Consistency);

public sealed record ForgeCompilationArtifact(
    string StepText,
    string ArtifactHash,
    BrepBody? Body,
    ForgeCirEvidence? Cir,
    IReadOnlyList<ForgeCapabilityEvidence> Capabilities,
    IReadOnlyList<ForgeProvenanceEntry> Provenance);

public sealed record ForgeCompilationResult(
    ForgeCompilationArtifact? Artifact,
    IReadOnlyList<ForgeDiagnostic> Diagnostics,
    TimeSpan RegistrationTime,
    TimeSpan ResolutionTime,
    TimeSpan TemplateInvocationTime,
    TimeSpan ExtensionLoweringTime,
    TimeSpan CompilerLoweringTime)
{
    public bool IsSuccess => Artifact is not null && Diagnostics.All(diagnostic => diagnostic.Severity != ForgeDiagnosticSeverity.Error);
}

public sealed record ForgeAnalysisInvocationResult(
    LinearElasticAnalysisIr? AnalysisIr,
    LinearElasticAnalysisResult? NativeResult,
    AbaqusExportArtifact? Abaqus,
    IReadOnlyList<ForgeDiagnostic> Diagnostics,
    TimeSpan TemplateInvocationTime,
    TimeSpan AnalysisCompilationTime)
{
    public bool IsSuccess => AnalysisIr is not null && NativeResult?.IsSuccess == true && Abaqus is not null
        && Diagnostics.All(item => item.Severity != ForgeDiagnosticSeverity.Error);
}
