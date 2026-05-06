using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Lowering;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class CirBrepMaterializerTests
{
    [Fact]
    public void CirMaterializer_BoxMinusCylinder_Succeeds()
    {
        var root = new CirSubtractNode(
            new CirTransformNode(new CirBoxNode(20, 20, 10), Transform3D.CreateTranslation(new Vector3D(0, 0, 5))),
            new CirTransformNode(new CirCylinderNode(3, 20), Transform3D.CreateTranslation(new Vector3D(0, 0, 5))));

        var result = CirBrepMaterializer.TryMaterialize(root);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Body);
        Assert.Equal(CirBrepMaterializer.BoxMinusCylinderPattern, result.PatternName);
    }

    [Fact]
    public void CirMaterializer_UnsupportedPattern_FailsClearly()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(3));
        var result = CirBrepMaterializer.TryMaterialize(root);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Body);
        Assert.Equal("rhs-not-cylinder", result.UnsupportedReason);
    }

    [Fact]
    public void CirOnly_BoxMinusCylinder_RematerializesToBRepActive()
    {
        var plan = BuildBoxMinusCylinderPlan();
        var state = new NativeGeometryState(NativeGeometryExecutionMode.CirOnly, NativeGeometryMaterializationAuthority.PendingRematerialization, null, "hole", new NativeGeometryReplayLog([]), [], new NativeGeometryCirMirrorState(CirMirrorStatus.NotAttempted, null, []));

        var remat = NativeGeometryRematerializer.TryRematerialize(plan, state);

        Assert.True(remat.IsSuccess);
        Assert.Equal(NativeGeometryExecutionMode.BRepActive, remat.Value.ExecutionMode);
        Assert.NotNull(remat.Value.MaterializedBody);
        Assert.Contains(remat.Value.TransitionEvents, e => e.FromMode == NativeGeometryExecutionMode.CirOnly && e.ToMode == NativeGeometryExecutionMode.BRepActive);
    }

    private static FirmamentPrimitiveLoweringPlan BuildBoxMinusCylinderPlan()
    {
        var primitives = new[]
        {
            new FirmamentLoweredPrimitive(0, "base", FirmamentLoweredPrimitiveKind.Box, new FirmamentLoweredBoxParameters(20,20,10), null)
        };
        var booleans = new[]
        {
            new FirmamentLoweredBoolean(1, "hole", FirmamentLoweredBooleanKind.Subtract, "from", "base", new FirmamentLoweredToolOp("cylinder", new Dictionary<string,string>{{"op","cylinder"},{"radius","3"},{"height","20"}}, "op cylinder radius=3 height=20"), null)
        };
        return new FirmamentPrimitiveLoweringPlan(primitives, booleans, []);
    }
}
