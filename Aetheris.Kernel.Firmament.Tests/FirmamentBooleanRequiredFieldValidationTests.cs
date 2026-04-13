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
    public void Compiler_Accepts_ValidBoundedDraftShape() =>
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
              40
              20
              10
          -
            op: draft
            id: drafted
            from: base
            pull: +z
            angle: 2
            faces[2]:
              x_min
              x_max
        """);

    [Fact]
    public void Compiler_Rejects_Draft_UnsupportedPullDirection() =>
        AssertBooleanFailure(
            """
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
                  20
                  20
                  20
              -
                op: draft
                id: drafted
                from: base
                pull: +x
                angle: 2
                faces[1]:
                  x_min
            """,
            FirmamentDiagnosticCodes.BooleanInvalidFieldValue,
            "Boolean op 'draft' at index 1 has invalid field 'pull'; expected '+z' for bounded M4 draft pull direction.");

    [Fact]
    public void Compiler_Accepts_ValidBoundedChamferShape() =>
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
              40
              20
              10
          -
            op: chamfer
            id: edge_break
            from: base
            edges[1]:
              x_max_y_max
            distance: 1.5
        """);

    [Fact]
    public void Compiler_Rejects_Chamfer_UnsupportedEdgeToken() =>
        AssertBooleanFailure(
            """
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
                  20
                  20
                  20
              -
                op: chamfer
                id: edge_break
                from: base
                edges[1]:
                  curved_edge
                distance: 1
            """,
            FirmamentDiagnosticCodes.BooleanInvalidFieldValue,
            "Boolean op 'chamfer' at index 1 has invalid field 'edges'; supported bounded M5a edge tokens are x_min_y_min, x_min_y_max, x_max_y_min, x_max_y_max.");

    [Fact]
    public void Compiler_Rejects_ChamferCornerIncidentEdgeSelector_DuplicateEdgeToken() =>
        AssertBooleanFailure(
            """
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
                  20
                  20
                  20
              -
                op: chamfer
                id: corner_break
                from: base
                corners[1]:
                  x_max_y_max_z_max
                corner_edges[2]:
                  x_neg
                  x_neg
                distance: 1
            """,
            FirmamentDiagnosticCodes.BooleanInvalidFieldValue,
            "Boolean op 'chamfer' at index 1 has invalid field 'corner_edges'; bounded E5b corner-edge selector requires two distinct incident edge tokens.");

    [Fact]
    public void Compiler_Rejects_Chamfer_TwoEdge_NonCornerFirst_Selector_Form() =>
        AssertBooleanFailure(
            """
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
                  20
                  20
                  20
              -
                op: chamfer
                id: edge_break
                from: base
                edges[2]:
                  x_max_y_max
                  x_max_y_min
                distance: 1
            """,
            FirmamentDiagnosticCodes.BooleanInvalidFieldTypeOrShape,
            "Boolean op 'chamfer' at index 1 has invalid field 'edges'; expected a single-item string array with one explicit edge token.");

    [Fact]
    public void Compiler_Accepts_ValidBoundedChamferCornerE2Shape() =>
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
              40
              20
              10
          -
            op: chamfer
            id: corner_break
            from: base
            corners[1]:
              x_max_y_max_z_max
            distance: 1.5
        """);

    [Fact]
    public void Compiler_Rejects_Chamfer_UnsupportedCornerToken() =>
        AssertBooleanFailure(
            """
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
                  20
                  20
                  20
              -
                op: chamfer
                id: corner_break
                from: base
                corners[1]:
                  x_min_y_min_z_min
                distance: 1
            """,
            FirmamentDiagnosticCodes.BooleanInvalidFieldValue,
            "Boolean op 'chamfer' at index 1 has invalid field 'corners'; supported bounded E2 corner tokens are x_max_y_max_z_max.");

    [Fact]
    public void Compiler_Allows_ValidBoundedFilletShape_Past_FieldValidation()
    {
        const string source = """
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
              30
              20
              10
          -
            op: add
            id: rib
            to: base
            with:
              op: box
              size[3]:
                10
                10
                10
          -
            op: fillet
            id: corner_relief
            from: rib
            edges[1]:
              inner_x_max_y_max
            radius: 1.25
        """;

        var compiler = new FirmamentCompiler();
        var result = compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));

        Assert.False(result.Compilation.IsSuccess);
        Assert.DoesNotContain(result.Compilation.Diagnostics, diagnostic => diagnostic.Message.Contains("[FIRM-STRUCT-", StringComparison.Ordinal));
        Assert.Contains(result.Compilation.Diagnostics, diagnostic =>
            diagnostic.Source == "firmament.fillet-bounded");
    }

    [Fact]
    public void Compiler_Rejects_Fillet_UnsupportedEdgeToken() =>
        AssertBooleanFailure(
            """
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
                  20
                  20
                  20
              -
                op: fillet
                id: edge_break
                from: base
                edges[1]:
                  x_max_y_max
                radius: 1
            """,
            FirmamentDiagnosticCodes.BooleanInvalidFieldValue,
            "Boolean op 'fillet' at index 1 has invalid field 'edges'; supported bounded M5b edge tokens are inner_x_min_y_min, inner_x_min_y_max, inner_x_max_y_min, inner_x_max_y_max.");

    [Fact]
    public void Compiler_Allows_ValidTorusBooleanToolShape_Past_FieldValidation()
    {
        const string source = """
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
              40
              30
              12
          -
            op: subtract
            id: groove
            from: base
            with:
              op: torus
              major_radius: 10
              minor_radius: 3
        """;

        var compiler = new FirmamentCompiler();
        var result = compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));

        Assert.False(result.Compilation.IsSuccess);
        Assert.DoesNotContain(result.Compilation.Diagnostics, diagnostic => diagnostic.Message.Contains("[FIRM-STRUCT-", StringComparison.Ordinal));
        Assert.Contains(result.Compilation.Diagnostics, diagnostic =>
            diagnostic.Code == Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticCode.NotImplemented
            && (diagnostic.Message.Contains("bounded boolean family only supports recognized axis-aligned boxes from BrepPrimitives.CreateBox(...).", StringComparison.Ordinal)
                || diagnostic.Message.Contains("analytic tool surface kind", StringComparison.Ordinal)));
    }

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
    public void Compiler_Rejects_TorusTool_MajorRadius_NotGreaterThan_MinorRadius() =>
        AssertBooleanFailure(
            """
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
                  40
                  30
                  12
              -
                op: add
                id: joined
                to: base
                with:
                  op: torus
                  major_radius: 3
                  minor_radius: 3
            """,
            FirmamentDiagnosticCodes.BooleanInvalidFieldValue,
            "Boolean op 'add' at index 1 has invalid field 'with.major_radius'; expected a numeric value greater than 'with.minor_radius'.");

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
