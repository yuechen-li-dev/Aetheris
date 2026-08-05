using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Materializer;

public sealed record SemanticHoleInspectionResult(bool Succeeded, string? HoleId, BrepBody? Body, SemanticTopologyCorrespondence? Correspondence, IReadOnlyList<string> Diagnostics);
public static class SemanticHoleInspection
{
    public static SemanticHoleInspectionResult Inspect(FirmamentV2Document document)
    {
        var holes = FirmamentV2SemanticHoleLowering.LowerSemanticHoles(document);
        var binding = document.Solids.FirstOrDefault();
        if (holes.Count != 1 || binding?.Primitive is not FirmamentV2BoxRecord box || box.Size.Count != 3) return new(false, null, null, null, ["MissingCorrespondenceEvidence: bounded inspection requires one shaft hole in one Box host."]);
        var host = document.ConceptIr is null ? new AirHoleSimpleShaftHost(box.Size[0], box.Size[1], -box.Size[2] / 2d, box.Size[2] / 2d) : new AirHoleSimpleShaftHost(box.Size[0], box.Size[1], 0d, box.Size[2]);
        var result = AirHoleSimpleShaftMaterializer.Execute(holes[0], host);
        return new(result.Succeeded && result.Correspondence is not null, holes[0].FeatureId, result.Body, result.Correspondence, result.Diagnostics);
    }
}

internal enum AirHoleSimpleShaftMaterializationStatus
{
    Succeeded,
    InvalidSemanticHole,
    UnsupportedPlacement,
    ExecutionFailed
}

internal sealed record AirHoleSimpleShaftHost(
    double Width,
    double Depth,
    double ZMin,
    double ZMax,
    string TopFaceName = "top",
    string BottomFaceName = "bottom")
{
    public double Thickness => ZMax - ZMin;
}

internal sealed record AirHoleSimpleShaftMaterializationPlan(
    AirHoleFeature SemanticFeature,
    AirHoleSimpleShaftHost Host,
    string SemanticFeatureId,
    string SemanticSourceKind,
    string EntryFaceName,
    double CenterU,
    double CenterV,
    double AxisZ,
    double Radius,
    double CutZMin,
    double CutZMax,
    AirHoleEndConditionKind EndConditionKind,
    AirHoleStackKind StackKind,
    IReadOnlyList<AirHoleStackComponentKind> StackComponentRoles,
    ProfileStackExtrudeSpec ProfileStackSpec,
    IReadOnlyList<string> Diagnostics);

internal sealed record AirHoleSimpleShaftMaterializationResult(
    AirHoleSimpleShaftMaterializationStatus Status,
    AirHoleSimpleShaftMaterializationPlan? Plan,
    BrepBody? Body,
    IReadOnlyList<string> Diagnostics,
    SemanticTopologyCorrespondence? Correspondence = null)
{
    public bool Succeeded => Status == AirHoleSimpleShaftMaterializationStatus.Succeeded;
}

internal static class AirHoleSimpleShaftMaterializer
{
    private const double Tolerance = 1e-9;

    public static AirHoleSimpleShaftMaterializationResult Execute(AirHoleFeature feature, AirHoleSimpleShaftHost host)
    {
        var planResult = TryCreatePlan(feature, host);
        if (planResult.Status != AirHoleSimpleShaftMaterializationStatus.Succeeded || planResult.Plan is null)
        {
            return planResult;
        }

        var diagnostics = planResult.Diagnostics.ToList();
        diagnostics.Add("air-hole-x2 execution route: semantic AirHoleFeature -> simple shaft materialization plan -> ProfileStackExtrudeExecutor.");
        diagnostics.Add($"air-hole-x2 semantic-parent featureId={planResult.Plan.SemanticFeatureId} source={planResult.Plan.SemanticSourceKind}.");

        var execution = ProfileStackExtrudeExecutor.Execute(planResult.Plan.ProfileStackSpec);
        diagnostics.AddRange(execution.Diagnostics);
        if (execution.Status != ProfileStackExtrudeExecutionStatus.Succeeded || execution.Body is null)
        {
            diagnostics.Add($"air-hole-x2 profile-stack execution failed: {execution.Status}.");
            return new(AirHoleSimpleShaftMaterializationStatus.ExecutionFailed, planResult.Plan, null, diagnostics);
        }

        diagnostics.Add("air-hole-x2 materialization succeeded with semantic parent preserved; ProfileStackExtrudeSpec is lowering furniture, not source truth.");
        var correspondence = BuildCorrespondence(planResult.Plan, execution.Body);
        diagnostics.Add("air-hole-x2 semantic correspondence published: entry/exit loops and edges plus shaft wall face.");
        return new(AirHoleSimpleShaftMaterializationStatus.Succeeded, planResult.Plan, execution.Body, diagnostics, correspondence);
    }

