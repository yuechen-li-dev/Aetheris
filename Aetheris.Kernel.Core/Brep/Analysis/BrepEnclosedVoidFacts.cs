using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Analysis;

/// <summary>
/// Deterministic enclosed-void facts for a built B-rep body.
/// </summary>
public sealed record BrepEnclosedVoidFacts(
    bool HasEnclosedVoids,
    int EnclosedVoidCount,
    IReadOnlyList<ShellId> EnclosedVoidShellIds);

public static class BrepEnclosedVoidAnalyzer
{
    public static BrepEnclosedVoidFacts Analyze(BrepBody body)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (body.ShellRepresentation is null)
        {
            return new BrepEnclosedVoidFacts(false, 0, []);
        }

        var enclosedShellIds = body.ShellRepresentation.InnerShellIds
            .Where(shellId => body.Topology.TryGetShell(shellId, out _))
            .Distinct()
            .OrderBy(shellId => shellId.Value)
            .ToArray();

        return new BrepEnclosedVoidFacts(
            enclosedShellIds.Length > 0,
            enclosedShellIds.Length,
            enclosedShellIds);
    }
}
