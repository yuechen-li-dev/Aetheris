using Aetheris.Kernel.Firmament.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentValidationRequiredFieldValidationTests
{
    [Fact]
    public void Compiler_Accepts_ValidExpectExists() =>
        AssertValidationSuccess(
            """
            {
              "firmament": { "version": "1" },
              "model": { "name": "demo", "units": "mm" },
              "ops": [
                { "op": "expect_exists", "target": "base.top_face" }
              ]
            }
            """);

    [Fact]
    public void Compiler_Accepts_ValidExpectSelectable() =>
        AssertValidationSuccess(
            """
            {
              "firmament": { "version": "1" },
              "model": { "name": "demo", "units": "mm" },
              "ops": [
                { "op": "expect_selectable", "target": "hole", "count": 4 }
              ]
            }
            """);

    [Fact]
    public void Compiler_Accepts_ValidExpectManifold_WithoutPayload() =>
        AssertValidationSuccess(
            """
            {
              "firmament": { "version": "1" },
              "model": { "name": "demo", "units": "mm" },
              "ops": [
                { "op": "expect_manifold" }
              ]
            }
            """);

    [Fact]
    public void Compiler_Rejects_ExpectExists_MissingTarget() =>
        AssertValidationFailure(
            """
            {
              "firmament": { "version": "1" },
              "model": { "name": "demo", "units": "mm" },
              "ops": [
                { "op": "expect_exists" }
              ]
            }
            """,
            FirmamentDiagnosticCodes.ValidationMissingRequiredField,
            "Validation op 'expect_exists' at index 0 is missing required field 'target'.");

    [Fact]
    public void Compiler_Rejects_ExpectSelectable_MissingTarget() =>
        AssertValidationFailure(
            """
            {
              "firmament": { "version": "1" },
              "model": { "name": "demo", "units": "mm" },
              "ops": [
                { "op": "expect_selectable", "count": 2 }
              ]
            }
            """,
            FirmamentDiagnosticCodes.ValidationMissingRequiredField,
            "Validation op 'expect_selectable' at index 0 is missing required field 'target'.");

    [Fact]
    public void Compiler_Rejects_ExpectSelectable_MissingCount() =>
        AssertValidationFailure(
            """
            {
              "firmament": { "version": "1" },
              "model": { "name": "demo", "units": "mm" },
              "ops": [
                { "op": "expect_selectable", "target": "base" }
              ]
            }
            """,
            FirmamentDiagnosticCodes.ValidationMissingRequiredField,
            "Validation op 'expect_selectable' at index 0 is missing required field 'count'.");

    [Fact]
    public void Compiler_Rejects_ExpectSelectable_NonNumericCount() =>
        AssertValidationFailure(
            """
            {
              "firmament": { "version": "1" },
              "model": { "name": "demo", "units": "mm" },
              "ops": [
                { "op": "expect_selectable", "target": "base", "count": "many" }
              ]
            }
            """,
            FirmamentDiagnosticCodes.ValidationInvalidFieldTypeOrShape,
            "Validation op 'expect_selectable' at index 0 has invalid field 'count'; expected a numeric scalar value.");

    [Fact]
    public void Compiler_Rejects_ExpectSelectable_NonIntegerCount() =>
        AssertValidationFailure(
            """
            {
              "firmament": { "version": "1" },
              "model": { "name": "demo", "units": "mm" },
              "ops": [
                { "op": "expect_selectable", "target": "base", "count": 1.5 }
              ]
            }
            """,
            FirmamentDiagnosticCodes.ValidationInvalidFieldValue,
            "Validation op 'expect_selectable' at index 0 has invalid field 'count' value; expected an integer-valued number greater than 0.");

    [Fact]
    public void Compiler_Rejects_ExpectSelectable_NonPositiveCount() =>
        AssertValidationFailure(
            """
            {
              "firmament": { "version": "1" },
              "model": { "name": "demo", "units": "mm" },
              "ops": [
                { "op": "expect_selectable", "target": "base", "count": 0 }
              ]
            }
            """,
            FirmamentDiagnosticCodes.ValidationInvalidFieldValue,
            "Validation op 'expect_selectable' at index 0 has invalid field 'count' value; expected an integer-valued number greater than 0.");

    [Fact]
    public void Compiler_Rejects_ExpectSelectable_MissingCount_Deterministically()
    {
        var source =
            """
            {
              "firmament": { "version": "1" },
              "model": { "name": "demo", "units": "mm" },
              "ops": [
                { "op": "expect_selectable", "target": "base" }
              ]
            }
            """;

        var compiler = new FirmamentCompiler();
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

    private static void AssertValidationSuccess(string source)
    {
        var compiler = new FirmamentCompiler();

        var result = compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));

        Assert.True(result.Compilation.IsSuccess);
        Assert.NotNull(result.Compilation.Value.ParsedDocument);
    }

    private static void AssertValidationFailure(string source, FirmamentDiagnosticCode expectedCode, string expectedMessageSuffix)
    {
        var compiler = new FirmamentCompiler();

        var result = compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));

        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);

        Assert.Contains($"[{expectedCode.Value}]", diagnostic.Message, StringComparison.Ordinal);
        Assert.EndsWith(expectedMessageSuffix, diagnostic.Message, StringComparison.Ordinal);
    }
}
