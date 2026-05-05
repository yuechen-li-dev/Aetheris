using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.CLI;

public static class StepAnalyzer
{

public sealed record VolumeBoundingBox(Point3D Min, Point3D Max);
public sealed record VolumeAnalysisResult(
    string InputPath,
    bool Success,
    double Volume,
    string LengthUnit,
    string VolumeUnit,
    VolumeBoundingBox BoundingBox,
    string Method,
    bool Exact,
    bool Approximate,
    int? Resolution,
    Point3D? VoxelSize,
    int? OccupiedCount,
    int? TotalCount,
    IReadOnlyList<string> Notes);

    public static AnalyzeResult Analyze(string stepPath, int? faceId = null, int? edgeId = null, int? vertexId = null)
    {
        var (fullPath, body) = ImportStepBody(stepPath);
        return AnalyzeImportedBody(body, fullPath, faceId, edgeId, vertexId);
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

    public static OrthographicMapResult AnalyzeMap(string stepPath, OrthographicView view, int rows, int cols)
    {
        var (fullPath, body) = ImportStepBody(stepPath);
        return AnalyzeImportedBodyMap(body, fullPath, view, rows, cols);
    }

    public static SectionAnalysisResult AnalyzeSection(string stepPath, SectionPlaneFamily planeFamily, double offset)
    {
        var (fullPath, body) = ImportStepBody(stepPath);
        return AnalyzeImportedBodySection(body, fullPath, planeFamily, offset);
    }

    public static VolumeAnalysisResult AnalyzeVolume(string stepPath, bool approximate = false, int? resolution = null)
    {
        var (fullPath, body) = ImportStepBody(stepPath);
        var notes = new List<string>();
        var bbox = TryComputeBodyBoundingBox(body) ?? throw new InvalidOperationException("Volume analysis requires body vertex coordinates for bounding box reporting.");
        var shells = body.ShellRepresentation;
        if (shells is null)
        {
            throw new InvalidOperationException("Volume analysis requires explicit shell-role representation.");
        }

        if (approximate)
        {
            if (!resolution.HasValue)
            {
                throw new InvalidOperationException("Approximate volume mode requires explicit --resolution <N>.");
            }

            return ComputeApproximateVoxelVolume(stepPath, body, bbox, resolution.Value);
        }

        var sphereFaces = body.Topology.Faces.Where(f => body.TryGetFaceSurface(f.Id, out var sf) && sf?.Sphere is not null).ToArray();
        if (body.Topology.Faces.Count() == 1 && sphereFaces.Length == 1)
        {
            var r = sphereFaces[0];
            body.TryGetFaceSurface(r.Id, out var sph);
            var radius = sph!.Sphere!.Value.Radius;
            var vol = 4d/3d*double.Pi*radius*radius*radius;
            notes.Add("Exact analytic sphere volume from spherical face radius.");
            return new VolumeAnalysisResult(stepPath, true, vol, "model-unit", "model-unit^3", new VolumeBoundingBox(bbox.Min, bbox.Max), "analytic-sphere", true, false, null, null, null, null, notes);
        }

        var cylFaces = body.Topology.Faces.Where(f => body.TryGetFaceSurface(f.Id, out var sf) && sf?.Cylinder is not null).ToArray();
        if (cylFaces.Length == 1 && body.Topology.Faces.Count() == 3)
        {
            body.TryGetFaceSurface(cylFaces[0].Id, out var cs);
            var cyl = cs!.Cylinder!.Value;
            var axis = cyl.Axis.ToVector();
            var min = double.PositiveInfinity; var max = double.NegativeInfinity;
            foreach (var v in body.Topology.Vertices)
            {
                if (!body.TryGetVertexPoint(v.Id, out var pt)) continue;
                var t = (pt - cyl.Origin).Dot(axis);
                min = double.Min(min,t); max = double.Max(max,t);
            }
            if (!double.IsFinite(min) || !double.IsFinite(max) || max <= min)
                throw new InvalidOperationException("Cylinder volume analysis could not resolve finite axial span from vertices.");
            var h=max-min; var vol=double.Pi*cyl.Radius*cyl.Radius*h;
            notes.Add("Exact analytic cylinder volume from cylinder radius and cap-span derived from bound vertices.");
            return new VolumeAnalysisResult(stepPath, true, vol, "model-unit", "model-unit^3", new VolumeBoundingBox(bbox.Min, bbox.Max), "analytic-cylinder", true, false, null, null, null, null, notes);
        }

        var shellVolume = TryComputePlanarClosedShellVolume(body, shells, out var planarVolume, out var planarFailureReason);
        if (shellVolume)
        {
            notes.Add("Exact closed-shell volume from oriented planar-face triangulation and signed tetrahedral accumulation.");
            return new VolumeAnalysisResult(stepPath, true, planarVolume, "model-unit", "model-unit^3", new VolumeBoundingBox(bbox.Min, bbox.Max), "planar-closed-shell", true, false, null, null, null, null, notes);
        }

        throw new InvalidOperationException(planarFailureReason ?? "Volume analysis currently supports canonical sphere, single-lateral-face cylinder, and enclosed planar closed-shell bodies only.");
    }

    private static VolumeAnalysisResult ComputeApproximateVoxelVolume(string stepPath, BrepBody body, BoundingBox3D bbox, int resolution)
    {
        if (resolution is < 8 or > 512)
        {
            throw new InvalidOperationException("Approximate volume resolution must be an integer between 8 and 512.");
        }

        var dx = bbox.Max.X - bbox.Min.X;
        var dy = bbox.Max.Y - bbox.Min.Y;
        var dz = bbox.Max.Z - bbox.Min.Z;
        if (dx <= 0d || dy <= 0d || dz <= 0d)
        {
            throw new InvalidOperationException("Approximate volume requires a non-degenerate body bounding box.");
        }

        var longest = double.Max(dx, double.Max(dy, dz));
        var nx = int.Max(1, (int)double.Round(resolution * (dx / longest), MidpointRounding.AwayFromZero));
        var ny = int.Max(1, (int)double.Round(resolution * (dy / longest), MidpointRounding.AwayFromZero));
        var nz = int.Max(1, (int)double.Round(resolution * (dz / longest), MidpointRounding.AwayFromZero));
        var cell = new Point3D(dx / nx, dy / ny, dz / nz);
        var cellVolume = cell.X * cell.Y * cell.Z;
        var total = nx * ny * nz;
        var occupied = 0;

        for (var ix = 0; ix < nx; ix++)
        for (var iy = 0; iy < ny; iy++)
        for (var iz = 0; iz < nz; iz++)
        {
            var sample = new Point3D(
                bbox.Min.X + (ix + 0.5d) * cell.X,
                bbox.Min.Y + (iy + 0.5d) * cell.Y,
                bbox.Min.Z + (iz + 0.5d) * cell.Z);
            var containment = BrepSpatialQueries.ClassifyPoint(body, sample);
            if (!containment.IsSuccess)
            {
                throw new InvalidOperationException($"Approximate volume classification is unsupported for this body ({containment.Diagnostics.FirstOrDefault().Message}).");
            }

            if (containment.Value == PointContainment.Unknown)
            {
                throw new InvalidOperationException("Approximate volume classification is unsupported for this body (point containment returned unknown).");
            }

            if (containment.Value is PointContainment.Inside or PointContainment.Boundary)
            {
                occupied++;
            }
        }

        var volume = occupied * cellVolume;
        return new VolumeAnalysisResult(
            stepPath,
            true,
            volume,
            "model-unit",
            "model-unit^3",
            new VolumeBoundingBox(bbox.Min, bbox.Max),
            "voxel-approximation",
            false,
            true,
            resolution,
            cell,
            occupied,
            total,
            new[]
            {
                "Approximate volume mode: deterministic center-point voxel sampling over the body axis-aligned bounding box.",
                "Resolution means samples along the longest bounding-box axis; other axis counts are derived proportionally.",
                "Estimated result is not exact and should be used for comparison/localization only."
            });
    }

    private static bool TryComputePlanarClosedShellVolume(BrepBody body, BrepBodyShellRepresentation shells, out double volume, out string? failureReason)
    {
        volume = 0d;
        failureReason = null;

        if (body.Topology.Bodies.Count() != 1)
        {
            failureReason = "Volume analysis requires a single-body enclosed shell representation (assembly-like/multi-root STEP is unsupported for volume).";
            return false;
        }

        var shellIds = shells.OrderedShellIds;
        if (shellIds.Count == 0)
        {
            failureReason = "Volume analysis requires at least one shell in shell representation.";
            return false;
        }

        var totalSigned = 0d;
        foreach (var shellId in shellIds)
        {
            if (!body.Topology.TryGetShell(shellId, out var shell) || shell is null)
            {
                failureReason = $"Volume analysis shell {shellId.Value} is missing from topology.";
                return false;
            }

            foreach (var faceId in shell.FaceIds)
            {
                if (!body.Topology.TryGetFace(faceId, out var face) || face is null)
                {
                    failureReason = $"Volume analysis face {faceId.Value} is missing from topology.";
                    return false;
                }

                if (!body.TryGetFaceSurface(faceId, out var surface) || surface is null)
                {
                    failureReason = $"Volume analysis face {faceId.Value} is missing bound surface geometry.";
                    return false;
                }

                if (surface.Kind != SurfaceGeometryKind.Plane || surface.Plane is not PlaneSurface plane)
                {
                    failureReason = $"Volume analysis encountered unsupported non-planar face {faceId.Value} ({surface.Kind}); curved trimmed-shell integration remains deferred.";
                    return false;
                }

                var faceSignedVolume = 0d;
                foreach (var loopId in face.LoopIds)
                {
                    var loopVertices = TryBuildOrientedLoopVertices(body, loopId, out var loopFailureReason);
                    if (loopVertices is null)
                    {
                        failureReason = loopFailureReason;
                        return false;
                    }

                    if (loopVertices.Count < 3)
                    {
                        continue;
                    }

                    var triangles = TriangulateLoopOnPlane(loopVertices, plane, out var triangulationFailureReason);
                    if (triangles is null)
                    {
                        failureReason = triangulationFailureReason;
                        return false;
                    }

                    foreach (var triangle in triangles)
                    {
                        faceSignedVolume += SignedTetraVolume(triangle.A, triangle.B, triangle.C);
                    }
                }

                totalSigned += faceSignedVolume;
            }
        }

        volume = double.Abs(totalSigned);
        return true;
    }

    private static IReadOnlyList<Point3D>? TryBuildOrientedLoopVertices(BrepBody body, LoopId loopId, out string? failureReason)
    {
        failureReason = null;
        if (!body.Topology.TryGetLoop(loopId, out var loop) || loop is null)
        {
            failureReason = $"Volume analysis loop {loopId.Value} is missing from topology.";
            return null;
        }

        var vertices = new List<Point3D>(loop.CoedgeIds.Count);
        foreach (var coedgeId in loop.CoedgeIds)
        {
            if (!body.Topology.TryGetCoedge(coedgeId, out var coedge) || coedge is null)
            {
                failureReason = $"Volume analysis coedge {coedgeId.Value} is missing from topology.";
                return null;
            }

            if (!body.Topology.TryGetEdge(coedge.EdgeId, out var edge) || edge is null)
            {
                failureReason = $"Volume analysis edge {coedge.EdgeId.Value} is missing from topology.";
                return null;
            }

            var vertexId = coedge.IsReversed ? edge.EndVertexId : edge.StartVertexId;
            if (!body.TryGetVertexPoint(vertexId, out var point))
            {
                failureReason = $"Volume analysis loop {loopId.Value} is missing vertex coordinate for vertex {vertexId.Value}.";
                return null;
            }

            vertices.Add(point);
        }

        return vertices;
    }

    private static IReadOnlyList<(Point3D A, Point3D B, Point3D C)>? TriangulateLoopOnPlane(
        IReadOnlyList<Point3D> vertices,
        PlaneSurface plane,
        out string? failureReason)
    {
        failureReason = null;
        var origin = plane.Origin;
        var u = plane.UAxis.ToVector();
        var v = plane.VAxis.ToVector();
        var normal = plane.Normal.ToVector();

        var indices = Enumerable.Range(0, vertices.Count).ToList();
        var uv = vertices.Select(p =>
        {
            var delta = p - origin;
            return new Point2D(delta.Dot(u), delta.Dot(v));
        }).ToArray();

        var area = SignedArea2D(indices.Select(i => uv[i]).ToArray());
        if (double.Abs(area) <= 1e-12d)
        {
            failureReason = "Volume analysis loop triangulation failed: degenerate planar loop area.";
            return null;
        }

        var ccw = area > 0d;
        var triangles = new List<(Point3D A, Point3D B, Point3D C)>();
        var guard = 0;
        while (indices.Count > 3 && guard++ < vertices.Count * vertices.Count)
        {
            var earFound = false;
            for (var i = 0; i < indices.Count; i++)
            {
                var iPrev = indices[(i - 1 + indices.Count) % indices.Count];
                var iCurr = indices[i];
                var iNext = indices[(i + 1) % indices.Count];
                if (!IsEar(uv, indices, iPrev, iCurr, iNext, ccw))
                {
                    continue;
                }

                var a = vertices[iPrev];
                var b = vertices[iCurr];
                var c = vertices[iNext];
                var triNormalDot = (b - a).Cross(c - a).Dot(normal);
                triangles.Add(triNormalDot >= 0d ? (a, b, c) : (a, c, b));
                indices.RemoveAt(i);
                earFound = true;
                break;
            }

            if (!earFound)
            {
                failureReason = "Volume analysis loop triangulation failed: non-simple or numerically unstable planar loop.";
                return null;
            }
        }

        if (indices.Count == 3)
        {
            var a = vertices[indices[0]];
            var b = vertices[indices[1]];
            var c = vertices[indices[2]];
            var triNormalDot = (b - a).Cross(c - a).Dot(normal);
            triangles.Add(triNormalDot >= 0d ? (a, b, c) : (a, c, b));
        }

        return triangles;
    }

    private static bool IsEar(Point2D[] uv, List<int> polygon, int iPrev, int iCurr, int iNext, bool ccw)
    {
        var a = uv[iPrev];
        var b = uv[iCurr];
        var c = uv[iNext];
        var cross = Cross2D(a, b, c);
        if (ccw ? cross <= 1e-12d : cross >= -1e-12d)
        {
            return false;
        }

        foreach (var candidate in polygon)
        {
            if (candidate == iPrev || candidate == iCurr || candidate == iNext)
            {
                continue;
            }

            if (PointInTriangle(uv[candidate], a, b, c))
            {
                return false;
            }
        }

        return true;
    }

    private static double SignedArea2D(IReadOnlyList<Point2D> points)
    {
        var sum = 0d;
        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            var q = points[(i + 1) % points.Count];
            sum += (p.U * q.V) - (q.U * p.V);
        }

        return 0.5d * sum;
    }

