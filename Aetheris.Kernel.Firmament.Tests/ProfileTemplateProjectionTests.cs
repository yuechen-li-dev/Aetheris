using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Xunit;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ProfileTemplateProjectionTests
{
    [Fact]
    public void RecordMemberValidationDoesNotInterpretStringContentsAsReferences()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        var source = File.ReadAllText(Path.Combine(directory!.FullName, "fixtures/Regression/BodyStateDebt/record-member-string.firmament"));
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join(";", parsed.Diagnostics));
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join(";", built.Diagnostics.Select(d => d.Message)));
        Assert.Equal(new[] { 40d, 20d, 5d }, parsed.Document!.Solid.Box!.Size);
        var invalid = FirmamentV2Parser.Parse(source.Replace("[spec.width,", "[spec.Missing,"));
        Assert.False(invalid.IsSuccess);
        Assert.Contains(invalid.Diagnostics, d => d.StartsWith("firmament-template-unknown-record-member:spec.Missing", StringComparison.Ordinal));
    }

    [Fact]
    public void SpecializationRewritesBoundGuidesWithoutCorruptingFieldsOrStrings()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        var body = File.ReadAllText(Path.Combine(directory!.FullName, "fixtures/Regression/BodyStateDebt/profile-hygiene.fragment.firmament"));
        var errors = new List<string>();
        var output = ProfileTemplateProjection.Render("P", "", body, errors);
        Assert.Empty(errors);
        var syntax = FirmamentSyntaxParser.Parse(output);
        Assert.True(syntax.IsSuccess);
        var declarations = FirmamentSyntaxTraversal.Declarations(syntax.Document).ToArray();
        Assert.Contains(declarations, d => d.Name?.Text == "P_Local");
        Assert.Equal(2, declarations.Count(d => d.Type.Name.Text == "Point2" && d.Name?.Text == "P_A"));
        Assert.Contains(declarations, d => d.Type.Name.Text == "Segment" && d.Name?.Text == "P_A");
        var members = FirmamentSyntaxTraversal.Expressions(syntax.Document).OfType<MemberSyntax>()
            .Select(m => FirmamentSyntaxValues.Reference(m)).ToArray();
        Assert.Contains("P_Local.P_A", members);
        Assert.Contains("External.A", members);
        Assert.DoesNotContain("External.P_A", members);
        Assert.Contains("\"A Local.A External.A\"", output);
        Assert.Contains("A: \"A\"", output);
        Assert.DoesNotContain(declarations.Single(d => d.Type.Name.Text == "Profile").Body!.Statements.OfType<DeclarationSyntax>(), d => d.Type.Name.Text == "Point2");
    }
}
