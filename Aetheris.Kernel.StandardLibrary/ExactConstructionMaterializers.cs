using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.StandardLibrary;

internal readonly record struct ExactEdgeUse(EdgeId Edge, bool Reversed);
internal readonly record struct ExactRing(VertexId Positive, VertexId Negative, EdgeId Arc0, EdgeId Arc1);

/// <summary>Shared deterministic allocator used by all exact construction materializers.</summary>
internal sealed class ExactBrepEmissionContext
{
    private static readonly Direction3D PlusX = Direction3D.Create(new Vector3D(1d, 0d, 0d));
    private static readonly Direction3D PlusY = Direction3D.Create(new Vector3D(0d, 1d, 0d));
    private readonly TopologyBuilder topology = new();
    private readonly BrepGeometryStore geometry = new();
    private readonly BrepBindingModel bindings = new();
    private readonly Dictionary<VertexId, Point3D> points = [];
    private readonly List<FaceId> faces = [];

    internal IReadOnlyDictionary<VertexId, Point3D> Points => points;
    internal BrepGeometryStore Geometry => geometry;

    internal VertexId Vertex(Point3D point) { var id = topology.AddVertex(); points[id] = point; return id; }

    internal EdgeId CurveEdge(VertexId start, VertexId end, CurveGeometry curve, double t0, double t1)
    {
        var edge = topology.AddEdge(start, end);
        var curveId = new CurveGeometryId(geometry.Curves.Count() + 1);
        geometry.AddCurve(curveId, curve);
        bindings.AddEdgeBinding(new EdgeGeometryBinding(edge, curveId, new ParameterInterval(t0, t1)));
        return edge;
    }

    internal EdgeId Line(VertexId start, VertexId end)
    {
        var a = points[start]; var vector = points[end] - a;
        return CurveEdge(start, end, CurveGeometry.FromLine(new Line3Curve(a, Direction3D.Create(vector))), 0d, vector.Length);
    }

    internal ExactRing CircleRing(double x, double radius)
    {
        var positive = Vertex(new Point3D(x, radius, 0d));
        var negative = Vertex(new Point3D(x, -radius, 0d));
        var support = new Circle3Curve(new Point3D(x, 0d, 0d), PlusX, radius, PlusY);
        return new(positive, negative,
            CurveEdge(positive, negative, CurveGeometry.FromCircle(support), 0d, Math.PI),
            CurveEdge(negative, positive, CurveGeometry.FromCircle(support), Math.PI, 2d * Math.PI));
    }

    internal SurfaceGeometryId SharedSurface(SurfaceGeometry surface)
    {
        var id = new SurfaceGeometryId(geometry.Surfaces.Count() + 1);
        geometry.AddSurface(id, surface);
        return id;
    }

    internal FaceId Face(IReadOnlyList<IReadOnlyList<ExactEdgeUse>> loops, SurfaceGeometry surface,
        bool sameSense = true, SurfaceGeometryId? sharedSurfaceId = null)
    {
        var loopIds = new List<LoopId>();
        foreach (var uses in loops)
        {
            var loop = topology.AllocateLoopId();
            var coedges = uses.Select(_ => topology.AllocateCoedgeId()).ToArray();
            for (var i = 0; i < coedges.Length; i++)
                topology.AddCoedge(new Coedge(coedges[i], uses[i].Edge, loop, coedges[(i + 1) % coedges.Length],
                    coedges[(i + coedges.Length - 1) % coedges.Length], uses[i].Reversed));
            topology.AddLoop(new Loop(loop, coedges));
            loopIds.Add(loop);
        }
        var face = topology.AddFace(loopIds);
        var surfaceId = sharedSurfaceId ?? SharedSurface(surface);
        bindings.AddFaceBinding(new FaceGeometryBinding(face, surfaceId, sameSense));
        faces.Add(face);
        return face;
    }

