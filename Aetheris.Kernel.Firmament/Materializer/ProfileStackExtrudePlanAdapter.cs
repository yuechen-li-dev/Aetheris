namespace Aetheris.Kernel.Firmament.Materializer;

public static class ProfileStackExtrudePlanAdapter
{
    public static bool TryFromSteppedHolePlan(HoleRecoveryPlan plan, out ProfileStackExtrudeSpec? spec, out IReadOnlyList<string> diagnostics)
    {
        var d = new List<string> { "profile-stack adapter started." };
        spec = null;
        var tol = 1e-9;

        if (plan.HoleKind != HoleKind.Stepped || plan.DepthKind != HoleDepthKind.ThroughWithEntryRelief || plan.HostKind != HoleHostKind.RectangularBox || plan.Axis != HoleAxisKind.Z || plan.ProfileStack.Count != 3)
        { d.Add("Stepped plan rejected before Boolean: profile-stack adapter requires bounded stepped plan shape."); diagnostics = d; return false; }

        if (!HoleProfileSegmentPlacementValidator.TryValidate(plan, out var placementIssues))
        { d.AddRange(placementIssues.Select(x => $"Stepped plan rejected before Boolean: {x}.")); diagnostics = d; return false; }

        var large = plan.ProfileStack[0];
        var medium = plan.ProfileStack[1];
        var small = plan.ProfileStack[2];

        if (plan.ProfileStack.Any(s => s.SegmentKind != HoleProfileSegmentKind.Cylindrical)) { d.Add("Stepped plan rejected before Boolean: all profile segments must be cylindrical."); diagnostics = d; return false; }
        if (!small.IsThrough || small.AnchorSide != HoleTierAnchorSide.Through) { d.Add("Stepped plan rejected before Boolean: small tier must explicitly be through with through anchor."); diagnostics = d; return false; }
        if (medium.IsThrough || large.IsThrough) { d.Add("Stepped plan rejected before Boolean: medium/large tiers must be blind tiers (IsThrough=false)."); diagnostics = d; return false; }
        if (medium.AnchorSide != large.AnchorSide || (medium.AnchorSide != HoleTierAnchorSide.Top && medium.AnchorSide != HoleTierAnchorSide.Bottom)) { d.Add("Stepped plan rejected before Boolean: medium and large blind tiers must share a concrete entry anchor side."); diagnostics = d; return false; }
        if (small.RadiusStart >= medium.RadiusStart - tol || medium.RadiusStart >= large.RadiusStart - tol) { d.Add("Stepped plan rejected before Boolean: strict radius ordering (small < medium < large) is required."); diagnostics = d; return false; }

        var hostMinZ = plan.HostTranslation.Z - (plan.HostSizeZ * 0.5d);
        var hostMaxZ = plan.HostTranslation.Z + (plan.HostSizeZ * 0.5d);

        ProfileStackLayer[] layers;
        if (medium.AnchorSide == HoleTierAnchorSide.Top)
        {
            var z0 = hostMinZ;
            var z1 = medium.ZMin;
            var z2 = large.ZMin;
            var z3 = hostMaxZ;
            layers = [
                new(z0, z1, small.RadiusStart, "stepped-bottom-small", []),
                new(z1, z2, medium.RadiusStart, "stepped-middle-medium", []),
                new(z2, z3, large.RadiusStart, "stepped-top-large", [])
            ];
        }
        else
        {
            var z0 = hostMinZ;
            var z1 = large.ZMax;
            var z2 = medium.ZMax;
            var z3 = hostMaxZ;
            layers = [
                new(z0, z1, large.RadiusStart, "stepped-bottom-large", []),
                new(z1, z2, medium.RadiusStart, "stepped-middle-medium", []),
                new(z2, z3, small.RadiusStart, "stepped-top-small", [])
            ];
        }

        if (layers.Any(l => l.ZMax - l.ZMin <= tol)) { d.Add("Stepped plan rejected before Boolean: every tier must provide a valid explicit z-span."); diagnostics = d; return false; }

        spec = new ProfileStackExtrudeSpec(plan.HostSizeX, plan.HostSizeY, hostMinZ, hostMaxZ, layers,
            ["Stepped explicit-placement validation succeeded.", "stepped plan converted to profile-stack-extrude spec."]);
        d.Add("stepped plan converted to profile-stack-extrude spec.");
        diagnostics = d;
        return true;
    }
}
