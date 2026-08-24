using Aetheris.Kernel.Core.Math;
using Aetheris.Surfacing;
using Xunit;

namespace Aetheris.Modules.Tests;

public sealed class SurfX3bConstructionStateTests
{
    [Fact]
    public void AddSectionChainBuildsOneBodyAndSurvivesSerializedReplay()
    {
        var baseState = Base(); var chain = AdditiveChain();
        var operation = new AddSectionChainOperation("Grip.AddSectionChain", chain,
            new(SculptedHousingFactory.HousingSideEast, "Attach", SectionChainAttachmentPlacement.RelativeToSupport,
                SpanIds.Select(id => new SectionSpanCorrespondence(id, id)).ToArray()),
            [SculptedHousingFactory.HousingSideEast], new(40, -20, 0, 88, 20, 20), Preserve(), Requirements());

        var added = AddSectionChainSculptor.Apply(baseState, "GripAdded", operation);
        Assert.True(added.IsSuccess, Messages(added.Diagnostics));
        Assert.Single(added.OutputState!.Body.Topology.Bodies);
        Assert.Equal("AddSectionChain", Assert.Single(added.OutputState.ConstructionAuthority!.Operations).OperationKind);
        Assert.Contains(added.Evidence, item => item.Check == "AddSectionChainPositiveVolume" && item.Satisfied);

        var serialized = ConstructionStateSerializer.Serialize(added.OutputState.ConstructionAuthority);
        Assert.True(serialized.IsSuccess, Messages(serialized.Diagnostics));
        var deserialized = ConstructionStateSerializer.Deserialize(serialized.Json!);
        Assert.True(deserialized.IsSuccess, Messages(deserialized.Diagnostics));
        var replay = ConstructionStateReplayer.Replay(deserialized.ConstructionState!);
        Assert.True(replay.IsSuccess, Messages(replay.Diagnostics));
        Assert.Equal(added.OutputState.StateId, replay.OutputState!.StateId);
        Assert.Equal(SculptStepExporter.Export(added.OutputState, "Grip").Step, SculptStepExporter.Export(replay.OutputState, "Grip").Step);
    }

    [Fact]
    public void RemoveSectionChainBuildsChangingThroughDuctAndSurvivesReplay()
    {
        var baseState = Base(); var chain = DuctChain();
        var operation = new RemoveSectionChainOperation("Duct.RemoveSectionChain", chain,
            [SculptedHousingFactory.HousingSideWest, SculptedHousingFactory.HousingSideEast],
            [SculptedHousingFactory.HousingSideWest, SculptedHousingFactory.HousingSideEast],
            new(-40, -7, 5, 40, 7, 15), Preserve(), Requirements());

        var removed = RemoveSectionChainSculptor.Apply(baseState, "DuctRemoved", operation);
        Assert.True(removed.IsSuccess, Messages(removed.Diagnostics));
        Assert.Single(removed.OutputState!.Body.Topology.Bodies);
        Assert.Equal("RemoveSectionChain", Assert.Single(removed.OutputState.ConstructionAuthority!.Operations).OperationKind);
        Assert.Contains(removed.Evidence, item => item.Check == "RemoveSectionChainPositiveVolume" && item.Satisfied);
        var replay = ConstructionStateReplayer.Replay(removed.OutputState.ConstructionAuthority);
        Assert.True(replay.IsSuccess, Messages(replay.Diagnostics));
        Assert.Equal(removed.OutputState.StateId, replay.OutputState!.StateId);
    }

