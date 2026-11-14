using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Visualization;

namespace Aetheris.Kernel.Firmament.Drawing;

internal sealed record DrawingProjectionBody(string OccurrenceIdentity, string DefinitionIdentity, BrepBody Body);

/// <summary>Exact B-rep edges plus a bounded display-mesh occlusion oracle. The mesh decides
/// only visibility intervals; it never replaces the authoritative edge geometry.</summary>
internal static class DrawingProjectionEngine
{
    private readonly record struct CameraPoint(double X, double Y, double Depth);
    private sealed record RawEdge(string Id, string Occurrence, string Definition, IReadOnlyList<CameraPoint> Points, bool Curved)
    {
        /// <summary>The faces bounding this edge (occurrence:face id): their display triangles never hide it.</summary>
        public IReadOnlySet<string> OwnFaces { get; init; } = new HashSet<string>();
    }
    private sealed record Triangle(CameraPoint A, CameraPoint B, CameraPoint C, string Identity, string Face);

    public static DrawingViewIr Project(
        IReadOnlyList<DrawingProjectionBody> bodies,
        string name,
        DrawingProjectionKind projection,
        DrawingHiddenLinePolicy hiddenPolicy,
        IReadOnlyList<double> directionSource,
        IReadOnlyList<string> pmi,
        DrawingRect viewport,
        DrawingLocationIr location)
    {
        var direction = Normalize(new Vector3D(directionSource[0], directionSource[1], directionSource[2]));
        var upCandidate = Math.Abs(direction.Z) > .9 ? new Vector3D(0, 1, 0) : new Vector3D(0, 0, 1);
        var right = Normalize(upCandidate.Cross(direction));
        var up = Normalize(direction.Cross(right));
        CameraPoint Camera(Point3D point) => new(
            point.X * right.X + point.Y * right.Y + point.Z * right.Z,
            point.X * up.X + point.Y * up.Y + point.Z * up.Z,
            point.X * direction.X + point.Y * direction.Y + point.Z * direction.Z);

        var raw = new List<RawEdge>();
        var triangles = new List<Triangle>();
        var unsupported = new List<string>();
        foreach (var projectionBody in bodies.OrderBy(body => body.OccurrenceIdentity, StringComparer.Ordinal))
        {
            var body = projectionBody.Body;
            var samples = BrepWireframeSvgRenderer.SampleGeometry(body, new(CurveSamples: 64, Density: 5));
            unsupported.AddRange(samples.Evidence.UnsupportedCurveFamilies.Select(kind => $"{projectionBody.OccurrenceIdentity}:curve:{kind}"));
            var loopFace = body.Topology.Faces.SelectMany(face => face.LoopIds.Select(loop => (loop, face.Id.Value))).ToDictionary(x => x.loop, x => x.Value);
            var edgeFaces = body.Topology.Coedges.Where(c => loopFace.ContainsKey(c.LoopId)).GroupBy(c => c.EdgeId.Value)
                .ToDictionary(g => g.Key, g => g.Select(c => loopFace[c.LoopId]).Distinct().ToArray());
            foreach (var line in samples.Boundaries)
            {
                var edge = body.Topology.Edges.Single(e => e.Id.Value == line.EdgeId);
                if (IsInternalSupportEdge(body, edge.Id)) continue;
                var curved = body.TryGetEdgeCurveGeometry(edge.Id, out var curve) && curve?.Kind != CurveGeometryKind.Line3;
                var points = curved ? line.Points : new[] { line.Points[0], line.Points[^1] };
                raw.Add(new(line.Identity, projectionBody.OccurrenceIdentity, projectionBody.DefinitionIdentity, points.Select(Camera).ToArray(), curved)
                { OwnFaces = (edgeFaces.TryGetValue(edge.Id.Value, out var own) ? own : []).Select(f => $"{projectionBody.OccurrenceIdentity}:{f}").ToHashSet() });
            }
            if (hiddenPolicy == DrawingHiddenLinePolicy.Wireframe)
            {
                if (projection == DrawingProjectionKind.Isometric)
                    foreach (var line in samples.Isolines)
                        raw.Add(new(line.Identity, projectionBody.OccurrenceIdentity, projectionBody.DefinitionIdentity, line.Points.Select(Camera).ToArray(), false));
                unsupported.AddRange(samples.Evidence.UnsupportedSurfaceFamilies.Select(kind => $"{projectionBody.OccurrenceIdentity}:surface:{kind}"));
                continue;
            }

            // The admitted tessellator is the deterministic support-family bound. Avoid a
            // wall-clock cutoff here: timeout-dependent partial patches would make DrawingIR
            // depend on machine load and violate reproducible HLR evidence.
            var meshResult = BrepDisplayTessellator.Tessellate(body);
            var mesh = meshResult.IsSuccess ? meshResult.Value : new DisplayTessellationResult([], [], meshResult.Diagnostics.Select(diagnostic =>
                new DisplayFaceMaterializationDiagnostic(null, null, "Tessellate", diagnostic.Source ?? diagnostic.Code.ToString(), diagnostic.Message)).ToArray());
            foreach (var diagnostic in mesh.FaceDiagnostics ?? [])
                unsupported.Add($"{projectionBody.OccurrenceIdentity}:face:{diagnostic.FaceId?.Value.ToString(CultureInfo.InvariantCulture) ?? "?"}:{diagnostic.SurfaceKind ?? diagnostic.Code}");
            // Contours of curved faces (a cylinder's sides seen across its axis) are not B-rep edges, so without them a
            // rounded end reads as an open shape. Each is where the surface normal turns perpendicular to the view: the
            // zero crossing of n·d across each display triangle, interpolated from its vertex normals.
            foreach (var patch in mesh.FacePatches.OrderBy(patch => patch.FaceId.Value))
            {
                if (patch.Normals.Count != patch.Positions.Count) continue;
                if (body.TryGetFaceSurfaceGeometry(patch.FaceId, out var faceSurface) && faceSurface?.Plane is not null) continue;
                var pieces = new List<(Point3D A, Point3D B)>();
                for (var index = 0; index + 2 < patch.TriangleIndices.Count; index += 3)
                {
                    var corners = new[] { patch.TriangleIndices[index], patch.TriangleIndices[index + 1], patch.TriangleIndices[index + 2] };
                    var s = corners.Select(c => patch.Normals[c].Dot(direction)).ToArray();
                    var crossings = new List<Point3D>();
                    for (var k = 0; k < 3; k++)
                    {
                        // Interpolate from the lower vertex index so both triangles sharing an edge produce the same point.
                        var i = k; var j = (k + 1) % 3;
                        if (corners[i] > corners[j]) (i, j) = (j, i);
                        if (s[i] < 0 == s[j] < 0 || Math.Abs(s[i] - s[j]) < 1e-12) continue;
                        var t = s[i] / (s[i] - s[j]); var p = patch.Positions[corners[i]]; var q = patch.Positions[corners[j]];
                        crossings.Add(new(p.X + (q.X - p.X) * t, p.Y + (q.Y - p.Y) * t, p.Z + (q.Z - p.Z) * t));
                    }
                    if (crossings.Count == 2 && (crossings[0] - crossings[1]).Length > 1e-9) pieces.Add((crossings[0], crossings[1]));
                }
                foreach (var (chain, number) in ChainPieces(pieces).Select((c, i) => (c, i)))
                    raw.Add(new($"contour:face:{patch.FaceId.Value}:{number}", projectionBody.OccurrenceIdentity,
                        projectionBody.DefinitionIdentity, chain.Select(Camera).ToArray(), true)
                    { OwnFaces = new HashSet<string> { $"{projectionBody.OccurrenceIdentity}:{patch.FaceId.Value}" } });
            }
            foreach (var patch in mesh.FacePatches.OrderBy(patch => patch.FaceId.Value))
            for (var index = 0; index + 2 < patch.TriangleIndices.Count; index += 3)
            {
                var a = Camera(patch.Positions[patch.TriangleIndices[index]]);
                var b = Camera(patch.Positions[patch.TriangleIndices[index + 1]]);
                var c = Camera(patch.Positions[patch.TriangleIndices[index + 2]]);
                if (Math.Abs(Cross(a, b, c)) > 1e-12)
                    triangles.Add(new(a, b, c, $"{projectionBody.OccurrenceIdentity}:face:{patch.FaceId.Value}:triangle:{index / 3}", $"{projectionBody.OccurrenceIdentity}:{patch.FaceId.Value}"));
            }
        }
        if (raw.Count == 0) throw new InvalidOperationException("drawing-projection-no-exact-edges");

        var all = raw.SelectMany(edge => edge.Points).ToArray();
        var minX = all.Min(point => point.X); var maxX = all.Max(point => point.X);
        var minY = all.Min(point => point.Y); var maxY = all.Max(point => point.Y);
        var modelWidth = Math.Max(1e-9, maxX - minX); var modelHeight = Math.Max(1e-9, maxY - minY);
        var scale = Math.Min((viewport.Width - 24) / modelWidth, (viewport.Height - 24) / modelHeight);
        var originX = viewport.X + (viewport.Width - modelWidth * scale) / 2 - minX * scale;
        var originY = viewport.Y + (viewport.Height - modelHeight * scale) / 2 + maxY * scale;
        DrawingPoint2 Page(CameraPoint point) => new(originX + point.X * scale, originY - point.Y * scale);

        var primitives = new List<DrawingProjectedPrimitiveIr>();
        var candidateSegments = 0; var visibleSegments = 0; var hiddenSegments = 0; var splitPoints = 0;
        if (hiddenPolicy == DrawingHiddenLinePolicy.Wireframe)
        {
            foreach (var edge in raw.OrderBy(edge => edge.Occurrence, StringComparer.Ordinal).ThenBy(edge => edge.Id, StringComparer.Ordinal))
                primitives.Add(new($"{edge.Occurrence}:{edge.Id}:wireframe", edge.Id.StartsWith("face:", StringComparison.Ordinal) ? DrawingPrimitiveKind.SurfaceGrid : DrawingPrimitiveKind.Visible,
                    edge.Points.Select(Page).ToArray(), edge.Occurrence, edge.Points.Average(p => p.Depth), edge.Definition,
                    edge.Id.StartsWith("edge:", StringComparison.Ordinal) ? edge.Id : null));
            candidateSegments = raw.Sum(edge => edge.Points.Count - 1);
            visibleSegments = candidateSegments;
        }
        else
        {
        var grid = new TriangleGrid(triangles);
        foreach (var edge in raw.OrderBy(edge => edge.Occurrence, StringComparer.Ordinal).ThenBy(edge => edge.Id, StringComparer.Ordinal))
        {
            // Contiguous intervals of one kind along an edge are one polyline: a dense part splits its edges at every
            // triangle they cross, and one primitive per interval made drawings of hundreds of megabytes.
            var run = new List<CameraPoint>(); DrawingPrimitiveKind? runKind = null; var runs = 0; var interiorEnd = false;
            void Flush()
            {
                if (runKind is { } k && run.Count > 1)
                    primitives.Add(new($"{edge.Occurrence}:{edge.Id}:run:{runs++}:{k}", k, run.Select(Page).ToArray(), edge.Occurrence,
                        run.Average(p => p.Depth), edge.Definition, edge.Id));
                run = []; runKind = null; interiorEnd = false;
            }
            for (var segment = 1; segment < edge.Points.Count; segment++)
            {
                candidateSegments++;
                var a = edge.Points[segment - 1]; var b = edge.Points[segment];
                var cuts = new List<double> { 0, 1 };
                // Only triangles whose bounds meet the segment's can cut or cover it.
                // An edge lies on its own faces, whose display triangles are chords that can sit a hair in front of it;
                // they never hide it.
                var near = grid.Near(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y))
                    .Where(t => !edge.OwnFaces.Contains(t.Face)).ToList();
                foreach (var triangle in near)
                {
                    AddIntersection(a, b, triangle.A, triangle.B, cuts);
                    AddIntersection(a, b, triangle.B, triangle.C, cuts);
                    AddIntersection(a, b, triangle.C, triangle.A, cuts);
                }
                var ordered = cuts.Where(value => value >= 0 && value <= 1).Order().Aggregate(new List<double>(), (values, value) =>
                { if (values.Count == 0 || Math.Abs(values[^1] - value) > 1e-8) values.Add(value); return values; });
                splitPoints += Math.Max(0, ordered.Count - 2);
                for (var interval = 1; interval < ordered.Count; interval++)
                {
                    var from = ordered[interval - 1]; var to = ordered[interval];
                    if (to - from < 1e-8) continue;
                    var midpoint = Lerp(a, b, (from + to) / 2);
                    var hidden = near.Any(triangle => Contains(triangle, midpoint.X, midpoint.Y, out var depth) && depth > midpoint.Depth + 1e-5);
                    if (hidden) hiddenSegments++; else visibleSegments++;
                    if (hidden && hiddenPolicy == DrawingHiddenLinePolicy.VisibleOnly) { Flush(); continue; }
                    var start = Lerp(a, b, from); var end = Lerp(a, b, to);
                    var kind = hidden ? DrawingPrimitiveKind.Hidden : edge.Curved ? DrawingPrimitiveKind.Silhouette : DrawingPrimitiveKind.Visible;
                    if (runKind != kind) { Flush(); runKind = kind; run.Add(start); }
                    // A split inside one straight segment adds no shape: extend the last point instead of adding one.
                    else if (interiorEnd) run[^1] = end;
                    if (runKind == kind && run[^1] != end) run.Add(end);
                    interiorEnd = to < 1 - 1e-12;
                }
            }
            Flush();
        }
        }

