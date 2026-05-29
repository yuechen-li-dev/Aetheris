namespace Aetheris.Kernel.Core.Cir.Mirrors;

internal enum CirMirrorStatus
{
    MirrorUnavailable,
    MirrorAdmittedExact,
    MirrorAdmittedConservative,
    MirrorAdmittedApproximate,
    MirrorRejectedUnsupportedAtom,
    MirrorRejectedLossyForRequest,
    MirrorRejectedStaleOrMismatched,
}

[Flags]
internal enum CirMirrorCapability
{
    None = 0,
    PointContainment = 1 << 0,
    ApproximateVolume = 1 << 1,
    MapOccupancy = 1 << 2,
    SectionSampling = 1 << 3,
    FaceIdentity = 1 << 4,
    TopologyParity = 1 << 5,
}

[Flags]
internal enum CirMirrorLossFlags
{
    None = 0,
    FaceIdentityLost = 1 << 0,
    LoopIdentityLost = 1 << 1,
    SplitFaceLineageLost = 1 << 2,
    FeatureRoleLabelsLost = 1 << 3,
    BoundaryPrecisionLimited = 1 << 4,
    ExactTopologyUnavailable = 1 << 5,
}

internal enum CirMirrorSourceRepresentationKind
{
    Firmament,
    Air,
    Brep,
    Step,
    Cir,
}

internal enum CirMirrorAtomKind
{
    Unknown,
    BoxPrimitive,
    CylinderPrimitive,
    SpherePrimitive,
    PrismaticSectionTransition,
    ProfileAuthoredVerticalChamfer,
}

internal sealed record CirMirrorProvenance(
    CirMirrorSourceRepresentationKind SourceRepresentationKind,
    string SourceRoute,
    string? SourceIdOrLabel = null,
    string? EmitterOrMirrorVersion = null,
    string? TopologySummary = null,
    string? ToleranceContext = null,
    IReadOnlyList<string>? Diagnostics = null);

internal sealed record CirMirrorDescriptor(
    string Name,
    CirMirrorAtomKind AtomKind,
    CirMirrorStatus Status,
    CirMirrorCapability Capabilities,
    CirMirrorLossFlags KnownLosses,
    CirMirrorProvenance Provenance,
    IReadOnlyList<string> Diagnostics);

internal sealed record CirMirrorMetadata(
    CirMirrorDescriptor Descriptor,
    string StatusText,
    IReadOnlyList<string> Diagnostics);

internal sealed record CirMirrorAdmission(
    CirMirrorSourceRepresentationKind SourceRepresentationKind,
    string SourceRoute,
    CirMirrorAtomKind AtomKind,
    CirMirrorCapability RequestedCapabilities,
    string? SourceIdOrLabel = null,
    string? EmitterOrMirrorVersion = null,
    string? ExpectedTopologySummary = null,
    string? ActualTopologySummary = null,
    string? ToleranceContext = null,
    string? DiagnosticsLabel = null);

internal sealed record CirMirrorAdmissionResult(
    CirMirrorStatus Status,
    CirMirrorCapability AllowedCapabilities,
    CirMirrorLossFlags KnownLosses,
    CirMirrorProvenance Provenance,
    IReadOnlyList<string> Diagnostics,
    CirMirrorDescriptor? Descriptor = null)
{
    public string StatusText => Status.ToStableString();

    public bool Supports(CirMirrorCapability capability) => (AllowedCapabilities & capability) == capability;

    public bool HasLoss(CirMirrorLossFlags loss) => (KnownLosses & loss) == loss;
}

internal static class CirMirrorRegistry
{
    public const string PrototypeVersion = "air-cir-x1-metadata-v1";

    public const CirMirrorCapability PrimitiveFieldCapabilities =
        CirMirrorCapability.PointContainment |
        CirMirrorCapability.ApproximateVolume |
        CirMirrorCapability.MapOccupancy |
        CirMirrorCapability.SectionSampling;

    public const CirMirrorLossFlags PrimitiveFieldLosses =
        CirMirrorLossFlags.FaceIdentityLost |
        CirMirrorLossFlags.LoopIdentityLost |
        CirMirrorLossFlags.SplitFaceLineageLost |
        CirMirrorLossFlags.FeatureRoleLabelsLost |
        CirMirrorLossFlags.BoundaryPrecisionLimited |
        CirMirrorLossFlags.ExactTopologyUnavailable;

