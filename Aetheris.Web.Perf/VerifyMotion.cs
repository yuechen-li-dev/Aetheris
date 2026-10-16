using System.Text.Json;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Web.Perf;

internal static class VerifyMotion
{
    public static int Run(string[] args)
    {
        if (args.Length != 1) { Console.Error.WriteLine("Usage: Aetheris.Web.Perf verify-motion <articulated-motion.firmament>"); return 2; }
        var path = Path.GetFullPath(args[0]);
        var motion = File.ReadAllText(path);
        var source = motion;
        var documents = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["motion.firmament"] = source,
            ["articulated-subassemblies.firmament"] = File.ReadAllText(Path.Combine(Path.GetDirectoryName(path)!, "articulated-subassemblies.firmament"))
        };
        var native = new AssemblyM1Pipeline().CompileFile(path);
        if (!native.IsSuccess) throw new InvalidOperationException(string.Join(';', native.Diagnostics.Select(d => d.Message)));
        JsonElement Call(object request)
        {
            using var response = JsonDocument.Parse(Runtime.Program.Invoke(JsonSerializer.Serialize(request)));
            if (!response.RootElement.GetProperty("ok").GetBoolean()) throw new InvalidOperationException(response.RootElement.ToString());
            return response.RootElement.GetProperty("result").Clone();
        }
        var built = Call(new { operation = "compile", source, sourceName = "motion.firmament", projectDocuments = documents });
        if (!built.GetProperty("success").GetBoolean()) throw new InvalidOperationException(built.ToString());
        var model = built.GetProperty("model");
        var sessionId = model.GetProperty("id").GetString();
        if (model.GetProperty("motions").GetArrayLength() != 1) throw new InvalidOperationException("Missing compiled Motion metadata.");
        foreach (var seconds in new[] { 0, 1.25, .231, 1.749, 2, -0.75, 3.25 })
        {
            var actual = Call(new { operation = "sampleMotion", sessionId, motion = "Cycle", timeSeconds = seconds });
            var expected = AssemblyMotion.Evaluate(native.Ir!, "Cycle", seconds);
            if (!expected.IsSuccess) throw new InvalidOperationException("Native pose failed.");
            foreach (var transform in actual.GetProperty("transforms").EnumerateArray())
            {
                var item = expected.Instances.Single(i => i.StableId == transform.GetProperty("id").GetString());
                if (!item.ResolvedTransform!.Matrix.SequenceEqual(transform.GetProperty("matrix").EnumerateArray().Select(v => v.GetDouble())))
                    throw new InvalidOperationException("Native/runtime Motion mismatch: " + item.Path);
            }
        }
        // Invalid edits retain both the visible geometry and its matching graph.
        Call(new { operation = "setSource", sessionId, source = source.Replace("repeat: Loop", "repeat: Unknown") });
        var failed = Call(new { operation = "rebuild", sessionId });
        if (failed.GetProperty("success").GetBoolean()) throw new InvalidOperationException("Invalid Motion was admitted.");
        var located = failed.GetProperty("diagnostics").EnumerateArray().First(d => d.GetProperty("code").GetString() == "assembly-motion-repeat-invalid").GetProperty("source");
        if (located.GetProperty("line").GetInt32() <= 1 || located.GetProperty("start").GetInt32() != source.IndexOf("Motion Cycle", StringComparison.Ordinal))
            throw new InvalidOperationException("Motion source provenance was lost in the runtime projection.");
        var retained = Call(new { operation = "sampleMotion", sessionId, motion = "Cycle", timeSeconds = 1.25 });
        if (retained.GetProperty("revision").GetInt32() != model.GetProperty("revision").GetInt32()) throw new InvalidOperationException("Motion revision drift.");
        Call(new { operation = "disposeSession", sessionId });
        Console.WriteLine("PASS: native/runtime Motion parity at seven times; cached graph and last-valid revision retained.");
        return 0;
    }
}
