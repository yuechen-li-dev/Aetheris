using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Lattice;
using Aetheris.Continuum.Regions.Analytic;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Continuum.Tests.Lattice;

public sealed class ContinuumM4Tests
{
    [Fact]
    public void WholeShellQuery_DiscoversSingleFaceEdgeAndCornerWithExactIdentity()
    {
        var fixture=Fixture(Transform3D.Identity); var faces=fixture.Shell.Faces;
        var face=faces[0]; var facePoint=Average(face.OuterTrimVertices);
        var edgeId=face.EdgeIds[0]; var edge=fixture.Body.Topology.GetEdge(edgeId); var edgePoint=Mid(fixture.Shell.TransformPoint(edge.StartVertexId),fixture.Shell.TransformPoint(edge.EndVertexId));
        var vertex=edge.StartVertexId; var vertexPoint=fixture.Shell.TransformPoint(vertex);

        Assert.Single(fixture.Shell.Query(Around(facePoint,1e-3d)));
        Assert.Equal(2,fixture.Shell.Query(Around(edgePoint,1e-3d)).Count);
        Assert.Equal(3,fixture.Shell.Query(Around(vertexPoint,1e-3d)).Count);
        Assert.All(fixture.Shell.Faces,c=>Assert.Equal(fixture.Region.Id.Value,c.Reference.ContinuumRegionId));
        Assert.Contains(fixture.Shell.Faces,c=>c.Reference.SemanticRegion is not null);
    }

    [Fact]
    public void CirProbes_ResolveSingleEdgeAndCornerCompositionWithoutJudgment()
    {
        var fixture=Fixture(Transform3D.CreateRotationX(.19d)*Transform3D.CreateRotationZ(.31d));
        var face=fixture.Shell.Faces[0]; var facePoint=Average(face.OuterTrimVertices);
        var edge=fixture.Body.Topology.GetEdge(face.EdgeIds[0]); var edgePoint=Mid(fixture.Shell.TransformPoint(edge.StartVertexId),fixture.Shell.TransformPoint(edge.EndVertexId));
        var vertexPoint=fixture.Shell.TransformPoint(edge.StartVertexId);
        var a=fixture.Composer.Compose(new(0,0,0),Around(facePoint,1e-3));
        var b=fixture.Composer.Compose(new(0,0,1),Around(edgePoint,1e-3));
        var c=fixture.Composer.Compose(new(0,0,2),Around(vertexPoint,1e-3));

        Assert.Equal(CutCellCompositionKind.SingleFace,a.CompositionKind);
        Assert.Equal(CutCellCompositionKind.TwoFaceEdge,b.CompositionKind);
        Assert.Equal(CutCellCompositionKind.ThreeFaceCorner,c.CompositionKind);
        Assert.All(a.Contributors.Concat(b.Contributors).Concat(c.Contributors),x=>Assert.Equal(MaterialSideStatus.Resolved,x.MaterialSide.Status));
        Assert.Equal(0,fixture.Composer.JudgmentCallCount);
    }

    [Theory]
    [InlineData(0d,0d,0d)]
    [InlineData(0d,.37d,0d)]
    [InlineData(.23d,.41d,.17d)]
    public void WholeClosedSolid_ExactPlanarCompositionPreservesVolumeAndArea(double rx,double ry,double rz)
    {
        var fixture=Fixture(Transform3D.CreateRotationX(rx)*Transform3D.CreateRotationY(ry)*Transform3D.CreateRotationZ(rz)*Transform3D.CreateTranslation(new(.031d,-.027d,.019d)));
        var grid=Run(fixture,16);
        Assert.InRange(double.Abs(grid.Volume-fixture.Region.ExactVolume),0d,5e-7d);
        Assert.InRange(double.Abs(grid.Area-fixture.Region.ExactBoundaryArea),0d,5e-7d);
        Assert.True(grid.Cut>0); Assert.True(grid.Single>0); Assert.True(grid.Edge>0); Assert.True(grid.Corner>0);
        Assert.Equal(0,grid.Judgments);
    }

