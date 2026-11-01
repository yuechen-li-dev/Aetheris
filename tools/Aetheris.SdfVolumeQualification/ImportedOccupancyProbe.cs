using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

internal static class ImportedOccupancyProbe
{
    public static int Run(string path)
    {
        var source = File.ReadAllText(path);
        var imported = Step242Importer.ImportBody(source);
        if (!imported.IsSuccess)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { Status = "ImportRejected", imported.Diagnostics }));
            return 1;
        }
        var body = imported.Value;
        var vertices = body.Topology.Vertices.Select(v => body.TryGetVertexPoint(v.Id, out var p) ? p
            : throw new InvalidOperationException("Probe requires explicit vertex coordinates.")).ToArray();
        var min = new Point3D(vertices.Min(p => p.X), vertices.Min(p => p.Y), vertices.Min(p => p.Z));
        var max = new Point3D(vertices.Max(p => p.X), vertices.Max(p => p.Y), vertices.Max(p => p.Z));
        var query = BrepSpatialQueries.CreatePointContainmentQueryContext(body);
        _ = BrepSpatialQueries.ClassifyPoint(body, min, null, query);
        var rows = new List<object>();
        var counts = new Dictionary<string, int>();
        var watch = Stopwatch.StartNew();
        for (var x = 0; x < 5; x++)
        for (var y = 0; y < 5; y++)
        for (var z = 0; z < 5; z++)
        {
            var point = new Point3D(min.X + (max.X - min.X) * (x + .37) / 5,
                min.Y + (max.Y - min.Y) * (y + .37) / 5, min.Z + (max.Z - min.Z) * (z + .37) / 5);
            var result = BrepSpatialQueries.ClassifyPoint(body, point, null, query);
            var status = result.IsSuccess ? result.Value.ToString() : "QueryRejected";
            counts[status] = counts.GetValueOrDefault(status) + 1;
            rows.Add(new { Point = new[] { point.X, point.Y, point.Z }, Classification = status });
        }
        watch.Stop();
        var admitted = BrepSdfVolumeLowering.TryLower(body, out _, out var reason);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Status = "PointProbeNotVolumeQualification",
            SourceStepSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant(),
            FaceCount = body.Topology.Faces.Count(), Counts = counts, Milliseconds = watch.Elapsed.TotalMilliseconds,
            CompleteFieldAdmitted = admitted, FieldAdmissionReason = reason,
            SamplingDomain = new { Minimum = new[] { min.X, min.Y, min.Z }, Maximum = new[] { max.X, max.Y, max.Z },
                Basis = "Vertex extents only; not certified enclosing bounds or a volume measure." },
            Samples = rows
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
}