    private static double Cross2D(Point2D a, Point2D b, Point2D c) => ((b.U - a.U) * (c.V - a.V)) - ((b.V - a.V) * (c.U - a.U));
    private static bool PointInTriangle(Point2D p, Point2D a, Point2D b, Point2D c)
    {
        var c1 = Cross2D(a, b, p);
        var c2 = Cross2D(b, c, p);
        var c3 = Cross2D(c, a, p);
        var hasNeg = (c1 < -1e-12d) || (c2 < -1e-12d) || (c3 < -1e-12d);
        var hasPos = (c1 > 1e-12d) || (c2 > 1e-12d) || (c3 > 1e-12d);
        return !(hasNeg && hasPos);
    }

    private static double SignedTetraVolume(Point3D a, Point3D b, Point3D c)
    {
        var av = new Vector3D(a.X, a.Y, a.Z);
        var bv = new Vector3D(b.X, b.Y, b.Z);
        var cv = new Vector3D(c.X, c.Y, c.Z);
        return av.Dot(bv.Cross(cv)) / 6d;
    }

    private static (string FullPath, BrepBody Body) ImportStepBody(string stepPath)
    {
        var fullPath = Path.GetFullPath(stepPath);
        var import = Step242Importer.ImportBody(File.ReadAllText(fullPath));
        if (!import.IsSuccess)
        {
            throw new StepAnalysisImportException(fullPath, import.Diagnostics);
        }

        return (fullPath, import.Value);
    }