    public const CirMirrorCapability PrismaticConvexMirrorCapabilities =
        CirMirrorCapability.PointContainment |
        CirMirrorCapability.MapOccupancy |
        CirMirrorCapability.SectionSampling;

    public const CirMirrorLossFlags PrismaticConvexMirrorLosses =
        CirMirrorLossFlags.FaceIdentityLost |
        CirMirrorLossFlags.LoopIdentityLost |
        CirMirrorLossFlags.SplitFaceLineageLost |
        CirMirrorLossFlags.FeatureRoleLabelsLost |
        CirMirrorLossFlags.ExactTopologyUnavailable;

    public static bool IsAdmittedPrimitive(CirMirrorAtomKind atomKind) =>
        atomKind is CirMirrorAtomKind.BoxPrimitive or CirMirrorAtomKind.CylinderPrimitive or CirMirrorAtomKind.SpherePrimitive;
}

internal static class CirMirrorAdmissionService
{
    public static CirMirrorAdmissionResult Admit(CirMirrorAdmission request)
    {
        var source = NormalizeDiagnosticToken(request.SourceRoute);
        var diagnostics = new List<string>
        {
            "air-cir-x1-mirror-admission-started",
        };

        if (!string.IsNullOrWhiteSpace(request.DiagnosticsLabel))
        {
            diagnostics.Add($"air-cir-x1-diagnostics-label:{NormalizeDiagnosticToken(request.DiagnosticsLabel)}");
        }

        if (IsStaleOrMismatched(request))
        {
            diagnostics.Add($"air-cir-x1-mirror-rejected-stale-or-mismatched:{source}");
            diagnostics.Add("air-cir-x1-no-production-analyzer-behavior-changed");
            return CreateResult(CirMirrorStatus.MirrorRejectedStaleOrMismatched, CirMirrorCapability.None, CirMirrorLossFlags.ExactTopologyUnavailable, request, diagnostics);
        }

        if ((request.RequestedCapabilities & CirMirrorCapability.FaceIdentity) != 0 ||
            (request.RequestedCapabilities & CirMirrorCapability.TopologyParity) != 0)
        {
            diagnostics.Add($"air-cir-x1-mirror-rejected-lossy-for-request:{RequestedCapabilityToken(request.RequestedCapabilities)}");
            diagnostics.Add("air-cir-x1-loss-face-identity");
            diagnostics.Add("air-cir-x1-loss-topology-parity");
            diagnostics.Add("air-cir-x1-no-production-analyzer-behavior-changed");
            return CreateResult(CirMirrorStatus.MirrorRejectedLossyForRequest, CirMirrorCapability.None, CirMirrorRegistry.PrimitiveFieldLosses, request, diagnostics);
        }

        if (CirMirrorRegistry.IsAdmittedPrimitive(request.AtomKind))
        {
            diagnostics.Add($"air-cir-x1-mirror-admitted-exact:{source}");
            diagnostics.Add("air-cir-x1-capability-map-occupancy");
            diagnostics.Add("air-cir-x1-capability-point-containment");
            diagnostics.Add("air-cir-x1-capability-approximate-volume");
            diagnostics.Add("air-cir-x1-no-production-analyzer-behavior-changed");
            diagnostics.Add("air-cir-x1-no-prismatic-mirror-created");
            diagnostics.Add("air-cir-x1-no-cir-to-brep-extraction");
            return CreateResult(CirMirrorStatus.MirrorAdmittedExact, CirMirrorRegistry.PrimitiveFieldCapabilities, CirMirrorRegistry.PrimitiveFieldLosses, request, diagnostics);
        }

        if (request.AtomKind == CirMirrorAtomKind.PrismaticSectionTransition)
        {
            diagnostics.Add($"air-cir-x1-mirror-rejected-unsupported-atom:{source}");
            diagnostics.Add($"air-cir-x1-mirror-unavailable:{source}");
            diagnostics.Add("air-cir-x1-no-prismatic-mirror-created");
            diagnostics.Add("air-cir-x1-no-production-analyzer-behavior-changed");
            diagnostics.Add("air-cir-x1-no-cir-to-brep-extraction");
            return CreateResult(CirMirrorStatus.MirrorRejectedUnsupportedAtom, CirMirrorCapability.None, CirMirrorLossFlags.ExactTopologyUnavailable, request, diagnostics);
        }

        if (request.AtomKind == CirMirrorAtomKind.ProfileAuthoredVerticalChamfer)
        {
            diagnostics.Add($"air-cir-x1-mirror-unavailable:{source}");
            diagnostics.Add("air-cir-x1-profile-chamfer-mirror-deferred");
            diagnostics.Add("air-cir-x1-no-prismatic-mirror-created");
            diagnostics.Add("air-cir-x1-no-production-analyzer-behavior-changed");
            diagnostics.Add("air-cir-x1-no-cir-to-brep-extraction");
            return CreateResult(CirMirrorStatus.MirrorUnavailable, CirMirrorCapability.None, CirMirrorLossFlags.ExactTopologyUnavailable, request, diagnostics);
        }

        diagnostics.Add($"air-cir-x1-mirror-unavailable:{source}");
        diagnostics.Add("air-cir-x1-no-production-analyzer-behavior-changed");
        diagnostics.Add("air-cir-x1-no-cir-to-brep-extraction");
        return CreateResult(CirMirrorStatus.MirrorUnavailable, CirMirrorCapability.None, CirMirrorLossFlags.ExactTopologyUnavailable, request, diagnostics);
    }

