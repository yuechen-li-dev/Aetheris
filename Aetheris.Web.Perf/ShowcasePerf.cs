using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aetheris.Web.Perf;

/// <summary>
/// BUILD-PERF-M0: the Helios showcase projects measured through the browser runtime's own entry
/// point (<see cref="Aetheris.Web.Runtime.Program.Invoke"/>). Each project runs in a fresh process
/// so its first build is a true cold build. See
/// docs/development/milestones/general/build-perf-ladder.md.
/// </summary>
internal static class ShowcasePerf
{
    private sealed record Project(string Name, string? Directory, string Root, string? InlineSource,
        string? LeafEditDocument = null, string? LeafEditFrom = null, string? LeafEditTo = null);

    private const string BracketSource = """
        Model WebBracket {
            Units: mm
            Box Plate { Size: [50mm, 40mm, 8mm] }
            Modify Plate {
                Hole<Shaft> CenterMount { On: +Z Center: Point2(0mm, 0mm) Diameter: 8mm End: ThroughAll }
            }
        }

        """;

    // The same sources Helios ships in fixtures/showcase (identical apart from line endings).
    private static readonly Project[] Projects =
    [
        new("bracket", null, "bracket.firmament", BracketSource),
        new("bolt", "fixtures/Thread", "hexbolt-showcase.firmament", null),
        new("atlas", "fixtures/Canonical/AssemblyInterfaces/IndustrialAtlas", "atlas-industrial.firmament", null),
        new("guitar", "fixtures/Canonical/AssemblyInterfaces/GuitarX0", "guitar.firmasm", null,
            "knob.firmament", "R:9mm,H:9mm", "R:9.5mm,H:9mm"),
        new("house", "fixtures/Canonical/Scene/WarmModernHouse", "house.firmament", null),
    ];

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static int Run(string[] args)
    {
        string? only = null, outDir = null, check = null, writeBaseline = null;
        var runs = 1; var checkTime = false; var timeTolerance = 1.5;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--project" when i + 1 < args.Length: only = args[++i]; break;
                case "--runs" when i + 1 < args.Length: runs = int.Parse(args[++i]); break;
                case "--out" when i + 1 < args.Length: outDir = args[++i]; break;
                case "--check" when i + 1 < args.Length: check = args[++i]; break;
                case "--write-baseline" when i + 1 < args.Length: writeBaseline = args[++i]; break;
                case "--check-time": checkTime = true; break;
                case "--time-tolerance" when i + 1 < args.Length: timeTolerance = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                default: return Usage();
            }
        }
        var repo = FindRepositoryRoot();
        if (repo is null) { Console.Error.WriteLine("Run from inside the Aetheris repository."); return 2; }
        // A child process measures one project once; the parent fans out and aggregates.
        if (only is not null && Environment.GetEnvironmentVariable("AETHERIS_BUILD_PERF_CHILD") == "1")
        {
            var project = Projects.SingleOrDefault(p => p.Name == only);
            if (project is null) { Console.Error.WriteLine($"Unknown project '{only}'."); return 2; }
            var report = Measure(project, repo);
            Console.Out.Write(report.ToJsonString());
            return 0;
        }

        outDir ??= Path.Combine(repo, "artifacts", "local", "build-perf", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(outDir);
        var selected = only is null ? Projects : Projects.Where(p => p.Name == only).ToArray();
        if (selected.Length == 0) { Console.Error.WriteLine($"Unknown project '{only}'."); return 2; }
        var results = new JsonObject
        {
            ["schema"] = "aetheris/build-perf/m0",
            ["runtime"] = $"Native {RuntimeInfo()}",
            ["machine"] = $"{Environment.MachineName} · {Environment.ProcessorCount} CPU · {Environment.OSVersion}",
            ["commit"] = GitCommit(repo),
            ["runs"] = runs,
        };
        var projects = new JsonObject();
        foreach (var project in selected)
        {
            var samples = new List<JsonObject>();
            for (var run = 0; run < runs; run++)
            {
                var sample = RunChild(project.Name, repo);
                if (sample is null) return 1;
                samples.Add(sample);
                Console.Error.WriteLine($"{project.Name} run {run + 1}/{runs}: cold {Ms(sample, "cold", "invokeMilliseconds")} · warm {Ms(sample, "warm", "invokeMilliseconds")} · no-change {Ms(sample, "noChangeRebuild", "invokeMilliseconds")}");
            }
            projects[project.Name] = Summarize(samples);
        }
        results["projects"] = projects;
        var reportPath = Path.Combine(outDir, "build-perf.json");
        File.WriteAllText(reportPath, results.ToJsonString(Indented));
        PrintTable(projects);
        Console.WriteLine($"Report: {reportPath}");

        if (writeBaseline is not null)
        {
            File.WriteAllText(writeBaseline, Baseline(results).ToJsonString(Indented) + "\n");
            Console.WriteLine($"Baseline written: {writeBaseline}");
        }
        if (check is not null)
            return Check(JsonNode.Parse(File.ReadAllText(check))!.AsObject(), projects, checkTime, timeTolerance) ? 0 : 1;
        return 0;
    }

    private static int Usage()
    {
        Console.Error.WriteLine("""
            Usage: Aetheris.Web.Perf showcase [--project <name>] [--runs <n>] [--out <dir>]
                                              [--write-baseline <budgets.json>] [--check <budgets.json>]
                                              [--check-time] [--time-tolerance <factor>]
            Projects: bracket, bolt, atlas, guitar, house.
            --check always gates machine-independent results (STEP hash, response size, allocations,
            reused definitions); --check-time also gates wall time against the recorded machine.
            """);
        return 2;
    }

    // ---------------------------------------------------------------- measurement (child process)

    private static JsonObject Measure(Project project, string repo)
    {
        var documents = project.InlineSource is not null
            ? new Dictionary<string, string>(StringComparer.Ordinal) { [project.Root] = project.InlineSource }
            : System.IO.Directory.GetFiles(Path.Combine(repo, project.Directory!))
                .Where(path => path.EndsWith(".firmament", StringComparison.Ordinal) || path.EndsWith(".firmasm", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToDictionary(Path.GetFileName, path => File.ReadAllText(path).Replace("\r\n", "\n"), StringComparer.Ordinal)!;
        var projectDocuments = documents.Count > 1 ? documents : null;
        var report = new JsonObject { ["documents"] = documents.Count, ["root"] = project.Root };

        // Cold: the first build in this process (JIT, static initialization, empty caches).
        var (cold, session) = Compile(documents, project.Root, projectDocuments);
        report["cold"] = cold;
        report["determinism"] = Determinism(session, cold, project.Name);
        Dispose(session);

        // Warm: a new session in a warm process. Definition caches are per session, so this is a
        // full build without JIT cost.
        var (warm, warmSession) = Compile(documents, project.Root, projectDocuments);
        report["warm"] = warm;

        // Editor loop on the warm session.
        report["noChangeRebuild"] = Rebuild(warmSession);
        // Invariant: a rebuild that changes no geometry reproduces the cold build exactly.
        var rebuilt = Determinism(warmSession, report["noChangeRebuild"]!.AsObject(), project.Name, dump: false);
        report["determinism"]!["rebuildStepMatchesCold"] = rebuilt["stepDataSha256"]?.ToString() == report["determinism"]!["stepDataSha256"]?.ToString();
        report["determinism"]!["rebuildMeshMatchesCold"] = report["noChangeRebuild"]!["meshContentSha256"]?.ToString() == cold["meshContentSha256"]?.ToString();
        var edited = new Dictionary<string, string>(documents, StringComparer.Ordinal);
        if (project.LeafEditDocument is { } leaf && project.LeafEditFrom is { } from && project.LeafEditTo is { } to
            && edited.TryGetValue(leaf, out var leafText) && leafText.Contains(from, StringComparison.Ordinal))
        {
            edited[leaf] = leafText.Replace(from, to, StringComparison.Ordinal);
            report["leafValueEdit"] = SetSourceAndRebuild(warmSession, edited, project.Root);
            report["leafValueEdit"]!["edit"] = $"{leaf}: {from} -> {to}";
            edited = new(documents, StringComparer.Ordinal);
            SetSourceAndRebuild(warmSession, edited, project.Root);
        }
        var nonRoot = documents.Keys.Where(name => name != project.Root).Order(StringComparer.Ordinal).FirstOrDefault();
        if (nonRoot is not null)
        {
            edited[nonRoot] += "\n// build-perf: leaf comment\n";
            report["leafCommentEdit"] = SetSourceAndRebuild(warmSession, edited, project.Root);
            report["leafCommentEdit"]!["edit"] = $"{nonRoot}: appended comment";
            edited = new(documents, StringComparer.Ordinal);
            SetSourceAndRebuild(warmSession, edited, project.Root);
        }
        edited[project.Root] += "\n// build-perf: root comment\n";
        report["rootCommentEdit"] = SetSourceAndRebuild(warmSession, edited, project.Root);
        Dispose(warmSession);
        return report;
    }

    private static (JsonObject Sample, string SessionId) Compile(IReadOnlyDictionary<string, string> documents, string root,
        IReadOnlyDictionary<string, string>? projectDocuments)
    {
        var (response, sample) = Invoke(new { operation = "compile", source = documents[root], sourceName = root, projectDocuments, performance = true });
        var result = response["result"]!;
        var model = result["model"] ?? throw new InvalidOperationException("Build failed: " + Truncate(result["diagnostics"]?.ToJsonString()));
        return (Annotate(sample, result), model["id"]!.GetValue<string>());
    }

    private static JsonObject Rebuild(string sessionId)
    {
        var (response, sample) = Invoke(new { operation = "rebuild", sessionId, performance = true });
        return Annotate(sample, response["result"]!);
    }

    private static JsonObject SetSourceAndRebuild(string sessionId, IReadOnlyDictionary<string, string> documents, string root)
    {
        Invoke(new { operation = "setSource", sessionId, source = documents[root], sourceName = root, projectDocuments = documents.Count > 1 ? documents : null });
        return Rebuild(sessionId);
    }

    private static void Dispose(string sessionId) => Invoke(new { operation = "disposeSession", sessionId });

    private static (JsonNode Response, JsonObject Sample) Invoke(object request)
    {
        var json = JsonSerializer.Serialize(request);
        var allocated = GC.GetTotalAllocatedBytes(precise: true);
        var started = Stopwatch.GetTimestamp();
        var text = Aetheris.Web.Runtime.Program.Invoke(json);
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocated;
        var response = JsonNode.Parse(text)!;
        if (response["ok"]?.GetValue<bool>() != true)
            throw new InvalidOperationException("Runtime error: " + Truncate(response["error"]?.ToJsonString()));
        return (response, new JsonObject
        {
            ["invokeMilliseconds"] = Round(elapsed),
            ["responseBytes"] = Encoding.UTF8.GetByteCount(text),
            ["allocatedBytes"] = allocatedBytes,
        });
    }

    private static JsonObject Annotate(JsonObject sample, JsonNode result)
    {
        if (result["success"]?.GetValue<bool>() != true)
            throw new InvalidOperationException("Build failed: " + Truncate(result["diagnostics"]?.ToJsonString()));
        var timings = result["model"]?["timings"];
        sample["rebuildMilliseconds"] = Round(timings?["totalBeforeSnapshotMilliseconds"]?.GetValue<double>() ?? 0);
        sample["compileMilliseconds"] = Round(timings?["compileMilliseconds"]?.GetValue<double>() ?? 0);
        sample["meshMilliseconds"] = Round(timings?["meshMilliseconds"]?.GetValue<double>() ?? 0);
        if (timings?["reuse"]?["definitions"] is JsonArray reuse)
        {
            sample["definitions"] = reuse.Count;
            sample["reusedDefinitions"] = reuse.Count(item => item?["reused"]?.GetValue<bool>() == true);
        }
        var profile = timings?["profile"];
        if (profile is not null)
        {
            sample["gc"] = new JsonArray(profile["gc0"]!.GetValue<int>(), profile["gc1"]!.GetValue<int>(), profile["gc2"]!.GetValue<int>());
            var phases = new JsonObject();
            foreach (var group in profile["phases"]!.AsArray()
                         .Select(phase => (Name: phase!["name"]!.GetValue<string>(), Exclusive: phase["exclusiveMilliseconds"]!.GetValue<double>()))
                         .GroupBy(phase => phase.Name)
                         .OrderByDescending(group => group.Sum(phase => phase.Exclusive)))
                phases[group.Key] = new JsonObject { ["count"] = group.Count(), ["exclusiveMilliseconds"] = Round(group.Sum(phase => phase.Exclusive)) };
            sample["phases"] = phases;
        }
        sample["meshSha256"] = result["model"]?["mesh"] is { } mesh ? Sha256(mesh.ToJsonString()) : null;
        // Same mesh content regardless of which build revision produced it.
        sample["meshContentSha256"] = result["model"]?["mesh"] is { } content ? Sha256(WithoutRevisions(content.DeepClone()).ToJsonString()) : null;
        return sample;
    }

    private static JsonObject Determinism(string sessionId, JsonObject cold, string projectName, bool dump = true)
    {
        var determinism = new JsonObject { ["meshSha256"] = cold["meshSha256"]?.GetValue<string>() };
        try
        {
            var (response, _) = Invoke(new { operation = "exportStep", sessionId });
            var step = Encoding.UTF8.GetString(Convert.FromBase64String(response["result"]!["base64"]!.GetValue<string>()));
            // The STEP header carries a timestamp; the DATA section is the geometry contract.
            var data = step.IndexOf("DATA;", StringComparison.Ordinal);
            determinism["stepBytes"] = Encoding.UTF8.GetByteCount(step);
            determinism["stepDataSha256"] = Sha256(data >= 0 ? step[data..] : step);
            if (dump && Environment.GetEnvironmentVariable("AETHERIS_BUILD_PERF_DUMP_STEP") is { Length: > 0 } directory)
                File.WriteAllText(Path.Combine(directory, $"native-{projectName}.step"), step);
        }
        catch (InvalidOperationException error) when (error.Message.Contains("step-export-unavailable", StringComparison.Ordinal))
        {
            determinism["stepDataSha256"] = null;
            determinism["note"] = "No STEP product artifact (Scene).";
        }
        return determinism;
    }

    // ---------------------------------------------------------------- aggregation (parent process)

    private static JsonObject? RunChild(string project, string repo)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = repo,
        };
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!) is "dotnet") start.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location);
        foreach (var argument in new[] { "showcase", "--project", project }) start.ArgumentList.Add(argument);
        start.Environment["AETHERIS_BUILD_PERF_CHILD"] = "1";
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            Console.Error.WriteLine($"{project}: measurement failed (exit {process.ExitCode}).\n{Truncate(stderr.Result, 4000)}");
            return null;
        }
        return JsonNode.Parse(stdout.Result)!.AsObject();
    }

    private static readonly string[] Scenarios = ["cold", "warm", "noChangeRebuild", "leafValueEdit", "leafCommentEdit", "rootCommentEdit"];

    /// <summary>Median of each numeric field across runs; hashes must agree across runs.</summary>
    private static JsonObject Summarize(IReadOnlyList<JsonObject> samples)
    {
        var first = samples[0];
        var summary = new JsonObject { ["documents"] = first["documents"]!.DeepClone(), ["root"] = first["root"]!.DeepClone() };
        var determinism = first["determinism"]!.DeepClone().AsObject();
        var stepHashes = samples.Select(sample => sample["determinism"]?["stepDataSha256"]?.ToString()).Distinct().ToArray();
        var meshHashes = samples.Select(sample => sample["determinism"]?["meshSha256"]?.ToString()).Distinct().ToArray();
        determinism["stableAcrossRuns"] = stepHashes.Length == 1 && meshHashes.Length == 1;
        determinism["rebuildStepMatchesCold"] = samples.All(sample => sample["determinism"]?["rebuildStepMatchesCold"]?.GetValue<bool>() != false);
        determinism["rebuildMeshMatchesCold"] = samples.All(sample => sample["determinism"]?["rebuildMeshMatchesCold"]?.GetValue<bool>() != false);
        summary["determinism"] = determinism;
        foreach (var scenario in Scenarios)
        {
            var present = samples.Select(sample => sample[scenario] as JsonObject).Where(sample => sample is not null).Cast<JsonObject>().ToArray();
            if (present.Length == 0) continue;
            var aggregate = new JsonObject();
            foreach (var field in new[] { "invokeMilliseconds", "rebuildMilliseconds", "compileMilliseconds", "meshMilliseconds", "responseBytes", "allocatedBytes", "definitions", "reusedDefinitions" })
            {
                var values = present.Select(sample => sample[field]?.GetValue<double>()).Where(value => value is not null).Select(value => value!.Value).Order().ToArray();
                if (values.Length == 0) continue;
                aggregate[field] = Round(values[values.Length / 2]);
                if (field.EndsWith("Milliseconds", StringComparison.Ordinal) && values.Length > 1)
                    aggregate[field.Replace("Milliseconds", "SpreadPercent", StringComparison.Ordinal)] = Round(100 * (values[^1] - values[0]) / Math.Max(values[values.Length / 2], 1e-9));
            }
            if (present[0]["edit"] is { } edit) aggregate["edit"] = edit.DeepClone();
            // Phases from the median-time run of this scenario.
            var median = present.OrderBy(sample => sample["invokeMilliseconds"]!.GetValue<double>()).ElementAt(present.Length / 2);
            if (median["phases"] is { } phases) aggregate["phases"] = phases.DeepClone();
            if (median["gc"] is { } gc) aggregate["gc"] = gc.DeepClone();
            summary[scenario] = aggregate;
        }
        return summary;
    }

    private static void PrintTable(JsonObject projects)
    {
        Console.WriteLine();
        Console.WriteLine($"{"project",-9} {"cold",10} {"warm",10} {"no-change",10} {"leaf edit",10} {"reused",8} {"response",10} {"warm alloc",11}  stable");
        foreach (var (name, node) in projects)
        {
            var p = node!.AsObject();
            var leaf = p["leafValueEdit"] ?? p["leafCommentEdit"];
            var reused = leaf?["definitions"] is null ? "—" : $"{leaf["reusedDefinitions"]}/{leaf["definitions"]}";
            Console.WriteLine($"{name,-9} {Ms(p, "cold", "invokeMilliseconds"),10} {Ms(p, "warm", "invokeMilliseconds"),10} {Ms(p, "noChangeRebuild", "invokeMilliseconds"),10} " +
                $"{(leaf is null ? "—" : Format(leaf["invokeMilliseconds"]!.GetValue<double>())),10} {reused,8} {Bytes(p["warm"]?["responseBytes"]),10} {Bytes(p["warm"]?["allocatedBytes"]),11}  " +
                $"{p["determinism"]?["stableAcrossRuns"]}");
        }
        Console.WriteLine();
    }

    // ---------------------------------------------------------------- budgets

    private static JsonObject Baseline(JsonObject results)
    {
        var budgets = new JsonObject
        {
            ["schema"] = "aetheris/build-perf-budgets/m0",
            ["note"] = "Machine-independent fields always gate; *Milliseconds fields gate only with --check-time on the recorded machine.",
            ["recordedOn"] = results["machine"]!.DeepClone(),
            ["recordedCommit"] = results["commit"]!.DeepClone(),
        };
        var projects = new JsonObject();
        foreach (var (name, node) in results["projects"]!.AsObject())
        {
            var p = node!.AsObject();
            var budget = new JsonObject
            {
                ["stepDataSha256"] = p["determinism"]?["stepDataSha256"]?.DeepClone(),
                ["meshSha256"] = p["determinism"]?["meshSha256"]?.DeepClone(),
                ["rebuildMatchesCold"] = p["determinism"]?["rebuildStepMatchesCold"]?.GetValue<bool>() == true && p["determinism"]?["rebuildMeshMatchesCold"]?.GetValue<bool>() == true,
            };
            foreach (var scenario in Scenarios)
            {
                if (p[scenario] is not JsonObject s) continue;
                var entry = new JsonObject
                {
                    ["invokeMilliseconds"] = s["invokeMilliseconds"]?.DeepClone(),
                    ["responseBytes"] = s["responseBytes"]?.DeepClone(),
                    ["allocatedBytes"] = s["allocatedBytes"]?.DeepClone(),
                };
                if (s["reusedDefinitions"] is { } reused) entry["reusedDefinitions"] = reused.DeepClone();
                budget[scenario] = entry;
            }
            projects[name] = budget;
        }
        budgets["projects"] = projects;
        return budgets;
    }

    private static bool Check(JsonObject budgets, JsonObject projects, bool checkTime, double timeTolerance)
    {
        const double allocationTolerance = 1.10, responseTolerance = 1.01;
        var failures = new List<string>();
        var improvements = new List<string>();
        foreach (var (name, budgetNode) in budgets["projects"]!.AsObject())
        {
            if (projects[name] is not JsonObject actual) continue;
            var budget = budgetNode!.AsObject();
            void Exact(string field, string? expected, string? got)
            {
                if (expected is not null && expected != got) failures.Add($"{name}: {field} changed ({expected[..12]} -> {got?[..Math.Min(12, got.Length)] ?? "none"}); geometry or display output differs from the baseline.");
            }
            Exact("STEP DATA hash", budget["stepDataSha256"]?.ToString(), actual["determinism"]?["stepDataSha256"]?.ToString());
            Exact("display mesh hash", budget["meshSha256"]?.ToString(), actual["determinism"]?["meshSha256"]?.ToString());
            if (actual["determinism"]?["stableAcrossRuns"]?.GetValue<bool>() == false) failures.Add($"{name}: output differs between runs.");
            if (budget["rebuildMatchesCold"]?.GetValue<bool>() == true &&
                (actual["determinism"]?["rebuildStepMatchesCold"]?.GetValue<bool>() == false || actual["determinism"]?["rebuildMeshMatchesCold"]?.GetValue<bool>() == false))
                failures.Add($"{name}: a rebuild with no geometry change no longer reproduces the cold build.");
            foreach (var scenario in Scenarios)
            {
                if (budget[scenario] is not JsonObject b || actual[scenario] is not JsonObject a) continue;
                void AtMost(string field, double tolerance)
                {
                    if (b[field] is null || a[field] is null) return;
                    double limit = b[field]!.GetValue<double>(), got = a[field]!.GetValue<double>();
                    string Show(double value) => field.EndsWith("Milliseconds", StringComparison.Ordinal) ? Format(value) : Bytes(JsonValue.Create(value));
                    if (got > limit * tolerance) failures.Add($"{name}/{scenario}: {field} {Show(got)} exceeds budget {Show(limit)} (×{tolerance}).");
                    else if (got < limit * 0.8) improvements.Add($"{name}/{scenario}: {field} {Show(got)} vs budget {Show(limit)}");
                }
                AtMost("responseBytes", responseTolerance);
                AtMost("allocatedBytes", allocationTolerance);
                if (checkTime) AtMost("invokeMilliseconds", timeTolerance);
                if (b["reusedDefinitions"] is { } r && a["reusedDefinitions"] is { } got && got.GetValue<double>() < r.GetValue<double>())
                    failures.Add($"{name}/{scenario}: reused definitions {got} below budget {r}.");
            }
        }
        foreach (var improvement in improvements) Console.WriteLine($"improved: {improvement} — consider --write-baseline");
        foreach (var failure in failures) Console.Error.WriteLine($"FAIL {failure}");
        Console.WriteLine(failures.Count == 0 ? "Build-perf budgets: pass." : $"Build-perf budgets: {failures.Count} failure(s).");
        return failures.Count == 0;
    }

    // ---------------------------------------------------------------- helpers

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

    private static string? FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Aetheris.slnx"))) return dir.FullName;
        return null;
    }

    private static string GitCommit(string repo)
    {
        try
        {
            var head = File.ReadAllText(Path.Combine(repo, ".git", "HEAD")).Trim();
            if (!head.StartsWith("ref: ", StringComparison.Ordinal)) return head[..12];
            var reference = Path.Combine(repo, ".git", head[5..]);
            return File.Exists(reference) ? File.ReadAllText(reference).Trim()[..12] : head[5..];
        }
        catch (IOException) { return "unknown"; }
    }

    private static string RuntimeInfo() => $"{System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription} {(Debugger.IsAttached ? "(debugger)" : "")}".Trim();
    private static double Round(double value) => Math.Round(value, 1);
    private static string Sha256(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static string Truncate(string? text, int length = 600) => text is null ? "" : text.Length <= length ? text : text[..length] + "…";
    private static string Ms(JsonObject p, string scenario, string field) => p[scenario]?[field] is { } v ? Format(v.GetValue<double>()) : "—";
    private static string Format(double ms) => ms >= 1000 ? $"{ms / 1000:0.00} s" : $"{ms:0} ms";
    private static string Bytes(JsonNode? node) => node is null ? "—" : node.GetValue<double>() switch
    {
        >= 1 << 30 => $"{node.GetValue<double>() / (1 << 30):0.0} GB",
        >= 1 << 20 => $"{node.GetValue<double>() / (1 << 20):0.0} MB",
        _ => $"{node.GetValue<double>() / 1024:0} KB",
    };
}
