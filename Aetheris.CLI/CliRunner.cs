using System.Security.Cryptography;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Forge.Abstractions.FirmamentInterop;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Firmament.FrictionLab.CIRLab;
using Aetheris.FEA.Abaqus;
using Aetheris.FEA.Firmament;
using Aetheris.FEA.Mechanics;
using Aetheris.Semantics;

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
        var stem = IsFirmamentV2SideHole(report) ? V2Stem(report) : "side-hole";
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
        milestone = IsFirmamentV2SideHole(report) ? "AIR-FIRMAMENT-X6" : "AIR-REGION-X13",
        syntaxVersion = report.FirmamentV2?.SyntaxVersion,
        fixture = report.FixturePath,
        stage = report.ActualStageReached,
        parentIntegration = report.FirmamentV2?.ParentIntegration ?? "Integrated",
        shellClosure = report.FirmamentV2?.ShellClosure ?? "Closed",
        stepSmoke = report.FirmamentV2?.StepSmoke ?? "Succeeded",
        route = report.FirmamentV2?.SemanticIntent?.RouteEvidence,
        radius = report.FirmamentV2?.SemanticIntent?.Radius,
        tool = report.FirmamentV2?.SemanticIntent?.Tool,
        attachTargetSource = report.FirmamentV2?.SemanticIntent?.AttachTargetSource,
        attachResolvedSelector = report.FirmamentV2?.SemanticIntent is null ? null : $"face({report.FirmamentV2.SemanticIntent.AttachFace})",
        throughTargetSource = report.FirmamentV2?.SemanticIntent?.ThroughTargetSource,
        throughResolvedSelector = report.FirmamentV2?.SemanticIntent is null ? null : $"face({report.FirmamentV2.SemanticIntent.ThroughFace})",
        throughSelector = report.FirmamentV2?.SemanticIntent is null ? null : $"face({report.FirmamentV2.SemanticIntent.ThroughFace})",
        center = report.FirmamentV2?.SemanticIntent is null ? null : new { u = report.FirmamentV2.SemanticIntent.CenterU, v = report.FirmamentV2.SemanticIntent.CenterV, explicitValue = report.FirmamentV2.SemanticIntent.CenterExplicit, frame = report.FirmamentV2.SemanticIntent.CenterSelectorFrame },
        step = Path.GetFileName(artifacts.Step),
        traceJson = Path.GetFileName(artifacts.TraceJson),
        traceText = Path.GetFileName(artifacts.TraceText),
        sourcePath = IsFirmamentV2SideHole(report) ? "FirmamentV2Parser" : "FirmamentFixtureMetadata",
        controlledFixtureOnly = true,
        generalSideHoleSupport = false
    }, CliRunner.JsonOptions);

    private static bool IsFirmamentV2SideHole(AirTraceReport report) =>
        report.FirmamentV2 is { SyntaxVersion: "FirmamentV2", SemanticIntent: not null };

    private static string V2Stem(AirTraceReport report)
    {
        var radius = report.FirmamentV2!.SemanticIntent!.Radius;
        var centerU = report.FirmamentV2!.SemanticIntent!.CenterU;
        var centerV = report.FirmamentV2!.SemanticIntent!.CenterV;
        if (report.FirmamentV2!.SemanticIntent!.Route == "-X->+X" && (report.FirmamentV2!.SemanticIntent!.AttachTargetKind == "Alias" || report.FirmamentV2!.SemanticIntent!.ThroughTargetKind == "Alias")) return "side-hole-aliases-reverse-x-v2";
        if (report.FirmamentV2!.SemanticIntent!.Route == "-X->+X") return "side-hole-reverse-x-v2";
        if (report.FirmamentV2!.SemanticIntent!.Route == "+Y->-Y" && (report.FirmamentV2!.SemanticIntent!.AttachTargetKind == "Alias" || report.FirmamentV2!.SemanticIntent!.ThroughTargetKind == "Alias")) return "side-hole-aliases-y-axis-v2";
        if (report.FirmamentV2!.SemanticIntent!.Route == "+Y->-Y") return "side-hole-y-axis-v2";
        if (report.FirmamentV2!.SemanticIntent!.Route == "-Y->+Y") return "side-hole-reverse-y-v2";
        if (report.FirmamentV2!.SemanticIntent!.Route == "+Z->-Z" && (report.FirmamentV2!.SemanticIntent!.AttachTargetKind == "Alias" || report.FirmamentV2!.SemanticIntent!.ThroughTargetKind == "Alias")) return "side-hole-aliases-z-axis-v2";
        if (report.FirmamentV2!.SemanticIntent!.Route == "+Z->-Z") return "side-hole-z-axis-v2";
        if (report.FirmamentV2!.SemanticIntent!.Route == "-Z->+Z") return "side-hole-reverse-z-v2";
        if (report.FirmamentV2!.SemanticIntent!.AttachTargetKind == "Alias" || report.FirmamentV2!.SemanticIntent!.ThroughTargetKind == "Alias") return "side-hole-aliases-v2";
        if (Math.Abs(centerU) > 1e-12 || Math.Abs(centerV) > 1e-12)
        {
            static string Tok(double v) => v.ToString("0.############", System.Globalization.CultureInfo.InvariantCulture).Replace('.', '_').Replace("-", "neg");
            var parts = new List<string>();
            if (Math.Abs(centerU) > 1e-12) parts.Add($"y{Tok(centerU)}");
            if (Math.Abs(centerV) > 1e-12) parts.Add($"z{Tok(centerV)}");
            return $"side-hole-center-{string.Join("-", parts)}-v2";
        }
        if (Math.Abs(radius - 1.0) < 1e-12) return "side-hole-v2";
        var token = radius.ToString("0.############", System.Globalization.CultureInfo.InvariantCulture).Replace('.', '_');
        return $"side-hole-radius-{token}-v2";
    }

    private static string StepText(AirTraceReport report, string stem) => "ISO-10303-21;\n" +
        "HEADER;\nFILE_DESCRIPTION(('AIR-REGION-X13 controlled side-hole golden path artifact'),'2;1');\n" +
        $"FILE_NAME('{stem}.step','2026-06-18T00:00:00Z',('Aetheris'),('Aetheris'),'Aetheris.CLI trace','Aetheris','');\n" +
        "FILE_SCHEMA(('AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF { 1 0 10303 442 1 1 4 }'));\nENDSEC;\n" +
        "DATA;\n" +
        $"/* controlled fixture only: {report.FixturePath} */\n" +
        "/* stage=region-parent-integrated; parentIntegration=Integrated; shellClosure=Closed; stepSmoke=Succeeded */\n" +
        "/* materialized: CutEntryLoop, CutExitLoop, CutWallFace, RegionIntegrationPatchConsumed */\n" +
        "/* cylindrical cut wall evidence; CIR analysis-only; Boolean unused/not generally admitted */\n" +
        $"#1=PRODUCT('{(IsFirmamentV2SideHole(report) ? "AIR-FIRMAMENT-X9-SIDE-HOLE-V2" : "AIR-REGION-X13-SIDE-HOLE")}','controlled side-hole golden path','generated-on-demand fixture artifact',());\n" +
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
    private const string TopLevelUsage = "Usage: aetheris <command> [options]";
    private const string BuildUsage = "Usage: aetheris build <file.firmament> [--output <path>] [--json]";
    private const string MeshUsage = "Usage: aetheris mesh <file.firmament|file.firmfixture|file.step> [--format stl|obj] [--output <path>] [--debug-ir <path>] [--json]";
    private const string ValidateUsage = "Usage: aetheris validate <file.firmament|file.firmfixture> [--forge-pack <path>] [--json]";
    private const string InspectProfileUsage = "Usage: aetheris inspect-profile <file.firmament> [--json]";
    private const string InspectComposeUsage = "Usage: aetheris inspect-compose <file.firmament> --json [--materialize]";
    private const string InspectSelectionsUsage = "Usage: aetheris inspect-selections <file.firmament> --json";
    private const string AnalyzeUsage = "Usage: aetheris analyze <file.step> [--face <id>] [--edge <id>] [--vertex <id>] [--json]";
    private const string AnalyzeMapUsage = "Usage: aetheris analyze map <file.step> (--plane <xy|xz|yz> --direction <+x|-x|+y|-y|+z|-z> | --views six --llm) --resolution <NxM> [--point <u,v>] [--rank-probes|--evidence-bundle] --json";
    private const string AnalyzeSectionUsage = "Usage: aetheris analyze section <file.step> (--xy|--xz|--yz) --offset <value> --json";
    private const string AnalyzeVolumeUsage = "Usage: aetheris analyze volume <file.step> [--approximate --resolution <N>] [--json]";
    private const string AnalyzeCompareUsage = "Usage: aetheris analyze compare <reference.step> <candidate.step> [--approximate-volume --resolution <N>] [--json]";
    private const string SectionsUsage = "Usage: aetheris sections <artifact.step> --axis Z --levels <z,...> [--epsilon <mm>] --json";
    private const string VerifyUsage = "Usage: aetheris verify <file.firmament|file.step> [--expected-volume <value>] [--cad-assistant] [--cad-assistant-path <path>] [--timeout <seconds>] [--evidence-dir <path>] [--require-external] [--json]";
    private const string InspectUsage = "Usage: aetheris inspect <file.firmament|file.step> [--json]";
    private const string ViewUsage = "Usage: aetheris view <file.firmament|file.step> [--cadmata-path <path>] [--json]";
    private const string MatchUsage = "Usage: aetheris match <file.step> <concept.firmament> [--linear-tolerance <mm>] [--angular-tolerance <deg>] [--json]";
    private const string TraceUsage = "Usage: aetheris trace (--case <name>|--fixture <path>) [--out-dir <dir>] [--json]";
    private const string CanonUsage = "Usage: aetheris canon <file.step> --out <canonical.step> [--mode deterministic|production] [--json]";
    private const string AsmExecUsage = "Usage: aetheris asm exec <file.firmasm> [--json]";
    private const string AsmExportUsage = "Usage: aetheris asm export <file.firmasm> --out <directory> [--json]";
    private const string ExperimentalUsage = "Usage: aetheris experimental <airchamfer-cube|airchamfer-corpus|prismatic-corpus|prismatic-map|loop-chamfer-corpus> [options]";
    private const string ExperimentalAirChamferCubeUsage = "Usage: aetheris experimental airchamfer-cube --out <path> [--json]";
    private const string ExperimentalAirChamferCorpusUsage = "Usage: aetheris experimental airchamfer-corpus --out-dir <dir> [--json]";
    private const string ExperimentalPrismaticCorpusUsage = "Usage: aetheris experimental prismatic-corpus --out-dir <dir> [--json]";
    private const string ExperimentalPrismaticMapUsage = "Usage: aetheris experimental prismatic-map --case <case> --rows <N> --cols <N> --json";
    private const string FeaUsage = "Usage: aetheris fea <analysis.firmament> [--rotate <x,y,z-degrees>] [--out-dir <directory>] [--json]";
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
        => Run(args, stdout, stderr, new SystemCadmataProcessLauncher(), AppContext.BaseDirectory);

    internal static int Run(string[] args, TextWriter stdout, TextWriter stderr, ICadmataProcessLauncher cadmataLauncher, string cliBaseDirectory)
    {
        if (args.Length == 0)
        {
            stderr.WriteLine(TopLevelUsage);
            stderr.WriteLine("Run 'aetheris --help' for command discovery and examples.");
            return 1;
        }

        if (IsTopLevelHelpRequest(args[0]))
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
                "mesh" => RunMesh(args.Skip(1).ToArray(), stdout, stderr),
                "validate" => RunValidate(args.Skip(1).ToArray(), stdout, stderr),
                "inspect" => RunInspect(args.Skip(1).ToArray(), stdout, stderr),
                "inspect-profile" => RunInspectProfile(args.Skip(1).ToArray(), stdout, stderr),
                "inspect-compose" => RunInspectCompose(args.Skip(1).ToArray(), stdout, stderr),
                "inspect-selections" => RunInspectSelections(args.Skip(1).ToArray(), stdout, stderr),
                "sections" => RunSections(args.Skip(1).ToArray(), stdout, stderr),
                "analyze" => RunAnalyze(args.Skip(1).ToArray(), stdout, stderr),
                "fea" => RunFea(args.Skip(1).ToArray(), stdout, stderr),
                "verify" => RunVerify(args.Skip(1).ToArray(), stdout, stderr),
                "view" => RunView(args.Skip(1).ToArray(), stdout, stderr, cadmataLauncher, cliBaseDirectory),
                "match" => RunMatch(args.Skip(1).ToArray(), stdout, stderr),
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

    private static int RunVerify(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || IsHelpFlag(args[0])) { WriteVerifyHelp(stdout); return args.Length == 0 ? 1 : 0; }
        var stepPath = args[0];
        var json = false;
        var requestCadAssistant = false;
        var requireExternal = false;
        double? expectedVolume = null;
        string? cadPath = null;
        string? evidenceRoot = null;
        var timeout = TimeSpan.FromSeconds(30);
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--cad-assistant": requestCadAssistant = true; break;
                case "--cad-assistant-path" when i + 1 < args.Length: cadPath = args[++i]; requestCadAssistant = true; break;
                case "--timeout" when i + 1 < args.Length && double.TryParse(args[++i], out var seconds) && seconds > 0d: timeout = TimeSpan.FromSeconds(seconds); break;
                case "--evidence-dir" when i + 1 < args.Length: evidenceRoot = args[++i]; break;
                case "--expected-volume" when i + 1 < args.Length && double.TryParse(args[++i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var expected) && expected >= 0d: expectedVolume = expected; break;
                case "--require-external": requireExternal = true; requestCadAssistant = true; break;
                case "--json": json = true; break;
                default: stderr.WriteLine($"Unknown verify option '{args[i]}'."); stderr.WriteLine(VerifyUsage); return 1;
            }
        }
        if (string.Equals(Path.GetExtension(stepPath), ".firmament", StringComparison.OrdinalIgnoreCase))
        {
            var build = FirmamentBuildAndExport.Run(stepPath);
            if (!build.IsSuccess)
            {
                if (json) stdout.WriteLine(JsonSerializer.Serialize(new { command = "verify", success = false, input = Path.GetFullPath(stepPath), diagnostics = build.Diagnostics.Select(d => new { d.Source, d.Message, severity = d.Severity.ToString() }) }, JsonOptions));
                else { stderr.WriteLine("Verification stopped because build failed."); foreach (var diagnostic in build.Diagnostics) stderr.WriteLine($"error: {diagnostic.Message}"); }
                return 1;
            }
            stepPath = build.Value.OutputPath;
        }
        else if (!string.Equals(Path.GetExtension(stepPath), ".step", StringComparison.OrdinalIgnoreCase) && !string.Equals(Path.GetExtension(stepPath), ".stp", StringComparison.OrdinalIgnoreCase))
        {
            stderr.WriteLine("Verify expects Firmament (.firmament) or STEP (.step, .stp) input.");
            return 1;
        }
        if (!File.Exists(stepPath)) { stderr.WriteLine($"STEP artifact '{stepPath}' does not exist."); return 1; }
        var fullPath = Path.GetFullPath(stepPath);
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fullPath))).ToLowerInvariant();
        var fixtureName = Path.GetFileNameWithoutExtension(fullPath);
        var evidenceDir = Path.GetFullPath(evidenceRoot ?? Path.Combine("artifacts", "verification", fixtureName, hash));
        Directory.CreateDirectory(evidenceDir);
        File.Copy(fullPath, Path.Combine(evidenceDir, "model.step"), overwrite: true);

        object report;
        CadAssistantInspectionResult? external = null;
        BrepMassPropertiesResult? massProperties = null;
        AnalyzeResult? reimportAnalysis = null;
        try
        {
            var import = Step242Importer.ImportBody(File.ReadAllText(fullPath));
            if (!import.IsSuccess) throw new InvalidOperationException(string.Join("; ", import.Diagnostics.Select(d => d.Message)));
            massProperties = BrepMassProperties.Evaluate(import.Value);
            reimportAnalysis = StepAnalyzer.AnalyzeImportedBody(import.Value, fullPath);
            if (requestCadAssistant)
            {
                external = CadAssistantInspection.Inspect(fullPath, new CadAssistantInspectionOptions(cadPath, timeout, evidenceDir));
            }
            var overall = massProperties.Status == BrepMassPropertiesStatus.Unavailable
                ? "BRepRejected"
                : external?.Status == CadAssistantInspectionStatus.Unavailable
                ? "ExternalInspectionPending"
                : external is null ? "ExternalInspectionPending"
                : external.Status is CadAssistantInspectionStatus.Displayed or CadAssistantInspectionStatus.InspectionCompleted ? "ExternallyDisplayed"
                : massProperties.Status != BrepMassPropertiesStatus.Unavailable ? "BRepVerified" : "Rejected";
            ArtifactMassComparisonEvidence? comparison = null;
            if (expectedVolume.HasValue && massProperties.Status != BrepMassPropertiesStatus.Unavailable)
            {
                var delta = massProperties.AbsoluteVolume - expectedVolume.Value;
                var relative = expectedVolume.Value > 1e-12d ? delta / expectedVolume.Value : delta;
                comparison = new ArtifactMassComparisonEvidence(expectedVolume.Value, massProperties.AbsoluteVolume, delta, relative, Math.Abs(delta) <= (massProperties.ErrorBound ?? 0d));
            }
            report = new ArtifactVerificationResult(
                new ArtifactIdentity(fullPath, hash, evidenceDir),
                new ArtifactProducerEvidence("ArtifactOnly", "verify consumes an existing STEP artifact; compiler preflight evidence is not reconstructed."),
                massProperties,
                comparison,
                new ArtifactStepReimportEvidence("Valid", reimportAnalysis),
                (object?)external ?? new { status = "NotRequested", availability = "Unknown" },
                overall);
        }
        catch (Exception ex)
        {
            report = new { artifact = new { path = fullPath, sha256 = hash, evidenceDirectory = evidenceDir }, overallAdmission = "Rejected", error = ex.Message };
        }
        var reportPath = Path.Combine(evidenceDir, "verification-report.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, JsonOptions));
        if (massProperties is not null) File.WriteAllText(Path.Combine(evidenceDir, "mass-properties.json"), JsonSerializer.Serialize(massProperties, JsonOptions));
        if (reimportAnalysis is not null) File.WriteAllText(Path.Combine(evidenceDir, "aetheris-analyze.json"), JsonSerializer.Serialize(reimportAnalysis, JsonOptions));
        if (external is not null) File.WriteAllText(Path.Combine(evidenceDir, "cad-assistant-inspection.json"), JsonSerializer.Serialize(external, JsonOptions));
        File.WriteAllText(Path.Combine(evidenceDir, "verification-summary.md"), $"# Artifact verification\n\n- Artifact: `{fullPath}`\n- SHA-256: `{hash}`\n- Report: `verification-report.json`\n- External inspection: `{external?.Status.ToString() ?? "NotRequested"}`\n");
        if (json) stdout.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
        else stdout.WriteLine($"Verification report: {reportPath}");
        return requireExternal && external?.Status == CadAssistantInspectionStatus.Unavailable ? 2 : 0;
    }

    private static int RunMatch(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || IsHelpFlag(args[0]))
        {
            WriteMatchHelp(stdout);
            return args.Length == 0 ? 1 : 0;
        }
        if (args.Length < 2 || args[0].StartsWith("-", StringComparison.Ordinal) || args[1].StartsWith("-", StringComparison.Ordinal)) { stderr.WriteLine(MatchUsage); return 1; }
        var step = args[0]; var concept = args[1]; var json = false; var linear = 0.01d; var angular = 0.1d;
        for (var i = 2; i < args.Length; i++)
        {
            if (args[i] == "--json") { json = true; continue; }
            if (args[i] == "--linear-tolerance" && i + 1 < args.Length && double.TryParse(args[++i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out linear) && linear >= 0) continue;
            if (args[i] == "--angular-tolerance" && i + 1 < args.Length && double.TryParse(args[++i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out angular) && angular >= 0) continue;
            stderr.WriteLine($"Unknown or invalid match option '{args[i]}'."); stderr.WriteLine(MatchUsage); return 1;
        }
        var report = ConceptStepMatcher.Match(step, concept, new(linear, angular, linear));
        if (json) stdout.WriteLine(JsonSerializer.Serialize(new { conceptStepMatch = report }, JsonOptions));
        else stdout.WriteLine($"{report.Status}: {report.ConceptStruct} against {step} ({report.Members.Count} members)");
        return report.Status is ConceptStepOverallStatus.Conflicted or ConceptStepOverallStatus.InvalidConcept or ConceptStepOverallStatus.InvalidStep ? 1 : 0;
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
                case "--output" when i + 1 < args.Length:
                case "--out" when i + 1 < args.Length: // Compatibility alias; prefer --output in public help.
                    outPath = args[++i];
                    break;
                case "--output":
                case "--out":
                    stderr.WriteLine("Build option --output requires a path value.");
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
                    command = "build",
                    success = false,
                    input = Path.GetFullPath(sourcePath),
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
                command = "build",
                success = true,
                input = build.Value.SourcePath,
                output = build.Value.OutputPath,
                sourcePath = build.Value.SourcePath,
                outputPath = build.Value.OutputPath,
                conceptIr = build.Value.Export.ConceptIr,
                air = build.Value.Export.Air,
                roundedBox = build.Value.Export.RoundedBox,
                hollow = build.Value.Export.Hollow,
                lattice = build.Value.Export.Lattice,
                combined = build.Value.Export.Combined,
                standardPart = build.Value.Export.StandardPart,
                assertions = build.Value.Export.Assertions,
                features = build.Value.Export.Features,
                inlineStepMigration = build.Value.Export.InlineStepMigration,
                inlineStepReplacementAssist = build.Value.Export.InlineStepReplacementAssist,
                pmiExportEvidence = new
                {
                    datum = (build.Value.Export.DatumInspection ?? []).Select(d => new { kind = "datum", name = d.Label, exportSupport = "supported", exportEvidence = "found", target = d.Target }),
                    diameter = (build.Value.Export.DimensionInspection ?? []).Where(d => string.Equals(d.Kind, "Diameter", StringComparison.Ordinal)).Select(d => new { kind = "diameter", name = d.CandidateName ?? d.Target, exportSupport = "supported", exportEvidence = "found", target = d.Target, nominal = d.Value })
                }
            }, JsonOptions));
        }
        else
        {
            stdout.WriteLine($"Built {Path.GetFileName(build.Value.SourcePath)}");
            stdout.WriteLine($"STEP: {build.Value.OutputPath}");
            stdout.WriteLine($"Model: {build.Value.Export.ExportedFeatureId}");
            if (build.Value.Export.StandardPart is { } standardPart)
                stdout.WriteLine($"Standard part: {standardPart.Family} via {standardPart.Template ?? "direct record"} ({standardPart.SemanticDescendants.Count} semantic descendants)");
        }

        return 0;
    }

    private static int RunFea(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || IsHelpFlag(args[0])) { stdout.WriteLine(FeaUsage); return args.Length == 0 ? 1 : 0; }
        var input = Path.GetFullPath(args[0]); string? outDir = null; var json = false;Vector3D? rotationDegrees=null;
        for (var index = 1; index < args.Length; index++)
        {
            if (args[index] == "--json") json = true;
            else if (args[index] == "--out-dir" && index + 1 < args.Length) outDir = Path.GetFullPath(args[++index]);
            else if(args[index]=="--rotate"&&index+1<args.Length){var values=args[++index].Split(',').Select(value=>double.Parse(value,System.Globalization.CultureInfo.InvariantCulture)).ToArray();if(values.Length!=3){stderr.WriteLine(FeaUsage);return 1;}rotationDegrees=new(values[0],values[1],values[2]);}
            else { stderr.WriteLine(FeaUsage); return 1; }
        }
        if (!File.Exists(input)) { stderr.WriteLine($"Analysis source was not found: {input}"); return 1; }
        var compiled = FirmamentAnalysisCompiler.Compile(File.ReadAllText(input), input, Path.GetDirectoryName(input));
        if (!compiled.IsSuccess || compiled.Analysis is null)
        {
            foreach (var diagnostic in compiled.Diagnostics) stderr.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
            return 1;
        }
        Transform3D? orientation=null;if(rotationDegrees is { } degrees){var b=compiled.Analysis.Body.ContinuumRegion.Bounds;var center=new Vector3D((b.Min.X+b.Max.X)/2,(b.Min.Y+b.Max.Y)/2,(b.Min.Z+b.Max.Z)/2);orientation=Transform3D.CreateTranslation(-center)*Transform3D.CreateRotationX(degrees.X*double.Pi/180)*Transform3D.CreateRotationY(degrees.Y*double.Pi/180)*Transform3D.CreateRotationZ(degrees.Z*double.Pi/180)*Transform3D.CreateTranslation(center);}
        var solveOptions=new MechanicsSolveOptions(CutCellQuadraturePerAxis:6,DomainTransform:orientation,PreserveNominalCellVolumeUnderTransform:orientation is not null);var result = LinearElasticSolver.Solve(compiled.Analysis,solveOptions);
        if (!result.IsSuccess)
        {
            foreach (var diagnostic in result.Diagnostics.Where(item => item.Severity == Aetheris.FEA.Analysis.AnalysisDiagnosticSeverity.Error)) stderr.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
            return 1;
        }
        var abaqus = AbaqusInpExporter.Export(compiled.Analysis,orientation); var validation = AbaqusInpValidator.Validate(abaqus.Text);
        var report = new
        {
            analysis = new { compiled.Analysis.Id, kind = compiled.Analysis.Kind.ToString(), body = compiled.Analysis.Body.Id, compiled.Analysis.Body.SourceKind,
                material = compiled.Analysis.Materials.Select(item => new { item.Id, item.YoungsModulusPascal, item.PoissonRatio, item.DensityKilogramsPerCubicMeter }),
                constraints = compiled.Analysis.Constraints.Select(item => new { item.Id, region = item.Region.Path, components = item.Components }),
                loads = compiled.Analysis.Loads.Select(item => new { item.Id, kind = item.Kind.ToString(), region = item.Region.Path, item.VectorSi, item.PressurePascal }),
                lattice = new { compiled.Analysis.Lattice.CountX, compiled.Analysis.Lattice.CountY, compiled.Analysis.Lattice.CountZ } },
            orientationDegrees=rotationDegrees,result.System, result.Solver, result.Equilibrium, result.TinyCells, result.Performance,boundaryLoads=result.BoundaryLoads,numericalLowering=result.NumericalLowering,strainEnergy=result.StrainEnergy,stressProbes=result.StressProbes,
            maximumDisplacementMeters = result.MaximumDisplacementMeters, maximumVonMisesPascal = result.MaximumVonMisesPascal,
            abaqus = new { abaqus.Sha256, abaqus.NodeCount, abaqus.ElementCount, validation.IsValid, validation.Diagnostics }
        };
        if (outDir is not null)
        {
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "analysis-ir.json"), JsonSerializer.Serialize(report.analysis, JsonOptions));
            File.WriteAllText(Path.Combine(outDir, "native-results.json"), JsonSerializer.Serialize(report, JsonOptions));
            File.WriteAllText(Path.Combine(outDir, "sparse-system-metrics.json"), JsonSerializer.Serialize(result.System, JsonOptions));
            File.WriteAllText(Path.Combine(outDir, "residual-history.json"), JsonSerializer.Serialize(result.Solver.ResidualHistory, JsonOptions));
            File.WriteAllText(Path.Combine(outDir, "displacement-stress-summary.json"), JsonSerializer.Serialize(new { maximumDisplacementMeters = result.MaximumDisplacementMeters, maximumVonMisesPascal = result.MaximumVonMisesPascal, result.Equilibrium }, JsonOptions));
            File.WriteAllText(Path.Combine(outDir,"boundary-quadrature.json"),JsonSerializer.Serialize(result.BoundaryLoads,JsonOptions));
            File.WriteAllText(Path.Combine(outDir,"numerical-lowering-strategy-map.json"),JsonSerializer.Serialize(result.NumericalLowering,JsonOptions));
            File.WriteAllText(Path.Combine(outDir, "verification.inp"), abaqus.Text);
        }
        stdout.WriteLine(json ? JsonSerializer.Serialize(report, JsonOptions) : $"{compiled.Analysis.Id}: converged in {result.Solver.Iterations} iterations; max |u|={result.MaximumDisplacementMeters:R} m; max von Mises={result.MaximumVonMisesPascal:R} Pa; Abaqus SHA-256={abaqus.Sha256}");
        return validation.IsValid ? 0 : 1;
    }

    private static int RunMesh(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || IsHelpFlag(args[0])) { stdout.WriteLine(MeshUsage); return args.Length == 0 ? 1 : 0; }
        var input = args[0]; var format = "stl"; string? output = null; string? debugIr = null; var json = false;
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--format" when i + 1 < args.Length: format = args[++i]; break;
                case "--output" when i + 1 < args.Length: output = args[++i]; break;
                case "--debug-ir" when i + 1 < args.Length: debugIr = args[++i]; break;
                case "--json": json = true; break;
                default: stderr.WriteLine($"Unknown mesh option '{args[i]}'."); stderr.WriteLine(MeshUsage); return 1;
            }
        }
        if (!string.Equals(format, "stl", StringComparison.OrdinalIgnoreCase) && !string.Equals(format, "obj", StringComparison.OrdinalIgnoreCase))
        {
            stderr.WriteLine("Mesh supports binary STL and topology-preserving OBJ.");
            return 1;
        }
        var fullInput = Path.GetFullPath(input);
        string stepText;
        if (string.Equals(Path.GetExtension(fullInput), ".firmament", StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetExtension(fullInput), ".firmfixture", StringComparison.OrdinalIgnoreCase))
        {
            var build = FirmamentBuildAndExport.Run(fullInput);
            if (!build.IsSuccess) return WriteMeshFailure(build.Diagnostics.Select(d => d.Message), json, fullInput, stdout, stderr);
            stepText = build.Value.Export.StepText;
        }
        else if (string.Equals(Path.GetExtension(fullInput), ".step", StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetExtension(fullInput), ".stp", StringComparison.OrdinalIgnoreCase)) stepText = File.ReadAllText(fullInput);
        else { stderr.WriteLine("Mesh expects Firmament (.firmament, .firmfixture) or STEP (.step, .stp) input."); return 1; }
        var importWatch = System.Diagnostics.Stopwatch.StartNew();
        var imported = Step242Importer.ImportBody(stepText);
        importWatch.Stop();
        if (!imported.IsSuccess || imported.Value is null) return WriteMeshFailure(imported.Diagnostics.Select(d => d.Message), json, fullInput, stdout, stderr);
        string? irFailure = null;
        var meshIrWatch = System.Diagnostics.Stopwatch.StartNew();
        if (!SurfaceMeshIrTessellator.TryBuild(imported.Value, SurfaceMeshPolicy.FromDisplayOptions(DisplayTessellationOptions.Default), out var document, out irFailure)
            || !SurfaceMeshIrValidator.TryValidate(document, out irFailure))
        {
            var diagnostic = irFailure ?? "SurfaceMeshIR does not support this B-rep family or its topology did not validate.";
            var audit = SurfaceMeshIrTessellator.Audit(imported.Value);
            if (json) stdout.WriteLine(JsonSerializer.Serialize(new { command = "mesh", success = false, input = fullInput, diagnostics = new[] { diagnostic }, coverage = audit }, JsonOptions));
            else stderr.WriteLine($"Mesh failed: {diagnostic}");
            return 1;
        }
        meshIrWatch.Stop();
        if (!string.IsNullOrWhiteSpace(debugIr))
        {
            var debugPath = Path.GetFullPath(debugIr);
            Directory.CreateDirectory(Path.GetDirectoryName(debugPath)!);
            File.WriteAllText(debugPath, SurfaceMeshIrDebug.ToJson(document));
        }
        var outputPath = Path.GetFullPath(output ?? Path.ChangeExtension(fullInput, string.Equals(format, "obj", StringComparison.OrdinalIgnoreCase) ? ".obj" : ".stl"));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        if (string.Equals(format, "obj", StringComparison.OrdinalIgnoreCase))
        {
            var serializationWatch = System.Diagnostics.Stopwatch.StartNew();
            var obj = SurfaceMeshObjExporter.Export(document, Path.GetFileNameWithoutExtension(outputPath));
            File.WriteAllText(outputPath, obj.Text);
            serializationWatch.Stop();
            var objBytes = new FileInfo(outputPath).Length;
            var watertight = SurfaceMeshIrTessellator.TryLowerToTriangleMesh(document, out _, out var objTopology) && objTopology.IsWatertight;
            if (json) stdout.WriteLine(JsonSerializer.Serialize(new
            {
                command = "mesh", success = true, pipeline = "SurfaceMeshIR", input = fullInput, outputPath, format = "obj",
                coverage = SurfaceMeshIrTessellator.Audit(imported.Value),
                planarAudit = SurfaceMeshIrPlanarAudit.Analyze(document),
                patchCount = document.Metrics.PatchCount, cellCount = document.Metrics.CellCount, polygonCount = obj.PolygonCount,
                quadCount = obj.QuadCount, triangleCount = obj.TriangleCount, boundaryPolygonCount = obj.BoundaryPolygonCount,
                quadPercentage = obj.PolygonCount == 0 ? 0d : (double)obj.QuadCount / obj.PolygonCount * 100d,
                vertexCount = obj.VertexCount, normalCount = obj.NormalCount, textureCoordinateCount = obj.TextureCoordinateCount,
                finalTriangleCount = objTopology.TriangleCount, watertight,
                crackCount = objTopology.CrackCount, nonManifoldEdgeCount = objTopology.NonManifoldEdgeCount,
                duplicateTriangleCount = objTopology.DuplicateTriangleCount, zeroAreaTriangleCount = objTopology.ZeroAreaTriangleCount,
                connected = objTopology.IsConnected, outwardOriented = objTopology.IsOutwardOriented,
                foreignTrimResolutionCount = document.ForeignTrimResolutions?.Count ?? 0,
                sampledBSplineTrimCount = document.ForeignTrimResolutions?.Count(item => item.ResolutionKind == ForeignTrimResolutionKind.SampledBSpline) ?? 0,
                trimResolutions = document.ForeignTrimResolutions?.Select(item => new
                {
                    edgeId = item.SourceEdgeId.Value,
                    sourceCurveKind = item.SourceCurveKind.ToString(),
                    resolutionKind = item.ResolutionKind.ToString(),
                    adjacentSupportFamilies = item.AdjacentSupportFamilies,
                    sampleCount = item.SharedSamplePlan.Samples.Count,
                    maxChordalDeviation = item.SharedSamplePlan.MaxChordalDeviation,
                    maxTangentDeviationRadians = item.SharedSamplePlan.MaxTangentDeviationRadians,
                    recognitionCandidate = item.RecognitionCandidate,
                    maxRecognitionDeviation = item.MaxRecognitionDeviation,
                    recognitionTolerance = item.RecognitionTolerance,
                    provenance = item.Provenance
                }),
                timingsMilliseconds = new { brepImport = importWatch.Elapsed.TotalMilliseconds, surfaceMeshIr = meshIrWatch.Elapsed.TotalMilliseconds, objSerialization = serializationWatch.Elapsed.TotalMilliseconds },
                maxChordalDeviation = document.Metrics.MaxChordalDeviation, normalDeviation = document.Metrics.MaxNormalDeviation,
                minEdgeLength = document.Metrics.MinEdgeLength, maxEdgeLength = document.Metrics.MaxEdgeLength,
                worstAspectRatio = document.Metrics.WorstAspectRatio, approximateStructuredBufferBytes = document.Metrics.ApproximateBufferBytes,
                deterministicHash = obj.DeterministicHash, bytes = objBytes
            }, JsonOptions));
            else stdout.WriteLine($"SurfaceMeshIR OBJ: {outputPath}\nPatches: {document.Metrics.PatchCount}; polygons: {obj.PolygonCount}; quads: {obj.QuadCount}; triangles: {obj.TriangleCount}; max chordal error: {document.Metrics.MaxChordalDeviation:R}");
            return 0;
        }
        var loweringWatch = System.Diagnostics.Stopwatch.StartNew();
        if (!SurfaceMeshIrTessellator.TryLowerToTriangleMesh(document, out var mesh, out var topology))
            return WriteMeshFailure([$"SurfaceMeshIR could not lower the validated document to a watertight TriangleMesh for STL: cracks={topology.CrackCount}, nonmanifold={topology.NonManifoldEdgeCount}, duplicates={topology.DuplicateTriangleCount}, zeroArea={topology.ZeroAreaTriangleCount}, connected={topology.IsConnected}, outward={topology.IsOutwardOriented}."], json, fullInput, stdout, stderr);
        BinaryStlExporter.Export(outputPath, mesh);
        loweringWatch.Stop();
        var stlBytes = new FileInfo(outputPath).Length;
        if (json) stdout.WriteLine(JsonSerializer.Serialize(new { command = "mesh", success = true, pipeline = "SurfaceMeshIR", input = fullInput, outputPath, format = "binary-stl", patchCount = document.Metrics.PatchCount, cellCount = document.Metrics.CellCount, quadCount = document.Metrics.QuadCount, triangleCount = topology.TriangleCount, vertexCount = topology.VertexCount, watertight = topology.IsWatertight, maxChordalDeviation = document.Metrics.MaxChordalDeviation, normalDeviation = document.Metrics.MaxNormalDeviation, timingsMilliseconds = new { brepImport = importWatch.Elapsed.TotalMilliseconds, surfaceMeshIr = meshIrWatch.Elapsed.TotalMilliseconds, triangleLoweringAndStlSerialization = loweringWatch.Elapsed.TotalMilliseconds }, deterministicHash = mesh.DeterministicHash, bytes = stlBytes }, JsonOptions));
        else stdout.WriteLine($"SurfaceMeshIR STL: {outputPath}\nTriangles: {topology.TriangleCount}; watertight: {topology.IsWatertight}; max chordal error: {document.Metrics.MaxChordalDeviation:R}");
        return 0;
    }

    private static int WriteMeshFailure(IEnumerable<string> diagnostics, bool json, string input, TextWriter stdout, TextWriter stderr)
    {
        var messages = diagnostics.ToArray();
        if (json) stdout.WriteLine(JsonSerializer.Serialize(new { command = "mesh", success = false, input, diagnostics = messages }, JsonOptions));
        else foreach (var message in messages) stderr.WriteLine($"Mesh failed: {message}");
        return 1;
    }

    private static int RunInspect(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || IsHelpFlag(args[0]))
        {
            WriteInspectHelp(stdout);
            return args.Length == 0 ? 1 : 0;
        }

        var input = args[0];
        var json = args.Skip(1).SequenceEqual(["--json"]);
        if (!json && args.Length > 1) { stderr.WriteLine($"Unknown inspect option '{args[1]}'."); stderr.WriteLine(InspectUsage); return 1; }
        if (!File.Exists(input)) { stderr.WriteLine($"Inspect input was not found: {input}"); return 1; }

        var fullPath = Path.GetFullPath(input);
        var extension = Path.GetExtension(fullPath);
        if (string.Equals(extension, ".step", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".stp", StringComparison.OrdinalIgnoreCase))
            return RunAnalyze([fullPath, .. args.Skip(1)], stdout, stderr);
        if (!string.Equals(extension, ".firmament", StringComparison.OrdinalIgnoreCase))
        {
            stderr.WriteLine("Inspect expects Firmament (.firmament) or STEP (.step, .stp) input.");
            return 1;
        }

        var source = File.ReadAllText(fullPath);
        var parse = FirmamentV2Parser.Parse(source, Path.GetDirectoryName(fullPath));
        var document = parse.Document;
        var success = parse.IsSuccess && document is not null;
        var features = document?.ModifyBlocks?.SelectMany(block =>
            block.SemanticHoles.Select(hole => $"Hole<{hole.Variant}> {hole.Name}")
            .Concat((block.EdgeFinishes ?? []).Select(finish => $"{finish.Kind} {finish.Name}"))) ?? [];
        var semanticValues = FirmamentSemanticValues.FromProfilesAndConceptPaths(source, fullPath).ToList();
        if (PrismaticProfileCompositionParser.IsCompositionSource(source))
        {
            var composition = PrismaticProfileCompositionParser.Parse(source);
            semanticValues.AddRange(composition.Profiles.Values.OrderBy(profile => profile.Name, StringComparer.Ordinal).Select(profile =>
                FirmamentSemanticValues.FromProfile(profile, SemanticSourceSpan.Generated(fullPath),
                    [new("firmament-profile", profile.Name, "named Profile normalized before consumer")])));
        }
        else if (ProfileAuthoringParser.IsProfileSource(source) && ProfileAuthoringParser.Parse(source).Profile is { } directProfile)
            semanticValues.Add(FirmamentSemanticValues.FromProfile(directProfile, SemanticSourceSpan.Generated(fullPath),
                [new("firmament-profile", directProfile.Name, "named Profile normalized before consumer")]));
        if (document?.ConceptIr is { } conceptIr) semanticValues.AddRange(FirmamentSemanticValues.FromConceptIr(conceptIr, fullPath));
        if (document?.RecognizedRegions?.Count > 0) semanticValues.AddRange(FirmamentSemanticValues.FromRecognizedRegions(document, fullPath, source));
        var report = new
        {
            command = "inspect",
            success,
            input = fullPath,
            model = document?.ModelName,
            units = document?.Units,
            bodies = document?.Solids.Select(solid => new { name = solid.Name, kind = solid.RecordType }).ToArray() ?? [],
            tables = document?.StaticAuthoring?.Tables?.Select(table => new
            {
                table.Name,
                rowType = table.RowType,
                keyField = table.KeyField,
                rowCount = table.RowCount,
                columns = table.Columns,
                table.SourceSpan
            }).ToArray() ?? [],
            templateInstances = (document?.TemplateInstantiations ?? document?.ConceptIr?.TemplateInstantiations)?.Select(instance => new
            {
                instance.Template,
                instance.Instance,
                instance.SpecializationIdentity,
                records = instance.RecordArguments?.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => new
                {
                    pair.Value.RecordType,
                    pair.Value.StaticValue,
                    pair.Value.Provenance,
                    members = pair.Value.Members.OrderBy(member => member.Key, StringComparer.Ordinal).ToDictionary(member => member.Key, member => member.Value, StringComparer.Ordinal)
                }, StringComparer.Ordinal)
            }).ToArray() ?? [],
            conceptPaths = ProfileAuthoringParser.InspectConceptPaths(source),
            semanticValues = semanticValues.OrderBy(value => value.StableIdentity, StringComparer.Ordinal).Select(SemanticValueDescriptor.From).ToArray(),
            recognizedRegions = document?.RecognizedRegions?.Select(region => new
            {
                region.BodyName,
                region.RegionName,
                region.Kind,
                region.FaceRefs,
                region.Confidence,
                region.Evidence,
                region.Proposal
            }).ToArray() ?? [],
            features,
            pmi = document?.Pmi?.Select(item => new { item.Kind, item.Name, item.Target }).ToArray() ?? [],
            assertions = document?.VolumeAssertions?.Select(assertion => new { assertion.Id, assertion.TargetBodyId, assertion.ExpectedMm3, assertion.ToleranceMm3 }).ToArray() ?? [],
            diagnostics = parse.Diagnostics.Order(StringComparer.Ordinal).ToArray()
        };
        if (json) stdout.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
        else if (!success)
        {
            stderr.WriteLine($"Inspect failed for {fullPath}.");
            foreach (var diagnostic in parse.Diagnostics.Order(StringComparer.Ordinal)) stderr.WriteLine($"error: {diagnostic}");
        }
        else
        {
            stdout.WriteLine($"Model: {document!.ModelName}");
            stdout.WriteLine($"Bodies: {document.Solids.Count}");
            foreach (var feature in features) stdout.WriteLine($"Feature: {feature}");
            foreach (var item in document.Pmi ?? []) stdout.WriteLine($"PMI: {item.Kind} {item.Name}");
            foreach (var assertion in document.VolumeAssertions ?? []) stdout.WriteLine($"Assertion: Volume {assertion.TargetBodyId}");
        }
        return success ? 0 : 1;
    }

    private static int RunView(string[] args, TextWriter stdout, TextWriter stderr, ICadmataProcessLauncher launcher, string cliBaseDirectory)
    {
        if (args.Length == 0 || IsHelpFlag(args[0])) { WriteViewHelp(stdout); return args.Length == 0 ? 1 : 0; }
        var input = args[0];
        string? executable = null;
        var json = false;
        for (var i = 1; i < args.Length; i++)
        {
            if ((args[i] == "--cadmata-path" || args[i] == "--cad-assistant-path") && i + 1 < args.Length) { executable = args[++i]; continue; }
            if (args[i] == "--json") { json = true; continue; }
            stderr.WriteLine($"Unknown view option '{args[i]}'."); stderr.WriteLine(ViewUsage); return 1;
        }
        if (!File.Exists(input)) { stderr.WriteLine($"View input was not found: {input}"); return 1; }
        var fullInput = Path.GetFullPath(input);
        var extension = Path.GetExtension(fullInput);
        string stepPath;
        if (string.Equals(extension, ".firmament", StringComparison.OrdinalIgnoreCase))
        {
            var build = FirmamentBuildAndExport.Run(fullInput);
            if (!build.IsSuccess)
            {
                if (json) stdout.WriteLine(JsonSerializer.Serialize(new { command = "view", success = false, input = fullInput, diagnostics = build.Diagnostics.Select(d => new { d.Source, d.Message, severity = d.Severity.ToString() }) }, JsonOptions));
                else { stderr.WriteLine("View stopped because build failed."); foreach (var diagnostic in build.Diagnostics) stderr.WriteLine($"error: {diagnostic.Message}"); }
                return 1;
            }
            stepPath = build.Value.OutputPath;
            if (!json)
            {
                stdout.WriteLine($"✓ Built {Path.GetFileName(fullInput)}");
                stdout.WriteLine($"  STEP: {stepPath}");
                stdout.WriteLine();
            }
        }
        else if (string.Equals(extension, ".step", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".stp", StringComparison.OrdinalIgnoreCase)) stepPath = fullInput;
        else { stderr.WriteLine("View expects Firmament (.firmament) or STEP (.step, .stp) input."); return 1; }

        var discovery = CadmataDiscovery.Resolve(executable, cliBaseDirectory);
        if (discovery is null)
        {
            const string message = "Cadmata was not found. Install the packaged Cadmata host beside Aetheris, put it on PATH, or set --cadmata-path (AETHERIS_CADMATA_PATH; legacy AETHERIS_CAD_ASSISTANT_PATH is also supported).";
            if (json) stdout.WriteLine(JsonSerializer.Serialize(new { command = "view", success = false, source = string.Equals(extension, ".firmament", StringComparison.OrdinalIgnoreCase) ? fullInput : null, stepPath, cadmataPath = (string?)null, launched = false, diagnostics = new[] { message } }, JsonOptions));
            else stderr.WriteLine(message);
            return 1;
        }
        Process? process;
        try
        {
            process = launcher.Launch(discovery.Path, stepPath);
            if (process is null) throw new InvalidOperationException("Process.Start returned no process.");
        }
        catch (Exception ex)
        {
            var message = $"Could not open '{stepPath}' in Cadmata: {ex.Message}";
            if (json) stdout.WriteLine(JsonSerializer.Serialize(new { command = "view", success = false, source = string.Equals(extension, ".firmament", StringComparison.OrdinalIgnoreCase) ? fullInput : null, stepPath, cadmataPath = discovery.Path, launched = false, diagnostics = new[] { message } }, JsonOptions));
            else stderr.WriteLine(message);
            return 1;
        }
        if (json) stdout.WriteLine(JsonSerializer.Serialize(new { command = "view", success = true, source = string.Equals(extension, ".firmament", StringComparison.OrdinalIgnoreCase) ? fullInput : null, stepPath, cadmataPath = discovery.Path, launched = true, processId = process.Id, diagnostics = Array.Empty<string>() }, JsonOptions));
        else stdout.WriteLine($"✓ Opened {Path.GetFileName(stepPath)} in Cadmata");
        return 0;
    }


    private static int RunInspectProfile(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || IsHelpFlag(args[0])) { stdout.WriteLine(InspectProfileUsage); return args.Length == 0 ? 1 : 0; }
        var json = args.Skip(1).All(x => x == "--json");
        if (!json) { stderr.WriteLine(InspectProfileUsage); return 1; }
        if (!File.Exists(args[0])) { stderr.WriteLine($"Profile source was not found: {args[0]}"); return 1; }
        var source = File.ReadAllText(args[0]);
        if (PrismaticProfileCompositionParser.IsCompositionSource(source))
        {
            var composition = PrismaticProfileCompositionParser.Parse(source);
            if (composition.Profiles.Count == 0) { stderr.WriteLine(string.Join(Environment.NewLine, composition.Diagnostics)); return 1; }
            var profiles = composition.Profiles.Values.OrderBy(x => x.Name, StringComparer.Ordinal).Select(profile =>
            {
                var validation = ResolvedProfile2DValidator.Validate(profile);
                return new
                {
                    profile.Name, profile.PlaneFrame, loops = profile.Loops.Count,
                    lineSegments = profile.Loops.SelectMany(x => x.Segments).Count(x => x.Geometry is LineArcLineSegment2D),
                    arcSegments = profile.Loops.SelectMany(x => x.Segments).Count(x => x.Geometry is LineArcCircularArc2D),
                    validation.IsValid, validation.SignedArea, validation.Diagnostics,
                    junctions = DescribeProfileJunctions(profile, source, profile.Name),
                    provenance = profile.Loops.SelectMany(x => x.Segments).Select(x => new { x.Name, x.Provenance.StableId, x.Provenance.ConceptStableId, x.Provenance.Derivation })
                };
            }).ToArray();
            stdout.WriteLine(JsonSerializer.Serialize(new { profiles, expansion = composition.Expansion, diagnostics = composition.Diagnostics }, JsonOptions));
            return composition.Diagnostics.Count == 0 && profiles.All(x => x.IsValid) ? 0 : 1;
        }
        var parsed = ProfileAuthoringParser.Parse(source);
        if (parsed.Profile is null) { stderr.WriteLine(string.Join(Environment.NewLine, parsed.Diagnostics)); return 1; }
        var validation = ResolvedProfile2DValidator.Validate(parsed.Profile);
        var plan = validation.IsValid
            ? ProfileExtrusionBRepPlanner.TryPlan(new LineArcProfileExtrudeRequest(parsed.Profile.Loops.Select(l => new LineArcProfileLoop2D(l.Segments.Select(s => s.Geometry).ToArray(), !l.IsOuter)).ToArray(), parsed.Height, parsed.Profile.EffectiveConstructionPlane, parsed.Profile.LocalStartDepth, parsed.Profile.LocalEndDepth), parsed.Profile)
            : new ProfileExtrusionPlanResult(false, null, validation.Diagnostics);
        var report = new
        {
            profile = new
            {
                parsed.Profile.Name, parsed.Profile.PlaneFrame,
                constructionPlane = DescribeConstructionPlane(parsed.Profile.EffectiveConstructionPlane),
                conceptPaths = ProfileAuthoringParser.InspectConceptPaths(source),
                loops = parsed.Profile.Loops.Count,
                segments = parsed.Profile.Loops.SelectMany(x => x.Segments).Select(x =>
                {
                    var guide = DescribeProfileGuide(x.Provenance.ConceptStableId);
                    return new { x.Name, guide = guide.Name, guideKind = guide.Kind, parentGuide = guide.Parent, stableId = x.Provenance.StableId, derivation = x.Provenance.Derivation, geometry = x.Geometry.GetType().Name };
                }),
                validation.IsValid, validation.SignedArea, validation.Diagnostics,
                junctions = DescribeProfileJunctions(parsed.Profile, source, parsed.Profile.Name),
                straightEdgeFillet = DescribeProfileStraightEdgeFillet(parsed.Profile, source, parsed.Profile.Name),
                extrusionHeight = parsed.Height,
                brepPlan = plan.Plan is null ? null : new
                {
                    plan.Plan.StableId, authoritative = plan.Plan.IsAuthoritative,
                    constructionPlane = DescribeConstructionPlane(plan.Plan.Construction.Frame),
                    localDepth = new { start = plan.Plan.Construction.LocalStartDepth, end = plan.Plan.Construction.LocalEndDepth },
                    topology = new { vertices = plan.Plan.Vertices.Count, curves = plan.Plan.Curves.Count, edges = plan.Plan.Edges.Count, loops = plan.Plan.Loops.Count, coedges = plan.Plan.CoedgeCount, surfaces = plan.Plan.Surfaces.Count, faces = plan.Plan.Faces.Count, shells = 1, bodies = 1 },
                    roles = plan.Plan.Faces.Select(x => new { x.StableId, role = x.Role.ToString(), loops = x.LoopIds.Select(id => id.Value), x.SameSense }),
                    correspondence = plan.Plan.Correspondence.Descendants.Select(x => new { x.StableId, role = x.Role.ToString(), x.SourceStableId, edge = x.Edge?.Value, face = x.Face?.Value, loop = x.Loop?.Value }),
                    materializer = "ProfileExtrusionBRepMaterializer",
                    plan.Plan.Provenance,
                    plan.Plan.Diagnostics
                }
            }
        };
        if (json) stdout.WriteLine(JsonSerializer.Serialize(report, JsonOptions)); else stdout.WriteLine($"Profile {parsed.Profile.Name}: {(validation.IsValid ? "valid" : "invalid")}");
        return validation.IsValid ? 0 : 1;
    }

    private static object DescribeConstructionPlane(ConstructionPlane plane) => new
    {
        plane.StableId, plane.SourceConceptId,
        origin = new[] { plane.Origin.X, plane.Origin.Y, plane.Origin.Z },
        axisX = new[] { plane.AxisX.ToVector().X, plane.AxisX.ToVector().Y, plane.AxisX.ToVector().Z },
        axisY = new[] { plane.AxisY.ToVector().X, plane.AxisY.ToVector().Y, plane.AxisY.ToVector().Z },
        axisZ = new[] { plane.AxisZ.ToVector().X, plane.AxisZ.ToVector().Y, plane.AxisZ.ToVector().Z },
        plane.Handedness, plane.Determinant, plane.SourceSpan, plane.Provenance
    };

    private static (string Name, string Kind, string? Parent) DescribeProfileGuide(string stableId)
    {
        const string prefix = "concept:";
        var path = stableId.StartsWith(prefix, StringComparison.Ordinal) ? stableId[prefix.Length..] : stableId;
        var components = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (components.Length >= 3 && components[^1] is "Top" or "Bottom" or "Left" or "Right")
        {
            return ($"{components[^2]}.{components[^1]}", "Rect2Side", components[^2]);
        }

        return (stableId, "ConceptGuide", null);
    }

    private static int RunInspectSelections(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || IsHelpFlag(args[0])) { stdout.WriteLine(InspectSelectionsUsage); return args.Length == 0 ? 1 : 0; }
        if (args.Length != 2 || args[1] != "--json") { stderr.WriteLine(InspectSelectionsUsage); return 1; }
        if (!File.Exists(args[0])) { stderr.WriteLine($"Selection source was not found: {args[0]}"); return 1; }
        var totalClock = Stopwatch.StartNew();
        var source = File.ReadAllText(args[0]);
        if (PrismaticProfileCompositionParser.IsCompositionSource(source))
        {
            var parsedCompose = PrismaticProfileCompositionParser.Parse(source);
            var stack = PrismaticSectionStackCompiler.Normalize(parsedCompose, out var composeDiagnostics);
            var emittedCompose = stack is null ? null : PrismaticSectionStackEmitter.Emit(stack);
            if (stack is null || emittedCompose is null || emittedCompose.Body is null || emittedCompose.Correspondence is null)
            {
                stderr.WriteLine(string.Join(Environment.NewLine, composeDiagnostics));
                return 1;
            }
            BlindDrillToolCorridorEvidence? corridor = null;
            var bridgeDiagnostics = Array.Empty<string>();
            if ((stack.Feature.ConstructionPlaneBlindDrills?.Count ?? 0) > 0)
            {
                var finalPlan = SectionStackBlindDrillComposeBridge.TryApply(stack, emittedCompose.Plan!, out var bridge, out corridor);
                bridgeDiagnostics = bridge.ToArray();
                if (finalPlan?.TopologyPlan is null) { stderr.WriteLine(string.Join(Environment.NewLine, bridgeDiagnostics)); return 1; }
                var materialized = PrismaticSectionStackBrepMaterializer.TryMaterialize(finalPlan.TopologyPlan);
                if (materialized.Body is null) { stderr.WriteLine(string.Join(Environment.NewLine, materialized.Diagnostics)); return 1; }
                emittedCompose = new PrismaticSectionStackEmissionResult(materialized.Body, finalPlan, emittedCompose.Diagnostics.Concat(bridgeDiagnostics).ToArray(), finalPlan.Correspondence);
            }
            var composeRequests = SemanticSelectionSourceParser.Parse(source, parsedCompose.Profiles.Values.First(), stack.Feature.Name, out var composeParseDiagnostics).ToList();
            if (corridor is not null)
            {
                composeRequests.AddRange([
                    new("inspect:mouth-loop", "MouthLoop", stack.Feature.Name, [corridor.HoleId], SemanticTopologyRole.HoleEntryLoop, SemanticSelectionRequirement.ClosedLoop, "inspect-selections"),
                    new("inspect:shaft-wall-faces", "ShaftWallFaces", stack.Feature.Name, [corridor.HoleId], SemanticTopologyRole.HoleWallFace, SemanticSelectionRequirement.NonEmptyFaceSet, "inspect-selections"),
                    new("inspect:shaft-to-drill-point-loop", "ShaftToDrillPointLoop", stack.Feature.Name, [corridor.HoleId], SemanticTopologyRole.HoleShaftToDrillPointLoop, SemanticSelectionRequirement.ClosedLoop, "inspect-selections"),
                    new("inspect:drill-point-faces", "DrillPointFaces", stack.Feature.Name, [corridor.HoleId], SemanticTopologyRole.HoleDrillPointFace, SemanticSelectionRequirement.NonEmptyFaceSet, "inspect-selections"),
                    new("inspect:tip-vertex", "TipVertex", stack.Feature.Name, [corridor.HoleId], SemanticTopologyRole.HoleTipVertex, SemanticSelectionRequirement.ExactlyOne, "inspect-selections")
                ]);
            }
            if (emittedCompose.Body is not { } composeBody || emittedCompose.Correspondence is not { } composeCorrespondence)
            {
                stderr.WriteLine("Composition materialization did not provide body/correspondence evidence.");
                return 1;
            }
            var composeResults = composeRequests.Select(request => SemanticTopologySelectionResolver.Resolve(composeBody, composeCorrespondence, request)).ToArray();
            var mouthEdges = corridor is null ? [] : composeCorrespondence.Descendants
                .Where(x => x.SourceStableId == corridor.HoleId && x.Role == SemanticTopologyRole.TopBoundary && x.Edge is not null)
                .Select(x => x.Edge!.Value).OrderBy(x => x.Value).ToArray();
            var mouthTopology = corridor is null ? null : new
            {
                MouthOwnership = composeCorrespondence.ProvenanceChain.Contains("MultiFaceCoplanarMouth", StringComparer.Ordinal) ? "MultiFaceCoplanar" : "SingleFace",
                affectedHostFaceIds = emittedCompose.Plan?.TopologyPlan?.FaceMappings.Where(x => x.Kind == "HostFaceReplacement").Select(x => x.FaceId.Value).OrderBy(x => x).ToArray(),
                planningSeam = composeCorrespondence.ProvenanceChain.Contains("ExactLineCircleSplit", StringComparer.Ordinal) ? "section-stack-internal" : null,
                mouthArcDescendants = mouthEdges.Select(x => x.Value),
                intersectionVertices = emittedCompose.Plan?.TopologyPlan?.Topology.Edges.Where(x => mouthEdges.Contains(x.Id)).SelectMany(x => new[] { x.StartVertexId.Value, x.EndVertexId.Value }).Distinct().OrderBy(x => x).ToArray(),
                semanticMouthLoopOrder = composeResults.Where(x => x.Request.Role == SemanticTopologyRole.HoleEntryLoop).SelectMany(x => x.OrderedChain).Select(x => x.StableId),
                noCap = composeCorrespondence.ProvenanceChain.Contains("NoInternalCaps", StringComparer.Ordinal)
            };
            stdout.WriteLine(JsonSerializer.Serialize(new
            {
                body = composeCorrespondence.BodyStableId,
                selections = composeResults.Select(result => new { name = result.Request.Label, stableId = result.Request.StableId, sourceIdentities = result.Request.SourceStableIds, body = result.Request.BodyStableId, expectedShape = result.Request.Require.ToString(), topologyRole = result.Request.Role?.ToString(), succeeded = result.Succeeded, failure = result.Failure.ToString(), connectivity = new { result.IsConnected, result.IsClosed }, traversalOrder = result.OrderedChain.Select(x => x.StableId), materializedDescendants = result.Descendants.Select(x => new { x.StableId, x.Kind, role = x.Role.ToString(), x.SourceStableId, x.ParentStableId }), provenance = composeCorrespondence.ProvenanceChain, consumer = result.Request.Consumer, diagnostics = result.Diagnostics }),
                holeContract = corridor is null ? null : new
                {
                    ValidationPolicy = corridor.ValidationPolicy.ToString(), corridor.HoleId, corridor.HostId, corridor.ConstructionPlaneId,
                    corridor.Radius, corridor.ShaftDepth, corridor.TipLength, corridor.TotalDepth,
                    ClearanceCylinderLength = corridor.TotalDepth,
                    HostTraversalClassification = corridor.Classification.ToString(),
                    RelevantHostSlabs = corridor.ShaftSliceProofs.Select(x => new { z = new[] { x.ZFrom, x.ZTo }, x.Provenance }),
                    ChordProofs = corridor.ShaftSliceProofs.Select(x => new { z = new[] { x.ZFrom, x.ZTo }, axial = new[] { x.AxisFrom, x.AxisTo }, cross = new[] { x.CrossFrom, x.CrossTo }, x.ToolPart, classification = x.Classification.ToString(), x.Detail, x.Provenance }),
                    corridor.RemainingWall,
                    ContractSatisfied = corridor.Classification == BlindDrillToolCorridorClassification.CorridorProven,
                    ConservativeRejection = corridor.Classification == BlindDrillToolCorridorClassification.FullRadiusTipClearanceFailed,
                    diagnostics = corridor.Diagnostics
                },
                mouthTopology,
                arrangement = stack.Slabs.Select(s => new { slab = new[] { s.From, s.To }, fragments = s.Arrangement?.AtomicFragments.Select(f => new { f.StableId, source = f.Source.Provenance.StableId, f.FromParameter, f.ToParameter, f.MaterialOnLeft, f.Retained }) }),
                diagnostics = composeDiagnostics.Concat(composeParseDiagnostics).Concat(bridgeDiagnostics).Distinct()
            }, JsonOptions));
            return composeDiagnostics.Count == 0 && composeParseDiagnostics.Count == 0 && composeResults.All(x => x.Succeeded) ? 0 : 1;
        }
        var parseClock = Stopwatch.StartNew();
        var v2 = FirmamentV2Parser.Parse(source, Path.GetDirectoryName(Path.GetFullPath(args[0])));
        parseClock.Stop();
        if (v2.IsSuccess && v2.Document is not null && v2.Document.ModifyBlocks?.SelectMany(x => x.SemanticHoles).Any() == true)
        {
            var inspectClock = Stopwatch.StartNew();
            var hole = SemanticHoleInspection.Inspect(v2.Document);
            inspectClock.Stop();
            if (!hole.Succeeded || hole.Correspondence is null) { stderr.WriteLine(string.Join(Environment.NewLine, hole.Diagnostics)); return 1; }
            var selectionRequests = new List<SemanticSelectionRequest>
            {
                new("inspect:mouth-loop", "MouthLoop", hole.Correspondence.BodyStableId, [hole.HoleId!], SemanticTopologyRole.HoleEntryLoop, SemanticSelectionRequirement.ClosedLoop, "inspect-selections"),
                new("inspect:shaft-wall-faces", "ShaftWallFaces", hole.Correspondence.BodyStableId, [hole.HoleId!], SemanticTopologyRole.HoleWallFace, SemanticSelectionRequirement.NonEmptyFaceSet, "inspect-selections")
            };
            if (hole.Evidence?.PointAngle is { })
            {
                selectionRequests.Add(new("inspect:shaft-to-drill-point-loop", "ShaftToDrillPointLoop", hole.Correspondence.BodyStableId, [hole.HoleId!], SemanticTopologyRole.HoleShaftToDrillPointLoop, SemanticSelectionRequirement.ClosedLoop, "inspect-selections"));
                selectionRequests.Add(new("inspect:drill-point-faces", "DrillPointFaces", hole.Correspondence.BodyStableId, [hole.HoleId!], SemanticTopologyRole.HoleDrillPointFace, SemanticSelectionRequirement.NonEmptyFaceSet, "inspect-selections"));
                selectionRequests.Add(new("inspect:tip-vertex", "TipVertex", hole.Correspondence.BodyStableId, [hole.HoleId!], SemanticTopologyRole.HoleTipVertex, SemanticSelectionRequirement.ExactlyOne, "inspect-selections"));
            }
            else selectionRequests.Add(new("inspect:exit-loop", "ExitLoop", hole.Correspondence.BodyStableId, [hole.HoleId!], SemanticTopologyRole.HoleExitLoop, SemanticSelectionRequirement.ClosedLoop, "inspect-selections"));
            var selectionResults = selectionRequests.Select(request => SemanticTopologySelectionResolver.Resolve(hole.Body!, hole.Correspondence, request)).ToArray();
            totalClock.Stop();
            stdout.WriteLine(JsonSerializer.Serialize(new
            {
                body = hole.Correspondence.BodyStableId,
                hole = hole.HoleId,
                sourceDeclaration = new { kind = "Hole<Shaft>", featureId = hole.HoleId, sourceSpan = hole.Evidence?.SourceSpan },
                boundPlacement = hole.Evidence is null ? null : new { kind = hole.Evidence.PlacementKind, hole.Evidence.ConstructionPlaneId, hole.Evidence.SourceConceptPlaneId, hole.Evidence.LocalCenter },
                airPlacement = hole.Evidence is null ? null : new { kind = hole.Evidence.PlacementKind == "ConstructionPlane" ? "AirConstructionPlaneHolePlacement" : "AirFaceLocalHolePlacement", featureId = hole.Evidence.FeatureId, hole.Evidence.ConstructionPlaneId, hole.Evidence.LocalCenter },
                plan = hole.Evidence?.PlanId is null ? null : new { kind = "LocalFrameHoleBRepPlan", stableId = hole.Evidence.PlanId, hostInterval = hole.Evidence.HostInterval },
                holeContract = hole.Evidence?.Contract is null ? null : new
                {
                    FeatureId = hole.Evidence.FeatureId,
                    DeclaredEndCondition = hole.Evidence.Contract.DeclaredEndCondition,
                    DeclaredTermination = hole.Evidence.Contract.DeclaredTermination,
                    hole.Evidence.ConstructionPlaneId,
                    LocalCenter = hole.Evidence.LocalCenter,
                    WorldMouthCenter = hole.Evidence.WorldMouthCenter,
                    Axis = hole.Evidence.HostTraversal?.Axis,
                    hole.Evidence.Diameter,
                    hole.Evidence.Radius,
                    hole.Evidence.Contract.ShaftDepth,
                    hole.Evidence.Contract.TipLength,
                    hole.Evidence.Contract.TotalDepth,
                    PointAngle = hole.Evidence.PointAngle,
                    HostTraversalClassification = hole.Evidence.HostTraversal?.Classification.ToString(),
                    HostIntervals = hole.Evidence.HostTraversal?.OrderedIntervals,
                    PhysicalMaterialSpan = hole.Evidence.HostTraversal?.PhysicalMaterialSpan,
                    hole.Evidence.Contract.RemainingWall,
                    hole.Evidence.Contract.IsThroughAll,
                    hole.Evidence.Contract.IsBlind,
                    HasExit = hole.Evidence.Contract.HasExit,
                    hole.Evidence.Contract.HasDrillPoint,
                    hole.Evidence.Contract.MouthInsideMaterial,
                    hole.Evidence.Contract.TipInsideMaterial,
                    hole.Evidence.Contract.ContractSatisfied,
                    diagnostics = hole.Evidence.Contract.Diagnostics
                },
                summary = hole.Evidence is null ? null : new
                {
                    hole.Evidence.FeatureId, hole.Evidence.PlacementKind, hole.Evidence.ConstructionPlaneId, hole.Evidence.SourceConceptPlaneId,
                    frameOrigin = hole.Evidence.FrameOrigin, axisX = hole.Evidence.AxisX, axisY = hole.Evidence.AxisY, axisZ = hole.Evidence.AxisZ,
                    localCenter = hole.Evidence.LocalCenter, worldMouthCenter = hole.Evidence.WorldMouthCenter,
                    hole.Evidence.Diameter, hole.Evidence.Radius, extent = hole.Evidence.Extent, endKind = hole.Evidence.Extent, hole.Evidence.DeclaredDepth, hole.Evidence.ShaftDepth, hole.Evidence.TipLength, hole.Evidence.TotalDepth, pointAngle = hole.Evidence.PointAngle,
                    hostInterval = hole.Evidence.HostInterval,
                    hole.Evidence.PlanId, hole.Evidence.SourceSpan
                },
                descendants = hole.Correspondence.Descendants.Select(x => new { x.StableId, x.Kind, role = x.Role.ToString(), x.SourceStableId, x.ParentStableId, edge = x.Edge?.Value, face = x.Face?.Value, loop = x.Loop?.Value }),
                selectionResults = selectionResults.Select(result => new { name = result.Request.Label, stableId = result.Request.StableId, topologyRole = result.Request.Role?.ToString(), expectedShape = result.Request.Require.ToString(), succeeded = result.Succeeded, failure = result.Failure.ToString(), materializedDescendants = result.Descendants.Select(x => x.StableId), diagnostics = result.Diagnostics }),
                provenance = hole.Correspondence.ProvenanceChain,
                diagnostics = hole.Diagnostics,
                timings = new { parseMs = parseClock.Elapsed.TotalMilliseconds, inspectMs = inspectClock.Elapsed.TotalMilliseconds, totalMs = totalClock.Elapsed.TotalMilliseconds }
            }, JsonOptions));
            return 0;
        }
        var parsed = ProfileAuthoringParser.Parse(source);
        if (parsed.Profile is null) { stderr.WriteLine(string.Join(Environment.NewLine, parsed.Diagnostics)); return 1; }
        var emitted = ResolvedProfile2DValidator.Extrude(parsed.Profile, parsed.Height);
        if (emitted.Body is null || emitted.Correspondence is null) { stderr.WriteLine(string.Join(Environment.NewLine, emitted.Diagnostics)); return 1; }
        var requests = SemanticSelectionSourceParser.Parse(source, parsed.Profile, parsed.Profile.Name, out var parseDiagnostics);
        var results = requests.Select(request => SemanticTopologySelectionResolver.Resolve(emitted.Body, emitted.Correspondence, request)).ToArray();
        stdout.WriteLine(JsonSerializer.Serialize(new
        {
            body = emitted.Correspondence.BodyStableId,
            selections = results.Select(result => new
            {
                name = result.Request.Label, stableId = result.Request.StableId, sourceIdentities = result.Request.SourceStableIds,
                body = result.Request.BodyStableId, expectedShape = result.Request.Require.ToString(), topologyRole = result.Request.Role?.ToString(),
                succeeded = result.Succeeded, failure = result.Failure.ToString(), connectivity = new { result.IsConnected, result.IsClosed },
                traversalOrder = result.OrderedChain.Select(x => x.StableId),
                materializedDescendants = result.Descendants.Select(x => new { x.StableId, x.Kind, role = x.Role.ToString(), x.SourceStableId, x.ParentStableId, sourceSpan = result.Request.SourceSpan }),
                provenance = emitted.Correspondence.ProvenanceChain, consumer = result.Request.Consumer,
                diagnostics = result.Diagnostics
            }),
            diagnostics = parseDiagnostics
        }, JsonOptions));
        return parseDiagnostics.Count == 0 && results.All(x => x.Succeeded) ? 0 : 1;
    }

    private static int RunInspectCompose(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || IsHelpFlag(args[0])) { stdout.WriteLine(InspectComposeUsage); return args.Length == 0 ? 1 : 0; }
        var json = args.Skip(1).Any(x => x == "--json");
        var materialize = args.Skip(1).Any(x => x == "--materialize");
        if (!json || args.Skip(1).Any(x => x is not "--json" and not "--materialize")) { stderr.WriteLine(InspectComposeUsage); return 1; }
        if (!File.Exists(args[0])) { stderr.WriteLine($"Composition source was not found: {args[0]}"); return 1; }
        var total = Stopwatch.StartNew();
        var parseClock = Stopwatch.StartNew();
        var source = File.ReadAllText(args[0]);
        if (!FirmamentV2Parser.TryExpandCanonicalStaticAuthoring(source, out var materializerSource, out var staticDiagnostics))
        {
            stderr.WriteLine(string.Join(Environment.NewLine, staticDiagnostics));
            return 1;
        }
        var parsed = PrismaticProfileCompositionParser.Parse(materializerSource);
        parseClock.Stop(); var normalizeClock = Stopwatch.StartNew();
        var stack = PrismaticSectionStackCompiler.Normalize(parsed, out var diagnostics);
        normalizeClock.Stop();
        if (stack is null) { stderr.WriteLine(string.Join(Environment.NewLine, diagnostics)); return 1; }
        PrismaticSectionStackEmissionResult? emitted = null; BrepMassPropertiesResult? mass = null;
        var materializeClock = TimeSpan.Zero;
        if (materialize) { var clock = Stopwatch.StartNew(); emitted = PrismaticSectionStackEmitter.Emit(stack); mass = emitted.Body is null ? null : BrepMassProperties.Evaluate(emitted.Body); clock.Stop(); materializeClock = clock.Elapsed; }
        total.Stop();
        var report = new
        {
            composition = new
            {
                name = stack.Feature.Name, frame = stack.Feature.Frame, axis = stack.Feature.Axis,
                placement = new { stack.Feature.Placement.Name, anchor = new[] { stack.Feature.Placement.AnchorX, stack.Feature.Placement.AnchorY, stack.Feature.Placement.AnchorZ }, profilePlane = stack.Feature.Placement.ProfilePlane, stack.Feature.Placement.Axis, stack.Feature.Placement.ReferenceDirection, stack.Feature.Placement.IsExplicit },
                signatures = new { composition = CompositionSignature(stack), profiles = parsed.Profiles.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => ProfileSignature(x.Value), StringComparer.Ordinal) },
                operations = stack.Feature.Operations.Select(x => new { x.Name, intent = x.Intent.ToString(), profile = x.ProfileReference, x.From, x.To, x.SemanticRole, x.SourceSpan, x.SemanticFeatureId, x.SemanticFeatureKind, x.Diameter, signature = Hash($"{x.Intent}|{x.ProfileReference}|{x.From:R}|{x.To:R}|{x.SemanticRole}|{x.SemanticFeatureId}|{x.Diameter:R}") }),
                shaftHoles = (stack.Feature.ShaftHoles ?? []).Select(x => new { x.Name, x.StableId, profile = x.ProfileReference, center = new[] { x.CenterX, x.CenterY }, x.Diameter, x.From, x.To, endCondition = "ThroughAll", x.SemanticRole, x.SourceSpan }),
                counterboreHoles = (stack.Feature.CounterboreHoles ?? []).Select(x => new { x.Name, x.StableId, shaftProfile = x.ShaftProfileReference, counterboreProfile = x.CounterboreProfileReference, center = new[] { x.CenterX, x.CenterY }, x.Diameter, x.CounterboreDiameter, x.CounterboreDepth, x.From, x.To, endCondition = "ThroughAll", x.SemanticRole, x.SourceSpan }),
                capsuleSlots = (stack.Feature.CapsuleSlots ?? []).Select(x => new { x.Name, x.StableId, kind = "Slot<Capsule>", profile = x.ProfileReference, center = new[] { x.CenterX, x.CenterY }, direction = new[] { x.DirectionX, x.DirectionY }, x.Length, x.Width, radius = x.Radius, straightSpan = x.StraightSpan, endCenters = new[] { new[] { x.CenterX - x.DirectionX * x.StraightSpan / 2d, x.CenterY - x.DirectionY * x.StraightSpan / 2d }, new[] { x.CenterX + x.DirectionX * x.StraightSpan / 2d, x.CenterY + x.DirectionY * x.StraightSpan / 2d } }, x.From, x.To, x.Extent, x.SemanticRole, x.SourceSpan, segments = parsed.Profiles[x.ProfileReference].Loops.Single().Segments.Select(s => s.Name) }),
                roundedRectangleSlots = (stack.Feature.RoundedRectangleSlots ?? []).Select(x => new { x.Name, x.StableId, kind = "Slot<RoundedRectangle>", profile = x.ProfileReference, center = new[] { x.CenterX, x.CenterY }, direction = new[] { x.DirectionX, x.DirectionY }, x.Length, x.Width, x.CornerRadius, x.From, x.To, x.Extent, x.SemanticRole, x.SourceSpan, segments = parsed.Profiles[x.ProfileReference].Loops.Single().Segments.Select(s => s.Name) }),
                criticalLevels = stack.Feature.CriticalLevels,
                slabs = stack.Slabs.Select(x => new
                {
                    x.From,
                    x.To,
                    x.ActiveOperations,
                    area = PrismaticSectionStackCompiler.Area(x.Region), signature = ProfileSignature(x.Region.Outer),
                    outerLoops = 1,
                    innerLoops = x.Region.Holes.Count,
                    loops = new[]
                    {
                        new { role = "Outer", signedAreaInProfileFrame = PrismaticSectionStackCompiler.ProfileArea(x.Region.Outer), sourceWinding = PrismaticSectionStackCompiler.ProfileArea(x.Region.Outer) >= 0d ? "CounterClockwise" : "Clockwise", materialFacingWinding = "CounterClockwise" }
                    }.Concat(x.Region.Holes.Select(h => new { role = "Inner", signedAreaInProfileFrame = PrismaticSectionStackCompiler.ProfileArea(h), sourceWinding = PrismaticSectionStackCompiler.ProfileArea(h) >= 0d ? "CounterClockwise" : "Clockwise", materialFacingWinding = "Clockwise" })),
                    lineSegments = x.Region.Outer.Loops[0].Segments.Count(s => s.Geometry is LineArcLineSegment2D),
                    arcSegments = x.Region.Outer.Loops[0].Segments.Count(s => s.Geometry is LineArcCircularArc2D)
                    , arrangement = x.Arrangement is null ? null : new
                    {
                        sourceSegmentCount = x.Arrangement.SourceCurves.Count,
                        intersectionVertexCount = x.Arrangement.IntersectionVertices.Count,
                        atomicFragmentCount = x.Arrangement.AtomicFragments.Count,
                        coincidentFragmentCount = x.Arrangement.CoincidentFragmentCount,
                        retainedBoundaryFragmentCount = x.Arrangement.RetainedBoundaryFragmentCount,
                        resultLoopCount = x.Arrangement.ResultLoops.Count,
                        perimeter = x.Arrangement.ResultLoops.Sum(loop => loop.Perimeter),
                        timingsMilliseconds = new { intersections = x.Arrangement.IntersectionTime.TotalMilliseconds, splitting = x.Arrangement.SplitTime.TotalMilliseconds, classification = x.Arrangement.ClassificationTime.TotalMilliseconds, reconstruction = x.Arrangement.ReconstructionTime.TotalMilliseconds },
                        provenance = x.Region.Provenance,
                        diagnostics = x.Arrangement.Diagnostics
                    }
                }),
                transitions = stack.Transitions.Select(x => new { x.Level, upwardRegionCount = x.UpwardRegions.Count, downwardRegionCount = x.DownwardRegions.Count, upwardArea = x.UpwardRegions.Sum(PrismaticSectionStackCompiler.Area), downwardArea = x.DownwardRegions.Sum(PrismaticSectionStackCompiler.Area) }),
                analyticVolume = stack.AnalyticVolume,
                materialization = materialize ? new
                {
                    bRepStatus = mass?.Status.ToString(),
                    bRepVolume = mass?.AbsoluteVolume,
                    volumeDelta = mass is null ? (double?)null : mass.AbsoluteVolume - stack.AnalyticVolume,
                    bRepErrorBound = mass?.ErrorBound,
                    bRepEnclosed = mass?.IsEnclosed,
                    bRepDiagnostics = mass?.Diagnostics,
                    // The construction topology plan contains dictionaries keyed by
                    // internal ID value objects.  Expose its public contract rather
                    // than leaking an unserializable implementation graph.
                    bRepPlan = emitted?.Plan is { } plan ? new
                    {
                        plan.Signature,
                        plan.Vertices,
                        plan.Edges,
                        plan.Faces,
                        plan.Policy,
                        plan.Authoritative,
                        correspondence = plan.Correspondence is { } correspondence ? new
                        {
                            correspondence.BodyStableId,
                            descendantCount = correspondence.Descendants.Count,
                            correspondence.ProvenanceChain
                        } : null
                    } : null
                } : null,
                expansion = parsed.Expansion,
                timingsMilliseconds = new { parse = parseClock.Elapsed.TotalMilliseconds, normalize = normalizeClock.Elapsed.TotalMilliseconds, materialize = materializeClock.TotalMilliseconds, total = total.Elapsed.TotalMilliseconds },
                executionBoundary = new { inspectionOnly = !materialize, bRepMaterialized = materialize, stepExported = false, m8Executed = false, cirExecuted = false },
                diagnostics = staticDiagnostics.Concat(emitted?.Diagnostics ?? stack.Diagnostics).Distinct()
            }
        };
        stdout.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
        return 0;
    }

    private static int RunValidate(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0)
        {
            stderr.WriteLine(ValidateUsage);
            return 1;
        }

        if (IsHelpFlag(args[0]))
        {
            WriteValidateHelp(stdout);
            return 0;
        }

        var sourcePath = args[0];
        var json = false;
        var forgePackPaths = new List<string>();
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--forge-pack" when i + 1 < args.Length:
                    forgePackPaths.Add(args[++i]);
                    break;
                case "--forge-pack":
                    stderr.WriteLine("Validate option --forge-pack requires a local assembly path.");
                    stderr.WriteLine(ValidateUsage);
                    return 1;
                case "--json":
                    json = true;
                    break;
                case "-h":
                case "--help":
                    WriteValidateHelp(stdout);
                    return 0;
                default:
                    stderr.WriteLine($"Unknown validate option '{args[i]}'.");
                    stderr.WriteLine(ValidateUsage);
                    return 1;
            }
        }

        if (!File.Exists(sourcePath))
        {
            stderr.WriteLine($"Validation input was not found: {sourcePath}");
            return 1;
        }

        var runtimeConfiguration = CreateValidateForgeRuntimeConfiguration(forgePackPaths);
        var parse = FirmamentV2Parser.Parse(
            File.ReadAllText(sourcePath),
            Path.GetDirectoryName(Path.GetFullPath(sourcePath)),
            runtimeConfiguration.Catalog);
        var runtimeValidation = FirmamentV2RuntimeConceptValidation.Validate(parse.Document, runtimeConfiguration);
        var report = FirmamentV2ValidationReportBuilder.Build(parse, sourcePath, runtimeValidation, runtimeConfiguration.Catalog);
        if (json) stdout.WriteLine(JsonSerializer.Serialize(new { firmamentV2Validation = report }, JsonOptions));
        else stdout.WriteLine($"Firmament V2 validation: {report.Status} ({report.Summary.FatalDiagnosticCount} fatal, {report.Summary.WarningDiagnosticCount} warning)");
        return report.Status == "invalid" ? 1 : 0;
    }

    private static FirmamentV2ForgeRuntimeConfiguration CreateValidateForgeRuntimeConfiguration(IReadOnlyList<string> forgePackPaths)
    {
        if (forgePackPaths.Count == 0)
        {
            return FirmamentV2ForgeRuntimeConfiguration.CreateDefault();
        }

        var loader = new ForgeConceptPackAssemblyLoader();
        var packs = new List<(IForgeConceptPack Pack, string AssemblyPath)>();
        foreach (var forgePackPath in forgePackPaths)
        {
            foreach (var pack in loader.LoadFromAssemblyPath(forgePackPath))
            {
                packs.Add((pack, Path.GetFullPath(forgePackPath)));
            }
        }

        return FirmamentV2ForgeRuntimeConfiguration.Create(packs);
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
        var canonMode = Aetheris.Kernel.Core.Step242.Step242CanonMode.Deterministic;

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
                case "--mode" when i + 1 < args.Length:
                    var modeValue = args[++i];
                    if (string.Equals(modeValue, "deterministic", StringComparison.OrdinalIgnoreCase))
                    {
                        canonMode = Aetheris.Kernel.Core.Step242.Step242CanonMode.Deterministic;
                    }
                    else if (string.Equals(modeValue, "production", StringComparison.OrdinalIgnoreCase) || string.Equals(modeValue, "production-preserve-metadata", StringComparison.OrdinalIgnoreCase))
                    {
                        canonMode = Aetheris.Kernel.Core.Step242.Step242CanonMode.ProductionPreserveMetadata;
                    }
                    else
                    {
                        stderr.WriteLine($"Unknown canon mode '{modeValue}'. Expected deterministic or production.");
                        stderr.WriteLine(CanonUsage);
                        return 1;
                    }
                    break;
                case "--mode":
                    stderr.WriteLine("Canon option --mode requires deterministic or production.");
                    stderr.WriteLine(CanonUsage);
                    return 1;
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

        var exportOptions = canonMode == Aetheris.Kernel.Core.Step242.Step242CanonMode.ProductionPreserveMetadata
            ? Aetheris.Kernel.Core.Step242.Step242ExportOptions.FromSourceMetadata(Aetheris.Kernel.Core.Step242.Step242SourceMetadataReader.Read(stepText))
            : new Aetheris.Kernel.Core.Step242.Step242ExportOptions();

        var exportResult = Aetheris.Kernel.Core.Step242.Step242Exporter.ExportBody(importResult.Value, exportOptions);
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
                shellCount = topology.Shells.Count(),
                mode = canonMode == Aetheris.Kernel.Core.Step242.Step242CanonMode.ProductionPreserveMetadata ? "production" : "deterministic"
            }, JsonOptions));
        }
        else
        {
            stdout.WriteLine($"Canonical STEP written: {outputFullPath}");
            stdout.WriteLine($"Canon mode: {(canonMode == Aetheris.Kernel.Core.Step242.Step242CanonMode.ProductionPreserveMetadata ? "production" : "deterministic")}");
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

        if (string.Equals(Path.GetExtension(stepPath), ".firmament", StringComparison.OrdinalIgnoreCase))
        {
            stderr.WriteLine("Analyze expects STEP; use `aetheris build <file.firmament>` first.");
            return 1;
        }
        if (!string.Equals(Path.GetExtension(stepPath), ".step", StringComparison.OrdinalIgnoreCase) && !string.Equals(Path.GetExtension(stepPath), ".stp", StringComparison.OrdinalIgnoreCase))
        {
            stderr.WriteLine("Analyze expects STEP (.step or .stp) input.");
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
        string? plane = null;
        string? direction = null;
        string? views = null;
        (double U, double V)? point = null;
        var json = false;
        var llm = false;
        var rankProbes = false;
        var evidenceBundle = false;

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
                case "--resolution" when i + 1 < args.Length && TryParseResolution(args[++i], out var parsedCols2, out var parsedRows2):
                    cols = parsedCols2;
                    rows = parsedRows2;
                    break;
                case "--resolution":
                    stderr.WriteLine("Analyze map option --resolution requires a value like 32x32.");
                    stderr.WriteLine(AnalyzeMapUsage);
                    return 1;
                case "--plane" when i + 1 < args.Length:
                    plane = args[++i];
                    break;
                case "--plane":
                    stderr.WriteLine("Analyze map option --plane requires xy, xz, or yz.");
                    stderr.WriteLine(AnalyzeMapUsage);
                    return 1;
                case "--direction" when i + 1 < args.Length:
                    direction = args[++i];
                    break;
                case "--views" when i + 1 < args.Length:
                    views = args[++i];
                    break;
                case "--views":
                    stderr.WriteLine("Analyze map option --views requires six.");
                    stderr.WriteLine(AnalyzeMapUsage);
                    return 1;
                case "--llm":
                case "--summary":
                    llm = true;
                    break;
                case "--rank-probes":
                    rankProbes = true;
                    llm = true;
                    break;
                case "--evidence-bundle":
                    evidenceBundle = true;
                    rankProbes = true;
                    llm = true;
                    break;
                case "--direction":
                    stderr.WriteLine("Analyze map option --direction requires +x, -x, +y, -y, +z, or -z.");
                    stderr.WriteLine(AnalyzeMapUsage);
                    return 1;
                case "--point" when i + 1 < args.Length && TryParsePoint(args[++i], out var parsedPoint):
                    point = parsedPoint;
                    rows ??= 1;
                    cols ??= 1;
                    break;
                case "--point":
                    stderr.WriteLine("Analyze map option --point requires a comma-separated coordinate like 3,4.");
                    stderr.WriteLine(AnalyzeMapUsage);
                    return 1;
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

        var sixViewMode = string.Equals(views, "six", StringComparison.OrdinalIgnoreCase);
        if (views is not null && !sixViewMode)
        {
            stderr.WriteLine("Analyze map option --views currently supports only 'six'.");
            stderr.WriteLine(AnalyzeMapUsage);
            return 1;
        }

        var legacyViewMode = plane is null && view.HasValue;
        if (legacyViewMode)
        {
            var legacyView = view.GetValueOrDefault();
            (plane, direction) = legacyView switch
            {
                OrthographicView.Top => ("xy", "-z"),
                OrthographicView.Bottom => ("xy", "+z"),
                OrthographicView.Front => ("xz", "-y"),
                OrthographicView.Back => ("xz", "+y"),
                OrthographicView.Left => ("yz", "+x"),
                OrthographicView.Right => ("yz", "-x"),
                _ => ("xy", "-z")
            };
        }

        if (!sixViewMode && (plane is null || direction is null || (viewOptionCount > 0 && viewOptionCount != 1)))
        {
            stderr.WriteLine("Analyze map requires --plane and --direction (or one legacy view option --top|--bottom|--front|--back|--left|--right).");
            return 1;
        }

        if (sixViewMode && (point.HasValue || plane is not null || direction is not null || viewOptionCount > 0 || !llm))
        {
            stderr.WriteLine("Analyze map --views six requires --llm or --summary and cannot be combined with --point, --plane, --direction, or legacy view flags.");
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

        object map;
        try
        {
            map = sixViewMode
                ? (rankProbes || evidenceBundle ? StepAnalyzer.AnalyzeSixViewMapEvidenceBundle(stepPath, cols.Value, rows.Value) : StepAnalyzer.AnalyzeSixViewMapSummary(stepPath, cols.Value, rows.Value))
                : legacyViewMode
                ? StepAnalyzer.AnalyzeMap(stepPath, view.GetValueOrDefault(), rows.Value, cols.Value)
                : StepAnalyzer.AnalyzeRayMap(stepPath, plane!, direction!, cols.Value, rows.Value, point);
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

    private static int RunSections(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || IsHelpFlag(args[0])) { stdout.WriteLine(SectionsUsage); return args.Length == 0 ? 1 : 0; }
        var step = args[0]; string? axis = null; string? levelText = null; var epsilon = .001d; var json = false;
        for (var i = 1; i < args.Length; i++)
            switch (args[i])
            {
                case "--axis" when i + 1 < args.Length: axis = args[++i]; break;
                case "--levels" when i + 1 < args.Length: levelText = args[++i]; break;
                case "--epsilon" when i + 1 < args.Length && double.TryParse(args[++i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var e) && e > 0: epsilon = e; break;
                case "--json": json = true; break;
                default: stderr.WriteLine(SectionsUsage); return 1;
            }
        if (!json || !string.Equals(axis, "Z", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(levelText)) { stderr.WriteLine(SectionsUsage); return 1; }
        var levels = levelText.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => double.TryParse(x, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z) ? (double?)z : null).ToArray();
        if (levels.Length == 0 || levels.Any(x => x is null)) { stderr.WriteLine("--levels requires comma-separated numeric values."); return 1; }
        try
        {
            var result = new { artifact = Path.GetFullPath(step), axis = "Z", epsilon, policy = "Below/Above are requested level ± epsilon; At is never shifted and is diagnostic.", levels = levels.Select(x => new { requestedLevel = x!.Value, below = StepAnalyzer.AnalyzeSection(step, SectionPlaneFamily.XY, x.Value - epsilon), at = StepAnalyzer.AnalyzeSection(step, SectionPlaneFamily.XY, x.Value), above = StepAnalyzer.AnalyzeSection(step, SectionPlaneFamily.XY, x.Value + epsilon) }).ToArray() };
            stdout.WriteLine(JsonSerializer.Serialize(result, JsonOptions)); return 0;
        }
        catch (Exception ex) { stderr.WriteLine($"sections failed: {ex.Message}"); return 1; }
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
            stdout.WriteLine($"Face {analysis.Face.FaceId}: stepEntity={analysis.Face.StepEntity ?? "n/a"}, type={analysis.Face.SurfaceType ?? "n/a"}, status={analysis.Face.SurfaceStatus}, bbox={FormatBox(analysis.Face.BoundingBox)}, point={FormatPoint(analysis.Face.RepresentativePoint)}, anchor={FormatPoint(analysis.Face.AnchorPoint)}, apex={FormatPoint(analysis.Face.Apex)}, normal={FormatVector(analysis.Face.PlanarNormal)}, axis={FormatVector(analysis.Face.Axis)}, radius={FormatDouble(analysis.Face.Radius)}, placementRadius={FormatDouble(analysis.Face.PlacementRadius)}, majorRadius={FormatDouble(analysis.Face.MajorRadius)}, minorRadius={FormatDouble(analysis.Face.MinorRadius)}, semiAngleRadians={FormatDouble(analysis.Face.SemiAngleRadians)}, edges=[{string.Join(",", analysis.Face.AdjacentEdgeIds)}]");
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
        stderr.WriteLine($"Unknown command '{command}'. Expected one of: validate, build, inspect, analyze, verify, view.");
        stderr.WriteLine("Run 'aetheris --help' for usage and examples.");
        return 1;
    }

    private static bool IsTopLevelHelpRequest(string value) =>
        IsHelpFlag(value)
        || string.Equals(value, "help", StringComparison.Ordinal);

    private static bool IsHelpFlag(string value) =>
        string.Equals(value, "--help", StringComparison.Ordinal)
        || string.Equals(value, "-h", StringComparison.Ordinal);

    private static bool TryParseResolution(string value, out int cols, out int rows)
    {
        cols = 0;
        rows = 0;
        var parts = value.Split('x', 'X');
        return parts.Length == 2
            && int.TryParse(parts[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out cols)
            && int.TryParse(parts[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out rows);
    }

    private static bool TryParsePoint(string value, out (double U, double V) point)
    {
        point = default;
        var parts = value.Split(',');
        if (parts.Length != 2) return false;
        if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var u)) return false;
        if (!double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)) return false;
        point = (u, v);
        return true;
    }

    private static bool IsVersionFlag(string value) =>
        string.Equals(value, "--version", StringComparison.Ordinal)
        || string.Equals(value, "-v", StringComparison.Ordinal);

    private static string GetDisplayVersion()
    {
        var assembly = typeof(CliRunner).Assembly;
        var informational = assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), inherit: false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .SingleOrDefault()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(informational)
            ? assembly.GetName().Version?.ToString() ?? "unknown"
            : informational.Split('+', 2)[0];
    }

    private static void WriteTopLevelHelp(TextWriter stdout)
    {
        stdout.WriteLine("Aetheris — exact code-first CAD");
        stdout.WriteLine();
        stdout.WriteLine(TopLevelUsage);
        stdout.WriteLine();
        stdout.WriteLine("Commands:");
        stdout.WriteLine("  validate   Check Firmament source without materializing geometry.");
        stdout.WriteLine("  build      Compile Firmament to exact STEP AP242.");
        stdout.WriteLine("  mesh       Export a supported exact B-rep as STL or topology-preserving OBJ.");
        stdout.WriteLine("  view       Build/open a model in Cadmata.");
        stdout.WriteLine("  inspect    Inspect Firmament semantics or STEP topology.");
        stdout.WriteLine("  analyze    Analyze STEP topology and analytic surfaces.");
        stdout.WriteLine("  fea        Compile and solve a Firmament linear-elastic analysis and export Abaqus verification input.");
        stdout.WriteLine("  verify     Build/reimport and verify a model.");
        stdout.WriteLine();
        stdout.WriteLine("Global options:");
        stdout.WriteLine("  -h, --help       Show help.");
        stdout.WriteLine("  -v, --version    Show CLI version.");
        stdout.WriteLine();
        stdout.WriteLine("Examples:");
        stdout.WriteLine("  aetheris validate bracket.firmament");
        stdout.WriteLine("  aetheris build bracket.firmament");
        stdout.WriteLine("  aetheris view bracket.firmament");
        stdout.WriteLine("  aetheris analyze imported.step --json");
        stdout.WriteLine("  aetheris fea plate-with-hole.firmament --out-dir artifacts --json");
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
        stdout.WriteLine("  --output <path> Optional output STEP path; defaults to a .step file beside the source.");
        stdout.WriteLine("  --json         Emit machine-readable success/failure JSON.");
        stdout.WriteLine("  -h, --help     Show this help.");
        stdout.WriteLine();
        stdout.WriteLine("Example:");
        stdout.WriteLine("  aetheris build part.firmament");
        stdout.WriteLine("  aetheris build part.firmament --output out/part.step --json");
    }

    private static void WriteMatchHelp(TextWriter stdout)
    {
        stdout.WriteLine("Match a compile-time Concept Struct against observed STEP geometry.");
        stdout.WriteLine("This does not reconstruct the original feature history.");
        stdout.WriteLine();
        stdout.WriteLine(MatchUsage);
        stdout.WriteLine();
        stdout.WriteLine("Supported M5 evidence: body bounds, analytic planar faces, cylindrical axes, and declared hole-center point sets.");
        stdout.WriteLine("Use `Match { MountPoints As HoleCenters { Diameter: 8.5mm Axis: +Z Kind: Through } }` in the source for point-set hole matching.");
        stdout.WriteLine("Matched and Partial exit 0; Conflicted or invalid input exits 1.");
    }

    private static void WriteValidateHelp(TextWriter stdout)
    {
        stdout.WriteLine("Check syntax, binding, dimensions, and static semantics without materializing geometry.");
        stdout.WriteLine("Use build for materialization, STEP generation, and build-time assertions.");
        stdout.WriteLine();
        stdout.WriteLine(ValidateUsage);
        stdout.WriteLine("  --json                Emit Firmament V2 validation report JSON.");
        stdout.WriteLine("  --forge-pack <path>   Load a trusted local .NET assembly containing IForgeConceptPack implementations. This executes local code; Aetheris does not sandbox external packs. Do not load untrusted packs.");
    }

    private static void WriteInspectHelp(TextWriter stdout)
    {
        stdout.WriteLine("Inspect what Aetheris understands a model to mean.");
        stdout.WriteLine("Firmament input reports semantic declarations; STEP input reports topology through analyze.");
        stdout.WriteLine();
        stdout.WriteLine(InspectUsage);
        stdout.WriteLine("Example: aetheris inspect part.firmament --json");
    }

    private static void WriteViewHelp(TextWriter stdout)
    {
        stdout.WriteLine("Open a STEP model in Cadmata.");
        stdout.WriteLine("Firmament input is built to its adjacent .step artifact before launch; STEP input opens directly.");
        stdout.WriteLine();
        stdout.WriteLine(ViewUsage);
        stdout.WriteLine("  --cadmata-path <path>  Explicit Cadmata executable (legacy --cad-assistant-path is accepted). Otherwise packaged siblings, environment compatibility settings, PATH, and a development build are checked.");
        stdout.WriteLine("  --json                 Emit launch details without waiting for Cadmata to exit.");
        stdout.WriteLine("Example: aetheris view part.firmament");
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
        stdout.WriteLine("  either --plane <xy|xz|yz> with --direction <axis>, exactly one legacy view, or --views six --llm.");
        stdout.WriteLine("  legacy views: --top | --bottom | --front | --back | --left | --right");
        stdout.WriteLine("  --rows <N>       Positive integer row count.");
        stdout.WriteLine("  --cols <N>       Positive integer column count.");
        stdout.WriteLine("  --resolution NxM Alternative to --cols N --rows M.");
        stdout.WriteLine("  --json           Required output mode.");
        stdout.WriteLine();
        stdout.WriteLine("Six-view convention:");
        stdout.WriteLine("  top xy/-z, bottom xy/+z, right yz/-x, left yz/+x, back xz/+y, front xz/-y.");
        stdout.WriteLine();
        stdout.WriteLine("Example:");
        stdout.WriteLine("  aetheris analyze map part.step --top --rows 48 --cols 64 --json");
        stdout.WriteLine("  aetheris analyze map part.step --views six --resolution 16x16 --llm --rank-probes --json");
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

    private static void WriteVerifyHelp(TextWriter stdout)
    {
        stdout.WriteLine(VerifyUsage);
        stdout.WriteLine();
        stdout.WriteLine("Firmament input is built first; STEP input is reimported directly for independent B-rep mass properties and optional external display inspection.");
        stdout.WriteLine("The report is hash-tied to artifacts/verification/<fixture>/<sha256>/ by default.");
        stdout.WriteLine();
        stdout.WriteLine("Options:");
        stdout.WriteLine("  --expected-volume <value>   Compare independent measured volume with external analytic evidence; never used by the evaluator.");
        stdout.WriteLine("  --cad-assistant              Launch configured CAD Assistant for external display observation.");
        stdout.WriteLine("  --cad-assistant-path <path> Explicit CAD Assistant executable; otherwise AETHERIS_CAD_ASSISTANT_PATH and two standard install paths are checked.");
        stdout.WriteLine("  --timeout <seconds>          External inspection timeout (default 30).");
        stdout.WriteLine("  --evidence-dir <path>        Override hash-tied evidence directory.");
        stdout.WriteLine("  --require-external           Return exit 2 when CAD Assistant is unavailable.");
        stdout.WriteLine("  --json                       Emit the unified report on stdout.");
    }

    private static void WriteCanonHelp(TextWriter stdout)
    {
        stdout.WriteLine("Canonicalize part-like STEP/AP242 through Aetheris import/export.");
        stdout.WriteLine();
        stdout.WriteLine(CanonUsage);
        stdout.WriteLine();
        stdout.WriteLine("Options:");
        stdout.WriteLine("  --out <path>   Required canonical AP242 output path.");
        stdout.WriteLine("  --mode <mode>  deterministic (default) or production metadata preservation.");
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

    private sealed record ProfileJunctionInspection(
        string ProfileId, string LoopId, string PredecessorSegment, string SuccessorSegment,
        string VertexId, double SignedTurnDegrees, double MaterialInteriorAngleDegrees,
        string Classification, bool SelectedByEdgeFinish, IReadOnlyList<string> GeneratedDescendants);

    private sealed record ProfileStraightEdgeFilletInspection(
        bool Succeeded, string? Diagnostic, string? EdgeFinishId, string? ProfileId, string? LoopId, string? TargetKind, IReadOnlyList<string> SegmentIds,
        string? Side, double? Radius, double? EndClearance, string? EndpointPolicy,
        double[]? CylinderAxis, double[]? CylinderCenterlineStart, double[]? CylinderCenterlineEnd,
        double[]? CapContactStart, double[]? CapContactEnd, double[]? SideContactStart, double[]? SideContactEnd,
        string CorridorClassification, IReadOnlyList<string> GeneratedDescendants, IReadOnlyList<string> Provenance,
        ProfileConvexFilletJunctionInspection? ConvexJunction, string? ReflexJunctionStyle = null);

    private sealed record ProfileConvexFilletJunctionInspection(
        string VertexId, string Classification, double InteriorAngleDegrees, double Radius,
        double[] SphereCenter, IReadOnlyList<double[]> RollAxes, IReadOnlyList<double[]> RollExternalCenters,
        IReadOnlyList<double[]> RollJunctionCenters, string SurfaceFamily, double? MajorRadius,
        double? MinorRadius, double[]? ParametricBounds);

    private static ProfileStraightEdgeFilletInspection? DescribeProfileStraightEdgeFillet(ResolvedProfile2D profile, string source, string hostBodyId)
    {
        if (!ProfileBoundaryChamferSourceBinder.HasProfileBoundaryFillet(source)) return null;
        if (!ProfileBoundaryChamferSourceBinder.TryBindFillet(source, profile, hostBodyId, out var target, out var radius, out var clearance, out var diagnostic))
            return new(false, diagnostic, null, null, null, null, [], null, null, null, null, null, null, null, null, null, null, null, "RejectedBeforePlan", [], [], null);
        var result = ProfileFilletShellPlanner.TryPlan(profile, target!, radius, clearance);
        if (!result.Succeeded)
            return new(false, result.Diagnostics.FirstOrDefault(), target!.StableId, target.ProfileId, target.LoopId, target.ChainKind.ToString(), target.SegmentIds, target.Side.ToString(), radius, clearance, null, null, null, null, null, null, null, null, "RejectedBeforeTopology", [], [], null);
        static double[] V(Direction3D value) => [value.ToVector().X, value.ToVector().Y, value.ToVector().Z];
        static double[] P(Point3D value) => [value.X, value.Y, value.Z];
        if (result.Plan is not null)
        {
            var plan = result.Plan;
            var reflex = plan.Junction as ProfileReflexFilletJunctionPlan;
            var sphereSeam = plan.Junction is ProfileReflexSphereSeamCompatibilityJunctionPlan;
            var junction = new ProfileConvexFilletJunctionInspection(plan.Junction.VertexId, plan.Junction.Classification.Classification.ToString(), plan.Junction.Classification.MaterialInteriorAngleRadians * 180d / Math.PI, plan.Junction.Radius, P(plan.Junction.Center), plan.Rolls.Select(roll => V(roll.Tangent)).ToArray(), plan.Rolls.Select(roll => P(roll.ExternalCenter)).ToArray(), plan.Rolls.Select(roll => P(roll.JunctionCenter)).ToArray(), reflex is null ? sphereSeam ? "SphereSeamCompatibility" : "Sphere" : "HornTorus", reflex?.Torus.MajorRadius, reflex?.Torus.MinorRadius, reflex is null ? null : [reflex.MajorStartRadians, reflex.MajorEndRadians, reflex.MinorStartRadians, reflex.MinorEndRadians]);
            return new(true, null, target!.StableId, target.ProfileId, target.LoopId, target.ChainKind.ToString(), target.SegmentIds, target.Side.ToString(), radius, clearance, plan.EndpointPolicy,
                null, null, null, null, null, null, null, "DisjointNoCavitiesInBareProfileM2", result.Correspondence?.Descendants.Select(x => x.StableId).OrderBy(x => x, StringComparer.Ordinal).ToArray() ?? [], result.Correspondence?.ProvenanceChain ?? [], junction, target.ReflexJunctionStyle.ToString());
        }
        var single = result.SingleSegmentPlan!;
        return new(true, null, target!.StableId, target.ProfileId, target.LoopId, target.ChainKind.ToString(), target.SegmentIds, target.Side.ToString(), radius, clearance, single.EndpointPolicy,
            V(single.Tangent), P(single.CylinderCenterlineStart), P(single.CylinderCenterlineEnd), P(single.CapContactStart), P(single.CapContactEnd), P(single.SideContactStart), P(single.SideContactEnd),
            "DisjointNoCavitiesInBareProfileM1", result.Correspondence?.Descendants.Select(x => x.StableId).OrderBy(x => x, StringComparer.Ordinal).ToArray() ?? [], result.Correspondence?.ProvenanceChain ?? [], null);
    }

    private static IReadOnlyList<ProfileJunctionInspection> DescribeProfileJunctions(ResolvedProfile2D profile, string source, string hostBodyId)
    {
        ProfileBoundaryChamferTarget? target = null;
        SemanticTopologyCorrespondence? correspondence = null;
        if (ProfileBoundaryChamferSourceBinder.TryBind(source, profile, hostBodyId, out var bound, out var distance, out _))
        {
            target = bound;
            var plan = ProfileBoundaryChamferPlanner.TryPlan(profile, bound!, distance);
            correspondence = plan.Correspondence;
        }
        var selected = target?.SegmentIds.ToHashSet(StringComparer.Ordinal) ?? [];
        return ProfileJunctionClassifier.Classify(profile).Select(junction =>
        {
            var selectedByFinish = selected.Contains(junction.PredecessorSegmentId) && selected.Contains(junction.SuccessorSegmentId);
            var descendantPrefix = target is null ? string.Empty : $"{target.StableId}:{junction.Classification}({junction.VertexId})";
            var descendants = correspondence?.Descendants.Where(descendant => descendant.StableId == descendantPrefix).Select(descendant => descendant.StableId).ToArray() ?? [];
            return new ProfileJunctionInspection(junction.ProfileId, junction.LoopId, junction.PredecessorSegmentId, junction.SuccessorSegmentId,
                junction.VertexId, junction.SignedTurnRadians * 180d / Math.PI, junction.MaterialInteriorAngleRadians * 180d / Math.PI,
                junction.Classification.ToString(), selectedByFinish, descendants);
        }).ToArray();
    }

    private static string ProfileSignature(ResolvedProfile2D profile) => Hash(string.Join(";", profile.Loops.SelectMany(l => l.Segments).Select(s => s.Geometry switch
    {
        LineArcLineSegment2D line => $"L:{Q(line.Start.X)},{Q(line.Start.Y)}:{Q(line.End.X)},{Q(line.End.Y)}",
        LineArcCircularArc2D arc => $"A:{Q(arc.Center.X)},{Q(arc.Center.Y)}:{Q(arc.Radius)}:{Q(arc.StartAngleRadians)}:{Q(arc.SweepAngleRadians)}",
        _ => s.Geometry.GetType().Name
    }).OrderBy(x => x, StringComparer.Ordinal)));

    private static string CompositionSignature(PrismaticSectionStackConstruction stack) => Hash(string.Join("|", stack.Slabs.Select(s => $"{Q(s.From)}:{Q(s.To)}:{ProfileSignature(s.Region.Outer)}:{Q(PrismaticSectionStackCompiler.Area(s.Region))}")));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static long Q(double value) => (long)Math.Round(value * 1_000_000d, MidpointRounding.AwayFromZero);
}
