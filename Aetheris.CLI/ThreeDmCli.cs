using System.Text.Json;
using Aetheris.ThreeDm;

namespace Aetheris.CLI;

internal static class ThreeDmCli
{
    public static int Recover(string[] args, TextWriter stdout, TextWriter stderr, JsonSerializerOptions jsonOptions)
    {
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            stdout.WriteLine("Usage: aetheris recover-3dm <file.3dm> [--recovery-tolerance-mm <positive-mm>] [--body <index> [--edge <index>|--face <index>]] [--bind-step --out-dir <local-directory> [--combined-step]] [--json]");
            return 0;
        }
        if (args.Length < 1)
        {
            stderr.WriteLine("Usage: aetheris recover-3dm <file.3dm> [--recovery-tolerance-mm <positive-mm>] [--body <index> [--edge <index>|--face <index>]] [--bind-step --out-dir <local-directory> [--combined-step]] [--json]");
            return 1;
        }
        var json = false;
        var bindStep = false;
        var combinedStep = false;
        string? outputDirectory = null;
        var recoveryTolerance = 0.1d;
        int? bodyIndex = null, edgeIndex = null, faceIndex = null;
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--json") { json = true; continue; }
            if (args[i] == "--bind-step") { bindStep = true; continue; }
            if (args[i] == "--combined-step") { combinedStep = true; continue; }
            if (args[i] == "--out-dir" && i + 1 < args.Length)
            {
                outputDirectory = args[++i];
                continue;
            }
            if (args[i] == "--recovery-tolerance-mm" && i + 1 < args.Length &&
                double.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsedTolerance) &&
                double.IsFinite(parsedTolerance) && parsedTolerance > 0d)
            {
                recoveryTolerance = parsedTolerance;
                i++;
                continue;
            }
            if (args[i] is "--body" or "--edge" or "--face" && i + 1 < args.Length &&
                int.TryParse(args[i + 1], out var index) && index >= 0)
            {
                switch (args[i])
                {
                    case "--body": bodyIndex = index; break;
                    case "--edge": edgeIndex = index; break;
                    case "--face": faceIndex = index; break;
                }
                i++;
                continue;
            }
            stderr.WriteLine($"Invalid recover-3dm option: {args[i]}");
            return 1;
        }
        if ((edgeIndex.HasValue || faceIndex.HasValue) && (!bodyIndex.HasValue || edgeIndex.HasValue && faceIndex.HasValue))
        {
            stderr.WriteLine("--edge or --face requires --body; choose only one focused entity.");
            return 1;
        }
        if (bindStep)
        {
            if (edgeIndex.HasValue || faceIndex.HasValue || string.IsNullOrWhiteSpace(outputDirectory) || combinedStep && bodyIndex.HasValue)
            {
                stderr.WriteLine("--bind-step requires --out-dir; --combined-step requires the whole file and cannot be combined with --body, --edge, or --face.");
                return 1;
            }
            var bound = ThreeDmStepRecovery.Run(args[0], outputDirectory, recoveryTolerance, bodyIndex, combinedStep);
            if (json) stdout.WriteLine(JsonSerializer.Serialize(bound, jsonOptions));
            else
            {
                stdout.WriteLine($"Bound {bound.QualifiedBodyCount}/{bound.Bodies.Count} source BReps to STEP and reimported them; {bound.PartialBodyCount} partial; faces {bound.BoundFaceCount}/{bound.Bodies.Sum(body => body.SourceFaces)}, trims {bound.BoundTrimCount}/{bound.Bodies.Sum(body => body.SourceTrims)}; {bound.TotalMilliseconds:F0} ms total.");
                foreach (var body in bound.Bodies)
                    stdout.WriteLine($"Object {body.ObjectIndex}: status={body.Status}, components={body.ComponentCount}, faces {body.BoundFaces}/{body.SourceFaces}, trims {body.BoundTrims}/{body.SourceTrims}, pcurves {body.SourcePcurves} source/{body.RecoveredPcurves} recovered, enclosed={body.BoundIsEnclosed}, STEP={body.StepExported}, reimport={body.StepReimported}, path={body.StepPath ?? "none"}, diagnostic={body.Diagnostics.FirstOrDefault() ?? "none"}");
                if (combinedStep)
                    stdout.WriteLine($"Combined STEP: reimport={bound.CombinedStepReimported}, solids={bound.CombinedSolidCount}, drift={bound.CombinedBoundingBoxDriftMillimetres:G6} mm, path={bound.CombinedStepPath ?? "none"}, diagnostic={bound.CombinedDiagnostics.FirstOrDefault() ?? "none"}");
            }
            return combinedStep ? bound.CombinedStepReimported ? 0 : 2 : bound.QualifiedBodyCount > 0 ? 0 : 2;
        }
        if (outputDirectory is not null || combinedStep)
        {
            stderr.WriteLine("--out-dir and --combined-step require --bind-step.");
            return 1;
        }
        var report = ThreeDmRecovery.Analyze(args[0], recoveryTolerance);
        if (bodyIndex is int selectedBody)
        {
            var body = report.Bodies.FirstOrDefault(b => b.ObjectIndex == selectedBody);
            if (body is null) { stderr.WriteLine($"3DM BRep object {selectedBody} was not found."); return 1; }
            object focus = body;
            if (edgeIndex is int selectedEdge)
                focus = new { body.ObjectIndex, body.SourceId, Edge = body.Edges.FirstOrDefault(e => e.EdgeIndex == selectedEdge)
                    ?? throw new ArgumentOutOfRangeException(nameof(edgeIndex), $"Edge {selectedEdge} was not found in object {selectedBody}.") };
            if (faceIndex is int selectedFace)
                focus = new { body.ObjectIndex, body.SourceId, Face = body.Faces.FirstOrDefault(f => f.FaceIndex == selectedFace)
                    ?? throw new ArgumentOutOfRangeException(nameof(faceIndex), $"Face {selectedFace} was not found in object {selectedBody}.") };
            if (json) stdout.WriteLine(JsonSerializer.Serialize(focus, jsonOptions));
            else stdout.WriteLine($"Object {selectedBody}: {JsonSerializer.Serialize(focus, jsonOptions)}");
        }
        else if (json)
            stdout.WriteLine(JsonSerializer.Serialize(report, jsonOptions));
        else
        {
            stdout.WriteLine($"Source: {report.Bodies.Count} BReps, {report.SourceRationalEdgeCount} rational edges, {report.SourceRationalSurfaceCount} rational surfaces");
            stdout.WriteLine($"Rational edges: {report.NativeAnalyticRationalEdgeCount} native analytic supports; {report.StudiedRationalEdgeCount} previously unclassified; {report.QualifiedRecoveredRationalEdgeCount} of those have support candidates within source tolerance; {report.UnresolvedRationalEdgeCount} unresolved supports");
            stdout.WriteLine($"Rational surface study: {report.QualifiedAnalyticRationalSurfaceCount} analytic support candidates within source tolerance; {report.UnresolvedRationalSurfaceCount} unresolved");
            stdout.WriteLine($"Generic spline supports at {report.RecoveryToleranceMillimetres:G6} mm: {report.RecoveredCurveCount} curves, {report.RecoveredSurfaceCount} surfaces; {report.UnrecoveredCurveCount} curves and {report.UnrecoveredSurfaceCount} surfaces exceed/failed budget; trim/topology qualification pending");
            stdout.WriteLine(report.ProductionStatus);
        }
        return 0;
    }

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr, JsonSerializerOptions jsonOptions)
    {
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            stdout.WriteLine("Usage: aetheris inspect-3dm <file.3dm> [--json]");
            return 0;
        }
        if (args.Length is < 1 or > 2 || (args.Length == 2 && args[1] != "--json"))
        {
            stderr.WriteLine("Usage: aetheris inspect-3dm <file.3dm> [--json]");
            return 1;
        }
        var inventory = ThreeDmInspector.Inspect(args[0]);
        if (args.Length == 2)
            stdout.WriteLine(JsonSerializer.Serialize(inventory, jsonOptions));
        else
        {
            stdout.WriteLine($"3DM v{inventory.ArchiveVersion}: {inventory.ObjectCount} objects, {inventory.LayerCount} layers");
            stdout.WriteLine($"Units: {inventory.SourceUnit} ({inventory.MillimetresPerSourceUnit} mm/unit); absolute tolerance: {inventory.AbsoluteToleranceMillimetres} mm");
            stdout.WriteLine($"BReps: {inventory.BrepCount}; faces/loops/trims/edges/vertices: {inventory.FaceCount}/{inventory.LoopCount}/{inventory.TrimCount}/{inventory.EdgeCount}/{inventory.VertexCount}");
            stdout.WriteLine($"Seam/singular trims: {inventory.SeamTrimCount}/{inventory.SingularTrimCount}; rational generic edges: {inventory.RationalGenericEdgeCount}");
            stdout.WriteLine($"Import qualification: not yet mapped to Aetheris BRep; STEP conversion unavailable.");
        }
        return 0;
    }
}
