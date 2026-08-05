using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Materializer;

public sealed record SemanticHoleSourceInspectionEvidence(
    string FeatureId, string PlacementKind, string? ConstructionPlaneId, string? SourceConceptPlaneId,
    double[]? FrameOrigin, double[]? AxisX, double[]? AxisY, double[]? AxisZ,
    double[] LocalCenter, double[]? WorldMouthCenter, double Diameter, double Radius,
    string Extent, double[]? HostInterval, string? PlanId, string? SourceSpan,
    double? DeclaredDepth = null, double? ShaftDepth = null, double? TipLength = null, double? TotalDepth = null, double? PointAngle = null,
    HoleHostTraversalEvidence? HostTraversal = null, HoleEndConditionContractEvidence? Contract = null);
public sealed record SemanticHoleInspectionResult(bool Succeeded, string? HoleId, BrepBody? Body, SemanticTopologyCorrespondence? Correspondence, IReadOnlyList<string> Diagnostics, SemanticHoleSourceInspectionEvidence? Evidence = null);
public static class SemanticHoleInspection
{
    public static SemanticHoleInspectionResult Inspect(FirmamentV2Document document)
    {
        var holes = FirmamentV2SemanticHoleLowering.LowerSemanticHoles(document);
        var binding = document.Solids.FirstOrDefault();
        if (holes.Count != 1 || binding?.Primitive is not FirmamentV2BoxRecord box || box.Size.Count != 3) return new(false, null, null, null, ["MissingCorrespondenceEvidence: bounded inspection requires one shaft hole in one Box host."]);
        var host = document.ConceptIr is null ? new AirHoleSimpleShaftHost(box.Size[0], box.Size[1], -box.Size[2] / 2d, box.Size[2] / 2d) : new AirHoleSimpleShaftHost(box.Size[0], box.Size[1], 0d, box.Size[2]);
        var result = AirHoleSimpleShaftMaterializer.Execute(holes[0], host);
        var feature = holes[0]; var placement = feature.ConstructionPlanePlacement;
        var drill = feature.Termination as AirHoleTermination.DrillPoint;
        var tipLength = drill is null ? (double?)null : feature.Shaft.Radius / Math.Tan(drill.PointAngleDegrees * Math.PI / 360d);
        double? declaredDepth = feature.EndCondition switch { AirHoleEndCondition.ShaftDepth d => d.Value, AirHoleEndCondition.TotalDepth d => d.Value, _ => null };
        double? shaftDepth = drill is null ? null : feature.EndCondition switch { AirHoleEndCondition.ShaftDepth d => d.Value, AirHoleEndCondition.TotalDepth d => d.Value - tipLength!.Value, _ => null };
        double? totalDepth = drill is null ? null : shaftDepth!.Value + tipLength!.Value;
        double[] Vector(Aetheris.Kernel.Core.Math.Vector3D v) => [v.X, v.Y, v.Z];
        var evidence = placement is null
            ? new SemanticHoleSourceInspectionEvidence(feature.FeatureId, "FaceLocal", null, null, null, null, null, null,
                [feature.Placement.U, feature.Placement.V], null, feature.Shaft.Diameter, feature.Shaft.Radius, feature.EndCondition.Kind.ToString(), null, null, null)
            : new SemanticHoleSourceInspectionEvidence(feature.FeatureId, "ConstructionPlane", placement.ConstructionPlaneId, placement.SourceConceptPlaneId,
                [placement.FrameOrigin.X, placement.FrameOrigin.Y, placement.FrameOrigin.Z], Vector(placement.AxisX.ToVector()), Vector(placement.AxisY.ToVector()), Vector(placement.AxisZ.ToVector()),
                [placement.LocalCenterX, placement.LocalCenterY], [placement.WorldMouthCenter.X, placement.WorldMouthCenter.Y, placement.WorldMouthCenter.Z],
                feature.Shaft.Diameter, feature.Shaft.Radius, feature.EndCondition.Kind.ToString(), result.Plan?.HoleBRepPlan is { } plan ? [plan.HostMaterialInterval.Start, plan.HostMaterialInterval.End] : null,
                result.Plan?.HoleBRepPlan?.StableId, placement.SourceSpan, declaredDepth, shaftDepth, tipLength, totalDepth, drill?.PointAngleDegrees,
                result.Plan?.HoleBRepPlan?.TraversalEvidence, result.Plan?.HoleBRepPlan?.ContractEvidence);
        return new(result.Succeeded && result.Correspondence is not null, feature.FeatureId, result.Body, result.Correspondence, result.Diagnostics, evidence);
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
    IReadOnlyList<string> Diagnostics,
    LocalFrameHoleBRepPlan? HoleBRepPlan = null);

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
        if (feature.ConstructionPlanePlacement is { } constructionPlacement)
            return ExecuteConstructionPlaneThroughAll(feature, host, constructionPlacement);

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
        if (feature.Placement is not AirFaceLocalHolePlacement placement)
        {
            diagnostics.Add("HoleConstructionPlaneHostUnsupported: the legacy profile-stack route accepts face-local placements only.");
            return new(AirHoleSimpleShaftMaterializationStatus.UnsupportedPlacement, null, null, diagnostics);
        }

