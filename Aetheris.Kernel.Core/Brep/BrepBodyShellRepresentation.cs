using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep;

/// <summary>
/// Explicit shell role partition for a single solid body: one outer shell and zero or more inner void shells.
/// </summary>
public sealed record BrepBodyShellRepresentation(ShellId OuterShellId, IReadOnlyList<ShellId> InnerShellIds)
{
    public IReadOnlyList<ShellId> OrderedShellIds => [OuterShellId, .. InnerShellIds];
}
