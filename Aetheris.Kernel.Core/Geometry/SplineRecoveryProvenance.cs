namespace Aetheris.Kernel.Core.Geometry;

/// <summary>Measured import evidence, in normalized model millimetres, for a non-analytic spline.</summary>
public sealed record SplineRecoveryProvenance(
    string SourceGeometryKind,
    string RecoveryMethod,
    double RecoveryToleranceMillimetres,
    double MeasuredMaxDeviationMillimetres,
    bool AnalyticRecognitionAttempted,
    string AnalyticRecognitionResult,
    bool RationalSourceRetained = false);
