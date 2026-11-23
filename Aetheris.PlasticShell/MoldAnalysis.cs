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

    public static MoldReport? Analyze(BrepBody body, MoldIntent intent, List<MoldDiagnostic> diagnostics) => Analyze(body, intent, diagnostics, out _);

    /// <param name="formedBy">Faces whose half is fixed (the pieces of faces cut at the parting plane): +1 the +pull half,
    /// −1 the other. Each is released along its half's pull.</param>
    internal static MoldReport? Analyze(BrepBody body, MoldIntent intent, List<MoldDiagnostic> diagnostics, out MoldInserts.Result? inserts,
        IReadOnlyDictionary<int, int>? formedBy = null)
    {
        inserts = null;
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
        var samplesOf = new Dictionary<int, (Point3D Start, Vector3D Normal)[]>();
        var trianglesOf = new Dictionary<int, (Point3D Centroid, Vector3D Normal, double Area)[]>();
        var surfaceOf = new Dictionary<int, (SurfaceGeometry? Surface, bool Aligned)>();
        foreach (var patch in patches)
        {
            var face = patch.FaceId.Value;
            body.TryGetFaceSurfaceGeometry(patch.FaceId, out var surface);
            var aligned = body.Bindings.TryGetFaceBinding(patch.FaceId, out var binding) && binding.Orientation.IsAlignedWithSurface;
            surfaceOf[face] = (surface, aligned);
            var normals = patch.Normals.Select(n => n * orientation).ToArray();
            var (dMin, dMax, strength) = Against(surface, aligned, normals, pull);
            var fixedHalf = formedBy is not null && formedBy.TryGetValue(face, out var half) ? half : 0;
            var side = fixedHalf > 0 ? MoldFaceSide.Positive : fixedHalf < 0 ? MoldFaceSide.Negative
                : dMin >= -ParallelTolerance && dMax <= ParallelTolerance ? MoldFaceSide.Vertical
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
            var samples = new List<(Point3D, Vector3D)>();
            var blocked = 0; var freePositive = 0; var freeNegative = 0;
            for (var t = 0; t < triangles.Length; t += stride)
            {
                var (centroid, normal, _) = triangles[t];
                var start = centroid + normal * eps;
                samples.Add((start, normal));
                var d = normal.Dot(pull);
                if (fixedHalf != 0)
                {
                    // Formed by a fixed half: the steel leaves along that half's pull, and cannot if it faces against it.
                    if (d * fixedHalf < -ParallelTolerance || rays.Hits(start, pull * fixedHalf, eps)) blocked++;
                }
                else if (double.Abs(d) <= ParallelTolerance)
                {
                    var up = !rays.Hits(start, pull, eps); var down = !rays.Hits(start, -pull, eps);
                    if (up) freePositive++;
                    if (down) freeNegative++;
                    if (!up && !down) blocked++;
                }
                else if (rays.Hits(start, pull * double.Sign(d), eps)) blocked++;
            }
            samplesOf[face] = samples.ToArray();
            trianglesOf[face] = triangles;
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
                samples.Count == 0 ? 0d : (double)blocked / samples.Count, samples.Count, cosmetic.Contains(face), representative, area, strength,
                SilhouetteHeight: side == MoldFaceSide.Straddling ? Silhouette(surface, samplesOf[face], pull) : null));
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

        var faceOfLoop = body.Topology.Faces.SelectMany(f => f.LoopIds.Select(l => (Loop: l, Face: f.Id.Value))).ToDictionary(x => x.Loop, x => x.Face);
        var edgeFaces = body.Topology.Coedges.GroupBy(c => c.EdgeId).ToDictionary(g => g.Key, g => g.Select(c => faceOfLoop[c.LoopId]).Distinct().ToArray());

        // Undercuts, grouped where they touch; each group is formed by a declared action that can withdraw from it, or
        // reported with the directions across the pull that would release it.
        var grids = new Dictionary<string, RayGrid> { [Key(pull)] = rays };
        RayGrid Grid(Vector3D direction) => grids.TryGetValue(Key(direction), out var grid) ? grid : grids[Key(direction)] = new RayGrid(patches, direction);
        bool Releases(IEnumerable<int> faces, Vector3D direction) => faces.All(face => samplesOf[face].All(sample =>
            sample.Normal.Dot(direction) >= -ParallelTolerance && !Grid(direction).Hits(sample.Start, direction, eps)));
        var undercutFaces = evidence.Where(f => f.UndercutFraction > 0).Select(f => f.FaceId).ToHashSet();
        var neighbours = undercutFaces.ToDictionary(f => f, _ => new HashSet<int>());
        foreach (var faces in edgeFaces.Values.Where(v => v.Length == 2 && undercutFaces.Contains(v[0]) && undercutFaces.Contains(v[1])))
        { neighbours[faces[0]].Add(faces[1]); neighbours[faces[1]].Add(faces[0]); }
        var helper = double.Abs(pull.X) < 0.9 ? new Vector3D(1, 0, 0) : new Vector3D(0, 1, 0);
        var across = pull.Cross(helper); across /= across.Length;
        var across2 = pull.Cross(across); across2 /= across2.Length;
        Vector3D[] candidates = [across, -across, across2, -across2];
        var actions = intent.Actions ?? [];
        var groups = new List<MoldUndercutGroup>();
        var actionOf = new Dictionary<int, string>();
        var unseen = undercutFaces.Order().ToList();
        while (unseen.Count > 0)
        {
            var group = new List<int>(); var queue = new Queue<int>([unseen[0]]); unseen.RemoveAt(0);
            while (queue.TryDequeue(out var face)) { group.Add(face); foreach (var next in neighbours[face]) if (unseen.Remove(next)) queue.Enqueue(next); }
            group.Sort();
            var members = evidence.Where(f => group.Contains(f.FaceId)).ToArray();
            var total = members.Sum(f => f.Area);
            var centre = Point3D.Origin + members.Aggregate(Vector3D.Zero, (acc, f) => acc + (f.Representative - Point3D.Origin) * f.Area) / double.Max(total, 1e-300);
            var releasing = actions.Where(a => Releases(group, a.Pull.ToVector())).OrderBy(a => a.Near is { } near ? (near - centre).Length : double.PositiveInfinity).ToArray();
            var action = releasing.FirstOrDefault()?.Name;
            if (action is not null) foreach (var face in group) actionOf[face] = action;
            groups.Add(new(group, centre, action, candidates.Where(c => Releases(group, c)).Select(Name).ToArray()));
        }
        var actionEvidence = new List<MoldActionEvidence>();
        foreach (var action in actions)
        {
            var faces = actionOf.Where(p => p.Value == action.Name).Select(p => p.Key).Order().ToArray();
            if (faces.Length == 0)
            {
                diagnostics.Add(new(MoldDiagnosticCodes.ActionReleasesNothing, PlasticDiagnosticSeverity.Error,
                    $"Action {action.Name} along {Name(action.Pull.ToVector())} releases none of the part's undercuts"
                    + (groups.Count == 0 ? ": the part has none." : ": " + string.Join("; ", groups.Select(g => $"faces {string.Join(", ", g.FaceIds)} near {Format(g.Representative)} release along {(g.ReleasingDirections.Count == 0 ? "no direction across the pull" : string.Join(" or ", g.ReleasingDirections))}")) + "."),
                    intent.MoldName));
                continue;
            }
            // An action's faces are drafted against its own pull.
            var direction = action.Pull.ToVector();
            var minimum = faces.Min(face => Against(surfaceOf[face].Surface, surfaceOf[face].Aligned, samplesOf[face].Select(x => x.Normal).ToArray(), direction).Min);
            var draft = double.Asin(double.Clamp(minimum, 0d, 1d)) * 180d / double.Pi;
            actionEvidence.Add(new(action.Name, action.Pull, faces, draft, intent.Policy.MinimumDraftDegrees, draft + 1e-9 >= intent.Policy.MinimumDraftDegrees));
        }
        evidence = evidence.Select(f => actionOf.TryGetValue(f.FaceId, out var name) ? f with { Action = name } : f).ToList();
        foreach (var face in actionOf.Keys) sideOf.Remove(face);

        // A straddling face that releases splits along its silhouette, where the parting line crosses it.
        var splits = evidence.Where(f => f.Side == MoldFaceSide.Straddling && f.Action is null && f.UndercutFraction == 0 && f.SilhouetteHeight is not null).ToArray();
        bool OnPlane(double h) => partingHeight is not { } ph || double.Abs(h - ph) <= 1e-6 * size + 1e-6;

        // Parting line: edges between the two halves, in closed loops.
        var polylines = mesh.Value.EdgePolylines.ToDictionary(e => e.EdgeId);
        var partingEdges = edgeFaces.Where(e => e.Value.Length == 2
                && sideOf.GetValueOrDefault(e.Value[0], MoldFaceSide.Vertical) is var a && sideOf.GetValueOrDefault(e.Value[1], MoldFaceSide.Vertical) is var b
                && ((a == MoldFaceSide.Positive && b == MoldFaceSide.Negative) || (a == MoldFaceSide.Negative && b == MoldFaceSide.Positive)))
            .Select(e => e.Key).OrderBy(e => e.Value).ToArray();
        double Height(Point3D p) => (p - Point3D.Origin).Dot(pull);
        var loops = new List<MoldPartingLoop>();
        foreach (var chain in Chains(body, partingEdges))
        {
            var points = chain.SelectMany(e => polylines.TryGetValue(e, out var line) ? line.Points : []).ToArray();
            if (points.Length == 0) continue;
            var (lo, hi) = (points.Min(Height), points.Max(Height));
            var flat = hi - lo <= 1e-6 * size + 1e-6;
            var kind = !flat ? MoldPartingLoopKind.Stepped
                : partingHeight is not null && OnPlane(lo) ? MoldPartingLoopKind.Parting
                : EnclosesOpening(points, pull, across, across2, rays, eps) ? MoldPartingLoopKind.ShutOff
                : partingHeight is null ? MoldPartingLoopKind.Parting
                : MoldPartingLoopKind.Stepped;
            loops.Add(new(chain.Select(e => e.Value).ToArray(), lo, hi, kind));
        }
        // Without a declared plane, one flat loop is the parting line and the others must be shut-offs.
        if (partingHeight is null && loops.Count(l => l.Kind == MoldPartingLoopKind.Parting) > 1)
        {
            var heights = loops.Where(l => l.Kind == MoldPartingLoopKind.Parting).Select(l => l.MinimumHeight).ToArray();
            if (heights.Max() - heights.Min() > 1e-6 * size + 1e-6)
                loops = loops.Select(l => l.Kind == MoldPartingLoopKind.Parting && double.Abs(l.MinimumHeight - heights[0]) > 1e-6 * size + 1e-6 ? l with { Kind = MoldPartingLoopKind.Stepped } : l).ToList();
        }
        var lineHeights = loops.Where(l => l.Kind != MoldPartingLoopKind.ShutOff).SelectMany(l => new[] { l.MinimumHeight, l.MaximumHeight })
            .Concat(splits.Select(f => f.SilhouetteHeight!.Value)).ToArray();
        var (hMin, hMax) = lineHeights.Length == 0 ? (double.NaN, double.NaN) : (lineHeights.Min(), lineHeights.Max());
        var onPlane = loops.All(l => l.Kind != MoldPartingLoopKind.Stepped) && splits.All(f => OnPlane(f.SilhouetteHeight!.Value));
        var cosmeticTouched = partingEdges.SelectMany(e => edgeFaces[e]).Where(cosmetic.Contains).Distinct().Order().ToArray();
        var partingLine = new MoldPartingLineEvidence(partingEdges.Select(e => e.Value).ToArray(), hMin, hMax, onPlane, cosmeticTouched, loops);

        // Wall thickness: from each sample straight into the material to the far side.
        // Inside corners: edges where two faces meet at an angle, with the material on the outside of the turn.
        var (corners, smooth) = InsideCorners(body, mesh.Value.EdgePolylines, edgeFaces, trianglesOf);
        var walls = WallThickness(body, patches, evidence, samplesOf, surfaceOf, smooth, eps);
        evidence = evidence.Select(f => walls.TryGetValue(f.FaceId, out var w) ? f with { MinimumWall = w.Min, MaximumWall = w.Max, WallStrength = w.Strength } : f).ToList();

        // Findings, scaled by stage.
        foreach (var f in evidence.Where(f => f.Side == MoldFaceSide.Straddling && f.Action is null && !(splits.Contains(f) && OnPlane(f.SilhouetteHeight!.Value))))
            diagnostics.Add(new(MoldDiagnosticCodes.StraddlingFace, Severity(intent.Stage, hard: true),
                $"Face {f.FaceId} ({f.Surface} near {Format(f.Representative)}) faces both ways along the pull; the parting line has to split it"
                + (f.SilhouetteHeight is { } h && partingHeight is { } p ? $" along its silhouette at {h:0.###} mm, off the parting plane at {p:0.###} mm. Put the parting plane through it, or form it with an action." : ", and it cannot release whole from either half."), $"face:{f.FaceId}"));
        foreach (var g in groups.Where(g => g.Action is null))
        {
            var members = evidence.Where(f => g.FaceIds.Contains(f.FaceId)).ToArray();
            diagnostics.Add(new(members.Any(f => f.Cosmetic) ? MoldDiagnosticCodes.UndercutOnCosmetic : MoldDiagnosticCodes.Undercut, Severity(intent.Stage, hard: true),
                $"Face(s) {string.Join(", ", members.Select(f => $"{f.FaceId} ({f.Surface}, {f.UndercutFraction:P0} blocked)"))} near {Format(g.Representative)} cannot release along the pull. "
                + (g.ReleasingDirections.Count > 0 ? $"They release along {string.Join(" or ", g.ReleasingDirections)}: declare an Action with that pull." : "No direction across the pull releases them either; they need a lifter, a collapsing core or a different pull."),
                "faces:" + string.Join(",", g.FaceIds)));
        }
        // Draft findings come as one line per required angle: a part at its first review typically has every wall
        // vertical, and the report lists the faces individually.
        foreach (var group in evidence.Where(f => !f.DraftSatisfied && f.Side != MoldFaceSide.Straddling && f.Action is null).GroupBy(f => f.RequiredDraftDegrees))
        {
            var faces = group.ToArray();
            diagnostics.Add(new(MoldDiagnosticCodes.DraftInsufficient, Severity(intent.Stage, hard: false),
                $"{faces.Length} face(s) have less than the {group.Key:0.##}° of draft {intent.Policy.Name} needs: "
                + string.Join("; ", faces.Select(f => $"{f.FaceId} ({f.Surface} near {Format(f.Representative)}, {f.MinimumDraftDegrees:0.##}°)")) + ".",
                "faces:" + string.Join(",", faces.Select(f => f.FaceId))));
        }
        foreach (var action in actionEvidence.Where(a => !a.DraftSatisfied))
            diagnostics.Add(new(MoldDiagnosticCodes.DraftInsufficient, Severity(intent.Stage, hard: false),
                $"Action {action.Name}'s faces {string.Join(", ", action.FaceIds)} have {action.MinimumDraftDegrees:0.##}° of draft along its pull {Name(action.Pull.ToVector())}; {intent.Policy.Name} needs {action.RequiredDraftDegrees:0.##}°.",
                $"action:{action.Name}"));
        if (!onPlane)
        {
            var stepped = loops.Where(l => l.Kind == MoldPartingLoopKind.Stepped).Select(l => $"{l.MinimumHeight:0.###}–{l.MaximumHeight:0.###} mm")
                .Concat(splits.Where(f => !OnPlane(f.SilhouetteHeight!.Value)).Select(f => $"face {f.FaceId}'s silhouette at {f.SilhouetteHeight:0.###} mm")).ToArray();
            diagnostics.Add(new(MoldDiagnosticCodes.PartingLineOffPlane, Severity(intent.Stage, hard: false),
                $"The parting line leaves the declared parting plane at {partingHeight:0.###} mm ({string.Join("; ", stepped)}); the part needs a stepped parting surface or a different plane.", intent.MoldName));
        }
        var thin = evidence.Where(f => f.MinimumWall is { } w && w < intent.Policy.MinimumWall - 1e-9).ToArray();
        if (thin.Length > 0)
            diagnostics.Add(new(MoldDiagnosticCodes.WallTooThin, Severity(intent.Stage, hard: false),
                $"{thin.Length} face(s) are thinner than the {intent.Policy.MinimumWall:0.###} mm wall {intent.Policy.Name} fills: "
                + string.Join("; ", thin.Select(f => $"{f.FaceId} ({f.Surface} near {Format(f.Representative)}, {f.MinimumWall:0.###} mm{(f.WallStrength == MoldEvidenceStrength.ExactAnalytic ? "" : " sampled")})")) + ".",
                "faces:" + string.Join(",", thin.Select(f => f.FaceId))));
        var thick = evidence.Where(f => f.MaximumWall is { } w && w > intent.Policy.MaximumWall + 1e-9).ToArray();
        if (thick.Length > 0)
            diagnostics.Add(new(MoldDiagnosticCodes.WallTooThick, Severity(intent.Stage, hard: false),
                $"{thick.Length} face(s) back onto sections thicker than {intent.Policy.MaximumWall:0.###} mm, which sink and void in {intent.Policy.Name}; core them out to an even wall: "
                + string.Join("; ", thick.Take(12).Select(f => $"{f.FaceId} ({f.Surface} near {Format(f.Representative)}, up to {f.MaximumWall:0.###} mm)")) + (thick.Length > 12 ? "; …" : "") + ".",
                "faces:" + string.Join(",", thick.Select(f => f.FaceId))));
        // Sharp inside corners are a guideline (stress and flow), reported at every stage; a Require makes them binding.
        if (corners.Count > 0 && intent.Policy.MinimumInsideRadius > 0)
            diagnostics.Add(new(MoldDiagnosticCodes.SharpInsideCorner, PlasticDiagnosticSeverity.Warning,
                $"{corners.Count} inside edge(s) are sharp; round them to at least {intent.Policy.MinimumInsideRadius:0.###} mm (about half the wall), with the outside radius the inside plus the wall: "
                + string.Join("; ", corners.Take(8).Select(c => $"edge {c.EdgeId} between faces {c.FaceIds[0]} and {c.FaceIds[1]} near {Format(c.Midpoint)} ({c.AngleDegrees:0.#}°)")) + (corners.Count > 8 ? "; …" : "") + ".",
                "edges:" + string.Join(",", corners.Select(c => c.EdgeId))));
        if (cosmeticTouched.Length > 0)
            diagnostics.Add(new(MoldDiagnosticCodes.PartingLineOnCosmetic, Severity(intent.Stage, hard: false),
                $"The parting line runs along cosmetic face(s) {string.Join(", ", cosmeticTouched)}; it leaves a witness line there.", intent.MoldName));

        // Projected along the pull: the +pull half's faces, and the upward part of each face split at its silhouette.
        var splitIds = splits.Select(f => f.FaceId).ToHashSet();
        var projected = patches.Where(p => sideOf.GetValueOrDefault(p.FaceId.Value, MoldFaceSide.Vertical) == MoldFaceSide.Positive || splitIds.Contains(p.FaceId.Value))
            .SelectMany(p => Triangles(p, orientation)).Sum(t => t.Area * double.Max(0d, t.Normal.Dot(pull)));
        var volume = BrepMassProperties.Evaluate(body).AbsoluteVolume;
        var summary = new MoldSummary(evidence.Count,
            evidence.Count(f => f.Side == MoldFaceSide.Positive && f.Action is null), evidence.Count(f => f.Side == MoldFaceSide.Negative && f.Action is null),
            evidence.Count(f => f.Side == MoldFaceSide.Vertical && f.Action is null), evidence.Count(f => f.Side == MoldFaceSide.Straddling && f.Action is null),
            evidence.Count(f => !f.DraftSatisfied && f.Action is null && f.Side != MoldFaceSide.Straddling), groups.Where(g => g.Action is null).Sum(g => g.FaceIds.Count),
            projected, volume, volume * intent.Policy.Density / 1000d, actionOf.Count);
        // The core and cavity inserts, when asked for.
        if (intent.InsertMargin is { } margin)
        {
            var plane = partingHeight ?? loops.FirstOrDefault(l => l.Kind == MoldPartingLoopKind.Parting)?.MinimumHeight;
            string? reason = plane is null ? "the part has no parting line" : null;
            // Faces that cross the parting plane are cut there first; the cut part is reviewed again, so that each of
            // its faces is formed by one half, and the inserts are cut from it.
            var faceMesh = new MoldMeshRays(patches);
            var cut = reason is null ? MoldSplit.AtPlane(body, pull, plane!.Value, 1e-6 * size + 1e-6,
                (face, p) => faceMesh.Closest(p, f => f != face)?.Distance ?? double.PositiveInfinity, eps, out reason) : null;
            if (cut is { SplitFaces.Count: > 0 })
            {
                var inner = new List<MoldDiagnostic>();
                Analyze(cut.Body, intent with { Requirements = null }, inner, out inserts, cut.Pieces);
                var undercut = inner.FirstOrDefault(d => d.Code is MoldDiagnosticCodes.Undercut or MoldDiagnosticCodes.UndercutOnCosmetic);
                var refused = inner.FirstOrDefault(d => d.Code == MoldDiagnosticCodes.InsertsUnavailable);
                if (undercut is not null) { inserts = null; reason = $"cut at the parting plane (faces {string.Join(", ", cut.SplitFaces)}), a piece cannot leave the half on its side of the plane (face ids of the cut part follow): {undercut.Message}"; }
                else if (refused is not null) reason = $"cut at the parting plane (faces {string.Join(", ", cut.SplitFaces)}), {refused.Message.Replace("The core and cavity inserts cannot be cut: ", "", StringComparison.Ordinal).TrimEnd('.')}";
                else if (inserts is not null) inserts = inserts with { Evidence = inserts.Evidence with { SplitFaces = cut.SplitFaces } };
            }
            else if (reason is null)
            {
                var slides = actionEvidence.Select(a => new MoldInserts.Slide(a.Name, a.Pull.ToVector(), a.FaceIds)).ToArray();
                inserts = MoldInserts.Build(body, sideOf, loops, pull, plane!.Value, margin, (min, max), slides, out reason);
            }
            if (inserts is null)
                diagnostics.Add(new(MoldDiagnosticCodes.InsertsUnavailable, PlasticDiagnosticSeverity.Error, $"The core and cavity inserts cannot be cut: {reason}.", intent.MoldName));
        }
        return new(intent, evidence, partingLine, summary,
            "Face normals against the pull (exact on planes, and on cylinders and cones about the pull; mesh extremes otherwise); release by casting the display mesh's samples along the pull, and along each action's pull, against the part; parting line from face adjacency, in loops. Undercuts between samples are not proven absent.",
            groups, actionEvidence, inserts?.Evidence, corners,
            Measure(evidence, corners, groups, actionOf.Count, body, surfaceOf));
    }

    /// <summary>
    /// Wall thickness behind each face, as the diameter of the largest ball inside the material that touches the face at
    /// a sample (the rolling-ball measure: a rim or a rib is as thick as it is wide, not as long). A ball wedged against
    /// a single neighbouring face across the face's own edge measures the corner, not a wall, and is set aside. Exact
    /// where the ball rests on a parallel plane, or on a coaxial cone (cylinder) of the same angle.
    /// </summary>
    private static Dictionary<int, (double Min, double Max, MoldEvidenceStrength Strength)> WallThickness(BrepBody body, IReadOnlyList<DisplayFaceMeshPatch> patches,
        IReadOnlyList<MoldFaceEvidence> evidence, IReadOnlyDictionary<int, (Point3D Start, Vector3D Normal)[]> samplesOf,
        IReadOnlyDictionary<int, (SurfaceGeometry? Surface, bool Aligned)> surfaceOf, IReadOnlySet<(int, int)> smooth, double eps)
    {
        var rays = new MoldMeshRays(patches);
        var faceOfLoop = body.Topology.Faces.SelectMany(f => f.LoopIds.Select(l => (Loop: l, Face: f.Id.Value))).ToDictionary(x => x.Loop, x => x.Face);
        var adjacent = body.Topology.Coedges.GroupBy(c => c.EdgeId).Select(g => g.Select(c => faceOfLoop[c.LoopId]).Distinct().ToArray()).Where(f => f.Length == 2)
            .SelectMany(f => new[] { (f[0], f[1]), (f[1], f[0]) }).ToHashSet();
        var result = new Dictionary<int, (double, double, MoldEvidenceStrength)>();
        foreach (var f in evidence)
        {
            double min = double.PositiveInfinity, max = 0; var exact = true; var any = false;
            foreach (var (start, normal) in samplesOf[f.FaceId])
            {
                var surfacePoint = start - normal * eps;
                if (rays.Nearest(surfacePoint - normal * eps, -normal, 0) is not { } through) continue;
                // The ball's radius: the largest r whose centre p − r n is at least r from every other face.
                double lo = 0, hi = (through.Distance + eps) / 2;
                // The face's own surface goes on smoothly into its tangent neighbours; neither stops the ball.
                bool Own(int face) => face == f.FaceId || smooth.Contains((f.FaceId, face));
                bool Fits(double r) => rays.Closest(surfacePoint - normal * r, Own, r) is null;
                if (Fits(hi)) lo = hi;
                else for (var k = 0; k < 24 && hi - lo > 1e-4 * hi; k++) { var mid = (lo + hi) / 2; if (Fits(mid)) lo = mid; else hi = mid; }
                var radius = lo;
                var centre = surfacePoint - normal * radius;
                var contact = rays.Closest(centre, Own);
                if (contact is null) continue;
                // A single contact on a neighbour across this face's own edge: the ball is in a corner.
                var second = rays.Closest(centre, face => Own(face) || face == contact.Value.Face || smooth.Contains((contact.Value.Face, face)), radius * 1.05 + eps);
                if (second is null && adjacent.Contains((f.FaceId, contact.Value.Face))) continue;
                any = true;
                var thickness = 2 * radius;
                if (Exact(surfaceOf[f.FaceId], surfaceOf.GetValueOrDefault(contact.Value.Face)) is { } exactThickness && double.Abs(exactThickness - thickness) < 0.05 * exactThickness + 2 * eps)
                    thickness = exactThickness;
                else exact = false;
                min = double.Min(min, thickness); max = double.Max(max, thickness);
            }
            if (any) result[f.FaceId] = (min, max, exact ? MoldEvidenceStrength.ExactAnalytic : MoldEvidenceStrength.SampledConservative);
        }
        return result;

        static double? Exact((SurfaceGeometry? Surface, bool Aligned) a, (SurfaceGeometry? Surface, bool Aligned) b)
        {
            switch (a.Surface, b.Surface)
            {
                case ({ Plane: { } p }, { Plane: { } q }):
                    var n = p.Normal.ToVector() * (a.Aligned ? 1 : -1); var m = q.Normal.ToVector() * (b.Aligned ? 1 : -1);
                    return n.Dot(m) < -1 + 1e-12 ? double.Abs((q.Origin - p.Origin).Dot(n)) : null;
                case ({ Cylinder: { } c }, { Cylinder: { } d }) when Coaxial(c.Origin, c.Axis.ToVector(), d.Origin, d.Axis.ToVector()):
                    return double.Abs(c.Radius - d.Radius);
                case ({ Cone: { } c }, { Cone: { } d }) when double.Abs(c.SemiAngleRadians - d.SemiAngleRadians) < 1e-12 && Coaxial(c.PlacementOrigin, c.Axis.ToVector(), d.PlacementOrigin, d.Axis.ToVector()):
                    // Radii compared at one station along the shared axis.
                    var axis = c.Axis.ToVector();
                    var rc = c.PlacementRadius; var rd = d.PlacementRadius + (c.PlacementOrigin - d.PlacementOrigin).Dot(d.Axis.ToVector()) * double.Tan(d.SemiAngleRadians);
                    return double.Abs(rc - rd) * double.Cos(c.SemiAngleRadians);
            }
            return null;
        }
        static bool Coaxial(Point3D o1, Vector3D a1, Point3D o2, Vector3D a2)
        {
            if (double.Abs(double.Abs(a1.Dot(a2)) - 1) > 1e-12) return false;
            var d = o2 - o1; var off = d - a1 * d.Dot(a1);
            return off.Length < 1e-9;
        }
    }

    /// <summary>Edges between two faces whose material meets on the outside of the turn, at more than 5°: no radius.</summary>
    private static (IReadOnlyList<MoldCornerEvidence> Corners, IReadOnlySet<(int, int)> Smooth) InsideCorners(BrepBody body, IReadOnlyList<DisplayEdgePolyline> polylines,
        IReadOnlyDictionary<EdgeId, int[]> edgeFaces, IReadOnlyDictionary<int, (Point3D Centroid, Vector3D Normal, double Area)[]> trianglesOf)
    {
        var corners = new List<MoldCornerEvidence>();
        var smooth = new HashSet<(int, int)>();
        foreach (var line in polylines)
        {
            if (!edgeFaces.TryGetValue(line.EdgeId, out var faces) || faces.Length != 2 || line.Points.Count < 2) continue;
            if (!trianglesOf.TryGetValue(faces[0], out var t1) || !trianglesOf.TryGetValue(faces[1], out var t2) || t1.Length == 0 || t2.Length == 0) continue;
            var k = line.Points.Count / 2;
            var m = line.Points.Count % 2 == 1 ? line.Points[k] : Point3D.Origin + ((line.Points[k - 1] - Point3D.Origin) + (line.Points[k] - Point3D.Origin)) * 0.5;
            var tangent = line.Points[^1] - line.Points[0];
            if (line.Points.Count > 2) tangent = line.Points[int.Min(k + 1, line.Points.Count - 1)] - line.Points[int.Max(k - 1, 0)];
            if (tangent.Length < 1e-12) continue;
            tangent /= tangent.Length;
            var a = t1.MinBy(t => (t.Centroid - m).Length); var b = t2.MinBy(t => (t.Centroid - m).Length);
            var cos = a.Normal.Dot(b.Normal);
            if (cos > double.Cos(5 * double.Pi / 180)) { smooth.Add((faces[0], faces[1])); smooth.Add((faces[1], faces[0])); continue; }
            // Into face A, across the edge: concave when B's outward normal leans over it.
            var into = a.Centroid - m; into -= tangent * into.Dot(tangent);
            if (into.Length < 1e-12) continue;
            into /= into.Length;
            if (b.Normal.Dot(into) <= 0.02) continue;
            corners.Add(new(line.EdgeId.Value, faces.Order().ToArray(), 180 - double.Acos(double.Clamp(cos, -1, 1)) * 180 / double.Pi, m));
        }
        return (corners.OrderBy(c => c.EdgeId).ToArray(), smooth);
    }

    /// <summary>The scalar measurements a Mold Require can name.</summary>
    private static MoldMeasurements Measure(IReadOnlyList<MoldFaceEvidence> evidence, IReadOnlyList<MoldCornerEvidence> corners,
        IReadOnlyList<MoldUndercutGroup> groups, int actionFaces, BrepBody body, IReadOnlyDictionary<int, (SurfaceGeometry? Surface, bool Aligned)> surfaceOf)
    {
        var halves = evidence.Where(f => f.Action is null && f.Side != MoldFaceSide.Straddling).ToArray();
        var draft = halves.Length == 0 ? 90d : halves.Min(f => f.MinimumDraftDegrees);
        var walls = evidence.Where(f => f.MinimumWall is not null).ToArray();
        // The nominal wall is the area-weighted median of each face's thinnest wall.
        var ordered = walls.OrderBy(f => f.MinimumWall!.Value).ToArray();
        var half = ordered.Sum(f => f.Area) / 2; var running = 0d; var nominal = 0d;
        foreach (var f in ordered) { running += f.Area; if (running >= half) { nominal = f.MinimumWall!.Value; break; } }
        // Inside radius: none at a sharp corner; otherwise the smallest concave round.
        var radius = corners.Count > 0 ? 0d : MinimumConcaveRadius(evidence, surfaceOf) ?? 1e6;
        return new(draft, walls.Length == 0 ? 0 : walls.Min(f => f.MinimumWall!.Value), walls.Length == 0 ? 0 : walls.Max(f => f.MaximumWall!.Value), nominal,
            corners.Count, radius, groups.Count(g => g.Action is null), groups.Count(g => g.Action is not null));
    }

    private static double? MinimumConcaveRadius(IReadOnlyList<MoldFaceEvidence> evidence, IReadOnlyDictionary<int, (SurfaceGeometry? Surface, bool Aligned)> surfaceOf)
    {
        double? min = null;
        foreach (var f in evidence)
        {
            var (surface, aligned) = surfaceOf[f.FaceId];
            // A round is concave when its outward normal points toward its axis: the surface's own normal reversed.
            if (aligned) continue;
            var r = surface switch { { Torus: { } t } => (double?)t.MinorRadius, _ => null };
            if (r is { } value) min = min is null ? value : double.Min(min.Value, value);
        }
        return min;
    }

    /// <summary>The extremes of n·direction over a face: exact on planes, and on cylinders and cones about the direction.</summary>
    private static (double Min, double Max, MoldEvidenceStrength Strength) Against(SurfaceGeometry? surface, bool aligned, IReadOnlyList<Vector3D> normals, Vector3D direction)
    {
        var dots = normals.Select(n => n.Dot(direction) / double.Max(n.Length, 1e-300)).ToArray();
        var (min, max) = dots.Length == 0 ? (0d, 0d) : (dots.Min(), dots.Max());
        switch (surface)
        {
            case { Plane: { } plane }:
                var d = plane.Normal.ToVector().Dot(direction) * (aligned ? 1d : -1d);
                return (d, d, MoldEvidenceStrength.ExactAnalytic);
            case { Cylinder: { } cylinder } when double.Abs(double.Abs(cylinder.Axis.ToVector().Dot(direction)) - 1d) < 1e-12:
                return (0d, 0d, MoldEvidenceStrength.ExactAnalytic);
            case { Cone: { } cone } when double.Abs(double.Abs(cone.Axis.ToVector().Dot(direction)) - 1d) < 1e-12:
                var c = double.Sign(dots.Average()) * double.Sin(cone.SemiAngleRadians);
                return (c, c, MoldEvidenceStrength.ExactAnalytic);
        }
        return (min, max, MoldEvidenceStrength.SampledConservative);
    }

    /// <summary>
    /// Where a straddling face turns from one half to the other, along the pull: exact for a cylinder across the pull
    /// (its axis) and a sphere (its centre); otherwise the mean height of the samples nearest to parallel.
    /// </summary>
    private static double? Silhouette(SurfaceGeometry? surface, IReadOnlyList<(Point3D Start, Vector3D Normal)> samples, Vector3D pull)
    {
        switch (surface)
        {
            case { Cylinder: { } cylinder } when double.Abs(cylinder.Axis.ToVector().Dot(pull)) < 1e-12:
                return (cylinder.Origin - Point3D.Origin).Dot(pull);
            case { Sphere: { } sphere }:
                return (sphere.Center - Point3D.Origin).Dot(pull);
        }
        if (samples.Count == 0) return null;
        var nearest = samples.Min(x => double.Abs(x.Normal.Dot(pull)));
        var band = samples.Where(x => double.Abs(x.Normal.Dot(pull)) <= nearest + 0.05).ToArray();
        return band.Average(x => (x.Start - Point3D.Origin).Dot(pull));
    }

    /// <summary>The parting edges in closed chains, each followed through shared vertices.</summary>
    private static IReadOnlyList<IReadOnlyList<EdgeId>> Chains(BrepBody body, IReadOnlyList<EdgeId> edges)
    {
        var ends = edges.ToDictionary(e => e, e => body.Topology.GetEdge(e));
        var at = new Dictionary<VertexId, List<EdgeId>>();
        foreach (var (id, edge) in ends)
        {
            (at.TryGetValue(edge.StartVertexId, out var a) ? a : at[edge.StartVertexId] = []).Add(id);
            (at.TryGetValue(edge.EndVertexId, out var b) ? b : at[edge.EndVertexId] = []).Add(id);
        }
        var left = edges.ToHashSet();
        var chains = new List<IReadOnlyList<EdgeId>>();
        foreach (var first in edges)
        {
            if (!left.Remove(first)) continue;
            var chain = new List<EdgeId> { first };
            var vertex = ends[first].EndVertexId;
            while (at[vertex].FirstOrDefault(left.Contains) is { Value: > 0 } next)
            {
                left.Remove(next); chain.Add(next);
                vertex = ends[next].StartVertexId == vertex ? ends[next].EndVertexId : ends[next].StartVertexId;
            }
            chains.Add(chain);
        }
        return chains;
    }

    /// <summary>
    /// Whether a flat loop across the pull rings an opening: a point inside it, in its plane, lies outside the part
    /// (an even number of crossings along the pull).
    /// </summary>
    private static bool EnclosesOpening(IReadOnlyList<Point3D> points, Vector3D pull, Vector3D u, Vector3D v, RayGrid rays, double eps)
    {
        var plane = points.Select(p => ((p - Point3D.Origin).Dot(u), (p - Point3D.Origin).Dot(v))).ToArray();
        var height = points.Average(p => (p - Point3D.Origin).Dot(pull));
        var centre = (plane.Average(p => p.Item1), plane.Average(p => p.Item2));
        if (!Inside(plane, centre)) return false;
        var origin = Point3D.Origin + u * centre.Item1 + v * centre.Item2 + pull * height;
        return rays.Count(origin, pull, eps) % 2 == 0;
    }

    private static bool Inside(IReadOnlyList<(double X, double Y)> polygon, (double X, double Y) p)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            if ((polygon[i].Y > p.Y) != (polygon[j].Y > p.Y)
                && p.X < (polygon[j].X - polygon[i].X) * (p.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X) inside = !inside;
        return inside;
    }

    private static string Key(Vector3D v) => $"{v.X:F9},{v.Y:F9},{v.Z:F9}";

    /// <summary>An axis name for an axis-aligned direction, a vector otherwise.</summary>
    internal static string Name(Vector3D v)
    {
        foreach (var (axis, label) in new[] { (new Vector3D(1, 0, 0), "X"), (new Vector3D(0, 1, 0), "Y"), (new Vector3D(0, 0, 1), "Z") })
        {
            var d = v.Dot(axis) / v.Length;
            if (double.Abs(double.Abs(d) - 1) < 1e-9) return (d > 0 ? "+" : "-") + label;
        }
        return $"[{v.X:0.###}, {v.Y:0.###}, {v.Z:0.###}]";
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

        public bool Hits(Point3D origin, Vector3D direction, double eps) => Count(origin, direction, eps, stopAtFirst: true) > 0;

        /// <summary>How many triangles the ray crosses beyond <paramref name="eps"/>.</summary>
        public int Count(Point3D origin, Vector3D direction, double eps, bool stopAtFirst = false)
        {
            var count = 0;
            var (pu, pv) = P(origin);
            var list = cells[Index(pu - u0) * n + Index(pv - v0)];
            if (list is null) return 0;
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
                if (f * e2.Dot(q) > eps) { count++; if (stopAtFirst) return count; }
            }
            return count;
        }
    }
}