    public static AirHoleSimpleShaftMaterializationResult TryCreatePlan(AirHoleFeature feature, AirHoleSimpleShaftHost host)
    {
        var diagnostics = new List<string> { "air-hole-x2 semantic hole planner started." };
        if (!feature.IsValid)
        {
            diagnostics.AddRange(feature.Diagnostics.Select(d => $"semantic diagnostic {d.Code}: {d.Message}"));
            diagnostics.Add("air-hole-x2 rejected: semantic hole invalid before materialization.");
            return new(AirHoleSimpleShaftMaterializationStatus.InvalidSemanticHole, null, null, diagnostics);
        }

        if (!IsFinite(host.Width) || !IsFinite(host.Depth) || !IsFinite(host.ZMin) || !IsFinite(host.ZMax) || host.Width <= 0d || host.Depth <= 0d || host.Thickness <= Tolerance)
        {
            diagnostics.Add("air-hole-x2 rejected: host must be a finite rectangular profile stack with positive width/depth/thickness.");
            return new(AirHoleSimpleShaftMaterializationStatus.UnsupportedPlacement, null, null, diagnostics);
        }

        if (Math.Abs(feature.Placement.U) + feature.Shaft.Radius > host.Width / 2d + Tolerance ||
            Math.Abs(feature.Placement.V) + feature.Shaft.Radius > host.Depth / 2d + Tolerance)
        {
            diagnostics.Add("air-hole-x2 rejected: face-local center/radius does not fit within the supported rectangular entry face.");
            return new(AirHoleSimpleShaftMaterializationStatus.UnsupportedPlacement, null, null, diagnostics);
        }

        var axisZ = feature.Axis.Direction.Z;
        var top = string.Equals(feature.Placement.EntryFaceName, host.TopFaceName, StringComparison.OrdinalIgnoreCase) && axisZ > 1d - Tolerance;
        var bottom = string.Equals(feature.Placement.EntryFaceName, host.BottomFaceName, StringComparison.OrdinalIgnoreCase) && axisZ < -1d + Tolerance;
        if (!top && !bottom)
        {
            diagnostics.Add("air-hole-x2 rejected: only planar top/+Z and bottom/-Z face-local placements are supported by this rectangular profile-stack lowering lane.");
            return new(AirHoleSimpleShaftMaterializationStatus.UnsupportedPlacement, null, null, diagnostics);
        }

        var (cutZMin, cutZMax) = ResolveCutSpan(feature, host, top);
        if (cutZMax - cutZMin <= Tolerance)
        {
            diagnostics.Add("air-hole-x2 rejected: resolved cut span is empty.");
            return new(AirHoleSimpleShaftMaterializationStatus.UnsupportedPlacement, null, null, diagnostics);
        }

        var layers = BuildLayers(feature, host, cutZMin, cutZMax).ToArray();
        var spec = new ProfileStackExtrudeSpec(host.Width, host.Depth, host.ZMin, host.ZMax, layers,
            [$"air-hole-x2 provenance featureId={feature.FeatureId}", $"air-hole-x2 provenance source={nameof(AirHoleFeature)}"],
            feature.Placement.U,
            feature.Placement.V);
        var plan = new AirHoleSimpleShaftMaterializationPlan(feature, host, feature.FeatureId, nameof(AirHoleFeature), feature.Placement.EntryFaceName,
            feature.Placement.U, feature.Placement.V, axisZ, feature.Shaft.Radius, cutZMin, cutZMax, feature.EndCondition.Kind, feature.Stack.Kind, feature.Stack.Components.Select(c => c.Kind).ToArray(), spec, diagnostics.ToArray());
        diagnostics.Add("air-hole-x2/x3 plan created; semantic AirHoleFeature remains parent intent and owns stack components.");
        return new(AirHoleSimpleShaftMaterializationStatus.Succeeded, plan, null, diagnostics);
    }

    private static (double CutZMin, double CutZMax) ResolveCutSpan(AirHoleFeature feature, AirHoleSimpleShaftHost host, bool top)
    {
        if (feature.EndCondition is AirHoleEndCondition.ThroughAll) return (host.ZMin, host.ZMax);
        var depth = ((AirHoleEndCondition.Depth)feature.EndCondition).Value;
        return top ? (Math.Max(host.ZMin, host.ZMax - depth), host.ZMax) : (host.ZMin, Math.Min(host.ZMax, host.ZMin + depth));
    }

