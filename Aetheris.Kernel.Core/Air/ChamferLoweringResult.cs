namespace Aetheris.Kernel.Core.Air;

internal enum ChamferLoweringErrorKind
{
    InvalidAuthoredInput,
    UnsupportedSurfacePair,
    UnsupportedSelection,
    AmbiguousMaterialSide,
    DistanceTooLarge,
    OpenEdgeChain,
    NonManifoldSelection,
    CornerPolicyRequired,
    MissingConstructionWitness,
    MultipleValidCornerPatches,
    SelfIntersection,
    DegenerateTransition,
    UnsupportedHistory,
    UnsupportedJunction,
    BackendMaterializationDefect,
    VerificationFailure,
}

internal sealed record ChamferLoweringError(
    ChamferLoweringErrorKind Kind,
    string Code,
    string Message,
    string Stage,
    IReadOnlyList<string>? Evidence = null);

/// <summary>
/// Typed boundary between semantic chamfer intent and exact Construction AIR.
/// A failed lowering never carries a construction value and must not enter topology emission.
/// </summary>
internal sealed record ChamferLoweringResult<T>(T? Value, ChamferLoweringError? Error) where T : class
{
    public bool IsSuccess => Value is not null && Error is null;

    public static ChamferLoweringResult<T> Ok(T value) => new(value, null);

    public static ChamferLoweringResult<T> Err(ChamferLoweringError error) => new(null, error);
}
