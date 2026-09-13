using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Lattice;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.StandardLibrary.Materials;

namespace Aetheris.FEA.Analysis;

public enum AnalysisKind { LinearStaticElasticity }
public enum AnalysisMode { ProductionSolid, ExperimentalShell }
public enum ThinWallGeometryMapKind { Flat, Cylindrical }
public enum AnalysisResultField { Displacement, Strain, Stress, ReactionForce, StrainEnergy }
public enum BoundaryLoadKind { Traction, ResultantForce, Pressure }
public enum LoadDistributionPolicy { TotalResultantOverSelectedArea, TractionPerUnitArea, PressureNormalToSurface }
public enum AnalysisGeometrySourceKind { FirmamentNative, InlineStep, NativeSheetMetal }
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
    AnalysisProvenance Provenance,
    string? StableMaterialId = null,
    MaterialConstitutiveClass? ConstitutiveClass = null,
    double? YieldStrengthPascal = null,
    ResolvedMaterial? CatalogMaterial = null);

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
    AnalysisProvenance Provenance,
    LoadDistributionPolicy Distribution = LoadDistributionPolicy.TotalResultantOverSelectedArea);

/// <summary>A uniform acceleration applied to the physical material volume.</summary>
public sealed record BodyForceIr(
    string Id,
    Vector3D AccelerationMetersPerSecondSquared,
    AnalysisProvenance Provenance);

public sealed record AnalysisBodyIr(
    string Id,
    string SourceKind,
    IContinuumRegion ContinuumRegion,
    string? BrepBodyId,
    string? ResourceHash,
    AnalysisProvenance Provenance)
{
    public AnalysisGeometrySourceKind GeometrySource => Enum.Parse<AnalysisGeometrySourceKind>(SourceKind);
}

public sealed record AnalysisResultSemantics(AnalysisResultField Field,string Representation,string Unit,string Location,string Recovery);
public static class AnalysisResultContracts
{
    public static AnalysisResultSemantics Describe(AnalysisResultField field)=>field switch
    {
        AnalysisResultField.Displacement=>new(field,"Vector3","m","admitted lattice node","primary solved degrees of freedom; aggregated nodes are affine extensions"),
        AnalysisResultField.Strain=>new(field,"symmetric tensor","1","occupied cell center","small-strain tensor recovered from the displacement gradient"),
        AnalysisResultField.Stress=>new(field,"Cauchy symmetric tensor plus von Mises scalar","Pa","occupied cell center","linear isotropic constitutive recovery; no nodal interpolation"),
        AnalysisResultField.ReactionForce=>new(field,"Vector3 per constraint","N","selected boundary condition","assembled constrained residual or consistent Nitsche boundary reaction"),
        AnalysisResultField.StrainEnergy=>new(field,"Scalar","J","analysis domain","one half of displacement transpose times assembled elastic stiffness times displacement"),
        _=>throw new ArgumentOutOfRangeException(nameof(field)),
    };
}

/// <summary>Explicit settings for the experimental full-3D thin-wall finite-cell discretization.</summary>
public sealed record ExperimentalShellSettings(
    double ThicknessMeters,
    int PolynomialOrder,
    int MasterCellsR,
    int MasterCellsS,
    double FictitiousStiffnessFactor = 1e-12,
    int MaxSubdivisionDepth = 4,
    int? QuadratureOrder = null,
    ThinWallGeometryMapKind GeometryMap = ThinWallGeometryMapKind.Flat,
    double? CylinderRadiusMeters = null,
    double? CylinderAngleRadians = null);

