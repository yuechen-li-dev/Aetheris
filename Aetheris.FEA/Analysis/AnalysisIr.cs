using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Lattice;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.FEA.Analysis;

public enum AnalysisKind { LinearStaticElasticity }
public enum AnalysisResultField { Displacement, Strain, Stress, ReactionForce }
public enum BoundaryLoadKind { Traction, ResultantForce, Pressure }
public enum DisplacementComponent { X, Y, Z }

public sealed record AnalysisProvenance(
    string Source,
    int Start,
    int Length,
    string Declaration,
    string? Template = null,
    string? ExactBrepFaceId = null,
    string? ContinuumFragmentId = null);

/// <summary>A semantic region reference. Path is source-level identity, never a mesh ID.</summary>
public sealed record SemanticRegionBinding(
    string Body,
    string Path,
    string? ExactBrepFaceId = null,
    IReadOnlyList<string>? RecognizedFaceIds = null,
    AnalysisProvenance? Provenance = null,
    string? SemanticStableId = null,
    IReadOnlyList<string>? CapabilityEvidence = null,
    string? ExactBindingKind = null);

public sealed record LinearElasticMaterialIr(
    string Id,
    double YoungsModulusPascal,
    double PoissonRatio,
    double? DensityKilogramsPerCubicMeter,
    string RegionPath,
    AnalysisProvenance Provenance);

public sealed record DisplacementConstraintIr(
    string Id,
    SemanticRegionBinding Region,
    IReadOnlySet<DisplacementComponent> Components,
    Vector3D ValueMeters,
    AnalysisProvenance Provenance);

public sealed record BoundaryLoadIr(
    string Id,
    BoundaryLoadKind Kind,
    SemanticRegionBinding Region,
    Vector3D VectorSi,
    double PressurePascal,
    AnalysisProvenance Provenance);

public sealed record AnalysisBodyIr(
    string Id,
    string SourceKind,
    IContinuumRegion ContinuumRegion,
    string? BrepBodyId,
    string? ResourceHash,
    AnalysisProvenance Provenance);

public sealed record LinearElasticAnalysisIr(
    string Id,
    AnalysisKind Kind,
    AnalysisBodyIr Body,
    IReadOnlyList<LinearElasticMaterialIr> Materials,
    IReadOnlyList<DisplacementConstraintIr> Constraints,
    IReadOnlyList<BoundaryLoadIr> Loads,
    IReadOnlySet<AnalysisResultField> RequestedFields,
    LatticeSpec Lattice,
    AnalysisProvenance Provenance);

public enum AnalysisDiagnosticSeverity { Info, Warning, Error }
public sealed record AnalysisDiagnostic(string Code, AnalysisDiagnosticSeverity Severity, string Message, AnalysisProvenance? Provenance = null);

public static class AnalysisIrValidator
{
    public static IReadOnlyList<AnalysisDiagnostic> Validate(LinearElasticAnalysisIr analysis)
    {
        var diagnostics = new List<AnalysisDiagnostic>();
        if (analysis.Materials.Count == 0)
            diagnostics.Add(Error("fea-missing-material", "Linear elasticity requires one material assignment.", analysis.Provenance));
        if (analysis.Materials.Count > 1)
            diagnostics.Add(Error("fea-m5-multiple-material-regions-unsupported", "M5 supports one homogeneous material while retaining region-shaped assignments.", analysis.Provenance));
        foreach (var material in analysis.Materials)
        {
            if (!double.IsFinite(material.YoungsModulusPascal) || material.YoungsModulusPascal <= 0)
                diagnostics.Add(Error("fea-invalid-youngs-modulus", "Young's modulus must be finite and positive.", material.Provenance));
            if (!double.IsFinite(material.PoissonRatio) || material.PoissonRatio <= -1 || material.PoissonRatio >= 0.5)
                diagnostics.Add(Error("fea-invalid-poisson-ratio", "Poisson ratio must be in the open interval (-1, 0.5).", material.Provenance));
        }
        if (analysis.Constraints.Count == 0)
            diagnostics.Add(Error("fea-rigid-body-mode", "No displacement constraints were declared; rigid-body modes are present.", analysis.Provenance));
        foreach (var constraint in analysis.Constraints.Where(c => c.Components.Count == 0))
            diagnostics.Add(Error("fea-empty-constraint", $"Constraint '{constraint.Id}' constrains no displacement components.", constraint.Provenance));
        foreach (var load in analysis.Loads.Where(l => string.IsNullOrWhiteSpace(l.Region.Path)))
            diagnostics.Add(Error("fea-empty-region-selection", $"Load '{load.Id}' has no semantic region.", load.Provenance));
        return diagnostics;
    }

    private static AnalysisDiagnostic Error(string code, string message, AnalysisProvenance provenance) =>
        new(code, AnalysisDiagnosticSeverity.Error, message, provenance);
}
