using Aetheris.Kernel.Core.Brep;

namespace Aetheris.Kernel.Firmament.Execution;

internal enum FirmamentAirChamferExperimentalFaceFamily { Planar, Cylindrical, Unsupported }

internal enum FirmamentAirChamferExperimentalCandidateStatus { Succeeded, Rejected, Deferred, Failed, FallbackLegacy }

internal sealed record FirmamentAirChamferExperimentalOptions(
    bool EnableAirChamferExperimentalRoute = false,
    string CaseName = "edge-v4-firmament-controlled-box-convex-planar-single-edge",
    FirmamentAirChamferExperimentalFaceFamily FaceFamily = FirmamentAirChamferExperimentalFaceFamily.Planar,
    bool IsEdgeChain = false,
    bool IsCornerChain = false,
    bool LegacyDependency = false,
    bool IncludeStepSmoke = true,
    Func<FirmamentAirChamferExperimentalCandidateRequest, FirmamentAirChamferExperimentalCandidateReport>? CandidateProvider = null,
    Action<IReadOnlyList<string>>? DiagnosticSink = null)
{
    public static FirmamentAirChamferExperimentalOptions Disabled { get; } = new();
}

internal sealed record FirmamentAirChamferExperimentalCandidateRequest(
    string CaseName,
    BrepBody SourceBody,
    double ChamferDistance,
    FirmamentAirChamferExperimentalFaceFamily FaceFamily,
    bool IsEdgeChain,
    bool IsCornerChain,
    bool LegacyDependency,
    bool IncludeStepSmoke);

internal sealed record FirmamentAirChamferExperimentalCandidateReport(
    bool CandidateProduced,
    FirmamentAirChamferExperimentalCandidateStatus Status,
    string Decision,
    BrepBody? CandidateBody,
    bool TopologyContractSatisfied,
    bool StepSmokeSucceeded,
    bool RecognitionContractSatisfied,
    bool NoThreeDimensionalBooleanUsed,
    IReadOnlyList<string> Diagnostics);
