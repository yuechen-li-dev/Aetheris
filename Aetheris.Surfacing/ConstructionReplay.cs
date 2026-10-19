using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Geometry.Surfaces;

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
        var authority = input.ConstructionAuthority ?? ConstructionState.FromHousing(input.Construction ?? throw new InvalidOperationException("A non-housing state requires explicit construction authority."), input.BodyStableId + ".legacy-base");
        return authority.Append(ConstructionOperationState.Accepted(input, outputName, operation, outputStateId, delta, evidence));
    }
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
        options.Converters.Add(new SurfacePatchConverter());
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

    private sealed class SurfacePatchConverter : JsonConverter<BoundedSurfacePatch>
    {
        public override BoundedSurfacePatch Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            try
            {
                // Earlier writers emitted the effective support without a patch
                // discriminator. Recover that actual spline data, not a delegate.
                var splineJson = root.TryGetProperty("spline", out var explicitSpline) ? explicitSpline
                    : root.GetProperty("support").GetProperty("bSplineSurfaceWithKnots");
                if (root.TryGetProperty("$patchKind", out var kind) && kind.GetString() != "BSpline")
                    throw new JsonException("Unsupported persisted patch kind.");
                var spline = splineJson.Deserialize<BSplineSurfaceWithKnots>(options) ?? throw new JsonException("Missing spline support.");
                return new BSplineSurfacePatch(root.GetProperty("patchId").GetString() ?? throw new JsonException("Missing patch identity."),
                    spline, root.GetProperty("parameterDomain").Deserialize<SurfaceParameterDomain>(options),
                    root.GetProperty("boundaryLoop").Deserialize<SurfaceBoundaryLoop>(options) ?? throw new JsonException("Missing boundary correspondence."),
                    root.GetProperty("reversedOrientation").GetBoolean(), root.GetProperty("surfaceClass").Deserialize<SurfacePatchClass>(options));
            }
            catch (Exception exception) when (exception is KeyNotFoundException or InvalidOperationException or ArgumentException)
            { throw new JsonException("Invalid persisted surface-patch construction: " + exception.Message, exception); }
        }

        public override void Write(Utf8JsonWriter writer, BoundedSurfacePatch value, JsonSerializerOptions options)
        {
            if (value is not BSplineSurfacePatch patch)
                throw new NotSupportedException("Persisted patch construction requires typed BSpline support; an opaque analytic evaluator is not replay authority.");
            writer.WriteStartObject();
            writer.WriteString("$patchKind", "BSpline"); writer.WriteString("patchId", patch.PatchId);
            writer.WritePropertyName("spline"); JsonSerializer.Serialize(writer, patch.Spline, options);
            writer.WritePropertyName("parameterDomain"); JsonSerializer.Serialize(writer, patch.ParameterDomain, options);
            writer.WritePropertyName("boundaryLoop"); JsonSerializer.Serialize(writer, patch.BoundaryLoop, options);
            writer.WriteBoolean("reversedOrientation", patch.ReversedOrientation);
            writer.WritePropertyName("surfaceClass"); JsonSerializer.Serialize(writer, patch.SurfaceClass, options);
            writer.WriteEndObject();
        }
    }

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
