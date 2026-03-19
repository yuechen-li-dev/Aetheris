using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Lowering;
using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentValidationExecutionTests
{
    [Fact]
    public void Compile_Executes_ExpectExists_For_BareFeatureId_Target()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m2c-valid-expect-exists-bare-target-earlier.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var validation = Assert.Single(result.Compilation.Value.ValidationExecutionResult!.Validations);
        Assert.Equal(1, validation.OpIndex);
        Assert.Equal(FirmamentKnownOpKind.ExpectExists, validation.Kind);
        Assert.Equal("base", validation.Target);
        Assert.True(validation.IsExecuted);
        Assert.True(validation.IsSuccess);
        Assert.Null(validation.Reason);
    }

    [Fact]
    public void Compile_Executes_ExpectExists_For_TopologyBacked_SelectorTargets()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m6b-valid-selector-exists.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var validations = result.Compilation.Value.ValidationExecutionResult!.Validations;
        Assert.Equal(4, validations.Count);
        Assert.All(validations, validation =>
        {
            Assert.True(validation.IsExecuted);
            Assert.True(validation.IsSuccess);
            Assert.Null(validation.Reason);
        });
    }

    [Fact]
    public void Compile_Rejects_Sphere_Edge_Selector_After_Contract_Is_Reduced_To_Truthful_Surface_Only()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m6b-invalid-selector-empty.firmament");

        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Equal(
            "[FIRM-REF-0006] Validation op 'expect_exists' at index 1 has selector port 'edges' not allowed for feature kind 'sphere' on feature id 'sphere' via field 'target'.",
            diagnostic.Message);
    }

    [Fact]
    public void Compile_Executes_ExpectSelectable_With_TopologyBacked_Counts_As_Successes()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m6c-valid-selectable-count.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var validations = result.Compilation.Value.ValidationExecutionResult!.Validations;
        Assert.Equal(4, validations.Count);
        Assert.All(validations, validation =>
        {
            Assert.True(validation.IsExecuted);
            Assert.True(validation.IsSuccess);
            Assert.Null(validation.Reason);
        });
    }

    [Fact]
    public void Compile_Executes_Cylinder_Selector_Contract_With_Truthful_Runtime_Counts()
    {
        const string source = """
firmament:
  version: 1

model:
  name: cylinder_selector_truth
  units: mm

ops[7]:
  -
    op: cylinder
    id: cyl
    radius: 2
    height: 5
  -
    op: expect_selectable
    target: cyl.top_face
    count: 1
  -
    op: expect_selectable
    target: cyl.bottom_face
    count: 1
  -
    op: expect_selectable
    target: cyl.side_face
    count: 1
  -
    op: expect_selectable
    target: cyl.circular_edges
    count: 2
  -
    op: expect_selectable
    target: cyl.edges
    count: 3
  -
    op: expect_selectable
    target: cyl.vertices
    count: 4
""";

        var result = Compile(source);

        Assert.True(result.Compilation.IsSuccess);
        Assert.Equal(6, result.Compilation.Value.ValidationExecutionResult!.Validations.Count);
        Assert.All(result.Compilation.Value.ValidationExecutionResult.Validations, validation =>
        {
            Assert.True(validation.IsExecuted);
            Assert.True(validation.IsSuccess);
            Assert.Null(validation.Reason);
        });
    }


    [Fact]
    public void Compile_Executes_Sphere_Selector_Contract_With_Truthful_Runtime_Counts()
    {
        const string source = """
firmament:
  version: 1

model:
  name: sphere_selector_truth
  units: mm

ops[2]:
  -
    op: sphere
    id: ball
    radius: 4
  -
    op: expect_selectable
    target: ball.surface
    count: 1
""";

        var result = Compile(source);

        Assert.True(result.Compilation.IsSuccess);
        var validation = Assert.Single(result.Compilation.Value.ValidationExecutionResult!.Validations);
        Assert.True(validation.IsExecuted);
        Assert.True(validation.IsSuccess);
        Assert.Null(validation.Reason);

        var body = Assert.Single(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives).Body;
        Assert.Single(body.Topology.Faces);
        Assert.Empty(body.Topology.Edges);
        Assert.Empty(body.Topology.Vertices);
    }

    [Fact]
    public void Compile_Executes_ExpectSelectable_With_Count_Mismatch_As_Deterministic_Failure()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m6c-invalid-selectable-count-mismatch.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var validations = result.Compilation.Value.ValidationExecutionResult!.Validations;
        Assert.Equal(2, validations.Count);

        Assert.Collection(
            validations,
            first =>
            {
                Assert.True(first.IsExecuted);
                Assert.False(first.IsSuccess);
                Assert.Equal("Selector 'base.side_faces' resolved to 6 elements but 3 were expected.", first.Reason);
            },
            second =>
            {
                Assert.True(second.IsExecuted);
                Assert.False(second.IsSuccess);
                Assert.Equal("Selector 'sphere.surface' resolved to 1 elements but 2 were expected.", second.Reason);
            });

    }

    [Fact]
    public void Compile_Records_Unsupported_For_ExpectSelectable_On_BareFeatureId_Target()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m6a-unsupported-expect-selectable-bare-target.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var validation = Assert.Single(result.Compilation.Value.ValidationExecutionResult!.Validations);
        Assert.False(validation.IsExecuted);
        Assert.False(validation.IsSuccess);
        Assert.Contains("does not support bare feature-id", validation.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_Executes_ExpectManifold_For_Valid_Solids()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m6d-valid-manifold.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var validations = result.Compilation.Value.ValidationExecutionResult!.Validations;
        Assert.Equal(4, validations.Count);
        Assert.All(validations, validation =>
        {
            Assert.Equal(FirmamentKnownOpKind.ExpectManifold, validation.Kind);
            Assert.True(validation.IsExecuted);
            Assert.True(validation.IsSuccess);
            Assert.Null(validation.Reason);
        });
    }

    [Fact]
    public void Compile_Rejects_SelectorShaped_Targets_For_ExpectManifold_Deterministically()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m6d-invalid-nonmanifold.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var validation = Assert.Single(result.Compilation.Value.ValidationExecutionResult!.Validations);
        Assert.Equal(FirmamentKnownOpKind.ExpectManifold, validation.Kind);
        Assert.False(validation.IsExecuted);
        Assert.False(validation.IsSuccess);
        Assert.Equal("expect_manifold does not support selector-shaped targets at M6d.", validation.Reason);
    }

    [Fact]
    public void ValidationExecutor_Reports_NonManifold_Bodies_Deterministically()
    {
        var parsedDocument = new FirmamentParsedDocument(
            new FirmamentParsedHeader("1"),
            new FirmamentParsedModelHeader("demo", "mm"),
            new FirmamentParsedOpsSection(
            [
                new FirmamentParsedOpEntry(
                    OpName: "expect_manifold",
                    KnownKind: FirmamentKnownOpKind.ExpectManifold,
                    Family: FirmamentOpFamily.Validation,
                    RawFields: new Dictionary<string, string>(StringComparer.Ordinal) { ["target"] = "base" },
                    ClassifiedFields: new Dictionary<string, string>(StringComparer.Ordinal) { ["targetShape"] = "FeatureId" })
            ]),
            Schema: null,
            HasPmi: false);

        var executionResult = new FirmamentPrimitiveExecutionResult(
            ExecutedPrimitives:
            [
                new FirmamentExecutedPrimitive(0, "base", FirmamentLoweredPrimitiveKind.Box, BuildNonManifoldBody())
            ],
            ExecutedBooleans: []);

        var validationExecution = FirmamentValidationExecutor.Execute(parsedDocument, executionResult);

        Assert.True(validationExecution.IsSuccess);
        var validation = Assert.Single(validationExecution.Value.Validations);
        Assert.True(validation.IsExecuted);
        Assert.False(validation.IsSuccess);
        Assert.Equal("Feature 'base' produced non-manifold geometry.", validation.Reason);

        var diagnostic = Assert.Single(validationExecution.Diagnostics);
        Assert.Contains("[FIRM-REF-0009]", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("Feature 'base' produced non-manifold geometry.", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_Preserves_ValidationExecution_Source_Order_And_OpIndices()
    {
        const string source = """
firmament:
  version: 1

model:
  name: demo
  units: mm

ops[4]:
  -
    op: box
    id: base
    size[3]:
      1
      2
      3
  -
    op: expect_exists
    target: base
  -
    op: expect_selectable
    target: base.top_face
    count: 1
  -
    op: expect_manifold
    target: base
""";

        var result = Compile(source);

        Assert.True(result.Compilation.IsSuccess);
        var validations = result.Compilation.Value.ValidationExecutionResult!.Validations;
        Assert.Collection(
            validations,
            first =>
            {
                Assert.Equal(1, first.OpIndex);
                Assert.Equal(FirmamentKnownOpKind.ExpectExists, first.Kind);
            },
            second =>
            {
                Assert.Equal(2, second.OpIndex);
                Assert.Equal(FirmamentKnownOpKind.ExpectSelectable, second.Kind);
            },
            third =>
            {
                Assert.Equal(3, third.OpIndex);
                Assert.Equal(FirmamentKnownOpKind.ExpectManifold, third.Kind);
            });
    }

    [Fact]
    public void Compile_Still_Executes_Primitives_And_Booleans_With_M6b_ValidationExecution_Output()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m3d-mixed-primitive-boolean-validation.firmament");

        Assert.True(result.Compilation.IsSuccess);
        Assert.NotEmpty(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives);
        Assert.NotEmpty(result.Compilation.Value.ValidationExecutionResult!.Validations);
    }

    private static BrepBody BuildNonManifoldBody()
    {
        var topologyBuilder = new TopologyBuilder();
        var v0 = topologyBuilder.AddVertex();
        var v1 = topologyBuilder.AddVertex();
        var edge = topologyBuilder.AddEdge(v0, v1);

        var faces = new List<FaceId>();
        for (var i = 0; i < 3; i++)
        {
            var loopId = topologyBuilder.AllocateLoopId();
            var coedgeId = topologyBuilder.AllocateCoedgeId();
            topologyBuilder.AddLoop(new Loop(loopId, [coedgeId]));
            topologyBuilder.AddCoedge(new Coedge(coedgeId, edge, loopId, coedgeId, coedgeId, false));
            faces.Add(topologyBuilder.AddFace([loopId]));
        }

        var shell = topologyBuilder.AddShell(faces);
        topologyBuilder.AddBody([shell]);

        return new BrepBody(topologyBuilder.Model, new BrepGeometryStore(), new BrepBindingModel());
    }

    private static FirmamentCompileResult CompileFixture(string fixturePath) =>
        Compile(FirmamentCorpusHarness.ReadFixtureText(fixturePath));

    private static FirmamentCompileResult Compile(string source)
    {
        var compiler = new FirmamentCompiler();
        return compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));
    }
}
