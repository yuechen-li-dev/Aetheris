using Aetheris.Kernel.Core.Math;
using Aetheris.FEA.Analysis;
using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Lattice;

namespace Aetheris.FEA.Mechanics;

public readonly record struct MechanicsQuadraturePoint(Point3D Position, double Weight, double Xi, double Eta, double Zeta);

/// <summary>Solver integration evidence; intentionally distinct from Continuum GeometrySamplePlan.</summary>
public sealed record MechanicsQuadraturePlan(string Rule, IReadOnlyList<MechanicsQuadraturePoint> Points, double IntegratedVolume, bool FullCellFastPath);

public readonly record struct MechanicsBoundaryQuadraturePoint(Point3D Position,double AreaWeight,Vector3D OutwardNormal,IReadOnlyList<double> ShapeFunctions);
public sealed record MechanicsBoundaryFragment(CellIndex Cell,IReadOnlyList<Point3D> Polygon,IReadOnlyList<MechanicsBoundaryQuadraturePoint> Points,double Area,string OwnershipKey);
public sealed record MechanicsBoundaryQuadraturePlan(string RegionPath,string BoundaryId,string? ExactBrepFaceId,BoundaryLocalFrame Frame,double ExactSelectedArea,Point3D ExactCentroid,IReadOnlyList<MechanicsBoundaryFragment> Fragments,string OwnershipRule,string MaterialSideEvidence,AnalysisProvenance? Provenance)
{
    public double IntegratedArea=>Fragments.Sum(item=>item.Area);
    public int QuadraturePointCount=>Fragments.Sum(item=>item.Points.Count);
}
public sealed record BoundaryLoadEvidence(string LoadId,string RegionPath,string BoundaryId,string? ExactBrepFaceId,double ExactArea,double IntegratedArea,Vector3D ExpectedResultant,Vector3D IntegratedResultant,Vector3D ExpectedMoment,Vector3D IntegratedMoment,double ResultantResidual,double MomentResidual,int OwnedFragments,int QuadraturePoints,string MaterialSideEvidence);

public readonly record struct MechanicsNode(int Id, Point3D Position);
public readonly record struct SymmetricTensor(double XX, double YY, double ZZ, double XY, double YZ, double XZ);
public sealed record CellFieldResult(int I, int J, int K, Point3D Position, SymmetricTensor Strain, SymmetricTensor StressPascal, double VonMisesPascal);
public sealed record NodalDisplacement(int NodeId, Point3D Position, Vector3D DisplacementMeters);
public sealed record ReactionResult(string ConstraintId, Vector3D ForceNewton);
public sealed record SolverConvergence(bool Converged, int Iterations, double InitialResidual, double FinalResidual, IReadOnlyList<double> ResidualHistory, TimeSpan Runtime);
public sealed record MechanicsPerformance(
    TimeSpan DomainSetup,TimeSpan QuadratureSetup,TimeSpan Assembly,TimeSpan BoundaryAssembly,TimeSpan Solve,TimeSpan Recovery,
    long SparseBytes,long ResultBytes,
    TimeSpan SemanticFaceResolution=default,TimeSpan LocalFaceProjectionAndClipping=default,TimeSpan LoadAssembly=default,TimeSpan ConstraintResolution=default);
public sealed record TinyCellDiagnostics(double MinimumActiveFraction,int BelowOnePercent,int BelowFivePercent,int BelowTenPercent,IReadOnlyList<double> ActiveFractions);
public sealed record SparseSystemMetrics(int DegreesOfFreedom, int Nonzeros, double MaximumAsymmetry, bool Finite, bool DeterministicStructure, double AppliedLoadNewton, double IntegratedLoadResidualNewton,int CutCells=0,bool? IndependentSpdCheck=null);
public sealed record EquilibriumResult(Vector3D AppliedForceNewton, Vector3D ReactionForceNewton, Vector3D ResidualNewton);

public sealed record LinearElasticAnalysisResult(
    string AnalysisId,
    bool IsSuccess,
    SolverConvergence Solver,
    IReadOnlyList<NodalDisplacement> Displacements,
    IReadOnlyList<CellFieldResult> CellFields,
    IReadOnlyList<ReactionResult> Reactions,
    EquilibriumResult Equilibrium,
    SparseSystemMetrics System,
    TinyCellDiagnostics TinyCells,
    MechanicsPerformance Performance,
    IReadOnlyList<AnalysisDiagnostic> Diagnostics,
    IReadOnlyList<BoundaryLoadEvidence>? BoundaryLoads=null)
{
    public double MaximumDisplacementMeters => Displacements.Count == 0 ? 0 : Displacements.Max(item => item.DisplacementMeters.Length);
    public double MaximumVonMisesPascal => CellFields.Count == 0 ? 0 : CellFields.Max(item => item.VonMisesPascal);
}
