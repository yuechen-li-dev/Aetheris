using System.Security.Cryptography;
using System.Text;
using Aetheris.Forge.Host;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Modules;
using Aetheris.Modules.BuiltIn;
using Aetheris.Piping;
using Aetheris.Surfacing;
using Xunit;

namespace Aetheris.Modules.Tests;

public sealed class ModuleArchitectureTests
{
    [Fact] public void BuiltInsHaveStableIdentityVersionAndDeterministicOrder()
    {
        var first=BuiltInModules.Catalog;var second=AetherisModuleCatalog.Create(first.Modules.Reverse());
        Assert.Equal(["Aetheris.Core","Aetheris.Piping","Aetheris.Surfacing","Aetheris.PlasticShell","Aetheris.SheetMetal"],first.Modules.Select(m=>m.Id.Value));
        Assert.Equal(first.Modules.Select(m=>m.Id.Value),second.Modules.Select(m=>m.Id.Value));
        Assert.Equal("0.3.0",first.Modules.Single(m=>m.Id==SurfacingModule.Id).Version.ToString());
        Assert.Equal(first.Capabilities.Select(c=>c.Id).Order(StringComparer.Ordinal),first.Capabilities.Select(c=>c.Id));
    }

    [Fact] public void DuplicateModuleAndCapabilityOwnershipAreRejected()
    {
        var duplicate=Assert.Throws<ModuleCatalogException>(()=>AetherisModuleCatalog.Create([CoreModule.Definition,CoreModule.Definition]));
        Assert.Contains(duplicate.Diagnostics,d=>d.Kind==ModuleDiagnosticKind.DuplicateModuleId);
        var id=new AetherisModuleId("Aetheris.Other");var other=NewModule(id,capabilities:[new("Surfacing.RuledSurface",id,new(1,0,0),"collision")]);
        var collision=Assert.Throws<ModuleCatalogException>(()=>AetherisModuleCatalog.Create([CoreModule.Definition,SurfacingModule.Definition,other]));
        Assert.Contains(collision.Diagnostics,d=>d.Kind==ModuleDiagnosticKind.DuplicateCapability);
    }

    [Fact] public void DependencyCycleAndMissingDependencyAreTypedAndDeterministic()
    {
        var a=new AetherisModuleId("Aetheris.A");var b=new AetherisModuleId("Aetheris.B");
        var cycle=Assert.Throws<ModuleCatalogException>(()=>AetherisModuleCatalog.Create([NewModule(a,[new(b,new(1,0,0))]),NewModule(b,[new(a,new(1,0,0))])]));
        Assert.Equal("Module dependency cycle: Aetheris.A -> Aetheris.B -> Aetheris.A.",Assert.Single(cycle.Diagnostics).Message);
        var missing=Assert.Throws<ModuleCatalogException>(()=>AetherisModuleCatalog.Create([NewModule(a,[new(b,new(1,0,0))])]));
        Assert.Equal(ModuleDiagnosticKind.MissingDependency,Assert.Single(missing.Diagnostics).Kind);
    }

    [Fact] public void CapabilityRequirementNamesOwnerAndVersion()
    {
        var diagnostic=BuiltInModules.Catalog.RequireCapability("Piping.Future",PipingModule.Id,new(2,0,0));
        Assert.NotNull(diagnostic);Assert.Equal("Piping.Future",diagnostic!.CapabilityId);Assert.Equal(PipingModule.Id,diagnostic.OwningModule);Assert.Equal(new ModuleVersion(2,0,0),diagnostic.RequiredVersion);
        Assert.Null(BuiltInModules.Catalog.RequireCapability(PipingModule.PipeRouteCapability,PipingModule.Id,new(0,1,0)));
    }

    [Fact] public void ForgeHostInspectsEngineeringModulesWithoutKernelSdk()
    {
        var host=new ForgeHost();Assert.Contains(host.EngineeringModules.Modules,m=>m.Id==PipingModule.Id);Assert.DoesNotContain(typeof(ForgeHost).Assembly.GetReferencedAssemblies(),a=>a.Name=="Aetheris.Forge.KernelSDK");
    }

    private static AetherisModule NewModule(AetherisModuleId id,IReadOnlyList<ModuleDependency>? dependencies=null,IReadOnlyList<ModuleCapability>? capabilities=null)=>new(id,id.Value,new(1,0,0),capabilities??[],[],[],[],[],dependencies??[],new("test"));
}

