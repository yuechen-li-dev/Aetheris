using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheris.Geometry;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;
using Xunit;

namespace Aetheris.Geometry.Tests;

public sealed class ContactQueryTests
{
    private static readonly Plane3 Z0 = new(Point3D.Origin, Direction3D.Create(new(0, 0, 1)));

    [Fact]
    public void Curve_plane_scalar_calibration_is_exact_only_through_available_evidence()
    {
        var linear = ContactQuery.Between(CurveGraph("t", CurveExpression.T), Z0);
        Assert.Equal(ContactClassification.Transverse, linear.Classification);
        Assert.True(linear.ContactExists);
        Assert.Equal((ContactOrderStatus.Exact, 1), (linear.OrderEvidence.Status, linear.OrderEvidence.Order));

        var quadratic = ContactQuery.Between(CurveGraph("t2", CurveExpression.Power(CurveExpression.T, 2)), Z0);
        Assert.Equal(ContactClassification.Tangent, quadratic.Classification);
        Assert.Equal((ContactOrderStatus.Exact, 2), (quadratic.OrderEvidence.Status, quadratic.OrderEvidence.Order));
        Assert.Equal(ContactDerivativeRelation.ZeroWithinTolerance, quadratic.Witnesses[0].Derivatives.Single(x => x.Order == 1).Relation);

        var quartic = ContactQuery.Between(CurveGraph("t4", CurveExpression.Power(CurveExpression.T, 4)), Z0);
        Assert.Equal(ContactClassification.HigherOrderCandidate, quartic.Classification);
        Assert.Equal(ContactOrderStatus.AtLeast, quartic.OrderEvidence.Status);
        Assert.Equal(2, quartic.OrderEvidence.ProvenLowerBound);
        Assert.Null(quartic.OrderEvidence.Order);
        Assert.Equal(2, quartic.OrderEvidence.MaximumDerivativeOrderChecked);
        Assert.Contains("neither proves order 3/4", quartic.OrderEvidence.Diagnostic);
    }

