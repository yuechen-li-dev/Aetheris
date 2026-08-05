using System.Diagnostics;

namespace Aetheris.Kernel.Firmament.Materializer;

public abstract record LineArcProfileCurve2D;
public sealed record LineArcLineSegment2D((double X, double Y) Start, (double X, double Y) End) : LineArcProfileCurve2D;
public sealed record LineArcCircularArc2D((double X, double Y) Center, double Radius, double StartAngleRadians, double SweepAngleRadians) : LineArcProfileCurve2D;
public sealed record LineArcFullCircle2D((double X, double Y) Center, double Radius) : LineArcProfileCurve2D;
public sealed record LineArcProfileLoop2D(IReadOnlyList<LineArcProfileCurve2D> Curves, bool IsHole);

/// <summary>Local profile and local depth interval. Height-only callers retain the historic centered interval.</summary>
public sealed record LineArcProfileExtrudeRequest(
    IReadOnlyList<LineArcProfileLoop2D> Loops,
    double Height,
    ConstructionPlane? ConstructionPlane = null,
    double? LocalStartDepth = null,
    double? LocalEndDepth = null);

public enum LineArcProfileExtrudeStatus { Succeeded, Rejected, Deferred, Failed }
public sealed record ProfileExtrusionTiming(TimeSpan PlanConstruction, TimeSpan BRepMaterialization);

/// <summary>The plan is published before materialization; correspondence is owned by that plan.</summary>
public sealed record LineArcProfileExtrudeResult(
    LineArcProfileExtrudeStatus Status,
    Aetheris.Kernel.Core.Brep.BrepBody? Body,
    IReadOnlyList<string> Diagnostics,
    SemanticTopologyCorrespondence? Correspondence = null,
    ProfileExtrusionBRepPlan? BRepPlan = null,
    ProfileExtrusionTiming? Timing = null);

/// <summary>
/// Compatibility entry point.  It is intentionally only a plan consumer: topology authority is
/// <see cref="ProfileExtrusionBRepPlanner"/>, and this class only materializes its exact entities.
/// </summary>
public static class LineArcProfileExtrudeEmitter
{
    public static LineArcProfileExtrudeResult TryEmit(LineArcProfileExtrudeRequest request)
        => Emit(request, null);

    /// <summary>Materializes a resolved authored Profile through its authoritative BRepPlan.</summary>
    public static LineArcProfileExtrudeResult TryEmit(ResolvedProfile2D profile, double height)
    {
        var validation = ResolvedProfile2DValidator.Validate(profile);
        if (!validation.IsValid)
            return new(LineArcProfileExtrudeStatus.Rejected, null, validation.Diagnostics);
        var request = new LineArcProfileExtrudeRequest(
            profile.Loops.Select(l => new LineArcProfileLoop2D(l.Segments.Select(s => s.Geometry).ToArray(), !l.IsOuter)).ToArray(),
            height,
            profile.EffectiveConstructionPlane,
            profile.LocalStartDepth,
            profile.LocalEndDepth);
        return Emit(request, profile);
    }

    private static LineArcProfileExtrudeResult Emit(LineArcProfileExtrudeRequest request, ResolvedProfile2D? source)
    {
        var planClock = Stopwatch.StartNew();
        var planned = ProfileExtrusionBRepPlanner.TryPlan(request, source);
        planClock.Stop();
        if (!planned.Succeeded || planned.Plan is null)
            return new(LineArcProfileExtrudeStatus.Rejected, null, planned.Diagnostics, null, null, new(planClock.Elapsed, TimeSpan.Zero));

        var materializationClock = Stopwatch.StartNew();
        var materialized = ProfileExtrusionBRepMaterializer.TryMaterialize(planned.Plan);
        materializationClock.Stop();
        var diagnostics = planned.Diagnostics.Concat(materialized.Diagnostics).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var timing = new ProfileExtrusionTiming(planClock.Elapsed, materializationClock.Elapsed);
        return materialized.Succeeded && materialized.Body is not null
            ? new(LineArcProfileExtrudeStatus.Succeeded, materialized.Body, diagnostics, planned.Plan.Correspondence, planned.Plan, timing)
            : new(LineArcProfileExtrudeStatus.Failed, null, diagnostics, planned.Plan.Correspondence, planned.Plan, timing);
    }
}
