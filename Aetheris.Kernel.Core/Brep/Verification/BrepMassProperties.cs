using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Verification;

/// <summary>
/// The confidence assigned to a mass-property result.  The verifier deliberately
/// never promotes a tessellated result to an exact result.
/// </summary>
public enum BrepMassPropertiesStatus
{
    Unavailable,
    NumericalWithBound,
    NumericalConverged,
}

public sealed record BrepMassPropertiesOptions(
    double LinearTolerance,
    double AngularToleranceRadians,
    double ConvergenceTolerance,
    int MinimumSegments = 12,
    int MaximumSegments = 16)
{
    // M8 is a bounded independent estimate, not the primary mass analyzer. Keep
    // its mesh deliberately coarse enough for interactive verification and make
    // the resulting conservative bound explicit to callers.
    public static BrepMassPropertiesOptions Default { get; } = new(0.1d, double.Pi / 12d, 1d, MaximumSegments: 16);
}

public sealed record BrepMassPropertiesFaceContribution(
    FaceId FaceId,
    SurfaceGeometryKind? SurfaceKind,
    double SignedVolume,
    double SurfaceArea,
    int TriangleCount,
    bool FaceSenseAvailable,
    bool FaceSameSense,
    bool TriangleOrientationCoherent);

public sealed record BrepMassPropertiesTopologyDiagnostics(
    bool IsSingleBody,
    bool IsEnclosed,
    bool IsOrientationConsistent,
    int ConnectedShellCount,
    IReadOnlyList<string> Messages);

public sealed record BrepMassPropertiesResult(
    BrepMassPropertiesStatus Status,
    double SignedVolume,
    double AbsoluteVolume,
    double SurfaceArea,
    Point3D? Centroid,
    bool IsEnclosed,
    bool IsOrientationConsistent,
    IReadOnlyList<BrepMassPropertiesFaceContribution> FaceContributions,
    string EvaluationMethod,
    double? ErrorBound,
    BrepMassPropertiesOptions Tolerance,
    BrepMassPropertiesTopologyDiagnostics Topology,
    IReadOnlyList<string> Diagnostics)
{
    /// <summary>
    /// True only for bounded analytic recognizers. The generic trimmed-face
    /// route consumes display tessellation and is a diagnostic estimate, never
    /// an authoritative occupied-volume measurement.
    /// </summary>
    public bool IsAuthoritativeForVolumeAssertion => EvaluationMethod.StartsWith("Exact", StringComparison.Ordinal);

    public bool IsTessellatedSanityEstimate => !IsAuthoritativeForVolumeAssertion;
}

/// <summary>
/// B-rep mass-property diagnostic. Bounded analytic recognizers provide exact
/// cross-checks for a few admitted families. The generic route deterministically
/// tessellates faces at two resolutions and integrates oriented triangles as
/// tetrahedra; that result is a display-derived sanity estimate and must not be
/// used as authoritative occupied-volume evidence.
/// </summary>
public static class BrepMassProperties
{
    public static BrepMassPropertiesResult Evaluate(BrepBody body, BrepMassPropertiesOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(body);
        var effective = options ?? BrepMassPropertiesOptions.Default;
        var topology = ValidateTopology(body);
        if (!topology.IsEnclosed)
        {
            return Unavailable(effective, topology, "Mass properties require a closed, orientable material boundary.");
        }

        // A lattice node is an exact spherical surface with several disjoint
        // circular openings.  Its trim arrangement is deliberately richer than
        // the display tessellator's one-loop sphere path, but its material
        // measure is available directly from the emitted B-rep supports and
        // seam circles.  This route consumes topology/bindings only; it never
        // consults the lattice construction plan.
        if (TryEvaluateExactSphereCylinderSeamBody(body, effective, topology, out var exact))
        {
            return exact;
        }

        // The exact-Z-prism lane is authoritative for section volume. Its first
        // implementation did not populate surface area or centroid, which made
        // a WorldXY extrusion observably less complete than a rotated equivalent
        // frame. Complete those public fields from the independent boundary
        // evaluator while retaining the exact section-volume evidence.
        if (TryEvaluateExactVerticalLineArcPrism(body, effective, topology, out exact))
        {
            return CompleteExactPrismResult(body, effective, exact);
        }

        // A finite straight Profile fillet on an otherwise axis-aligned box is
        // a second exact bounded family.  The materializer emits the retained
        // box corners, one exact quarter-cylinder, and two circular end trims.
        // Generic display triangulation is deliberately too coarse to verify a
        // tight analytic Assert Volume here, so recognize this topology from
        // the emitted B-rep (never from Firmament construction metadata).
        if (TryEvaluateExactAxisAlignedBoxQuarterCylinderFillet(body, effective, topology, out exact))
        {
            return exact;
        }

        var coarse = EvaluateAtResolution(body, effective, refinement: 1);
        if (coarse.Error is not null)
        {
            return Unavailable(effective, topology, coarse.Error);
        }

        var refined = EvaluateAtResolution(body, effective, refinement: 2);
        if (refined.Error is not null)
        {
            return Unavailable(effective, topology, refined.Error);
        }

        var signed = refined.SignedVolume;
        var orientationConsistent = topology.IsOrientationConsistent && System.Math.Abs(signed) > effective.LinearTolerance * effective.LinearTolerance * effective.LinearTolerance;
        var sign = signed < 0d ? -1d : 1d;
        Point3D? centroid = System.Math.Abs(signed) > 1e-18d
            ? new Point3D(refined.CentroidNumerator.X / signed, refined.CentroidNumerator.Y / signed, refined.CentroidNumerator.Z / signed)
            : null;
        var coarseCentroid = CentroidOf(coarse);
        var refinedCentroid = CentroidOf(refined);
        var centroidDelta = coarseCentroid is null || refinedCentroid is null
            ? double.PositiveInfinity
            : (refinedCentroid.Value - coarseCentroid.Value).Length;
        var refinementDelta = System.Math.Max(System.Math.Abs(refined.SignedVolume - coarse.SignedVolume), System.Math.Abs(refined.SurfaceArea - coarse.SurfaceArea));
        // A refinement delta alone misses systematic chord error when a surface
        // reaches the deterministic segment cap. A normal displacement no
        // greater than the requested chord tolerance changes enclosed volume
        // by at most area × displacement; the factor four conservatively covers
        // the two sampled resolutions and bounded trim seams.
        var error = System.Math.Max(refinementDelta, refined.SurfaceArea * effective.LinearTolerance * 4d);
        var actualRefinement = refined.Contributions.Zip(coarse.Contributions, (fine, rough) => fine.TriangleCount > rough.TriangleCount).Any(increased => increased);
        var converged = actualRefinement && refinementDelta <= effective.ConvergenceTolerance && centroidDelta <= effective.ConvergenceTolerance;
        var diagnostics = new List<string>(topology.Messages)
        {
            "Verification triangulation is not authoritative geometry.",
            "Volume is the oriented sum of tetrahedra (0,a,b,c); centroid is its signed first moment divided by signed volume.",
            $"coarseVolume={coarse.SignedVolume:G17}; refinedVolume={refined.SignedVolume:G17}; refinementDelta={refinementDelta:G17}; centroidDelta={centroidDelta:G17}; actualRefinement={actualRefinement}; conservativeErrorBound={error:G17}."
        };
        if (!orientationConsistent) diagnostics.Add("Face senses or signed enclosure orientation are inconsistent; absolute volume remains a magnitude only.");

        return new BrepMassPropertiesResult(
            converged ? BrepMassPropertiesStatus.NumericalConverged : BrepMassPropertiesStatus.NumericalWithBound,
            signed,
            System.Math.Abs(signed),
            refined.SurfaceArea,
            centroid,
            topology.IsEnclosed,
            orientationConsistent,
            refined.Contributions,
            "DeterministicTrimmedFaceTriangulationBoundaryIntegral",
            error,
            effective,
            topology with { IsOrientationConsistent = orientationConsistent },
            diagnostics);
    }

