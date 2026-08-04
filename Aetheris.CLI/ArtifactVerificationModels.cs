using Aetheris.Kernel.Core.Brep.Verification;

namespace Aetheris.CLI;

/// <summary>
/// Preserves independent evidence layers. A false or pending layer is never
/// collapsed into an undifferentiated pass/fail value.
/// </summary>
public sealed record ArtifactIdentity(string Path, string Sha256, string EvidenceDirectory);
public sealed record ArtifactProducerEvidence(string Status, string Note);
public sealed record ArtifactStepReimportEvidence(string Status, AnalyzeResult? Analysis, string? Error = null);
public sealed record ArtifactMassComparisonEvidence(
    double ExpectedVolume,
    double ObservedVolume,
    double Delta,
    double RelativeDelta,
    bool WithinReportedErrorBound);
public sealed record ArtifactVerificationResult(
    ArtifactIdentity Artifact,
    ArtifactProducerEvidence ProducerEvidence,
    BrepMassPropertiesResult? BrepMassProperties,
    ArtifactMassComparisonEvidence? MassComparison,
    ArtifactStepReimportEvidence StepReimport,
    object CadAssistant,
    string OverallAdmission);
