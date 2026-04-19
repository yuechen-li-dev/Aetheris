using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Boolean;

public static class BrepBooleanBoxMixedThroughVoidBuilder
{
    public static KernelResult<BrepBody> Build(
        SafeBooleanComposition composition,
        SupportedPrismaticSubtractTool prismaticTool,
        ToleranceContext tolerance)
    {
        if (composition.RootDescriptor.Kind != SafeBooleanRootKind.Box)
        {
            return Failure("Boolean Subtract: bounded mixed through-void builder requires a recognized safe box root.");
        }

        if (composition.OpenSlots is { Count: > 0 })
        {
            return Failure("Boolean Subtract: bounded mixed through-void builder does not support prior open-slot history.");
        }

        if (composition.Holes.Count != 1)
        {
            return Failure("Boolean Subtract: bounded mixed through-void builder currently supports exactly one analytic through-void history entry.");
        }

        var hole = composition.Holes[0];
        if (hole.SpanKind != SupportedBooleanHoleSpanKind.Through)
        {
            return Failure("Boolean Subtract: bounded mixed through-void builder currently supports only through analytic history.");
        }

        if (hole.Surface.Kind is not (AnalyticSurfaceKind.Cylinder or AnalyticSurfaceKind.Cone))
        {
            return Failure("Boolean Subtract: bounded mixed through-void builder currently supports cylinder/cone analytic through-void history.");
        }

        var axis = hole.Axis.ToVector();
        if (!ToleranceMath.AlmostZero(axis.X, tolerance)
            || !ToleranceMath.AlmostZero(axis.Y, tolerance)
            || !ToleranceMath.AlmostEqual(System.Math.Abs(axis.Z), 1d, tolerance))
        {
            return Failure("Boolean Subtract: bounded mixed through-void builder requires world-Z aligned analytic through-void history.");
        }

        if (!TryClassifyContainedAnalyticInsidePrismaticFootprint(hole, prismaticTool.Footprint, tolerance, out var classReason))
        {
            return Failure(classReason);
        }

        var rebuilt = BrepBooleanBoxPrismThroughCutBuilder.Build(composition.RootDescriptor.Box, prismaticTool.Footprint, tolerance);
        if (!rebuilt.IsSuccess)
        {
            return rebuilt;
        }

        var resultingComposition = composition with
        {
            Holes = [],
            ThroughVoids = new SupportedThroughVoidSet(
                AnalyticVoids: [],
                PrismaticVoids: [new SupportedPrismaticThroughVoid(prismaticTool.Bounds, prismaticTool.Footprint)])
        };
        var rebuiltBody = new BrepBody(
            rebuilt.Value.Topology,
            rebuilt.Value.Geometry,
            rebuilt.Value.Bindings,
            GetVertexPoints(rebuilt.Value),
            resultingComposition,
            rebuilt.Value.ShellRepresentation);
        return KernelResult<BrepBody>.Success(rebuiltBody, rebuilt.Diagnostics);
    }

    private static bool TryClassifyContainedAnalyticInsidePrismaticFootprint(
        in SupportedBooleanHole hole,
        IReadOnlyList<(double X, double Y)> footprint,
        ToleranceContext tolerance,
        out string unsupportedReason)
    {
        unsupportedReason = "Boolean Subtract: bounded mixed through-void builder requires a valid prismatic footprint.";
        if (footprint.Count < 3)
        {
            return false;
        }

        var radius = hole.MaxBoundaryRadius;
        var center = (X: hole.CenterX, Y: hole.CenterY);
        if (!IsPointInsidePolygon(center, footprint, tolerance))
        {
            unsupportedReason = "Boolean Subtract: bounded mixed through-void builder currently supports only the containment interaction class where the analytic through-void lies inside the incoming prismatic footprint.";
            return false;
        }

        var minEdgeDistance = ComputeMinimumDistanceToPolygonEdges(center, footprint);
        if (minEdgeDistance <= radius + (2d * tolerance.Linear))
        {
            unsupportedReason = "Boolean Subtract: bounded mixed through-void builder rejects tangent/edge-grazing analytic-prismatic interactions; containment requires strict positive radial margin.";
            return false;
        }

        unsupportedReason = string.Empty;
        return true;
    }

    private static bool IsPointInsidePolygon(
        (double X, double Y) point,
        IReadOnlyList<(double X, double Y)> polygon,
        ToleranceContext tolerance)
    {
        var winding = false;
        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            if (DistancePointToSegment(point, a, b) <= tolerance.Linear)
            {
                return true;
            }

            var intersects = ((a.Y > point.Y) != (b.Y > point.Y))
                && (point.X < (((b.X - a.X) * (point.Y - a.Y)) / (b.Y - a.Y + double.Epsilon)) + a.X);
            if (intersects)
            {
                winding = !winding;
            }
        }

        return winding;
    }

    private static double ComputeMinimumDistanceToPolygonEdges((double X, double Y) point, IReadOnlyList<(double X, double Y)> polygon)
    {
        var minDistance = double.PositiveInfinity;
        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            var distance = DistancePointToSegment(point, a, b);
            if (distance < minDistance)
            {
                minDistance = distance;
            }
        }

        return minDistance;
    }

    private static double DistancePointToSegment((double X, double Y) point, (double X, double Y) a, (double X, double Y) b)
    {
        var abX = b.X - a.X;
        var abY = b.Y - a.Y;
        var abLengthSquared = (abX * abX) + (abY * abY);
        if (abLengthSquared <= double.Epsilon)
        {
            var dx = point.X - a.X;
            var dy = point.Y - a.Y;
            return System.Math.Sqrt((dx * dx) + (dy * dy));
        }

        var t = (((point.X - a.X) * abX) + ((point.Y - a.Y) * abY)) / abLengthSquared;
        t = System.Math.Max(0d, System.Math.Min(1d, t));
        var closestX = a.X + (t * abX);
        var closestY = a.Y + (t * abY);
        var ddx = point.X - closestX;
        var ddy = point.Y - closestY;
        return System.Math.Sqrt((ddx * ddx) + (ddy * ddy));
    }

    private static IReadOnlyDictionary<VertexId, Point3D> GetVertexPoints(BrepBody source)
    {
        var points = new Dictionary<VertexId, Point3D>();
        foreach (var vertex in source.Topology.Vertices)
        {
            if (source.TryGetVertexPoint(vertex.Id, out var point))
            {
                points[vertex.Id] = point;
            }
        }

        return points;
    }

    private static KernelResult<BrepBody> Failure(string message)
        => KernelResult<BrepBody>.Failure([
            new KernelDiagnostic(
                KernelDiagnosticCode.NotImplemented,
                KernelDiagnosticSeverity.Error,
                message,
                "BrepBooleanBoxMixedThroughVoidBuilder.Build"),
        ]);
}
