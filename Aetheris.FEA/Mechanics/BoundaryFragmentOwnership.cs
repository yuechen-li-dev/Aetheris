using Aetheris.Continuum.Lattice;

namespace Aetheris.FEA.Mechanics;

/// <summary>
/// Half-open ownership for exact fragments on lattice planes. The adjacent cell with the
/// lexicographically smaller (K,J,I) index owns an interior-aligned fragment; exterior
/// fragments are owned by their sole material-side cell. This rule does not depend on normal orientation.
/// </summary>
public static class BoundaryFragmentOwnership
{
    public static CellIndex Own(CellIndex first, CellIndex? second)
    {
        if (second is null) return first;
        var other = second.Value;
        return Compare(first, other) <= 0 ? first : other;
    }

    public static bool IsOwner(CellIndex candidate, CellIndex first, CellIndex? second) => candidate == Own(first, second);

    private static int Compare(CellIndex left, CellIndex right)
    {
        var k = left.K.CompareTo(right.K); if (k != 0) return k;
        var j = left.J.CompareTo(right.J); if (j != 0) return j;
        return left.I.CompareTo(right.I);
    }
}
