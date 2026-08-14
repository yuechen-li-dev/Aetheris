using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Surgery;

/// <summary>
/// An explicitly oriented use of a caller-selected topology edge.
/// Surgery never discovers the edge or its orientation.
/// </summary>
internal readonly record struct BrepEdgeUse(EdgeId EdgeId, bool IsReversed)
{
    public static BrepEdgeUse Forward(EdgeId edgeId) => new(edgeId, IsReversed: false);

    public static BrepEdgeUse Reversed(EdgeId edgeId) => new(edgeId, IsReversed: true);
}
