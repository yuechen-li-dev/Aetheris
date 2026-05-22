using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Cir;

public enum CirRegionPlanAction
{
    ClassifyInside,
    ClassifyOutside,
    MarkMixed,
    Subdivide,
    SampleDirectly,
    RejectUnknown,
}

public sealed record CirRegionPlannerOptions(
    int MaxDepth = 6,
    int DirectSampleThreshold = 64,
    double MinimumRegionExtent = 0.05d)
{
    public static CirRegionPlannerOptions Default { get; } = new();
}

public readonly record struct CirRegionPlanContext(
    CirBounds Region,
    FieldInterval Interval,
    CirRegionClassification Classification,
    int Depth,
    int MaxDepth,
    int EstimatedSampleCount,
    int DirectSampleThreshold,
    double RegionVolume,
    ToleranceContext Tolerance);

public readonly record struct CirRegionPlanRejection(string CandidateName, string Reason);

public sealed record CirRegionPlanResult(
    CirRegionPlanAction Action,
    string SelectedCandidate,
    FieldInterval Interval,
    CirRegionClassification Classification,
    CirBounds Region,
    double? Score,
    IReadOnlyList<CirRegionPlanRejection> RejectedCandidates,
    int Depth,
    int MaxDepth,
    string? Note = null);

public sealed class CirRegionPlanner
{
    private static readonly JudgmentEngine<CirRegionPlanContext> Engine = new();
    private static readonly IReadOnlyList<JudgmentCandidate<CirRegionPlanContext>> Candidates = BuildCandidates();

    public CirRegionPlanResult Plan(CirRegionPlanContext context)
    {
        var result = Engine.Evaluate(context, Candidates);
        if (result.IsSuccess)
        {
            var selection = result.Selection!.Value;
            var rejected = BuildTraceRejections(context, selection, result.Rejections);
            return new CirRegionPlanResult(
                MapCandidateToAction(selection.Candidate.Name),
                selection.Candidate.Name,
                context.Interval,
                context.Classification,
                context.Region,
                selection.Score,
                rejected,
                context.Depth,
                context.MaxDepth);
        }

        var rejections = result.Rejections.Select(r => new CirRegionPlanRejection(r.CandidateName, r.Reason)).ToArray();
        var note = rejections.Length > 0
            ? $"No admissible planner candidates. First rejection: {rejections[0].CandidateName} ({rejections[0].Reason})."
            : "No admissible planner candidates.";

        return new CirRegionPlanResult(
            CirRegionPlanAction.RejectUnknown,
            "reject_unknown",
            context.Interval,
            context.Classification,
            context.Region,
            null,
            rejections,
            context.Depth,
            context.MaxDepth,
            note);
    }

    public CirRegionPlanResult Plan(CirTape tape, CirBounds region, int depth, CirRegionPlannerOptions? options = null, ToleranceContext? tolerance = null)
    {
        ArgumentNullException.ThrowIfNull(tape);

        var plannerOptions = options ?? CirRegionPlannerOptions.Default;
        var effectiveTolerance = tolerance ?? ToleranceContext.Default;
        var interval = tape.EvaluateInterval(region);
        var classification = tape.ClassifyRegion(region, effectiveTolerance);

        return Plan(BuildContext(region, interval, classification, depth, plannerOptions, effectiveTolerance));
    }

    public static CirRegionPlanContext BuildContext(
        CirBounds region,
        FieldInterval interval,
        CirRegionClassification classification,
        int depth,
        CirRegionPlannerOptions options,
        ToleranceContext tolerance)
    {
        var sizeX = double.Max(region.SizeX, 0d);
        var sizeY = double.Max(region.SizeY, 0d);
        var sizeZ = double.Max(region.SizeZ, 0d);
        var regionVolume = sizeX * sizeY * sizeZ;
        var estimatedSampleCount = EstimateSampleCount(region, options.MinimumRegionExtent);

        return new CirRegionPlanContext(region, interval, classification, depth, options.MaxDepth, estimatedSampleCount, options.DirectSampleThreshold, regionVolume, tolerance);
    }

