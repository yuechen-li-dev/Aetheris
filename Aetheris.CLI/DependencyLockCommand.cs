using System.Text;
using System.Text.Json;
using Aetheris.Kernel.Firmament.Modules;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.CLI;

internal static class DependencyLockCommand
{
    internal static int Run(string[] args, TextWriter stdout, TextWriter stderr, JsonSerializerOptions options)
    {
        const string usage = "Usage: aetheris lock <root.firmament|root.firmasm> [--update] [--json]";
        if (args.Length == 0 || args[0] is "--help" or "-h")
        { (args.Length == 0 ? stderr : stdout).WriteLine(usage); return args.Length == 0 ? 1 : 0; }
        var json = args.Contains("--json");
        if (args.Skip(1).Any(a => a is not ("--update" or "--json"))) { stderr.WriteLine(usage); return 1; }
        var inspected = FirmamentDependencies.InspectFile(args[0], args.Contains("--update"));
        if (!inspected.IsSuccess)
        {
            var diagnostics = Present(inspected.Diagnostics, args[0]);
            if (json) stdout.WriteLine(JsonSerializer.Serialize(new { success = false, diagnostics }, options));
            else Write(diagnostics, stderr);
            return 1;
        }
        var path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0]))!, FirmamentDependencyLock.FileName);
        // Inspect the complete graph before replacing the receipt; compilation never writes here.
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, inspected.Manifest!.ToToml(), new UTF8Encoding(false)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        if (json) stdout.WriteLine(JsonSerializer.Serialize(new { success = true, path, dependencies = inspected.Manifest!.Dependencies }, options));
        else stdout.WriteLine("Locked dependencies: " + path);
        return 0;
    }

    internal static bool Verify(string source, bool required, bool json, TextWriter stdout, TextWriter stderr, JsonSerializerOptions options, string command)
    {
        var diagnostics = Present(FirmamentDependencies.VerifyFile(source, required), source);
        if (diagnostics.Count == 0) return true;
        if (json) stdout.WriteLine(command == "validate"
            ? JsonSerializer.Serialize(new { command, success = false, status = "invalid", diagnostics,
                firmamentV2Validation = new { status = "invalid", validationLevel = "Rejected", diagnostics } }, options)
            : JsonSerializer.Serialize(new { command, success = false, status = "invalid", diagnostics }, options));
        else Write(diagnostics, stderr);
        return false;
    }

    internal static IReadOnlyList<FirmamentV2ValidationDiagnostic> Present(IEnumerable<AssemblyDiagnostic> diagnostics, string source) => diagnostics.Select(d =>
    {
        var file = d.SourceSpan?.Source ?? source;
        var span = d.SourceSpan is {} s ? new FirmamentV2SourceSpan(s.Start,s.Length) : null;
        int? line = null, column = null;
        if (span is not null && File.Exists(file))
        {
            var prefix = File.ReadAllText(file); prefix = prefix[..Math.Min(span.Start,prefix.Length)];
            line = 1 + prefix.Count(c => c == '\n'); column = prefix.Length - prefix.LastIndexOf('\n');
        }
        return new FirmamentV2ValidationDiagnostic(d.Code,d.Severity.ToString().ToLowerInvariant(),d.Message)
        { SourceFile = file, SourceSpan = span, Line = line, Column = column, Stage = "Dependency binding", IsOriginalSource = true, LocationNote = d.LocationNote };
    }).ToArray();

    static void Write(IEnumerable<FirmamentV2ValidationDiagnostic> diagnostics, TextWriter writer)
    {
        foreach (var d in diagnostics) writer.WriteLine($"{d.SourceFile}:{d.Line?.ToString() ?? "?"}:{d.Column?.ToString() ?? "?"}: {d.Severity} {d.Code}: {d.Message}");
    }
}
