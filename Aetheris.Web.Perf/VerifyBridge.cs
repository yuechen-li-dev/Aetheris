using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aetheris.Web.Perf;

/// <summary>
/// BUILD-PERF-M3: the bridge (binary arrays + retained definitions) must deliver exactly what the
/// legacy JSON response delivers. A legacy session and a bridged session build the same project
/// through cold build, no-change rebuild and root-comment edit; each bridged model is
/// reconstructed the way the SDK does it and compared with the legacy model.
/// </summary>
internal static class VerifyBridge
{
    public static int Run(string[] args)
    {
        if (args.Length != 2) { Console.Error.WriteLine("Usage: Aetheris.Web.Perf verify-bridge <project-directory|file> <root>"); return 2; }
        var (documents, root) = Load(args[0], args[1]);
        var projectDocuments = documents.Count > 1 ? documents : null;
        var failures = 0;
        JsonNode? legacySession = null, bridgedSession = null;
        JsonObject? previousBridged = null;
        var steps = new (string Name, Func<Dictionary<string, string>, object> Request)[]
        {
            ("cold build", d => new { operation = "compile", source = d[root], sourceName = root, projectDocuments = d.Count > 1 ? d : null }),
            ("no-change rebuild", _ => new { operation = "rebuild" }),
            ("root comment edit", _ => new { operation = "rebuild" }),
        };
        var current = new Dictionary<string, string>(documents, StringComparer.Ordinal);
        foreach (var (name, request) in steps)
        {
            if (name == "root comment edit")
            {
                current[root] += "\n// verify-bridge\n";
                foreach (var session in new[] { legacySession, bridgedSession })
                    Call(new { operation = "setSource", sessionId = session!.GetValue<string>(), source = current[root], sourceName = root, projectDocuments = current.Count > 1 ? current : null }, bridged: false);
            }
            var legacy = Call(WithSession(request(current), legacySession), bridged: false);
            var (bridgedRaw, binary) = CallBridged(WithSession(request(current), bridgedSession));
            var bridged = Reconstruct(bridgedRaw, binary, previousBridged, out var retained, out var binaryArrays);
            legacySession ??= legacy["result"]!["model"]!["id"]!.DeepClone();
            bridgedSession ??= bridged["result"]!["model"]!["id"]!.DeepClone();
            var expected = Comparable(legacy); var actual = Comparable(bridged);
            var equal = JsonNode.DeepEquals(expected, actual);
            Console.WriteLine($"{name,-18} legacy {Size(legacy.ToJsonString())} · bridged {Size(bridgedRaw.ToJsonString())} JSON + {Size(binary.Length)} binary · " +
                $"{binaryArrays} binary arrays · {retained} retained definitions · {(equal ? "identical" : "DIFFERENT")}");
            if (!equal) { failures++; Report(expected, actual, "model"); }
            previousBridged = bridged["result"]!["model"]!.AsObject();
        }
        Console.WriteLine(failures == 0 ? "Bridge delivers exactly the legacy model." : $"{failures} step(s) differ.");
        return failures == 0 ? 0 : 1;
    }

    private static (Dictionary<string, string>, string) Load(string path, string root)
    {
        if (File.Exists(path)) return (new(StringComparer.Ordinal) { [root] = File.ReadAllText(path).Replace("\r\n", "\n") }, root);
        return (Directory.GetFiles(path).Where(f => f.EndsWith(".firmament", StringComparison.Ordinal) || f.EndsWith(".firmasm", StringComparison.Ordinal))
            .ToDictionary(Path.GetFileName, f => File.ReadAllText(f).Replace("\r\n", "\n"), StringComparer.Ordinal)!, root);
    }

    private static object WithSession(object request, JsonNode? session)
    {
        var node = JsonSerializer.SerializeToNode(request)!.AsObject();
        if (session is not null) node["sessionId"] = session.DeepClone();
        return node;
    }

    private static JsonObject Call(object request, bool bridged)
    {
        var node = JsonSerializer.SerializeToNode(request)!.AsObject();
        if (bridged) node["bridge"] = 1;
        var response = JsonNode.Parse(Aetheris.Web.Runtime.Program.Invoke(node.ToJsonString()))!.AsObject();
        if (response["ok"]?.GetValue<bool>() != true) throw new InvalidOperationException(response.ToJsonString()[..Math.Min(600, response.ToJsonString().Length)]);
        return response;
    }

