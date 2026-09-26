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

    [Fact]
    public void ModelScope_OffersSchemaOwnedThreadAndBox()
    {
        const string thread = "Model M {\n Thr";
        var found = FirmamentLanguageService.Complete(thread, "draft.firmament", "3", thread.Length);
        Assert.Contains(found.Entries!, entry => entry.Name == "Thread");
        const string box = "Model M {\n Bo";
        Assert.Contains(FirmamentLanguageService.Complete(box, "draft.firmament", "3", box.Length).Entries!, entry => entry.Name == "Box");
    }

    [Fact]
    public void ThreadFieldsAndBoundedValues_ComeFromSchema()
    {
        const string fields = "Model M { Thread T {\n Maj";
        var found = FirmamentLanguageService.Complete(fields, "draft.firmament", "4", fields.Length);
        Assert.Contains(found.Fields, field => field.Name == "MajorDiameter");
        const string emptyThread = "Model M { Thread T {\n ";
        var available = FirmamentLanguageService.Complete(emptyThread, "draft.firmament", "4", emptyThread.Length);
        Assert.Contains(available.Fields, field => field.Name == "Surface");
        Assert.Contains(available.Fields, field => field.Name == "Pitch");
        Assert.Contains(available.Fields, field => field.Name == "Length");
        const string value = "Model M { Thread T {\n Hand: R";
        Assert.Equal(["Right"], FirmamentLanguageService.Complete(value, "draft.firmament", "5", value.Length).Values);
    }

    [Fact]
    public void Analysis_UsesParserErrorsAndSchemaClassificationWithoutGeometry()
    {
        const string source = "Model M {\n Units: mm\n Thread T {\n Wrong: 1mm\n }\n Box Body { Size: [8mm, 8mm, 8mm] }\n}";
        var analysis = FirmamentLanguageAnalysisService.Analyze(source, "test.firmament", "6");
        Assert.Equal("6", analysis.Revision);
        Assert.Contains(analysis.Tokens, token => token.Kind == "construct" && source.Substring(token.Start, token.Length) == "Thread");
        Assert.Contains(analysis.Tokens, token => token.Kind == "field" && source.Substring(token.Start, token.Length) == "Size");
        Assert.Contains(analysis.Diagnostics, diagnostic => diagnostic.Code.Contains("thread-field-invalid", StringComparison.Ordinal));
        var hover = FirmamentLanguageAnalysisService.Hover(source, "test.firmament", "6", source.IndexOf("Thread", StringComparison.Ordinal) + 2);
        Assert.Contains("thread", hover!.Description, StringComparison.OrdinalIgnoreCase);
        var definition = FirmamentLanguageAnalysisService.Definition(source + "\nT", "test.firmament", source.Length + 1);
        Assert.Equal(source.IndexOf("T {", StringComparison.Ordinal), definition!.Start);
    }

    [Fact]
    public void Formatter_PreservesCommentsAndIsIdempotentForSimpleModel()
    {
        const string source = "Model M {\n Units: mm\n // important stock\n Box Body { Size: [10mm, 8mm, 4mm] }\n}\n";
        var once = FirmamentLanguageAnalysisService.Format(source, "m.firmament", "7");
        var twice = FirmamentLanguageAnalysisService.Format(once.Text, "m.firmament", "8");
        Assert.Contains("// important stock", once.Text);
        Assert.Equal(once.Text, twice.Text);
        var before = FirmamentV2Parser.Parse(source);
        var after = FirmamentV2Parser.Parse(once.Text);
        Assert.True(after.IsSuccess);
        Assert.Equal(before.Document!.ModelName, after.Document!.ModelName);
        Assert.Equal(before.Document.Units, after.Document.Units);
        Assert.Equal(before.Document.Solids[0].Name, after.Document.Solids[0].Name);
        Assert.Equal(((FirmamentV2BoxRecord)before.Document.Solids[0].Primitive).Size,
            ((FirmamentV2BoxRecord)after.Document.Solids[0].Primitive).Size);
    }
}
