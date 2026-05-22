using Aetheris.Kernel.Core.Brep;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public enum SteppedArchitectureStrategyStatus { Succeeded, Failed, Deferred, Skipped }

public sealed record SteppedArchitectureStrategyResult(
    string Strategy,
    SteppedArchitectureStrategyStatus Status,
    BrepBody? Body,
    string FailureCode,
    string FailureStage,
    IReadOnlyList<string> Diagnostics,
    bool Admissible,
    bool Executed,
    bool StepSmokeAttempted,
    bool StepSmokeSucceeded,
    IReadOnlyList<string> StepMarkers,
    bool HasSafeBooleanComposition,
    bool ManifoldSolidBrep,
    bool HasBrepWithVoids,
    int FaceCount,
    int LoopCount,
    int EdgeCount,
    int CoedgeCount,
    int VertexCount,
    string RecommendedNextStep);

public sealed record SteppedHoleExecutionArchitectureLabResult(
    IReadOnlyList<SteppedArchitectureStrategyResult> Strategies,
    string Recommendation,
    IReadOnlyList<string> Diagnostics);
