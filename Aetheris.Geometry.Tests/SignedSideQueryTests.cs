using System.Text.Json;
using Aetheris.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Surfacing;
using Xunit;

namespace Aetheris.Geometry.Tests;

public sealed class SignedSideQueryTests
{
    private static readonly Plane3 ToolingPlane = new(Point3D.Origin, Direction3D.Create(new Vector3D(0, 0, 1)));

    [Fact]
    public void DomainFirstJetNormalIdentityAndProvenanceArePublic()
    {
        var invalid = Assert.Throws<GeometryDefinitionException>(() => new ParameterInterval2(1, 1));
        Assert.Equal(GeometryQueryDiagnosticCode.InvalidParameterDomain, invalid.Code);
        var patch = Graph("jet", SurfaceExpression.Add(SurfaceExpression.Length(3), SurfaceExpression.Multiply(SurfaceExpression.Length(2), SurfaceExpression.U)));
        var jet = patch.Evaluate(.25, -.5);
        Assert.Equal(new Point3D(.25, -.5, 3.5), jet.Point);
        Assert.Equal(new Vector3D(1, 0, 2), jet.Du);
        Assert.Equal(new Vector3D(0, 1, 0), jet.Dv);
        Assert.False(jet.IsSingular); Assert.NotNull(jet.Normal);
        Assert.Equal("jet", patch.StableId); Assert.Equal("fixture:jet", patch.Provenance);
        Assert.Equal(GeometryRepresentationKind.AnalyticExpression, patch.Representation);
    }

