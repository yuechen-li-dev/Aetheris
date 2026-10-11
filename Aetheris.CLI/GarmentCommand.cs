using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Aetheris.Humanoid;
using Aetheris.Kernel.Firmament.Garment;

namespace Aetheris.CLI;

internal static class GarmentCommand
{
    internal const string Usage = "aetheris garment <inspect|build|drape> <garment.firmament> [--body gameplay-body.json] [--ticks 1..600] [--out-dir directory] [--json]";

    internal static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            stdout.WriteLine(Usage);
            return args.Length == 0 ? 1 : 0;
        }
        try
        {
            if (args.Length < 2 || args[0] is not ("inspect" or "build" or "drape")) throw new ArgumentException(Usage);
            string operation = args[0];
            string input = Path.GetFullPath(args[1]);
            bool json = false;
            string? bodyPath = null;
            string? outputDirectory = null;
            int ticks = 60;
            for (int i = 2; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--json": json = true; break;
                    case "--body" when i + 1 < args.Length: bodyPath = Path.GetFullPath(args[++i]); break;
                    case "--out-dir" when i + 1 < args.Length: outputDirectory = Path.GetFullPath(args[++i]); break;
                    case "--ticks" when i + 1 < args.Length && int.TryParse(args[++i], out ticks) && ticks is >= 1 and <= 600: break;
                    default: throw new ArgumentException("Unknown or incomplete garment option: " + args[i]);
                }
            }
            if (operation != "drape" && (bodyPath is not null || ticks != 60)) throw new ArgumentException("--body and --ticks belong to garment drape.");
            if (operation == "inspect" && outputDirectory is not null) throw new ArgumentException("inspect does not write output.");
            var watch = Stopwatch.StartNew();
            var compiled = GarmentCompiler.Compile(File.ReadAllText(input), input);
            if (!compiled.IsSuccess)
            {
                if (json) stdout.WriteLine(JsonSerializer.Serialize(new { success = false, domain = "Garment", diagnostics = compiled.Diagnostics }, CliRunner.JsonOptions));
                else foreach (var diagnostic in compiled.Diagnostics) stderr.WriteLine(diagnostic.Code + ": " + diagnostic.Message);
                return 1;
            }
            var garment = compiled.Garment!;
            GarmentDrapeResult? drape = null;
            HumanoidGameplayBody? body = null;
            if (operation == "drape")
            {
                if (bodyPath is null) throw new ArgumentException("garment-body-required: Bind the declared figure with --body gameplay-body.json.");
                body = HumanoidGameplayBody.Load(bodyPath);
                drape = GarmentDraping.Settle(garment, body, ticks);
            }
            if (operation != "inspect")
            {
                outputDirectory ??= Path.GetFullPath(Path.Combine("artifacts", "local", "garment", Path.GetFileNameWithoutExtension(input)));
                string[] names = ["garment.json", "garment.obj", "garment.usda", "patterns.svg", "body.obj", "evidence.json"];
                if (names.Select(name => Path.Combine(outputDirectory, name)).Any(path => path.Equals(input, StringComparison.OrdinalIgnoreCase)
                    || path.Equals(bodyPath, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ArgumentException("Garment output must not replace an input artifact.");
                }
                Directory.CreateDirectory(outputDirectory);
                Write("garment.json", GarmentExport.Artifact(garment, drape?.Snapshot));
                Write("garment.obj", GarmentExport.Obj(garment, drape?.Snapshot));
                Write("garment.usda", GarmentExport.Usd(garment, drape?.Snapshot));
                Write("patterns.svg", GarmentExport.PatternsSvg(garment));
                if (body is not null) Write("body.obj", GarmentExport.BodyObj(body));
            }
            bool qualified = drape is null || (drape.Metrics.Finite && drape.ReplayExact
                && drape.MaximumSeamGap <= .01f && drape.Metrics.MaximumSurfacePenetration <= .005f
                && drape.Metrics.MaximumStretch <= .15f);
            var report = new
            {
                success = qualified,
                domain = "Garment",
                name = garment.Source.Name,
                garment.Source.SourceHash,
                garment.Cloth.ContentKey,
                units = "m",
                upAxis = "Z",
                vertices = garment.Cloth.Definition.Positions.Length,
                triangles = garment.Cloth.Definition.Indices.Length / 3,
                pins = garment.Cloth.Definition.Pins.Length,
                seamConstraints = garment.Cloth.Definition.Stitches.Length,
                panels = garment.Panels.Select(panel => new { panel.Identity, panel.VertexCount, edges = panel.Edges.Keys }),
                features = garment.Source.Features,
                patterns = garment.Source.Patterns,
                drape = drape is null ? null : new
                {
                    settleTicks = ticks,
                    sewingTicks = 30,
                    snapshotTick = drape.Snapshot.Tick,
                    bodyArtifactHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(bodyPath!))),
                    drape.BodyId,
                    drape.BodyTopology,
                    drape.InitialSeamGap,
                    drape.MaximumSeamGap,
                    drape.Metrics,
                    drape.ReplayExact,
                    drape.ContactOutliers,
                    drape.StretchOutliers,
                    qualification = qualified ? "passed" : "failed; inspect the retained geometry and metrics",
                    limits = new[] { "Static Rest pose", "Explicit authored supports", "Discrete oriented body contact", "No CCD, friction or garment self-contact", "CPU body-contact backend" },
                },
                outputDirectory,
                elapsedMilliseconds = watch.Elapsed.TotalMilliseconds,
            };
            string evidence = JsonSerializer.Serialize(report, CliRunner.JsonOptions);
            if (outputDirectory is not null) Write("evidence.json", evidence);
            stdout.WriteLine(json ? evidence : $"Garment {garment.Source.Name}: {report.vertices} vertices, {report.triangles} triangles, {report.seamConstraints} seam constraints; "
                + (qualified ? "passed" : "drape qualification failed") + (outputDirectory is null ? "." : "; artifacts: " + outputDirectory));
            return qualified ? 0 : 2;

            void Write(string name, string text) => File.WriteAllText(Path.Combine(outputDirectory!, name), text, new UTF8Encoding(false));
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            stderr.WriteLine("garment-command-failed: " + exception.Message);
            return 1;
        }
    }
}
