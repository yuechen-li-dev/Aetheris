using System.Text.Json.Serialization;

namespace Aetheris.Kernel.Firmament;

// Language string literals use JSON escaping, including in NativeAOT hosts.
// Keep their primitive metadata explicit instead of relying on reflection.
[JsonSerializable(typeof(string))]
internal partial class FirmamentJsonContext : JsonSerializerContext;
