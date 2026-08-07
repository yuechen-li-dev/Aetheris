using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>
/// Source-order, pre-emission description of a complete Profile edge finish.
/// This is intentionally a plan rather than a B-rep patch list: every analytic
/// patch and every shared curve is known before topology is allocated.
/// </summary>
public sealed record ProfileEdgeFinishMixedShellPlan(
    ProfileBoundaryChamferTarget Target,
    ProfileEdgeFinishKind FinishKind,
    double FinishSize,
    IReadOnlyList<AnalyticEdgeFinishPatch> OrderedPatches,
    IReadOnlyList<AnalyticEdgeFinishSeam> OrderedSeams,
    IReadOnlyList<string> DegenerateVertices,
    string TopCapPlan,
    string SideTrimPlan,
    IReadOnlyList<string> Provenance);

public sealed record ProfileEdgeFinishMixedShellPlanResult(
    bool Succeeded,
    ProfileEdgeFinishMixedShellPlan? Plan,
    IReadOnlyList<string> Diagnostics);

/// <summary>Typed analytic patch base; nullable-heavy surface records are deliberately avoided.</summary>
public abstract record AnalyticEdgeFinishPatch(
    string StableId,
    string SegmentId,
    ProfileEdgeFinishSurfaceFamily SurfaceFamily,
    ProfileEdgeFinishRegularity Regularity,
    string PlannerKind,
    string LocalFrame,
    string LowerBoundary,
    string UpperBoundary,
    string SideStartBoundary,
    string SideEndBoundary,
    IReadOnlyList<string> SemanticDescendants);

public sealed record PlanarChamferPatch(
    string StableId, string SegmentId, PlaneSurface Surface, string LocalFrame,
    IReadOnlyList<string> SemanticDescendants)
    : AnalyticEdgeFinishPatch(StableId, SegmentId, ProfileEdgeFinishSurfaceFamily.Plane,
        ProfileEdgeFinishRegularity.Regular, "LineChamferPlan", LocalFrame,
        "source-line@transition", "inset-line@cap", "line-seam:start", "line-seam:end", SemanticDescendants);

public sealed record ConicalChamferPatch(
    string StableId, string SegmentId, ConeSurface Surface, double SourceRadius,
    double InsetRadius, ProfileEdgeFinishRegularity Regularity, ConicalChamferTrimTopology TrimTopology, string LocalFrame,
    IReadOnlyList<string> SemanticDescendants)
    : AnalyticEdgeFinishPatch(StableId, SegmentId, ProfileEdgeFinishSurfaceFamily.Cone,
        Regularity, Regularity == ProfileEdgeFinishRegularity.BoundedDegenerate ? "ArcChamferApexPlan" : "ArcChamferConePlan",
        LocalFrame, "source-arc@transition", InsetRadius == 0d ? "apex-vertex@cap" : "inset-arc@cap",
        "line-seam:start", "line-seam:end", SemanticDescendants);

/// <summary>
/// The regular rounded-source Chamfer is one conical-frustum sector.  Its two
/// circular boundaries belong to the source and inset sections respectively;
/// its two generators are the explicit source-order Plane/Cone seams.  It is
/// not a planar miter patch at a line/arc transition.
/// </summary>
public enum ConicalChamferTrimTopology
{
    FrustumSector,
    ApexSector
}

public sealed record CylindricalFilletPatch(
    string StableId, string SegmentId, CylinderSurface Surface, string LocalFrame,
    IReadOnlyList<string> SemanticDescendants)
    : AnalyticEdgeFinishPatch(StableId, SegmentId, ProfileEdgeFinishSurfaceFamily.Cylinder,
        ProfileEdgeFinishRegularity.Regular, "StraightRoll", LocalFrame,
        "side-contact-line@transition", "cap-contact-line@cap", "quarter-circle-seam:start", "quarter-circle-seam:end", SemanticDescendants);

public sealed record SphericalFilletPatch(
    string StableId, string SegmentId, SphereSurface Surface, string LocalFrame,
    IReadOnlyList<string> SemanticDescendants)
    : AnalyticEdgeFinishPatch(StableId, SegmentId, ProfileEdgeFinishSurfaceFamily.Sphere,
        ProfileEdgeFinishRegularity.BoundedDegenerate, "ArcFilletSphereLimitPlan", LocalFrame,
        "source-arc@transition", "sphere-limit-apex@cap", "quarter-circle-seam:start", "quarter-circle-seam:end", SemanticDescendants);

public sealed record ToroidalFilletPatch(
    string StableId, string SegmentId, TorusSurface Surface, ProfileEdgeFinishTorusRegime Regime,
    ProfileEdgeFinishRegularity Regularity, string LocalFrame, IReadOnlyList<string> SemanticDescendants)
    : AnalyticEdgeFinishPatch(StableId, SegmentId, ProfileEdgeFinishSurfaceFamily.Torus,
        Regularity, "ArcFilletTorusPlan", LocalFrame, "source-arc@transition", "cap-contact-arc@cap",
        "quarter-circle-seam:start", "quarter-circle-seam:end", SemanticDescendants);

