using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aetheris.Web.Perf;

public static class PerfProgram
{
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "showcase") return ShowcasePerf.Run(args[1..]);
        if (args.Length > 0 && args[0] == "compare-step") return CompareStep.Run(args[1..]);
        if (args.Length > 0 && args[0] == "verify-bridge") return VerifyBridge.Run(args[1..]);
        if (args.Length > 0 && args[0] == "verify-motion") return VerifyMotion.Run(args[1..]);
        if (args.Length > 0 && args[0] == "verify-incremental") return VerifyIncremental.Run(args[1..]);
        if (args.Length > 0 && args[0] == "profile") return ProfileOne(args[1..]);
        if (args.Length != 1) { Console.Error.WriteLine("Usage: Aetheris.Web.Perf <helix-fixture> | Aetheris.Web.Perf showcase --help | Aetheris.Web.Perf compare-step <a.step> <b.step>"); return 2; }
        var source = File.ReadAllText(args[0]);
        var variants = new[] { ("helix-1", source), ("helix-2", source.Replace("Turns: 1", "Turns: 2", StringComparison.Ordinal)),
            ("helix-5", source.Replace("Turns: 1", "Turns: 5", StringComparison.Ordinal)),
            ("helix-10", source.Replace("Turns: 1", "Turns: 10", StringComparison.Ordinal)),
            ("box", "Model PerfBox { Units: mm Box Body { Size: [30mm, 20mm, 10mm] } }"),
            ("hole", "Model PerfHole { Units: mm Box Body { Size: [30mm, 20mm, 10mm] } Modify Body { Hole<Shaft> H { On: +Z Center: Point2(0mm, 0mm) Diameter: 6mm End: ThroughAll } } }") };
        foreach (var (name, text) in variants)
        {
            var runs = name == "helix-1" ? 6 : 1;
            for (var run = 0; run < runs; run++)
            {
                var request = JsonSerializer.Serialize(new { operation = "compile", source = text, sourceName = name + ".firmament", performance = true });
                var started = Stopwatch.GetTimestamp();
                var response = Aetheris.Web.Runtime.Program.Invoke(request);
                var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                using var document = JsonDocument.Parse(response);
                var root = document.RootElement;
                if (!root.GetProperty("ok").GetBoolean()) { Console.WriteLine(JsonSerializer.Serialize(new { runtime = "Native RyuJIT", name, run, elapsed, error = root.GetProperty("error") })); return 1; }
                var result = root.GetProperty("result");
                if (!result.GetProperty("success").GetBoolean()) { Console.WriteLine(JsonSerializer.Serialize(new { runtime = "Native RyuJIT", name, run, elapsed, diagnostics = result.GetProperty("diagnostics") })); return 1; }
                var model = result.GetProperty("model");
                object? stepEvidence = null;
                if (run == 0)
                {
                    using var stepResult = JsonDocument.Parse(Aetheris.Web.Runtime.Program.Invoke(JsonSerializer.Serialize(new { operation = "exportStep", sessionId = model.GetProperty("id").GetString() })));
                    var bytes = Convert.FromBase64String(stepResult.RootElement.GetProperty("result").GetProperty("base64").GetString()!);
                    if (name == "helix-5")
                    {
                        var output = Path.Combine("artifacts", "local", "web-perf", "helix-5-native.step");
                        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                        File.WriteAllBytes(output, bytes);
                    }
                    stepEvidence = new { bytes = bytes.Length, sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() };
                }
                var definition = model.GetProperty("mesh").GetProperty("definitions")[0];
                Console.WriteLine(JsonSerializer.Serialize(new { runtime = "Native RyuJIT", name, run, elapsed, timings = model.GetProperty("timings"),
                    mesh = new { definitions = model.GetProperty("mesh").GetProperty("definitions").GetArrayLength(),
                        vertices = definition.GetProperty("positions").GetArrayLength() / 3, triangles = definition.GetProperty("indices").GetArrayLength() / 3,
                        ranges = definition.GetProperty("ranges").GetArrayLength(), edges = definition.GetProperty("edges").GetArrayLength() }, step = stepEvidence }));
                Aetheris.Web.Runtime.Program.Invoke(JsonSerializer.Serialize(new { operation = "disposeSession", sessionId = model.GetProperty("id").GetString() }));
            }
        }
        return 0;
    }

    /// <summary>One build of one document through the runtime entry point, with its top phases:
    /// <c>profile &lt;file&gt; [--project]</c> (--project sends the file's directory as project documents).</summary>
    private static int ProfileOne(string[] args)
    {
        if (args.Length < 1) { Console.Error.WriteLine("Usage: Aetheris.Web.Perf profile <file> [--project]"); return 2; }
        var path = Path.GetFullPath(args[0]);
        var root = Path.GetFileName(path);
        Dictionary<string, string>? documents = null;
        if (args.Contains("--project"))
            documents = Directory.GetFiles(Path.GetDirectoryName(path)!).Where(f => f.EndsWith(".firmament", StringComparison.Ordinal) || f.EndsWith(".firmasm", StringComparison.Ordinal))
                .ToDictionary(Path.GetFileName, f => File.ReadAllText(f).Replace("\r\n", "\n"), StringComparer.Ordinal)!;
        var started = Stopwatch.GetTimestamp();
        var response = JsonNode.Parse(Aetheris.Web.Runtime.Program.Invoke(JsonSerializer.Serialize(new
        {
            operation = "compile", source = File.ReadAllText(path).Replace("\r\n", "\n"), sourceName = documents is null ? path : root,
            projectDocuments = documents, performance = true
        })))!;
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var model = response["result"]?["model"];
        Console.WriteLine($"{root}: {elapsed:0} ms, success={response["result"]?["success"]}");
        foreach (var phase in (model?["timings"]?["profile"]?["phases"]?.AsArray() ?? [])
                     .GroupBy(p => p!["name"]!.GetValue<string>())
                     .Select(g => (Name: g.Key, Count: g.Count(), Ms: g.Sum(p => p!["exclusiveMilliseconds"]!.GetValue<double>())))
                     .OrderByDescending(p => p.Ms).Take(20))
            Console.WriteLine($"  {phase.Ms,9:0.0} ms  {phase.Count,5}x  {phase.Name}");
        if (model is null) Console.WriteLine(response.ToJsonString()[..Math.Min(2000, response.ToJsonString().Length)]);
        return 0;
    }
}
