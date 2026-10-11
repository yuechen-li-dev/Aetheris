using Aetheris.Humanoid;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Garment;
using Aetheris.Kernel.Firmament.Humanoid;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class HumanoidPoseAuthoringTests
{
    [Fact]
    public void ForwardInheritanceUsesAbsoluteAnglesAndPreservesOriginalBind()
    {
        var document = HumanoidPoseAuthoring.Parse("""
            Pose wearing { figure: Anatolia; from: fitting; Joint LeftShoulder { abduction: 35deg; } }
            Pose fitting { figure: Anatolia; from: TPose; Joint LeftElbow { flexion: 10deg + 5deg; } }
            PoseTransition lowerArms { from: fitting; to: wearing; duration: 0.5s; }
            schema Garment
            """);
        Assert.Contains("schema Garment", document.RemainingSource);
        Assert.DoesNotContain("Pose fitting", document.RemainingSource);
        Assert.Equal(.5, document.Transitions["lowerArms"].DurationSeconds);
        Assert.Equal(15, document.Poses["wearing"].Joints.Single(joint => joint.Joint == HumanoidJointKind.LeftElbow).FlexionDegrees);

        var body = Body();
        string restPoseId = body.Skeleton.RestPoseId;
        var binds = body.Skeleton.Joints.Select(joint => joint.GlobalBind).ToArray();
        var fitting = HumanoidPoseAuthoring.Solve(document.Poses["fitting"], body);
        var wearing = HumanoidPoseAuthoring.Solve(document.Poses["wearing"], body);
        Assert.Equal(restPoseId, body.Skeleton.RestPoseId);
        Assert.Equal(binds, body.Skeleton.Joints.Select(joint => joint.GlobalBind));
        Assert.InRange(Math.Abs(HumanoidPoseSemantics.Measure(body.Skeleton, fitting.GlobalTransforms,
            HumanoidJointKind.LeftShoulder).AbductionDegrees - 90), 0, .01);
        Assert.InRange(Math.Abs(HumanoidPoseSemantics.Measure(body.Skeleton, wearing.GlobalTransforms,
            HumanoidJointKind.LeftShoulder).AbductionDegrees - 35), 0, .01);
        Assert.NotEqual(body.EvaluatePositions(fitting).ToArray(), body.EvaluatePositions(wearing).ToArray());
    }

    [Fact]
    public void PartialRestOverrideKeepsOmittedAnatomicalAngles()
    {
        var body = Body();
        var document = HumanoidPoseAuthoring.Parse("""
            Pose bent { figure: Anatolia; from: Rest; Joint LeftShoulder { flexion: 5deg; } }
            Pose inherited { figure: Anatolia; from: bent; Joint LeftShoulder { twist: 2deg; } }
            """);
        var actualRest = HumanoidPoseSemantics.Measure(body.Skeleton,
            body.Skeleton.Joints.Select(joint => joint.GlobalBind).ToArray(), HumanoidJointKind.LeftShoulder);
        var request = Assert.Single(HumanoidPoseAuthoring.Requests(document.Poses["inherited"], body));
        Assert.Equal(actualRest.AbductionDegrees, request.AbductionDegrees);
        Assert.Equal(5, request.FlexionDegrees);
        Assert.Equal(2, request.TwistDegrees);
        Assert.NotEqual(0, request.AbductionDegrees);
    }

    [Theory]
    [InlineData("Pose a { figure: F; from: b; } Pose b { figure: F; from: a; }", "humanoid-pose-cycle")]
    [InlineData("Pose a { figure: F; from: missing; }", "humanoid-pose-reference")]
    [InlineData("Pose a { figure: F; } Pose b { figure: G; from: a; }", "humanoid-pose-figure-mismatch")]
    [InlineData("Pose a { figure: F; Joint LeftElbow { flexion: 12mm; } }", "humanoid-pose-angle")]
    [InlineData("Pose a { figure: F; Joint Unknown { flexion: 5deg; } }", "humanoid-pose-joint")]
    [InlineData("Pose a { figure: F; } PoseTransition t { from: a; to: a; duration: 0s; }", "humanoid-pose-duration")]
    [InlineData("Pose a { figure: F; } PoseTransition t { from: a; to: missing; duration: 1s; }", "humanoid-pose-transition-binding")]
    public void InvalidPoseProgramsHaveOwnedDiagnostics(string source, string code)
    {
        var error = Assert.Throws<InvalidDataException>(() => HumanoidPoseAuthoring.Parse(source));
        Assert.StartsWith(code + ":", error.Message);
    }

    [Fact]
    public void JointLimitsAreEnforcedByTheExistingSolver()
    {
        var document = HumanoidPoseAuthoring.Parse("Pose a { figure: F; from: APose; Joint LeftElbow { flexion: 999deg; } }");
        var error = Assert.Throws<InvalidDataException>(() => HumanoidPoseAuthoring.Solve(document.Poses["a"], Body()));
        Assert.StartsWith("humanoid-pose-solve:", error.Message);
    }

    [Fact]
    public void PoseFieldsUseTheSharedLanguageServiceAndAngleSchema()
    {
        string source = "Pose fitting { figure: Anatolia; Joint LeftElbow {\n ";
        var completion = FirmamentLanguageService.Complete(source, "pose.firmament", "1", source.Length);
        Assert.Equal("Joint", completion.Context);
        Assert.Contains(completion.Fields, field => field.Name == "Flexion");
        Assert.Equal(FirmamentUnitKind.Angle, FirmamentSemanticSchemas.Get("Joint")!.Fields[0].Unit);
    }

    internal static HumanoidGameplayBody Body()
    {
        var canonical = HumanoidRestPoseNormalizer.Normalize(CanonicalAdultTemplate.Create());
        return new(new(HumanoidGameplayBody.Schema, "pose-test", canonical.Surface,
            canonical.Skeleton, [], canonical.Provenance));
    }
}