    [Fact]
    public void MissingSemanticSupportFailsWithoutNearestFaceRebinding()
    {
        var baseState = Base();
        var operation = new AddSectionChainOperation("Grip.AddSectionChain", AdditiveChain(),
            new("DeletedSupport", "Attach", SectionChainAttachmentPlacement.RelativeToSupport, []), ["DeletedSupport"],
            new(40, -20, 0, 88, 20, 20), Preserve(), Requirements());
        var result = AddSectionChainSculptor.Apply(baseState, "Invalid", operation);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, item => item.Code == "bodystate-operation-support-missing");
        Assert.Null(result.OutputState);
    }

    [Theory]
    [InlineData("surf-x3b-add-section-chain-grip.firmament", "AddSectionChain")]
    [InlineData("surf-x3b-remove-section-chain-duct.firmament", "RemoveSectionChain")]
    public void FirmamentFlagshipsLowerToTypedOperationsAndReplay(string fixture, string kind)
    {
        var compiled = SculptingAuthoring.CompileFile(Fixture("Canonical", "BodyState", fixture));
        Assert.True(compiled.IsSuccess, Messages(compiled.Diagnostics));
        Assert.Equal(kind, Assert.Single(compiled.OutputState!.ConstructionAuthority!.Operations).OperationKind);
        var replay = ConstructionStateReplayer.Replay(compiled.OutputState.ConstructionAuthority);
        Assert.True(replay.IsSuccess, Messages(replay.Diagnostics));
        Assert.Equal(compiled.OutputState.StateId, replay.OutputState!.StateId);
        Assert.Equal(SculptStepExporter.Export(compiled.OutputState, compiled.ModelName).Step,
            SculptStepExporter.Export(replay.OutputState, compiled.ModelName).Step);
    }

    [Fact]
    public void ThreeTypedFirmamentOperationsAndSafeFeatureAfterSculptSurviveReplay()
    {
        var multi = SculptingAuthoring.CompileFile(Fixture("Canonical", "BodyState", "surf-x3b-multi-operation-replay.firmament"));
        Assert.True(multi.IsSuccess, Messages(multi.Diagnostics));
        Assert.Equal(["HoleFeature", "HoleFeature", "AddSectionChain"], multi.OutputState!.ConstructionAuthority!.Operations.Select(item => item.OperationKind));
        Assert.True(ConstructionStateReplayer.Replay(multi.OutputState.ConstructionAuthority).IsSuccess);

        var after = SculptingAuthoring.CompileFile(Fixture("Canonical", "BodyState", "surf-x3b-safe-feature-after-add.firmament"));
        Assert.True(after.IsSuccess, Messages(after.Diagnostics));
        Assert.Equal(["AddSectionChain", "HoleFeature"], after.OutputState!.ConstructionAuthority!.Operations.Select(item => item.OperationKind));
        Assert.Contains("GripChain.AttachedSurface", after.OutputState.SemanticInventory.Keys);
        var replay = ConstructionStateReplayer.Replay(after.OutputState.ConstructionAuthority);
        Assert.True(replay.IsSuccess, Messages(replay.Diagnostics));
        Assert.Contains("GripChain.AttachedSurface", replay.OutputState!.SemanticInventory.Keys);
    }

    [Fact]
    public void ReorderedSerializedOperationsFailAtTheBrokenAuthoredPredecessorLink()
    {
        var compiled = SculptingAuthoring.CompileFile(Fixture("Canonical", "BodyState", "surf-x3b-multi-operation-replay.firmament"));
        Assert.True(compiled.IsSuccess, Messages(compiled.Diagnostics));
        var authority = compiled.OutputState!.ConstructionAuthority!;
        var operations = authority.Operations.ToArray();
        var reordered = authority with { Operations = [operations[0], operations[2], operations[1]] };

        var replay = ConstructionStateReplayer.Replay(reordered);

        Assert.False(replay.IsSuccess);
        Assert.Null(replay.OutputState);
        Assert.Equal(operations[0].OutputStateId, replay.AuthoritativePredecessor!.StateId);
        Assert.Equal(operations[2].OperationId, replay.FailedOperation!.OperationId);
        Assert.Contains(replay.Diagnostics, item => item.Code == "bodystate-operation-order-invalid");
        Assert.Contains(replay.Diagnostics, item => item.Code == "bodystate-operation-replay-failed");
    }

    [Fact]
    public void WiderUpstreamBaseRebindsRelativeAttachmentSemanticallyAndMissingSupportInvalidates()
    {
        var compiled = SculptingAuthoring.CompileFile(Fixture("Canonical", "BodyState", "surf-x3b-add-section-chain-grip.firmament"));
        var authority = compiled.OutputState!.ConstructionAuthority!;
        var housing = Assert.IsType<HousingBaseConstruction>(authority.Base);
        var wider = authority with { Base = housing with { Housing = housing.Housing with { Width = 90, CrownWidth = 90 } } };
        var replay = ConstructionStateReplayer.Replay(wider, "WiderGrip");
        Assert.True(replay.IsSuccess, Messages(replay.Diagnostics));
        Assert.NotEqual(compiled.OutputState.StateId, replay.OutputState!.StateId);
        Assert.Equal("AddSectionChain", Assert.Single(replay.OutputState.ConstructionAuthority!.Operations).OperationKind);

        var add = authority.Operations.Single();
        var invalidPayload = ((AddSectionChainOperation)add.Payload) with
        {
            Attachment = ((AddSectionChainOperation)add.Payload).Attachment with { SupportRegion = "DeletedSupport" },
            MayModify = ["DeletedSupport"]
        };
        var invalid = authority with { Operations = [add with { OperationId = invalidPayload.StableId, OperationKind = invalidPayload.OperationKind, Payload = invalidPayload }] };
        var failed = ConstructionStateReplayer.Replay(invalid);
        Assert.False(failed.IsSuccess); Assert.NotNull(failed.AuthoritativePredecessor);
        Assert.Contains(failed.Diagnostics, item => item.Code == "bodystate-operation-support-missing");
    }

    private static BodyState Base()
    {
        var result = SculptedHousingFactory.CreateBase("Base", 80, 40, 20,
            [new("MountA", -25, -12, 4), new("MountB", 25, 12, 4)]);
        Assert.True(result.IsSuccess, Messages(result.Diagnostics)); return result.OutputState!;
    }

    private static SectionChain AdditiveChain() => Chain("GripChain", SectionTermination.Open, SectionTermination.Cap,
        ("Attach", 0d, 40d, 20d, 10d), ("Neck", 10d, 34d, 18d, 10d), ("PalmFront", 22d, 30d, 16d, 10d),
        ("PalmRear", 36d, 24d, 14d, 10d), ("Tail", 48d, 14d, 10d, 10d));

    private static SectionChain DuctChain() => Chain("DuctChain", SectionTermination.Open, SectionTermination.Open,
        ("West", -40d, 10d, 8d, 10d), ("Entry", -20d, 13d, 9d, 10d), ("Center", 0d, 12d, 10d, 10d),
        ("Exit", 20d, 9d, 7d, 10d), ("East", 40d, 11d, 8d, 10d));

    private static SectionChain Chain(string id, SectionTermination start, SectionTermination end,
        params (string Id, double X, double Width, double Height, double Z)[] stations)
    {
        var sections = stations.Select(station => new Section(station.Id,
            SectionFrame.Create(new(station.X, 0, station.Z), new(0, 1, 0), new(0, 0, 1)), Profile(station.Id, station.Width, station.Height))).ToArray();
        var correspondence = sections.Zip(sections.Skip(1), (a, b) => new AdjacentSectionCorrespondence(a.SectionId, b.SectionId,
            SpanIds.Select(span => new SectionSpanCorrespondence(span, span)).ToArray())).ToArray();
        return new(id, sections, correspondence, SectionTransitionPolicy.Ruled, start, end);
    }

    private static SectionProfile Profile(string id, double width, double height)
    {
        var w = width / 2d; var h = height / 2d;
        var points = new[] { new SectionPoint2D(-w, -h), new(w, -h), new(w, h), new(-w, h) };
        return new(id + ".Profile", Enumerable.Range(0, 4).Select(index => new SectionProfileSpan(SpanIds[index],
            new SectionProfileCurve.Line(points[index], points[(index + 1) % 4]) as SectionProfileCurve)).ToArray(), SpanIds[0]);
    }

    private static readonly string[] SpanIds = ["South", "East", "North", "West"];
    private static IReadOnlyList<PreservationContract> Preserve() =>
        [new(SculptedHousingFactory.BottomMountingInterface, PreservationMode.ExactGeometry), new(SculptedHousingFactory.MountingHolePattern, PreservationMode.PatternPlacementAndDiameter)];
    private static IReadOnlyList<SculptRequirement> Requirements() => [SculptRequirement.ClosedManifold, SculptRequirement.OrientationConsistency, SculptRequirement.NoSelfIntersection];
    private static string Messages(IEnumerable<SculptDiagnostic> diagnostics) => string.Join(" | ", diagnostics.Select(item => $"{item.Code}:{item.Message}"));
    private static string Fixture(params string[] parts) => Path.Combine([Root(), "fixtures", .. parts]);
    private static string Root() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent; return directory!.FullName; }
}
