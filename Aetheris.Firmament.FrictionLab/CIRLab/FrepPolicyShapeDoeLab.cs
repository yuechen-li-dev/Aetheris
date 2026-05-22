using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.FrictionLab;

public enum PolicyShapeCapability { ExactBRep, PlannedExact, DiagnosticOnly, CirOnly, Unsupported, ForgeOrDeferred }
public enum RecoveryPlanShape { PairSpecificPlan, ThroughHoleRecoveryPlan, CylindricalToolCutPlan, GenericBooleanRecoveryPlan, SurfaceFeaturePlan, FallbackNumericalPlan, CirOnlyPlan, UnsupportedPlan }

public sealed record PolicyShapeScenario(string Id, string Group, string Description, CirNode? Root, string Category, bool Deferred = false);

public sealed record PolicyShapeEvaluation(
    string PolicyName,
    bool Admissible,
    double Score,
    PolicyShapeCapability Capability,
    RecoveryPlanShape PlanShape,
    IReadOnlyList<string> EvidenceUsed,
    IReadOnlyList<string> RejectedReasons,
    bool OverAdmits,
    bool UnderAdmits,
    int PairSpecificityRisk,
    int FutureScalabilityScore,
    IReadOnlyList<string> Diagnostics);

public sealed record PolicyShapeDecision(string? Winner, IReadOnlyList<PolicyShapeEvaluation> Ranking, IReadOnlyList<string> Diagnostics);

public interface IPolicyShapeCandidate { string Name { get; } int TieBreakerPriority { get; } PolicyShapeEvaluation Evaluate(PolicyShapeScenario scenario); }

public static class FrepPolicyShapeDoeLab
{
    public static IReadOnlyList<PolicyShapeScenario> BuildScenarioMatrix() =>
    [
        new("A1","A","box - centered cylinder through Z", new CirSubtractNode(new CirBoxNode(10,8,6), new CirCylinderNode(2,8)), "canonical_through_hole"),
        new("A2","A","box - offset cylinder through Z with strict clearance", new CirSubtractNode(new CirBoxNode(10,8,6), new CirTransformNode(new CirCylinderNode(1.5,8), Transform3D.CreateTranslation(new(1,1,0)))), "canonical_through_hole"),
        new("A3","A","translated box - translated cylinder through Z", new CirSubtractNode(new CirTransformNode(new CirBoxNode(10,8,6), Transform3D.CreateTranslation(new(3,2,5))), new CirTransformNode(new CirCylinderNode(2,8), Transform3D.CreateTranslation(new(3.5,2.5,5)))), "canonical_through_hole"),
        new("A4","A","rectangular plate - cylinder through Z", new CirSubtractNode(new CirBoxNode(12,8,2), new CirCylinderNode(1,3)), "canonical_through_hole"),
        new("B1","B","box - blind short cylinder", new CirSubtractNode(new CirBoxNode(10,8,6), new CirCylinderNode(2,4)), "invalid_hole"),
        new("B2","B","box - tangent/grazing cylinder", new CirSubtractNode(new CirBoxNode(10,8,6), new CirTransformNode(new CirCylinderNode(2,8), Transform3D.CreateTranslation(new(3,0,0)))), "invalid_hole"),
        new("B3","B","box - cylinder outside box", new CirSubtractNode(new CirBoxNode(10,8,6), new CirTransformNode(new CirCylinderNode(2,8), Transform3D.CreateTranslation(new(7,0,0)))), "invalid_hole"),
        new("B4","B","box - oversized cylinder", new CirSubtractNode(new CirBoxNode(10,8,6), new CirCylinderNode(6,8)), "invalid_hole"),
        new("B5","B","box - rotated cylinder", new CirSubtractNode(new CirBoxNode(10,8,6), new CirTransformNode(new CirCylinderNode(2,8), Transform3D.CreateRotationX(Math.PI/4))), "invalid_hole"),
        new("C1","C","box - cylinder side notch", new CirSubtractNode(new CirBoxNode(10,8,6), new CirTransformNode(new CirCylinderNode(2,8), Transform3D.CreateTranslation(new(4,0,0)))), "cylindrical_non_through"),
        new("C2","C","box - shallow cylindrical pocket", new CirSubtractNode(new CirBoxNode(10,8,6), new CirCylinderNode(1.5,1.5)), "cylindrical_non_through"),
        new("D1","D","cylindrical host - axial cylindrical hole", null, "future_variant", true),
        new("D4","D","rectangular host - countersink-like through feature", null, "future_variant", true),
        new("E1","E","round groove on cylinder", null, "surface_feature", true),
        new("E3","E","thread on cylinder", null, "surface_feature", true),
        new("F1","F","generic torus subtract", null, "unsupported", true),
        new("F2","F","box - sphere", new CirSubtractNode(new CirBoxNode(10,8,6), new CirSphereNode(2)), "unsupported"),
        new("F3","F","nested booleans", new CirSubtractNode(new CirSubtractNode(new CirBoxNode(10,8,6), new CirCylinderNode(1,8)), new CirCylinderNode(1,8)), "unsupported"),
        new("F4","F","union/intersect cases", new CirUnionNode(new CirBoxNode(1,1,1), new CirCylinderNode(1,1)), "unsupported")
    ];

    public static IReadOnlyList<IPolicyShapeCandidate> DefaultCandidates => [new PairSpecific(), new ThroughHole(), new CylindricalToolCut(), new GenericBoolean(), new GenericNumericalContour(), new CirOnlyFallback()];

