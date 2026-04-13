using System.Text.Json;
using Aetheris.Kernel.Firmament;

namespace Aetheris.CLI;

public static class CliRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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

        var analysis = StepAnalyzer.Analyze(stepPath, faceId, edgeId, vertexId);
        if (json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(analysis, JsonOptions));
            return 0;
        }

        WriteSummaryText(analysis, stdout);
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
        stdout.WriteLine("Surface Families:");
        foreach (var family in summary.SurfaceFamilies)
        {
            stdout.WriteLine($"  {family.Key}: {family.Value}");
        }

        if (analysis.Face is not null)
        {
            stdout.WriteLine($"Face {analysis.Face.FaceId}: type={analysis.Face.SurfaceType}, bbox={FormatBox(analysis.Face.BoundingBox)}, point={FormatPoint(analysis.Face.RepresentativePoint)}, normal={FormatVector(analysis.Face.PlanarNormal)}, axis={FormatVector(analysis.Face.Axis)}, radius={FormatDouble(analysis.Face.Radius)}, semiAngleRadians={FormatDouble(analysis.Face.SemiAngleRadians)}, edges=[{string.Join(",", analysis.Face.AdjacentEdgeIds)}]");
        }

        if (analysis.Edge is not null)
        {
            stdout.WriteLine($"Edge {analysis.Edge.EdgeId}: curve={analysis.Edge.CurveType}, start={analysis.Edge.StartVertexId}:{FormatPoint(analysis.Edge.StartVertex)}, end={analysis.Edge.EndVertexId}:{FormatPoint(analysis.Edge.EndVertex)}, faces=[{string.Join(",", analysis.Edge.AdjacentFaceIds)}], length={FormatDouble(analysis.Edge.Length)}");
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
