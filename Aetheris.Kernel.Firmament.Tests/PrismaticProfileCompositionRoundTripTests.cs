using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class PrismaticProfileCompositionRoundTripTests
{
    [Fact]
    public void SemanticCapsuleSlot_LowersToExactProfileAndPublishesStableDescendants()
    {
        var source = File.ReadAllText(CompositionFixture(Path.Combine("valid", "semantic-capsule-slot-through.firmament")));
        var parsed = PrismaticProfileCompositionParser.Parse(source);
        Assert.Empty(parsed.Diagnostics);
        var stack = Assert.IsType<PrismaticSectionStackConstruction>(PrismaticSectionStackCompiler.Normalize(parsed, out var diagnostics));
        Assert.Empty(diagnostics);
        var slot = Assert.Single(stack.Feature.CapsuleSlots!);
        Assert.Equal(20d, slot.Radius); Assert.Equal(40d, slot.StraightSpan);
        var profile = parsed.Profiles[slot.ProfileReference];
        Assert.Equal(2, profile.Loops.Single().Segments.Count(x => x.Geometry is LineArcLineSegment2D));
        Assert.Equal(2, profile.Loops.Single().Segments.Count(x => x.Geometry is LineArcCircularArc2D));
        Assert.Equal(20d * 40d + Math.PI * 20d * 20d, 2056.6370614359173d, 8);
        Assert.Equal((200d * 100d - (40d * 40d + Math.PI * 20d * 20d)) * 20d, stack.AnalyticVolume, 8);
        var emitted = PrismaticSectionStackEmitter.Emit(stack);
        Assert.NotNull(emitted.Body); Assert.NotNull(emitted.Correspondence);
        Assert.Contains(emitted.Correspondence!.Descendants, x => x.Role == SemanticTopologyRole.SlotEntryLoop);
        Assert.Equal(2, emitted.Correspondence.Descendants.Count(x => x.Role == SemanticTopologyRole.SlotStraightWallFace));
        Assert.Equal(2, emitted.Correspondence.Descendants.Count(x => x.Role == SemanticTopologyRole.SlotEndWallFace));
    }

    [Fact]
    public void SemanticRoundedRectangleSlot_LowersToExactProfileAndPublishesStableDescendants()
    {
        var source = File.ReadAllText(CompositionFixture(Path.Combine("valid", "semantic-rounded-rectangle-slot-through.firmament")));
        var parsed = PrismaticProfileCompositionParser.Parse(source);
        Assert.Empty(parsed.Diagnostics);
        var stack = Assert.IsType<PrismaticSectionStackConstruction>(PrismaticSectionStackCompiler.Normalize(parsed, out var diagnostics));
        Assert.Empty(diagnostics);
        var slot = Assert.Single(stack.Feature.RoundedRectangleSlots!);
        Assert.Equal(10d, slot.CornerRadius);
        var segments = parsed.Profiles[slot.ProfileReference].Loops.Single().Segments;
        Assert.Equal(4, segments.Count(x => x.Geometry is LineArcLineSegment2D));
        Assert.Equal(4, segments.Count(x => x.Geometry is LineArcCircularArc2D));
        var expectedArea = 80d * 40d - (4d - Math.PI) * 10d * 10d;
        Assert.Equal((200d * 100d - expectedArea) * 20d, stack.AnalyticVolume, 8);
        var emitted = PrismaticSectionStackEmitter.Emit(stack);
        Assert.Equal(4, emitted.Correspondence!.Descendants.Count(x => x.Role == SemanticTopologyRole.SlotStraightWallFace));
        Assert.Equal(4, emitted.Correspondence.Descendants.Count(x => x.Role == SemanticTopologyRole.SlotEndWallFace));
    }

    [Theory]
    [InlineData("add-overlapped-by-remove.firmament", 418d, 4090d, 1)]
    [InlineData("overlapping-removes.firmament", 304d, 5520d, 1)]
    [InlineData("shared-boundary-adds.firmament", 600d, 5000d, 0)]
    [InlineData("crossing-removal-notch.firmament", 352d, 3760d, 0)]
    public void RemainingMaterialPolicies_AgreeAcrossArrangementBrepStepAndM8(string fixture, double finalArea, double volume, int holes)
    {
        var source = File.ReadAllText(CompositionFixture(fixture));
        var parsed = PrismaticProfileCompositionParser.Parse(source);
        Assert.Empty(parsed.Diagnostics);
        var stack = Assert.IsType<PrismaticSectionStackConstruction>(PrismaticSectionStackCompiler.Normalize(parsed, out var diagnostics));
        Assert.Empty(diagnostics);
        var finalSlab = stack.Slabs.OrderBy(x => x.To).Last();
        Assert.Equal(finalArea, PrismaticSectionStackCompiler.Area(finalSlab.Region), 8);
        Assert.Equal(holes, finalSlab.Region.Holes.Count);
        Assert.Equal(volume, stack.AnalyticVolume, 8);

        var emitted = PrismaticSectionStackEmitter.Emit(stack);
        var body = Assert.IsType<Aetheris.Kernel.Core.Brep.BrepBody>(emitted.Body);
        var inMemory = BrepMassProperties.Evaluate(body);
        Assert.NotEqual(BrepMassPropertiesStatus.Unavailable, inMemory.Status);
        Assert.InRange(Math.Abs(inMemory.AbsoluteVolume - volume), 0d, 0.01d);
        var step = Step242Exporter.ExportBody(body, new Step242ExportOptions { BrepExportPreflightMode = BrepExportPreflightMode.Enforce });
        Assert.True(step.IsSuccess, string.Join(" | ", step.Diagnostics.Select(x => x.Message)));
        var imported = Step242Importer.ImportBody(step.Value);
        Assert.True(imported.IsSuccess, string.Join(" | ", imported.Diagnostics.Select(x => x.Message)));
        var m8 = BrepMassProperties.Evaluate(imported.Value);
        Assert.NotEqual(BrepMassPropertiesStatus.Unavailable, m8.Status);
        Assert.InRange(Math.Abs(m8.AbsoluteVolume - volume), 0d, 0.01d);
    }

    [Fact]
    public void AddOverlappedByRemove_OperationEnumerationDoesNotChangeNormalizedMaterial()
    {
        var source = File.ReadAllText(CompositionFixture("add-overlapped-by-remove.firmament"));
        var reversed = source.Replace(
            "  Add Pad { Profile: AddProfile; From: 5mm; To: 10mm; Role: Pad }\n  Remove Relief { Profile: RemoveProfile; From: 5mm; To: 10mm; Role: Relief }",
            "  Remove Relief { Profile: RemoveProfile; From: 5mm; To: 10mm; Role: Relief }\n  Add Pad { Profile: AddProfile; From: 5mm; To: 10mm; Role: Pad }",
            StringComparison.Ordinal);
        var a = Assert.IsType<PrismaticSectionStackConstruction>(PrismaticSectionStackCompiler.Normalize(PrismaticProfileCompositionParser.Parse(source), out var da));
        var b = Assert.IsType<PrismaticSectionStackConstruction>(PrismaticSectionStackCompiler.Normalize(PrismaticProfileCompositionParser.Parse(reversed), out var db));
        Assert.Empty(da); Assert.Empty(db);
        Assert.Equal(a.AnalyticVolume, b.AnalyticVolume, 10);
        Assert.Equal(a.Slabs.Select(x => PrismaticSectionStackCompiler.Area(x.Region)), b.Slabs.Select(x => PrismaticSectionStackCompiler.Area(x.Region)));
        Assert.Equal(a.Slabs.Select(x => x.Region.Holes.Count), b.Slabs.Select(x => x.Region.Holes.Count));
    }

    [Theory]
    [InlineData("Anchor: [0mm, 0mm, 0mm]", "Anchor: [1mm, 0mm, 0mm]", "compose-placement-unsupported-nonzero-anchor")]
    [InlineData("Axis: +Z", "Axis: -Z", "compose-placement-unsupported-orientation")]
    public void ExplicitComposePlacement_RejectsUnsupportedTransformInsteadOfIgnoringIt(string before, string after, string expected)
    {
        var source = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "testdata", "firmament", "reconstructions", "nist_ctc_01", "ctc01_prismatic_blockout_x2.firmament")));
        var parsed = PrismaticProfileCompositionParser.Parse(source.Replace(before, after, StringComparison.Ordinal));
        Assert.Null(parsed.Feature);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.Contains(expected, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("point-only-tangent-connection.firmament", "point-only-tangent-or-zero-width-ligament")]
    [InlineData("zero-width-ligament.firmament", "point-only-tangent-or-zero-width-ligament")]
    [InlineData("contradictory-coincident-add-remove.firmament", "contradictory-coincident-add-remove-boundary")]
    [InlineData("ambiguous-tangent-crossing.firmament", "ambiguous-tangent-crossing")]
    [InlineData("dangling-arrangement-fragment.firmament", "endpoint mismatch")]
    [InlineData("disconnected-final-material.firmament", "disconnected-or-invalid-material")]
    [InlineData("unresolved-angular-ordering.firmament", "unresolved-angular-order")]
    public void InvalidMaterialPolicies_RejectBeforeBrepEmission(string fixture, string expectedDiagnostic)
    {
        var parsed = PrismaticProfileCompositionParser.Parse(File.ReadAllText(CompositionFixture(Path.Combine("invalid", fixture))));
        var stack = PrismaticSectionStackCompiler.Normalize(parsed, out var diagnostics);
        Assert.Null(stack);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Contains(expectedDiagnostic, StringComparison.Ordinal));
    }

    [Fact]
    public void Ctc01Blockout_AllAuthoritativeFacesTessellateForM8()
    {
        var source = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "testdata", "firmament", "reconstructions", "nist_ctc_01", "ctc01_prismatic_blockout_x2.firmament")));
        var parsed = PrismaticProfileCompositionParser.Parse(source);
        var stack = Assert.IsType<PrismaticSectionStackConstruction>(PrismaticSectionStackCompiler.Normalize(parsed, out var diagnostics));
        Assert.Empty(diagnostics);
        var emitted = PrismaticSectionStackEmitter.Emit(stack);
        var body = Assert.IsType<Aetheris.Kernel.Core.Brep.BrepBody>(emitted.Body);
        Assert.Equal(2, Assert.Single(stack.Transitions, transition => transition.Level == -60d).DownwardRegions.Count);
        var mesh = BrepDisplayTessellator.Tessellate(body, DisplayTessellationOptions.Default);
        Assert.True(mesh.IsSuccess, string.Join(" | ", mesh.Diagnostics.Select(x => x.Message)));
        var empty = body.Topology.Faces.Where(face => !mesh.Value.FacePatches.Any(patch => patch.FaceId == face.Id && patch.TriangleIndices.Count >= 3)).Select(face => face.Id.Value).ToArray();
        Assert.True(empty.Length == 0, $"empty faces={string.Join(',', empty)}; diagnostics={string.Join(" | ", mesh.Diagnostics.Select(x => x.Message))}");
        var m8 = BrepMassProperties.Evaluate(body);
        Assert.NotEqual(BrepMassPropertiesStatus.Unavailable, m8.Status);
        Assert.InRange(Math.Abs(m8.AbsoluteVolume - stack.AnalyticVolume), 0d, 5d);
    }

    [Fact]
    public void Ctc01X4_MultipleCurvedInnerLoopsTessellateAndRespectVoidOrientation()
    {
        var source = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "testdata", "firmament", "reconstructions", "nist_ctc_01", "ctc01_prismatic_blockout_x4.firmament")));
        var parsed = PrismaticProfileCompositionParser.Parse(source);
        var stack = Assert.IsType<PrismaticSectionStackConstruction>(PrismaticSectionStackCompiler.Normalize(parsed, out var diagnostics));
        Assert.Empty(diagnostics);

        var emitted = PrismaticSectionStackEmitter.Emit(stack);
        var body = Assert.IsType<Aetheris.Kernel.Core.Brep.BrepBody>(emitted.Body);
        var holeWalls = emitted.Correspondence!.Descendants
            .Where(descendant => descendant.Role == SemanticTopologyRole.HoleWallFace)
            .Select(descendant => descendant.Face!.Value)
            .Distinct()
            .ToArray();
        Assert.Equal(32, holeWalls.Length);
        Assert.All(holeWalls, face => Assert.False(body.Bindings.FaceBindings.Single(binding => binding.FaceId == face).SameSense));

        var m8 = BrepMassProperties.Evaluate(body);
        Assert.NotEqual(BrepMassPropertiesStatus.Unavailable, m8.Status);
        Assert.True(m8.IsEnclosed, string.Join(" | ", m8.Diagnostics));
        Assert.True(m8.IsOrientationConsistent, string.Join(" | ", m8.Diagnostics));
        Assert.InRange(Math.Abs(m8.AbsoluteVolume - stack.AnalyticVolume), 0d, m8.ErrorBound ?? 0d);
    }

    [Fact]
    public void MixedLineArcAdditiveOverlap_PreservesAnalyticCurvesAcrossStepRoundTrip()
    {
        var source = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", "FirmamentV2", "ProfileComposition", "mixed-line-arc-additive-overlap.firmament")));
        var parsed = PrismaticProfileCompositionParser.Parse(source);
        Assert.Empty(parsed.Diagnostics);
        var stack = Assert.IsType<PrismaticSectionStackConstruction>(PrismaticSectionStackCompiler.Normalize(parsed, out var diagnostics));
        Assert.Empty(diagnostics);
        var top = Assert.Single(stack.Slabs.Where(x => x.From == 10d && x.To == 15d));
        Assert.Equal(458.72298071147134d, PrismaticSectionStackCompiler.Area(top.Region), 8);
        Assert.NotNull(top.Arrangement);
        Assert.Equal(4, top.Region.Outer.Loops[0].Segments.Count(x => x.Geometry is LineArcCircularArc2D));
        Assert.True(top.Arrangement!.AtomicFragments.Count > top.Arrangement.RetainedBoundaryFragmentCount);
        Assert.Contains(top.Arrangement.AtomicFragments, x => x.Source.Segment == "ArcEast" && x.StableId.EndsWith("part1", StringComparison.Ordinal));

        var emitted = PrismaticSectionStackEmitter.Emit(stack);
        Assert.DoesNotContain(emitted.Diagnostics, x => x.Contains("rejected", StringComparison.Ordinal));
        var body = Assert.IsType<Aetheris.Kernel.Core.Brep.BrepBody>(emitted.Body);
        var inMemory = BrepMassProperties.Evaluate(body);
        Assert.True(inMemory.IsEnclosed, string.Join(" | ", inMemory.Diagnostics));
        Assert.InRange(Math.Abs(inMemory.AbsoluteVolume - stack.AnalyticVolume), 0d, 0.01d);
        Assert.Equal(4, body.Geometry.Surfaces.Count(x => x.Value.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cylinder));
        var step = Step242Exporter.ExportBody(body, new Step242ExportOptions { BrepExportPreflightMode = BrepExportPreflightMode.Enforce });
        Assert.True(step.IsSuccess, string.Join(" | ", step.Diagnostics.Select(x => x.Message)));
        Assert.DoesNotContain("B_SPLINE", step.Value, StringComparison.OrdinalIgnoreCase);
        var reimported = Step242Importer.ImportBody(step.Value);
        Assert.True(reimported.IsSuccess, string.Join(" | ", reimported.Diagnostics.Select(x => x.Message)));
        var reimportedMass = BrepMassProperties.Evaluate(reimported.Value);
        Assert.True(reimportedMass.IsEnclosed, string.Join(" | ", reimportedMass.Diagnostics));
        Assert.InRange(Math.Abs(reimportedMass.AbsoluteVolume - stack.AnalyticVolume), 0d, 0.01d);
    }

    [Theory]
    [InlineData("additive", 6400d, 480d, 0)]
    [InlineData("removal", 5520d, 304d, 1)]
    public void ProperOverlaps_NormalizeToOneExactSectionStack(string kind, double expectedVolume, double expectedTopArea, int expectedHoles)
    {
        var parsed = PrismaticProfileCompositionParser.Parse(OverlapSource(kind));
        Assert.Empty(parsed.Diagnostics);
        var stack = Assert.IsType<PrismaticSectionStackConstruction>(PrismaticSectionStackCompiler.Normalize(parsed, out var diagnostics));
        Assert.Empty(diagnostics);
        var top = Assert.Single(stack.Slabs.Where(x => x.From == 10d && x.To == 15d));
        Assert.Equal(expectedTopArea, PrismaticSectionStackCompiler.Area(top.Region), 6);
        Assert.Equal(expectedHoles, top.Region.Holes.Count);
        Assert.NotNull(top.Arrangement);
        Assert.True(top.Arrangement!.IntersectionVertices.Count > 0);
        Assert.True(top.Arrangement.RetainedBoundaryFragmentCount < top.Arrangement.AtomicFragments.Count || top.Arrangement.CoincidentFragmentCount > 0);
        Assert.Equal(expectedVolume, stack.AnalyticVolume, 6);

        var emitted = PrismaticSectionStackEmitter.Emit(stack);
        var body = Assert.IsType<Aetheris.Kernel.Core.Brep.BrepBody>(emitted.Body);
        Assert.Equal(expectedVolume, BrepMassProperties.Evaluate(body).AbsoluteVolume, 6);
        var step = Step242Exporter.ExportBody(body, new Step242ExportOptions { BrepExportPreflightMode = BrepExportPreflightMode.Enforce });
        Assert.True(step.IsSuccess, string.Join(" | ", step.Diagnostics.Select(x => x.Message)));
        var reimported = Step242Importer.ImportBody(step.Value);
        Assert.True(reimported.IsSuccess, string.Join(" | ", reimported.Diagnostics.Select(x => x.Message)));
        Assert.Equal(expectedVolume, BrepMassProperties.Evaluate(reimported.Value).AbsoluteVolume, 6);
    }

    [Theory]
    [InlineData("shallow-pocket", 3808d)]
    [InlineData("through-cut", 3640d)]
    public void NestedRect2Composition_PreservesAnalyticVolumeAcrossStepRoundTrip(string kind, double expectedVolume)
    {
        var parsed = PrismaticProfileCompositionParser.Parse(Source(kind));
        Assert.Empty(parsed.Diagnostics);

        var stack = Assert.IsType<PrismaticSectionStackConstruction>(PrismaticSectionStackCompiler.Normalize(parsed, out var normalizeDiagnostics));
        Assert.Empty(normalizeDiagnostics);
        Assert.Equal(expectedVolume, stack.AnalyticVolume, 6);

        var emitted = PrismaticSectionStackEmitter.Emit(stack);
        Assert.DoesNotContain(emitted.Diagnostics, d => d.StartsWith("compose-rejected", StringComparison.Ordinal));
        var body = Assert.IsType<Aetheris.Kernel.Core.Brep.BrepBody>(emitted.Body);
        var inMemory = BrepMassProperties.Evaluate(body);
        Assert.True(inMemory.IsEnclosed, string.Join(" | ", inMemory.Diagnostics));
        Assert.Equal(expectedVolume, inMemory.AbsoluteVolume, 6);

        var step = Step242Exporter.ExportBody(body, new Step242ExportOptions { BrepExportPreflightMode = BrepExportPreflightMode.Enforce });
        Assert.True(step.IsSuccess, string.Join(" | ", step.Diagnostics.Select(d => d.Message)));
        var imported = Step242Importer.ImportBody(step.Value);
        Assert.True(imported.IsSuccess, string.Join(" | ", imported.Diagnostics.Select(d => d.Message)));
        var reimported = BrepMassProperties.Evaluate(imported.Value);
        Assert.True(reimported.IsEnclosed, string.Join(" | ", reimported.Diagnostics));
        Assert.Equal(expectedVolume, reimported.AbsoluteVolume, 6);
    }

    [Fact]
    public void SemanticThroughShaftHoles_UseActualHostBoundaryAndPublishTypedCorrespondence()
    {
        var source = """
            Concept Struct Layout On XY {
                Rect2 BaseGuide { Center: [0mm, 0mm]; Size: [30mm, 20mm]; Role: ProfileGuide }
                Rect2 PadGuide { Center: [0mm, 0mm]; Size: [6mm, 6mm]; Role: ProfileGuide }
            }
            Profile BaseProfile Using Layout { Loop Outer {
                Segment South { Trace: BaseGuide.Bottom; From: BaseGuide.BottomLeft; To: BaseGuide.BottomRight }
                Segment East { Trace: BaseGuide.Right; From: BaseGuide.BottomRight; To: BaseGuide.TopRight }
                Segment North { Trace: BaseGuide.Top; From: BaseGuide.TopRight; To: BaseGuide.TopLeft }
                Segment West { Trace: BaseGuide.Left; From: BaseGuide.TopLeft; To: BaseGuide.BottomLeft }
            } }
            Profile PadProfile Using Layout { Loop Outer {
                Segment South { Trace: PadGuide.Bottom; From: PadGuide.BottomLeft; To: PadGuide.BottomRight }
                Segment East { Trace: PadGuide.Right; From: PadGuide.BottomRight; To: PadGuide.TopRight }
                Segment North { Trace: PadGuide.Top; From: PadGuide.TopRight; To: PadGuide.TopLeft }
                Segment West { Trace: PadGuide.Left; From: PadGuide.TopLeft; To: PadGuide.BottomLeft }
            } }
            Struct Composition { Compose Body {
                Base Stock { Profile: BaseProfile; From: 0mm; To: 10mm; Role: Stock }
                Add Pad { Profile: PadProfile; From: 10mm; To: 15mm; Role: Pad }
                Hole<Shaft> LeftMount { Center: [-10mm, 3mm]; Diameter: 4mm; End: ThroughAll; Role: MountingHole }
                Hole<Shaft> RightMount { Center: [10mm, 3mm]; Diameter: 4mm; End: ThroughAll; Role: MountingHole }
            } }
            """;

        var parsed = PrismaticProfileCompositionParser.Parse(source);
        Assert.Empty(parsed.Diagnostics);
        Assert.Equal(2, parsed.Feature!.ShaftHoles!.Count);
        var normalized = PrismaticSectionStackCompiler.Normalize(parsed, out var diagnostics);
        Assert.True(normalized is not null, string.Join(" | ", diagnostics));
        var stack = normalized!;
        Assert.Empty(diagnostics);
        Assert.Equal(2, Assert.Single(stack.Slabs, slab => slab.From == 0d && slab.To == 10d).Region.Holes.Count);
        Assert.Equal(6180d - 80d * Math.PI, stack.AnalyticVolume, 8);

        var emitted = PrismaticSectionStackEmitter.Emit(stack);
        Assert.DoesNotContain(emitted.Diagnostics, diagnostic => diagnostic.StartsWith("compose-rejected", StringComparison.Ordinal));
        var body = Assert.IsType<Aetheris.Kernel.Core.Brep.BrepBody>(emitted.Body);
        var correspondence = Assert.IsType<SemanticTopologyCorrespondence>(emitted.Correspondence);
        const string sourceId = "hole:Body.LeftMount";
        Assert.Single(correspondence.Descendants, descendant => descendant.SourceStableId == sourceId && descendant.Role == SemanticTopologyRole.HoleEntryLoop);
        Assert.Single(correspondence.Descendants, descendant => descendant.SourceStableId == sourceId && descendant.Role == SemanticTopologyRole.HoleExitLoop);
        Assert.Equal(4, correspondence.Descendants.Count(descendant => descendant.SourceStableId == sourceId && descendant.Role == SemanticTopologyRole.HoleWallFace));
        var wallFaceIds = correspondence.Descendants.Where(descendant => descendant.SourceStableId == sourceId && descendant.Role == SemanticTopologyRole.HoleWallFace).Select(descendant => descendant.Face!.Value).ToHashSet();
        var wallDisplayFaces = AnalyticDisplayPacketBuilder.Build(body).AnalyticFaces.Where(face => wallFaceIds.Contains(face.FaceId)).ToArray();
        Assert.Equal(4, wallDisplayFaces.Length);
        Assert.All(wallDisplayFaces, face =>
        {
            Assert.NotNull(face.DomainHint);
            Assert.Equal(Math.PI / 2d, face.DomainHint!.Value.MaxU!.Value - face.DomainHint.Value.MinU!.Value, 8);
            Assert.Equal(10d, face.DomainHint.Value.MaxV!.Value - face.DomainHint.Value.MinV!.Value, 8);
        });
        var entry = SemanticTopologySelectionResolver.Resolve(body, correspondence,
            new("selection:left-entry", "LeftEntry", "Body", [sourceId], SemanticTopologyRole.HoleEntryLoop, SemanticSelectionRequirement.ClosedLoop, "test"));
        Assert.True(entry.Succeeded, string.Join(" | ", entry.Diagnostics));
        Assert.True(entry.IsClosed);
        var entryEdge = body.Topology.Edges.Single(edge => edge.Id == body.Topology.Coedges.First(coedge => coedge.LoopId == entry.Descendants.Single().Loop).EdgeId);
        Assert.True(body.TryGetVertexPoint(entryEdge.StartVertexId, out var entryPoint));
        Assert.Equal(10d, entryPoint.Z, 8);
    }

    private static string Source(string kind) => $$"""
        Concept Struct Layout On XY {
            Rect2 Base { Center: [0mm, 0mm]; Size: [20mm, 20mm]; Role: ProfileGuide }
            Rect2 Inner { Center: [0mm, 0mm]; Size: [{{(kind == "shallow-pocket" ? 8 : 6)}}mm, {{(kind == "shallow-pocket" ? 8 : 6)}}mm]; Role: ProfileGuide }
        }
        Profile Plate Using Layout { Loop Outer {
            Segment South { Trace: Base.Bottom; From: Base.BottomLeft; To: Base.BottomRight }
            Segment East { Trace: Base.Right; From: Base.BottomRight; To: Base.TopRight }
            Segment North { Trace: Base.Top; From: Base.TopRight; To: Base.TopLeft }
            Segment West { Trace: Base.Left; From: Base.TopLeft; To: Base.BottomLeft }
        } }
        Profile Void Using Layout { Loop Outer {
            Segment South { Trace: Inner.Bottom; From: Inner.BottomLeft; To: Inner.BottomRight }
            Segment East { Trace: Inner.Right; From: Inner.BottomRight; To: Inner.TopRight }
            Segment North { Trace: Inner.Top; From: Inner.TopRight; To: Inner.TopLeft }
            Segment West { Trace: Inner.Left; From: Inner.TopLeft; To: Inner.BottomLeft }
        } }
        Struct Composition { Compose Body {
            Base Stock { Profile: Plate; From: 0mm; To: 10mm; Role: Stock }
            Remove Cut { Profile: Void; From: {{(kind == "shallow-pocket" ? 7 : 0)}}mm; To: 10mm; Role: Relief }
        } }
        """;

    private static string OverlapSource(string kind) => $$"""
        Concept Struct Layout On XY {
            Rect2 BaseGuide { Center: [0mm, 0mm]; Size: [20mm, 20mm]; Role: ProfileGuide }
            Rect2 LeftGuide { Center: [-3mm, 10mm]; Size: [10mm, 10mm]; Role: ProfileGuide }
            Rect2 RightGuide { Center: [3mm, 10mm]; Size: [10mm, 10mm]; Role: ProfileGuide }
            Rect2 VoidLeftGuide { Center: [-2mm, 0mm]; Size: [8mm, 8mm]; Role: ProfileGuide }
            Rect2 VoidRightGuide { Center: [2mm, 0mm]; Size: [8mm, 8mm]; Role: ProfileGuide }
        }
        {{RectProfile("BaseProfile", "BaseGuide")}}
        {{RectProfile("LeftProfile", kind == "additive" ? "LeftGuide" : "VoidLeftGuide")}}
        {{RectProfile("RightProfile", kind == "additive" ? "RightGuide" : "VoidRightGuide")}}
        Struct Composition { Compose Body {
            Base Stock { Profile: BaseProfile; From: 0mm; To: 15mm; Role: Stock }
            {{(kind == "additive" ? "Add Left { Profile: LeftProfile; From: 10mm; To: 15mm; Role: Pad }\n            Add Right { Profile: RightProfile; From: 10mm; To: 15mm; Role: Pad }" : "Remove Left { Profile: LeftProfile; From: 10mm; To: 15mm; Role: Relief }\n            Remove Right { Profile: RightProfile; From: 10mm; To: 15mm; Role: Relief }")}}
        } }
        """;

    private static string RectProfile(string profile, string guide) => $$"""
        Profile {{profile}} Using Layout { Loop Outer {
            Segment South { Trace: {{guide}}.Bottom; From: {{guide}}.BottomLeft; To: {{guide}}.BottomRight }
            Segment East { Trace: {{guide}}.Right; From: {{guide}}.BottomRight; To: {{guide}}.TopRight }
            Segment North { Trace: {{guide}}.Top; From: {{guide}}.TopRight; To: {{guide}}.TopLeft }
            Segment West { Trace: {{guide}}.Left; From: {{guide}}.TopLeft; To: {{guide}}.BottomLeft }
        } }
        """;

    private static string CompositionFixture(string name) => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", "FirmamentV2", "ProfileComposition", name));
}
