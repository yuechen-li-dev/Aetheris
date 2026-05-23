namespace Aetheris.Kernel.Firmament.Materializer;

public sealed record AirRectangleProfile(double Width, double Height);

public sealed record AirCenteredCircleLoop(double Radius);

public sealed record AirProfileRegion2D(
    AirRectangleProfile Outer,
    AirCenteredCircleLoop? InnerCircle,
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
            new AirProfileRegion2D(new AirRectangleProfile(spec.Width, spec.Depth), l.InnerCircleRadius.HasValue ? new AirCenteredCircleLoop(l.InnerCircleRadius.Value) : null, l.Role, l.Diagnostics),
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
            diagnostics = [.. d, "air-profile-stack-v1-blind-deferred"];
            return false;
        }

        if (plan.HoleKind == HoleKind.Counterbore)
        {
            diagnostics = [.. d, "air-profile-stack-v1-counterbore-deferred"];
            return false;
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
            ordered.Select(l => new ProfileStackLayer(l.ZMin, l.ZMax, l.Region.InnerCircle?.Radius, l.Role, l.Diagnostics)).ToArray(),
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
            if (l.Region.InnerCircle is not null && l.Region.InnerCircle.Radius <= tol) d.Add($"air-validation-failed-invalid-inner-radius[{i}]");
            if (i > 0 && Math.Abs(ordered[i - 1].ZMax - l.ZMin) > tol) d.Add($"air-validation-failed-non-contiguous[{i - 1}->{i}]");
        }

        if (Math.Abs(ordered[0].ZMin - air.GlobalZMin) > tol || Math.Abs(ordered[^1].ZMax - air.GlobalZMax) > tol) d.Add("air-validation-failed-global-bounds-mismatch");
        diagnostics = d;
        return d.Count == 0;
    }
}
