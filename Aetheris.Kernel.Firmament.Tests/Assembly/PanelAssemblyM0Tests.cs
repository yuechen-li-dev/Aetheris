using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Semantics;
using Aetheris.Surfacing;
using Xunit;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class PanelAssemblyM0Tests
{
    [Fact]
    public void ExistingInterfaceMateArchitectureValidatesExactPanelEdgeG0()
    {
        var showcase=PanelShowcases.DevelopableFoldedCanopy();
        var a=Member("A",showcase.Panels[0]);var b=Member("B",showcase.Panels[1]);
        var root=new AssemblyMemberSource("Root",AssemblyInstanceKind.Assembly,"PanelNetwork",[a,b],[]);
        var seam=new InterfaceDefinition("interface:PanelEdgeJoin","PanelEdgeJoin",
            [new("A",["BoundaryEdgeCapable","CurveCapable","ExactGeometryCapable"]),new("B",["BoundaryEdgeCapable","CurveCapable","ExactGeometryCapable"])],[],
            Continuity:"G0",EdgeCorrespondence:"OppositeDirections",GapToleranceMm:1e-6);
        var mate=new MateSource("SeamAB","PanelEdgeJoin",[new("A",AssemblyPath.Parse("Root.A.North")),new("B",AssemblyPath.Parse("Root.B.South"))]);
        var result=new AssemblyM0Compiler().Compile(new("Canopy",root,[seam],[mate],AssemblyPath.Parse("Root"),[],[],"fixture"));
        Assert.True(result.IsSuccess,string.Join(Environment.NewLine,result.Diagnostics.Select(item=>$"{item.Code}: {item.Message}")));
        var evidence=Assert.Single(result.Ir!.PanelMateEvidence!);Assert.Equal(0,evidence.G0ResidualMm,9);Assert.Equal("valid",evidence.Status);
        Assert.Equal(AssemblyInstanceKind.Panel,result.Ir.Instances.Single(instance=>instance.Path.ToString()=="Root.A").Kind);
    }

    [Fact]
    public void PanelMateReportsOrientationG1AndDuplicateUseDiagnostics()
    {
        var showcase=PanelShowcases.DevelopableFoldedCanopy();var a=Member("A",showcase.Panels[0]);var b=Member("B",showcase.Panels[1]);
        var root=new AssemblyMemberSource("Root",AssemblyInstanceKind.Assembly,"PanelNetwork",[a,b],[]);
        var seam=new InterfaceDefinition("interface:PanelEdgeJoin","PanelEdgeJoin",
            [new("A",["BoundaryEdgeCapable"]),new("B",["BoundaryEdgeCapable"])],[],Continuity:"G1",EdgeCorrespondence:"SameDirection");
        var roles=new[]{new MateRoleAssignment("A",AssemblyPath.Parse("Root.A.North")),new MateRoleAssignment("B",AssemblyPath.Parse("Root.B.South"))};
        var result=new AssemblyM0Compiler().Compile(new("Canopy",root,[seam],[new("M1","PanelEdgeJoin",roles),new("M2","PanelEdgeJoin",roles)],AssemblyPath.Parse("Root"),[],[],"fixture"));
        Assert.Contains(result.Diagnostics,item=>item.Code=="assembly-panel-mate-endpoint-mismatch");
        Assert.Contains(result.Diagnostics,item=>item.Code=="assembly-panel-mate-g1-unsupported");
        Assert.Contains(result.Diagnostics,item=>item.Code=="assembly-panel-mate-edge-already-mated");
    }

    private static AssemblyMemberSource Member(string name,PanelIr panel)=>new(name,AssemblyInstanceKind.Panel,panel.StableId,[],panel.BoundaryEdges.Select(edge=>edge.SemanticValue).ToArray(),
        Provenance:[new SemanticProvenance("panel-occurrence",panel.StableId,panel.SurfaceConstruction.Kind.ToString())]);
}
