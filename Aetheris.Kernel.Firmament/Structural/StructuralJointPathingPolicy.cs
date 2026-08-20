using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Firmament.Structural;

public sealed record StructuralMiterPathingContext(Vector3D FirstAway, Vector3D SecondAway);

public sealed record StructuralMiterPathingPolicyResult(
    bool IsSuccess, string? SelectedStrategy, Vector3D PlaneNormal, double UtilityScore,
    bool RetainedHalfSpacesOpposed, IReadOnlyList<string> RejectedCandidates);

/// <summary>
/// Bounded strategy chooser for a two-member miter. Candidate selection is
/// explicit because the two angle bisectors have different material-side
/// semantics even though both pass through the same centerline node.
/// </summary>
public static class StructuralJointPathingPolicy
{
    private const double Tolerance = 1e-9;
    private const string Separating = "SeparatingAngleBisector";
    private const string Reflex = "ReflexAngleBisector";

    public static StructuralMiterPathingPolicyResult SelectMiter(StructuralMiterPathingContext context)
    {
        var candidates = Candidates();
        var judgment = new JudgmentEngine<StructuralMiterPathingContext>().Evaluate(context, candidates);
        var rejected = candidates.Where(candidate => !candidate.IsAdmissible(context))
            .Select(candidate => $"{candidate.Name}:{candidate.RejectionReason?.Invoke(context) ?? "candidate predicates were not satisfied"}")
            .ToArray();
        if (!judgment.IsSuccess || !judgment.Selection.HasValue)
            return new(false, null, default, double.NegativeInfinity, false, rejected);

        var selection = judgment.Selection.Value;
        var normal = selection.Candidate.Name switch
        {
            Separating => context.FirstAway - context.SecondAway,
            Reflex => context.FirstAway + context.SecondAway,
            _ => default
        };
        return new(true, selection.Candidate.Name, normal, selection.Score, OpposesMaterial(context, normal), rejected);
    }

    private static IReadOnlyList<JudgmentCandidate<StructuralMiterPathingContext>> Candidates() =>
    [
        new(Separating,
            IsAdmissible: context => IsFiniteNondegenerate(context.FirstAway - context.SecondAway)
                && OpposesMaterial(context, context.FirstAway - context.SecondAway),
            Score: context => 200d + BalanceScore(context, context.FirstAway - context.SecondAway),
            RejectionReason: _ => "requires a finite bisector that places the two retained member rays in opposite open half-spaces",
            TieBreakerPriority: 0),
        new(Reflex,
            IsAdmissible: context => IsFiniteNondegenerate(context.FirstAway + context.SecondAway)
                && OpposesMaterial(context, context.FirstAway + context.SecondAway),
            Score: context => 100d + BalanceScore(context, context.FirstAway + context.SecondAway),
            RejectionReason: _ => "reflex bisector leaves both retained member rays on the same side and would admit volumetric overlap",
            TieBreakerPriority: 1)
    ];

    private static bool OpposesMaterial(StructuralMiterPathingContext context, Vector3D normal)
    {
        if (!IsFiniteNondegenerate(normal)) return false;
        var first = normal.Dot(context.FirstAway);
        var second = normal.Dot(context.SecondAway);
        return first * second < -Tolerance;
    }

    private static double BalanceScore(StructuralMiterPathingContext context, Vector3D normal)
    {
        var unit = normal / normal.Length;
        return 1d - Math.Abs(Math.Abs(unit.Dot(context.FirstAway)) - Math.Abs(unit.Dot(context.SecondAway)));
    }

    private static bool IsFiniteNondegenerate(Vector3D value) =>
        double.IsFinite(value.X) && double.IsFinite(value.Y) && double.IsFinite(value.Z) && value.Length > Tolerance;
}
