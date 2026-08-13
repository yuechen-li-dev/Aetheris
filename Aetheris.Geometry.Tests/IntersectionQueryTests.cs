using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheris.Geometry;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Surfacing;
using Xunit;

namespace Aetheris.Geometry.Tests;

public sealed class IntersectionQueryTests
{
    private static readonly Plane3 Z0 = new(Point3D.Origin, Direction3D.Create(new(0, 0, 1)));

    [Fact]
    public void Curve_plane_reports_certified_disjoint_crossing_and_structural_zero()
    {
        var disjoint = IntersectionQuery.Between(Line("above", new(-1, 0, 2), new(1, 0, 2)), Z0);
        var crossing = IntersectionQuery.Between(Line("crossing", new(0, 0, -1), new(0, 0, 1)), Z0);
        var zero = IntersectionQuery.Between(Line("zero", new(-1, 0, 0), new(1, 0, 0)), Z0);

        Assert.Equal((IntersectionRelation.Disjoint, PredicateEvidenceKind.Certified), (disjoint.Relation, disjoint.Evidence));
        Assert.Equal((IntersectionRelation.Crossing, PredicateEvidenceKind.Certified), (crossing.Relation, crossing.Evidence));
        Assert.Equal((IntersectionRelation.Coincident, PredicateEvidenceKind.Structural), (zero.Relation, zero.Evidence));
        Assert.True(disjoint.IsDefinitelyDisjoint); Assert.Single(crossing.WitnessPoints);
    }

    [Fact]
    public void Curve_plane_tangent_needs_global_one_sided_and_second_order_evidence()
    {
        var t = CurveExpression.T; var square = CurveExpression.Multiply(CurveExpression.Length(1), CurveExpression.Power(t, 2));
        var parabola = new BoundedParametricCurve3("parabola", new(-1, 1),
            new(CurveExpression.Multiply(CurveExpression.Length(1), t), CurveExpression.Length(0), square), "fixture");
        var result = IntersectionQuery.Between(parabola, Z0, new() { LinearTolerance = 1e-9 });
        Assert.Equal(IntersectionRelation.Touching, result.Relation); Assert.Equal(PredicateEvidenceKind.ToleranceBounded, result.Evidence);
        Assert.Equal(IntersectionLocalRelation.TouchingCandidate, Assert.Single(result.WitnessPoints).LocalRelation);
        Assert.True(result.WitnessPoints[0].Contact!.HasSecondJet);

        var localOnly = BoundedParametricCurve3.Procedural("local-only", new(-1, 1), x =>
            (new(x, 0, x * x), new Vector3D(1, 0, 2 * x)), "fixture");
        var unknown = IntersectionQuery.Between(localOnly, Z0);
        Assert.Equal(IntersectionRelation.Unknown, unknown.Relation);
        Assert.Contains(unknown.Diagnostics, d => d.Code == GeometryQueryDiagnosticCode.InsufficientSecondJetEvidence);
    }

    [Fact]
    public void Patch_plane_reuses_signed_side_and_is_conservative_about_tangency()
    {
        var above = Graph("above", SurfaceExpression.Length(2));
        var saddle = Graph("saddle", SurfaceExpression.Multiply(SurfaceExpression.Length(1), SurfaceExpression.U));
        var squared = SurfaceExpression.Add(
            SurfaceExpression.Multiply(SurfaceExpression.Length(1), SurfaceExpression.Power(SurfaceExpression.U, 2)),
            SurfaceExpression.Multiply(SurfaceExpression.Length(1), SurfaceExpression.Power(SurfaceExpression.V, 2)));
        var tangent = Graph("paraboloid", squared);
        Assert.Equal(IntersectionRelation.Disjoint, IntersectionQuery.Between(above, Z0).Relation);
        Assert.Equal(IntersectionRelation.Crossing, IntersectionQuery.Between(saddle, Z0).Relation);
        var contact = IntersectionQuery.Between(tangent, Z0);
        Assert.Equal(IntersectionRelation.Touching, contact.Relation); Assert.Equal(PredicateEvidenceKind.ToleranceBounded, contact.Evidence);

        var sampled = IntersectionQuery.Between(above, Z0, new() { EvidencePreference = IntersectionEvidencePreference.AllowSampled });
        Assert.Equal(PredicateEvidenceKind.Sampled, sampled.Evidence); Assert.NotEqual(PredicateEvidenceKind.Certified, sampled.Evidence);
    }

