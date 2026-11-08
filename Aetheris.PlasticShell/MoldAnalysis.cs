using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.PlasticShell;

/// <summary>
/// Single-direction moldability of a built part against its declared pull.
/// <list type="bullet">
/// <item>Side and draft come from each face's outward normals against the pull: exact for planes, and for cylinders
/// and cones whose axis is the pull; otherwise the extremes over the face's display mesh.</item>
/// <item>A face is an undercut where steel cannot leave it along its side's pull: rays from its mesh samples along
/// +pull (or −pull) that hit the part. Faces parallel to the pull take whichever direction is free, and the parting
/// plane's side when both are.</item>
/// <item>The parting line is every edge between a face of the +pull half and one of the −pull half.</item>
/// </list>
/// </summary>
public static class MoldAnalysis
{
    private const double ParallelTolerance = 1.75e-4; // sin(0.01°): a face this close to the pull is vertical
    private const int MaximumSamplesPerFace = 64;

    public static MoldReport? Analyze(BrepBody body, MoldIntent intent, List<MoldDiagnostic> diagnostics)
    {
        // Rays start a few chord tolerances off the surface: neighbouring faces' meshes may sample a shared curved edge
        // differently, and their chords overhang each other by up to that much. A step smaller than this offset is
        // below what the check can see.
        var scale = BodyDiagonal(body);
        var chord = double.Clamp(scale / 4000d, 0.002d, 0.05d);
        var mesh = BrepDisplayTessellator.Tessellate(body, DisplayTessellationOptions.Default with { ChordTolerance = chord, AngularToleranceRadians = double.Pi / 36d, MaximumSegments = 1024 });
        if (!mesh.IsSuccess)
        {
            diagnostics.Add(new(MoldDiagnosticCodes.PartBuildFailed, PlasticDiagnosticSeverity.Error, "The part could not be meshed for the pull analysis.", intent.PartName));
            return null;
        }
        var patches = mesh.Value.FacePatches.Where(p => p.TriangleIndices.Count > 0).ToArray();
        var pull = intent.Pull.ToVector();
        var (min, max) = Bounds(patches);
        var size = (max - min).Length;
        var eps = 3d * chord;

        // Display normals point out of the material; a mesh whose enclosed volume comes out negative is flipped.
        var orientation = SignedVolume(patches) < 0 ? -1d : 1d;
        var rays = new RayGrid(patches, pull);

        var cosmetic = new HashSet<int>();
        foreach (var selector in intent.Cosmetic.Where(IsAxisSelector))
        {
            var faces = AxisFaces(body, selector);
            if (faces.Count == 0) diagnostics.Add(new(MoldDiagnosticCodes.SelectorUnresolved, PlasticDiagnosticSeverity.Error, $"{selector} resolves to no planar face of the part: the part's extreme in that direction is not planar.", intent.MoldName));
            cosmetic.UnionWith(faces.Select(f => f.Value));
        }
        var textured = new HashSet<int>();
        foreach (var selector in (intent.Texture?.Selectors ?? []).Where(IsAxisSelector))
        {
            var faces = AxisFaces(body, selector);
            if (faces.Count == 0) diagnostics.Add(new(MoldDiagnosticCodes.SelectorUnresolved, PlasticDiagnosticSeverity.Error, $"texture {selector} resolves to no planar face of the part.", intent.MoldName));
            textured.UnionWith(faces.Select(f => f.Value));
        }

        double? partingHeight = null;
        if (intent.Parting is { } parting)
        {
            if (double.IsNaN(parting.Origin.X))
            {
                var faces = AxisFaces(body, parting.Source);
                var planes = faces.Select(f => body.TryGetFaceSurfaceGeometry(f, out var s) ? s?.Plane : null).OfType<Aetheris.Kernel.Core.Geometry.Surfaces.PlaneSurface>().ToArray();
                if (planes.Length == 0) diagnostics.Add(new(MoldDiagnosticCodes.SelectorUnresolved, PlasticDiagnosticSeverity.Error, $"parting at {parting.Source} resolves to no planar face of the part.", intent.MoldName));
                else if (double.Abs(double.Abs(planes[0].Normal.ToVector().Dot(pull)) - 1d) > 1e-9)
                    diagnostics.Add(new(MoldDiagnosticCodes.PartingInvalid, PlasticDiagnosticSeverity.Error, $"parting at {parting.Source}: that face is not perpendicular to the pull, so a plane through it cannot be the parting plane.", intent.MoldName));
                else partingHeight = (planes[0].Origin - Point3D.Origin).Dot(pull);
            }
            else partingHeight = (parting.Origin - Point3D.Origin).Dot(pull);
        }
        if (diagnostics.Any(d => d.Severity == PlasticDiagnosticSeverity.Error)) return null;

        var evidence = new List<MoldFaceEvidence>();
        var sideOf = new Dictionary<int, MoldFaceSide>();
        foreach (var patch in patches)
        {
            var face = patch.FaceId.Value;
            body.TryGetFaceSurfaceGeometry(patch.FaceId, out var surface);
            var aligned = body.Bindings.TryGetFaceBinding(patch.FaceId, out var binding) && binding.Orientation.IsAlignedWithSurface;
            var normals = patch.Normals.Select(n => n * orientation).ToArray();
            var dots = normals.Select(n => n.Dot(pull) / double.Max(n.Length, 1e-300)).ToArray();
            var (dMin, dMax) = (dots.Min(), dots.Max());
            var strength = MoldEvidenceStrength.SampledConservative;
            switch (surface)
            {
                case { Plane: { } plane }:
                    dMin = dMax = plane.Normal.ToVector().Dot(pull) * (aligned ? 1d : -1d);
                    strength = MoldEvidenceStrength.ExactAnalytic;
                    break;
                case { Cylinder: { } cylinder } when double.Abs(double.Abs(cylinder.Axis.ToVector().Dot(pull)) - 1d) < 1e-12:
                    dMin = dMax = 0d;
                    strength = MoldEvidenceStrength.ExactAnalytic;
                    break;
                case { Cone: { } cone } when double.Abs(double.Abs(cone.Axis.ToVector().Dot(pull)) - 1d) < 1e-12:
                    dMin = dMax = double.Sign(dots.Average()) * double.Sin(cone.SemiAngleRadians);
                    strength = MoldEvidenceStrength.ExactAnalytic;
                    break;
            }
            var side = dMin >= -ParallelTolerance && dMax <= ParallelTolerance ? MoldFaceSide.Vertical
                : dMin >= -ParallelTolerance ? MoldFaceSide.Positive
                : dMax <= ParallelTolerance ? MoldFaceSide.Negative
                : MoldFaceSide.Straddling;
            var draft = side switch
            {
                MoldFaceSide.Positive => double.Asin(double.Clamp(dMin, 0d, 1d)),
                MoldFaceSide.Negative => double.Asin(double.Clamp(-dMax, 0d, 1d)),
                _ => 0d,
            } * 180d / double.Pi;
            var required = intent.Policy.MinimumDraftDegrees + (textured.Contains(face) ? intent.Policy.TextureDraftDegreesPerMillimetre * intent.Texture!.Depth : 0d);

            // Release: rays from the face's mesh samples along its half's pull must leave the part.
            var triangles = Triangles(patch, orientation);
            var stride = int.Max(1, triangles.Length / MaximumSamplesPerFace);
            var samples = 0; var blocked = 0; var freePositive = 0; var freeNegative = 0;
            for (var t = 0; t < triangles.Length; t += stride)
            {
                var (centroid, normal, _) = triangles[t];
                var start = centroid + normal * eps;
                var d = normal.Dot(pull);
                samples++;
                if (double.Abs(d) <= ParallelTolerance)
                {
                    var up = !rays.Hits(start, pull, eps); var down = !rays.Hits(start, -pull, eps);
                    if (up) freePositive++;
                    if (down) freeNegative++;
                    if (!up && !down) blocked++;
                }
                else if (rays.Hits(start, pull * double.Sign(d), eps)) blocked++;
            }
            var area = triangles.Sum(x => x.Area);
            var representative = triangles.Length == 0 ? Point3D.Origin : Weighted(triangles);
            if (side == MoldFaceSide.Vertical)
            {
                // A wall parallel to the pull goes with the half it can leave towards; when both are open, with the
                // half on its side of the parting plane.
                var height = (representative - Point3D.Origin).Dot(pull);
                sideOf[face] = freePositive > 0 && freeNegative == 0 ? MoldFaceSide.Positive
                    : freeNegative > 0 && freePositive == 0 ? MoldFaceSide.Negative
                    : partingHeight is { } h ? (height > h ? MoldFaceSide.Positive : MoldFaceSide.Negative)
                    : MoldFaceSide.Vertical;
            }
            else sideOf[face] = side;

            evidence.Add(new(face, surface?.Kind.ToString() ?? "Unknown", side, draft, required, draft + 1e-9 >= required,
                samples == 0 ? 0d : (double)blocked / samples, samples, cosmetic.Contains(face), representative, area, strength));
        }

        // side(+pull) and side(-pull) name the faces a half forms, known only now.
        HashSet<int> Side(IEnumerable<string> selectors) => selectors.Where(x => !IsAxisSelector(x))
            .SelectMany(x => sideOf.Where(p => p.Value == (x.Contains('+') ? MoldFaceSide.Positive : MoldFaceSide.Negative)).Select(p => p.Key)).ToHashSet();
        cosmetic.UnionWith(Side(intent.Cosmetic));
        textured.UnionWith(Side(intent.Texture?.Selectors ?? []));
        evidence = evidence.Select(f =>
        {
            var required = intent.Policy.MinimumDraftDegrees + (textured.Contains(f.FaceId) ? intent.Policy.TextureDraftDegreesPerMillimetre * intent.Texture!.Depth : 0d);
            return f with { RequiredDraftDegrees = required, DraftSatisfied = f.MinimumDraftDegrees + 1e-9 >= required, Cosmetic = cosmetic.Contains(f.FaceId) };
        }).ToList();

        // Parting line: edges between the two halves.
        var faceOfLoop = body.Topology.Faces.SelectMany(f => f.LoopIds.Select(l => (Loop: l, Face: f.Id.Value))).ToDictionary(x => x.Loop, x => x.Face);
        var edgeFaces = body.Topology.Coedges.GroupBy(c => c.EdgeId).ToDictionary(g => g.Key, g => g.Select(c => faceOfLoop[c.LoopId]).Distinct().ToArray());
        var polylines = mesh.Value.EdgePolylines.ToDictionary(e => e.EdgeId);
        var partingEdges = edgeFaces.Where(e => e.Value.Length == 2
                && sideOf.GetValueOrDefault(e.Value[0]) is var a && sideOf.GetValueOrDefault(e.Value[1]) is var b
                && ((a == MoldFaceSide.Positive && b == MoldFaceSide.Negative) || (a == MoldFaceSide.Negative && b == MoldFaceSide.Positive)))
            .Select(e => e.Key).OrderBy(e => e.Value).ToArray();
        var heights = partingEdges.SelectMany(e => polylines.TryGetValue(e, out var line) ? line.Points : []).Select(p => (p - Point3D.Origin).Dot(pull)).ToArray();
        var (hMin, hMax) = heights.Length == 0 ? (double.NaN, double.NaN) : (heights.Min(), heights.Max());
        var onPlane = partingHeight is not { } ph || heights.All(h => double.Abs(h - ph) <= 1e-6 * size + 1e-6);
        var cosmeticTouched = partingEdges.SelectMany(e => edgeFaces[e]).Where(cosmetic.Contains).Distinct().Order().ToArray();
        var partingLine = new MoldPartingLineEvidence(partingEdges.Select(e => e.Value).ToArray(), hMin, hMax, onPlane, cosmeticTouched);

        // Findings, scaled by stage.
        foreach (var f in evidence.Where(f => f.Side == MoldFaceSide.Straddling))
            diagnostics.Add(new(MoldDiagnosticCodes.StraddlingFace, Severity(intent.Stage, hard: true),
                $"Face {f.FaceId} ({f.Surface} near {Format(f.Representative)}) faces both ways along the pull; the parting line has to split it.", $"face:{f.FaceId}"));
        foreach (var f in evidence.Where(f => f.UndercutFraction > 0))
            diagnostics.Add(new(f.Cosmetic ? MoldDiagnosticCodes.UndercutOnCosmetic : MoldDiagnosticCodes.Undercut, Severity(intent.Stage, hard: true),
                $"Face {f.FaceId} ({f.Surface} near {Format(f.Representative)}) cannot release along the pull: {f.UndercutFraction:P0} of its samples are blocked by the part. It needs a side action, a lifter, or a different pull.", $"face:{f.FaceId}"));
        // Draft findings come as one line per required angle: a part at its first review typically has every wall
        // vertical, and the report lists the faces individually.
        foreach (var group in evidence.Where(f => !f.DraftSatisfied && f.Side != MoldFaceSide.Straddling).GroupBy(f => f.RequiredDraftDegrees))
        {
            var faces = group.ToArray();
            diagnostics.Add(new(MoldDiagnosticCodes.DraftInsufficient, Severity(intent.Stage, hard: false),
                $"{faces.Length} face(s) have less than the {group.Key:0.##}° of draft {intent.Policy.Name} needs: "
                + string.Join("; ", faces.Select(f => $"{f.FaceId} ({f.Surface} near {Format(f.Representative)}, {f.MinimumDraftDegrees:0.##}°)")) + ".",
                "faces:" + string.Join(",", faces.Select(f => f.FaceId))));
        }
        if (!onPlane)
            diagnostics.Add(new(MoldDiagnosticCodes.PartingLineOffPlane, Severity(intent.Stage, hard: false),
                $"The parting line runs from {hMin:0.###} to {hMax:0.###} mm along the pull, off the declared parting plane at {partingHeight:0.###} mm; the part needs a stepped parting surface or a different plane.", intent.MoldName));
        if (cosmeticTouched.Length > 0)
            diagnostics.Add(new(MoldDiagnosticCodes.PartingLineOnCosmetic, Severity(intent.Stage, hard: false),
                $"The parting line runs along cosmetic face(s) {string.Join(", ", cosmeticTouched)}; it leaves a witness line there.", intent.MoldName));

        var projected = patches.Where(p => sideOf.GetValueOrDefault(p.FaceId.Value) == MoldFaceSide.Positive)
            .SelectMany(p => Triangles(p, orientation)).Sum(t => t.Area * double.Abs(t.Normal.Dot(pull)));
        var volume = BrepMassProperties.Evaluate(body).AbsoluteVolume;
        var summary = new MoldSummary(evidence.Count,
            evidence.Count(f => f.Side == MoldFaceSide.Positive), evidence.Count(f => f.Side == MoldFaceSide.Negative),
            evidence.Count(f => f.Side == MoldFaceSide.Vertical), evidence.Count(f => f.Side == MoldFaceSide.Straddling),
            evidence.Count(f => !f.DraftSatisfied), evidence.Count(f => f.UndercutFraction > 0),
            projected, volume, volume * intent.Policy.Density / 1000d);
        return new(intent, evidence, partingLine, summary,
            "Face normals against the pull (exact on planes, and on cylinders and cones about the pull; mesh extremes otherwise); release by casting the display mesh's samples along the pull against the part; parting line from face adjacency. Undercuts between samples are not proven absent.");
    }

