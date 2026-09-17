using Aetheris.Humanoid;
using Xunit;

namespace Aetheris.Humanoid.Tests;

public sealed class HumanoidRestX2Tests
{
    [Fact]
    public void CanonicalCorpusRoundtripsFromIndependentSourceRest()
    {
        var source = CanonicalAdultTemplate.Create();
        var normalized = HumanoidRestPoseNormalizer.Normalize(source);
        var cases = new[]
        {
            new AnatomicalJointRequest(HumanoidJointKind.LeftShoulder, AbductionDegrees: 90),
            new AnatomicalJointRequest(HumanoidJointKind.LeftElbow, 90),
            new AnatomicalJointRequest(HumanoidJointKind.LeftHip, 70),
            new AnatomicalJointRequest(HumanoidJointKind.LeftHip, AbductionDegrees: 45),
            new AnatomicalJointRequest(HumanoidJointKind.LeftKnee, 90)
        };
        foreach (var request in cases)
        {
            var solved = HumanoidKinematicSolver.Solve(normalized.Skeleton,
                new("roundtrip", normalized.Skeleton.SkeletonId, normalized.Skeleton.RestPoseId, 0, [request]));
            Assert.NotNull(solved.Pose);
            Assert.All(solved.Pose!.SemanticResiduals,
                residual => Assert.InRange(residual.MaximumAbsoluteDegrees, 0, .05));
        }
    }

    [Fact]
    public void Height1800RetainsRestPoseMeasurementsAndAttachments()
    {
        var normalized = HumanoidRestPoseNormalizer.Normalize(CanonicalAdultTemplate.Create());
        var shaped = HumanoidMorphing.Apply(normalized,
            new Dictionary<HumanoidMorphId, double> { [HumanoidMorphId.Height] = 1800 });
        Assert.Equal(CanonicalHumanoidRestPose.Id, shaped.Skeleton.RestPoseId);
        Assert.Equal(normalized.Surface.ConnectivityHash, shaped.Surface.ConnectivityHash);
        var height = HumanoidMeasurements.Evaluate(shaped).Single(x => x.Measurement == HumanoidMeasurementId.Height);
        Assert.True(height.IsValid);
        Assert.Equal(1800, height.ValueMm!.Value, 6);
        var solve = HumanoidKinematicSolver.Solve(shaped.Skeleton,
            new("height1800-shoulder90", shaped.Skeleton.SkeletonId, shaped.Skeleton.RestPoseId,
                shaped.ShapeRevision, [new(HumanoidJointKind.LeftShoulder, AbductionDegrees: 90)]), shaped.ShapeRevision);
        Assert.True(solve.IsSolved, string.Join("; ", solve.Diagnostics.Select(x => x.Code + ": " + x.Message)));
        var pose = solve.Pose!;
        Assert.InRange(pose.SemanticResiduals.Max(x => x.MaximumAbsoluteDegrees), 0, .05);
        foreach (var site in new[] { "site:Wrist.Left", "site:UpperArm.Left", "site:Chest", "site:Waist", "site:Thigh.Left", "site:Foot.Left" })
        {
            var frame = HumanoidPosing.EvaluateAttachment(shaped, site, pose.PoseState);
            Assert.True(double.IsFinite(frame.Position.X + frame.Position.Y + frame.Position.Z));
        }
    }
}
