using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Kernel.Firmament;

namespace Aetheris.CLI;

public static class CliRunner
{
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
            stderr.WriteLine("Usage: aetheris <build|analyze> <path> [options]");
            return 1;
        }

        try
        {
            return args[0] switch
            {
                "build" => RunBuild(args.Skip(1).ToArray(), stdout, stderr),
                "analyze" => RunAnalyze(args.Skip(1).ToArray(), stdout, stderr),
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
            stderr.WriteLine("Usage: aetheris build <file.firmament> [--out <path>] [--json]");
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
                case "--json":
                    json = true;
                    break;
                default:
                    stderr.WriteLine($"Unknown build option '{args[i]}'.");
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

    private static int RunAnalyze(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0)
        {
            stderr.WriteLine("Usage: aetheris analyze <file.step> [--face <id>] [--edge <id>] [--vertex <id>] [--json]");
            stderr.WriteLine("   or: aetheris analyze map <file.step> (--top|--bottom|--front|--back|--left|--right) --rows <N> --cols <N> --json");
            stderr.WriteLine("   or: aetheris analyze section <file.step> (--xy|--xz|--yz) --offset <value> --json");
            return 1;
        }

        if (string.Equals(args[0], "map", StringComparison.Ordinal))
        {
            return RunAnalyzeMap(args.Skip(1).ToArray(), stdout, stderr);
        }

        if (string.Equals(args[0], "section", StringComparison.Ordinal))
        {
            return RunAnalyzeSection(args.Skip(1).ToArray(), stdout, stderr);
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
                case "--edge" when i + 1 < args.Length && int.TryParse(args[++i], out var edge):
                    edgeId = edge;
                    break;
                case "--vertex" when i + 1 < args.Length && int.TryParse(args[++i], out var vertex):
                    vertexId = vertex;
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    stderr.WriteLine($"Unknown analyze option '{args[i]}'.");
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
            stderr.WriteLine("Usage: aetheris analyze map <file.step> (--top|--bottom|--front|--back|--left|--right) --rows <N> --cols <N> --json");
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
                case "--cols" when i + 1 < args.Length && int.TryParse(args[++i], out var parsedCols):
                    cols = parsedCols;
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    stderr.WriteLine($"Unknown analyze map option '{args[i]}'.");
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
            stderr.WriteLine("Analyze map currently requires --json output.");
            return 1;
        }

        stdout.WriteLine(JsonSerializer.Serialize(map, JsonOptions));
        return 0;
    }

    private static int RunAnalyzeSection(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0)
        {
            stderr.WriteLine("Usage: aetheris analyze section <file.step> (--xy|--xz|--yz) --offset <value> --json");
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
                case "--json":
                    json = true;
                    break;
                default:
                    stderr.WriteLine($"Unknown analyze section option '{args[i]}'.");
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
            stderr.WriteLine("Analyze section currently requires --json output.");
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
        stderr.WriteLine($"Unknown command '{command}'. Expected 'build' or 'analyze'.");
        return 1;
    }

    private static string FormatBox(Aetheris.Kernel.Core.Math.BoundingBox3D? box) =>
        box is null ? "unknown" : $"min{FormatPoint(box.Value.Min)} max{FormatPoint(box.Value.Max)}";

    private static string FormatPoint(Aetheris.Kernel.Core.Math.Point3D? point) =>
        point is null ? "unknown" : $"({point.Value.X:F6},{point.Value.Y:F6},{point.Value.Z:F6})";

    private static string FormatVector(Aetheris.Kernel.Core.Math.Vector3D? vector) =>
        vector is null ? "n/a" : $"({vector.Value.X:F6},{vector.Value.Y:F6},{vector.Value.Z:F6})";

    private static string FormatDouble(double? value) => value?.ToString("G17") ?? "n/a";
}
