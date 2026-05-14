using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Step242;

public enum StepSolidRootExportKind
{
    ManifoldSolidBrep,
    BrepWithVoids,
    Unsupported
}

public enum StepSolidRootExportCapability
{
    Supported,
    Unsupported
}

public sealed record StepSolidRootExportPolicyEvaluation(
    string PolicyName,
    bool Admissible,
    double Score,
    StepSolidRootExportKind Kind,
    IReadOnlyList<string> RejectionReasons,
    IReadOnlyList<string> Diagnostics);

public sealed record StepSolidRootExportDecision(
    StepSolidRootExportKind Kind,
    string SelectedPolicyName,
    StepSolidRootExportCapability Capability,
    IReadOnlyList<StepSolidRootExportPolicyEvaluation> Evaluations,
    IReadOnlyList<string> Diagnostics,
    int TopologyBodyCount,
    int TopologyShellCount,
    ShellId? OuterShellId,
    IReadOnlyList<ShellId> InnerShellIds);

public static class StepSolidRootExportPlanner
{
    private static readonly JudgmentEngine<BrepBody> Engine = new();

    public static StepSolidRootExportDecision Decide(BrepBody body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var bodyNodes = body.Topology.Bodies.OrderBy(b => b.Id.Value).ToArray();
        var topologyBodyCount = bodyNodes.Length;
        var topologyShellCount = bodyNodes.Length == 1 ? bodyNodes[0].ShellIds.Count : 0;
        var shellRepresentation = body.ShellRepresentation;
        var innerShellIds = shellRepresentation?.InnerShellIds?.OrderBy(id => id.Value).ToArray() ?? Array.Empty<ShellId>();

        var diagnostics = new List<string>
        {
            "StepSolidRootExportPlanner started.",
            $"Topology body count: {topologyBodyCount}.",
            $"Topology shell count: {topologyShellCount}.",
            shellRepresentation is null ? "Shell representation: missing." : "Shell representation: found.",
            $"Outer shell id: {(shellRepresentation is null ? "<none>" : shellRepresentation.OuterShellId.Value.ToString())}.",
            $"Inner shell ids: {(innerShellIds.Length == 0 ? "<none>" : string.Join(",", innerShellIds.Select(s => s.Value)))}."
        };

        var evaluations = new[]
        {
            EvaluateManifold(body, bodyNodes, shellRepresentation),
            EvaluateBrepWithVoids(body, bodyNodes, shellRepresentation),
            EvaluateUnsupported(body, bodyNodes, shellRepresentation)
        };

        var candidates = evaluations.Select((evaluation, index) => new JudgmentCandidate<BrepBody>(
            evaluation.PolicyName,
            _ => evaluation.Admissible,
            _ => evaluation.Score,
            _ => evaluation.RejectionReasons.Count == 0 ? "Policy evaluation was non-admissible." : string.Join(" | ", evaluation.RejectionReasons),
            index)).ToArray();

        var judgment = Engine.Evaluate(body, candidates);

        var manifoldAdmissible = evaluations.Single(e => e.Kind == StepSolidRootExportKind.ManifoldSolidBrep).Admissible;
        var voidsAdmissible = evaluations.Single(e => e.Kind == StepSolidRootExportKind.BrepWithVoids).Admissible;
        diagnostics.Add($"Manifold policy: {(manifoldAdmissible ? "admissible" : "rejected")}.");
        diagnostics.Add($"BrepWithVoids policy: {(voidsAdmissible ? "admissible" : "rejected")}.");

        if (manifoldAdmissible && voidsAdmissible)
        {
            diagnostics.Add("Conflict: both manifold and brep-with-voids policies are admissible.");
        }

        if (!judgment.IsSuccess)
        {
            diagnostics.Add("Unsupported fallback selected: no admissible policy from judgment engine.");
            return new(
                StepSolidRootExportKind.Unsupported,
                nameof(UnsupportedShellTopologyPolicy),
                StepSolidRootExportCapability.Unsupported,
                evaluations,
                diagnostics,
                topologyBodyCount,
                topologyShellCount,
                shellRepresentation?.OuterShellId,
                innerShellIds);
        }

        var selectedName = judgment.Selection!.Value.Candidate.Name;
        var selected = evaluations.Single(e => string.Equals(e.PolicyName, selectedName, StringComparison.Ordinal));
        diagnostics.Add($"Selected root kind: {selected.Kind} via {selected.PolicyName}.");
        if (selected.Kind == StepSolidRootExportKind.Unsupported)
        {
            diagnostics.Add("Unsupported fallback selected.");
        }

        return new(
            selected.Kind,
            selected.PolicyName,
            selected.Kind == StepSolidRootExportKind.Unsupported ? StepSolidRootExportCapability.Unsupported : StepSolidRootExportCapability.Supported,
            evaluations,
            diagnostics,
            topologyBodyCount,
            topologyShellCount,
            shellRepresentation?.OuterShellId,
            innerShellIds);
    }

