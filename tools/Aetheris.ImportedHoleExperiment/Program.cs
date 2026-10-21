using System.Text.Json;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

// Outside the solution: a fixture harness for the production kernel owner,
// not a second Firmament admission path.
try
{
    if (args.Length == 0 || args[0] is "--help" or "-h")
    {
        Console.WriteLine("Imported-hole experiment: <input.step> [inventory | move <seed-face> <dx-mm> <dy-mm> [output-directory] | qualify [output-directory]] [--import-tolerance-mm value]");
        return;
    }
    var toleranceIndex = Array.IndexOf(args, "--import-tolerance-mm");
    var tolerance = toleranceIndex >= 0 && toleranceIndex + 1 < args.Length
        ? double.Parse(args[toleranceIndex + 1], System.Globalization.CultureInfo.InvariantCulture)
        : args.Length > 1 && args[1] == "qualify" ? .01 : .001;
    var imported = Step242Importer.ImportBody(File.ReadAllText(args[0]), new Aetheris.Kernel.Core.Import.ImportPolicy(PcurveQualificationToleranceMillimetres: tolerance));
    if (!imported.IsSuccess) throw new Exception(JsonSerializer.Serialize(imported.Diagnostics));
    var body = imported.Value;
    if (args.Length > 1 && args[1] == "move")
    {
        if (args.Length < 5) throw new ArgumentException("move requires seed-face, dx-mm and dy-mm.");
        MoveExperiment.Run(body, int.Parse(args[2]), new(double.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture), double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture), 0),
            args.Length > 5 && !args[5].StartsWith("--", StringComparison.Ordinal) ? args[5] : "artifacts/local/imported-hole-experiment");
        return;
    }
    if (args.Length > 1 && args[1] == "qualify")
    {
        MoveExperiment.Qualify(body, args.Length > 2 && !args[2].StartsWith("--", StringComparison.Ordinal) ? args[2] : "artifacts/local/imported-hole-experiment");
        return;
    }
    if (args.Length > 1 && args[1] != "inventory")
        throw new ArgumentException($"Unknown experimental operation '{args[1]}'.");
    var edgeFaces = body.Topology.Faces.SelectMany(f => f.LoopIds.SelectMany(l => body.Topology.GetLoop(l).CoedgeIds)
        .Select(c => body.Topology.GetCoedge(c).EdgeId).Distinct().Select(e => (Edge: e, Face: f.Id)))
        .GroupBy(x => x.Edge).ToDictionary(g => g.Key, g => g.Select(x => x.Face).ToArray());
    var inventory = body.Topology.Faces.Where(f => body.TryGetFaceSurfaceGeometry(f.Id, out var s) && s!.Kind == SurfaceGeometryKind.Cylinder).Select(f =>
    {
        body.TryGetFaceSurfaceGeometry(f.Id, out var s);
        var cylinder = s!.Cylinder!.Value;
        var edges = f.LoopIds.SelectMany(l => body.Topology.GetLoop(l).CoedgeIds).Select(c => body.Topology.GetCoedge(c).EdgeId).Distinct().ToArray();
        return new
        {
            face = f.Id.Value,
            step = body.Bindings.GetFaceBinding(f.Id).SourceStepEntityId,
            radius = cylinder.Radius,
            origin = cylinder.Origin,
            axis = cylinder.Axis,
            aligned = body.Bindings.GetFaceBinding(f.Id).Orientation.IsAlignedWithSurface,
            neighbors = edges.SelectMany(e => edgeFaces[e]).Where(id => id != f.Id).Distinct().Select(id =>
            {
                body.TryGetFaceSurfaceGeometry(id, out var ns);
                return new { face = id.Value, kind = ns!.Kind.ToString(), loops = body.Topology.GetFace(id).LoopIds.Count };
            }),
            edges = edges.Select(id =>
            {
                body.TryGetEdgeCurveGeometry(id, out var c); var edge = body.Topology.GetEdge(id); return new
                {
                    id = id.Value,
                    curve = c!.Kind.ToString(),
                    circle = c.Circle3,
                    start = edge.StartVertexId.Value,
                    end = edge.EndVertexId.Value,
                    trim = body.Bindings.GetEdgeBinding(id).TrimInterval
                };
            })
        };
    });
    Console.WriteLine(JsonSerializer.Serialize(inventory, new JsonSerializerOptions { WriteIndented = true }));
}
catch (Exception error)
{
    Console.Error.WriteLine(JsonSerializer.Serialize(new { status = "experiment-failed", errorType = error.GetType().Name, reason = error.Message }));
    Environment.ExitCode = 2;
}
