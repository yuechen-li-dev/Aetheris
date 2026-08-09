using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Regions.Constructive;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.StandardLibrary;

namespace Aetheris.Continuum.Tests.Lattice;

public sealed class ContinuumM4BDualLoweringTests
{
    [Fact]
    public void SameTypedPlanProducesCompleteAssociatedBrepAndCir()
    {
        var dual=Fixture("m4b-complete",Transform3D.Identity);
        var association=Association(dual);
        var shell=new WholeShellBoundaryQuery(dual.Brep.Body,association,dual.Continuum.Transform,Semantics(dual));
        var consistency=BrepCirConsistencyChecker.Check(dual.Continuum,shell,2e-7d);
        Assert.True(consistency.Passed,consistency.Summary+Environment.NewLine+string.Join(Environment.NewLine,consistency.Probes.Where(p=>!p.Passed)));
        Assert.Equal(new[]{SurfaceGeometryKind.Plane,SurfaceGeometryKind.Cylinder,SurfaceGeometryKind.Cone,SurfaceGeometryKind.Torus},
            shell.Faces.Select(f=>f.SupportKind).Distinct().Order().ToArray());
        var composer=new WholePartCutCellComposer(dual.Continuum,shell);
        Assert.Equal(0,composer.JudgmentCallCount);
        Assert.All(shell.Faces,face=>Assert.Equal(MaterialSideStatus.Resolved,composer.MaterialSides[face.FaceId].Status));
    }

    [Fact]
    public void AssociationRejectsDifferentConstructiveLineage()
    {
        var a=Fixture("m4b-a",Transform3D.Identity);var b=Fixture("m4b-b",Transform3D.Identity);
        var association=Association(a) with { ConstructionSourceIdentity=b.ConstructionSourceIdentity };
        var shell=new WholeShellBoundaryQuery(a.Brep.Body,association,a.Continuum.Transform,Semantics(a));
        var result=BrepCirConsistencyChecker.Check(a.Continuum,shell,2e-7d);
        Assert.False(result.Passed);Assert.Contains(result.Probes,p=>p.Kind=="lineage"&&!p.Passed);
    }

    [Fact]
    public void ReferenceHexBoltDualLowersWithoutRepresentationMismatch()
    {
        var plan=HexBoltConstructionPlanner.Plan(McMasterHexBoltSpecs.Reference91180A151,"HexBolt-M4B").Value!;
        var dual=ExactCoaxialDualMaterializer.Materialize(plan,Transform3D.Identity);
        var shell=new WholeShellBoundaryQuery(dual.Brep.Body,Association(dual),dual.Continuum.Transform,Semantics(dual));
        var result=BrepCirConsistencyChecker.Check(dual.Continuum,shell,2e-7d);
        Assert.True(result.Passed,result.Summary+Environment.NewLine+string.Join(Environment.NewLine,result.Probes.Where(p=>!p.Passed)));
        var composer=new WholePartCutCellComposer(dual.Continuum,shell);
        Assert.All(new[]{SurfaceGeometryKind.Plane,SurfaceGeometryKind.Cylinder,SurfaceGeometryKind.Cone,SurfaceGeometryKind.Torus},
            family=>Assert.Contains(shell.Faces,f=>f.SupportKind==family));
        Assert.All(composer.MaterialSides.Values,e=>Assert.Equal(MaterialSideStatus.Resolved,e.Status));
    }

    private static ExactCoaxialDualLowering Fixture(string id,Transform3D transform)
    {
        var recipe=new ExactCoaxialPartRecipe(id,6,3d,.9d,1d,30d,.25d,1.5d,2d,.25d,1.15d,.8d,"M4B","proof");
        return ExactCoaxialDualMaterializer.Materialize(ExactCoaxialPartBuilder.Plan(recipe).Value!,transform);
    }
    private static CirBrepAssociation Association(ExactCoaxialDualLowering dual)=>new(dual.Continuum.Id,dual.Plan.StableId,"outer-shell",dual.Plan.StableId,dual.ConstructionSourceIdentity);
    private static IReadOnlyDictionary<Aetheris.Kernel.Core.Topology.FaceId,string> Semantics(ExactCoaxialDualLowering dual)=>dual.Brep.Semantics.Where(s=>s.Face.HasValue).GroupBy(s=>s.Face!.Value).ToDictionary(g=>g.Key,g=>string.Join("|",g.Select(s=>s.StableId).Order()));
}
