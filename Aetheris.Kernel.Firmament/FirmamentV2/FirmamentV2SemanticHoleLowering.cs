using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

internal static class FirmamentV2SemanticHoleLowering
{
    public static AirHoleFeature Lower(FirmamentV2Document document, FirmamentV2ModifyBlock modify, FirmamentV2SemanticHoleDecl hole)
    {
        // This lowerer receives parser-validated declarations.  Keep the contract
        // explicit nevertheless: optional parsed scalars are never dereferenced.
        static double Required(double? value, string field) => value ?? throw new ArgumentException($"Validated hole is missing {field}.", nameof(value));
        if (hole.Placement is FirmamentV2ConstructionPlaneHolePlacement construction)
        {
            var plane = construction.Plane;
            var constructionPlacement = new AirConstructionPlaneHolePlacement(plane.StableId, plane.SourceConceptId, plane.Origin, plane.AxisX, plane.AxisY, plane.AxisZ,
                construction.Center.U, construction.Center.V, $"{construction.SourceSpan.Start}:{construction.SourceSpan.Length}", plane.Provenance);
            AirHoleEndCondition constructionEnd = hole.EndCondition.Kind switch
            {
                FirmamentV2SemanticHoleEndKind.ThroughAll => new AirHoleEndCondition.ThroughAll(),
                FirmamentV2SemanticHoleEndKind.ShaftDepth => new AirHoleEndCondition.ShaftDepth(Required(hole.EndCondition.Depth, "end depth")),
                FirmamentV2SemanticHoleEndKind.TotalDepth => new AirHoleEndCondition.TotalDepth(Required(hole.EndCondition.Depth, "end depth")),
                _ => new AirHoleEndCondition.Depth(Required(hole.EndCondition.Depth, "end depth"))
            };
            AirHoleTermination? constructionTermination = hole.Termination?.Kind switch
            {
                FirmamentV2SemanticHoleTerminationKind.DrillPoint => new AirHoleTermination.DrillPoint(hole.Termination.PointAngleDegrees ?? AirHoleTermination.DrillPoint.DefaultPointAngleDegrees),
                _ => null
            };
            var constructionProvenance = new AirProvenance("CONSTRUCTION-PLANE-HOLE-SOURCE-X3", "Construction Plane semantic hole source", hole.Name,
                $"{modify.TargetSolid}.{hole.Name}", nameof(AirConstructionPlaneHolePlacement), AirSelectionClass.None, AirRuleKind.None,
                "FirmamentV2 Hole<Shaft> From ConstructionPlane", true,
                [$"target-solid:{modify.TargetSolid}", $"construction-plane:{plane.StableId}", $"source-concept-plane:{plane.SourceConceptId}",
                    $"local-center:[{construction.Center.U:R},{construction.Center.V:R}]", "extent:" + constructionEnd.Kind,
                    "termination:" + (constructionTermination?.Kind.ToString() ?? "FlatBottom"), constructionTermination is AirHoleTermination.DrillPoint point ? $"point-angle:{point.PointAngleDegrees:R}deg" : ""]);
            return AirHoleFeature.CreateConstructionPlaneSimpleShaft(hole.Name, $"{modify.TargetSolid}.{hole.Name}", modify.TargetSolid, constructionPlacement,
                new AirHoleShaft(hole.ShaftDiameter), constructionEnd, constructionProvenance, constructionTermination);
        }
        var pointSource = hole.ResolvedCenter is null ? null : new AirResolvedPoint3PlacementSource(
            hole.ResolvedCenter.X, hole.ResolvedCenter.Y, hole.ResolvedCenter.Z, hole.ResolvedCenter.StableId,
            hole.ResolvedCenter.SourceMember, hole.ResolvedCenter.Ordinal, hole.ResolvedCenter.PlacementFace,
            hole.ResolvedCenter.PlaneDistance, $"{hole.ResolvedCenter.SourceSpan.Start}:{hole.ResolvedCenter.SourceSpan.Length}");
        var placement = new AirFaceLocalHolePlacement(hole.EntryFace.Axis switch { "+Z" => "top", "-Z" => "bottom", _ => hole.EntryFace.Source }, hole.Center.U, hole.Center.V, FirmamentV2FaceLocalPoint2D.ConventionFor(hole.EntryFace.Axis), hole.EntryFace.ResolvedSelector, pointSource);
        var axis = new AirHoleAxis(Direction3D.Create(hole.EntryFace.Axis switch { "-Z" => new Vector3D(0,0,-1), "+Z" => new Vector3D(0,0,1), "+X" => new Vector3D(1,0,0), "-X" => new Vector3D(-1,0,0), "+Y" => new Vector3D(0,1,0), _ => new Vector3D(0,-1,0) }), true);
        var shaft = new AirHoleShaft(hole.ShaftDiameter);
        AirHoleEndCondition endCondition = hole.EndCondition.Kind == FirmamentV2SemanticHoleEndKind.ThroughAll ? new AirHoleEndCondition.ThroughAll() : new AirHoleEndCondition.Depth(Required(hole.EndCondition.Depth, "end depth"));
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
            FirmamentV2SemanticHoleVariant.Counterbore => AirHoleFeature.CreateCounterbore(hole.Name, $"{modify.TargetSolid}.{hole.Name}", modify.TargetSolid, placement, axis, shaft, endCondition, new AirHoleCounterboreComponent(Required(hole.CounterboreDiameter, "CounterboreDiameter"), Required(hole.CounterboreDepth, "CounterboreDepth")), provenance),
            FirmamentV2SemanticHoleVariant.Countersink => AirHoleFeature.CreateCountersink(hole.Name, $"{modify.TargetSolid}.{hole.Name}", modify.TargetSolid, placement, axis, shaft, endCondition, new AirHoleCountersinkComponent(Required(hole.CountersinkDiameter, "CountersinkDiameter"), Required(hole.CountersinkAngleDegrees, "CountersinkAngle")), provenance),
            _ => AirHoleFeature.CreateSimpleShaft(hole.Name, $"{modify.TargetSolid}.{hole.Name}", modify.TargetSolid, placement, axis, shaft, endCondition, provenance)
        };
    }

    public static IReadOnlyList<AirHoleFeature> LowerSemanticHoles(FirmamentV2Document document) =>
        document.ModifyBlocks?.SelectMany(m => m.SemanticHoles.Select(h => Lower(document, m, h))).ToArray() ?? [];
}
