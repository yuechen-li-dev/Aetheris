using System.Diagnostics;
using System.Text.Json;
using Aetheris.Kernel.Firmament.Drawing;

namespace Aetheris.CLI;

internal static class Presentation3DCommand
{
    internal const string Usage = "aetheris presentation compile <deck.json> [out.pptx] [--json]";
    private sealed record Deck(string Title, IReadOnlyList<Model3DSlide> Slides,
        double WidthMm = 338.6667, double HeightMm = 190.5);

    internal static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h") { stdout.WriteLine(Usage); return args.Length == 0 ? 1 : 0; }
        try
        {
            if (args[0] != "compile" || args.Length < 2) throw new ArgumentException(Usage);
            var input = Path.GetFullPath(args[1]);
            var output = Path.GetFullPath(Path.Combine("artifacts", "local", "presentations", Path.GetFileNameWithoutExtension(input) + ".pptx"));
            var json = false;
            for (var i = 2; i < args.Length; i++)
            {
                if (i == 2 && !args[i].StartsWith('-')) output = Path.GetFullPath(args[i]);
                else if (args[i] == "--json") json = true;
                else throw new ArgumentException("Unknown presentation option: " + args[i]);
            }
            if (!Path.GetExtension(output).Equals(".pptx", StringComparison.OrdinalIgnoreCase) || output.Equals(input, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Presentation output must be a separate .pptx file.");
            var deck = JsonSerializer.Deserialize<Deck>(File.ReadAllText(input), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new ArgumentException("A presentation deck object is required.");
            if (deck.Slides is null || deck.Slides.Any(s => s is null || s.Models is null || s.Models.Any(m => m is null)))
                throw new ArgumentException("Every slide requires an explicit Models list.");
            var directory = Path.GetDirectoryName(input)!;
            var slides = deck.Slides.Select(s => s with { Models = s.Models.Select(m => m with
                { Source = Path.GetFullPath(Path.Combine(directory, m.Source)), Preview = Path.GetFullPath(Path.Combine(directory, m.Preview)) }).ToArray() }).ToArray();
            var timer = Stopwatch.StartNew();
            DrawingPptxWriter.WriteModel3DDeck(slides, output, deck.Title, deck.WidthMm, deck.HeightMm);
            var report = new { status = "compiled", output, slides = slides.Length, embeddedModels = slides.Sum(s => s.Models.Count), bytes = new FileInfo(output).Length, compilationMilliseconds = timer.Elapsed.TotalMilliseconds };
            stdout.WriteLine(json ? JsonSerializer.Serialize(report, CliRunner.JsonOptions) : $"Presentation compiled: {output}; {report.embeddedModels} embedded 3D models.");
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or JsonException or OverflowException)
        { stderr.WriteLine("presentation-3d-compile-failed: " + ex.Message); return 1; }
    }
}
