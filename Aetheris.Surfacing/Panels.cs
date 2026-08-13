using Aetheris.Geometry;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Semantics;

namespace Aetheris.Surfacing;

public enum PanelNormalOrientation { SupportNormal, ReversedSupportNormal }
public enum PanelMaterialSide { Front, Back }
public enum PanelEdgeDirection { AlongSource, OppositeSource }
public enum PanelContinuity { PositionG0, TangentG1, CurvatureG2 }
public enum PanelEdgeCorrespondence { OppositeDirections, SameDirection }

public sealed record PanelOrientation(
    PanelNormalOrientation Normal,
    PanelMaterialSide MaterialSide)
{
    public static PanelOrientation Front { get; } = new(PanelNormalOrientation.SupportNormal, PanelMaterialSide.Front);
    public bool SameSense => Normal == PanelNormalOrientation.SupportNormal;
}

public sealed record PanelConstruction(
    string StableId,
    SurfaceConstructionKind Kind,
    ParametricDomain ParameterDomain,
    SurfaceGeometry Support,
    Func<double, double, Point3D> Evaluate,
    BoundedParametricPatch3 AuthoredPatch,
    SurfaceMaterializationKind MaterializationKind,
    ApproximationCertificate? Approximation,
    DevelopabilityEvidence Developability,
    IReadOnlyList<BoundaryProvenance> Provenance);

public sealed record PanelEdgeIr(
    string StableId,
    string Name,
    int BoundaryOrder,
    CurveGeometry Curve,
    double ParameterStart,
    double ParameterEnd,
    PanelEdgeDirection SourceDirection,
    string SourceCurveStableId,
    IReadOnlyList<BoundaryProvenance> Provenance,
    SemanticValue SemanticValue)
{
    /// <summary>The semantic edge as authored, including its directed parameter orientation.</summary>
    public BoundedParametricCurve3 AuthoredCurve { get; } = BoundedParametricCurve3.FromCurveGeometry(
        StableId + ":curve", Curve, ParameterStart, ParameterEnd,
        string.Join(";", Provenance.Select(item => item.SourceIdentity + ":" + item.Role)), StableId);
    public Point3D Start => Evaluate(0);
    public Point3D End => Evaluate(1);
    public Point3D Evaluate(double normalized)
    {
        return AuthoredCurve.Evaluate(AuthoredCurve.Domain.Map(normalized));
    }

    public double Length(int segments = 64)
    {
        var length = 0d; var previous = Start;
        for (var i = 1; i <= segments; i++) { var next = Evaluate(i / (double)segments); length += (next - previous).Length; previous = next; }
        return length;
    }
}

public sealed record PanelCornerIr(string StableId, string Name, Point3D Point, SemanticValue SemanticValue);

/// <summary>A bounded, oriented engineering object backed by a surface construction. It is not a solid body.</summary>
public sealed record PanelIr(
    string StableId,
    PanelConstruction SurfaceConstruction,
    IReadOnlyList<PanelEdgeIr> BoundaryEdges,
    IReadOnlyDictionary<string, PanelCornerIr> Corners,
    PanelOrientation Orientation,
    double? Thickness,
    string? Material,
    SemanticValue SemanticValue)
{
    public BoundedParametricPatch3 AuthoredPatch => SurfaceConstruction.AuthoredPatch;
    public ParametricDomain ParameterDomain => SurfaceConstruction.ParameterDomain;
    public DevelopabilityEvidence Developability => SurfaceConstruction.Developability;
    public ApproximationCertificate? ApproximationStatus => SurfaceConstruction.Approximation;
    public PanelEdgeIr this[string edgeName] => BoundaryEdges.Single(edge => edge.Name == edgeName);
}

public sealed record PanelResult(PanelIr? Panel, IReadOnlyList<SurfacingDiagnostic> Diagnostics)
{
    public bool IsSuccess => Panel is not null && Diagnostics.Count == 0;
}

public sealed record PanelConceptEvidence(bool HasBoundedSurface,bool HasClosedOrderedBoundary,bool HasOrientation,IReadOnlyList<SurfacingDiagnostic> Diagnostics)
{ public bool Satisfies => HasBoundedSurface&&HasClosedOrderedBoundary&&HasOrientation&&Diagnostics.Count==0; }

