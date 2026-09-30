using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.SheetMetal;

namespace Aetheris.CLI;

internal static class AssemblyUsdCommand
{
    internal const string Usage = "aetheris asm export-usd <assembly.firmament|assembly.firmasm> [out.usda] [--state Joint=value] [--sample time:Joint=value,Joint=value] [--materials appearance.json] [--evidence evidence.json] [--json]";

    internal static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h") { (args.Length == 0 ? stderr : stdout).WriteLine(Usage); return args.Length == 0 ? 1 : 0; }
        try
        {
            var path = args[0];
            var output = Path.Combine("artifacts", "local", "usd", Path.GetFileNameWithoutExtension(path) + ".usda");
            var state = new Dictionary<string, double>(StringComparer.Ordinal);
            var samples = new List<AssemblyUsdSample>();
            string? materialPath = null, evidencePath = null;
            var json = false;
            for (var i = 1; i < args.Length; i++)
            {
                if (i == 1 && !args[i].StartsWith('-')) output = args[i];
                else if (args[i] == "--json") json = true;
                else if (args[i] == "--state" && i + 1 < args.Length) ReadState(args[++i], state);
                else if (args[i] == "--sample" && i + 1 < args.Length)
                {
                    var value = args[++i]; var colon = value.IndexOf(':');
                    if (colon < 0) throw new ArgumentException("USD sample requires time:Joint=value,Joint=value.");
                    var sampleState = new Dictionary<string, double>(StringComparer.Ordinal);
                    ReadState(value[(colon + 1)..], sampleState);
                    samples.Add(new(double.Parse(value[..colon], CultureInfo.InvariantCulture), sampleState));
                }
                else if (args[i] == "--materials" && i + 1 < args.Length) materialPath = args[++i];
                else if (args[i] == "--evidence" && i + 1 < args.Length) evidencePath = args[++i];
                else throw new ArgumentException("Unknown or incomplete USD option: " + args[i]);
            }
            if (!string.Equals(Path.GetExtension(output), ".usda", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("X0 writes readable .usda files; use external usdcat to convert to .usdc.");
            var materials = materialPath is null ? null : JsonSerializer.Deserialize<Dictionary<string, AssemblyUsdMaterial>>(File.ReadAllText(materialPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var compilation = Path.GetExtension(path).Equals(".firmasm", StringComparison.OrdinalIgnoreCase)
                ? new FirmamentAssemblyDocumentCompiler().CompileFile(path).Compilation
                : new AssemblyM1Pipeline().CompileFile(path, File.ReadAllText(path).Contains("Use SheetMetal.ProductFamilies", StringComparison.Ordinal) ? EnclosureProductFamilies.MaterializeAssemblyPart : null);
            if (!compilation.IsSuccess) throw new InvalidOperationException(string.Join("\n", compilation.Diagnostics.Select(d => d.Code + ": " + d.Message)));
            var pose = AssemblyKinematics.Evaluate(compilation.Ir!, state);
            var watch = Stopwatch.StartNew();
            var mesh = AssemblyDisplayMeshExporter.Export(compilation, pose: pose);
            var meshMilliseconds = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            var usd = AssemblyUsdExporter.Serialize(compilation.Ir!, mesh, new(state, samples, materials), pose.State);
            var serializationMilliseconds = watch.Elapsed.TotalMilliseconds;
            var fullOutput = Path.GetFullPath(output);
            if (fullOutput.Equals(Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)
                || (materialPath is not null && fullOutput.Equals(Path.GetFullPath(materialPath), StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("USD output must differ from source inputs.");
            var fullEvidence = evidencePath is null ? null : Path.GetFullPath(evidencePath);
            if (fullEvidence is not null && (fullEvidence.Equals(fullOutput, StringComparison.OrdinalIgnoreCase)
                || fullEvidence.Equals(Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)
                || (materialPath is not null && fullEvidence.Equals(Path.GetFullPath(materialPath), StringComparison.OrdinalIgnoreCase))))
                throw new ArgumentException("Evidence output must differ from USD and source inputs.");
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutput)!);
            File.WriteAllText(fullOutput, usd, new System.Text.UTF8Encoding(false));
            var report = new { success = true, output = fullOutput, units = "mm", metersPerUnit = .001, upAxis = "Z", handedness = "rightHanded", definitions = mesh.Definitions.Count,
                occurrences = mesh.Occurrences.Count, joints = compilation.Ir!.Joints?.Count ?? 0, samples = samples.Count,
                meshMilliseconds, serializationMilliseconds, bytes = new FileInfo(fullOutput).Length, state = pose.State };
            if (fullEvidence is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullEvidence)!);
                File.WriteAllText(fullEvidence, JsonSerializer.Serialize(new { report, mesh, joints = compilation.Ir.Joints, geometry = compilation.Geometry!.Artifact,
                    samplePoses = samples.Select(s => new { s.Time, pose = AssemblyKinematics.Evaluate(compilation.Ir, s.State) }) }, CliRunner.JsonOptions));
            }
            stdout.WriteLine(json ? JsonSerializer.Serialize(report, CliRunner.JsonOptions) : $"USD exported: {fullOutput}; {mesh.Definitions.Count} shared definitions, {mesh.Occurrences.Count} occurrences.");
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or JsonException or FormatException or OverflowException)
        { stderr.WriteLine("assembly-usd-export-failed: " + ex.Message); return 1; }
    }

    private static void ReadState(string value, Dictionary<string, double> state)
    {
        foreach (var entry in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = entry.Split('=');
            if (pair.Length != 2 || string.IsNullOrWhiteSpace(pair[0]) || !double.TryParse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var scalar) || !double.IsFinite(scalar))
                throw new ArgumentException("USD state requires Joint=finite-value (degrees or mm).");
            if (!state.TryAdd(pair[0], scalar)) throw new ArgumentException("Duplicate USD state: " + pair[0]);
        }
    }
}
