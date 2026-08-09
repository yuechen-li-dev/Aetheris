using Aetheris.Kernel.Core.Math;
using Aetheris.FEA.Analysis;

namespace Aetheris.FEA.Mechanics;

public readonly record struct MechanicsQuadraturePoint(Point3D Position, double Weight, double Xi, double Eta, double Zeta);

/// <summary>Solver integration evidence; intentionally distinct from Continuum GeometrySamplePlan.</summary>
public sealed record MechanicsQuadraturePlan(string Rule, IReadOnlyList<MechanicsQuadraturePoint> Points, double IntegratedVolume, bool FullCellFastPath);

public readonly record struct MechanicsNode(int Id, Point3D Position);
public readonly record struct SymmetricTensor(double XX, double YY, double ZZ, double XY, double YZ, double XZ);
public sealed record CellFieldResult(int I, int J, int K, Point3D Position, SymmetricTensor Strain, SymmetricTensor StressPascal, double VonMisesPascal);
public sealed record NodalDisplacement(int NodeId, Point3D Position, Vector3D DisplacementMeters);
public sealed record ReactionResult(string ConstraintId, Vector3D ForceNewton);
public sealed record SolverConvergence(bool Converged, int Iterations, double InitialResidual, double FinalResidual, IReadOnlyList<double> ResidualHistory, TimeSpan Runtime);
public sealed record MechanicsPerformance(TimeSpan DomainSetup, TimeSpan QuadratureSetup, TimeSpan Assembly, TimeSpan BoundaryAssembly, TimeSpan Solve, TimeSpan Recovery, long SparseBytes, long ResultBytes);
public sealed record TinyCellDiagnostics(double MinimumActiveFraction, int BelowOnePercent, int BelowFivePercent, int BelowTenPercent, IReadOnlyList<double> ActiveFractions);
public sealed record SparseSystemMetrics(int DegreesOfFreedom, int Nonzeros, double MaximumAsymmetry, bool Finite, bool DeterministicStructure, double AppliedLoadNewton, double IntegratedLoadResidualNewton);
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
    IReadOnlyList<AnalysisDiagnostic> Diagnostics)
{
    public double MaximumDisplacementMeters => Displacements.Count == 0 ? 0 : Displacements.Max(item => item.DisplacementMeters.Length);
    public double MaximumVonMisesPascal => CellFields.Count == 0 ? 0 : CellFields.Max(item => item.VonMisesPascal);
}
