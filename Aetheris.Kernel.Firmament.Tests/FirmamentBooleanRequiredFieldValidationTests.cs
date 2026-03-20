using Aetheris.Kernel.Firmament.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentBooleanRequiredFieldValidationTests
{
    [Fact]
    public void Compiler_Accepts_ValidAdd() =>
        AssertValidBooleanOp("""
        firmament:
          version: 1
        
        model:
          name: demo
          units: mm
        
        ops[2]:
          -
            op: box
            id: base
            size[3]:
              1
              2
              3
          -
            op: add
            id: add1
            to: base
            with:
              op: box
              size[3]:
                1
                1
                1
        """);

    [Fact]
    public void Compiler_Accepts_ValidSubtract() =>
        AssertValidBooleanOp("""
        firmament:
          version: 1
        
        model:
          name: demo
          units: mm
        
        ops[3]:
          -
            op: box
            id: base
            size[3]:
              1
              1
              1
          -
            op: add
            id: anchor
            to: base
            with:
              op: box
              size[3]:
                1
                1
                1
            place:
              on: origin
              offset[3]:
                10
                0
                0
          -
            op: subtract
            id: sub1
            from: anchor
            with:
              op: box
              size[3]:
                1
                1
                1
        """);

    [Fact]
    public void Compiler_Accepts_ValidIntersect() =>
        AssertValidBooleanOp("""
        firmament:
          version: 1
        
        model:
          name: demo
          units: mm
        
        ops[2]:
          -
            op: box
            id: base
            size[3]:
              1
              2
              3
          -
            op: intersect
            id: int1
            left: base
            with:
              op: box
              size[3]:
                1
                1
                1
        """);

    [Fact]
    public void Compiler_Rejects_Add_MissingId() =>
        AssertBooleanFailure(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: add
                to: base
                with:
                  op: box
            """,
            FirmamentDiagnosticCodes.BooleanMissingRequiredField,
            "Boolean op 'add' at index 0 is missing required field 'id'.");

    [Fact]
    public void Compiler_Rejects_Add_MissingTo() =>
        AssertBooleanFailure(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: add
                id: a1
                with:
                  op: box
            """,
            FirmamentDiagnosticCodes.BooleanMissingRequiredField,
            "Boolean op 'add' at index 0 is missing required field 'to'.");

    [Fact]
    public void Compiler_Rejects_Add_MissingWith() =>
        AssertBooleanFailure(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: add
                id: a1
                to: base
            """,
            FirmamentDiagnosticCodes.BooleanMissingRequiredField,
            "Boolean op 'add' at index 0 is missing required field 'with'.");

    [Fact]
    public void Compiler_Rejects_Add_InvalidWithShape() =>
        AssertBooleanFailure(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: add
                id: a1
                to: base
                with: sphere
            """,
            FirmamentDiagnosticCodes.BooleanInvalidFieldTypeOrShape,
            "Boolean op 'add' at index 0 has invalid field 'with'; expected an object-like mapping.");

    [Fact]
    public void Compiler_Rejects_Add_MissingWithOp() =>
        AssertBooleanFailure(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: add
                id: a1
                to: base
                with:
                  id: tool1
            """,
            FirmamentDiagnosticCodes.BooleanInvalidFieldTypeOrShape,
            "Boolean op 'add' at index 0 has invalid field 'with.op'; expected a non-empty scalar/string-like value.");

    [Fact]
    public void Compiler_Rejects_Subtract_MissingId() =>
        AssertBooleanFailure(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: subtract
                from: base
                with:
                  op: box
            """,
            FirmamentDiagnosticCodes.BooleanMissingRequiredField,
            "Boolean op 'subtract' at index 0 is missing required field 'id'.");

    [Fact]
    public void Compiler_Rejects_Subtract_MissingFrom() =>
        AssertBooleanFailure(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: subtract
                id: s1
                with:
                  op: box
            """,
            FirmamentDiagnosticCodes.BooleanMissingRequiredField,
            "Boolean op 'subtract' at index 0 is missing required field 'from'.");

    [Fact]
    public void Compiler_Rejects_Subtract_MissingWith() =>
        AssertBooleanFailure(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: subtract
                id: s1
                from: base
            """,
            FirmamentDiagnosticCodes.BooleanMissingRequiredField,
            "Boolean op 'subtract' at index 0 is missing required field 'with'.");

    [Fact]
    public void Compiler_Rejects_Subtract_InvalidWithShape() =>
        AssertBooleanFailure(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: subtract
                id: s1
                from: base
                with[1]:
                  tool
            """,
            FirmamentDiagnosticCodes.BooleanInvalidFieldTypeOrShape,
            "Boolean op 'subtract' at index 0 has invalid field 'with'; expected an object-like mapping.");

    [Fact]
    public void Compiler_Rejects_Subtract_MissingWithOp() =>
        AssertBooleanFailure(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: subtract
                id: s1
                from: base
                with:
                  op: 
            """,
            FirmamentDiagnosticCodes.BooleanInvalidFieldTypeOrShape,
            "Boolean op 'subtract' at index 0 has invalid field 'with.op'; expected a non-empty scalar/string-like value.");

    [Fact]
    public void Compiler_Rejects_Intersect_MissingId() =>
        AssertBooleanFailure(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: intersect
                left: base
                with:
                  op: box
            """,
            FirmamentDiagnosticCodes.BooleanMissingRequiredField,
            "Boolean op 'intersect' at index 0 is missing required field 'id'.");

    [Fact]
    public void Compiler_Rejects_Intersect_MissingLeft() =>
        AssertBooleanFailure(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: intersect
                id: i1
                with:
                  op: box
            """,
            FirmamentDiagnosticCodes.BooleanMissingRequiredField,
            "Boolean op 'intersect' at index 0 is missing required field 'left'.");

    [Fact]
    public void Compiler_Rejects_Intersect_MissingWith() =>
        AssertBooleanFailure(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: intersect
                id: i1
                left: base
            """,
            FirmamentDiagnosticCodes.BooleanMissingRequiredField,
            "Boolean op 'intersect' at index 0 is missing required field 'with'.");

    [Fact]
    public void Compiler_Rejects_Intersect_InvalidWithShape() =>
        AssertBooleanFailure(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: intersect
                id: i1
                left: base
                with: 7
            """,
            FirmamentDiagnosticCodes.BooleanInvalidFieldTypeOrShape,
            "Boolean op 'intersect' at index 0 has invalid field 'with'; expected an object-like mapping.");

    [Fact]
    public void Compiler_Rejects_Intersect_MissingWithOp() =>
        AssertBooleanFailure(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: intersect
                id: i1
                left: base
                with:
                  id: tool
            """,
            FirmamentDiagnosticCodes.BooleanInvalidFieldTypeOrShape,
            "Boolean op 'intersect' at index 0 has invalid field 'with.op'; expected a non-empty scalar/string-like value.");

    [Fact]
    public void Compiler_Rejects_Add_BoxTool_MissingSize() =>
        AssertBooleanFailure(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: add
                id: a1
                to: base
                with:
                  op: box
            """,
            FirmamentDiagnosticCodes.BooleanMissingRequiredField,
            "Boolean op 'add' at index 0 is missing required field 'with.size'.");

    [Fact]
    public void Compiler_Rejects_Subtract_CylinderTool_MissingHeight() =>
        AssertBooleanFailure(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: subtract
                id: s1
                from: base
                with:
                  op: cylinder
                  radius: 1
            """,
            FirmamentDiagnosticCodes.BooleanMissingRequiredField,
            "Boolean op 'subtract' at index 0 is missing required field 'with.height'.");

    [Fact]
    public void Compiler_Rejects_Intersect_SphereTool_InvalidRadiusValue() =>
        AssertBooleanFailure(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: intersect
                id: i1
                left: base
                with:
                  op: sphere
                  radius: 0
            """,
            FirmamentDiagnosticCodes.BooleanInvalidFieldValue,
            "Boolean op 'intersect' at index 0 has invalid field 'with.radius'; expected a numeric value greater than 0.");

    [Fact]
    public void BooleanValidation_Diagnostics_AreDeterministic()
    {
        const string source = """
        firmament:
          version: 1
        
        model:
          name: demo
          units: mm
        
        ops[1]:
          -
            op: add
            id: a1
            to: base
            with:
              id: tool
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

    private static void AssertValidBooleanOp(string source)
    {
        var compiler = new FirmamentCompiler();
        var result = compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));
        Assert.True(result.Compilation.IsSuccess);
    }

    private static void AssertBooleanFailure(string source, FirmamentDiagnosticCode expectedCode, string expectedMessageTail)
    {
        var compiler = new FirmamentCompiler();
        var result = compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));

        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Equal($"[{expectedCode.Value}] {expectedMessageTail}", diagnostic.Message);
    }
}
