using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Lattice;
using Aetheris.Continuum.Regions.Constructive;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.StandardLibrary;

namespace Aetheris.Continuum.Tests.Lattice;

public sealed class ContinuumM4CTests
{
    [Fact]
    public void FixedWholePartHexBoltBeatsComparableFineControlAndUsesCurvedSupportMaps()
    {
        var f=Fixture(Transform3D.Identity);var fixedRun=Run(f,12,4,true);var fine=ContinuumGridClassifier.Classify(f.Dual.Continuum,new LatticeSpec(f.Dual.Continuum.Bounds,24,24,24),4);
        var exact=f.Dual.Continuum.AnalyticReferenceVolume;var fixedError=double.Abs(fixedRun.Volume-exact)/exact;var fineError=double.Abs(fine.EstimatedOccupiedVolume-exact)/exact;
        Assert.True(fixedError<fineError,$"fixed={fixedError:R}; fine={fineError:R}");Assert.InRange(fixedError,0d,.0015d);
        Assert.Contains(fixedRun.Sets.SelectMany(s=>s.Contributors),c=>c.SupportKind==SurfaceGeometryKind.Cylinder&&c.LocalMap is not null);
        Assert.Contains(fixedRun.Sets.SelectMany(s=>s.Contributors),c=>c.SupportKind==SurfaceGeometryKind.Cone&&c.LocalMap is not null);
        Assert.Contains(fixedRun.Sets.SelectMany(s=>s.Contributors),c=>c.SupportKind==SurfaceGeometryKind.Torus&&c.LocalMap is not null);
    }

    [Fact]
    public void MultiSupportEdgesFilletsAndTrimJunctionsRetainPerSupportIdentity()
    {
        var run=Run(Fixture(Transform3D.Identity),12,4,true);
        Assert.Contains(run.Sets,s=>s.CompositionKind==CutCellCompositionKind.TwoFaceEdge&&s.Contributors.Any(c=>c.SupportKind==SurfaceGeometryKind.Plane)&&s.Contributors.Any(c=>c.SupportKind is SurfaceGeometryKind.Cylinder or SurfaceGeometryKind.Cone&&c.LocalMap is not null));
        Assert.Contains(run.Sets,s=>s.CompositionKind==CutCellCompositionKind.FilletContact&&s.Contributors.Any(c=>c.SupportKind==SurfaceGeometryKind.Torus));
        Assert.Contains(run.Sets,s=>s.CompositionKind==CutCellCompositionKind.MultiFaceTrimJunction&&s.Contributors.Any(c=>c.SupportKind==SurfaceGeometryKind.Cone&&c.LocalMap is not null));
        Assert.All(run.Sets.Where(s=>s.Contributors.Count>1&&s.Contributors.Any(c=>c.SupportKind!=SurfaceGeometryKind.Plane)),s=>Assert.Contains("composite-rays",s.Integration.Method,StringComparison.Ordinal));
    }

    [Fact]
    public void HyperbolaTrimmedConePlaneJunctionKeepsBrepTrimAndConeMap()
    {
        var f=Fixture(Transform3D.Identity);var hyperbolaEdges=f.Dual.Brep.Body.Topology.Edges.Where(e=>f.Dual.Brep.Body.TryGetEdgeCurveGeometry(e.Id,out var curve)&&curve?.Kind==CurveGeometryKind.Hyperbola3).Select(e=>e.Id).ToHashSet();
        Assert.NotEmpty(hyperbolaEdges);
        var run=Run(f,12,4,true);Assert.Contains(run.Sets,s=>s.Contributors.Any(c=>c.EdgeIds.Any(hyperbolaEdges.Contains)&&c.SupportKind==SurfaceGeometryKind.Cone&&c.LocalMap is not null)&&s.Contributors.Any(c=>c.SupportKind==SurfaceGeometryKind.Plane));
    }

    [Fact]
    public void ObviousPlanarCellsBypassJudgmentAndPlanSelectionIsDeterministic()
    {
        var a=Run(Fixture(Transform3D.Identity),8,4,true);var b=Run(Fixture(Transform3D.Identity),8,4,true);
        var planar=a.Sets.First(s=>s.CompositionKind==CutCellCompositionKind.SingleFace&&s.Contributors.All(c=>c.SupportKind==SurfaceGeometryKind.Plane));
        Assert.Null(planar.Judgment);Assert.Equal("exact-convex-planar-clipping",planar.Integration.Method);
        Assert.Equal(a.Sets.Select(Stable),b.Sets.Select(Stable));
    }

