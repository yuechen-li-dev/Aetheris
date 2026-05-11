using Aetheris.Kernel.Core.Judgment;

namespace Aetheris.Kernel.Firmament.Execution;

internal enum RestrictedContourSnapRouteKind
{
    AnalyticCircle,
    AnalyticLine,
    NumericalOnly,
    Deferred,
    Unsupported,
}

internal enum RestrictedContourExportCapability
{
    ElementaryCurveCandidate,
    NumericalOnlyNotExportable,
    Deferred,
    Unsupported,
}

internal sealed record RestrictedContourSnapSelectionCandidate(
    RestrictedContourSnapRouteKind RouteKind,
    RestrictedContourSnapKind CandidateKind,
    bool Admissible,
    double Score,
    string RejectionReason,
    IReadOnlyList<string> Diagnostics);

internal sealed record RestrictedContourSnapSelectionResult(
    bool Success,
    RestrictedContourSnapRouteKind SelectedRoute,
    RestrictedContourSnapCandidate? SelectedCandidate,
    bool AcceptedAnalyticCandidate,
    RestrictedContourExportCapability ExportCapability,
    IReadOnlyList<RestrictedContourSnapSelectionCandidate> CandidateTraces,
    IReadOnlyList<string> Diagnostics,
    bool BRepTopologyImplemented = false,
    bool StepExportImplemented = false);

internal static class RestrictedContourSnapSelector
{
    private sealed record SelectionContext(
        SurfaceTrimContourChain2D Chain,
        IReadOnlyList<RestrictedContourSnapCandidate> Candidates,
        RestrictedContourSnapOptions Options,
        bool PreferCircle,
        bool PreferLine);

    private static readonly JudgmentEngine<SelectionContext> Engine = new();

    internal static RestrictedContourSnapSelectionResult Select(
        SurfaceTrimContourStitchResult stitchResult,
        RestrictedContourSnapAnalysisResult analysis,
        RestrictedContourSnapOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stitchResult);
        ArgumentNullException.ThrowIfNull(analysis);
        var opts = options ?? RestrictedContourSnapOptions.ConservativeDefaults;

        var traces = new List<RestrictedContourSnapSelectionCandidate>();
        var diagnostics = new List<string>
        {
            "snap-selection-started",
            $"snap-selection-chain-count:{stitchResult.Chains.Count}",
            $"snap-selection-candidate-count:{analysis.Candidates.Count}",
        };

        var selected = new List<(SurfaceTrimContourChain2D Chain, RestrictedContourSnapCandidate Candidate, RestrictedContourSnapRouteKind Route, double Score)>();
        foreach (var chain in stitchResult.Chains.OrderBy(c => c.ChainId))
        {
            var chainCandidates = analysis.Candidates.Where(c => c.ChainId == chain.ChainId).ToArray();
            if (chainCandidates.Length == 0)
            {
                diagnostics.Add($"snap-selection-chain-no-candidates:{chain.ChainId}");
                continue;
            }

            var ctx = new SelectionContext(
                chain,
                chainCandidates,
                opts,
                chain.Status == SurfaceTrimContourChainStatus.ClosedLoop || chain.Closed,
                chain.Status is SurfaceTrimContourChainStatus.OpenChain or SurfaceTrimContourChainStatus.BoundaryTouching || chain.BoundaryTouching);

            var judgmentCandidates = BuildJudgmentCandidates(chainCandidates, ctx);
            var eval = Engine.Evaluate(ctx, judgmentCandidates);
            foreach (var jc in judgmentCandidates)
            {
                var admissible = jc.IsAdmissible(ctx);
                var score = admissible ? jc.Score(ctx) : double.NegativeInfinity;
                var rejection = admissible ? string.Empty : jc.RejectionReason?.Invoke(ctx) ?? "candidate-not-admissible";
                traces.Add(new RestrictedContourSnapSelectionCandidate(
                    ParseRoute(jc.Name),
                    ParseKind(jc.Name),
                    admissible,
                    score,
                    rejection,
                    chainCandidates.Where(c => KindName(c.Kind) == KindName(ParseKind(jc.Name))).SelectMany(c => c.Diagnostics).ToArray()));
            }

            if (!eval.IsSuccess)
            {
                diagnostics.Add($"snap-selection-chain-no-admissible-analytic:{chain.ChainId}");
                diagnostics.AddRange(eval.Rejections.Select(r => $"snap-selection-rejection:{chain.ChainId}:{r.CandidateName}:{r.Reason}"));
                continue;
            }

            var sel = eval.Selection!.Value;
            var picked = chainCandidates.First(c => RouteFromKind(c.Kind) == ParseRoute(sel.Candidate.Name));
            selected.Add((chain, picked, ParseRoute(sel.Candidate.Name), sel.Score));
        }

