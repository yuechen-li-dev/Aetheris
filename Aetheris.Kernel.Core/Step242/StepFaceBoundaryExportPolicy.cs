using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Step242;

internal static class StepFaceBoundaryExportPolicy
{
    internal const string ExplicitRolesCandidate = "explicit-face-boundary-roles";
    internal const string AuthoredOrderCandidate = "authored-loop-order";

    internal sealed record Decision(
        string Candidate,
        IReadOnlyList<(LoopId LoopId, FaceBoundaryRole Role)> Boundaries,
        IReadOnlyList<JudgmentRejection> Rejections);

    private sealed record Context(
        Face Face,
        IReadOnlyList<FaceBoundaryRoleBinding> Roles,
        bool HasCompleteExplicitRoles,
        bool HasExactlyOneOuter);

    private static readonly JudgmentEngine<Context> Engine = new();
    private static readonly IReadOnlyList<JudgmentCandidate<Context>> Candidates =
    [
        new(
            ExplicitRolesCandidate,
            context => context.HasCompleteExplicitRoles && context.HasExactlyOneOuter,
            _ => 1d,
            context => context.Roles.Count == 0
                ? "No explicit face-boundary role evidence is present."
                : "Explicit face-boundary roles are incomplete, cross-face, duplicated, or do not identify exactly one outer loop.",
            TieBreakerPriority: 0),
        new(
            AuthoredOrderCandidate,
            context => context.Roles.Count == 0 && context.Face.LoopIds.Count > 0,
            _ => 0.5d,
            _ => "Authored loop order is only admissible when no explicit role evidence exists.",
            TieBreakerPriority: 1)
    ];

    internal static Decision? Decide(BrepBody body, Face face)
    {
        if (face.LoopIds.Count == 0)
            return new Decision(AuthoredOrderCandidate, [], []);

        var roles = face.LoopIds
            .Where(loopId => body.Bindings.TryGetFaceBoundaryRoleBinding(loopId, out _))
            .Select(loopId => body.Bindings.GetFaceBoundaryRoleBinding(loopId))
            .ToArray();
        var uniqueLoops = roles.Select(role => role.LoopId).Distinct().Count() == roles.Length;
        var sameFace = roles.All(role => role.FaceId == face.Id);
        var complete = roles.Length == face.LoopIds.Count && uniqueLoops && sameFace;
        var exactlyOneOuter = roles.Count(role => role.Role == FaceBoundaryRole.Outer) == 1;
        var context = new Context(face, roles, complete, exactlyOneOuter);
        var judgment = Engine.Evaluate(context, Candidates);
        if (!judgment.IsSuccess) return null;

        var selected = judgment.Selection!.Value.Candidate.Name;
        var boundaries = selected == ExplicitRolesCandidate
            ? roles.OrderBy(role => role.Role == FaceBoundaryRole.Outer ? 0 : 1)
                .Select(role => (role.LoopId, role.Role))
                .ToArray()
            : face.LoopIds.Select((loopId, index) =>
                (loopId, index == 0 ? FaceBoundaryRole.Outer : FaceBoundaryRole.Inner)).ToArray();
        return new Decision(selected, boundaries, judgment.Rejections);
    }
}