    private static PlasticDiagnosticSeverity Severity(MoldStage stage, bool hard) => stage switch
    {
        MoldStage.Tooling => PlasticDiagnosticSeverity.Error,
        MoldStage.Prototype when hard => PlasticDiagnosticSeverity.Error,
        _ => PlasticDiagnosticSeverity.Warning,
    };

    private static string Format(Point3D p) => $"({p.X:0.##}, {p.Y:0.##}, {p.Z:0.##})";

    private static bool IsAxisSelector(string selector) => selector.StartsWith("face(", StringComparison.Ordinal);

    /// <summary>The faces <c>face(±X|Y|Z)</c> names: planar faces lying in the part's extreme plane in that direction.</summary>
    internal static IReadOnlyList<FaceId> AxisFaces(BrepBody body, string selector)
    {
        var token = selector.Replace(" ", "")[5..^1];
        var s = token[0] == '-' ? -1d : 1d;
        var axis = token[1] switch { 'X' => new Vector3D(s, 0, 0), 'Y' => new Vector3D(0, s, 0), _ => new Vector3D(0, 0, s) };
        var planes = body.Topology.Faces
            .Select(face => (face.Id, Plane: body.TryGetFaceSurfaceGeometry(face.Id, out var surface) ? surface?.Plane : null,
                Aligned: body.Bindings.TryGetFaceBinding(face.Id, out var binding) && binding.Orientation.IsAlignedWithSurface))
            .Where(f => f.Plane is { } plane && plane.Normal.ToVector().Dot(axis) * (f.Aligned ? 1d : -1d) > 1 - 1e-8)
            .Select(f => (f.Id, Offset: (f.Plane!.Value.Origin - Point3D.Origin).Dot(axis))).ToArray();
        if (planes.Length == 0) return [];
        var extreme = body.Topology.Vertices.Select(v => body.TryGetVertexPoint(v.Id, out var p) ? (p - Point3D.Origin).Dot(axis) : double.NegativeInfinity).Max();
        var support = planes.Max(p => p.Offset);
        return support < extreme - 1e-7 ? [] : planes.Where(p => double.Abs(p.Offset - support) < 1e-7).Select(p => p.Id).ToArray();
    }