/// <summary>Bounded structural Concept: one support/domain, one closed ordered boundary, and one explicit orientation.</summary>
public static class PanelConcept
{
    public static PanelConceptEvidence Validate(PanelIr panel)
    {
        ArgumentNullException.ThrowIfNull(panel);var diagnostics=new List<SurfacingDiagnostic>();
        var bounded=panel.ParameterDomain.U.Maximum>panel.ParameterDomain.U.Minimum&&panel.ParameterDomain.V.Maximum>panel.ParameterDomain.V.Minimum;
        var ordered=panel.BoundaryEdges.Count is 3 or 4&&panel.BoundaryEdges.Select(edge=>edge.BoundaryOrder).SequenceEqual(Enumerable.Range(0,panel.BoundaryEdges.Count));
        if(!bounded)diagnostics.Add(new("panel-concept-unbounded-surface","PanelConcept requires a bounded surface domain."));
        if(!ordered)diagnostics.Add(new("panel-concept-boundary-invalid","PanelConcept requires a closed ordered three- or four-edge boundary."));
        return new(bounded,ordered,panel.Orientation is not null,diagnostics);
    }
}

public static class RuledCanopyPanelTemplate
{
    public static PanelResult Create(string stableId,double width,double depth,double rise,double? thickness=null,string? material=null) =>
        PanelFactory.FromRuled(RuledCanopyTemplate.Create(stableId,width,depth,rise),thickness:thickness,material:material);
}

public static class PanelManufacturability
{
    public static IReadOnlyList<SurfacingDiagnostic> RequireDevelopable(PanelIr panel) => panel.Developability.Kind switch
    {
        DevelopabilityKind.Developable => [],
        DevelopabilityKind.NonDevelopable => [new("panel-fabrication-nondevelopable",$"Panel '{panel.StableId}' is NonDevelopable and cannot be sent to a future flat-pattern lowering.")],
        _ => [new("panel-fabrication-developability-indeterminate",$"Panel '{panel.StableId}' has Indeterminate developability; fabrication requires positive evidence.")]
    };
}

public static class PanelFactory
{
    private static readonly string[] EdgeNames = ["South", "East", "North", "West"];

    public static PanelResult FromParametric(
        ParametricSurfaceIr source,
        PanelOrientation? orientation = null,
        double? thickness = null,
        string? material = null,
        int controlCountU = 17,
        int controlCountV = 17,
        double tolerance = .1)
    {
        ArgumentNullException.ThrowIfNull(source);
        var materialized = ParametricSurfaceMaterializer.Materialize(source, controlCountU, controlCountV, tolerance);
        var construction = new PanelConstruction(source.StableId, source.ConstructionKind, source.Domain,
            SurfaceGeometry.FromBSplineSurfaceWithKnots(materialized.Surface),
            (u, v) => source.Evaluate(source.Domain.U.Map(u), source.Domain.V.Map(v)).Point,
            source.Patch,
            materialized.Kind, materialized.Certificate,
            new(DevelopabilityKind.Indeterminate, "parametric curvature classification", null, 0, "A bounded parametric panel is not assumed developable."),
            [new(source.StableId, source.Provenance, "surface-construction")]);
        return Create(construction, orientation, thickness, material);
    }

    public static PanelResult FromRuled(
        RuledSurfaceIr source,
        PanelOrientation? orientation = null,
        double? thickness = null,
        string? material = null)
    {
        var lowered = RuledSurfaceLowering.Lower(source);
        if (!lowered.IsSuccess) return new(null, lowered.Diagnostics);
        var patch = lowered.Patch!;
        var construction = new PanelConstruction(source.StableId,
            source.Kind == RuledConstructionKind.RuledTransition ? SurfaceConstructionKind.RuledTransition : SurfaceConstructionKind.RuledSurface,
            patch.Domain, patch.ExactSurface, patch.Evaluate,
            RuledPatch(source,patch.Domain,patch.Evaluate,string.Join(";",patch.BoundaryProvenance.Select(item=>item.BoundaryStableId))),
            patch.MaterializationKind, patch.ApproximationCertificate,
            patch.Developability, patch.BoundaryProvenance);
        var curves = new[]
        {
            FromBoundary(source.BoundaryA),
            FromPoints(patch.Evaluate(1, 0), patch.Evaluate(1, 1)),
            Reverse(FromBoundary(source.BoundaryB)),
            FromPoints(patch.Evaluate(0, 1), patch.Evaluate(0, 0))
        };
        var sourceIds = new[] { source.BoundaryA.StableId, source.StableId + ":u-max", source.BoundaryB.StableId, source.StableId + ":u-min" };
        return Create(construction, orientation, thickness, material, curves, sourceIds);
    }

