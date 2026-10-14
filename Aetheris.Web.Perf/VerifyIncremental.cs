using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Aetheris.Web.Perf;

/// <summary>
/// BUILD-PERF-M2 safety net: an incremental rebuild must equal a from-scratch build. Edits are applied
/// to a live session (comment appended, line inserted at the top of a document so every later offset
/// shifts, first numeric literal changed); after each edit the session's model is compared with a
/// fresh compile of the edited project, and after reverting it with the original model. Geometry,
/// mesh, tree, properties, projections, diagnostics and every source span must be identical.
/// </summary>
internal static class VerifyIncremental
{
    private sealed record Edit(string Name, string Document, Func<string, string?> Apply);

    public static int Run(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: Aetheris.Web.Perf verify-incremental <project-directory|file> <root> [--documents N]"); return 2; }
        var maximumDocuments = 6;
        for (var i = 2; i < args.Length; i++)
            if (args[i] == "--documents" && i + 1 < args.Length) maximumDocuments = int.Parse(args[++i]);
        var (documents, root) = Load(args[0], args[1]);
        var edits = Edits(documents, root, maximumDocuments);
        var baseline = Fresh(documents, root);
        var session = Compile(documents, root, out var sessionResult);
        var failures = 0;
        failures += Compare("initial build", baseline, sessionResult);
        foreach (var edit in edits)
        {
            var text = edit.Apply(documents[edit.Document]);
            if (text is null || text == documents[edit.Document]) continue;
            var edited = new Dictionary<string, string>(documents, StringComparer.Ordinal) { [edit.Document] = text };
            var started = Stopwatch.GetTimestamp();
            var incremental = SetSourceAndRebuild(session, edited, root);
            var incrementalMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            started = Stopwatch.GetTimestamp();
            var fresh = Fresh(edited, root);
            var freshMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            failures += Compare($"{edit.Name} ({incrementalMs:0} ms vs fresh {freshMs:0} ms, {Reused(incremental)})", fresh, incremental);
            failures += Compare($"  revert {edit.Name}", baseline, SetSourceAndRebuild(session, documents, root));
        }
        Console.WriteLine(failures == 0 ? "Every incremental rebuild equals a fresh build." : $"{failures} comparison(s) differ.");
        return failures == 0 ? 0 : 1;
    }

    private static IReadOnlyList<Edit> Edits(Dictionary<string, string> documents, string root, int maximumDocuments)
    {
        var names = documents.Keys.Order(StringComparer.Ordinal).ToList();
        // A deterministic spread over the project, always including the root.
        var picked = names.Count <= maximumDocuments ? names
            : Enumerable.Range(0, maximumDocuments).Select(i => names[i * names.Count / maximumDocuments]).Append(root).Distinct().ToList();
        var edits = new List<Edit>();
        foreach (var name in picked)
        {
            edits.Add(new($"{name}: comment appended", name, text => text + "\n// verify-incremental\n"));
            edits.Add(new($"{name}: line inserted at top", name, text => "// verify-incremental: shifts every offset\n" + text));
            edits.Add(new($"{name}: first value changed", name, text =>
            {
                var match = Regex.Match(text, @"(?<![\w.])(\d+(?:\.\d+)?)(mm)\b");
                if (!match.Success) return null;
                var value = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                var changed = (value * 1.05 + 0.1).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                return text[..match.Index] + changed + text[(match.Index + match.Groups[1].Length)..];
            }));
        }
        if (documents.TryGetValue("knob.firmament", out var knob) && knob.Contains("R:9mm,H:9mm", StringComparison.Ordinal))
            edits.Add(new("knob.firmament: knob radius", "knob.firmament", text => text.Replace("R:9mm,H:9mm", "R:9.5mm,H:9mm", StringComparison.Ordinal)));
        return edits;
    }

    private static (Dictionary<string, string>, string) Load(string path, string root)
    {
        if (File.Exists(path)) return (new(StringComparer.Ordinal) { [root] = File.ReadAllText(path).Replace("\r\n", "\n") }, root);
        return (Directory.GetFiles(path).Where(f => f.EndsWith(".firmament", StringComparison.Ordinal) || f.EndsWith(".firmasm", StringComparison.Ordinal))
            .ToDictionary(Path.GetFileName, f => File.ReadAllText(f).Replace("\r\n", "\n"), StringComparer.Ordinal)!, root);
    }