public sealed record LinearElasticAnalysisIr(
    string Id,
    AnalysisKind Kind,
    AnalysisBodyIr Body,
    IReadOnlyList<LinearElasticMaterialIr> Materials,
    IReadOnlyList<DisplacementConstraintIr> Constraints,
    IReadOnlyList<BoundaryLoadIr> Loads,
    IReadOnlySet<AnalysisResultField> RequestedFields,
    LatticeSpec Lattice,
    AnalysisProvenance Provenance,
    AnalysisMode Mode = AnalysisMode.ProductionSolid,
    ExperimentalShellSettings? ExperimentalShell = null,
    MidsurfacePatchGraph? MidsurfacePatchGraph = null,
    IReadOnlyList<BodyForceIr>? BodyForces = null);

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
            if (material.ConstitutiveClass is not null and not MaterialConstitutiveClass.LinearElasticIsotropic)
                diagnostics.Add(Error("fea-unsupported-constitutive-class", $"Material '{material.Id}' uses unsupported constitutive class '{material.ConstitutiveClass}'. X1 supports LinearElasticIsotropic.", material.Provenance));
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
        foreach(var load in analysis.Loads.Where(load=>!double.IsFinite(load.VectorSi.X)||!double.IsFinite(load.VectorSi.Y)||!double.IsFinite(load.VectorSi.Z)||!double.IsFinite(load.PressurePascal)))
            diagnostics.Add(Error("fea-invalid-load",$"Load '{load.Id}' contains a non-finite or unit-incompatible value.",load.Provenance));
        foreach(var load in analysis.BodyForces??[])
        {
            var acceleration=load.AccelerationMetersPerSecondSquared;
            if(!double.IsFinite(acceleration.X)||!double.IsFinite(acceleration.Y)||!double.IsFinite(acceleration.Z))
                diagnostics.Add(Error("fea-invalid-body-force",$"Body force '{load.Id}' contains a non-finite or unit-incompatible acceleration.",load.Provenance));
            if(analysis.Materials.Any(material=>material.DensityKilogramsPerCubicMeter is null or <=0))
                diagnostics.Add(Error("fea-body-force-density-missing",$"Body force '{load.Id}' requires a finite positive material density.",load.Provenance));
        }
        if(analysis.Mode!=AnalysisMode.ExperimentalShell&&(analysis.BodyForces?.Count??0)>0)
            diagnostics.Add(Error("fea-body-force-mode-unsupported","BodyForce is currently qualified only for ExperimentalShell.",analysis.Provenance));
        if(analysis.Mode==AnalysisMode.ExperimentalShell)
        {
            var shell=analysis.ExperimentalShell;
            if(shell is null)diagnostics.Add(Error("thinwall-settings-missing","ExperimentalShell requires thin-wall discretization settings.",analysis.Provenance));
            else
            {
                if(!double.IsFinite(shell.ThicknessMeters)||shell.ThicknessMeters<=0)diagnostics.Add(Error("thinwall-thickness-invalid","ExperimentalShell thickness must be finite and positive.",analysis.Provenance));
                if(shell.PolynomialOrder is <1 or >6)diagnostics.Add(Error("thinwall-basis-order-invalid","ExperimentalShell X0 supports polynomial orders 1 through 6.",analysis.Provenance));
                if(shell.MasterCellsR<1||shell.MasterCellsS<1)diagnostics.Add(Error("thinwall-master-grid-invalid","ExperimentalShell master-grid dimensions must be positive.",analysis.Provenance));
                if(!double.IsFinite(shell.FictitiousStiffnessFactor)||shell.FictitiousStiffnessFactor<=0||shell.FictitiousStiffnessFactor>=1)diagnostics.Add(Error("thinwall-alpha-invalid","ExperimentalShell fictitious stiffness factor must be finite and strictly between zero and one.",analysis.Provenance));
                if(shell.MaxSubdivisionDepth is <0 or >8)diagnostics.Add(Error("thinwall-subdivision-depth-invalid","ExperimentalShell X0 supports subdivision depths 0 through 8.",analysis.Provenance));
                if(shell.QuadratureOrder is <2 or >12)diagnostics.Add(Error("thinwall-quadrature-order-invalid","ExperimentalShell quadrature order must be between 2 and 12.",analysis.Provenance));
                if(shell.GeometryMap==ThinWallGeometryMapKind.Cylindrical&&
                   (shell.CylinderRadiusMeters is null||!double.IsFinite(shell.CylinderRadiusMeters.Value)||shell.CylinderRadiusMeters<=shell.ThicknessMeters/2||
                    shell.CylinderAngleRadians is null||!double.IsFinite(shell.CylinderAngleRadians.Value)||shell.CylinderAngleRadians<=0))
                    diagnostics.Add(Error("thinwall-map-singular","Cylindrical ExperimentalShell geometry requires a finite positive angle and a radius greater than half-thickness.",analysis.Provenance));
            }
            if(analysis.Body.GeometrySource==AnalysisGeometrySourceKind.InlineStep)
                diagnostics.Add(Error("thinwall-geometry-unsupported","ExperimentalShell X0 does not infer midsurfaces or thickness from imported STEP.",analysis.Body.Provenance));
        }
        return diagnostics;
    }

    private static AnalysisDiagnostic Error(string code, string message, AnalysisProvenance provenance) =>
        new(code, AnalysisDiagnosticSeverity.Error, message, provenance);
}
