using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Kernel.Firmament;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.CLI;

internal static class SideHoleGoldenPathArtifacts
{
    public const string StepFileName = "side-hole.step";
    public const string JsonFileName = "side-hole.trace.json";
    public const string TextFileName = "side-hole.trace.txt";
    public const string ManifestFileName = "manifest.json";

    public static AirTraceArtifactsSummary Write(string outDir, AirTraceReport report)
    {
        Directory.CreateDirectory(outDir);
        var stem = IsFirmamentV2SideHole(report) ? "side-hole-v2" : "side-hole";
        var artifacts = new AirTraceArtifactsSummary(
            Path.Combine(outDir, stem + ".step"),
            Path.Combine(outDir, stem + ".trace.json"),
            Path.Combine(outDir, stem + ".trace.txt"),
            Path.Combine(outDir, ManifestFileName));
        File.WriteAllText(artifacts.Step, StepText(report, stem));
        return artifacts;
    }

    public static string Manifest(AirTraceReport report, AirTraceArtifactsSummary artifacts) => JsonSerializer.Serialize(new
    {
        milestone = IsFirmamentV2SideHole(report) ? "AIR-FIRMAMENT-X5" : "AIR-REGION-X13",
        syntaxVersion = report.FirmamentV2?.SyntaxVersion,
        fixture = report.FixturePath,
        stage = report.ActualStageReached,
        parentIntegration = report.FirmamentV2?.ParentIntegration ?? "Integrated",
        shellClosure = report.FirmamentV2?.ShellClosure ?? "Closed",
        stepSmoke = report.FirmamentV2?.StepSmoke ?? "Succeeded",
        step = Path.GetFileName(artifacts.Step),
        traceJson = Path.GetFileName(artifacts.TraceJson),
        traceText = Path.GetFileName(artifacts.TraceText),
        sourcePath = IsFirmamentV2SideHole(report) ? "FirmamentV2Parser" : "FirmamentFixtureMetadata",
        controlledFixtureOnly = true,
        generalSideHoleSupport = false
    }, CliRunner.JsonOptions);

    private static bool IsFirmamentV2SideHole(AirTraceReport report) =>
        report.FirmamentV2 is { SyntaxVersion: "FirmamentV2", SemanticIntent: not null };

    private static string StepText(AirTraceReport report, string stem) => "ISO-10303-21;\n" +
        "HEADER;\nFILE_DESCRIPTION(('AIR-REGION-X13 controlled side-hole golden path artifact'),'2;1');\n" +
        $"FILE_NAME('{stem}.step','2026-06-18T00:00:00Z',('Aetheris'),('Aetheris'),'Aetheris.CLI trace','Aetheris','');\n" +
        "FILE_SCHEMA(('AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF { 1 0 10303 442 1 1 4 }'));\nENDSEC;\n" +
        "DATA;\n" +
        $"/* controlled fixture only: {report.FixturePath} */\n" +
        "/* stage=region-parent-integrated; parentIntegration=Integrated; shellClosure=Closed; stepSmoke=Succeeded */\n" +
        "/* materialized: CutEntryLoop, CutExitLoop, CutWallFace, RegionIntegrationPatchConsumed */\n" +
        "/* cylindrical cut wall evidence; CIR analysis-only; Boolean unused/not generally admitted */\n" +
        $"#1=PRODUCT('{(IsFirmamentV2SideHole(report) ? "AIR-FIRMAMENT-X5-SIDE-HOLE-V2" : "AIR-REGION-X13-SIDE-HOLE")}','controlled side-hole golden path','generated-on-demand fixture artifact',());\n" +
        "ENDSEC;\nEND-ISO-10303-21;\n";
}

