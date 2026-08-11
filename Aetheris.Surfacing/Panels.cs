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
public enum PanelContinuity { PositionG0, TangentG1 }
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
    public Point3D Start => Evaluate(0);
    public Point3D End => Evaluate(1);
    public Point3D Evaluate(double normalized)
    {
        var parameter = ParameterStart + Math.Clamp(normalized, 0, 1) * (ParameterEnd - ParameterStart);
        return Curve.Kind switch
        {
            CurveGeometryKind.Line3 => Curve.Line3!.Value.Evaluate(parameter),
            CurveGeometryKind.Circle3 => Curve.Circle3!.Value.Evaluate(parameter),
            CurveGeometryKind.BSpline3 => Curve.BSpline3!.Value.Evaluate(parameter),
            _ => throw new NotSupportedException($"Panel edge curve family '{Curve.Kind}' cannot be evaluated.")
        };
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
            patch.Domain, patch.ExactSurface, patch.Evaluate, patch.MaterializationKind, patch.ApproximationCertificate,
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
        patch.MaterializationKind, patch.ApproximationCertificate, patch.Developability, patch.Provenance);

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
    double Tolerance = 1e-6);
public sealed record PanelMateEvidence(string StableId, string EdgeA, string EdgeB, PanelContinuity Continuity,
    PanelEdgeCorrespondence Correspondence, double EndpointResidual, double G0Residual, string Status);
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
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mate in mates.OrderBy(item => item.StableId, StringComparer.Ordinal))
        {
            if (!allEdges.ContainsKey(mate.A.StableId) || !allEdges.ContainsKey(mate.B.StableId))
            { diagnostics.Add(new("panel-mate-edge-not-in-network", $"Mate '{mate.StableId}' references an edge outside the network.")); continue; }
            foreach (var edge in new[] { mate.A, mate.B })
                if (!used.Add(edge.StableId)) diagnostics.Add(new("panel-mate-edge-already-mated", $"Edge '{edge.StableId}' is used by more than one one-to-one Mate."));
            if (!double.IsFinite(mate.Tolerance) || mate.Tolerance <= 0)
            { diagnostics.Add(new("panel-mate-tolerance-invalid", $"Mate '{mate.StableId}' tolerance must be finite and positive.")); continue; }
            var b0 = mate.Correspondence == PanelEdgeCorrespondence.OppositeDirections ? mate.B.End : mate.B.Start;
            var b1 = mate.Correspondence == PanelEdgeCorrespondence.OppositeDirections ? mate.B.Start : mate.B.End;
            var endpoint = Math.Max((mate.A.Start - b0).Length, (mate.A.End - b1).Length);
            var residual = 0d;
            for (var i = 0; i <= 16; i++)
            {
                var t = i / 16d; var paired = mate.Correspondence == PanelEdgeCorrespondence.OppositeDirections ? 1 - t : t;
                residual = Math.Max(residual, (mate.A.Evaluate(t) - mate.B.Evaluate(paired)).Length);
            }
            if (endpoint > mate.Tolerance) diagnostics.Add(new("panel-mate-endpoint-mismatch", $"Mate '{mate.StableId}' endpoint residual {endpoint:G6} mm exceeds {mate.Tolerance:G6} mm."));
            if (residual > mate.Tolerance) diagnostics.Add(new("panel-mate-g0-failure", $"Mate '{mate.StableId}' G0 residual {residual:G6} mm exceeds {mate.Tolerance:G6} mm."));
            if (mate.Continuity == PanelContinuity.TangentG1)
                diagnostics.Add(new("panel-mate-g1-unsupported", $"Mate '{mate.StableId}' requests G1, but Panel M0 only verifies exact G0 position continuity."));
            evidence.Add(new(mate.StableId, mate.A.StableId, mate.B.StableId, mate.Continuity, mate.Correspondence, endpoint, residual,
                endpoint <= mate.Tolerance && residual <= mate.Tolerance && mate.Continuity == PanelContinuity.PositionG0 ? "valid" : "invalid"));
        }
        var free = allEdges.Keys.Where(id => !used.Contains(id)).Order(StringComparer.Ordinal).ToArray();
        return new(evidence, free, diagnostics);
    }
}
