using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

internal static class ConstructionAuthorityEvolution
{
    public static ConstructionState Append(
        BodyState input,
        string outputName,
        IConstructionOperation operation,
        BodyStateId outputStateId,
        GeometricDelta delta,
        IReadOnlyList<SculptValidationEvidence> evidence)
    {
        var authority = input.ConstructionAuthority ?? ConstructionState.FromHousing(input.Construction, input.BodyStableId + ".legacy-base");
        return authority.Append(ConstructionOperationState.Accepted(input, outputName, operation, outputStateId, delta, evidence));
    }
}

public sealed record ConstructionReplayStep(
    int Index,
    string OperationId,
    string OperationKind,
    BodyStateId AuthoredPredecessor,
    BodyStateId RealizedPredecessor,
    BodyStateId? RealizedOutput,
    bool Succeeded,
    IReadOnlyList<SculptDiagnostic> Diagnostics);

public sealed record ConstructionReplayResult(
    bool IsSuccess,
    BodyState? OutputState,
    BodyState? AuthoritativePredecessor,
    ConstructionOperationState? FailedOperation,
    IReadOnlyList<ConstructionReplayStep> Steps,
    IReadOnlyList<SculptDiagnostic> Diagnostics);

/// <summary>Correctness-baseline full replay of one linear typed construction program.</summary>
public static class ConstructionStateReplayer
{
    public static ConstructionReplayResult Replay(ConstructionState constructionState, string? outputName = null)
    {
        ArgumentNullException.ThrowIfNull(constructionState);
        if (!StringComparer.Ordinal.Equals(constructionState.SchemaId, ConstructionState.CurrentSchemaId)
            || constructionState.SchemaVersion != ConstructionState.CurrentSchemaVersion)
            return Failure(null, null, null, [], "bodystate-construction-version-unsupported",
                $"ConstructionState schema '{constructionState.SchemaId}' version {constructionState.SchemaVersion} is unsupported; expected {ConstructionState.CurrentSchemaId} v{ConstructionState.CurrentSchemaVersion}.");

        SculptResult created;
        switch (constructionState.Base)
        {
            case HousingBaseConstruction housing when housing.SchemaVersion == 1:
                var recipe = housing.Housing;
                created = SculptedHousingFactory.CreateBase(housing.BaseId, recipe.Width, recipe.Depth, recipe.BaseHeight, recipe.Holes);
                break;
            case HousingBaseConstruction housing:
                return Failure(null, null, null, [], "bodystate-base-version-unsupported",
                    $"BaseConstruction '{housing.BaseId}' payload version {housing.SchemaVersion} is unsupported.");
            default:
                return Failure(null, null, null, [], "bodystate-base-kind-unsupported",
                    $"BaseConstruction kind '{constructionState.Base.BaseKind}' is not admitted by this runtime.");
        }

        if (!created.IsSuccess || created.OutputState is null)
            return new(false, null, null, null, [], [new("bodystate-base-replay-failed",
                "BaseConstruction could not be realized: " + string.Join(" | ", created.Diagnostics.Select(item => $"{item.Code}:{item.Message}"))) ]);

        var current = created.OutputState with { ConstructionAuthority = constructionState with { Operations = [] } };
        var steps = new List<ConstructionReplayStep>();
        for (var index = 0; index < constructionState.Operations.Count; index++)
        {
            var authored = constructionState.Operations[index];
            if (index > 0 && authored.PredecessorStateId != constructionState.Operations[index - 1].OutputStateId)
                return OperationFailure(current, authored, index, steps, [new("bodystate-operation-order-invalid",
                    $"Operation '{authored.OperationId}' names predecessor {authored.PredecessorStateId.Value}, but the preceding authored operation outputs {constructionState.Operations[index - 1].OutputStateId.Value}.", authored.OperationId)]);
            if (authored.PayloadVersion != authored.Payload.SchemaVersion || authored.PayloadVersion != 1)
                return OperationFailure(current, authored, index, steps, [new("bodystate-operation-version-unsupported",
                    $"Operation '{authored.OperationId}' ({authored.OperationKind}) payload version {authored.PayloadVersion} is unsupported.", authored.OperationId)]);
            if (!StringComparer.Ordinal.Equals(authored.OperationId, authored.Payload.StableId)
                || !StringComparer.Ordinal.Equals(authored.OperationKind, authored.Payload.OperationKind))
                return OperationFailure(current, authored, index, steps, [new("bodystate-operation-identity-invalid",
                    $"Operation envelope '{authored.OperationId}'/{authored.OperationKind} does not match its typed payload '{authored.Payload.StableId}'/{authored.Payload.OperationKind}.", authored.OperationId)]);

            var nextName = index == constructionState.Operations.Count - 1 && !string.IsNullOrWhiteSpace(outputName)
                ? outputName! : authored.OutputAuthoredName;
            var realized = Apply(current, nextName, authored.Payload);
            if (!realized.IsSuccess || realized.OutputState is null)
                return OperationFailure(current, authored, index, steps, realized.Diagnostics);

            var output = realized.OutputState;
            var replayedOperations = output.ConstructionAuthority!.Operations.ToArray();
            replayedOperations[^1] = replayedOperations[^1] with { ReplayStatus = ConstructionReplayStatus.ReplayedAndValidated };
            current = output with { ConstructionAuthority = output.ConstructionAuthority with { Operations = replayedOperations } };
            steps.Add(new(index, authored.OperationId, authored.OperationKind, authored.PredecessorStateId,
                realized.Delta!.InputState, current.StateId, true, []));
        }

        return new(true, current, current, null, steps, []);
    }

