using System.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

internal sealed class DisplayTessellationExecutionBudget
{
    private static readonly TimeSpan MinimumRemaining = TimeSpan.FromMilliseconds(1);
    private readonly Stopwatch stopwatch;
    private readonly TimeSpan maxElapsed;

    internal DisplayTessellationExecutionBudget(TimeSpan maxElapsed)
    {
        stopwatch = Stopwatch.StartNew();
        this.maxElapsed = maxElapsed;
    }

    internal static DisplayTessellationExecutionBudget CreateDefault() => new(TimeSpan.FromSeconds(5));

    internal TimeSpan Elapsed => stopwatch.Elapsed;

    internal TimeSpan Remaining
    {
        get
        {
            var remaining = maxElapsed - stopwatch.Elapsed;
            return remaining > MinimumRemaining ? remaining : MinimumRemaining;
        }
    }

    internal void ThrowIfExpired(string phase, FaceId? faceId = null, SurfaceGeometryKind? surfaceKind = null)
    {
        if (stopwatch.Elapsed <= maxElapsed)
        {
            return;
        }

        throw new DisplayTessellationTimeoutException(phase, stopwatch.Elapsed, faceId, surfaceKind);
    }
}

internal sealed class DisplayTessellationTimeoutException : Exception
{
    internal DisplayTessellationTimeoutException(string phase, TimeSpan elapsed, FaceId? faceId, SurfaceGeometryKind? surfaceKind)
        : base(BuildMessage(phase, elapsed, faceId, surfaceKind))
    {
        Phase = phase;
        Elapsed = elapsed;
        FaceId = faceId;
        SurfaceKind = surfaceKind;
    }

    internal string Phase { get; }

    internal TimeSpan Elapsed { get; }

    internal FaceId? FaceId { get; }

    internal SurfaceGeometryKind? SurfaceKind { get; }

    private static string BuildMessage(string phase, TimeSpan elapsed, FaceId? faceId, SurfaceGeometryKind? surfaceKind)
    {
        var faceText = faceId.HasValue ? $"face {faceId.Value.Value}" : "unknown face";
        var surfaceText = surfaceKind?.ToString() ?? "unknown";
        return $"Display tessellation exceeded the bounded execution budget after {elapsed.TotalMilliseconds:F0} ms while processing {faceText} on surface '{surfaceText}' during phase '{phase}'.";
    }
}
