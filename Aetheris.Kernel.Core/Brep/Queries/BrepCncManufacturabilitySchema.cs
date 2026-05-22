using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Queries;

public static class BrepCncManufacturabilitySchema
{
    public static CncManufacturabilitySchemaResult Evaluate(
        BrepBody body,
        CncManufacturabilitySchemaInput input,
        ToleranceContext? tolerance = null)
    {
        ArgumentNullException.ThrowIfNull(body);

        var context = tolerance ?? ToleranceContext.Default;
        var issues = new List<CncManufacturabilitySchemaIssue>();

        EvaluateInternalCornerRadiusRule(body, input, issues, context);
        EvaluateMinimumWallThicknessRule(body, input, issues, context);

        var hasFailure = issues.Any(issue => issue.Kind is CncManufacturabilityIssueKind.Violation or CncManufacturabilityIssueKind.Unsupported);
        return new CncManufacturabilitySchemaResult(!hasFailure, issues);
    }

    private static void EvaluateInternalCornerRadiusRule(
        BrepBody body,
        CncManufacturabilitySchemaInput input,
        List<CncManufacturabilitySchemaIssue> issues,
        ToleranceContext tolerance)
    {
        var concaveEdges = BrepManufacturingQueries.EnumerateInternalConcaveEdges(body, tolerance);
        if (!concaveEdges.IsSuccess)
        {
            issues.Add(new CncManufacturabilitySchemaIssue(
                CncManufacturabilityIssueKind.Unsupported,
                CncManufacturabilityRuleIds.MinimumInternalCornerRadius,
                Location: "body",
                MeasuredValue: null,
                RequiredThreshold: input.MinimumToolRadius,
                "Cannot evaluate minimum internal corner radius: internal concave-edge query is unsupported for this body."));
            return;
        }

        foreach (var edge in concaveEdges.Value)
        {
            if (!edge.RequiresFiniteToolRadius)
            {
                continue;
            }

            if (edge.MinimumToolRadiusLowerBound + tolerance.Linear >= input.MinimumToolRadius)
            {
                continue;
            }

            issues.Add(new CncManufacturabilitySchemaIssue(
                CncManufacturabilityIssueKind.Violation,
                CncManufacturabilityRuleIds.MinimumInternalCornerRadius,
                Location: $"edge:{edge.EdgeId.Value}",
                MeasuredValue: edge.MinimumToolRadiusLowerBound,
                RequiredThreshold: input.MinimumToolRadius,
                $"Internal concave edge '{edge.EdgeId}' is sharp/local-radius-bounded below minimum tool radius."));
        }
    }

    private static void EvaluateMinimumWallThicknessRule(
        BrepBody body,
        CncManufacturabilitySchemaInput input,
        List<CncManufacturabilitySchemaIssue> issues,
        ToleranceContext tolerance)
    {
        var planarFaces = new List<(FaceId FaceId, PlaneSurface Plane)>();
        var hasNonPlanarFace = false;

        foreach (var binding in body.Bindings.FaceBindings)
        {
            if (!body.TryGetFaceSurfaceGeometry(binding.FaceId, out var surface) || surface is null)
            {
                continue;
            }

            if (surface.Kind != SurfaceGeometryKind.Plane || surface.Plane is null)
            {
                hasNonPlanarFace = true;
                continue;
            }

            planarFaces.Add((binding.FaceId, surface.Plane.Value));
        }

        if (planarFaces.Count == 0)
        {
            issues.Add(new CncManufacturabilitySchemaIssue(
                CncManufacturabilityIssueKind.Unsupported,
                CncManufacturabilityRuleIds.MinimumWallThickness,
                Location: "body",
                MeasuredValue: null,
                RequiredThreshold: input.MinimumWallThickness,
                "Cannot evaluate minimum wall thickness: no planar faces are available for bounded probing."));
            return;
        }

        if (hasNonPlanarFace)
        {
            issues.Add(new CncManufacturabilitySchemaIssue(
                CncManufacturabilityIssueKind.Unsupported,
                CncManufacturabilityRuleIds.MinimumWallThickness,
                Location: "body",
                MeasuredValue: null,
                RequiredThreshold: input.MinimumWallThickness,
                "Minimum wall thickness v1 evaluates planar faces only; non-planar faces are present."));
        }

        foreach (var (faceId, plane) in planarFaces)
        {
            if (!TryResolvePlanarProbePoint(body, faceId, plane, tolerance, out var probePoint))
            {
                issues.Add(new CncManufacturabilitySchemaIssue(
                    CncManufacturabilityIssueKind.Unsupported,
                    CncManufacturabilityRuleIds.MinimumWallThickness,
                    Location: $"face:{faceId.Value}",
                    MeasuredValue: null,
                    RequiredThreshold: input.MinimumWallThickness,
                    "Could not resolve a bounded planar-face sample point for local thickness probing."));
                continue;
            }

            var thickness = BrepManufacturingQueries.ProbeLocalThickness(
                body,
                new LocalThicknessProbe(faceId, probePoint),
                tolerance);
            if (!thickness.IsSuccess)
            {
                issues.Add(new CncManufacturabilitySchemaIssue(
                    CncManufacturabilityIssueKind.Unsupported,
                    CncManufacturabilityRuleIds.MinimumWallThickness,
                    Location: $"face:{faceId.Value}",
                    MeasuredValue: null,
                    RequiredThreshold: input.MinimumWallThickness,
                    "Local thickness probe failed for planar-face inward measurement."));
                continue;
            }

            if (thickness.Value.Thickness + tolerance.Linear >= input.MinimumWallThickness)
            {
                continue;
            }

            issues.Add(new CncManufacturabilitySchemaIssue(
                CncManufacturabilityIssueKind.Violation,
                CncManufacturabilityRuleIds.MinimumWallThickness,
                Location: $"face:{faceId.Value}",
                MeasuredValue: thickness.Value.Thickness,
                RequiredThreshold: input.MinimumWallThickness,
                $"Local wall thickness on planar face '{faceId}' is below minimum wall threshold."));
        }
    }

