using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.Materializer;
using System.Text.RegularExpressions;

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
    int? UnknownCount,
    double? UnknownRatio,
    string? UnknownPolicy,
    IReadOnlyList<string> Notes);

    public static AnalyzeResult Analyze(string stepPath, int? faceId = null, int? edgeId = null, int? vertexId = null)
    {
        var (fullPath, body) = ImportStepBody(stepPath);
        var stepText = File.ReadAllText(fullPath);
        var analysis = AnalyzeImportedBody(body, fullPath, faceId, edgeId, vertexId) with
        {
            SemanticPmi = Step242SemanticPmiInspector.Inspect(stepText)
        };
        if (analysis.Face is null) return analysis;

        // Imported B-rep IDs are the public sequential vocabulary.  Preserve the
        // originating ADVANCED_FACE alongside it for traceability, never as a
        // requirement for normal authoring.
        var entities = Regex.Matches(stepText, @"(?m)^\s*(#[0-9]+)\s*=\s*ADVANCED_FACE\s*\(", RegexOptions.CultureInvariant)
            .Cast<Match>().Select(match => match.Groups[1].Value).ToArray();
        var entity = analysis.Face.FaceId > 0 && analysis.Face.FaceId <= entities.Length ? entities[analysis.Face.FaceId - 1] : null;
        return analysis with { Face = analysis.Face with { StepEntity = entity } };
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
        var shells = ResolveShellRepresentationForVolume(body);

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
            return new VolumeAnalysisResult(stepPath, true, vol, "model-unit", "model-unit^3", new VolumeBoundingBox(bbox.Min, bbox.Max), "analytic-sphere", true, false, null, null, null, null, null, null, null, notes);
        }

        var cylFaces = body.Topology.Faces.Where(f => body.TryGetFaceSurface(f.Id, out var sf) && sf?.Cylinder is not null).ToArray();
        var coneFaces = body.Topology.Faces.Where(f => body.TryGetFaceSurface(f.Id, out var sf) && sf?.Cone is not null).ToArray();
        if (cylFaces.Length + coneFaces.Length >= 2
            && body.Topology.Faces.Count() == cylFaces.Length + coneFaces.Length + 2
            && TryComputePiecewiseLinearRevolvedProfileVolume(body, out var revolvedVolume, out var revolvedBasis))
        {
            notes.Add(revolvedBasis);
            return new VolumeAnalysisResult(stepPath, true, revolvedVolume, "model-unit", "model-unit^3", new VolumeBoundingBox(bbox.Min, bbox.Max), "analytic-piecewise-linear-revolved-profile", true, false, null, null, null, null, null, null, null, notes);
        }

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
            return new VolumeAnalysisResult(stepPath, true, vol, "model-unit", "model-unit^3", new VolumeBoundingBox(bbox.Min, bbox.Max), "analytic-cylinder", true, false, null, null, null, null, null, null, null, notes);
        }

        if (coneFaces.Length == 1 && body.Topology.Faces.Count() == 3)
        {
            body.TryGetFaceSurface(coneFaces[0].Id, out var coneSurface);
            var cone = coneSurface!.Cone!.Value;
            var axis = cone.Axis.ToVector();
            var min = double.PositiveInfinity; var max = double.NegativeInfinity;
            foreach (var v in body.Topology.Vertices)
            {
                if (!body.TryGetVertexPoint(v.Id, out var pt)) continue;
                var t = (pt - cone.PlacementOrigin).Dot(axis);
                min = double.Min(min, t); max = double.Max(max, t);
            }
            if (!double.IsFinite(min) || !double.IsFinite(max) || max <= min)
                throw new InvalidOperationException("Cone volume analysis could not resolve finite axial span from vertices.");
            var h = max - min;
            var tan = double.Tan(cone.SemiAngleRadians);
            var r1 = double.Abs(cone.PlacementRadius + min * tan);
            var r2 = double.Abs(cone.PlacementRadius + max * tan);
            var vol = double.Pi * h / 3d * (r1 * r1 + r1 * r2 + r2 * r2);
            notes.Add("Exact analytic cone/frustum volume from conical face radii and cap-span derived from bound vertices.");
            return new VolumeAnalysisResult(stepPath, true, vol, "model-unit", "model-unit^3", new VolumeBoundingBox(bbox.Min, bbox.Max), "analytic-cone", true, false, null, null, null, null, null, null, null, notes);
        }

        var torusFaces = body.Topology.Faces.Where(f => body.TryGetFaceSurface(f.Id, out var sf) && sf?.Torus is not null).ToArray();
        if (body.Topology.Faces.Count() == 1 && torusFaces.Length == 1)
        {
            body.TryGetFaceSurface(torusFaces[0].Id, out var ts);
            var torus = ts!.Torus!.Value;
            var vol = 2d * double.Pi * double.Pi * torus.MajorRadius * torus.MinorRadius * torus.MinorRadius;
            notes.Add("Exact analytic torus volume from toroidal face radii.");
            return new VolumeAnalysisResult(stepPath, true, vol, "model-unit", "model-unit^3", new VolumeBoundingBox(bbox.Min, bbox.Max), "analytic-torus", true, false, null, null, null, null, null, null, null, notes);
        }

        if (TryComputeAxisAlignedBoxWithZHoleVolume(body, bbox, out var zHoleVolume, out var zHoleBasis))
        {
            notes.Add(zHoleBasis);
            return new VolumeAnalysisResult(stepPath, true, zHoleVolume, "model-unit", "model-unit^3", new VolumeBoundingBox(bbox.Min, bbox.Max), "analytic-box-minus-z-hole", true, false, null, null, null, null, null, null, null, notes);
        }

        if (TryComputeAxisAlignedBoxWithXHoleVolume(body, bbox, out var xHoleVolume, out var xHoleBasis))
        {
            notes.Add(xHoleBasis);
            return new VolumeAnalysisResult(stepPath, true, xHoleVolume, "model-unit", "model-unit^3", new VolumeBoundingBox(bbox.Min, bbox.Max), "analytic-box-minus-x-hole", true, false, null, null, null, null, null, null, null, notes);
        }

        var shellVolume = TryComputePlanarClosedShellVolume(body, shells, out var planarVolume, out var planarFailureReason);
        if (shellVolume)
        {
            notes.Add("Exact closed-shell volume from oriented planar-face triangulation and signed tetrahedral accumulation.");
            return new VolumeAnalysisResult(stepPath, true, planarVolume, "model-unit", "model-unit^3", new VolumeBoundingBox(bbox.Min, bbox.Max), "planar-closed-shell", true, false, null, null, null, null, null, null, null, notes);
        }

        var unsupportedSweptSurfaceKind = body.Topology.Faces
            .Select(face => body.TryGetFaceSurface(face.Id, out var surface) ? surface : null)
            .Where(surface => surface is not null)
            .Select(surface => surface!.Kind)
            .FirstOrDefault(kind => kind is SurfaceGeometryKind.LinearExtrusion or SurfaceGeometryKind.SurfaceOfRevolution);
        if (unsupportedSweptSurfaceKind is SurfaceGeometryKind.LinearExtrusion or SurfaceGeometryKind.SurfaceOfRevolution)
        {
            var surfaceKindName = ToSurfaceFamilyName(unsupportedSweptSurfaceKind);
            var structural = BuildSummary(body, notes).StructuralAssessment;
            var bodyDescription = structural == "enclosed-manifold" ? "body" : "open or non-solid body";
            throw new InvalidOperationException($"Exact volume is not supported for {bodyDescription} containing {surfaceKindName} surfaces.");
        }

        throw new InvalidOperationException(planarFailureReason ?? "Volume analysis currently supports canonical sphere, single-lateral-face cylinder, and enclosed planar closed-shell bodies only.");
    }

    private static bool TryComputePiecewiseLinearRevolvedProfileVolume(BrepBody body, out double volume, out string basis)
    {
        volume = 0;
        basis = string.Empty;
        var circles = body.Geometry.Curves.Select(c => c.Value.Circle3).Where(c => c.HasValue).Select(c => c!.Value).ToArray();
        if (circles.Length < 3) return false;
        var axis = circles[0].Normal.ToVector();
        if (circles.Any(c => double.Abs(double.Abs(c.Normal.ToVector().Dot(axis)) - 1d) > 1e-8)) return false;
        var origin = circles[0].Center;
        var profile = circles
            .Select(c => (T: (c.Center - origin).Dot(axis), c.Radius))
            .OrderBy(p => p.T)
            .ToArray();
        if (profile.Zip(profile.Skip(1)).Any(p => p.Second.T - p.First.T <= 1e-9)) return false;
        for (var i = 0; i < profile.Length - 1; i++)
        {
            var h = profile[i + 1].T - profile[i].T;
            var r0 = profile[i].Radius;
            var r1 = profile[i + 1].Radius;
            volume += double.Pi * h / 3d * (r0 * r0 + r0 * r1 + r1 * r1);
        }
        basis = $"Exact analytic volume from {profile.Length}-point coaxial circular profile and cylindrical/conical segment integration.";
        return double.IsFinite(volume) && volume > 0;
    }

    private static bool TryComputeAxisAlignedBoxWithXHoleVolume(BrepBody body, BoundingBox3D bbox, out double volume, out string basis)
    {
        volume = 0d;
        basis = string.Empty;
        var cylinders = new List<(double CenterY, double CenterZ, double Radius, double XMin, double XMax)>();

        foreach (var face in body.Topology.Faces)
        {
            if (!body.TryGetFaceSurface(face.Id, out var surface) || surface is null)
            {
                return false;
            }

            if (surface.Kind == SurfaceGeometryKind.Cylinder && surface.Cylinder is { } cylinder)
            {
                if (!IsXAxis(cylinder.Axis.ToVector())) return false;
                if (!TryResolveFaceXSpan(body, face.Id, out var xMin, out var xMax)) return false;
                cylinders.Add((cylinder.Origin.Y, cylinder.Origin.Z, cylinder.Radius, xMin, xMax));
                continue;
            }

            if (surface.Kind != SurfaceGeometryKind.Plane)
            {
                return false;
            }
        }

        if (cylinders.Count == 0)
        {
            return false;
        }

        var baseVolume = (bbox.Max.X - bbox.Min.X) * (bbox.Max.Y - bbox.Min.Y) * (bbox.Max.Z - bbox.Min.Z);
        // One analytic cylinder may be represented by multiple trimmed faces
        // (the local-frame hole planner deliberately uses two arcs to avoid a
        // reused longitudinal seam). Deduplicate that partition before adding
        // physical removal volumes; distinct centers remain distinct holes.
        var physicalCylinders = cylinders.DistinctBy(c => (c.CenterY, c.CenterZ, c.Radius, c.XMin, c.XMax));
        var removed = physicalCylinders.Sum(c => double.Pi * c.Radius * c.Radius * (c.XMax - c.XMin));
        volume = baseVolume - removed;
        basis = "Exact analytic volume for an axis-aligned rectangular box with supported locked Firmament V2 +X/-X cylindrical side-hole interval.";
        return double.IsFinite(volume) && volume > 0d;
    }

    private static bool TryComputeAxisAlignedBoxWithZHoleVolume(BrepBody body, BoundingBox3D bbox, out double volume, out string basis)
    {
        volume = 0d;
        basis = string.Empty;

        var cylinders = new List<(double CenterX, double CenterY, double Radius, double ZMin, double ZMax)>();
        var cones = new List<(double RadiusAtZMin, double RadiusAtZMax, double ZMin, double ZMax)>();

        foreach (var face in body.Topology.Faces)
        {
            if (!body.TryGetFaceSurface(face.Id, out var surface) || surface is null)
            {
                return false;
            }

            if (surface.Kind == SurfaceGeometryKind.Cylinder && surface.Cylinder is { } cylinder)
            {
                if (!IsZAxis(cylinder.Axis.ToVector())) return false;
                if (!TryResolveFaceZSpan(body, face.Id, out var zMin, out var zMax)) return false;
                cylinders.Add((cylinder.Origin.X, cylinder.Origin.Y, cylinder.Radius, zMin, zMax));
                continue;
            }

            if (surface.Kind == SurfaceGeometryKind.Cone && surface.Cone is { } cone)
            {
                if (!IsZAxis(cone.Axis.ToVector())) return false;
                if (!TryResolveFaceZSpan(body, face.Id, out var zMin, out var zMax)) return false;
                if (!TryResolveConeRadiiAtZSpan(body, face.Id, cone.Axis.ToVector(), new Point3D(cone.PlacementOrigin.X, cone.PlacementOrigin.Y, 0d), zMin, zMax, out var rMin, out var rMax)) return false;
                cones.Add((rMin, rMax, zMin, zMax));
                continue;
            }

            if (surface.Kind != SurfaceGeometryKind.Plane)
            {
                return false;
            }
        }

        if (cylinders.Count == 0 && cones.Count == 0)
        {
            return false;
        }

        var baseVolume = (bbox.Max.X - bbox.Min.X) * (bbox.Max.Y - bbox.Min.Y) * (bbox.Max.Z - bbox.Min.Z);
        var breakpoints = cylinders.SelectMany(c => new[] { c.ZMin, c.ZMax }).Distinct().OrderBy(z => z).ToArray();
        var removed = 0d;
        for (var i = 0; i + 1 < breakpoints.Length; i++)
        {
            var a = breakpoints[i];
            var b = breakpoints[i + 1];
            var mid = (a + b) / 2d;
            var active = cylinders.Where(c => mid >= c.ZMin - 1e-9 && mid <= c.ZMax + 1e-9).ToArray();
            var removedArea = active
                .GroupBy(c => (Math.Round(c.CenterX, 9), Math.Round(c.CenterY, 9)))
                .Sum(g => double.Pi * g.Max(c => c.Radius) * g.Max(c => c.Radius));
            removed += removedArea * (b - a);
        }

        foreach (var cone in cones)
        {
            var h = cone.ZMax - cone.ZMin;
            if (h <= 0d) return false;
            var frustum = double.Pi * h / 3d * (cone.RadiusAtZMin * cone.RadiusAtZMin + cone.RadiusAtZMin * cone.RadiusAtZMax + cone.RadiusAtZMax * cone.RadiusAtZMax);
            var overlapRadius = cylinders
                .Where(c => c.ZMin <= cone.ZMin + 1e-9 && c.ZMax >= cone.ZMax - 1e-9)
                .Select(c => c.Radius)
                .DefaultIfEmpty(0d)
                .Min();
            removed += frustum - double.Pi * overlapRadius * overlapRadius * h;
        }

        volume = baseVolume - removed;
        basis = "Exact analytic volume for an axis-aligned rectangular box with supported +Z semantic cylindrical/counterbore/countersink hole intervals.";
        return double.IsFinite(volume) && volume > 0d;
    }

    private static bool TryResolveFaceZSpan(BrepBody body, FaceId faceId, out double zMin, out double zMax)
    {
        zMin = double.PositiveInfinity;
        zMax = double.NegativeInfinity;
        if (!body.Topology.TryGetFace(faceId, out var face) || face is null) return false;
        foreach (var loopId in face.LoopIds)
        {
            var vertices = TryBuildOrientedLoopVertices(body, loopId, out _);
            if (vertices is null) return false;
            foreach (var vertex in vertices)
            {
                zMin = double.Min(zMin, vertex.Z);
                zMax = double.Max(zMax, vertex.Z);
            }
        }

        return double.IsFinite(zMin) && double.IsFinite(zMax) && zMax > zMin;
    }

    private static bool TryResolveConeRadiiAtZSpan(BrepBody body, FaceId faceId, Vector3D axis, Point3D axisPoint, double zMin, double zMax, out double radiusAtZMin, out double radiusAtZMax)
    {
        radiusAtZMin = 0d;
        radiusAtZMax = 0d;
        if (!body.Topology.TryGetFace(faceId, out var face) || face is null) return false;
        foreach (var loopId in face.LoopIds)
        {
            var vertices = TryBuildOrientedLoopVertices(body, loopId, out _);
            if (vertices is null) return false;
            foreach (var vertex in vertices)
            {
                var radial = new Vector3D(vertex.X - axisPoint.X, vertex.Y - axisPoint.Y, 0d).Length;
                if (double.Abs(vertex.Z - zMin) <= 1e-7) radiusAtZMin = double.Max(radiusAtZMin, radial);
                if (double.Abs(vertex.Z - zMax) <= 1e-7) radiusAtZMax = double.Max(radiusAtZMax, radial);
            }
        }

        return radiusAtZMin > 0d && radiusAtZMax > 0d;
    }

    private static bool IsZAxis(Vector3D axis) =>
        double.Abs(axis.X) <= 1e-9 && double.Abs(axis.Y) <= 1e-9 && double.Abs(double.Abs(axis.Z) - 1d) <= 1e-9;

    private static bool IsXAxis(Vector3D axis) =>
        double.Abs(axis.Y) <= 1e-9 && double.Abs(axis.Z) <= 1e-9 && double.Abs(double.Abs(axis.X) - 1d) <= 1e-9;

    private static bool TryResolveFaceXSpan(BrepBody body, FaceId faceId, out double xMin, out double xMax)
    {
        xMin = double.PositiveInfinity;
        xMax = double.NegativeInfinity;
        if (!body.Topology.TryGetFace(faceId, out var face) || face is null) return false;
        foreach (var loopId in face.LoopIds)
        {
            var vertices = TryBuildOrientedLoopVertices(body, loopId, out _);
            if (vertices is null) return false;
            foreach (var vertex in vertices)
            {
                xMin = double.Min(xMin, vertex.X);
                xMax = double.Max(xMax, vertex.X);
            }
        }

        return double.IsFinite(xMin) && double.IsFinite(xMax) && xMax > xMin;
    }

    private static BrepBodyShellRepresentation ResolveShellRepresentationForVolume(BrepBody body)
    {
        if (body.ShellRepresentation is { } shells)
        {
            return shells;
        }

        var bodies = body.Topology.Bodies.OrderBy(candidate => candidate.Id.Value).ToArray();
        if (bodies.Length != 1)
        {
            throw new InvalidOperationException("Volume analysis requires explicit shell-role representation for multi-body topology.");
        }

        var shellIds = bodies[0].ShellIds.OrderBy(shellId => shellId.Value).ToArray();
        if (shellIds.Length == 1)
        {
            return new BrepBodyShellRepresentation(shellIds[0], []);
        }

        throw new InvalidOperationException("Volume analysis requires explicit shell-role representation for multi-shell bodies.");
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
        var unknown = 0;

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
                var diag = containment.Diagnostics.FirstOrDefault()?.Message ?? "no diagnostic provided";
                throw new InvalidOperationException($"Approximate volume classification is unsupported for this body ({diag}).");
            }

            if (containment.Value == PointContainment.Unknown)
            {
                unknown++;
                continue;
            }

            if (containment.Value is PointContainment.Inside or PointContainment.Boundary)
            {
                occupied++;
            }
        }

        var volume = occupied * cellVolume;
        var unknownRatio = total > 0 ? (double)unknown / total : 0d;
        var notes = new List<string>
        {
            "Approximate volume mode: deterministic center-point voxel sampling over the body axis-aligned bounding box.",
            "Resolution means samples along the longest bounding-box axis; other axis counts are derived proportionally.",
            "Estimated result is not exact and should be used for comparison/localization only."
        };
        if (unknown > 0)
        {
            notes.Add($"Unknown containment samples were conservatively treated as outside ({unknown}/{total}, ratio={unknownRatio:G6}).");
        }
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
            unknown,
            unknownRatio,
            "conservative-outside",
            notes);
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

                    var loopSignedArea = ComputeSignedLoopAreaOnPlane(loopVertices, plane);
                    if (double.Abs(loopSignedArea) <= 1e-12d)
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
                        // TriangulateLoopOnPlane already emits winding relative to the
                        // face plane. Applying the loop's projected-area sign a second
                        // time inverted valid Box faces and was the source of the four
                        // generic Box/derivation/PMI volume failures.
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

            var vertexId = DirectedEdgeUse.Resolve(edge, coedge).StartVertexId;
            if (!body.TryGetVertexPoint(vertexId, out var point))
            {
                failureReason = $"Volume analysis loop {loopId.Value} is missing vertex coordinate for vertex {vertexId.Value}.";
                return null;
            }

            vertices.Add(point);
        }

        if (vertices.Count == 0)
        {
            return vertices;
        }

        static bool NearlyEqual(Point3D a, Point3D b)
            => double.Abs(a.X - b.X) <= 1e-9d && double.Abs(a.Y - b.Y) <= 1e-9d && double.Abs(a.Z - b.Z) <= 1e-9d;

        var normalized = new List<Point3D>(vertices.Count);
        foreach (var vertex in vertices)
        {
            if (normalized.Count > 0 && NearlyEqual(normalized[^1], vertex))
            {
                continue;
            }

            normalized.Add(vertex);
        }

        if (normalized.Count > 1 && NearlyEqual(normalized[0], normalized[^1]))
        {
            normalized.RemoveAt(normalized.Count - 1);
        }

        return normalized;
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

    private static double ComputeSignedLoopAreaOnPlane(IReadOnlyList<Point3D> vertices, PlaneSurface plane)
    {
        var origin = plane.Origin;
        var u = plane.UAxis.ToVector();
        var v = plane.VAxis.ToVector();
        var uv = vertices.Select(p =>
        {
            var delta = p - origin;
            return new Point2D(delta.Dot(u), delta.Dot(v));
        }).ToArray();
        return SignedArea2D(uv);
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
                continue;
            }

            if (surface.Kind == SurfaceGeometryKind.Cone && surface.Cone is ConeSurface cone)
            {
                rawSegments.AddRange(BuildConeFaceSectionSegments(body, face, cone, frame, epsilon, notes));
                continue;
            }

            notes.Add($"UnsupportedSectionCurve:face={face.Id.Value}:surface={surface.Kind}:plane={frame.FixedAxis}={offset:R}:bounded analytic intersection adapter is not implemented");
        }

        // Importer fragments are normalized by the shared analytic arrangement; do
        // not reintroduce the former first-neighbour greedy chain walker here.
        var arrangement = NormalizeSectionFragments(rawSegments, frame, offset, notes);
        var loops = arrangement.Diagnostics.Count == 0
            ? arrangement.ResultLoops.Select((loop, index) => new SectionLoop(index + 1, true, loop.SignedArea > 0d ? "ccw" : "cw", ComputeBoundingBox2D(loop.Fragments.SelectMany(x => x.Geometry switch
                {
                    LineArcLineSegment2D l => new[] { new Point2D(l.Start.X, l.Start.Y), new Point2D(l.End.X, l.End.Y) },
                    LineArcCircularArc2D a => new[] { new Point2D(a.Center.X + a.Radius * Math.Cos(a.StartAngleRadians), a.Center.Y + a.Radius * Math.Sin(a.StartAngleRadians)), new Point2D(a.Center.X + a.Radius * Math.Cos(a.StartAngleRadians + a.SweepAngleRadians), a.Center.Y + a.Radius * Math.Sin(a.StartAngleRadians + a.SweepAngleRadians)) },
                    _ => [] }).ToArray()), loop.Fragments.Select(ToSectionSegment).ToArray(), loop.IsOuter ? "Outer" : "Inner"))
                .Concat(rawSegments.Where(x => x.IsClosed).GroupBy(x => $"{x.Center!.U:R}:{x.Center.V:R}:{x.Radius:R}", StringComparer.Ordinal).Select((g, index) => FullCircleLoop(arrangement.ResultLoops.Count + index + 1, g.First()))).ToArray()
            : Array.Empty<SectionLoop>();
        notes.AddRange(arrangement.Diagnostics);
        notes.Add($"section-normalization:raw={rawSegments.Count}:canonicalVertices={arrangement.IntersectionVertices.Count}:atomic={arrangement.AtomicFragments.Count}:collapsedDuplicates={arrangement.CoincidentFragmentCount}:loops={loops.Length}");
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
        var unaccounted = arrangement.Diagnostics.Where(x => x.StartsWith("OpenSection:unaccounted-atomic-fragments:", StringComparison.Ordinal)).Select(x => x.Split(':')).Select(x => x.Length > 2 && int.TryParse(x[2], out var n) ? n : 0).Sum();
        var normalization = new SectionNormalizationDiagnostics(rawSegments.Count, arrangement.IntersectionVertices.Count, arrangement.AtomicFragments.Count,
            arrangement.CoincidentFragmentCount, loops.Length, loops.Count(x => x.Role == "Outer"), loops.Count(x => x.Role == "Inner"), unaccounted, rawSegments.Select((x, i) => new SectionFragmentEvidence($"raw:{i}", x.SourceFace, x.SourceEntity ?? $"ADVANCED_FACE:{x.SourceFace}", x.SurfaceFamily ?? "Unknown", x.Kind.ToString(), x.Kind == RawSectionSegmentKind.Arc ? "AngularRadians" : "NormalizedLinear", 0d, x.Kind == RawSectionSegmentKind.Arc ? x.SweepRadians ?? 0d : 1d, x.Start, x.End, x.Center, x.Radius, x.MaterialSideEvidence ?? "unresolved")).ToArray(), arrangement.Diagnostics,
            arrangement.IntersectionTime.TotalMilliseconds, arrangement.SplitTime.TotalMilliseconds,
            arrangement.ClassificationTime.TotalMilliseconds, arrangement.ReconstructionTime.TotalMilliseconds);
        return new SectionAnalysisResult(metadata, summary, loops, notes, normalization);
    }

    private static ProfileArrangement2D NormalizeSectionFragments(IReadOnlyList<RawSectionSegment> raw, SectionFrame frame, double offset, ICollection<string> notes)
    {
        var sources = new List<ArrangementSourceCurve2D>();
        foreach (var (segment, index) in raw.Select((x, i) => (x, i)))
        {
            var provenance = new ProfileSegmentProvenance($"step-section:{frame.FixedAxis}:{offset:R}:{index}", segment.SourceEntity ?? $"face:{segment.SourceFace}", $"face:{segment.SourceFace}", $"STEP plane/surface intersection; {segment.MaterialSideEvidence ?? "material-side-unresolved"}", "XY");
            var common = (StableId: $"step:{segment.SourceFace}:{index}", Provenance: provenance);
            switch (segment.Kind)
            {
                case RawSectionSegmentKind.Line:
                    sources.Add(new(common.StableId, $"face:{segment.SourceFace}", PrismaticProfileIntent.Base, "STEP", "Boundary", $"segment:{index}", new LineArcLineSegment2D((segment.Start.U, segment.Start.V), (segment.End.U, segment.End.V)), common.Provenance));
                    break;
                case RawSectionSegmentKind.Arc when segment.Center is not null && segment.Radius is not null && segment.SweepRadians is not null:
                    var start = Math.Atan2(segment.Start.V - segment.Center.V, segment.Start.U - segment.Center.U);
                    var sweep = string.Equals(segment.Direction, "cw", StringComparison.Ordinal) ? -Math.Abs(segment.SweepRadians.Value) : Math.Abs(segment.SweepRadians.Value);
                    // A full circle is already a closed analytic component. It is
                    // accounted for separately by the caller; adding a synthetic
                    // seam to the graph would turn a valid circle into a false
                    // high-valence coincidence with adjacent face partitions.
                    if (Math.Abs(sweep) >= 2d * Math.PI - 1e-9d)
                    {
                        notes.Add($"section-normalization:closed-full-circle:fragment={index}:face={segment.SourceFace}");
                    }
                    else sources.Add(new(common.StableId, $"face:{segment.SourceFace}", PrismaticProfileIntent.Base, "STEP", "Boundary", $"segment:{index}", new LineArcCircularArc2D((segment.Center.U, segment.Center.V), segment.Radius.Value, start, sweep), common.Provenance));
                    break;
                default:
                    notes.Add($"UnsupportedSectionCurve:fragment={index}:face={segment.SourceFace}:family={segment.Kind}:reason={segment.UnsupportedReason ?? "missing analytic support"}");
                    break;
            }
        }
        return ProfileArrangementBuilder.NormalizeBoundary("XY", sources, $"section:{frame.FixedAxis}={offset:R}");
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
        var faceSurfaceKinds = BuildFaceSurfaceKinds(body, familyNames: false);

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

    public static RayMapResult AnalyzeRayMap(string stepPath, string plane, string direction, int cols, int rows, (double U, double V)? point = null)
    {
        var (fullPath, body) = ImportStepBody(stepPath);
        return AnalyzeImportedBodyRayMap(body, fullPath, plane, direction, cols, rows, point);
    }

    public static SixViewMapResult AnalyzeSixViewMapSummary(string stepPath, int cols, int rows)
    {
        var (fullPath, body) = ImportStepBody(stepPath);
        return AnalyzeImportedBodySixViewMapSummary(body, fullPath, cols, rows);
    }

    public static SixViewMapResult AnalyzeSixViewMapEvidenceBundle(string stepPath, int cols, int rows)
    {
        var result = AnalyzeSixViewMapSummary(stepPath, cols, rows);
        var ranked = RankSixViewProbes(result.Views, stepPath, cols, rows, 30).ToArray();
        return result with
        {
            RankedProbes = ranked,
            EvidenceBundle = new EvidenceBundle(
                stepPath,
                new EvidenceBundleCoarseMap([cols, rows], result.Views.Count, true),
                ranked,
                ranked.SelectMany(r => r.RecommendedActions).Take(90).ToArray(),
                Array.Empty<object>(),
                new EvidenceBundleLimits(30, 0),
                ["A6 does not execute follow-up probes automatically; commands are bounded local evidence requests.", "Local map bounds are recommendations because analyze map does not yet accept explicit --bounds."])
        };
    }

    public static SixViewMapResult AnalyzeImportedBodySixViewMapSummary(BrepBody body, string stepPath, int cols, int rows)
    {
        if (rows <= 0 || cols <= 0) throw new InvalidOperationException("Map resolution must be positive.");
        (string Name, string Plane, string Direction)[] definitions =
        [
            ("top", "xy", "-z"),
            ("bottom", "xy", "+z"),
            ("right", "yz", "-x"),
            ("left", "yz", "+x"),
            ("back", "xz", "+y"),
            ("front", "xz", "-y")
        ];

        var views = new List<SixViewMapView>();
        var diagnostics = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            var map = AnalyzeImportedBodyRayMap(body, stepPath, definition.Plane, definition.Direction, cols, rows);
            foreach (var diagnostic in map.Diagnostics)
            {
                diagnostics.Add($"{definition.Name}: {diagnostic}");
            }

            views.Add(BuildSixViewSummary(definition.Name, map, cols, rows, stepPath));
        }

        var suggested = views.SelectMany(v => v.SuggestedProbes).Take(30).ToArray();
        return new SixViewMapResult("six-view-summary", "analyze-map-v1", [cols, rows], views, suggested, diagnostics.ToArray());
    }

    public static RayMapResult AnalyzeImportedBodyRayMap(BrepBody body, string stepPath, string plane, string direction, int cols, int rows, (double U, double V)? point = null)
    {
        if (rows <= 0 || cols <= 0) throw new InvalidOperationException("Map resolution must be positive.");
        var bbox = TryComputeBodyBoundingBox(body) ?? throw new InvalidOperationException("Map probe requires body vertex coordinates to compute a bounding box.");
        var frame = ResolveRayMapFrame(plane, direction, bbox);
        var faceSurfaceKinds = BuildFaceSurfaceKinds(body, familyNames: true);
        var diagnostics = new List<string>();
        var diagnosticSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var family in faceSurfaceKinds.Values.Distinct(StringComparer.Ordinal).Where(f => f is not "plane" and not "cylinder" and not "sphere" and not "cone" and not "torus").OrderBy(f => f, StringComparer.Ordinal))
        {
            AddDiagnostic(diagnostics, diagnosticSet, $"Exact ray intersection unavailable for {family}; used tessellated fallback.");
        }

        var tessellation = BrepDisplayTessellator.TessellateBoundedPartial(body, DisplayTessellationOptions.Default, TimeSpan.FromSeconds(10));
        foreach (var d in tessellation.FaceDiagnostics ?? [])
        {
            AddDiagnostic(diagnostics, diagnosticSet, $"Tessellation diagnostic for face {d.FaceId?.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"}: {d.Message}");
        }

        var samples = new List<RayMapSample>();
        var allHeights = new List<double>();
        var surfaceHits = new Dictionary<string, int>(StringComparer.Ordinal);
        var total = point.HasValue ? 1 : rows * cols;
        var analyticHitCount = 0;
        var cirHitCount = 0;
        var tessellatedFallbackHitCount = 0;
        var unsupportedSampleCount = 0;

        for (var j = 0; j < (point.HasValue ? 1 : rows); j++)
        {
            var v = point?.V ?? frame.MinV + (rows == 1 ? 0.5d : j / (double)(rows - 1)) * frame.RangeV;
            for (var i = 0; i < (point.HasValue ? 1 : cols); i++)
            {
                var u = point?.U ?? frame.MinU + (cols == 1 ? 0.5d : i / (double)(cols - 1)) * frame.RangeU;
                var origin = frame.PlaneOrigin + (frame.UAxis * u) + (frame.VAxis * v) - (frame.RayDirection * Math.Max(1e-5d, ToleranceContext.Default.Linear * 64d));
                var analyticHits = IntersectAnalyticRay(body, origin, frame.RayDirection, faceSurfaceKinds).ToArray();
                var analyticFaceIds = analyticHits.Select(h => h.FaceIndex).Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();
                var tessellatedHits = IntersectTessellatedRay(tessellation, origin, frame.RayDirection, faceSurfaceKinds)
                    .Where(h => h.SurfaceFamily is not "plane" && (h.FaceIndex is null || !analyticFaceIds.Contains(h.FaceIndex.Value)))
                    .ToArray();

                foreach (var hit in tessellatedHits)
                {
                    var family = hit.SurfaceFamily ?? "unknown";
                    AddDiagnostic(diagnostics, diagnosticSet, $"Exact ray intersection unavailable for {family}; used tessellated fallback.");
                }

                var hits = analyticHits.Concat(tessellatedHits)
                    .OrderBy(h => h.T)
                    .ToArray();
                var first = hits.FirstOrDefault();
                var last = hits.LastOrDefault();
                if (first is not null)
                {
                    allHeights.Add(frame.Height(first.Position));
                    if (first.SurfaceFamily is { } sf)
                    {
                        surfaceHits[sf] = surfaceHits.TryGetValue(sf, out var c) ? c + 1 : 1;
                    }
                }
                else
                {
                    unsupportedSampleCount++;
                }

                var modeCounts = hits.GroupBy(h => h.IntersectionMode, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
                modeCounts.TryAdd("analytic", 0);
                modeCounts.TryAdd("cir-evaluated", 0);
                modeCounts.TryAdd("tessellated-fallback", 0);
                modeCounts.TryAdd("unsupported", 0);
                analyticHitCount += modeCounts["analytic"];
                cirHitCount += modeCounts["cir-evaluated"];
                tessellatedFallbackHitCount += modeCounts["tessellated-fallback"];

                samples.Add(new RayMapSample(i, j, u, v, first is not null, first, last, hits.Length, hits, new SortedDictionary<string, int>(modeCounts, StringComparer.Ordinal)));
            }
        }

        var summary = new RayMapSummary(
            samples.Count(s => s.Hit) / (double)total,
            allHeights.Count == 0 ? null : [allHeights.Min(), allHeights.Max()],
            new SortedDictionary<string, int>(surfaceHits, StringComparer.Ordinal),
            analyticHitCount,
            cirHitCount,
            tessellatedFallbackHitCount,
            unsupportedSampleCount);
        var bounds = new RayMapBounds([frame.MinU, frame.MaxU], [frame.MinV, frame.MaxV]);
        var mode = point.HasValue ? "point" : "grid";
        var pointArray = point.HasValue ? new[] { point.Value.U, point.Value.V } : null;
        var resultMode = tessellatedFallbackHitCount > 0 ? "analytic-first-with-tessellated-fallback" : "analytic";
        var result = new RayMapResult(mode, plane.ToLowerInvariant(), direction.ToLowerInvariant(), point.HasValue ? null : [cols, rows], pointArray, bounds, samples, samples.Sum(s => s.HitCount), point.HasValue ? samples.Single().Hits : null, summary, resultMode, "analytic-cir-tessellated-fallback", diagnostics);
        return point.HasValue ? result with { PointSummary = BuildCompactPointProbeSummary(samples.Single(), direction) } : result;
    }

    private static SixViewMapView BuildSixViewSummary(string name, RayMapResult map, int cols, int rows, string stepPath)
    {
        var sampleCount = map.Samples.Count;
        var hitCount = map.Samples.Count(s => s.Hit);
        var bands = BuildDominantBands(map.Samples, sampleCount, map.Direction);
        var backendCounts = new SortedDictionary<string, int>(StringComparer.Ordinal)
        {
            ["analytic"] = map.Summary.AnalyticHitCount,
            ["cir-evaluated"] = map.Summary.CirHitCount,
            ["tessellated-fallback"] = map.Summary.TessellatedFallbackHitCount,
            ["unsupported"] = map.Summary.UnsupportedSampleCount
        };
        var backendHitTotal = map.Summary.AnalyticHitCount + map.Summary.CirHitCount + map.Summary.TessellatedFallbackHitCount;
        var summary = new SixViewMapSummary(
            sampleCount,
            hitCount,
            sampleCount == 0 ? 0d : hitCount / (double)sampleCount,
            map.Summary.HeightRange,
            bands,
            map.Summary.SurfaceFamiliesHit,
            backendCounts,
            backendHitTotal == 0 ? 0d : map.Summary.TessellatedFallbackHitCount / (double)backendHitTotal);

        var compactGrid = cols <= 64 && rows <= 64 ? BuildCompactGrid(map.Samples, cols, rows, bands, map.Direction) : null;
        var components = BuildSixViewComponents(name, map, cols, rows, bands);
        var probes = BuildSuggestedProbes(name, map.Plane, map.Direction, stepPath, components).Take(10).ToArray();
        var measured = BuildMeasuredSummary(name, map, summary, components);
        return new SixViewMapView(name, map.Plane, map.Direction, summary, compactGrid, components, probes, measured);
    }

    private static IReadOnlyList<DominantBand> BuildDominantBands(IReadOnlyList<RayMapSample> samples, int sampleCount, string direction)
    {
        var groups = new Dictionary<double, (int Count, int Fallback)>();
        var noHit = 0;
        foreach (var sample in samples)
        {
            if (!sample.Hit || sample.FirstHit is null)
            {
                noHit++;
                continue;
            }

            var value = Math.Round(GetRayAxisScalar(sample.FirstHit.Position, direction), 4, MidpointRounding.AwayFromZero);
            groups.TryGetValue(value, out var current);
            var fallback = sample.FirstHit.IntersectionMode == "tessellated-fallback" ? 1 : 0;
            groups[value] = (current.Count + 1, current.Fallback + fallback);
        }

        var bands = groups
            .OrderByDescending(kvp => kvp.Value.Count)
            .ThenBy(kvp => kvp.Key)
            .Take(5)
            .Select(kvp => new DominantBand(kvp.Key, kvp.Value.Count, sampleCount == 0 ? 0d : kvp.Value.Count / (double)sampleCount, null, kvp.Value.Fallback > kvp.Value.Count / 2d))
            .ToList();
        if (noHit > 0)
        {
            bands.Add(new DominantBand(null, noHit, sampleCount == 0 ? 0d : noHit / (double)sampleCount, "no-hit", false));
        }

        return bands.OrderByDescending(b => b.SampleCount).ThenBy(b => b.Value ?? double.PositiveInfinity).ToArray();
    }

    private static double GetRayAxisScalar(Point3D position, string direction)
    {
        return direction.EndsWith("x", StringComparison.OrdinalIgnoreCase)
            ? position.X
            : direction.EndsWith("y", StringComparison.OrdinalIgnoreCase)
                ? position.Y
                : position.Z;
    }

    private static CompactGrid BuildCompactGrid(IReadOnlyList<RayMapSample> samples, int cols, int rows, IReadOnlyList<DominantBand> bands, string direction)
    {
        var symbols = "0123456789";
        var valueToSymbol = bands.Where(b => b.Value.HasValue)
            .Select((b, index) => (Value: b.Value!.Value, Symbol: symbols[Math.Min(index, symbols.Length - 1)]))
            .ToDictionary(x => x.Value, x => x.Symbol);
        var byCell = samples.ToDictionary(s => (s.I, s.J));
        var gridRows = new List<string>();
        for (var j = rows - 1; j >= 0; j--)
        {
            var chars = new char[cols];
            for (var i = 0; i < cols; i++)
            {
                if (!byCell.TryGetValue((i, j), out var sample) || !sample.Hit || sample.FirstHit is null)
                {
                    chars[i] = '.';
                    continue;
                }

                if (sample.FirstHit.IntersectionMode == "tessellated-fallback")
                {
                    chars[i] = '~';
                    continue;
                }

                if (sample.FirstHit.IntersectionMode == "unsupported")
                {
                    chars[i] = '?';
                    continue;
                }

                var value = Math.Round(GetRayAxisScalar(sample.FirstHit.Position, direction), 4, MidpointRounding.AwayFromZero);
                chars[i] = valueToSymbol.TryGetValue(value, out var symbol) ? symbol : '9';
            }

            gridRows.Add(new string(chars));
        }

        return new CompactGrid("height-band-ascii", cols, rows, new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["."] = "no-hit",
            ["0"] = "most common rounded first-hit axis value",
            ["1"] = "next most common rounded first-hit axis value",
            ["2-9"] = "additional rounded first-hit axis value bands",
            ["~"] = "tessellated-fallback or approximate first hit",
            ["?"] = "unsupported or unknown first hit"
        }, gridRows);
    }

    private static SixViewMapComponents BuildSixViewComponents(string view, RayMapResult map, int cols, int rows, IReadOnlyList<DominantBand> bands)
    {
        const int limit = 10;
        var noHit = FindComponents(view, "no-hit", cols, rows, map.Samples, s => !s.Hit || s.FirstHit is null, _ => "no-hit")
            .Select((c, index) => c with
            {
                ComponentId = $"{view}.nohit.{index}",
                ClassificationHint = c.TouchesBorder ? "silhouette-or-exterior-gap" : "interior-opening-candidate",
                Confidence = c.TouchesBorder ? (c.CellCount >= 8 ? "medium" : "low") : (c.CellCount >= 4 ? "medium" : "low")
            })
            .OrderByDescending(c => c.CellCount)
            .ThenBy(c => c.BboxCells.MinJ)
            .ThenBy(c => c.BboxCells.MinI)
            .ToArray();

        var bandValues = bands.Where(b => b.Value.HasValue).Select((b, i) => (Value: b.Value!.Value, Symbol: i.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToDictionary(x => x.Value, x => x.Symbol);
        var heightBands = FindComponents(view, "height-band", cols, rows, map.Samples, s => s.Hit && s.FirstHit is not null && s.FirstHit.IntersectionMode != "tessellated-fallback", s =>
            Math.Round(GetRayAxisScalar(s.FirstHit!.Position, map.Direction), 4, MidpointRounding.AwayFromZero).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture))
            .Select((c, index) =>
            {
                var value = double.TryParse(c.Band, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : (double?)null;
                var symbol = value.HasValue && bandValues.TryGetValue(value.Value, out var b) ? b : c.Band;
                return c with { ComponentId = $"{view}.band.{index}", Band = symbol, RepresentativeValue = value, Confidence = c.CellCount >= 4 ? "medium" : "low" };
            })
            .OrderByDescending(c => c.CellCount)
            .Where(c => IsSignificantComponent(c, rows * cols)).Take(20)
            .ToArray();

        var surfaceFamilies = FindComponents(view, "surface-family", cols, rows, map.Samples, s => s.Hit && s.FirstHit is not null, s => s.FirstHit!.SurfaceFamily ?? "unknown")
            .Select((c, index) => c with { ComponentId = $"{view}.surface.{index}", SurfaceFamily = c.Band, Band = null, Confidence = c.CellCount >= 3 ? "medium" : "low" })
            .OrderByDescending(c => c.CellCount)
            .Where(c => IsSignificantComponent(c, rows * cols)).Take(20)
            .ToArray();

        var fallback = FindComponents(view, "fallback", cols, rows, map.Samples, s => s.FirstHit?.IntersectionMode == "tessellated-fallback", _ => "tessellated-fallback")
            .Select((c, index) => c with { ComponentId = $"{view}.fallback.{index}", BackendModeDominance = "tessellated-fallback", Confidence = c.CellCount >= 3 ? "medium" : "low" })
            .OrderByDescending(c => c.CellCount)
            .ToArray();

        var omitted = Math.Max(0, noHit.Length - limit) + Math.Max(0, heightBands.Length - limit) + Math.Max(0, surfaceFamilies.Length - limit) + Math.Max(0, fallback.Length - limit);
        return new SixViewMapComponents(noHit.Take(limit).ToArray(), heightBands.Take(limit).ToArray(), surfaceFamilies.Take(limit).ToArray(), fallback.Take(limit).ToArray(), omitted > 0, omitted);
    }

    private static bool IsSignificantComponent(MapComponent component, int totalCells) => component.CellCount > 1 || component.Coverage >= 0.02d;

    private static IReadOnlyList<MapComponent> FindComponents(string view, string kind, int cols, int rows, IReadOnlyList<RayMapSample> samples, Func<RayMapSample, bool> include, Func<RayMapSample, string> keySelector)
    {
        var byCell = samples.ToDictionary(s => (s.I, s.J));
        var visited = new HashSet<(int I, int J)>();
        var components = new List<MapComponent>();
        for (var j = 0; j < rows; j++)
        for (var i = 0; i < cols; i++)
        {
            if (visited.Contains((i, j)) || !byCell.TryGetValue((i, j), out var seed) || !include(seed)) continue;
            var key = keySelector(seed);
            var queue = new Queue<RayMapSample>();
            var cells = new List<RayMapSample>();
            queue.Enqueue(seed);
            visited.Add((i, j));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                cells.Add(current);
                foreach (var (ni, nj) in new[] { (current.I - 1, current.J), (current.I + 1, current.J), (current.I, current.J - 1), (current.I, current.J + 1) })
                {
                    if (ni < 0 || nj < 0 || ni >= cols || nj >= rows || visited.Contains((ni, nj)) || !byCell.TryGetValue((ni, nj), out var next) || !include(next) || keySelector(next) != key) continue;
                    visited.Add((ni, nj));
                    queue.Enqueue(next);
                }
            }

            var bbox = new CellBoundingBox(cells.Min(c => c.I), cells.Min(c => c.J), cells.Max(c => c.I), cells.Max(c => c.J));
            var centroidCell = new[] { cells.Average(c => c.I), cells.Average(c => c.J) };
            var centroidUv = new[] { cells.Average(c => c.U), cells.Average(c => c.V) };
            var backend = cells.Select(c => c.FirstHit?.IntersectionMode ?? "unsupported").GroupBy(x => x, StringComparer.Ordinal).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).First().Key;
            components.Add(new MapComponent("", kind, view, cells.Count, cells.Count / (double)(cols * rows), cells.Any(c => c.I == 0 || c.J == 0 || c.I == cols - 1 || c.J == rows - 1), bbox, centroidCell, centroidUv, null, "low", key, null, null, backend));
        }

        return components;
    }

    private static IReadOnlyList<SuggestedMapProbe> BuildSuggestedProbes(string view, string plane, string direction, string stepPath, SixViewMapComponents components)
    {
        var probes = new List<SuggestedMapProbe>();
        foreach (var component in components.NoHit.Take(3))
        {
            var reason = component.TouchesBorder ? "Border-touching silhouette gap; probe to confirm exterior or edge cutout region." : "Center of interior no-hit component; probe to distinguish through-opening, recess, or missing hit.";
            probes.Add(MakeProbe($"{component.ComponentId}.center", view, plane, direction, stepPath, component, reason));
        }

        foreach (var component in components.SurfaceFamilies.Where(c => c.SurfaceFamily is "cylinder" or "cone" or "sphere" or "torus").Take(3))
        {
            var family = component.SurfaceFamily == "cylinder" ? "cylindrical" : component.SurfaceFamily;
            probes.Add(MakeProbe($"{component.ComponentId}.center", view, plane, direction, stepPath, component, $"Center of {family} hit component; inspect possible round or curved feature."));
        }

        foreach (var component in components.HeightBands.Take(2))
        {
            var reason = component == components.HeightBands.FirstOrDefault()
                ? "Representative point on dominant height plateau."
                : "Representative point on isolated height band component.";
            probes.Add(MakeProbe($"{component.ComponentId}.center", view, plane, direction, stepPath, component, reason));
        }

        foreach (var component in components.Fallback.Take(2))
        {
            probes.Add(MakeProbe($"{component.ComponentId}.center", view, plane, direction, stepPath, component, "Center of tessellated fallback component; measurement is approximate and may need analytic support."));
        }

        return probes.Take(10).ToArray();
    }

    private static SuggestedMapProbe MakeProbe(string id, string view, string plane, string direction, string stepPath, MapComponent component, string reason)
    {
        var pointText = $"{component.CentroidUv[0]:0.####},{component.CentroidUv[1]:0.####}";
        var command = $"aetheris analyze map {stepPath} --plane {plane} --direction {direction} --point {pointText} --json";
        return new SuggestedMapProbe(id, view, plane, direction, component.CentroidUv, reason, command, component.ComponentId);
    }


    private static CompactPointProbeSummary BuildCompactPointProbeSummary(RayMapSample sample, string direction)
    {
        static CompactHitSummary? Hit(RayMapHit? h) => h is null ? null : new CompactHitSummary(h.SurfaceFamily, h.Position, h.FaceIndex, h.IntersectionMode);
        var sequence = sample.Hits.Select(h => h.SurfaceFamily ?? "unknown").ToArray();
        if (sequence.Length > 8)
        {
            sequence = sequence.Take(4).Concat(["...x" + (sequence.Length - 8).ToString(System.Globalization.CultureInfo.InvariantCulture)]).Concat(sequence.TakeLast(4)).ToArray();
        }

        var range = sample.Hits.Count == 0 ? null : new[] { sample.Hits.Min(h => h.T), sample.Hits.Max(h => h.T) };
        return new CompactPointProbeSummary(sample.HitCount, Hit(sample.FirstHit), Hit(sample.LastHit), sequence, sample.IntersectionModes, range, sample.Hits.SelectMany(h => h.Diagnostics).Distinct(StringComparer.Ordinal).Take(8).ToArray());
    }

    private static IReadOnlyList<RankedMapProbe> RankSixViewProbes(IReadOnlyList<SixViewMapView> views, string stepPath, int cols, int rows, int limit)
    {
        var candidates = views.SelectMany(v => v.Components.NoHit.Concat(v.Components.SurfaceFamilies).Concat(v.Components.HeightBands).Concat(v.Components.Fallback).Select(c => (View: v, Component: c)))
            .Select(x => ScoreComponent(x.View, x.Component, stepPath, cols, rows))
            .OrderByDescending(x => x.Score).ThenBy(x => x.View).ThenBy(x => x.ComponentId, StringComparer.Ordinal)
            .Take(limit).ToArray();
        var max = candidates.Length == 0 ? 1d : Math.Max(1e-9d, candidates.Max(c => c.Score));
        return candidates.Select((c, i) => c with { Rank = i + 1, NormalizedScore = Math.Round(c.Score / max, 4) }).ToArray();
    }

    private static RankedMapProbe ScoreComponent(SixViewMapView view, MapComponent c, string stepPath, int cols, int rows)
    {
        var reasons = new List<string>(); var terms = new List<string>(); var score = 0.05d;
        if (c.Kind == "no-hit" && !c.TouchesBorder) { score += 0.45; reasons.Add("interior no-hit component"); terms.Add("interior-no-hit"); }
        if (c.Kind == "no-hit" && c.TouchesBorder) { score -= 0.10; reasons.Add("border-touching exterior/silhouette candidate"); terms.Add("border-no-hit"); }
        if (c.SurfaceFamily is "cylinder" or "cone" or "torus") { score += 0.35; reasons.Add($"{c.SurfaceFamily} surface-family cluster"); terms.Add("curved-analytic-family"); }
        if (c.SurfaceFamily is "sphere") { score += 0.20; reasons.Add("sphere surface-family cluster"); terms.Add("curved-analytic-family"); }
        if (c.BackendModeDominance == "analytic") { score += 0.12; reasons.Add("analytic provenance"); terms.Add("analytic-provenance"); }
        if (c.BackendModeDominance == "tessellated-fallback") { score += 0.20; reasons.Add("fallback component needs truth-checking"); terms.Add("fallback-uncertain"); }
        if (c.Kind == "height-band" && c.Coverage < 0.20) { score += 0.18; reasons.Add("small isolated height-band component"); terms.Add("local-height-band"); }
        if (!c.TouchesBorder) { score += 0.10; reasons.Add("interior to view bounds"); terms.Add("interior-locality"); }
        var centerDistance = Math.Abs(c.CentroidCell[0] - (cols - 1) / 2d) / Math.Max(1, cols) + Math.Abs(c.CentroidCell[1] - (rows - 1) / 2d) / Math.Max(1, rows);
        if (centerDistance < 0.25) { score += 0.08; reasons.Add("central region"); terms.Add("centrality"); }
        score += Math.Min(0.12, c.CellCount / 64d);
        var uncertainty = c.BackendModeDominance == "tessellated-fallback" ? 0.55 : c.Confidence == "low" ? 0.35 : 0.2;
        var classification = c.ClassificationHint ?? (c.SurfaceFamily is null ? c.Kind : c.SurfaceFamily + "-feature-candidate");
        var actions = BuildEvidenceActions(view, c, stepPath, classification).ToArray();
        return new RankedMapProbe(0, Math.Round(Math.Clamp(score, 0d, 1d), 4), 0, "componentProbe", view.Name, c.ComponentId, classification, reasons.Count == 0 ? ["bounded component worth sampling"] : reasons, terms, uncertainty, actions.FirstOrDefault()?.Kind ?? "pointProbe", actions);
    }

    private static IEnumerable<EvidenceAction> BuildEvidenceActions(SixViewMapView view, MapComponent c, string stepPath, string reason)
    {
        var pointText = $"{c.CentroidUv[0]:0.####},{c.CentroidUv[1]:0.####}";
        yield return new EvidenceAction("pointProbe", view.Name, $"aetheris analyze map {stepPath} --plane {view.Plane} --direction {view.Direction} --point {pointText} --json", reason);
        var axes = view.Plane switch { "xy" => ("--yz", "--xz"), "xz" => ("--yz", "--xy"), "yz" => ("--xz", "--xy"), _ => ("--xy", "--xz") };
        yield return new EvidenceAction("sectionProbe", view.Name, $"aetheris analyze section {stepPath} {axes.Item1} --offset {c.CentroidUv[0]:0.####} --json", "Section through component centroid along first view axis.");
        yield return new EvidenceAction("sectionProbe", view.Name, $"aetheris analyze section {stepPath} {axes.Item2} --offset {c.CentroidUv[1]:0.####} --json", "Section through component centroid along second view axis.");
        yield return new EvidenceAction("localMap", view.Name, $"suggestedLocalMapUnsupported: analyze map does not yet accept explicit local bounds", "Refine around ranked component when --bounds is added.", new { u = new[] { c.CentroidUv[0] - 1d, c.CentroidUv[0] + 1d }, v = new[] { c.CentroidUv[1] - 1d, c.CentroidUv[1] + 1d } }, [16, 16]);
    }

    private static IReadOnlyList<string> BuildMeasuredSummary(string name, RayMapResult map, SixViewMapSummary summary, SixViewMapComponents? components = null)
    {
        var lines = new List<string>
        {
            $"{name} view: {summary.HitCoverage:P1} of samples hit the model."
        };
        var dominant = summary.DominantBands.FirstOrDefault(b => b.Value.HasValue);
        if (dominant is not null)
        {
            lines.Add($"Dominant rounded first-hit axis value is {dominant.Value:0.####} across {dominant.Coverage:P1} of samples.");
        }

        var noHit = summary.DominantBands.FirstOrDefault(b => b.Meaning == "no-hit");
        if (noHit is not null)
        {
            lines.Add($"{noHit.Coverage:P1} of samples are no-hit, indicating measured empty rays in this view.");
        }

        if (components is not null)
        {
            var interiorNoHit = components.NoHit.Count(c => c.ClassificationHint == "interior-opening-candidate");
            var borderNoHit = components.NoHit.Count(c => c.ClassificationHint == "silhouette-or-exterior-gap");
            if (interiorNoHit > 0) lines.Add($"{name} view: found {interiorNoHit} interior no-hit component(s); largest covers {components.NoHit.Where(c => c.ClassificationHint == "interior-opening-candidate").Max(c => c.Coverage):P1} of the sampled view.");
            if (borderNoHit > 0) lines.Add($"{name} view: found {borderNoHit} border-touching no-hit component(s), likely silhouette or exterior gap regions.");
            var curved = components.SurfaceFamilies.FirstOrDefault(c => c.SurfaceFamily is "cylinder" or "cone" or "sphere" or "torus");
            if (curved is not null) lines.Add($"{name} view: largest {curved.SurfaceFamily} hit component is centered near ({curved.CentroidUv[0]:0.####}, {curved.CentroidUv[1]:0.####}); inspect with suggested probe.");
        }

        lines.Add(summary.FallbackRatio > 0d
            ? $"{name} view includes tessellated fallback hits; fallback ratio is {summary.FallbackRatio:P1} of hit intersections."
            : $"{name} view hit intersections are reported without tessellated fallback.");
        return lines;
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
            ["linear-extrusion"] = 0,
            ["surface-of-revolution"] = 0,
            ["other"] = 0
        };
        var curveFamilies = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["line"] = 0,
            ["circle"] = 0,
            ["hyperbola"] = 0,
            ["ellipse"] = 0,
            ["bspline"] = 0,
            ["unsupported"] = 0
        };

        foreach (var edge in topology.Edges)
        {
            if (!body.TryGetEdgeCurveGeometry(edge.Id, out var curve) || curve is null) { curveFamilies["unsupported"]++; continue; }
            var key = curve.Kind switch
            {
                CurveGeometryKind.Line3 => "line",
                CurveGeometryKind.Circle3 => "circle",
                CurveGeometryKind.Hyperbola3 => "hyperbola",
                CurveGeometryKind.Ellipse3 => "ellipse",
                CurveGeometryKind.BSpline3 => "bspline",
                _ => "unsupported"
            };
            curveFamilies[key]++;
        }

        foreach (var face in topology.Faces)
        {
            if (!body.TryGetFaceSurface(face.Id, out var surface) || surface is null)
            {
                surfaceFamilies["other"]++;
                continue;
            }

            switch (surface.Kind)
            {
                case SurfaceGeometryKind.Plane: IncrementSurfaceFamily(surfaceFamilies, "plane"); break;
                case SurfaceGeometryKind.Cylinder: IncrementSurfaceFamily(surfaceFamilies, "cylinder"); break;
                case SurfaceGeometryKind.Cone: IncrementSurfaceFamily(surfaceFamilies, "cone"); break;
                case SurfaceGeometryKind.Sphere: IncrementSurfaceFamily(surfaceFamilies, "sphere"); break;
                case SurfaceGeometryKind.Torus: IncrementSurfaceFamily(surfaceFamilies, "torus"); break;
                case SurfaceGeometryKind.LinearExtrusion: IncrementSurfaceFamily(surfaceFamilies, "linear-extrusion"); break;
                case SurfaceGeometryKind.SurfaceOfRevolution: IncrementSurfaceFamily(surfaceFamilies, "surface-of-revolution"); break;
                case SurfaceGeometryKind.BSplineSurfaceWithKnots: IncrementSurfaceFamily(surfaceFamilies, "bspline"); break;
                default: IncrementSurfaceFamily(surfaceFamilies, "other"); break;
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
        var loopsConnected = AreOrderedLoopsConnected(body, out var disconnectedLoop);
        if (!loopsConnected)
        {
            notes.Add($"Face loop {disconnectedLoop} is not connected in declared coedge order; edge-use manifold incidence is insufficient for enclosed-manifold.");
        }
        var structural = nonManifoldEdges == 0 && loopsConnected
            ? "enclosed-manifold"
            : !loopsConnected ? "invalid-face-loops" : (leakyEdges > 0 ? "leaky-or-open" : "non-manifold");
        var basis = "derived from ordered directed coedge traversal plus imported topology edge-to-face coedge incidence counts";

        return new AnalyzeSummary(
            topology.Bodies.Count(),
            topology.Shells.Count(),
            topology.Faces.Count(),
            topology.Edges.Count(),
            topology.Vertices.Count(),
            bbox,
            structural,
            surfaceFamilies,
            curveFamilies,
            basis,
            "mm",
            "assumed; STEP import length units not yet preserved",
            BuildIdRange(topology.Faces.Select(f => f.Id.Value)),
            BuildIdRange(topology.Edges.Select(e => e.Id.Value)),
            BuildIdRange(topology.Vertices.Select(v => v.Id.Value)));
    }

    private static bool AreOrderedLoopsConnected(BrepBody body, out int disconnectedLoop)
    {
        foreach (var loop in body.Topology.Loops.OrderBy(loop => loop.Id.Value))
        {
            if (loop.CoedgeIds.Count == 0) { disconnectedLoop = loop.Id.Value; return false; }
            for (var index = 0; index < loop.CoedgeIds.Count; index++)
            {
                if (!body.Topology.TryGetCoedge(loop.CoedgeIds[index], out var coedge) || coedge is null
                    || !body.Topology.TryGetEdge(coedge.EdgeId, out var edge) || edge is null
                    || !body.Topology.TryGetCoedge(loop.CoedgeIds[(index + 1) % loop.CoedgeIds.Count], out var next) || next is null
                    || !body.Topology.TryGetEdge(next.EdgeId, out var nextEdge) || nextEdge is null)
                {
                    disconnectedLoop = loop.Id.Value;
                    return false;
                }
                var end = DirectedEdgeUse.Resolve(edge, coedge).EndVertexId;
                var start = DirectedEdgeUse.Resolve(nextEdge, next).StartVertexId;
                if (!VerticesMatch(body, end, start))
                {
                    disconnectedLoop = loop.Id.Value;
                    return false;
                }
            }
        }
        disconnectedLoop = 0;
        return true;
    }

    private static bool VerticesMatch(BrepBody body, VertexId left, VertexId right)
    {
        if (left == right) return true;
        return body.TryGetVertexPoint(left, out var a)
            && body.TryGetVertexPoint(right, out var b)
            && (a - b).Length <= 1e-6d;
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
            .ToList();

        // Periodic circle edges commonly use one seam vertex, so vertex-only bounds collapse
        // their radial extent. Include exact axis-aligned extrema for full-circle bindings.
        var hasAxialCircularSurface = body.Geometry.Surfaces.Any(s => s.Value.Kind is SurfaceGeometryKind.Cylinder or SurfaceGeometryKind.Cone);
        foreach (var binding in hasAxialCircularSurface ? body.Bindings.EdgeBindings : [])
        {
            if (binding.TrimInterval is not { } interval || interval.End - interval.Start < 2d * Math.PI - 1e-9
                || !body.Geometry.TryGetCurve(binding.CurveGeometryId, out var geometry)
                || geometry?.Circle3 is not { } circle)
                continue;
            var x = circle.XAxis.ToVector();
            var y = circle.YAxis.ToVector();
            var dx = circle.Radius * Math.Sqrt(x.X * x.X + y.X * y.X);
            var dy = circle.Radius * Math.Sqrt(x.Y * x.Y + y.Y * y.Y);
            var dz = circle.Radius * Math.Sqrt(x.Z * x.Z + y.Z * y.Z);
            points.Add(new Point3D(circle.Center.X - dx, circle.Center.Y - dy, circle.Center.Z - dz));
            points.Add(new Point3D(circle.Center.X + dx, circle.Center.Y + dy, circle.Center.Z + dz));
        }

        // A full torus is periodic in both parameters. Its seam vertices (and even its
        // circular seam edges) do not reach the extrema of the body, so a vertex-only
        // box can collapse to a point after STEP reimport. Include the exact world-axis
        // extrema only for faces whose boundary is made entirely of full circular seams.
        foreach (var face in body.Topology.Faces)
        {
            if (!body.TryGetFaceSurface(face.Id, out var surface)
                || surface?.Torus is not { } torus
                || !IsFullPeriodicTorusFace(body, face.Id))
            {
                continue;
            }

            var axis = torus.Axis.ToVector();
            var xExtent = (torus.MajorRadius * Math.Sqrt(Math.Max(0d, 1d - (axis.X * axis.X)))) + torus.MinorRadius;
            var yExtent = (torus.MajorRadius * Math.Sqrt(Math.Max(0d, 1d - (axis.Y * axis.Y)))) + torus.MinorRadius;
            var zExtent = (torus.MajorRadius * Math.Sqrt(Math.Max(0d, 1d - (axis.Z * axis.Z)))) + torus.MinorRadius;
            points.Add(new Point3D(torus.Center.X - xExtent, torus.Center.Y - yExtent, torus.Center.Z - zExtent));
            points.Add(new Point3D(torus.Center.X + xExtent, torus.Center.Y + yExtent, torus.Center.Z + zExtent));
        }

        // A bounded polynomial B-spline can reach extrema away from every trim vertex.
        // Sample its native knot domain deterministically so a freeform crown does not
        // collapse to the height of its planar boundary in structured inspection.
        foreach (var surface in body.Geometry.Surfaces.Select(x => x.Value.BSplineSurfaceWithKnots).Where(x => x is not null).Select(x => x!))
        {
            const int samples = 33;
            for (var i = 0; i < samples; i++)
            for (var j = 0; j < samples; j++)
            {
                var u = surface.DomainStartU + (surface.DomainEndU - surface.DomainStartU) * i / (samples - 1d);
                var v = surface.DomainStartV + (surface.DomainEndV - surface.DomainStartV) * j / (samples - 1d);
                points.Add(surface.Evaluate(u, v));
            }
        }

        if (points.Count > 0)
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

    private static bool IsFullPeriodicTorusFace(BrepBody body, FaceId faceId)
    {
        var edgeIds = body.GetLoopIds(faceId)
            .SelectMany(loopId => body.GetCoedgeIds(loopId))
            .Select(coedgeId => body.Topology.GetCoedge(coedgeId))
            .Select(coedge => coedge.EdgeId)
            .Distinct()
            .ToArray();

        if (edgeIds.Length == 0)
        {
            return false;
        }

        foreach (var edgeId in edgeIds)
        {
            if (!body.Bindings.TryGetEdgeBinding(edgeId, out var binding)
                || binding.TrimInterval is not { } interval
                || Math.Abs(Math.Abs(interval.End - interval.Start) - (2d * Math.PI)) > 1e-9d
                || !body.Geometry.TryGetCurve(binding.CurveGeometryId, out var curve)
                || curve?.Circle3 is null)
            {
                return false;
            }
        }

        return true;
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


    private static void IncrementSurfaceFamily(IDictionary<string, int> surfaceFamilies, string family)
    {
        surfaceFamilies.TryGetValue(family, out var count);
        surfaceFamilies[family] = count + 1;
    }

    private static string ToSurfaceFamilyName(SurfaceGeometryKind kind)
        => kind switch
        {
            SurfaceGeometryKind.Plane => "plane",
            SurfaceGeometryKind.Cylinder => "cylinder",
            SurfaceGeometryKind.Cone => "cone",
            SurfaceGeometryKind.Sphere => "sphere",
            SurfaceGeometryKind.Torus => "torus",
            SurfaceGeometryKind.LinearExtrusion => "linear-extrusion",
            SurfaceGeometryKind.SurfaceOfRevolution => "surface-of-revolution",
            SurfaceGeometryKind.BSplineSurfaceWithKnots => "bspline",
            _ => "other"
        };

    private static Dictionary<int, string> BuildFaceSurfaceKinds(BrepBody body, bool familyNames = false)
    {
        var result = new Dictionary<int, string>();
        foreach (var face in body.Topology.Faces)
        {
            if (!body.TryGetFaceSurface(face.Id, out var surface) || surface is null)
            {
                continue;
            }

            result[face.Id.Value] = familyNames ? ToSurfaceFamilyName(surface.Kind) : surface.Kind.ToString();
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
        if (ordered.Length % 2 != 0)
        {
            notes.Add($"OpenSection:planar-face-odd-intersection-count:face={face.Id.Value}:count={ordered.Length}");
            return [];
        }

        var outward = facePlane.Normal.ToVector();
        var hasFaceBinding = body.Bindings.TryGetFaceBinding(face.Id, out var faceBinding);
        if (hasFaceBinding && !faceBinding.SameSense) outward = -outward;
        return Enumerable.Range(0, ordered.Length / 2).Select(i => OrientForMaterialLeft(RawSectionSegment.Line(ProjectPoint(frame, ordered[i * 2].Point), ProjectPoint(frame, ordered[i * 2 + 1].Point)) with { SourceFace = face.Id.Value, SurfaceFamily = "Plane", SourceEntity = $"ADVANCED_FACE:{face.Id.Value}", MaterialSideEvidence = $"faceSameSense={(hasFaceBinding ? faceBinding.SameSense : null)}" }, frame, outward)).ToArray();
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
        // A cylindrical B-rep face may be angularly trimmed by vertical seam edges.
        // The old extractor emitted its entire support circle for every such face,
        // leaving the planar-outline fragments dangling. Preserve the bounded trim.
        var angles = body.GetEdges(face.Id).SelectMany(edge => IntersectEdgeWithSectionPlane(body, edge, frame, epsilon)).Select(p => ProjectPoint(frame, p)).Where(p => Math.Abs(Math.Sqrt((p.U-center.U)*(p.U-center.U)+(p.V-center.V)*(p.V-center.V))-cylinder.Radius) <= epsilon * 32d).Select(p => Math.Atan2(p.V-center.V,p.U-center.U)).Order().Aggregate(new List<double>(), (a,x) => { if(a.Count==0 || Math.Abs(a[^1]-x)>1e-7d) a.Add(x); return a; });
        if (angles.Count == 2)
        {
            var sweep = angles[1] - angles[0];
            if (sweep > Math.PI) { (angles[0], angles[1]) = (angles[1], angles[0]); sweep = 2d * Math.PI - sweep; }
            var start = new Point2D(center.U + cylinder.Radius * Math.Cos(angles[0]), center.V + cylinder.Radius * Math.Sin(angles[0]));
            var end = new Point2D(center.U + cylinder.Radius * Math.Cos(angles[0] + sweep), center.V + cylinder.Radius * Math.Sin(angles[0] + sweep));
            var sameSense = body.Bindings.TryGetFaceBinding(face.Id, out var binding) && binding.SameSense;
            notes.Add($"section-fragment:cylinder:face={face.Id.Value}:sameSense={sameSense}:center=({center.U:R},{center.V:R}):radius={cylinder.Radius:R}:angles=({angles[0]:R},{angles[1]:R}):sweep={sweep:R}:start=({start.U:R},{start.V:R}):end=({end.U:R},{end.V:R})");
            var midAngle = angles[0] + sweep * .5d;
            var radial = (center3D - cylinder.Origin) + (frame.UAxis * (cylinder.Radius * Math.Cos(midAngle))) + (frame.VAxis * (cylinder.Radius * Math.Sin(midAngle)));
            var hasCylinderBinding = body.Bindings.TryGetFaceBinding(face.Id, out var cylinderBinding);
            if (hasCylinderBinding && !cylinderBinding.SameSense) radial = -radial;
            return [OrientForMaterialLeft(RawSectionSegment.Arc(start, end, center, cylinder.Radius, "ccw", sweep) with { SourceFace = face.Id.Value, SurfaceFamily = "Cylinder", SourceEntity = $"ADVANCED_FACE:{face.Id.Value}", MaterialSideEvidence = $"faceSameSense={(hasCylinderBinding ? cylinderBinding.SameSense : null)}" }, frame, radial)];
        }
        if (angles.Count > 2) notes.Add($"UnsupportedSectionCurve:cylinder-trim-ambiguous:face={face.Id.Value}:angularVertices={angles.Count}");
        var fullStart = new Point2D(center.U + cylinder.Radius, center.V);
        return [RawSectionSegment.Arc(fullStart, fullStart, center, cylinder.Radius, "ccw", 2d * double.Pi) with { SourceFace = face.Id.Value, SurfaceFamily = "Cylinder", SourceEntity = $"ADVANCED_FACE:{face.Id.Value}", MaterialSideEvidence = "full-circle:face-boundary" }];
    }

    private static IReadOnlyList<RawSectionSegment> BuildConeFaceSectionSegments(BrepBody body, Face face, ConeSurface cone, SectionFrame frame, double epsilon, ICollection<string> notes)
    {
        var axisDot = Math.Abs(cone.Axis.ToVector().Dot(frame.Normal));
        if (axisDot < 1d - epsilon * 8d) { notes.Add($"UnsupportedSectionCurve:face={face.Id.Value}:surface=Cone:plane-not-normal-to-axis"); return []; }
        var v = (frame.Offset - (cone.Apex - Point3D.Origin).Dot(frame.Normal)) / (cone.Axis.ToVector().Dot(frame.Normal));
        var radius = Math.Abs(v * Math.Tan(cone.SemiAngleRadians));
        if (radius <= epsilon) { notes.Add($"DegenerateLoop:cone-apex-section:face={face.Id.Value}"); return []; }
        var center3D = cone.Apex + cone.Axis.ToVector() * v;
        var center = ProjectPoint(frame, center3D);
        var edgeHits = body.GetEdges(face.Id).SelectMany(edge => IntersectEdgeWithSectionPlane(body, edge, frame, epsilon)).Select(p => ProjectPoint(frame, p)).Where(p => Math.Abs(Math.Sqrt((p.U-center.U)*(p.U-center.U)+(p.V-center.V)*(p.V-center.V))-radius) <= epsilon * 32d).ToArray();
        var angles = edgeHits.Select(p => Math.Atan2(p.V-center.V,p.U-center.U)).Order().Aggregate(new List<double>(), (a,x) => { if(a.Count==0 || Math.Abs(a[^1]-x)>1e-7d) a.Add(x); return a; });
        if (angles.Count == 2)
        {
            var sweep = angles[1]-angles[0]; if (sweep > Math.PI) { (angles[0],angles[1])=(angles[1],angles[0]); sweep=2*Math.PI-sweep; }
            var start = new Point2D(center.U+radius*Math.Cos(angles[0]),center.V+radius*Math.Sin(angles[0]));
            var end = new Point2D(center.U+radius*Math.Cos(angles[0]+sweep),center.V+radius*Math.Sin(angles[0]+sweep));
            notes.Add($"section-fragment:cone:face={face.Id.Value}:center=({center.U:R},{center.V:R}):radius={radius:R}:sweep={sweep:R}");
            return [RawSectionSegment.Arc(start,end,center,radius,"ccw",sweep) with { SourceFace=face.Id.Value, SurfaceFamily="Cone" }];
        }
        if (angles.Count > 2) { notes.Add($"UnsupportedSectionCurve:cone-trim-ambiguous:face={face.Id.Value}:angularVertices={angles.Count}"); return []; }
        var fullStart = new Point2D(center.U+radius,center.V);
        return [RawSectionSegment.Arc(fullStart,fullStart,center,radius,"ccw",2*Math.PI) with { SourceFace=face.Id.Value, SurfaceFamily="Cone" }];
    }

    private static RawSectionSegment OrientForMaterialLeft(RawSectionSegment segment, SectionFrame frame, Vector3D outwardNormal)
    {
        // For a section plane with normal N, T = N x outward has solid material
        // on its left (the left in-plane normal is -outward's in-plane component).
        var desired = frame.Normal.Cross(outwardNormal);
        var du = desired.Dot(frame.UAxis); var dv = desired.Dot(frame.VAxis);
        var tangent = segment.Kind == RawSectionSegmentKind.Arc && segment.Center is not null && segment.Radius is not null && segment.SweepRadians is not null
            ? new Point2D(-Math.Sin(Math.Atan2(segment.Start.V-segment.Center.V, segment.Start.U-segment.Center.U) + segment.SweepRadians.Value*.5d) * Math.Sign(segment.SweepRadians.Value), Math.Cos(Math.Atan2(segment.Start.V-segment.Center.V, segment.Start.U-segment.Center.U) + segment.SweepRadians.Value*.5d) * Math.Sign(segment.SweepRadians.Value))
            : new Point2D(segment.End.U-segment.Start.U, segment.End.V-segment.Start.V);
        return tangent.U * du + tangent.V * dv < 0d ? segment.Reversed() : segment;
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
            RawSectionSegmentKind.Line => new SectionSegment("line", raw.Start, raw.End, null, null, null, null, null, raw.SourceFace, raw.SourceEntity, raw.SurfaceFamily, "NormalizedLinear", 0d, 1d, raw.MaterialSideEvidence),
            RawSectionSegmentKind.Arc => new SectionSegment("arc", raw.Start, raw.End, raw.Center, raw.Radius, raw.Direction, raw.SweepRadians, null, raw.SourceFace, raw.SourceEntity, raw.SurfaceFamily, "AngularRadians", 0d, raw.SweepRadians, raw.MaterialSideEvidence),
            _ => new SectionSegment("unsupported", raw.Start, raw.End, null, null, null, null, raw.UnsupportedReason ?? "unsupported", raw.SourceFace, raw.SourceEntity, raw.SurfaceFamily, null, null, null, raw.MaterialSideEvidence)
        };

    private static SectionSegment ToSectionSegment(ArrangementFragment2D fragment) => fragment.Geometry switch
    {
        LineArcLineSegment2D line => new("line", new(line.Start.X, line.Start.Y), new(line.End.X, line.End.Y), null, null, null, null, null),
        LineArcCircularArc2D arc => new("arc", new(arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians)), new(arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians)), new(arc.Center.X, arc.Center.Y), arc.Radius, arc.SweepAngleRadians >= 0d ? "ccw" : "cw", arc.SweepAngleRadians, null),
        _ => new("unsupported", new(0, 0), new(0, 0), null, null, null, null, "UnsupportedSectionCurve")
    };

    private static SectionLoop FullCircleLoop(int id, RawSectionSegment segment)
    {
        var center = segment.Center ?? throw new InvalidOperationException("Full circle is missing a center.");
        var radius = segment.Radius ?? throw new InvalidOperationException("Full circle is missing a radius.");
        var role = segment.MaterialSideEvidence?.Contains("False", StringComparison.Ordinal) == true ? "Inner" : "Outer";
        return new(id, true, "ccw", new BoundingBox2D(new(center.U - radius, center.V - radius), new(center.U + radius, center.V + radius)), [ToSectionSegment(segment)], role);
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

    private static RayMapFrame ResolveRayMapFrame(string plane, string direction, BoundingBox3D bbox)
    {
        var dir = direction.ToLowerInvariant() switch
        {
            "+x" => new Vector3D(1d, 0d, 0d),
            "-x" => new Vector3D(-1d, 0d, 0d),
            "+y" => new Vector3D(0d, 1d, 0d),
            "-y" => new Vector3D(0d, -1d, 0d),
            "+z" => new Vector3D(0d, 0d, 1d),
            "-z" => new Vector3D(0d, 0d, -1d),
            _ => throw new InvalidOperationException("Analyze map direction must be one of +x, -x, +y, -y, +z, -z.")
        };

        return plane.ToLowerInvariant() switch
        {
            "xy" => new RayMapFrame(new Point3D(0d, 0d, dir.Z < 0 ? bbox.Max.Z : bbox.Min.Z), new Vector3D(1d, 0d, 0d), new Vector3D(0d, 1d, 0d), dir, bbox.Min.X, bbox.Max.X, bbox.Min.Y, bbox.Max.Y, p => p.Z),
            "xz" => new RayMapFrame(new Point3D(0d, dir.Y < 0 ? bbox.Max.Y : bbox.Min.Y, 0d), new Vector3D(1d, 0d, 0d), new Vector3D(0d, 0d, 1d), dir, bbox.Min.X, bbox.Max.X, bbox.Min.Z, bbox.Max.Z, p => p.Y),
            "yz" => new RayMapFrame(new Point3D(dir.X < 0 ? bbox.Max.X : bbox.Min.X, 0d, 0d), new Vector3D(0d, 1d, 0d), new Vector3D(0d, 0d, 1d), dir, bbox.Min.Y, bbox.Max.Y, bbox.Min.Z, bbox.Max.Z, p => p.X),
            _ => throw new InvalidOperationException("Analyze map plane must be xy, xz, or yz.")
        };
    }

    private static void AddDiagnostic(ICollection<string> diagnostics, ISet<string> diagnosticSet, string diagnostic)
    {
        if (diagnosticSet.Add(diagnostic))
        {
            diagnostics.Add(diagnostic);
        }
    }

    private static IReadOnlyList<RayMapHit> IntersectAnalyticRay(BrepBody body, Point3D origin, Vector3D direction, IReadOnlyDictionary<int, string> faceSurfaceKinds)
    {
        var hits = new List<RayMapHit>();
        foreach (var face in body.Topology.Faces)
        {
            if (!body.TryGetFaceSurface(face.Id, out var surface) || surface is null)
            {
                continue;
            }

            if (surface.Plane is not PlaneSurface plane)
            {
                if (surface.Cylinder is CylinderSurface cylinder)
                {
                    hits.AddRange(IntersectAnalyticCylinderFaceRay(body, face, cylinder, origin, direction, faceSurfaceKinds));
                    continue;
                }

                if (surface.Sphere is SphereSurface sphere)
                {
                    hits.AddRange(IntersectAnalyticSphereFaceRay(body, face, sphere, origin, direction, faceSurfaceKinds));
                    continue;
                }

                if (surface.Cone is ConeSurface cone)
                {
                    hits.AddRange(IntersectAnalyticConeFaceRay(body, face, cone, origin, direction, faceSurfaceKinds));
                    continue;
                }

                if (surface.Torus is TorusSurface torus)
                {
                    hits.AddRange(IntersectAnalyticTorusFaceRay(body, face, torus, origin, direction, faceSurfaceKinds));
                    continue;
                }

                continue;
            }

            var normal = plane.Normal.ToVector();
            var denom = normal.Dot(direction);
            if (Math.Abs(denom) < 1e-10d)
            {
                continue;
            }

            var t = (plane.Origin - origin).Dot(normal) / denom;
            if (t < -1e-9d)
            {
                continue;
            }

            var position = origin + direction * t;
            if (!IsPointInPlanarFaceBounds(body, face.Id, plane, position))
            {
                continue;
            }

            var family = faceSurfaceKinds.TryGetValue(face.Id.Value, out var sf) ? sf : "plane";
            hits.Add(new RayMapHit(t, position, face.Id.Value, family, normal, "analytic", "exact", []));
        }

        return hits.OrderBy(h => h.T).ToArray();
    }


    private static IReadOnlyList<RayMapHit> IntersectAnalyticCylinderFaceRay(BrepBody body, Face face, CylinderSurface cylinder, Point3D origin, Vector3D direction, IReadOnlyDictionary<int, string> faceSurfaceKinds)
    {
        var hits = new List<RayMapHit>();
        var axis = cylinder.Axis.ToVector();
        if (!TryNormalize(axis, out axis) || cylinder.Radius <= 0d)
        {
            return hits;
        }

        if (!TryGetFaceAxisSpan(body, face.Id, cylinder.Origin, axis, out var minAxis, out var maxAxis))
        {
            return hits;
        }

        var oc = origin - cylinder.Origin;
        var dParallel = axis * direction.Dot(axis);
        var oParallel = axis * oc.Dot(axis);
        var dPerp = direction - dParallel;
        var oPerp = oc - oParallel;
        var a = dPerp.Dot(dPerp);
        var b = 2d * oPerp.Dot(dPerp);
        var c = oPerp.Dot(oPerp) - (cylinder.Radius * cylinder.Radius);
        if (Math.Abs(a) < 1e-14d)
        {
            return hits;
        }

        foreach (var t in SolveQuadraticNonnegative(a, b, c))
        {
            var p = origin + direction * t;
            var axial = (p - cylinder.Origin).Dot(axis);
            if (axial < minAxis - 1e-7d || axial > maxAxis + 1e-7d)
            {
                continue;
            }

            var radial = (p - cylinder.Origin) - (axis * axial);
            if (!TryNormalize(radial, out var normal))
            {
                continue;
            }

            var family = faceSurfaceKinds.TryGetValue(face.Id.Value, out var sf) ? sf : "cylinder";
            hits.Add(new RayMapHit(t, p, face.Id.Value, family, normal, "analytic", "exact", []));
        }

        return hits;
    }

    private static IReadOnlyList<RayMapHit> IntersectAnalyticSphereFaceRay(BrepBody body, Face face, SphereSurface sphere, Point3D origin, Vector3D direction, IReadOnlyDictionary<int, string> faceSurfaceKinds)
    {
        var hits = new List<RayMapHit>();
        if (sphere.Radius <= 0d || body.GetEdges(face.Id).Any())
        {
            return hits;
        }

        var oc = origin - sphere.Center;
        var a = direction.Dot(direction);
        var b = 2d * oc.Dot(direction);
        var c = oc.Dot(oc) - (sphere.Radius * sphere.Radius);
        foreach (var t in SolveQuadraticNonnegative(a, b, c))
        {
            var p = origin + direction * t;
            var radial = p - sphere.Center;
            if (!TryNormalize(radial, out var normal))
            {
                continue;
            }

            var family = faceSurfaceKinds.TryGetValue(face.Id.Value, out var sf) ? sf : "sphere";
            hits.Add(new RayMapHit(t, p, face.Id.Value, family, normal, "analytic", "exact", []));
        }

        return hits;
    }


    private static IReadOnlyList<RayMapHit> IntersectAnalyticConeFaceRay(BrepBody body, Face face, ConeSurface cone, Point3D origin, Vector3D direction, IReadOnlyDictionary<int, string> faceSurfaceKinds)
    {
        var hits = new List<RayMapHit>();
        var axis = cone.Axis.ToVector();
        if (!TryNormalize(axis, out axis)) return hits;
        if (!TryGetFaceAxisSpan(body, face.Id, cone.Apex, axis, out var minAxis, out var maxAxis)) return hits;

        var tan = Math.Tan(cone.SemiAngleRadians);
        var tan2 = tan * tan;
        var delta = origin - cone.Apex;
        var dAxial = direction.Dot(axis);
        var oAxial = delta.Dot(axis);
        var dPerp = direction - axis * dAxial;
        var oPerp = delta - axis * oAxial;
        var a = dPerp.Dot(dPerp) - tan2 * dAxial * dAxial;
        var b = 2d * (oPerp.Dot(dPerp) - tan2 * oAxial * dAxial);
        var c = oPerp.Dot(oPerp) - tan2 * oAxial * oAxial;
        if (Math.Abs(a) < 1e-14d) return hits;

        foreach (var t in SolveQuadraticNonnegative(a, b, c))
        {
            var p = origin + direction * t;
            var axial = (p - cone.Apex).Dot(axis);
            if (axial < Math.Max(0d, minAxis) - 1e-7d || axial > maxAxis + 1e-7d) continue;
            var radial = (p - cone.Apex) - axis * axial;
            if (!TryNormalize(radial, out var radialNormal)) continue;
            if (!TryNormalize(radialNormal - axis * tan, out var normal)) continue;
            var family = faceSurfaceKinds.TryGetValue(face.Id.Value, out var sf) ? sf : "cone";
            hits.Add(new RayMapHit(t, p, face.Id.Value, family, normal, "analytic", "exact", []));
        }

        return DeduplicateHits(hits);
    }

    private static IReadOnlyList<RayMapHit> IntersectAnalyticTorusFaceRay(BrepBody body, Face face, TorusSurface torus, Point3D origin, Vector3D direction, IReadOnlyDictionary<int, string> faceSurfaceKinds)
    {
        var hits = new List<RayMapHit>();
        if (torus.MajorRadius <= 0d || torus.MinorRadius <= 0d) return hits;
        if (body.Topology.Faces.Count() != 1)
        {
            return hits;
        }

        if (!TryIntersectBoundingSphere(origin, direction, torus.Center, torus.MajorRadius + torus.MinorRadius, out var near, out var far)) return hits;
        near = Math.Max(0d, near);
        if (far < near) return hits;

        const int samples = 1024;
        var dt = (far - near) / samples;
        var roots = new List<double>();
        var prevT = near;
        var prevF = TorusImplicit(origin + direction * prevT, torus);
        if (Math.Abs(prevF) <= 1e-9d) roots.Add(prevT);
        for (var i = 1; i <= samples; i++)
        {
            var t = i == samples ? far : near + i * dt;
            var f = TorusImplicit(origin + direction * t, torus);
            if (Math.Abs(f) <= 1e-9d) roots.Add(t);
            if ((prevF < 0d && f > 0d) || (prevF > 0d && f < 0d)) roots.Add(BisectTorusRoot(prevT, t, origin, direction, torus));
            prevT = t; prevF = f;
        }

        var family = faceSurfaceKinds.TryGetValue(face.Id.Value, out var sf) ? sf : "torus";
        foreach (var t in roots.Where(t => t >= -1e-9d).Select(t => Math.Max(0d, t)).OrderBy(t => t))
        {
            if (hits.Any(h => Math.Abs(h.T - t) <= 1e-7d)) continue;
            var p = origin + direction * t;
            if (!TryTorusNormal(p, torus, out var normal)) continue;
            hits.Add(new RayMapHit(t, p, face.Id.Value, family, normal, "analytic", "exact", []));
        }
        return hits;
    }

    private static bool TryIntersectBoundingSphere(Point3D origin, Vector3D direction, Point3D center, double radius, out double near, out double far)
    {
        near = far = 0d;
        var oc = origin - center;
        var b = 2d * oc.Dot(direction);
        var c = oc.Dot(oc) - radius * radius;
        var disc = b * b - 4d * direction.Dot(direction) * c;
        if (disc < -1e-9d) return false;
        var sqrt = Math.Sqrt(Math.Max(0d, disc));
        var a2 = 2d * direction.Dot(direction);
        near = (-b - sqrt) / a2;
        far = (-b + sqrt) / a2;
        return far >= -1e-9d;
    }

    private static double TorusImplicit(Point3D p, TorusSurface torus)
    {
        var d = p - torus.Center;
        var x = d.Dot(torus.XAxis.ToVector());
        var y = d.Dot(torus.YAxis.ToVector());
        var z = d.Dot(torus.Axis.ToVector());
        var sum = x*x + y*y + z*z + torus.MajorRadius*torus.MajorRadius - torus.MinorRadius*torus.MinorRadius;
        return sum*sum - 4d*torus.MajorRadius*torus.MajorRadius*(x*x + y*y);
    }

    private static double BisectTorusRoot(double lo, double hi, Point3D origin, Vector3D direction, TorusSurface torus)
    {
        var flo = TorusImplicit(origin + direction * lo, torus);
        for (var i = 0; i < 80; i++)
        {
            var mid = (lo + hi) * 0.5d;
            var fmid = TorusImplicit(origin + direction * mid, torus);
            if (Math.Abs(fmid) <= 1e-12d || hi - lo <= 1e-9d) return mid;
            if ((flo < 0d && fmid > 0d) || (flo > 0d && fmid < 0d)) hi = mid;
            else { lo = mid; flo = fmid; }
        }
        return (lo + hi) * 0.5d;
    }

    private static bool TryTorusNormal(Point3D p, TorusSurface torus, out Vector3D normal)
    {
        var d = p - torus.Center;
        var xAxis = torus.XAxis.ToVector();
        var yAxis = torus.YAxis.ToVector();
        var zAxis = torus.Axis.ToVector();
        var x = d.Dot(xAxis); var y = d.Dot(yAxis); var z = d.Dot(zAxis);
        var common = x*x + y*y + z*z + torus.MajorRadius*torus.MajorRadius - torus.MinorRadius*torus.MinorRadius;
        var n = xAxis * (4d*x*(common - 2d*torus.MajorRadius*torus.MajorRadius))
              + yAxis * (4d*y*(common - 2d*torus.MajorRadius*torus.MajorRadius))
              + zAxis * (4d*z*common);
        return TryNormalize(n, out normal);
    }

    private static IReadOnlyList<RayMapHit> DeduplicateHits(List<RayMapHit> hits)
    {
        var ordered = hits.OrderBy(h => h.T).ToList();
        for (var i = ordered.Count - 1; i > 0; i--)
            if (Math.Abs(ordered[i].T - ordered[i - 1].T) <= 1e-7d) ordered.RemoveAt(i);
        return ordered;
    }

    private static IReadOnlyList<double> SolveQuadraticNonnegative(double a, double b, double c)
    {
        var discriminant = (b * b) - (4d * a * c);
        if (discriminant < -1e-10d)
        {
            return [];
        }

        if (Math.Abs(discriminant) <= 1e-10d)
        {
            var t = -b / (2d * a);
            return t >= -1e-9d ? [Math.Max(0d, t)] : [];
        }

        var sqrt = Math.Sqrt(discriminant);
        var t0 = (-b - sqrt) / (2d * a);
        var t1 = (-b + sqrt) / (2d * a);
        var roots = new List<double>(2);
        if (t0 >= -1e-9d) roots.Add(Math.Max(0d, t0));
        if (t1 >= -1e-9d && Math.Abs(t1 - t0) > 1e-8d) roots.Add(Math.Max(0d, t1));
        roots.Sort();
        return roots;
    }

    private static bool TryGetFaceAxisSpan(BrepBody body, FaceId faceId, Point3D axisOrigin, Vector3D axis, out double minAxis, out double maxAxis)
    {
        var projections = body.GetEdges(faceId)
            .SelectMany(edge => body.GetVertices(edge))
            .Distinct()
            .Select(vertex => body.TryGetVertexPoint(vertex, out var p) ? (double?)(p - axisOrigin).Dot(axis) : null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToArray();
        if (projections.Length < 2)
        {
            minAxis = maxAxis = 0d;
            return false;
        }

        minAxis = projections.Min();
        maxAxis = projections.Max();
        return maxAxis - minAxis > 1e-8d;
    }

    private static bool IsPointInPlanarFaceBounds(BrepBody body, FaceId faceId, PlaneSurface plane, Point3D point)
    {
        var vertices = body.GetEdges(faceId)
            .SelectMany(edge => body.GetVertices(edge))
            .Distinct()
            .Select(vertex => body.TryGetVertexPoint(vertex, out var p) ? (Point3D?)p : null)
            .Where(p => p.HasValue)
            .Select(p => p!.Value)
            .ToArray();
        if (vertices.Length < 3)
        {
            return false;
        }

        var uAxis = plane.UAxis.ToVector();
        var vAxis = plane.VAxis.ToVector();
        var projected = vertices
            .Select(v => ((v - plane.Origin).Dot(uAxis), (v - plane.Origin).Dot(vAxis)))
            .Distinct()
            .ToArray();
        if (projected.Length < 3)
        {
            return false;
        }

        var centerU = projected.Average(p => p.Item1);
        var centerV = projected.Average(p => p.Item2);
        var polygon = projected
            .OrderBy(p => Math.Atan2(p.Item2 - centerV, p.Item1 - centerU))
            .ToArray();
        var pu = (point - plane.Origin).Dot(uAxis);
        var pv = (point - plane.Origin).Dot(vAxis);
        var inside = false;
        const double epsilon = 1e-8d;
        for (var i = 0; i < polygon.Length; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Length];
            var cross = (b.Item1 - a.Item1) * (pv - a.Item2) - (b.Item2 - a.Item2) * (pu - a.Item1);
            var dot = (pu - a.Item1) * (pu - b.Item1) + (pv - a.Item2) * (pv - b.Item2);
            if (Math.Abs(cross) <= epsilon && dot <= epsilon)
            {
                return true;
            }

            if (((a.Item2 > pv) != (b.Item2 > pv)) &&
                pu < (b.Item1 - a.Item1) * (pv - a.Item2) / (b.Item2 - a.Item2) + a.Item1)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static IReadOnlyList<RayMapHit> IntersectTessellatedRay(DisplayTessellationResult tessellation, Point3D origin, Vector3D direction, IReadOnlyDictionary<int, string> faceSurfaceKinds)
    {
        var hits = new List<RayMapHit>();
        foreach (var patch in tessellation.FacePatches)
        {
            for (var index = 0; index + 2 < patch.TriangleIndices.Count; index += 3)
            {
                var a = patch.Positions[patch.TriangleIndices[index]];
                var b = patch.Positions[patch.TriangleIndices[index + 1]];
                var c = patch.Positions[patch.TriangleIndices[index + 2]];
                if (!TryIntersectTriangle(origin, direction, a, b, c, out var t, out var normal)) continue;
                var p = origin + direction * t;
                var family = faceSurfaceKinds.TryGetValue(patch.FaceId.Value, out var sf) ? sf : "unknown";
                if (hits.Any(h => Math.Abs(h.T - t) < 1e-7d && h.FaceIndex == patch.FaceId.Value)) continue;
                hits.Add(new RayMapHit(t, p, patch.FaceId.Value, family, normal, "tessellated-fallback", "approximate", [$"Exact ray intersection unavailable for {family}; used tessellated fallback."]));
            }
        }

        return hits.OrderBy(h => h.T).ToArray();
    }

    private static bool TryIntersectTriangle(Point3D origin, Vector3D direction, Point3D a, Point3D b, Point3D c, out double t, out Vector3D normal)
    {
        t = 0d;
        normal = new Vector3D(0d, 0d, 0d);
        var e1 = b - a;
        var e2 = c - a;
        var p = direction.Cross(e2);
        var det = e1.Dot(p);
        if (Math.Abs(det) < 1e-10d) return false;
        var invDet = 1d / det;
        var tv = origin - a;
        var u = tv.Dot(p) * invDet;
        if (u < -1e-9d || u > 1d + 1e-9d) return false;
        var q = tv.Cross(e1);
        var v = direction.Dot(q) * invDet;
        if (v < -1e-9d || u + v > 1d + 1e-9d) return false;
        t = e2.Dot(q) * invDet;
        if (t < -1e-9d) return false;
        normal = e1.Cross(e2);
        return TryNormalize(normal, out normal);
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

    private readonly record struct RayMapFrame(
        Point3D PlaneOrigin,
        Vector3D UAxis,
        Vector3D VAxis,
        Vector3D RayDirection,
        double MinU,
        double MaxU,
        double MinV,
        double MaxV,
        Func<Point3D, double> Height)
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
        string? UnsupportedReason,
        int SourceFace = -1,
        string? SurfaceFamily = null,
        string? SourceEntity = null,
        string? MaterialSideEvidence = null)
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