    private static (Point3D Centroid, Vector3D Normal, double Area)[] Triangles(DisplayFaceMeshPatch patch, double orientation)
    {
        var result = new (Point3D, Vector3D, double)[patch.TriangleIndices.Count / 3];
        for (var i = 0; i < result.Length; i++)
        {
            int a = patch.TriangleIndices[3 * i], b = patch.TriangleIndices[3 * i + 1], c = patch.TriangleIndices[3 * i + 2];
            var (pa, pb, pc) = (patch.Positions[a], patch.Positions[b], patch.Positions[c]);
            var cross = (pb - pa).Cross(pc - pa);
            var n = (patch.Normals[a] + patch.Normals[b] + patch.Normals[c]) * orientation;
            var length = n.Length;
            result[i] = (new Point3D((pa.X + pb.X + pc.X) / 3, (pa.Y + pb.Y + pc.Y) / 3, (pa.Z + pb.Z + pc.Z) / 3), length > 0 ? n / length : n, cross.Length / 2);
        }
        return result;
    }

    /// <summary>A point on the face near its area centroid: the mesh sample closest to it, so a ring's point is on the ring.</summary>
    private static Point3D Weighted((Point3D Centroid, Vector3D Normal, double Area)[] triangles)
    {
        var total = triangles.Sum(t => t.Area);
        if (total <= 0) return triangles[0].Centroid;
        var centre = Point3D.Origin + triangles.Aggregate(Vector3D.Zero, (acc, t) => acc + (t.Centroid - Point3D.Origin) * t.Area) / total;
        return triangles.MinBy(t => (t.Centroid - centre).Length).Centroid;
    }