    private static bool TryResolvePlanarProbePoint(
        BrepBody body,
        FaceId faceId,
        PlaneSurface plane,
        ToleranceContext tolerance,
        out Point3D probePoint)
    {
        probePoint = plane.Origin;

        if (!AnalyticPlanarFaceDomain.TryCreate(body, faceId, plane, out var domain))
        {
            return false;
        }

        if (domain.Contains(probePoint, tolerance))
        {
            return true;
        }

        var points = GetDistinctFaceVertices(body, faceId);
        if (points.Count == 0)
        {
            return false;
        }

        var centroid = new Point3D(points.Average(p => p.X), points.Average(p => p.Y), points.Average(p => p.Z));
        if (domain.Contains(centroid, tolerance))
        {
            probePoint = centroid;
            return true;
        }

        foreach (var point in points)
        {
            if (!domain.Contains(point, tolerance))
            {
                continue;
            }

            probePoint = point;
            return true;
        }

        return false;
    }

    private static IReadOnlyList<Point3D> GetDistinctFaceVertices(BrepBody body, FaceId faceId)
    {
        var points = new List<Point3D>();
        var seen = new HashSet<VertexId>();
        foreach (var edgeId in body.GetEdges(faceId))
        {
            foreach (var vertexId in body.GetVertices(edgeId))
            {
                if (!seen.Add(vertexId))
                {
                    continue;
                }

                if (body.TryGetVertexPoint(vertexId, out var point))
                {
                    points.Add(point);
                }
            }
        }

        return points;
    }
}

public readonly record struct CncManufacturabilitySchemaInput
{
    public CncManufacturabilitySchemaInput(double minimumToolRadius, double minimumWallThickness)
    {
        if (minimumToolRadius <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumToolRadius), "minimumToolRadius must be greater than 0.");
        }

        if (minimumWallThickness <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumWallThickness), "minimumWallThickness must be greater than 0.");
        }

        MinimumToolRadius = minimumToolRadius;
        MinimumWallThickness = minimumWallThickness;
    }

    public double MinimumToolRadius { get; }

    public double MinimumWallThickness { get; }
}

public static class CncManufacturabilityRuleIds
{
    public const string MinimumInternalCornerRadius = "cnc.minimum_internal_corner_radius";
    public const string MinimumWallThickness = "cnc.minimum_wall_thickness";
}

public enum CncManufacturabilityIssueKind
{
    Violation,
    Unsupported,
}

public readonly record struct CncManufacturabilitySchemaIssue(
    CncManufacturabilityIssueKind Kind,
    string RuleId,
    string Location,
    double? MeasuredValue,
    double RequiredThreshold,
    string Message);

public readonly record struct CncManufacturabilitySchemaResult(
    bool IsPass,
    IReadOnlyList<CncManufacturabilitySchemaIssue> Issues);
