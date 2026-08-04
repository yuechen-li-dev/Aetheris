using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Brep.Prismatic;

internal enum PrismaticPlannedEdgeKind { Section, Transition }
internal enum PrismaticPlannedFaceKind { BottomCap, TopCap, StableSide, Transition }
internal readonly record struct PrismaticPlannedEdgeUse(int EdgeIndex, bool Reversed);
internal sealed record PrismaticPlannedVertex(string Id, int SectionIndex, int ProfileVertexIndex, Point3D Point);
internal sealed record PrismaticPlannedEdge(string Id, PrismaticPlannedEdgeKind Kind, int StartVertexIndex, int EndVertexIndex, int? SectionIndex, int? IntervalIndex, int ProfileEdgeIndex);
internal sealed record PrismaticPlannedFace(string Id, PrismaticPlannedFaceKind Kind, IReadOnlyList<PrismaticPlannedEdgeUse> Boundary, int? IntervalIndex, int? ProfileEdgeIndex);

/// <summary>Immutable topology authority consumed by both AIR BRepPlan reporting and BRep materialization.</summary>
internal sealed record PrismaticSectionTransitionTopologyPlan(
    PrismaticSectionTransitionRequest Construction,
    IReadOnlyList<PrismaticPlannedVertex> Vertices,
    IReadOnlyList<PrismaticPlannedEdge> Edges,
    IReadOnlyList<PrismaticPlannedFace> Faces,
    string SplitPolicy,
    string DeterministicSignature)
{
    public int ExpectedLoopCount => Faces.Count;
    public int ExpectedCoedgeCount => Faces.Sum(face => face.Boundary.Count);
}

internal static class PrismaticSectionTransitionTopologyPlanner
{
    public const string PreserveSectionSplits = "preserve-section-splits";

    public static PrismaticSectionTransitionTopologyPlan Create(PrismaticSectionTransitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sections = request.Sections;
        if (sections.Count < 2 || sections.Select(s => s.OuterLoop.Count).Distinct().Count() != 1)
            throw new ArgumentException("A topology plan requires at least two equal-cardinality sections.", nameof(request));

        var n = sections[0].OuterLoop.Count;
        var vertices = new List<PrismaticPlannedVertex>(sections.Count * n);
        for (var s = 0; s < sections.Count; s++)
        for (var i = 0; i < n; i++)
        {
            var p = sections[s].OuterLoop[i];
            vertices.Add(new($"v:s{s}:{i}", s, i, new Point3D(p.X, p.Y, sections[s].Z)));
        }

        var edges = new List<PrismaticPlannedEdge>();
        var sectionEdges = new int[sections.Count, n];
        for (var s = 0; s < sections.Count; s++)
        for (var i = 0; i < n; i++)
        {
            sectionEdges[s, i] = edges.Count;
            edges.Add(new($"e:section:{s}:{i}", PrismaticPlannedEdgeKind.Section, (s * n) + i, (s * n) + ((i + 1) % n), s, null, i));
        }

        var transitionEdges = new int[sections.Count - 1, n];
        for (var s = 0; s < sections.Count - 1; s++)
        for (var i = 0; i < n; i++)
        {
            transitionEdges[s, i] = edges.Count;
            edges.Add(new($"e:transition:{s}:{i}", PrismaticPlannedEdgeKind.Transition, (s * n) + i, ((s + 1) * n) + i, null, s, i));
        }

        var faces = new List<PrismaticPlannedFace>
        {
            new("f:cap:bottom", PrismaticPlannedFaceKind.BottomCap, Enumerable.Range(0, n).Select(i => new PrismaticPlannedEdgeUse(sectionEdges[0, i], false)).ToArray(), null, null),
            // Reversing an oriented loop requires reversing its coedge order too.
            new("f:cap:top", PrismaticPlannedFaceKind.TopCap, Enumerable.Range(0, n).Reverse().Select(i => new PrismaticPlannedEdgeUse(sectionEdges[sections.Count - 1, i], true)).ToArray(), null, null),
        };
        for (var s = 0; s < sections.Count - 1; s++)
        for (var i = 0; i < n; i++)
        {
            var next = (i + 1) % n;
            var kind = sections[s].OuterLoop[i] == sections[s + 1].OuterLoop[i] && sections[s].OuterLoop[next] == sections[s + 1].OuterLoop[next]
                ? PrismaticPlannedFaceKind.StableSide
                : PrismaticPlannedFaceKind.Transition;
            faces.Add(new($"f:{(kind == PrismaticPlannedFaceKind.StableSide ? "side" : "transition")}:interval{s}:edge{i}", kind,
                [new(sectionEdges[s, i], false), new(transitionEdges[s, next], false), new(sectionEdges[s + 1, i], true), new(transitionEdges[s, i], true)], s, i));
        }

        var signature = string.Join("|", vertices.Select(v => FormattableString.Invariant($"{v.Id}:{v.Point.X:R},{v.Point.Y:R},{v.Point.Z:R}"))
            .Concat(edges.Select(e => $"{e.Id}:{e.StartVertexIndex}>{e.EndVertexIndex}"))
            .Concat(faces.Select(f => $"{f.Id}:{string.Join(',', f.Boundary.Select(u => $"{u.EdgeIndex}:{(u.Reversed ? 'R' : 'F')}"))}")));
        return new(request, vertices, edges, faces, PreserveSectionSplits, signature);
    }
}