    [Fact]
    public void Curve_plane_structural_coincidence_has_no_finite_order_and_singular_contact_is_unknown()
    {
        var coincident = ContactQuery.Between(BoundedParametricCurve3.LineSegment("in-plane", new(-1, 0, 0), new(1, 0, 0), "fixture"), Z0);
        Assert.Equal(ContactClassification.Coincident, coincident.Classification);
        Assert.True(coincident.ContactExists);
        Assert.Equal(ContactEvidenceScope.Structural, coincident.Scope);
        Assert.Equal(PredicateEvidenceKind.Structural, coincident.Evidence);
        Assert.Equal(ContactOrderStatus.Unknown, coincident.OrderEvidence.Status);
        Assert.Contains("no finite", coincident.OrderEvidence.Diagnostic);

        var singular = BoundedParametricCurve3.Procedural("singular", new(-1, 1), t => (new(t * t * t, 0, t * t * t * t), new Vector3D(3 * t * t, 0, 4 * t * t * t)), "fixture");
        var result = ContactQuery.Between(singular, Z0);
        Assert.Equal(ContactClassification.Unknown, result.Classification);
        Assert.Null(result.ContactExists);
        Assert.Contains("singular", result.OrderEvidence.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Derivative_zero_is_explicitly_tolerance_qualified_and_parameterization_invariant()
    {
        var near = CurveGraph("near", CurveExpression.Add(CurveExpression.Power(CurveExpression.T, 2),
            CurveExpression.Multiply(CurveExpression.Number(1e-12), CurveExpression.T)));
        var result = ContactQuery.Between(near, Z0, new() { AngularTolerance = 1e-9, LinearTolerance = 1e-8 });
        Assert.False(result.OrderEvidence.Status == ContactOrderStatus.Exact && result.OrderEvidence.Order == 1);
        Assert.Equal(ContactDerivativeRelation.ZeroWithinTolerance, result.Witnesses[0].Derivatives.Single(x => x.Order == 1).Relation);

        var t = CurveExpression.T;
        var reversed = new BoundedParametricCurve3("t2-reversed", new(-1, 1), new(
            CurveExpression.Multiply(CurveExpression.Length(-2), t), CurveExpression.Length(0),
            CurveExpression.Multiply(CurveExpression.Length(1), CurveExpression.Power(t, 2))), "fixture");
        var ordinary = ContactQuery.Between(CurveGraph("t2-ordinary", CurveExpression.Power(t, 2)), Z0);
        var scaled = ContactQuery.Between(reversed, Z0);
        Assert.Equal(ordinary.Classification, scaled.Classification);
        Assert.Equal(ordinary.OrderEvidence.Status, scaled.OrderEvidence.Status);
        Assert.Equal(ordinary.OrderEvidence.Order, scaled.OrderEvidence.Order);
    }

    [Fact]
    public void Patch_plane_uses_whole_domain_side_before_local_derivatives()
    {
        var paraboloid = Graph("paraboloid", SurfaceExpression.Add(PowerU(2), PowerV(2)));
        var tangent = ContactQuery.Between(paraboloid, Z0);
        Assert.Equal(ContactClassification.Tangent, tangent.Classification);
        Assert.Equal(SignedSideClassification.Unknown, tangent.SideRelation);
        Assert.Equal(ContactCurvatureRelation.Separating, tangent.CurvatureRelation);
        Assert.Equal(3, tangent.Witnesses[0].DirectionalObservations.Count);

        var saddle = Graph("saddle", SurfaceExpression.Subtract(PowerU(2), PowerV(2)));
        var crossing = ContactQuery.Between(saddle, Z0);
        Assert.Equal(ContactClassification.Crossing, crossing.Classification);
        Assert.Equal(ContactEvidenceScope.WholeDomain, crossing.Scope);
        Assert.Equal(SignedSideClassification.Crossing, crossing.SideRelation);
        Assert.NotEmpty(crossing.Witnesses);
        Assert.Equal(0, crossing.Witnesses[0].Point.Z, 12);
    }

    [Fact]
    public void Patch_plane_reduces_classification_when_second_jet_is_unavailable()
    {
        var firstOnly = BoundedParametricPatch3.Procedural("first-only", Domain(), (u, v) =>
        {
            var du = new Vector3D(1, 0, 2 * u); var dv = new Vector3D(0, 1, 2 * v);
            return new(new(u, v, u * u + v * v), du, dv, Direction3D.Create(du.Cross(dv)), false);
        }, "fixture");
        var result = ContactQuery.Between(firstOnly, Z0);
        Assert.Equal(ContactClassification.Tangent, result.Classification);
        Assert.Equal(ContactCurvatureRelation.Unavailable, result.CurvatureRelation);
        Assert.Equal(ContactOrderStatus.Unknown, result.OrderEvidence.Status);
    }

    [Fact]
    public void Curve_patch_distinguishes_transverse_tangent_second_order_and_unknown()
    {
        var plane = PlanePatch("plane", Domain());
        var transverse = ContactQuery.Between(BoundedParametricCurve3.LineSegment("pierce", new(0, 0, -1), new(0, 0, 1), "fixture"), plane);
        Assert.Equal(ContactClassification.Transverse, transverse.Classification);

        var tangent = ContactQuery.Between(CurveGraph("curve-bowl", CurveExpression.Power(CurveExpression.T, 2)), plane);
        Assert.Equal(ContactClassification.Tangent, tangent.Classification);
        Assert.Equal(ContactCurvatureRelation.Separating, tangent.CurvatureRelation);
        Assert.Equal(IntersectionRelation.Unknown, tangent.IntersectionRelation);
        Assert.Equal(ContactEvidenceScope.Local, tangent.Scope);

        var compatible = ContactQuery.Between(BoundedParametricCurve3.LineSegment("in-plane-distinct", new(-1, 0, 0), new(1, 0, 0), "fixture"), plane);
        Assert.Equal(ContactClassification.SecondOrderCompatible, compatible.Classification);
        Assert.NotEqual(ContactClassification.Coincident, compatible.Classification);

        var singular = BoundedParametricCurve3.Procedural("singular-cp", new(-1, 1), _ => (Point3D.Origin, Vector3D.Zero), "fixture");
        Assert.Equal(ContactClassification.Unknown, ContactQuery.Between(singular, plane).Classification);
    }

    [Fact]
    public void Patch_patch_uses_geometric_directional_curvature_and_never_integer_order()
    {
        var horizontal = PlanePatch("horizontal", Domain());
        var vertical = VerticalPatch("vertical");
        Assert.Equal(ContactClassification.Transverse, ContactQuery.Between(horizontal, vertical).Classification);

        var compatible = ContactQuery.Between(horizontal, PlanePatch("flat-distinct", Domain()));
        Assert.Equal(ContactClassification.SecondOrderCompatible, compatible.Classification);
        Assert.Equal(3, compatible.Witnesses[0].DirectionalObservations.Count);
        Assert.Equal(ContactOrderStatus.Unknown, compatible.OrderEvidence.Status);
        Assert.NotEqual(ContactClassification.Coincident, compatible.Classification);

        var bowl = Graph("bowl", SurfaceExpression.Add(PowerU(2), PowerV(2)));
        var incompatible = ContactQuery.Between(horizontal, bowl);
        Assert.Equal(ContactClassification.Tangent, incompatible.Classification);
        Assert.Equal(ContactCurvatureRelation.Separating, incompatible.CurvatureRelation);

        var cylinder = Graph("cylinder", PowerU(2));
        var directional = ContactQuery.Between(horizontal, cylinder);
        Assert.Equal(ContactClassification.Tangent, directional.Classification);
        Assert.Equal(ContactCurvatureRelation.DirectionDependent, directional.CurvatureRelation);
        Assert.Contains(directional.Witnesses[0].DirectionalObservations, x => x.Relation == ContactCurvatureRelation.Compatible);
        Assert.Contains(directional.Witnesses[0].DirectionalObservations, x => x.Relation == ContactCurvatureRelation.Separating);

        var structural = ContactQuery.Between(horizontal, PlanePatch("horizontal", Domain()));
        Assert.Equal(ContactClassification.Coincident, structural.Classification);
        Assert.Equal(ContactEvidenceScope.Structural, structural.Scope);
    }

    [Fact]
    public void Patch_patch_classification_is_invariant_to_scaling_reversal_and_umbilics_need_no_principal_direction()
    {
        var a = Graph("base-plane", SurfaceExpression.Length(0));
        var reparameterized = new BoundedParametricPatch3("reparameterized-plane", Domain(), new(
            SurfaceExpression.Multiply(SurfaceExpression.Length(-2), SurfaceExpression.U),
            SurfaceExpression.Multiply(SurfaceExpression.Length(3), SurfaceExpression.V), SurfaceExpression.Length(0)), "fixture");
        var result = ContactQuery.Between(a, reparameterized);
        Assert.Equal(ContactClassification.SecondOrderCompatible, result.Classification);
        Assert.All(result.Witnesses[0].DirectionalObservations, x => Assert.Equal(ContactCurvatureRelation.Compatible, x.Relation));
    }

    [Fact]
    public void Equal_principal_curvature_values_alone_do_not_establish_second_order_identity()
    {
        var xCylinder = Graph("x-cylinder", PowerU(2));
        var yCylinder = Graph("y-cylinder", PowerV(2));
        var ka = CurvatureQuery.Patch(xCylinder, 0, 0);
        var kb = CurvatureQuery.Patch(yCylinder, 0, 0);
        Assert.Equal(ka.K1!.Value, kb.K1!.Value, 10);
        Assert.Equal(ka.K2!.Value, kb.K2!.Value, 10);
        var contact = ContactQuery.Between(xCylinder, yCylinder);
        Assert.NotEqual(ContactClassification.SecondOrderCompatible, contact.Classification);
        Assert.NotEqual(ContactClassification.Coincident, contact.Classification);
    }

    [Fact]
    public void Same_support_identity_with_different_domains_remains_unknown()
    {
        var full = PlanePatch("shared-support", Domain());
        var subset = PlanePatch("shared-support", new(new(-.5, .5), new(-.5, .5)));
        var result = ContactQuery.Between(full, subset);
        Assert.Equal(ContactClassification.Unknown, result.Classification);
        Assert.Null(result.ContactExists);
        Assert.Equal(ContactOrderStatus.Unknown, result.OrderEvidence.Status);
        Assert.Contains(result.Diagnostics, x => x.Code == GeometryQueryDiagnosticCode.AmbiguousOverlap);
    }

    [Fact]
    public void Panel_style_G1_and_G2_fixtures_map_without_replacing_panel_semantics()
    {
        var leftFlat = GraphDomain("panel-left-flat", new(new(-1, 0), new(-1, 1)), SurfaceExpression.Length(0));
        var rightBreak = GraphDomain("panel-right-break", new(new(0, 1), new(-1, 1)), PowerU(2));
        var g1PassG2Fail = ContactQuery.Between(leftFlat, rightBreak);
        Assert.Equal(ContactClassification.Tangent, g1PassG2Fail.Classification);
        Assert.Equal(ContactCurvatureRelation.DirectionDependent, g1PassG2Fail.CurvatureRelation);

        var leftSmooth = GraphDomain("panel-left-smooth", new(new(-1, 0), new(-1, 1)), PowerU(2));
        var rightSmooth = GraphDomain("panel-right-smooth", new(new(0, 1), new(-1, 1)), PowerU(2));
        var g2Pass = ContactQuery.Between(leftSmooth, rightSmooth);
        Assert.Equal(ContactClassification.SecondOrderCompatible, g2Pass.Classification);
        Assert.NotEqual(ContactClassification.Coincident, g2Pass.Classification);
    }

    [Fact]
    public void Contact_is_deterministic_and_cannot_author_topology()
    {
        var body = BrepPrimitives.CreateBox(4, 6, 8).Value;
        var before = Counts(body);
        var a = PlanePatch("firewall-a", Domain()); var b = Graph("firewall-b", PowerU(2));
        var first = JsonSerializer.Serialize(ContactQuery.Between(a, b));
        var second = JsonSerializer.Serialize(ContactQuery.Between(a, b));
        var after = Counts(body);
        Assert.Equal(first, second);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(first))), Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(second))));
        Assert.Equal(before, after);
        var result = ContactQuery.Between(a, b);
        Assert.False(result.HasTopologyAuthority);
        Assert.DoesNotContain(result.GetType().GetProperties(), p => p.PropertyType == typeof(BrepBody));
    }

    private static BoundedParametricCurve3 CurveGraph(string id, SurfaceScalarExpression z) => new(id, new(-1, 1), new(
        CurveExpression.Multiply(CurveExpression.Length(1), CurveExpression.T), CurveExpression.Length(0),
        CurveExpression.Multiply(CurveExpression.Length(1), z)), "fixture");
    private static SurfaceScalarExpression PowerU(int exponent) => SurfaceExpression.Multiply(SurfaceExpression.Length(1), SurfaceExpression.Power(SurfaceExpression.U, exponent));
    private static SurfaceScalarExpression PowerV(int exponent) => SurfaceExpression.Multiply(SurfaceExpression.Length(1), SurfaceExpression.Power(SurfaceExpression.V, exponent));
    private static ParametricDomain Domain() => new(new(-1, 1), new(-1, 1));
    private static BoundedParametricPatch3 Graph(string id, SurfaceScalarExpression z) => GraphDomain(id, Domain(), z);
    private static BoundedParametricPatch3 GraphDomain(string id, ParametricDomain domain, SurfaceScalarExpression z) => new(id, domain, new(
        SurfaceExpression.Multiply(SurfaceExpression.Length(1), SurfaceExpression.U),
        SurfaceExpression.Multiply(SurfaceExpression.Length(1), SurfaceExpression.V), z), "fixture");
    private static BoundedParametricPatch3 PlanePatch(string id, ParametricDomain domain) => BoundedParametricPatch3.Procedural(id, domain,
        (u, v) => new(new(u, v, 0), new(1, 0, 0), new(0, 1, 0), Direction3D.Create(new(0, 0, 1)), false),
        (u, v) => new(new(u, v, 0), new(1, 0, 0), new(0, 1, 0), Vector3D.Zero, Vector3D.Zero, Vector3D.Zero, DifferentialSingularityKind.Regular), "fixture");
    private static BoundedParametricPatch3 VerticalPatch(string id) => BoundedParametricPatch3.Procedural(id, Domain(),
        (u, v) => new(new(u, 0, v), new(1, 0, 0), new(0, 0, 1), Direction3D.Create(new(0, -1, 0)), false),
        (u, v) => new(new(u, 0, v), new(1, 0, 0), new(0, 0, 1), Vector3D.Zero, Vector3D.Zero, Vector3D.Zero, DifferentialSingularityKind.Regular), "fixture");
    private static (int Vertices, int Edges, int Faces, int Curves, int Surfaces) Counts(BrepBody body)
        => (body.Topology.Vertices.Count(), body.Topology.Edges.Count(), body.Topology.Faces.Count(), body.Geometry.Curves.Count(), body.Geometry.Surfaces.Count());
}