    internal BrepBody Complete()
    {
        var shell = topology.AddShell(faces);
        topology.AddBody([shell]);
        return new BrepBody(topology.Model, geometry, bindings, points);
    }
}

internal sealed record RegularPrismSkeleton(
    VertexId[] Lower, VertexId[] Upper, EdgeId[] LowerEdges, EdgeId[] AxialEdges);

internal static class RegularPrismMaterializer
{
    internal static RegularPrismSkeleton PlanIncidence(ExactBrepEmissionContext context, RegularPrismConstruction prism)
    {
        var n = prism.SideCount;
        var step = 2d * Math.PI / n;
        var orientation = prism.OrientationDegrees * Math.PI / 180d;
        var lower = new VertexId[n]; var upper = new VertexId[n];
        for (var i = 0; i < n; i++)
        {
            var angle = orientation + i * step;
            var y = prism.Circumradius * Math.Cos(angle); var z = prism.Circumradius * Math.Sin(angle);
            lower[i] = context.Vertex(new Point3D(prism.Start, y, z));
            upper[i] = context.Vertex(new Point3D(prism.End, y, z));
        }
        var lowerEdges = new EdgeId[n]; var axialEdges = new EdgeId[n];
        for (var i = 0; i < n; i++)
        {
            lowerEdges[i] = context.Line(lower[i], lower[(i + 1) % n]);
            axialEdges[i] = context.Line(lower[i], upper[i]);
        }
        return new(lower, upper, lowerEdges, axialEdges);
    }

    internal static FaceId[] EmitSides(ExactBrepEmissionContext context, RegularPrismSkeleton prism, EdgeId[] upperEdges)
    {
        var n = prism.Lower.Length; var faces = new FaceId[n];
        var plusX = Direction3D.Create(new Vector3D(1d, 0d, 0d));
        for (var i = 0; i < n; i++)
        {
            var a = context.Points[prism.Lower[i]]; var b = context.Points[prism.Lower[(i + 1) % n]];
            var tangent = Direction3D.Create(b - a);
            var outward = Direction3D.Create(tangent.ToVector().Cross(plusX.ToVector()));
            faces[i] = context.Face([[new(prism.LowerEdges[i], false), new(prism.AxialEdges[(i + 1) % n], false),
                new(upperEdges[i], true), new(prism.AxialEdges[i], true)]], SurfaceGeometry.FromPlane(new PlaneSurface(a, outward, plusX)));
        }
        return faces;
    }
}

internal sealed record ConePlanarTrimEmission(EdgeId[] TrimEdges, EdgeId[] CapArcs, EdgeId[] Generators, FaceId[] Faces, FaceId Cap);

internal static class ConePlanarTrimMaterializer
{
    private static readonly Direction3D PlusX = Direction3D.Create(new Vector3D(1d, 0d, 0d));
    private static readonly Direction3D MinusX = Direction3D.Create(new Vector3D(-1d, 0d, 0d));
    private static readonly Direction3D PlusY = Direction3D.Create(new Vector3D(0d, 1d, 0d));

    internal static EdgeId[] EmitTrimEdges(ExactBrepEmissionContext context, RegularPrismConstruction prism,
        ConePlanarTrimConstruction trim, VertexId[] boundary)
    {
        var n = prism.SideCount; var edges = new EdgeId[n];
        var step = 2d * Math.PI / n; var orientation = prism.OrientationDegrees * Math.PI / 180d;
        for (var i = 0; i < n; i++)
        {
            var normalAngle = orientation + step / 2d + i * step;
            var normal = new Vector3D(0d, Math.Cos(normalAngle), Math.Sin(normalAngle));
            var support = new Hyperbola3Curve(new Point3D(trim.Apex, normal.Y * prism.Apothem, normal.Z * prism.Apothem),
                Direction3D.Create(normal), PlusX, prism.Apothem / Math.Tan(trim.SemiAngleDegrees * Math.PI / 180d),
                prism.Apothem, HyperbolaBranch.PositiveAxisU);
            var t0 = Parameter(support, context.Points[boundary[i]]); var t1 = Parameter(support, context.Points[boundary[(i + 1) % n]]);
            if (t1 < t0) { support = support.Reverse(); t0 = Parameter(support, context.Points[boundary[i]]); t1 = Parameter(support, context.Points[boundary[(i + 1) % n]]); }
            edges[i] = context.CurveEdge(boundary[i], boundary[(i + 1) % n], CurveGeometry.FromHyperbola(support), t0, t1);
        }
        return edges;
    }

