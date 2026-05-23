using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public sealed record AirRectangleProfile(double Width, double Height);
public sealed record AirCenteredCircleLoop(double Radius);
public sealed record AirProfileRegion2D(AirRectangleProfile OuterRectangle, AirCenteredCircleLoop? InnerCircle, string SemanticRole);
public sealed record AirProfileStackLayer(double ZMin, double ZMax, AirProfileRegion2D Region, string LayerRole, IReadOnlyList<string> Diagnostics);
public sealed record AirProfileStackExtrude(IReadOnlyList<AirProfileStackLayer> Layers, double GlobalZMin, double GlobalZMax, IReadOnlyList<string> Diagnostics);

public sealed record AirScenarioResult(
    string Scenario,
    bool Success,
    string Status,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> LayerRoles,
    IReadOnlyList<string> StepMarkers,
    bool HasBrepWithVoids);

public sealed record AirProfileStackExtrudeLabResult(
    AirProfileStackExtrude ThroughHole,
    AirProfileStackExtrude SteppedHole,
    AirProfileStackExtrude BlindHole,
    AirProfileStackExtrude Counterbore,
    AirScenarioResult ThroughHoleResult,
    AirScenarioResult SteppedHoleResult,
    AirScenarioResult BlindHoleResult,
    AirScenarioResult CounterboreResult,
    IReadOnlyList<string> MappingFindings,
    string MappingRecommendation,
    string DeferredFieldsSummary);

public static class AirProfileStackExtrudeLab
{
    public static AirProfileStackExtrudeLabResult Run()
    {
        var through = BuildThrough();
        var stepped = BuildStepped();
        var blind = BuildBlind();
        var counterbore = BuildCounterbore();

        return new(
            through,
            stepped,
            blind,
            counterbore,
            Execute("through-hole", through),
            Execute("stepped-hole", stepped),
            Execute("blind-hole", blind),
            Execute("counterbore", counterbore),
            [
                "mapping-option: HoleRecoveryPlan -> AIR gives complete semantic role/anchor diagnostics early.",
                "mapping-option: ProfileStackExtrudeSpec -> AIR is narrower and aligns directly to existing emitter contract.",
                "current-executor-contract: contiguous full-height layers with positive inner radius in every layer.",
                "blind/counterbore blocker category: emitter-side (ProfileStackExtrudeExecutor V2 shape gate), not AIR-model-side."
            ],
            "recommendation: AIR-V1 should normalize HoleRecoveryPlan -> ProfileStackExtrudeSpec -> AIR for executable cylindrical lanes, while preserving HoleRecoveryPlan diagnostics/provenance on AIR nodes.",
            "deferred-fields: arbitrary 2D loops, non-centered circles, multiple inner loops, loop orientation, 2D boolean provenance, non-rectangular outers.");
    }

    public static bool TryMapFromProfileStackSpec(Aetheris.Kernel.Firmament.Materializer.ProfileStackExtrudeSpec spec, out AirProfileStackExtrude? air, out IReadOnlyList<string> diagnostics)
    {
        var d = new List<string> { "air-map: source=ProfileStackExtrudeSpec" };
        var layers = new List<AirProfileStackLayer>();
        foreach (var layer in spec.Layers.OrderBy(l => l.ZMin))
        {
            var region = new AirProfileRegion2D(new AirRectangleProfile(spec.Width, spec.Depth),
                layer.InnerCircleRadius.HasValue ? new AirCenteredCircleLoop(layer.InnerCircleRadius.Value) : null,
                layer.Role);
            layers.Add(new AirProfileStackLayer(layer.ZMin, layer.ZMax, region, layer.Role, layer.Diagnostics));
        }

        air = new AirProfileStackExtrude(layers, spec.ZMin, spec.ZMax, spec.Diagnostics.Append("mapped-from-profile-stack-spec").ToArray());
        diagnostics = d;
        return true;
    }

