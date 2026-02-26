namespace Aetheris.Kernel.Core.Brep.Queries;

public readonly record struct RayQueryOptions(
    double? MaxDistance = null,
    bool IncludeBackfaces = true);
