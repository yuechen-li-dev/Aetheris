using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.StandardLibrary;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ExactConstructionIrM1Tests
{
    [Fact]
    public void RegularPrism_IsIndependentOfBoltAndDerivesDeterministicGeometry()
    {
        var hex = new RegularPrismConstruction("hex", 6, 13d, 0d, 5d);
        var octagon = new RegularPrismConstruction("octagon", 8, 20d, -2d, 7d, 22.5d);
        Assert.Equal(6.5d, hex.Apothem, 12);
        Assert.Equal(13d / Math.Sqrt(3d), hex.Circumradius, 12);
        Assert.Equal(10d / Math.Cos(Math.PI / 8d), octagon.Circumradius, 12);
        Assert.Equal(22.5d, octagon.OrientationDegrees);
    }

    [Fact]
    public void CoaxialPlan_ExplicitlySharesAnalyticSupportsAndPreservesRootBlend()
    {
        var plan = HexBoltConstructionPlanner.Plan(McMasterHexBoltSpecs.Reference91180A151, "fixture");
        Assert.True(plan.IsSuccess);
        Assert.Contains(plan.Value.Stack.Sections, x => x is ConcaveFilletConstruction);
        Assert.Contains(plan.Value.Stack.Sections, x => x is ConePlanarTrimConstruction);

        // A non-bolt fixture can use the same two exact axial intent nodes without
        // importing HexBoltSpec or any bolt semantic name.
        AxialSectionStackConstruction steppedShaft = new("stepped-shaft", [
            new AxialCylinderConstruction("shaft", 5d, 0d, 20d),
            new ConcaveFilletConstruction("shoulder-root", 0.5d, 5.5d, 0d, 0.5d)]);
        Assert.Equal(2, steppedShaft.Sections.Count);
    }

    [Fact]
    public void PlannedRoute_EmitsExactAnalyticFamiliesWithoutTheLegacyBuilder()
    {
        var plan = HexBoltConstructionPlanner.Plan(McMasterHexBoltSpecs.Reference91180A151, "planned");
        var result = ExactConstructionMaterializer.Materialize(plan.Value);
        Assert.True(result.IsSuccess);
        Assert.True(BrepExportPreflight.Validate(result.Value.Body).IsValid);
        Assert.Equal(1, result.Value.Body.Geometry.Surfaces.Count(x => x.Value.Kind == SurfaceGeometryKind.Torus));
        Assert.Equal(2, result.Value.Body.Geometry.Surfaces.Count(x => x.Value.Kind == SurfaceGeometryKind.Cone));
    }

    [Fact]
    public void GenericEmitter_MaterializesAnOctagonalNonBoltFixture()
    {
        var source = HexBoltConstructionPlanner.Plan(McMasterHexBoltSpecs.Reference91180A151, "source").Value;
        var prism = source.Prism with { StableId = "octagonal-collar", SideCount = 8, OrientationDegrees = 22.5d };
        prism = prism with { End = source.ConePlanarTrim.Apex + prism.Circumradius / Math.Tan(source.ConePlanarTrim.SemiAngleDegrees * Math.PI / 180d) };
        ConstructionSemanticClaim[] claims =
        [
            new("SteppedShaft", ConstructionSemanticKind.Part),
            new("SteppedShaft.Collar.Face[{i}]", ConstructionSemanticKind.Face, "PrismSides", "SteppedShaft")
        ];
        var stack = source.Stack with { StableId = "stepped-shaft-stack", Sections = [prism, source.ConePlanarTrim, source.TopCap, source.RootBlend, source.Cylinder, source.EndFrustum, source.EndCap] };
        var fixture = source with { StableId = "SteppedShaft", Prism = prism, Stack = stack, SemanticClaims = claims, Metadata = new Dictionary<string, string>(), DeterministicSignature = "non-bolt-fixture" };

        var result = ExactConstructionMaterializer.Materialize(fixture);
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(x => x.Message)));
        Assert.True(BrepExportPreflight.Validate(result.Value.Body).IsValid);
        Assert.Equal(8, result.Value.FaceGroups["PrismSides"].Count);
        Assert.Equal(8, result.Value.Body.Geometry.Curves.Count(x => x.Value.Kind == CurveGeometryKind.Hyperbola3));
        Assert.DoesNotContain(result.Value.Semantics, x => x.StableId.Contains("Bolt", StringComparison.Ordinal));
    }

    [Fact]
    public void ExactCoaxialPartBuilder_EmitsNonHexRecipeWithoutHexBoltTypes()
    {
        var recipe = new ExactCoaxialPartRecipe("OctagonalSpacer", 8, 13d, 5.3d, 12.35d, 25d, 0.2d,
            8d, 35d, 0.9375d, 6.125d, 10d, "axial-zone", "fixture");
        var result = ExactCoaxialPartBuilder.Create(recipe);
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(x => x.Message)));
        Assert.Equal(8, result.Value.ConstructionPlan.Prism.SideCount);
        Assert.Equal(8, result.Value.Body.Geometry.Curves.Count(x => x.Value.Kind == CurveGeometryKind.Hyperbola3));
        Assert.True(BrepExportPreflight.Validate(result.Value.Body).IsValid);
    }

    [Fact]
    public void PeriodicMaterializers_ReuseOneSupportAcrossSplitFaces()
    {
        var plan = HexBoltConstructionPlanner.Plan(McMasterHexBoltSpecs.Reference91180A151, "sharing").Value;
        var result = ExactConstructionMaterializer.Materialize(plan).Value;
        foreach (var role in new[] { "ConePlanarTrim", "RootBlend", "Cylinder", "EndFrustum" })
        {
            var supportIds = result.FaceGroups[role]
                .Select(face => result.Body.Bindings.GetFaceBinding(face).SurfaceGeometryId)
                .Distinct().ToArray();
            Assert.Single(supportIds);
        }
        var torus = Assert.Single(result.Body.Geometry.Surfaces, x => x.Value.Kind == SurfaceGeometryKind.Torus).Value.Torus!.Value;
        Assert.Equal(plan.RootBlend.ShoulderRadius, torus.MajorRadius, 12);
        Assert.Equal(plan.RootBlend.Radius, torus.MinorRadius, 12);
        Assert.Equal(plan.RootBlend.End, torus.Center.X, 12);
    }
}
