using System.Globalization;
using System.Text.Json;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Firmament;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Kernel.Firmament.Modules;

namespace Aetheris.CLI;

internal static class ImportedFeaturesCommand
{
    const string Usage = "Usage: aetheris imported-features inspect <part.step> [--import-tolerance <mm>] [--json]\n"
        + "       aetheris imported-features plan <part.step> --candidate <id> --as <name> (--offset <du> <dv> | --to <x> <y> <z> | --remove) --geometry-only [--clearance <mm>] [--source-out <file.firmament>] [--import-tolerance <mm>] [--json]";

    internal static int Run(string[] args, TextWriter stdout, TextWriter stderr, JsonSerializerOptions options)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        { (args.Length == 0 ? stderr : stdout).WriteLine(Usage); return args.Length == 0 ? 1 : 0; }
        var json = args.Contains("--json");
        try
        {
            if (args.Length < 2 || args[0] is not ("inspect" or "plan")) throw new ArgumentException(Usage);
            var input = Path.GetFullPath(args[1]);
            string? candidate = null, alias = null, output = null;
            Point3D? target = null; (double X, double Y)? offset = null;
            var remove = false; var geometryOnly = false; var tolerance = .001; var clearance = .1;
            var seen = new HashSet<string>();
            for (var i = 2; i < args.Length; i++)
            {
                var flag = args[i];
                if (!seen.Add(flag)) throw new ArgumentException("Duplicate option: " + flag);
                string Next() => ++i < args.Length ? args[i] : throw new ArgumentException("Missing value: " + flag);
                double Number() => double.TryParse(Next(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n)
                    ? n : throw new ArgumentException("Expected finite millimetres: " + flag);
                switch (flag)
                {
                    case "--json": break;
                    case "--candidate": candidate = Next(); break;
                    case "--as": alias = Next(); break;
                    case "--source-out": output = Next(); break;
                    case "--geometry-only": geometryOnly = true; break;
                    case "--remove": remove = true; break;
                    case "--import-tolerance": tolerance = Number(); break;
                    case "--clearance": clearance = Number(); break;
                    case "--to": target = new(Number(), Number(), Number()); break;
                    case "--offset": offset = (Number(), Number()); break;
                    default: throw new ArgumentException("Unknown option: " + flag);
                }
            }
            if (args[0] == "inspect" && seen.Any(f => f is not ("--json" or "--import-tolerance")))
                throw new ArgumentException("Inspection does not consume edit options.");
            var inspected = FirmamentImportedFeatures.Inspect(File.ReadAllText(input), tolerance);
            if (!inspected.IsSuccess) return Failure(inspected.Diagnostics.Select(d => FirmamentBuildAdmissionValidation.Present(d, input)));
            var inspection = inspected.Value;
            if (args[0] == "inspect")
            {
                if (json) stdout.WriteLine(JsonSerializer.Serialize(new { success = true, admission = "InspectionOnly", input, inspection }, options));
                else
                {
                    stdout.WriteLine($"Source {inspection.SourceHash}; qualified: {inspection.Discovery.Qualified}");
                    foreach (var c in inspection.Discovery.Candidates)
                        stdout.WriteLine($"{c.Id}: diameter {c.Diameter:G9} mm; center {c.Center}; recognized {c.Recognized}; canPlan {c.CanPlan}. {string.Join(" | ", c.Diagnostics)}");
                }
                return 0;
            }
            if (candidate is null || alias is null || (target.HasValue ? 1 : 0) + (offset.HasValue ? 1 : 0) + (remove ? 1 : 0) != 1)
                throw new ArgumentException("Plan requires --candidate, --as and exactly one of --offset/--to/--remove.");
            if (offset is { } delta)
            {
                var selected = inspection.Discovery.Candidates.SingleOrDefault(c => c.Id == candidate);
                if (selected?.Center is not { } center) throw new ArgumentException("Candidate has no proved through-hole center; inspect again.");
                target = selected.AxisIndex switch { 0 => new(center.X, center.Y + delta.X, center.Z + delta.Y), 1 => new(center.X + delta.X, center.Y, center.Z + delta.Y), _ => new(center.X + delta.X, center.Y + delta.Y, center.Z) };
            }
            var outputPath = Path.GetFullPath(output ?? Path.Combine("artifacts", "local", "imported-features", "revision.firmament"));
            if (string.Equals(outputPath, input, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) throw new ArgumentException("Source output must not overwrite the imported STEP.");
            if (!Path.GetExtension(outputPath).Equals(".firmament", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Source output must use .firmament extension.");
            var directory = Path.GetDirectoryName(outputPath)!;
            string generatedSource; object details; IReadOnlyList<KernelDiagnostic> planDiagnostics;
            var reference = Path.GetRelativePath(directory, input).Replace('\\', '/');
            if (remove)
            {
                var plan = FirmamentImportedFeatures.PlanRemove(inspection, candidate, alias, geometryOnly, reference, directory, clearance);
                if (!plan.IsSuccess) return Failure(plan.Diagnostics.Select(d => FirmamentBuildAdmissionValidation.Present(d, "generated removal")));
                generatedSource = plan.Value.Source; details = plan.Value; planDiagnostics = plan.Diagnostics;
            }
            else
            {
                var plan = FirmamentImportedFeatures.PlanMove(inspection, candidate, alias, target!.Value, geometryOnly, reference, directory, clearance);
                if (!plan.IsSuccess) return Failure(plan.Diagnostics.Select(d => FirmamentBuildAdmissionValidation.Present(d, "generated revision")));
                generatedSource = plan.Value.Source; details = plan.Value; planDiagnostics = plan.Diagnostics;
            }
            var dependencies = FirmamentDependencies.InspectSource(generatedSource, outputPath);
            if (!dependencies.IsSuccess) return Failure(DependencyLockCommand.Present(dependencies.Diagnostics, outputPath));
            // The normal AST owns the field span; the generated source keeps
            // engineering intent while the generated receipt keeps byte identity.
            var pin = FirmamentSyntaxTraversal.Statements(FirmamentSyntaxParser.Parse(generatedSource).Document)
                .OfType<FieldSyntax>().Single(f => f.Name.Text == "textSha256");
            var authored = generatedSource.Remove(pin.Span.Start, pin.Span.Length);
            var lockPath = Path.Combine(directory, FirmamentDependencyLock.FileName);
            Directory.CreateDirectory(directory);
            var previousSource = File.Exists(outputPath) ? File.ReadAllBytes(outputPath) : null;
            var previousLock = File.Exists(lockPath) ? File.ReadAllBytes(lockPath) : null;
            var committed = false;
            object savedPlan = details;
            try
            {
                File.WriteAllText(lockPath, dependencies.Manifest!.ToToml());
                File.WriteAllText(outputPath, authored);
                var qualified = FirmamentBuildAndExport.CompileSource(authored, directory);
                if (!qualified.IsSuccess) return Failure(qualified.Diagnostics.Select(d => FirmamentBuildAdmissionValidation.Present(d, outputPath)));
                savedPlan = details switch
                {
                    ImportedFeatureMovePlan move => move with { Source = authored, Revision = qualified.Value.ImportedHoleRevision!, Export = qualified.Value },
                    ImportedFeatureRemovalPlan removal => removal with { Source = authored, Removal = qualified.Value.ImportedHoleRemoval!, Export = qualified.Value },
                    _ => throw new InvalidOperationException("Unknown imported edit plan.")
                };
                committed = true;
            }
            finally
            {
                if (!committed)
                {
                    if (previousSource is null) File.Delete(outputPath); else File.WriteAllBytes(outputPath, previousSource);
                    if (previousLock is null) File.Delete(lockPath); else File.WriteAllBytes(lockPath, previousLock);
                }
            }
            if (json) stdout.WriteLine(JsonSerializer.Serialize(new
            {
                success = true,
                input,
                sourcePath = outputPath,
                lockPath,
                plan = savedPlan,
                diagnostics = planDiagnostics.Select(d => FirmamentBuildAdmissionValidation.Present(d, "generated revision"))
            }, options));
            else stdout.WriteLine($"Built {(remove ? "removal" : "move")} plan for {alias}: {outputPath}\nOriginal source PMI excluded explicitly; {(remove ? "the retired hole has no successor PMI" : "replacement PMI is generated")}.");
            return 0;
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException)
        {
            var diagnostic = new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error,
                "imported-feature-command-rejected: " + error.Message, "CLI.ImportedFeatures");
            return Failure(new[] { FirmamentBuildAdmissionValidation.Present(diagnostic, args.Length > 1 ? args[1] : "command") });
        }

        int Failure(object diagnostics)
        {
            if (json) stdout.WriteLine(JsonSerializer.Serialize(new { success = false, diagnostics }, options));
            else stderr.WriteLine(JsonSerializer.Serialize(diagnostics, options));
            return 1;
        }
    }
}
