using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Firmament.Execution;

internal enum RestrictedContourSnapKind
{
    Line,
    Circle,
    EllipseDeferred,
    BSplineDeferred,
    Unsupported,
}

internal enum RestrictedContourSnapStatus
{
    Candidate,
    Rejected,
    Deferred,
    Unsupported,
}

internal sealed record LineSnapParameters2D(double PointU, double PointV, double DirectionU, double DirectionV);

internal sealed record CircleSnapParameters2D(double CenterU, double CenterV, double Radius);

internal sealed record RestrictedContourSnapCandidate(
    int ChainId,
    RestrictedContourSnapKind Kind,
    RestrictedContourSnapStatus Status,
    object? Parameters,
    double MaxError,
    double MeanError,
    int SampleCount,
    IReadOnlyList<string> Diagnostics);

internal sealed record RestrictedContourSnapOptions(double MaxCircleError, double MaxLineError, int MinPointCount)
{
    internal static RestrictedContourSnapOptions ConservativeDefaults { get; } = new(0.02d, 0.01d, 8);
}

internal sealed record RestrictedContourSnapAnalysisResult(
    bool Success,
    IReadOnlyList<RestrictedContourSnapCandidate> Candidates,
    IReadOnlyList<string> Diagnostics,
    int AcceptedCount = 0,
    bool ExactTrimAccepted = false,
    bool BRepTopologyImplemented = false,
    bool StepExportImplemented = false);

internal static class RestrictedContourSnapAnalyzer
{
    internal static RestrictedContourSnapAnalysisResult Analyze(SurfaceTrimContourStitchResult stitchResult, RestrictedContourSnapOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stitchResult);
        var opts = options ?? RestrictedContourSnapOptions.ConservativeDefaults;
        var diagnostics = new List<string>
        {
            "snap-analysis-started",
            $"snap-chain-count:{stitchResult.Chains.Count}",
            $"snap-option-max-circle-error:{opts.MaxCircleError:R}",
            $"snap-option-max-line-error:{opts.MaxLineError:R}",
            $"snap-option-min-point-count:{opts.MinPointCount}",
        };

        var candidates = new List<RestrictedContourSnapCandidate>();

        foreach (var chain in stitchResult.Chains.OrderBy(c => c.ChainId))
        {
            if (chain.Points.Count < opts.MinPointCount)
            {
                candidates.Add(new RestrictedContourSnapCandidate(
                    chain.ChainId,
                    chain.Closed ? RestrictedContourSnapKind.Circle : RestrictedContourSnapKind.Line,
                    RestrictedContourSnapStatus.Rejected,
                    null,
                    double.PositiveInfinity,
                    double.PositiveInfinity,
                    chain.Points.Count,
                    [$"snap-rejected-too-few-points:{chain.Points.Count}"]));
                continue;
            }

            if (chain.Closed || chain.Status == SurfaceTrimContourChainStatus.ClosedLoop)
            {
                candidates.Add(FitCircle(chain, opts));
                candidates.Add(new RestrictedContourSnapCandidate(chain.ChainId, RestrictedContourSnapKind.BSplineDeferred, RestrictedContourSnapStatus.Deferred, null, double.NaN, double.NaN, chain.Points.Count, ["snap-bspline-deferred:candidate-only-milestone"]));
            }
            else if (chain.Status is SurfaceTrimContourChainStatus.OpenChain or SurfaceTrimContourChainStatus.BoundaryTouching)
            {
                candidates.Add(FitLine(chain, opts));
                candidates.Add(new RestrictedContourSnapCandidate(chain.ChainId, RestrictedContourSnapKind.EllipseDeferred, RestrictedContourSnapStatus.Deferred, null, double.NaN, double.NaN, chain.Points.Count, ["snap-ellipse-deferred:out-of-scope"]));
            }
            else
            {
                candidates.Add(new RestrictedContourSnapCandidate(chain.ChainId, RestrictedContourSnapKind.Unsupported, RestrictedContourSnapStatus.Unsupported, null, double.NaN, double.NaN, chain.Points.Count, [$"snap-chain-status-unsupported:{chain.Status}"]));
            }
        }

