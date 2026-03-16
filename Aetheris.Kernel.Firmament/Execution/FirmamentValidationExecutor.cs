using System.Globalization;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.ParsedModel;
using Aetheris.Kernel.Firmament.Validation;

namespace Aetheris.Kernel.Firmament.Execution;

internal static class FirmamentValidationExecutor
{
    public static KernelResult<FirmamentValidationExecutionResult> Execute(FirmamentParsedDocument parsedDocument)
    {
        ArgumentNullException.ThrowIfNull(parsedDocument);

        var validations = new List<FirmamentExecutedValidation>();

        for (var index = 0; index < parsedDocument.Ops.Entries.Count; index++)
        {
            var entry = parsedDocument.Ops.Entries[index];
            if (entry.Family != FirmamentOpFamily.Validation)
            {
                continue;
            }

            var target = entry.RawFields.TryGetValue("target", out var targetRaw) ? targetRaw : null;
            var result = entry.KnownKind switch
            {
                FirmamentKnownOpKind.ExpectExists => ExecuteExpectExists(index, entry, target),
                FirmamentKnownOpKind.ExpectSelectable => ExecuteExpectSelectable(index, entry, target),
                FirmamentKnownOpKind.ExpectManifold => new FirmamentExecutedValidation(
                    OpIndex: index,
                    Kind: entry.KnownKind,
                    Target: target,
                    IsExecuted: false,
                    IsSuccess: false,
                    Reason: "expect_manifold is unsupported at M6a contract-level validation execution."),
                _ => new FirmamentExecutedValidation(
                    OpIndex: index,
                    Kind: entry.KnownKind,
                    Target: target,
                    IsExecuted: false,
                    IsSuccess: false,
                    Reason: $"Validation op '{entry.OpName}' is not executable.")
            };

            validations.Add(result);
        }

        return KernelResult<FirmamentValidationExecutionResult>.Success(
            new FirmamentValidationExecutionResult(validations));
    }

    private static FirmamentExecutedValidation ExecuteExpectExists(int opIndex, FirmamentParsedOpEntry entry, string? target)
    {
        var isSelectorShaped = entry.ClassifiedFields is not null
            && entry.ClassifiedFields.TryGetValue("targetShape", out var targetShape)
            && string.Equals(targetShape, FirmamentValidationTargetShape.SelectorShaped.ToString(), StringComparison.Ordinal);

        var isSuccess = !string.IsNullOrWhiteSpace(target)
            && (isSelectorShaped || IsFeatureIdTarget(entry));

        return new FirmamentExecutedValidation(
            OpIndex: opIndex,
            Kind: entry.KnownKind,
            Target: target,
            IsExecuted: true,
            IsSuccess: isSuccess,
            Reason: isSuccess ? null : "expect_exists target is not executable at M6a.");
    }

    private static FirmamentExecutedValidation ExecuteExpectSelectable(int opIndex, FirmamentParsedOpEntry entry, string? target)
    {
        if (IsFeatureIdTarget(entry))
        {
            return new FirmamentExecutedValidation(
                OpIndex: opIndex,
                Kind: entry.KnownKind,
                Target: target,
                IsExecuted: false,
                IsSuccess: false,
                Reason: "expect_selectable does not support bare feature-id targets at M6a.");
        }

        if (entry.ClassifiedFields is null
            || !entry.ClassifiedFields.TryGetValue("selectorCardinality", out var selectorCardinality)
            || !entry.RawFields.TryGetValue("count", out var countRaw)
            || !int.TryParse(countRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var requestedCount))
        {
            return new FirmamentExecutedValidation(
                OpIndex: opIndex,
                Kind: entry.KnownKind,
                Target: target,
                IsExecuted: true,
                IsSuccess: false,
                Reason: "expect_selectable missing executable contract-cardinality inputs.");
        }

        var isSuccess = string.Equals(selectorCardinality, FirmamentSelectorCardinality.One.ToString(), StringComparison.Ordinal)
            ? requestedCount == 1
            : string.Equals(selectorCardinality, FirmamentSelectorCardinality.Many.ToString(), StringComparison.Ordinal)
                && requestedCount > 1;

        return new FirmamentExecutedValidation(
            OpIndex: opIndex,
            Kind: entry.KnownKind,
            Target: target,
            IsExecuted: true,
            IsSuccess: isSuccess,
            Reason: isSuccess
                ? null
                : $"expect_selectable count '{requestedCount}' is incompatible with selector cardinality '{selectorCardinality}' at M6a contract level.");
    }

    private static bool IsFeatureIdTarget(FirmamentParsedOpEntry entry) =>
        entry.ClassifiedFields is not null
        && entry.ClassifiedFields.TryGetValue("targetShape", out var targetShape)
        && string.Equals(targetShape, FirmamentValidationTargetShape.FeatureId.ToString(), StringComparison.Ordinal);
}
