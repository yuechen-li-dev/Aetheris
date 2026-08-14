using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Surgery;

internal readonly record struct BrepShellAssembly(ShellId ShellId, BodyId BodyId);

/// <summary>
/// Assembles one known face set into one shell/body pair. The caller owns face
/// selection; Surgery checks ownership and closed two-use edge incidence.
/// </summary>
internal static class BrepShellAssembler
{
    public static KernelResult<BrepShellAssembly> CreateClosedBody(
        TopologyBuilder builder,
        IReadOnlyList<FaceId> faceIds)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(faceIds);

        if (faceIds.Count == 0)
        {
            return Failure("Closed shell assembly requires at least one caller-selected face.");
        }

        var seenFaces = new HashSet<FaceId>();
        var edgeUseCounts = new Dictionary<EdgeId, int>();
        foreach (var faceId in faceIds)
        {
            if (!seenFaces.Add(faceId))
            {
                return Failure($"Closed shell assembly repeats face {faceId.Value}.");
            }

            if (!builder.Model.TryGetFace(faceId, out var face) || face is null)
            {
                return Failure($"Closed shell assembly references missing face {faceId.Value}.");
            }

            foreach (var loopId in face.LoopIds)
            {
                if (!builder.Model.TryGetLoop(loopId, out var loop) || loop is null)
                {
                    return Failure($"Face {faceId.Value} references missing loop {loopId.Value}.");
                }

                foreach (var coedgeId in loop.CoedgeIds)
                {
                    if (!builder.Model.TryGetCoedge(coedgeId, out var coedge) || coedge is null)
                    {
                        return Failure($"Loop {loopId.Value} references missing coedge {coedgeId.Value}.");
                    }

                    edgeUseCounts[coedge.EdgeId] = edgeUseCounts.GetValueOrDefault(coedge.EdgeId) + 1;
                }
            }
        }

        var invalidIncidence = edgeUseCounts.FirstOrDefault(pair => pair.Value != 2);
        if (!invalidIncidence.Equals(default(KeyValuePair<EdgeId, int>)))
        {
            return Failure(
                $"Closed shell edge {invalidIncidence.Key.Value} has {invalidIncidence.Value} face-boundary uses; expected exactly 2.");
        }

        var shellId = builder.AddShell(faceIds);
        var bodyId = builder.AddBody([shellId]);
        return KernelResult<BrepShellAssembly>.Success(new BrepShellAssembly(shellId, bodyId));
    }

    private static KernelResult<BrepShellAssembly> Failure(string message)
        => KernelResult<BrepShellAssembly>.Failure([
            new KernelDiagnostic(
                KernelDiagnosticCode.ValidationFailed,
                KernelDiagnosticSeverity.Error,
                message,
                "Brep.Surgery.ShellAssembler"),
        ]);
}