        diagnostics.Add($"snap-candidate-count:{candidates.Count}");
        diagnostics.Add($"snap-circle-candidate-count:{candidates.Count(c => c.Kind == RestrictedContourSnapKind.Circle && c.Status == RestrictedContourSnapStatus.Candidate)}");
        diagnostics.Add($"snap-line-candidate-count:{candidates.Count(c => c.Kind == RestrictedContourSnapKind.Line && c.Status == RestrictedContourSnapStatus.Candidate)}");
        diagnostics.Add("snap-exact-trim-not-accepted");
        diagnostics.Add("snap-brep-topology-not-implemented");
        diagnostics.Add("snap-step-export-not-implemented");

        return new RestrictedContourSnapAnalysisResult(stitchResult.Success, candidates, diagnostics);
    }

    private static RestrictedContourSnapCandidate FitCircle(SurfaceTrimContourChain2D chain, RestrictedContourSnapOptions options)
    {
        var pts = chain.Points.Select(p => (u: p.U, v: p.V)).ToArray();
        var centerU = pts.Average(p => p.u);
        var centerV = pts.Average(p => p.v);
        var radii = pts.Select(p => double.Sqrt((p.u - centerU) * (p.u - centerU) + (p.v - centerV) * (p.v - centerV))).ToArray();
        var radius = radii.Average();
        var errors = radii.Select(r => double.Abs(r - radius)).ToArray();
        var maxError = errors.Max();
        var meanError = errors.Average();
        var status = maxError <= options.MaxCircleError ? RestrictedContourSnapStatus.Candidate : RestrictedContourSnapStatus.Rejected;
        var diag = new List<string>
        {
            "snap-circle-fit:centroid-mean-radius",
            $"snap-circle-max-error:{maxError:R}",
            $"snap-circle-mean-error:{meanError:R}",
            $"snap-circle-threshold:{options.MaxCircleError:R}",
            status == RestrictedContourSnapStatus.Candidate ? "snap-circle-candidate-produced" : "snap-circle-candidate-rejected:high-radial-error",
            "snap-candidate-only:not-exact-trim"
        };

        return new RestrictedContourSnapCandidate(chain.ChainId, RestrictedContourSnapKind.Circle, status, new CircleSnapParameters2D(centerU, centerV, radius), maxError, meanError, pts.Length, diag);
    }

    private static RestrictedContourSnapCandidate FitLine(SurfaceTrimContourChain2D chain, RestrictedContourSnapOptions options)
    {
        var pts = chain.Points.Select(p => (u: p.U, v: p.V)).ToArray();
        var first = pts.First();
        var last = pts.Last();
        var du = last.u - first.u;
        var dv = last.v - first.v;
        var length = double.Sqrt((du * du) + (dv * dv));
        if (ToleranceMath.AlmostZero(length, ToleranceContext.Default))
        {
            return new RestrictedContourSnapCandidate(chain.ChainId, RestrictedContourSnapKind.Line, RestrictedContourSnapStatus.Rejected, null, double.PositiveInfinity, double.PositiveInfinity, pts.Length, ["snap-line-candidate-rejected:degenerate-direction"]);
        }

        var dirU = du / length;
        var dirV = dv / length;
        var errors = pts.Select(p => double.Abs(((p.u - first.u) * (-dirV)) + ((p.v - first.v) * dirU))).ToArray();
        var maxError = errors.Max();
        var meanError = errors.Average();
        var status = maxError <= options.MaxLineError ? RestrictedContourSnapStatus.Candidate : RestrictedContourSnapStatus.Deferred;
        return new RestrictedContourSnapCandidate(
            chain.ChainId,
            RestrictedContourSnapKind.Line,
            status,
            new LineSnapParameters2D(first.u, first.v, dirU, dirV),
            maxError,
            meanError,
            pts.Length,
            [
                "snap-line-fit:endpoint-direction",
                $"snap-line-max-error:{maxError:R}",
                $"snap-line-mean-error:{meanError:R}",
                $"snap-line-threshold:{options.MaxLineError:R}",
                status == RestrictedContourSnapStatus.Candidate ? "snap-line-candidate-produced" : "snap-line-candidate-deferred:high-perpendicular-error",
                "snap-candidate-only:not-exact-trim"
            ]);
    }
}
