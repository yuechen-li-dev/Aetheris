using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Core.Brep.Boolean;

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

        if (IsCylinderRootThroughHoleArtifact(body, enclosedShellIds))
        {
            return new BrepEnclosedVoidFacts(false, 0, []);
        }

        return new BrepEnclosedVoidFacts(
            enclosedShellIds.Length > 0,
            enclosedShellIds.Length,
            enclosedShellIds);
    }

    private static bool IsCylinderRootThroughHoleArtifact(BrepBody body, IReadOnlyList<ShellId> enclosedShellIds)
    {
        if (enclosedShellIds.Count == 0
            || body.SafeBooleanComposition is not { } composition
            || composition.RootDescriptor.Kind != SafeBooleanRootKind.Cylinder)
        {
            return false;
        }

        if (composition.Holes.Count != enclosedShellIds.Count)
        {
            return false;
        }

        return composition.Holes.Count > 0
            && composition.Holes.All(hole => hole.SpanKind == SupportedBooleanHoleSpanKind.Through);
    }
}
