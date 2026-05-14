using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Lowering;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FrepSemanticRecoveryRematerializerTests
{
    [Fact]
    public void SemanticRecovery_BoxCylinder_ProducesBRepBody()
    {
        var root = new CirSubtractNode(new CirBoxNode(20d, 10d, 8d), new CirCylinderNode(2d, 20d));
        var result = FrepSemanticRecoveryRematerializer.TryRecover(root);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Body);
        Assert.Equal(nameof(ThroughHoleRecoveryPolicy), result.SelectedPolicy);
        Assert.Contains(result.Diagnostics, d => d.Contains("planner ran", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Contains("executor succeeded", StringComparison.Ordinal));
    }

    [Fact]
    public void SemanticRecovery_TranslatedBoxCylinder_ProducesBRepBody()
    {
        var root = new CirSubtractNode(
            new CirTransformNode(new CirBoxNode(20d, 20d, 10d), Transform3D.CreateTranslation(new Vector3D(5d, 2d, 4d))),
            new CirTransformNode(new CirCylinderNode(3d, 16d), Transform3D.CreateTranslation(new Vector3D(4d, 1d, 4d))));

        var result = FrepSemanticRecoveryRematerializer.TryRecover(root);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Body);
        Assert.Equal(nameof(ThroughHoleRecoveryPolicy), result.SelectedPolicy);
    }

    [Fact]
    public void SemanticRecovery_UnsupportedBoxSphere_FallsBackOrRejectsCleanly()
    {
        var root = new CirSubtractNode(new CirBoxNode(20d, 10d, 8d), new CirSphereNode(2d));
        var result = FrepSemanticRecoveryRematerializer.TryRecover(root);

        Assert.False(result.Succeeded);
        Assert.Null(result.Body);
        Assert.Equal(FrepMaterializerDecisionStatus.Selected, result.Decision.Status);
        Assert.Equal(nameof(CirOnlyFallbackPolicy), result.SelectedPolicy);
        Assert.Contains(result.Diagnostics, d => d.Contains("selected policy was not ThroughHoleRecoveryPolicy", StringComparison.Ordinal));
    }


    [Fact]
    public void CirOnlyFallback_DoesNotProduceBrepSuccess()
    {
        var root = new CirSubtractNode(new CirBoxNode(20d, 10d, 8d), new CirSphereNode(2d));
        var result = FrepSemanticRecoveryRematerializer.TryRecover(root);

        Assert.False(result.Succeeded);
        Assert.Null(result.Body);
        Assert.Equal(nameof(CirOnlyFallbackPolicy), result.SelectedPolicy);
        Assert.Contains(result.Diagnostics, d => d.Contains("selected policy was not ThroughHoleRecoveryPolicy", StringComparison.Ordinal));
    }

    [Fact]
    public void Rematerializer_CirOnlyBoxCylinder_TransitionsToBRepActive_UsingSemanticRecovery()
    {
        var plan = BuildBoxMinusCylinderPlan();
        var state = new NativeGeometryState(NativeGeometryExecutionMode.CirOnly, NativeGeometryMaterializationAuthority.PendingRematerialization, null, "hole", BuildReplay("cylinder"), [], new NativeGeometryCirMirrorState(CirMirrorStatus.NotAttempted, null, []));

        var remat = NativeGeometryRematerializer.TryRematerialize(plan, state);

        Assert.True(remat.IsSuccess, string.Join(" | ", remat.Diagnostics.Select(d => d.Message)));
        Assert.Equal(NativeGeometryExecutionMode.BRepActive, remat.Value.ExecutionMode);
        Assert.Contains(remat.Value.TransitionEvents, e => e.Message.Contains("semantic recovery policy", StringComparison.Ordinal));
    }

    private static NativeGeometryReplayLog BuildReplay(string toolKind)
        => new([
            new NativeGeometryReplayOperation(0, "base", "primitive:box", null, null, null, null, new NativeGeometryResolvedPlacement(NativeGeometryPlacementKind.None, null, null, Vector3D.Zero, Vector3D.Zero, true, null), null),
            new NativeGeometryReplayOperation(1, "cut", "boolean:subtract", "base", toolKind, "tool", null, new NativeGeometryResolvedPlacement(NativeGeometryPlacementKind.None, null, null, Vector3D.Zero, Vector3D.Zero, true, null), null)
        ]);

    private static FirmamentPrimitiveLoweringPlan BuildBoxMinusCylinderPlan()
    {
        var primitives = new[] { new FirmamentLoweredPrimitive(0, "base", FirmamentLoweredPrimitiveKind.Box, new FirmamentLoweredBoxParameters(20, 20, 10), null) };
        var booleans = new[] { new FirmamentLoweredBoolean(1, "hole", FirmamentLoweredBooleanKind.Subtract, "from", "base", new FirmamentLoweredToolOp("cylinder", new Dictionary<string, string> { { "op", "cylinder" }, { "radius", "3" }, { "height", "20" } }, "op cylinder radius=3 height=20"), null) };
        return new FirmamentPrimitiveLoweringPlan(primitives, booleans, []);
    }
}
