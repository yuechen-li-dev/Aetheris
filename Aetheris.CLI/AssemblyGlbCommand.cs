using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.SheetMetal;

namespace Aetheris.CLI;

internal static class AssemblyGlbCommand
{
    internal const string Usage = "aetheris asm export-glb <assembly.firmament|assembly.firmasm> [out.glb] [--state Joint=value] [--materials appearance.json] [--json]";

    internal sealed record AppearanceInput(double Red = .48, double Green = .55, double Blue = .63,
        double Metallic = .8, double Roughness = .28, string? BaseColorTexture = null, double[]? TextureCoordinates = null);

    internal static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        { (args.Length == 0 ? stderr : stdout).WriteLine(Usage); return args.Length == 0 ? 1 : 0; }
        try
        {
            var source = Path.GetFullPath(args[0]);
            var output = Path.GetFullPath(Path.Combine("artifacts", "local", "glb", Path.GetFileNameWithoutExtension(source) + ".glb"));
            string? materialsPath = null; var json = false;
            var state = new Dictionary<string, double>(StringComparer.Ordinal);
            for (var i = 1; i < args.Length; i++)
            {
                if (i == 1 && !args[i].StartsWith('-')) output = Path.GetFullPath(args[i]);
                else if (args[i] == "--json") json = true;
                else if (args[i] == "--materials" && i + 1 < args.Length) materialsPath = Path.GetFullPath(args[++i]);
                else if (args[i] == "--state" && i + 1 < args.Length)
                {
                    var pair = args[++i].Split('=');
                    if (pair.Length != 2 || string.IsNullOrWhiteSpace(pair[0])
                        || !double.TryParse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                        || !double.IsFinite(value) || !state.TryAdd(pair[0], value))
                        throw new ArgumentException("GLB state requires unique Joint=finite-value (degrees or mm).");
                }
                else throw new ArgumentException("Unknown or incomplete GLB option: " + args[i]);
            }
            if (!Path.GetExtension(output).Equals(".glb", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Static presentation export writes .glb.");
            if (output.Equals(source, StringComparison.OrdinalIgnoreCase) || output.Equals(materialsPath, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("GLB output must differ from source inputs.");
            Dictionary<string, AssemblyGlbAppearance>? appearances = null;
            if (materialsPath is not null)
            {
                var input = JsonSerializer.Deserialize<Dictionary<string, AppearanceInput>>(File.ReadAllText(materialsPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new ArgumentException("GLB materials must be a definition-keyed object.");
                appearances = new(StringComparer.Ordinal);
                foreach (var (identity, m) in input)
                {
                    if (m is null) throw new ArgumentException("GLB material must be an object.");
                    byte[]? bytes = null; string? mime = null;
                    if (m.BaseColorTexture is not null)
                    {
                        var texturePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(materialsPath)!, m.BaseColorTexture));
                        if (output.Equals(texturePath, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("GLB output must differ from texture input.");
                        mime = Path.GetExtension(texturePath).ToLowerInvariant() switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", _ => throw new ArgumentException("GLB textures require PNG or JPEG.") };
                        bytes = File.ReadAllBytes(texturePath);
                    }
                    appearances.Add(identity, new(new(m.Red, m.Green, m.Blue, m.Metallic, m.Roughness), m.TextureCoordinates, bytes, mime));
                }
            }
            var watch = Stopwatch.StartNew();
            var compilation = Path.GetExtension(source).Equals(".firmasm", StringComparison.OrdinalIgnoreCase)
                ? new FirmamentAssemblyDocumentCompiler().CompileFile(source).Compilation
                : new AssemblyM1Pipeline().CompileFile(source, File.ReadAllText(source).Contains("Use SheetMetal.ProductFamilies", StringComparison.Ordinal) ? EnclosureProductFamilies.MaterializeAssemblyPart : null);
            if (!compilation.IsSuccess) throw new InvalidOperationException(string.Join("\n", compilation.Diagnostics.Select(d => d.Code + ": " + d.Message)));
            var compilationMilliseconds = watch.Elapsed.TotalMilliseconds; watch.Restart();
            var pose = AssemblyKinematics.Evaluate(compilation.Ir!, state);
            var display = AssemblyDisplayMeshExporter.Export(compilation, pose: pose);
            var meshMilliseconds = watch.Elapsed.TotalMilliseconds; watch.Restart();
            var bytesGlb = AssemblyGlbExporter.Serialize(display, appearances);
            var serializationMilliseconds = watch.Elapsed.TotalMilliseconds;
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllBytes(output, bytesGlb);
            var report = new { status = "exported", format = "glb", output, definitions = display.Definitions.Count,
                occurrences = display.Occurrences.Count, bytes = bytesGlb.Length, compilationMilliseconds, meshMilliseconds, serializationMilliseconds };
            stdout.WriteLine(json ? JsonSerializer.Serialize(report, CliRunner.JsonOptions) : $"GLB exported: {output}; {display.Definitions.Count} shared definitions, {display.Occurrences.Count} occurrences.");
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or JsonException or FormatException or OverflowException)
        { stderr.WriteLine("assembly-glb-export-failed: " + ex.Message); return 1; }
    }
}
