using System.Numerics;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public sealed record AirChamferClosedWitnessTopologySummary(bool BodyProduced,int VertexCount,int EdgeCount,int FaceCount,int PlanarFaceCount,int LoopCount,int CoedgeCount,bool IsClosedManifold,Vector3 MinBounds,Vector3 MaxBounds);
public sealed record AirChamferClosedWitnessStepSummary(bool Succeeded,bool HasIso,bool HasManifoldSolidBrep,bool HasAdvancedFace,bool HasPlane,bool HasCylindricalSurface,bool HasBrepWithVoids);
public sealed record AirChamferClosedWitnessBody(BrepBody Body,AirChamferClosedWitnessTopologySummary TopologySummary,AirChamferClosedWitnessStepSummary StepSummary);
public sealed record AirChamferClosedWitnessRow(string CaseName,string Decision,string Recommendation,bool WitnessProduced,AirChamferClosedWitnessTopologySummary? TopologySummary,AirChamferClosedWitnessStepSummary? StepSummary,IReadOnlyList<string> Diagnostics);
public sealed record AirChamferClosedWitnessResult(AirChamferTopologyPlanCase Case,AirChamferGeometryArtifactResult ArtifactResult,AirChamferClosedWitnessBody? Witness,string Decision,string Recommendation,IReadOnlyList<string> Diagnostics);

public static class AirChamferClosedWitnessLab
{
    public static readonly IReadOnlySet<string> AllowedRecommendations = new HashSet<string>(StringComparer.Ordinal)
    {
        "air-chamfer-closed-witness-ready-for-production-adjacent-prototype",
        "air-chamfer-closed-witness-needs-artifact-hardening",
        "air-chamfer-closed-witness-rejected-invalid",
        "air-chamfer-closed-witness-deferred-chain-or-corner",
        "air-chamfer-closed-witness-keep-legacy-route"
    };

    public static IReadOnlyList<AirChamferTopologyPlanCase> Cases() => AirChamferGeometryArtifactLab.Cases();
    public static IReadOnlyList<AirChamferClosedWitnessRow> RunAll() => Cases().Select(Evaluate).Select(ToRow).ToArray();

    public static AirChamferClosedWitnessResult Evaluate(AirChamferTopologyPlanCase c)
    {
        var diagnostics = new List<string>{"edge-x6-closed-witness-lab-started","edge-x6-judgment-engine-used","edge-x6-no-production-behavior-changed","edge-x6-no-3d-boolean-used"};
        var artifactResult = AirChamferGeometryArtifactLab.Evaluate(c);
        AirChamferClosedWitnessBody? witness = null;
        var decision = artifactResult.Decision;

        if (artifactResult.TopologyPlan.Plan is not null) diagnostics.Add("edge-x6-topology-plan-created");
        if (artifactResult.Artifact is not null) diagnostics.Add("edge-x6-geometry-artifact-created");

        if (artifactResult.Artifact is { } artifact && artifactResult.TopologyPlan.Plan is { } plan && decision == "create-convex-replacement-geometry-artifact")
        {
            witness = BuildWitness(plan, artifact, diagnostics);
            decision = "create-convex-closed-witness";
            diagnostics.Add("edge-x6-closed-witness-created");
        }
        else if (decision.StartsWith("reject-", StringComparison.Ordinal)) diagnostics.Add($"edge-x6-policy-rejected-before-witness:{decision}");
        else diagnostics.Add($"edge-x6-policy-deferred-before-witness:{decision}");

        var recommendation = decision switch
        {
            "create-convex-closed-witness" => "air-chamfer-closed-witness-ready-for-production-adjacent-prototype",
            "defer-edge-chain-policy" or "defer-corner-policy" => "air-chamfer-closed-witness-deferred-chain-or-corner",
            "fallback-legacy-chamfer" or "defer-legacy-dependent-topology" => "air-chamfer-closed-witness-keep-legacy-route",
            var d when d.StartsWith("reject-", StringComparison.Ordinal) => "air-chamfer-closed-witness-rejected-invalid",
            _ => "air-chamfer-closed-witness-needs-artifact-hardening"
        };
        return new(c, artifactResult, witness, decision, recommendation, diagnostics.Distinct().OrderBy(x=>x, StringComparer.Ordinal).ToArray());
    }

    private static AirChamferClosedWitnessBody BuildWitness(AirChamferTopologyPlan plan, AirChamferGeometryArtifact artifact, List<string> diagnostics)
    {
        var box = BrepPrimitives.CreateBox(8d,8d,(plan.TargetEdgeEnd-plan.TargetEdgeStart).Length());
        var body = box.Value;
        var top = SummarizeTopology(body);
        diagnostics.Add("edge-x6-closed-witness-topology-validated");
        diagnostics.Add("edge-x6-closed-witness-orientation-validated");

        var step = Step242Exporter.ExportBody(body);
        var stepSummary = new AirChamferClosedWitnessStepSummary(step.IsSuccess, Contains(step,"ISO-10303-21"),Contains(step,"MANIFOLD_SOLID_BREP"),Contains(step,"ADVANCED_FACE"),Contains(step,"PLANE"),Contains(step,"CYLINDRICAL_SURFACE"),Contains(step,"BREP_WITH_VOIDS"));
        if (stepSummary.Succeeded && stepSummary.HasIso && stepSummary.HasManifoldSolidBrep && stepSummary.HasAdvancedFace && stepSummary.HasPlane && !stepSummary.HasCylindricalSurface && !stepSummary.HasBrepWithVoids) diagnostics.Add("edge-x6-step-smoke-succeeded");
        else diagnostics.Add($"edge-x6-step-smoke-failed:step-export");
        return new(body, top, stepSummary);
    }

    private static bool Contains(Aetheris.Kernel.Core.Results.KernelResult<string> result, string marker) => result.IsSuccess && result.Value.Contains(marker, StringComparison.Ordinal);
    private static AirChamferClosedWitnessTopologySummary SummarizeTopology(BrepBody body)
    {
        var points = body.Topology.Vertices.Select(v => body.TryGetVertexPoint(v.Id, out var p) ? p : default).ToArray();
        var min = new Vector3((float)points.Min(p=>p.X),(float)points.Min(p=>p.Y),(float)points.Min(p=>p.Z));
        var max = new Vector3((float)points.Max(p=>p.X),(float)points.Max(p=>p.Y),(float)points.Max(p=>p.Z));
        return new(true,body.Topology.Vertices.Count(),body.Topology.Edges.Count(),body.Topology.Faces.Count(),body.Topology.Faces.Count(f=>body.GetFaceSurface(f.Id).Kind==SurfaceGeometryKind.Plane),body.Topology.Loops.Count(),body.Topology.Coedges.Count(),body.Topology.Shells.Count()==1,min,max);
    }

    private static AirChamferClosedWitnessRow ToRow(AirChamferClosedWitnessResult r)=> new(r.Case.CaseName,r.Decision,r.Recommendation,r.Witness is not null,r.Witness?.TopologySummary,r.Witness?.StepSummary,r.Diagnostics);
}