    private static SculptResult Apply(BodyState input, string outputName, IConstructionOperation operation) => operation switch
    {
        OffsetRegionOperation offset => OffsetRegionSculptor.Apply(input, outputName, offset),
        ReplaceRegionOperation replace => ReplaceRegionSculptor.Apply(input, outputName, replace),
        SafeHoleOperation hole => SafeHoleSculptor.Apply(input, outputName, hole),
        BlendBoundaryOperation blend => Blend(BlendBoundarySculptor.Apply(input, outputName, blend)),
        AddSectionChainOperation add => AddSectionChainSculptor.Apply(input, outputName, add),
        RemoveSectionChainOperation remove => RemoveSectionChainSculptor.Apply(input, outputName, remove),
        _ => SculptResult.Failure([new("bodystate-operation-kind-unsupported",
            $"Typed construction operation '{operation.OperationKind}' is not admitted by this runtime.", operation.StableId)])
    };

    private static SculptResult Blend(BlendBoundaryResult result) => result.IsSuccess && result.OutputState is not null
        ? new(true, result.OutputState, result.Delta, result.OutputState.ValidationEvidence, result.Diagnostics)
        : SculptResult.Failure(result.Diagnostics);

    private static ConstructionReplayResult OperationFailure(BodyState current, ConstructionOperationState operation, int index,
        List<ConstructionReplayStep> steps, IReadOnlyList<SculptDiagnostic> cause)
    {
        var diagnostics = new List<SculptDiagnostic>
        {
            new("bodystate-operation-replay-failed",
                $"Replay failed atomically at operation {index} '{operation.OperationId}' ({operation.OperationKind}); predecessor {current.StateId.Value} remains authoritative.", operation.OperationId)
        };
        diagnostics.AddRange(cause);
        steps.Add(new(index, operation.OperationId, operation.OperationKind, operation.PredecessorStateId, current.StateId, null, false, cause));
        return new(false, null, current, operation, steps, diagnostics);
    }

    private static ConstructionReplayResult Failure(BodyState? output, BodyState? predecessor, ConstructionOperationState? operation,
        IReadOnlyList<ConstructionReplayStep> steps, string code, string message)
        => new(false, output, predecessor, operation, steps, [new(code, message)]);
}

public sealed record ConstructionStateSerializationResult(
    bool IsSuccess,
    ConstructionState? ConstructionState,
    string? Json,
    IReadOnlyList<SculptDiagnostic> Diagnostics);

public static class ConstructionStateSerializer
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new Direction3DConverter());
        return options;
    }

    public static ConstructionStateSerializationResult Serialize(ConstructionState state)
    {
        try { return new(true, state, JsonSerializer.Serialize(state, Options), []); }
        catch (NotSupportedException exception) { return Failure("bodystate-construction-serialization-failed", exception.Message); }
    }

    public static ConstructionStateSerializationResult Deserialize(string json)
    {
        try
        {
            var state = JsonSerializer.Deserialize<ConstructionState>(json, Options);
            return state is null ? Failure("bodystate-construction-deserialization-failed", "ConstructionState payload was empty.") : new(true, state, json, []);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return Failure("bodystate-construction-deserialization-failed", exception.Message);
        }
    }

    private static ConstructionStateSerializationResult Failure(string code, string message) => new(false, null, null, [new(code, message)]);

    private sealed class Direction3DConverter : JsonConverter<Direction3D>
    {
        public override Direction3D Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader); var root = document.RootElement;
            return Direction3D.Create(new(root.GetProperty("x").GetDouble(), root.GetProperty("y").GetDouble(), root.GetProperty("z").GetDouble()));
        }

        public override void Write(Utf8JsonWriter writer, Direction3D value, JsonSerializerOptions options)
        {
            writer.WriteStartObject(); writer.WriteNumber("x", value.X); writer.WriteNumber("y", value.Y); writer.WriteNumber("z", value.Z); writer.WriteEndObject();
        }
    }
}
