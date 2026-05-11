namespace Aetheris.Kernel.Firmament.Execution;

internal enum SurfaceFeatureEvidenceStatus
{
    Planned,
    Deferred,
    Forge,
    Unsupported,
    Invalid
}

internal enum SurfaceFeatureGeometryStatus
{
    Exact,
    Deferred,
    DescriptorMissing
}

internal sealed record SurfaceFeatureEvidenceResult(
    bool Success,
    string FeatureId,
    SurfaceFeatureKind FeatureKind,
    string EvidenceKind,
    SurfaceFeatureEvidenceStatus Status,
    SurfaceFeatureCapabilityTier CapabilityTier,
    bool MaterializationClaimed,
    PlanarRoundGrooveEvidence? Evidence,
    IReadOnlyList<string> BlockingReasons,
    IReadOnlyList<string> Diagnostics);

internal sealed record PlanarRoundGrooveEvidence(
    string FeatureId,
    SurfacePatchFamily HostSurfaceFamily,
    SurfaceFeaturePathEvidence PathEvidence,
    SurfaceFeatureProfileEvidence ProfileEvidence,
    SurfaceFeatureDirection Direction,
    IReadOnlyList<SurfaceFeaturePatchRoleEvidence> PatchRoles,
    IReadOnlyList<SurfaceFeatureTrimRoleEvidence> TrimRoles,
    string ExactnessPolicy,
    string ExportPolicy,
    IReadOnlyList<string> Diagnostics);

internal sealed record SurfaceFeaturePathEvidence(
    SurfaceFeaturePathKind PathKind,
    string? Center,
    string? Normal,
    double? Radius,
    SurfaceFeatureGeometryStatus GeometryStatus,
    IReadOnlyList<string> Diagnostics);

internal sealed record SurfaceFeatureProfileEvidence(
    SurfaceFeatureProfileKind ProfileKind,
    double Radius,
    double Depth,
    double Width,
    SurfaceFeatureGeometryStatus GeometryStatus,
    IReadOnlyList<string> Diagnostics);

internal sealed record SurfaceFeaturePatchRoleEvidence(
    SurfaceFeaturePatchRole Role,
    SurfacePatchFamily SurfaceFamily,
    SurfaceFeatureCapabilityTier Capability,
    IReadOnlyList<string> Diagnostics);

internal sealed record SurfaceFeatureTrimRoleEvidence(
    SurfaceFeatureTrimRole Role,
    TrimCurveFamily CurveFamily,
    TrimCurveCapability Capability,
    string ExactnessPolicy,
    IReadOnlyList<string> Diagnostics);

