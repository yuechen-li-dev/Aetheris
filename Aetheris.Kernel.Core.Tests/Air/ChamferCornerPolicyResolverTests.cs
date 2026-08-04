using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Air.BRepPlan;
using Aetheris.Kernel.Core.Brep.Prismatic;

namespace Aetheris.Kernel.Core.Tests.Air;

public sealed class ChamferCornerPolicyResolverTests
{
    [Fact]
    public void ClosedRectangularLoop_HasOneHardValidPolicy_AndSelectsDirectly()
    {
        var planned = AirTopFaceLoopChamferBRepPlanner.Plan(new PrismaticTopFaceLoopChamferRequest(10, 8, 6, 1));
        Assert.True(planned.Succeeded);
        var witness = new ChamferCornerConstructionWitness(
            "corner-witness:top-loop:0",
            "PrismaticSectionTransition",
            ChamferCornerPolicy.SectionTransitionJunction,
            planned.Plan!,
            ChamferCornerSourceProvenance.GeneratedHistoryKnown);

        var result = ChamferCornerPolicyResolver.Resolve(ValidContext(
            topology: ChamferCornerTopologyKind.ClosedLoop,
            incidentSelectedEdges: 2,
            valence: 3,
            witness: witness));

        Assert.True(result.IsSuccess);
        Assert.Equal(ChamferCornerPolicy.SectionTransitionJunction, result.Value!.Policy);
        Assert.Equal(ChamferCornerSelectionMode.Direct, result.Value.SelectionMode);
        var candidate = Assert.Single(result.Value.Candidates);
        Assert.True(candidate.Admitted);
        Assert.Null(candidate.UtilityScore);
        Assert.Same(planned.Plan, result.Value.Witness.Plan);
        Assert.True(result.Value.Witness.Plan.IsAuthoritative);
    }

    [Theory]
    [InlineData(2, "PlanarEdgePairCut")]
    [InlineData(3, "PlanarTriangularCut")]
    public void ConvexJunction_LegacyGeometryWithoutAuthoritativePlan_IsNotModernAdmission(
        int selectedEdges,
        string expectedPolicy)
    {
        var result = ChamferCornerPolicyResolver.Resolve(ValidContext(
            topology: ChamferCornerTopologyKind.Junction,
            incidentSelectedEdges: selectedEdges,
            valence: 3,
            witness: null));

        Assert.False(result.IsSuccess);
        Assert.Equal(ChamferLoweringErrorKind.ConstructionWitnessRequired, result.Error!.Kind);
        Assert.Equal("chamfer-corner-construction-witness-required:authoritative-brep-plan", result.Error.Code);
        Assert.Contains(result.Error.Evidence!, value => value.Contains($"candidate={expectedPolicy}", StringComparison.Ordinal));
        Assert.Contains(result.Error.Evidence!, value => value.Contains("MissingAuthoritativeBRepPlan", StringComparison.Ordinal));
    }

    [Fact]
    public void ConcaveTrihedralCorner_RequiresAnExplicitValidatedWitness()
    {
        var context = ValidContext(ChamferCornerTopologyKind.Junction, 3, 3, null) with
        {
            Convexity = ChamferCornerConvexity.Concave,
            MaterialSide = ChamferCornerMaterialSide.RetainExterior,
            SourceProvenance = ChamferCornerSourceProvenance.GeneratedHistoryKnown,
        };

        var result = ChamferCornerPolicyResolver.Resolve(context);

        Assert.False(result.IsSuccess);
        Assert.Equal(ChamferLoweringErrorKind.ConstructionWitnessRequired, result.Error!.Kind);
        Assert.Contains(result.Error.Evidence!, value => value.Contains("candidate=ExplicitWitness", StringComparison.Ordinal));
    }

    [Fact]
    public void HardInvariantFailure_IsARejectionAndNeverAUtilityPenalty()
    {
        var planned = AirTopFaceLoopChamferBRepPlanner.Plan(new PrismaticTopFaceLoopChamferRequest(10, 8, 6, 1));
        var witness = new ChamferCornerConstructionWitness("w", "PrismaticSectionTransition", ChamferCornerPolicy.SectionTransitionJunction, planned.Plan!, ChamferCornerSourceProvenance.GeneratedHistoryKnown);
        var context = ValidContext(ChamferCornerTopologyKind.ClosedLoop, 2, 3, witness) with { NonSelfIntersecting = false };

        var result = ChamferCornerPolicyResolver.Resolve(context);

        Assert.False(result.IsSuccess);
        Assert.Equal(ChamferLoweringErrorKind.SelfIntersection, result.Error!.Kind);
        Assert.Contains(result.Error.Evidence!, value => value.Contains("failure=SelfIntersection", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Error.Evidence!, value => value.Contains("score", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AsymmetricRule_IsHardRejected_NotUtilityRanked()
    {
        var planned = AirTopFaceLoopChamferBRepPlanner.Plan(new PrismaticTopFaceLoopChamferRequest(10, 8, 6, 1));
        var witness = new ChamferCornerConstructionWitness("w", "PrismaticSectionTransition", ChamferCornerPolicy.SectionTransitionJunction, planned.Plan!, ChamferCornerSourceProvenance.GeneratedHistoryKnown);
        var context = ValidContext(ChamferCornerTopologyKind.ClosedLoop, 2, 3, witness) with { Rule = ChamferCornerRule.Asymmetric };

        var result = ChamferCornerPolicyResolver.Resolve(context);

        Assert.False(result.IsSuccess);
        Assert.Equal(ChamferLoweringErrorKind.UnsupportedSelection, result.Error!.Kind);
        Assert.Contains(result.Error.Evidence!, value => value.Contains("failure=UnsupportedChamferRule", StringComparison.Ordinal));
    }

    private static ChamferCornerContext ValidContext(
        ChamferCornerTopologyKind topology,
        int incidentSelectedEdges,
        int valence,
        ChamferCornerConstructionWitness? witness) => new(
            CornerId: "Corner0",
            Convexity: ChamferCornerConvexity.Convex,
            IncidentSelectedEdgeCount: incidentSelectedEdges,
            VertexValence: valence,
            SupportSurfaces: Enumerable.Repeat(ChamferCornerSupportSurfaceFamily.Plane, valence).ToArray(),
            MaterialSide: ChamferCornerMaterialSide.RetainInterior,
            Rule: ChamferCornerRule.UniformEqualDistance,
            TopologyKind: topology,
            HasConstructionHistory: true,
            IsSymmetric: true,
            SourceProvenance: ChamferCornerSourceProvenance.GeneratedHistoryKnown,
            DistanceAdmissible: true,
            NonSelfIntersecting: true,
            HasClosedReplacementRegion: true,
            HasRetainedRegionOwnership: true,
            HasReplacementRegionOwnership: true,
            PreservesManifoldTopology: true,
            ExactConstructionAvailable: true,
            AvailableWitness: witness);
}
