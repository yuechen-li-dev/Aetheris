using Aetheris.Kernel.Firmament.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentScaffoldTests
{
    [Fact]
    public void CompileBoundaryContracts_CanBeConstructed()
    {
        var document = new FirmamentSourceDocument("", SourceName: "empty.firmament", LanguageVersion: "0");
        var request = new FirmamentCompileRequest(document);
        var result = new FirmamentCompileResult(
            Aetheris.Kernel.Core.Results.KernelResult<FirmamentCompilationArtifact>.Failure());

        Assert.Same(document, request.Document);
        Assert.False(result.Compilation.IsSuccess);
    }

    [Fact]
    public void CompilerStub_ReturnsDeterministicNotImplementedFailure()
    {
        var compiler = new FirmamentCompiler();
        var request = new FirmamentCompileRequest(new FirmamentSourceDocument(string.Empty));

        var first = compiler.Compile(request);
        var second = compiler.Compile(request);

        Assert.False(first.Compilation.IsSuccess);
        Assert.False(second.Compilation.IsSuccess);

        var firstDiagnostic = Assert.Single(first.Compilation.Diagnostics);
        var secondDiagnostic = Assert.Single(second.Compilation.Diagnostics);

        Assert.Equal(firstDiagnostic.Code, secondDiagnostic.Code);
        Assert.Equal(firstDiagnostic.Severity, secondDiagnostic.Severity);
        Assert.Equal(firstDiagnostic.Message, secondDiagnostic.Message);
        Assert.Equal(firstDiagnostic.Source, secondDiagnostic.Source);
    }

    [Fact]
    public void DiagnosticTaxonomyIdentifiers_AreStableAndNonEmpty()
    {
        Assert.Equal("firmament", FirmamentDiagnosticConventions.Source);
        Assert.StartsWith("FIRM-PARSE", FirmamentDiagnosticCodes.ParsePlaceholder.Value);
        Assert.StartsWith("FIRM-STRUCT", FirmamentDiagnosticCodes.StructurePlaceholder.Value);
        Assert.StartsWith("FIRM-REF", FirmamentDiagnosticCodes.ReferencePlaceholder.Value);
        Assert.StartsWith("FIRM-SEL", FirmamentDiagnosticCodes.SelectorPlaceholder.Value);
        Assert.StartsWith("FIRM-SCHEMA", FirmamentDiagnosticCodes.SchemaPlaceholder.Value);
        Assert.StartsWith("FIRM-LOWER", FirmamentDiagnosticCodes.LoweringPlaceholder.Value);
    }

    [Fact]
    public void SourceLocationContracts_ExposeExpectedStructure()
    {
        var start = new FirmamentSourcePosition(1, 1);
        var end = new FirmamentSourcePosition(1, 5);
        var span = new FirmamentSourceSpan(start, end);

        Assert.Equal(1, span.Start.Line);
        Assert.Equal(1, span.Start.Column);
        Assert.Equal(1, span.End.Line);
        Assert.Equal(5, span.End.Column);
    }
}
