using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.FrictionLab;

public enum HoleFamilyScenarioExpectation { ExactHoleFamily, FutureHoleFamily, DeferredForge, NotHoleFamily }
public enum HoleFamilyArchitectureKind { SeparatePolicySet, MonolithicHolePolicy, CompositionalHolePolicy, ProfileStackPolicy }
public enum HoleFeatureKind { Through, Blind, Counterbore, Countersink, Stepped, ChamferedEntry, Threaded, Unknown }

public sealed record HoleFamilyScenario(string Id, string Group, string Description, CirNode? Root, HoleFeatureKind FeatureKind, HoleFamilyScenarioExpectation Expectation);
public sealed record HoleFamilyArchitectureScore(string Architecture, bool AdmitsScenario, bool CorrectlyRejectsScenario, int PolicyCountGrowth, int BranchComplexity, int LocalityOfChangeScore, int DiagnosticClarityScore, int FutureScalabilityScore, int RiskOfOverAdmission, int RiskOfUnderAdmission, int PlanShapeQuality, int ExecutorReusability, IReadOnlyList<string> Diagnostics);
public sealed record HoleFamilyDecision(string Winner, IReadOnlyList<HoleFamilyArchitectureScore> Scores, IReadOnlyList<string> Diagnostics);

public static class HoleFamilyPolicyShapeLab
{
    public static IReadOnlyList<HoleFamilyScenario> BuildScenarioMatrix() =>
    [
        new("A1","A","box + cylindrical through-hole",new CirSubtractNode(new CirBoxNode(10,8,6),new CirCylinderNode(2,8)),HoleFeatureKind.Through,HoleFamilyScenarioExpectation.ExactHoleFamily),
        new("A2","A","translated box + translated cylindrical through-hole",new CirSubtractNode(new CirTransformNode(new CirBoxNode(10,8,6),Transform3D.CreateTranslation(new(2,1,3))),new CirTransformNode(new CirCylinderNode(2,8),Transform3D.CreateTranslation(new(2.5,1,3)))),HoleFeatureKind.Through,HoleFamilyScenarioExpectation.ExactHoleFamily),
        new("A3","A","offset cylindrical through-hole",new CirSubtractNode(new CirBoxNode(10,8,6),new CirTransformNode(new CirCylinderNode(1.5,8),Transform3D.CreateTranslation(new(1,1,0)))),HoleFeatureKind.Through,HoleFamilyScenarioExpectation.ExactHoleFamily),
        new("B1","B","box + blind hole",new CirSubtractNode(new CirBoxNode(10,8,6),new CirCylinderNode(2,3)),HoleFeatureKind.Blind,HoleFamilyScenarioExpectation.FutureHoleFamily),
        new("B2","B","box + counterbore",null,HoleFeatureKind.Counterbore,HoleFamilyScenarioExpectation.FutureHoleFamily),
        new("B3","B","box + countersink",null,HoleFeatureKind.Countersink,HoleFamilyScenarioExpectation.FutureHoleFamily),
        new("B4","B","box + stepped through-hole",null,HoleFeatureKind.Stepped,HoleFamilyScenarioExpectation.FutureHoleFamily),
        new("B5","B","box + chamfered-entry through-hole",null,HoleFeatureKind.ChamferedEntry,HoleFamilyScenarioExpectation.FutureHoleFamily),
        new("C1","C","cylindrical host + axial hole",null,HoleFeatureKind.Through,HoleFamilyScenarioExpectation.FutureHoleFamily),
        new("C2","C","cylindrical host + radial hole",null,HoleFeatureKind.Through,HoleFamilyScenarioExpectation.FutureHoleFamily),
        new("C3","C","conical tool tapered hole",null,HoleFeatureKind.Countersink,HoleFamilyScenarioExpectation.FutureHoleFamily),
        new("C4","C","rectangular host + hole pattern",null,HoleFeatureKind.Through,HoleFamilyScenarioExpectation.FutureHoleFamily),
        new("D1","D","threaded hole",null,HoleFeatureKind.Threaded,HoleFamilyScenarioExpectation.DeferredForge),
        new("D2","D","knurled/texture feature",null,HoleFeatureKind.Unknown,HoleFamilyScenarioExpectation.DeferredForge),
        new("D3","D","arbitrary swept slot",null,HoleFeatureKind.Unknown,HoleFamilyScenarioExpectation.DeferredForge),
        new("D4","D","freeform projected emboss/deboss",null,HoleFeatureKind.Unknown,HoleFamilyScenarioExpectation.DeferredForge),
        new("E1","E","round groove",null,HoleFeatureKind.Unknown,HoleFamilyScenarioExpectation.NotHoleFamily),
        new("E2","E","keyway/slot",null,HoleFeatureKind.Unknown,HoleFamilyScenarioExpectation.NotHoleFamily),
        new("E3","E","side notch",new CirSubtractNode(new CirBoxNode(10,8,6),new CirTransformNode(new CirCylinderNode(2,8),Transform3D.CreateTranslation(new(4,0,0)))),HoleFeatureKind.Unknown,HoleFamilyScenarioExpectation.NotHoleFamily),
        new("E4","E","box - sphere cavity",new CirSubtractNode(new CirBoxNode(10,8,6),new CirSphereNode(2)),HoleFeatureKind.Unknown,HoleFamilyScenarioExpectation.NotHoleFamily),
        new("E5","E","generic torus subtract",null,HoleFeatureKind.Unknown,HoleFamilyScenarioExpectation.NotHoleFamily)
    ];