    private static AirScenarioResult Execute(string name, AirProfileStackExtrude air)
    {
        var diagnostics = new List<string> { $"air-scenario:{name}" };
        var ordered = air.Layers.OrderBy(x => x.ZMin).ToArray();
        var kernelLayers = ordered.Select(x => new Aetheris.Kernel.Firmament.Materializer.ProfileStackLayer(x.ZMin, x.ZMax, x.Region.InnerCircle?.Radius, x.LayerRole, x.Diagnostics)).ToArray();
        var spec = new Aetheris.Kernel.Firmament.Materializer.ProfileStackExtrudeSpec(ordered[0].Region.OuterRectangle.Width, ordered[0].Region.OuterRectangle.Height, air.GlobalZMin, air.GlobalZMax, kernelLayers, air.Diagnostics);
        var emitted = ProfileStackExtrudeExecutor.Execute(spec);
        diagnostics.AddRange(emitted.Diagnostics);

        if (emitted.Status != ProfileStackExtrudeExecutionStatus.Succeeded || emitted.Body is null)
        {
            return new AirScenarioResult(name, false, $"blocker:emitter:{emitted.Status}", diagnostics, ordered.Select(x => x.LayerRole).ToArray(), [], false);
        }

        var step = Step242Exporter.ExportBody(emitted.Body);
        if (!step.IsSuccess)
        {
            diagnostics.AddRange(step.Diagnostics.Select(d => d.Message));
            return new AirScenarioResult(name, false, "blocker:step-export", diagnostics, ordered.Select(x => x.LayerRole).ToArray(), [], false);
        }

        var text = step.Value;
        var markers = new[] { "ISO-10303-21", "MANIFOLD_SOLID_BREP", "ADVANCED_FACE", "CYLINDRICAL_SURFACE" }
            .Where(m => text.Contains(m, StringComparison.Ordinal)).ToArray();
        return new AirScenarioResult(name, true, "success", diagnostics, ordered.Select(x => x.LayerRole).ToArray(), markers, text.Contains("BREP_WITH_VOIDS", StringComparison.Ordinal));
    }

    private static AirProfileStackExtrude BuildThrough() =>
        new([
            new AirProfileStackLayer(-10, 10,
                new AirProfileRegion2D(new AirRectangleProfile(30, 30), new AirCenteredCircleLoop(2), "rectangle-minus-circle"),
                "through-core", [])
        ], -10, 10, ["scenario=through-hole"]);

    private static AirProfileStackExtrude BuildStepped() =>
        new([
            new AirProfileStackLayer(-10, 2, new AirProfileRegion2D(new AirRectangleProfile(30, 30), new AirCenteredCircleLoop(2), "small-core"), "stepped-layer-small", []),
            new AirProfileStackLayer(2, 6, new AirProfileRegion2D(new AirRectangleProfile(30, 30), new AirCenteredCircleLoop(3), "medium-relief"), "stepped-layer-medium", []),
            new AirProfileStackLayer(6, 10, new AirProfileRegion2D(new AirRectangleProfile(30, 30), new AirCenteredCircleLoop(4), "large-relief"), "stepped-layer-large", [])
        ], -10, 10, ["scenario=stepped-hole"]);

    private static AirProfileStackExtrude BuildBlind() =>
        new([
            new AirProfileStackLayer(-10, 2, new AirProfileRegion2D(new AirRectangleProfile(30, 30), null, "solid-zone"), "blind-solid-lower", []),
            new AirProfileStackLayer(2, 10, new AirProfileRegion2D(new AirRectangleProfile(30, 30), new AirCenteredCircleLoop(2), "blind-cut"), "blind-cut-upper", [])
        ], -10, 10, ["scenario=blind-hole"]);

    private static AirProfileStackExtrude BuildCounterbore() =>
        new([
            new AirProfileStackLayer(-10, 6, new AirProfileRegion2D(new AirRectangleProfile(30, 30), new AirCenteredCircleLoop(2), "deep-small"), "counterbore-deep-small", []),
            new AirProfileStackLayer(6, 10, new AirProfileRegion2D(new AirRectangleProfile(30, 30), new AirCenteredCircleLoop(4), "entry-large"), "counterbore-entry-large", [])
        ], -10, 10, ["scenario=counterbore"]);
}
