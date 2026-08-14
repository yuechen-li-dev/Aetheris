using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Surgery;

/// <summary>
/// Realizes a loop from an already-known ordered edge-use cycle.
/// It validates the supplied cycle but does not infer ordering or feature intent.
/// </summary>
internal static class BrepLoopBuilder
{
    public static KernelResult<LoopId> CreateKnownLoop(
        TopologyBuilder builder,
        IReadOnlyList<BrepEdgeUse> orderedEdgeUses)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(orderedEdgeUses);

        var validation = ValidateUses(builder, orderedEdgeUses, requireEndpointClosure: true);
        if (!validation.IsSuccess)
        {
            return KernelResult<LoopId>.Failure(validation.Diagnostics);
        }

        return CreateCycle(builder, orderedEdgeUses);
    }

    /// <summary>
    /// Compatibility-only realization for canonical recipes whose historical
    /// coedge sense predates DirectedEdgeUse endpoint closure. New callers must
    /// use <see cref="CreateKnownLoop"/>.
    /// </summary>
    internal static KernelResult<LoopId> CreateKnownLoopPreservingLegacySense(
        TopologyBuilder builder,
        IReadOnlyList<BrepEdgeUse> orderedEdgeUses)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(orderedEdgeUses);
        var validation = ValidateUses(builder, orderedEdgeUses, requireEndpointClosure: false);
        return validation.IsSuccess
            ? CreateCycle(builder, orderedEdgeUses)
            : KernelResult<LoopId>.Failure(validation.Diagnostics);
    }

    private static KernelResult<bool> ValidateUses(
        TopologyBuilder builder,
        IReadOnlyList<BrepEdgeUse> orderedEdgeUses,
        bool requireEndpointClosure)
    {
        if (orderedEdgeUses.Count == 0)
        {
            return ValidationFailure("A known loop requires at least one oriented edge use.");
        }

        var directedUses = new DirectedEdgeUse[orderedEdgeUses.Count];
        var seenUses = new HashSet<BrepEdgeUse>();
        for (var index = 0; index < orderedEdgeUses.Count; index++)
        {
            var use = orderedEdgeUses[index];
            if (!builder.Model.TryGetEdge(use.EdgeId, out var edge) || edge is null)
            {
                return ValidationFailure($"Known loop edge use {index} references missing edge {use.EdgeId.Value}.");
            }

            // Periodic faces legitimately use the same seam once in each sense.
            // Repeating the identical directed use, however, cannot describe one
            // simple caller-authorized boundary cycle.
            if (!seenUses.Add(use))
            {
                return ValidationFailure($"Known loop repeats edge {use.EdgeId.Value} with the same orientation.");
            }

            directedUses[index] = DirectedEdgeUse.Resolve(edge, use.IsReversed);
        }

        if (requireEndpointClosure)
        {
            for (var index = 0; index < directedUses.Length; index++)
            {
                var next = (index + 1) % directedUses.Length;
                if (directedUses[index].EndVertexId != directedUses[next].StartVertexId)
                {
                    return ValidationFailure(
                        $"Known loop is open between edge uses {index} and {next}: " +
                        $"vertex {directedUses[index].EndVertexId.Value} does not meet vertex {directedUses[next].StartVertexId.Value}.");
                }
            }
        }

        return KernelResult<bool>.Success(true);
    }

    private static KernelResult<LoopId> CreateCycle(
        TopologyBuilder builder,
        IReadOnlyList<BrepEdgeUse> orderedEdgeUses)
    {
        var loopId = builder.AllocateLoopId();
        var coedgeIds = new CoedgeId[orderedEdgeUses.Count];
        for (var index = 0; index < coedgeIds.Length; index++)
        {
            coedgeIds[index] = builder.AllocateCoedgeId();
        }

        for (var index = 0; index < orderedEdgeUses.Count; index++)
        {
            builder.AddCoedge(new Coedge(
                coedgeIds[index],
                orderedEdgeUses[index].EdgeId,
                loopId,
                coedgeIds[(index + 1) % coedgeIds.Length],
                coedgeIds[(index + coedgeIds.Length - 1) % coedgeIds.Length],
                orderedEdgeUses[index].IsReversed));
        }

        builder.AddLoop(new Loop(loopId, coedgeIds));
        return KernelResult<LoopId>.Success(loopId);
    }

    private static KernelResult<LoopId> Failure(string message)
        => KernelResult<LoopId>.Failure([
            new KernelDiagnostic(
                KernelDiagnosticCode.ValidationFailed,
                KernelDiagnosticSeverity.Error,
                message,
                "Brep.Surgery.LoopBuilder"),
        ]);

    private static KernelResult<bool> ValidationFailure(string message)
        => KernelResult<bool>.Failure(Failure(message).Diagnostics);
}
