using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

public enum CutoutSide { Right, Left }
public readonly record struct ArchPoint(double X, double Z);
public sealed record ArchCubic(ArchPoint Start, ArchPoint Control1, ArchPoint Control2, ArchPoint End);
public sealed record CutoutMirror(string Feature, string Across);
public sealed record CutoutCageArch(string Id, string Profile, IReadOnlyList<ArchCubic> Spans, CutoutSide Side,
    double OuterMm, double ReturnDepthMm, double BlendToMm, CutoutMirror? Mirror = null) : MeshSculptOperation(Id);
public sealed record MeshCutoutEvidence(string Feature, string Profile, string? MirroredFeature, string? Across,
    double SillMm, double ReturnDepthMm, double InboardLimitMm, int MovedControls,
    IReadOnlyDictionary<string, int> LipControls, MeshComparisonEvidence Movement);

internal static class SubdivisionCutout
{
    private const double Tolerance = 1e-6;

    internal static (SubdivisionCage Cage, IReadOnlyDictionary<string, string[]> Regions, MeshCutoutEvidence Evidence)
        Apply(MeshSculptState state, CutoutCageArch cutout, IReadOnlySet<string> pinned)
    {
        if (cutout.Spans is not { Count: >= 1 and <= 8 } || !Enum.IsDefined(cutout.Side)
            || !double.IsFinite(cutout.OuterMm) || !double.IsFinite(cutout.ReturnDepthMm) || !double.IsFinite(cutout.BlendToMm)
            || cutout.ReturnDepthMm <= 0 || cutout.OuterMm <= cutout.ReturnDepthMm)
            throw new InvalidOperationException("mesh-cutout-invalid: Use 1..8 cubic spans and finite dimensions with 0 < returnDepth < outer.");

        var first = cutout.Spans[0].Start;
        var last = cutout.Spans[^1].End;
        var previous = first;
        foreach (var span in cutout.Spans)
        {
            var controls = new[] { span.Start, span.Control1, span.Control2, span.End };
            if (controls.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Z) || p.Z < first.Z - Tolerance || p.Z >= cutout.BlendToMm)
                || Math.Abs(previous.X - span.Start.X) > Tolerance || Math.Abs(previous.Z - span.Start.Z) > Tolerance
                || !(span.Start.X < span.End.X && span.Start.X <= span.Control1.X
                    && span.Control1.X <= span.Control2.X && span.Control2.X <= span.End.X))
                throw new InvalidOperationException("mesh-cutout-profile-invalid: The cubic arch must be continuous, ordered along X, and below blendTo.");
            previous = span.End;
        }
        if (Math.Abs(first.Z - last.Z) > Tolerance)
            throw new InvalidOperationException("mesh-cutout-profile-invalid: The arch must start and end on one horizontal sill.");

        var sill = first.Z;
        var inner = cutout.OuterMm - cutout.ReturnDepthMm;
        var shoulder = cutout.BlendToMm;
        var returnWidth = cutout.ReturnDepthMm / 4;
        var primarySign = cutout.Side == CutoutSide.Right ? -1 : 1;
        var regions = state.VertexRegions!.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var source = state.Cage.Vertices.ToDictionary(v => v.Id, v => v.Position, StringComparer.Ordinal);
        var lip = new Dictionary<string, List<string>>(StringComparer.Ordinal) { [cutout.Id] = [] };
        if (cutout.Mirror is { } mirror && !lip.TryAdd(mirror.Feature, []))
            throw new InvalidOperationException("mesh-cutout-region-duplicate: Give the two lips distinct names.");
        if (lip.Keys.Any(regions.ContainsKey))
            throw new InvalidOperationException("mesh-cutout-region-duplicate: An existing cage region already uses the lip name.");

        var vertices = state.Cage.Vertices.Select(vertex =>
        {
            var p = vertex.Position;
            var side = p.Y * primarySign > 0 ? cutout.Id : cutout.Mirror?.Feature;
            if (side is null || Math.Abs(p.Y) <= inner || p.Z >= shoulder || p.X <= first.X || p.X >= last.X) return vertex;
            var lateral = Math.Clamp((Math.Abs(p.Y) - inner) / returnWidth, 0, 1);
            lateral *= lateral * (3 - 2 * lateral);
            var vertical = Math.Clamp((shoulder - p.Z) / (shoulder - sill), 0, 1);
            // A monotone vertical remap lifts the sole into the open well.
            // Adjacent rows form the return into the unmodified inboard floor.
            var lift = lateral * vertical * (HeightAt(cutout.Spans, p.X) - sill);
            if (lift <= Tolerance) return vertex;
            if (pinned.Contains(vertex.Id))
                throw new InvalidOperationException("mesh-cutout-protected: The arch intersects the qualified rim or a protected region.");
            // Include the shoulder of the opening and the neighboring sole rows.
            if (Math.Abs(p.Y) >= cutout.OuterMm - returnWidth && p.Z <= sill + (shoulder - sill) * 0.15)
                lip[side].Add(vertex.Id);
            return vertex with { Position = new(p.X, p.Y, p.Z + lift) };
        }).ToArray();
        if (lip.Values.Any(ids => ids.Count < 4))
            throw new InvalidOperationException("mesh-cutout-unsupported-band: The guide must meet a populated lower side and sole band on each requested side.");
        foreach (var (name, ids) in lip) regions.Add(name, ids.ToArray());

        var output = state.Cage with { Vertices = vertices };
        var after = vertices.ToDictionary(v => v.Id, v => v.Position, StringComparer.Ordinal);
        // Side triangles can legitimately rotate through a large angle in a
        // deep cut. Reject coincident or reversed vertical edges separately
        // from the ordinary closed-mesh and crease admission checks.
        foreach (var edge in state.Edges)
        {
            var a = source[edge.A]; var b = source[edge.B];
            if (Math.Abs(a.X - b.X) <= Tolerance && Math.Abs(a.Y - b.Y) <= Tolerance
                && (a.Z - b.Z) * (after[edge.A].Z - after[edge.B].Z) <= 0)
                throw new InvalidOperationException("mesh-cutout-foldover: The remap reverses a vertical edge.");
        }

        var movement = SubdivisionComparison.Measure(state, state with { Cage = output });
        return (output, regions, new(cutout.Id, cutout.Profile, cutout.Mirror?.Feature, cutout.Mirror?.Across,
            sill, cutout.ReturnDepthMm, inner, vertices.Count(v => v.Position != source[v.Id]),
            lip.ToDictionary(pair => pair.Key, pair => pair.Value.Count, StringComparer.Ordinal), movement));
    }

    private static double HeightAt(IReadOnlyList<ArchCubic> spans, double x)
    {
        var span = spans.First(s => x <= s.End.X);
        double low = 0, high = 1;
        for (var i = 0; i < 40; i++)
        {
            var t = (low + high) / 2;
            if (Evaluate(span.Start.X, span.Control1.X, span.Control2.X, span.End.X, t) < x) low = t;
            else high = t;
        }
        return Evaluate(span.Start.Z, span.Control1.Z, span.Control2.Z, span.End.Z, (low + high) / 2);

        static double Evaluate(double a, double b, double c, double d, double t)
        {
            var s = 1 - t;
            return s * s * s * a + 3 * s * s * t * b + 3 * s * t * t * c + t * t * t * d;
        }
    }
}