    private static CirMirrorAdmissionResult CreateResult(
        CirMirrorStatus status,
        CirMirrorCapability allowedCapabilities,
        CirMirrorLossFlags knownLosses,
        CirMirrorAdmission request,
        IReadOnlyList<string> diagnostics)
    {
        var provenance = new CirMirrorProvenance(
            request.SourceRepresentationKind,
            request.SourceRoute,
            request.SourceIdOrLabel,
            request.EmitterOrMirrorVersion ?? CirMirrorRegistry.PrototypeVersion,
            request.ActualTopologySummary ?? request.ExpectedTopologySummary,
            request.ToleranceContext,
            diagnostics);
        var descriptor = new CirMirrorDescriptor(
            request.SourceRoute,
            request.AtomKind,
            status,
            allowedCapabilities,
            knownLosses,
            provenance,
            diagnostics);
        return new CirMirrorAdmissionResult(status, allowedCapabilities, knownLosses, provenance, diagnostics, descriptor);
    }

    private static bool IsStaleOrMismatched(CirMirrorAdmission request) =>
        !string.IsNullOrWhiteSpace(request.ExpectedTopologySummary) &&
        !string.IsNullOrWhiteSpace(request.ActualTopologySummary) &&
        !string.Equals(request.ExpectedTopologySummary, request.ActualTopologySummary, StringComparison.Ordinal);

    private static string RequestedCapabilityToken(CirMirrorCapability capabilities)
    {
        var tokens = new List<string>();
        if ((capabilities & CirMirrorCapability.FaceIdentity) != 0)
        {
            tokens.Add("face-identity");
        }

        if ((capabilities & CirMirrorCapability.TopologyParity) != 0)
        {
            tokens.Add("topology-parity");
        }

        if ((capabilities & CirMirrorCapability.MapOccupancy) != 0)
        {
            tokens.Add("map-occupancy");
        }

        if ((capabilities & CirMirrorCapability.PointContainment) != 0)
        {
            tokens.Add("point-containment");
        }

        if ((capabilities & CirMirrorCapability.ApproximateVolume) != 0)
        {
            tokens.Add("approximate-volume");
        }

        if ((capabilities & CirMirrorCapability.SectionSampling) != 0)
        {
            tokens.Add("section-sampling");
        }

        return tokens.Count == 0 ? "none" : string.Join("+", tokens);
    }

    private static string NormalizeDiagnosticToken(string value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().Replace(' ', '-');
}

internal static class CirMirrorStatusExtensions
{
    public static string ToStableString(this CirMirrorStatus status) => status switch
    {
        CirMirrorStatus.MirrorUnavailable => "mirror-unavailable",
        CirMirrorStatus.MirrorAdmittedExact => "mirror-admitted-exact",
        CirMirrorStatus.MirrorAdmittedConservative => "mirror-admitted-conservative",
        CirMirrorStatus.MirrorAdmittedApproximate => "mirror-admitted-approximate",
        CirMirrorStatus.MirrorRejectedUnsupportedAtom => "mirror-rejected-unsupported-atom",
        CirMirrorStatus.MirrorRejectedLossyForRequest => "mirror-rejected-lossy-for-request",
        CirMirrorStatus.MirrorRejectedStaleOrMismatched => "mirror-rejected-stale-or-mismatched",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown CIR mirror status."),
    };
}
