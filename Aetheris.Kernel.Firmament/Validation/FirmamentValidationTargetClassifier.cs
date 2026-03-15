namespace Aetheris.Kernel.Firmament.Validation;

internal static class FirmamentValidationTargetClassifier
{
    public static bool TryClassify(string rawTarget, out FirmamentValidationTargetShape targetShape)
    {
        targetShape = default;

        if (string.IsNullOrWhiteSpace(rawTarget))
        {
            return false;
        }

        var trimmed = rawTarget.Trim();
        if (trimmed.Contains(' ', StringComparison.Ordinal)
            || trimmed.Contains('\t', StringComparison.Ordinal)
            || trimmed.Contains('\n', StringComparison.Ordinal)
            || trimmed.Contains('\r', StringComparison.Ordinal))
        {
            return false;
        }

        var dotCount = 0;
        foreach (var character in trimmed)
        {
            if (character == '.')
            {
                dotCount++;
            }
        }

        if (dotCount == 0)
        {
            if (!IsValidIdentifier(trimmed))
            {
                return false;
            }

            targetShape = FirmamentValidationTargetShape.FeatureId;
            return true;
        }

        if (dotCount != 1)
        {
            return false;
        }

        var separatorIndex = trimmed.IndexOf('.', StringComparison.Ordinal);
        var left = trimmed[..separatorIndex];
        var right = trimmed[(separatorIndex + 1)..];

        if (!IsValidIdentifier(left) || !IsValidIdentifier(right))
        {
            return false;
        }

        targetShape = FirmamentValidationTargetShape.SelectorShaped;
        return true;
    }

    private static bool IsValidIdentifier(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        if (!IsIdentifierStart(value[0]))
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            if (!IsIdentifierContinue(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsIdentifierStart(char character) =>
        (character is >= 'a' and <= 'z')
        || (character is >= 'A' and <= 'Z')
        || character == '_';

    private static bool IsIdentifierContinue(char character) =>
        IsIdentifierStart(character)
        || (character is >= '0' and <= '9');
}