    public static SectionAnalysisResult AnalyzeImportedBodySection(BrepBody body, string stepPath, SectionPlaneFamily planeFamily, double offset)
    {
        var notes = new List<string>();
        var bbox = TryComputeBodyBoundingBox(body) ?? throw new InvalidOperationException("Section analyzer requires body vertex coordinates to compute bounding box.");
        var frame = ResolveSectionFrame(planeFamily, offset);
        var epsilon = Math.Max(ToleranceContext.Default.Linear * 64d, 1e-6d);
        var rawSegments = new List<RawSectionSegment>();

        foreach (var face in body.Topology.Faces)
        {
            if (!body.TryGetFaceSurface(face.Id, out var surface) || surface is null)
            {
                continue;
            }

            if (surface.Kind == SurfaceGeometryKind.Plane && surface.Plane is PlaneSurface facePlane)
            {
                rawSegments.AddRange(BuildPlanarFaceSectionSegments(body, face, facePlane, frame, epsilon, notes));
                continue;
            }

            if (surface.Kind == SurfaceGeometryKind.Cylinder && surface.Cylinder is CylinderSurface cylinder)
            {
                rawSegments.AddRange(BuildCylinderFaceSectionSegments(body, face, cylinder, frame, epsilon, notes));
            }
        }

        var loops = BuildLoops(rawSegments, epsilon);
        var metadata = new SectionAnalysisMetadata(
            stepPath,
            bbox,
            planeFamily,
            offset,
            frame.FixedAxis,
            frame.OffsetEquation,
            frame.AxisU,
            frame.AxisV,
            frame.MappingDescription);
        var summary = BuildSectionSummary(loops);
        return new SectionAnalysisResult(metadata, summary, loops, notes);
    }

