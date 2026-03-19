using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Firmament.ParsedModel;

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
        firmament:
          version: 1
        
        model:
          name: demo
          units: mm
        
        ops[0]:
        """;

        var result = compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));

        Assert.True(result.Compilation.IsSuccess);
        var artifact = result.Compilation.Value;
        Assert.Equal("firmament-placement-executed", artifact.ArtifactKind);
        Assert.NotNull(artifact.ParsedDocument);
        Assert.Equal("1", artifact.ParsedDocument!.Firmament.Version);
        Assert.Equal("demo", artifact.ParsedDocument.Model.Name);
        Assert.Equal("mm", artifact.ParsedDocument.Model.Units);
        Assert.Empty(artifact.ParsedDocument.Ops.Entries);
    }

    [Fact]
    public void Compiler_Accepts_KnownPrimitiveOp() =>
        AssertValidOpsCount(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: box
                id: b1
                size[3]:
                  1
                  2
                  3
            """,
            1);

    [Fact]
    public void Compiler_Accepts_MultipleKnownOps() =>
        AssertValidOpsCount(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[2]:
              -
                op: box
                id: b1
                size[3]:
                  2
                  3
                  4
              -
                op: subtract
                id: cut1
                from: b1
                with:
                  op: box
                  size[3]:
                    1
                    1
                    1
                  size[3]:
                    1
                    1
                    1
            """,
            2);


    [Fact]
    public void Compiler_Classifies_KnownOpKind_OnParsedEntry()
    {
        var compiler = new FirmamentCompiler();
        var source = """
        firmament:
          version: 1
        
        model:
          name: demo
          units: mm
        
        ops[1]:
          -
            op: sphere
            id: s1
            radius: 2.5
            note: x
        """;

        var result = compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));

        Assert.True(result.Compilation.IsSuccess);
        var op = Assert.Single(result.Compilation.Value.ParsedDocument!.Ops.Entries);
        Assert.Equal("sphere", op.OpName);
        Assert.Equal(FirmamentKnownOpKind.Sphere, op.KnownKind);
        Assert.Equal(FirmamentOpFamily.Primitive, op.Family);
        Assert.Equal("x", op.RawFields["note"]);
    }


    [Fact]
    public void Compiler_Classifies_KnownPrimitiveOpFamily() =>
        AssertClassifiedFamily("box", FirmamentOpFamily.Primitive);

    [Fact]
    public void Compiler_Classifies_Cone_As_KnownPrimitiveOpFamily() =>
        AssertClassifiedFamily("cone", FirmamentOpFamily.Primitive);

    [Fact]
    public void Compiler_Classifies_KnownBooleanOpFamily() =>
        AssertClassifiedFamily("intersect", FirmamentOpFamily.Boolean);

    [Fact]
    public void Compiler_Classifies_KnownValidationOpFamily() =>
        AssertClassifiedFamily("expect_selectable", FirmamentOpFamily.Validation);

    [Fact]
    public void Compiler_Preserves_FamilyClassification_ForMultipleOps()
    {
        var compiler = new FirmamentCompiler();
        var source = """
        firmament:
          version: 1
        
        model:
          name: demo
          units: mm
        
        ops[3]:
          -
            op: box
            id: b1
            size[3]:
              1
              2
              3
          -
            op: subtract
            id: s1
            from: b1
            with:
              op: sphere
              radius: 1
              radius: 1
          -
            op: expect_manifold
            target: s1
        """;

        var result = compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));

        Assert.True(result.Compilation.IsSuccess);
        var ops = result.Compilation.Value.ParsedDocument!.Ops.Entries;
        Assert.Collection(
            ops,
            op => Assert.Equal(FirmamentOpFamily.Primitive, op.Family),
            op => Assert.Equal(FirmamentOpFamily.Boolean, op.Family),
            op => Assert.Equal(FirmamentOpFamily.Validation, op.Family));
    }

    [Fact]
    public void Compiler_Accepts_KnownBooleanOp() =>
        AssertValidOpsCount(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[2]:
              -
                op: box
                id: b1
                size[3]:
                  1
                  2
                  3
              -
                op: add
                id: add1
                to: b1
                with:
                  op: sphere
                  radius: 1
            """,
            2);

    [Fact]
    public void Compiler_Accepts_KnownValidationOp() =>
        AssertValidOpsCount(
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
                  1
                  1
                  1
              -
                op: expect_exists
                target: base.top_face
            """,
            2);

    [Fact]
    public void Compiler_Rejects_OpEntry_MissingOp() =>
        AssertSingleValidationError(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                target: a
            """,
            FirmamentDiagnosticCodes.StructureMissingRequiredOpField,
            "Operation entry at index 0 is missing required field 'op'.");

    [Fact]
    public void Compiler_Rejects_OpEntry_WrongShape() =>
        AssertSingleValidationError(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              - not-an-object
            """,
            FirmamentDiagnosticCodes.StructureInvalidOpsEntryShape,
            "Operation entry at index 0 must be an object with fields.");

    [Fact]
    public void Compiler_Rejects_OpValue_InvalidShape() =>
        AssertSingleValidationError(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: 
            """,
            FirmamentDiagnosticCodes.StructureInvalidOpFieldValue,
            "Operation entry at index 0 has invalid 'op' value; expected a non-empty scalar.");


    [Fact]
    public void Compiler_Rejects_UnknownOpKind() =>
        AssertSingleValidationError(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: torus
            """,
            FirmamentDiagnosticCodes.StructureUnknownOpKind,
            "Operation entry at index 0 has unknown op kind 'torus'.");

    [Fact]
    public void Compiler_Rejects_UnknownOpKind_Deterministically()
    {
        var compiler = new FirmamentCompiler();
        var source = """
        firmament:
          version: 1
        
        model:
          name: demo
          units: mm
        
        ops[1]:
          -
            op: torus
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
    public void Compiler_Rejects_OpEntry_MissingOp_Deterministically()
    {
        var compiler = new FirmamentCompiler();
        var source = """
        firmament:
          version: 1
        
        model:
          name: demo
          units: mm
        
        ops[1]:
          -
            target: a
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
            firmament:
              version: 1
            
            ops[0]:
            """,
            FirmamentDiagnosticCodes.StructureMissingRequiredSection,
            "Missing required top-level section 'model'.");

    [Fact]
    public void Compiler_Rejects_MissingOps() =>
        AssertSingleValidationError(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            """,
            FirmamentDiagnosticCodes.StructureMissingRequiredSection,
            "Missing required top-level section 'ops'.");

    [Fact]
    public void Compiler_Rejects_MissingVersion() =>
        AssertSingleValidationError(
            """
            firmament:
            
            model:
              name: demo
              units: mm
            
            ops[0]:
            """,
            FirmamentDiagnosticCodes.StructureMissingRequiredField,
            "Missing required field 'version' in section 'firmament'.");

    [Fact]
    public void Compiler_Rejects_MissingName() =>
        AssertSingleValidationError(
            """
            firmament:
              version: 1
            
            model:
              units: mm
            
            ops[0]:
            """,
            FirmamentDiagnosticCodes.StructureMissingRequiredField,
            "Missing required field 'name' in section 'model'.");

    [Fact]
    public void Compiler_Rejects_MissingUnits() =>
        AssertSingleValidationError(
            """
            firmament:
              version: 1
            
            model:
              name: demo
            
            ops[0]:
            """,
            FirmamentDiagnosticCodes.StructureMissingRequiredField,
            "Missing required field 'units' in section 'model'.");

    [Fact]
    public void Compiler_Rejects_UnknownTopLevelSection() =>
        AssertSingleValidationError(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[0]:
            
            other:
            """,
            FirmamentDiagnosticCodes.StructureUnknownTopLevelSection,
            "Unknown top-level section 'other'.");

    [Fact]
    public void Compiler_Accepts_ValidCylinderPrimitiveOp() =>
        AssertValidOpsCount(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: cylinder
                id: c1
                radius: 1.5
                height: 10
            """,
            1);

    [Fact]
    public void Compiler_Rejects_Box_MissingId() =>
        AssertSingleValidationError(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: box
                size[3]:
                  1
                  2
                  3
            """,
            FirmamentDiagnosticCodes.PrimitiveMissingRequiredField,
            "Primitive op 'box' at index 0 is missing required field 'id'.");

    [Fact]
    public void Compiler_Rejects_Box_MissingSize() =>
        AssertSingleValidationError(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: box
                id: b1
            """,
            FirmamentDiagnosticCodes.PrimitiveMissingRequiredField,
            "Primitive op 'box' at index 0 is missing required field 'size'.");

    [Fact]
    public void Compiler_Rejects_Box_InvalidSizeShape() =>
        AssertSingleValidationError(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: box
                id: b1
                size[2]:
                  1
                  2
            """,
            FirmamentDiagnosticCodes.PrimitiveInvalidFieldTypeOrShape,
            "Primitive op 'box' at index 0 has invalid field 'size'; expected exactly 3 numeric components.");

    [Fact]
    public void Compiler_Rejects_Box_NonPositiveSizeComponent() =>
        AssertSingleValidationError(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: box
                id: b1
                size[3]:
                  1
                  0
                  3
            """,
            FirmamentDiagnosticCodes.PrimitiveInvalidFieldValue,
            "Primitive op 'box' at index 0 has invalid field 'size' value; all components must be greater than 0.");

    [Fact]
    public void Compiler_Rejects_Cylinder_MissingRadius() =>
        AssertSingleValidationError(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: cylinder
                id: c1
                height: 5
            """,
            FirmamentDiagnosticCodes.PrimitiveMissingRequiredField,
            "Primitive op 'cylinder' at index 0 is missing required field 'radius'.");

    [Fact]
    public void Compiler_Rejects_Cylinder_MissingHeight() =>
        AssertSingleValidationError(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: cylinder
                id: c1
                radius: 2
            """,
            FirmamentDiagnosticCodes.PrimitiveMissingRequiredField,
            "Primitive op 'cylinder' at index 0 is missing required field 'height'.");

    [Fact]
    public void Compiler_Rejects_Cylinder_NonPositiveRadiusOrHeight() =>
        AssertSingleValidationError(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: cylinder
                id: c1
                radius: -1
                height: 5
            """,
            FirmamentDiagnosticCodes.PrimitiveInvalidFieldValue,
            "Primitive op 'cylinder' at index 0 has invalid field 'radius' value; expected a numeric value greater than 0.");

    [Fact]
    public void Compiler_Rejects_Cone_NonPositiveBottomRadius() =>
        AssertSingleValidationError(
            FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/fixtures/m10e-invalid-cone-bottom-radius-non-positive.firmament"),
            FirmamentDiagnosticCodes.PrimitiveInvalidFieldValue,
            "Primitive op 'cone' at index 0 has invalid field 'bottom_radius' value; expected a numeric value greater than 0.");

    [Fact]
    public void Compiler_Rejects_Cone_NonPositiveTopRadius() =>
        AssertSingleValidationError(
            FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/fixtures/m10e-invalid-cone-top-radius-non-positive.firmament"),
            FirmamentDiagnosticCodes.PrimitiveInvalidFieldValue,
            "Primitive op 'cone' at index 0 has invalid field 'top_radius' value; expected a numeric value greater than 0.");

    [Fact]
    public void Compiler_Rejects_Cone_NonPositiveHeight() =>
        AssertSingleValidationError(
            FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/fixtures/m10e-invalid-cone-height-non-positive.firmament"),
            FirmamentDiagnosticCodes.PrimitiveInvalidFieldValue,
            "Primitive op 'cone' at index 0 has invalid field 'height' value; expected a numeric value greater than 0.");

    [Fact]
    public void Compiler_Rejects_Cone_EqualRadii() =>
        AssertSingleValidationError(
            FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/fixtures/m10e-invalid-cone-equal-radii.firmament"),
            FirmamentDiagnosticCodes.PrimitiveInvalidFieldValue,
            "Primitive op 'cone' at index 0 has invalid field 'top_radius' value; expected a numeric value different from 'bottom_radius'.");

    [Fact]
    public void Compiler_Rejects_Sphere_MissingRadius() =>
        AssertSingleValidationError(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: sphere
                id: s1
            """,
            FirmamentDiagnosticCodes.PrimitiveMissingRequiredField,
            "Primitive op 'sphere' at index 0 is missing required field 'radius'.");

    [Fact]
    public void Compiler_Rejects_Sphere_NonPositiveRadius() =>
        AssertSingleValidationError(
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: sphere
                id: s1
                radius: 0
            """,
            FirmamentDiagnosticCodes.PrimitiveInvalidFieldValue,
            "Primitive op 'sphere' at index 0 has invalid field 'radius' value; expected a numeric value greater than 0.");

    [Fact]
    public void PrimitiveValidation_Diagnostics_AreDeterministic()
    {
        var compiler = new FirmamentCompiler();
        var source = """
        firmament:
          version: 1
        
        model:
          name: demo
          units: mm
        
        ops[1]:
          -
            op: box
            id: b1
            size[2]:
              1
              2
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
        Assert.StartsWith("FIRM-STRUCT", FirmamentDiagnosticCodes.StructureUnknownOpKind.Value);
        Assert.StartsWith("FIRM-STRUCT", FirmamentDiagnosticCodes.PrimitiveMissingRequiredField.Value);
        Assert.StartsWith("FIRM-STRUCT", FirmamentDiagnosticCodes.PrimitiveInvalidFieldTypeOrShape.Value);
        Assert.StartsWith("FIRM-STRUCT", FirmamentDiagnosticCodes.PrimitiveInvalidFieldValue.Value);
        Assert.StartsWith("FIRM-STRUCT", FirmamentDiagnosticCodes.BooleanMissingRequiredField.Value);
        Assert.StartsWith("FIRM-STRUCT", FirmamentDiagnosticCodes.BooleanInvalidFieldTypeOrShape.Value);
        Assert.StartsWith("FIRM-REF", FirmamentDiagnosticCodes.ReferenceDuplicateFeatureId.Value);
        Assert.StartsWith("FIRM-REF", FirmamentDiagnosticCodes.ReferenceUnknownFeatureId.Value);
        Assert.StartsWith("FIRM-SEL", FirmamentDiagnosticCodes.SelectorPlaceholder.Value);
        Assert.StartsWith("FIRM-SCHEMA", FirmamentDiagnosticCodes.SchemaUnknownProcess.Value);
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

    private static void AssertClassifiedFamily(string opName, FirmamentOpFamily expectedFamily)
    {
        var compiler = new FirmamentCompiler();
        var payload = opName switch
        {
            "box" => """
  -
    op: box
    id: b1
    size[3]:
      1
      1
      1
""",
            "cylinder" => """
  -
    op: cylinder
    id: c1
    radius: 1
    height: 2
""",
            "cone" => """
  -
    op: cone
    id: frustum1
    bottom_radius: 3
    top_radius: 1
    height: 5
""",
            "sphere" => """
  -
    op: sphere
    id: s1
    radius: 1
""",
            "add" => """
  -
    op: box
    id: b1
    size[3]:
      1
      1
      1
  -
    op: add
    id: a1
    to: b1
    with:
      op: sphere
      radius: 1
      radius: 1
""",
            "subtract" => """
  -
    op: box
    id: b1
    size[3]:
      1
      1
      1
  -
    op: subtract
    id: s1
    from: b1
    with:
      op: sphere
      radius: 1
      radius: 1
""",
            "intersect" => """
  -
    op: box
    id: b1
    size[3]:
      1
      1
      1
  -
    op: intersect
    id: i1
    left: b1
    with:
      op: sphere
      radius: 1
      radius: 1
""",
            "expect_exists" => """
  -
    op: box
    id: b1
    size[3]:
      1
      1
      1
  -
    op: expect_exists
    target: b1.top_face
""",
            "expect_selectable" => """
  -
    op: box
    id: b1
    size[3]:
      1
      1
      1
  -
    op: expect_selectable
    target: b1.top_face
    count: 1
""",
            "expect_manifold" => """
  -
    op: box
    id: b1
    size[3]:
      1
      1
      1
  -
    op: expect_manifold
    target: b1
""",
            _ => $$"""
  -
    op: {{opName}}
"""
        };

        var source = $$"""
        firmament:
          version: 1
        
        model:
          name: demo
          units: mm
        
        ops[{{((opName is "add" or "subtract" or "intersect" or "expect_exists" or "expect_selectable" or "expect_manifold") ? 2 : 1)}}]:
        {{payload}}
        """;

        var result = compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));

        Assert.True(result.Compilation.IsSuccess);
        var ops = result.Compilation.Value.ParsedDocument!.Ops.Entries;
        var op = ops[^1];
        Assert.Equal(expectedFamily, op.Family);
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
