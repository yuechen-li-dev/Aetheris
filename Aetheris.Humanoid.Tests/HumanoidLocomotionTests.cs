using System.Numerics;
using Xunit;

namespace Aetheris.Humanoid.Tests;

public sealed class HumanoidLocomotionTests
{
    [Fact]
    public void AuthoredAdmissionPreservesLinksAndRejectsInvalidFrames()
    {
        var normalized = HumanoidRestPoseNormalizer.Normalize(CanonicalAdultTemplate.Create());
        var skeleton = normalized.Skeleton;
        var request = new AuthoredHumanoidPose("idle", skeleton.SkeletonId, skeleton.RestPoseId, [], new(0, 0, -25));
        var solved = HumanoidKinematicSolver.SolveAuthored(skeleton, request);
        Assert.True(solved.IsSolved);
        Assert.All(solved.Pose!.Residuals, residual => Assert.InRange(residual.CenterMm, 0, .001));
        Assert.False(HumanoidKinematicSolver.SolveAuthored(skeleton, request with { RootOffsetMm = new(0, 0, 251) }).IsSolved);
        Assert.False(HumanoidKinematicSolver.SolveAuthored(skeleton, request with
        {
            Rotations = [new(HumanoidJointKind.Neck, Quaternion.CreateFromAxisAngle(Vector3.UnitX, 2))],
        }).IsSolved);
        Assert.False(HumanoidKinematicSolver.SolveAuthored(skeleton, request with
        {
            Rotations = [new(HumanoidJointKind.LeftHip, new(0, 0, 0, 2))],
        }).IsSolved);
    }

    [Theory]
    [InlineData(AnatomicalSide.Left)]
    [InlineData(AnatomicalSide.Right)]
    public void TwoBoneSolveReachesAnkleAndPreservesTheRigidSkeleton(AnatomicalSide side)
    {
        var normalized = HumanoidRestPoseNormalizer.Normalize(CanonicalAdultTemplate.Create());
        var skeleton = normalized.Skeleton;
        var source = HumanoidKinematicSolver.SolveAuthored(skeleton,
            new("rest", skeleton.SkeletonId, skeleton.RestPoseId, [], Vector3.Zero)).Pose!;
        int index = skeleton.Joints.ToList().FindIndex(joint => joint.Kind.ToString() == side + "Ankle");
        Vector3 target = source.GlobalTransforms[index].Translation + new Vector3(0, 40, 80);
        var result = HumanoidKinematicSolver.SolveLeg(skeleton, source, new(side, target, Vector3.UnitZ));
        Assert.InRange(result.ResidualMm, 0, .01f);
        Assert.False(result.ReachClamped);
        Assert.All(result.Pose.Residuals, residual =>
        {
            Assert.InRange(residual.CenterMm, 0, .001);
            Assert.InRange(residual.LinkLengthMm, 0, .001);
        });
        var binding = new HumanoidSkinningBinding(normalized.Surface, skeleton);
        Assert.Equal(skeleton.Joints.Count, binding.CreatePalette(result.Pose).Count);
    }

    [Fact]
    public void UnreachableTargetIsReportedWithoutStretchingTheLeg()
    {
        var normalized = HumanoidRestPoseNormalizer.Normalize(CanonicalAdultTemplate.Create());
        var skeleton = normalized.Skeleton;
        var source = HumanoidKinematicSolver.SolveAuthored(skeleton,
            new("rest", skeleton.SkeletonId, skeleton.RestPoseId, [], Vector3.Zero)).Pose!;
        var result = HumanoidKinematicSolver.SolveLeg(skeleton, source,
            new(AnatomicalSide.Left, new(-100, 0, -2000), Vector3.UnitZ));
        Assert.True(result.ReachClamped);
        Assert.True(result.ResidualMm > 500);
        Assert.All(result.Pose.Residuals, residual => Assert.InRange(residual.LinkLengthMm, 0, .001));
    }
}
