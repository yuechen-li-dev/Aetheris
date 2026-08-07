using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Firmament;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2ParserTests
{
    [Fact]
    public void FirmamentV2Parser_Box_ParsesModelUnitsAndSolid()
    {
        var result = FirmamentV2Parser.Parse(Source("Primitive/valid/box-v2.valid.firmfixture"));
        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics));
        Assert.NotNull(result.Document);
        var document = result.Document!;
        Assert.Equal("BoxExample", document.ModelName);
        Assert.Equal("mm", document.Units);
        Assert.Equal("base", document.Solid.Name);
        Assert.Equal("Box", document.Solid.RecordType);
        Assert.Equal([10, 8, 6], Box(document.Solid).Size);
    }

    [Fact]
    public void FirmamentV2Parser_Box_LowersToFeatureAir()
    {
        var result = FirmamentFrontendTraceProbe.ParseV2Only(Source("Primitive/valid/box-v2.valid.firmfixture"));
        Assert.True(result.ParseSucceeded, string.Join(", ", result.Diagnostics));
        Assert.Equal("FirmamentV2Parser", result.ParserName);
        Assert.Equal("feature-air", result.FrontendStageReached);
        Assert.NotNull(result.FeatureAir);
        Assert.Equal("CreateBox", result.FeatureAir!.FeatureAirNodeKind);
        Assert.Null(result.ConstructiveAir);
    }

    [Fact]
    public void FirmamentV2Parser_MissingUnits_IsDiagnostic()
    {
        var result = FirmamentV2Parser.Parse(Source("Primitive/invalid/box-v2-missing-units.invalid.firmfixture"));
        Assert.False(result.IsSuccess);
        Assert.Contains(FirmamentV2Parser.MissingUnits, result.Diagnostics);
    }

    [Fact]
    public void FirmamentV2Parser_NegativeSize_IsDegenerateDimension()
    {
        var result = FirmamentV2Parser.Parse(Source("Primitive/invalid/box-v2-negative-size.invalid.firmfixture"));
        Assert.False(result.IsSuccess);
        Assert.Contains(FirmamentV2Parser.DegenerateDimension, result.Diagnostics);
    }

    [Fact]
    public void FirmamentV2Parser_UnknownRecord_IsDiagnostic()
    {
        var result = FirmamentV2Parser.Parse(Source("Primitive/invalid/box-v2-unknown-record.invalid.firmfixture"));
        Assert.False(result.IsSuccess);
        Assert.Contains(FirmamentV2Parser.UnknownRecordType, result.Diagnostics);
    }

    [Fact]
    public void FirmamentV2Parser_DoesNotUseV1Parser()
    {
        var result = FirmamentFrontendTraceProbe.ParseV2Only(Source("Primitive/valid/box-v2.valid.firmfixture"));
        Assert.Equal("FirmamentV2Parser", result.ParserName);
        Assert.Contains("firmament-v2-no-v1-parser", result.Diagnostics);
        Assert.DoesNotContain(result.Diagnostics, d => d.Contains("FirmamentTopLevelParser", StringComparison.Ordinal));
    }

    [Fact]
    public void FirmamentV2Parser_WithBox_ParsesBaseAndDerivedSolid()
    {
        var result = FirmamentV2Parser.Parse(Source("RecordDerivation/valid/box-with-size-variant-v2.valid.firmfixture"));
        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics));
        var document = result.Document!;
        Assert.Equal("BoxVariant", document.ModelName);
        Assert.Equal("mm", document.Units);
        Assert.Equal(2, document.Solids.Count);
        Assert.Equal("base", document.Solids[0].Name);
        Assert.Equal("Box", document.Solids[0].RecordType);
        Assert.Equal([10, 8, 6], Box(document.Solids[0]).Size);
        Assert.Equal("tall", document.Solids[1].Name);
        Assert.Equal("base", document.Solids[1].DerivedFrom);
        Assert.Equal([10, 8, 12], document.Solids[1].Overrides!["size"]);
    }

    [Fact]
    public void FirmamentV2Parser_WithBox_LowersDerivedToFeatureAir()
    {
        var result = FirmamentFrontendTraceProbe.ParseV2Only(Source("RecordDerivation/valid/box-with-size-variant-v2.valid.firmfixture"));
        Assert.True(result.ParseSucceeded, string.Join(", ", result.Diagnostics));
        Assert.Equal("FirmamentV2Parser", result.ParserName);
        Assert.Equal("CreateBox", result.FeatureAir!.FeatureAirNodeKind);
        Assert.Equal(10, result.FeatureAir.SourceDimensions!.Width);
        Assert.Equal(8, result.FeatureAir.SourceDimensions.Depth);
        Assert.Equal(12, result.FeatureAir.SourceDimensions.Height);
        Assert.Equal("tall", result.FirmamentV2!.SolidName);
    }

    [Fact]
    public void FirmamentV2Parser_WithBox_BaseRemainsUnchanged()
    {
        var result = FirmamentV2Parser.Parse(Source("RecordDerivation/valid/box-with-size-variant-v2.valid.firmfixture"));
        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics));
        var document = Assert.IsType<FirmamentV2Document>(result.Document);
        Assert.Equal([10, 8, 6], Box(document.Solids.Single(s => s.Name == "base")).Size);
        Assert.Equal([10, 8, 12], Box(document.Solids.Single(s => s.Name == "tall")).Size);
    }


    [Fact]
    public void FirmamentV2Parser_WithBox_ChainedTwiceLowersWithoutStaleDimensions()
    {
        var result = FirmamentFrontendTraceProbe.ParseV2Only(Source("RecordDerivation/valid/derivation-v2-with-chained-twice-step-verified.valid.firmfixture"));
        Assert.True(result.ParseSucceeded, string.Join(", ", result.Diagnostics));
        Assert.Equal("CreateBox", result.FeatureAir!.FeatureAirNodeKind);
        Assert.Equal(12, result.FeatureAir.SourceDimensions!.Width);
        Assert.Equal(8, result.FeatureAir.SourceDimensions.Depth);
        Assert.Equal(7, result.FeatureAir.SourceDimensions.Height);
        Assert.Equal("taller", result.FirmamentV2!.SolidName);

        var parse = FirmamentV2Parser.Parse(Source("RecordDerivation/valid/derivation-v2-with-chained-twice-step-verified.valid.firmfixture"));
        Assert.True(parse.IsSuccess, string.Join(", ", parse.Diagnostics));
        Assert.Equal([10, 8, 6], parse.Document!.Solids.Single(s => s.Name == "base").Box!.Size);
        Assert.Equal([12, 8, 6], parse.Document.Solids.Single(s => s.Name == "wider").Box!.Size);
        Assert.Equal([12, 8, 7], parse.Document.Solids.Single(s => s.Name == "taller").Box!.Size);
    }


    [Fact]
    public void FirmamentV2Parser_CompositeMultipleHoles_ParseAndLowerAsDistinctAirHoleFeatures()
    {
        var result = FirmamentV2Parser.Parse(Source("Composite/valid/composite-v2-two-independent-holes-step-verified.valid.firmfixture"));
        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics));
        var holes = Assert.Single(result.Document!.ModifyBlocks!).SemanticHoles;
        Assert.Equal(["leftMount", "rightMount"], holes.Select(h => h.Name).ToArray());
        Assert.All(holes, h => Assert.Equal(FirmamentV2SemanticHoleVariant.Shaft, h.Variant));

        var lowered = FirmamentV2SemanticHoleLowering.LowerSemanticHoles(result.Document);
        Assert.Equal(2, lowered.Count);
        Assert.Equal(["base.leftMount", "base.rightMount"], lowered.Select(h => h.FeatureId).ToArray());
        Assert.All(lowered, h => Assert.Equal(AirHoleStackKind.SimpleShaft, h.Stack.Kind));
    }
    [Fact]
    public void FirmamentV2Parser_DerivedCompositeHole_LowersSelectedDerivedSolidAndAirHoleFeature()
    {
        var result = FirmamentV2Parser.Parse(Source("Composite/valid/composite-v2-hole-plus-derived-variant-step-verified.valid.firmfixture"));
        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics));
        var document = result.Document!;
        Assert.Equal([10, 8, 6], document.Solids.Single(s => s.Name == "base").Box!.Size);
        Assert.Equal([12, 8, 6], document.Solids.Single(s => s.Name == "wider").Box!.Size);
        Assert.Equal("wider", document.Solid.Name);

        var modify = Assert.Single(document.ModifyBlocks!);
        Assert.Equal("wider", modify.TargetSolid);
        var hole = Assert.Single(modify.SemanticHoles);
        Assert.Equal(FirmamentV2SemanticHoleVariant.Shaft, hole.Variant);
        Assert.Equal(2, hole.ShaftDiameter);

        var lowered = Assert.Single(FirmamentV2SemanticHoleLowering.LowerSemanticHoles(document));
        Assert.Equal("wider.mount", lowered.FeatureId);
        Assert.Equal("wider", lowered.TargetBodyId);
        Assert.Equal(AirHoleStackKind.SimpleShaft, lowered.Stack.Kind);
        Assert.Equal(nameof(AirHoleFeature), lowered.Provenance.RouteName);
        Assert.Contains("target-solid:wider", lowered.Provenance.Notes);
    }


    [Fact]
    public void FirmamentV2Parser_SemanticFaceAlias_ResolvesBeforeHoleLowering()
    {
        var result = FirmamentV2Parser.Parse(Source("SemanticRefs/valid/semanticref-v2-expose-face-alias-resolves-in-step.valid.firmfixture"));
        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics));
        var hole = Assert.Single(Assert.Single(result.Document!.ModifyBlocks!).SemanticHoles);
        Assert.Equal("top", hole.EntryFace.Source);
        Assert.Equal("Alias", hole.EntryFace.Kind);
        Assert.Equal("+Z", hole.EntryFace.Axis);
        Assert.Equal("face(+Z)", hole.EntryFace.ResolvedSelector);
        Assert.Equal(FirmamentV2SemanticHoleVariant.Shaft, hole.Variant);
    }

    [Fact]
    public void FirmamentV2Parser_SemanticFaceAliasFailure_IsDeterministicDiagnostic()
    {
        const string source = """
model AliasFailure {
    units mm
    solid base: Box {
        size: [10, 8, 6]
        expose { face(+Z) => top }
    }
    modify base {
        hole<shaft> mount {
            on: missingAlias
            center: [0, 0]
            diameter: 2
            end: throughAll
        }
    }
}
""";
        var result = FirmamentV2Parser.Parse(source);
        Assert.False(result.IsSuccess);
        Assert.Contains(FirmamentV2Parser.AliasUnresolved, result.Diagnostics);
    }

    [Fact]
    public void FirmamentV2Parser_WithBox_DegenerateDerivedSize_IsDiagnostic()
    {
        var result = FirmamentV2Parser.Parse(Source("RecordDerivation/invalid/with-degenerate-box-v2.invalid.firmfixture"));
        Assert.False(result.IsSuccess);
        Assert.Contains(FirmamentV2Parser.DegenerateDimension, result.Diagnostics);
    }

    [Fact]
    public void FirmamentV2Parser_WithBox_UnknownField_IsDiagnostic()
    {
        var result = FirmamentV2Parser.Parse(Source("RecordDerivation/invalid/with-unknown-field-v2.invalid.firmfixture"));
        Assert.False(result.IsSuccess);
        Assert.Contains(FirmamentV2Parser.WithFieldNotFound, result.Diagnostics);
    }

    [Fact]
    public void FirmamentV2Parser_WithBox_UndefinedBase_IsDiagnostic()
    {
        var result = FirmamentV2Parser.Parse(Source("RecordDerivation/invalid/with-undefined-base-v2.invalid.firmfixture"));
        Assert.False(result.IsSuccess);
        Assert.Contains(FirmamentV2Parser.NameUnresolved, result.Diagnostics);
    }

    [Fact]
    public void FirmamentV2Parser_WithBox_DuplicateName_IsDiagnostic()
    {
        var result = FirmamentV2Parser.Parse(Source("RecordDerivation/invalid/with-duplicate-solid-name-v2.invalid.firmfixture"));
        Assert.False(result.IsSuccess);
        Assert.Contains(FirmamentV2Parser.DuplicateName, result.Diagnostics);
    }

    [Fact]
    public void FirmamentV2Parser_WithBox_DoesNotUseV1Parser()
    {
        var result = FirmamentFrontendTraceProbe.ParseV2Only(Source("RecordDerivation/valid/box-with-size-variant-v2.valid.firmfixture"));
        Assert.Equal("FirmamentV2Parser", result.ParserName);
        Assert.Contains("firmament-v2-no-v1-parser", result.Diagnostics);
    }

    [Fact]
    public void FirmamentV2Parser_ExposeBoxFaces_ParsesAliases()
    {
        var result = FirmamentV2Parser.Parse(Source("SemanticRefs/valid/named-box-faces-v2.valid.firmfixture"));
        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics));
        var exposures = Box(Assert.IsType<FirmamentV2Document>(result.Document).Solid).Exposures;
        Assert.Equal(4, exposures.Count);
        Assert.Equal(["top", "bottom", "right", "topRim"], exposures.Select(e => e.Alias).ToArray());
        Assert.Equal("FaceRef", exposures.Single(e => e.Alias == "top").RefType);
        Assert.Equal("LoopRef", exposures.Single(e => e.Alias == "topRim").RefType);
    }

    [Fact]
    public void FirmamentV2Parser_ExposeBoxFaces_LowersBoxToFeatureAir()
    {
        var result = FirmamentFrontendTraceProbe.ParseV2Only(Source("SemanticRefs/valid/named-box-faces-v2.valid.firmfixture"));
        Assert.True(result.ParseSucceeded, string.Join(", ", result.Diagnostics));
        Assert.Equal("CreateBox", result.FeatureAir!.FeatureAirNodeKind);
        Assert.Equal(10, result.FeatureAir.SourceDimensions!.Width);
        Assert.Equal(8, result.FeatureAir.SourceDimensions.Depth);
        Assert.Equal(6, result.FeatureAir.SourceDimensions.Height);
        Assert.Equal(4, result.FirmamentV2!.Solids.Single().Exposures.Count);
    }

    [Fact]
    public void FirmamentV2Parser_ExposeBoxFaces_Diagnostics()
    {
        Assert.Contains(FirmamentV2Parser.ExposeAliasDuplicate, FirmamentV2Parser.Parse(Source("SemanticRefs/invalid/duplicate-expose-alias-v2.invalid.firmfixture")).Diagnostics);
        Assert.Contains(FirmamentV2Parser.SelectorAxisInvalid, FirmamentV2Parser.Parse(Source("SemanticRefs/invalid/invalid-face-axis-v2.invalid.firmfixture")).Diagnostics);
        Assert.Contains(FirmamentV2Parser.RawBackendIdReferenceForbidden, FirmamentV2Parser.Parse(Source("SemanticRefs/invalid/raw-brep-id-reference-v2.invalid.firmfixture")).Diagnostics);
        Assert.Contains(FirmamentV2Parser.FatArrowOutsideExpose, FirmamentV2Parser.Parse(Source("SemanticRefs/invalid/fat-arrow-outside-expose-v2.invalid.firmfixture")).Diagnostics);
        Assert.Contains(FirmamentV2Parser.SelectorUnsupported, FirmamentV2Parser.Parse(Source("SemanticRefs/invalid/unsupported-selector-edge-v2.invalid.firmfixture")).Diagnostics);
    }

    [Fact]
    public void FirmamentV2Parser_ExposeBoxFaces_DoesNotUseV1Parser()
    {
        var result = FirmamentFrontendTraceProbe.ParseV2Only(Source("SemanticRefs/valid/named-box-faces-v2.valid.firmfixture"));
        Assert.Equal("FirmamentV2Parser", result.ParserName);
        Assert.Contains("firmament-v2-no-v1-parser", result.Diagnostics);
    }

    [Fact]
    public void FirmamentV2Parser_SideHole_ParsesRegionCutCylinder()
    {
        var result = FirmamentV2Parser.Parse(Source("Region/valid/side-hole-v2.valid.firmfixture"));
        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics));
        var document = result.Document!;
        Assert.Equal("SideHoleV2", document.ModelName);
        Assert.Equal("mm", document.Units);
        Assert.Equal("base", document.Solid.Name);
        Assert.Equal([10, 8, 6], Box(document.Solid).Size);
        var modify = Assert.Single(document.ModifyBlocks!);
        Assert.Equal("base", modify.TargetSolid);
        var region = Assert.Single(modify.Regions);
        Assert.Equal("sideHole", region.Name);
        Assert.Equal("+X", region.Attachment.Axis);
        Assert.Equal("Cut", region.Cut.OperationKind);
        Assert.Equal("Cylinder", region.Cut.Tool.ToolType);
        Assert.Equal(1, region.Cut.Tool.Radius);
        Assert.Equal("-X", region.Cut.Tool.Through.Axis);
    }

    [Fact]
    public void FirmamentV2Parser_SideHole_ProducesSemanticIntent()
    {
        var intent = FirmamentV2Parser.Parse(Source("Region/valid/side-hole-v2.valid.firmfixture")).Document!.SideHoleIntent;
        Assert.NotNull(intent);
        Assert.Equal("base", intent!.TargetSolid);
        Assert.Equal("sideHole", intent.RegionName);
        Assert.Equal("+X", intent.AttachFace);
        Assert.Equal("-X", intent.ThroughFace);
        Assert.Equal("Cylinder", intent.Tool);
        Assert.Equal(1, intent.Radius);
        Assert.Equal("mm", intent.Units);
    }

    [Fact]
    public void FirmamentV2Parser_SideHole_LoweringOutcomeIsTruthful()
    {
        var result = FirmamentFrontendTraceProbe.ParseV2Only(Source("Region/valid/side-hole-v2.valid.firmfixture"));
        Assert.True(result.ParseSucceeded, string.Join(", ", result.Diagnostics));
        Assert.Equal("region-parent-integrated", result.FrontendStageReached);
        Assert.NotNull(result.FirmamentV2!.SemanticIntent);
        Assert.Null(result.FirmamentV2.Blocker);
    }

    [Fact]
    public void FirmamentV2Parser_SideHole_GoldenPathIfReached()
    {
        var result = FirmamentFrontendTraceProbe.ParseV2Only(Source("Region/valid/side-hole-v2.valid.firmfixture"));
        Assert.Equal("Integrated", result.FirmamentV2!.ParentIntegration);
        Assert.Equal("Closed", result.FirmamentV2.ShellClosure);
        Assert.Equal("Succeeded", result.FirmamentV2.StepSmoke);
    }



    [Fact]
    public void FirmamentV2SideHoleCenter_ParsesCenterOffsets()
    {
        foreach (var (fixture, u, v) in new[] { ("Region/valid/side-hole-center-y1-v2.valid.firmfixture", 1d, 0d), ("Region/valid/side-hole-center-z1-v2.valid.firmfixture", 0d, 1d), ("Region/valid/side-hole-center-y1-z1-v2.valid.firmfixture", 1d, 1d) })
        {
            var intent = FirmamentV2Parser.Parse(Source(fixture)).Document!.SideHoleIntent!;
            Assert.Equal(u, intent.CenterU);
            Assert.Equal(v, intent.CenterV);
            Assert.True(intent.CenterExplicit);
            Assert.Equal("face(+X):u=+Y,v=+Z", intent.CenterSelectorFrame);
        }
    }

    [Fact]
    public void FirmamentV2SideHoleCenter_DefaultsToZeroWhenOmitted()
    {
        var intent = FirmamentV2Parser.Parse(Source("Region/valid/side-hole-v2.valid.firmfixture")).Document!.SideHoleIntent!;
        Assert.Equal(0, intent.CenterU);
        Assert.Equal(0, intent.CenterV);
        Assert.False(intent.CenterExplicit);
    }

    [Fact]
    public void FirmamentV2SideHoleCenter_ValidOffsetsReachGoldenPath()
    {
        foreach (var fixture in new[] { "Region/valid/side-hole-center-y1-v2.valid.firmfixture", "Region/valid/side-hole-center-z1-v2.valid.firmfixture", "Region/valid/side-hole-center-y1-z1-v2.valid.firmfixture" })
        {
            var result = FirmamentFrontendTraceProbe.ParseV2Only(Source(fixture));
            Assert.True(result.ParseSucceeded, string.Join(", ", result.Diagnostics));
            Assert.Equal("region-parent-integrated", result.FirmamentV2!.StageReached);
            Assert.Equal("Integrated", result.FirmamentV2.ParentIntegration);
            Assert.Equal("Closed", result.FirmamentV2.ShellClosure);
            Assert.Equal("Succeeded", result.FirmamentV2.StepSmoke);
            Assert.Null(result.FirmamentV2.Blocker);
        }
    }

    [Fact]
    public void FirmamentV2SideHoleCenter_InvalidDiagnostics()
    {
        Assert.Contains(FirmamentV2Parser.SideHoleCenterExceedsClearance, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-center-y-boundary-v2.invalid.firmfixture")).Diagnostics);
        Assert.Contains(FirmamentV2Parser.SideHoleCenterExceedsClearance, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-center-z-boundary-v2.invalid.firmfixture")).Diagnostics);
        Assert.Contains(FirmamentV2Parser.CylinderCenterArityInvalid, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-center-arity-one-v2.invalid.firmfixture")).Diagnostics);
        Assert.Contains(FirmamentV2Parser.CylinderCenterArityInvalid, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-center-arity-three-v2.invalid.firmfixture")).Diagnostics);
    }

    [Fact]
    public void FirmamentV2SideHoleRadius_ParsesRadiusVariations()
    {
        Assert.Equal(0.5, FirmamentV2Parser.Parse(Source("Region/valid/side-hole-radius-0_5-v2.valid.firmfixture")).Document!.SideHoleIntent!.Radius);
        Assert.Equal(1.5, FirmamentV2Parser.Parse(Source("Region/valid/side-hole-radius-1_5-v2.valid.firmfixture")).Document!.SideHoleIntent!.Radius);
    }

    [Fact]
    public void FirmamentV2SideHoleRadius_ValidVariationsReachGoldenPath()
    {
        foreach (var fixture in new[] { "Region/valid/side-hole-radius-0_5-v2.valid.firmfixture", "Region/valid/side-hole-radius-1_5-v2.valid.firmfixture" })
        {
            var result = FirmamentFrontendTraceProbe.ParseV2Only(Source(fixture));
            Assert.True(result.ParseSucceeded, string.Join(", ", result.Diagnostics));
            Assert.Equal("region-parent-integrated", result.FirmamentV2!.StageReached);
            Assert.Equal("Integrated", result.FirmamentV2.ParentIntegration);
            Assert.Equal("Closed", result.FirmamentV2.ShellClosure);
            Assert.Equal("Succeeded", result.FirmamentV2.StepSmoke);
            Assert.Null(result.FirmamentV2.Blocker);
        }
    }

    [Fact]
    public void FirmamentV2SideHoleRadius_InvalidRadiusDiagnostics()
    {
        Assert.Contains(FirmamentV2Parser.CylinderRadiusInvalid, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-radius-zero-v2.invalid.firmfixture")).Diagnostics);
        Assert.Contains(FirmamentV2Parser.CylinderRadiusInvalid, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-radius-negative-v2.invalid.firmfixture")).Diagnostics);
        Assert.Contains(FirmamentV2Parser.SideHoleRadiusExceedsClearance, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-radius-too-large-v2.invalid.firmfixture")).Diagnostics);
    }

    [Fact]
    public void FirmamentV2Parser_SideHole_InvalidAttachFaceDiagnostic()
    {
        Assert.Contains(FirmamentV2Parser.SideHoleOnlyPlusXMinusXSupported, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-invalid-attach-v2.invalid.firmfixture")).Diagnostics);
    }

    [Fact]
    public void FirmamentV2Parser_SideHole_InvalidThroughFaceDiagnostic()
    {
        Assert.Contains(FirmamentV2Parser.SideHoleOnlyPlusXMinusXSupported, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-invalid-through-v2.invalid.firmfixture")).Diagnostics);
    }

    [Fact]
    public void FirmamentV2Parser_SideHole_InvalidRadiusDiagnostic()
    {
        Assert.Contains(FirmamentV2Parser.CylinderRadiusInvalid, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-invalid-radius-v2.invalid.firmfixture")).Diagnostics);
    }

    [Fact]
    public void FirmamentV2Parser_SideHole_UnknownModifyTargetDiagnostic()
    {
        Assert.Contains(FirmamentV2Parser.ModifyTargetUnresolved, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-unknown-modify-target-v2.invalid.firmfixture")).Diagnostics);
    }

    [Fact]
    public void FirmamentV2Parser_SideHole_DoesNotUseV1Parser()
    {
        var result = FirmamentFrontendTraceProbe.ParseV2Only(Source("Region/valid/side-hole-v2.valid.firmfixture"));
        Assert.Equal("FirmamentV2Parser", result.ParserName);
        Assert.Contains("firmament-v2-no-v1-parser", result.Diagnostics);
    }


    [Fact]
    public void FirmamentV2SideHoleReverseX_ParsesDirectReverseRoute()
    {
        var intent = FirmamentV2Parser.Parse(Source("Region/valid/side-hole-reverse-x-v2.valid.firmfixture")).Document!.SideHoleIntent!;
        Assert.Equal("face(-X)", intent.AttachTargetSource);
        Assert.Equal("DirectSelector", intent.AttachTargetKind);
        Assert.Equal("-X", intent.AttachFace);
        Assert.Equal("face(+X)", intent.ThroughTargetSource);
        Assert.Equal("DirectSelector", intent.ThroughTargetKind);
        Assert.Equal("+X", intent.ThroughFace);
        Assert.Equal("-X->+X", intent.Route);
        Assert.Equal("face(-X):u=+Y,v=+Z", intent.CenterSelectorFrame);
    }

    [Fact]
    public void FirmamentV2SideHoleReverseX_DirectReachesGoldenPath()
    {
        var result = FirmamentFrontendTraceProbe.ParseV2Only(Source("Region/valid/side-hole-reverse-x-v2.valid.firmfixture"));
        Assert.True(result.ParseSucceeded, string.Join(", ", result.Diagnostics));
        Assert.Equal("region-parent-integrated", result.FirmamentV2!.StageReached);
        Assert.Equal("Integrated", result.FirmamentV2.ParentIntegration);
        Assert.Equal("Closed", result.FirmamentV2.ShellClosure);
        Assert.Equal("Succeeded", result.FirmamentV2.StepSmoke);
        Assert.Null(result.FirmamentV2.Blocker);
    }

    [Fact]
    public void FirmamentV2SideHoleReverseX_ParsesAliasReverseRoute()
    {
        var doc = FirmamentV2Parser.Parse(Source("Region/valid/side-hole-aliases-reverse-x-v2.valid.firmfixture")).Document!;
        Assert.Contains(Box(doc.Solid).Exposures, e => e.Alias == "left" && e.Selector == "face(-X)");
        Assert.Contains(Box(doc.Solid).Exposures, e => e.Alias == "right" && e.Selector == "face(+X)");
        var region = Assert.Single(Assert.Single(doc.ModifyBlocks!).Regions);
        Assert.Equal("left", region.Attachment.Source);
        Assert.Equal("face(-X)", region.Attachment.ResolvedSelector);
        Assert.Equal("right", region.Cut.Tool.Through.Source);
        Assert.Equal("face(+X)", region.Cut.Tool.Through.ResolvedSelector);
        Assert.Equal("-X->+X", doc.SideHoleIntent!.Route);
    }

    [Fact]
    public void FirmamentV2SideHoleReverseX_AliasReachesGoldenPath()
    {
        var result = FirmamentFrontendTraceProbe.ParseV2Only(Source("Region/valid/side-hole-aliases-reverse-x-v2.valid.firmfixture"));
        Assert.True(result.ParseSucceeded, string.Join(", ", result.Diagnostics));
        Assert.Equal("region-parent-integrated", result.FirmamentV2!.StageReached);
        Assert.Equal("Integrated", result.FirmamentV2.ParentIntegration);
        Assert.Equal("Closed", result.FirmamentV2.ShellClosure);
        Assert.Equal("Succeeded", result.FirmamentV2.StepSmoke);
        Assert.Null(result.FirmamentV2.Blocker);
    }

    [Fact]
    public void FirmamentV2SideHoleReverseX_InvalidRoutesRejected()
    {
        Assert.Contains(FirmamentV2Parser.SideHoleSameFaceUnsupported, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-same-face-x-v2.invalid.firmfixture")).Diagnostics);
        Assert.Contains(FirmamentV2Parser.SideHoleRouteUnsupported, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-mixed-axis-x-to-y-v2.invalid.firmfixture")).Diagnostics);
        Assert.Contains(FirmamentV2Parser.SideHoleRouteUnsupported, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-mixed-axis-z-to-x-v2.invalid.firmfixture")).Diagnostics);
        Assert.Contains(FirmamentV2Parser.SideHoleRouteUnsupported, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-alias-reverse-x-wrong-through-v2.invalid.firmfixture")).Diagnostics);
    }


    [Fact]
    public void FirmamentV2SideHoleAliases_ParsesExposeAndAliasTargets()
    {
        var doc = FirmamentV2Parser.Parse(Source("Region/valid/side-hole-aliases-v2.valid.firmfixture")).Document!;
        Assert.Contains(Box(doc.Solid).Exposures, e => e.Alias == "right" && e.Selector == "face(+X)");
        Assert.Contains(Box(doc.Solid).Exposures, e => e.Alias == "left" && e.Selector == "face(-X)");
        var region = Assert.Single(Assert.Single(doc.ModifyBlocks!).Regions);
        Assert.Equal("right", region.Attachment.Source);
        Assert.Equal("Alias", region.Attachment.Kind);
        Assert.Equal("left", region.Cut.Tool.Through.Source);
        Assert.Equal("Alias", region.Cut.Tool.Through.Kind);
    }

    [Fact]
    public void FirmamentV2SideHoleAliases_ResolvesAliasesToFaceSelectors()
    {
        var region = Assert.Single(Assert.Single(FirmamentV2Parser.Parse(Source("Region/valid/side-hole-aliases-v2.valid.firmfixture")).Document!.ModifyBlocks!).Regions);
        Assert.Equal("face(+X)", region.Attachment.ResolvedSelector);
        Assert.Equal("+X", region.Attachment.Axis);
        Assert.Equal("FaceRef", region.Attachment.RefType);
        Assert.Equal("face(-X)", region.Cut.Tool.Through.ResolvedSelector);
        Assert.Equal("-X", region.Cut.Tool.Through.Axis);
        Assert.Equal("FaceRef", region.Cut.Tool.Through.RefType);
    }

    [Fact]
    public void FirmamentV2SideHoleAliases_ProducesSemanticIntent()
    {
        var intent = FirmamentV2Parser.Parse(Source("Region/valid/side-hole-aliases-v2.valid.firmfixture")).Document!.SideHoleIntent!;
        Assert.Equal("right", intent.AttachTargetSource);
        Assert.Equal("Alias", intent.AttachTargetKind);
        Assert.Equal("+X", intent.AttachFace);
        Assert.Equal("left", intent.ThroughTargetSource);
        Assert.Equal("Alias", intent.ThroughTargetKind);
        Assert.Equal("-X", intent.ThroughFace);
        Assert.Equal(1, intent.Radius);
        Assert.Equal(1, intent.CenterU);
        Assert.Equal(0, intent.CenterV);
    }

    [Fact]
    public void FirmamentV2SideHoleAliases_ReachGoldenPath()
    {
        var result = FirmamentFrontendTraceProbe.ParseV2Only(Source("Region/valid/side-hole-aliases-v2.valid.firmfixture"));
        Assert.True(result.ParseSucceeded, string.Join(", ", result.Diagnostics));
        Assert.Equal("region-parent-integrated", result.FirmamentV2!.StageReached);
        Assert.Equal("Integrated", result.FirmamentV2.ParentIntegration);
        Assert.Equal("Closed", result.FirmamentV2.ShellClosure);
        Assert.Equal("Succeeded", result.FirmamentV2.StepSmoke);
        Assert.Null(result.FirmamentV2.Blocker);
    }

    [Fact]
    public void FirmamentV2SideHoleAliases_InvalidDiagnostics()
    {
        Assert.Contains(FirmamentV2Parser.AliasUnresolved, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-alias-unknown-on-v2.invalid.firmfixture")).Diagnostics);
        Assert.Contains(FirmamentV2Parser.AliasUnresolved, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-alias-unknown-through-v2.invalid.firmfixture")).Diagnostics);
        var loopDiagnostics = FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-alias-loopref-on-v2.invalid.firmfixture")).Diagnostics;
        Assert.Contains(FirmamentV2Parser.SideHoleAliasMustResolveToFace, loopDiagnostics);
        Assert.Contains(FirmamentV2Parser.SideHoleAliasResolvesToUnsupportedFace, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-alias-wrong-face-v2.invalid.firmfixture")).Diagnostics);
    }

    [Fact]
    public void FirmamentV2SideHoleYAxis_ParsesDirectYRoute()
    {
        var intent = FirmamentV2Parser.Parse(Source("Region/valid/side-hole-y-axis-v2.valid.firmfixture")).Document!.SideHoleIntent!;
        Assert.Equal("+Y", intent.AttachFace);
        Assert.Equal("-Y", intent.ThroughFace);
        Assert.Equal("+Y->-Y", intent.Route);
        Assert.Equal("Y", intent.RouteEvidence.Axis);
        Assert.Equal("face(+Y):u=+X,v=+Z", intent.CenterSelectorFrame);
    }

    [Fact]
    public void FirmamentV2SideHoleYAxis_DirectYReachesGoldenPath()
    {
        var result = FirmamentFrontendTraceProbe.ParseV2Only(Source("Region/valid/side-hole-y-axis-v2.valid.firmfixture"));
        Assert.True(result.ParseSucceeded, string.Join(", ", result.Diagnostics));
        Assert.Equal("region-parent-integrated", result.FirmamentV2!.StageReached);
        Assert.Equal("Integrated", result.FirmamentV2.ParentIntegration);
        Assert.Equal("Closed", result.FirmamentV2.ShellClosure);
        Assert.Equal("Succeeded", result.FirmamentV2.StepSmoke);
        Assert.Null(result.FirmamentV2.Blocker);
    }

    [Fact]
    public void FirmamentV2SideHoleYAxis_ParsesReverseYRoute()
    {
        var intent = FirmamentV2Parser.Parse(Source("Region/valid/side-hole-reverse-y-v2.valid.firmfixture")).Document!.SideHoleIntent!;
        Assert.Equal("-Y", intent.AttachFace);
        Assert.Equal("+Y", intent.ThroughFace);
        Assert.Equal("-Y->+Y", intent.Route);
        Assert.Equal("face(-Y):u=+X,v=+Z", intent.CenterSelectorFrame);
    }

    [Fact]
    public void FirmamentV2SideHoleYAxis_ParsesAliasYRoute()
    {
        var doc = FirmamentV2Parser.Parse(Source("Region/valid/side-hole-aliases-y-axis-v2.valid.firmfixture")).Document!;
        Assert.Contains(Box(doc.Solid).Exposures, e => e.Alias == "front" && e.Selector == "face(+Y)");
        Assert.Contains(Box(doc.Solid).Exposures, e => e.Alias == "back" && e.Selector == "face(-Y)");
        var intent = doc.SideHoleIntent!;
        Assert.Equal("front", intent.AttachTargetSource);
        Assert.Equal("back", intent.ThroughTargetSource);
        Assert.Equal("+Y->-Y", intent.Route);
    }

    [Fact]
    public void FirmamentV2SideHoleYAxis_InvalidRoutesRejected()
    {
        Assert.Contains(FirmamentV2Parser.SideHoleRouteUnsupported, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-mixed-axis-z-to-x-v2.invalid.firmfixture")).Diagnostics);
        Assert.Contains(FirmamentV2Parser.SideHoleRouteUnsupported, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-mixed-axis-y-to-x-v2.invalid.firmfixture")).Diagnostics);
        Assert.Contains(FirmamentV2Parser.SideHoleCenterExceedsClearance, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-y-center-x-boundary-v2.invalid.firmfixture")).Diagnostics);
        Assert.Contains(FirmamentV2Parser.SideHoleCenterExceedsClearance, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-y-center-z-boundary-v2.invalid.firmfixture")).Diagnostics);
        Assert.Contains(FirmamentV2Parser.SideHoleRouteUnsupported, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-alias-y-wrong-through-v2.invalid.firmfixture")).Diagnostics);
    }

    [Fact]
    public void FirmamentV2SideHoleZAxis_ParsesDirectZRoute()
    {
        var intent = FirmamentV2Parser.Parse(Source("Region/valid/side-hole-z-axis-v2.valid.firmfixture")).Document!.SideHoleIntent!;
        Assert.Equal("+Z", intent.AttachFace);
        Assert.Equal("-Z", intent.ThroughFace);
        Assert.Equal("+Z->-Z", intent.Route);
        Assert.Equal("Z", intent.RouteEvidence.Axis);
        Assert.Equal("face(+Z):u=+X,v=+Y", intent.CenterSelectorFrame);
    }

    [Fact]
    public void FirmamentV2SideHoleZAxis_DirectZReachesGoldenPath()
    {
        var result = FirmamentFrontendTraceProbe.ParseV2Only(Source("Region/valid/side-hole-z-axis-v2.valid.firmfixture"));
        Assert.True(result.ParseSucceeded, string.Join(", ", result.Diagnostics));
        Assert.Equal("region-parent-integrated", result.FirmamentV2!.StageReached);
        Assert.Equal("Integrated", result.FirmamentV2.ParentIntegration);
        Assert.Equal("Closed", result.FirmamentV2.ShellClosure);
        Assert.Equal("Succeeded", result.FirmamentV2.StepSmoke);
        Assert.Null(result.FirmamentV2.Blocker);
    }

    [Fact]
    public void FirmamentV2SideHoleZAxis_ParsesReverseZRoute()
    {
        var intent = FirmamentV2Parser.Parse(Source("Region/valid/side-hole-reverse-z-v2.valid.firmfixture")).Document!.SideHoleIntent!;
        Assert.Equal("-Z", intent.AttachFace);
        Assert.Equal("+Z", intent.ThroughFace);
        Assert.Equal("-Z->+Z", intent.Route);
        Assert.Equal("face(-Z):u=+X,v=+Y", intent.CenterSelectorFrame);
    }

    [Fact]
    public void FirmamentV2SideHoleZAxis_ReverseZReachesGoldenPath()
    {
        var result = FirmamentFrontendTraceProbe.ParseV2Only(Source("Region/valid/side-hole-reverse-z-v2.valid.firmfixture"));
        Assert.True(result.ParseSucceeded, string.Join(", ", result.Diagnostics));
        Assert.Equal("region-parent-integrated", result.FirmamentV2!.StageReached);
        Assert.Equal("Integrated", result.FirmamentV2.ParentIntegration);
        Assert.Equal("Closed", result.FirmamentV2.ShellClosure);
        Assert.Equal("Succeeded", result.FirmamentV2.StepSmoke);
        Assert.Null(result.FirmamentV2.Blocker);
    }

    [Fact]
    public void FirmamentV2SideHoleZAxis_ParsesAliasZRoute()
    {
        var doc = FirmamentV2Parser.Parse(Source("Region/valid/side-hole-aliases-z-axis-v2.valid.firmfixture")).Document!;
        Assert.Contains(Box(doc.Solid).Exposures, e => e.Alias == "top" && e.Selector == "face(+Z)");
        Assert.Contains(Box(doc.Solid).Exposures, e => e.Alias == "bottom" && e.Selector == "face(-Z)");
        var region = Assert.Single(Assert.Single(doc.ModifyBlocks!).Regions);
        Assert.Equal("top", region.Attachment.Source);
        Assert.Equal("face(+Z)", region.Attachment.ResolvedSelector);
        Assert.Equal("bottom", region.Cut.Tool.Through.Source);
        Assert.Equal("face(-Z)", region.Cut.Tool.Through.ResolvedSelector);
    }

    [Fact]
    public void FirmamentV2SideHoleZAxis_AliasZReachesGoldenPath()
    {
        var result = FirmamentFrontendTraceProbe.ParseV2Only(Source("Region/valid/side-hole-aliases-z-axis-v2.valid.firmfixture"));
        Assert.True(result.ParseSucceeded, string.Join(", ", result.Diagnostics));
        Assert.Equal("region-parent-integrated", result.FirmamentV2!.StageReached);
        Assert.Equal("Integrated", result.FirmamentV2.ParentIntegration);
        Assert.Equal("Closed", result.FirmamentV2.ShellClosure);
        Assert.Equal("Succeeded", result.FirmamentV2.StepSmoke);
        Assert.Null(result.FirmamentV2.Blocker);
    }

    [Fact]
    public void FirmamentV2SideHoleZAxis_InvalidRoutesAndClearanceRejected()
    {
        Assert.Contains(FirmamentV2Parser.SideHoleRouteUnsupported, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-mixed-axis-z-to-x-v2.invalid.firmfixture")).Diagnostics);
        Assert.Contains(FirmamentV2Parser.SideHoleCenterExceedsClearance, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-z-center-x-boundary-v2.invalid.firmfixture")).Diagnostics);
        Assert.Contains(FirmamentV2Parser.SideHoleCenterExceedsClearance, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-z-center-y-boundary-v2.invalid.firmfixture")).Diagnostics);
        Assert.Contains(FirmamentV2Parser.SideHoleRouteUnsupported, FirmamentV2Parser.Parse(Source("Region/invalid/side-hole-alias-z-wrong-through-v2.invalid.firmfixture")).Diagnostics);
    }


    [Fact]
    public void FirmamentV2SideHoleRoutePolicy_AllSixRoutesAreSupported()
    {
        var size = new double[] { 10, 8, 6 };
        foreach (var (attach, through) in new[] { ("+X", "-X"), ("-X", "+X"), ("+Y", "-Y"), ("-Y", "+Y"), ("+Z", "-Z"), ("-Z", "+Z") })
        {
            var result = FirmamentV2SideHoleRoutePolicy.Resolve(attach, through, size, radius: 1);
            Assert.True(result.IsSupported, $"{attach}->{through}: {result.Diagnostic}");
            Assert.Equal($"{attach}->{through}", result.Route!.Direction);
        }
    }

    [Fact]
    public void FirmamentV2SideHoleRoutePolicy_RouteFramesAreCorrect()
    {
        var size = new double[] { 10, 8, 6 };
        Assert.Equal(("+Y", "+Z", "face(+X):u=+Y,v=+Z"), Frame("+X", "-X"));
        Assert.Equal(("+Y", "+Z", "face(-X):u=+Y,v=+Z"), Frame("-X", "+X"));
        Assert.Equal(("+X", "+Z", "face(+Y):u=+X,v=+Z"), Frame("+Y", "-Y"));
        Assert.Equal(("+X", "+Z", "face(-Y):u=+X,v=+Z"), Frame("-Y", "+Y"));
        Assert.Equal(("+X", "+Y", "face(+Z):u=+X,v=+Y"), Frame("+Z", "-Z"));
        Assert.Equal(("+X", "+Y", "face(-Z):u=+X,v=+Y"), Frame("-Z", "+Z"));

        (string U, string V, string CenterFrame) Frame(string attach, string through)
        {
            var route = FirmamentV2SideHoleRoutePolicy.Resolve(attach, through, size, radius: 1).Route!;
            return (route.UAxis, route.VAxis, route.CenterFrame);
        }
    }

    [Fact]
    public void FirmamentV2SideHoleRoutePolicy_ClearanceUsesCorrectHalfExtents()
    {
        var size = new double[] { 10, 8, 6 };
        Assert.Equal((4, 3), Extents("+X", "-X"));
        Assert.Equal((4, 3), Extents("-X", "+X"));
        Assert.Equal((5, 3), Extents("+Y", "-Y"));
        Assert.Equal((5, 3), Extents("-Y", "+Y"));
        Assert.Equal((5, 4), Extents("+Z", "-Z"));
        Assert.Equal((5, 4), Extents("-Z", "+Z"));

        (double U, double V) Extents(string attach, string through)
        {
            var route = FirmamentV2SideHoleRoutePolicy.Resolve(attach, through, size, radius: 1).Route!;
            return (route.UHalfExtent, route.VHalfExtent);
        }
    }

    [Fact]
    public void FirmamentV2SideHoleRoutePolicy_RejectsSameFace()
    {
        var result = FirmamentV2SideHoleRoutePolicy.Resolve("+X", "+X", new double[] { 10, 8, 6 }, radius: 1);
        Assert.False(result.IsSupported);
        Assert.Equal(FirmamentV2Parser.SideHoleSameFaceUnsupported, result.Diagnostic);
    }

    [Fact]
    public void FirmamentV2SideHoleRoutePolicy_RejectsMixedAxis()
    {
        var result = FirmamentV2SideHoleRoutePolicy.Resolve("+Z", "+X", new double[] { 10, 8, 6 }, radius: 1);
        Assert.False(result.IsSupported);
        Assert.Equal(FirmamentV2Parser.SideHoleRouteUnsupported, result.Diagnostic);
    }

    [Fact]
    public void FirmamentV2SideHoleRoutePolicy_RejectsCenterBoundary()
    {
        var result = FirmamentV2SideHoleRoutePolicy.Resolve("+Z", "-Z", new double[] { 10, 8, 6 }, radius: 1, centerU: 4, centerV: 0);
        Assert.False(result.IsSupported);
        Assert.Equal(FirmamentV2Parser.SideHoleCenterExceedsClearance, result.Diagnostic);
    }

    [Fact]
    public void FirmamentV2SideHoleRoutePolicy_DirectFixturesStillGreen()
    {
        foreach (var fixture in new[] { "Region/valid/side-hole-v2.valid.firmfixture", "Region/valid/side-hole-y-axis-v2.valid.firmfixture", "Region/valid/side-hole-z-axis-v2.valid.firmfixture" })
        {
            var result = FirmamentFrontendTraceProbe.ParseV2Only(Source(fixture));
            Assert.True(result.ParseSucceeded, fixture + ": " + string.Join(", ", result.Diagnostics));
            Assert.Equal("Integrated", result.FirmamentV2!.ParentIntegration);
            Assert.Equal("Closed", result.FirmamentV2.ShellClosure);
            Assert.Equal("Succeeded", result.FirmamentV2.StepSmoke);
        }
    }

    [Fact]
    public void FirmamentV2SideHoleRoutePolicy_AliasFixturesStillGreen()
    {
        foreach (var fixture in new[] { "Region/valid/side-hole-aliases-v2.valid.firmfixture", "Region/valid/side-hole-aliases-y-axis-v2.valid.firmfixture", "Region/valid/side-hole-aliases-z-axis-v2.valid.firmfixture" })
        {
            var result = FirmamentFrontendTraceProbe.ParseV2Only(Source(fixture));
            Assert.True(result.ParseSucceeded, fixture + ": " + string.Join(", ", result.Diagnostics));
            Assert.Equal("Integrated", result.FirmamentV2!.ParentIntegration);
            Assert.Equal("Closed", result.FirmamentV2.ShellClosure);
            Assert.Equal("Succeeded", result.FirmamentV2.StepSmoke);
        }
    }

    [Fact]
    public void FirmamentV2_AllPriorSideHoleMilestonesRemainGreen()
    {
        foreach (var fixture in Directory.EnumerateFiles(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/FirmamentV2/Region/valid")), "side-hole*.valid.firmfixture"))
        {
            var lines = File.ReadAllLines(fixture);
            var bodyStart = Array.FindIndex(lines, line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("//", StringComparison.Ordinal));
            var result = FirmamentFrontendTraceProbe.ParseV2Only(string.Join(Environment.NewLine, lines.Skip(Math.Max(0, bodyStart))));
            Assert.True(result.ParseSucceeded, Path.GetFileName(fixture) + ": " + string.Join(", ", result.Diagnostics));
        }
    }


    [Fact]
    public void FirmamentV2Parser_SemanticPmi_ParsesHoleDiameterAndDatumPlane()
    {
        var hole = FirmamentV2Parser.Parse(Source("PMI/valid/pmi-v2-hole-diameter-callout-emits-in-step.valid.firmfixture"));
        Assert.True(hole.IsSuccess, string.Join(", ", hole.Diagnostics));
        var holePmi = Assert.Single(hole.Document!.Pmi!);
        Assert.Equal(FirmamentV2PmiKind.HoleDiameter, holePmi.Kind);
        Assert.Equal("mountDiameter", holePmi.Name);
        Assert.Equal("mount", holePmi.Target);
        Assert.Equal(2d, holePmi.Value);

        var datum = FirmamentV2Parser.Parse(Source("PMI/valid/pmi-v2-datum-plane-emits-in-step.valid.firmfixture"));
        Assert.True(datum.IsSuccess, string.Join(", ", datum.Diagnostics));
        var datumPmi = Assert.Single(datum.Document!.Pmi!);
        Assert.Equal(FirmamentV2PmiKind.DatumPlane, datumPmi.Kind);
        Assert.Equal("A", datumPmi.Name);
        Assert.Equal("top", datumPmi.Target);
    }

    [Fact]
    public void FirmamentV2Parser_SemanticPmi_InvalidTargetAndDiameterAreDeterministicDiagnostics()
    {
        var unknown = FirmamentV2Parser.Parse("""
model BadPmiTarget {
  units mm
  solid base: Box { size: [10, 8, 6] }
  pmi { diameter bad { target: missing value: 2mm } }
}
""");
        Assert.Contains(FirmamentV2Parser.PmiTargetUnresolved, unknown.Diagnostics);

        var invalidDiameter = FirmamentV2Parser.Parse("""
model BadPmiDiameter {
  units mm
  solid base: Box { size: [10, 8, 6] }
  modify base { hole<shaft> mount { on: face(+Z) center: [0, 0] diameter: 2 end: throughAll } }
  pmi { diameter bad { target: mount value: 2deg } }
}
""");
        Assert.Contains(FirmamentV2Parser.PmiDiameterInvalid, invalidDiameter.Diagnostics);
    }


    [Fact]
    public void FirmamentV2Parser_RecordPmiDatumDiameter_BindsTolerancedDimensionAndTargets()
    {
        var result = FirmamentV2Parser.Parse(Source("InlineStep/valid/inline-step-v2-record-pmi-datum-diameter-step-verified.valid.firmfixture"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/FirmamentV2/InlineStep/valid")));

        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics));
        var bound = result.Document!.BoundPmi!;
        var datum = Assert.Single(bound.Datums);
        Assert.Equal(FirmamentV2PmiKind.DatumPlane, datum.Kind);
        Assert.Equal("A", datum.Name);
        Assert.Equal("part.region(\"baseFace\")", Assert.Single(datum.Targets));

        var diameter = Assert.Single(bound.Dimensions);
        Assert.Equal(FirmamentV2PmiKind.HoleDiameter, diameter.Kind);
        Assert.Equal("mountHoleADiameter", diameter.Name);
        Assert.Equal("part.region(\"mountHoleA\")", Assert.Single(diameter.Targets));
        Assert.Equal(6.0d, diameter.DimensionValue!.NumericValue);
        Assert.NotNull(diameter.DimensionTolerance);
        Assert.Equal(0.05d, diameter.DimensionTolerance!.Plus);
        Assert.Equal(0.05d, diameter.DimensionTolerance.Minus);
    }

    [Fact]
    public void FirmamentV2P2_RecordPmiDatumDiameter_ExportsAp242EvidenceAndReimports()
    {
        var fixture = FixturePath("InlineStep/valid/inline-step-v2-record-pmi-datum-diameter-step-verified.valid.firmfixture");
        var output = Path.Combine(Path.GetTempPath(), "aetheris-p2-record-pmi-" + Guid.NewGuid().ToString("N") + ".step");

        var build = FirmamentBuildAndExport.Run(fixture, output);

        Assert.True(build.IsSuccess, string.Join(", ", build.Diagnostics.Select(d => d.Message)));
        Assert.True(File.Exists(output));
        var step = File.ReadAllText(output);
        Assert.Contains("SHAPE_ASPECT('firmament-datum:A'", step, StringComparison.Ordinal);
        Assert.Contains("PROPERTY_DEFINITION('datum:A:part'", step, StringComparison.Ordinal);
        Assert.Contains("SHAPE_DIMENSION_REPRESENTATION('diameter:part.mountHoleADiameter'", step, StringComparison.Ordinal);
        Assert.Contains("PROPERTY_DEFINITION('diameter:part.mountHoleADiameter'", step, StringComparison.Ordinal);
        Assert.Contains("SHAPE_DIMENSION_REPRESENTATION('diameter_tolerance:part.mountHoleADiameter'", step, StringComparison.Ordinal);
        Assert.Contains("'tolerance_plus'", step, StringComparison.Ordinal);
        Assert.Contains("'tolerance_minus'", step, StringComparison.Ordinal);

        var reimport = Aetheris.Kernel.Core.Step242.Step242Importer.ImportBody(step);
        Assert.True(reimport.IsSuccess, string.Join(", ", reimport.Diagnostics.Select(d => d.Message)));
        Assert.Single(build.Value.Export.DatumInspection!);
        Assert.Single(build.Value.Export.DimensionInspection!);
    }

    [Fact]
    public void FirmamentV2P2_RecordPmiDiameterDimensionWithoutTolerance_IsRejected()
    {
        var result = FirmamentV2Parser.Parse(Source("InlineStep/invalid/inline-step-v2-record-pmi-diameter-missing-tolerance.invalid.firmfixture"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/FirmamentV2/InlineStep/invalid")));

        Assert.False(result.IsSuccess);
        Assert.Contains(FirmamentV2Parser.PmiDimensionMissingTolerance, result.Diagnostics);
    }

    [Fact]
    public void FirmamentV2Parser_RecognizesMalformedConceptStructSource_WithoutV1FallbackAdmission()
    {
        var result = FirmamentV2Parser.Parse(Source("Language/invalid/concept-struct-diagnostic-routing-x1.invalid.firmfixture"));

        Assert.False(result.IsSuccess);
        Assert.Equal(FirmamentV2ParseDisposition.RecognizedInvalid, result.Disposition);
        Assert.Contains(FirmamentV2Parser.HoleConstructionPlaneCenterMissing, result.Diagnostics);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.StartsWith("firmament-concept-missing-member:", StringComparison.Ordinal));
    }

    [Fact]
    public void FirmamentV2Parser_LeavesLegacyToonAndArbitraryText_Unrecognized()
    {
        var legacy = FirmamentV2Parser.Parse("""
firmament:
  version: 1
model:
  name: legacy
""");
        var arbitrary = FirmamentV2Parser.Parse("this is not Firmament source");

        Assert.Equal(FirmamentV2ParseDisposition.NotRecognized, legacy.Disposition);
        Assert.Equal(FirmamentV2ParseDisposition.NotRecognized, arbitrary.Disposition);
    }

    [Fact]
    public void FirmamentV2P2_ExportDeferredFlatness_IsReportedAndBuildRejectedDeterministically()
    {
        var fixture = FixturePath("InlineStep/invalid/inline-step-v2-record-pmi-export-deferred-flatness.invalid.firmfixture");
        var parse = FirmamentV2Parser.Parse(Source("InlineStep/invalid/inline-step-v2-record-pmi-export-deferred-flatness.invalid.firmfixture"), Path.GetDirectoryName(fixture));
        var report = FirmamentV2ValidationReportBuilder.Build(parse, fixture);

        var pmi = Assert.Single(report.Pmi);
        Assert.Equal("flatness", pmi.Kind);
        Assert.Equal("deferred", pmi.ExportSupport);
        Assert.Equal("export-deferred", pmi.Status);

        var build = FirmamentBuildAndExport.Run(fixture, Path.Combine(Path.GetTempPath(), "aetheris-flatness-deferred.step"));
        Assert.False(build.IsSuccess);
        Assert.Contains(build.Diagnostics, d => d.Message.Contains("firmament-v2-pmi-export-deferred", StringComparison.Ordinal));
    }

    [Fact]
    public void FirmamentV2Parser_StaticFactFlowsThroughHoleRequireAndProjectedPmi()
    {
        var result = FirmamentV2Parser.Parse(Source("Canonical/valid/pmi-projected-hole-diameter-asymmetric-tolerance.firmament"));
        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics));
        var document = Assert.IsType<FirmamentV2Document>(result.Document);
        var constraint = Assert.Single(document.StaticAuthoring!.SemanticConstraints!);
        Assert.True(constraint.ValidationSucceeded);
        Assert.Equal("Mount", constraint.Subject);
        Assert.Equal("MountSpecs[0].Diameter", constraint.ExpectedProvenance);
        var pmi = Assert.Single(document.PmiBlock!.Records);
        Assert.Equal("MountDiameterConstraint", pmi.Projection!.SourceRequireId);
        Assert.Equal(8d, Assert.Single(document.BoundPmi!.Dimensions).DimensionValue!.NumericValue);
    }

    [Fact]
    public void FirmamentV2Parser_ManualAndProjectedHoleDiameterHaveEquivalentPmiSemantics()
    {
        var result = FirmamentV2Parser.Parse(Source("Canonical/valid/pmi-manual-vs-projected-equivalence.firmament"));
        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics));
        var dimensions = result.Document!.BoundPmi!.Dimensions.OrderBy(record => record.Name, StringComparer.Ordinal).ToArray();
        var manual = Assert.Single(dimensions, record => record.Name == "ManualCallout");
        var projected = Assert.Single(dimensions, record => record.Name == "ProjectedCallout");
        Assert.Equal(manual.Kind, projected.Kind);
        Assert.Equal(manual.DimensionValue!.NumericValue, projected.DimensionValue!.NumericValue);
        Assert.Equal(manual.DimensionTolerance, projected.DimensionTolerance);
        Assert.Equal(manual.DatumRefs, projected.DatumRefs);
        Assert.Equal("ProjectedDiameter", projected.ProjectionSource);
    }

    private static string Source(string relative)
    {
        var path = FixturePath(relative);
        var lines = File.ReadAllLines(path);
        var bodyStart = Array.FindIndex(lines, line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("//", StringComparison.Ordinal));
        return string.Join(Environment.NewLine, lines.Skip(Math.Max(0, bodyStart)));
    }

    private static string FixturePath(string relative) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/FirmamentV2", relative));

    [Fact]
    public void FirmamentV2Parser_LetPrimitiveLiterals_ParsesAndBinds()
    {
        var result = FirmamentV2Parser.Parse(Source("Language/valid/let-primitive-literals.valid.firmfixture"));

        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics));
        var lets = result.Document!.Lets!;
        var boundLets = result.Document!.BoundLets!;
        Assert.Equal(6, lets.Count);
        Assert.Equal(6, boundLets.Count);
        AssertLet(boundLets[0], "holeCount", FirmamentV2PrimitiveType.Int, 4, null);
        AssertLet(boundLets[1], "scale", FirmamentV2PrimitiveType.Float, 1.25d, null);
        AssertLet(boundLets[2], "holeDiameter", FirmamentV2PrimitiveType.Length, 6.0d, "mm");
        AssertLet(boundLets[3], "draftAngle", FirmamentV2PrimitiveType.Angle, 3.0d, "deg");
        AssertLet(boundLets[4], "materialName", FirmamentV2PrimitiveType.String, "Aluminum6061", null);
        AssertLet(boundLets[5], "inspectionRequired", FirmamentV2PrimitiveType.Bool, true, null);
    }

    [Theory]
    [InlineData("let holeCount: int = 4.0", FirmamentV2Parser.LetTypeMismatch)]
    [InlineData("let holeDiameter: length = 6.0", FirmamentV2Parser.LetTypeMismatch)]
    [InlineData("let draftAngle: angle = 3mm", FirmamentV2Parser.LetUnitMismatch)]
    [InlineData("let scale: float = 1.25mm", FirmamentV2Parser.LetUnitMismatch)]
    [InlineData("let unknownThing: banana = 1", FirmamentV2Parser.LetUnknownType)]
    [InlineData("let radius: length = holeDiameter / 2", FirmamentV2Parser.ExpressionUnknownSymbol)]
    public void FirmamentV2Parser_LetPrimitiveLiterals_InvalidCasesAreDiagnostics(string letSource, string expectedDiagnostic)
    {
        var result = FirmamentV2Parser.Parse($$"""
            model LetInvalidExample {
                units mm
                solid base: Box {
                    size: [10, 8, 6]
                }
                {{letSource}}
            }
            """);

        Assert.False(result.IsSuccess);
        Assert.Contains(expectedDiagnostic, result.Diagnostics);
    }

    [Fact]
    public void FirmamentV2Parser_LetPrimitiveLiterals_DuplicateNameIsDiagnostic()
    {
        var result = FirmamentV2Parser.Parse("""
            model LetDuplicateExample {
                units mm
                solid base: Box {
                    size: [10, 8, 6]
                }
                let holeCount: int = 4
                let holeCount: int = 5
            }
            """);

        Assert.False(result.IsSuccess);
        Assert.Contains(FirmamentV2Parser.LetDuplicateName, result.Diagnostics);
    }


    [Fact]
    public void FirmamentV2Parser_LetRecordGroups_ParseAndBindFields()
    {
        var result = FirmamentV2Parser.Parse(Source("Language/valid/let-record-groups.valid.firmfixture"));

        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics));
        var record = Assert.Single(result.Document!.LetRecords!);
        Assert.Equal("MountingPattern", record.Name);
        Assert.Equal(6, record.Fields.Count);
        var bound = Assert.Single(result.Document!.BoundLetRecords!);
        Assert.Equal("MountingPattern", bound.Name);
        AssertLet(bound.Fields["holeDiameter"], "holeDiameter", FirmamentV2PrimitiveType.Length, 6.0d, "mm");
        AssertLet(bound.Fields["holeSpacingX"], "holeSpacingX", FirmamentV2PrimitiveType.Length, 80.0d, "mm");
        AssertLet(bound.Fields["holeSpacingY"], "holeSpacingY", FirmamentV2PrimitiveType.Length, 40.0d, "mm");
        AssertLet(bound.Fields["holeCount"], "holeCount", FirmamentV2PrimitiveType.Int, 4, null);
        AssertLet(bound.Fields["label"], "label", FirmamentV2PrimitiveType.String, "M6 mount group", null);
        AssertLet(bound.Fields["inspectionRequired"], "inspectionRequired", FirmamentV2PrimitiveType.Bool, true, null);
    }

    [Fact]
    public void FirmamentV2Parser_LetRecordGroups_AllowSameFieldNameAcrossRecords()
    {
        var result = FirmamentV2Parser.Parse("""
            model MultipleRecords {
                units mm
                solid base: Box { size: [10, 8, 6] }
                let MountingPattern { holeDiameter: length = 6.0mm }
                let InspectionPattern { holeDiameter: length = 6.1mm }
            }
            """);

        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics));
        Assert.Equal(2, result.Document!.BoundLetRecords!.Count);
    }

    [Fact]
    public void FirmamentV2Parser_DottedReferenceScalarLet_ResolvesRecordFieldValue()
    {
        var result = FirmamentV2Parser.Parse("""
            model DottedReference {
                units mm
                solid base: Box { size: [10, 8, 6] }
                let MountingPattern { holeDiameter: length = 6.0mm }
                let exportedHoleDiameter: length = MountingPattern.holeDiameter
            }
            """);

        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics));
        AssertLet(Assert.Single(result.Document!.BoundLets!), "exportedHoleDiameter", FirmamentV2PrimitiveType.Length, 6.0d, "mm");
    }


    [Fact]
    public void FirmamentV2Parser_TolerancedValues_ParseBilateralAsymmetricAndRecordFields()
    {
        var result = FirmamentV2Parser.Parse(Source("Language/valid/let-toleranced-values.valid.firmfixture"));

        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics));
        var lets = result.Document!.BoundLets!.ToDictionary(l => l.Name);
        AssertLet(lets["holeDiameter"], "holeDiameter", FirmamentV2PrimitiveType.Length, 6.0d, "mm");
        AssertTolerance(lets["holeDiameter"].Tolerance, FirmamentV2ToleranceKind.Bilateral, 0.05d, 0.05d, "mm");
        AssertTolerance(lets["slotWidth"].Tolerance, FirmamentV2ToleranceKind.Asymmetric, 0.10d, 0.05d, "mm");
        AssertTolerance(lets["draftAngle"].Tolerance, FirmamentV2ToleranceKind.Bilateral, 0.5d, 0.5d, "deg");

        var fields = Assert.Single(result.Document!.BoundLetRecords!).Fields;
        AssertTolerance(fields["holeDiameter"].Tolerance, FirmamentV2ToleranceKind.Bilateral, 0.05d, 0.05d, "mm");
        AssertTolerance(fields["holeSpacingX"].Tolerance, FirmamentV2ToleranceKind.Asymmetric, 0.10d, 0.05d, "mm");
    }

    [Fact]
    public void FirmamentV2Parser_TolerancedExpressions_ExplicitToleranceAndAliasesBehaveDeterministically()
    {
        var result = FirmamentV2Parser.Parse("""
            model TolerancedExpressions {
                units mm
                solid base: Box { size: [10, 8, 6] }
                let holeDiameter: length = 6.0mm tol 0.05mm
                let clearance: length = 0.25mm
                let drill: length = holeDiameter + clearance tol 0.05mm
                let radius: length = holeDiameter / 2
                let exportedHoleDiameter: length = holeDiameter
                let MountingPattern { fieldDiameter: length = 7.0mm tol 0.1mm }
                let exportedFieldDiameter: length = MountingPattern.fieldDiameter
            }
            """);

        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics));
        Assert.Contains(FirmamentV2Parser.ToleranceDroppedThroughArithmetic, result.Diagnostics);
        var lets = result.Document!.BoundLets!.ToDictionary(l => l.Name);
        AssertLet(lets["drill"], "drill", FirmamentV2PrimitiveType.Length, 6.25d, "mm");
        AssertTolerance(lets["drill"].Tolerance, FirmamentV2ToleranceKind.Bilateral, 0.05d, 0.05d, "mm");
        AssertLet(lets["radius"], "radius", FirmamentV2PrimitiveType.Length, 3.0d, "mm");
        Assert.Null(lets["radius"].Tolerance);
        AssertTolerance(lets["exportedHoleDiameter"].Tolerance, FirmamentV2ToleranceKind.Bilateral, 0.05d, 0.05d, "mm");
        AssertTolerance(lets["exportedFieldDiameter"].Tolerance, FirmamentV2ToleranceKind.Bilateral, 0.1d, 0.1d, "mm");
    }

    [Theory]
    [InlineData("let holeCount: int = 4 tol 1", FirmamentV2Parser.ToleranceInvalidType)]
    [InlineData("let scale: float = 1.25 tol 0.01", FirmamentV2Parser.ToleranceInvalidType)]
    [InlineData("let name: string = \"Aluminum\" tol 1", FirmamentV2Parser.ToleranceInvalidType)]
    [InlineData("let flag: bool = true tol 1", FirmamentV2Parser.ToleranceInvalidType)]
    [InlineData("let holeDiameter: length = 6.0mm tol 0.5deg", FirmamentV2Parser.ToleranceUnitMismatch)]
    [InlineData("let draftAngle: angle = 3deg tol 0.1mm", FirmamentV2Parser.ToleranceUnitMismatch)]
    [InlineData("let x: length = 1.0mm tol +0.1mm", FirmamentV2Parser.ToleranceMissingMinus)]
    [InlineData("let x: length = 1.0mm tol -0.1mm", FirmamentV2Parser.ToleranceNegativeBilateral)]
    [InlineData("let x: length = 1.0mm tol +0.1mm +0.2mm", FirmamentV2Parser.ToleranceMissingMinus)]
    [InlineData("let x: length = 1.0mm tol -0.1mm +0.2mm", FirmamentV2Parser.ToleranceMissingPlus)]
    [InlineData("let x: length = 1.0mm tol 0.1", FirmamentV2Parser.ToleranceInvalidLiteral)]
    [InlineData("let bad: length = (6.0mm tol 0.05mm) + 1.0mm", FirmamentV2Parser.ToleranceUnsupported)]
    public void FirmamentV2Parser_TolerancedValues_InvalidCasesAreDiagnostics(string letSource, string expectedDiagnostic)
    {
        var result = FirmamentV2Parser.Parse($$"""
            model InvalidTolerance {
                units mm
                solid base: Box { size: [10, 8, 6] }
                {{letSource}}
            }
            """);

        Assert.False(result.IsSuccess);
        Assert.Contains(expectedDiagnostic, result.Diagnostics);
    }

    [Fact]
    public void FirmamentV2Parser_LetArithmeticExpressions_EvaluateStrictTypedGraph()
    {
        var result = FirmamentV2Parser.Parse(Source("Language/valid/let-arithmetic-expressions.valid.firmfixture"));

        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics));
        var bound = result.Document!.BoundLets!.ToDictionary(l => l.Name);
        AssertLet(bound["radius"], "radius", FirmamentV2PrimitiveType.Length, 3.0d, "mm");
        AssertLet(bound["drill"], "drill", FirmamentV2PrimitiveType.Length, 6.25d, "mm");
        AssertLet(bound["doubled"], "doubled", FirmamentV2PrimitiveType.Int, 8, null);
        AssertLet(bound["ratio"], "ratio", FirmamentV2PrimitiveType.Float, 2.0d, null);
        AssertLet(bound["halfAngle"], "halfAngle", FirmamentV2PrimitiveType.Angle, 45.0d, "deg");
        AssertLet(bound["scaled"], "scaled", FirmamentV2PrimitiveType.Length, 7.5d, "mm");
        Assert.Contains("diameter", bound["radius"].Dependencies!);
    }

    [Theory]
    [InlineData("let diameter: length = 6.0mm\nlet bad: length = diameter + 2", FirmamentV2Parser.ExpressionInvalidOperator)]
    [InlineData("let diameter: length = 6.0mm\nlet draftAngle: angle = 90deg\nlet bad: length = diameter + draftAngle", FirmamentV2Parser.ExpressionInvalidOperator)]
    [InlineData("let name: string = \"Aluminum6061\"\nlet bad: string = name + name", FirmamentV2Parser.ExpressionInvalidOperator)]
    [InlineData("let flag: bool = true\nlet bad: bool = flag * 2", FirmamentV2Parser.ExpressionInvalidOperator)]
    [InlineData("let diameter: length = 6.0mm\nlet bad: length = diameter / diameter", FirmamentV2Parser.ExpressionTypeMismatch)]
    [InlineData("let bad: float = 1.0 / 0.0", FirmamentV2Parser.ExpressionDivisionByZero)]
    [InlineData("let badLength: length = 6.0mm / 0", FirmamentV2Parser.ExpressionDivisionByZero)]
    [InlineData("let a: length = b + 1.0mm\nlet b: length = a + 1.0mm", FirmamentV2Parser.ExpressionCycle)]
    [InlineData("let x: length = if true { 1.0mm } else { 2.0mm }", FirmamentV2Parser.ExpressionUnsupported)]
    [InlineData("let x: length = foo(1.0mm)", FirmamentV2Parser.ExpressionUnsupported)]
    public void FirmamentV2Parser_LetArithmeticExpressions_InvalidCasesAreDiagnostics(string letSource, string expectedDiagnostic)
    {
        var result = FirmamentV2Parser.Parse($$"""
            model InvalidArithmetic {
                units mm
                solid base: Box { size: [10, 8, 6] }
                {{letSource}}
            }
            """);

        Assert.False(result.IsSuccess);
        Assert.Contains(expectedDiagnostic, result.Diagnostics);
    }

    [Theory]
    [InlineData("let MountingPattern { holeDiameter: length = 6.0mm holeDiameter: length = 7.0mm }", FirmamentV2Parser.LetInvalidLiteral)]
    [InlineData("let MountingPattern { radius: length = holeDiameter / 2 }", FirmamentV2Parser.LetLiteralOnly)]
    [InlineData("let Process { Tooling { minimumRadius: length = 1.5mm } }", FirmamentV2Parser.LetInvalidLiteral)]
    public void FirmamentV2Parser_LetRecordGroups_InvalidRecordBodiesAreDiagnostics(string recordSource, string expectedDiagnostic)
    {
        var result = FirmamentV2Parser.Parse($$"""
            model InvalidRecord {
                units mm
                solid base: Box { size: [10, 8, 6] }
                {{recordSource}}
            }
            """);

        Assert.False(result.IsSuccess);
        Assert.Contains(expectedDiagnostic, result.Diagnostics);
    }


    [Fact]
    public void FirmamentV2Parser_LetRecordGroups_DuplicateFieldAndTopLevelNamesAreDiagnostics()
    {
        var duplicateField = FirmamentV2Parser.Parse("""
            model DuplicateRecordField {
                units mm
                solid base: Box { size: [10, 8, 6] }
                let MountingPattern {
                    holeDiameter: length = 6.0mm
                    holeDiameter: length = 7.0mm
                }
            }
            """);
        Assert.False(duplicateField.IsSuccess);
        Assert.Contains(FirmamentV2Parser.LetRecordDuplicateField, duplicateField.Diagnostics);

        var duplicateTopLevel = FirmamentV2Parser.Parse("""
            model DuplicateTopLevel {
                units mm
                solid base: Box { size: [10, 8, 6] }
                let holeDiameter: length = 6.0mm
                let holeDiameter { value: length = 6.0mm }
            }
            """);
        Assert.False(duplicateTopLevel.IsSuccess);
        Assert.Contains(FirmamentV2Parser.LetDuplicateName, duplicateTopLevel.Diagnostics);
    }

    [Theory]
    [InlineData("let exportedHoleDiameter: length = MissingPattern.holeDiameter", FirmamentV2Parser.LetReferenceUnknownRecord)]
    [InlineData("let MountingPattern { holeDiameter: length = 6.0mm }\nlet exportedHoleDiameter: length = MountingPattern.missingField", FirmamentV2Parser.LetReferenceUnknownField)]
    [InlineData("let holeDiameter: length = 6.0mm\nlet exported: length = holeDiameter.value", FirmamentV2Parser.LetReferenceNonRecord)]
    [InlineData("let MountingPattern { holeCount: int = 4 }\nlet exportedHoleDiameter: length = MountingPattern.holeCount", FirmamentV2Parser.LetTypeMismatch)]
    [InlineData("let MountingPattern { holeDiameter: length = 6.0mm }\nlet exportedHoleDiameter: length = MountingPattern", FirmamentV2Parser.LetReferenceRecordUsedAsValue)]
    public void FirmamentV2Parser_DottedReference_InvalidReferencesAreDiagnostics(string letSource, string expectedDiagnostic)
    {
        var result = FirmamentV2Parser.Parse($$"""
            model InvalidReference {
                units mm
                solid base: Box { size: [10, 8, 6] }
                {{letSource}}
            }
            """);

        Assert.False(result.IsSuccess);
        Assert.Contains(expectedDiagnostic, result.Diagnostics);
    }


    [Fact]
    public void FirmamentV2Parser_RecordShapedPmi_DatumDiameterAndRelationsBind()
    {
        var result = FirmamentV2Parser.Parse("""
            model PmiRecordShaped {
                units mm
                let MountingPattern {
                    holeDiameter: length = 6.0mm tol 0.05mm
                    holeSpacingX: length = 80.0mm tol +0.10mm -0.05mm
                }
                solid part: Box { size: [100, 60, 10] }
                pmi {
                    datum A {
                        target: part.region("baseFace")
                    }
                    diameter mountHoleADiameter {
                        target: part.region("mountHoleA")
                        dimension: MountingPattern.holeDiameter
                    }
                    distance mountHoleSpacingX {
                        targetA: part.region("mountHoleA")
                        targetB: part.region("mountHoleB")
                        dimension: MountingPattern.holeSpacingX
                    }
                    flatness baseFlatness {
                        target: part.region("baseFace")
                        tolerance: 0.03mm
                    }
                    coplanar topToDatumA {
                        target: part.region("topFace")
                        datum: A
                        tolerance: 0.05mm
                    }
                    perpendicular sideToDatumA {
                        target: part.region("sideFace")
                        datum: A
                        tolerance: 0.05mm
                    }
                    parallel topParallelToDatumA {
                        target: part.region("topFace")
                        datum: A
                        tolerance: 0.05mm
                    }
                }
            }
            """);

        Assert.True(result.IsSuccess, string.Join(",", result.Diagnostics));
        var bound = result.Document!.BoundPmi!;
        Assert.Single(bound.Datums);
        Assert.Equal("A", bound.Datums[0].Name);
        var diameter = Assert.Single(bound.Dimensions, d => d.Kind == FirmamentV2PmiKind.HoleDiameter);
        Assert.Equal(6.0d, diameter.DimensionValue!.NumericValue);
        AssertTolerance(diameter.DimensionTolerance, FirmamentV2ToleranceKind.Bilateral, 0.05d, 0.05d, "mm");
        Assert.Contains(bound.Controls, c => c.Kind == FirmamentV2PmiKind.Flatness && c.ControlTolerance!.NumericValue!.Value == 0.03d);
        Assert.Contains(bound.Controls, c => c.Kind == FirmamentV2PmiKind.Coplanar && c.DatumRefs.Single() == "A");
    }

    [Fact]
    public void FirmamentV2Parser_RecordShapedPmi_RejectsDimensionWithoutTolerance()
    {
        var result = FirmamentV2Parser.Parse("""
            model PmiMissingTolerance {
                units mm
                let holeDiameter: length = 6.0mm
                solid part: Box { size: [100, 60, 10] }
                pmi {
                    diameter mountHoleADiameter {
                        target: part.region("mountHoleA")
                        dimension: holeDiameter
                    }
                }
            }
            """);

        Assert.False(result.IsSuccess);
        Assert.Contains(FirmamentV2Parser.PmiDimensionMissingTolerance, result.Diagnostics);
    }

    [Fact]
    public void FirmamentV2Parser_RecordShapedPmi_RejectsTypeMismatchUnknownDatumAndDuplicates()
    {
        var typeMismatch = FirmamentV2Parser.Parse("""
            model PmiBadType {
                units mm
                let holeCount: int = 4
                solid part: Box { size: [10, 8, 6] }
                pmi { diameter badDiameter {
                    target: part.region("mountHoleA")
                    dimension: holeCount
                } }
            }
            """);
        Assert.False(typeMismatch.IsSuccess);
        Assert.Contains(FirmamentV2Parser.PmiDimensionTypeMismatch, typeMismatch.Diagnostics);

        var unknownDatum = FirmamentV2Parser.Parse("""
            model PmiUnknownDatum {
                units mm
                solid part: Box { size: [10, 8, 6] }
                pmi { coplanar topToDatumA {
                    target: part.region("topFace")
                    datum: A
                    tolerance: 0.05mm
                } }
            }
            """);
        Assert.False(unknownDatum.IsSuccess);
        Assert.Contains(FirmamentV2Parser.PmiUnknownDatum, unknownDatum.Diagnostics);

        var duplicate = FirmamentV2Parser.Parse("""
            model PmiDuplicateDatum {
                units mm
                solid part: Box { size: [10, 8, 6] }
                pmi {
                    datum A { target: part.region("baseFace") }
                    datum A { target: part.region("topFace") }
                }
            }
            """);
        Assert.False(duplicate.IsSuccess);
        Assert.Contains(FirmamentV2Parser.PmiDuplicateRecord, duplicate.Diagnostics);
        Assert.Contains(FirmamentV2Parser.PmiDuplicateDatum, duplicate.Diagnostics);
    }

    private static FirmamentV2BoxRecord Box(FirmamentV2SolidBinding solid) => Assert.IsType<FirmamentV2BoxRecord>(solid.Box);

    private static void AssertLet(FirmamentV2BoundLet actual, string name, FirmamentV2PrimitiveType type, object value, string? unit)
    {
        Assert.Equal(name, actual.Name);
        Assert.Equal(type, actual.Type);
        Assert.Equal(value, actual.Value.Value);
        Assert.Equal(unit, actual.Value.Unit);
    }

    private static void AssertTolerance(FirmamentV2Tolerance? actual, FirmamentV2ToleranceKind kind, double plus, double minus, string unit)
    {
        Assert.NotNull(actual);
        Assert.Equal(kind, actual.Kind);
        Assert.Equal(plus, actual.Plus);
        Assert.Equal(minus, actual.Minus);
        Assert.Equal(unit, actual.Unit);
    }

}
