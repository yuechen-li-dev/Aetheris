using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

public sealed record SectionCageRimEvidence(
    string Section, int SampleCount, double MaximumSampledDeviationMm, double MaximumAllowedDeviationMm);

public sealed record SectionCageFit(
    int TransitionSampleCount, double InitialMaximumSampledTransitionDeviationMm,
    double MaximumSampledTransitionDeviationMm, SectionCageRimEvidence Rim);

internal sealed record SectionCageRim(
    string Section, string[] Vertices, BSplineSurfaceWithKnots[] Supports, double ToleranceMm)
{
    private const int SamplesPerEdge = 8;

    internal string[] SampleIds(int levels)
    {
        var samples = Vertices;
        for (var level = 0; level < levels; level++)
        {
            var children = new string[2 * samples.Length];
            for (var i = 0; i < samples.Length; i++)
            {
                children[2 * i] = samples[i];
                children[2 * i + 1] = SubdivisionSculptor.EdgeId(samples[i], samples[(i + 1) % samples.Length]);
            }
            samples = children;
        }
        return samples;
    }

    internal SectionCageRimEvidence Qualify(SubdivisionCage cage)
    {
        var points = cage.Vertices.ToDictionary(v => v.Id, v => v.Position, StringComparer.Ordinal);
        var creases = cage.Creases.ToHashSet(StringComparer.Ordinal);
        var ring = Vertices.Select(id => points[id]).ToArray();
        var count = ring.Length;
        // The measured curve is the cubic crease limit only while exactly two sharp
        // edges meet each rim vertex. A new sharp spoke would change the stencil.
        var rimSet = Vertices.ToHashSet(StringComparer.Ordinal);
        var expected = Vertices.Select((v, i) => SubdivisionSculptor.EdgeId(v, Vertices[(i + 1) % count])).ToHashSet();
        var actual = SubdivisionSculptor.Edges(cage)
            .Where(e => creases.Contains(e.Id) && (rimSet.Contains(e.A) || rimSet.Contains(e.B)))
            .Select(e => e.Id).ToHashSet();
        if (!actual.SetEquals(expected))
            throw new InvalidOperationException("mesh-section-rim-crease-changed: The source rim must retain exactly its two crease edges at each vertex.");

        var segments = count / Supports.Length;
        var maximum = 0d;
        for (var i = 0; i < count; i++)
        {
            var p = ring[i];
            var prior = ring[(i + count - 1) % count] - p;
            var next = ring[(i + 1) % count] - p;
            var after = ring[(i + 2) % count] - p;
            for (var s = 0; s < SamplesPerEdge; s++)
            {
                double t = (double)s / SamplesPerEdge;
                double t2 = t * t, t3 = t2 * t, inverse = 1 - t;
                var limit = p + (prior * (inverse * inverse * inverse)
                    + next * (-3 * t3 + 3 * t2 + 3 * t + 1) + after * t3) / 6;
                var reference = SectionChainSubdivision.Sample(Supports[i / segments], (i % segments + t) / segments, 0);
                maximum = Math.Max(maximum, (limit - reference).Length);
            }
        }
        if (!double.IsFinite(maximum) || maximum > ToleranceMm)
            throw new InvalidOperationException(FormattableString.Invariant(
                $"mesh-section-rim-deviation-exceeded: Sampled limit rim deviation {maximum:R}mm exceeds {ToleranceMm:R}mm at section '{Section}'."));
        return new(Section, count * SamplesPerEdge, maximum, ToleranceMm);
    }
}

internal static class SectionChainSubdivision
{
    private const int SpanCount = 4;
    private const int FitIterations = 16;

