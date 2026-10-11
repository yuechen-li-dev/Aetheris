using System.Collections.Immutable;
using System.Numerics;
using Aetheris.Cloth3D;

namespace Aetheris.Kernel.Firmament.Garment;

public sealed record CompiledGarment(GarmentSource Source, CompiledCloth3D Cloth, ImmutableArray<GarmentMeshPanel> Panels)
{
    public float MaximumSeamGap(ClothSnapshot3D snapshot)
    {
        ClothState3D.ValidateSnapshot(Cloth, snapshot);
        float maximum = 0;
        foreach (var stitch in Cloth.Definition.Stitches)
        {
            Vector3 gap = Vector3.Zero;
            for (int i = 0; i < stitch.Vertices.Length; i++)
            {
                gap += snapshot.Positions[stitch.Vertices[i]] * stitch.Weights[i];
            }
            maximum = MathF.Max(maximum, gap.Length());
        }
        return maximum;
    }
}

public sealed record GarmentCompilationResult(CompiledGarment? Garment, IReadOnlyList<GarmentDiagnostic> Diagnostics)
{
    public bool IsSuccess => Garment is not null && Diagnostics.Count == 0;
}

public static class GarmentCompiler
{
    public static GarmentCompilationResult Compile(string source, string sourceIdentity = "<memory>")
    {
        var parsed = GarmentAuthoring.Parse(source, sourceIdentity);
        if (!parsed.IsSuccess)
        {
            return new(null, parsed.Diagnostics);
        }
        try
        {
            return new(Compile(parsed.Source!), []);
        }
        catch (GarmentAuthoringException exception)
        {
            return new(null, [new(exception.Code, exception.Message, sourceIdentity)]);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return new(null, [new("garment-cloth-invalid", exception.Message, sourceIdentity)]);
        }
    }