    private static BrepMassPropertiesResult Unavailable(BrepMassPropertiesOptions options, BrepMassPropertiesTopologyDiagnostics topology, string reason)
        => new(BrepMassPropertiesStatus.Unavailable, 0d, 0d, 0d, null, topology.IsEnclosed, topology.IsOrientationConsistent, [], "DeterministicTrimmedFaceTriangulationBoundaryIntegral", null, options, topology, topology.Messages.Append(reason).ToArray());

    private static BrepMassPropertiesResult CompleteExactPrismResult(BrepBody body, BrepMassPropertiesOptions options, BrepMassPropertiesResult exact)
    {
        var boundary = EvaluateAtResolution(body, options, refinement: 2);
        if (boundary.Error is not null || System.Math.Abs(boundary.SignedVolume) <= 1e-18d)
            return exact;

        var centroid = new Point3D(
            boundary.CentroidNumerator.X / boundary.SignedVolume,
            boundary.CentroidNumerator.Y / boundary.SignedVolume,
            boundary.CentroidNumerator.Z / boundary.SignedVolume);
        var error = System.Math.Max(exact.ErrorBound ?? 0d, boundary.SurfaceArea * options.LinearTolerance * 4d);
        return exact with
        {
            SurfaceArea = boundary.SurfaceArea,
            Centroid = centroid,
            FaceContributions = boundary.Contributions,
            EvaluationMethod = "ExactVerticalLineArcPrismSectionIntegration+DeterministicTrimmedFaceTriangulationBoundaryIntegral",
            ErrorBound = error,
            Diagnostics = exact.Diagnostics.Append("Exact section volume was completed with deterministic boundary area and centroid evidence.").ToArray()
        };
    }

