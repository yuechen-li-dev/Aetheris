namespace Aetheris.Kernel.Firmament.Materializer;

public sealed record AirRectangleProfile(double Width, double Height);

public sealed record AirCenteredCircleLoop(double Radius);
public enum AirProfileStackLayerKind
{
    Unsupported = 0,
    SolidInterval = 1,
    CircularCutInterval = 2
}

public sealed record AirProfileRegion2D(
    AirRectangleProfile Outer,
    AirCenteredCircleLoop? InnerCircle,
    AirProfileStackLayerKind LayerKind,
    string Role,
    IReadOnlyList<string> Diagnostics);

public sealed record AirProfileStackLayer(
    double ZMin,
    double ZMax,
    AirProfileRegion2D Region,
    string Role,
    IReadOnlyList<string> Diagnostics);

public sealed record AirProfileStackExtrude(
    IReadOnlyList<AirProfileStackLayer> Layers,
    double GlobalZMin,
    double GlobalZMax,
    IReadOnlyList<string> Provenance,
    IReadOnlyList<string> Diagnostics);

public static class AirProfileStackExtrudeAdapter
{
    public static bool TryFromProfileStackSpec(ProfileStackExtrudeSpec spec, out AirProfileStackExtrude? air, out IReadOnlyList<string> diagnostics)
    {
        var d = new List<string> { "air-profile-stack adapter started.", "air-profile-stack-extrude", "air-converted-from-profile-stack-spec" };
        air = null;

        var layers = spec.Layers.Select((l, i) => new AirProfileStackLayer(
            l.ZMin,
            l.ZMax,
            new AirProfileRegion2D(new AirRectangleProfile(spec.Width, spec.Depth), l.InnerCircleRadius.HasValue ? new AirCenteredCircleLoop(l.InnerCircleRadius.Value) : null, l.InnerCircleRadius.HasValue ? AirProfileStackLayerKind.CircularCutInterval : AirProfileStackLayerKind.SolidInterval, l.Role, l.Diagnostics),
            l.Role,
            [.. l.Diagnostics, $"air-layer-index={i}"])).ToArray();

        var model = new AirProfileStackExtrude(layers, spec.ZMin, spec.ZMax, ["source:ProfileStackExtrudeSpec"], [.. spec.Diagnostics, .. d]);
        if (!TryValidate(model, out var issues))
        {
            diagnostics = [.. d, .. issues];
            return false;
        }

        air = model;
        diagnostics = d;
        return true;
    }

    public static bool TryFromHoleRecoveryPlan(HoleRecoveryPlan plan, out AirProfileStackExtrude? air, out IReadOnlyList<string> diagnostics)
    {
        var d = new List<string> { "air-profile-stack adapter started.", "air-profile-stack-extrude", "air-converted-from-hole-plan" };
        air = null;

        if (plan.HoleKind == HoleKind.Blind)
        {
            _ = TryFromBlindPlan(plan, d, out _, out var blindDiagnostics);
            diagnostics = [.. blindDiagnostics, "air-profile-stack-v2b-blind-emitter-deferred"];
            return false;
        }
        if (plan.HoleKind == HoleKind.Counterbore && TryFromCounterborePlan(plan, d, out air, out diagnostics))
        {
            return true;
        }

        if (plan.HoleKind is HoleKind.Countersink or HoleKind.ChamferedEntry)
        {
            diagnostics = [.. d, "air-profile-stack-v1-conical-deferred"];
            return false;
        }

        if (!ProfileStackExtrudePlanAdapter.TryFromHoleRecoveryPlan(plan, out var spec, out var profileDiagnostics) || spec is null)
        {
            diagnostics = [.. d, .. profileDiagnostics];
            return false;
        }

        if (!TryFromProfileStackSpec(spec, out var converted, out var specDiagnostics) || converted is null)
        {
            diagnostics = [.. d, .. profileDiagnostics, .. specDiagnostics];
            return false;
        }

        air = converted with { Provenance = ["source:HoleRecoveryPlan", "normalized:ProfileStackExtrudeSpec"], Diagnostics = [.. converted.Diagnostics, .. profileDiagnostics] };
        diagnostics = [.. d, .. profileDiagnostics, .. specDiagnostics];
        return true;
    }