    private static JsonObject Invoke(object request)
    {
        var response = JsonNode.Parse(Aetheris.Web.Runtime.Program.Invoke(JsonSerializer.Serialize(request)))!.AsObject();
        if (response["ok"]?.GetValue<bool>() != true) throw new InvalidOperationException(response.ToJsonString());
        return response["result"]!.AsObject();
    }

    private static string Compile(Dictionary<string, string> documents, string root, out JsonObject result)
    {
        result = Invoke(new { operation = "compile", source = documents[root], sourceName = root, projectDocuments = documents.Count > 1 ? documents : null });
        return result["model"]?["id"]?.GetValue<string>() ?? throw new InvalidOperationException("The original project does not build.");
    }

    private static JsonObject Fresh(Dictionary<string, string> documents, string root)
    {
        var result = Invoke(new { operation = "compile", source = documents[root], sourceName = root, projectDocuments = documents.Count > 1 ? documents : null });
        if (result["model"]?["id"]?.GetValue<string>() is { } id) Invoke(new { operation = "disposeSession", sessionId = id });
        return result;
    }

    private static JsonObject SetSourceAndRebuild(string session, Dictionary<string, string> documents, string root)
    {
        Invoke(new { operation = "setSource", sessionId = session, source = documents[root], sourceName = root, projectDocuments = documents.Count > 1 ? documents : null });
        return Invoke(new { operation = "rebuild", sessionId = session });
    }

    private static string Reused(JsonObject result) =>
        result["model"]?["timings"]?["reuse"]?["definitions"] is JsonArray reuse
            ? $"{reuse.Count(item => item?["reused"]?.GetValue<bool>() == true)}/{reuse.Count} reused" : "no definition cache";

    /// <summary>A failed build compares by its diagnostics; a successful one by its whole model,
    /// without the session id, build revision, timings and change set (which legitimately differ).</summary>
    private static int Compare(string label, JsonObject expected, JsonObject actual)
    {
        var a = Comparable(expected); var b = Comparable(actual);
        var equal = JsonNode.DeepEquals(a, b);
        Console.WriteLine($"{(equal ? "ok  " : "DIFF")} {label}");
        if (!equal) Report(a, b, "");
        return equal ? 0 : 1;
    }

    private static JsonNode Comparable(JsonObject result)
    {
        if (result["success"]?.GetValue<bool>() != true)
            return new JsonObject { ["failed"] = true, ["diagnostics"] = result["diagnostics"]?.DeepClone() };
        var model = result["model"]!.DeepClone().AsObject();
        var session = model["id"]!.GetValue<string>();
        foreach (var key in new[] { "id", "revision", "timings", "changes" }) model.Remove(key);
        var text = Regex.Replace(model.ToJsonString(), $@"(?<![\w-]){Regex.Escape(session)}(?!\d)", "<session>");
        return WithoutRevisions(JsonNode.Parse(text)!);
    }

    private static JsonNode WithoutRevisions(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(pair => pair.Key).Where(key => key.Contains("evision", StringComparison.Ordinal)).ToArray()) obj.Remove(key);
            foreach (var (_, child) in obj) if (child is not null) WithoutRevisions(child);
        }
        else if (node is JsonArray array) foreach (var child in array) if (child is not null) WithoutRevisions(child);
        return node;
    }

    private static void Report(JsonNode? expected, JsonNode? actual, string path, int budget = 6)
    {
        if (JsonNode.DeepEquals(expected, actual)) return;
        if (expected is JsonObject a && actual is JsonObject b)
        {
            foreach (var key in a.Select(p => p.Key).Union(b.Select(p => p.Key)))
                if (!JsonNode.DeepEquals(a[key], b[key]) && budget-- > 0) Report(a[key], b[key], $"{path}.{key}", 2);
            return;
        }
        if (expected is JsonArray x && actual is JsonArray y && x.Count == y.Count)
        {
            for (var i = 0; i < x.Count && budget > 0; i++) if (!JsonNode.DeepEquals(x[i], y[i])) { Report(x[i], y[i], $"{path}[{i}]", 1); budget--; }
            return;
        }
        Console.Error.WriteLine($"     {path}: fresh {Short(expected)} · incremental {Short(actual)}");
    }

    private static string Short(JsonNode? node) { var text = node?.ToJsonString() ?? "null"; return text.Length > 140 ? text[..140] + "…" : text; }
}