    /// <summary>
    /// Exact volume recognizer for a materialized line/arc prismatic shell.
    /// It intentionally has no knowledge of Firmament section stacks: its only
    /// inputs are topology, vertex positions, and exact plane/cylinder curves.
    /// </summary>
    private static bool TryEvaluateExactVerticalLineArcPrism(
        BrepBody body,
        BrepMassPropertiesOptions options,
        BrepMassPropertiesTopologyDiagnostics topology,
        out BrepMassPropertiesResult result)
    {
        result = default!;
        const double tolerance = 1e-8d;
        var vertices = body.Topology.Vertices
            .Select(vertex => body.TryGetVertexPoint(vertex.Id, out var point) ? (vertex.Id, Point: (Point3D?)point) : (vertex.Id, Point: (Point3D?)null))
            .ToDictionary(x => x.Id, x => x.Point);
        if (vertices.Count == 0 || vertices.Values.Any(point => point is null)) return false;

        var levels = vertices.Values.Select(point => point!.Value.Z).DistinctBy(z => System.Math.Round(z / tolerance)).OrderBy(z => z).ToArray();
        if (levels.Length < 2) return false;

        var faces = body.Topology.Faces.ToArray();
        if (faces.Length == 0) return false;
        foreach (var face in faces)
        {
            if (!body.TryGetFaceSurfaceGeometry(face.Id, out var surface) || surface is null
                || surface.Kind is not (SurfaceGeometryKind.Plane or SurfaceGeometryKind.Cylinder))
                return false;
            if (surface.Kind == SurfaceGeometryKind.Cylinder && System.Math.Abs(surface.Cylinder!.Value.Axis.ToVector().Z) < 1d - tolerance)
                return false;
            if (surface.Kind == SurfaceGeometryKind.Plane
                && System.Math.Abs(surface.Plane!.Value.Normal.ToVector().Z) > tolerance
                && System.Math.Abs(surface.Plane!.Value.Normal.ToVector().Z) < 1d - tolerance)
                return false;
        }

        var total = 0d;
        var intervalEvidence = new List<string>();
        var contributions = new List<BrepMassPropertiesFaceContribution>();
        for (var index = 0; index < levels.Length - 1; index++)
        {
            var from = levels[index];
            var to = levels[index + 1];
            if (to - from <= tolerance) continue;
            var boundaryEdges = new HashSet<EdgeId>();
            foreach (var face in faces)
            {
                if (!IsVerticalSideFace(body, face, vertices, from, to, tolerance)) continue;
                foreach (var loopId in face.LoopIds)
                foreach (var coedgeId in body.GetCoedgeIds(loopId))
                {
                    var edgeId = body.GetCoedgeEdgeId(coedgeId);
                    var edge = body.Topology.GetEdge(edgeId);
                    var a = vertices[edge.StartVertexId]!.Value;
                    var b = vertices[edge.EndVertexId]!.Value;
                    if (System.Math.Abs(a.Z - from) <= tolerance && System.Math.Abs(b.Z - from) <= tolerance)
                        boundaryEdges.Add(edgeId);
                }
            }

            if (boundaryEdges.Count == 0) return false;
            if (!TryEvaluateSectionArea(boundaryEdges, body, vertices, out var area)) return false;
            if (area <= tolerance) return false;
            total += area * (to - from);
            intervalEvidence.Add($"z=[{from:R},{to:R}];area={area:R}");
        }

        if (total <= tolerance) return false;
        foreach (var face in faces)
        {
            var kind = body.TryGetFaceSurfaceGeometry(face.Id, out var surface) ? surface?.Kind : null;
            contributions.Add(new(face.Id, kind, 0d, 0d, 0, body.Bindings.TryGetFaceBinding(face.Id, out var binding), binding.SameSense, true));
        }
        var analyticRoundoffBound = System.Math.Max(1e-8d, System.Math.Abs(total) * 1e-14d);
        result = new BrepMassPropertiesResult(
            BrepMassPropertiesStatus.NumericalConverged,
            total,
            total,
            0d,
            null,
            topology.IsEnclosed,
            topology.IsOrientationConsistent,
            contributions,
            "ExactVerticalLineArcPrismSectionIntegration",
            analyticRoundoffBound,
            options,
            topology,
            ["Exact materialized plane/cylinder section integration; no construction metadata or tessellation was used.", ..intervalEvidence]);
        return true;
    }

    /// <summary>
    /// Reconstructs the section's closed line loops from side-face boundary edges
    /// and classifies containment geometrically.  Side-face loop winding is a 3D
    /// shell convention and is not the 2D material-loop winding; treating its
    /// raw edge binding direction as area orientation was the source of the
    /// nested Remove regression.
    /// </summary>
    private static bool TryEvaluateSectionArea(
        IReadOnlyCollection<EdgeId> edgeIds,
        BrepBody body,
        IReadOnlyDictionary<VertexId, Point3D?> vertices,
        out double area)
    {
        area = 0d;
        var adjacency = new Dictionary<VertexId, List<(EdgeId Edge, VertexId Other)>>();
        foreach (var edgeId in edgeIds)
        {
            if (!body.TryGetEdgeCurveGeometry(edgeId, out var curve) || curve?.Kind is not (CurveGeometryKind.Line3 or CurveGeometryKind.Circle3)) return false;
            var edge = body.Topology.GetEdge(edgeId);
            if (!adjacency.TryAdd(edge.StartVertexId, [(edgeId, edge.EndVertexId)])) adjacency[edge.StartVertexId].Add((edgeId, edge.EndVertexId));
            if (!adjacency.TryAdd(edge.EndVertexId, [(edgeId, edge.StartVertexId)])) adjacency[edge.EndVertexId].Add((edgeId, edge.StartVertexId));
        }
        if (adjacency.Count == 0 || adjacency.Values.Any(uses => uses.Count != 2)) return false;

        var remaining = new HashSet<EdgeId>(edgeIds);
        var loops = new List<(List<Point3D> Points, List<(EdgeId Edge, VertexId From)> Uses)>();
        while (remaining.Count > 0)
        {
            var firstEdge = remaining.OrderBy(id => id.Value).First();
            var first = body.Topology.GetEdge(firstEdge);
            var start = first.StartVertexId;
            var current = start;
            var loop = new List<Point3D>();
            var uses = new List<(EdgeId Edge, VertexId From)>();
            while (true)
            {
                if (!vertices.TryGetValue(current, out var point) || point is null) return false;
                loop.Add(point.Value);
                var next = adjacency[current].Where(candidate => remaining.Contains(candidate.Edge)).OrderBy(candidate => candidate.Edge.Value).FirstOrDefault();
                if (next == default) return false;
                remaining.Remove(next.Edge);
                uses.Add((next.Edge, current));
                current = next.Other;
                if (current == start) break;
                if (loop.Count > edgeIds.Count + 1) return false;
            }
            if (loop.Count < 3) return false;
            loops.Add((loop, uses));
        }

        for (var i = 0; i < loops.Count; i++)
        {
            var candidate = loops[i];
            var signedArea = 0d;
            foreach (var use in candidate.Uses)
            {
                if (!TrySignedPlanarCurveAreaTwice(body, use.Edge, use.From, out var contribution)) return false;
                signedArea += contribution / 2d;
            }
            var probe = new Point3D(candidate.Points.Average(p => p.X), candidate.Points.Average(p => p.Y), candidate.Points[0].Z);
            var depth = loops.Where((_, index) => index != i).Count(loop => PointInPolygon(probe, loop.Points));
            area += (depth & 1) == 0 ? System.Math.Abs(signedArea) : -System.Math.Abs(signedArea);
        }
        return area > 0d;
    }

