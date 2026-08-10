using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Firmament.Drawing;

internal sealed record DrawingProjectionBody(string OccurrenceIdentity, string DefinitionIdentity, BrepBody Body);

/// <summary>Exact B-rep edges plus a bounded display-mesh occlusion oracle. The mesh decides
/// only visibility intervals; it never replaces the authoritative edge geometry.</summary>
internal static class DrawingProjectionEngine
{
    private readonly record struct CameraPoint(double X, double Y, double Depth);
    private sealed record RawEdge(string Id, string Occurrence, string Definition, IReadOnlyList<CameraPoint> Points, bool Curved);
    private sealed record Triangle(CameraPoint A, CameraPoint B, CameraPoint C, string Identity);

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
            foreach (var edge in body.Topology.Edges.OrderBy(edge => edge.Id.Value))
            {
                if (IsCoplanarInternalEdge(body, edge.Id)) continue;
                if (!body.TryGetVertexPoint(edge.StartVertexId, out var start) || !body.TryGetVertexPoint(edge.EndVertexId, out var end)) continue;
                IReadOnlyList<Point3D> points;
                var curved = false;
                if (body.TryGetEdgeCurveGeometry(edge.Id, out var curve) && curve?.Kind == CurveGeometryKind.Circle3 && curve.Circle3 is { } circle)
                { points = Enumerable.Range(0, 65).Select(i => circle.Evaluate(i * Math.Tau / 64d)).ToArray(); curved = true; }
                else if (body.TryGetEdgeCurveGeometry(edge.Id, out curve) && curve?.Kind == CurveGeometryKind.Ellipse3 && curve.Ellipse3 is { } ellipse)
                { points = Enumerable.Range(0, 65).Select(i => ellipse.Evaluate(i * Math.Tau / 64d)).ToArray(); curved = true; }
                else points = [start, end];
                raw.Add(new($"edge:{edge.Id.Value}", projectionBody.OccurrenceIdentity, projectionBody.DefinitionIdentity, points.Select(Camera).ToArray(), curved));
            }

            // The admitted tessellator is the deterministic support-family bound. Avoid a
            // wall-clock cutoff here: timeout-dependent partial patches would make DrawingIR
            // depend on machine load and violate reproducible HLR evidence.
            var meshResult = BrepDisplayTessellator.Tessellate(body);
            var mesh = meshResult.IsSuccess ? meshResult.Value : new DisplayTessellationResult([], [], meshResult.Diagnostics.Select(diagnostic =>
                new DisplayFaceMaterializationDiagnostic(null, null, "Tessellate", diagnostic.Source ?? diagnostic.Code.ToString(), diagnostic.Message)).ToArray());
            foreach (var diagnostic in mesh.FaceDiagnostics ?? [])
                unsupported.Add($"{projectionBody.OccurrenceIdentity}:face:{diagnostic.FaceId?.Value.ToString(CultureInfo.InvariantCulture) ?? "?"}:{diagnostic.SurfaceKind ?? diagnostic.Code}");
            foreach (var patch in mesh.FacePatches.OrderBy(patch => patch.FaceId.Value))
            for (var index = 0; index + 2 < patch.TriangleIndices.Count; index += 3)
            {
                var a = Camera(patch.Positions[patch.TriangleIndices[index]]);
                var b = Camera(patch.Positions[patch.TriangleIndices[index + 1]]);
                var c = Camera(patch.Positions[patch.TriangleIndices[index + 2]]);
                if (Math.Abs(Cross(a, b, c)) > 1e-12)
                    triangles.Add(new(a, b, c, $"{projectionBody.OccurrenceIdentity}:face:{patch.FaceId.Value}:triangle:{index / 3}"));
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
        foreach (var edge in raw.OrderBy(edge => edge.Occurrence, StringComparer.Ordinal).ThenBy(edge => edge.Id, StringComparer.Ordinal))
        for (var segment = 1; segment < edge.Points.Count; segment++)
        {
            candidateSegments++;
            var a = edge.Points[segment - 1]; var b = edge.Points[segment];
            var cuts = new List<double> { 0, 1 };
            foreach (var triangle in triangles)
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
                var hidden = triangles.Any(triangle => Contains(triangle, midpoint.X, midpoint.Y, out var depth) && depth > midpoint.Depth + 1e-5);
                if (hidden) hiddenSegments++; else visibleSegments++;
                if (hidden && hiddenPolicy == DrawingHiddenLinePolicy.VisibleOnly) continue;
                var start = Lerp(a, b, from); var end = Lerp(a, b, to);
                var kind = hidden ? DrawingPrimitiveKind.Hidden : edge.Curved ? DrawingPrimitiveKind.Silhouette : DrawingPrimitiveKind.Visible;
                var stable = $"{edge.Occurrence}:{edge.Id}:segment:{segment - 1}:interval:{interval - 1}:{kind}";
                primitives.Add(new(stable, kind, [Page(start), Page(end)], edge.Occurrence, (start.Depth + end.Depth) / 2,
                    edge.Definition, edge.Id));
            }
        }

        var geometryBounds = new DrawingRect(originX + minX * scale, originY - maxY * scale, modelWidth * scale, modelHeight * scale);
        var anchors = pmi.ToDictionary(reference => reference, _ => geometryBounds.Center, StringComparer.Ordinal);
        var evidenceSource = string.Join("|", primitives.Select(item => item.StableId)) + $"|{candidateSegments}|{visibleSegments}|{hiddenSegments}|{splitPoints}|{triangles.Count}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidenceSource))).ToLowerInvariant();
        var evidence = new DrawingVisibilityEvidenceIr(candidateSegments, visibleSegments, hiddenSegments, splitPoints, triangles.Count,
            unsupported.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            "Exact B-rep edge intervals classified against deterministic bounded face triangles; depth epsilon 1e-5 mm.", hash);
        return new(name, projection, hiddenPolicy, new(direction.X, direction.Y), directionSource, viewport, geometryBounds, scale,
            primitives, anchors, pmi, location, evidence);
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

    private static bool IsCoplanarInternalEdge(BrepBody body, Aetheris.Kernel.Core.Topology.EdgeId edgeId)
    {
        var loopIds = body.Topology.Coedges.Where(coedge => coedge.EdgeId == edgeId).Select(coedge => coedge.LoopId).ToHashSet();
        var faces = body.Topology.Faces.Where(face => face.LoopIds.Any(loopIds.Contains)).ToArray();
        if (faces.Length != 2) return false;
        if (!body.TryGetFaceSurfaceGeometry(faces[0].Id, out var first) || !body.TryGetFaceSurfaceGeometry(faces[1].Id, out var second)
            || first?.Plane is not { } a || second?.Plane is not { } b) return false;
        var na = a.Normal.ToVector(); var nb = b.Normal.ToVector();
        return Math.Abs(Math.Abs(na.Dot(nb)) - 1) < 1e-8 && Math.Abs((b.Origin - a.Origin).Dot(na)) < 1e-7;
    }
}
