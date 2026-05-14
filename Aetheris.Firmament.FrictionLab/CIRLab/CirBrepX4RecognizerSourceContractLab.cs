using System.Reflection;
using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Firmament.Lowering;

namespace Aetheris.FrictionLab;

public sealed record CirBoxCylinderRecognizerInput(
    CirNode Root,
    NativeGeometryReplayLog? ReplayLog = null,
    NativeGeometryState? NativeState = null,
    FirmamentPrimitiveLoweringPlan? LoweringPlan = null,
    FirmamentPrimitiveExecutionResult? ExecutionResult = null,
    string? SourceLabel = null)
{
    public static CirBoxCylinderRecognizerInput FromNode(CirNode root, NativeGeometryReplayLog? replayLog = null, string? sourceLabel = null)
        => new(root, replayLog, null, null, null, sourceLabel);

    public static CirBoxCylinderRecognizerInput FromNativeState(NativeGeometryState nativeState, CirNode? root = null, string? sourceLabel = null)
    {
        ArgumentNullException.ThrowIfNull(nativeState);
        if (root is null)
        {
            throw new InvalidOperationException("NativeGeometryState does not contain a traversable CIR root; explicit CirNode root is required for geometric recognition.");
        }

        return new(root, nativeState.ReplayLog, nativeState, null, null, sourceLabel);
    }
}

public sealed record CirRecognizerSourceInventory(
    bool HasRootNode,
    bool HasLoweringPlan,
    bool HasExecutionResult,
    bool HasNativeGeometryState,
    bool HasReplayLog,
    bool HasCirMirrorSummary,
    string RootNodeKind,
    string? CirIntentRootReference);

public sealed record CirBoxCylinderRecognitionWithDiagnostics(
    CirBoxCylinderRecognitionLabResult Recognition,
    IReadOnlyList<string> Diagnostics,
    bool ReplayGeometryMismatch);

public static class CirBrepX4RecognizerSourceContractLab
{
    public static CirRecognizerSourceInventory BuildInventory(FirmamentCompilationArtifact compile, CirNode loweredRoot)
    {
        var execution = compile.PrimitiveExecutionResult;
        var state = execution?.NativeGeometryState;
        return new(
            HasRootNode: true,
            HasLoweringPlan: compile.PrimitiveLoweringPlan is not null,
            HasExecutionResult: execution is not null,
            HasNativeGeometryState: state is not null,
            HasReplayLog: state?.ReplayLog is not null,
            HasCirMirrorSummary: state?.CirMirror.Summary is not null,
            RootNodeKind: loweredRoot.Kind.ToString(),
            CirIntentRootReference: state?.CirIntentRootReference);
    }

    public static CirBoxCylinderRecognitionWithDiagnostics Recognize(CirBoxCylinderRecognizerInput input)
    {
        var recognition = CirBoxCylinderRecognitionLab.Recognize(input.Root, allowTranslationWrappers: true);
        var diagnostics = new List<string>();
        var mismatch = false;

        if (input.ReplayLog is null)
        {
            diagnostics.Add("Replay unavailable; provenance fields (op/feature/tool ids) not attached.");
        }
        else
        {
            var subtractOps = input.ReplayLog.Operations.Where(o => string.Equals(o.OperationKind, "boolean:subtract", StringComparison.Ordinal)).ToArray();
            if (subtractOps.Length == 0)
            {
                diagnostics.Add("Replay present but no boolean:subtract operations were found.");
            }

            var boxCylinderSubtract = subtractOps.FirstOrDefault(o => string.Equals(o.ToolKind, "cylinder", StringComparison.Ordinal));
            if (boxCylinderSubtract is not null)
            {
                diagnostics.Add($"Replay matched subtract op '{boxCylinderSubtract.FeatureId}' from '{boxCylinderSubtract.SourceFeatureId}'.");
            }
            else if (recognition.Success)
            {
                diagnostics.Add("Geometry recognized as box-cylinder, but replay did not confirm cylinder subtract tool kind.");
                mismatch = true;
            }

            if (!recognition.Success && boxCylinderSubtract is not null)
            {
                diagnostics.Add("Replay suggests subtract+cylinder provenance, but geometry was not recognized from CirNode.");
                mismatch = true;
            }
        }

        return new(recognition, diagnostics, mismatch);
    }

    public static CirLoweringResult LowerViaReflection(FirmamentPrimitiveLoweringPlan plan)
    {
        var lowererType = typeof(FirmamentCompiler).Assembly.GetType("Aetheris.Kernel.Firmament.Lowering.FirmamentCirLowerer");
        var method = lowererType?.GetMethod("Lower", BindingFlags.Public | BindingFlags.Static);
        var kernelResult = method?.Invoke(null, [plan]);
        if (kernelResult is null)
        {
            throw new InvalidOperationException("Unable to resolve FirmamentCirLowerer.Lower via reflection.");
        }

        dynamic result = kernelResult;
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException("FirmamentCirLowerer.Lower returned failure.");
        }

        return (CirLoweringResult)result.Value;
    }
}
