using Aetheris.FrictionLab;
using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Lowering;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class CirBrepX4RecognizerSourceContractLabTests
{
    [Fact]
    public void Experiment1_SourceInventory_CollectsAllCandidateSourcesForBooleanBoxCylinderHole()
    {
        var compile = CompileFixture();
        var lowered = CirBrepX4RecognizerSourceContractLab.LowerViaReflection(compile.PrimitiveLoweringPlan!);

        var inventory = CirBrepX4RecognizerSourceContractLab.BuildInventory(compile, lowered.Root);

        Assert.True(inventory.HasRootNode, "Lowered CirNode root must be available for direct geometric recognition.");
        Assert.True(inventory.HasLoweringPlan, "Lowering plan should be available in Firmament compilation artifact.");
        Assert.True(inventory.HasExecutionResult, "Execution result should be available after successful compile.");
        Assert.True(inventory.HasNativeGeometryState, "NativeGeometryState should be available from execution result.");
        Assert.True(inventory.HasReplayLog, "Replay log should be available as provenance source.");
        Assert.True(inventory.HasCirMirrorSummary, "CIR mirror summary should be available as envelope diagnostic.");
    }

    [Fact]
    public void Experiment2_NodeOnlyEnvelope_RecognizesDirectAndTransformWrappedSubtract_ButLacksProvenance()
    {
        var direct = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 12));
        var wrapped = new CirTransformNode(direct, Transform3D.CreateTranslation(new Vector3D(3, 0, 0)));

        var directResult = CirBrepX4RecognizerSourceContractLab.Recognize(CirBoxCylinderRecognizerInput.FromNode(direct));
        var wrappedResult = CirBrepX4RecognizerSourceContractLab.Recognize(CirBoxCylinderRecognizerInput.FromNode(wrapped));

        Assert.True(directResult.Recognition.Success, $"Node-only direct subtract should recognize; got {directResult.Recognition.Reason}: {directResult.Recognition.Diagnostic}");
        Assert.True(wrappedResult.Recognition.Success, $"Node-only wrapped subtract should recognize; got {wrappedResult.Recognition.Reason}: {wrappedResult.Recognition.Diagnostic}");
        Assert.Contains(directResult.Diagnostics, d => d.Contains("Replay unavailable", StringComparison.Ordinal));
    }

    [Fact]
    public void Experiment3_NodePlusReplay_PreservesRecognition_AndAddsProvenanceDiagnostics()
    {
        var compile = CompileFixture();
        var lowered = CirBrepX4RecognizerSourceContractLab.LowerViaReflection(compile.PrimitiveLoweringPlan!);
        var replay = compile.PrimitiveExecutionResult!.NativeGeometryState.ReplayLog;

        var result = CirBrepX4RecognizerSourceContractLab.Recognize(CirBoxCylinderRecognizerInput.FromNode(lowered.Root, replay));

        Assert.True(result.Recognition.Success, $"Node+replay should preserve geometric recognition; got {result.Recognition.Reason}: {result.Recognition.Diagnostic}");
        Assert.Contains(result.Diagnostics, d => d.Contains("Replay matched subtract op", StringComparison.Ordinal));
        Assert.False(result.ReplayGeometryMismatch, "Replay should match node geometry for canonical fixture.");
    }

    [Fact]
    public void Experiment4_NativeStateAlone_IsInsufficientWithoutExplicitRoot()
    {
        var compile = CompileFixture();
        var nativeState = compile.PrimitiveExecutionResult!.NativeGeometryState;

        var ex = Assert.Throws<InvalidOperationException>(() => CirBoxCylinderRecognizerInput.FromNativeState(nativeState));
        Assert.Contains("explicit CirNode root is required", ex.Message, StringComparison.Ordinal);

        var lowered = CirBrepX4RecognizerSourceContractLab.LowerViaReflection(compile.PrimitiveLoweringPlan!);
        var input = CirBoxCylinderRecognizerInput.FromNativeState(nativeState, lowered.Root);
        var recognized = CirBrepX4RecognizerSourceContractLab.Recognize(input);
        Assert.True(recognized.Recognition.Success, "NativeState wrapper with explicit root should recognize because geometry still comes from node.");
    }

    [Fact]
    public void Experiment5_LoweringPlanAndExecutionResult_AreUsefulEnvelopesButNotPrimaryGeometrySource()
    {
        var compile = CompileFixture();
        var plan = compile.PrimitiveLoweringPlan!;
        var execution = compile.PrimitiveExecutionResult!;

        Assert.Contains(plan.Booleans, b => b.Kind == FirmamentLoweredBooleanKind.Subtract && b.Tool.OpName == "cylinder");
        Assert.NotNull(execution.NativeGeometryState.ReplayLog);
        Assert.True(string.IsNullOrWhiteSpace(execution.NativeGeometryState.CirIntentRootReference) || execution.NativeGeometryState.CirIntentRootReference == "hole");
    }

    [Fact]
    public void Experiment6_MismatchPolicy_NodeGeometryIsAuthoritative_ReplayIsDiagnosticOnly()
    {
        var geometry = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(2));
        var replay = new NativeGeometryReplayLog([
            new NativeGeometryReplayOperation(2, "hole", "boolean:subtract", "base", "cylinder", "tool", null, default, "ref")
        ]);

        var result = CirBrepX4RecognizerSourceContractLab.Recognize(CirBoxCylinderRecognizerInput.FromNode(geometry, replay));

        Assert.False(result.Recognition.Success, "Node geometry should reject non-cylinder subtract even if replay claims cylinder tool kind.");
        Assert.True(result.ReplayGeometryMismatch, "Node/replay disagreement should be surfaced as mismatch diagnostic.");
    }

    [Fact]
    public void Experiment7_RecommendedInputShape_SupportsNodeOnlyAndNodePlusReplayConstruction()
    {
        var nodeInput = CirBoxCylinderRecognizerInput.FromNode(new CirSubtractNode(new CirBoxNode(8, 8, 8), new CirCylinderNode(1, 10)));
        Assert.NotNull(nodeInput.Root);
        Assert.Null(nodeInput.ReplayLog);

        var replay = new NativeGeometryReplayLog([]);
        var nodeReplayInput = CirBoxCylinderRecognizerInput.FromNode(nodeInput.Root, replay, "fixture");
        Assert.Same(replay, nodeReplayInput.ReplayLog);
        Assert.Equal("fixture", nodeReplayInput.SourceLabel);
    }

    private static FirmamentCompilationArtifact CompileFixture()
    {
        var root = ResolveRepoRoot();
        var absolute = Path.Combine(root, "testdata/firmament/examples/boolean_box_cylinder_hole.firmament");
        var source = File.ReadAllText(absolute);
        var compiler = new FirmamentCompiler();
        var result = compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source, absolute)));
        Assert.True(result.Compilation.IsSuccess, string.Join(" | ", result.Compilation.Diagnostics.Select(d => d.Message)));
        return result.Compilation.Value;
    }

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
