using Aetheris.Kernel.Core.Diagnostics;
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
    public void Compiler_Accepts_ValidMinimalTopLevelSkeleton()
    {
        var compiler = new FirmamentCompiler();
        var source = """
        {
          "firmament": { "version": "1" },
          "model": { "name": "demo", "units": "mm" },
          "ops": []
        }
        """;

        var result = compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));

        Assert.True(result.Compilation.IsSuccess);
        var artifact = result.Compilation.Value;
        Assert.Equal("firmament-ops-structure-parsed", artifact.ArtifactKind);
        Assert.NotNull(artifact.ParsedDocument);
        Assert.Equal("1", artifact.ParsedDocument!.Firmament.Version);
        Assert.Equal("demo", artifact.ParsedDocument.Model.Name);
        Assert.Equal("mm", artifact.ParsedDocument.Model.Units);
        Assert.Empty(artifact.ParsedDocument.Ops.Entries);
    }

    [Fact]
    public void Compiler_Accepts_StructurallyValidSingleOp() =>
        AssertValidOpsCount(
            """
            {
              "firmament": { "version": "1" },
              "model": { "name": "demo", "units": "mm" },
              "ops": [
                { "op": "box" }
              ]
            }
            """,
            1);

    [Fact]
    public void Compiler_Accepts_StructurallyValidMultipleOps() =>
        AssertValidOpsCount(
            """
            {
              "firmament": { "version": "1" },
              "model": { "name": "demo", "units": "mm" },
              "ops": [
                { "op": "box" },
                { "op": "subtract", "target": "a" }
              ]
            }
            """,
            2);

    [Fact]
    public void Compiler_Rejects_OpEntry_MissingOp() =>
        AssertSingleValidationError(
            """
            {
              "firmament": { "version": "1" },
              "model": { "name": "demo", "units": "mm" },
              "ops": [
                { "target": "a" }
              ]
            }
            """,
            FirmamentDiagnosticCodes.StructureMissingRequiredOpField,
            "Operation entry at index 0 is missing required field 'op'.");

    [Fact]
    public void Compiler_Rejects_OpEntry_WrongShape() =>
        AssertSingleValidationError(
            """
            {
              "firmament": { "version": "1" },
              "model": { "name": "demo", "units": "mm" },
              "ops": [
                "not-an-object"
              ]
            }
            """,
            FirmamentDiagnosticCodes.StructureInvalidOpsEntryShape,
            "Operation entry at index 0 must be an object with fields.");

    [Fact]
    public void Compiler_Rejects_OpValue_InvalidShape() =>
        AssertSingleValidationError(
            """
            {
              "firmament": { "version": "1" },
              "model": { "name": "demo", "units": "mm" },
              "ops": [
                { "op": "" }
              ]
            }
            """,
            FirmamentDiagnosticCodes.StructureInvalidOpFieldValue,
            "Operation entry at index 0 has invalid 'op' value; expected a non-empty scalar.");

    [Fact]
    public void Compiler_Rejects_OpEntry_MissingOp_Deterministically()
    {
        var compiler = new FirmamentCompiler();
        var source = """
        {
          "firmament": { "version": "1" },
          "model": { "name": "demo", "units": "mm" },
          "ops": [
            { "target": "a" }
          ]
        }
        """;

        var first = compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));
        var second = compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));

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
    public void Compiler_Rejects_MissingFirmament_And_Diagnostics_AreDeterministic()
    {
        var compiler = new FirmamentCompiler();
        var source = """
        {
          "model": { "name": "demo", "units": "mm" },
          "ops": []
        }
        """;

        var first = compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));
        var second = compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));

        Assert.False(first.Compilation.IsSuccess);
        Assert.False(second.Compilation.IsSuccess);

        var firstDiagnostic = Assert.Single(first.Compilation.Diagnostics);
        var secondDiagnostic = Assert.Single(second.Compilation.Diagnostics);

        Assert.Equal(KernelDiagnosticCode.ValidationFailed, firstDiagnostic.Code);
        Assert.Equal(firstDiagnostic.Code, secondDiagnostic.Code);
        Assert.Equal(firstDiagnostic.Severity, secondDiagnostic.Severity);
        Assert.Equal(firstDiagnostic.Message, secondDiagnostic.Message);
        Assert.Equal(firstDiagnostic.Source, secondDiagnostic.Source);
        Assert.Contains(FirmamentDiagnosticCodes.StructureMissingRequiredSection.Value, firstDiagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compiler_Rejects_MissingModel() =>
        AssertSingleValidationError(
            """
            {
              "firmament": { "version": "1" },
              "ops": []
            }
            """,
            FirmamentDiagnosticCodes.StructureMissingRequiredSection,
            "Missing required top-level section 'model'.");

    [Fact]
    public void Compiler_Rejects_MissingOps() =>
        AssertSingleValidationError(
            """
            {
              "firmament": { "version": "1" },
              "model": { "name": "demo", "units": "mm" }
            }
            """,
            FirmamentDiagnosticCodes.StructureMissingRequiredSection,
            "Missing required top-level section 'ops'.");

    [Fact]
    public void Compiler_Rejects_MissingVersion() =>
        AssertSingleValidationError(
            """
            {
              "firmament": { },
              "model": { "name": "demo", "units": "mm" },
              "ops": []
            }
            """,
            FirmamentDiagnosticCodes.StructureMissingRequiredField,
            "Missing required field 'version' in section 'firmament'.");

    [Fact]
    public void Compiler_Rejects_MissingName() =>
        AssertSingleValidationError(
            """
            {
              "firmament": { "version": "1" },
              "model": { "units": "mm" },
              "ops": []
            }
            """,
            FirmamentDiagnosticCodes.StructureMissingRequiredField,
            "Missing required field 'name' in section 'model'.");

    [Fact]
    public void Compiler_Rejects_MissingUnits() =>
        AssertSingleValidationError(
            """
            {
              "firmament": { "version": "1" },
              "model": { "name": "demo" },
              "ops": []
            }
            """,
            FirmamentDiagnosticCodes.StructureMissingRequiredField,
            "Missing required field 'units' in section 'model'.");

    [Fact]
    public void Compiler_Rejects_UnknownTopLevelSection() =>
        AssertSingleValidationError(
            """
            {
              "firmament": { "version": "1" },
              "model": { "name": "demo", "units": "mm" },
              "ops": [],
              "other": {}
            }
            """,
            FirmamentDiagnosticCodes.StructureUnknownTopLevelSection,
            "Unknown top-level section 'other'.");

    [Fact]
    public void DiagnosticTaxonomyIdentifiers_AreStableAndNonEmpty()
    {
        Assert.Equal("firmament", FirmamentDiagnosticConventions.Source);
        Assert.StartsWith("FIRM-PARSE", FirmamentDiagnosticCodes.ParseInvalidDocumentSyntax.Value);
        Assert.StartsWith("FIRM-STRUCT", FirmamentDiagnosticCodes.StructureMissingRequiredSection.Value);
        Assert.StartsWith("FIRM-STRUCT", FirmamentDiagnosticCodes.StructureUnknownTopLevelSection.Value);
        Assert.StartsWith("FIRM-STRUCT", FirmamentDiagnosticCodes.StructureMissingRequiredField.Value);
        Assert.StartsWith("FIRM-STRUCT", FirmamentDiagnosticCodes.StructureInvalidSectionShape.Value);
        Assert.StartsWith("FIRM-STRUCT", FirmamentDiagnosticCodes.StructureInvalidOpsEntryShape.Value);
        Assert.StartsWith("FIRM-STRUCT", FirmamentDiagnosticCodes.StructureMissingRequiredOpField.Value);
        Assert.StartsWith("FIRM-STRUCT", FirmamentDiagnosticCodes.StructureInvalidOpFieldValue.Value);
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

    private static void AssertValidOpsCount(string source, int expectedOps)
    {
        var compiler = new FirmamentCompiler();
        var result = compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));

        Assert.True(result.Compilation.IsSuccess);
        Assert.Equal(expectedOps, result.Compilation.Value.ParsedDocument!.Ops.Entries.Count);
    }

    private static void AssertSingleValidationError(string source, FirmamentDiagnosticCode expectedFirmamentCode, string expectedMessageTail)
    {
        var compiler = new FirmamentCompiler();

        var result = compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));

        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.ValidationFailed, diagnostic.Code);
        Assert.Equal(FirmamentDiagnosticConventions.Source, diagnostic.Source);
        Assert.Equal($"[{expectedFirmamentCode.Value}] {expectedMessageTail}", diagnostic.Message);
    }
}