    private static SemanticTopologyCorrespondence? BuildCorrespondence(AirHoleSimpleShaftMaterializationPlan plan, BrepBody body)
    {
        // The bounded ProfileStack executor emits the retained box faces first and shaft walls in ProfileStack order.
        // This is construction-plan ownership, not a post-hoc geometric recognizer.
        if (plan.EndConditionKind != AirHoleEndConditionKind.ThroughAll || plan.StackKind != AirHoleStackKind.SimpleShaft) return null;
        var faces = body.Topology.Faces.OrderBy(x => x.Id.Value).ToArray();
        if (faces.Length < 7) return null;
        var bottom = faces[0]; var top = faces[1]; var wall = faces[6];
        if (top.LoopIds.Count < 2 || bottom.LoopIds.Count < 2) return null;
        var entryLoop = top.LoopIds[^1]; var exitLoop = bottom.LoopIds[^1];
        EdgeId EdgeOf(LoopId loopId) => body.Topology.Coedges.Single(x => x.Id == body.Topology.Loops.Single(l => l.Id == loopId).CoedgeIds[0]).EdgeId;
        var source = $"hole:{plan.SemanticFeatureId}";
        var descendants = new SemanticTopologyDescendant[]
        {
            new($"material:{source}:entry-loop", "Loop", SemanticTopologyRole.HoleEntryLoop, source, Loop: entryLoop, ParentStableId: plan.SemanticFeatureId),
            new($"material:{source}:exit-loop", "Loop", SemanticTopologyRole.HoleExitLoop, source, Loop: exitLoop, ParentStableId: plan.SemanticFeatureId),
            new($"material:{source}:entry-edge", "Edge", SemanticTopologyRole.TopBoundary, source, Edge: EdgeOf(entryLoop), ParentStableId: plan.SemanticFeatureId),
            new($"material:{source}:exit-edge", "Edge", SemanticTopologyRole.BottomBoundary, source, Edge: EdgeOf(exitLoop), ParentStableId: plan.SemanticFeatureId),
            new($"material:{source}:wall", "Face", SemanticTopologyRole.HoleWallFace, source, Face: wall.Id, ParentStableId: plan.SemanticFeatureId)
        };
        return new(plan.SemanticFeature.TargetBodyId ?? "semantic-hole-host", descendants, ["HoleAIR", "AirHoleSimpleShaftMaterializationPlan", "ProfileStackExtrudeSpec", "AuthoritativeBRepPlan"]);
    }

    private static IEnumerable<ProfileStackLayer> BuildLayers(AirHoleFeature feature, AirHoleSimpleShaftHost host, double cutZMin, double cutZMax)
    {
        if (cutZMin > host.ZMin + Tolerance) yield return new(host.ZMin, cutZMin, null, "air-hole-x2-solid-before-blind-depth", []);
        if (feature.Stack.Kind == AirHoleStackKind.Counterbore)
        {
            var cb = feature.Stack.Components.OfType<AirHoleCounterboreComponent>().Single();
            var entryMin = Math.Max(cutZMin, cutZMax - cb.Depth);
            yield return new(cutZMin, cutZMax, feature.Shaft.Radius, $"air-hole-x3-shaft:{feature.FeatureId}", []);
            yield return new(entryMin, cutZMax, cb.Radius, $"air-hole-x3-counterbore-entry:{feature.FeatureId}", []);
        }
        else if (feature.Stack.Kind == AirHoleStackKind.Countersink)
        {
            var cs = feature.Stack.Components.OfType<AirHoleCountersinkComponent>().Single();
            var sinkDepth = cs.DerivedDepthForShaft(feature.Shaft);
            var entryMin = Math.Max(cutZMin, cutZMax - sinkDepth);
            yield return new(cutZMin, cutZMax, feature.Shaft.Radius, $"air-hole-x3-shaft:{feature.FeatureId}", []);
            yield return new(entryMin, cutZMax, cs.EntryRadius, $"air-hole-x3-countersink-entry:{feature.FeatureId}", [], feature.Shaft.Radius);
        }
        else
        {
            yield return new(cutZMin, cutZMax, feature.Shaft.Radius, $"air-hole-x2-simple-shaft:{feature.FeatureId}", []);
        }
        if (cutZMax < host.ZMax - Tolerance) yield return new(cutZMax, host.ZMax, null, "air-hole-x2-solid-after-blind-depth", []);
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
