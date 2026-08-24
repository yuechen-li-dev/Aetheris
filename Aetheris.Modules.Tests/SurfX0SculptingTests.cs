using Aetheris.Kernel.Core.Step242;
using Aetheris.Surfacing;
using Xunit;

namespace Aetheris.Modules.Tests;

public sealed class SurfX0SculptingTests
{
    [Fact]
    public void CanonicalOffsetRegionProducesImmutableSinglePredecessorStateAndInspectableDelta()
    {
        var result = Compile("Canonical/Sculpting/sculpted-housing.firmament");
        Assert.True(result.IsSuccess, string.Join(';', result.Diagnostics.Select(x => x.Code)));
        var state = Assert.IsType<BodyState>(result.OutputState);
        Assert.NotNull(state.PredecessorStateId); Assert.Equal(result.States["Base"].StateId, state.PredecessorStateId);
        Assert.NotSame(result.States["Base"].Body, state.Body); Assert.Equal(20d, result.States["Base"].Construction.FinalHeight); Assert.Equal(26d, state.Construction.FinalHeight);
        Assert.Contains(SculptedHousingFactory.BottomMountingInterface, state.Delta!.Preserves);
        Assert.Contains(SculptedHousingFactory.CrownRegion, state.Delta.Replaces);
        Assert.Contains(SculptedHousingFactory.TransitionZone, state.Delta.Introduces);
    }

    [Fact]
    public void BranchVariantsDeriveFromBaseWithoutMutatingEachOtherAndAreDeterministic()
    {
        var first = Compile("Canonical/Sculpting/sculpt-branch-variants.firmament"); var second = Compile("Canonical/Sculpting/sculpt-branch-variants.firmament");
        Assert.True(first.IsSuccess); Assert.Equal(first.OutputState!.StateId, second.OutputState!.StateId);
        var baseState = first.States["Base"]; var six = first.States["CrownRaised"]; var ten = first.States["CrownHigh"];
        Assert.Equal(baseState.StateId, six.PredecessorStateId); Assert.Equal(baseState.StateId, ten.PredecessorStateId);
        Assert.NotEqual(six.StateId, ten.StateId); Assert.Equal(20d, baseState.Construction.FinalHeight); Assert.Equal(26d, six.Construction.FinalHeight); Assert.Equal(30d, ten.Construction.FinalHeight);
    }

    [Fact]
    public void LocalityAndPreservationAreExactAndClosedBrepIsIndependentlyVerified()
    {
        var state = Compile("Canonical/Sculpting/sculpt-preserve-interface.firmament").OutputState!;
        Assert.Contains(state.ValidationEvidence, x => x.Check == "AuthorizedLocality" && x.Level == LocalityEvidenceLevel.ExactAnalytic && x.MaximumObservedDeviation == 0d);
        Assert.Contains(state.ValidationEvidence, x => x.Check == "Preserve:MountingHolePattern" && x.Satisfied);
        Assert.Contains(state.ValidationEvidence, x => x.Check == "ClosedManifold" && x.Satisfied);
        Assert.Contains(state.ValidationEvidence, x => x.Check == "OrientationConsistency" && x.Satisfied);
    }

    [Theory]
    [InlineData("Invalid/Sculpting/sculpt-outside-authorized-region.firmament", "sculpt-outside-authorized-region")]
    [InlineData("Invalid/Sculpting/sculpt-breaks-preserved-interface.firmament", "sculpt-breaks-preserved-interface")]
    [InlineData("Invalid/Sculpting/sculpt-self-intersection.firmament", "sculpt-self-intersection")]
    [InlineData("Invalid/Sculpting/sculpt-disconnected-result.firmament", "sculpt-target-domain-invalid")]
    public void FailedSculptIsAtomicAndTyped(string fixture, string diagnostic)
    {
        var result = Compile(fixture); Assert.False(result.IsSuccess); Assert.Contains(result.Diagnostics, x => x.Code == diagnostic);
        Assert.True(result.States.TryGetValue("Base", out var baseState)); Assert.Equal(20d, baseState.Construction.FinalHeight);
        Assert.DoesNotContain(result.States.Values, x => x.AuthoredName == "CrownRaised");
    }

    [Fact]
    public void StepBoundaryIsAnalyticRationalFreeAndReimports()
    {
        var state = Compile("Canonical/Sculpting/sculpted-housing.firmament").OutputState!; var export = SculptStepExporter.Export(state, "SURF-X0");
        Assert.True(export.IsSuccess, string.Join(';', export.Diagnostics.Select(x => x.Code))); Assert.Equal(0, export.Inventory.RationalNurbs);
        Assert.Equal(10, export.Inventory.Plane); Assert.Equal(4, export.Inventory.Cylinder); Assert.Equal(0, export.Inventory.NonRationalBSpline);
        Assert.DoesNotContain("RATIONAL_B_SPLINE_SURFACE", export.Step!, StringComparison.Ordinal);
        var import = Step242Importer.ImportBody(export.Step!); Assert.True(import.IsSuccess, string.Join(';', import.Diagnostics.Select(x => x.Message)));
    }

