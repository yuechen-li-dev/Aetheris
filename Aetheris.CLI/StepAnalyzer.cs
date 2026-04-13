using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.CLI;

public static class StepAnalyzer
{
    public static AnalyzeResult Analyze(string stepPath, int? faceId = null, int? edgeId = null, int? vertexId = null)
    {
        var fullPath = Path.GetFullPath(stepPath);
        var import = Step242Importer.ImportBody(File.ReadAllText(fullPath));
        if (!import.IsSuccess)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, import.Diagnostics.Select(d => d.Message)));
        }

        return AnalyzeImportedBody(import.Value, fullPath, faceId, edgeId, vertexId);
    }

    public static AnalyzeResult AnalyzeImportedBody(BrepBody body, string stepPath, int? faceId = null, int? edgeId = null, int? vertexId = null)
    {
        var notes = new List<string>();

        var summary = BuildSummary(body, notes);
        var face = faceId.HasValue ? BuildFaceDetail(body, new FaceId(faceId.Value), notes) : null;
        var edge = edgeId.HasValue ? BuildEdgeDetail(body, new EdgeId(edgeId.Value), notes) : null;
        var vertex = vertexId.HasValue ? BuildVertexDetail(body, new VertexId(vertexId.Value), notes) : null;

        return new AnalyzeResult(stepPath, summary, face, edge, vertex, notes);
    }

    private static AnalyzeSummary BuildSummary(BrepBody body, ICollection<string> notes)
    {
        var topology = body.Topology;
        var surfaceFamilies = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["plane"] = 0,
            ["cylinder"] = 0,
            ["cone"] = 0,
            ["sphere"] = 0,
            ["torus"] = 0,
            ["bspline"] = 0,
            ["other"] = 0
        };

        foreach (var face in topology.Faces)
        {
            if (!body.TryGetFaceSurface(face.Id, out var surface) || surface is null)
            {
                surfaceFamilies["other"]++;
                continue;
            }

            switch (surface.Kind)
            {
                case SurfaceGeometryKind.Plane: surfaceFamilies["plane"]++; break;
                case SurfaceGeometryKind.Cylinder: surfaceFamilies["cylinder"]++; break;
                case SurfaceGeometryKind.Cone: surfaceFamilies["cone"]++; break;
                case SurfaceGeometryKind.Sphere: surfaceFamilies["sphere"]++; break;
                case SurfaceGeometryKind.Torus: surfaceFamilies["torus"]++; break;
                case SurfaceGeometryKind.BSplineSurfaceWithKnots: surfaceFamilies["bspline"]++; break;
                default: surfaceFamilies["other"]++; break;
            }
        }

        var bbox = TryComputeBodyBoundingBox(body);
        if (bbox is null)
        {
            notes.Add("Bounding box unavailable because one or more vertices did not expose XYZ coordinates.");
        }

        var edgeUseCounts = BuildEdgeFaceIncidenceCounts(body);
        var leakyEdges = edgeUseCounts.Count(kvp => kvp.Value == 1);
        var nonManifoldEdges = edgeUseCounts.Count(kvp => kvp.Value != 2);
        var structural = nonManifoldEdges == 0 ? "enclosed-manifold" : (leakyEdges > 0 ? "leaky-or-open" : "non-manifold");
        var basis = "derived from imported topology edge-to-face coedge incidence counts";

        return new AnalyzeSummary(
            topology.Bodies.Count(),
            topology.Shells.Count(),
            topology.Faces.Count(),
            topology.Edges.Count(),
            topology.Vertices.Count(),
            bbox,
            structural,
            surfaceFamilies,
            basis,
            "mm",
            "assumed; STEP import length units not yet preserved",
            BuildIdRange(topology.Faces.Select(f => f.Id.Value)),
            BuildIdRange(topology.Edges.Select(e => e.Id.Value)),
            BuildIdRange(topology.Vertices.Select(v => v.Id.Value)));
    }

    private static FaceDetail BuildFaceDetail(BrepBody body, FaceId faceId, ICollection<string> notes)
    {
        if (!body.Topology.TryGetFace(faceId, out var face) || face is null)
        {
            throw new InvalidOperationException($"Face '{faceId.Value}' was not found.");
        }

        var edgeIds = body.GetEdges(faceId).Select(id => id.Value).OrderBy(id => id).ToArray();
        var faceVertices = body.GetEdges(faceId)
            .SelectMany(edge => body.GetVertices(edge))
            .Distinct()
            .Select(v => body.TryGetVertexPoint(v, out var p) ? (Point3D?)p : null)
            .Where(p => p.HasValue)
            .Select(p => p!.Value)
            .ToArray();

        BoundingBox3D? bbox = null;
        Point3D? rep = null;
        if (faceVertices.Length > 0)
        {
            bbox = ComputeBoundingBox(faceVertices);
            rep = new Point3D(faceVertices.Average(v => v.X), faceVertices.Average(v => v.Y), faceVertices.Average(v => v.Z));
        }
        else
        {
            notes.Add($"Face {faceId.Value} has no resolved vertex coordinates for bounds/representative point.");
        }

        if (!body.TryGetFaceSurface(faceId, out var surface) || surface is null)
        {
            return new FaceDetail(faceId.Value, null, "binding-missing", bbox, rep, null, null, null, null, null, null, null, null, null, edgeIds);
        }

        Point3D? anchor = null;
        Point3D? apex = null;
        Vector3D? normal = null;
        Vector3D? axis = null;
        double? radius = null;
        double? placementRadius = null;
        double? majorRadius = null;
        double? minorRadius = null;
        double? semiAngle = null;

        if (surface.Plane is { } plane)
        {
            anchor = plane.Origin;
            normal = plane.Normal.ToVector();
        }

        if (surface.Cylinder is { } cylinder)
        {
            anchor = cylinder.Origin;
            axis = cylinder.Axis.ToVector();
            radius = cylinder.Radius;
        }

        if (surface.Cone is { } cone)
        {
            anchor = cone.PlacementOrigin;
            apex = cone.Apex;
            axis = cone.Axis.ToVector();
            semiAngle = cone.SemiAngleRadians;
            placementRadius = cone.PlacementRadius;
        }

        if (surface.Sphere is { } sphere)
        {
            anchor = sphere.Center;
            radius = sphere.Radius;
            notes.Add($"Face {faceId.Value} is spherical; axis omitted because spheres have no intrinsic axis.");
        }

        if (surface.Torus is { } torus)
        {
            anchor = torus.Center;
            axis = torus.Axis.ToVector();
            majorRadius = torus.MajorRadius;
            minorRadius = torus.MinorRadius;
        }

        return new FaceDetail(faceId.Value, surface.Kind.ToString(), "bound", bbox, rep, anchor, apex, normal, axis, radius, placementRadius, majorRadius, minorRadius, semiAngle, edgeIds);
    }

    private static EdgeDetail BuildEdgeDetail(BrepBody body, EdgeId edgeId, ICollection<string> notes)
    {
        if (!body.Topology.TryGetEdge(edgeId, out var edge) || edge is null)
        {
            throw new InvalidOperationException($"Edge '{edgeId.Value}' was not found.");
        }

        var curveType = "unknown";
        double? parameterRange = null;
        double? arcLength = null;
        var arcLengthStatus = "unavailable";

        if (body.Bindings.TryGetEdgeBinding(edgeId, out var binding))
        {
            curveType = binding.TrimInterval is null ? "untrimmed" : "trimmed";
            parameterRange = binding.TrimInterval is { } interval ? interval.End - interval.Start : null;

            if (body.Geometry.TryGetCurve(binding.CurveGeometryId, out var curve) && curve is not null)
            {
                curveType = curve.Kind == CurveGeometryKind.Unsupported
                    ? $"Unsupported({curve.UnsupportedKind ?? "unknown"})"
                    : curve.Kind.ToString();

                if (binding.TrimInterval is { } trim)
                {
                    switch (curve.Kind)
                    {
                        case CurveGeometryKind.Line3:
                            arcLength = double.Abs(trim.End - trim.Start);
                            arcLengthStatus = "computed";
                            break;
                        case CurveGeometryKind.Circle3 when curve.Circle3 is { } circle:
                            arcLength = circle.Radius * double.Abs(trim.End - trim.Start);
                            arcLengthStatus = "computed";
                            break;
                        default:
                            arcLengthStatus = "unsupported-for-curve-kind";
                            break;
                    }
                }
                else
                {
                    arcLengthStatus = "unavailable-no-trim-interval";
                }
            }
            else
            {
                arcLengthStatus = "unavailable-curve-missing";
            }
        }
        else
        {
            notes.Add($"Edge {edgeId.Value} has no curve binding, so curve-type and length are limited.");
            arcLengthStatus = "unavailable-binding-missing";
        }

        Point3D? startPoint = body.TryGetVertexPoint(edge.StartVertexId, out var start) ? start : null;
        Point3D? endPoint = body.TryGetVertexPoint(edge.EndVertexId, out var end) ? end : null;
        var adjacentFaces = BuildEdgeFaceAdjacency(body)
            .TryGetValue(edgeId, out var faces)
            ? faces.Select(id => id.Value).OrderBy(v => v).ToArray()
            : [];

        return new EdgeDetail(edgeId.Value, curveType, edge.StartVertexId.Value, startPoint, edge.EndVertexId.Value, endPoint, adjacentFaces, parameterRange, arcLength, arcLengthStatus);
    }

    private static VertexDetail BuildVertexDetail(BrepBody body, VertexId vertexId, ICollection<string> notes)
    {
        if (!body.Topology.TryGetVertex(vertexId, out _))
        {
            throw new InvalidOperationException($"Vertex '{vertexId.Value}' was not found.");
        }

        Point3D? position = body.TryGetVertexPoint(vertexId, out var point) ? point : null;
        if (position is null)
        {
            notes.Add($"Vertex {vertexId.Value} coordinates are unavailable in imported body.");
        }

        var incidentEdges = body.Topology.Edges
            .Where(edge => edge.StartVertexId == vertexId || edge.EndVertexId == vertexId)
            .Select(edge => edge.Id.Value)
            .OrderBy(id => id)
            .ToArray();

        return new VertexDetail(vertexId.Value, position, incidentEdges);
    }

    private static IdRangeSummary BuildIdRange(IEnumerable<int> ids)
    {
        var sorted = ids.OrderBy(id => id).ToArray();
        if (sorted.Length == 0)
        {
            return new IdRangeSummary(0, 0, 0, true);
        }

        var contiguous = true;
        for (var index = 1; index < sorted.Length; index++)
        {
            if (sorted[index] != sorted[index - 1] + 1)
            {
                contiguous = false;
                break;
            }
        }

        return new IdRangeSummary(sorted[0], sorted[^1], sorted.Length, contiguous);
    }

    private static BoundingBox3D? TryComputeBodyBoundingBox(BrepBody body)
    {
        var points = body.Topology.Vertices
            .Select(v => body.TryGetVertexPoint(v.Id, out var point) ? (Point3D?)point : null)
            .Where(p => p.HasValue)
            .Select(p => p!.Value)
            .ToArray();

        return points.Length == 0 ? null : ComputeBoundingBox(points);
    }

    private static BoundingBox3D ComputeBoundingBox(IReadOnlyList<Point3D> points)
    {
        var minX = points.Min(p => p.X);
        var minY = points.Min(p => p.Y);
        var minZ = points.Min(p => p.Z);
        var maxX = points.Max(p => p.X);
        var maxY = points.Max(p => p.Y);
        var maxZ = points.Max(p => p.Z);
        return new BoundingBox3D(new Point3D(minX, minY, minZ), new Point3D(maxX, maxY, maxZ));
    }

    private static Dictionary<EdgeId, HashSet<FaceId>> BuildEdgeFaceAdjacency(BrepBody body)
    {
        var edgeFaces = new Dictionary<EdgeId, HashSet<FaceId>>();

        foreach (var face in body.Topology.Faces)
        {
            foreach (var loopId in face.LoopIds)
            {
                if (!body.Topology.TryGetLoop(loopId, out var loop) || loop is null)
                {
                    continue;
                }

                foreach (var coedgeId in loop.CoedgeIds)
                {
                    if (!body.Topology.TryGetCoedge(coedgeId, out var coedge) || coedge is null)
                    {
                        continue;
                    }

                    if (!edgeFaces.TryGetValue(coedge.EdgeId, out var faces))
                    {
                        faces = [];
                        edgeFaces.Add(coedge.EdgeId, faces);
                    }

                    faces.Add(face.Id);
                }
            }
        }

        return edgeFaces;
    }

    private static Dictionary<EdgeId, int> BuildEdgeFaceIncidenceCounts(BrepBody body)
    {
        var edgeCounts = new Dictionary<EdgeId, int>();

        foreach (var face in body.Topology.Faces)
        {
            foreach (var loopId in face.LoopIds)
            {
                if (!body.Topology.TryGetLoop(loopId, out var loop) || loop is null)
                {
                    continue;
                }

                foreach (var coedgeId in loop.CoedgeIds)
                {
                    if (!body.Topology.TryGetCoedge(coedgeId, out var coedge) || coedge is null)
                    {
                        continue;
                    }

                    edgeCounts.TryGetValue(coedge.EdgeId, out var count);
                    edgeCounts[coedge.EdgeId] = count + 1;
                }
            }
        }

        return edgeCounts;
    }
}
