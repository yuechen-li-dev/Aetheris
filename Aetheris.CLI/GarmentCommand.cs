using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Aetheris.Humanoid;
using Aetheris.Kernel.Firmament.Garment;
using Aetheris.Kernel.Firmament.Humanoid;

namespace Aetheris.CLI;

internal static class GarmentCommand
{
    internal const string Usage = "aetheris garment <pose|preview|inspect|build|drape> <garment.firmament> [--body gameplay-body.json] [--figure Anatolia] [--pose namedPose] [--ticks 1..600] [--substeps 1..64] [--iterations 1..64] [--until-settled] [--out-dir directory] [--json]\naetheris garment import-garmentcode <specification.json> [--out-dir directory] [--json]";

    internal static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            stdout.WriteLine(Usage);
            return args.Length == 0 ? 1 : 0;
        }
        try
        {
            if (args[0] == "import-garmentcode")
            {
                return ImportGarmentCode(args, stdout, stderr);
            }
            if (args.Length < 2 || args[0] is not ("pose" or "preview" or "inspect" or "build" or "drape")) throw new ArgumentException(Usage);
            string operation = args[0];
            string input = Path.GetFullPath(args[1]);
            bool json = false;
            string? bodyPath = null;
            string? outputDirectory = null;
            string figureName = "Anatolia";
            string? poseName = null;
            int ticks = 60;
            var drapeSettings = new GarmentDrapeSettings();
            bool drapeOptions = false;
            for (int i = 2; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--json": json = true; break;
                    case "--body" when i + 1 < args.Length: bodyPath = Path.GetFullPath(args[++i]); break;
                    case "--figure" when i + 1 < args.Length: figureName = args[++i]; break;
                    case "--pose" when i + 1 < args.Length: poseName = args[++i]; break;
                    case "--out-dir" when i + 1 < args.Length: outputDirectory = Path.GetFullPath(args[++i]); break;
                    case "--ticks" when i + 1 < args.Length && int.TryParse(args[++i], out ticks) && ticks is >= 1 and <= 600: break;
                    case "--substeps" when i + 1 < args.Length && int.TryParse(args[++i], out int substeps) && substeps is >= 1 and <= 64:
                        drapeSettings = drapeSettings with { Substeps = substeps };
                        drapeOptions = true;
                        break;
                    case "--iterations" when i + 1 < args.Length && int.TryParse(args[++i], out int iterations) && iterations is >= 1 and <= 64:
                        drapeSettings = drapeSettings with { Iterations = iterations };
                        drapeOptions = true;
                        break;
                    case "--until-settled":
                        drapeSettings = drapeSettings with { StopWhenSettled = true };
                        drapeOptions = true;
                        break;
                    default: throw new ArgumentException("Unknown or incomplete garment option: " + args[i]);
                }
            }
            if (operation != "drape" && ticks != 60) throw new ArgumentException("--ticks belongs to garment drape.");
            if (operation != "drape" && drapeOptions) throw new ArgumentException("Solver and settling options belong to garment drape.");
            if (operation == "inspect" && outputDirectory is not null) throw new ArgumentException("inspect does not write output.");
            if (operation != "pose" && poseName is not null)
            {
                throw new ArgumentException("--pose belongs to garment pose; drape selects its pose in Firmament.");
            }
            var watch = Stopwatch.StartNew();
            string source = File.ReadAllText(input);
            HumanoidGameplayBody? body = bodyPath is null ? null : HumanoidGameplayBody.Load(bodyPath);
            GarmentFigureBinding? binding = body is null ? null : new(figureName, body,
                Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(bodyPath!))));
            if (operation == "pose")
            {
                if (body is null || poseName is null)
                {
                    throw new ArgumentException("garment pose requires --body and --pose.");
                }
                var document = HumanoidPoseAuthoring.Parse(source);
                var recipe = document.Resolve(poseName, figureName);
                var solved = HumanoidPoseAuthoring.Solve(recipe, body);
                var surface = body.Evaluate(solved);
                outputDirectory ??= Path.GetFullPath(Path.Combine("artifacts", "local", "garment", Path.GetFileNameWithoutExtension(input), "poses", poseName));
                if (new[] { "body.obj", "evidence.json" }.Any(name => Path.Combine(outputDirectory, name).Equals(input, StringComparison.OrdinalIgnoreCase)
                    || Path.Combine(outputDirectory, name).Equals(bodyPath, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ArgumentException("Pose output must not replace an input artifact.");
                }
                Directory.CreateDirectory(outputDirectory);
                Write("body.obj", GarmentExport.BodyObj(body, surface.Positions));
                string poseEvidence = JsonSerializer.Serialize(new
                {
                    success = surface.Evidence.IsAdmissible,
                    stage = "posed-figure",
                    body.Id,
                    body.Skeleton.SkeletonId,
                    body.Skeleton.RestPoseId,
                    bodyArtifactHash = binding!.ArtifactHash,
                    sourceHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(source))),
                    recipe,
                    solved.SemanticResiduals,
                    surface = surface.Evidence,
                    outputDirectory,
                    simulated = false,
                }, CliRunner.JsonOptions);
                Write("evidence.json", poseEvidence);
                stdout.WriteLine(poseEvidence);
                return surface.Evidence.IsAdmissible ? 0 : 2;
            }
            if (operation == "preview")
            {
                var parsed = GarmentAuthoring.Parse(source, input, binding);
                if (!parsed.IsSuccess)
                {
                    if (json) stdout.WriteLine(JsonSerializer.Serialize(new { success = false, domain = "Garment", diagnostics = parsed.Diagnostics }, CliRunner.JsonOptions));
                    else foreach (var diagnostic in parsed.Diagnostics) stderr.WriteLine(diagnostic.Code + ": " + diagnostic.Message);
                    return 1;
                }
                outputDirectory ??= Path.GetFullPath(Path.Combine("artifacts", "local", "garment", Path.GetFileNameWithoutExtension(input), "preview"));
                if (new[] { "patterns.svg", "evidence.json" }.Any(name => Path.Combine(outputDirectory, name).Equals(input, StringComparison.OrdinalIgnoreCase)
                    || Path.Combine(outputDirectory, name).Equals(bodyPath, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ArgumentException("Garment output must not replace an input artifact.");
                }
                Directory.CreateDirectory(outputDirectory);
                Write("patterns.svg", GarmentPatternPreview.Svg(parsed.Source!));
                string previewEvidence = JsonSerializer.Serialize(new
                {
                    success = true,
                    domain = "Garment",
                    stage = "resolved-patterns",
                    parsed.Source!.SourceHash,
                    figureFits = parsed.Source.FigureFits,
                    seams = GarmentPatternInspection.Inspect(parsed.Source),
                    outputDirectory,
                    meshed = false,
                    simulated = false,
                }, CliRunner.JsonOptions);
                Write("evidence.json", previewEvidence);
                stdout.WriteLine(json ? previewEvidence : "Pattern preview: " + Path.Combine(outputDirectory, "patterns.svg"));
                return 0;
            }
            var compiled = GarmentCompiler.Compile(source, input, binding);
            if (!compiled.IsSuccess)
            {
                if (json) stdout.WriteLine(JsonSerializer.Serialize(new { success = false, domain = "Garment", diagnostics = compiled.Diagnostics }, CliRunner.JsonOptions));
                else foreach (var diagnostic in compiled.Diagnostics) stderr.WriteLine(diagnostic.Code + ": " + diagnostic.Message);
                return 1;
            }
            var garment = compiled.Garment!;
            GarmentDrapeResult? drape = null;
            if (operation == "drape")
            {
                if (bodyPath is null) throw new ArgumentException("garment-body-required: Bind the declared figure with --body gameplay-body.json.");
                drape = GarmentDraping.Settle(garment, body!, ticks, progress => stderr.WriteLine(
                    FormattableString.Invariant($"garment {progress.Phase}: {progress.Completed}/{progress.Total} ({progress.ElapsedSeconds:F1}s)")), drapeSettings);
            }
            if (operation != "inspect")
            {
                outputDirectory ??= Path.GetFullPath(Path.Combine("artifacts", "local", "garment", Path.GetFileNameWithoutExtension(input)));
                string[] names = ["garment.json", "garment.obj", "garment.usda", "patterns.svg", "body.obj", "evidence.json",
                    "guided-garment.obj", "guided-body.obj",
                    "fitting-garment.obj", "fitting-body.obj", "midway-garment.obj", "midway-body.obj", "wearing-garment.obj", "wearing-body.obj"];
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
                foreach (var checkpoint in drape?.FittingCheckpoints ?? [])
                {
                    Write(checkpoint.Name + "-garment.obj", GarmentExport.Obj(garment, checkpoint.Snapshot));
                }
                if (body is not null)
                {
                    var positions = drape?.BodyPositions;
                    if (positions is null && garment.Source.Pose is not null && garment.Source.Pose != "Rest")
                    {
                        var recipe = garment.Source.Posing!.Resolve(garment.Source.Pose, garment.Source.Figure!);
                        positions = body.EvaluatePositions(HumanoidPoseAuthoring.Solve(recipe, body));
                    }
                    Write("body.obj", GarmentExport.BodyObj(body, positions));
                    foreach (var frame in drape?.Frames ?? [])
                    {
                        Write(frame.Name + "-garment.obj", GarmentExport.Obj(garment, frame.Snapshot));
                        Write(frame.Name + "-body.obj", GarmentExport.BodyObj(body, frame.BodyPositions));
                    }
                    foreach (var checkpoint in drape?.FittingCheckpoints ?? [])
                    {
                        Write(checkpoint.Name + "-body.obj", GarmentExport.BodyObj(body, checkpoint.BodyPositions));
                    }
                }
            }
            bool qualified = drape is null || (drape.ReplayExact && drape.TransitionReplayExact
                && drape.GuidesReleased
                && GarmentDrapeLimits.Accept(drape.Metrics, drape.MaximumSeamGap)
                && drape.Frames.All(frame => frame.BodyEvidence.IsAdmissible
                    && GarmentDrapeLimits.Accept(frame.Metrics, frame.MaximumSeamGap)));
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
                meshQuality = GarmentMeshInspection.Measure(garment),
                pins = garment.Cloth.Definition.Pins.Length,
                seamConstraints = garment.Cloth.Definition.Stitches.Length,
                panels = garment.Panels.Select(panel => new { panel.Identity, panel.VertexCount, edges = panel.Edges.Keys }),
                features = garment.Source.Features,
                figureFits = garment.Source.FigureFits,
                fittingGuides = garment.FittingGuides.Select(guide => new
                {
                    guide.ConceptPlane,
                    guide.ReleaseAfter,
                    guide.Constraint.Id,
                    guide.Constraint.Vertices,
                    normal = new[] { guide.Constraint.Normal.X, guide.Constraint.Normal.Y, guide.Constraint.Normal.Z },
                    guide.Constraint.Offset,
                    guide.Constraint.Compliance,
                }),
                posing = garment.Source.Posing,
                transition = garment.Source.Transition,
                patterns = garment.Source.Patterns,
                drape = drape is null ? null : new
                {
                    settleTicks = ticks,
                    sewingTicks = 30,
                    drape.CompletedSettlingTicks,
                    drape.StopReason,
                    drape.Settings,
                    drape.Samples,
                    drape.GuidesReleased,
                    fittingCheckpoints = drape.FittingCheckpoints.Select(checkpoint => new { checkpoint.Name, checkpoint.Snapshot.Tick }),
                    snapshotTick = drape.Snapshot.Tick,
                    bodyArtifactHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(bodyPath!))),
                    drape.BodyId,
                    drape.BodyTopology,
                    drape.InitialSeamGap,
                    drape.MaximumSeamGap,
                    drape.Metrics,
                    drape.ReplayExact,
                    drape.TransitionReplayExact,
                    drape.MaximumBodySubstepTravel,
                    frames = drape.Frames.Select(frame => new
                    {
                        frame.Name,
                        frame.MotionSeconds,
                        tick = frame.Snapshot.Tick,
                        frame.Metrics,
                        frame.MaximumSeamGap,
                        frame.StretchOutliers,
                        frame.BodyEvidence,
                        frame.Joints,
                    }),
                    drape.ContactOutliers,
                    drape.SurfaceContactOutliers,
                    drape.StretchOutliers,
                    qualification = qualified ? "passed" : "failed; inspect the retained geometry and metrics",
                    limits = new[] { "Authored one-way body poses", "Explicit authored supports", "Discrete oriented body contact", "No CCD, friction or garment self-contact", "CPU body-contact backend", "Checkpoint qualification does not certify every intermediate surface" },
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

    private static int ImportGarmentCode(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException(Usage);
        }
        string input = Path.GetFullPath(args[1]);
        string output = Path.GetFullPath(Path.Combine("artifacts", "local", "garment", Path.GetFileNameWithoutExtension(input)));
        bool json = false;
        for (int i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--out-dir" when i + 1 < args.Length:
                    output = Path.GetFullPath(args[++i]);
                    break;
                default:
                    throw new ArgumentException("Unknown or incomplete import option: " + args[i]);
            }
        }
        string source = File.ReadAllText(input);
        var imported = GarmentCodeImport.Import(source, input);
        if (!imported.IsSuccess)
        {
            if (json)
            {
                stdout.WriteLine(JsonSerializer.Serialize(new { success = false, imported.Diagnostics }, CliRunner.JsonOptions));
            }
            else
            {
                foreach (var diagnostic in imported.Diagnostics)
                {
                    stderr.WriteLine(diagnostic.Code + ": " + diagnostic.Message);
                }
            }
            return 1;
        }
        string[] files = ["garment.firmament", "specification.json", "patterns.svg", "import-evidence.json"];
        if (files.Select(file => Path.Combine(output, file)).Any(path => path.Equals(input, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Import output must not replace the input specification.");
        }
        var parsed = GarmentAuthoring.Parse(imported.Source!, input);
        string evidence = JsonSerializer.Serialize(new
        {
            success = true,
            stage = "imported-patterns",
            imported.InputHash,
            imported.Panels,
            seams = GarmentPatternInspection.Inspect(parsed.Source!),
            sourceSpecification = input,
            outputDirectory = output,
            meshed = false,
            simulated = false,
            limits = new[] { "Resolved patterns only", "Fabric and grain use explicit Aetheris defaults",
                "Placement is retained, not fitted to a new body", "Keep the source specification's license with derivatives" },
        }, CliRunner.JsonOptions);
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "garment.firmament"), imported.Source, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(output, "specification.json"), source, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(output, "patterns.svg"), GarmentPatternPreview.Svg(parsed.Source!), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(output, "import-evidence.json"), evidence, new UTF8Encoding(false));
        stdout.WriteLine(json ? evidence : "Imported sewing pattern: " + Path.Combine(output, "garment.firmament"));
        return 0;
    }
}