        if (!IsFinite(host.Width) || !IsFinite(host.Depth) || !IsFinite(host.ZMin) || !IsFinite(host.ZMax) || host.Width <= 0d || host.Depth <= 0d || host.Thickness <= Tolerance)
        {
            diagnostics.Add("air-hole-x2 rejected: host must be a finite rectangular profile stack with positive width/depth/thickness.");
            return new(AirHoleSimpleShaftMaterializationStatus.UnsupportedPlacement, null, null, diagnostics);
        }

        if (Math.Abs(placement.U) + feature.Shaft.Radius > host.Width / 2d + Tolerance ||
            Math.Abs(placement.V) + feature.Shaft.Radius > host.Depth / 2d + Tolerance)
        {
            diagnostics.Add("air-hole-x2 rejected: face-local center/radius does not fit within the supported rectangular entry face.");
            return new(AirHoleSimpleShaftMaterializationStatus.UnsupportedPlacement, null, null, diagnostics);
        }

        var axisZ = feature.Axis.Direction.Z;
        var top = string.Equals(placement.EntryFaceName, host.TopFaceName, StringComparison.OrdinalIgnoreCase) && axisZ > 1d - Tolerance;
        var bottom = string.Equals(placement.EntryFaceName, host.BottomFaceName, StringComparison.OrdinalIgnoreCase) && axisZ < -1d + Tolerance;
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
            placement.U,
            placement.V);
        var plan = new AirHoleSimpleShaftMaterializationPlan(feature, host, feature.FeatureId, nameof(AirHoleFeature), placement.EntryFaceName,
            placement.U, placement.V, axisZ, feature.Shaft.Radius, cutZMin, cutZMax, feature.EndCondition.Kind, feature.Stack.Kind, feature.Stack.Components.Select(c => c.Kind).ToArray(), spec, diagnostics.ToArray());
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

    /// <summary>
    /// The initial LOCAL-FRAME-HOLES-X2 host query.  A rectangular Box is admitted
    /// only when the supplied proper frame is a signed permutation of the host
    /// world axes.  In that case transforming the eight Box corners proves one
    /// exact local-Z material interval; there is no spatial search or oversized
    /// cutting tool.  The resulting host-with-hole is planned as one ordinary
    /// circular-inner-loop local profile extrusion.
    /// </summary>
    private static AirHoleSimpleShaftMaterializationResult ExecuteConstructionPlaneThroughAll(
        AirHoleFeature feature, AirHoleSimpleShaftHost host, AirConstructionPlaneHolePlacement placement)
    {
        var diagnostics = new List<string> { "LocalFrameHoleConstructionPlanePlacement", "HoleBRepPlan", "HostMaterialIntervalQuery:BoxSignedPermutation" };
        if (!feature.IsValid)
        {
            diagnostics.AddRange(feature.Diagnostics.Select(d => $"semantic diagnostic {d.Code}: {d.Message}"));
            return new(AirHoleSimpleShaftMaterializationStatus.InvalidSemanticHole, null, null, diagnostics);
        }
        if (feature.EndCondition is not AirHoleEndCondition.ThroughAll && feature.Termination is not AirHoleTermination.DrillPoint)
        {
            diagnostics.Add("HoleConstructionPlaneExtentUnsupported: Construction Plane bounded holes require DrillPoint termination and explicit ShaftDepth or TotalDepth.");
            return new(AirHoleSimpleShaftMaterializationStatus.UnsupportedPlacement, null, null, diagnostics);
        }
        if (feature.Stack.Kind != AirHoleStackKind.SimpleShaft)
        {
            diagnostics.Add("HoleConstructionPlaneHostUnsupported: construction-plane execution currently admits a simple Box host and simple cylindrical shaft only.");
            return new(AirHoleSimpleShaftMaterializationStatus.UnsupportedPlacement, null, null, diagnostics);
        }
        if (!IsSignedAxis(placement.AxisX) || !IsSignedAxis(placement.AxisY) || !IsSignedAxis(placement.AxisZ) ||
            Math.Abs(placement.AxisX.ToVector().Dot(placement.AxisY.ToVector())) > Tolerance ||
            Math.Abs(placement.AxisX.ToVector().Dot(placement.AxisZ.ToVector())) > Tolerance ||
            Math.Abs(placement.AxisY.ToVector().Dot(placement.AxisZ.ToVector())) > Tolerance)
        {
            diagnostics.Add("HoleConstructionPlaneOrientationUnsupported: admitted Box host requires a proper signed-permutation Construction Plane frame.");
            return new(AirHoleSimpleShaftMaterializationStatus.UnsupportedPlacement, null, null, diagnostics);
        }

        var worldCorners = new[]
        {
            new Aetheris.Kernel.Core.Math.Point3D(-host.Width / 2d, -host.Depth / 2d, host.ZMin), new Aetheris.Kernel.Core.Math.Point3D(host.Width / 2d, -host.Depth / 2d, host.ZMin),
            new Aetheris.Kernel.Core.Math.Point3D(-host.Width / 2d, host.Depth / 2d, host.ZMin), new Aetheris.Kernel.Core.Math.Point3D(host.Width / 2d, host.Depth / 2d, host.ZMin),
            new Aetheris.Kernel.Core.Math.Point3D(-host.Width / 2d, -host.Depth / 2d, host.ZMax), new Aetheris.Kernel.Core.Math.Point3D(host.Width / 2d, -host.Depth / 2d, host.ZMax),
            new Aetheris.Kernel.Core.Math.Point3D(-host.Width / 2d, host.Depth / 2d, host.ZMax), new Aetheris.Kernel.Core.Math.Point3D(host.Width / 2d, host.Depth / 2d, host.ZMax)
        };
        (double X, double Y, double Z) Local(Aetheris.Kernel.Core.Math.Point3D point)
        {
            var delta = point - placement.FrameOrigin;
            return (delta.Dot(placement.AxisX.ToVector()), delta.Dot(placement.AxisY.ToVector()), delta.Dot(placement.AxisZ.ToVector()));
        }
        var local = worldCorners.Select(Local).ToArray();
        var xmin = local.Min(p => p.X); var xmax = local.Max(p => p.X); var ymin = local.Min(p => p.Y); var ymax = local.Max(p => p.Y);
        var zmin = local.Min(p => p.Z); var zmax = local.Max(p => p.Z);
        if (Math.Abs(zmin) > Tolerance || zmax <= Tolerance)
        {
            diagnostics.Add($"HoleDirectionDoesNotEnterHost: mouth localZ interval is [{zmin:R},{zmax:R}], expected [0,+). constructionPlane={placement.ConstructionPlaneId}");
            return new(AirHoleSimpleShaftMaterializationStatus.UnsupportedPlacement, null, null, diagnostics);
        }
        var footprintSupported = placement.LocalCenterX - feature.Shaft.Radius >= xmin - Tolerance && placement.LocalCenterX + feature.Shaft.Radius <= xmax + Tolerance &&
            placement.LocalCenterY - feature.Shaft.Radius >= ymin - Tolerance && placement.LocalCenterY + feature.Shaft.Radius <= ymax + Tolerance;
        var traversal = new HoleHostTraversalEvidence(
            feature.FeatureId, feature.TargetBodyId ?? "semantic-hole-host", placement.ConstructionPlaneId,
            [placement.WorldMouthCenter.X, placement.WorldMouthCenter.Y, placement.WorldMouthCenter.Z],
            [placement.AxisZ.ToVector().X, placement.AxisZ.ToVector().Y, placement.AxisZ.ToVector().Z], feature.Shaft.Radius,
            HoleHostTraversalClassification.OneContiguousInterval,
            [new HoleHostMaterialIntervalEvidence(0d, zmax, "box:single-material-span", "BoxSignedPermutation", footprintSupported, "Mouth", "FarBoundary")],
            ["HostMaterialIntervalQuery:BoxSignedPermutation", "HostMaterialSpan:[0," + zmax.ToString("R") + "]"]);
        var contract = HoleEndConditionContract.Evaluate(feature, traversal);
        diagnostics.AddRange(traversal.Diagnostics);
        diagnostics.AddRange(contract.Diagnostics);
        if (!footprintSupported)
        {
            diagnostics.Add("HoleMouthMissesHost: local circular mouth does not lie fully within the admitted Box cross-section.");
            return new(AirHoleSimpleShaftMaterializationStatus.UnsupportedPlacement, null, null, diagnostics.Distinct().ToArray());
        }
        if (!contract.MouthInsideMaterial || !contract.ContractSatisfied)
            return new(AirHoleSimpleShaftMaterializationStatus.UnsupportedPlacement, null, null, diagnostics.Distinct().ToArray());

        if (feature.Termination is AirHoleTermination.DrillPoint point)
        {
            var halfAngleRadians = point.PointAngleDegrees * Math.PI / 360d;
            var tipLength = feature.Shaft.Radius / Math.Tan(halfAngleRadians);
            double shaftDepth; double totalDepth;
            switch (feature.EndCondition)
            {
                case AirHoleEndCondition.ShaftDepth declared:
                    shaftDepth = declared.Value; totalDepth = shaftDepth + tipLength; break;
                case AirHoleEndCondition.TotalDepth declared:
                    totalDepth = declared.Value; shaftDepth = totalDepth - tipLength; break;
                default:
                    diagnostics.Add("HoleBlindDepthMissing: DrillPoint requires ShaftDepth or TotalDepth.");
                    return new(AirHoleSimpleShaftMaterializationStatus.UnsupportedPlacement, null, null, diagnostics);
            }
            if (feature.EndCondition is AirHoleEndCondition.TotalDepth && totalDepth < tipLength - Tolerance)
            {
                diagnostics.Add($"HoleTotalDepthShorterThanTip: feature={feature.FeatureId}; totalDepth={totalDepth:R}; tipLength={tipLength:R}; pointAngle={point.PointAngleDegrees:R}deg.");
                return new(AirHoleSimpleShaftMaterializationStatus.UnsupportedPlacement, null, null, diagnostics);
            }
            if (!double.IsFinite(tipLength) || tipLength <= Tolerance || !double.IsFinite(shaftDepth) || shaftDepth < -Tolerance || !double.IsFinite(totalDepth) || totalDepth <= Tolerance)
            {
                diagnostics.Add($"HoleBlindDepthInvalid: feature={feature.FeatureId}; shaftDepth={shaftDepth:R}; tipLength={tipLength:R}; totalDepth={totalDepth:R}.");
                return new(AirHoleSimpleShaftMaterializationStatus.UnsupportedPlacement, null, null, diagnostics);
            }
            if (totalDepth >= zmax - Tolerance)
            {
                diagnostics.Add($"HoleDrillPointExitsHost: feature={feature.FeatureId}; constructionPlane={placement.ConstructionPlaneId}; localCenter=[{placement.LocalCenterX:R},{placement.LocalCenterY:R}]; diameter={feature.Shaft.Diameter:R}; shaftDepth={shaftDepth:R}; tipLength={tipLength:R}; totalDepth={totalDepth:R}; hostInterval=[0,{zmax:R}].");
                return new(AirHoleSimpleShaftMaterializationStatus.UnsupportedPlacement, null, null, diagnostics.Distinct().ToArray());
            }
            var blind = BlindDrillPointBRepPlanner.TryPlan(feature, placement, (xmin, xmax, ymin, ymax, zmax), Math.Max(0d, shaftDepth), tipLength);
            if (!blind.Succeeded || blind.Plan is null)
            {
                diagnostics.AddRange(blind.Diagnostics); diagnostics.Add("HoleDrillPointPlanInvalid: authoritative blind local-frame plan was not created.");
                return new(AirHoleSimpleShaftMaterializationStatus.ExecutionFailed, null, null, diagnostics);
            }
            var materializedBlind = ProfileExtrusionBRepMaterializer.TryMaterialize(blind.Plan);
            diagnostics.AddRange(materializedBlind.Diagnostics);
            if (!materializedBlind.Succeeded || materializedBlind.Body is null)
            {
                diagnostics.Add("HoleDrillPointMaterializerDiverged: authoritative blind plan failed materialization.");
                return new(AirHoleSimpleShaftMaterializationStatus.ExecutionFailed, null, null, diagnostics);
            }
            var provenance = blind.Plan.Correspondence.ProvenanceChain.Concat(["HostMaterialIntervalQuery:BoxSignedPermutation", "DrillPointTermination"]).ToArray();
            var blindHolePlan = new LocalFrameHoleBRepPlan($"brep-plan:hole:{feature.FeatureId}:{placement.ConstructionPlaneId}", feature.FeatureId, placement, (0d, zmax), blind.Plan,
                blind.Plan.Correspondence with { ProvenanceChain = provenance }, provenance, traversal, contract);
            var specBlind = new ProfileStackExtrudeSpec(host.Width, host.Depth, host.ZMin, host.ZMax, [], [], placement.LocalCenterX, placement.LocalCenterY);
            var planBlind = new AirHoleSimpleShaftMaterializationPlan(feature, host, feature.FeatureId, nameof(AirHoleFeature), placement.ConstructionPlaneId,
                placement.LocalCenterX, placement.LocalCenterY, 1d, feature.Shaft.Radius, 0d, totalDepth, feature.EndCondition.Kind, feature.Stack.Kind,
                feature.Stack.Components.Select(c => c.Kind).ToArray(), specBlind, diagnostics.ToArray(), blindHolePlan);
            diagnostics.Add($"BlindDrillPoint: declared={feature.EndCondition.Kind}; shaftDepth={shaftDepth:R}; tipLength={tipLength:R}; totalDepth={totalDepth:R}; pointAngle={point.PointAngleDegrees:R}deg; hostInterval=[0,{zmax:R}].");
            return new(AirHoleSimpleShaftMaterializationStatus.Succeeded, planBlind, materializedBlind.Body, diagnostics, blindHolePlan.Correspondence);
        }

        var frame = new ConstructionPlane(placement.ConstructionPlaneId, placement.SourceConceptPlaneId, placement.FrameOrigin,
            placement.AxisX, placement.AxisY, placement.AxisZ, placement.SourceSpan, placement.Provenance);
        var outer = new LineArcProfileLoop2D([
            new LineArcLineSegment2D((xmin, ymin), (xmax, ymin)), new LineArcLineSegment2D((xmax, ymin), (xmax, ymax)),
            new LineArcLineSegment2D((xmax, ymax), (xmin, ymax)), new LineArcLineSegment2D((xmin, ymax), (xmin, ymin))], false);
        // A deterministic two-arc loop avoids a reused longitudinal seam edge.
        // It is still one exact circle/cylinder support, but yields normal
        // DirectedEdgeUse closure and correct signed mass integration.
        var inner = new LineArcProfileLoop2D([
            new LineArcCircularArc2D((placement.LocalCenterX, placement.LocalCenterY), feature.Shaft.Radius, 0d, -Math.PI),
            new LineArcCircularArc2D((placement.LocalCenterX, placement.LocalCenterY), feature.Shaft.Radius, Math.PI, -Math.PI)], true);
        var planned = ProfileExtrusionBRepPlanner.TryPlan(new LineArcProfileExtrudeRequest([outer, inner], zmax, frame, 0d, zmax));
        if (!planned.Succeeded || planned.Plan is null)
        {
            diagnostics.AddRange(planned.Diagnostics); diagnostics.Add("HolePlanInvalid: local-frame circular extrusion plan was not created.");
            return new(AirHoleSimpleShaftMaterializationStatus.ExecutionFailed, null, null, diagnostics);
        }
        var holePlan = LocalFrameHoleBRepPlan.FromProfilePlan(feature, placement, (0d, zmax), planned.Plan, traversal, contract);
        var materialized = ProfileExtrusionBRepMaterializer.TryMaterialize(planned.Plan);
        diagnostics.AddRange(materialized.Diagnostics);
        if (!materialized.Succeeded || materialized.Body is null)
        {
            diagnostics.Add("HoleMaterializerDiverged: authoritative HoleBRepPlan failed materialization.");
            return new(AirHoleSimpleShaftMaterializationStatus.ExecutionFailed, null, null, diagnostics);
        }
        var spec = new ProfileStackExtrudeSpec(host.Width, host.Depth, host.ZMin, host.ZMax, [], [], placement.LocalCenterX, placement.LocalCenterY);
        var plan = new AirHoleSimpleShaftMaterializationPlan(feature, host, feature.FeatureId, nameof(AirHoleFeature), placement.ConstructionPlaneId,
            placement.LocalCenterX, placement.LocalCenterY, 1d, feature.Shaft.Radius, 0d, zmax, feature.EndCondition.Kind, feature.Stack.Kind,
            feature.Stack.Components.Select(c => c.Kind).ToArray(), spec, diagnostics.ToArray(), holePlan);
        diagnostics.Add($"HostMaterialIntervals:[0,{zmax:R}] local-Z; constructionPlane={placement.ConstructionPlaneId}; sourceConceptPlane={placement.SourceConceptPlaneId}.");
        diagnostics.Add("local-frame-hole materialization succeeded: authoritative HoleBRepPlan -> ProfileExtrusionBRepMaterializer.");
        return new(AirHoleSimpleShaftMaterializationStatus.Succeeded, plan, materialized.Body, diagnostics, holePlan.Correspondence);
    }

    private static bool IsSignedAxis(Aetheris.Kernel.Core.Math.Direction3D axis)
    {
        var values = new[] { Math.Abs(axis.ToVector().X), Math.Abs(axis.ToVector().Y), Math.Abs(axis.ToVector().Z) };
        return values.Count(v => Math.Abs(v - 1d) <= Tolerance) == 1 && values.Count(v => v <= Tolerance) == 2;
    }
}
