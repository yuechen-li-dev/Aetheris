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

    private static string Source(string relative)
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/FirmamentV2", relative));
        var lines = File.ReadAllLines(path);
        var bodyStart = Array.FindIndex(lines, line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("//", StringComparison.Ordinal));
        return string.Join(Environment.NewLine, lines.Skip(Math.Max(0, bodyStart)));
    }
}