    [Fact]
    public void InvalidPlaneIsATypedUnknownResult()
    {
        var result = SignedSideQuery.Query(Graph("invalid-plane", SurfaceExpression.Length(1)), default, SignedSidePolicy.Sampled());
        Assert.Equal(SignedSideClassification.Unknown, result.Classification);
        Assert.Equal(PredicateEvidenceKind.Unknown, result.EvidenceKind);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == GeometryQueryDiagnosticCode.InvalidPlane);
    }

    [Fact]
    public void SampledPolicyClassifiesStrictSidesAndCrossingButNeverCertifies()
    {
        var positive = SignedSideQuery.Query(Graph("positive", SurfaceExpression.Length(2)), ToolingPlane, SignedSidePolicy.Sampled(samplesU: 5, samplesV: 7));
        var negative = SignedSideQuery.Query(Graph("negative", SurfaceExpression.Length(-2)), ToolingPlane, SignedSidePolicy.Sampled());
        var crossing = SignedSideQuery.Query(Graph("crossing", SurfaceExpression.Multiply(SurfaceExpression.Length(2), SurfaceExpression.U)), ToolingPlane, SignedSidePolicy.Sampled());
        Assert.Equal(SignedSideClassification.Positive, positive.Classification);
        Assert.Equal(SignedSideClassification.Negative, negative.Classification);
        Assert.Equal(SignedSideClassification.Crossing, crossing.Classification);
        Assert.All(new[] { positive, negative, crossing }, result => Assert.Equal(PredicateEvidenceKind.Sampled, result.EvidenceKind));
        Assert.Equal(35, positive.Statistics.SampleCount); Assert.Equal(-2, crossing.ObservedMinimum); Assert.Equal(2, crossing.ObservedMaximum);
    }

    [Fact]
    public void NearContactSamplingIsUnknownWithContactCandidate()
    {
        var tangent = Graph("tangent", SurfaceExpression.Multiply(SurfaceExpression.Length(2), SurfaceExpression.Power(SurfaceExpression.U, 2)));
        var result = SignedSideQuery.Query(tangent, ToolingPlane, SignedSidePolicy.Sampled(1e-12, 9, 9));
        Assert.Equal(SignedSideClassification.Unknown, result.Classification);
        Assert.Equal(PredicateEvidenceKind.Sampled, result.EvidenceKind);
        Assert.Contains(result.Witnesses, witness => witness.Kind == "contact-candidate");
    }

    [Fact]
    public void IntervalPolicyCertifiesPositiveNegativeAndCrossing()
    {
        var squared = SurfaceExpression.Multiply(SurfaceExpression.Length(1), SurfaceExpression.Power(SurfaceExpression.U, 2));
        var positive = SignedSideQuery.Query(Graph("safe", SurfaceExpression.Add(SurfaceExpression.Length(1), squared)), ToolingPlane, SignedSidePolicy.Certified());
        var negative = SignedSideQuery.Query(Graph("below", SurfaceExpression.Subtract(SurfaceExpression.Length(-1), squared)), ToolingPlane, SignedSidePolicy.Certified());
        var crossing = SignedSideQuery.Query(Graph("failing", SurfaceExpression.Multiply(SurfaceExpression.Length(2), SurfaceExpression.U)), ToolingPlane, SignedSidePolicy.Certified(maximumSubdivisionDepth: 4));
        Assert.Equal((SignedSideClassification.Positive, PredicateEvidenceKind.Certified), (positive.Classification, positive.EvidenceKind));
        Assert.Equal((SignedSideClassification.Negative, PredicateEvidenceKind.Certified), (negative.Classification, negative.EvidenceKind));
        Assert.Equal((SignedSideClassification.Crossing, PredicateEvidenceKind.Certified), (crossing.Classification, crossing.EvidenceKind));
        Assert.True(crossing.Statistics.ResolvedLeaves > 0);
    }

    [Fact]
    public void TangencyAndBudgetExhaustionRemainUnknown()
    {
        var tangent = Graph("tangent", SurfaceExpression.Multiply(SurfaceExpression.Length(2), SurfaceExpression.Power(SurfaceExpression.U, 2)));
        var result = SignedSideQuery.Query(tangent, ToolingPlane, SignedSidePolicy.Certified(maximumSubdivisionDepth: 2, maximumLeafCount: 16));
        Assert.Equal(SignedSideClassification.Unknown, result.Classification);
        Assert.Equal(PredicateEvidenceKind.Unknown, result.EvidenceKind);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == GeometryQueryDiagnosticCode.SubdivisionBudgetExhausted);
    }

    [Fact]
    public void SinCosAndDivisionUseSoundBoundedEvaluation()
    {
        var trig = SurfaceExpression.Add(SurfaceExpression.Length(3), SurfaceExpression.Multiply(SurfaceExpression.Length(1),
            SurfaceExpression.Add(SurfaceExpression.Sin(SurfaceExpression.U), SurfaceExpression.Cos(SurfaceExpression.V))));
        var result = SignedSideQuery.Query(Graph("trig", trig), ToolingPlane, SignedSidePolicy.Certified());
        Assert.Equal(SignedSideClassification.Positive, result.Classification);
        Assert.Equal(PredicateEvidenceKind.Certified, result.EvidenceKind);

        var singularDivision = SurfaceExpression.Divide(SurfaceExpression.Length(1), SurfaceExpression.U);
        var unsupported = SignedSideQuery.Query(Graph("division", singularDivision), ToolingPlane, SignedSidePolicy.Certified());
        Assert.Equal(SignedSideClassification.Unknown, unsupported.Classification);
        Assert.Contains(unsupported.Diagnostics, item => item.Code == GeometryQueryDiagnosticCode.UnsupportedCertifiedExpression);
    }

    [Fact]
    public void ProceduralPatchCertificationDoesNotSilentlySample()
    {
        var domain = new ParametricDomain(new(-1, 1), new(-1, 1));
        var patch = BoundedParametricPatch3.Procedural("procedural", domain, (u, v) =>
            new(new(u, v, 2), new(1, 0, 0), new(0, 1, 0), Direction3D.Create(new Vector3D(0, 0, 1)), false), "fixture");
        var result = SignedSideQuery.Query(patch, ToolingPlane, SignedSidePolicy.Certified());
        Assert.Equal(SignedSideClassification.Unknown, result.Classification);
        Assert.Equal(PredicateEvidenceKind.Unknown, result.EvidenceKind);
        Assert.Equal(0, result.Statistics.SampleCount);
    }

    [Fact]
    public void PanelAdapterUsesAuthoredPatchForOrdinaryToolingClearance()
    {
        var surface = new ParametricSurfaceIr("tool-clearance", SurfaceConstructionKind.ParametricSurface,
            new(new(-1, 1), new(-1, 1)), GraphExpression(SurfaceExpression.Length(5)), "cad:tooling-clearance-panel");
        var panel = PanelFactory.FromParametric(surface).Panel!;
        Assert.Same(surface.Patch, panel.AuthoredPatch);
        var result = SignedSideQuery.Query(panel.AuthoredPatch, ToolingPlane, SignedSidePolicy.Certified());
        Assert.Equal((SignedSideClassification.Positive, PredicateEvidenceKind.Certified), (result.Classification, result.EvidenceKind));
        Assert.Equal("cad:tooling-clearance-panel", result.Provenance);
    }

    [Fact]
    public void IdenticalQueriesSerializeDeterministically()
    {
        var patch = Graph("determinism", SurfaceExpression.Add(SurfaceExpression.Length(1), SurfaceExpression.Multiply(SurfaceExpression.Length(1), SurfaceExpression.U)));
        var policy = SignedSidePolicy.Sampled(1e-9, 7, 7);
        var first = JsonSerializer.Serialize(SignedSideQuery.Query(patch, ToolingPlane, policy));
        var second = JsonSerializer.Serialize(SignedSideQuery.Query(patch, ToolingPlane, policy));
        Assert.Equal(first, second);
    }

    [Fact]
    public void SignedSideExpectationIsAnEvidenceObligationNotMaterialization()
    {
        var result = SignedSideQuery.Query(Graph("obligation", SurfaceExpression.Length(2)), ToolingPlane, SignedSidePolicy.Sampled());
        Assert.True(new SignedSideExpectation(SignedSideClassification.Positive, PredicateEvidenceKind.Sampled).Evaluate(result).Satisfied);
        var rejected = new SignedSideExpectation(SignedSideClassification.Positive, PredicateEvidenceKind.Certified).Evaluate(result);
        Assert.False(rejected.Satisfied); Assert.Contains("Certified", rejected.RejectionReason);
    }

    private static BoundedParametricPatch3 Graph(string id, SurfaceScalarExpression z) =>
        new(id, new(new(-1, 1), new(-1, 1)), GraphExpression(z), "fixture:" + id);
    private static SurfacePointExpression GraphExpression(SurfaceScalarExpression z) =>
        new(SurfaceExpression.Multiply(SurfaceExpression.Length(1), SurfaceExpression.U),
            SurfaceExpression.Multiply(SurfaceExpression.Length(1), SurfaceExpression.V), z);
}