    private static (JsonObject Response, byte[] Binary) CallBridged(object request)
    {
        var response = Call(request, bridged: true);
        return (response, Aetheris.Web.Runtime.Program.TakeBinary());
    }

    /// <summary>What the SDK does: binary placeholders become arrays, retained definitions are
    /// taken from the previous model's definitions by id.</summary>
    private static JsonObject Reconstruct(JsonObject response, byte[] binary, JsonObject? previous, out int retained, out int arrays)
    {
        var copy = response.DeepClone().AsObject();
        var counts = new int[2];
        JsonNode? Revive(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject obj when obj["$binary"] is { } type:
                    counts[1]++;
                    var offset = obj["byteOffset"]!.GetValue<int>(); var length = obj["length"]!.GetValue<int>();
                    var array = new JsonArray();
                    if (type.GetValue<string>() == "f64")
                        for (var i = 0; i < length; i++) array.Add(BitConverter.ToDouble(binary, offset + 8 * i));
                    else
                        for (var i = 0; i < length; i++) array.Add(BitConverter.ToInt32(binary, offset + 4 * i));
                    return array;
                case JsonObject obj:
                    foreach (var key in obj.Select(pair => pair.Key).ToArray()) obj[key] = Revive(obj[key]?.DeepClone());
                    return obj;
                case JsonArray list:
                    var revived = new JsonArray();
                    foreach (var item in list) revived.Add(Revive(item?.DeepClone()));
                    return revived;
                default: return node;
            }
        }
        var revivedResponse = Revive(copy)!.AsObject();
        if (revivedResponse["result"]?["model"]?["mesh"]?["definitions"] is JsonArray definitions)
        {
            var kept = (previous?["mesh"]?["definitions"] as JsonArray)?.ToDictionary(d => d!["id"]!.GetValue<string>(), d => d!) ?? [];
            for (var i = 0; i < definitions.Count; i++)
                if (definitions[i]?["retained"]?.GetValue<bool>() == true)
                {
                    counts[0]++;
                    var id = definitions[i]!["id"]!.GetValue<string>();
                    definitions[i] = kept.TryGetValue(id, out var definition) ? definition.DeepClone()
                        : throw new InvalidOperationException($"Retained definition {id} is not in the previous model.");
                }
        }
        retained = counts[0]; arrays = counts[1];
        return revivedResponse;
    }

    /// <summary>The model without its session id and timings (which legitimately differ).</summary>
    private static JsonNode Comparable(JsonObject response)
    {
        var model = response["result"]!["model"]!.DeepClone().AsObject();
        var session = model["id"]!.GetValue<string>();
        model.Remove("id"); model.Remove("timings");
        // The part path embeds the session id in entity ids; two sessions legitimately differ there.
        var text = System.Text.RegularExpressions.Regex.Replace(model.ToJsonString(),
            $@"(?<![\w-]){System.Text.RegularExpressions.Regex.Escape(session)}(?!\d)", "<session>");
        return JsonNode.Parse(text)!;
    }

    private static void Report(JsonNode? expected, JsonNode? actual, string path, int budget = 5)
    {
        if (JsonNode.DeepEquals(expected, actual)) return;
        if (expected is JsonObject a && actual is JsonObject b)
        {
            foreach (var key in a.Select(p => p.Key).Union(b.Select(p => p.Key))) Report(a[key], b[key], $"{path}.{key}", budget);
            return;
        }
        if (expected is JsonArray x && actual is JsonArray y && x.Count == y.Count)
        {
            for (var i = 0; i < x.Count && budget > 0; i++) if (!JsonNode.DeepEquals(x[i], y[i])) { Report(x[i], y[i], $"{path}[{i}]", 1); budget--; }
            return;
        }
        Console.Error.WriteLine($"  {path}: {Short(expected)} vs {Short(actual)}");
    }

    private static string Short(JsonNode? node) { var text = node?.ToJsonString() ?? "null"; return text.Length > 120 ? text[..120] + "…" : text; }
    private static string Size(long bytes) => bytes >= 1 << 20 ? $"{bytes / (double)(1 << 20):0.0} MB" : $"{bytes / 1024.0:0} KB";
    private static string Size(string text) => Size(System.Text.Encoding.UTF8.GetByteCount(text));
}
