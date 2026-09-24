using Aetheris.Kernel.Core.Air;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>
/// Materializes Box holes whose mouth crosses the edge of the entry face.
/// <para>
/// The interior-hole routes build a box with circular inner loops, so they cannot express a hole that opens through a
/// side face. That is still an ordinary hole - a through hole drilled near an edge leaves a notch - and the author wrote
/// exactly what they meant, so no new syntax is involved. The Box is lowered as a prismatic section stack instead: one
/// Base rectangle over the stock thickness and one Remove disc per hole layer, resolved by the same planar arrangement
/// that already handles Compose/Remove. Shaft and counterbore stacks are prismatic and are supported; a countersink or
/// drill-point tip is conical, which a section stack cannot represent, so those are rejected with that reason.
/// </para>
/// </summary>
internal static class AirHoleBreakoutMaterializer
{
    public static AirHoleSimpleShaftMaterializationResult Execute(IReadOnlyList<AirHoleFeature> features, AirHoleSimpleShaftHost host)
    {
        var diagnostics = new List<string> { "hole-breakout: a hole crosses the edge of its entry face; lowering the Box and its holes as a prismatic section stack." };
        var halfW = host.Width / 2d; var halfD = host.Depth / 2d;
        var profiles = new Dictionary<string, ResolvedProfile2D>(StringComparer.Ordinal)
        {
            ["stock"] = Profile("stock", "Stock", Rectangle(halfW, halfD))
        };
        var operations = new List<PrismaticProfileOperation>
        {
            new("Stock", PrismaticProfileIntent.Base, "stock", host.ZMin, host.ZMax, "Stock", "hole-breakout:stock")
        };

        foreach (var feature in features)
        {
            if (feature.Placement is not AirFaceLocalHolePlacement placement)
                return Fail(diagnostics, $"Hole '{feature.FeatureId}' cuts through a side face, which is supported only for face-local placements on +Z or -Z.");
            if (feature.Stack.Kind == AirHoleStackKind.Countersink)
                return Fail(diagnostics, $"Hole '{feature.FeatureId}' cuts through a side face, but its countersink is conical and a conical hole breaking out through a side face is not supported yet. Move it inward to keep a wall, or use a counterbore.");
            if (feature.Termination is AirHoleTermination.DrillPoint)
                return Fail(diagnostics, $"Hole '{feature.FeatureId}' cuts through a side face, but its drill-point tip is conical and is not supported when the hole breaks out yet. Use a flat-bottomed or through hole there.");

            var top = feature.Axis.Direction.Z > 0d;
            var (cutMin, cutMax) = feature.EndCondition switch
            {
                AirHoleEndCondition.ThroughAll => (host.ZMin, host.ZMax),
                AirHoleEndCondition.Depth depth => top ? (Math.Max(host.ZMin, host.ZMax - depth.Value), host.ZMax) : (host.ZMin, Math.Min(host.ZMax, host.ZMin + depth.Value)),
                _ => (double.NaN, double.NaN)
            };
            if (double.IsNaN(cutMin))
                return Fail(diagnostics, $"Hole '{feature.FeatureId}' cuts through a side face with end condition {feature.EndCondition.Kind}, which this route does not support yet.");

            var shaftKey = $"{feature.FeatureId}.shaft";
            profiles[shaftKey] = Profile(shaftKey, "Bore", new LineArcFullCircle2D((placement.U, placement.V), feature.Shaft.Radius));
            operations.Add(new(feature.FeatureId, PrismaticProfileIntent.Remove, shaftKey, cutMin, cutMax, "Hole", feature.FeatureId, feature.FeatureId, "Hole", feature.Shaft.Diameter));

            if (feature.Stack.Components.OfType<AirHoleCounterboreComponent>().SingleOrDefault() is { } counterbore)
            {
                var (boreMin, boreMax) = top ? (Math.Max(cutMin, cutMax - counterbore.Depth), cutMax) : (cutMin, Math.Min(cutMax, cutMin + counterbore.Depth));
                var counterboreKey = $"{feature.FeatureId}.counterbore";
                profiles[counterboreKey] = Profile(counterboreKey, "Counterbore", new LineArcFullCircle2D((placement.U, placement.V), counterbore.Radius));
                operations.Add(new($"{feature.FeatureId}.Counterbore", PrismaticProfileIntent.Remove, counterboreKey, boreMin, boreMax, "Hole", feature.FeatureId, feature.FeatureId, "Hole", counterbore.Radius * 2d));
            }
        }

        var levels = operations.SelectMany(o => new[] { o.From, o.To }).Distinct().Order().ToArray();
        var composition = new PrismaticProfileCompositionFeature(
            features[0].TargetBodyId ?? "semantic-hole-host", "XY", "+Z",
            new PrismaticProfilePlacement("LegacyImplicitWorldXY", 0d, 0d, 0d, "XY", "+Z", "+X", false),
            operations, levels, "hole-breakout");
        var stack = PrismaticSectionStackCompiler.Normalize(new PrismaticProfileCompositionParseResult(composition, profiles, []), out var stackDiagnostics);
        diagnostics.AddRange(stackDiagnostics);
        if (stack is null) return Fail(diagnostics, "The section stack for the broken-out hole could not be resolved; see the arrangement diagnostics above.");

        var emitted = PrismaticSectionStackEmitter.Emit(stack);
        diagnostics.AddRange(emitted.Diagnostics);
        if (emitted.Body is null) return Fail(diagnostics, "The section stack for the broken-out hole resolved but did not materialize a body.");

        diagnostics.Add("hole-breakout: materialized through the prismatic section stack.");
        return new(AirHoleSimpleShaftMaterializationStatus.Succeeded, null, emitted.Body, diagnostics, emitted.Correspondence);
    }

    private static AirHoleSimpleShaftMaterializationResult Fail(List<string> diagnostics, string reason)
    {
        diagnostics.Add("rejected: " + reason);
        return new(AirHoleSimpleShaftMaterializationStatus.UnsupportedPlacement, null, null, diagnostics);
    }

    private static IReadOnlyList<LineArcProfileCurve2D> Rectangle(double halfW, double halfD) =>
    [
        new LineArcLineSegment2D((-halfW, -halfD), (halfW, -halfD)),
        new LineArcLineSegment2D((halfW, -halfD), (halfW, halfD)),
        new LineArcLineSegment2D((halfW, halfD), (-halfW, halfD)),
        new LineArcLineSegment2D((-halfW, halfD), (-halfW, -halfD)),
    ];

    private static ResolvedProfile2D Profile(string name, string segmentPrefix, params LineArcProfileCurve2D[] curves) => Profile(name, segmentPrefix, (IReadOnlyList<LineArcProfileCurve2D>)curves);

    private static ResolvedProfile2D Profile(string name, string segmentPrefix, IReadOnlyList<LineArcProfileCurve2D> curves)
    {
        string[] sides = ["Bottom", "Right", "Top", "Left"];
        var segments = curves.Select((curve, index) =>
        {
            var segmentName = curves.Count == 4 ? sides[index] : segmentPrefix;
            return new ResolvedProfileSegment2D(segmentName, curve,
                new ProfileSegmentProvenance($"profile:{name}.Outer.{segmentName}", $"hole-breakout:{name}", "hole-breakout", "HoleBreakout", "XY"));
        }).ToArray();
        return new ResolvedProfile2D(name, "XY", [new ResolvedProfileLoop2D("Outer", true, segments)]);
    }
}
