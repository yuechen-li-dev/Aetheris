using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

public sealed record MeshSectionMount(string Name, string Section, string Span, double At);
public sealed record MeshSurfaceMount(string Name, string Section, string Span, double At, Point3D PositionMm,
    Vector3D Longitudinal, Vector3D Crosswise, Vector3D Normal);

internal sealed record SectionMountBinding(MeshSectionMount Definition, string[] Before, string[] Across, string[] After);

internal static class SubdivisionSurfaceMount
{
    internal static SectionMountBinding[] Bind(IReadOnlyList<MeshSectionMount>? definitions, IReadOnlyList<Section> sections,
        string[][] rings, int segments)
    {
        if (definitions is not { Count: > 0 }) return [];
        if (definitions.Count > 64)
            throw new InvalidOperationException("mesh-mount-budget-exceeded: At most 64 mounts are allowed per cage.");
        var stations = sections.Select((section, index) => (section, index))
            .ToDictionary(item => item.section.SectionId, item => item.index, StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<SectionMountBinding>(definitions.Count);
        foreach (var mount in definitions)
        {
            if (mount is null || string.IsNullOrWhiteSpace(mount.Name) || !names.Add(mount.Name))
                throw new InvalidOperationException("mesh-mount-invalid: Give each mount a unique name.");
            if (!double.IsFinite(mount.At) || mount.At <= 0 || mount.At >= 1)
                throw new InvalidOperationException("mesh-mount-parameter-invalid: A mount requires a dimensionless at strictly between 0 and 1.");
            if (!stations.TryGetValue(mount.Section, out var station) || station == 0 || station == sections.Count - 1)
                throw new InvalidOperationException($"mesh-mount-section-unresolved: '{mount.Name}' needs a named interior section.");
            var spans = sections[station].Profile.Spans;
            var span = Enumerable.Range(0, spans.Count).FirstOrDefault(i => spans[i].SpanId == mount.Span, -1);
            if (span < 0)
                throw new InvalidOperationException($"mesh-mount-span-unresolved: '{mount.Name}' needs a span on section '{mount.Section}'.");

            var row = station * segments;
            result.Add(new(mount, Segment(row - 1), Segment(row), Segment(row + 1)));

            string[] Segment(int index)
            {
                var ring = rings[index];
                return Enumerable.Range(0, segments + 1).Select(i => ring[(span * segments + i) % ring.Length]).ToArray();
            }
        }

        return result.ToArray();
    }

    internal static IReadOnlyList<MeshSurfaceMount>? Measure(SubdivisionCage cage, int previewLevels,
        IReadOnlyList<SectionMountBinding> bindings)
    {
        if (bindings.Count == 0) return null;
        var points = SubdivisionSculptor.SamplePreview(cage, previewLevels).Vertices.ToDictionary(v => v.Id, v => v.Position, StringComparer.Ordinal);
        var creases = cage.Creases.ToHashSet(StringComparer.Ordinal);
        return bindings.Select(binding =>
        {
            var mount = binding.Definition;
            var rail = binding.Across;
            if (rail.Zip(rail.Skip(1)).Any(edge => creases.Contains(SubdivisionSculptor.EdgeId(edge.First, edge.Second))))
                throw new InvalidOperationException($"mesh-mount-crease: '{mount.Name}' requires a smooth section span.");

            var position = At(rail, out var crosswise);
            var along = At(binding.After, out _) - At(binding.Before, out _);
            if (!along.TryNormalize(out var x) || !crosswise.Cross(x).TryNormalize(out var z))
                throw new InvalidOperationException($"mesh-mount-frame-degenerate: '{mount.Name}' cannot define a right-handed frame.");
            return new MeshSurfaceMount(mount.Name, mount.Section, mount.Span, mount.At, position, x, z.Cross(x), z);

            Point3D At(string[] row, out Vector3D tangent)
            {
                for (var level = 0; level < previewLevels; level++)
                {
                    var refined = new string[2 * row.Length - 1];
                    for (var i = 0; i + 1 < row.Length; i++)
                    {
                        refined[2 * i] = row[i];
                        refined[2 * i + 1] = SubdivisionSculptor.EdgeId(row[i], row[i + 1]);
                    }
                    refined[^1] = row[^1];
                    row = refined;
                }

                var t = mount.At * (row.Length - 1);
                var i0 = Math.Min((int)t, row.Length - 2);
                if (!points.TryGetValue(row[i0], out var a) || !points.TryGetValue(row[i0 + 1], out var b))
                    throw new InvalidOperationException($"mesh-mount-support-removed: The edit removed the surface support of '{mount.Name}'.");
                tangent = b - a;
                return a + tangent * (t - i0);
            }
        }).ToArray();
    }
}