/// <summary>Topological shared curve known by provenance, never by spatial proximity.</summary>
public abstract record AnalyticEdgeFinishSeam(
    string StableId, string PredecessorPatchId, string SuccessorPatchId, string SourceVertexId,
    string CurveFamily, bool TraversesWithCurveParameter, string StartVertex, string EndVertex,
    IReadOnlyList<string> SemanticProvenance);

public sealed record PlaneConeSeam(
    string StableId, string PredecessorPatchId, string SuccessorPatchId, string SourceVertexId,
    bool TraversesWithCurveParameter, string StartVertex, string EndVertex, IReadOnlyList<string> SemanticProvenance)
    : AnalyticEdgeFinishSeam(StableId, PredecessorPatchId, SuccessorPatchId, SourceVertexId, "Line",
        TraversesWithCurveParameter, StartVertex, EndVertex, SemanticProvenance);

public sealed record CylinderTorusSeam(
    string StableId, string PredecessorPatchId, string SuccessorPatchId, string SourceVertexId,
    bool TraversesWithCurveParameter, string StartVertex, string EndVertex, IReadOnlyList<string> SemanticProvenance)
    : AnalyticEdgeFinishSeam(StableId, PredecessorPatchId, SuccessorPatchId, SourceVertexId, "Circle",
        TraversesWithCurveParameter, StartVertex, EndVertex, SemanticProvenance);

public sealed record CylinderSphereSeam(
    string StableId, string PredecessorPatchId, string SuccessorPatchId, string SourceVertexId,
    bool TraversesWithCurveParameter, string StartVertex, string EndVertex, IReadOnlyList<string> SemanticProvenance)
    : AnalyticEdgeFinishSeam(StableId, PredecessorPatchId, SuccessorPatchId, SourceVertexId, "Circle",
        TraversesWithCurveParameter, StartVertex, EndVertex, SemanticProvenance);

public sealed record SameFamilySeam(
    string StableId, string PredecessorPatchId, string SuccessorPatchId, string SourceVertexId,
    string CurveFamily, bool TraversesWithCurveParameter, string StartVertex, string EndVertex,
    IReadOnlyList<string> SemanticProvenance)
    : AnalyticEdgeFinishSeam(StableId, PredecessorPatchId, SuccessorPatchId, SourceVertexId, CurveFamily,
        TraversesWithCurveParameter, StartVertex, EndVertex, SemanticProvenance);

/// <summary>
/// Builds the authoritative analytic plan for a selected closed Profile loop.
/// Materialization is intentionally separate: consumers cannot stitch separately
/// emitted station B-reps because this plan owns the complete seam ring.
/// </summary>
public static class ProfileEdgeFinishMixedShellPlanner
{
    private const double Tolerance = 1e-8;