    public static PanelResult FromBoundaryPatch(
        BoundaryPatchIr source,
        PanelOrientation? orientation = null,
        double? thickness = null,
        string? material = null)
    {
        var lowered = BoundaryPatchLowering.Lower(source);
        if (!lowered.IsSuccess) return new(null, lowered.Diagnostics);
        var patch = lowered.Patch!;
        var construction = FromPatch(patch);
        var curves = new[] { FromBoundary(source.South), FromBoundary(source.East), Reverse(FromBoundary(source.North)), Reverse(FromBoundary(source.West)) };
        return Create(construction, orientation, thickness, material, curves,
            [source.South.StableId, source.East.StableId, source.North.StableId, source.West.StableId]);
    }

    public static PanelResult FromSectionSurface(
        SectionSurfaceIr source,
        PanelOrientation? orientation = null,
        double? thickness = null,
        string? material = null)
    {
        var lowered = SectionSurfaceLowering.Lower(source);
        if (!lowered.IsSuccess) return new(null, lowered.Diagnostics);
        return Create(FromPatch(lowered.Patch!), orientation, thickness, material);
    }

    public static PanelResult FromPatch(
        ConstructedSurfacePatch patch,
        PanelOrientation? orientation = null,
        double? thickness = null,
        string? material = null) => Create(FromPatch(patch), orientation, thickness, material);

    private static PanelConstruction FromPatch(ConstructedSurfacePatch patch) => new(
        patch.StableId, patch.ConstructionKind, patch.Domain, patch.Support, patch.Evaluate,
        ProceduralPatch(patch.StableId, patch.Domain, patch.Evaluate, string.Join(";", patch.Provenance.Select(item => item.BoundaryStableId))),
        patch.MaterializationKind, patch.ApproximationCertificate, patch.Developability, patch.Provenance);

    private static BoundedParametricPatch3 ProceduralPatch(string stableId, ParametricDomain domain,
        Func<double, double, Point3D> evaluate, string provenance) =>
        BoundedParametricPatch3.Procedural(stableId, domain, (u, v) =>
        {
            var hu = (domain.U.Maximum - domain.U.Minimum) * 1e-6;
            var hv = (domain.V.Maximum - domain.V.Minimum) * 1e-6;
            var u0 = double.Max(domain.U.Minimum, u - hu); var u1 = double.Min(domain.U.Maximum, u + hu);
            var v0 = double.Max(domain.V.Minimum, v - hv); var v1 = double.Min(domain.V.Maximum, v + hv);
            var point = evaluate(u, v);
            var du = (evaluate(u1, v) - evaluate(u0, v)) * (1d / (u1 - u0));
            var dv = (evaluate(u, v1) - evaluate(u, v0)) * (1d / (v1 - v0));
            var singular = !du.Cross(dv).TryNormalize(out var normal);
            return new(point, du, dv, singular ? null : Direction3D.Create(normal), singular);
        }, provenance);

    private static BoundedParametricPatch3 RuledPatch(RuledSurfaceIr source,ParametricDomain domain,Func<double,double,Point3D> evaluate,string provenance)
    {
        var a=BoundaryCurve(source.BoundaryA);var b=BoundaryCurve(source.BoundaryB);
        CurveJet2 Jet(BoundedParametricCurve3 curve,double u){var jet=curve.EvaluateJet2(curve.Domain.Map(u));var scale=curve.Domain.Length;return jet with{FirstDerivative=jet.FirstDerivative*scale,SecondDerivative=jet.SecondDerivative*(scale*scale)};}
        PatchJet2 Second(double u,double v){var ja=Jet(a,u);var jb=Jet(b,u);var du=ja.FirstDerivative*(1-v)+jb.FirstDerivative*v;var dv=jb.Point-ja.Point;return new(evaluate(u,v),du,dv,ja.SecondDerivative*(1-v)+jb.SecondDerivative*v,jb.FirstDerivative-ja.FirstDerivative,Vector3D.Zero,du.Cross(dv).TryNormalize(out _)?DifferentialSingularityKind.Regular:DifferentialSingularityKind.Singular);}
        return BoundedParametricPatch3.Procedural(source.StableId,domain,(u,v)=>{var jet=Second(u,v);var singular=jet.Singularity!=DifferentialSingularityKind.Regular;return new(jet.Point,jet.Du,jet.Dv,singular?null:Direction3D.Create(jet.Du.Cross(jet.Dv)),singular);},Second,provenance);
    }

