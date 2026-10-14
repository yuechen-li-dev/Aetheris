using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Web.Runtime;

/// <summary>
/// BUILD-PERF-M3: state of one bridge-mode response. Numeric arrays of at least
/// <see cref="BinaryThreshold"/> elements are written to one little-endian binary buffer,
/// 8-byte aligned, and replaced in the JSON by <c>{"$binary":"f64"|"i32","byteOffset":o,"length":n}</c>.
/// Mesh definitions in <see cref="Retained"/> (the very objects the session's previous response
/// delivered) are written as <c>{"id","identity","retained":true}</c>; the SDK substitutes the
/// definition it already holds. Values are bit-identical to the JSON path.
/// </summary>
internal sealed class WebResponseBridge(IReadOnlySet<object>? retained)
{
    internal const int BinaryThreshold = 64;
    [ThreadStatic] internal static WebResponseBridge? Current;

    private readonly MemoryStream binary = new();
    internal IReadOnlySet<object>? Retained { get; } = retained;

    internal long Append(ReadOnlySpan<byte> bytes)
    {
        while (binary.Length % 8 != 0) binary.WriteByte(0);
        var offset = binary.Length;
        binary.Write(bytes);
        return offset;
    }

    internal byte[] ToArray() => binary.Length == 0 ? [] : binary.ToArray();

    internal static JsonSerializerOptions Options(JsonSerializerOptions baseline)
    {
        var arrays = new JsonSerializerOptions(baseline);
        arrays.Converters.Add(new DoubleArrayConverter());
        arrays.Converters.Add(new Int32ArrayConverter());
        var bridged = new JsonSerializerOptions(arrays);
        bridged.Converters.Add(new RetainedDefinitionConverter(arrays));
        return bridged;
    }

    private static void WriteBinary(Utf8JsonWriter writer, string type, long offset, int length)
    {
        writer.WriteStartObject();
        writer.WriteString("$binary", type);
        writer.WriteNumber("byteOffset", offset);
        writer.WriteNumber("length", length);
        writer.WriteEndObject();
    }

    private sealed class DoubleArrayConverter : JsonConverter<double[]>
    {
        public override double[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            var values = new List<double>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) values.Add(reader.GetDouble());
            return values.ToArray();
        }

        public override void Write(Utf8JsonWriter writer, double[] value, JsonSerializerOptions options)
        {
            if (Current is { } bridge && value.Length >= BinaryThreshold)
            {
                WriteBinary(writer, "f64", bridge.Append(MemoryMarshal.AsBytes(value.AsSpan())), value.Length);
                return;
            }
            writer.WriteStartArray();
            foreach (var item in value) writer.WriteNumberValue(item);
            writer.WriteEndArray();
        }
    }

    private sealed class Int32ArrayConverter : JsonConverter<int[]>
    {
        public override int[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            var values = new List<int>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) values.Add(reader.GetInt32());
            return values.ToArray();
        }

        public override void Write(Utf8JsonWriter writer, int[] value, JsonSerializerOptions options)
        {
            if (Current is { } bridge && value.Length >= BinaryThreshold)
            {
                WriteBinary(writer, "i32", bridge.Append(MemoryMarshal.AsBytes(value.AsSpan())), value.Length);
                return;
            }
            writer.WriteStartArray();
            foreach (var item in value) writer.WriteNumberValue(item);
            writer.WriteEndArray();
        }
    }

    private sealed class RetainedDefinitionConverter(JsonSerializerOptions definitionOptions) : JsonConverter<AssemblyDisplayMeshDefinition>
    {
        public override AssemblyDisplayMeshDefinition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException("Display mesh definitions are output only.");

        public override void Write(Utf8JsonWriter writer, AssemblyDisplayMeshDefinition value, JsonSerializerOptions options)
        {
            if (Current?.Retained?.Contains(value) == true)
            {
                writer.WriteStartObject();
                writer.WriteString("id", value.Id);
                writer.WriteString("identity", value.Identity);
                writer.WriteBoolean("retained", true);
                writer.WriteEndObject();
                return;
            }
            JsonSerializer.Serialize(writer, value, definitionOptions);
        }
    }
}