    [Fact]
    public void BaselineAndRotationsRemainOnTheSameAccuracyScale()
    {
        var transforms=new[]{Transform3D.Identity,Transform3D.CreateRotationY(29d*double.Pi/180d),Transform3D.CreateRotationX(17d*double.Pi/180d)*Transform3D.CreateRotationY(31d*double.Pi/180d)*Transform3D.CreateRotationZ(13d*double.Pi/180d)};
        var errors=transforms.Select(t=>{var f=Fixture(t);var r=Run(f,12,4,true);return double.Abs(r.Volume-f.Dual.Continuum.AnalyticReferenceVolume)/f.Dual.Continuum.AnalyticReferenceVolume;}).ToArray();
        Assert.All(errors,e=>Assert.InRange(e,0d,.0015d));Assert.True(errors.Max()<20d*double.Max(errors.Min(),1e-12d));
    }

    [Fact]
    public void TorusQuarticRootIsolationRetainsAllChartsDeterministically()
    {
        // (x^2-1)(x^2-4) is the four-branch section produced by a ray through a torus.
        var a=RealPolynomialRoots.InInterval([4d,0d,-5d,0d,1d],-3d,3d);
        var b=RealPolynomialRoots.InInterval([4d,0d,-5d,0d,1d],-3d,3d);
        Assert.Equal(new[]{-2d,-1d,1d,2d},a.Select(x=>double.Round(x,9)));
        Assert.Equal(a,b);
    }

    [Fact]
    public void CompositeProductionPathUsesNoVolumetricMsaaFallback()
    {
        var run=Run(Fixture(Transform3D.Identity),12,4,true);
        Assert.All(run.Sets,s=>Assert.Equal(0,s.Integration.MsaaFallbackSamples));
        Assert.Contains(run.Sets,s=>s.Contributors.Any(c=>c.SupportKind==SurfaceGeometryKind.Torus)&&s.Integration.Method.Contains("composite-rays",StringComparison.Ordinal));
    }

    private static string Stable(CutCellBoundarySet s)=>$"{s.CellIndex}:{s.CompositionKind}:{s.Integration.Method}:{string.Join(',',s.Contributors.Select(c=>$"{c.SupportKind}:{c.LocalMap?.Approximation.ResolutionU}x{c.LocalMap?.Approximation.ResolutionV}"))}";
    private static (double Volume,IReadOnlyList<CutCellBoundarySet> Sets) Run(FixtureData f,int n,int samples,bool compose)
    {var grid=ContinuumGridClassifier.Classify(f.Dual.Continuum,new LatticeSpec(f.Dual.Continuum.Bounds,n,n,n),samples);var sets=compose?grid.CutCells.Where(c=>f.Shell.Query(c.Bounds).Count>0).Select(c=>f.Composer.Compose(c.Index,c.Bounds)).ToArray():[];var size=grid.Lattice.CellSize;var cv=size.X*size.Y*size.Z;return(grid.InsideCellCount*cv+sets.Sum(s=>s.Integration.OccupancyFraction*cv),sets);}
    private static FixtureData Fixture(Transform3D transform)
    {var plan=HexBoltConstructionPlanner.Plan(McMasterHexBoltSpecs.Reference91180A151,"M4C-Test-HexBolt").Value!;var dual=ExactCoaxialDualMaterializer.Materialize(plan,transform);var association=new CirBrepAssociation(dual.Continuum.Id,plan.StableId,"outer-shell",plan.StableId,dual.ConstructionSourceIdentity);var semantics=dual.Brep.Semantics.Where(s=>s.Face.HasValue).GroupBy(s=>s.Face!.Value).ToDictionary(g=>g.Key,g=>string.Join("|",g.Select(x=>x.StableId).Order()));var shell=new WholeShellBoundaryQuery(dual.Brep.Body,association,transform,semantics);return new(dual,shell,new WholePartCutCellComposer(dual.Continuum,shell));}
    private sealed record FixtureData(ExactCoaxialDualLowering Dual,WholeShellBoundaryQuery Shell,WholePartCutCellComposer Composer);
}
