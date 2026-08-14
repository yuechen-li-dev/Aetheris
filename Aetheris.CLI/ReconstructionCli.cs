using System.Text.Json;
using Aetheris.Reconstruction;

namespace Aetheris.CLI;

internal static class ReconstructionCli
{
    private const string Usage = "Usage: aetheris reconstruct mesh <input.ply> --mode fast --out <output.obj> [--report <report.json>] [--error-ply <samples.ply>] [--json]";

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr, JsonSerializerOptions jsonOptions)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help") { WriteHelp(stdout); return args.Length == 0 ? 1 : 0; }
        if (!string.Equals(args[0], "mesh", StringComparison.Ordinal)) { stderr.WriteLine(Usage); return 1; }
        if (args.Length < 2) { stderr.WriteLine(Usage); return 1; }
        var input = args[1]; string? output = null; string? reportPath = null; string? errorPly = null; var json = false; var mode = "fast";
        for (var i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--mode" when i + 1 < args.Length: mode = args[++i]; break;
                case "--out" when i + 1 < args.Length: output = args[++i]; break;
                case "--report" when i + 1 < args.Length: reportPath = args[++i]; break;
                case "--error-ply" when i + 1 < args.Length: errorPly = args[++i]; break;
                case "--json": json = true; break;
                case "-h" or "--help": WriteHelp(stdout); return 0;
                default: stderr.WriteLine($"Unknown reconstruct option '{args[i]}'."); stderr.WriteLine(Usage); return 1;
            }
        }
        if (!string.Equals(mode, "fast", StringComparison.OrdinalIgnoreCase))
        {
            stderr.WriteLine("Only the bounded experimental 'fast' reconstruction policy is currently implemented."); return 1;
        }
        if (output is null || !File.Exists(input)) { stderr.WriteLine(Usage); return 1; }
        if (!string.Equals(Path.GetExtension(input), ".ply", StringComparison.OrdinalIgnoreCase))
        {
            stderr.WriteLine("Experimental reconstruction currently supports ASCII triangle PLY input."); return 1;
        }
        if (!string.Equals(Path.GetExtension(output), ".obj", StringComparison.OrdinalIgnoreCase))
        {
            stderr.WriteLine("Experimental reconstruction currently exports OBJ visualization output."); return 1;
        }

        var fullInput = Path.GetFullPath(input); var fullOutput = Path.GetFullPath(output);
        using var stream = File.OpenRead(fullInput);
        var source = PlyTriangleSurfaceLoader.LoadAscii(stream, Path.GetFileName(input),
            new Dictionary<string, string> { ["path"] = fullInput, ["format"] = "ASCII triangle PLY" });
        var result = SurfaceReconstruction.Remesh(source, ReconstructionPolicy.Fast);
        var report = CreateReport(result, fullInput, fullOutput);
        if (result.Mesh is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutput)!);
            using var writer = File.CreateText(fullOutput); ReconstructionObjExporter.Write(result.Mesh, writer);
        }
        if (reportPath is not null)
        {
            var fullReport = Path.GetFullPath(reportPath); Directory.CreateDirectory(Path.GetDirectoryName(fullReport)!);
            File.WriteAllText(fullReport, JsonSerializer.Serialize(report, jsonOptions));
        }
        if (errorPly is not null && result.Quality is not null)
        {
            var fullError = Path.GetFullPath(errorPly); Directory.CreateDirectory(Path.GetDirectoryName(fullError)!);
            using var writer = File.CreateText(fullError); ReconstructionPlyExporter.WriteErrorSamples(result, writer);
        }
        if (json) stdout.WriteLine(JsonSerializer.Serialize(report, jsonOptions));
        else
        {
            stdout.WriteLine($"Experimental approximate reconstruction: {result.Status}");
            if (result.Statistics is { } statistics)
                stdout.WriteLine($"{statistics.Topology.Vertices} vertices, {statistics.Topology.Quads} quads, {statistics.Topology.Triangles} transitions, {statistics.Topology.QuadPercentage:F3}% quads, {statistics.Topology.BoundaryLoops} boundary loops, {statistics.TotalMilliseconds:F1} ms");
            if (result.Mesh is not null) stdout.WriteLine($"OBJ: {fullOutput}");
            if (reportPath is not null) stdout.WriteLine($"Report: {Path.GetFullPath(reportPath)}");
            if (errorPly is not null) stdout.WriteLine($"Error samples: {Path.GetFullPath(errorPly)}");
        }
        return result.Status is ReconstructionStatus.Success or ReconstructionStatus.Partial ? 0 : 1;
    }

    private static object CreateReport(SurfaceReconstructionResult result, string input, string output) => new
    {
        experimental = true,
        approximation = result.Provenance.Approximation,
        status = result.Status,
        input,
        output = result.Mesh is null ? null : output,
        policy = result.Provenance.Policy,
        result.Quality,
        result.Statistics,
        diagnostics = result.Diagnostics.Take(20),
        correspondence = new
        {
            result.Correspondences.Count,
            result.Correspondences.LookupCount,
            result.Correspondences.HitCount,
            result.Correspondences.ProjectionCallCount,
            result.Correspondences.InvalidatedEntryCount
        },
        result.Provenance
    };

    private static void WriteHelp(TextWriter output)
    {
        output.WriteLine("Experimentally reconstruct an approximate, predominantly quad SurfaceMeshDocument from a triangle surface.");
        output.WriteLine("This is shrink-wrap/structured remeshing, not CAD feature or design-intent recognition.");
        output.WriteLine(); output.WriteLine(Usage);
        output.WriteLine(); output.WriteLine("Supported input: ASCII PLY containing triangle faces. Output: deterministic OBJ, optional compact JSON, and optional colored error-sample PLY.");
        output.WriteLine("Fast uses scale-derived tolerance, bounded local matching, cached correspondence, bounded quality sampling, and no global atlas search.");
    }
}
