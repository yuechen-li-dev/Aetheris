using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

internal static class FirmamentV2SemanticHoleLowering
{
    public static AirHoleFeature Lower(FirmamentV2Document document, FirmamentV2ModifyBlock modify, FirmamentV2SemanticHoleDecl hole)
    {
        var pointSource = hole.ResolvedCenter is null ? null : new AirResolvedPoint3PlacementSource(
            hole.ResolvedCenter.X, hole.ResolvedCenter.Y, hole.ResolvedCenter.Z, hole.ResolvedCenter.StableId,
            hole.ResolvedCenter.SourceMember, hole.ResolvedCenter.Ordinal, hole.ResolvedCenter.PlacementFace,
            hole.ResolvedCenter.PlaneDistance, $"{hole.ResolvedCenter.SourceSpan.Start}:{hole.ResolvedCenter.SourceSpan.Length}");
        var placement = new AirFaceLocalHolePlacement(hole.EntryFace.Axis switch { "+Z" => "top", "-Z" => "bottom", _ => hole.EntryFace.Source }, hole.Center.U, hole.Center.V, FirmamentV2FaceLocalPoint2D.ConventionFor(hole.EntryFace.Axis), hole.EntryFace.ResolvedSelector, pointSource);
        var axis = new AirHoleAxis(Direction3D.Create(hole.EntryFace.Axis switch { "-Z" => new Vector3D(0,0,-1), "+Z" => new Vector3D(0,0,1), "+X" => new Vector3D(1,0,0), "-X" => new Vector3D(-1,0,0), "+Y" => new Vector3D(0,1,0), _ => new Vector3D(0,-1,0) }), true);
        var shaft = new AirHoleShaft(hole.ShaftDiameter);
        AirHoleEndCondition endCondition = hole.EndCondition.Kind == FirmamentV2SemanticHoleEndKind.ThroughAll ? new AirHoleEndCondition.ThroughAll() : new AirHoleEndCondition.Depth(hole.EndCondition.Depth!.Value);
        var notes = new List<string> { $"source-variant:{hole.Variant}", $"target-solid:{modify.TargetSolid}", $"entry-face:{hole.EntryFace.Source}" };
        if (hole.ResolvedCenter is { } center)
        {
            notes.Add($"center-source:{center.SourceMember}[{center.Ordinal}]");
            notes.Add($"center-stable-id:{center.StableId}");
            notes.Add($"center-point3:[{center.X:R},{center.Y:R},{center.Z:R}]");
            notes.Add($"center-plane-distance:{center.PlaneDistance:R}");
        }
        var provenance = new AirProvenance("CONCEPT-MATERIALIZATION-M2", "Typed Concept Point3 semantic hole source", hole.Name, $"{modify.TargetSolid}.{hole.Name}", nameof(AirHoleFeature), AirSelectionClass.None, AirRuleKind.None, $"FirmamentV2 hole<{hole.Variant}>", true, notes);
        return hole.Variant switch
        {
            FirmamentV2SemanticHoleVariant.Counterbore => AirHoleFeature.CreateCounterbore(hole.Name, $"{modify.TargetSolid}.{hole.Name}", modify.TargetSolid, placement, axis, shaft, endCondition, new AirHoleCounterboreComponent(hole.CounterboreDiameter!.Value, hole.CounterboreDepth!.Value), provenance),
            FirmamentV2SemanticHoleVariant.Countersink => AirHoleFeature.CreateCountersink(hole.Name, $"{modify.TargetSolid}.{hole.Name}", modify.TargetSolid, placement, axis, shaft, endCondition, new AirHoleCountersinkComponent(hole.CountersinkDiameter!.Value, hole.CountersinkAngleDegrees!.Value), provenance),
            _ => AirHoleFeature.CreateSimpleShaft(hole.Name, $"{modify.TargetSolid}.{hole.Name}", modify.TargetSolid, placement, axis, shaft, endCondition, provenance)
        };
    }

    public static IReadOnlyList<AirHoleFeature> LowerSemanticHoles(FirmamentV2Document document) =>
        document.ModifyBlocks?.SelectMany(m => m.SemanticHoles.Select(h => Lower(document, m, h))).ToArray() ?? [];
}
