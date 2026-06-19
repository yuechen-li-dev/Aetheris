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
        Assert.Equal([10, 8, 6], document.Solid.Box.Size);
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
        Assert.Equal([10, 8, 6], document.Solids[0].Box.Size);
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
        Assert.Equal([10, 8, 6], result.Document!.Solids.Single(s => s.Name == "base").Box.Size);
        Assert.Equal([10, 8, 12], result.Document.Solids.Single(s => s.Name == "tall").Box.Size);
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
        var exposures = result.Document!.Solid.Box.Exposures;
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
        Assert.Equal([10, 8, 6], document.Solid.Box.Size);
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
        Assert.Contains(doc.Solid.Box.Exposures, e => e.Alias == "left" && e.Selector == "face(-X)");
        Assert.Contains(doc.Solid.Box.Exposures, e => e.Alias == "right" && e.Selector == "face(+X)");
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
        Assert.Contains(doc.Solid.Box.Exposures, e => e.Alias == "right" && e.Selector == "face(+X)");
        Assert.Contains(doc.Solid.Box.Exposures, e => e.Alias == "left" && e.Selector == "face(-X)");
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
        Assert.Contains(doc.Solid.Box.Exposures, e => e.Alias == "front" && e.Selector == "face(+Y)");
        Assert.Contains(doc.Solid.Box.Exposures, e => e.Alias == "back" && e.Selector == "face(-Y)");
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
        Assert.Contains(doc.Solid.Box.Exposures, e => e.Alias == "top" && e.Selector == "face(+Z)");
        Assert.Contains(doc.Solid.Box.Exposures, e => e.Alias == "bottom" && e.Selector == "face(-Z)");
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

    private static string Source(string relative)
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/FirmamentV2", relative));
        var lines = File.ReadAllLines(path);
        var bodyStart = Array.FindIndex(lines, line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("//", StringComparison.Ordinal));
        return string.Join(Environment.NewLine, lines.Skip(Math.Max(0, bodyStart)));
    }
}