    [Fact]
    public void Curve_patch_uses_distance_gate_then_local_transversality()
    {
        var patch = PlanePatch("panel", 0);
        var separated = IntersectionQuery.Between(Line("gap", new(-.5, 0, 2), new(.5, 0, 2)), patch);
        var crossing = IntersectionQuery.Between(Line("pierce", new(0, 0, -1), new(0, 0, 1)), patch);
        var tangent = IntersectionQuery.Between(Line("tangent", new(-.5, 0, 0), new(.5, 0, 0)), patch);
        Assert.Equal(IntersectionRelation.Disjoint, separated.Relation);
        Assert.Equal(IntersectionRelation.Crossing, crossing.Relation);
        Assert.Equal(IntersectionRelation.Unknown, tangent.Relation);
        Assert.Equal(IntersectionLocalRelation.TouchingCandidate, Assert.Single(tangent.WitnessPoints).LocalRelation);
        Assert.False(tangent.WitnessesAreAuthoritativeTrims);
    }

    [Fact]
    public void Curve_patch_budget_exhaustion_is_typed_unknown()
    {
        var result = IntersectionQuery.Between(Line("curve", new(0, 0, -1), new(0, 0, 1)), PlanePatch("patch", 0), new() { SubdivisionBudget = 16 });
        Assert.Equal(IntersectionRelation.Unknown, result.Relation); Assert.True(result.Statistics.BudgetExhausted);
        Assert.Contains(result.Diagnostics, d => d.Code == GeometryQueryDiagnosticCode.SubdivisionBudgetExhausted);
    }

    [Fact]
    public void Patch_patch_distinguishes_gap_transverse_contact_and_identity()
    {
        var a = PlanePatch("a", 0); var gap = PlanePatch("gap", 2);
        var transverse = VerticalPatch("vertical"); var tangent = PlanePatch("tangent", 0);
        var sameIdentity = PlanePatch("a", 0);
        Assert.Equal(IntersectionRelation.Disjoint, IntersectionQuery.Between(a, gap).Relation);
        var transverseResult = IntersectionQuery.Between(a, transverse);
        Assert.Equal(IntersectionRelation.Crossing, transverseResult.Relation);
        Assert.True(transverseResult.WitnessPoints.Count > 1);
        var tangentResult = IntersectionQuery.Between(a, tangent);
        Assert.Equal(IntersectionRelation.Unknown, tangentResult.Relation);
        Assert.NotEmpty(tangentResult.WitnessPoints);
        Assert.All(tangentResult.WitnessPoints, witness => Assert.Equal(IntersectionLocalRelation.TouchingCandidate, witness.LocalRelation));
        var coincidence = IntersectionQuery.Between(a, sameIdentity);
        Assert.Equal((IntersectionRelation.Coincident, PredicateEvidenceKind.Structural), (coincidence.Relation, coincidence.Evidence));
    }

    [Fact]
    public void Numerical_near_zero_never_becomes_structural_overlap_or_a_bool_only_claim()
    {
        var a = PlanePatch("a", 0); var near = PlanePatch("near", 5e-7);
        var result = IntersectionQuery.Between(a, near);
        Assert.NotEqual(IntersectionRelation.Coincident, result.Relation);
        Assert.NotEqual(IntersectionRelation.Overlapping, result.Relation);
        Assert.Equal(IntersectionRelation.Unknown, result.Relation);
        Assert.False(result.IsDefinitelyDisjoint);

        var nearLine = IntersectionQuery.Between(Line("near-line", new(-1, 0, 5e-7), new(1, 0, 5e-7)), Z0);
        Assert.NotEqual(IntersectionRelation.Coincident, nearLine.Relation);
        Assert.NotEqual(PredicateEvidenceKind.Structural, nearLine.Evidence);

        var subdomain = BoundedParametricPatch3.Procedural("a", new(new(-.5, .5), new(-.5, .5)),
            (u, v) => new(new(u, v, 0), new(1, 0, 0), new(0, 1, 0), Direction3D.Create(new(0, 0, 1)), false), "fixture");
        var sharedSupportDifferentDomain = IntersectionQuery.Between(a, subdomain);
        Assert.Equal(IntersectionRelation.Unknown, sharedSupportDifferentDomain.Relation);
        Assert.Contains(sharedSupportDifferentDomain.Diagnostics, d => d.Code == GeometryQueryDiagnosticCode.AmbiguousOverlap);
    }