    internal static ConePlanarTrimEmission EmitTreatment(ExactBrepEmissionContext context, RegularPrismConstruction prism,
        ConePlanarTrimConstruction trim, RegularPrismSkeleton skeleton, EdgeId[] trimEdges)
    {
        var n = prism.SideCount; var step = 2d * Math.PI / n; var orientation = prism.OrientationDegrees * Math.PI / 180d;
        var capVertices = new VertexId[n]; var capArcs = new EdgeId[n]; var generators = new EdgeId[n];
        var circle = new Circle3Curve(new Point3D(trim.CapPosition, 0d, 0d), PlusX, trim.CapRadius, PlusY);
        for (var i = 0; i < n; i++)
        {
            var angle = orientation + i * step;
            capVertices[i] = context.Vertex(new Point3D(trim.CapPosition, trim.CapRadius * Math.Cos(angle), trim.CapRadius * Math.Sin(angle)));
        }
        for (var i = 0; i < n; i++)
        {
            var angle = orientation + i * step;
            capArcs[i] = context.CurveEdge(capVertices[i], capVertices[(i + 1) % n], CurveGeometry.FromCircle(circle), angle, angle + step);
            generators[i] = context.Line(capVertices[i], skeleton.Upper[i]);
        }
        var cone = SurfaceGeometry.FromCone(new ConeSurface(new Point3D(trim.Apex, 0d, 0d), PlusX, trim.SemiAngleDegrees * Math.PI / 180d, PlusY));
        var coneId = context.SharedSurface(cone); var faces = new FaceId[n];
        for (var i = 0; i < n; i++)
            faces[i] = context.Face([[new(capArcs[i], false), new(generators[(i + 1) % n], false), new(trimEdges[i], true), new(generators[i], true)]], cone, sharedSurfaceId: coneId);
        var cap = context.Face([capArcs.Select(x => new ExactEdgeUse(x, false)).ToArray()],
            SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(trim.CapPosition, 0d, 0d), MinusX, PlusY)));
        return new(trimEdges, capArcs, generators, faces, cap);
    }

    private static double Parameter(Hyperbola3Curve curve, Point3D point) =>
        Math.Asinh((point - curve.Center).Dot(curve.AxisV.ToVector()) / curve.SemiAxisB);
}

internal sealed record PeriodicSpanEmission(FaceId[] Faces);

internal static class AxialCylinderMaterializer
{
    internal static PeriodicSpanEmission Emit(ExactBrepEmissionContext context, AxialCylinderConstruction section, ExactRing start, ExactRing end)
    {
        var plus = context.Line(start.Positive, end.Positive); var minus = context.Line(start.Negative, end.Negative);
        var plusX = Direction3D.Create(new Vector3D(1d, 0d, 0d)); var plusY = Direction3D.Create(new Vector3D(0d, 1d, 0d));
        var support = SurfaceGeometry.FromCylinder(new CylinderSurface(new Point3D(section.Start, 0d, 0d), plusX, section.Radius, plusY));
        var supportId = context.SharedSurface(support);
        return new([
            context.Face([[new(start.Arc0, false), new(minus, false), new(end.Arc0, true), new(plus, true)]], support, sharedSurfaceId: supportId),
            context.Face([[new(start.Arc1, false), new(plus, false), new(end.Arc1, true), new(minus, true)]], support, sharedSurfaceId: supportId)]);
    }
}