    private static BoundedParametricCurve3 BoundaryCurve(RuledBoundary boundary)=>boundary switch
    {
        RuledBoundary.Line line=>BoundedParametricCurve3.LineSegment(line.Id,line.Start,line.End,"ruled-boundary"),
        RuledBoundary.Arc arc=>BoundedParametricCurve3.FromCurveGeometry(arc.Id,CurveGeometry.FromCircle(new(arc.Center,arc.Normal,arc.Radius,arc.ReferenceAxis)),arc.StartAngleRadians,arc.StartAngleRadians+arc.SweepAngleRadians,"ruled-boundary"),
        RuledBoundary.Circle circle=>BoundedParametricCurve3.FromCurveGeometry(circle.Id,CurveGeometry.FromCircle(new(circle.Center,circle.Normal,circle.Radius,circle.ReferenceAxis)),0,2*Math.PI,"ruled-boundary"),
        RuledBoundary.BSpline spline=>BoundedParametricCurve3.FromCurveGeometry(spline.Id,CurveGeometry.FromBSpline(spline.Curve),spline.Curve.DomainStart,spline.Curve.DomainEnd,"ruled-boundary"),
        _=>throw new NotSupportedException($"Ruled boundary '{boundary.GetType().Name}' has no second-jet adapter.")
    };

