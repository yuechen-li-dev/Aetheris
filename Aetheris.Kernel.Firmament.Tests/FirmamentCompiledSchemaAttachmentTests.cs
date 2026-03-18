using Aetheris.Kernel.Firmament.CompiledModel;
using System.Linq;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentCompiledSchemaAttachmentTests
{
    [Fact]
    public void Compile_WithoutSchema_Preserves_ExplicitSchemaAbsence_InArtifact()
    {
        var result = Compile(BasicOpsSource);

        Assert.True(result.Compilation.IsSuccess);
        Assert.Null(result.Compilation.Value.CompiledSchema);
    }

    [Fact]
    public void Compile_WithValidCncSchema_Attaches_CompiledSchemaArtifact()
    {
        var result = Compile($"""
{Header}

schema:
  process: cnc
  minimum_tool_radius: 1.5

{OpsSingleBox}
""");

        Assert.True(result.Compilation.IsSuccess);
        var compiledSchema = Assert.IsType<FirmamentCompiledSchema>(result.Compilation.Value.CompiledSchema);
        Assert.Equal(FirmamentCompiledSchemaProcess.Cnc, compiledSchema.Process);

        var payload = Assert.IsType<FirmamentCompiledCncSchema>(compiledSchema.Payload);
        Assert.Equal(1.5d, payload.MinimumToolRadius);
    }

    [Fact]
    public void Compile_WithValidInjectionMoldedSchema_Attaches_CompiledSchemaArtifact()
    {
        var result = Compile($"""
{Header}

schema:
  process: injection_molded
  parting_plane: xy
  gate_location:
    x: 1
    y: 2
    z: 3
  draft_angle: 2

{OpsSingleBox}
""");

        Assert.True(result.Compilation.IsSuccess);
        var compiledSchema = Assert.IsType<FirmamentCompiledSchema>(result.Compilation.Value.CompiledSchema);
        Assert.Equal(FirmamentCompiledSchemaProcess.InjectionMolded, compiledSchema.Process);

        var payload = Assert.IsType<FirmamentCompiledInjectionMoldedSchema>(compiledSchema.Payload);
        Assert.Equal("xy", payload.PartingPlane);
        Assert.Equal(new FirmamentCompiledSchemaGateLocation(1d, 2d, 3d), payload.GateLocation);
        Assert.Equal(2d, payload.DraftAngle);
    }

    [Fact]
    public void Compile_WithValidAdditiveSchema_Attaches_CompiledSchemaArtifact()
    {
        var result = Compile($"""
{Header}

schema:
  process: additive
  printer_resolution: 0.1

{OpsSingleBox}
""");

        Assert.True(result.Compilation.IsSuccess);
        var compiledSchema = Assert.IsType<FirmamentCompiledSchema>(result.Compilation.Value.CompiledSchema);
        Assert.Equal(FirmamentCompiledSchemaProcess.Additive, compiledSchema.Process);

        var payload = Assert.IsType<FirmamentCompiledAdditiveSchema>(compiledSchema.Payload);
        Assert.Equal(0.1d, payload.PrinterResolution);
    }

    [Fact]
    public void Compile_WithSchema_DoesNotChange_Lowering_Execution_Placement_Validation_Or_Selectors()
    {
        const string baseline = """
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
      4
      2
      1
  -
    op: cylinder
    id: pin
    radius: 0.5
    height: 2
    place:
      on: base.top_face
      offset[3]:
        0
        0
        0
  -
    op: expect_exists
    target: base.top_face
""";

        const string withSchema = """
firmament:
  version: 1

model:
  name: demo
  units: mm

schema:
  process: cnc
  minimum_tool_radius: 1

ops[3]:
  -
    op: box
    id: base
    size[3]:
      4
      2
      1
  -
    op: cylinder
    id: pin
    radius: 0.5
    height: 2
    place:
      on: base.top_face
      offset[3]:
        0
        0
        0
  -
    op: expect_exists
    target: base.top_face
""";

        var first = Compile(baseline);
        var second = Compile(withSchema);

        Assert.True(first.Compilation.IsSuccess);
        Assert.True(second.Compilation.IsSuccess);

        Assert.Equivalent(first.Compilation.Value.PrimitiveLoweringPlan, second.Compilation.Value.PrimitiveLoweringPlan);

        var firstExecutionMetadata = first.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives
            .Select(p => (p.OpIndex, p.FeatureId, Kind: p.Kind.ToString(), BodyCount: p.Body.Topology.Bodies.Count(), FaceCount: p.Body.Topology.Faces.Count()))
            .Concat(first.Compilation.Value.PrimitiveExecutionResult.ExecutedBooleans
                .Select(b => (b.OpIndex, b.FeatureId, Kind: b.Kind.ToString(), BodyCount: b.Body.Topology.Bodies.Count(), FaceCount: b.Body.Topology.Faces.Count())))
            .OrderBy(entry => entry.OpIndex)
            .ToArray();
        var secondExecutionMetadata = second.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives
            .Select(p => (p.OpIndex, p.FeatureId, Kind: p.Kind.ToString(), BodyCount: p.Body.Topology.Bodies.Count(), FaceCount: p.Body.Topology.Faces.Count()))
            .Concat(second.Compilation.Value.PrimitiveExecutionResult.ExecutedBooleans
                .Select(b => (b.OpIndex, b.FeatureId, Kind: b.Kind.ToString(), BodyCount: b.Body.Topology.Bodies.Count(), FaceCount: b.Body.Topology.Faces.Count())))
            .OrderBy(entry => entry.OpIndex)
            .ToArray();

        Assert.Equal(firstExecutionMetadata, secondExecutionMetadata);
        Assert.Equal(first.Compilation.Value.ValidationExecutionResult!.Validations, second.Compilation.Value.ValidationExecutionResult!.Validations);
    }

    private static FirmamentCompileResult Compile(string source)
    {
        var compiler = new FirmamentCompiler();
        return compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));
    }

    private const string Header = """
firmament:
  version: 1

model:
  name: demo
  units: mm
""";

    private const string OpsSingleBox = """
ops[1]:
  -
    op: box
    id: base
    size[3]:
      1
      2
      3
""";

    private const string BasicOpsSource = Header + "\n" + OpsSingleBox;
}