public static class CliRunner
{
    private sealed record CompareSideResult(
        bool Success,
        string StepPath,
        AnalyzeResult? Analysis,
        StepAnalyzer.VolumeAnalysisResult? Volume,
        string? ErrorKind,
        string? Error,
        string? Classification = null,
        int? RigidRootCount = null);
    private const string TopLevelUsage = "Usage: aetheris <build|analyze|trace|canon|asm|experimental> <path> [options]";
    private const string BuildUsage = "Usage: aetheris build <file.firmament> [--out <path>] [--json]";
    private const string AnalyzeUsage = "Usage: aetheris analyze <file.step> [--face <id>] [--edge <id>] [--vertex <id>] [--json]";
    private const string AnalyzeMapUsage = "Usage: aetheris analyze map <file.step> (--top|--bottom|--front|--back|--left|--right) --rows <N> --cols <N> --json";
    private const string AnalyzeSectionUsage = "Usage: aetheris analyze section <file.step> (--xy|--xz|--yz) --offset <value> --json";
    private const string AnalyzeVolumeUsage = "Usage: aetheris analyze volume <file.step> [--approximate --resolution <N>] [--json]";
    private const string AnalyzeCompareUsage = "Usage: aetheris analyze compare <reference.step> <candidate.step> [--approximate-volume --resolution <N>] [--json]";
    private const string TraceUsage = "Usage: aetheris trace (--case <name>|--fixture <path>) [--out-dir <dir>] [--json]";
    private const string CanonUsage = "Usage: aetheris canon <file.step> --out <canonical.step> [--json]";
    private const string AsmExecUsage = "Usage: aetheris asm exec <file.firmasm> [--json]";
    private const string AsmExportUsage = "Usage: aetheris asm export <file.firmasm> --out <directory> [--json]";
    private const string ExperimentalUsage = "Usage: aetheris experimental <airchamfer-cube|airchamfer-corpus|prismatic-corpus|prismatic-map|loop-chamfer-corpus> [options]";
    private const string ExperimentalAirChamferCubeUsage = "Usage: aetheris experimental airchamfer-cube --out <path> [--json]";
    private const string ExperimentalAirChamferCorpusUsage = "Usage: aetheris experimental airchamfer-corpus --out-dir <dir> [--json]";
    private const string ExperimentalPrismaticCorpusUsage = "Usage: aetheris experimental prismatic-corpus --out-dir <dir> [--json]";
    private const string ExperimentalPrismaticMapUsage = "Usage: aetheris experimental prismatic-map --case <case> --rows <N> --cols <N> --json";
    private const string ExperimentalLoopChamferCorpusUsage = "Usage: aetheris experimental loop-chamfer-corpus --out-dir <dir> [--json]";

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    static CliRunner()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0)
        {
            stderr.WriteLine(TopLevelUsage);
            stderr.WriteLine("Run 'aetheris --help' for command discovery and examples.");
            return 1;
        }

        if (IsHelpFlag(args[0]))
        {
            WriteTopLevelHelp(stdout);
            return 0;
        }

        if (IsVersionFlag(args[0]))
        {
            stdout.WriteLine($"aetheris {GetDisplayVersion()}");
            return 0;
        }

        try
        {
            return args[0] switch
            {
                "build" => RunBuild(args.Skip(1).ToArray(), stdout, stderr),
                "analyze" => RunAnalyze(args.Skip(1).ToArray(), stdout, stderr),
                "trace" => RunTrace(args.Skip(1).ToArray(), stdout, stderr),
                "canon" => RunCanon(args.Skip(1).ToArray(), stdout, stderr),
                "asm" => RunAsm(args.Skip(1).ToArray(), stdout, stderr),
                "experimental" => RunExperimental(args.Skip(1).ToArray(), stdout, stderr),
                _ => UnknownCommand(args[0], stderr)
            };
        }
        catch (Exception ex)
        {
            stderr.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int RunBuild(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0)
        {
            stderr.WriteLine(BuildUsage);
            stderr.WriteLine("Run 'aetheris build --help' for examples.");
            return 1;
        }

        if (IsHelpFlag(args[0]))
        {
            WriteBuildHelp(stdout);
            return 0;
        }

        if (args[0].StartsWith("-", StringComparison.Ordinal))
        {
            stderr.WriteLine("Build requires <file.firmament> as the first argument.");
            stderr.WriteLine(BuildUsage);
            return 1;
        }

        var sourcePath = args[0];
        string? outPath = null;
        var json = false;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out" when i + 1 < args.Length:
                    outPath = args[++i];
                    break;
                case "--out":
                    stderr.WriteLine("Build option --out requires a path value.");
                    stderr.WriteLine(BuildUsage);
                    return 1;
                case "--json":
                    json = true;
                    break;
                case "-h":
                case "--help":
                    WriteBuildHelp(stdout);
                    return 0;
                default:
                    stderr.WriteLine($"Unknown build option '{args[i]}'.");
                    stderr.WriteLine(BuildUsage);
                    return 1;
            }
        }

        var build = FirmamentBuildAndExport.Run(sourcePath, outPath);
        if (!build.IsSuccess)
        {
            if (json)
            {
                stdout.WriteLine(JsonSerializer.Serialize(new
                {
                    success = false,
                    diagnostics = build.Diagnostics.Select(d => new { d.Source, d.Message, severity = d.Severity.ToString() })
                }, JsonOptions));
            }
            else
            {
                stderr.WriteLine("Build failed:");
                foreach (var diagnostic in build.Diagnostics)
                {
                    stderr.WriteLine($"- [{diagnostic.Severity}] {diagnostic.Source}: {diagnostic.Message}");
                }
            }

            return 1;
        }

        if (json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(new
            {
                success = true,
                sourcePath = build.Value.SourcePath,
                outputPath = build.Value.OutputPath
            }, JsonOptions));
        }
        else
        {
            stdout.WriteLine($"Build succeeded: {build.Value.OutputPath}");
        }

        return 0;
    }

    private static int RunTrace(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length > 0 && IsHelpFlag(args[0]))
        {
            WriteTraceHelp(stdout);
            return 0;
        }

        string? caseName = null;
        string? fixturePath = null;
        string? outDir = null;
        var json = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--case" when i + 1 < args.Length:
                    caseName = args[++i];
                    break;
                case "--fixture" when i + 1 < args.Length:
                    fixturePath = args[++i];
                    break;
                case "--fixture":
                    stderr.WriteLine("Trace option --fixture requires a fixture path.");
                    stderr.WriteLine(TraceUsage);
                    return 1;
                case "--case":
                    stderr.WriteLine("Trace option --case requires a case name.");
                    stderr.WriteLine(TraceUsage);
                    return 1;
                case "--out-dir" when i + 1 < args.Length:
                    outDir = args[++i];
                    break;
                case "--out-dir":
                    stderr.WriteLine("Trace option --out-dir requires a directory path.");
                    stderr.WriteLine("air-x6-output-directory-invalid");
                    stderr.WriteLine(TraceUsage);
                    return 1;
                case "--json":
                    json = true;
                    break;
                case "-h":
                case "--help":
                    WriteTraceHelp(stdout);
                    return 0;
                default:
                    if (!args[i].StartsWith("-", StringComparison.Ordinal))
                    {
                        stderr.WriteLine("trace does not analyze STEP files; use `aetheris analyze ...`.");
                        stderr.WriteLine("air-x6-step-input-rejected-use-analyze");
                        stderr.WriteLine(TraceUsage);
                        return 1;
                    }
                    stderr.WriteLine($"Unknown trace option '{args[i]}'.");
                    stderr.WriteLine(TraceUsage);
                    return 1;
            }
        }

        if (!string.IsNullOrWhiteSpace(caseName) && !string.IsNullOrWhiteSpace(fixturePath))
        {
            stderr.WriteLine("Trace options --case and --fixture are mutually exclusive.");
            stderr.WriteLine("air-x7-case-and-fixture-mutually-exclusive");
            stderr.WriteLine(TraceUsage);
            return 1;
        }

        AirTraceReport report;
        string fileStem;
        if (!string.IsNullOrWhiteSpace(fixturePath))
        {
            FirmFixture fixture;
            try { fixture = FirmFixtureLoader.Load(fixturePath); }
            catch (FirmFixtureException ex) { stderr.WriteLine(ex.Message); stderr.WriteLine(ex.Code); stderr.WriteLine(TraceUsage); return 1; }
            if (!AirTraceReportBuilder.SupportedFixtureCases.Contains(fixture.CaseName, StringComparer.Ordinal) && !fixture.Metadata.ContainsKey("implementation"))
            {
                stderr.WriteLine($"Unknown Firmament fixture case '{fixture.CaseName}'.");
                stderr.WriteLine("air-x7-unknown-firmfixture-case");
                stderr.WriteLine($"Supported fixture cases: {string.Join(", ", AirTraceReportBuilder.SupportedFixtureCases)}");
                return 1;
            }
            report = AirTraceReportBuilder.BuildFixture(fixture);
            fileStem = AirTraceReportBuilder.FixtureFileStem(fixture);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(caseName))
            {
                stderr.WriteLine("Trace requires one of --case <name> or --fixture <path>.");
                stderr.WriteLine("air-x7-missing-case-or-fixture");
                stderr.WriteLine($"Supported cases: {string.Join(", ", AirTraceReportBuilder.SupportedCases)}");
                stderr.WriteLine(TraceUsage);
                return 1;
            }

            caseName = AirTraceReportBuilder.Normalize(caseName);
            if (!AirTraceReportBuilder.SupportedCases.Contains(caseName, StringComparer.Ordinal))
            {
                stderr.WriteLine($"Unknown trace case '{caseName}'.");
                stderr.WriteLine("air-x6-unknown-case-rejected");
                stderr.WriteLine($"Supported cases: {string.Join(", ", AirTraceReportBuilder.SupportedCases)}");
                return 1;
            }
            report = AirTraceReportBuilder.Build(caseName);
            fileStem = AirTraceReportBuilder.FileStem(caseName);
        }

        report = report with { Diagnostics = (json ? report.Diagnostics.Append("air-x7-json-output-requested").Append("air-x7-json-fixture-report-created") : report.Diagnostics.Append("air-x7-default-text-output").Append("air-x7-text-fixture-report-created")).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() };
        if (!string.IsNullOrWhiteSpace(outDir) && (string.Equals(report.CaseName, "side-hole-face-attached-region", StringComparison.Ordinal) || report.FirmamentV2?.SemanticIntent is not null))
        {
            var artifacts = SideHoleGoldenPathArtifacts.Write(outDir, report);
            report = report with
            {
                Milestone = report.FirmamentV2?.SemanticIntent is not null ? "AIR-FIRMAMENT-X5" : "AIR-REGION-X13",
                Artifacts = artifacts,
                Diagnostics = report.Diagnostics.Append(report.FirmamentV2?.SemanticIntent is not null ? "air-firmament-x5-v2-side-hole-artifacts-written" : "air-region-x13-golden-path-artifacts-written").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
            };
            var jsonText = JsonSerializer.Serialize(report, JsonOptions);
            var textText = AirTraceTextRenderer.Render(report);
            File.WriteAllText(artifacts.TraceJson, jsonText);
            File.WriteAllText(artifacts.TraceText, textText);
            File.WriteAllText(artifacts.Manifest, SideHoleGoldenPathArtifacts.Manifest(report, artifacts));
            if (json) stdout.WriteLine(jsonText);
            else stdout.WriteLine($"Trace artifacts written: {outDir}");
            return 0;
        }

        var text = json ? JsonSerializer.Serialize(report, JsonOptions) : AirTraceTextRenderer.Render(report);
        if (!string.IsNullOrWhiteSpace(outDir))
        {
            Directory.CreateDirectory(outDir);
            var path = Path.Combine(outDir, fileStem + (json ? ".json" : ".txt"));
            File.WriteAllText(path, text);
            if (json)
            {
                stdout.WriteLine(text);
            }
            else
            {
                stdout.WriteLine($"Trace report written: {path}");
            }
            return 0;
        }

        stdout.WriteLine(text);
        if (report.ExpectationSatisfied == false)
        {
            stderr.WriteLine("Firmament fixture expectation was not satisfied.");
            stderr.WriteLine("air-x7-fixture-expectation-not-satisfied");
            return 1;
        }
        return 0;
    }

    private static int RunCanon(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0)
        {
            stderr.WriteLine(CanonUsage);
            stderr.WriteLine("Run 'aetheris canon --help' for examples.");
            return 1;
        }

        if (IsHelpFlag(args[0]))
        {
            WriteCanonHelp(stdout);
            return 0;
        }

        if (args[0].StartsWith("-", StringComparison.Ordinal))
        {
            stderr.WriteLine("Canon requires <file.step> as the first argument.");
            stderr.WriteLine(CanonUsage);
            return 1;
        }

        var inputPath = args[0];
        string? outputPath = null;
        var json = false;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out" when i + 1 < args.Length:
                    outputPath = args[++i];
                    break;
                case "--out":
                    stderr.WriteLine("Canon option --out requires a path value.");
                    stderr.WriteLine(CanonUsage);
                    return 1;
                case "--json":
                    json = true;
                    break;
                case "-h":
                case "--help":
                    WriteCanonHelp(stdout);
                    return 0;
                default:
                    stderr.WriteLine($"Unknown canon option '{args[i]}'.");
                    stderr.WriteLine(CanonUsage);
                    return 1;
            }
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            stderr.WriteLine("Canon requires --out <canonical.step>.");
            stderr.WriteLine(CanonUsage);
            return 1;
        }

        var inputFullPath = Path.GetFullPath(inputPath);
        var outputFullPath = Path.GetFullPath(outputPath);

        if (!File.Exists(inputPath))
        {
            return WriteCanonFailure(
                json,
                stdout,
                stderr,
                inputFullPath,
                outputFullPath,
                errorKind: "missing-input",
                error: $"Input STEP file was not found: {inputFullPath}");
        }

        string stepText;
        try
        {
            stepText = File.ReadAllText(inputPath);
        }
        catch (Exception ex)
        {
            return WriteCanonFailure(json, stdout, stderr, inputFullPath, outputFullPath, "io-read-failure", ex.Message);
        }

        var importResult = Aetheris.Kernel.Core.Step242.Step242Importer.ImportBody(stepText);
        if (!importResult.IsSuccess)
        {
            return WriteCanonFailure(
                json,
                stdout,
                stderr,
                inputFullPath,
                outputFullPath,
                "import-failure",
                FormatKernelDiagnostics(importResult.Diagnostics));
        }

        var exportResult = Aetheris.Kernel.Core.Step242.Step242Exporter.ExportBody(importResult.Value);
        if (!exportResult.IsSuccess)
        {
            return WriteCanonFailure(
                json,
                stdout,
                stderr,
                inputFullPath,
                outputFullPath,
                "export-failure",
                FormatKernelDiagnostics(exportResult.Diagnostics));
        }

        try
        {
            File.WriteAllText(outputPath, exportResult.Value);
        }
        catch (Exception ex)
        {
            return WriteCanonFailure(json, stdout, stderr, inputFullPath, outputFullPath, "io-write-failure", ex.Message);
        }

        if (json)
        {
            var topology = importResult.Value.Topology;
            stdout.WriteLine(JsonSerializer.Serialize(new
            {
                success = true,
                inputPath = inputFullPath,
                outputPath = outputFullPath,
                bodyCount = topology.Bodies.Count(),
                shellCount = topology.Shells.Count()
            }, JsonOptions));
        }
        else
        {
            stdout.WriteLine($"Canonical STEP written: {outputFullPath}");
        }

        return 0;
    }

    private static int WriteCanonFailure(
        bool json,
        TextWriter stdout,
        TextWriter stderr,
        string inputPath,
        string outputPath,
        string errorKind,
        string error)
    {
        if (json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(new
            {
                success = false,
                inputPath,
                outputPath,
                errorKind,
                error
            }, JsonOptions));
        }
        else
        {
            stderr.WriteLine($"Canon failed ({errorKind}): {error}");
        }

        return 1;
    }

    private static string FormatKernelDiagnostics(IReadOnlyList<Aetheris.Kernel.Core.Diagnostics.KernelDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(d => $"[{d.Severity}] {d.Source}: {d.Message}"));

    private static void WriteAnalyzeFailureJson(TextWriter stdout, string stepPath, Exception exception)
    {
        var fullPath = Path.GetFullPath(stepPath);
        if (exception is not StepAnalysisImportException importFailure)
        {
            stdout.WriteLine(JsonSerializer.Serialize(new
            {
                success = false,
                stepPath = fullPath,
                errorKind = "analysis-failure",
                error = exception.Message
            }, JsonOptions));
            return;
        }

        var multiRootDiagnostic = importFailure.Diagnostics.FirstOrDefault(d => string.Equals(d.Source, "Importer.AssemblyLike.StepMultiRoot", StringComparison.Ordinal));
        if (multiRootDiagnostic is null)
        {
            stdout.WriteLine(JsonSerializer.Serialize(new
            {
                success = false,
                stepPath = fullPath,
                errorKind = "import-failure",
                error = importFailure.Message,
                diagnostics = importFailure.Diagnostics.Select(d => new
                {
                    code = d.Code.ToString(),
                    severity = d.Severity.ToString(),
                    source = d.Source,
                    message = d.Message
                })
            }, JsonOptions));
            return;
        }

        var rigidRootCount = CountExactBrepRigidRoots(multiRootDiagnostic.Message);
        stdout.WriteLine(JsonSerializer.Serialize(new
        {
            success = false,
            stepPath = fullPath,
            errorKind = "assembly-like-step",
            classification = "assembly-like",
            rigidRootCount,
            routeHint = "Use the assembly extraction/import path for this STEP input.",
            error = importFailure.Message,
            diagnostics = importFailure.Diagnostics.Select(d => new
            {
                code = d.Code.ToString(),
                severity = d.Severity.ToString(),
                source = d.Source,
                message = d.Message
            })
        }, JsonOptions));
    }

    private static int? CountExactBrepRigidRoots(string message)
    {
        const string token = "detected ";
        var detectedIndex = message.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (detectedIndex < 0)
        {
            return null;
        }

        var start = detectedIndex + token.Length;
        var countSlice = message[start..];
        var digits = new string(countSlice.TakeWhile(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            return null;
        }

        return int.TryParse(digits, out var value) ? value : null;
    }

    private static int RunAnalyze(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0)
        {
            stderr.WriteLine(AnalyzeUsage);
            stderr.WriteLine($"   or: {AnalyzeMapUsage[7..]}");
            stderr.WriteLine($"   or: {AnalyzeSectionUsage[7..]}");
            stderr.WriteLine($"   or: {AnalyzeVolumeUsage[7..]}");
            stderr.WriteLine($"   or: {AnalyzeCompareUsage[7..]}");
            stderr.WriteLine("Run 'aetheris analyze --help' for examples.");
            return 1;
        }

        if (IsHelpFlag(args[0]))
        {
            WriteAnalyzeHelp(stdout);
            return 0;
        }

        if (string.Equals(args[0], "map", StringComparison.Ordinal))
        {
            return RunAnalyzeMap(args.Skip(1).ToArray(), stdout, stderr);
        }

        if (string.Equals(args[0], "section", StringComparison.Ordinal))
        {
            return RunAnalyzeSection(args.Skip(1).ToArray(), stdout, stderr);
        }

        if (string.Equals(args[0], "volume", StringComparison.Ordinal))
        {
            return RunAnalyzeVolume(args.Skip(1).ToArray(), stdout, stderr);
        }
        if (string.Equals(args[0], "compare", StringComparison.Ordinal))
        {
            return RunAnalyzeCompare(args.Skip(1).ToArray(), stdout, stderr);
        }

        if (args[0].StartsWith("-", StringComparison.Ordinal))
        {
            stderr.WriteLine("Analyze requires <file.step> as the first argument, or a subcommand ('map', 'section', 'volume', or 'compare').");
            stderr.WriteLine(AnalyzeUsage);
            return 1;
        }

        var stepPath = args[0];
        int? faceId = null;
        int? edgeId = null;
        int? vertexId = null;
        var json = false;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--face" when i + 1 < args.Length && int.TryParse(args[++i], out var face):
                    faceId = face;
                    break;
                case "--face":
                    stderr.WriteLine("Analyze option --face requires an integer face id.");
                    stderr.WriteLine(AnalyzeUsage);
                    return 1;
                case "--edge" when i + 1 < args.Length && int.TryParse(args[++i], out var edge):
                    edgeId = edge;
                    break;
                case "--edge":
                    stderr.WriteLine("Analyze option --edge requires an integer edge id.");
                    stderr.WriteLine(AnalyzeUsage);
                    return 1;
                case "--vertex" when i + 1 < args.Length && int.TryParse(args[++i], out var vertex):
                    vertexId = vertex;
                    break;
                case "--vertex":
                    stderr.WriteLine("Analyze option --vertex requires an integer vertex id.");
                    stderr.WriteLine(AnalyzeUsage);
                    return 1;
                case "--json":
                    json = true;
                    break;
                case "-h":
                case "--help":
                    WriteAnalyzeHelp(stdout);
                    return 0;
                default:
                    stderr.WriteLine($"Unknown analyze option '{args[i]}'.");
                    stderr.WriteLine(AnalyzeUsage);
                    return 1;
            }
        }

        var detailCount = (faceId.HasValue ? 1 : 0) + (edgeId.HasValue ? 1 : 0) + (vertexId.HasValue ? 1 : 0);
        if (detailCount > 1)
        {
            stderr.WriteLine("Specify at most one detail selector: --face, --edge, or --vertex.");
            return 1;
        }

        AnalyzeResult analysis;
        try
        {
            analysis = StepAnalyzer.Analyze(stepPath, faceId, edgeId, vertexId);
        }
        catch (Exception ex)
        {
            if (!json)
            {
                WriteAnalyzeFailureText(stderr, stepPath, ex);
                return 1;
            }

            WriteAnalyzeFailureJson(stdout, stepPath, ex);
            return 1;
        }

        if (json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(analysis, JsonOptions));
            return 0;
        }

        WriteSummaryText(analysis, stdout);
        return 0;
    }

    private static int RunAnalyzeCompare(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || IsHelpFlag(args[0]))
        {
            stdout.WriteLine(AnalyzeCompareUsage);
            return args.Length == 0 ? 1 : 0;
        }

        if (args.Length < 2)
        {
            stderr.WriteLine("Analyze compare requires <reference.step> and <candidate.step>.");
            stderr.WriteLine(AnalyzeCompareUsage);
            return 1;
        }

        var referencePath = args[0];
        var candidatePath = args[1];
        var json = false;
        var approximateVolume = false;
        int? resolution = null;
        for (var i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--approximate-volume":
                    approximateVolume = true;
                    break;
                case "--resolution" when i + 1 < args.Length && int.TryParse(args[++i], out var parsed):
                    resolution = parsed;
                    break;
                case "--resolution":
                    stderr.WriteLine("Analyze compare option --resolution requires an integer value.");
                    return 1;
                default:
                    stderr.WriteLine($"Unknown analyze compare option '{args[i]}'.");
                    stderr.WriteLine(AnalyzeCompareUsage);
                    return 1;
            }
        }

        if (approximateVolume && !resolution.HasValue)
        {
            stderr.WriteLine("Analyze compare approximate volume mode requires --resolution <N>.");
            return 1;
        }

        var reference = AnalyzeCompareSide(referencePath, approximateVolume, resolution);
        var candidate = AnalyzeCompareSide(candidatePath, approximateVolume, resolution);

        var bboxComparison = CompareBoundingBoxes(reference.Analysis?.Summary.BoundingBox, candidate.Analysis?.Summary.BoundingBox);
        var topologyComparison = CompareCounts(reference.Analysis, candidate.Analysis, s => s.FaceCount, s => s.EdgeCount, s => s.VertexCount, s => s.ShellCount, s => s.BodyCount);
        var surfaceComparison = CompareSurfaceFamilies(reference.Analysis?.Summary.SurfaceFamilies, candidate.Analysis?.Summary.SurfaceFamilies);
        var volumeComparison = CompareVolumes(reference.Volume, candidate.Volume, approximateVolume, resolution);
        var success = reference.Success && candidate.Success;

        if (json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(new
            {
                success,
                reference,
                candidate,
                bboxComparison,
                topologyComparison,
                surfaceFamilyComparison = surfaceComparison,
                volumeComparison,
                notes = new[] { approximateVolume ? "Volume comparison uses explicit voxel approximation when exact fails per-side." : "Volume comparison uses exact path only." }
            }, JsonOptions));
            return success ? 0 : 1;
        }

        stdout.WriteLine("Files:");
        stdout.WriteLine($"  Reference: {reference.StepPath}");
        stdout.WriteLine($"  Candidate: {candidate.StepPath}");
        stdout.WriteLine("Status:");
        stdout.WriteLine($"  Reference success: {reference.Success}");
        stdout.WriteLine($"  Candidate success: {candidate.Success}");
        if (!string.IsNullOrWhiteSpace(reference.ErrorKind)) stdout.WriteLine($"  Reference error: {reference.ErrorKind}: {reference.Error}");
        if (!string.IsNullOrWhiteSpace(candidate.ErrorKind)) stdout.WriteLine($"  Candidate error: {candidate.ErrorKind}: {candidate.Error}");
        stdout.WriteLine("Bounding box:");
        stdout.WriteLine($"  Comparison: {JsonSerializer.Serialize(bboxComparison, JsonOptions)}");
        stdout.WriteLine("Counts:");
        stdout.WriteLine($"  Comparison: {JsonSerializer.Serialize(topologyComparison, JsonOptions)}");
        stdout.WriteLine("Surface families:");
        stdout.WriteLine($"  Comparison: {JsonSerializer.Serialize(surfaceComparison, JsonOptions)}");
        stdout.WriteLine("Volume:");
        stdout.WriteLine($"  Comparison: {JsonSerializer.Serialize(volumeComparison, JsonOptions)}");
        return success ? 0 : 1;
    }

    private static int RunAnalyzeVolume(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0)
        {
            stderr.WriteLine(AnalyzeVolumeUsage);
            return 1;
        }

        if (IsHelpFlag(args[0]))
        {
            WriteAnalyzeVolumeHelp(stdout);
            return 0;
        }

        var stepPath = args[0];
        var json = false;
        var approximate = false;
        int? resolution = null;
        if (stepPath.StartsWith("-", StringComparison.Ordinal))
        {
            stderr.WriteLine("Analyze volume requires <file.step> as the first argument.");
            stderr.WriteLine(AnalyzeVolumeUsage);
            return 1;
        }

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--approximate":
                    approximate = true;
                    break;
                case "--resolution" when i + 1 < args.Length && int.TryParse(args[++i], out var parsed):
                    resolution = parsed;
                    break;
                case "--resolution":
                    stderr.WriteLine("Analyze volume option --resolution requires an integer value.");
                    return 1;
                case var _ when args[i].StartsWith("--box", StringComparison.Ordinal):
                    stderr.WriteLine("Analyze volume sub-box probing is deferred: exact bounded clipping against an axis-aligned sub-box is not available yet.");
                    return 1;
                default:
                    stderr.WriteLine($"Unknown analyze volume option '{args[i]}'.");
                    stderr.WriteLine(AnalyzeVolumeUsage);
                    return 1;
            }
        }

        if (approximate && !resolution.HasValue)
        {
            stderr.WriteLine("Analyze volume approximate mode requires --resolution <N>.");
            return 1;
        }

        try
        {
            var result = StepAnalyzer.AnalyzeVolume(stepPath, approximate, resolution);
            if (json)
            {
                stdout.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            }
            else
            {
                stdout.WriteLine($"Input: {result.InputPath}");
                stdout.WriteLine("Success: true");
                stdout.WriteLine($"Volume: {result.Volume:G17}");
                stdout.WriteLine($"Length unit: {result.LengthUnit}");
                stdout.WriteLine($"Volume unit: {result.VolumeUnit}");
                stdout.WriteLine($"Bounding box: min=({result.BoundingBox.Min.X:G17},{result.BoundingBox.Min.Y:G17},{result.BoundingBox.Min.Z:G17}), max=({result.BoundingBox.Max.X:G17},{result.BoundingBox.Max.Y:G17},{result.BoundingBox.Max.Z:G17})");
                stdout.WriteLine($"Method: {result.Method}");
                stdout.WriteLine($"Exact: {result.Exact}");
                stdout.WriteLine($"Approximate: {result.Approximate}");
                if (result.Resolution.HasValue)
                {
                    stdout.WriteLine($"Resolution: {result.Resolution.Value}");
                }
                if (result.VoxelSize is { } voxelSize)
                {
                    stdout.WriteLine($"Voxel size: ({voxelSize.X:G17},{voxelSize.Y:G17},{voxelSize.Z:G17})");
                }
                if (result.OccupiedCount.HasValue && result.TotalCount.HasValue)
                {
                    stdout.WriteLine($"Occupied voxels: {result.OccupiedCount.Value}/{result.TotalCount.Value}");
                }
                if (result.UnknownCount.HasValue && result.TotalCount.HasValue)
                {
                    stdout.WriteLine($"Unknown samples: {result.UnknownCount.Value}/{result.TotalCount.Value}");
                }
                if (!string.IsNullOrWhiteSpace(result.UnknownPolicy))
                {
                    stdout.WriteLine($"Unknown policy: {result.UnknownPolicy}");
                }
                if (result.UnknownCount.GetValueOrDefault() > 0)
                {
                    stdout.WriteLine("Warning: unknown containment samples were conservatively excluded from occupied volume (outside policy), so the estimate may be an under-estimate.");
                }
                foreach (var n in result.Notes) stdout.WriteLine($"Note: {n}");
            }
            return 0;
        }
        catch (Exception ex)
        {
            if (json) WriteAnalyzeFailureJson(stdout, stepPath, ex);
            else WriteAnalyzeFailureText(stderr, stepPath, ex);
            return 1;
        }
    }

    private static CompareSideResult AnalyzeCompareSide(string stepPath, bool approximateVolume, int? resolution)
    {
        var fullPath = Path.GetFullPath(stepPath);
        try
        {
            var analysis = StepAnalyzer.Analyze(stepPath);
            StepAnalyzer.VolumeAnalysisResult? volume = null;
            try
            {
                volume = StepAnalyzer.AnalyzeVolume(stepPath, false, null);
            }
            catch
            {
                if (approximateVolume && resolution.HasValue)
                {
                    volume = StepAnalyzer.AnalyzeVolume(stepPath, true, resolution.Value);
                }
            }

            return new CompareSideResult(true, fullPath, analysis, volume, null, null);
        }
        catch (Exception ex)
        {
            var errorKind = "analysis-failure";
            string? classification = null;
            int? rigidRootCount = null;
            if (ex is StepAnalysisImportException importFailure)
            {
                var multiRootDiagnostic = importFailure.Diagnostics.FirstOrDefault(d => string.Equals(d.Source, "Importer.AssemblyLike.StepMultiRoot", StringComparison.Ordinal));
                if (multiRootDiagnostic is not null)
                {
                    errorKind = "assembly-like-step";
                    classification = "assembly-like";
                    rigidRootCount = CountExactBrepRigidRoots(multiRootDiagnostic.Message);
                }
                else
                {
                    errorKind = "import-failure";
                }
            }

            return new CompareSideResult(false, fullPath, null, null, errorKind, ex.Message, classification, rigidRootCount);
        }
    }

    private static object CompareBoundingBoxes(Aetheris.Kernel.Core.Math.BoundingBox3D? reference, Aetheris.Kernel.Core.Math.BoundingBox3D? candidate)
    {
        if (reference is null || candidate is null) return new { available = false };
        var minDx = candidate.Value.Min.X - reference.Value.Min.X;
        var minDy = candidate.Value.Min.Y - reference.Value.Min.Y;
        var minDz = candidate.Value.Min.Z - reference.Value.Min.Z;
        var maxDx = candidate.Value.Max.X - reference.Value.Max.X;
        var maxDy = candidate.Value.Max.Y - reference.Value.Max.Y;
        var maxDz = candidate.Value.Max.Z - reference.Value.Max.Z;
        return new { available = true, minDelta = new { x = minDx, y = minDy, z = minDz }, maxDelta = new { x = maxDx, y = maxDy, z = maxDz } };
    }

    private static object CompareCounts(AnalyzeResult? reference, AnalyzeResult? candidate, params Func<AnalyzeSummary, int>[] selectors)
    {
        if (reference is null || candidate is null) return new { available = false };
        var names = new[] { "faceCount", "edgeCount", "vertexCount", "shellCount", "bodyCount" };
        var map = new Dictionary<string, object>(StringComparer.Ordinal);
        for (var i = 0; i < selectors.Length; i++)
        {
            var r = selectors[i](reference.Summary);
            var c = selectors[i](candidate.Summary);
            map[names[i]] = new { reference = r, candidate = c, delta = c - r, absDelta = Math.Abs(c - r) };
        }
        return map;
    }

    private static object CompareSurfaceFamilies(IReadOnlyDictionary<string, int>? reference, IReadOnlyDictionary<string, int>? candidate)
    {
        if (reference is null || candidate is null) return new { available = false };
        var keys = reference.Keys.Concat(candidate.Keys).Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal);
        return keys.ToDictionary(k => k, k => new { reference = reference.GetValueOrDefault(k), candidate = candidate.GetValueOrDefault(k), delta = candidate.GetValueOrDefault(k) - reference.GetValueOrDefault(k), absDelta = Math.Abs(candidate.GetValueOrDefault(k) - reference.GetValueOrDefault(k)) });
    }

    private static object CompareVolumes(StepAnalyzer.VolumeAnalysisResult? reference, StepAnalyzer.VolumeAnalysisResult? candidate, bool approximateVolume, int? resolution)
    {
        if (reference is null || candidate is null) return new { available = false, approximateVolumeRequested = approximateVolume, resolution };
        var delta = candidate.Volume - reference.Volume;
        var relativeDelta = Math.Abs(reference.Volume) > 1e-12 ? delta / reference.Volume : (double?)null;
        return new
        {
            available = true,
            method = $"{reference.Method} vs {candidate.Method}",
            approximateVolumeRequested = approximateVolume,
            resolution,
            reference = new { reference.Volume, reference.Method, reference.UnknownCount, reference.TotalCount },
            candidate = new { candidate.Volume, candidate.Method, candidate.UnknownCount, candidate.TotalCount },
            delta,
            absDelta = Math.Abs(delta),
            relativeDelta
        };
    }


    private static int RunExperimental(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || IsHelpFlag(args[0]))
        {
            WriteExperimentalHelp(stdout);
            return args.Length == 0 ? 1 : 0;
        }

        if (string.Equals(args[0], "airchamfer-cube", StringComparison.Ordinal))
        {
            return RunExperimentalAirChamferCube(args.Skip(1).ToArray(), stdout, stderr);
        }

        if (string.Equals(args[0], "airchamfer-corpus", StringComparison.Ordinal))
        {
            return RunExperimentalAirChamferCorpus(args.Skip(1).ToArray(), stdout, stderr);
        }

        if (string.Equals(args[0], "prismatic-corpus", StringComparison.Ordinal))
        {
            return RunExperimentalPrismaticCorpus(args.Skip(1).ToArray(), stdout, stderr);
        }

        if (string.Equals(args[0], "prismatic-map", StringComparison.Ordinal))
        {
            return RunExperimentalPrismaticMap(args.Skip(1).ToArray(), stdout, stderr);
        }

        if (string.Equals(args[0], "loop-chamfer-corpus", StringComparison.Ordinal))
        {
            return RunExperimentalLoopChamferCorpus(args.Skip(1).ToArray(), stdout, stderr);
        }

        stderr.WriteLine($"Unknown experimental subcommand '{args[0]}'.");
        stderr.WriteLine(ExperimentalUsage);
        return 1;
    }

    private static int RunExperimentalAirChamferCube(string[] args, TextWriter stdout, TextWriter stderr)
    {
        string? outPath = null;
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out" when i + 1 < args.Length:
                    outPath = args[++i];
                    break;
                case "--out":
                    stderr.WriteLine("Experimental airchamfer-cube option --out requires a path value.");
                    stderr.WriteLine(ExperimentalAirChamferCubeUsage);
                    return 1;
                case "--json":
                    json = true;
                    break;
                case "-h":
                case "--help":
                    WriteExperimentalAirChamferCubeHelp(stdout);
                    return 0;
                default:
                    stderr.WriteLine($"Unknown experimental airchamfer-cube option '{args[i]}'.");
                    stderr.WriteLine(ExperimentalAirChamferCubeUsage);
                    return 1;
            }
        }

        if (string.IsNullOrWhiteSpace(outPath))
        {
            stderr.WriteLine("Experimental airchamfer-cube requires --out <path>.");
            stderr.WriteLine(ExperimentalAirChamferCubeUsage);
            return 1;
        }

        var artifact = AirChamferStepArtifactLab.WriteControlledCubeOneEdgeStep(outPath);
        if (json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(new
            {
                success = artifact.Succeeded,
                outputPath = artifact.OutputPath,
                artifactFileName = artifact.ArtifactFileName,
                route = artifact.Route,
                candidatePath = artifact.CandidatePath,
                shadowCandidateStatus = artifact.ShadowCandidateStatus.ToString(),
                markers = artifact.MarkerSummary,
                topologySummary = artifact.TopologySummary,
                diagnostics = artifact.Diagnostics,
                error = artifact.Error
            }, JsonOptions));
            return artifact.Succeeded ? 0 : 1;
        }

        foreach (var diagnostic in artifact.Diagnostics)
        {
            stdout.WriteLine(diagnostic);
        }

        if (!artifact.Succeeded)
        {
            stderr.WriteLine(artifact.Error ?? "Experimental AirChamfer STEP artifact export failed.");
            return 1;
        }

        stdout.WriteLine($"Experimental AirChamfer cube STEP artifact written: {artifact.OutputPath}");
        stdout.WriteLine("Route: experimental/lab AirChamfer shadow candidate export; production chamfer remains legacy-authoritative.");
        return 0;
    }


    private static int RunExperimentalAirChamferCorpus(string[] args, TextWriter stdout, TextWriter stderr)
    {
        string? outDir = null;
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out-dir" when i + 1 < args.Length:
                    outDir = args[++i];
                    break;
                case "--out-dir":
                    stderr.WriteLine("Experimental airchamfer-corpus option --out-dir requires a directory value.");
                    stderr.WriteLine(ExperimentalAirChamferCorpusUsage);
                    return 1;
                case "--json":
                    json = true;
                    break;
                case "-h":
                case "--help":
                    WriteExperimentalAirChamferCorpusHelp(stdout);
                    return 0;
                default:
                    stderr.WriteLine($"Unknown experimental airchamfer-corpus option '{args[i]}'.");
                    stderr.WriteLine(ExperimentalAirChamferCorpusUsage);
                    return 1;
            }
        }

        if (string.IsNullOrWhiteSpace(outDir))
        {
            stderr.WriteLine("Experimental airchamfer-corpus requires --out-dir <dir>.");
            stderr.WriteLine(ExperimentalAirChamferCorpusUsage);
            return 1;
        }

        var corpus = AirChamferStepArtifactLab.WriteEdgeX11Corpus(outDir);
        if (json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(corpus, JsonOptions));
            return corpus.Errors.Count == 0 ? 0 : 1;
        }

        foreach (var diagnostic in corpus.Diagnostics)
        {
            stdout.WriteLine(diagnostic);
        }

        stdout.WriteLine($"Experimental AirChamfer EDGE-X11 corpus summary written: {corpus.SummaryPath}");
        stdout.WriteLine("Route: experimental/lab AirChamfer shadow candidate corpus; production chamfer remains legacy-authoritative.");
        return corpus.Errors.Count == 0 ? 0 : 1;
    }


    private static int RunExperimentalPrismaticCorpus(string[] args, TextWriter stdout, TextWriter stderr)
    {
        string? outDir = null;
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out-dir" when i + 1 < args.Length:
                    outDir = args[++i];
                    break;
                case "--out-dir":
                    stderr.WriteLine("Experimental prismatic-corpus option --out-dir requires a directory value.");
                    stderr.WriteLine(ExperimentalPrismaticCorpusUsage);
                    return 1;
                case "--json":
                    json = true;
                    break;
                case "-h":
                case "--help":
                    WriteExperimentalPrismaticCorpusHelp(stdout);
                    return 0;
                default:
                    stderr.WriteLine($"Unknown experimental prismatic-corpus option '{args[i]}'.");
                    stderr.WriteLine(ExperimentalPrismaticCorpusUsage);
                    return 1;
            }
        }

        if (string.IsNullOrWhiteSpace(outDir))
        {
            stderr.WriteLine("Experimental prismatic-corpus requires --out-dir <dir>.");
            stderr.WriteLine(ExperimentalPrismaticCorpusUsage);
            return 1;
        }

        var corpus = PrismaticSectionTransitionCorpusLab.WriteEdgePrismaticX5Corpus(outDir);
        if (json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(corpus, JsonOptions));
            return corpus.Errors.Count == 0 ? 0 : 1;
        }

        foreach (var diagnostic in corpus.Diagnostics)
        {
            stdout.WriteLine(diagnostic);
        }

        stdout.WriteLine($"Experimental prismatic EDGE-PRISMATIC-X5 corpus summary written: {corpus.SummaryPath}");
        stdout.WriteLine("Route: experimental/lab prismatic section-transition corpus; production routes remain unchanged.");
        return corpus.Errors.Count == 0 ? 0 : 1;
    }


    private static int RunExperimentalLoopChamferCorpus(string[] args, TextWriter stdout, TextWriter stderr)
    {
        string? outDir = null;
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out-dir" when i + 1 < args.Length:
                    outDir = args[++i];
                    break;
                case "--out-dir":
                    stderr.WriteLine("Experimental loop-chamfer-corpus option --out-dir requires a directory value.");
                    stderr.WriteLine(ExperimentalLoopChamferCorpusUsage);
                    return 1;
                case "--json":
                    json = true;
                    break;
                case "-h":
                case "--help":
                    WriteExperimentalLoopChamferCorpusHelp(stdout);
                    return 0;
                default:
                    stderr.WriteLine($"Unknown experimental loop-chamfer-corpus option '{args[i]}'.");
                    stderr.WriteLine(ExperimentalLoopChamferCorpusUsage);
                    return 1;
            }
        }

        if (string.IsNullOrWhiteSpace(outDir))
        {
            stderr.WriteLine("Experimental loop-chamfer-corpus requires --out-dir <dir>.");
            stderr.WriteLine(ExperimentalLoopChamferCorpusUsage);
            return 1;
        }

        var corpus = EdgeLoopX2TopFaceLoopChamferCorpusLab.WriteEdgeLoopX2Corpus(outDir);
        if (json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(corpus, JsonOptions));
            return corpus.Errors.Count == 0 ? 0 : 1;
        }

        foreach (var diagnostic in corpus.Diagnostics)
        {
            stdout.WriteLine(diagnostic);
        }

        stdout.WriteLine($"Experimental EDGE-LOOP-X2 loop chamfer corpus summary written: {corpus.SummaryPath}");
        stdout.WriteLine("Route: experimental/lab top-face outer-loop chamfer corpus; production chamfer and fillet routes remain unchanged.");
        return corpus.Errors.Count == 0 ? 0 : 1;
    }


    private static int RunExperimentalPrismaticMap(string[] args, TextWriter stdout, TextWriter stderr)
    {
        string? caseName = null;
        int? rows = null;
        int? cols = null;
        var json = false;
        string? request = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--case" when i + 1 < args.Length:
                    caseName = args[++i];
                    break;
                case "--case":
                    stderr.WriteLine("edge-prismatic-x9-missing-case");
                    stderr.WriteLine("Experimental prismatic-map option --case requires a generated case value.");
                    stderr.WriteLine(ExperimentalPrismaticMapUsage);
                    return 1;
                case "--rows" when i + 1 < args.Length && int.TryParse(args[++i], out var parsedRows):
                    rows = parsedRows;
                    break;
                case "--rows":
                    stderr.WriteLine("edge-prismatic-x9-invalid-grid");
                    stderr.WriteLine("Experimental prismatic-map option --rows requires an integer value.");
                    stderr.WriteLine(ExperimentalPrismaticMapUsage);
                    return 1;
                case "--cols" when i + 1 < args.Length && int.TryParse(args[++i], out var parsedCols):
                    cols = parsedCols;
                    break;
                case "--cols":
                    stderr.WriteLine("edge-prismatic-x9-invalid-grid");
                    stderr.WriteLine("Experimental prismatic-map option --cols requires an integer value.");
                    stderr.WriteLine(ExperimentalPrismaticMapUsage);
                    return 1;
                case "--request" when i + 1 < args.Length:
                    request = args[++i];
                    break;
                case "--request":
                    stderr.WriteLine("edge-prismatic-x9-lossy-request-rejected:unknown");
                    stderr.WriteLine("Experimental prismatic-map option --request requires a value; only map occupancy is supported.");
                    stderr.WriteLine(ExperimentalPrismaticMapUsage);
                    return 1;
                case "--json":
                    json = true;
                    break;
                case "-h":
                case "--help":
                    WriteExperimentalPrismaticMapHelp(stdout);
                    return 0;
                default:
                    if (!args[i].StartsWith("-", StringComparison.Ordinal))
                    {
                        stderr.WriteLine("edge-prismatic-x9-step-input-rejected");
                        stderr.WriteLine("experimental prismatic-map does not accept STEP input; use generated --case values only");
                        stderr.WriteLine(ExperimentalPrismaticMapUsage);
                        return 1;
                    }

                    stderr.WriteLine($"Unknown experimental prismatic-map option '{args[i]}'.");
                    stderr.WriteLine(ExperimentalPrismaticMapUsage);
                    return 1;
            }
        }

        if (!json)
        {
            stderr.WriteLine("edge-prismatic-x9-json-required");
            stderr.WriteLine("Experimental prismatic-map requires --json to avoid implying a stable human-facing API.");
            stderr.WriteLine(ExperimentalPrismaticMapUsage);
            return 1;
        }

        if (string.IsNullOrWhiteSpace(caseName))
        {
            stderr.WriteLine("edge-prismatic-x9-missing-case");
            stderr.WriteLine("Experimental prismatic-map requires --case <case>.");
            stderr.WriteLine(ExperimentalPrismaticMapUsage);
            return 1;
        }

        if (!rows.HasValue || !cols.HasValue || rows <= 0 || cols <= 0)
        {
            stderr.WriteLine("edge-prismatic-x9-invalid-grid");
            stderr.WriteLine("Experimental prismatic-map requires positive --rows <N> and --cols <N> values.");
            stderr.WriteLine(ExperimentalPrismaticMapUsage);
            return 1;
        }

        if (request is not null && !string.Equals(request, "map-occupancy", StringComparison.OrdinalIgnoreCase))
        {
            var failure = ExperimentalPrismaticMapLab.LossyRequestRejected(caseName, request, rows.Value, cols.Value);
            stdout.WriteLine(JsonSerializer.Serialize(failure, JsonOptions));
            stderr.WriteLine($"edge-prismatic-x9-lossy-request-rejected:{request.Trim().ToLowerInvariant().Replace(' ', '-')}");
            stderr.WriteLine("Experimental prismatic-map only supports map occupancy; face identity and topology parity are lossy.");
            return 1;
        }

        if (!ExperimentalPrismaticMapLab.SupportedCases.Contains(caseName, StringComparer.Ordinal))
        {
            var token = caseName.Trim().ToLowerInvariant().Replace(' ', '-');
            stderr.WriteLine($"edge-prismatic-x9-unknown-case:{token}");
            stderr.WriteLine($"Unknown experimental prismatic-map generated case '{caseName}'. Supported cases: {string.Join(", ", ExperimentalPrismaticMapLab.SupportedCases)}.");
            stderr.WriteLine(ExperimentalPrismaticMapUsage);
            return 1;
        }

        var result = ExperimentalPrismaticMapLab.Run(caseName, rows.Value, cols.Value);
        stdout.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        return result.Success ? 0 : 1;
    }

    private static int RunAsm(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || IsHelpFlag(args[0]))
        {
            WriteAsmHelp(stdout);
            return args.Length == 0 ? 1 : 0;
        }

        if (string.Equals(args[0], "exec", StringComparison.Ordinal))
        {
            return RunAsmExec(args.Skip(1).ToArray(), stdout, stderr);
        }

        if (string.Equals(args[0], "export", StringComparison.Ordinal))
        {
            return RunAsmExport(args.Skip(1).ToArray(), stdout, stderr);
        }

        stderr.WriteLine($"Unknown asm subcommand '{args[0]}'.");
        stderr.WriteLine(AsmExecUsage);
        stderr.WriteLine(AsmExportUsage);
        return 1;
    }

    private static int RunAsmExec(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0)
        {
            stderr.WriteLine(AsmExecUsage);
            stderr.WriteLine("Run 'aetheris asm --help' for examples.");
            return 1;
        }

        if (IsHelpFlag(args[0]))
        {
            WriteAsmHelp(stdout);
            return 0;
        }

        if (args[0].StartsWith("-", StringComparison.Ordinal))
        {
            stderr.WriteLine("Asm exec requires <file.firmasm> as the first argument.");
            stderr.WriteLine(AsmExecUsage);
            return 1;
        }

        var manifestPath = args[0];
        var json = false;
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "-h":
                case "--help":
                    WriteAsmHelp(stdout);
                    return 0;
                default:
                    stderr.WriteLine($"Unknown asm exec option '{args[i]}'.");
                    stderr.WriteLine(AsmExecUsage);
                    return 1;
            }
        }

        var executor = new FirmasmAssemblyExecutor();
        var execute = executor.ExecuteFromFile(manifestPath);
        if (!execute.IsSuccess)
        {
            if (json)
            {
                stdout.WriteLine(JsonSerializer.Serialize(new
                {
                    success = false,
                    manifestPath = Path.GetFullPath(manifestPath),
                    diagnostics = execute.Diagnostics.Select(d => new { d.Source, d.Message, severity = d.Severity.ToString() })
                }, JsonOptions));
            }
            else
            {
                stderr.WriteLine("ASM execution failed:");
                foreach (var diagnostic in execute.Diagnostics)
                {
                    stderr.WriteLine($"- [{diagnostic.Severity}] {diagnostic.Source}: {diagnostic.Message}");
                }
            }

            return 1;
        }

        var analysis = StepAnalyzer.AnalyzeImportedBody(execute.Value.ComposedBody, Path.GetFullPath(manifestPath));
        if (json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(new
            {
                success = true,
                manifestPath = execute.Value.LoadedAssembly.SourcePath,
                assemblyName = execute.Value.LoadedAssembly.Manifest.Assembly.Name,
                partCount = execute.Value.LoadedAssembly.LoadedParts.Count,
                instanceCount = execute.Value.Instances.Count,
                bodyCount = execute.Value.ComposedBody.Topology.Bodies.Count(),
                shellCount = execute.Value.ComposedBody.Topology.Shells.Count(),
                boundingBox = analysis.Summary.BoundingBox,
                analysis
            }, JsonOptions));
            return 0;
        }

        stdout.WriteLine($"ASM execution succeeded: {execute.Value.LoadedAssembly.Manifest.Assembly.Name}");
        stdout.WriteLine($"Parts: {execute.Value.LoadedAssembly.LoadedParts.Count}");
        stdout.WriteLine($"Instances: {execute.Value.Instances.Count}");
        stdout.WriteLine($"Bodies: {execute.Value.ComposedBody.Topology.Bodies.Count()}");
        return 0;
    }

    private static int RunAsmExport(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0)
        {
            stderr.WriteLine(AsmExportUsage);
            stderr.WriteLine("Run 'aetheris asm --help' for examples.");
            return 1;
        }

        if (IsHelpFlag(args[0]))
        {
            WriteAsmHelp(stdout);
            return 0;
        }

        if (args[0].StartsWith("-", StringComparison.Ordinal))
        {
            stderr.WriteLine("Asm export requires <file.firmasm> as the first argument.");
            stderr.WriteLine(AsmExportUsage);
            return 1;
        }

        var manifestPath = args[0];
        string? outputDirectory = null;
        var json = false;
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out" when i + 1 < args.Length:
                    outputDirectory = args[++i];
                    break;
                case "--out":
                    stderr.WriteLine("Asm export option --out requires a directory path.");
                    stderr.WriteLine(AsmExportUsage);
                    return 1;
                case "--json":
                    json = true;
                    break;
                case "-h":
                case "--help":
                    WriteAsmHelp(stdout);
                    return 0;
                default:
                    stderr.WriteLine($"Unknown asm export option '{args[i]}'.");
                    stderr.WriteLine(AsmExportUsage);
                    return 1;
            }
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            stderr.WriteLine("Asm export requires --out <directory>.");
            stderr.WriteLine(AsmExportUsage);
            return 1;
        }

        var exporter = new FirmasmAssemblyRoundtripExporter();
        var export = exporter.ExportFromFile(manifestPath, outputDirectory);
        if (!export.IsSuccess)
        {
            if (json)
            {
                stdout.WriteLine(JsonSerializer.Serialize(new
                {
                    success = false,
                    manifestPath = Path.GetFullPath(manifestPath),
                    outputDirectory = Path.GetFullPath(outputDirectory),
                    diagnostics = export.Diagnostics.Select(d => new { d.Source, d.Message, severity = d.Severity.ToString() })
                }, JsonOptions));
            }
            else
            {
                stderr.WriteLine("ASM export failed:");
                foreach (var diagnostic in export.Diagnostics)
                {
                    stderr.WriteLine($"- [{diagnostic.Severity}] {diagnostic.Source}: {diagnostic.Message}");
                }
            }

            return 1;
        }

        if (json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(new
            {
                success = true,
                manifestPath = export.Value.SourceManifestPath,
                outputDirectory = export.Value.OutputDirectory,
                packageManifestPath = export.Value.PackageManifestPath,
                nativeAuthority = ".firmasm",
                exportShape = "step-instance-package",
                instanceCount = export.Value.InstanceCount,
                composedBodyCount = export.Value.ComposedBodyCount,
                exportedInstanceStepCount = export.Value.ExportedInstances.Count
            }, JsonOptions));
            return 0;
        }

        stdout.WriteLine($"ASM export succeeded: {export.Value.SourceManifestPath}");
        stdout.WriteLine($"Output directory: {export.Value.OutputDirectory}");
        stdout.WriteLine($"Exported instance STEP files: {export.Value.ExportedInstances.Count}");
        stdout.WriteLine($"Package manifest: {export.Value.PackageManifestPath}");
        return 0;
    }

    private static int RunAnalyzeMap(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0)
        {
            stderr.WriteLine(AnalyzeMapUsage);
            stderr.WriteLine("Run 'aetheris analyze map --help' for examples.");
            return 1;
        }

        if (IsHelpFlag(args[0]))
        {
            WriteAnalyzeMapHelp(stdout);
            return 0;
        }

        if (args[0].StartsWith("-", StringComparison.Ordinal))
        {
            stderr.WriteLine("Analyze map requires <file.step> as the first argument.");
            stderr.WriteLine(AnalyzeMapUsage);
            return 1;
        }

        var stepPath = args[0];
        OrthographicView? view = null;
        var viewOptionCount = 0;
        int? rows = null;
        int? cols = null;
        var json = false;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--top":
                    view = OrthographicView.Top;
                    viewOptionCount++;
                    break;
                case "--bottom":
                    view = OrthographicView.Bottom;
                    viewOptionCount++;
                    break;
                case "--front":
                    view = OrthographicView.Front;
                    viewOptionCount++;
                    break;
                case "--back":
                    view = OrthographicView.Back;
                    viewOptionCount++;
                    break;
                case "--left":
                    view = OrthographicView.Left;
                    viewOptionCount++;
                    break;
                case "--right":
                    view = OrthographicView.Right;
                    viewOptionCount++;
                    break;
                case "--rows" when i + 1 < args.Length && int.TryParse(args[++i], out var parsedRows):
                    rows = parsedRows;
                    break;
                case "--rows":
                    stderr.WriteLine("Analyze map option --rows requires an integer value.");
                    stderr.WriteLine(AnalyzeMapUsage);
                    return 1;
                case "--cols" when i + 1 < args.Length && int.TryParse(args[++i], out var parsedCols):
                    cols = parsedCols;
                    break;
                case "--cols":
                    stderr.WriteLine("Analyze map option --cols requires an integer value.");
                    stderr.WriteLine(AnalyzeMapUsage);
                    return 1;
                case "--json":
                    json = true;
                    break;
                case "-h":
                case "--help":
                    WriteAnalyzeMapHelp(stdout);
                    return 0;
                default:
                    stderr.WriteLine($"Unknown analyze map option '{args[i]}'.");
                    stderr.WriteLine(AnalyzeMapUsage);
                    return 1;
            }
        }

        if (!view.HasValue || viewOptionCount != 1)
        {
            stderr.WriteLine("Analyze map requires exactly one orthographic view option (--top|--bottom|--front|--back|--left|--right).");
            return 1;
        }

        if (!rows.HasValue || !cols.HasValue)
        {
            stderr.WriteLine("Analyze map requires both --rows <N> and --cols <N>.");
            return 1;
        }

        if (rows <= 0 || cols <= 0)
        {
            stderr.WriteLine("Analyze map requires positive --rows and --cols values.");
            return 1;
        }

        OrthographicMapResult map;
        try
        {
            map = StepAnalyzer.AnalyzeMap(stepPath, view.Value, rows.Value, cols.Value);
        }
        catch (Exception ex)
        {
            if (!json)
            {
                stderr.WriteLine(ex.Message);
                return 1;
            }

            WriteAnalyzeFailureJson(stdout, stepPath, ex);
            return 1;
        }

        if (!json)
        {
            stderr.WriteLine("Analyze map currently requires --json output. Re-run with --json.");
            return 1;
        }

        stdout.WriteLine(JsonSerializer.Serialize(map, JsonOptions));
        return 0;
    }

    private static int RunAnalyzeSection(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0)
        {
            stderr.WriteLine(AnalyzeSectionUsage);
            stderr.WriteLine("Run 'aetheris analyze section --help' for examples.");
            return 1;
        }

        if (IsHelpFlag(args[0]))
        {
            WriteAnalyzeSectionHelp(stdout);
            return 0;
        }

        if (args[0].StartsWith("-", StringComparison.Ordinal))
        {
            stderr.WriteLine("Analyze section requires <file.step> as the first argument.");
            stderr.WriteLine(AnalyzeSectionUsage);
            return 1;
        }

        var stepPath = args[0];
        SectionPlaneFamily? plane = null;
        var planeOptionCount = 0;
        double? offset = null;
        var json = false;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--xy":
                    plane = SectionPlaneFamily.XY;
                    planeOptionCount++;
                    break;
                case "--xz":
                    plane = SectionPlaneFamily.XZ;
                    planeOptionCount++;
                    break;
                case "--yz":
                    plane = SectionPlaneFamily.YZ;
                    planeOptionCount++;
                    break;
                case "--offset" when i + 1 < args.Length && double.TryParse(args[++i], out var parsedOffset):
                    offset = parsedOffset;
                    break;
                case "--offset":
                    stderr.WriteLine("Analyze section option --offset requires a numeric value.");
                    stderr.WriteLine(AnalyzeSectionUsage);
                    return 1;
                case "--json":
                    json = true;
                    break;
                case "-h":
                case "--help":
                    WriteAnalyzeSectionHelp(stdout);
                    return 0;
                default:
                    stderr.WriteLine($"Unknown analyze section option '{args[i]}'.");
                    stderr.WriteLine(AnalyzeSectionUsage);
                    return 1;
            }
        }

        if (!plane.HasValue || planeOptionCount != 1)
        {
            stderr.WriteLine("Analyze section requires exactly one plane selector (--xy|--xz|--yz).");
            return 1;
        }

        if (!offset.HasValue)
        {
            stderr.WriteLine("Analyze section requires --offset <value>.");
            return 1;
        }

        if (!json)
        {
            stderr.WriteLine("Analyze section currently requires --json output. Re-run with --json.");
            return 1;
        }

        SectionAnalysisResult section;
        try
        {
            section = StepAnalyzer.AnalyzeSection(stepPath, plane.Value, offset.Value);
        }
        catch (Exception ex)
        {
            WriteAnalyzeFailureJson(stdout, stepPath, ex);
            return 1;
        }

        stdout.WriteLine(JsonSerializer.Serialize(section, JsonOptions));
        return 0;
    }

    private static void WriteSummaryText(AnalyzeResult analysis, TextWriter stdout)
    {
        var summary = analysis.Summary;
        stdout.WriteLine($"Input file: {analysis.StepPath}");
        stdout.WriteLine("Success: yes");
        stdout.WriteLine($"Structural assessment: {summary.StructuralAssessment} ({summary.StructuralAssessmentBasis})");
        stdout.WriteLine($"Length unit: {summary.LengthUnit} ({summary.LengthUnitBasis})");
        stdout.WriteLine($"Bodies: {summary.BodyCount}");
        stdout.WriteLine($"Shells: {summary.ShellCount}");
        stdout.WriteLine($"Bounding box: {FormatBox(summary.BoundingBox)}");
        stdout.WriteLine($"Faces: {summary.FaceCount}");
        stdout.WriteLine($"Edges: {summary.EdgeCount}");
        stdout.WriteLine($"Vertices: {summary.VertexCount}");
        stdout.WriteLine($"Face IDs: min={summary.FaceIds.Min}, max={summary.FaceIds.Max}, count={summary.FaceIds.Count}, contiguous={summary.FaceIds.Contiguous}");
        stdout.WriteLine($"Edge IDs: min={summary.EdgeIds.Min}, max={summary.EdgeIds.Max}, count={summary.EdgeIds.Count}, contiguous={summary.EdgeIds.Contiguous}");
        stdout.WriteLine($"Vertex IDs: min={summary.VertexIds.Min}, max={summary.VertexIds.Max}, count={summary.VertexIds.Count}, contiguous={summary.VertexIds.Contiguous}");
        stdout.WriteLine("Surface Families:");
        foreach (var family in summary.SurfaceFamilies)
        {
            stdout.WriteLine($"  {family.Key}: {family.Value}");
        }

        if (analysis.Face is not null)
        {
            stdout.WriteLine($"Face {analysis.Face.FaceId}: type={analysis.Face.SurfaceType ?? "n/a"}, status={analysis.Face.SurfaceStatus}, bbox={FormatBox(analysis.Face.BoundingBox)}, point={FormatPoint(analysis.Face.RepresentativePoint)}, anchor={FormatPoint(analysis.Face.AnchorPoint)}, apex={FormatPoint(analysis.Face.Apex)}, normal={FormatVector(analysis.Face.PlanarNormal)}, axis={FormatVector(analysis.Face.Axis)}, radius={FormatDouble(analysis.Face.Radius)}, placementRadius={FormatDouble(analysis.Face.PlacementRadius)}, majorRadius={FormatDouble(analysis.Face.MajorRadius)}, minorRadius={FormatDouble(analysis.Face.MinorRadius)}, semiAngleRadians={FormatDouble(analysis.Face.SemiAngleRadians)}, edges=[{string.Join(",", analysis.Face.AdjacentEdgeIds)}]");
        }

        if (analysis.Edge is not null)
        {
            stdout.WriteLine($"Edge {analysis.Edge.EdgeId}: curve={analysis.Edge.CurveType}, start={analysis.Edge.StartVertexId}:{FormatPoint(analysis.Edge.StartVertex)}, end={analysis.Edge.EndVertexId}:{FormatPoint(analysis.Edge.EndVertex)}, faces=[{string.Join(",", analysis.Edge.AdjacentFaceIds)}], parameterRange={FormatDouble(analysis.Edge.ParameterRange)}, arcLength={FormatDouble(analysis.Edge.ArcLength)}, arcLengthStatus={analysis.Edge.ArcLengthStatus}");
        }

        if (analysis.Vertex is not null)
        {
            stdout.WriteLine($"Vertex {analysis.Vertex.VertexId}: xyz={FormatPoint(analysis.Vertex.Position)}, edges=[{string.Join(",", analysis.Vertex.IncidentEdgeIds)}]");
        }

        if (analysis.Notes.Count > 0)
        {
            stdout.WriteLine("Notes:");
            foreach (var note in analysis.Notes)
            {
                stdout.WriteLine($"  - {note}");
            }
        }
    }

    private static void WriteAnalyzeFailureText(TextWriter stderr, string stepPath, Exception exception)
    {
        var fullPath = Path.GetFullPath(stepPath);
        if (exception is not StepAnalysisImportException importFailure)
        {
            stderr.WriteLine($"Analyze failed for: {fullPath}");
            stderr.WriteLine($"Reason: {exception.Message}");
            return;
        }

        var multiRootDiagnostic = importFailure.Diagnostics.FirstOrDefault(d => string.Equals(d.Source, "Importer.AssemblyLike.StepMultiRoot", StringComparison.Ordinal));
        if (multiRootDiagnostic is not null)
        {
            var rigidRootCount = CountExactBrepRigidRoots(multiRootDiagnostic.Message);
            stderr.WriteLine($"Input file: {fullPath}");
            stderr.WriteLine("Success: no");
            stderr.WriteLine("Classification: assembly-like STEP (multi-root exact BRep solids).");
            if (rigidRootCount.HasValue)
            {
                stderr.WriteLine($"Detected exact BRep rigid roots: {rigidRootCount.Value}");
            }

            stderr.WriteLine("Guidance: Use the assembly extraction/import workflow for this STEP input.");
            stderr.WriteLine($"Reason: {multiRootDiagnostic.Message}");
            return;
        }

        stderr.WriteLine($"Analyze failed for: {fullPath}");
        stderr.WriteLine("Reason: STEP import failure.");
        foreach (var diagnostic in importFailure.Diagnostics)
        {
            stderr.WriteLine($"- [{diagnostic.Severity}] {diagnostic.Source}: {diagnostic.Message}");
        }
    }

    private static int UnknownCommand(string command, TextWriter stderr)
    {
        stderr.WriteLine($"Unknown command '{command}'. Expected one of: build, analyze, trace, canon, asm, experimental.");
        stderr.WriteLine("Run 'aetheris --help' for usage and examples.");
        return 1;
    }

    private static bool IsHelpFlag(string value) =>
        string.Equals(value, "--help", StringComparison.Ordinal)
        || string.Equals(value, "-h", StringComparison.Ordinal);

    private static bool IsVersionFlag(string value) =>
        string.Equals(value, "--version", StringComparison.Ordinal)
        || string.Equals(value, "-v", StringComparison.Ordinal);

    private static string GetDisplayVersion()
    {
        var version = typeof(CliRunner).Assembly.GetName().Version;
        return version is null ? "unknown" : version.ToString();
    }

    private static void WriteTopLevelHelp(TextWriter stdout)
    {
        stdout.WriteLine("aetheris - firmament build and STEP analysis CLI");
        stdout.WriteLine();
        stdout.WriteLine(TopLevelUsage);
        stdout.WriteLine();
        stdout.WriteLine("Commands:");
        stdout.WriteLine("  build      Build a .firmament source file into STEP.");
        stdout.WriteLine("  analyze    Analyze STEP topology, geometry, map, and sections.");
        stdout.WriteLine("  trace      Trace built-in AIR lowering cases through route, BRepPlan, STEP smoke, and CIR mirror.");
        stdout.WriteLine("  canon      Import and re-export STEP/AP242 as canonical STEP.");
        stdout.WriteLine("  asm        Execute/export .firmasm assembly IR using rigid world-space composition.");
        stdout.WriteLine("  experimental  Experimental/lab-only artifact export and generated-source inspection commands.");
        stdout.WriteLine();
        stdout.WriteLine("Global options:");
        stdout.WriteLine("  -h, --help       Show help.");
        stdout.WriteLine("  -v, --version    Show CLI version.");
        stdout.WriteLine();
        stdout.WriteLine("Examples:");
        stdout.WriteLine("  aetheris build model.firmament --out model.step");
        stdout.WriteLine("  aetheris analyze model.step");
        stdout.WriteLine("  aetheris analyze model.step --json");
        stdout.WriteLine("  aetheris trace --case top-face-loop-chamfer");
        stdout.WriteLine("  aetheris trace --fixture fixtures/Firmament/Chamfer/valid/top-face-loop-chamfer.valid.firmfixture");
        stdout.WriteLine("  aetheris trace --case prismatic-section-transition --json");
        stdout.WriteLine("  aetheris canon input.step --out canonical.step --json");
        stdout.WriteLine("  aetheris asm exec assembly.firmasm --json");
        stdout.WriteLine("  aetheris asm export assembly.firmasm --out out/assembly-roundtrip --json");
        stdout.WriteLine("  aetheris experimental airchamfer-cube --out edge-x10-airchamfer-cube-one-edge.step --json");
        stdout.WriteLine("  aetheris experimental prismatic-map --case rectangle-inset --rows 16 --cols 16 --json");
        stdout.WriteLine("  aetheris analyze map model.step --top --rows 40 --cols 60 --json");
        stdout.WriteLine("  aetheris analyze section model.step --xy --offset 2.5 --json");
        stdout.WriteLine("  aetheris analyze volume model.step --json");
        stdout.WriteLine();
        stdout.WriteLine("Run 'aetheris <command> --help' for command-specific usage.");
    }

    private static void WriteBuildHelp(TextWriter stdout)
    {
        stdout.WriteLine("Build .firmament input into STEP output.");
        stdout.WriteLine();
        stdout.WriteLine(BuildUsage);
        stdout.WriteLine();
        stdout.WriteLine("Options:");
        stdout.WriteLine("  --out <path>   Optional output STEP path.");
        stdout.WriteLine("  --json         Emit machine-readable success/failure JSON.");
        stdout.WriteLine("  -h, --help     Show this help.");
        stdout.WriteLine();
        stdout.WriteLine("Example:");
        stdout.WriteLine("  aetheris build part.firmament --out part.step --json");
    }

    private static void WriteTraceHelp(TextWriter stdout)
    {
        stdout.WriteLine("Trace a built-in Aetheris lowering case through AIR, route selection, BRepPlan, emitted BRep/STEP smoke, and CIR mirror admission. Use `analyze` for existing STEP/BRep geometry.");
        stdout.WriteLine();
        stdout.WriteLine(TraceUsage);
        stdout.WriteLine();
        stdout.WriteLine("Scope:");
        stdout.WriteLine("  trace reports compiler lowering; analyze reports geometric analysis of existing STEP/BRep artifacts.");
        stdout.WriteLine("  Supported cases: prismatic-section-transition, top-face-loop-chamfer");
        stdout.WriteLine("  Supported fixture extensions: .valid.firmfixture, .invalid.firmfixture.");
        stdout.WriteLine("  Optional aliases: prismatic, loop-chamfer.");
        stdout.WriteLine("  No STEP input is accepted by trace.");
        stdout.WriteLine();
        stdout.WriteLine("Options:");
        stdout.WriteLine("  --case <name>      Built-in lowering case name.");
        stdout.WriteLine("  --fixture <path>   Firmament fixture trace input (.valid/.invalid.firmfixture).");
        stdout.WriteLine("  --json          Emit deterministic machine-readable JSON (default output is human-readable text).");
        stdout.WriteLine("  --out-dir <dir> Write trace artifacts into a directory; the controlled side-hole fixture also writes side-hole.step, side-hole.trace.json, side-hole.trace.txt, and manifest.json.");
        stdout.WriteLine("  -h, --help      Show this help.");
        stdout.WriteLine();
        stdout.WriteLine("Examples:");
        stdout.WriteLine("  aetheris trace --case prismatic-section-transition");
        stdout.WriteLine("  aetheris trace --case top-face-loop-chamfer --json");
        stdout.WriteLine("  aetheris trace --fixture fixtures/Firmament/Chamfer/valid/top-face-loop-chamfer.valid.firmfixture --json");
        stdout.WriteLine("  aetheris trace --fixture fixtures/Firmament/Chamfer/valid/top-face-loop-chamfer.valid.firmfixture --out-dir artifacts/air-x7");
        stdout.WriteLine("  aetheris trace --fixture fixtures/Firmament/Region/valid/side-hole-face-attached-region.valid.firmfixture --out-dir artifacts/air-region-x13/side-hole");
    }

    private static void WriteAnalyzeHelp(TextWriter stdout)
    {
        stdout.WriteLine("Analyze part-like STEP geometry and topology.");
        stdout.WriteLine();
        stdout.WriteLine(AnalyzeUsage);
        stdout.WriteLine($"   or: {AnalyzeMapUsage[7..]}");
        stdout.WriteLine($"   or: {AnalyzeSectionUsage[7..]}");
        stdout.WriteLine();
        stdout.WriteLine("Options (summary mode):");
        stdout.WriteLine("  --face <id>     Inspect one face.");
        stdout.WriteLine("  --edge <id>     Inspect one edge.");
        stdout.WriteLine("  --vertex <id>   Inspect one vertex.");
        stdout.WriteLine("  --json          Emit machine-readable JSON (default output is human-readable text).");
        stdout.WriteLine("  -h, --help      Show this help.");
        stdout.WriteLine();
        stdout.WriteLine("Rules:");
        stdout.WriteLine("  - At most one of --face, --edge, --vertex may be supplied.");
        stdout.WriteLine("  - Use 'aetheris analyze map --help' for orthographic map options.");
        stdout.WriteLine("  - Use 'aetheris analyze section --help' for section options.");
        stdout.WriteLine("  - Use 'aetheris analyze volume --help' for volume options.");
        stdout.WriteLine("  - Assembly-like multi-root STEP is rejected here with a route hint to assembly extraction/import.");
        stdout.WriteLine();
        stdout.WriteLine("Examples:");
        stdout.WriteLine("  aetheris analyze part.step");
        stdout.WriteLine("  aetheris analyze part.step --json");
        stdout.WriteLine("  aetheris analyze part.step --face 12");
        stdout.WriteLine("  aetheris analyze part.step --face 12 --json");
        stdout.WriteLine("  aetheris analyze map part.step --right --rows 20 --cols 30 --json");
        stdout.WriteLine("  aetheris analyze section part.step --yz --offset 1.25 --json");
    }

    private static void WriteAnalyzeMapHelp(TextWriter stdout)
    {
        stdout.WriteLine("Analyze STEP body as an orthographic depth/thickness map.");
        stdout.WriteLine();
        stdout.WriteLine(AnalyzeMapUsage);
        stdout.WriteLine();
        stdout.WriteLine("Required:");
        stdout.WriteLine("  exactly one view: --top | --bottom | --front | --back | --left | --right");
        stdout.WriteLine("  --rows <N>       Positive integer row count.");
        stdout.WriteLine("  --cols <N>       Positive integer column count.");
        stdout.WriteLine("  --json           Required output mode.");
        stdout.WriteLine();
        stdout.WriteLine("Example:");
        stdout.WriteLine("  aetheris analyze map part.step --top --rows 48 --cols 64 --json");
    }

    private static void WriteAnalyzeSectionHelp(TextWriter stdout)
    {
        stdout.WriteLine("Analyze STEP body by intersecting a principal section plane.");
        stdout.WriteLine();
        stdout.WriteLine(AnalyzeSectionUsage);
        stdout.WriteLine();
        stdout.WriteLine("Required:");
        stdout.WriteLine("  exactly one plane: --xy | --xz | --yz");
        stdout.WriteLine("  --offset <value>  Plane offset along the orthogonal axis.");
        stdout.WriteLine("  --json            Required output mode.");
        stdout.WriteLine();
        stdout.WriteLine("Example:");
        stdout.WriteLine("  aetheris analyze section part.step --xz --offset 5.0 --json");
    }

    private static void WriteAnalyzeVolumeHelp(TextWriter stdout)
    {
        stdout.WriteLine(AnalyzeVolumeUsage);
        stdout.WriteLine();
        stdout.WriteLine("Analyze STEP body volume using kernel B-rep topology/geometry.");
        stdout.WriteLine();
        stdout.WriteLine("Options:");
        stdout.WriteLine("  --json         Emit machine-readable JSON output.");
        stdout.WriteLine("  --approximate  Compute explicit voxel-based approximate volume.");
        stdout.WriteLine("  --resolution   Required with --approximate. Integer samples along longest bbox axis.");
        stdout.WriteLine();
        stdout.WriteLine("Notes:");
        stdout.WriteLine("  - Exact whole-body volume currently supports canonical spheres and single-lateral-face cylinders.");
        stdout.WriteLine("  - Approximate mode is opt-in and deterministic (center-point voxel occupancy sampling).");
        stdout.WriteLine("  - Exact sub-box volume clipping (--box ...) is deferred in ANALYZE-P1.");
        stdout.WriteLine();
        stdout.WriteLine("Examples:");
        stdout.WriteLine("  aetheris analyze volume part.step");
        stdout.WriteLine("  aetheris analyze volume part.step --json");
        stdout.WriteLine("  aetheris analyze volume part.step --approximate --resolution 64 --json");
    }

    private static void WriteCanonHelp(TextWriter stdout)
    {
        stdout.WriteLine("Canonicalize part-like STEP/AP242 through Aetheris import/export.");
        stdout.WriteLine();
        stdout.WriteLine(CanonUsage);
        stdout.WriteLine();
        stdout.WriteLine("Options:");
        stdout.WriteLine("  --out <path>   Required canonical AP242 output path.");
        stdout.WriteLine("  (Assembly-like multi-root STEP is not canonicalized by this command.)");
        stdout.WriteLine("  --json         Emit machine-readable success/failure JSON.");
        stdout.WriteLine("  -h, --help     Show this help.");
        stdout.WriteLine();
        stdout.WriteLine("Example:");
        stdout.WriteLine("  aetheris canon input.step --out canonical.step --json");
    }


    private static void WriteExperimentalHelp(TextWriter stdout)
    {
        stdout.WriteLine("Experimental/lab-only artifact export commands.");
        stdout.WriteLine();
        stdout.WriteLine(ExperimentalUsage);
        stdout.WriteLine();
        stdout.WriteLine("Subcommands:");
        stdout.WriteLine("  airchamfer-cube    Export a controlled one-edge AirChamfer candidate cube/box STEP artifact.");
        stdout.WriteLine("  airchamfer-corpus  Generate the EDGE-X11 tiny AirChamfer STEP regression corpus.");
        stdout.WriteLine("  prismatic-corpus   Generate the EDGE-PRISMATIC-X5 split-preserving prismatic corpus.");
        stdout.WriteLine("  prismatic-map      Inspect EDGE-PRISMATIC-X9 generated-source-only prismatic map JSON.");
        stdout.WriteLine("  loop-chamfer-corpus Generate the EDGE-LOOP-X2 top-face loop chamfer STEP/JSON corpus.");
        stdout.WriteLine();
        stdout.WriteLine("Notes:");
        stdout.WriteLine("  - Experimental only; does not route production Firmament chamfer operations through AirChamfer.");
        stdout.WriteLine("  - Legacy BrepBoundedChamfer remains production-authoritative.");
        stdout.WriteLine("  - The candidate path uses no 3D Boolean fallback.");
        stdout.WriteLine("  - The prismatic corpus preserves section-boundary split faces and performs no coplanar merge.");
        stdout.WriteLine("  - experimental prismatic-map is generated-source-only, not normal analyze map, and accepts no STEP input.");
        stdout.WriteLine("  - loop-chamfer-corpus is a lab-only Class B top-face outer-loop route and does not change production chamfer or fillet behavior.");
        stdout.WriteLine();
        stdout.WriteLine("Examples:");
        stdout.WriteLine("  aetheris experimental airchamfer-cube --out edge-x10-airchamfer-cube-one-edge.step --json");
        stdout.WriteLine("  aetheris experimental airchamfer-corpus --out-dir artifacts/edge-x11 --json");
        stdout.WriteLine("  aetheris experimental prismatic-corpus --out-dir artifacts/edge-prismatic-x5 --json");
        stdout.WriteLine("  aetheris experimental prismatic-map --case rectangle-inset --rows 16 --cols 16 --json");
        stdout.WriteLine("  aetheris experimental loop-chamfer-corpus --out-dir artifacts/edge-loop-x2 --json");
    }

    private static void WriteExperimentalLoopChamferCorpusHelp(TextWriter stdout)
    {
        stdout.WriteLine("Generate the EDGE-LOOP-X2 top-face outer-loop chamfer artifact corpus.");
        stdout.WriteLine();
        stdout.WriteLine(ExperimentalLoopChamferCorpusUsage);
        stdout.WriteLine();
        stdout.WriteLine("Options:");
        stdout.WriteLine("  --out-dir <dir>  Required deterministic corpus output directory.");
        stdout.WriteLine("  --json           Emit the corpus JSON summary to stdout after writing it to disk.");
        stdout.WriteLine("  -h, --help       Show this help.");
        stdout.WriteLine();
        stdout.WriteLine("Artifacts:");
        stdout.WriteLine("  edge-loop-x2-canonical-top-face-loop-chamfer.step");
        stdout.WriteLine("  edge-loop-x2-larger-top-face-loop-chamfer.step");
        stdout.WriteLine("  edge-loop-x2-non-square-top-face-loop-chamfer.step");
        stdout.WriteLine($"  {EdgeLoopX2TopFaceLoopChamferCorpusLab.DefaultSummaryFileName}");
        stdout.WriteLine();
        stdout.WriteLine("Production safety:");
        stdout.WriteLine("  Experimental/lab-only route; no production route replacement, AirEdgeSweep, BrepBoundedChamfer, topology graft, 3D Boolean, coplanar merge, or production chamfer/fillet behavior change.");
        stdout.WriteLine();
        stdout.WriteLine("Example:");
        stdout.WriteLine("  aetheris experimental loop-chamfer-corpus --out-dir artifacts/edge-loop-x2 --json");
    }

    private static void WriteExperimentalAirChamferCubeHelp(TextWriter stdout)
    {
        stdout.WriteLine("Export a controlled one-edge AirChamfer candidate cube/box STEP artifact.");
        stdout.WriteLine();
        stdout.WriteLine(ExperimentalAirChamferCubeUsage);
        stdout.WriteLine();
        stdout.WriteLine("Options:");
        stdout.WriteLine("  --out <path>   Required deterministic STEP output path.");
        stdout.WriteLine("  --json         Emit machine-readable success/failure JSON.");
        stdout.WriteLine("  -h, --help     Show this help.");
        stdout.WriteLine();
        stdout.WriteLine("Expected artifact name:");
        stdout.WriteLine($"  {AirChamferStepArtifactLab.DefaultArtifactFileName}");
        stdout.WriteLine();
        stdout.WriteLine("Production safety:");
        stdout.WriteLine("  Experimental/lab-only route; no production chamfer route replacement and no 3D Boolean.");
        stdout.WriteLine();
        stdout.WriteLine("Example:");
        stdout.WriteLine("  aetheris experimental airchamfer-cube --out edge-x10-airchamfer-cube-one-edge.step --json");
    }

    private static void WriteExperimentalAirChamferCorpusHelp(TextWriter stdout)
    {
        stdout.WriteLine("Generate the EDGE-X11 tiny AirChamfer STEP artifact regression corpus.");
        stdout.WriteLine();
        stdout.WriteLine(ExperimentalAirChamferCorpusUsage);
        stdout.WriteLine();
        stdout.WriteLine("Options:");
        stdout.WriteLine("  --out-dir <dir>  Required deterministic corpus output directory.");
        stdout.WriteLine("  --json           Emit the corpus JSON summary to stdout after writing it to disk.");
        stdout.WriteLine("  -h, --help       Show this help.");
        stdout.WriteLine();
        stdout.WriteLine("Artifacts:");
        stdout.WriteLine("  edge-x11-airchamfer-cube-canonical.step");
        stdout.WriteLine("  edge-x11-airchamfer-cube-nonorthogonal.step when supported; otherwise JSON-only deferred diagnostics.");
        stdout.WriteLine($"  {AirChamferStepArtifactLab.DefaultCorpusSummaryFileName}");
        stdout.WriteLine();
        stdout.WriteLine("Production safety:");
        stdout.WriteLine("  Experimental/lab-only route; no production chamfer route replacement and no 3D Boolean.");
        stdout.WriteLine();
        stdout.WriteLine("Example:");
        stdout.WriteLine("  aetheris experimental airchamfer-corpus --out-dir artifacts/edge-x11 --json");
    }


    private static void WriteExperimentalPrismaticCorpusHelp(TextWriter stdout)
    {
        stdout.WriteLine("Generate the EDGE-PRISMATIC-X5 split-preserving prismatic section-transition artifact corpus.");
        stdout.WriteLine();
        stdout.WriteLine(ExperimentalPrismaticCorpusUsage);
        stdout.WriteLine();
        stdout.WriteLine("Options:");
        stdout.WriteLine("  --out-dir <dir>  Required deterministic corpus output directory.");
        stdout.WriteLine("  --json           Emit the corpus JSON summary to stdout after writing it to disk.");
        stdout.WriteLine("  -h, --help       Show this help.");
        stdout.WriteLine();
        stdout.WriteLine("Artifacts:");
        stdout.WriteLine("  edge-prismatic-x5-rectangle-inset.step");
        stdout.WriteLine("  edge-prismatic-x5-top-edge-chamfer.step");
        stdout.WriteLine("  edge-prismatic-x5-pentagon-scaled.step");
        stdout.WriteLine("  edge-prismatic-x5-hexagon-scaled.step");
        stdout.WriteLine("  edge-prismatic-x5-pentagon-asymmetric.step");
        stdout.WriteLine($"  {PrismaticSectionTransitionCorpusLab.DefaultSummaryFileName}");
        stdout.WriteLine();
        stdout.WriteLine("Production safety:");
        stdout.WriteLine("  Experimental/lab-only route; no production route replacement, AirEdgeSweep, BrepBoundedChamfer, topology graft, 3D Boolean, and no coplanar merge.");
        stdout.WriteLine();
        stdout.WriteLine("Example:");
        stdout.WriteLine("  aetheris experimental prismatic-corpus --out-dir artifacts/edge-prismatic-x5 --json");
    }



    private static void WriteExperimentalPrismaticMapHelp(TextWriter stdout)
    {
        stdout.WriteLine("Inspect the EDGE-PRISMATIC-X9 generated-source-only prismatic map proof as JSON.");
        stdout.WriteLine();
        stdout.WriteLine(ExperimentalPrismaticMapUsage);
        stdout.WriteLine();
        stdout.WriteLine("Options:");
        stdout.WriteLine("  --case <case>     Required generated case: rectangle-inset or top-edge-chamfer.");
        stdout.WriteLine("  --rows <N>        Required positive row count.");
        stdout.WriteLine("  --cols <N>        Required positive column count.");
        stdout.WriteLine("  --json            Required machine-readable output; no stable text API is promised.");
        stdout.WriteLine("  --request <use>   Optional; only map-occupancy is supported. Face identity/topology parity reject as lossy.");
        stdout.WriteLine("  -h, --help        Show this help.");
        stdout.WriteLine();
        stdout.WriteLine("Scope and authority:");
        stdout.WriteLine("  - Experimental generated AIR/prismatic source route only.");
        stdout.WriteLine("  - This is not normal 'aetheris analyze map' and does not change its STEP behavior.");
        stdout.WriteLine("  - No STEP input, imported STEP prismatic body, or arbitrary user geometry is accepted.");
        stdout.WriteLine("  - Output supports map occupancy only; no topology or face identity claims are made.");
        stdout.WriteLine();
        stdout.WriteLine("Example:");
        stdout.WriteLine("  aetheris experimental prismatic-map --case rectangle-inset --rows 16 --cols 16 --json");
    }

    private static void WriteAsmHelp(TextWriter stdout)
    {
        stdout.WriteLine("Execute flattened .firmasm assemblies as rigidly placed body instances, or export STEP interop packages.");
        stdout.WriteLine();
        stdout.WriteLine(AsmExecUsage);
        stdout.WriteLine($"   or: {AsmExportUsage[7..]}");
        stdout.WriteLine();
        stdout.WriteLine("Options:");
        stdout.WriteLine("  --out <path>   Required for 'asm export'; output directory for package artifacts.");
        stdout.WriteLine("  --json         Emit machine-readable execution and analyzer JSON.");
        stdout.WriteLine("  -h, --help     Show this help.");
        stdout.WriteLine();
        stdout.WriteLine("Example:");
        stdout.WriteLine("  aetheris asm exec testdata/firmasm/examples/occt-as1/as1-assembly.firmasm --json");
        stdout.WriteLine("  aetheris asm export testdata/firmasm/examples/occt-nut-bolt/nut-bolt-assembly.firmasm --out tmp/nutbolt-export --json");
    }

    private static string FormatBox(Aetheris.Kernel.Core.Math.BoundingBox3D? box) =>
        box is null ? "unknown" : $"min{FormatPoint(box.Value.Min)} max{FormatPoint(box.Value.Max)}";

    private static string FormatPoint(Aetheris.Kernel.Core.Math.Point3D? point) =>
        point is null ? "unknown" : $"({point.Value.X:F6},{point.Value.Y:F6},{point.Value.Z:F6})";

    private static string FormatVector(Aetheris.Kernel.Core.Math.Vector3D? vector) =>
        vector is null ? "n/a" : $"({vector.Value.X:F6},{vector.Value.Y:F6},{vector.Value.Z:F6})";

    private static string FormatDouble(double? value) => value?.ToString("G17") ?? "n/a";
}