    public static ProfileEdgeFinishMixedShellPlanResult TryPlan(
        ResolvedProfile2D profile,
        ProfileBoundaryChamferTarget target,
        ProfileEdgeFinishKind finishKind,
        double finishSize)
    {
        ProfileEdgeFinishMixedShellPlanResult Fail(string code) => new(false, null, [code]);
        if (!double.IsFinite(finishSize) || finishSize <= 0d) return Fail("ProfileEdgeFinishMixedShellFinishSizeInvalid");
        if (target.ChainKind != ProfileBoundaryChamferChainKind.ClosedLoop) return Fail("ProfileEdgeFinishMixedShellClosedLoopRequired");
        var loop = profile.Loops.SingleOrDefault(x => x.Name == target.LoopId);
        if (loop is null || !loop.IsOuter || profile.Loops.Count != 1) return Fail("ProfileEdgeFinishMixedShellOuterLoopRequired");
        if (!target.SegmentIds.SequenceEqual(loop.Segments.Select(x => x.Name))) return Fail("ProfileEdgeFinishMixedShellWholeLoopSourceOrderRequired");
        var start = profile.LocalStartDepth ?? -1d;
        var end = profile.LocalEndDepth ?? 1d;
        if (finishSize >= end - start - Tolerance) return Fail("ProfileEdgeFinishMixedShellFinishExceedsHostThickness");

        var frame = profile.EffectiveConstructionPlane;
        var signedArea = SignedArea(loop);
        if (Math.Abs(signedArea) <= Tolerance) return Fail("ProfileEdgeFinishMixedShellProfileDegenerate");
        var materialSign = Math.Sign(signedArea);
        var transition = target.Side == ProfileBoundaryChamferSide.Top ? end - finishSize : start + finishSize;
        var cap = target.Side == ProfileBoundaryChamferSide.Top ? end : start;
        var capOut = target.Side == ProfileBoundaryChamferSide.Top ? frame.AxisZ : Direction3D.Create(-frame.AxisZ.ToVector());
        var axialInto = -capOut.ToVector();

        var patches = new List<AnalyticEdgeFinishPatch>(loop.Segments.Count);
        var degeneracies = new List<string>();
        foreach (var segment in loop.Segments)
        {
            var stableId = $"{target.StableId}:patch:{segment.Name}";
            switch (segment.Geometry)
            {
                case LineArcLineSegment2D line:
                {
                    var tangent = Direction(line, frame);
                    var inward = Inward(line, signedArea, frame);
                    var startPoint = frame.ToWorld(line.Start, transition);
                    if (finishKind == ProfileEdgeFinishKind.Chamfer)
                    {
                        var normal = Direction3D.Create(tangent.ToVector().Cross((inward.ToVector() * finishSize) + (capOut.ToVector() * finishSize)));
                        patches.Add(new PlanarChamferPatch(stableId, segment.Name,
                            new PlaneSurface(startPoint, normal, tangent), Frame(tangent, inward, capOut), Descendants(segment)));
                    }
                    else
                    {
                        var center = startPoint + inward.ToVector() * finishSize;
                        patches.Add(new CylindricalFilletPatch(stableId, segment.Name,
                            new CylinderSurface(center, tangent, finishSize, capOut), Frame(tangent, inward, capOut), Descendants(segment)));
                    }
                    break;
                }
                case LineArcCircularArc2D arc:
                {
                    var material = Math.Sign(arc.SweepAngleRadians) * materialSign >= 0d
                        ? ProfileEdgeFinishMaterialSide.Convex : ProfileEdgeFinishMaterialSide.Reflex;
                    var station = StationName(segment.Name);
                    var policy = ProfileEdgeFinishAnalyticPolicy.Classify(new(station, finishKind,
                        ProfileEdgeFinishSourceFamily.ArcDerived, material, arc.Radius, finishSize, target.ReflexJunctionStyle));
                    if (policy.Admission == ProfileEdgeFinishAdmission.UnsupportedWithTypedDiagnostic)
                        return Fail($"ProfileEdgeFinishMixedShellPatchRejected:station={station}:planner={policy.PlannerKind}:diagnostic={policy.ExpectedDiagnostic}");
                    var radial = Direction3D.Create(frame.ToWorldDirection(new Vector3D(Math.Cos(arc.StartAngleRadians), Math.Sin(arc.StartAngleRadians), 0d)));
                    var center = frame.ToWorld(arc.Center, transition);
                    var insetRadius = material == ProfileEdgeFinishMaterialSide.Convex ? arc.Radius - finishSize : arc.Radius + finishSize;
                    if (finishKind == ProfileEdgeFinishKind.Chamfer)
                    {
                        var cone = Cone(center, arc.Radius, insetRadius, finishSize, capOut, radial);
                        if (policy.Regularity == ProfileEdgeFinishRegularity.BoundedDegenerate) degeneracies.Add($"ConeApex:{segment.Name}:vertex={segment.Name}:cap");
                        patches.Add(new ConicalChamferPatch(stableId, segment.Name, cone, arc.Radius, insetRadius,
                            policy.Regularity,
                            policy.Regularity == ProfileEdgeFinishRegularity.BoundedDegenerate
                                ? ConicalChamferTrimTopology.ApexSector
                                : ConicalChamferTrimTopology.FrustumSector,
                            Frame(radial, Direction3D.Create(frame.ToWorldDirection(new Vector3D(-Math.Sin(arc.StartAngleRadians), Math.Cos(arc.StartAngleRadians), 0d))), capOut), Descendants(segment)));
                    }
                    else if (policy.SurfaceFamily == ProfileEdgeFinishSurfaceFamily.Sphere)
                    {
                        degeneracies.Add($"SphereLimit:{segment.Name}:vertex={segment.Name}:cap");
                        patches.Add(new SphericalFilletPatch(stableId, segment.Name,
                            new SphereSurface(center, capOut, finishSize, radial), Frame(radial, capOut, Direction3D.Create(frame.ToWorldDirection(new Vector3D(-Math.Sin(arc.StartAngleRadians), Math.Cos(arc.StartAngleRadians), 0d)))), Descendants(segment)));
                    }
                    else
                    {
                        var major = policy.TorusMajorRadius!.Value;
                        patches.Add(new ToroidalFilletPatch(stableId, segment.Name,
                            new TorusSurface(center, capOut, major, finishSize, radial), policy.TorusRegime, policy.Regularity,
                            Frame(radial, capOut, Direction3D.Create(frame.ToWorldDirection(new Vector3D(-Math.Sin(arc.StartAngleRadians), Math.Cos(arc.StartAngleRadians), 0d)))), Descendants(segment)));
                    }
                    break;
                }
                default:
                    return Fail("ProfileEdgeFinishMixedShellSegmentKindUnsupported");
            }
        }

        var seams = new List<AnalyticEdgeFinishSeam>(patches.Count);
        for (var i = 0; i < patches.Count; i++)
        {
            var predecessor = patches[i];
            var successor = patches[(i + 1) % patches.Count];
            var sourceVertex = $"{loop.Name}.{loop.Segments[(i + 1) % loop.Segments.Count].Name}.Start";
            var seamId = $"{target.StableId}:seam:{predecessor.SegmentId}->{successor.SegmentId}";
            var orientation = true; // authored loop traversal, later emitted as EDGE_CURVE.same_sense.
            var provenance = new[] { $"profile:{profile.Name}.{loop.Name}", $"source:{predecessor.SegmentId}->{successor.SegmentId}", "SourceOrder" };
            seams.Add(CreateSeam(finishKind, seamId, predecessor, successor, sourceVertex, orientation, provenance));
        }

        return new(true, new ProfileEdgeFinishMixedShellPlan(target, finishKind, finishSize, patches, seams, degeneracies,
            finishKind == ProfileEdgeFinishKind.Chamfer ? "MixedInsetLineArcProfile" : "MixedCapContactLineArcProfile",
            finishKind == ProfileEdgeFinishKind.Chamfer ? "OriginalSideToPlaneConeTrim" : "OriginalSideToCylinderSphereTorusTrim",
            ["ResolvedProfile2D", "ProfileEdgeFinishAnalyticPolicy", "ProfileEdgeFinishMixedShellPlan", "SourceOrder", "PreEmissionSeamCorrespondence"]), []);
    }