    private static PanelResult Create(
        PanelConstruction construction,
        PanelOrientation? orientation,
        double? thickness,
        string? material,
        IReadOnlyList<DirectedCurve>? explicitCurves = null,
        IReadOnlyList<string>? sourceIds = null)
    {
        var diagnostics = new List<SurfacingDiagnostic>();
        if (thickness is { } t && (!double.IsFinite(t) || t <= 0))
            diagnostics.Add(new("surfacing-panel-thickness-invalid", "Panel thickness metadata must be finite and positive."));
        if (construction.ParameterDomain is null)
            diagnostics.Add(new("panel-open-boundary", "Panel requires a bounded parameter domain."));

        var curves = explicitCurves ?? ExtractSupportBoundary(construction.Support);
        if (curves.Count != 4)
            diagnostics.Add(new("panel-incomplete-boundary", $"Panel M0 requires four ordered boundary edges; received {curves.Count}."));
        if (diagnostics.Count > 0) return new(null, diagnostics);

        orientation ??= PanelOrientation.Front;
        sourceIds ??= EdgeNames.Select(name => construction.StableId + ":" + name.ToLowerInvariant()).ToArray();
        var edgeDrafts = EdgeNames.Select((name, index) => (name, curve: curves[index], sourceId: sourceIds[index])).ToArray();
        if (!orientation.SameSense)
            edgeDrafts = edgeDrafts.Reverse().Select(item => (item.name, curve: Reverse(item.curve), item.sourceId)).ToArray();

        var edges = edgeDrafts.Select((item, index) =>
        {
            var stableId = $"panel:{construction.StableId}:edge:{item.name.ToLowerInvariant()}";
            var provenance = new[] { new BoundaryProvenance(item.sourceId, construction.StableId, item.name) };
            var binding = new ExactCurveBinding(item.curve.Geometry, item.curve.Start, item.curve.End,
                item.curve.Direction == PanelEdgeDirection.AlongSource, stableId + ":curve");
            var semantic = new SemanticValue(stableId, new("PanelEdge"),
                [new CurveCapability(), new BoundaryEdgeCapability(), new ExactGeometryCapability(), new SelectableCapability()],
                [binding], provenance: [new("panel-boundary", stableId, item.sourceId)], exposedName: item.name);
            return new PanelEdgeIr(stableId, item.name, index, item.curve.Geometry, item.curve.Start, item.curve.End,
                item.curve.Direction, item.sourceId, provenance, semantic);
        }).ToArray();

        ValidateBoundary(construction, edges, diagnostics);
        if (diagnostics.Count > 0) return new(null, diagnostics);

        var cornerPoints = new Dictionary<string, Point3D>(StringComparer.Ordinal)
        {
            ["SW"] = construction.Evaluate(0, 0), ["SE"] = construction.Evaluate(1, 0),
            ["NE"] = construction.Evaluate(1, 1), ["NW"] = construction.Evaluate(0, 1)
        };
        var corners = cornerPoints.ToDictionary(pair => pair.Key, pair =>
        {
            var id = $"panel:{construction.StableId}:corner:{pair.Key.ToLowerInvariant()}";
            var semantic = new SemanticValue(id, new("PanelCorner"), [new PointCapability(), new ExactGeometryCapability(), new SelectableCapability()],
                [new ExactPointBinding(pair.Value.X, pair.Value.Y, pair.Value.Z, id + ":point")],
                provenance: [new("panel-corner", id, pair.Key)], exposedName: pair.Key);
            return new PanelCornerIr(id, pair.Key, pair.Value, semantic);
        }, StringComparer.Ordinal);
        var panelId = "panel:" + construction.StableId;
        var members = edges.Select(edge => edge.SemanticValue).Concat(corners.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value.SemanticValue)).ToArray();
        var root = new SemanticValue(panelId, new("Panel"), exposedMembers: members,
            provenance: construction.Provenance.Select(p => new SemanticProvenance("surface-construction", panelId, $"{p.SourceIdentity}:{p.Role}")));
        var panel = new PanelIr(panelId, construction, edges, corners, orientation, thickness, material, root);
        diagnostics.AddRange(SemanticValueValidator.Validate(root).Select(item => new SurfacingDiagnostic(item.Code, item.Message)));
        return diagnostics.Count == 0 ? new(panel, []) : new(null, diagnostics);
    }

    private static void ValidateBoundary(PanelConstruction construction, IReadOnlyList<PanelEdgeIr> edges, ICollection<SurfacingDiagnostic> diagnostics)
    {
        const double tolerance = 1e-6;
        if (edges.Select(edge => edge.StableId).Distinct(StringComparer.Ordinal).Count() != edges.Count)
            diagnostics.Add(new("panel-duplicate-boundary-edge", "Panel boundary contains a duplicate semantic edge identity."));
        if(edges.Select(edge=>edge.SourceCurveStableId).Distinct(StringComparer.Ordinal).Count()!=edges.Count)
            diagnostics.Add(new("panel-duplicate-boundary-edge","Panel boundary reuses one source curve in multiple semantic edge roles."));
        for (var i = 0; i < edges.Count; i++)
            if ((edges[i].End - edges[(i + 1) % edges.Count].Start).Length > tolerance)
                diagnostics.Add(new("panel-boundary-orientation-inconsistent", $"Boundary edge '{edges[i].Name}' does not end at '{edges[(i + 1) % edges.Count].Name}' within {tolerance:G} mm."));
        foreach(var edge in edges)
        {
            for(var i=0;i<=8;i++)
            {
                var t=i/8d;var expected=edge.Name switch{"South"=>construction.Evaluate(t,0),"North"=>construction.Evaluate(t,1),"West"=>construction.Evaluate(0,t),"East"=>construction.Evaluate(1,t),_=>edge.Evaluate(t)};
                var reversed=edge.Name switch{"South"=>construction.Evaluate(1-t,0),"North"=>construction.Evaluate(1-t,1),"West"=>construction.Evaluate(0,1-t),"East"=>construction.Evaluate(1,1-t),_=>edge.Evaluate(t)};
                if(Math.Min((edge.Evaluate(t)-expected).Length,(edge.Evaluate(t)-reversed).Length)>tolerance)
                {diagnostics.Add(new("panel-surface-boundary-mismatch",$"Boundary edge '{edge.Name}' does not lie on the corresponding support-domain boundary."));break;}
            }
        }
        foreach(var pair in new[]{(edges.Single(edge=>edge.Name=="South"),edges.Single(edge=>edge.Name=="North")),(edges.Single(edge=>edge.Name=="East"),edges.Single(edge=>edge.Name=="West"))})
            if(Enumerable.Range(1,15).Any(i=>Enumerable.Range(1,15).Any(j=>(pair.Item1.Evaluate(i/16d)-pair.Item2.Evaluate(j/16d)).Length<=1e-9)))
                diagnostics.Add(new("panel-self-crossing-boundary",$"Non-adjacent boundary edges '{pair.Item1.Name}' and '{pair.Item2.Name}' intersect in their interiors."));
        var differential = Differential(construction.Evaluate, .5, .5);
        if (differential <= 1e-12)
            diagnostics.Add(new("panel-singular-parametric-boundary", "Panel support is singular at the domain center; a stable orientation cannot be established."));
    }

    private static double Differential(Func<double, double, Point3D> evaluate, double u, double v)
    {
        const double h = 1e-6;
        var du = evaluate(Math.Min(1, u + h), v) - evaluate(Math.Max(0, u - h), v);
        var dv = evaluate(u, Math.Min(1, v + h)) - evaluate(u, Math.Max(0, v - h));
        return du.Cross(dv).Length;
    }

    private static IReadOnlyList<DirectedCurve> ExtractSupportBoundary(SurfaceGeometry support)
    {
        if (support.BSplineSurfaceWithKnots is not { } surface) return [];
        var south = Curve(surface.DegreeU, surface.ControlPoints.Select(row => row[0]).ToArray(), surface.KnotMultiplicitiesU, surface.KnotValuesU);
        var east = Curve(surface.DegreeV, surface.ControlPoints[^1].ToArray(), surface.KnotMultiplicitiesV, surface.KnotValuesV);
        var north = Curve(surface.DegreeU, surface.ControlPoints.Select(row => row[^1]).ToArray(), surface.KnotMultiplicitiesU, surface.KnotValuesU);
        var west = Curve(surface.DegreeV, surface.ControlPoints[0].ToArray(), surface.KnotMultiplicitiesV, surface.KnotValuesV);
        return [Wrap(south), Wrap(east), Reverse(Wrap(north)), Reverse(Wrap(west))];
    }

    private static BSpline3Curve Curve(int degree, IReadOnlyList<Point3D> controls, IReadOnlyList<int> multiplicities, IReadOnlyList<double> knots) =>
        new(degree, controls, multiplicities, knots, "UNSPECIFIED", false, false, "UNSPECIFIED");

    private static DirectedCurve FromBoundary(RuledBoundary boundary) => boundary switch
    {
        RuledBoundary.Line line => FromPoints(line.Start, line.End),
        RuledBoundary.Arc arc => new(CurveGeometry.FromCircle(new Circle3Curve(arc.Center, arc.Normal, arc.Radius, arc.ReferenceAxis)), arc.StartAngleRadians, arc.StartAngleRadians + arc.SweepAngleRadians, PanelEdgeDirection.AlongSource),
        RuledBoundary.Circle circle => new(CurveGeometry.FromCircle(new Circle3Curve(circle.Center, circle.Normal, circle.Radius, circle.ReferenceAxis)), 0, 2 * Math.PI, PanelEdgeDirection.AlongSource),
        RuledBoundary.BSpline spline => Wrap(spline.Curve),
        _ => throw new NotSupportedException($"Unsupported boundary family {boundary.GetType().Name}.")
    };

    private static DirectedCurve FromPoints(Point3D start, Point3D end)
    {
        var vector = end - start; var length = vector.Length;
        if (length <= 1e-12) throw new ArgumentException("Panel boundary edge cannot be degenerate.");
        return new(CurveGeometry.FromLine(new Line3Curve(start, Direction3D.Create(vector))), 0, length, PanelEdgeDirection.AlongSource);
    }

    private static DirectedCurve Wrap(BSpline3Curve curve) => new(CurveGeometry.FromBSpline(curve), curve.DomainStart, curve.DomainEnd, PanelEdgeDirection.AlongSource);
    private static DirectedCurve Reverse(DirectedCurve curve) => new(curve.Geometry, curve.End, curve.Start,
        curve.Direction == PanelEdgeDirection.AlongSource ? PanelEdgeDirection.OppositeSource : PanelEdgeDirection.AlongSource);
    private sealed record DirectedCurve(CurveGeometry Geometry, double Start, double End, PanelEdgeDirection Direction);
}