    private static StepSolidRootExportPolicyEvaluation EvaluateManifold(BrepBody body, Body[] bodyNodes, BrepBodyShellRepresentation? shellRepresentation)
    {
        var reject = new List<string>();
        if (bodyNodes.Length != 1) reject.Add("Multiple topology bodies are unsupported for manifold export.");
        if (shellRepresentation is null)
        {
            if (bodyNodes.Length == 1 && bodyNodes[0].ShellIds.Count != 1)
            {
                reject.Add("Missing shell representation for multi-shell topology.");
            }
        }
        else
        {
            if (shellRepresentation.InnerShellIds.Count > 0) reject.Add("Inner shell ids exist.");
            if (!body.Topology.TryGetShell(shellRepresentation.OuterShellId, out _)) reject.Add("Outer shell id is missing in topology.");
            if (shellRepresentation.InnerShellIds.Count == 0 && bodyNodes.Length == 1 && bodyNodes[0].ShellIds.Count > 1) reject.Add("Ambiguous multi-shell role state.");
        }

        return new(nameof(ManifoldSolidBrepExportPolicy), reject.Count == 0, reject.Count == 0 ? 1000d : 0d, StepSolidRootExportKind.ManifoldSolidBrep, reject, []);
    }

    private static StepSolidRootExportPolicyEvaluation EvaluateBrepWithVoids(BrepBody body, Body[] bodyNodes, BrepBodyShellRepresentation? shellRepresentation)
    {
        var reject = new List<string>();
        if (bodyNodes.Length != 1) reject.Add("Multiple topology bodies are unsupported for brep-with-voids export.");
        if (shellRepresentation is null) reject.Add("Shell representation is missing.");
        else
        {
            if (shellRepresentation.InnerShellIds.Count == 0) reject.Add("No inner shells are present.");
            if (!body.Topology.TryGetShell(shellRepresentation.OuterShellId, out _)) reject.Add("Outer shell id is missing in topology.");
            foreach (var inner in shellRepresentation.InnerShellIds)
            {
                if (!body.Topology.TryGetShell(inner, out _)) reject.Add($"Inner shell id {inner.Value} is missing in topology.");
            }
        }

        return new(nameof(BrepWithVoidsExportPolicy), reject.Count == 0, reject.Count == 0 ? 1000d : 0d, StepSolidRootExportKind.BrepWithVoids, reject, []);
    }

    private static StepSolidRootExportPolicyEvaluation EvaluateUnsupported(BrepBody body, Body[] bodyNodes, BrepBodyShellRepresentation? shellRepresentation)
    {
        var blockers = new List<string>();
        if (bodyNodes.Length == 0) blockers.Add("No body present.");
        if (bodyNodes.Length > 1) blockers.Add("Multiple topology bodies are unsupported.");
        if (bodyNodes.Length == 1 && bodyNodes[0].ShellIds.Count > 1 && shellRepresentation is null) blockers.Add("Missing shell representation for multi-shell topology.");
        if (shellRepresentation is not null)
        {
            if (!body.Topology.TryGetShell(shellRepresentation.OuterShellId, out _)) blockers.Add("Invalid outer shell id.");
            foreach (var inner in shellRepresentation.InnerShellIds)
            {
                if (!body.Topology.TryGetShell(inner, out _)) blockers.Add($"Invalid inner shell id {inner.Value}.");
            }
            if (shellRepresentation.InnerShellIds.Count == 0 && bodyNodes.Length == 1 && bodyNodes[0].ShellIds.Count > 1) blockers.Add("Ambiguous multi-shell roles.");
        }

        var admissible = blockers.Count > 0;
        return new(nameof(UnsupportedShellTopologyPolicy), admissible, admissible ? 1d : 0d, StepSolidRootExportKind.Unsupported, admissible ? Array.Empty<string>() : new[] { "No unsupported blockers were detected." }, blockers);
    }

    private sealed class ManifoldSolidBrepExportPolicy;
    private sealed class BrepWithVoidsExportPolicy;
    private sealed class UnsupportedShellTopologyPolicy;
}