        if (selected.Count == 0)
        {
            var route = stitchResult.Chains.Count == 0 ? RestrictedContourSnapRouteKind.Unsupported : RestrictedContourSnapRouteKind.NumericalOnly;
            diagnostics.Add(route == RestrictedContourSnapRouteKind.Unsupported
                ? "snap-selection-unsupported:no-chains"
                : "snap-selection-numerical-only:analytic-rejected-or-deferred");
            diagnostics.Add("snap-selection-not-exact-trim");
            diagnostics.Add("snap-selection-brep-topology-not-implemented");
            diagnostics.Add("snap-selection-step-export-not-implemented");
            diagnostics.Add("snap-selection-torus-generic-exactness-not-implied");
            return new RestrictedContourSnapSelectionResult(
                analysis.Success,
                route,
                null,
                false,
                route == RestrictedContourSnapRouteKind.Unsupported ? RestrictedContourExportCapability.Unsupported : RestrictedContourExportCapability.NumericalOnlyNotExportable,
                traces,
                diagnostics);
        }

        var best = selected
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Route)
            .ThenBy(s => s.Chain.ChainId)
            .First();

        diagnostics.Add($"snap-selection-route:{best.Route}");
        diagnostics.Add("snap-selection-accepted-internal-analytic-candidate");
        diagnostics.Add("snap-selection-not-exact-trim");
        diagnostics.Add("snap-selection-brep-topology-not-implemented");
        diagnostics.Add("snap-selection-step-export-not-implemented");
        if (best.Route == RestrictedContourSnapRouteKind.AnalyticCircle)
        {
            diagnostics.Add("snap-selection-torus-generic-exactness-not-implied");
        }

        return new RestrictedContourSnapSelectionResult(
            analysis.Success,
            best.Route,
            best.Candidate,
            true,
            RestrictedContourExportCapability.ElementaryCurveCandidate,
            traces,
            diagnostics);
    }

    private static IReadOnlyList<JudgmentCandidate<SelectionContext>> BuildJudgmentCandidates(IReadOnlyList<RestrictedContourSnapCandidate> chainCandidates, SelectionContext context)
    {
        var list = new List<JudgmentCandidate<SelectionContext>>();
        if (chainCandidates.Any(c => c.Kind == RestrictedContourSnapKind.Circle))
        {
            list.Add(new JudgmentCandidate<SelectionContext>(
                "AnalyticCircle:Circle",
                IsAdmissibleCircle,
                ScoreCircle,
                CircleRejection,
                TieBreakerPriority: context.PreferCircle ? 0 : 1));
        }

        if (chainCandidates.Any(c => c.Kind == RestrictedContourSnapKind.Line))
        {
            list.Add(new JudgmentCandidate<SelectionContext>(
                "AnalyticLine:Line",
                IsAdmissibleLine,
                ScoreLine,
                LineRejection,
                TieBreakerPriority: context.PreferLine ? 0 : 1));
        }

        return list;
    }

    private static bool IsAdmissibleCircle(SelectionContext ctx)
    {
        var candidate = ctx.Candidates.FirstOrDefault(c => c.Kind == RestrictedContourSnapKind.Circle);
        return candidate is not null
               && candidate.Status == RestrictedContourSnapStatus.Candidate
               && candidate.SampleCount >= ctx.Options.MinPointCount
               && candidate.MaxError <= ctx.Options.MaxCircleError
               && (ctx.Chain.Closed || ctx.Chain.Status == SurfaceTrimContourChainStatus.ClosedLoop);
    }

    private static bool IsAdmissibleLine(SelectionContext ctx)
    {
        var candidate = ctx.Candidates.FirstOrDefault(c => c.Kind == RestrictedContourSnapKind.Line);
        return candidate is not null
               && candidate.Status == RestrictedContourSnapStatus.Candidate
               && candidate.SampleCount >= ctx.Options.MinPointCount
               && candidate.MaxError <= ctx.Options.MaxLineError
               && (ctx.Chain.Status is SurfaceTrimContourChainStatus.OpenChain or SurfaceTrimContourChainStatus.BoundaryTouching || ctx.Chain.BoundaryTouching);
    }

    private static double ScoreCircle(SelectionContext ctx)
    {
        var candidate = ctx.Candidates.First(c => c.Kind == RestrictedContourSnapKind.Circle);
        var score = 1000d - (candidate.MaxError * 1000d) - (candidate.MeanError * 100d);
        if (ctx.PreferCircle) score += 10d;
        return score;
    }

    private static double ScoreLine(SelectionContext ctx)
    {
        var candidate = ctx.Candidates.First(c => c.Kind == RestrictedContourSnapKind.Line);
        var score = 1000d - (candidate.MaxError * 1000d) - (candidate.MeanError * 100d);
        if (ctx.PreferLine) score += 10d;
        return score;
    }

    private static string CircleRejection(SelectionContext ctx)
    {
        var c = ctx.Candidates.FirstOrDefault(x => x.Kind == RestrictedContourSnapKind.Circle);
        if (c is null) return "circle-candidate-missing";
        if (c.Status != RestrictedContourSnapStatus.Candidate) return $"circle-status:{c.Status}";
        if (c.SampleCount < ctx.Options.MinPointCount) return $"circle-sample-count-below-min:{c.SampleCount}";
        if (c.MaxError > ctx.Options.MaxCircleError) return $"circle-max-error-exceeds-threshold:{c.MaxError:R}";
        return "circle-chain-not-closed";
    }

    private static string LineRejection(SelectionContext ctx)
    {
        var c = ctx.Candidates.FirstOrDefault(x => x.Kind == RestrictedContourSnapKind.Line);
        if (c is null) return "line-candidate-missing";
        if (c.Status != RestrictedContourSnapStatus.Candidate) return $"line-status:{c.Status}";
        if (c.SampleCount < ctx.Options.MinPointCount) return $"line-sample-count-below-min:{c.SampleCount}";
        if (c.MaxError > ctx.Options.MaxLineError) return $"line-max-error-exceeds-threshold:{c.MaxError:R}";
        return "line-chain-not-open-or-boundary";
    }

    private static RestrictedContourSnapRouteKind RouteFromKind(RestrictedContourSnapKind kind) => kind switch
    {
        RestrictedContourSnapKind.Circle => RestrictedContourSnapRouteKind.AnalyticCircle,
        RestrictedContourSnapKind.Line => RestrictedContourSnapRouteKind.AnalyticLine,
        _ => RestrictedContourSnapRouteKind.Deferred,
    };

    private static RestrictedContourSnapRouteKind ParseRoute(string name) => name.StartsWith("AnalyticCircle", StringComparison.Ordinal)
        ? RestrictedContourSnapRouteKind.AnalyticCircle
        : RestrictedContourSnapRouteKind.AnalyticLine;

    private static RestrictedContourSnapKind ParseKind(string name) => name.EndsWith(":Circle", StringComparison.Ordinal)
        ? RestrictedContourSnapKind.Circle
        : RestrictedContourSnapKind.Line;

    private static string KindName(RestrictedContourSnapKind kind) => kind.ToString();
}