        var geometryBounds = new DrawingRect(originX + minX * scale, originY - maxY * scale, modelWidth * scale, modelHeight * scale);
        var anchors = pmi.ToDictionary(reference => reference, _ => geometryBounds.Center, StringComparer.Ordinal);
        var evidenceSource = string.Join("|", primitives.Select(item => item.StableId)) + $"|{candidateSegments}|{visibleSegments}|{hiddenSegments}|{splitPoints}|{triangles.Count}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidenceSource))).ToLowerInvariant();
        var evidence = new DrawingVisibilityEvidenceIr(candidateSegments, visibleSegments, hiddenSegments, splitPoints, triangles.Count,
            unsupported.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            hiddenPolicy == DrawingHiddenLinePolicy.Wireframe ? "Trimmed BRep curves with optional sampled surface isolines; rear edges retained; no hidden-line classification."
                : "Exact B-rep edge intervals classified against deterministic bounded face triangles; depth epsilon 1e-5 mm.", hash);
        return new DrawingViewIr(name, projection, hiddenPolicy, new(direction.X, direction.Y), directionSource, viewport, geometryBounds, scale,
            primitives, anchors, pmi, location, evidence)
        { Camera = new([right.X, right.Y, right.Z], [up.X, up.Y, up.Z], originX, originY) };
    }

    /// <summary>Joins contour pieces that share an end point into polylines, in a deterministic order.</summary>
    private static List<List<Point3D>> ChainPieces(List<(Point3D A, Point3D B)> pieces)
    {
        static (long, long, long) Key(Point3D p) => ((long)Math.Round(p.X * 1e4), (long)Math.Round(p.Y * 1e4), (long)Math.Round(p.Z * 1e4));
        var at = new Dictionary<(long, long, long), List<int>>();
        for (var i = 0; i < pieces.Count; i++)
            foreach (var key in new[] { Key(pieces[i].A), Key(pieces[i].B) })
                (at.TryGetValue(key, out var list) ? list : at[key] = []).Add(i);
        var used = new bool[pieces.Count]; var chains = new List<List<Point3D>>();
        int? Next(Point3D end) => at[Key(end)].Where(i => !used[i]).Select(i => (int?)i).FirstOrDefault();
        for (var start = 0; start < pieces.Count; start++)
        {
            if (used[start]) continue;
            used[start] = true; var chain = new LinkedList<Point3D>([pieces[start].A, pieces[start].B]);
            for (var forward = 0; forward < 2; forward++)
                while (true)
                {
                    var end = forward == 0 ? chain.Last!.Value : chain.First!.Value;
                    if (Next(end) is not { } i) break;
                    used[i] = true;
                    var other = Key(pieces[i].A) == Key(end) ? pieces[i].B : pieces[i].A;
                    if (forward == 0) chain.AddLast(other); else chain.AddFirst(other);
                }
            chains.Add([.. chain]);
        }
        return chains;
    }

    /// <summary>Uniform grid over the triangles' projected bounds, so a segment meets only nearby triangles.</summary>
    private sealed class TriangleGrid
    {
        private readonly List<Triangle> triangles; private readonly List<int>[] cells; private readonly int n;
        private readonly double minX, minY, cellW, cellH;
        private readonly (double X0, double Y0, double X1, double Y1)[] bounds;
        public TriangleGrid(List<Triangle> triangles)
        {
            this.triangles = triangles;
            bounds = triangles.Select(t => (Math.Min(t.A.X, Math.Min(t.B.X, t.C.X)), Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y)),
                Math.Max(t.A.X, Math.Max(t.B.X, t.C.X)), Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y)))).ToArray();
            n = Math.Clamp((int)Math.Sqrt(triangles.Count / 2.0), 1, 256);
            minX = bounds.Length == 0 ? 0 : bounds.Min(b => b.X0); minY = bounds.Length == 0 ? 0 : bounds.Min(b => b.Y0);
            var maxX = bounds.Length == 0 ? 1 : bounds.Max(b => b.X1); var maxY = bounds.Length == 0 ? 1 : bounds.Max(b => b.Y1);
            cellW = Math.Max(1e-9, (maxX - minX) / n); cellH = Math.Max(1e-9, (maxY - minY) / n);
            cells = Enumerable.Range(0, n * n).Select(_ => new List<int>()).ToArray();
            for (var i = 0; i < bounds.Length; i++)
            {
                var (cx0, cy0, cx1, cy1) = Range(bounds[i].X0, bounds[i].Y0, bounds[i].X1, bounds[i].Y1);
                for (var y = cy0; y <= cy1; y++) for (var x = cx0; x <= cx1; x++) cells[y * n + x].Add(i);
            }
        }
        private (int, int, int, int) Range(double x0, double y0, double x1, double y1) =>
            (Cell(x0 - 1e-7, minX, cellW), Cell(y0 - 1e-7, minY, cellH), Cell(x1 + 1e-7, minX, cellW), Cell(y1 + 1e-7, minY, cellH));
        private int Cell(double v, double origin, double size) => Math.Clamp((int)Math.Floor((v - origin) / size), 0, n - 1);
        public List<Triangle> Near(double x0, double y0, double x1, double y1)
        {
            var (cx0, cy0, cx1, cy1) = Range(x0, y0, x1, y1);
            var found = new HashSet<int>();
            for (var y = cy0; y <= cy1; y++) for (var x = cx0; x <= cx1; x++) found.UnionWith(cells[y * n + x]);
            return found.Where(i => bounds[i].X1 >= x0 - 1e-7 && bounds[i].X0 <= x1 + 1e-7 && bounds[i].Y1 >= y0 - 1e-7 && bounds[i].Y0 <= y1 + 1e-7)
                .Order().Select(i => triangles[i]).ToList();
        }
    }

    private static void AddIntersection(CameraPoint a, CameraPoint b, CameraPoint c, CameraPoint d, List<double> values)
    {
        var rx = b.X - a.X; var ry = b.Y - a.Y; var sx = d.X - c.X; var sy = d.Y - c.Y;
        var denominator = rx * sy - ry * sx;
        if (Math.Abs(denominator) < 1e-12) return;
        var qx = c.X - a.X; var qy = c.Y - a.Y;
        var t = (qx * sy - qy * sx) / denominator;
        var u = (qx * ry - qy * rx) / denominator;
        if (t > 1e-8 && t < 1 - 1e-8 && u >= -1e-8 && u <= 1 + 1e-8) values.Add(t);
    }

    private static bool Contains(Triangle triangle, double x, double y, out double depth)
    {
        var area = Cross(triangle.A, triangle.B, triangle.C);
        var p = new CameraPoint(x, y, 0);
        var wa = Cross(p, triangle.B, triangle.C) / area;
        var wb = Cross(triangle.A, p, triangle.C) / area;
        var wc = 1 - wa - wb;
        depth = wa * triangle.A.Depth + wb * triangle.B.Depth + wc * triangle.C.Depth;
        return wa >= -1e-9 && wb >= -1e-9 && wc >= -1e-9;
    }

    private static double Cross(CameraPoint a, CameraPoint b, CameraPoint c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
    private static CameraPoint Lerp(CameraPoint a, CameraPoint b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Depth + (b.Depth - a.Depth) * t);
    private static Vector3D Normalize(Vector3D vector) { var length = Math.Sqrt(vector.Dot(vector)); if (length < 1e-12) throw new InvalidOperationException("drawing-view-direction-zero"); return vector * (1 / length); }

    private static bool IsInternalSupportEdge(BrepBody body, Aetheris.Kernel.Core.Topology.EdgeId edgeId)
    {
        var loopIds = body.Topology.Coedges.Where(coedge => coedge.EdgeId == edgeId).Select(coedge => coedge.LoopId).ToHashSet();
        var faces = body.Topology.Faces.Where(face => face.LoopIds.Any(loopIds.Contains)).ToArray();
        if (faces.Length != 2) return false;
        if (!body.TryGetFaceSurfaceGeometry(faces[0].Id, out var first) || !body.TryGetFaceSurfaceGeometry(faces[1].Id, out var second)) return false;
        if (first?.Plane is { } a && second?.Plane is { } b)
            return Math.Abs(Math.Abs(a.Normal.ToVector().Dot(b.Normal.ToVector())) - 1) < 1e-8
                && Math.Abs((b.Origin - a.Origin).Dot(a.Normal.ToVector())) < 1e-7;
        if (first?.Cylinder is { } ca && second?.Cylinder is { } cb)
        {
            var axis = ca.Axis.ToVector(); var delta = cb.Origin - ca.Origin;
            return Math.Abs(ca.Radius - cb.Radius) < 1e-7 && axis.Cross(cb.Axis.ToVector()).Length < 1e-8
                && (delta - axis * delta.Dot(axis)).Length < 1e-7;
        }
        if (first?.Cone is { } sa && second?.Cone is { } sb)
            return Math.Abs(sa.SemiAngleRadians - sb.SemiAngleRadians) < 1e-8 && (sa.Apex - sb.Apex).Length < 1e-7
                && sa.Axis.ToVector().Dot(sb.Axis.ToVector()) > 1 - 1e-8;
        return false;
    }
}
