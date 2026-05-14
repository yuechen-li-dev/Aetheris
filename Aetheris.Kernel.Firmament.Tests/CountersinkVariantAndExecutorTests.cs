using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class CountersinkVariantAndExecutorTests
{
    [Fact]
    public void CountersinkVariant_IsEvaluated_AndRejectedWithExplicitConePrimitiveBlocker()
    {
        var root = new CirSubtractNode(new CirBoxNode(20d, 20d, 10d), new CirCylinderNode(2d, 20d));
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root));

        var countersinkDiagnostics = eval.EvaluationsFor(nameof(CountersinkVariant));
        Assert.NotEmpty(countersinkDiagnostics);
        Assert.Contains(countersinkDiagnostics, d => d.Contains("no cone primitive node", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CountersinkVariant_DoesNotOverrideCounterboreSelection()
    {
        var root = new CirSubtractNode(
            new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(2, 20)),
            new CirTransformNode(new CirCylinderNode(4, 4), Aetheris.Kernel.Core.Math.Transform3D.CreateTranslation(new Aetheris.Kernel.Core.Math.Vector3D(0, 0, -3))));

        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root));
        Assert.True(eval.Admissible);
        Assert.Contains("selected-variant:CounterboreVariant", eval.Evidence);
        Assert.DoesNotContain("selected-variant:CountersinkVariant", eval.Evidence);
    }
}