    public static bool TryToProfileStackSpec(AirProfileStackExtrude air, out ProfileStackExtrudeSpec? spec, out IReadOnlyList<string> diagnostics)
    {
        var d = new List<string> { "air-to-profile-stack-executor" };
        spec = null;
        if (!TryValidate(air, out var issues))
        {
            diagnostics = [.. d, .. issues];
            return false;
        }

        var ordered = air.Layers.OrderBy(l => l.ZMin).ToArray();
        spec = new ProfileStackExtrudeSpec(
            ordered[0].Region.Outer.Width,
            ordered[0].Region.Outer.Height,
            air.GlobalZMin,
            air.GlobalZMax,
            ordered.Select(l => new ProfileStackLayer(l.ZMin, l.ZMax, l.Region.LayerKind == AirProfileStackLayerKind.CircularCutInterval ? l.Region.InnerCircle?.Radius : null, l.Role, l.Diagnostics)).ToArray(),
            [.. air.Diagnostics, .. d]);
        diagnostics = d;
        return true;
    }

    public static bool TryValidate(AirProfileStackExtrude air, out IReadOnlyList<string> diagnostics)
    {
        var d = new List<string>();
        var tol = 1e-9;
        var ordered = air.Layers.OrderBy(l => l.ZMin).ToArray();
        if (ordered.Length == 0 || air.GlobalZMax - air.GlobalZMin <= tol)
        {
            diagnostics = ["air-validation-failed-empty-or-global-z"];
            return false;
        }

        for (var i = 0; i < ordered.Length; i++)
        {
            var l = ordered[i];
            if (l.ZMax - l.ZMin <= tol) d.Add($"air-validation-failed-non-positive-span[{i}]");
            if (l.ZMin < air.GlobalZMin - tol || l.ZMax > air.GlobalZMax + tol) d.Add($"air-validation-failed-outside-global[{i}]");
            if (l.Region.Outer.Width <= tol || l.Region.Outer.Height <= tol) d.Add($"air-validation-failed-invalid-outer-rect[{i}]");
            if (l.Region.LayerKind == AirProfileStackLayerKind.CircularCutInterval)
            {
                if (l.Region.InnerCircle is null) d.Add($"air-validation-failed-missing-inner-loop[{i}]");
                else if (l.Region.InnerCircle.Radius <= tol) d.Add($"air-validation-failed-invalid-inner-radius[{i}]");
            }
            else if (l.Region.LayerKind == AirProfileStackLayerKind.SolidInterval && l.Region.InnerCircle is not null)
            {
                d.Add($"air-validation-failed-solid-interval-has-inner-loop[{i}]");
            }
            else if (l.Region.LayerKind == AirProfileStackLayerKind.Unsupported)
            {
                d.Add($"air-validation-failed-unsupported-layer-kind[{i}]");
            }
            if (i > 0 && Math.Abs(ordered[i - 1].ZMax - l.ZMin) > tol) d.Add($"air-validation-failed-non-contiguous[{i - 1}->{i}]");
        }

        if (Math.Abs(ordered[0].ZMin - air.GlobalZMin) > tol || Math.Abs(ordered[^1].ZMax - air.GlobalZMax) > tol) d.Add("air-validation-failed-global-bounds-mismatch");
        diagnostics = d;
        return d.Count == 0;
    }