    [Fact]
    public void TypedConstructionAuthorityRoundTripsAndReplaysDeterministically()
    {
        var compiled = Compile("Canonical/Sculpting/sculpted-housing.firmament");
        var authored = Assert.IsType<ConstructionState>(compiled.OutputState!.ConstructionAuthority);
        var operation = Assert.Single(authored.Operations);
        Assert.Equal("OffsetRegion", operation.OperationKind);
        Assert.IsType<OffsetRegionOperation>(operation.Payload);

        var serialized = ConstructionStateSerializer.Serialize(authored);
        Assert.True(serialized.IsSuccess, string.Join(" | ", serialized.Diagnostics.Select(item => item.Message)));
        var deserialized = ConstructionStateSerializer.Deserialize(serialized.Json!);
        Assert.True(deserialized.IsSuccess, string.Join(" | ", deserialized.Diagnostics.Select(item => item.Message)));

        var replay = ConstructionStateReplayer.Replay(deserialized.ConstructionState!);
        Assert.True(replay.IsSuccess, string.Join(" | ", replay.Diagnostics.Select(item => item.Message)));
        Assert.Equal(compiled.OutputState.StateId, replay.OutputState!.StateId);
        Assert.Equal(SculptStepExporter.Export(compiled.OutputState, compiled.ModelName).Step,
            SculptStepExporter.Export(replay.OutputState, compiled.ModelName).Step);
        Assert.All(replay.OutputState.ConstructionAuthority!.Operations,
            item => Assert.Equal(ConstructionReplayStatus.ReplayedAndValidated, item.ReplayStatus));
    }

    [Fact]
    public void ReplayFailureIsAtomicAndLeavesPredecessorAuthoritative()
    {
        var authored = Compile("Canonical/Sculpting/sculpted-housing.firmament").OutputState!.ConstructionAuthority!;
        var operation = authored.Operations.Single();
        var unsupported = operation with { PayloadVersion = 99 };
        var replay = ConstructionStateReplayer.Replay(authored with { Operations = [unsupported] });

        Assert.False(replay.IsSuccess);
        Assert.Null(replay.OutputState);
        Assert.NotNull(replay.AuthoritativePredecessor);
        Assert.Null(replay.AuthoritativePredecessor!.PredecessorStateId);
        Assert.Equal(operation.OperationId, replay.FailedOperation!.OperationId);
        Assert.Contains(replay.Diagnostics, item => item.Code == "bodystate-operation-replay-failed");
        Assert.Contains(replay.Diagnostics, item => item.Code == "bodystate-operation-version-unsupported");
    }

    [Fact]
    public void TemporaryEqualWeightRationalSurfaceRecoversExactPlaneAndNonRemovableRationalityBlocks()
    {
        var points = new[] { new[] { new Aetheris.Kernel.Core.Math.Point3D(0, 0, 0), new(0, 1, 0) }, new[] { new Aetheris.Kernel.Core.Math.Point3D(1, 0, 0), new(1, 1, 0) } };
        var exact = RationalSurfaceNormalizer.Normalize(new(1, 1, points, new[] { new[] { 2d, 2d }, new[] { 2d, 2d } }));
        Assert.True(exact.IsSuccess); Assert.Equal("Plane", exact.EmittedFamily);
        var nonPlanar = new[] { new[] { new Aetheris.Kernel.Core.Math.Point3D(0, 0, 0), new(0, 1, 0) }, new[] { new Aetheris.Kernel.Core.Math.Point3D(1, 0, 0), new(1, 1, 1) } };
        var normalized = RationalSurfaceNormalizer.Normalize(new(1, 1, nonPlanar, new[] { new[] { 3d, 3d }, new[] { 3d, 3d } }));
        Assert.True(normalized.IsSuccess); Assert.Equal("NonRationalBSpline", normalized.EmittedFamily); Assert.NotNull(normalized.Surface!.BSplineSurfaceWithKnots);
        var blocked = RationalSurfaceNormalizer.Normalize(new(1, 1, points, new[] { new[] { 1d, .7d }, new[] { .7d, 1d } }));
        Assert.False(blocked.IsSuccess); Assert.Contains("surf-surface-export-normalization-failed", blocked.Diagnostic);
    }

    private static SculptingCompileResult Compile(string relative)
        => SculptingAuthoring.CompileFile(Path.Combine(RepositoryRoot(), "fixtures", relative.Replace('/', Path.DirectorySeparatorChar)));
    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Aetheris.slnx"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
