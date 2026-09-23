using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentLanguageServiceTests
{
    [Fact]
    public void IncompleteHelix_OffersOwnerFieldsAndMissingRequiredFields()
    {
        const string source = "Model M { WireForm Spring { Helix Winding {\n    Rad";
        var result = FirmamentLanguageService.Complete(source, "spring.firmament", "draft-7", source.Length);
        Assert.Equal("draft-7", result.Revision);
        Assert.Equal("Helix", result.Context);
        Assert.Equal("Rad", source.Substring(result.ReplaceStart, result.ReplaceLength));
        var radius = Assert.Single(result.Fields);
        Assert.Equal("Radius", radius.Name);
        Assert.Equal("Length", radius.Type);
        Assert.Contains("Radius", result.MissingRequiredFields);
        Assert.Contains("Turns", result.MissingRequiredFields);
        Assert.Contains("Pitch or Height", result.MissingRequiredFields);
    }

    [Fact]
    public void LoftFields_UseParserAdmissionAndDoNotRepeatPresentFields()
    {
        const string source = "Model M { Loft<Hollow> Body {\nRearProfile: Rear\n    ";
        var result = FirmamentLanguageService.Complete(source, "loft.firmament", "1", source.Length);
        Assert.Equal("Loft", result.Context);
        Assert.DoesNotContain(result.Fields, field => field.Name == "RearProfile");
        Assert.Contains(result.Fields, field => field.Name == "Thickness" && field.Required && field.Type == "Length");
        Assert.Contains(result.Fields, field => field.Name == "Rule" && field.Choices!.SequenceEqual(["Ruled"]));
    }

    [Fact]
    public void ValuePosition_DoesNotOfferFieldNames()
    {
        const string source = "Helix Winding {\n    Radius: ";
        var result = FirmamentLanguageService.Complete(source, "draft.firmament", "2", source.Length);
        Assert.Equal("Value", result.Context);
        Assert.Empty(result.Fields);
    }
}
