using System.Globalization;
using System.Text.Json;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.CLI;

internal static class AssemblyMotionCommand
{
    internal const string Usage = "aetheris asm motion <assembly.firmament|assembly.firmasm> --clip Name --time seconds [--endpoint] [--json]";
    internal static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        { (args.Length == 0 ? stderr : stdout).WriteLine(Usage); return args.Length == 0 ? 1 : 0; }
        try
        {
            string? clip = null; double? time = null; var endpoint = false; var json = false;
            for (var i = 1; i < args.Length; i++)
            {
                if (args[i] == "--clip" && i + 1 < args.Length && clip is null) clip = args[++i];
                else if (args[i] == "--time" && i + 1 < args.Length && time is null
                    && double.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && double.IsFinite(seconds)) time = seconds;
                else if (args[i] == "--endpoint") endpoint = true;
                else if (args[i] == "--json") json = true;
                else throw new ArgumentException("Unknown or invalid Motion option: " + args[i]);
            }
            if (clip is null || time is null) throw new ArgumentException("Motion sampling requires --clip Name and --time finite-seconds.");
            var source = Path.GetFullPath(args[0]);
            var compiled = new AssemblyM1Pipeline().CompileFile(source);
            var pose = compiled.IsSuccess ? AssemblyMotion.Evaluate(compiled.Ir!, clip, time.Value, endpoint) : null;
            var diagnostics = compiled.Diagnostics.Concat(pose?.Diagnostics ?? []).ToArray();
            var success = compiled.IsSuccess && pose?.IsSuccess == true;
            if (json) stdout.WriteLine(JsonSerializer.Serialize(new { success, source, clip, timeSeconds = time, state = pose?.State,
                transforms = pose?.Instances.Select(i => new { id = i.StableId, path = i.Path.ToString(), matrix = i.ResolvedTransform?.Matrix }),
                diagnostics }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }));
            else
            {
                foreach (var diagnostic in diagnostics)
                    (diagnostic.Severity == AssemblyDiagnosticSeverity.Error ? stderr : stdout).WriteLine($"{diagnostic.SourceSpan?.Source ?? source}{(diagnostic.SourceSpan is { } span ? $"@{span.Start}+{span.Length}" : "")}: {diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}");
                if (success) stdout.WriteLine($"Motion {clip} at {time.Value.ToString("R", CultureInfo.InvariantCulture)}s: {pose!.Instances.Count} occurrence transforms; shared geometry retained.");
            }
            return success ? 0 : 1;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        { stderr.WriteLine("assembly-motion-command-invalid: " + e.Message); return 1; }
    }
}