    public static IReadOnlyList<string> InventoryVocabulary() =>
    [
        "through-hole: first-class semantic recovery policy in production",
        "blind-hole: present in docs/frictionlab fixtures and boolean-deferred, not first-class recovery policy",
        "counterbore/countersink: present in FrictionLab cases and docs, not first-class recovery policy",
        "threaded hole: deferred/forge-style and surface-feature docs indicate deferred",
        "slot/keyway: bounded subtract families present in primitive execution vocabulary",
        "chamfer/fillet: bounded edge-feature families present",
        "round groove: surface-feature A0-A4 descriptor/planning/evidence path"
    ];

    public static HoleFamilyArchitectureScore Evaluate(HoleFamilyArchitectureKind kind, HoleFamilyScenario scenario)
    {
        var exact = scenario.Expectation == HoleFamilyScenarioExpectation.ExactHoleFamily;
        var future = scenario.Expectation == HoleFamilyScenarioExpectation.FutureHoleFamily;
        var deferred = scenario.Expectation == HoleFamilyScenarioExpectation.DeferredForge;
        var notHole = scenario.Expectation == HoleFamilyScenarioExpectation.NotHoleFamily;

        return kind switch
        {
            HoleFamilyArchitectureKind.SeparatePolicySet => Score("SeparatePolicySet", exact, future, deferred, notHole, 9, 8, 6, 6, 7, scenario),
            HoleFamilyArchitectureKind.MonolithicHolePolicy => Score("MonolithicHolePolicy", exact, future, deferred, notHole, 3, 10, 3, 4, 8, scenario),
            HoleFamilyArchitectureKind.CompositionalHolePolicy => Score("CompositionalHolePolicy", exact, future, deferred, notHole, 4, 4, 10, 9, 2, scenario),
            HoleFamilyArchitectureKind.ProfileStackPolicy => Score("ProfileStackPolicy", exact, future, deferred, notHole, 5, 6, 8, 7, 5, scenario),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    public static HoleFamilyDecision Recommend(HoleFamilyScenario scenario)
    {
        var scores = Enum.GetValues<HoleFamilyArchitectureKind>().Select(k => Evaluate(k, scenario)).ToArray();
        var byName = scores.ToDictionary(s => s.Architecture, StringComparer.Ordinal);
        var engine = new JudgmentEngine<HoleFamilyScenario>();
        var judgment = engine.Evaluate(scenario, scores.Select((s, idx) => new JudgmentCandidate<HoleFamilyScenario>(s.Architecture, _ => s.AdmitsScenario || s.CorrectlyRejectsScenario, _ => Aggregate(s), _ => string.Join(" | ", s.Diagnostics), idx)).ToArray());
        var winner = judgment.IsSuccess ? judgment.Selection!.Value.Candidate.Name : "CompositionalHolePolicy";
        return new(winner, scores.OrderByDescending(Aggregate).ThenBy(s => s.Architecture).ToArray(), [..judgment.Rejections.Select(r => $"{r.CandidateName}:{r.Reason}")]);
    }

    private static HoleFamilyArchitectureScore Score(string arch, bool exact, bool future, bool deferred, bool notHole, int growth, int complexity, int locality, int scalability, int overRisk, HoleFamilyScenario s)
    {
        var admits = exact || (future && arch != "SeparatePolicySet") || (future && arch == "SeparatePolicySet" && s.FeatureKind is HoleFeatureKind.Counterbore or HoleFeatureKind.Countersink);
        if (deferred || notHole) admits = arch == "ProfileStackPolicy" && s.Id == "E3"; // intentional over-admission evidence
        var rejectOk = (deferred || notHole) && !admits;
        var underRisk = future && !admits ? 8 : 2;
        var diag = new List<string>();
        if (admits && (deferred || notHole)) diag.Add("over-admission-detected");
        if (!admits && future) diag.Add("future-under-admission");
        if (arch == "MonolithicHolePolicy") diag.Add("branch-maze-risk");
        return new(arch, admits, rejectOk, growth, complexity, locality, 8 - complexity / 2, scalability, overRisk, underRisk, 10 - complexity / 2, 10 - growth / 2, diag);
    }

    private static double Aggregate(HoleFamilyArchitectureScore s)
        => (s.LocalityOfChangeScore * 3) + (s.FutureScalabilityScore * 3) + (s.DiagnosticClarityScore * 2) + s.PlanShapeQuality + s.ExecutorReusability - (s.RiskOfOverAdmission * 2) - s.RiskOfUnderAdmission - s.BranchComplexity;
}