    private static AnalyticEdgeFinishSeam CreateSeam(ProfileEdgeFinishKind kind, string id, AnalyticEdgeFinishPatch predecessor,
        AnalyticEdgeFinishPatch successor, string sourceVertex, bool orientation, IReadOnlyList<string> provenance)
    {
        var start = $"{sourceVertex}:transition";
        var end = $"{sourceVertex}:cap";
        if (kind == ProfileEdgeFinishKind.Chamfer &&
            ((predecessor is PlanarChamferPatch && successor is ConicalChamferPatch) || (predecessor is ConicalChamferPatch && successor is PlanarChamferPatch)))
            return new PlaneConeSeam(id, predecessor.StableId, successor.StableId, sourceVertex, orientation, start, end, provenance);
        if (kind == ProfileEdgeFinishKind.Fillet &&
            ((predecessor is CylindricalFilletPatch && successor is ToroidalFilletPatch) || (predecessor is ToroidalFilletPatch && successor is CylindricalFilletPatch)))
            return new CylinderTorusSeam(id, predecessor.StableId, successor.StableId, sourceVertex, orientation, start, end, provenance);
        if (kind == ProfileEdgeFinishKind.Fillet &&
            ((predecessor is CylindricalFilletPatch && successor is SphericalFilletPatch) || (predecessor is SphericalFilletPatch && successor is CylindricalFilletPatch)))
            return new CylinderSphereSeam(id, predecessor.StableId, successor.StableId, sourceVertex, orientation, start, end, provenance);
        return new SameFamilySeam(id, predecessor.StableId, successor.StableId, sourceVertex,
            kind == ProfileEdgeFinishKind.Chamfer ? "Line" : "Circle", orientation, start, end, provenance);
    }

    private static ConeSurface Cone(Point3D lowerCenter, double lowerRadius, double upperRadius, double height, Direction3D capOut, Direction3D radial)
    {
        var semiAngle = Math.Atan(Math.Abs(upperRadius - lowerRadius) / height);
        // Axis always points from the apex toward the increasing radius.  For a convex
        // apex/funnel it therefore points away from the cap; for a reflex cone it points
        // toward the cap.  Both placements retain the exact lower circular boundary.
        var axis = upperRadius > lowerRadius ? capOut : Direction3D.Create(-capOut.ToVector());
        return new ConeSurface(lowerCenter, axis, lowerRadius, semiAngle, radial);
    }