public sealed record PanelMateRequest(string StableId, PanelEdgeIr A, PanelEdgeIr B,
    PanelContinuity Continuity = PanelContinuity.PositionG0,
    PanelEdgeCorrespondence Correspondence = PanelEdgeCorrespondence.OppositeDirections,
    double Tolerance = 1e-6,
    double AngularToleranceRadians = 1e-6,
    double CurvatureTolerance = 1e-6,
    int SampleCount = 17);
public sealed record PanelMateEvidence(string StableId, string EdgeA, string EdgeB, PanelContinuity Continuity,
    PanelEdgeCorrespondence Correspondence, double EndpointResidual, double G0Residual, string Status,
    PredicateEvidenceKind Evidence=PredicateEvidenceKind.Sampled,double? MaximumAngularResidualRadians=null,
    double? MaximumNormalCurvatureResidual=null,int SampleCount=17,string? Diagnostic=null);
public sealed record PanelNetworkReport(IReadOnlyList<PanelMateEvidence> Mates, IReadOnlyList<string> FreeEdges,
    IReadOnlyList<SurfacingDiagnostic> Diagnostics)
{
    public bool IsSuccess => Diagnostics.Count == 0;
}

/// <summary>Geometry validator consumed by the existing Interface/Mate bridge; it does not place or Boolean-join Panels.</summary>
public static class PanelNetworkValidator
{
    public static PanelNetworkReport Validate(IReadOnlyList<PanelIr> panels, IReadOnlyList<PanelMateRequest> mates)
    {
        var diagnostics = new List<SurfacingDiagnostic>(); var evidence = new List<PanelMateEvidence>();
        var allEdges = panels.SelectMany(panel => panel.BoundaryEdges).ToDictionary(edge => edge.StableId, StringComparer.Ordinal);
        var owners=panels.SelectMany(panel=>panel.BoundaryEdges.Select(edge=>(edge.StableId,Panel:panel))).ToDictionary(item=>item.StableId,item=>item.Panel,StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mate in mates.OrderBy(item => item.StableId, StringComparer.Ordinal))
        {
            if (!allEdges.ContainsKey(mate.A.StableId) || !allEdges.ContainsKey(mate.B.StableId))
            { diagnostics.Add(new("panel-mate-edge-not-in-network", $"Mate '{mate.StableId}' references an edge outside the network.")); continue; }
            foreach (var edge in new[] { mate.A, mate.B })
                if (!used.Add(edge.StableId)) diagnostics.Add(new("panel-mate-edge-already-mated", $"Edge '{edge.StableId}' is used by more than one one-to-one Mate."));
            if (!double.IsFinite(mate.Tolerance) || mate.Tolerance <= 0||!double.IsFinite(mate.AngularToleranceRadians)||mate.AngularToleranceRadians<=0||!double.IsFinite(mate.CurvatureTolerance)||mate.CurvatureTolerance<=0||mate.SampleCount<2)
            { diagnostics.Add(new("panel-mate-tolerance-invalid", $"Mate '{mate.StableId}' tolerance must be finite and positive.")); continue; }
            var b0 = mate.Correspondence == PanelEdgeCorrespondence.OppositeDirections ? mate.B.End : mate.B.Start;
            var b1 = mate.Correspondence == PanelEdgeCorrespondence.OppositeDirections ? mate.B.Start : mate.B.End;
            var endpoint = Math.Max((mate.A.Start - b0).Length, (mate.A.End - b1).Length);
            var residual = 0d;
            var maxAngle=0d;var maxCurvature=0d;string? unknown=null;
            var panelA=owners[mate.A.StableId];var panelB=owners[mate.B.StableId];
            for (var i = 0; i < mate.SampleCount; i++)
            {
                var t = i / (double)(mate.SampleCount-1); var paired = mate.Correspondence == PanelEdgeCorrespondence.OppositeDirections ? 1 - t : t;
                residual = Math.Max(residual, (mate.A.Evaluate(t) - mate.B.Evaluate(paired)).Length);
                if(mate.Continuity==PanelContinuity.PositionG0)continue;
                var uvA=BoundaryParameter(panelA,mate.A,t);var uvB=BoundaryParameter(panelB,mate.B,paired);
                var firstA=panelA.AuthoredPatch.EvaluateJet1(uvA.U,uvA.V);var firstB=panelB.AuthoredPatch.EvaluateJet1(uvB.U,uvB.V);
                if(firstA.IsSingular||firstB.IsSingular||firstA.Normal is null||firstB.Normal is null){unknown="A seam tangent plane is singular.";continue;}
                var na=OrientedNormal(panelA,firstA.Normal.Value.ToVector());var nb=OrientedNormal(panelB,firstB.Normal.Value.ToVector());
                var dot=double.Clamp(na.Dot(nb),-1d,1d);maxAngle=Math.Max(maxAngle,double.Acos(double.Abs(dot)));
                if(mate.Continuity!=PanelContinuity.CurvatureG2)continue;
                if(!panelA.AuthoredPatch.SupportsSecondJet||!panelB.AuthoredPatch.SupportsSecondJet){unknown="Second-jet capability is unavailable on one or both Panel supports.";continue;}
                var tangent=mate.A.AuthoredCurve.EvaluateJet1(mate.A.AuthoredCurve.Domain.Map(t)).Derivative;
                if(!tangent.TryNormalize(out tangent)){unknown="The seam tangent is singular.";continue;}
                var transverseA=na.Cross(tangent);var transverseB=nb.Cross(tangent);
                var ka=CurvatureQuery.NormalCurvature(panelA.AuthoredPatch,uvA.U,uvA.V,transverseA);
                var kb=CurvatureQuery.NormalCurvature(panelB.AuthoredPatch,uvB.U,uvB.V,transverseB);
                if(ka.Status!=DifferentialQueryStatus.Available||kb.Status!=DifferentialQueryStatus.Available){unknown=ka.Diagnostic??kb.Diagnostic??"Normal curvature is unavailable.";continue;}
                var orientedKa=ka.Curvature!.Value*(panelA.Orientation.SameSense?1d:-1d);
                var orientedKb=kb.Curvature!.Value*(panelB.Orientation.SameSense?1d:-1d)*(dot>=0?1d:-1d);
                maxCurvature=Math.Max(maxCurvature,double.Abs(orientedKa-orientedKb));
            }
            if (endpoint > mate.Tolerance) diagnostics.Add(new("panel-mate-endpoint-mismatch", $"Mate '{mate.StableId}' endpoint residual {endpoint:G6} mm exceeds {mate.Tolerance:G6} mm."));
            if (residual > mate.Tolerance) diagnostics.Add(new("panel-mate-g0-failure", $"Mate '{mate.StableId}' G0 residual {residual:G6} mm exceeds {mate.Tolerance:G6} mm."));
            var g0=endpoint<=mate.Tolerance&&residual<=mate.Tolerance;var g1=maxAngle<=mate.AngularToleranceRadians;
            if(mate.Continuity!=PanelContinuity.PositionG0&&unknown is null&&g0&&!g1)diagnostics.Add(new("panel-mate-g1-failure",$"Mate '{mate.StableId}' tangent-plane angular residual {maxAngle:G6} rad exceeds {mate.AngularToleranceRadians:G6} rad."));
            if(mate.Continuity==PanelContinuity.CurvatureG2&&unknown is null&&g0&&g1&&maxCurvature>mate.CurvatureTolerance)diagnostics.Add(new("panel-mate-g2-failure",$"Mate '{mate.StableId}' transverse normal-curvature residual {maxCurvature:G6} exceeds {mate.CurvatureTolerance:G6}."));
            if(unknown is not null&&mate.Continuity!=PanelContinuity.PositionG0)diagnostics.Add(new(mate.Continuity==PanelContinuity.CurvatureG2?"panel-mate-g2-unknown":"panel-mate-g1-unknown",$"Mate '{mate.StableId}' continuity is Unknown: {unknown}"));
            var pass=g0&&(mate.Continuity==PanelContinuity.PositionG0||(unknown is null&&g1&&(mate.Continuity==PanelContinuity.TangentG1||maxCurvature<=mate.CurvatureTolerance)));
            evidence.Add(new(mate.StableId,mate.A.StableId,mate.B.StableId,mate.Continuity,mate.Correspondence,endpoint,residual,unknown is null?(pass?"valid":"invalid"):"unknown",PredicateEvidenceKind.Sampled,mate.Continuity==PanelContinuity.PositionG0?null:maxAngle,mate.Continuity==PanelContinuity.CurvatureG2?maxCurvature:null,mate.SampleCount,unknown));
        }
        var free = allEdges.Keys.Where(id => !used.Contains(id)).Order(StringComparer.Ordinal).ToArray();
        return new(evidence, free, diagnostics);
    }

    private static Vector3D OrientedNormal(PanelIr panel,Vector3D normal)=>panel.Orientation.SameSense?normal:-normal;
    private static (double U,double V) BoundaryParameter(PanelIr panel,PanelEdgeIr edge,double normalized)
    {
        var domain=panel.AuthoredPatch.Domain;
        (double U,double V) Map(double t)=>edge.Name switch{"South"=>(domain.U.Map(t),domain.V.Minimum),"East"=>(domain.U.Maximum,domain.V.Map(t)),"North"=>(domain.U.Map(t),domain.V.Maximum),"West"=>(domain.U.Minimum,domain.V.Map(t)),_=>(domain.U.Map(t),domain.V.Minimum)};
        var direct=Map(normalized);var reverse=Map(1d-normalized);var point=edge.Evaluate(normalized);
        return (panel.AuthoredPatch.EvaluatePoint(direct.U,direct.V)-point).Length<=(panel.AuthoredPatch.EvaluatePoint(reverse.U,reverse.V)-point).Length?direct:reverse;
    }
}
