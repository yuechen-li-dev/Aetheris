using System.Text.Json;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Import;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

// The experiment now consumes the production kernel. No parallel surgery engine.
static class MoveExperiment
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    sealed class RevisionRefusal(string reason) : Exception(reason);
    public static void Run(BrepBody source, int seed, Vector3D delta, string directory)
    {
        var face = new FaceId(seed);
        if (!source.TryGetFaceSurfaceGeometry(face, out var surface) || surface?.Cylinder is not { } cylinder)
            throw new RevisionRefusal("Select a cylindrical seed face.");
        var axis = cylinder.Axis.ToVector();
        var walls = source.Topology.Faces.Where(f => source.TryGetFaceSurfaceGeometry(f.Id, out var s) && s?.Cylinder is { } c
            && System.Math.Abs(c.Radius - cylinder.Radius) < 1e-6 && c.Axis.ToVector().Cross(axis).Length < 1e-6
            && (c.Origin - cylinder.Origin).Cross(axis).Length < 1e-6).Select(f => f.Id).ToArray();
        var edgeIds = walls.SelectMany(id => source.Topology.GetFace(id).LoopIds)
            .SelectMany(id => source.Topology.GetLoop(id).CoedgeIds).Select(id => source.Topology.GetCoedge(id).EdgeId).ToHashSet();
        var vertices = edgeIds.SelectMany(id => { var e = source.Topology.GetEdge(id); return new[] { e.StartVertexId, e.EndVertexId }; }).Distinct()
            .Select(id => source.TryGetVertexPoint(id, out var point) ? point : throw new InvalidOperationException("Missing vertex.")).ToArray();
        var low = vertices.Min(p => p.Z); var high = vertices.Max(p => p.Z);
        var entry = source.Topology.Faces.FirstOrDefault(f => source.TryGetFaceSurfaceGeometry(f.Id, out var s) && s?.Plane is { } plane
            && System.Math.Abs(plane.Origin.Z - high) < 1e-6
            && f.LoopIds.SelectMany(id => source.Topology.GetLoop(id).CoedgeIds).Any(id => edgeIds.Contains(source.Topology.GetCoedge(id).EdgeId)));
        if (entry is null) throw new RevisionRefusal("No upper planar entry support.");
        var center = cylinder.Origin + delta; center = center with { Z = high };
        var pad = cylinder.Radius + 2.5;
        var within = new ThroughHoleEditBounds(new(System.Math.Min(center.X, cylinder.Origin.X) - pad, System.Math.Min(center.Y, cylinder.Origin.Y) - pad, low),
            new(System.Math.Max(center.X, cylinder.Origin.X) + pad, System.Math.Max(center.Y, cylinder.Origin.Y) + pad, high));
        var tolerance = source.ImportQualification?.PcurveToleranceMillimetres ?? 1e-3;
        var edited = BrepThroughHoleRevision.Execute(source, new("MovedHole", walls, entry.Id, center, 2 * cylinder.Radius, within, tolerance));
        if (!edited.IsSuccess) throw new RevisionRefusal(string.Join(" | ", edited.Diagnostics.Select(d => d.Message)));
        var result = edited.Value;
        var step = Step242Exporter.ExportBody(result.Body, new Step242ExportOptions
        {
            BrepExportPreflightMode = BrepExportPreflightMode.Enforce,
            ImportedRecoveryToleranceMillimetres = tolerance,
            DistanceAccuracyMillimetres = System.Math.Max(tolerance, source.ImportQualification?.SourceDistanceAccuracyMillimetres ?? 0)
        });
        if (!step.IsSuccess) throw new InvalidOperationException(string.Join(" | ", step.Diagnostics));
        var roundtrip = Step242Importer.ImportBody(step.Value, new ImportPolicy(PcurveQualificationToleranceMillimetres: tolerance));
        if (!roundtrip.IsSuccess || roundtrip.Value.ImportQualification?.GeometryStatus != ImportQualificationStatus.Qualified)
            throw new InvalidOperationException("Production kernel result did not roundtrip as qualified.");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "replace.step"), step.Value);
        File.WriteAllText(Path.Combine(directory, "report.json"), JsonSerializer.Serialize(new
        {
            strategy = "ThroughHoleSuppressionReinsertion",
            kernel = "BrepThroughHoleRevision",
            geometryOnly = true,
            source.ImportQualification,
            tolerance,
            result.Center,
            result.Diameter,
            result.Depth,
            result.Within,
            result.PreservedFaces,
            result.PreservedEdges,
            retired = result.RetiredWallFaces.Select(id => id.Value),
            walls = result.WallFaces.Select(id => id.Value),
            roundtripQualification = roundtrip.Value.ImportQualification,
            note = "BRep geometry harness; use the Firmament fixture for source pinning, diagnostics and semantic replacement PMI."
        }, Json));
        Console.WriteLine($"Moved hole {seed}: diameter {result.Diameter:R}, center {center}; retained {result.PreservedFaces} faces/{result.PreservedEdges} edges.");
    }

    public static void Qualify(BrepBody source, string directory)
    {
        var cases = new[]{("web-hole",1,30d,true),("overlap",1,5d,true),("noop",1,0d,true),
            ("collision",1,320d,false),("breakout",1,-250d,false),("nonhole",4,30d,false),("blind",13,30d,false),("lobe",72,5d,false)};
        var before = JsonSerializer.Serialize(new { source.Topology, curves = source.Geometry.Curves, surfaces = source.Geometry.Surfaces });
        var results = new List<object>();
        foreach (var (name, seed, dx, accept) in cases)
        {
            try
            {
                Run(source, seed, new(dx, 0, 0), Path.Combine(directory, name));
                if (!accept) throw new InvalidOperationException($"Unexpected acceptance: {name}.");
                results.Add(new { name, passed = true, accepted = true });
            }
            catch (RevisionRefusal refusal) when (!accept) { results.Add(new { name, passed = true, accepted = false, reason = refusal.Message }); }
            if (before != JsonSerializer.Serialize(new { source.Topology, curves = source.Geometry.Curves, surfaces = source.Geometry.Surfaces }))
                throw new InvalidOperationException("Kernel edited the original input.");
        }
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "cases.json"), JsonSerializer.Serialize(results, Json));
        Console.WriteLine($"Production kernel qualification passed: {cases.Length} cases.");
    }
}