    private static string StationName(string segmentName)
    {
        var match = Regex.Match(segmentName, "^(?<name>(?:Convex|Reflex)(?:Sharp|Small|Medium|Large))Arc$", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["name"].Value : segmentName;
    }

    private static IReadOnlyList<string> Descendants(ResolvedProfileSegment2D segment) =>
        [$"source:{segment.Provenance.StableId}", "EdgeFinishReplacementFace", "AnalyticPatch"];

    private static Direction3D Direction(LineArcLineSegment2D line, ConstructionPlane frame)
    {
        var vector = new Vector3D(line.End.X - line.Start.X, line.End.Y - line.Start.Y, 0d);
        return Direction3D.Create(frame.ToWorldDirection(vector));
    }

    private static Direction3D Inward(LineArcLineSegment2D line, double signedArea, ConstructionPlane frame)
    {
        var vector = new Vector3D(line.End.X - line.Start.X, line.End.Y - line.Start.Y, 0d);
        var length = vector.Length;
        var normal = signedArea > 0d ? new Vector3D(-vector.Y / length, vector.X / length, 0d) : new Vector3D(vector.Y / length, -vector.X / length, 0d);
        return Direction3D.Create(frame.ToWorldDirection(normal));
    }

    private static string Frame(Direction3D x, Direction3D y, Direction3D z) =>
        string.Create(CultureInfo.InvariantCulture, $"x=({x.X:R},{x.Y:R},{x.Z:R});y=({y.X:R},{y.Y:R},{y.Z:R});z=({z.X:R},{z.Y:R},{z.Z:R})");

    private static double SignedArea(ResolvedProfileLoop2D loop) => loop.Segments.Sum(segment => segment.Geometry switch
    {
        LineArcLineSegment2D line => line.Start.X * line.End.Y - line.End.X * line.Start.Y,
        LineArcCircularArc2D arc => ArcContribution(arc),
        _ => 0d
    }) * .5d;

    private static double ArcContribution(LineArcCircularArc2D arc)
    {
        var a = arc.StartAngleRadians;
        var b = a + arc.SweepAngleRadians;
        return 2d * (arc.Center.X * arc.Radius * (Math.Sin(b) - Math.Sin(a))
                     - arc.Center.Y * arc.Radius * (Math.Cos(b) - Math.Cos(a))
                     + arc.Radius * arc.Radius * (b - a));
    }
}

/// <summary>
/// First material consumer of <see cref="ProfileEdgeFinishMixedShellPlan"/>.
/// It emits the complete mixed Chamfer shell at once, so Plane/Cone seams are
/// shared topology rather than later B-rep stitching operations.
/// </summary>
public static class ProfileEdgeFinishMixedShellMaterializer
{
    private const double Tolerance = 1e-8;