    private static double BodyDiagonal(BrepBody body)
    {
        var points = body.Topology.Vertices.Select(v => body.TryGetVertexPoint(v.Id, out var p) ? (Point3D?)p : null).OfType<Point3D>().ToArray();
        return points.Length < 2 ? 100d : new Vector3D(points.Max(p => p.X) - points.Min(p => p.X), points.Max(p => p.Y) - points.Min(p => p.Y), points.Max(p => p.Z) - points.Min(p => p.Z)).Length;
    }

    private static (Point3D Min, Point3D Max) Bounds(IEnumerable<DisplayFaceMeshPatch> patches)
    {
        var points = patches.SelectMany(p => p.Positions).ToArray();
        return (new Point3D(points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z)),
                new Point3D(points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z)));
    }

    private static double SignedVolume(IEnumerable<DisplayFaceMeshPatch> patches)
    {
        var volume = 0d;
        foreach (var patch in patches)
            for (var i = 0; i + 2 < patch.TriangleIndices.Count; i += 3)
            {
                var (a, b, c) = (patch.Positions[patch.TriangleIndices[i]], patch.Positions[patch.TriangleIndices[i + 1]], patch.Positions[patch.TriangleIndices[i + 2]]);
                var cross = (b - a).Cross(c - a);
                var n = patch.Normals[patch.TriangleIndices[i]] + patch.Normals[patch.TriangleIndices[i + 1]] + patch.Normals[patch.TriangleIndices[i + 2]];
                // Orient each triangle by its display normals, then sum the divergence volume.
                var s = cross.Dot(n) < 0 ? -1d : 1d;
                volume += s * (a - Point3D.Origin).Dot((b - Point3D.Origin).Cross(c - Point3D.Origin)) / 6d;
            }
        return volume;
    }

    /// <summary>Triangles bucketed on the plane perpendicular to the pull, so a ray along ±pull tests only its cell.</summary>
    private sealed class RayGrid
    {
        private readonly (Point3D A, Point3D B, Point3D C)[] triangles;
        private readonly List<int>[] cells;
        private readonly Vector3D u, v;
        private readonly double u0, v0, cell;
        private readonly int n;

        public RayGrid(IReadOnlyList<DisplayFaceMeshPatch> patches, Vector3D pull)
        {
            triangles = patches.SelectMany(p => Enumerable.Range(0, p.TriangleIndices.Count / 3)
                .Select(i => (p.Positions[p.TriangleIndices[3 * i]], p.Positions[p.TriangleIndices[3 * i + 1]], p.Positions[p.TriangleIndices[3 * i + 2]]))).ToArray();
            var helper = double.Abs(pull.X) < 0.9 ? new Vector3D(1, 0, 0) : new Vector3D(0, 1, 0);
            u = pull.Cross(helper); u /= u.Length;
            v = pull.Cross(u); v /= v.Length;
            var projected = triangles.Select(t => new[] { t.A, t.B, t.C }.Select(P).ToArray()).ToArray();
            var all = projected.SelectMany(x => x).ToArray();
            u0 = all.Min(p => p.U); v0 = all.Min(p => p.V);
            var extent = double.Max(all.Max(p => p.U) - u0, all.Max(p => p.V) - v0);
            n = int.Clamp((int)double.Sqrt(triangles.Length), 8, 256);
            cell = extent / n + 1e-12;
            cells = new List<int>[n * n];
            for (var i = 0; i < projected.Length; i++)
            {
                var p = projected[i];
                var (i0, i1) = (Index(p.Min(x => x.U) - u0), Index(p.Max(x => x.U) - u0));
                var (j0, j1) = (Index(p.Min(x => x.V) - v0), Index(p.Max(x => x.V) - v0));
                for (var a = i0; a <= i1; a++)
                    for (var b = j0; b <= j1; b++) (cells[a * n + b] ??= []).Add(i);
            }
        }

        private (double U, double V) P(Point3D p) => ((p - Point3D.Origin).Dot(u), (p - Point3D.Origin).Dot(v));
        private int Index(double offset) => int.Clamp((int)(offset / cell), 0, n - 1);

        public bool Hits(Point3D origin, Vector3D direction, double eps)
        {
            var (pu, pv) = P(origin);
            var list = cells[Index(pu - u0) * n + Index(pv - v0)];
            if (list is null) return false;
            foreach (var i in list)
            {
                var (a, b, c) = triangles[i];
                var e1 = b - a; var e2 = c - a;
                var h = direction.Cross(e2);
                var det = e1.Dot(h);
                if (double.Abs(det) < 1e-14 * e1.Length * e2.Length) continue;
                var s = origin - a;
                var f = 1d / det;
                var bu = f * s.Dot(h);
                if (bu < -1e-9 || bu > 1 + 1e-9) continue;
                var q = s.Cross(e1);
                var bv = f * direction.Dot(q);
                if (bv < -1e-9 || bu + bv > 1 + 1e-9) continue;
                if (f * e2.Dot(q) > eps) return true;
            }
            return false;
        }
    }
}