internal static class AxialFrustumMaterializer
{
    internal static PeriodicSpanEmission Emit(ExactBrepEmissionContext context, AxialFrustumConstruction section, ExactRing start, ExactRing end)
    {
        var plus = context.Line(start.Positive, end.Positive); var minus = context.Line(start.Negative, end.Negative);
        var slope = (section.StartRadius - section.EndRadius) / (section.End - section.Start);
        var apex = section.Start + section.StartRadius / slope;
        var minusX = Direction3D.Create(new Vector3D(-1d, 0d, 0d)); var plusY = Direction3D.Create(new Vector3D(0d, 1d, 0d));
        var support = SurfaceGeometry.FromCone(new ConeSurface(new Point3D(apex, 0d, 0d), minusX, Math.Atan(slope), plusY));
        var supportId = context.SharedSurface(support);
        return new([
            context.Face([[new(start.Arc0, false), new(minus, false), new(end.Arc0, true), new(plus, true)]], support, sharedSurfaceId: supportId),
            context.Face([[new(start.Arc1, false), new(plus, false), new(end.Arc1, true), new(minus, true)]], support, sharedSurfaceId: supportId)]);
    }
}

internal static class PeriodicTorusBlendMaterializer
{
    internal static PeriodicSpanEmission Emit(ExactBrepEmissionContext context, ConcaveFilletConstruction blend, ExactRing shoulder, ExactRing cylinder)
    {
        if (blend.Radius <= 1e-9d) return new([]);
        var minusX = Direction3D.Create(new Vector3D(-1d, 0d, 0d)); var plusX = Direction3D.Create(new Vector3D(1d, 0d, 0d));
        var plusY = Direction3D.Create(new Vector3D(0d, 1d, 0d));
        var plus = context.CurveEdge(shoulder.Positive, cylinder.Positive,
            CurveGeometry.FromCircle(new Circle3Curve(new Point3D(blend.End, blend.ShoulderRadius, 0d), Direction3D.Create(new Vector3D(0d, 0d, 1d)), blend.Radius, minusX)), 0d, Math.PI / 2d);
        var minus = context.CurveEdge(shoulder.Negative, cylinder.Negative,
            CurveGeometry.FromCircle(new Circle3Curve(new Point3D(blend.End, -blend.ShoulderRadius, 0d), Direction3D.Create(new Vector3D(0d, 0d, -1d)), blend.Radius, minusX)), 0d, Math.PI / 2d);
        var support = SurfaceGeometry.FromTorus(new TorusSurface(new Point3D(blend.End, 0d, 0d), plusX, blend.ShoulderRadius, blend.Radius, plusY));
        var supportId = context.SharedSurface(support);
        return new([
            context.Face([[new(shoulder.Arc0, false), new(minus, false), new(cylinder.Arc0, true), new(plus, true)]], support, sharedSurfaceId: supportId),
            context.Face([[new(shoulder.Arc1, false), new(plus, false), new(cylinder.Arc1, true), new(minus, true)]], support, sharedSurfaceId: supportId)]);
    }
}

/// <summary>Typed orchestration over the independent exact primitive materializers.</summary>
internal static class CoaxialConstructionMaterializer
{
    private const double Tolerance = 1e-9d;
    private static readonly Direction3D PlusX = Direction3D.Create(new Vector3D(1d, 0d, 0d));
    private static readonly Direction3D PlusY = Direction3D.Create(new Vector3D(0d, 1d, 0d));

