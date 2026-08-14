using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Surgery;

/// <summary>
/// Creates a topology face from caller-authorized outer and inner loops.
/// Loop zero is outer by contract; remaining loops are caller-oriented inner trims.
/// Surface binding remains an explicit recipe responsibility.
/// </summary>
internal static class BrepFaceBuilder
{
    public static KernelResult<FaceId> CreateKnownFaceFromLoops(
        TopologyBuilder builder,
        LoopId outerLoop,
        IReadOnlyList<LoopId>? innerLoops = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var loopIds = new List<LoopId>(1 + (innerLoops?.Count ?? 0)) { outerLoop };
        if (innerLoops is not null)
        {
            loopIds.AddRange(innerLoops);
        }

        var seen = new HashSet<LoopId>();
        foreach (var loopId in loopIds)
        {
            if (!seen.Add(loopId))
            {
                return Failure($"Known face repeats loop {loopId.Value}.");
            }

            if (!builder.Model.TryGetLoop(loopId, out _))
            {
                return Failure($"Known face references missing loop {loopId.Value}.");
            }
        }

        return KernelResult<FaceId>.Success(builder.AddFace(loopIds));
    }

    public static KernelResult<FaceId> CreateKnownFace(
        TopologyBuilder builder,
        IReadOnlyList<BrepEdgeUse> outerLoop,
        IReadOnlyList<IReadOnlyList<BrepEdgeUse>>? innerLoops = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(outerLoop);

        var loopIds = new List<LoopId>(1 + (innerLoops?.Count ?? 0));
        var outer = BrepLoopBuilder.CreateKnownLoop(builder, outerLoop);
        if (!outer.IsSuccess)
        {
            return KernelResult<FaceId>.Failure(outer.Diagnostics);
        }

        loopIds.Add(outer.Value);
        if (innerLoops is not null)
        {
            foreach (var innerLoop in innerLoops)
            {
                if (innerLoop is null)
                {
                    return Failure("Known face contains a null inner-loop specification.");
                }

                var inner = BrepLoopBuilder.CreateKnownLoop(builder, innerLoop);
                if (!inner.IsSuccess)
                {
                    return KernelResult<FaceId>.Failure(inner.Diagnostics);
                }

                loopIds.Add(inner.Value);
            }
        }

        return KernelResult<FaceId>.Success(builder.AddFace(loopIds));
    }

    private static KernelResult<FaceId> Failure(string message)
        => KernelResult<FaceId>.Failure([
            new KernelDiagnostic(
                KernelDiagnosticCode.ValidationFailed,
                KernelDiagnosticSeverity.Error,
                message,
                "Brep.Surgery.FaceBuilder"),
        ]);
}
