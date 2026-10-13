using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Web.Perf;

/// <summary>
/// BUILD-PERF cross-runtime determinism check: two STEP files (for example the native and the
/// WASM export of one project) describe the same product within a tolerance. Topology counts and
/// product structure must match exactly; volume, area, centroid and bounds within a relative
/// tolerance. Byte identity is checked per runtime by the ratchet; this checks equivalence.
/// </summary>
internal static class CompareStep
{
    private sealed record Metrics(int Faces, int Edges, int Vertices, double Volume, double Area,
        double[] Centroid, double[] Minimum, double[] Maximum);

    public static int Run(string[] args)
    {
        if (args.Length < 2) return Usage();
        var tolerance = 1e-6;
        for (var i = 2; i < args.Length; i++)
        {
            if (args[i] == "--tolerance" && i + 1 < args.Length) tolerance = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
            else return Usage();
        }
        var (left, leftOccurrences) = Load(args[0]);
        var (right, rightOccurrences) = Load(args[1]);
        var failures = new List<string>();
        var worst = 0d;
        foreach (var name in left.Keys.Union(right.Keys).Order(StringComparer.Ordinal))
        {
            if (!left.TryGetValue(name, out var a) || !right.TryGetValue(name, out var b)) { failures.Add($"{name}: present in only one file"); continue; }
            if (a is null || b is null) { if (a is null != b is null) failures.Add($"{name}: geometry present in only one file"); continue; }
            if ((a.Faces, a.Edges, a.Vertices) != (b.Faces, b.Edges, b.Vertices))
                failures.Add($"{name}: topology {a.Faces}F/{a.Edges}E/{a.Vertices}V vs {b.Faces}F/{b.Edges}E/{b.Vertices}V");
            var scale = Math.Max(1e-12, Enumerable.Range(0, 3).Max(axis => Math.Abs(a.Maximum[axis] - a.Minimum[axis])));
            void Close(string what, double x, double y, double reference)
            {
                var relative = Math.Abs(x - y) / Math.Max(Math.Abs(reference), 1e-12);
                worst = Math.Max(worst, relative);
                if (relative > tolerance) failures.Add($"{name}: {what} {x:G12} vs {y:G12} (relative {relative:G3})");
            }
            Close("volume", a.Volume, b.Volume, a.Volume);
            Close("area", a.Area, b.Area, a.Area);
            for (var axis = 0; axis < 3; axis++)
            {
                Close($"centroid[{axis}]", a.Centroid[axis], b.Centroid[axis], scale);
                Close($"minimum[{axis}]", a.Minimum[axis], b.Minimum[axis], scale);
                Close($"maximum[{axis}]", a.Maximum[axis], b.Maximum[axis], scale);
            }
        }
        if (leftOccurrences.Count != rightOccurrences.Count) failures.Add($"occurrences: {leftOccurrences.Count} vs {rightOccurrences.Count}");
        foreach (var (path, transform) in leftOccurrences)
        {
            if (!rightOccurrences.TryGetValue(path, out var other)) { failures.Add($"occurrence {path}: missing"); continue; }
            var difference = transform.Zip(other, (x, y) => Math.Abs(x - y)).DefaultIfEmpty(0).Max();
            if (difference > tolerance) failures.Add($"occurrence {path}: transform differs by {difference:G3}");
        }
        Console.WriteLine($"{left.Count} definition(s), {leftOccurrences.Count} occurrence(s); worst relative difference {worst:G3}; tolerance {tolerance:G3}.");
        foreach (var failure in failures) Console.Error.WriteLine($"DIFF {failure}");
        Console.WriteLine(failures.Count == 0 ? "Equivalent within tolerance." : $"{failures.Count} difference(s).");
        return failures.Count == 0 ? 0 : 1;
    }

    private static int Usage()
    {
        Console.Error.WriteLine("Usage: Aetheris.Web.Perf compare-step <a.step> <b.step> [--tolerance 1e-6]");
        return 2;
    }

    private static (Dictionary<string, Metrics?> Definitions, Dictionary<string, IReadOnlyList<double>> Occurrences) Load(string path)
    {
        var text = File.ReadAllText(path);
        var structure = Step242AssemblyImporter.Import(text);
        if (structure.IsSuccess && structure.Value.Definitions.Count > 1)
        {
            var definitions = new Dictionary<string, Metrics?>(StringComparer.Ordinal);
            foreach (var definition in structure.Value.Definitions)
                definitions[Unique(definitions, definition.Name)] = definition.Geometry is { } body ? Measure(body) : null;
            var byId = structure.Value.Definitions.ToDictionary(d => d.StableId, d => d.Name, StringComparer.Ordinal);
            var occurrences = new Dictionary<string, IReadOnlyList<double>>(StringComparer.Ordinal);
            var parents = structure.Value.Occurrences.ToDictionary(o => o.StableId, StringComparer.Ordinal);
            foreach (var occurrence in structure.Value.Occurrences)
            {
                var segments = new List<string>();
                for (var current = occurrence; current is not null; current = current.ParentStableId is { } parent && parents.TryGetValue(parent, out var next) ? next : null)
                    segments.Insert(0, current.Name);
                occurrences[Unique(occurrences, string.Join('/', segments) + ":" + byId.GetValueOrDefault(occurrence.DefinitionStableId))] = occurrence.LocalTransform;
            }
            return (definitions, occurrences);
        }
        var single = Step242Importer.ImportBody(text);
        if (!single.IsSuccess || single.Value is null) throw new InvalidOperationException($"{path}: STEP import failed.");
        return (new(StringComparer.Ordinal) { ["body"] = Measure(single.Value) }, new(StringComparer.Ordinal));
    }

    private static string Unique<T>(Dictionary<string, T> map, string name)
    {
        var key = name; var index = 1;
        while (map.ContainsKey(key)) key = $"{name}#{++index}";
        return key;
    }

    private static Metrics Measure(BrepBody body)
    {
        var mass = BrepMassProperties.Evaluate(body);
        var points = body.Topology.Vertices.Select(v => body.TryGetVertexPoint(v.Id, out var p) ? p : default).ToArray();
        var minimum = points.Length == 0 ? new double[3] : new[] { points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z) };
        var maximum = points.Length == 0 ? new double[3] : new[] { points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z) };
        var centroid = mass.Centroid is { } c ? new[] { c.X, c.Y, c.Z } : new double[3];
        return new(body.Topology.Faces.Count(), body.Topology.Edges.Count(), body.Topology.Vertices.Count(),
            mass.AbsoluteVolume, mass.SurfaceArea, centroid, minimum, maximum);
    }
}