internal static class SurfaceFeatureEvidenceGenerator
{
    internal static SurfaceFeatureEvidenceResult Generate(SurfaceFeatureDescriptor descriptor)
    {
        var plan = SurfaceFeaturePlanner.Plan(descriptor);
        var dryRun = SurfaceFeatureDryRunGenerator.Generate(descriptor);
        var diagnostics = new List<string>
        {
            "evidence-only: generates structured non-emitting evidence for future surface-feature materializers.",
            "a3-dry-run-consumed: evidence generation consumes SURFACE-FEATURE-A3 dry-run output.",
            "generic-torus-boolean-not-used: evidence remains semantic surface-feature planning and dry-run driven."
        };
        diagnostics.AddRange(plan.Diagnostics);
        diagnostics.AddRange(dryRun.Diagnostics);

        if (plan.Status != SurfaceFeaturePlanningStatus.Planned || !dryRun.Success)
        {
            diagnostics.Add("planner-or-dry-run-terminal: evidence stopped because planner/dry-run did not produce a planned groove dry-run.");
            return new(
                Success: false,
                FeatureId: descriptor.FeatureId,
                FeatureKind: plan.FeatureKind,
                EvidenceKind: "none",
                Status: MapStatus(plan.Status),
                CapabilityTier: plan.CapabilityTier,
                MaterializationClaimed: false,
                Evidence: null,
                BlockingReasons: plan.BlockingReasons.Concat(dryRun.BlockingReasons).Distinct(StringComparer.Ordinal).ToArray(),
                Diagnostics: diagnostics);
        }

        if (!IsPlanarRoundGroove(descriptor, plan))
        {
            diagnostics.Add("evidence-deferred: current A4 evidence scaffold is constrained to planar round groove with remove direction.");
            return new(false, descriptor.FeatureId, plan.FeatureKind, "none", SurfaceFeatureEvidenceStatus.Deferred, SurfaceFeatureCapabilityTier.Deferred, false, null, ["evidence-scope-limited"], diagnostics);
        }

        var pathEvidence = BuildPathEvidence(descriptor);
        var profileEvidence = new SurfaceFeatureProfileEvidence(
            descriptor.ProfileKind,
            Radius: descriptor.ProfileRadius,
            Depth: descriptor.DepthOrHeight,
            Width: descriptor.ProfileRadius * 2d,
            GeometryStatus: SurfaceFeatureGeometryStatus.Exact,
            Diagnostics: ["profile-parameters-from-descriptor: circular arc radius/depth captured from validated descriptor."]);

        var patchRoles = dryRun.PatchExpectations
            .Select(p => new SurfaceFeaturePatchRoleEvidence(p.PatchRole, p.SurfaceFamily, p.Capability, p.Diagnostics))
            .ToArray();

        var trimRoles = dryRun.TrimExpectations
            .Select(t => new SurfaceFeatureTrimRoleEvidence(t.TrimRole, t.CurveFamily, t.Capability, t.ExactnessPolicy, t.Diagnostics))
            .ToArray();

        var evidenceDiagnostics = new List<string>
        {
            "no-brep-emission: evidence does not emit faces/edges/loops/coedges.",
            "materialization-not-implemented: evidence is preparatory data for future materializers only.",
            "export-policy-deferred: STEP/export exactness remains deferred to later milestones."
        };

        var evidence = new PlanarRoundGrooveEvidence(
            descriptor.FeatureId,
            SurfacePatchFamily.Planar,
            pathEvidence,
            profileEvidence,
            descriptor.Direction,
            patchRoles,
            trimRoles,
            ExactnessPolicy: "circular host trims expected exact; profile coupling may require explicit BSpline approximation policy",
            ExportPolicy: "deferred-no-export-claim",
            Diagnostics: evidenceDiagnostics);

        diagnostics.Add("surface-feature-a4-success: planar round groove evidence assembled from A2 planner and A3 dry-run contracts.");

        return new(
            Success: true,
            FeatureId: descriptor.FeatureId,
            FeatureKind: plan.FeatureKind,
            EvidenceKind: "planar-round-groove",
            Status: SurfaceFeatureEvidenceStatus.Planned,
            CapabilityTier: SurfaceFeatureCapabilityTier.CoreExact,
            MaterializationClaimed: false,
            Evidence: evidence,
            BlockingReasons: ["brep-materialization-not-implemented", "topology-emission-not-implemented", "step-exactness-not-claimed"],
            Diagnostics: diagnostics);
    }

    private static SurfaceFeaturePathEvidence BuildPathEvidence(SurfaceFeatureDescriptor descriptor)
    {
        if (descriptor.PathParameters is null)
        {
            return new(
                descriptor.PathKind,
                Center: null,
                Normal: null,
                Radius: descriptor.CenterlineRadius,
                GeometryStatus: SurfaceFeatureGeometryStatus.DescriptorMissing,
                Diagnostics:
                [
                    "descriptor-missing-center-normal: path center/normal placement fields are not yet carried in SurfaceFeatureDescriptor.PathParameters.",
                    "radius-from-descriptor: centerline radius is available and captured."
                ]);
        }

        return new(
            descriptor.PathKind,
            Center: descriptor.PathParameters.TryGetValue("center", out var c) ? c.ToString("0.###") : null,
            Normal: descriptor.PathParameters.TryGetValue("normal", out var n) ? n.ToString("0.###") : null,
            Radius: descriptor.CenterlineRadius,
            GeometryStatus: SurfaceFeatureGeometryStatus.Deferred,
            Diagnostics: ["path-parameter-payload-present: parameter dictionary exists but typed center/normal vector fields are deferred in A4 scaffold."]);
    }

    private static bool IsPlanarRoundGroove(SurfaceFeatureDescriptor descriptor, SurfaceFeaturePlanningResult plan)
        => plan.FeatureKind == SurfaceFeatureKind.RoundGroove
           && descriptor.HostSurfaceFamily == SurfacePatchFamily.Planar
           && descriptor.PathKind == SurfaceFeaturePathKind.CircleOnPlane
           && descriptor.ProfileKind == SurfaceFeatureProfileKind.CircularArc
           && descriptor.Direction == SurfaceFeatureDirection.Remove;

    private static SurfaceFeatureEvidenceStatus MapStatus(SurfaceFeaturePlanningStatus status)
        => status switch
        {
            SurfaceFeaturePlanningStatus.Planned => SurfaceFeatureEvidenceStatus.Planned,
            SurfaceFeaturePlanningStatus.Deferred => SurfaceFeatureEvidenceStatus.Deferred,
            SurfaceFeaturePlanningStatus.Forge => SurfaceFeatureEvidenceStatus.Forge,
            SurfaceFeaturePlanningStatus.Unsupported => SurfaceFeatureEvidenceStatus.Unsupported,
            SurfaceFeaturePlanningStatus.Invalid => SurfaceFeatureEvidenceStatus.Invalid,
            _ => SurfaceFeatureEvidenceStatus.Unsupported
        };
}
