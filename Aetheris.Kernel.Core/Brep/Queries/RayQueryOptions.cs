namespace Aetheris.Kernel.Core.Brep.Queries;

public readonly record struct RayQueryOptions(
    double? MaxDistance = null,
    bool IncludeBackfaces = true)
{
    public static RayQueryOptions Default => new(null, IncludeBackfaces: true);
}
