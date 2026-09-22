using System.Text.Json;
using Aetheris.ThreeDm;

namespace Aetheris.CLI;

internal static class ThreeDmCli
{
    public static int Recover(string[] args, TextWriter stdout, TextWriter stderr, JsonSerializerOptions jsonOptions)
    {
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            stdout.WriteLine("Usage: aetheris recover-3dm <file.3dm> [--body <index> [--edge <index>|--face <index>]] [--json]");
            return 0;
        }
        if (args.Length < 1)
        {
            stderr.WriteLine("Usage: aetheris recover-3dm <file.3dm> [--body <index> [--edge <index>|--face <index>]] [--json]");
            return 1;
        }
        var json = false;
        int? bodyIndex = null, edgeIndex = null, faceIndex = null;
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--json") { json = true; continue; }
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
        var report = ThreeDmRecovery.Analyze(args[0]);
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