    public static ProfileBoundaryChamferPlanResult TryMaterializeChamfer(
        ResolvedProfile2D profile, ProfileBoundaryChamferTarget target, ProfileEdgeFinishMixedShellPlan plan)
    {
        ProfileBoundaryChamferPlanResult Fail(string code) => new(false, null, null, target, [code]);
        if (plan.FinishKind != ProfileEdgeFinishKind.Chamfer) return Fail("ProfileEdgeFinishMixedShellChamferPlanRequired");
        var loop = profile.Loops.SingleOrDefault(x => x.Name == target.LoopId);
        if (loop is null || loop.Segments.Count != plan.OrderedPatches.Count) return Fail("ProfileEdgeFinishMixedShellPlanProfileMismatch");
        var frame = profile.EffectiveConstructionPlane;
        var start = profile.LocalStartDepth ?? -1d;
        var end = profile.LocalEndDepth ?? 1d;
        var transition = target.Side == ProfileBoundaryChamferSide.Top ? end - plan.FinishSize : start + plan.FinishSize;
        var cap = target.Side == ProfileBoundaryChamferSide.Top ? end : start;
        if (target.Side != ProfileBoundaryChamferSide.Top) return Fail("ProfileEdgeFinishMixedShellBottomChamferNotImplemented");
        var area = SignedArea(loop);
        if (Math.Abs(area) <= Tolerance) return Fail("ProfileEdgeFinishMixedShellProfileDegenerate");

        var builder = new TopologyBuilder();
        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var points = new Dictionary<VertexId, Point3D>();
        var vertices = new Dictionary<string, VertexId>(StringComparer.Ordinal);
        var edges = new Dictionary<string, EdgeId>(StringComparer.Ordinal);
        var seamBySuccessorPatch = plan.OrderedSeams.ToDictionary(x => x.SuccessorPatchId, StringComparer.Ordinal);
        var curveId = 1;
        var surfaceId = 1;
        var descendants = new List<SemanticTopologyDescendant>();

        VertexId Vertex(string key, Point3D point)
        {
            if (vertices.TryGetValue(key, out var id)) return id;
            id = builder.AddVertex(); vertices.Add(key, id); points.Add(id, point); return id;
        }
        (double X, double Y) InsetPoint(int sourceVertex, out int? apexSegment)
        {
            var previous = (sourceVertex + loop.Segments.Count - 1) % loop.Segments.Count;
            var current = sourceVertex;
            apexSegment = new[] { previous, current }
                .Where(index => loop.Segments[index].Geometry is LineArcCircularArc2D arc && Math.Sign(arc.SweepAngleRadians) * Math.Sign(area) >= 0d && Math.Abs(arc.Radius - plan.FinishSize) <= Tolerance)
                .Select(index => (int?)index).FirstOrDefault();
            if (apexSegment is { } apex && loop.Segments[apex].Geometry is LineArcCircularArc2D apexArc)
                return apexArc.Center;
            if (loop.Segments[previous].Geometry is LineArcLineSegment2D previousLine && loop.Segments[current].Geometry is LineArcLineSegment2D currentLine)
                return Intersect(OffsetLine(previousLine, area, plan.FinishSize), OffsetLine(currentLine, area, plan.FinishSize));
            var currentInset = Inset(loop.Segments[current].Geometry, area, plan.FinishSize);
            if (currentInset is not null) return Ends(currentInset)[0];
            var previousInset = Inset(loop.Segments[previous].Geometry, area, plan.FinishSize);
            if (previousInset is not null) return Ends(previousInset)[1];
            throw new InvalidOperationException("A mixed edge-finish vertex cannot have two collapsed arc boundaries.");
        }
        VertexId SectionVertex(int segment, bool atEnd, double depth, bool inset)
        {
            var sourceVertex = atEnd ? (segment + 1) % loop.Segments.Count : segment;
            if (inset)
            {
                var insetPoint = InsetPoint(sourceVertex, out var apex);
                return Vertex(apex is { } ? $"apex:{apex}:{depth:R}" : $"section:{sourceVertex}:{depth:R}:inset", frame.ToWorld(insetPoint, depth));
            }
            var point = Ends(loop.Segments[segment].Geometry)[atEnd ? 1 : 0];
            return Vertex($"section:{sourceVertex}:{depth:R}:source", frame.ToWorld(point, depth));
        }
        EdgeId Edge(string key, VertexId a, VertexId b, CurveGeometry curve, ParameterInterval trim, bool oriented)
        {
            if (edges.TryGetValue(key, out var existing)) return existing;
            var edge = builder.AddEdge(a, b); edges.Add(key, edge);
            var curveGeometryId = new CurveGeometryId(curveId++);
            geometry.AddCurve(curveGeometryId, curve);
            bindings.AddEdgeBinding(new EdgeGeometryBinding(edge, curveGeometryId, trim, oriented));
            return edge;
        }
        EdgeId SectionEdge(int segment, double depth, bool inset)
        {
            var curve2 = inset ? Inset(loop.Segments[segment].Geometry, area, plan.FinishSize) : loop.Segments[segment].Geometry;
            if (curve2 is null) throw new InvalidOperationException("Cone apex has no circular top edge.");
            var a = SectionVertex(segment, false, depth, inset); var b = SectionVertex(segment, true, depth, inset);
            var trim = curve2 is LineArcLineSegment2D ? new ParameterInterval(0d, (points[b] - points[a]).Length) : Trim(curve2);
            return Edge($"section:{segment}:{depth:R}:{inset}", a, b, Curve(curve2, depth, frame, points[a], points[b]), trim, Oriented(curve2));
        }
        EdgeId SeamEdge(int sourceVertex, VertexId a, VertexId b)
        {
            var key = $"seam:{sourceVertex}:{Math.Min(a.Value, b.Value)}:{Math.Max(a.Value, b.Value)}";
            return Edge(key, a, b, CurveGeometry.FromLine(new Line3Curve(points[a], Direction3D.Create(points[b] - points[a]))),
                new ParameterInterval(0d, (points[b] - points[a]).Length), true);
        }
        EdgeId AnalyticSeamEdge(AnalyticEdgeFinishSeam seam, VertexId a, VertexId b)
        {
            if (seam is not PlaneConeSeam && seam is not SameFamilySeam)
                throw new InvalidOperationException("Mixed Chamfer plan contains a non-Chamfer analytic seam.");
            if (!string.Equals(seam.CurveFamily, "Line", StringComparison.Ordinal))
                throw new InvalidOperationException("Mixed Chamfer analytic seam must be a generator line.");

            var key = $"analytic-seam:{seam.StableId}";
            if (edges.TryGetValue(key, out var existing)) return existing;
            var edge = Edge(key, a, b, CurveGeometry.FromLine(new Line3Curve(points[a], Direction3D.Create(points[b] - points[a]))),
                new ParameterInterval(0d, (points[b] - points[a]).Length), seam.TraversesWithCurveParameter);
            descendants.Add(new SemanticTopologyDescendant(seam.StableId, "Edge", SemanticTopologyRole.ComposeTransition,
                $"profile-seam:{seam.SourceVertexId}", Edge: edge, ParentStableId: target.StableId,
                GeometryPreview: $"{seam.GetType().Name}:{seam.CurveFamily}:sameSense={seam.TraversesWithCurveParameter}"));
            return edge;
        }
        Use Use(EdgeId edge, VertexId startVertex) => new(edge, builder.Model.Edges.Single(x => x.Id == edge).StartVertexId != startVertex);
        FaceId Face(string stableId, IReadOnlyList<Use> uses, SurfaceGeometry surface, SemanticTopologyRole role, string source)
        {
            var loopId = builder.AllocateLoopId();
            var coedges = uses.Select(_ => builder.AllocateCoedgeId()).ToArray();
            for (var i = 0; i < uses.Count; i++)
                builder.AddCoedge(new Coedge(coedges[i], uses[i].Edge, loopId, coedges[(i + 1) % uses.Count], coedges[(i + uses.Count - 1) % uses.Count], uses[i].Reverse));
            builder.AddLoop(new Loop(loopId, coedges));
            var face = builder.AddFace([loopId]);
            var sid = new SurfaceGeometryId(surfaceId++); geometry.AddSurface(sid, surface); bindings.AddFaceBinding(new FaceGeometryBinding(face, sid, true));
            descendants.Add(new(stableId, "Face", role, source, Face: face, ParentStableId: target.StableId));
            return face;
        }

        var count = loop.Segments.Count;
        var bottom = new EdgeId[count]; var middle = new EdgeId[count]; var top = new EdgeId?[count];
        for (var i = 0; i < count; i++)
        {
            bottom[i] = SectionEdge(i, start, false);
            middle[i] = SectionEdge(i, transition, false);
            var inset = Inset(loop.Segments[i].Geometry, area, plan.FinishSize);
            top[i] = inset is null ? null : SectionEdge(i, cap, true);
        }

        // Bottom cap is the source loop reversed; the top cap follows the mixed inset loop.
        Face($"{target.StableId}:bottom-cap", Enumerable.Range(0, count).Reverse().Select(i => Use(bottom[i], SectionVertex(i, true, start, false))).ToArray(),
            SurfaceGeometry.FromPlane(new PlaneSurface(frame.ToWorld((0d, 0d), start), Direction3D.Create(-frame.AxisZ.ToVector()), frame.AxisX)), SemanticTopologyRole.BottomFaceBoundaryLoop, $"profile:{profile.Name}.{loop.Name}");
        var topUses = new List<Use>();
        for (var i = 0; i < count; i++)
            if (top[i] is { } edge) topUses.Add(Use(edge, SectionVertex(i, false, cap, true)));
        Face($"{target.StableId}:top-cap", topUses, SurfaceGeometry.FromPlane(new PlaneSurface(frame.ToWorld((0d, 0d), cap), frame.AxisZ, frame.AxisX)), SemanticTopologyRole.TopFaceBoundaryLoop, $"profile:{profile.Name}.{loop.Name}");

        for (var i = 0; i < count; i++)
        {
            var next = (i + 1) % count;
            var lowerStart = SectionVertex(i, false, start, false); var lowerEnd = SectionVertex(i, true, start, false);
            var middleStart = SectionVertex(i, false, transition, false); var middleEnd = SectionVertex(i, true, transition, false);
            var vEnd = SeamEdge(next, lowerEnd, middleEnd); var vStart = SeamEdge(i, middleStart, lowerStart);
            Face($"{target.StableId}:side:{loop.Segments[i].Name}", [Use(bottom[i], lowerStart), Use(vEnd, lowerEnd), Use(middle[i], middleEnd), Use(vStart, middleStart)],
                SideSurface(loop.Segments[i].Geometry, start, frame), SemanticTopologyRole.ExtrusionSideFace, loop.Segments[i].Provenance.StableId);

            var patch = plan.OrderedPatches[i];
            var incomingSeam = seamBySuccessorPatch[patch.StableId];
            var outgoingPatch = plan.OrderedPatches[next];
            var outgoingSeam = seamBySuccessorPatch[outgoingPatch.StableId];
            var capStart = SectionVertex(i, false, cap, true); var capEnd = SectionVertex(i, true, cap, true);
            var seamEnd = AnalyticSeamEdge(outgoingSeam, middleEnd, capEnd); var seamStart = AnalyticSeamEdge(incomingSeam, capStart, middleStart);
            var transitionUses = new List<Use> { Use(middle[i], middleStart), Use(seamEnd, middleEnd) };
            if (top[i] is { } topEdge) transitionUses.Add(Use(topEdge, capEnd));
            transitionUses.Add(Use(seamStart, capStart));
            var surface = patch switch
            {
                PlanarChamferPatch plane => SurfaceGeometry.FromPlane(plane.Surface),
                ConicalChamferPatch cone => SurfaceGeometry.FromCone(cone.Surface),
                _ => throw new InvalidOperationException("Mixed Chamfer plan contains non-Chamfer patch.")
            };
            Face($"{target.StableId}:chamfer:{loop.Segments[i].Name}", transitionUses, surface, SemanticTopologyRole.EdgeFinishReplacementFace, loop.Segments[i].Provenance.StableId);
        }

        var shell = builder.AddShell(builder.Model.Faces.Select(x => x.Id).ToArray());
        builder.AddBody([shell]);
        var body = new BrepBody(builder.Model, geometry, bindings, points);
        var validation = BrepBindingValidator.Validate(body, true);
        if (!validation.IsSuccess) return Fail("ProfileEdgeFinishMixedShellChamferTopologyInvalid");
        var correspondence = new SemanticTopologyCorrespondence(target.HostBodyId, descendants,
            ["ResolvedProfile2D", "ProfileEdgeFinishMixedShellPlan", "PlaneConeSeam", "ConeApex", "AuthoritativeBRepPlan"]);
        return new(true, body, correspondence, target, ["ProfileEdgeFinishMixedShellChamfer", "ProfileEdgeFinishMixedShellPlaneCone", "ProfileEdgeFinishMixedShellConeApex"]);
    }

