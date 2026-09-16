using System.Numerics;
using Aetheris.Humanoid;
using Aetheris.Kernel.Core.Judgment;
using Xunit;

namespace Aetheris.Humanoid.Tests;

public sealed class HumanoidX3Tests
{
    private static HumanoidDeformationContext Context(double degrees = 45)
    {
        var root = Matrix4x4.Identity;
        var hip = Matrix4x4.CreateTranslation(-10,0,100);
        Matrix4x4.Invert(hip,out var inverse);
        var skeleton = new HumanoidSkeleton("patch", "rest", "row-vector",
            [new("root",HumanoidJointKind.Root,AnatomicalSide.Center,null,HumanoidTransform.Identity,root,root),
             new("pelvis",HumanoidJointKind.Pelvis,AnatomicalSide.Center,0,HumanoidTransform.Identity,root,root),
             new("hip",HumanoidJointKind.LeftHip,AnatomicalSide.Left,1,HumanoidTransform.FromTranslation(new(-10,0,100)),hip,inverse)],
            new Dictionary<HumanoidJointKind,HumanoidJointKind>());
        var surface = new HumanoidSurface("patch", "triangles", "fixed",
            [new("a",new(-10,0,90),HumanoidRegionKind.LeftThigh,0),new("b",new(-8,0,90),HumanoidRegionKind.LeftThigh,1),new("c",new(-10,2,90),HumanoidRegionKind.LeftThigh,2),new("d",new(-12,0,90),HumanoidRegionKind.Chest,3)],
            [new("inside",0,1,2,HumanoidRegionKind.LeftThigh,null),new("outside",0,2,3,HumanoidRegionKind.Chest,null)], [], null,
            [new(0,[new(1,.5),new(2,.5)]),new(1,[new(1,.5),new(2,.5)]),new(2,[new(1,.5),new(2,.5)]),new(3,[new(1,.5),new(2,.5)])],[],new Dictionary<HumanoidRegionKind,HumanoidRegionKind>());
        var solved = HumanoidKinematicSolver.Solve(skeleton,new("pose","patch","rest",0,[new(HumanoidJointKind.LeftHip,degrees)])).Pose!;
        return HumanoidDeformationContext.Create(surface,skeleton,0,solved,HumanoidJointKind.LeftHip);
    }
    [Fact]
    public void DualQuaternionBlendMatchesAnalyticHalfRotationAboutFixedHipCenter()
    {
        var context=Context(90);
        var candidate=HumanoidDeformationCandidates.Generate(context,new("analytic",TransitionBlendMethod.DualQuaternion,1));
        var index=context.Vertices.ToList().FindIndex(v=>v.SurfaceIndex==1);
        var expected=new Aetheris.Kernel.Core.Math.Point3D(-8,Math.Sqrt(50),100-Math.Sqrt(50));
        Assert.InRange((candidate.PatchPositions[index]-expected).Length,0,.0001);
    }
    [Fact]
    public void LocalGeneratorsPreserveBoundaryAndNeverChangeTopology()
    {
        var context = Context();
        Assert.Equal(2,context.Vertices.Count(v => v.Boundary));
        foreach(var spec in HumanoidDeformationCandidates.Default)
        {
            var c = HumanoidDeformationCandidates.Generate(context,spec);
            var assembled = context.AssembleDiagnostic(c);
            Assert.Equal(context.Baseline[3],assembled[3]);
            Assert.All(context.Vertices.Where(v=>v.Boundary),v=>Assert.Equal(context.Baseline[v.SurfaceIndex],assembled[v.SurfaceIndex]));
            var metrics = HumanoidDeformationJudgment.Measure(context,c);
            Assert.Equal(0,metrics.BoundaryMaximumMm); Assert.Equal(0,metrics.FixedAttachmentMaximumMm);
        }
    }
    [Fact]
    public void CompressionCollapseBoundaryAndNonfiniteCandidatesCannotWin()
    {
        var context = Context();
        var baseline = HumanoidDeformationCandidates.Generate(context,HumanoidDeformationCandidates.Default[0]);
        var points = baseline.PatchPositions.ToArray(); points[1] = points[0];
        var c = baseline with {Id="collapsed",PatchPositions=points};
        var m = new MeasuredDeformationCandidate(c,HumanoidDeformationJudgment.Measure(context,c));
        var result=HumanoidDeformationJudgment.Model(DeformationPolicy.HipV1).Evaluate(context,[new(c.Id,m)]);
        Assert.Null(result.Winner);
        Assert.Contains(result.Candidates[0].Rules,r=>r.Id=="noncollapsed"&&!r.Result.Passed);
        points = baseline.PatchPositions.ToArray(); points[0] = points[0] + new Aetheris.Kernel.Core.Math.Vector3D(1,0,0);
        c = baseline with{Id="broken-boundary",PatchPositions=points};
        var metrics=HumanoidDeformationJudgment.Measure(context,c);
        Assert.True(metrics.BoundaryMaximumMm>.9);
        points[0]=new(double.NaN,0,0);
        Assert.False(HumanoidDeformationJudgment.Measure(context,c).Finite);
    }
    [Fact]
    public void EmptyCandidateSetDoesNotReturnBaselineMesh()
    {
        var result=HumanoidDeformationJudgment.Evaluate(Context(),DeformationPolicy.HipV1,[]);
        Assert.False(result.IsSuccess); Assert.Null(result.Positions); Assert.Null(result.WinnerId);
        Assert.Contains(result.Diagnostics,d=>d.Code=="HUM300");
    }
    [Fact]
    public void OptionalUtilityTermDoesNotModifyGenerators()
    {
        var context=Context(0);
        var result=HumanoidDeformationJudgment.Evaluate(context,DeformationPolicy.HipV1,
            extraTerms:[new("attachment-drift",.2,(c,_)=>new(1/(1+c.Metrics.FixedAttachmentMaximumMm/.001),c.Metrics.FixedAttachmentMaximumMm,"mm","Bounded attachment residual utility."))]);
        Assert.True(result.IsSuccess);
        Assert.Contains(result.Trace.Single(t=>t.Id==result.WinnerId).Scores,s=>s.Id=="attachment-drift");
    }
    [Fact]
    public void SelectionAndGeometryAreIndependentOfGeneratorEnumeration()
    {
        var context=Context(0);
        var first=HumanoidDeformationJudgment.Evaluate(context,DeformationPolicy.HipV1);
        var second=HumanoidDeformationJudgment.Evaluate(context,DeformationPolicy.HipV1,HumanoidDeformationCandidates.Default.Reverse().ToArray());
        Assert.Equal(first.WinnerId,second.WinnerId); Assert.Equal(first.Positions,second.Positions);
    }
    [Fact]
    public void PolicyCannotAdmitAStalePoseContext()
    {
        var context=Context();
        Assert.Throws<InvalidOperationException>(()=>HumanoidDeformationContext.Create(context.Surface,context.Skeleton,1,context.Solved,context.Joint));
    }
}