    private static bool PointInPolygon(Point3D point, IReadOnlyList<Point3D> polygon)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i]; var b = polygon[j];
            if ((a.Y > point.Y) == (b.Y > point.Y)) continue;
            var x = ((b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y)) + a.X;
            if (point.X < x) inside = !inside;
        }
        return inside;
    }

    private static bool IsVerticalSideFace(
        BrepBody body,
        Face face,
        IReadOnlyDictionary<VertexId, Point3D?> vertices,
        double from,
        double to,
        double tolerance)
    {
        if (!body.TryGetFaceSurfaceGeometry(face.Id, out var surface) || surface is null) return false;
        if (surface.Kind == SurfaceGeometryKind.Plane && System.Math.Abs(surface.Plane!.Value.Normal.ToVector().Z) > tolerance) return false;
        var hasFullVertical = false;
        foreach (var loopId in face.LoopIds)
        foreach (var coedgeId in body.GetCoedgeIds(loopId))
        {
            var edge = body.Topology.GetEdge(body.GetCoedgeEdgeId(coedgeId));
            var a = vertices[edge.StartVertexId]!.Value;
            var b = vertices[edge.EndVertexId]!.Value;
            if (System.Math.Abs(a.X - b.X) <= tolerance && System.Math.Abs(a.Y - b.Y) <= tolerance
                && ((System.Math.Abs(a.Z - from) <= tolerance && System.Math.Abs(b.Z - to) <= tolerance)
                    || (System.Math.Abs(a.Z - to) <= tolerance && System.Math.Abs(b.Z - from) <= tolerance)))
            {
                hasFullVertical = true;
                break;
            }
        }
        return hasFullVertical;
    }

    private static bool TrySignedPlanarCurveAreaTwice(BrepBody body, EdgeId edgeId, VertexId fromVertex, out double areaTwice)
    {
        areaTwice = 0d;
        if (!body.Bindings.TryGetEdgeBinding(edgeId, out var binding)
            || binding.TrimInterval is not { } trim
            || !body.Geometry.TryGetCurve(binding.CurveGeometryId, out var curve)
            || curve is null)
            return false;
        var edge = body.Topology.GetEdge(edgeId);
        var isTopologyForward = edge.StartVertexId == fromVertex;
        if (!isTopologyForward && edge.EndVertexId != fromVertex) return false;
        var followsTrim = isTopologyForward == binding.OrientedEdgeSense;
        var (start, end) = followsTrim ? (trim.Start, trim.End) : (trim.End, trim.Start);
        switch (curve.Kind)
        {
            case CurveGeometryKind.Line3 when curve.Line3 is { } line:
            {
                var a = line.Evaluate(start); var b = line.Evaluate(end);
                areaTwice = a.X * b.Y - b.X * a.Y;
                return true;
            }
            case CurveGeometryKind.Circle3 when curve.Circle3 is { } circle && System.Math.Abs(circle.Normal.ToVector().Z) > 1d - 1e-8d:
            {
                var sign = circle.Normal.ToVector().Z >= 0d ? 1d : -1d;
                var a = start * sign; var b = end * sign;
                areaTwice = (circle.Center.X * circle.Radius * (System.Math.Sin(b) - System.Math.Sin(a)))
                    - (circle.Center.Y * circle.Radius * (System.Math.Cos(b) - System.Math.Cos(a)))
                    + (circle.Radius * circle.Radius * (b - a));
                return true;
            }
            default:
                return false;
        }
    }

    private static bool TryEvaluateExactAxisAlignedBoxQuarterCylinderFillet(
        BrepBody body,
        BrepMassPropertiesOptions options,
        BrepMassPropertiesTopologyDiagnostics topology,
        out BrepMassPropertiesResult result)
    {
        result = default!;
        var faces = body.Topology.Faces.ToArray();
        if (faces.Length != 9)
        {
            return false;
        }

        var cylinders = faces
            .Select(face => body.TryGetFaceSurfaceGeometry(face.Id, out var surface) ? surface : null)
            .Where(surface => surface?.Kind == SurfaceGeometryKind.Cylinder && surface.Cylinder.HasValue)
            .Select(surface => surface!.Cylinder!.Value)
            .ToArray();
        if (cylinders.Length != 1 || faces.Any(face => !body.TryGetFaceSurfaceGeometry(face.Id, out var surface) || surface is null || surface.Kind is not (SurfaceGeometryKind.Plane or SurfaceGeometryKind.Cylinder)))
        {
            return false;
        }

        var vertices = body.Topology.Vertices
            .Select(vertex => body.TryGetVertexPoint(vertex.Id, out var point) ? (Point3D?)point : (Point3D?)null)
            .ToArray();
        if (vertices.Any(point => point is null))
        {
            return false;
        }

        var points = vertices.Select(point => point!.Value).ToArray();
        var minX = points.Min(point => point.X); var maxX = points.Max(point => point.X);
        var minY = points.Min(point => point.Y); var maxY = points.Max(point => point.Y);
        var minZ = points.Min(point => point.Z); var maxZ = points.Max(point => point.Z);
        var tolerance = options.LinearTolerance;
        var sizeX = maxX - minX; var sizeY = maxY - minY; var sizeZ = maxZ - minZ;
        if (sizeX <= tolerance || sizeY <= tolerance || sizeZ <= tolerance || !HasAllBoxCorners(points, minX, maxX, minY, maxY, minZ, maxZ, tolerance))
        {
            return false;
        }

        var cylinder = cylinders[0];
        var axis = cylinder.Axis.ToVector();
        var axisIsX = System.Math.Abs(System.Math.Abs(axis.X) - 1d) <= 1e-8 && System.Math.Abs(axis.Y) <= 1e-8 && System.Math.Abs(axis.Z) <= 1e-8;
        var axisIsY = System.Math.Abs(System.Math.Abs(axis.Y) - 1d) <= 1e-8 && System.Math.Abs(axis.X) <= 1e-8 && System.Math.Abs(axis.Z) <= 1e-8;
        if (!axisIsX && !axisIsY)
        {
            return false;
        }

        var endpointCircles = body.Bindings.EdgeBindings
            .Select(binding => body.Geometry.TryGetCurve(binding.CurveGeometryId, out var curve) ? curve : null)
            .Where(curve => curve?.Kind == CurveGeometryKind.Circle3 && curve.Circle3.HasValue)
            .Select(curve => curve!.Circle3!.Value)
            .Distinct()
            .ToArray();
        if (endpointCircles.Length != 2 || endpointCircles.Any(circle => System.Math.Abs(circle.Radius - cylinder.Radius) > tolerance))
        {
            return false;
        }

        var span = (endpointCircles[1].Center - endpointCircles[0].Center).Length;
        if (span <= tolerance)
        {
            return false;
        }

        var boxVolume = sizeX * sizeY * sizeZ;
        var removedVolume = span * cylinder.Radius * cylinder.Radius * (1d - (System.Math.PI / 4d));
        var exactVolume = boxVolume - removedVolume;
        if (exactVolume <= 0d)
        {
            return false;
        }

        var boundary = EvaluateAtResolution(body, options, refinement: 2);
        if (boundary.Error is not null)
        {
            return false;
        }

        var sign = boundary.SignedVolume < 0d ? -1d : 1d;
        Point3D? centroid = System.Math.Abs(boundary.SignedVolume) > 1e-18d
            ? new Point3D(boundary.CentroidNumerator.X / boundary.SignedVolume, boundary.CentroidNumerator.Y / boundary.SignedVolume, boundary.CentroidNumerator.Z / boundary.SignedVolume)
            : null;
        result = new BrepMassPropertiesResult(
            BrepMassPropertiesStatus.NumericalConverged,
            sign * exactVolume,
            exactVolume,
            boundary.SurfaceArea,
            centroid,
            topology.IsEnclosed,
            topology.IsOrientationConsistent,
            boundary.Contributions,
            "ExactAxisAlignedBoxQuarterCylinderFillet",
            0d,
            options,
            topology,
            ["Exact axis-aligned box volume minus finite quarter-cylinder removal was recovered from emitted B-rep topology, analytic cylinder radius, and exact circular end trims."]);
        return true;

        static bool HasAllBoxCorners(IReadOnlyList<Point3D> points, double minX, double maxX, double minY, double maxY, double minZ, double maxZ, double tolerance)
            => new[] { minX, maxX }.All(x => new[] { minY, maxY }.All(y => new[] { minZ, maxZ }.All(z => points.Any(point => System.Math.Abs(point.X - x) <= tolerance && System.Math.Abs(point.Y - y) <= tolerance && System.Math.Abs(point.Z - z) <= tolerance))));
    }

    private static bool TryEvaluateExactSphereCylinderSeamBody(
        BrepBody body,
        BrepMassPropertiesOptions options,
        BrepMassPropertiesTopologyDiagnostics topology,
        out BrepMassPropertiesResult result)
    {
        result = default!;
        var faces = body.Topology.Faces.OrderBy(face => face.Id.Value).ToArray();
        if (faces.Length == 0)
        {
            return false;
        }

        var sphereFaces = new List<(FaceId Id, Geometry.Surfaces.SphereSurface Surface)>();
        var cylinderFaces = new List<(FaceId Id, Geometry.Surfaces.CylinderSurface Surface)>();
        foreach (var face in faces)
        {
            if (!body.TryGetFaceSurfaceGeometry(face.Id, out var surface) || surface is null)
            {
                return false;
            }

            if (surface.Kind == SurfaceGeometryKind.Sphere && surface.Sphere is { } sphere)
            {
                sphereFaces.Add((face.Id, sphere));
            }
            else if (surface.Kind == SurfaceGeometryKind.Cylinder && surface.Cylinder is { } cylinder)
            {
                cylinderFaces.Add((face.Id, cylinder));
            }
            else
            {
                return false;
            }
        }

        if (sphereFaces.Count == 0 || cylinderFaces.Count == 0)
        {
            return false;
        }

        var volume = 0d;
        var area = 0d;
        var moment = new Vector3D(0d, 0d, 0d);
        var contributions = new List<BrepMassPropertiesFaceContribution>(faces.Length);

        foreach (var (faceId, sphere) in sphereFaces)
        {
            var capVolume = 0d;
            var capArea = 0d;
            var capMoment = new Vector3D(0d, 0d, 0d);
            var loops = body.GetLoopIds(faceId);
            if (loops.Count == 0)
            {
                return false;
            }

            foreach (var loopId in loops)
            {
                if (!TryGetSingleFullCircle(body, loopId, out var circle))
                {
                    return false;
                }

                var offset = circle.Center - sphere.Center;
                var distance = offset.Length;
                if (distance <= 1e-9d || distance >= sphere.Radius || double.Abs(circle.Radius * circle.Radius + distance * distance - sphere.Radius * sphere.Radius) > sphere.Radius * sphere.Radius * 1e-6d)
                {
                    return false;
                }

                var height = sphere.Radius - distance;
                var removedVolume = double.Pi * height * height * (sphere.Radius - height / 3d);
                var removedArea = 2d * double.Pi * sphere.Radius * height;
                var axialCentroid = (double.Pi / 4d) * double.Pow(sphere.Radius * sphere.Radius - distance * distance, 2d) / removedVolume;
                var normal = offset / distance;
                var capCentroid = sphere.Center + normal * axialCentroid;
                capVolume += removedVolume;
                capArea += removedArea;
                capMoment += ToVector(capCentroid) * removedVolume;
            }

            var fullVolume = 4d * double.Pi * double.Pow(sphere.Radius, 3d) / 3d;
            var faceVolume = fullVolume - capVolume;
            if (faceVolume <= 1e-12d)
            {
                return false;
            }

            var faceArea = 4d * double.Pi * sphere.Radius * sphere.Radius - capArea;
            volume += faceVolume;
            area += faceArea;
            moment += ToVector(sphere.Center) * fullVolume - capMoment;
            contributions.Add(new(faceId, SurfaceGeometryKind.Sphere, faceVolume, faceArea, 0, true, true, true));
        }

        foreach (var (faceId, cylinder) in cylinderFaces)
        {
            var loops = body.GetLoopIds(faceId);
            if (loops.Count != 2 || !TryGetSingleFullCircle(body, loops[0], out var first) || !TryGetSingleFullCircle(body, loops[1], out var second))
            {
                return false;
            }

            var length = (second.Center - first.Center).Length;
            if (length <= 1e-9d || double.Abs(first.Radius - cylinder.Radius) > cylinder.Radius * 1e-6d || double.Abs(second.Radius - cylinder.Radius) > cylinder.Radius * 1e-6d)
            {
                return false;
            }

            var faceVolume = double.Pi * cylinder.Radius * cylinder.Radius * length;
            var faceArea = 2d * double.Pi * cylinder.Radius * length;
            var centroid = new Point3D((first.Center.X + second.Center.X) / 2d, (first.Center.Y + second.Center.Y) / 2d, (first.Center.Z + second.Center.Z) / 2d);
            volume += faceVolume;
            area += faceArea;
            moment += ToVector(centroid) * faceVolume;
            contributions.Add(new(faceId, SurfaceGeometryKind.Cylinder, faceVolume, faceArea, 0, true, true, true));
        }

        if (volume <= 1e-12d)
        {
            return false;
        }

        var centroidResult = new Point3D(moment.X / volume, moment.Y / volume, moment.Z / volume);
        result = new BrepMassPropertiesResult(
            BrepMassPropertiesStatus.NumericalConverged,
            volume,
            volume,
            area,
            centroidResult,
            topology.IsEnclosed,
            topology.IsOrientationConsistent,
            contributions,
            "ExactAnalyticSphereCylinderSeamBoundaryIntegral",
            0d,
            options,
            topology,
            topology.Messages.Append("Exact sphere-cylinder seam integration used emitted B-rep supports and trim circles; no construction-plan data was consumed.").ToArray());
        return true;
    }

    private static bool TryGetSingleFullCircle(BrepBody body, LoopId loopId, out Circle3Curve circle)
    {
        circle = default;
        var coedges = body.GetCoedgeIds(loopId);
        if (coedges.Count != 1)
        {
            return false;
        }

        var coedge = body.Topology.GetCoedge(coedges[0]);
        if (!body.TryGetEdgeCurveGeometry(coedge.EdgeId, out var curve)
            || curve?.Kind != CurveGeometryKind.Circle3
            || curve.Circle3 is not { } circleValue
            || !body.Bindings.TryGetEdgeBinding(coedge.EdgeId, out var binding)
            || binding.TrimInterval is not { } interval
            || double.Abs((interval.End - interval.Start) - 2d * double.Pi) > 1e-6d)
        {
            return false;
        }

        circle = circleValue;
        return true;
    }

    private static Vector3D ToVector(Point3D point) => new(point.X, point.Y, point.Z);

    private static Evaluation EvaluateAtResolution(BrepBody body, BrepMassPropertiesOptions options, int refinement)
    {
        // Reserve the upper half of the declared deterministic segment budget for
        // the refined pass.  This avoids a false "converged" result when both
        // passes immediately hit the same cap.
        var maximumSegments = System.Math.Max(options.MinimumSegments * refinement, options.MaximumSegments / (3 - refinement));
        var displayOptions = DisplayTessellationOptions.Create(
            options.AngularToleranceRadians / refinement,
            options.LinearTolerance / refinement,
            options.MinimumSegments * refinement,
            maximumSegments);
        if (!displayOptions.IsSuccess) return new(0d, 0d, Vector3D.Zero, [], string.Join("; ", displayOptions.Diagnostics.Select(d => d.Message)));
        var mesh = BrepDisplayTessellator.Tessellate(body, displayOptions.Value);
        if (!mesh.IsSuccess) return new(0d, 0d, Vector3D.Zero, [], string.Join("; ", mesh.Diagnostics.Select(d => d.Message)));

        var missingFaces = body.Topology.Faces
            .Where(face => !mesh.Value.FacePatches.Any(patch => patch.FaceId == face.Id && patch.TriangleIndices.Count >= 3))
            .Select(face => face.Id.Value)
            .OrderBy(id => id)
            .ToArray();
        if (missingFaces.Length > 0)
            return new(0d, 0d, Vector3D.Zero, [], $"Verification tessellation produced no triangles for face(s) {string.Join(",", missingFaces)}; mass properties are unavailable rather than partial.");

        var volume = 0d;
        var area = 0d;
        var moment = Vector3D.Zero;
        var contributions = new List<BrepMassPropertiesFaceContribution>();
        foreach (var patch in mesh.Value.FacePatches.OrderBy(p => p.FaceId.Value))
        {
            var faceVolume = 0d;
            var faceArea = 0d;
            var faceMoment = Vector3D.Zero;
            var coherent = true;
            var senseAvailable = body.Bindings.TryGetFaceBinding(patch.FaceId, out var binding);
            if (!senseAvailable) return new(0d, 0d, Vector3D.Zero, [], $"Face {patch.FaceId.Value} has no face-sense binding.");
            for (var i = 0; i < patch.TriangleIndices.Count; i += 3)
            {
                var a = patch.Positions[patch.TriangleIndices[i]];
                var b = patch.Positions[patch.TriangleIndices[i + 1]];
                var c = patch.Positions[patch.TriangleIndices[i + 2]];
                var cross = (b - a).Cross(c - a);
                // STEP import normalizes planar ADVANCED_FACE same_sense by
                // reversing the decoded PlaneSurface itself. Applying the binding
                // flag a second time would invert an equivalent imported plane;
                // canonical producer plans therefore use material-facing plane
                // supports. Curved grid patches retain native parameter normals and
                // require their explicit SameSense binding here.
                var normal = patch.Normals[patch.TriangleIndices[i]] + patch.Normals[patch.TriangleIndices[i + 1]] + patch.Normals[patch.TriangleIndices[i + 2]];
                var isPlanar = body.TryGetFaceSurfaceGeometry(patch.FaceId, out var surfaceForSense) && surfaceForSense?.Kind == SurfaceGeometryKind.Plane;
                if (!isPlanar && !binding.SameSense) normal = -normal;
                if (cross.Dot(normal) < 0d)
                {
                    (b, c) = (c, b);
                    cross = -cross;
                }
                coherent &= cross.Dot(normal) >= -1e-12d;
                var triangleArea = 0.5d * System.Math.Sqrt(cross.Dot(cross));
                var av = new Vector3D(a.X, a.Y, a.Z);
                var bv = new Vector3D(b.X, b.Y, b.Z);
                var cv = new Vector3D(c.X, c.Y, c.Z);
                var tetraVolume = av.Dot(bv.Cross(cv)) / 6d;
                faceArea += triangleArea;
                faceVolume += tetraVolume;
                faceMoment += (av + bv + cv) * (tetraVolume / 4d);
            }
            volume += faceVolume;
            area += faceArea;
            moment += faceMoment;
            body.TryGetFaceSurfaceGeometry(patch.FaceId, out var surface);
            contributions.Add(new(patch.FaceId, surface?.Kind, faceVolume, faceArea, patch.TriangleIndices.Count / 3, senseAvailable, binding.SameSense, coherent));
        }
        return new(volume, area, moment, contributions, null);
    }

    private static BrepMassPropertiesTopologyDiagnostics ValidateTopology(BrepBody body)
    {
        var messages = new List<string>();
        var bodies = body.Topology.Bodies.ToArray();
        var singleBody = bodies.Length == 1;
        if (!singleBody) messages.Add($"Expected exactly one body; found {bodies.Length}.");
        var edgeUses = new Dictionary<EdgeId, List<(FaceId Face, bool Reversed)>>();
        var connectedShells = 0;
        var loopsClosed = true;
        var senses = true;
        foreach (var shell in body.Topology.Shells)
        {
            var graph = shell.FaceIds.ToDictionary(f => f, _ => new HashSet<FaceId>());
            foreach (var faceId in shell.FaceIds)
            {
                senses &= body.Bindings.TryGetFaceBinding(faceId, out _);
                if (!body.Topology.TryGetFace(faceId, out var face) || face is null) { loopsClosed = false; messages.Add($"Shell {shell.Id.Value} references missing face {faceId.Value}."); continue; }
                foreach (var loopId in face.LoopIds)
                {
                    if (!body.Topology.TryGetLoop(loopId, out var loop) || loop is null || loop.CoedgeIds.Count == 0) { loopsClosed = false; messages.Add($"Face {faceId.Value} has missing or empty loop {loopId.Value}."); continue; }
                    for (var i = 0; i < loop.CoedgeIds.Count; i++)
                    {
                        if (!body.Topology.TryGetCoedge(loop.CoedgeIds[i], out var coedge) || coedge is null || !body.Topology.TryGetEdge(coedge.EdgeId, out var edge) || edge is null) { loopsClosed = false; messages.Add($"Loop {loopId.Value} references missing coedge or edge."); continue; }
                        var expectedNext = loop.CoedgeIds[(i + 1) % loop.CoedgeIds.Count];
                        if (coedge.NextCoedgeId != expectedNext) { loopsClosed = false; messages.Add($"Loop {loopId.Value} next-link is not cyclic."); }
                        var next = body.Topology.GetCoedge(expectedNext);
                        var nextEdge = body.Topology.GetEdge(next.EdgeId);
                        var directedUse = DirectedEdgeUse.Resolve(edge, coedge);
                        var nextDirectedUse = DirectedEdgeUse.Resolve(nextEdge, next);
                        if (!VerticesCoincident(body, directedUse.EndVertexId, nextDirectedUse.StartVertexId)) { loopsClosed = false; messages.Add($"Loop {loopId.Value} has disconnected coedges at {coedge.Id.Value}: end vertex {directedUse.EndVertexId.Value}, next start vertex {nextDirectedUse.StartVertexId.Value}."); }
                        if (!edgeUses.TryGetValue(edge.Id, out var uses)) edgeUses[edge.Id] = uses = [];
                        uses.Add((faceId, coedge.IsReversed));
                    }
                }
            }
            foreach (var uses in edgeUses.Values.Where(u => u.Count == 2 && graph.ContainsKey(u[0].Face) && graph.ContainsKey(u[1].Face)))
            {
                graph[uses[0].Face].Add(uses[1].Face); graph[uses[1].Face].Add(uses[0].Face);
            }
            if (graph.Count > 0)
            {
                var visited = new HashSet<FaceId>(); var stack = new Stack<FaceId>(); stack.Push(graph.Keys.First());
                while (stack.Count > 0) { var current = stack.Pop(); if (!visited.Add(current)) continue; foreach (var next in graph[current]) stack.Push(next); }
                if (visited.Count == graph.Count) connectedShells++; else messages.Add($"Shell {shell.Id.Value} is disconnected.");
            }
        }
        var closedParametricSurface = edgeUses.Count == 0
            && body.Topology.Faces.Any()
            && body.Topology.Faces.All(face => face.LoopIds.Count == 0
                && body.TryGetFaceSurfaceGeometry(face.Id, out var surface)
                && surface?.Kind == SurfaceGeometryKind.Sphere);
        var manifold = (edgeUses.Count > 0 && edgeUses.All(pair => pair.Value.Count == 2)) || closedParametricSurface;
        foreach (var (edge, uses) in edgeUses.Where(p => p.Value.Count != 2)) messages.Add($"Edge {edge.Value} has {uses.Count} uses; expected 2 for a closed manifold boundary.");
        if (!senses) messages.Add("At least one face has no face-sense binding.");
        var allShellsConnected = connectedShells == body.Topology.Shells.Count();
        var enclosed = singleBody && loopsClosed && senses && manifold && allShellsConnected;
        return new(singleBody, enclosed, loopsClosed && senses, connectedShells, messages);
    }

    private sealed record Evaluation(double SignedVolume, double SurfaceArea, Vector3D CentroidNumerator, IReadOnlyList<BrepMassPropertiesFaceContribution> Contributions, string? Error);

    private static Point3D? CentroidOf(Evaluation evaluation)
        => System.Math.Abs(evaluation.SignedVolume) > 1e-18d
            ? new Point3D(
                evaluation.CentroidNumerator.X / evaluation.SignedVolume,
                evaluation.CentroidNumerator.Y / evaluation.SignedVolume,
                evaluation.CentroidNumerator.Z / evaluation.SignedVolume)
            : null;

    private static bool VerticesCoincident(BrepBody body, VertexId left, VertexId right)
    {
        if (left == right) return true;
        if (!body.TryGetVertexPoint(left, out var a) || !body.TryGetVertexPoint(right, out var b)) return false;
        return (a - b).Length <= 1e-6d;
    }
}