    public static OrthographicMapResult AnalyzeImportedBodyMap(BrepBody body, string stepPath, OrthographicView view, int rows, int cols)
    {
        if (rows <= 0 || cols <= 0)
        {
            throw new InvalidOperationException("Map rows and cols must be positive integers.");
        }

        var notes = new List<string>();
        var bbox = TryComputeBodyBoundingBox(body) ?? throw new InvalidOperationException("Map probe requires body vertex coordinates to compute a bounding box.");
        var frame = ResolveProjectionFrame(view, bbox);
        var epsilon = Math.Max(ToleranceContext.Default.Linear * 64d, 1e-5d);
        var faceSurfaceKinds = BuildFaceSurfaceKinds(body);

        var grid = new List<IReadOnlyList<OrthographicSample>>(rows);
        var hitSamples = 0;
        var entryDepths = new List<double>();
        var thicknesses = new List<double>();
        var visibleFaceIds = new HashSet<int>();
        var visibleSurfaceTypes = new HashSet<string>(StringComparer.Ordinal);

        for (var rowIndex = 0; rowIndex < rows; rowIndex++)
        {
            var row = new List<OrthographicSample>(cols);
            var planeV = frame.MinV + ((rowIndex + 0.5d) / rows * frame.RangeV);

            for (var colIndex = 0; colIndex < cols; colIndex++)
            {
                var planeU = frame.MinU + ((colIndex + 0.5d) / cols * frame.RangeU);
                var planePoint = frame.PlaneOrigin + (frame.UAxis * planeU) + (frame.VAxis * planeV);
                var rayOrigin = planePoint - (frame.RayDirection * epsilon);
                var ray = new Ray3D(rayOrigin, Direction3D.Create(frame.RayDirection));
                var cast = BrepSpatialQueries.Raycast(body, ray, RayQueryOptions.Default with { IncludeBackfaces = true });
                if (!cast.IsSuccess)
                {
                    var first = cast.Diagnostics.FirstOrDefault();
                    var message = first?.Message ?? "unknown raycast error";
                    throw new InvalidOperationException($"Orthographic map v1 currently supports bodies accepted by BrepSpatialQueries.Raycast ({message}).");
                }

                var forwardHits = cast.Value
                    .Where(hit => hit.T >= 0d)
                    .OrderBy(hit => hit.T)
                    .ToArray();

                if (forwardHits.Length == 0)
                {
                    row.Add(new OrthographicSample(false, planeU, planeV, null, null, null, null, null, null, null, null));
                    continue;
                }

                var entry = forwardHits[0];
                var exit = forwardHits[^1];
                var entryDepth = Math.Max(0d, entry.T - epsilon);
                var exitDepth = Math.Max(entryDepth, exit.T - epsilon);
                var thickness = exitDepth - entryDepth;
                var faceId = entry.FaceId?.Value;
                var surfaceType = faceId.HasValue && faceSurfaceKinds.TryGetValue(faceId.Value, out var kind) ? kind : null;

                hitSamples++;
                entryDepths.Add(entryDepth);
                thicknesses.Add(thickness);
                if (faceId.HasValue)
                {
                    visibleFaceIds.Add(faceId.Value);
                }

                if (surfaceType is not null)
                {
                    visibleSurfaceTypes.Add(surfaceType);
                }

                row.Add(new OrthographicSample(
                    true,
                    planeU,
                    planeV,
                    entryDepth,
                    exitDepth,
                    thickness,
                    faceId,
                    surfaceType,
                    entry.Point,
                    entry.Normal?.ToVector(),
                    exit.Point));
            }

            grid.Add(row);
        }

        var summary = new OrthographicMapSummary(
            rows * cols,
            hitSamples,
            rows * cols - hitSamples,
            entryDepths.Count == 0 ? null : entryDepths.Min(),
            entryDepths.Count == 0 ? null : entryDepths.Max(),
            thicknesses.Count == 0 ? null : thicknesses.Min(),
            thicknesses.Count == 0 ? null : thicknesses.Max(),
            visibleFaceIds.OrderBy(v => v).ToArray(),
            visibleSurfaceTypes.OrderBy(v => v, StringComparer.Ordinal).ToArray());

        var metadata = new OrthographicMapMetadata(
            stepPath,
            bbox,
            view,
            rows,
            cols,
            frame.PlaneAxisU,
            frame.PlaneAxisV,
            frame.RayDirectionAxis,
            frame.DepthReference);

        notes.Add("Depth values are measured from the selected view's projection plane on the near bounding-box side, increasing along ray direction.");

        return new OrthographicMapResult(metadata, summary, grid, notes);
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

        if (points.Length > 0)
        {
            return ComputeBoundingBox(points);
        }

        var sphereBounds = new List<BoundingBox3D>();
        foreach (var face in body.Topology.Faces)
        {
            if (!body.TryGetFaceSurface(face.Id, out var surface)
                || surface?.Sphere is not { } sphere)
            {
                continue;
            }

            sphereBounds.Add(new BoundingBox3D(
                new Point3D(sphere.Center.X - sphere.Radius, sphere.Center.Y - sphere.Radius, sphere.Center.Z - sphere.Radius),
                new Point3D(sphere.Center.X + sphere.Radius, sphere.Center.Y + sphere.Radius, sphere.Center.Z + sphere.Radius)));
        }

        if (sphereBounds.Count == 0)
        {
            return null;
        }

        return new BoundingBox3D(
            new Point3D(sphereBounds.Min(b => b.Min.X), sphereBounds.Min(b => b.Min.Y), sphereBounds.Min(b => b.Min.Z)),
            new Point3D(sphereBounds.Max(b => b.Max.X), sphereBounds.Max(b => b.Max.Y), sphereBounds.Max(b => b.Max.Z)));
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

    private static Dictionary<int, string> BuildFaceSurfaceKinds(BrepBody body)
    {
        var result = new Dictionary<int, string>();
        foreach (var face in body.Topology.Faces)
        {
            if (!body.TryGetFaceSurface(face.Id, out var surface) || surface is null)
            {
                continue;
            }

            result[face.Id.Value] = surface.Kind.ToString();
        }

        return result;
    }

    private static IReadOnlyList<RawSectionSegment> BuildPlanarFaceSectionSegments(BrepBody body, Face face, PlaneSurface facePlane, SectionFrame frame, double epsilon, ICollection<string> notes)
    {
        var crossDirection = facePlane.Normal.ToVector().Cross(frame.Normal);
        if (!TryNormalize(crossDirection, out var lineDirection))
        {
            return [];
        }

        if (!TrySolvePlaneIntersectionPoint(facePlane, frame, out var linePoint))
        {
            notes.Add($"Skipped planar face {face.Id.Value}: face/section plane intersection is numerically unstable.");
            return [];
        }

        var points = new List<Point3D>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
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

                foreach (var point in IntersectEdgeWithSectionPlane(body, coedge.EdgeId, frame, epsilon))
                {
                    if (seen.Add(QuantizedPointKey(point, epsilon)))
                    {
                        points.Add(point);
                    }
                }
            }
        }

