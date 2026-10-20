using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentFrontendX1Tests
{
    [Fact]
    public void SharedLexer_IsLosslessAndPreservesNumericAndStringTokens()
    {
        var source = Read("lexical");
        var parsed = Parse(source);
        Assert.Equal(source, string.Concat(parsed.Document.Tokens.Select(t => t.Text)));
        Assert.Equal(SyntaxTokenKind.End, parsed.Document.Tokens[^1].Kind);
        Assert.Contains(parsed.Document.Tokens, t => t.Kind == SyntaxTokenKind.Number && t.Text == "1e-3");
        Assert.Contains(parsed.Document.Tokens, t => t.Kind == SyntaxTokenKind.Number && t.Text == ".5");
        Assert.Contains(parsed.Document.Tokens, t => t.Kind == SyntaxTokenKind.String && t.Text.Contains("// not a comment"));
        foreach (var token in parsed.Document.Tokens)
            Assert.Equal(token.Text, source.Substring(token.Span.Start, token.Span.Length));
        Assert.Equal("StructuralOnlyUnbound", parsed.ValidationLevel);
    }

    [Theory]
    [InlineData("fixtures/Canonical/Templates/nested-template-pmi.firmament")]
    [InlineData("fixtures/Canonical/AssemblyInterfaces/GuitarX0/pickup.firmament")]
    [InlineData("fixtures/Canonical/AssemblyInterfaces/IndustrialAtlas/atlas-industrial.firmament")]
    [InlineData("fixtures/Canonical/Scene/warehouse.firmament")]
    [InlineData("fixtures/Canonical/Scene/WarmModernHouse/house.firmament")]
    [InlineData("fixtures/Canonical/Symmetry/radial-bolt-circle.firmament")]
    [InlineData("fixtures/Canonical/PMI/counterbore-shaft-diameter.firmament")]
    [InlineData("fixtures/Canonical/Revolve/sphere.firmament")]
    [InlineData("fixtures/Regression/CanonicalGeometry/profile-chamfer-reflex-junction-top.firmament")]
    [InlineData("fixtures/Regression/ProfileComposition/valid/semantic-split-compose-chamfer.firmament")]
    public void EstablishedSource_ProducesStructuredBodiesBeforeExpansion(string path)
    {
        var parsed = Parse(File.ReadAllText(Root(path)));
        var declarations = FirmamentSyntaxEvidence.Declarations(parsed.Document).ToArray();
        Assert.NotEmpty(declarations);
        Assert.All(declarations, d => Assert.True(d.Span.Length > 0));
        Assert.Equal(FirmamentSyntaxEvidence.Fingerprint(parsed.Document), FirmamentSyntaxEvidence.Fingerprint(Parse(File.ReadAllText(Root(path))).Document));
    }

    [Fact]
    public void NestedGenericsAndTemplateBodies_AreStructuredNotOpaqueText()
    {
        var parsed = Parse(Read("nested-generics"));
        var family = Assert.IsType<DeclarationSyntax>(Assert.Single(parsed.Document.Statements));
        Assert.Equal("Struct", family.Type.Name.Text);
        var template = Assert.Single(family.Modifiers);
        Assert.Equal("Template", template.Name.Text);
        var outer = Assert.IsType<SpecializationSyntax>(template.Arguments[0].Value);
        Assert.IsType<SpecializationSyntax>(Assert.Single(outer.Arguments).Value);
        Assert.NotNull(template.Arguments[1].Default);
        Assert.Equal(2, family.Body!.Statements.Count);
        var child = Assert.IsType<DeclarationSyntax>(family.Body.Statements[1]);
        var invocation = Assert.IsType<SpecializationSyntax>(child.Initializer);
        Assert.Equal("Spec", invocation.Arguments[0].Name!.Text);
        Assert.IsType<SpecializationSyntax>(invocation.Arguments[0].Value);
    }

    [Fact]
    public void Expressions_RetainPrecedenceCallsMemberIndexAndWithStructure()
    {
        var declarations = Parse(Read("expressions")).Document.Statements.Cast<DeclarationSyntax>().ToArray();
        var add = Assert.IsType<BinarySyntax>(declarations[0].Initializer);
        Assert.Equal(BinaryOperator.Add, add.Operator);
        var multiply = Assert.IsType<BinarySyntax>(add.Right);
        Assert.Equal(BinaryOperator.Multiply, multiply.Operator);
        Assert.Equal(BinaryOperator.Power, Assert.IsType<BinarySyntax>(multiply.Right).Operator);
        var point = Assert.IsType<CallSyntax>(declarations[1].Initializer);
        var shifted = Assert.IsType<BinarySyntax>(point.Arguments[0].Value);
        Assert.IsType<IndexSyntax>(Assert.IsType<MemberSyntax>(shifted.Left).Target);
        Assert.IsType<WithSyntax>(declarations[2].Initializer);
        Assert.Equal(BinaryOperator.Or, Assert.IsType<BinarySyntax>(declarations[3].Initializer).Operator);
    }

    [Theory]
    [InlineData("unrelated-static")]
    [InlineData("unrelated-slot")]
    [InlineData("unrelated-pattern")]
    [InlineData("unrelated-profile")]
    public void UnrelatedSiblings_DoNotChangeAnyExistingSubtree(string sibling)
    {
        var source = File.ReadAllText(Root("fixtures/Regression/FrontendX0/combined-fillet-hole.firmament"));
        var before = Assert.IsType<DeclarationSyntax>(Assert.Single(Parse(source).Document.Statements));
        var insertion = Read(sibling) + "\n";
        var afterSource = source.Insert(source.LastIndexOf('}'), insertion);
        var after = Assert.IsType<DeclarationSyntax>(Assert.Single(Parse(afterSource).Document.Statements));
        Assert.Equal(before.Body!.Statements.Select(FirmamentSyntaxEvidence.Normalize),
            after.Body!.Statements.Take(before.Body.Statements.Count).Select(FirmamentSyntaxEvidence.Normalize));
        // Offsets shift when inserted before the declaration as well.
        var prefixed = Parse(insertion + source).Document.Statements.OfType<DeclarationSyntax>().Single(d => d.Type.Name.Text == "Model");
        Assert.Equal(FirmamentSyntaxEvidence.Normalize(before), FirmamentSyntaxEvidence.Normalize(prefixed));
    }

    [Fact]
    public void ProductionRejectsUnconsumedComposeIntent()
    {
        var source = File.ReadAllText(Root("fixtures/Regression/FrontendX0/unknown-compose-statement.firmament"));
        var syntax = Parse(source);
        var fillet = Assert.Single(FirmamentSyntaxEvidence.Declarations(syntax.Document), d => d.Type.Name.Text == "Fillet");
        Assert.Equal("LostIntent", fillet.Name!.Text);
        var rejected = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(rejected.IsSuccess);
        Assert.Contains(rejected.Diagnostics, d => d.Message.Contains("Compose:Fillet", StringComparison.Ordinal));
        var admitted = FirmamentV2Parser.Parse(source);
        Assert.False(admitted.IsSuccess);
        Assert.Equal("invalid", FirmamentBuildAdmissionValidation.Admit(FirmamentV2ValidationReportBuilder.Build(admitted), source).Status);
        var boundary = File.ReadAllText(Root("fixtures/Regression/FrontendX0/unknown-boundary-field.firmament"));
        var shape = Assert.Single(FirmamentSyntaxEvidence.Declarations(Parse(boundary).Document), d => d.Name?.Text == "RecessGuide");
        var unknown = Assert.Single(shape.Body!.Statements.OfType<FieldSyntax>(), f => f.Name.Text == "heigth");
        Assert.Equal("heigth", boundary.Substring(unknown.Name.Span.Start, unknown.Name.Span.Length));
    }

    [Fact]
    public void InvalidExpression_RejectsAndRecoversIntoNextDeclaration_WithOriginalLocation()
    {
        var source = Read("malformed-field");
        var result = FirmamentSyntaxParser.Parse(source, "broken.firmament");
        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("broken.firmament", diagnostic.SourceFile);
        Assert.Equal(";", source.Substring(diagnostic.SourceSpan!.Start, diagnostic.SourceSpan.Length));
        Assert.Equal(2, diagnostic.Line);
        Assert.Equal("Box Body", diagnostic.Target);
        Assert.Contains(FirmamentSyntaxEvidence.Declarations(result.Document), d => d.Name?.Text == "Retained");
    }

    [Theory]
    [InlineData("/* unfinished")]
    [InlineData("Static label = \"unfinished")]
    public void UnterminatedLexerTokens_AreErrors(string source)
    {
        var result = FirmamentSyntaxParser.Parse(source);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == "firmament-syntax-unterminated-token");
    }

    [Fact]
    public void ExcessiveNesting_IsBoundedAndDiagnostic()
    {
        var source = "Static x = " + new string('(', 300) + "1" + new string(')', 300);
        var result = FirmamentSyntaxParser.Parse(source);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == "firmament-syntax-depth");
        Assert.NotEmpty(FirmamentSyntaxEvidence.Fingerprint(result.Document));
    }

    [Fact]
    public void MalformedOccurrence_RecoveryAdvancesPastUnexpectedScopeTokens()
    {
        var result = FirmamentSyntaxParser.Parse("<Assembly A> } </Assembly>");
        Assert.False(result.IsSuccess);
        Assert.Single(result.Diagnostics);
    }

    [Fact]
    public void WrappingInModelOrStruct_PreservesTheExistingDeclaration()
    {
        var source = File.ReadAllText(Root("fixtures/Regression/FrontendX0/combined-fillet-hole.firmament"));
        var box = Assert.Single(FirmamentSyntaxEvidence.Declarations(Parse(source).Document), d => d.Type.Name.Text == "Box");
        var original = source.Substring(box.Span.Start, box.Span.Length);
        foreach (var owner in new[] { "Model Wrapper", "Struct Wrapper" })
        {
            var wrapped = Assert.Single(FirmamentSyntaxEvidence.Declarations(Parse(owner + " {\n" + original + "\n}").Document), d => d.Type.Name.Text == "Box");
            Assert.Equal(FirmamentSyntaxEvidence.Normalize(box), FirmamentSyntaxEvidence.Normalize(wrapped));
        }
    }

    [Fact]
    public void Comparison_DetectsActualRecordDimensionDifferences()
    {
        var source = File.ReadAllText(Root("fixtures/Canonical/Basics/box.firmament"));
        var production = FirmamentV2Parser.Parse(source);
        Assert.True(production.IsSuccess);
        var solid = Assert.Single(production.Document!.Solids);
        var changed = production with { Document = production.Document with
        { Solids = [solid with { Primitive = new FirmamentV2BoxRecord([81d, 50d, 25d], []) }] } };
        var report = FirmamentShadowAnalysis.Compare(Parse(source), changed, source);
        Assert.Contains(report.Comparison.Differences, d => d.Code == "box-size-mismatch" && d.SourceSpan is not null);
    }

    [Fact]
    public void PrincipalFrame_IsRetainedAsAuthoredAndAdmittedWithItsOrientation()
    {
        var source = File.ReadAllText(Root("fixtures/Regression/FrontendX0/unsupported-xz-layout.firmament"));
        var declaration = Assert.Single(FirmamentSyntaxEvidence.Declarations(Parse(source).Document), d => d.Name?.Text == "Layout");
        var frame = Assert.IsType<ReferenceSyntax>(Assert.Single(declaration.Clauses).Value);
        Assert.Equal("XZ", frame.Name.Text);
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => d.Message)));
        Assert.Equal([60d, 0, 30], new Analysis.FirmamentMeasurements(built.Value).Bounds!.MaximumMm);
    }

    [Fact]
    public void UnusedSlot_DoesNotChangeAdmissionOrCompiledGeometry()
    {
        var source = Read("slot-route-perturbation");
        var syntax = Parse(source);
        var slot = Assert.Single(FirmamentSyntaxEvidence.Declarations(syntax.Document), d => d.Type.Name.Text == "Slot2");
        var baseline = source.Remove(slot.Span.Start, slot.Span.Length);
        var before = FirmamentBuildAndExport.CompileSource(baseline);
        var after = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(before.IsSuccess, string.Join("; ", before.Diagnostics.Select(d => d.Message)));
        Assert.True(after.IsSuccess, string.Join("; ", after.Diagnostics.Select(d => d.Message)));
        Assert.Equal(before.Value.StepText, after.Value.StepText);
        Assert.Equal("UnifiedMechanical", FirmamentV2Parser.Parse(baseline).ParserRoute);
        Assert.Equal("UnifiedMechanical", FirmamentV2Parser.Parse(source).ParserRoute);
        Assert.Equal("valid", FirmamentBuildAdmissionValidation.Admit(FirmamentV2ValidationReportBuilder.Build(FirmamentV2Parser.Parse(source)), source).Status);
        var original = Assert.IsType<DeclarationSyntax>(Assert.Single(Parse(baseline).Document.Statements));
        var changed = Assert.IsType<DeclarationSyntax>(Assert.Single(syntax.Document.Statements));
        Assert.Equal(original.Body!.Statements.Select(FirmamentSyntaxEvidence.Normalize),
            changed.Body!.Statements.Take(original.Body.Statements.Count).Select(FirmamentSyntaxEvidence.Normalize));
    }

    private static SyntaxParseResult Parse(string source)
    {
        var parsed = FirmamentSyntaxParser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("; ", parsed.Diagnostics.Select(d => $"{d.Line}:{d.Column} {d.Message}")));
        return parsed;
    }
    private static string Read(string name) => File.ReadAllText(Root($"fixtures/Regression/FrontendX1/{name}.firmament"));
    private static string Root(string path) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../..", path));
}
