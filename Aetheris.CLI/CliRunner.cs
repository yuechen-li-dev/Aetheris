using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Kernel.Firmament;

namespace Aetheris.CLI;

public static class CliRunner
{
    private const string TopLevelUsage = "Usage: aetheris <build|analyze|canon> <path> [options]";
    private const string BuildUsage = "Usage: aetheris build <file.firmament> [--out <path>] [--json]";
    private const string AnalyzeUsage = "Usage: aetheris analyze <file.step> [--face <id>] [--edge <id>] [--vertex <id>] [--json]";
    private const string AnalyzeMapUsage = "Usage: aetheris analyze map <file.step> (--top|--bottom|--front|--back|--left|--right) --rows <N> --cols <N> --json";
    private const string AnalyzeSectionUsage = "Usage: aetheris analyze section <file.step> (--xy|--xz|--yz) --offset <value> --json";
    private const string CanonUsage = "Usage: aetheris canon <file.step> --out <canonical.step> [--json]";

    private static readonly JsonSerializerOptions JsonOptions = new()
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
                "canon" => RunCanon(args.Skip(1).ToArray(), stdout, stderr),
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

    private static int RunAnalyze(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0)
        {
            stderr.WriteLine(AnalyzeUsage);
            stderr.WriteLine($"   or: {AnalyzeMapUsage[7..]}");
            stderr.WriteLine($"   or: {AnalyzeSectionUsage[7..]}");
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

        if (args[0].StartsWith("-", StringComparison.Ordinal))
        {
            stderr.WriteLine("Analyze requires <file.step> as the first argument, or a subcommand ('map' or 'section').");
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
            if (json)
            {
                stdout.WriteLine(JsonSerializer.Serialize(new
                {
                    success = false,
                    stepPath = Path.GetFullPath(stepPath),
                    error = ex.Message
                }, JsonOptions));
            }
            else
            {
                stderr.WriteLine(ex.Message);
            }

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
            if (json)
            {
                stdout.WriteLine(JsonSerializer.Serialize(new
                {
                    success = false,
                    stepPath = Path.GetFullPath(stepPath),
                    error = ex.Message
                }, JsonOptions));
            }
            else
            {
                stderr.WriteLine(ex.Message);
            }

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
            stdout.WriteLine(JsonSerializer.Serialize(new
            {
                success = false,
                stepPath = Path.GetFullPath(stepPath),
                error = ex.Message
            }, JsonOptions));
            return 1;
        }

        stdout.WriteLine(JsonSerializer.Serialize(section, JsonOptions));
        return 0;
    }