    private static CompiledGarment Compile(GarmentSource source)
    {
        var rest = new List<Vector3>();
        var initial = new List<Vector3>();
        var coordinates = new List<Vector2>();
        var indices = new List<int>();
        var pins = new HashSet<int>();
        var panels = ImmutableArray.CreateBuilder<GarmentMeshPanel>();
        foreach (var panel in source.Panels)
        {
            var mesh = GarmentPanelMesher.Mesh(panel, source.MeshSize);
            int offset = rest.Count;
            float minimumY = mesh.Points.Min(point => point.Y);
            float maximumY = mesh.Points.Max(point => point.Y);
            var edges = mesh.Edges.ToDictionary(entry => entry.Key,
                entry => entry.Value.Select(vertex => vertex + offset).ToImmutableArray(), StringComparer.Ordinal);
            panels.Add(new(panel.Name, panel.Identity, offset, mesh.Points.Count, indices.Count, mesh.Indices.Count, edges));
            foreach (Vector2 point in mesh.Points)
            {
                rest.Add(new(point.X, point.Y, 0));
                Vector3 placed = panel.Origin + panel.U * point.X + panel.V * point.Y;
                if (panel.WrapRadius is { } radius)
                {
                    float fraction = (point.Y - minimumY) / (maximumY - minimumY);
                    float arrangedRadius = radius + ((panel.WrapTopRadius ?? radius) - radius) * fraction;
                    float offsetAngle = panel.WrapAngle + (panel.WrapTopAngle - panel.WrapAngle) * fraction;
                    float angle = point.X / arrangedRadius + offsetAngle;
                    Vector3 topOrigin = panel.WrapTopOrigin ?? (panel.Origin + panel.V * (maximumY - minimumY));
                    Vector3 center = Vector3.Lerp(panel.Origin, topOrigin, fraction);
                    Vector3 outward = -Vector3.Cross(panel.U, panel.V);
                    placed = center + panel.U * (arrangedRadius * MathF.Sin(angle))
                        + outward * (arrangedRadius * MathF.Cos(angle)) + panel.V * minimumY;
                }
                initial.Add(placed);
                Vector2 across = new(panel.Grain.Y, -panel.Grain.X);
                coordinates.Add(new(Vector2.Dot(point, panel.Grain), Vector2.Dot(point, across)));
            }
            indices.AddRange(mesh.Indices.Select(index => index + offset));
            foreach (string edge in panel.Pins)
            {
                if (!edges.TryGetValue(edge, out var boundary))
                {
                    GarmentAuthoring.Fail("garment-pin-edge-unknown", panel.Name + ".edges." + edge);
                }
                pins.UnionWith(boundary);
            }
        }
        var compiledPanels = panels.ToImmutable();
        var stitches = ImmutableArray.CreateBuilder<ClothStitch3D>();
        var usedEdges = new HashSet<string>(StringComparer.Ordinal);
        foreach (var seam in source.Stitches)
        {
            var a = Boundary(seam.A);
            var b = Boundary(seam.B);
            if (a.SequenceEqual(b))
            {
                GarmentAuthoring.Fail("garment-stitch-self", seam.Name + " cannot stitch an edge to itself.");
            }
            float[] distancesA = Distances(a);
            float[] distancesB = Distances(b);
            float lengthA = distancesA[^1];
            float lengthB = distancesB[^1];
            float mismatch = MathF.Abs(lengthA - lengthB) / MathF.Max(lengthA, lengthB);
            if (mismatch > seam.Ease + .001f)
            {
                GarmentAuthoring.Fail("garment-stitch-length-mismatch", seam.Name + $": edge lengths {lengthA:R}m and {lengthB:R}m require explicit ease >= {mismatch:R}.");
            }
            var samples = distancesA.Select(distance => distance / lengthA)
                .Concat(distancesB.Select(distance => seam.Reversed ? 1 - distance / lengthB : distance / lengthB))
                .Order().Aggregate(new List<float>(), (list, value) =>
                {
                    if (list.Count == 0 || value - list[^1] > 1e-5f)
                    {
                        list.Add(value);
                    }
                    return list;
                });
            for (int sample = 0; sample < samples.Count; sample++)
            {
                var weights = new SortedDictionary<int, float>();
                Bind(a, distancesA, samples[sample], 1, weights);
                Bind(b, distancesB, seam.Reversed ? 1 - samples[sample] : samples[sample], -1, weights);
                var nonzero = weights.Where(entry => MathF.Abs(entry.Value) > 1e-7f).ToArray();
                stitches.Add(new(seam.Name + "/" + sample, nonzero.Select(entry => entry.Key).ToImmutableArray(),
                    nonzero.Select(entry => entry.Value).ToImmutableArray(), seam.Compliance));
            }
        }
        var definition = new ClothDefinition3D(source.Name, rest.ToImmutableArray(), coordinates.ToImmutableArray(), indices.ToImmutableArray())
        {
            InitialPositions = initial.ToImmutableArray(),
            Stitches = stitches.ToImmutable(),
            Pins = pins.Order().ToImmutableArray(),
            Material = source.Fabric,
        };
        return new(source, definition.Compile(), compiledPanels);

        ImmutableArray<int> Boundary(string reference)
        {
            int separator = reference.IndexOf(".edges.", StringComparison.Ordinal);
            if (separator < 1)
            {
                GarmentAuthoring.Fail("garment-stitch-reference", "Use panel.edges.namedSpan: " + reference);
            }
            string panelName = reference[..separator];
            string edgeName = reference[(separator + 7)..];
            var panel = compiledPanels.SingleOrDefault(panel => panel.Name == panelName || panel.Identity == panelName);
            if (panel is null || !panel.Edges.ContainsKey(edgeName))
            {
                GarmentAuthoring.Fail("garment-stitch-edge-unknown", reference);
            }
            string canonical = panel!.Identity + ".edges." + edgeName;
            if (!usedEdges.Add(canonical))
            {
                GarmentAuthoring.Fail("garment-stitch-edge-reused", canonical + " already has a seam.");
            }
            return panel.Edges[edgeName];
        }
        float[] Distances(ImmutableArray<int> boundary)
        {
            float[] distances = new float[boundary.Length];
            for (int i = 1; i < boundary.Length; i++)
            {
                distances[i] = distances[i - 1] + Vector3.Distance(rest[boundary[i - 1]], rest[boundary[i]]);
            }
            return distances;
        }
    }

    private static void Bind(ImmutableArray<int> boundary, float[] distances, float fraction, float sign, SortedDictionary<int, float> weights)
    {
        float target = Math.Clamp(fraction, 0, 1) * distances[^1];
        int segment = 0;
        while (segment + 2 < distances.Length && distances[segment + 1] < target)
        {
            segment++;
        }
        float t = Math.Clamp((target - distances[segment]) / (distances[segment + 1] - distances[segment]), 0, 1);
        Add(boundary[segment], sign * (1 - t));
        Add(boundary[segment + 1], sign * t);
        void Add(int vertex, float weight)
        {
            weights.TryGetValue(vertex, out float existing);
            weights[vertex] = existing + weight;
        }
    }
}