    internal static MeshSculptState Convert(BodyState source, string name, int segments, double maximumDeviationMm,
        int previewLevels, double? maximumRimDeviationMm, string? rimSection, IReadOnlyList<MeshSectionAnchor>? anchors,
        IReadOnlyList<MeshSectionMount>? mounts)
    {
        var native = source.NativeSectionChain;
        if (native is null || !ReferenceEquals(native.Body, source.Body) ||
            native.StartCap is not { } startCap || native.EndCap is not { } endCap ||
            native.Chain.TransitionPolicy != SectionTransitionPolicy.SmoothPolynomial ||
            native.Chain.Sections.Any(s => s.Profile.Spans.Count != SpanCount) ||
            native.Transitions.Any(t => t.Surfaces.Count != SpanCount))
            throw new InvalidOperationException(
                "mesh-section-cage-source-unsupported: Requires an unmodified, capped G1 SectionChain with four polynomial spans per section.");

        if (previewLevels < 1)
            throw new InvalidOperationException("mesh-preview-levels-invalid: SectionChain export requires at least one level of limit sampling.");
        if (segments < 2 || maximumRimDeviationMm is not { } rimBudget || !double.IsFinite(rimBudget) || rimBudget <= 0)
            throw new InvalidOperationException("mesh-section-cage-budget-invalid: Use 2..16 segments and a positive maximumRimDeviation.");
        var faceCount = (SpanCount * (long)native.Transitions.Count + 2) * segments * segments;
        if ((faceCount << (2 * previewLevels)) > SubdivisionSculptor.MaximumFaces)
            throw new InvalidOperationException("mesh-subdivision-budget-exceeded: SectionChain cage and preview exceed the face budget.");

        var supports = native.Transitions.Select(t => t.Surfaces.Select(s =>
        {
            if (!source.Body.TryGetFaceSurfaceGeometry(s.Face, out var support) ||
                support?.BSplineSurfaceWithKnots is not { IsRational: false } spline ||
                spline.KnotValuesU.Count != 2 || spline.KnotValuesV.Count != 2)
                throw new InvalidOperationException("mesh-section-cage-support-unsupported: Each transition face must be a single polynomial patch.");
            return spline;
        }).ToArray()).ToArray();

        var sections = native.Chain.Sections;
        int rimIndex = rimSection is null ? 0 : Array.FindIndex(sections.ToArray(), s => s.SectionId == rimSection);
        if (rimIndex < 0 || rimIndex >= supports.Length)
            throw new InvalidOperationException("mesh-section-rim-unresolved: The rim must name the first or an internal section of the exact chain.");
        var transitionRegions = sections.Zip(sections.Skip(1), (a, b) => $"{a.SectionId}_To_{b.SectionId}").ToArray();
        var regionNames = sections.Select(s => s.SectionId).Concat(transitionRegions).ToArray();
        if (regionNames.Distinct(StringComparer.Ordinal).Count() != regionNames.Length)
            throw new InvalidOperationException("mesh-section-cage-region-collision: Section names must produce distinct station and transition regions.");
        var ringCount = supports.Length * segments + 1;
        var ringSize = SpanCount * segments;
        var rings = new string[ringCount][];
        var vertices = new List<CageVertex>();
        var faces = new List<CageFace>();
        var centers = new List<(string Face, Point3D Target)>();
        for (var r = 0; r < ringCount; r++)
        {
            var transition = Math.Min(r / segments, supports.Length - 1);
            var step = r - transition * segments;
            var section = r % segments == 0 ? sections[r / segments] : null;
            var prefix = section?.SectionId ?? $"{sections[transition].SectionId}_{sections[transition + 1].SectionId}";
            var ringside = rings[r] = new string[ringSize];
            for (var c = 0; c < ringSize; c++)
            {
                int span = c / segments, u = c % segments;
                var spanId = (section ?? sections[transition]).Profile.Spans[span].SpanId;
                var id = section is null ? $"{prefix}_{spanId}_{u}_{step}" : $"{prefix}_{spanId}_{u}";
                ringside[c] = id;
                vertices.Add(new(id, Sample(supports[transition][span], (double)u / segments, (double)step / segments),
                    section is null ? [sections[transition].SectionId, sections[transition + 1].SectionId, spanId] : [section.SectionId, spanId]));
            }
        }

        for (var r = 0; r < ringCount - 1; r++)
        {
            var transition = r / segments;
            for (var c = 0; c < ringSize; c++)
            {
                var next = (c + 1) % ringSize;
                var span = c / segments;
                var id = $"Side_{rings[r][c]}";
                faces.Add(new(id, [rings[r][c], rings[r][next], rings[r + 1][next], rings[r + 1][c]],
                    [native.Transitions[transition].Surfaces[span].Face.Value]));
                centers.Add((id, Sample(supports[transition][span], (c % segments + 0.5) / segments, (r % segments + 0.5) / segments)));
            }
        }

        var ringPoints = new Dictionary<string, Point3D>(StringComparer.Ordinal);
        foreach (var vertex in vertices)
            if (!ringPoints.TryAdd(vertex.Id, vertex.Position))
                throw new InvalidOperationException($"mesh-section-cage-id-collision: The source names generate duplicate cage handle '{vertex.Id}'.");
        var anchorVertices = SubdivisionBrush.BindAnchors(anchors, sections, rings, segments);
        var mountBindings = SubdivisionSurfaceMount.Bind(mounts, sections, rings, segments);
        Cap(rings[0], sections[0].SectionId, startCap.Value, reverse: true);
        Cap(rings[^1], sections[^1].SectionId, endCap.Value, reverse: false);
        var rimRing = rings[rimIndex * segments];
        var rim = new SectionCageRim(sections[rimIndex].SectionId, rimRing, supports[rimIndex], rimBudget);
        var creases = RingCreases(rings[0]).Concat(RingCreases(rimRing)).Distinct(StringComparer.Ordinal).ToArray();
        var cage = new SubdivisionCage(vertices.ToArray(), faces.ToArray(), creases);
        SubdivisionSculptor.Evaluate(cage);
        var sideSamples = rings.SelectMany(ring => ring).ToHashSet(StringComparer.Ordinal);
        var initialDeviation = Measure(cage);
        var targets = vertices.Select(v => v.Position).ToArray();

        // Fit limit vertex samples against the exact transition and cap samples.
        // The rim ring has a closed cubic crease stencil, independent of the
        // interior update. Damping avoids oscillating against nearby controls.
        for (var iteration = 0; iteration < FitIterations; iteration++)
        {
            var samples = SubdivisionSculptor.LimitVertices(cage);
            cage = cage with
            {
                Vertices = cage.Vertices.Select((v, i) =>
                    v with { Position = v.Position + (targets[i] - samples[i].Position) * 0.75 }).ToArray()
            };
        }

        SubdivisionSculptor.Evaluate(cage);
        var fit = new SectionCageFit(sideSamples.Count + centers.Count, initialDeviation, Measure(cage), rim.Qualify(cage));
        if (!double.IsFinite(fit.MaximumSampledTransitionDeviationMm) || fit.MaximumSampledTransitionDeviationMm > maximumDeviationMm)
            throw new InvalidOperationException(FormattableString.Invariant(
                $"mesh-conversion-deviation-exceeded: Sampled transition limit deviation {fit.MaximumSampledTransitionDeviationMm:R}mm exceeds {maximumDeviationMm:R}mm. Increase segments within the preview budget or revise the source."));
        var recipe = new MeshConstruction(SubdivisionSculptor.Schema, source.ConstructionAuthority!, "SectionChain", segments, maximumDeviationMm, [])
        { PreviewLevels = previewLevels, MaximumRimDeviationMm = rimBudget, RimSection = rim.Section, Anchors = anchors is { Count: > 0 } ? anchors.ToArray() : null,
            Mounts = mounts is { Count: > 0 } ? mounts.ToArray() : null };
        var identity = FormattableString.Invariant(
            $"{source.StateId.Value}|{recipe.Schema}|{recipe.Cage}|{segments}|{maximumDeviationMm:R}|{previewLevels}|{rimBudget:R}|{rim.Section}|{SubdivisionSculptor.Fingerprint(cage)}");
        if (recipe.Anchors is not null)
            identity += "|anchors:" + System.Text.Json.JsonSerializer.Serialize(recipe.Anchors);
        if (recipe.Mounts is not null)
            identity += "|mounts:" + System.Text.Json.JsonSerializer.Serialize(recipe.Mounts);
        return new(SubdivisionSculptor.Hash(identity), source.StateId.Value, name, source.StateId.Value, cage, recipe,
            fit.MaximumSampledTransitionDeviationMm, "Sampled Catmull-Clark limit fit to corresponding exact transition surfaces.")
        {
            SectionFit = fit,
            VertexRegions = cage.Vertices.GroupBy(v => v.Parents.Count == 3 ? $"{v.Parents[0]}_To_{v.Parents[1]}" : v.Parents[0], StringComparer.Ordinal)
                .ToDictionary(region => region.Key, region => region.Select(v => v.Id).ToArray(), StringComparer.Ordinal),
            AnchorVertices = anchorVertices,
            MountBindings = mountBindings,
            SurfaceMounts = SubdivisionSurfaceMount.Measure(cage, previewLevels, mountBindings),
            SourceRim = rim
        };

        IEnumerable<string> RingCreases(string[] ring) =>
            ring.Select((id, i) => SubdivisionSculptor.EdgeId(id, ring[(i + 1) % ringSize]));

        double Measure(SubdivisionCage controls)
        {
            var refined = SubdivisionSculptor.Refine(controls);
            var samples = SubdivisionSculptor.LimitVertices(refined).ToDictionary(v => v.Id, v => v.Position, StringComparer.Ordinal);
            var maximum = vertices.Where(v => sideSamples.Contains(v.Id)).Max(v => (v.Position - samples[v.Id]).Length);
            foreach (var (face, target) in centers)
                maximum = Math.Max(maximum, (target - samples["f" + SubdivisionSculptor.Hash(face)[..20]]).Length);
            return maximum;
        }

        void Cap(string[] ring, string section, int faceId, bool reverse)
        {
            var grid = new string[segments + 1, segments + 1];
            for (var i = 0; i <= segments; i++)
            {
                grid[i, 0] = ring[i];
                grid[segments, i] = ring[segments + i];
                grid[i, segments] = ring[3 * segments - i];
                grid[0, i] = ring[(ringSize - i) % ringSize];
            }
            var origin = ringPoints[grid[0, 0]];
            for (var y = 1; y < segments; y++)
                for (var x = 1; x < segments; x++)
                {
                    var u = (double)x / segments;
                    var v = (double)y / segments;
                    // Coons grid spanning the actual four sampled cap sides.
                    var sides = (ringPoints[grid[x, 0]] - origin) * (1 - v) + (ringPoints[grid[x, segments]] - origin) * v
                        + (ringPoints[grid[0, y]] - origin) * (1 - u) + (ringPoints[grid[segments, y]] - origin) * u;
                    var corners = (ringPoints[grid[segments, 0]] - origin) * (u * (1 - v))
                        + (ringPoints[grid[0, segments]] - origin) * ((1 - u) * v)
                        + (ringPoints[grid[segments, segments]] - origin) * (u * v);
                    var id = grid[x, y] = $"Cap_{section}_{x}_{y}";
                    vertices.Add(new(id, origin + sides - corners, [section]));
                }
            for (var y = 0; y < segments; y++)
                for (var x = 0; x < segments; x++)
                {
                    string[] loop = [grid[x, y], grid[x + 1, y], grid[x + 1, y + 1], grid[x, y + 1]];
                    if (reverse) Array.Reverse(loop);
                    faces.Add(new($"CapFace_{section}_{x}_{y}", loop, [faceId]));
                }
        }
    }

    internal static Point3D Sample(BSplineSurfaceWithKnots surface, double u, double v) => surface.Evaluate(
        surface.DomainStartU + u * (surface.DomainEndU - surface.DomainStartU),
        surface.DomainStartV + v * (surface.DomainEndV - surface.DomainStartV));
}