    private static LineArcProfileCurve2D? Inset(LineArcProfileCurve2D curve, double area, double distance) => curve switch
    {
        LineArcLineSegment2D line => OffsetLine(line, area, distance),
        LineArcCircularArc2D arc => OffsetArc(arc, area, distance),
        _ => throw new NotSupportedException("Mixed edge finish requires bounded line/arc profile segments.")
    };

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
        return radius <= Tolerance ? null : new LineArcCircularArc2D(arc.Center, radius, arc.StartAngleRadians, arc.SweepAngleRadians);
    }

    private static (double X, double Y)[] Ends(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => [line.Start, line.End],
        LineArcCircularArc2D arc => [(arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians)),
            (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians))],
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
        LineArcLineSegment2D line => new ParameterInterval(0d, Math.Sqrt(Math.Pow(line.End.X - line.Start.X, 2d) + Math.Pow(line.End.Y - line.Start.Y, 2d))),
        LineArcCircularArc2D arc => new ParameterInterval(Math.Min(arc.StartAngleRadians, arc.StartAngleRadians + arc.SweepAngleRadians), Math.Max(arc.StartAngleRadians, arc.StartAngleRadians + arc.SweepAngleRadians)),
        _ => throw new NotSupportedException()
    };

    private static bool Oriented(LineArcProfileCurve2D curve) => curve is not LineArcCircularArc2D arc || arc.SweepAngleRadians >= 0d;

    private static SurfaceGeometry SideSurface(LineArcProfileCurve2D curve, double start, ConstructionPlane frame) => curve switch
    {
        LineArcLineSegment2D line => SurfaceGeometry.FromPlane(new PlaneSurface(frame.ToWorld(line.Start, start), Direction3D.Create(Direction(line, frame).ToVector().Cross(frame.AxisZ.ToVector())), Direction(line, frame))),
        LineArcCircularArc2D arc => SurfaceGeometry.FromCylinder(new CylinderSurface(frame.ToWorld(arc.Center, start), frame.AxisZ, arc.Radius, frame.AxisX)),
        _ => throw new NotSupportedException()
    };

    private static Direction3D Direction(LineArcLineSegment2D line, ConstructionPlane frame) => Direction3D.Create(frame.ToWorldDirection(new Vector3D(line.End.X - line.Start.X, line.End.Y - line.Start.Y, 0d)));
    private static (double X, double Y) Intersect(LineArcLineSegment2D first, LineArcLineSegment2D second)
    {
        var rx = first.End.X - first.Start.X; var ry = first.End.Y - first.Start.Y;
        var sx = second.End.X - second.Start.X; var sy = second.End.Y - second.Start.Y;
        var cross = rx * sy - ry * sx;
        if (Math.Abs(cross) <= Tolerance) throw new InvalidOperationException("Offset line intersection is degenerate.");
        var qx = second.Start.X - first.Start.X; var qy = second.Start.Y - first.Start.Y;
        var t = (qx * sy - qy * sx) / cross;
        return (first.Start.X + t * rx, first.Start.Y + t * ry);
    }
    private static double SignedArea(ResolvedProfileLoop2D loop) => loop.Segments.Sum(x => x.Geometry switch
    {
        LineArcLineSegment2D line => line.Start.X * line.End.Y - line.End.X * line.Start.Y,
        LineArcCircularArc2D arc => 2d * (arc.Center.X * arc.Radius * (Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians) - Math.Sin(arc.StartAngleRadians)) - arc.Center.Y * arc.Radius * (Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians) - Math.Cos(arc.StartAngleRadians)) + arc.Radius * arc.Radius * arc.SweepAngleRadians),
        _ => 0d
    }) * .5d;
    private readonly record struct Use(EdgeId Edge, bool Reverse);
}