    private static IReadOnlyList<JudgmentCandidate<CirRegionPlanContext>> BuildCandidates() =>
    [
        new JudgmentCandidate<CirRegionPlanContext>(
            "classify_inside",
            IsAdmissible: context => context.Interval.IsDefinitelyInside(context.Tolerance),
            Score: _ => 1000d,
            RejectionReason: context => $"Interval is not definitely inside [{context.Interval.MinValue:R}, {context.Interval.MaxValue:R}].",
            TieBreakerPriority: 0),
        new JudgmentCandidate<CirRegionPlanContext>(
            "classify_outside",
            IsAdmissible: context => context.Interval.IsDefinitelyOutside(context.Tolerance),
            Score: _ => 1000d,
            RejectionReason: context => $"Interval is not definitely outside [{context.Interval.MinValue:R}, {context.Interval.MaxValue:R}].",
            TieBreakerPriority: 1),
        new JudgmentCandidate<CirRegionPlanContext>(
            "subdivide_mixed",
            IsAdmissible: context => context.Classification == CirRegionClassification.Mixed
                                     && context.Depth < context.MaxDepth
                                     && MinExtent(context.Region) > 0d
                                     && MinExtent(context.Region) >= context.Tolerance.Linear,
            Score: context => 500d + context.RegionVolume,
            RejectionReason: BuildSubdivideRejection,
            TieBreakerPriority: 2),
        new JudgmentCandidate<CirRegionPlanContext>(
            "sample_directly",
            IsAdmissible: context => context.Classification == CirRegionClassification.Mixed
                                     && (context.Depth >= context.MaxDepth
                                         || MinExtent(context.Region) <= context.Tolerance.Linear
                                         || context.EstimatedSampleCount <= context.DirectSampleThreshold),
            Score: context => 250d - context.EstimatedSampleCount,
            RejectionReason: BuildSampleRejection,
            TieBreakerPriority: 3),
    ];

    private static int EstimateSampleCount(CirBounds region, double minimumRegionExtent)
    {
        var cell = double.Max(minimumRegionExtent, 1e-9d);
        var x = System.Math.Max(1, (int)double.Ceiling(region.SizeX / cell));
        var y = System.Math.Max(1, (int)double.Ceiling(region.SizeY / cell));
        var z = System.Math.Max(1, (int)double.Ceiling(region.SizeZ / cell));
        return x * y * z;
    }

    private static double MinExtent(CirBounds bounds) => double.Min(bounds.SizeX, double.Min(bounds.SizeY, bounds.SizeZ));

    private static string BuildSubdivideRejection(CirRegionPlanContext context)
    {
        if (context.Classification != CirRegionClassification.Mixed)
        {
            return $"Classification is {context.Classification}, subdivision reserved for mixed regions.";
        }

        if (context.Depth >= context.MaxDepth)
        {
            return $"Depth limit reached ({context.Depth}/{context.MaxDepth}).";
        }

        if (MinExtent(context.Region) < context.Tolerance.Linear)
        {
            return $"Region extent {MinExtent(context.Region):R} below tolerance {context.Tolerance.Linear:R}.";
        }

        return "Subdivision admissibility predicates were not satisfied.";
    }

    private static string BuildSampleRejection(CirRegionPlanContext context)
    {
        if (context.Classification != CirRegionClassification.Mixed)
        {
            return $"Classification is {context.Classification}, direct sampling reserved for mixed regions.";
        }

        return $"Mixed region remains eligible for subdivision ({context.Depth}/{context.MaxDepth}) with estimated samples {context.EstimatedSampleCount}.";
    }

    private static CirRegionPlanAction MapCandidateToAction(string candidateName)
        => candidateName switch
        {
            "classify_inside" => CirRegionPlanAction.ClassifyInside,
            "classify_outside" => CirRegionPlanAction.ClassifyOutside,
            "subdivide_mixed" => CirRegionPlanAction.Subdivide,
            "sample_directly" => CirRegionPlanAction.SampleDirectly,
            _ => CirRegionPlanAction.RejectUnknown,
        };

    private static IReadOnlyList<CirRegionPlanRejection> BuildTraceRejections(
        CirRegionPlanContext context,
        JudgmentSelection<CirRegionPlanContext> selection,
        IReadOnlyList<JudgmentRejection> engineRejections)
    {
        var rejections = engineRejections
            .Select(r => new CirRegionPlanRejection(r.CandidateName, r.Reason))
            .ToList();

        foreach (var candidate in Candidates)
        {
            if (candidate.Name == selection.Candidate.Name)
            {
                continue;
            }

            if (rejections.Any(r => r.CandidateName == candidate.Name))
            {
                continue;
            }

            var score = candidate.Score(context);
            rejections.Add(new CirRegionPlanRejection(candidate.Name, $"Admissible but lower score ({score:R}) than selected candidate ({selection.Score:R})."));
        }

        return rejections;
    }
}