    internal static KernelResult<ExactConstructionResult> Materialize(ExactCoaxialConstructionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var admission = Validate(plan);
        if (admission.Count > 0) return KernelResult<ExactConstructionResult>.Failure(admission);
        var realization = Build(plan);
        var preflight = BrepExportPreflight.Validate(realization.Body);
        if (!preflight.IsValid)
            return KernelResult<ExactConstructionResult>.Failure(preflight.Diagnostics
                .Where(d => d.Severity == BrepExportPreflightSeverity.Error)
                .Select(d => new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, d.Message, d.Context)));
        return KernelResult<ExactConstructionResult>.Success(realization);
    }

    private static IReadOnlyList<KernelDiagnostic> Validate(ExactCoaxialConstructionPlan plan)
    {
        var errors = new List<KernelDiagnostic>();
        void Require(bool condition, string message, string source)
        {
            if (!condition) errors.Add(new(KernelDiagnosticCode.InvalidArgument, KernelDiagnosticSeverity.Error, message, source));
        }
        var p = plan.Prism; var t = plan.ConePlanarTrim; var b = plan.RootBlend;
        var c = plan.Cylinder; var f = plan.EndFrustum;
        Require(p.SideCount >= 3, "A regular prism requires at least three sides.", "ExactConstruction.RegularPrism.SideCount");
        Require(double.IsFinite(p.AcrossFlats) && p.AcrossFlats > 0d && Math.Abs(p.End - p.Start) > Tolerance,
            "Regular prism dimensions must be finite and ordered.", "ExactConstruction.RegularPrism.Dimensions");
        Require(t.CapRadius > 0d && t.SemiAngleDegrees > 0d && t.SemiAngleDegrees < 90d,
            "Cone/plane trim requires a positive cap and semi-angle in (0,90).", "ExactConstruction.ConePlanarTrim");
        Require(b.Radius >= 0d && b.ShoulderRadius >= c.Radius && Math.Abs(b.Start - p.Start) <= Tolerance,
            "Root blend must join the prism shoulder to the cylinder.", "ExactConstruction.ConcaveFillet.Adjacency");
        Require(c.Radius > 0d && c.End > c.Start && Math.Abs(c.Start - b.End) <= Tolerance,
            "Cylinder span must be positive and adjacent to the root blend.", "ExactConstruction.AxialCylinder.Adjacency");
        Require(f.StartRadius > 0d && f.EndRadius > 0d && f.StartRadius != f.EndRadius && f.End > f.Start
            && Math.Abs(f.Start - c.End) <= Tolerance && Math.Abs(f.StartRadius - c.Radius) <= Tolerance,
            "Frustum must be non-cylindrical and adjacent to the cylinder.", "ExactConstruction.AxialFrustum.Adjacency");
        Require(Math.Abs(plan.TopCap.Position - t.CapPosition) <= Tolerance && Math.Abs(plan.TopCap.Radius - t.CapRadius) <= Tolerance,
            "Top cap must share the cone trim boundary.", "ExactConstruction.AxialSectionStack.TopCap");
        Require(Math.Abs(plan.EndCap.Position - f.End) <= Tolerance && Math.Abs(plan.EndCap.Radius - f.EndRadius) <= Tolerance,
            "End cap must share the frustum boundary.", "ExactConstruction.AxialSectionStack.EndCap");
        ExactConstructionNode[] expected = [p, t, plan.TopCap, b, c, f, plan.EndCap];
        Require(plan.Stack.Sections.SequenceEqual(expected), "Axial section stack order or node identity is inconsistent with the typed plan.",
            "ExactConstruction.AxialSectionStack.Order");
        var roles = new HashSet<string>(["PrismSides", "ConePlanarTrim", "TopCap", "Shoulder", "RootBlend", "Cylinder", "EndFrustum", "EndCap"], StringComparer.Ordinal);
        foreach (var claim in plan.SemanticClaims.Where(x => x.TopologyRole is not null))
            Require(roles.Contains(claim.TopologyRole!), $"Semantic claim references unknown topology role '{claim.TopologyRole}'.", "ExactConstruction.SemanticClaim.Role");
        return errors;
    }

    private static ExactConstructionResult Build(ExactCoaxialConstructionPlan plan)
    {
        var prism = plan.Prism; var trim = plan.ConePlanarTrim; var blend = plan.RootBlend;
        var cylinder = plan.Cylinder; var frustum = plan.EndFrustum;
        var context = new ExactBrepEmissionContext();
        var prismSkeleton = RegularPrismMaterializer.PlanIncidence(context, prism);
        var trimEdges = ConePlanarTrimMaterializer.EmitTrimEdges(context, prism, trim, prismSkeleton.Upper);
        var shoulderRing = context.CircleRing(prism.Start, blend.ShoulderRadius);
        var cylinderStartRing = blend.Radius > Tolerance ? context.CircleRing(blend.End, cylinder.Radius) : shoulderRing;
        var frustumStartRing = context.CircleRing(frustum.Start, frustum.StartRadius);
        var endRing = context.CircleRing(frustum.End, frustum.EndRadius);

        var sideFaces = RegularPrismMaterializer.EmitSides(context, prismSkeleton, trimEdges);
        var coneTreatment = ConePlanarTrimMaterializer.EmitTreatment(context, prism, trim, prismSkeleton, trimEdges);
        var shoulder = context.Face([
            prismSkeleton.LowerEdges.Reverse().Select(e => new ExactEdgeUse(e, true)).ToArray(),
            [new(shoulderRing.Arc0, false), new(shoulderRing.Arc1, false)]],
            SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(prism.Start, 0d, 0d), PlusX, PlusY)));
        var rootBlend = PeriodicTorusBlendMaterializer.Emit(context, blend, shoulderRing, cylinderStartRing);
        var cylinderSpan = AxialCylinderMaterializer.Emit(context, cylinder, cylinderStartRing, frustumStartRing);
        var frustumSpan = AxialFrustumMaterializer.Emit(context, frustum, frustumStartRing, endRing);
        var endCap = context.Face([[new(endRing.Arc1, true), new(endRing.Arc0, true)]],
            SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(plan.EndCap.Position, 0d, 0d), PlusX, PlusY)));
        var body = context.Complete();
        IReadOnlyDictionary<string, IReadOnlyList<FaceId>> groups = new Dictionary<string, IReadOnlyList<FaceId>>(StringComparer.Ordinal)
        {
            ["PrismSides"] = sideFaces, ["ConePlanarTrim"] = coneTreatment.Faces, ["TopCap"] = [coneTreatment.Cap],
            ["Shoulder"] = [shoulder], ["RootBlend"] = rootBlend.Faces, ["Cylinder"] = cylinderSpan.Faces,
            ["EndFrustum"] = frustumSpan.Faces, ["EndCap"] = [endCap]
        };
        return new(body, groups, BindSemantics(plan.SemanticClaims, groups), plan.Metadata, plan.DeterministicSignature);
    }

    private static IReadOnlyList<ExactConstructionSemanticDescendant> BindSemantics(
        IReadOnlyList<ConstructionSemanticClaim> claims, IReadOnlyDictionary<string, IReadOnlyList<FaceId>> groups)
    {
        var descendants = new List<ExactConstructionSemanticDescendant>();
        foreach (var claim in claims)
        {
            var kind = claim.Kind switch { ConstructionSemanticKind.Part => ExactConstructionSemanticKind.Part,
                ConstructionSemanticKind.Region => ExactConstructionSemanticKind.Region, _ => ExactConstructionSemanticKind.Face };
            if (claim.TopologyRole is null)
            {
                descendants.Add(new(claim.StableIdPattern, kind, ParentStableId: claim.ParentStableId, Metadata: claim.Metadata));
                continue;
            }
            var roleFaces = groups[claim.TopologyRole];
            for (var i = 0; i < roleFaces.Count; i++)
            {
                var stableId = claim.StableIdPattern.Contains("{i}", StringComparison.Ordinal)
                    ? claim.StableIdPattern.Replace("{i}", i.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                    : claim.StableIdPattern;
                descendants.Add(new(stableId, kind, roleFaces[i], claim.ParentStableId, claim.Metadata));
            }
        }
        return descendants;
    }
}
