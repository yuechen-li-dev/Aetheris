using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Analysis;
using Aetheris.Kernel.Core.Brep.Boolean;

namespace Aetheris.Kernel.Core.Tests.Brep.Analysis;

public sealed class BrepEnclosedVoidAnalyzerTests
{
    [Fact]
    public void Analyze_BoxSphereContainedSubtract_Detects_EnclosedVoid()
    {
        var baseBox = BrepPrimitives.CreateBox(40d, 30d, 12d).Value;
        var innerSphere = BrepPrimitives.CreateSphere(4d).Value;

        var result = BrepBoolean.Subtract(baseBox, innerSphere);

        Assert.True(result.IsSuccess);
        var facts = BrepEnclosedVoidAnalyzer.Analyze(result.Value);
        Assert.True(facts.HasEnclosedVoids);
        Assert.Equal(1, facts.EnclosedVoidCount);
        Assert.Single(facts.EnclosedVoidShellIds);
    }

    [Fact]
    public void Analyze_BoxCylinderThroughHole_DoesNotDetect_EnclosedVoid()
    {
        var baseBox = BrepPrimitives.CreateBox(40d, 30d, 12d).Value;
        var through = BrepPrimitives.CreateCylinder(4d, 20d).Value;

        var result = BrepBoolean.Subtract(baseBox, through);

        Assert.True(result.IsSuccess);
        var facts = BrepEnclosedVoidAnalyzer.Analyze(result.Value);
        Assert.False(facts.HasEnclosedVoids);
        Assert.Equal(0, facts.EnclosedVoidCount);
        Assert.Empty(facts.EnclosedVoidShellIds);
    }
}