    private static void WriteSummaryText(AnalyzeResult analysis, TextWriter stdout)
    {
        var summary = analysis.Summary;
        stdout.WriteLine($"STEP: {analysis.StepPath}");
        stdout.WriteLine($"Bodies: {summary.BodyCount}");
        stdout.WriteLine($"Shells: {summary.ShellCount}");
        stdout.WriteLine($"Faces: {summary.FaceCount}");
        stdout.WriteLine($"Edges: {summary.EdgeCount}");
        stdout.WriteLine($"Vertices: {summary.VertexCount}");
        stdout.WriteLine($"BoundingBox: {FormatBox(summary.BoundingBox)}");
        stdout.WriteLine($"Structure: {summary.StructuralAssessment} ({summary.StructuralAssessmentBasis})");
        stdout.WriteLine($"LengthUnit: {summary.LengthUnit} ({summary.LengthUnitBasis})");
        stdout.WriteLine($"FaceIds: min={summary.FaceIds.Min}, max={summary.FaceIds.Max}, count={summary.FaceIds.Count}, contiguous={summary.FaceIds.Contiguous}");
        stdout.WriteLine($"EdgeIds: min={summary.EdgeIds.Min}, max={summary.EdgeIds.Max}, count={summary.EdgeIds.Count}, contiguous={summary.EdgeIds.Contiguous}");
        stdout.WriteLine($"VertexIds: min={summary.VertexIds.Min}, max={summary.VertexIds.Max}, count={summary.VertexIds.Count}, contiguous={summary.VertexIds.Contiguous}");
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

    private static int UnknownCommand(string command, TextWriter stderr)
    {
        stderr.WriteLine($"Unknown command '{command}'. Expected one of: build, analyze, canon.");
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
        stdout.WriteLine("  canon      Import and re-export STEP/AP242 as canonical STEP.");
        stdout.WriteLine();
        stdout.WriteLine("Global options:");
        stdout.WriteLine("  -h, --help       Show help.");
        stdout.WriteLine("  -v, --version    Show CLI version.");
        stdout.WriteLine();
        stdout.WriteLine("Examples:");
        stdout.WriteLine("  aetheris build model.firmament --out model.step");
        stdout.WriteLine("  aetheris analyze model.step --json");
        stdout.WriteLine("  aetheris canon input.step --out canonical.step --json");
        stdout.WriteLine("  aetheris analyze map model.step --top --rows 40 --cols 60 --json");
        stdout.WriteLine("  aetheris analyze section model.step --xy --offset 2.5 --json");
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

    private static void WriteAnalyzeHelp(TextWriter stdout)
    {
        stdout.WriteLine("Analyze STEP geometry and topology.");
        stdout.WriteLine();
        stdout.WriteLine(AnalyzeUsage);
        stdout.WriteLine($"   or: {AnalyzeMapUsage[7..]}");
        stdout.WriteLine($"   or: {AnalyzeSectionUsage[7..]}");
        stdout.WriteLine();
        stdout.WriteLine("Options (summary mode):");
        stdout.WriteLine("  --face <id>     Inspect one face.");
        stdout.WriteLine("  --edge <id>     Inspect one edge.");
        stdout.WriteLine("  --vertex <id>   Inspect one vertex.");
        stdout.WriteLine("  --json          Emit machine-readable JSON.");
        stdout.WriteLine("  -h, --help      Show this help.");
        stdout.WriteLine();
        stdout.WriteLine("Rules:");
        stdout.WriteLine("  - At most one of --face, --edge, --vertex may be supplied.");
        stdout.WriteLine("  - Use 'aetheris analyze map --help' for orthographic map options.");
        stdout.WriteLine("  - Use 'aetheris analyze section --help' for section options.");
        stdout.WriteLine();
        stdout.WriteLine("Examples:");
        stdout.WriteLine("  aetheris analyze part.step --json");
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

    private static void WriteCanonHelp(TextWriter stdout)
    {
        stdout.WriteLine("Canonicalize STEP/AP242 through Aetheris import/export.");
        stdout.WriteLine();
        stdout.WriteLine(CanonUsage);
        stdout.WriteLine();
        stdout.WriteLine("Options:");
        stdout.WriteLine("  --out <path>   Required canonical AP242 output path.");
        stdout.WriteLine("  --json         Emit machine-readable success/failure JSON.");
        stdout.WriteLine("  -h, --help     Show this help.");
        stdout.WriteLine();
        stdout.WriteLine("Example:");
        stdout.WriteLine("  aetheris canon input.step --out canonical.step --json");
    }

    private static string FormatBox(Aetheris.Kernel.Core.Math.BoundingBox3D? box) =>
        box is null ? "unknown" : $"min{FormatPoint(box.Value.Min)} max{FormatPoint(box.Value.Max)}";

    private static string FormatPoint(Aetheris.Kernel.Core.Math.Point3D? point) =>
        point is null ? "unknown" : $"({point.Value.X:F6},{point.Value.Y:F6},{point.Value.Z:F6})";

    private static string FormatVector(Aetheris.Kernel.Core.Math.Vector3D? vector) =>
        vector is null ? "n/a" : $"({vector.Value.X:F6},{vector.Value.Y:F6},{vector.Value.Z:F6})";

    private static string FormatDouble(double? value) => value?.ToString("G17") ?? "n/a";
}