        if (points.Count < 2)
        {
            return [];
        }

        var ordered = points
            .Select(point => (Point: point, T: (point - linePoint).Dot(lineDirection)))
            .OrderBy(item => item.T)
            .ToArray();
        if (ordered.Length != 2)
        {
            notes.Add($"Skipped planar face {face.Id.Value}: section clipping produced {ordered.Length} intersections (v1 supports 2-point clipping only).");
            return [];
        }

        return [RawSectionSegment.Line(ProjectPoint(frame, ordered[0].Point), ProjectPoint(frame, ordered[1].Point))];
    }

    private static IReadOnlyList<RawSectionSegment> BuildCylinderFaceSectionSegments(BrepBody body, Face face, CylinderSurface cylinder, SectionFrame frame, double epsilon, ICollection<string> notes)
    {
        var axisDot = double.Abs(cylinder.Axis.ToVector().Dot(frame.Normal));
        if (axisDot < 1d - (epsilon * 8d))
        {
            notes.Add($"Skipped cylinder face {face.Id.Value}: v1 supports only section planes normal to cylinder axis.");
            return [];
        }

        var axisSamples = new List<double>();
        foreach (var edgeId in body.GetEdges(face.Id))
        {
            foreach (var vertexId in body.GetVertices(edgeId))
            {
                if (body.TryGetVertexPoint(vertexId, out var point))
                {
                    axisSamples.Add((point - Point3D.Origin).Dot(frame.Normal));
                }
            }
        }

        if (axisSamples.Count == 0)
        {
            notes.Add($"Skipped cylinder face {face.Id.Value}: no vertex samples to bound finite cylinder extent.");
            return [];
        }

        var minAxis = axisSamples.Min() - epsilon;
        var maxAxis = axisSamples.Max() + epsilon;
        if (frame.Offset < minAxis || frame.Offset > maxAxis)
        {
            return [];
        }

        var axisOriginCoord = (cylinder.Origin - Point3D.Origin).Dot(frame.Normal);
        var center3D = cylinder.Origin + (cylinder.Axis.ToVector() * (frame.Offset - axisOriginCoord));
        var center = ProjectPoint(frame, center3D);
        var start = new Point2D(center.U + cylinder.Radius, center.V);
        return [RawSectionSegment.Arc(start, start, center, cylinder.Radius, "ccw", 2d * double.Pi)];
    }

    private static IReadOnlyList<Point3D> IntersectEdgeWithSectionPlane(BrepBody body, EdgeId edgeId, SectionFrame frame, double epsilon)
    {
        if (!body.Bindings.TryGetEdgeBinding(edgeId, out var binding)
            || !body.Geometry.TryGetCurve(binding.CurveGeometryId, out var curve)
            || curve is null)
        {
            return [];
        }

        var trim = binding.TrimInterval ?? new ParameterInterval(0d, curve.Kind == CurveGeometryKind.Circle3 ? 2d * double.Pi : 1d);
        return curve.Kind switch
        {
            CurveGeometryKind.Line3 when curve.Line3 is { } line => IntersectLineEdge(line, trim, frame, epsilon),
            CurveGeometryKind.Circle3 when curve.Circle3 is { } circle => IntersectCircleEdge(circle, trim, frame, epsilon),
            _ => []
        };
    }

    private static IReadOnlyList<Point3D> IntersectLineEdge(Line3Curve line, ParameterInterval trim, SectionFrame frame, double epsilon)
    {
        var a = line.Evaluate(trim.Start);
        var b = line.Evaluate(trim.End);
        var da = SignedSectionDistance(a, frame);
        var db = SignedSectionDistance(b, frame);
        if ((da > epsilon && db > epsilon) || (da < -epsilon && db < -epsilon))
        {
            return [];
        }

        if (double.Abs(da) <= epsilon && double.Abs(db) <= epsilon)
        {
            return [];
        }

        var denom = da - db;
        if (double.Abs(denom) <= epsilon)
        {
            return [];
        }

        var t = Math.Clamp(da / denom, 0d, 1d);
        return [a + ((b - a) * t)];
    }

    private static IReadOnlyList<Point3D> IntersectCircleEdge(Circle3Curve circle, ParameterInterval trim, SectionFrame frame, double epsilon)
    {
        var planeDot = double.Abs(circle.Normal.ToVector().Dot(frame.Normal));
        var centerDistance = SignedSectionDistance(circle.Center, frame);
        if (planeDot < 1d - (epsilon * 8d) || double.Abs(centerDistance) > epsilon)
        {
            return [];
        }

        return [circle.Evaluate(trim.Start), circle.Evaluate(trim.End)];
    }

    private static List<SectionLoop> BuildLoops(IReadOnlyList<RawSectionSegment> rawSegments, double epsilon)
    {
        var unused = rawSegments.ToList();
        var loops = new List<SectionLoop>();
        var nextId = 1;
        while (unused.Count > 0)
        {
            var chain = new List<RawSectionSegment>();
            var first = unused[0];
            unused.RemoveAt(0);
            chain.Add(first);
            var cursor = first.End;

            while (!first.IsClosed && !PointsClose(cursor, first.Start, epsilon))
            {
                var index = FindConnectingSegment(unused, cursor, epsilon, out var reverse);
                if (index < 0)
                {
                    break;
                }

                var segment = unused[index];
                unused.RemoveAt(index);
                if (reverse)
                {
                    segment = segment.Reversed();
                }

                chain.Add(segment);
                cursor = segment.End;
            }

            var closed = first.IsClosed || PointsClose(chain[0].Start, chain[^1].End, epsilon);
            loops.Add(new SectionLoop(nextId++, closed, closed ? ComputeWinding(chain) : null, ComputeBoundingBox2D(chain.SelectMany(segment => segment.Points()).ToArray()), chain.Select(ToSectionSegment).ToArray()));
        }

        return loops.OrderByDescending(loop => loop.BoundingBox2D is null ? 0d : Area(loop.BoundingBox2D)).ToList();
    }

    private static SectionAnalysisSummary BuildSectionSummary(IReadOnlyList<SectionLoop> loops)
    {
        var segments = loops.SelectMany(loop => loop.Segments).ToArray();
        var points = segments.SelectMany(segment =>
        {
            var result = new List<Point2D> { segment.Start, segment.End };
            if (segment.Center is not null)
            {
                result.Add(segment.Center);
            }

            return result;
        }).ToArray();

        return new SectionAnalysisSummary(
            loops.Count,
            loops.Count(loop => loop.IsClosed),
            segments.Count(segment => segment.Kind == "line"),
            segments.Count(segment => segment.Kind == "arc"),
            segments.Count(segment => segment.Kind == "unsupported"),
            ComputeBoundingBox2D(points));
    }

    private static SectionSegment ToSectionSegment(RawSectionSegment raw) =>
        raw.Kind switch
        {
            RawSectionSegmentKind.Line => new SectionSegment("line", raw.Start, raw.End, null, null, null, null, null),
            RawSectionSegmentKind.Arc => new SectionSegment("arc", raw.Start, raw.End, raw.Center, raw.Radius, raw.Direction, raw.SweepRadians, null),
            _ => new SectionSegment("unsupported", raw.Start, raw.End, null, null, null, null, raw.UnsupportedReason ?? "unsupported")
        };

    private static int FindConnectingSegment(IReadOnlyList<RawSectionSegment> segments, Point2D cursor, double epsilon, out bool reverse)
    {
        for (var i = 0; i < segments.Count; i++)
        {
            if (PointsClose(segments[i].Start, cursor, epsilon))
            {
                reverse = false;
                return i;
            }

            if (PointsClose(segments[i].End, cursor, epsilon))
            {
                reverse = true;
                return i;
            }
        }

        reverse = false;
        return -1;
    }

    private static bool PointsClose(Point2D a, Point2D b, double epsilon)
    {
        var du = a.U - b.U;
        var dv = a.V - b.V;
        return (du * du) + (dv * dv) <= (epsilon * epsilon * 16d);
    }

    private static string? ComputeWinding(IReadOnlyList<RawSectionSegment> segments)
    {
        var points = segments.Select(segment => segment.Start).ToArray();
        if (points.Length < 3)
        {
            return null;
        }

        var signedArea2 = 0d;
        for (var i = 0; i < points.Length; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Length];
            signedArea2 += (a.U * b.V) - (b.U * a.V);
        }

        if (double.Abs(signedArea2) <= 1e-12d)
        {
            return null;
        }

        return signedArea2 > 0d ? "ccw" : "cw";
    }

    private static BoundingBox2D? ComputeBoundingBox2D(IReadOnlyList<Point2D> points)
    {
        if (points.Count == 0)
        {
            return null;
        }

        return new BoundingBox2D(
            new Point2D(points.Min(point => point.U), points.Min(point => point.V)),
            new Point2D(points.Max(point => point.U), points.Max(point => point.V)));
    }

    private static double SignedSectionDistance(Point3D point, SectionFrame frame) =>
        (point - Point3D.Origin).Dot(frame.Normal) - frame.Offset;

    private static string QuantizedPointKey(Point3D point, double epsilon)
    {
        var scale = 1d / Math.Max(epsilon, 1e-8d);
        return $"{Math.Round(point.X * scale):F0}:{Math.Round(point.Y * scale):F0}:{Math.Round(point.Z * scale):F0}";
    }

    private static Point2D ProjectPoint(SectionFrame frame, Point3D point) =>
        new((point - Point3D.Origin).Dot(frame.UAxis), (point - Point3D.Origin).Dot(frame.VAxis));

    private static bool TrySolvePlaneIntersectionPoint(PlaneSurface a, SectionFrame b, out Point3D point)
    {
        var n1 = a.Normal.ToVector();
        var n2 = b.Normal;
        var d1 = n1.Dot(a.Origin - Point3D.Origin);
        var d2 = b.Offset;
        var cross = n1.Cross(n2);
        var denom = cross.Dot(cross);
        if (denom <= 1e-20d)
        {
            point = default;
            return false;
        }

        var p = ((n2 * d1) - (n1 * d2)).Cross(cross) * (1d / denom);
        point = new Point3D(p.X, p.Y, p.Z);
        return double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);
    }

    private static bool TryNormalize(Vector3D vector, out Vector3D normalized)
    {
        if (vector.Length <= 1e-20d)
        {
            normalized = default;
            return false;
        }

        normalized = vector / vector.Length;
        return true;
    }

    private static double Area(BoundingBox2D bbox) => Math.Max(0d, (bbox.Max.U - bbox.Min.U) * (bbox.Max.V - bbox.Min.V));

    private static SectionFrame ResolveSectionFrame(SectionPlaneFamily family, double offset) =>
        family switch
        {
            SectionPlaneFamily.XY => new SectionFrame(new Vector3D(0d, 0d, 1d), new Vector3D(1d, 0d, 0d), new Vector3D(0d, 1d, 0d), offset, "Z", "z = offset", "X", "Y", "(u,v) -> (x,y)"),
            SectionPlaneFamily.XZ => new SectionFrame(new Vector3D(0d, 1d, 0d), new Vector3D(1d, 0d, 0d), new Vector3D(0d, 0d, 1d), offset, "Y", "y = offset", "X", "Z", "(u,v) -> (x,z)"),
            SectionPlaneFamily.YZ => new SectionFrame(new Vector3D(1d, 0d, 0d), new Vector3D(0d, 1d, 0d), new Vector3D(0d, 0d, 1d), offset, "X", "x = offset", "Y", "Z", "(u,v) -> (y,z)"),
            _ => throw new InvalidOperationException($"Unsupported section plane family '{family}'.")
        };

    private static ProjectionFrame ResolveProjectionFrame(OrthographicView view, BoundingBox3D bbox)
    {
        return view switch
        {
            OrthographicView.Top => new ProjectionFrame(
                new Point3D(0d, 0d, bbox.Max.Z),
                new Vector3D(1d, 0d, 0d),
                new Vector3D(0d, 1d, 0d),
                new Vector3D(0d, 0d, -1d),
                bbox.Min.X,
                bbox.Max.X,
                bbox.Min.Y,
                bbox.Max.Y,
                "X",
                "Y",
                "-Z",
                $"z={bbox.Max.Z:G17}"),
            OrthographicView.Bottom => new ProjectionFrame(
                new Point3D(0d, 0d, bbox.Min.Z),
                new Vector3D(1d, 0d, 0d),
                new Vector3D(0d, 1d, 0d),
                new Vector3D(0d, 0d, 1d),
                bbox.Min.X,
                bbox.Max.X,
                bbox.Min.Y,
                bbox.Max.Y,
                "X",
                "Y",
                "+Z",
                $"z={bbox.Min.Z:G17}"),
            OrthographicView.Front => new ProjectionFrame(
                new Point3D(0d, bbox.Max.Y, 0d),
                new Vector3D(1d, 0d, 0d),
                new Vector3D(0d, 0d, 1d),
                new Vector3D(0d, -1d, 0d),
                bbox.Min.X,
                bbox.Max.X,
                bbox.Min.Z,
                bbox.Max.Z,
                "X",
                "Z",
                "-Y",
                $"y={bbox.Max.Y:G17}"),
            OrthographicView.Back => new ProjectionFrame(
                new Point3D(0d, bbox.Min.Y, 0d),
                new Vector3D(1d, 0d, 0d),
                new Vector3D(0d, 0d, 1d),
                new Vector3D(0d, 1d, 0d),
                bbox.Min.X,
                bbox.Max.X,
                bbox.Min.Z,
                bbox.Max.Z,
                "X",
                "Z",
                "+Y",
                $"y={bbox.Min.Y:G17}"),
            OrthographicView.Left => new ProjectionFrame(
                new Point3D(bbox.Min.X, 0d, 0d),
                new Vector3D(0d, 1d, 0d),
                new Vector3D(0d, 0d, 1d),
                new Vector3D(1d, 0d, 0d),
                bbox.Min.Y,
                bbox.Max.Y,
                bbox.Min.Z,
                bbox.Max.Z,
                "Y",
                "Z",
                "+X",
                $"x={bbox.Min.X:G17}"),
            OrthographicView.Right => new ProjectionFrame(
                new Point3D(bbox.Max.X, 0d, 0d),
                new Vector3D(0d, 1d, 0d),
                new Vector3D(0d, 0d, 1d),
                new Vector3D(-1d, 0d, 0d),
                bbox.Min.Y,
                bbox.Max.Y,
                bbox.Min.Z,
                bbox.Max.Z,
                "Y",
                "Z",
                "-X",
                $"x={bbox.Max.X:G17}"),
            _ => throw new InvalidOperationException($"Unsupported view '{view}'.")
        };
    }

    private readonly record struct ProjectionFrame(
        Point3D PlaneOrigin,
        Vector3D UAxis,
        Vector3D VAxis,
        Vector3D RayDirection,
        double MinU,
        double MaxU,
        double MinV,
        double MaxV,
        string PlaneAxisU,
        string PlaneAxisV,
        string RayDirectionAxis,
        string DepthReference)
    {
        public double RangeU => MaxU - MinU;
        public double RangeV => MaxV - MinV;
    }

    private readonly record struct SectionFrame(
        Vector3D Normal,
        Vector3D UAxis,
        Vector3D VAxis,
        double Offset,
        string FixedAxis,
        string OffsetEquation,
        string AxisU,
        string AxisV,
        string MappingDescription);

    private enum RawSectionSegmentKind
    {
        Line,
        Arc,
        Unsupported
    }

    private readonly record struct RawSectionSegment(
        RawSectionSegmentKind Kind,
        Point2D Start,
        Point2D End,
        Point2D? Center,
        double? Radius,
        string? Direction,
        double? SweepRadians,
        string? UnsupportedReason)
    {
        public bool IsClosed =>
            Kind == RawSectionSegmentKind.Arc
            && double.Abs(Start.U - End.U) <= 1e-9d
            && double.Abs(Start.V - End.V) <= 1e-9d
            && SweepRadians.HasValue
            && SweepRadians.Value >= (2d * double.Pi) - 1e-9d;

        public static RawSectionSegment Line(Point2D start, Point2D end) =>
            new(RawSectionSegmentKind.Line, start, end, null, null, null, null, null);

        public static RawSectionSegment Arc(Point2D start, Point2D end, Point2D center, double radius, string direction, double sweepRadians) =>
            new(RawSectionSegmentKind.Arc, start, end, center, radius, direction, sweepRadians, null);

        public RawSectionSegment Reversed()
        {
            var reversedDirection = Direction is null
                ? null
                : string.Equals(Direction, "ccw", StringComparison.Ordinal) ? "cw" : "ccw";
            return this with
            {
                Start = End,
                End = Start,
                Direction = reversedDirection
            };
        }

        public IReadOnlyList<Point2D> Points() => Center is null ? [Start, End] : [Start, End, Center];
    }
}
