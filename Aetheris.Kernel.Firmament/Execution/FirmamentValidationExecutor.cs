using System.Globalization;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Firmament.ParsedModel;
using Aetheris.Kernel.Firmament.Validation;

namespace Aetheris.Kernel.Firmament.Execution;

internal static class FirmamentValidationExecutor
{
    public static KernelResult<FirmamentValidationExecutionResult> Execute(
        FirmamentParsedDocument parsedDocument,
        FirmamentPrimitiveExecutionResult primitiveExecutionResult)
    {
        ArgumentNullException.ThrowIfNull(parsedDocument);
        ArgumentNullException.ThrowIfNull(primitiveExecutionResult);

        var validations = new List<FirmamentExecutedValidation>();
        var diagnostics = new List<KernelDiagnostic>();
        var featureBodies = BuildFeatureBodyMap(primitiveExecutionResult);

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
                FirmamentKnownOpKind.ExpectExists => ExecuteExpectExists(index, entry, target, featureBodies, diagnostics),
                FirmamentKnownOpKind.ExpectSelectable => ExecuteExpectSelectable(index, entry, target, featureBodies, diagnostics),
                FirmamentKnownOpKind.ExpectManifold => ExecuteExpectManifold(index, entry, target, featureBodies, diagnostics),
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
            new FirmamentValidationExecutionResult(validations),
            diagnostics);
    }

    private static FirmamentExecutedValidation ExecuteExpectExists(
        int opIndex,
        FirmamentParsedOpEntry entry,
        string? target,
        IReadOnlyDictionary<string, Aetheris.Kernel.Core.Brep.BrepBody> featureBodies,
        ICollection<KernelDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return new FirmamentExecutedValidation(
                OpIndex: opIndex,
                Kind: entry.KnownKind,
                Target: target,
                IsExecuted: true,
                IsSuccess: false,
                Reason: "expect_exists target is not executable at M6a.");
        }

        var isSelectorShaped = entry.ClassifiedFields is not null
            && entry.ClassifiedFields.TryGetValue("targetShape", out var targetShape)
            && string.Equals(targetShape, FirmamentValidationTargetShape.SelectorShaped.ToString(), StringComparison.Ordinal);

        if (!isSelectorShaped)
        {
            var isSuccess = IsFeatureIdTarget(entry);
            return new FirmamentExecutedValidation(
                OpIndex: opIndex,
                Kind: entry.KnownKind,
                Target: target,
                IsExecuted: true,
                IsSuccess: isSuccess,
                Reason: isSuccess ? null : "expect_exists target is not executable at M6a.");
        }

        if (entry.ClassifiedFields is null
            || !entry.ClassifiedFields.TryGetValue("selectorResultKind", out var selectorResultKind)
            || !Enum.TryParse<FirmamentSelectorResultKind>(selectorResultKind, out var resultKind)
            || !FirmamentSelectorResolver.TryResolve(target, featureBodies, resultKind, out var resolution))
        {
            return new FirmamentExecutedValidation(
                OpIndex: opIndex,
                Kind: entry.KnownKind,
                Target: target,
                IsExecuted: true,
                IsSuccess: false,
                Reason: "expect_exists selector target could not be resolved to executable topology at M6b.");
        }

        var success = resolution.Count > 0;
        if (!success)
        {
            diagnostics.Add(CreateDiagnostic(
                FirmamentDiagnosticCodes.ValidationTargetSelectorResolvedEmpty,
                $"Selector '{target}' resolved to no topology elements."));
        }

        return new FirmamentExecutedValidation(
            OpIndex: opIndex,
            Kind: entry.KnownKind,
            Target: target,
            IsExecuted: true,
            IsSuccess: success,
            Reason: success
                ? null
                : $"Selector '{target}' resolved to no topology elements.");
    }

    private static FirmamentExecutedValidation ExecuteExpectSelectable(
        int opIndex,
        FirmamentParsedOpEntry entry,
        string? target,
        IReadOnlyDictionary<string, Aetheris.Kernel.Core.Brep.BrepBody> featureBodies,
        ICollection<KernelDiagnostic> diagnostics)
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
            || !entry.ClassifiedFields.TryGetValue("selectorResultKind", out var selectorResultKind)
            || !Enum.TryParse<FirmamentSelectorResultKind>(selectorResultKind, out var resultKind)
            || !entry.RawFields.TryGetValue("count", out var countRaw)
            || !int.TryParse(countRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var requestedCount)
            || string.IsNullOrWhiteSpace(target)
            || !FirmamentSelectorResolver.TryResolve(target, featureBodies, resultKind, out var resolution))
        {
            return new FirmamentExecutedValidation(
                OpIndex: opIndex,
                Kind: entry.KnownKind,
                Target: target,
                IsExecuted: true,
                IsSuccess: false,
                Reason: "expect_selectable selector target could not be resolved to executable topology at M6c.");
        }

        var isSuccess = resolution.Count == requestedCount;
        if (!isSuccess)
        {
            diagnostics.Add(CreateDiagnostic(
                FirmamentDiagnosticCodes.ValidationTargetSelectableCountMismatch,
                $"Selector '{target}' resolved to {resolution.Count.ToString(CultureInfo.InvariantCulture)} elements but {requestedCount.ToString(CultureInfo.InvariantCulture)} were expected."));
        }

        return new FirmamentExecutedValidation(
            OpIndex: opIndex,
            Kind: entry.KnownKind,
            Target: target,
            IsExecuted: true,
            IsSuccess: isSuccess,
            Reason: isSuccess
                ? null
                : $"Selector '{target}' resolved to {resolution.Count.ToString(CultureInfo.InvariantCulture)} elements but {requestedCount.ToString(CultureInfo.InvariantCulture)} were expected.");
    }


    private static FirmamentExecutedValidation ExecuteExpectManifold(
        int opIndex,
        FirmamentParsedOpEntry entry,
        string? target,
        IReadOnlyDictionary<string, Aetheris.Kernel.Core.Brep.BrepBody> featureBodies,
        ICollection<KernelDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return new FirmamentExecutedValidation(
                OpIndex: opIndex,
                Kind: entry.KnownKind,
                Target: target,
                IsExecuted: true,
                IsSuccess: false,
                Reason: "expect_manifold target is not executable.");
        }

        if (!IsFeatureIdTarget(entry))
        {
            return new FirmamentExecutedValidation(
                OpIndex: opIndex,
                Kind: entry.KnownKind,
                Target: target,
                IsExecuted: false,
                IsSuccess: false,
                Reason: "expect_manifold does not support selector-shaped targets at M6d.");
        }

        if (!featureBodies.TryGetValue(target, out var body))
        {
            return new FirmamentExecutedValidation(
                OpIndex: opIndex,
                Kind: entry.KnownKind,
                Target: target,
                IsExecuted: true,
                IsSuccess: false,
                Reason: $"Feature '{target}' does not have executable geometry for expect_manifold.");
        }

        var isSuccess = FirmamentManifoldChecker.IsManifold(body);
        if (!isSuccess)
        {
            diagnostics.Add(CreateDiagnostic(
                FirmamentDiagnosticCodes.ValidationTargetNonManifoldBody,
                $"Feature '{target}' produced non-manifold geometry."));
        }

        return new FirmamentExecutedValidation(
            OpIndex: opIndex,
            Kind: entry.KnownKind,
            Target: target,
            IsExecuted: true,
            IsSuccess: isSuccess,
            Reason: isSuccess ? null : $"Feature '{target}' produced non-manifold geometry.");
    }

    private static IReadOnlyDictionary<string, Aetheris.Kernel.Core.Brep.BrepBody> BuildFeatureBodyMap(FirmamentPrimitiveExecutionResult primitiveExecutionResult)
    {
        var map = new Dictionary<string, Aetheris.Kernel.Core.Brep.BrepBody>(StringComparer.Ordinal);

        foreach (var primitive in primitiveExecutionResult.ExecutedPrimitives)
        {
            map[primitive.FeatureId] = primitive.Body;
        }

        foreach (var boolean in primitiveExecutionResult.ExecutedBooleans)
        {
            map[boolean.FeatureId] = boolean.Body;
        }

        return map;
    }

    private static bool IsFeatureIdTarget(FirmamentParsedOpEntry entry) =>
        entry.ClassifiedFields is not null
        && entry.ClassifiedFields.TryGetValue("targetShape", out var targetShape)
        && string.Equals(targetShape, FirmamentValidationTargetShape.FeatureId.ToString(), StringComparison.Ordinal);

    private static KernelDiagnostic CreateDiagnostic(FirmamentDiagnosticCode code, string message) =>
        new(
            KernelDiagnosticCode.ValidationFailed,
            KernelDiagnosticSeverity.Warning,
            $"[{code.Value}] {message}",
            FirmamentDiagnosticConventions.Source);
}
