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
        if (hole.SpanKind is not (SupportedBooleanHoleSpanKind.Through
            or SupportedBooleanHoleSpanKind.BlindFromTop
            or SupportedBooleanHoleSpanKind.BlindFromBottom))
        {
            return Failure("Boolean Subtract: bounded mixed through-void builder currently supports through or exterior-opening blind analytic history.");
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
        if (!BrepBooleanPrismaticFootprintContainment.TryComputeContainmentMargin(center, footprint, tolerance, out var minEdgeDistance))
        {
            unsupportedReason = "Boolean Subtract: bounded mixed through-void builder currently supports only the containment interaction class where the analytic through-void lies inside the incoming prismatic footprint.";
            return false;
        }

        if (minEdgeDistance <= radius + (2d * tolerance.Linear))
        {
            unsupportedReason = "Boolean Subtract: bounded mixed through-void builder rejects tangent/edge-grazing analytic-prismatic interactions; containment requires strict positive radial margin.";
            return false;
        }

        unsupportedReason = string.Empty;
        return true;
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
