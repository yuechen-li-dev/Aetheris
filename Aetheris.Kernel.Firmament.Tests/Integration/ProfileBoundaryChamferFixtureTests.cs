using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Firmament.Tests.Integration;

public sealed class ProfileBoundaryChamferFixtureTests
{
    [Theory]
    [InlineData("profile-chamfer-convex-junction-top.firmament", 12, 6370.333333333333d)]
    [InlineData("profile-chamfer-reflex-junction-top.firmament", 18, 5569.666666666666d)]
    [InlineData("profile-chamfer-mixed-convex-reflex-loop-top.firmament", 14, 5521.333333333333d)]
    [InlineData("profile-chamfer-reflex-junction-bottom.firmament", 16, 5569.666666666666d)]
    [InlineData("profile-chamfer-reflex-low-level-segments.firmament", 18, 5569.666666666666d)]
    public void CanonicalFixture_ExportsAnEnclosedDeterministicPlanarStep(string fixtureName, int expectedFaces, double expectedVolume)
    {
        var source = FirmamentCorpusHarness.ResolveFixtureFullPath($"fixtures/FirmamentV2/Canonical/valid/{fixtureName}");
        var firstOutput = Path.Combine(Path.GetTempPath(), $"aetheris-{Guid.NewGuid():N}.step");
        var secondOutput = Path.Combine(Path.GetTempPath(), $"aetheris-{Guid.NewGuid():N}.step");
        try
        {
            var first = FirmamentBuildAndExport.Run(source, firstOutput);
            var second = FirmamentBuildAndExport.Run(source, secondOutput);

            Assert.True(first.IsSuccess, string.Join(Environment.NewLine, first.Diagnostics.Select(x => x.Message)));
            Assert.True(second.IsSuccess, string.Join(Environment.NewLine, second.Diagnostics.Select(x => x.Message)));
            Assert.Equal(first.Value.Export.StepText, second.Value.Export.StepText);
            var imported = Step242Importer.ImportBody(first.Value.Export.StepText);
            Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(x => x.Message)));
            Assert.NotNull(imported.Value);
            Assert.Equal(expectedFaces, imported.Value.Topology.Faces.Count());
            Assert.All(imported.Value.Bindings.FaceBindings, binding => Assert.Equal(SurfaceGeometryKind.Plane, imported.Value.Geometry.GetSurface(binding.SurfaceGeometryId).Kind));
            var mass = BrepMassProperties.Evaluate(imported.Value);
            Assert.True(mass.IsEnclosed);
            Assert.True(mass.IsOrientationConsistent);
            Assert.InRange(Math.Abs(mass.AbsoluteVolume - expectedVolume), 0d, 1e-8d);
        }
        finally
        {
            if (File.Exists(firstOutput)) File.Delete(firstOutput);
            if (File.Exists(secondOutput)) File.Delete(secondOutput);
        }
    }

    [Theory]
    [InlineData("profile-compose-reflex-chamfer-with-shaft.firmament")]
    [InlineData("profile-compose-reflex-chamfer-with-counterbore.firmament")]
    public void ComposedReflexFixture_AdmitsDisjointCavity(string fixtureName)
    {
        var source = FirmamentCorpusHarness.ResolveFixtureFullPath($"fixtures/FirmamentV2/Canonical/valid/{fixtureName}");
        var output = Path.Combine(Path.GetTempPath(), $"aetheris-{Guid.NewGuid():N}.step");
        try
        {
            var build = FirmamentBuildAndExport.Run(source, output);
            Assert.True(build.IsSuccess, string.Join(Environment.NewLine, build.Diagnostics.Select(x => x.Message)));
            var imported = Step242Importer.ImportBody(build.Value.Export.StepText);
            Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(x => x.Message)));
            Assert.True(BrepMassProperties.Evaluate(imported.Value!).IsEnclosed);
        }
        finally
        {
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Theory]
    [InlineData("profile-straight-edge-fillet-top.firmament")]
    [InlineData("profile-straight-edge-fillet-bottom.firmament")]
    public void StraightFilletFixture_ExportsDeterministicExactCylinderAndEndpointArcs(string fixtureName)
    {
        var source = FirmamentCorpusHarness.ResolveFixtureFullPath($"fixtures/FirmamentV2/Canonical/valid/{fixtureName}");
        var firstOutput = Path.Combine(Path.GetTempPath(), $"aetheris-{Guid.NewGuid():N}.step");
        var secondOutput = Path.Combine(Path.GetTempPath(), $"aetheris-{Guid.NewGuid():N}.step");
        try
        {
            var first = FirmamentBuildAndExport.Run(source, firstOutput); var second = FirmamentBuildAndExport.Run(source, secondOutput);
            Assert.True(first.IsSuccess, string.Join(Environment.NewLine, first.Diagnostics.Select(x => x.Message)));
            Assert.True(second.IsSuccess, string.Join(Environment.NewLine, second.Diagnostics.Select(x => x.Message)));
            Assert.Equal(first.Value.Export.StepText, second.Value.Export.StepText);
            var imported = Step242Importer.ImportBody(first.Value.Export.StepText);
            Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(x => x.Message)));
            Assert.Equal(1, imported.Value!.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Cylinder));
            Assert.Equal(2, imported.Value.Geometry.Curves.Count(c => c.Value.Kind == CurveGeometryKind.Circle3));
            Assert.True(BrepMassProperties.Evaluate(imported.Value).IsEnclosed);
        }
        finally
        {
            if (File.Exists(firstOutput)) File.Delete(firstOutput);
            if (File.Exists(secondOutput)) File.Delete(secondOutput);
        }
    }

    [Theory]
    [InlineData("profile-fillet-convex-two-segment-top.firmament")]
    [InlineData("profile-fillet-convex-two-segment-bottom.firmament")]
    public void ConvexTwoSegmentFilletFixture_ExportsDeterministicTwoCylindersAndSphere(string fixtureName)
    {
        var source = FirmamentCorpusHarness.ResolveFixtureFullPath($"fixtures/FirmamentV2/Canonical/valid/{fixtureName}");
        var firstOutput = Path.Combine(Path.GetTempPath(), $"aetheris-{Guid.NewGuid():N}.step");
        var secondOutput = Path.Combine(Path.GetTempPath(), $"aetheris-{Guid.NewGuid():N}.step");
        try
        {
            var first = FirmamentBuildAndExport.Run(source, firstOutput); var second = FirmamentBuildAndExport.Run(source, secondOutput);
            Assert.True(first.IsSuccess, string.Join(Environment.NewLine, first.Diagnostics.Select(item => item.Message)));
            Assert.True(second.IsSuccess, string.Join(Environment.NewLine, second.Diagnostics.Select(item => item.Message)));
            Assert.Equal(first.Value.Export.StepText, second.Value.Export.StepText);
            var imported = Step242Importer.ImportBody(first.Value.Export.StepText);
            Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(item => item.Message)));
            Assert.Equal(2, imported.Value!.Geometry.Surfaces.Count(item => item.Value.Kind == SurfaceGeometryKind.Cylinder));
            Assert.Equal(1, imported.Value.Geometry.Surfaces.Count(item => item.Value.Kind == SurfaceGeometryKind.Sphere));
            var mass = BrepMassProperties.Evaluate(imported.Value);
            Assert.True(mass.IsEnclosed);
            Assert.True(mass.IsOrientationConsistent);
        }
        finally
        {
            if (File.Exists(firstOutput)) File.Delete(firstOutput);
            if (File.Exists(secondOutput)) File.Delete(secondOutput);
        }
    }

    [Fact]
    public void ConvexTwoSegmentFillet_ConceptPathAndExplicitSegmentsAreStepEquivalent()
    {
        var concept = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/FirmamentV2/Canonical/valid/profile-fillet-convex-two-segment-concept-path.firmament");
        var lowLevel = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/FirmamentV2/Canonical/valid/profile-fillet-convex-two-segment-low-level.firmament");
        var conceptResult = FirmamentBuildAndExport.Run(concept, Path.Combine(Path.GetTempPath(), $"aetheris-{Guid.NewGuid():N}.step"));
        var lowLevelResult = FirmamentBuildAndExport.Run(lowLevel, Path.Combine(Path.GetTempPath(), $"aetheris-{Guid.NewGuid():N}.step"));

        Assert.True(conceptResult.IsSuccess, string.Join(Environment.NewLine, conceptResult.Diagnostics.Select(item => item.Message)));
        Assert.True(lowLevelResult.IsSuccess, string.Join(Environment.NewLine, lowLevelResult.Diagnostics.Select(item => item.Message)));
        Assert.Equal(conceptResult.Value.Export.StepText, lowLevelResult.Value.Export.StepText);
    }

    [Theory]
    [InlineData("profile-compose-reflex-chamfer-shaft-collision.firmament", "ProfileBoundaryChamferIntersectsShaft")]
    [InlineData("profile-compose-reflex-chamfer-counterbore-collision.firmament", "ProfileBoundaryChamferIntersectsCounterbore")]
    [InlineData("profile-chamfer-reflex-inset-collapse.firmament", "ProfileBoundaryChamferInsetCollapse")]
    [InlineData("profile-fillet-reflex-junction-not-materialized.firmament", "ProfileBoundaryFilletReflexJunctionUnsupported")]
    [InlineData("profile-straight-edge-fillet-shaft-collision.firmament", "ProfileBoundaryFilletIntersectsShaft")]
    public void InvalidFixture_ProducesTypedProfileBoundaryDiagnostic(string fixtureName, string diagnostic)
    {
        var source = FirmamentCorpusHarness.ResolveFixtureFullPath($"fixtures/FirmamentV2/Canonical/invalid/{fixtureName}");
        var build = FirmamentBuildAndExport.Run(source, Path.Combine(Path.GetTempPath(), $"aetheris-{Guid.NewGuid():N}.step"));

        Assert.False(build.IsSuccess);
        Assert.Contains(build.Diagnostics, x => x.Message.StartsWith(diagnostic, StringComparison.Ordinal));
    }
}