    [Fact]
    public void SameSenseAndStoredFaceNormalAreNotMaterialSideAuthority()
    {
        var original=BrepPrimitives.CreateBox(2d,1.5d,1d).Value!; var bindings=new BrepBindingModel();
        foreach(var edge in original.Bindings.EdgeBindings) bindings.AddEdgeBinding(edge);
        foreach(var face in original.Bindings.FaceBindings) bindings.AddFaceBinding(face with { SameSense=!face.SameSense });
        var reversed=new BrepBody(original.Topology,original.Geometry,bindings,
            vertexPoints:null,
            original.SafeBooleanComposition,original.ShellRepresentation);
        var fixture=Fixture(Transform3D.CreateRotationY(.27d),reversed);
        var result=Run(fixture,12);

        Assert.InRange(double.Abs(result.Volume-fixture.Region.ExactVolume),0d,5e-7d);
        Assert.All(fixture.Shell.Faces.Select(f=>fixture.Composer.Compose(new(0,0,0),Around(Average(f.OuterTrimVertices),1e-3))).SelectMany(c=>c.Contributors),
            c=>Assert.Contains("CIR probes",c.MaterialSide.Basis,StringComparison.Ordinal));
    }

    [Fact]
    public void GeneralMultiFaceAmbiguityUsesDeterministicJudgmentTrace()
    {
        var fixture=Fixture(Transform3D.Identity); var bounds=Expand(fixture.Region.Bounds,.1d);
        var first=fixture.Composer.Compose(new(1,2,3),bounds); var second=fixture.Composer.Compose(new(1,2,3),bounds);
        Assert.Equal(CutCellCompositionKind.MultiFaceTrimJunction,first.CompositionKind);
        Assert.NotNull(first.Judgment); Assert.Equal(first.Judgment!.SelectedComposition,second.Judgment!.SelectedComposition);
        Assert.Equal(first.Judgment.Evidence,second.Judgment.Evidence); Assert.Equal(2,fixture.Composer.JudgmentCallCount);
    }

    private static RunResult Run(FixtureData f,int n)
    {
        var lattice=new LatticeSpec(Expand(f.Region.Bounds,.173d),n,n,n); var cellVolume=lattice.CellSize.X*lattice.CellSize.Y*lattice.CellSize.Z;
        var volume=0d;var area=0d;var cut=0;var single=0;var edge=0;var corner=0;
        foreach(var index in lattice.Indices())
        {
            var bounds=lattice.CellBounds(index);var classification=ContinuumGridClassifier.ClassifyCell(f.Region,bounds);
            if(classification==CellClassification.Inside){volume+=cellVolume;continue;} if(classification==CellClassification.Outside)continue;
            var set=f.Composer.Compose(index,bounds);cut++;volume+=set.Integration.OccupancyFraction*cellVolume;area+=set.Integration.BoundaryArea;
            if(set.CompositionKind==CutCellCompositionKind.SingleFace)single++;else if(set.CompositionKind==CutCellCompositionKind.TwoFaceEdge)edge++;else if(set.CompositionKind==CutCellCompositionKind.ThreeFaceCorner)corner++;
        }
        return new(volume,area,cut,single,edge,corner,f.Composer.JudgmentCallCount);
    }

    private static FixtureData Fixture(Transform3D transform,BrepBody? body=null)
    {
        body??=BrepPrimitives.CreateBox(2d,1.5d,1d).Value!;var region=new ExactBrepBoxContinuumRegion(new("m4-box"),2d,1.5d,1d,transform);
        var outer=body.ShellRepresentation!.OuterShellId;var association=new CirBrepAssociation(region.Id,"box-body",outer.Value.ToString(),"m4-fixture");
        var semantics=body.Topology.Faces.ToDictionary(f=>f.Id,f=>$"DatumFace:{f.Id.Value}");var shell=new WholeShellBoundaryQuery(body,association,transform,semantics);
        return new(body,region,shell,new WholePartCutCellComposer(region,shell));
    }
    private static BoundingBox3D Around(Point3D p,double r)=>new(p-new Vector3D(r,r,r),p+new Vector3D(r,r,r));
    private static BoundingBox3D Expand(BoundingBox3D b,double r)=>new(b.Min-new Vector3D(r,r,r),b.Max+new Vector3D(r,r,r));
    private static Point3D Average(IReadOnlyList<Point3D> p)=>new(p.Average(x=>x.X),p.Average(x=>x.Y),p.Average(x=>x.Z));
    private static Point3D Mid(Point3D a,Point3D b)=>new((a.X+b.X)*.5,(a.Y+b.Y)*.5,(a.Z+b.Z)*.5);
    private sealed record FixtureData(BrepBody Body,ExactBrepBoxContinuumRegion Region,WholeShellBoundaryQuery Shell,WholePartCutCellComposer Composer);
    private sealed record RunResult(double Volume,double Area,int Cut,int Single,int Edge,int Corner,int Judgments);
}