    public static PolicyShapeDecision Decide(PolicyShapeScenario scenario, IReadOnlyList<IPolicyShapeCandidate>? candidates = null)
    {
        var set = candidates ?? DefaultCandidates;
        var evals = set.Select(c => c.Evaluate(scenario)).ToArray();
        var map = evals.ToDictionary(e => e.PolicyName, StringComparer.Ordinal);
        var engine = new JudgmentEngine<PolicyShapeScenario>();
        var j = engine.Evaluate(scenario, set.Select(c => new JudgmentCandidate<PolicyShapeScenario>(c.Name, _ => map[c.Name].Admissible, _ => map[c.Name].Score, _ => string.Join(" | ", map[c.Name].RejectedReasons), c.TieBreakerPriority)).ToArray());
        var winner = j.IsSuccess ? j.Selection!.Value.Candidate.Name : null;
        return new(winner, evals.OrderByDescending(e => e.Admissible).ThenByDescending(e => e.Score).ThenBy(e => e.PolicyName).ToArray(), [..j.Rejections.Select(r => $"{r.CandidateName}:{r.Reason}")]);
    }

    private sealed class PairSpecific : IPolicyShapeCandidate { public string Name => "BoxCylinderThroughHolePolicyShape"; public int TieBreakerPriority => 0; public PolicyShapeEvaluation Evaluate(PolicyShapeScenario s){ var ok = TryCanonical(s); return new(Name, ok, ok?92:0, ok?PolicyShapeCapability.ExactBRep:PolicyShapeCapability.Unsupported, RecoveryPlanShape.PairSpecificPlan, ok?["box-cylinder-recognizer"]:[], ok?[]:["not canonical box-cylinder through-hole"], false, s.Category=="future_variant", 9, 3, []);} }
    private sealed class ThroughHole : IPolicyShapeCandidate { public string Name => "ThroughHoleRecoveryPolicyShape"; public int TieBreakerPriority=>0; public PolicyShapeEvaluation Evaluate(PolicyShapeScenario s){ var supported = s.Category=="canonical_through_hole"||s.Category=="future_variant"; var admissible = s.Category=="canonical_through_hole"; return new(Name, admissible, admissible?95:0, admissible?PolicyShapeCapability.ExactBRep:(supported?PolicyShapeCapability.PlannedExact:PolicyShapeCapability.Unsupported), RecoveryPlanShape.ThroughHoleRecoveryPlan, ["semantic-through-hole","bounded-admissibility"], admissible?[]:["not admissible as strict through-hole"], false, false, 2, 9, []);} }
    private sealed class CylindricalToolCut : IPolicyShapeCandidate { public string Name => "CylindricalToolCutPolicyShape"; public int TieBreakerPriority=>1; public PolicyShapeEvaluation Evaluate(PolicyShapeScenario s){ var toolCyl = s.Root is CirSubtractNode sub && (sub.Right is CirCylinderNode || sub.Right is CirTransformNode { Child: CirCylinderNode }); var over = toolCyl && s.Category=="cylindrical_non_through"; return new(Name, toolCyl, toolCyl?72:0, toolCyl?PolicyShapeCapability.DiagnosticOnly:PolicyShapeCapability.Unsupported, RecoveryPlanShape.CylindricalToolCutPlan, toolCyl?["tool-family=cylindrical"]:[], toolCyl?[]:["tool is not cylindrical subtract"], over, false, 5, 5, over?["over-admission-risk: mixes hole/notch/pocket/groove"]:[]);} }
    private sealed class GenericBoolean : IPolicyShapeCandidate { public string Name => "CirBooleanRecoveryPolicyShape"; public int TieBreakerPriority=>2; public PolicyShapeEvaluation Evaluate(PolicyShapeScenario s){ var admissible = s.Root is CirSubtractNode or CirUnionNode or CirIntersectNode; return new(Name, admissible, admissible?40:0, admissible?PolicyShapeCapability.DiagnosticOnly:PolicyShapeCapability.Unsupported, RecoveryPlanShape.GenericBooleanRecoveryPlan, admissible?["generic-boolean"]:[], admissible?[]:["not a boolean CIR root"], admissible, false, 8, 2, admissible?["dispatcher-risk: broad boolean bucket"]:[]);} }
    private sealed class GenericNumericalContour : IPolicyShapeCandidate { public string Name=>"GenericNumericalContourPolicy"; public int TieBreakerPriority=>3; public PolicyShapeEvaluation Evaluate(PolicyShapeScenario s)=> new(Name,true,20,PolicyShapeCapability.DiagnosticOnly,RecoveryPlanShape.FallbackNumericalPlan,["fallback=numerical"],[],false,false,1,4,[]); }
    private sealed class CirOnlyFallback : IPolicyShapeCandidate { public string Name=>"CirOnlyFallbackPolicy"; public int TieBreakerPriority=>4; public PolicyShapeEvaluation Evaluate(PolicyShapeScenario s)=> new(Name,true,5,PolicyShapeCapability.CirOnly,RecoveryPlanShape.CirOnlyPlan,["fallback=cir-only"],[],false,false,1,3,[]); }

    private static bool TryCanonical(PolicyShapeScenario s)
    {
        if (s.Root is null) return false;
        var r = CirBrepX4RecognizerSourceContractLab.Recognize(CirBoxCylinderRecognizerInput.FromNode(s.Root));
        return r.Recognition.Success;
    }
}
