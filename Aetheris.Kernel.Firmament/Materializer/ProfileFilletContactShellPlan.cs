using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>Exact local boundary supplied by one analytic fillet component to its parent shell.</summary>
public sealed record ProfileFilletContactBoundary(
    string StableId,
    string ComponentId,
    string SourceStableId,
    ProfileFilletContactBoundaryKind Kind,
    CurveGeometry Curve,
    ParameterInterval Trim,
    bool TraversesWithCurveParameter,
    Point3D Start,
    Point3D End,
    string RegularityEvidence,
    IReadOnlyList<string> Provenance);

public enum ProfileFilletContactBoundaryKind { CapContact, SideContact, PreviousInterface, NextInterface }

/// <summary>
/// Immutable, source-order contract used before a closed Fillet shell allocates
/// any topology.  It deliberately has no face or endpoint-termination ids.
/// </summary>
public sealed record ProfileFilletContactShellPlan(
    ProfileBoundaryChamferTarget Target,
    IReadOnlyList<ProfileFilletComponentContactContract> OrderedComponents,
    IReadOnlyList<ProfileFilletContactBoundary> OrderedInterfaces,
    IReadOnlyList<ProfileFilletContactBoundary> OrderedCapContacts,
    IReadOnlyDictionary<string, IReadOnlyList<ProfileFilletContactBoundary>> SideContactsBySource,
    IReadOnlyList<string> Provenance);

public sealed record ProfileFilletComponentContactContract(
    string ComponentId,
    string SourceStableId,
    ProfileEdgeFinishSurfaceFamily SurfaceFamily,
    ProfileFilletContactBoundary CapContact,
    IReadOnlyList<ProfileFilletContactBoundary> SideContacts,
    ProfileFilletContactBoundary PreviousInterface,
    ProfileFilletContactBoundary NextInterface,
    IReadOnlyList<string> SemanticDescendants,
    IReadOnlyList<string> Provenance);

public sealed record ProfileFilletContactShellPlanResult(bool Succeeded, ProfileFilletContactShellPlan? Plan, IReadOnlyList<string> Diagnostics);

/// <summary>
/// Contact-only planner for the rounded, source-tangent portion of a whole
/// Profile.  Sharp vertices are intentionally rejected here until their M2/M3
/// components supply the displaced cap/side boundaries; this prevents a future
/// emitter from silently substituting a naïve source offset at those vertices.
/// </summary>
public static class ProfileFilletContactShellPlanner
{
    private const double Tolerance = 1e-8;

    public static ProfileFilletContactShellPlanResult TryPlan(
        ResolvedProfile2D profile, ProfileBoundaryChamferTarget target, ProfileEdgeFinishMixedShellPlan mixed)
    {
        ProfileFilletContactShellPlanResult Fail(string diagnostic) => new(false, null, [diagnostic]);
        if (mixed.FinishKind != ProfileEdgeFinishKind.Fillet) return Fail("ProfileFilletContactShellFilletPlanRequired");
        var loop = profile.Loops.SingleOrDefault(x => x.Name == target.LoopId);
        if (loop is null || target.ChainKind != ProfileBoundaryChamferChainKind.ClosedLoop) return Fail("ProfileFilletContactShellClosedLoopRequired");
        if (loop.Segments.Count != mixed.OrderedPatches.Count) return Fail("ProfileFilletContactShellProfileMismatch");
        var area = SignedArea(loop);
        if (Math.Abs(area) <= Tolerance) return Fail("ProfileFilletContactShellProfileDegenerate");

        // The extracted M2/M3 components own displaced contacts at sharp
        // line/line vertices.  Do not let a rounded-source planner invent them.
        for (var i = 0; i < loop.Segments.Count; i++)
        {
            var previous = loop.Segments[(i + loop.Segments.Count - 1) % loop.Segments.Count].Geometry;
            var current = loop.Segments[i].Geometry;
            if (previous is LineArcLineSegment2D && current is LineArcLineSegment2D)
                return Fail($"ProfileFilletContactSharpJunctionComponentRequired:vertex={loop.Name}.{loop.Segments[i].Name}.Start");
        }

        var frame = profile.EffectiveConstructionPlane;
        var cap = profile.LocalEndDepth ?? 1d;
        var transition = cap - mixed.FinishSize;
        var components = new List<ProfileFilletComponentContactContract>(loop.Segments.Count);
        var interfaces = new List<ProfileFilletContactBoundary>(loop.Segments.Count);
        var caps = new List<ProfileFilletContactBoundary>(loop.Segments.Count);
        var sides = new Dictionary<string, IReadOnlyList<ProfileFilletContactBoundary>>(StringComparer.Ordinal);

        for (var i = 0; i < loop.Segments.Count; i++)
        {
            var segment = loop.Segments[i];
            var patch = mixed.OrderedPatches[i];
            var source = segment.Provenance.StableId;
            var componentId = patch.StableId;
            var (sourceStart, sourceEnd) = Ends(segment.Geometry);
            var start = frame.ToWorld(sourceStart, transition);
            var end = frame.ToWorld(sourceEnd, transition);
            var capCurve = CapCurve(segment.Geometry, area, mixed.FinishSize, cap, frame, out var capStart, out var capEnd);
            var capContact = new ProfileFilletContactBoundary($"{componentId}:cap", componentId, source, ProfileFilletContactBoundaryKind.CapContact,
                capCurve, Trim(segment.Geometry), Oriented(segment.Geometry), capStart, capEnd, patch.Regularity.ToString(), ["SourceOrder", "ParentCap"]);
            var side = new ProfileFilletContactBoundary($"{componentId}:side", componentId, source, ProfileFilletContactBoundaryKind.SideContact,
                Curve(segment.Geometry, transition, frame, start, end), Trim(segment.Geometry), Oriented(segment.Geometry), start, end, patch.Regularity.ToString(), ["SourceOrder", "ParentSide"]);
            var previous = Interface(componentId, source, ProfileFilletContactBoundaryKind.PreviousInterface, start, capStart, patch, mixed.FinishSize);
            var next = Interface(componentId, source, ProfileFilletContactBoundaryKind.NextInterface, capEnd, end, patch, mixed.FinishSize);
            components.Add(new(componentId, source, patch.SurfaceFamily, capContact, [side], previous, next, patch.SemanticDescendants, [patch.PlannerKind, "ContactContract"]));
            caps.Add(capContact); interfaces.Add(previous); interfaces.Add(next); sides.Add(source, [side]);
        }

        return new(true, new(target, components, interfaces, caps, sides,
            ["ResolvedProfile2D", "ProfileEdgeFinishMixedShellPlan", "ContactBeforeTopology"]), []);
    }

