using System.Collections.Immutable;
using System.Numerics;

namespace Aetheris.Cloth3D;

/// <summary>Bounded barycentric surface contacts. These include edge midpoints and
/// triangle interiors, but remain discrete samples rather than a CCD/intersection certificate.</summary>
public static class ClothBodyContact3D
{
    public static ImmutableArray<Vector3> Samples { get; } =
    [
        new(.5f, .5f, 0), new(0, .5f, .5f), new(.5f, 0, .5f),
        new(1f / 3, 1f / 3, 1f / 3),
        new(2f / 3, 1f / 6, 1f / 6), new(1f / 6, 2f / 3, 1f / 6), new(1f / 6, 1f / 6, 2f / 3),
    ];
}
