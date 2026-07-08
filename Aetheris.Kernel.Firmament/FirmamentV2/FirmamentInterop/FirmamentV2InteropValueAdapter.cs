using Aetheris.Forge.Abstractions.FirmamentInterop;
using InteropSourceSpan = Aetheris.Forge.Abstractions.FirmamentInterop.FirmamentSourceSpan;

namespace Aetheris.Kernel.Firmament.FirmamentV2.FirmamentInterop;

internal static class FirmamentV2InteropValueAdapter
{
    public static FirmamentVariable AdaptVariable(string name, FirmamentV2BoundLet boundLet) =>
        new(name, AdaptValue(name, boundLet), AdaptSourceSpan(boundLet.SourceSpan));

    public static FirmamentValue AdaptValue(string name, FirmamentV2BoundLet boundLet) =>
        AdaptValue(name, boundLet.Value, boundLet.Tolerance, boundLet.SourceSpan);

    public static FirmamentValue AdaptValue(string name, FirmamentV2BoundExpression expression) =>
        AdaptValue(name, expression.Value, expression.AliasTolerance, expression.SourceSpan);

    private static FirmamentValue AdaptValue(string name, FirmamentV2LiteralValue literal, FirmamentV2Tolerance? tolerance, FirmamentV2SourceSpan sourceSpan)
    {
        return new FirmamentScalarValue(
            name,
            AdaptKind(literal.Type),
            literal.Value,
            AdaptNumericValue(literal),
            literal.Unit)
        {
            Tolerance = AdaptTolerance(tolerance),
            SourceSpan = AdaptSourceSpan(sourceSpan)
        };
    }

    public static FirmamentTolerance? AdaptTolerance(FirmamentV2Tolerance? tolerance) =>
        tolerance is null
            ? null
            : new(
                tolerance.Kind switch
                {
                    FirmamentV2ToleranceKind.Bilateral => FirmamentToleranceKind.Bilateral,
                    FirmamentV2ToleranceKind.Asymmetric => FirmamentToleranceKind.Asymmetric,
                    _ => FirmamentToleranceKind.None
                },
                tolerance.Plus,
                tolerance.Minus,
                tolerance.Unit,
                AdaptKind(tolerance.Type),
                AdaptSourceSpan(tolerance.SourceSpan));

    public static InteropSourceSpan? AdaptSourceSpan(FirmamentV2SourceSpan? sourceSpan) =>
        sourceSpan is null ? null : new InteropSourceSpan(sourceSpan.Start, sourceSpan.Length);

    public static FirmamentValueKind AdaptKind(FirmamentV2PrimitiveType primitiveType) =>
        primitiveType switch
        {
            FirmamentV2PrimitiveType.Int => FirmamentValueKind.Int,
            FirmamentV2PrimitiveType.Float => FirmamentValueKind.Float,
            FirmamentV2PrimitiveType.Length => FirmamentValueKind.Length,
            FirmamentV2PrimitiveType.Angle => FirmamentValueKind.Angle,
            FirmamentV2PrimitiveType.String => FirmamentValueKind.String,
            FirmamentV2PrimitiveType.Bool => FirmamentValueKind.Bool,
            _ => throw new ArgumentOutOfRangeException(nameof(primitiveType), primitiveType, "Unsupported Firmament V2 primitive type.")
        };

    private static double? AdaptNumericValue(FirmamentV2LiteralValue literal) =>
        literal.NumericValue ?? literal.Value switch
        {
            int value => value,
            long value => value,
            float value => value,
            double value => value,
            decimal value => (double)value,
            _ => null
        };
}