    private static bool TryFromCounterborePlan(HoleRecoveryPlan plan, List<string> d, out AirProfileStackExtrude? air, out IReadOnlyList<string> diagnostics)
    {
        air = null;
        diagnostics = [];
        var tol = 1e-9;
        if (plan.DepthKind != HoleDepthKind.ThroughWithEntryRelief || plan.HostKind != HoleHostKind.RectangularBox || plan.Axis != HoleAxisKind.Z || plan.ProfileStack.Count != 2)
        { diagnostics = [.. d, "air-profile-stack-v2b-counterbore-rejected-shape"]; return false; }
        var large = plan.ProfileStack[0]; var small = plan.ProfileStack[1];
        if (large.SegmentKind != HoleProfileSegmentKind.Cylindrical || small.SegmentKind != HoleProfileSegmentKind.Cylindrical || !small.IsThrough || large.IsThrough || large.RadiusStart <= small.RadiusStart + tol)
        { diagnostics = [.. d, "air-profile-stack-v2b-counterbore-rejected-segment-constraints"]; return false; }
        var hostMin = plan.HostTranslation.Z - (plan.HostSizeZ * 0.5d); var hostMax = plan.HostTranslation.Z + (plan.HostSizeZ * 0.5d);
        var split = large.AnchorSide == HoleTierAnchorSide.Top ? large.ZMin : large.ZMax;
        var layers = large.AnchorSide == HoleTierAnchorSide.Top
            ? new[] { new AirProfileStackLayer(hostMin, split, new AirProfileRegion2D(new(plan.HostSizeX, plan.HostSizeY), new(small.RadiusStart), AirProfileStackLayerKind.CircularCutInterval, "counterbore-through-small", []), "counterbore-through-small", []), new AirProfileStackLayer(split, hostMax, new AirProfileRegion2D(new(plan.HostSizeX, plan.HostSizeY), new(large.RadiusStart), AirProfileStackLayerKind.CircularCutInterval, "counterbore-entry-large", []), "counterbore-entry-large", []) }
            : new[] { new AirProfileStackLayer(hostMin, split, new AirProfileRegion2D(new(plan.HostSizeX, plan.HostSizeY), new(large.RadiusStart), AirProfileStackLayerKind.CircularCutInterval, "counterbore-entry-large", []), "counterbore-entry-large", []), new AirProfileStackLayer(split, hostMax, new AirProfileRegion2D(new(plan.HostSizeX, plan.HostSizeY), new(small.RadiusStart), AirProfileStackLayerKind.CircularCutInterval, "counterbore-through-small", []), "counterbore-through-small", []) };
        var model = new AirProfileStackExtrude(layers, hostMin, hostMax, ["source:HoleRecoveryPlan"], ["air-profile-stack-v2b-counterbore-contiguous-accepted"]);
        if (!TryValidate(model, out var issues)) { diagnostics = [.. d, "air-profile-stack-v2b-counterbore-rejected-validation", .. issues]; return false; }
        air = model; diagnostics = [.. d, "air-profile-stack-v2b-counterbore-contiguous-accepted"]; return true;
    }

    private static bool TryFromBlindPlan(HoleRecoveryPlan plan, List<string> d, out AirProfileStackExtrude? air, out IReadOnlyList<string> diagnostics)
    {
        air = null;
        diagnostics = [];
        if (plan.DepthKind != HoleDepthKind.Blind || plan.HostKind != HoleHostKind.RectangularBox || plan.Axis != HoleAxisKind.Z || plan.ProfileStack.Count != 1)
        { diagnostics = [.. d, "air-profile-stack-v2b-blind-rejected-shape"]; return false; }
        var seg = plan.ProfileStack[0];
        if (seg.SegmentKind != HoleProfileSegmentKind.Cylindrical || seg.IsThrough || (seg.AnchorSide != HoleTierAnchorSide.Top && seg.AnchorSide != HoleTierAnchorSide.Bottom))
        { diagnostics = [.. d, "air-profile-stack-v2b-blind-rejected-segment-constraints"]; return false; }
        var hostMin = plan.HostTranslation.Z - (plan.HostSizeZ * 0.5d); var hostMax = plan.HostTranslation.Z + (plan.HostSizeZ * 0.5d);
        AirProfileStackLayer[] layers = seg.AnchorSide == HoleTierAnchorSide.Top
            ? [new(hostMin, seg.ZMin, new(new(plan.HostSizeX, plan.HostSizeY), null, AirProfileStackLayerKind.SolidInterval, "blind-solid", []), "blind-solid", []), new(seg.ZMin, hostMax, new(new(plan.HostSizeX, plan.HostSizeY), new(seg.RadiusStart), AirProfileStackLayerKind.CircularCutInterval, "blind-cut", []), "blind-cut", [])]
            : [new(hostMin, seg.ZMax, new(new(plan.HostSizeX, plan.HostSizeY), new(seg.RadiusStart), AirProfileStackLayerKind.CircularCutInterval, "blind-cut", []), "blind-cut", []), new(seg.ZMax, hostMax, new(new(plan.HostSizeX, plan.HostSizeY), null, AirProfileStackLayerKind.SolidInterval, "blind-solid", []), "blind-solid", [])];
        var model = new AirProfileStackExtrude(layers, hostMin, hostMax, ["source:HoleRecoveryPlan"], ["air-profile-stack-v2b-blind-solid-cut-accepted"]);
        if (!TryValidate(model, out var issues)) { diagnostics = [.. d, "air-profile-stack-v2b-blind-rejected-validation", .. issues]; return false; }
        air = model; diagnostics = [.. d, "air-profile-stack-v2b-blind-solid-interval-recognized"]; return true;
    }
}
