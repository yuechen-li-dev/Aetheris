using System.Reflection;
using Aetheris.FrictionLab;
using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Lowering;
using Aetheris.Kernel.StandardLibrary;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class CirBrepX31FirmamentLoweringEntrypointLabTests
{
    [Fact]
    public void SetupVerification_CirLabArtifactsExist_AndCompilePathAvailable()
    {
        var root = ResolveRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "Aetheris.Firmament.FrictionLab/CIRLab/CIRBoxCylinderRecognitionLab.cs")));
        Assert.True(File.Exists(Path.Combine(root, "Aetheris.FrictionLab.Tests/CIRLab/CirBoxCylinderRecognitionLabTests.cs")));
        Assert.True(File.Exists(Path.Combine(root, "Aetheris.FrictionLab.Tests/CIRLab/Reports/cir-brep-x3-box-cylinder-recognition-lab.md")));
        Assert.True(File.Exists(Path.Combine(root, "docs/frictionlab/cir-brep-x3-box-cylinder-recognition-lab.md")));
    }

    [Fact]
    public void Experiment1_BooleanBoxCylinderHole_LowersToSubtractWithTransformWrappedOperands()
    {
        var compile = CompileFixture("testdata/firmament/examples/boolean_box_cylinder_hole.firmament");
        var lower = LowerViaReflection(compile.PrimitiveLoweringPlan!);

        var subtract = Assert.IsType<CirSubtractNode>(lower.Root);
        var leftTx = Assert.IsType<CirTransformNode>(subtract.Left);
        var rightTx = Assert.IsType<CirTransformNode>(subtract.Right);
        Assert.IsType<CirBoxNode>(leftTx.Child);
        Assert.IsType<CirCylinderNode>(rightTx.Child);

        var labRecognition = CirBoxCylinderRecognitionLab.Recognize(lower.Root);
        Assert.True(labRecognition.Success, $"Expected normalization to accept real lowered shape, got {labRecognition.Reason}: {labRecognition.Diagnostic}");

        var hole = compile.PrimitiveExecutionResult!.NativeGeometryState.ReplayLog.Operations.Single(o => o.FeatureId == "hole");
        Assert.Equal("boolean:subtract", hole.OperationKind);
        Assert.Equal("base", hole.SourceFeatureId);
        Assert.Equal("cylinder", hole.ToolKind);
    }

    [Fact]
    public void Experiment2_StandardLibraryReusablePart_CubeWithCylindricalHole_BypassesFirmamentCirLoweringPath()
    {
        var part = StandardLibraryReusableParts.CreateCubeWithCylindricalHole();
        Assert.True(part.IsSuccess);
    }

    [Fact]
    public void Experiment3_EntrypointComparison_RawNodePlanAndReplayHaveDifferentStrengths()
    {
        var compile = CompileFixture("testdata/firmament/examples/boolean_box_cylinder_hole.firmament");
        var plan = compile.PrimitiveLoweringPlan!;
        var lowered = LowerViaReflection(plan);
        var replay = compile.PrimitiveExecutionResult!.NativeGeometryState.ReplayLog;

        Assert.True(CirBoxCylinderRecognitionLab.Recognize(lowered.Root).Success);
        Assert.Contains(plan.Booleans, b => b.FeatureId == "hole" && b.Kind == FirmamentLoweredBooleanKind.Subtract);

        var holeReplay = replay.Operations.Single(o => o.FeatureId == "hole");
        Assert.Equal("boolean:subtract", holeReplay.OperationKind);
        Assert.Equal("cylinder", holeReplay.ToolKind);
        Assert.True(holeReplay.ResolvedPlacement.IsResolved);
    }

    [Fact]
    public void Experiment5And6_ReplayAndNativeStateExposeUsefulDiagnosticsButNotFullCsgTree()
    {
        var compile = CompileFixture("testdata/firmament/examples/boolean_box_cylinder_hole.firmament");
        var state = compile.PrimitiveExecutionResult!.NativeGeometryState;

        Assert.NotEmpty(state.ReplayLog.Operations);
        Assert.Equal(CirMirrorStatus.Available, state.CirMirror.Status);
        Assert.NotNull(state.CirMirror.Summary);
        Assert.True(state.CirMirror.Summary!.EstimatedVolume > 0d);
        Assert.True(state.CirIntentRootReference is null || state.CirIntentRootReference == "hole");
    }

    private static FirmamentCompilationArtifact CompileFixture(string path)
    {
        var absolute = Path.Combine(ResolveRepoRoot(), path);
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

    private static CirLoweringResult LowerViaReflection(FirmamentPrimitiveLoweringPlan plan)
    {
        var lowererType = typeof(FirmamentCompiler).Assembly.GetType("Aetheris.Kernel.Firmament.Lowering.FirmamentCirLowerer");
        var method = lowererType?.GetMethod("Lower", BindingFlags.Public | BindingFlags.Static);
        var kernelResult = method?.Invoke(null, [plan]);
        Assert.NotNull(kernelResult);
        dynamic result = kernelResult!;
        Assert.True(result.IsSuccess);
        return (CirLoweringResult)result.Value;
    }
}