    [Fact]
    public void Panel_plane_and_panel_panel_dogfood_use_authored_patches()
    {
        var surface = new ParametricSurfaceIr("panel", SurfaceConstructionKind.ParametricSurface,
            new(new(-1, 1), new(-1, 1)), GraphExpression(SurfaceExpression.Length(3)), "cad:panel");
        var panel = PanelFactory.FromParametric(surface).Panel!;
        var tooling = IntersectionQuery.Between(panel.AuthoredPatch, Z0);
        var other = PanelFactory.FromParametric(new ParametricSurfaceIr("panel-gap", SurfaceConstructionKind.ParametricSurface,
            new(new(-1, 1), new(-1, 1)), GraphExpression(SurfaceExpression.Length(5)), "cad:panel-gap")).Panel!;
        var gap = IntersectionQuery.Between(panel.AuthoredPatch, other.AuthoredPatch);
        Assert.Equal(IntersectionRelation.Disjoint, tooling.Relation); Assert.Equal(IntersectionRelation.Disjoint, gap.Relation);
        Assert.Equal("cad:panel", tooling.OperandA.Provenance!.Source);
    }

    [Fact]
    public void Generic_query_cannot_mutate_or_materialize_brep_topology()
    {
        var body = BrepPrimitives.CreateBox(4, 6, 8).Value;
        var before = (Vertices: body.Topology.Vertices.Count(), Edges: body.Topology.Edges.Count(), Faces: body.Topology.Faces.Count(), Curves: body.Geometry.Curves.Count(), Surfaces: body.Geometry.Surfaces.Count());
        var result = IntersectionQuery.Between(Line("query", new(0, 0, -1), new(0, 0, 1)), PlanePatch("patch", 0));
        var after = (Vertices: body.Topology.Vertices.Count(), Edges: body.Topology.Edges.Count(), Faces: body.Topology.Faces.Count(), Curves: body.Geometry.Curves.Count(), Surfaces: body.Geometry.Surfaces.Count());
        Assert.Equal(before, after); Assert.Equal(IntersectionRelation.Crossing, result.Relation);
        Assert.False(result.WitnessesAreAuthoritativeTrims);
        Assert.DoesNotContain(result.GetType().GetProperties(), p => p.PropertyType == typeof(BrepBody));
    }

    [Fact]
    public void Constructive_cone_plane_hyperbola_remains_a_separate_materializer()
    {
        var cone = new ConeSurface(Point3D.Origin, Direction3D.Create(new(1, 0, 0)), double.Pi / 4, Direction3D.Create(new(0, 0, 1)));
        var construction = TransverseConePlaneIntersection.IntersectWorldZ(cone, 2);
        Assert.True(construction.IsSuccess); Assert.Equal("Hyperbola3Curve", construction.Value.GetType().Name);
    }

    [Fact]
    public void Results_and_witness_order_are_deterministic()
    {
        var a = PlanePatch("horizontal", 0); var b = VerticalPatch("vertical");
        var first = JsonSerializer.Serialize(IntersectionQuery.Between(a, b));
        var second = JsonSerializer.Serialize(IntersectionQuery.Between(a, b));
        Assert.Equal(first, second);
        var hash1 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(first)));
        var hash2 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(second)));
        Assert.Equal(hash1, hash2);
    }

    private static BoundedParametricCurve3 Line(string id, Point3D a, Point3D b) => BoundedParametricCurve3.LineSegment(id, a, b, "fixture");
    private static BoundedParametricPatch3 Graph(string id, SurfaceScalarExpression z) => new(id, new(new(-1, 1), new(-1, 1)), GraphExpression(z), "fixture:" + id);
    private static SurfacePointExpression GraphExpression(SurfaceScalarExpression z) => new(
        SurfaceExpression.Multiply(SurfaceExpression.Length(1), SurfaceExpression.U),
        SurfaceExpression.Multiply(SurfaceExpression.Length(1), SurfaceExpression.V), z);
    private static BoundedParametricPatch3 PlanePatch(string id, double z) => BoundedParametricPatch3.Procedural(id,
        new(new(-1, 1), new(-1, 1)), (u, v) => new(new(u, v, z), new(1, 0, 0), new(0, 1, 0), Direction3D.Create(new(0, 0, 1)), false),
        (u, v) => new(new(u, v, z), new(1, 0, 0), new(0, 1, 0), new(0, 0, 0), new(0, 0, 0), new(0, 0, 0), DifferentialSingularityKind.Regular), "fixture");
    private static BoundedParametricPatch3 VerticalPatch(string id) => BoundedParametricPatch3.Procedural(id,
        new(new(-1, 1), new(-1, 1)), (u, v) => new(new(u, 0, v), new(1, 0, 0), new(0, 0, 1), Direction3D.Create(new(0, -1, 0)), false), "fixture");
}
