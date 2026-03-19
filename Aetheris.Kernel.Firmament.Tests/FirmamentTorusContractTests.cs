using System.Linq;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Firmament.Lowering;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentTorusContractTests
{
    [Fact]
    public void Compile_Attaches_Torus_Selector_Contract_Metadata_Truthfully()
    {
        const string source = """
firmament:
  version: 1

model:
  name: torus_selector_contract
  units: mm

ops[4]:
  -
    op: torus
    id: donut1
    major_radius: 10
    minor_radius: 3
  -
    op: expect_exists
    target: donut1.surface
  -
    op: expect_exists
    target: donut1.edges
  -
    op: expect_exists
    target: donut1.vertices
""";

        var result = FirmamentCorpusHarness.Compile(source);

        Assert.True(result.Compilation.IsSuccess);
        var parsedOps = result.Compilation.Value.ParsedDocument!.Ops.Entries;
        Assert.NotNull(parsedOps[1].ClassifiedFields);
        Assert.NotNull(parsedOps[2].ClassifiedFields);
        Assert.NotNull(parsedOps[3].ClassifiedFields);
        Assert.Equal("Face", parsedOps[1].ClassifiedFields!["selectorResultKind"]);
        Assert.Equal("One", parsedOps[1].ClassifiedFields!["selectorCardinality"]);
        Assert.Equal("EdgeSet", parsedOps[2].ClassifiedFields!["selectorResultKind"]);
        Assert.Equal("Many", parsedOps[2].ClassifiedFields!["selectorCardinality"]);
        Assert.Equal("VertexSet", parsedOps[3].ClassifiedFields!["selectorResultKind"]);
        Assert.Equal("Many", parsedOps[3].ClassifiedFields!["selectorCardinality"]);
    }

    [Fact]
    public void Compile_Executes_Torus_Selector_Contract_With_Truthful_Runtime_Topology()
    {
        const string source = """
firmament:
  version: 1

model:
  name: torus_selector_truth
  units: mm

ops[4]:
  -
    op: torus
    id: donut1
    major_radius: 10
    minor_radius: 3
  -
    op: expect_selectable
    target: donut1.surface
    count: 1
  -
    op: expect_selectable
    target: donut1.edges
    count: 2
  -
    op: expect_selectable
    target: donut1.vertices
    count: 1
""";

        var result = FirmamentCorpusHarness.Compile(source);

        Assert.True(result.Compilation.IsSuccess);
        var executed = Assert.Single(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives);
        Assert.Equal(FirmamentLoweredPrimitiveKind.Torus, executed.Kind);
        Assert.Single(executed.Body.Topology.Faces);
        Assert.Equal(2, executed.Body.Topology.Edges.Count());
        Assert.Single(executed.Body.Topology.Vertices);
        var face = Assert.Single(executed.Body.Topology.Faces);
        Assert.True(executed.Body.TryGetFaceSurfaceGeometry(face.Id, out var surface));
        Assert.Equal(SurfaceGeometryKind.Torus, surface!.Kind);

        Assert.Equal(3, result.Compilation.Value.ValidationExecutionResult!.Validations.Count);
        Assert.All(result.Compilation.Value.ValidationExecutionResult.Validations, validation =>
        {
            Assert.True(validation.IsExecuted);
            Assert.True(validation.IsSuccess);
            Assert.Null(validation.Reason);
        });
    }

    [Fact]
    public void Compile_Rejects_False_Torus_Selector_Families_Not_In_Truthful_Contract()
    {
        const string source = """
firmament:
  version: 1

model:
  name: torus_false_selector_family
  units: mm

ops[2]:
  -
    op: torus
    id: donut1
    major_radius: 10
    minor_radius: 3
  -
    op: expect_exists
    target: donut1.outer_surface
""";

        var result = FirmamentCorpusHarness.Compile(source);

        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Equal(
            "[FIRM-REF-0006] Validation op 'expect_exists' at index 1 has selector port 'outer_surface' not allowed for feature kind 'torus' on feature id 'donut1' via field 'target'.",
            diagnostic.Message);
    }
}