    private static ProfileFilletContactBoundary Interface(string componentId, string source, ProfileFilletContactBoundaryKind kind,
        Point3D start, Point3D end, AnalyticEdgeFinishPatch patch, double radius)
    {
        var centre = start + (end - start) * .5d;
        var radial = start - centre;
        var normal = radial.Cross(end - centre);
        if (normal.Length <= Tolerance) normal = new Vector3D(0d, 0d, 1d);
        return new($"{componentId}:{kind}", componentId, source, kind,
            CurveGeometry.FromCircle(new Circle3Curve(centre, Direction3D.Create(normal), radius, Direction3D.Create(radial))),
            new ParameterInterval(0d, Math.PI / 2d), true, start, end, patch.Regularity.ToString(), ["ExactRollingMeridian", "SourceTangency"]);
    }

    private static CurveGeometry CapCurve(LineArcProfileCurve2D curve, double area, double distance, double depth, ConstructionPlane frame, out Point3D start, out Point3D end)
    {
        LineArcProfileCurve2D? inset = curve switch
        {
            LineArcLineSegment2D line => OffsetLine(line, area, distance),
            LineArcCircularArc2D arc => OffsetArc(arc, area, distance),
            _ => throw new NotSupportedException()
        };
        if (inset is null) throw new InvalidOperationException("ProfileFilletContactSphereLimitRequiresExplicitApexComponent");
        var ends = Ends(inset); start = frame.ToWorld(ends.Start, depth); end = frame.ToWorld(ends.End, depth);
        return Curve(inset, depth, frame, start, end);
    }

    private static LineArcLineSegment2D OffsetLine(LineArcLineSegment2D line, double area, double distance)
    {
        var dx = line.End.X - line.Start.X; var dy = line.End.Y - line.Start.Y; var length = Math.Sqrt(dx * dx + dy * dy);
        var nx = area > 0d ? -dy / length : dy / length; var ny = area > 0d ? dx / length : -dx / length;
        return new((line.Start.X + nx * distance, line.Start.Y + ny * distance), (line.End.X + nx * distance, line.End.Y + ny * distance));
    }
    private static LineArcCircularArc2D? OffsetArc(LineArcCircularArc2D arc, double area, double distance)
    {
        var convex = Math.Sign(arc.SweepAngleRadians) * Math.Sign(area) >= 0d;
        var radius = convex ? arc.Radius - distance : arc.Radius + distance;
        return radius <= Tolerance ? null : new(arc.Center, radius, arc.StartAngleRadians, arc.SweepAngleRadians);
    }
    private static ((double X, double Y) Start, (double X, double Y) End) Ends(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => (line.Start, line.End),
        LineArcCircularArc2D arc => ((arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians)),
            (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians))),
        _ => throw new NotSupportedException()
    };
    private static CurveGeometry Curve(LineArcProfileCurve2D curve, double depth, ConstructionPlane frame, Point3D start, Point3D end) => curve switch
    {
        LineArcLineSegment2D => CurveGeometry.FromLine(new Line3Curve(start, Direction3D.Create(end - start))),
        LineArcCircularArc2D arc => CurveGeometry.FromCircle(new Circle3Curve(frame.ToWorld(arc.Center, depth), frame.AxisZ, arc.Radius, frame.AxisX)),
        _ => throw new NotSupportedException()
    };
    private static ParameterInterval Trim(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => new(0d, Math.Sqrt(Math.Pow(line.End.X - line.Start.X, 2d) + Math.Pow(line.End.Y - line.Start.Y, 2d))),
        LineArcCircularArc2D arc => new(Math.Min(arc.StartAngleRadians, arc.StartAngleRadians + arc.SweepAngleRadians), Math.Max(arc.StartAngleRadians, arc.StartAngleRadians + arc.SweepAngleRadians)),
        _ => throw new NotSupportedException()
    };
    private static bool Oriented(LineArcProfileCurve2D curve) => curve is not LineArcCircularArc2D arc || arc.SweepAngleRadians >= 0d;
    private static double SignedArea(ResolvedProfileLoop2D loop) => loop.Segments.Sum(x => x.Geometry switch
    {
        LineArcLineSegment2D line => line.Start.X * line.End.Y - line.End.X * line.Start.Y,
        LineArcCircularArc2D arc => 2d * (arc.Center.X * arc.Radius * (Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians) - Math.Sin(arc.StartAngleRadians)) - arc.Center.Y * arc.Radius * (Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians) - Math.Cos(arc.StartAngleRadians)) + arc.Radius * arc.Radius * arc.SweepAngleRadians),
        _ => 0d
    }) * .5d;
}
