namespace Aetheris.Forge.Abstractions.FirmamentInterop;

public interface IFirmamentVariables
{
    bool TryGet(string name, out FirmamentValue value);
    FirmamentValue GetRequired(string name);
    IReadOnlyList<FirmamentVariable> All { get; }
}

public sealed record FirmamentVariable(
    string Name,
    FirmamentValue Value,
    FirmamentSourceSpan? SourceSpan);

public abstract record FirmamentValue(string Name, FirmamentValueKind Kind)
{
    public FirmamentTolerance? Tolerance { get; init; }
    public FirmamentSourceSpan? SourceSpan { get; init; }
}

public enum FirmamentValueKind
{
    Int,
    Float,
    Length,
    Angle,
    String,
    Bool
}

public sealed record FirmamentScalarValue(
    string Name,
    FirmamentValueKind Kind,
    object? Nominal,
    double? NumericValue,
    string? Unit) : FirmamentValue(Name, Kind);

public sealed record FirmamentTolerance(
    FirmamentToleranceKind Kind,
    double Plus,
    double Minus,
    string? Unit,
    FirmamentValueKind ValueKind,
    FirmamentSourceSpan? SourceSpan);

public enum FirmamentToleranceKind
{
    None,
    Bilateral,
    Asymmetric
}

public sealed record FirmamentSourceSpan(int Start, int Length);