public sealed class SurfacingModuleTests
{
    [Fact] public void LineLineRuledSurfacePreservesIdentityAndProvenance()
    {
        var a=new RuledBoundary.Line("a",new(0,0,0),new(10,0,0));var b=new RuledBoundary.Line("b",new(0,5,1),new(10,5,-1));var ir=new RuledSurfaceIr("surface",RuledConstructionKind.RuledSurface,a,b,new("a","source:a","boundary-a"),new("b","source:b","boundary-b"));var result=RuledSurfaceLowering.Lower(ir);
        Assert.True(result.IsSuccess);Assert.Equal(SurfaceGeometryKind.BSplineSurfaceWithKnots,result.Patch!.ExactSurface.Kind);Assert.Equal("RULED_SURFACE",result.Patch.ExactSurface.BSplineSurfaceWithKnots!.SurfaceForm);Assert.Equal(["a","b"],result.Patch.BoundaryProvenance.Select(p=>p.BoundaryStableId));
    }

    [Fact] public void CoaxialCircleBoundariesLowerToExactCylinder()
    {
        var z=Direction3D.Create(new(0,0,1));var x=Direction3D.Create(new(1,0,0));var c0=new RuledBoundary.Circle("c0",new(0,0,0),z,3,x);var c1=new RuledBoundary.Circle("c1",new(0,0,8),z,3,x);var result=RuledSurfaceLowering.Lower(new("cylinder",RuledConstructionKind.RuledTransition,c0,c1,new("c0","s","section-a"),new("c1","s","section-b")));
        Assert.True(result.IsSuccess);Assert.Equal(SurfaceGeometryKind.Cylinder,result.Patch!.ExactSurface.Kind);Assert.Equal(RuledConstructionKind.RuledTransition,result.Patch.Ir.Kind);
    }

    [Fact] public void SaddleIsExactBilinearAndPanelStepIsDeterministic()
    {
        var ir=RuledSurfaceLowering.Saddle("saddle",4,3,2);var patch=RuledSurfaceLowering.Lower(ir).Patch!;Assert.Equal(new Point3D(0,0,0),patch.Evaluate(.5,.5));Assert.True(patch.Evaluate(0,0).Z>0);Assert.True(patch.Evaluate(1,1).Z>0);
        var panel=RuledSurfacePanelMaterializer.Materialize(ir,1);Assert.NotNull(panel.Body);Assert.Equal(6,panel.Body!.Topology.Faces.Count());var a=Step242Exporter.ExportBody(panel.Body);var b=Step242Exporter.ExportBody(panel.Body);Assert.True(a.IsSuccess);Assert.Equal(Hash(a.Value),Hash(b.Value));
    }
    private static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

public sealed class PipingModuleTests
{
    [Fact] public void StraightPathPipeIsExactCylinder()
    {var result=PipeRouteLowering.LowerStraight(40,new(10));Assert.NotNull(result.Body);Assert.Contains(result.Body!.Geometry.Surfaces,s=>s.Value.Kind==SurfaceGeometryKind.Cylinder);Assert.True(Step242Exporter.ExportBody(result.Body).IsSuccess);}

    [Fact] public void RouteLowersLineBendLineWithEndpointSemanticsAndExactTorus()
    {
        var result=PipeRouteLowering.Lower(StandardPipeElbowTemplate.Create("route",20,50,30));Assert.True(result.IsSuccess,string.Join(";",result.Diagnostics.Select(d=>d.Message)));Assert.Equal(3,result.Ir!.Elements.Count);Assert.Contains(result.Body!.Geometry.Surfaces,s=>s.Value.Kind==SurfaceGeometryKind.Torus);Assert.Equal(["bendEnd","bendStart","centerline","diameter","inlet","inletAxis","outlet","outletAxis"],result.Semantics!.ExposedMembers.Keys.Order(StringComparer.Ordinal));Assert.Contains("transported",result.Ir.FramePolicy);Assert.True(Step242Exporter.ExportBody(result.Body).IsSuccess);
    }

    [Fact] public void InvalidBendAndWallAreDomainDiagnostics()
    {var invalid=PipeRouteLowering.Lower(new("bad",10,4,double.Pi/2,10,new(10,1)));Assert.Contains(invalid.Diagnostics,d=>d.Code=="piping-bend-radius-invalid");Assert.Contains(invalid.Diagnostics,d=>d.Code=="piping-wall-not-supported");}

    [Fact] public void RouteStepAndSemanticIdentitiesAreDeterministic()
    {var a=PipeRouteLowering.Lower(StandardPipeElbowTemplate.Create("route",20,50,30));var b=PipeRouteLowering.Lower(StandardPipeElbowTemplate.Create("route",20,50,30));var sa=Step242Exporter.ExportBody(a.Body!);var sb=Step242Exporter.ExportBody(b.Body!);Assert.Equal(Hash(sa.Value),Hash(sb.Value));Assert.Equal(a.Semantics!.StableIdentity,b.Semantics!.StableIdentity);}
    private static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
