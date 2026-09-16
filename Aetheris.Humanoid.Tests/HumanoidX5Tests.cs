using System.Numerics;
using Aetheris.Humanoid;
using Xunit;

namespace Aetheris.Humanoid.Tests;

public sealed class HumanoidX5Tests
{
    private static string Fixture()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "fixtures", "Canonical", "Humanoid", "antonia-reference-skeleton-v1.json");
            if (File.Exists(path)) return path;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Could not locate the checked-in Antonia reference skeleton fixture.");
    }

    [Fact]
    public void CheckedReferenceMapsExactlyTheExisting55JointDomain()
    {
        var artifact = AntoniaReferenceRigAdapter.Load(Fixture());
        var skeleton = AntoniaReferenceRigAdapter.BuildSkeleton(artifact, "test-hash");

        Assert.Equal(182, artifact.SourceJoints.Count);
        Assert.Equal(55, skeleton.Joints.Count);
        Assert.Equal(55, skeleton.Joints.Select(joint => joint.Kind).Distinct().Count());
        Assert.Equal("thigh.L", skeleton.Reference!.JointMappings.Single(mapping => mapping.CanonicalJoint == HumanoidJointKind.LeftHip).SourceJointId);
        Assert.Equal("upper_arm.L", skeleton.Reference.JointMappings.Single(mapping => mapping.CanonicalJoint == HumanoidJointKind.LeftShoulder).SourceJointId);
        Assert.DoesNotContain(skeleton.Joints, joint => joint.Kind == HumanoidJointKind.Jaw);
    }

    [Fact]
    public void RuntimeRestReconstructsEveryImportedGlobalFrame()
    {
        var artifact = AntoniaReferenceRigAdapter.Load(Fixture());
        var skeleton = AntoniaReferenceRigAdapter.BuildSkeleton(artifact, "test-hash");
        var globals = HumanoidPosing.GlobalPose(skeleton, new(artifact.RestPoseId, [], HumanoidTransform.Identity));

        foreach (var mapping in artifact.CanonicalJoints)
        {
            var index = skeleton.Joints.ToList().FindIndex(joint => joint.Kind == mapping.CanonicalJoint);
            Assert.True(index >= 0);
            AssertMatrixNear(mapping.GlobalRestCanonical, globals[index], .001f);
            var identity = skeleton.Joints[index].InverseBind * globals[index];
            AssertMatrixNear(Matrix4x4.Identity, identity, .001f);
        }
    }

    [Theory]
    [InlineData(HumanoidJointKind.LeftHip, 70, 0)]
    [InlineData(HumanoidJointKind.LeftHip, 0, 45)]
    [InlineData(HumanoidJointKind.LeftKnee, 90, 0)]
    [InlineData(HumanoidJointKind.LeftShoulder, 0, 90)]
    [InlineData(HumanoidJointKind.LeftElbow, 120, 0)]
    public void ReferenceFramesSolveRepresentativePoseCorpus(HumanoidJointKind joint, double flexion, double abduction)
    {
        var artifact = AntoniaReferenceRigAdapter.Load(Fixture());
        var skeleton = AntoniaReferenceRigAdapter.BuildSkeleton(artifact, "test-hash");
        var request = new RequestedHumanoidPose("x5-test", skeleton.SkeletonId, skeleton.RestPoseId, 0,
            [new AnatomicalJointRequest(joint, flexion, abduction)]);

        var result = HumanoidKinematicSolver.Solve(skeleton, request);

        Assert.True(result.IsSolved, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.All(result.Pose!.Residuals, residual =>
        {
            Assert.InRange(residual.CenterMm, 0, .001);
            Assert.InRange(residual.LinkLengthMm, 0, .001);
        });
    }

    [Fact]
    public void GenericAdapterDoesNotDependOnSourceJointNames()
    {
        var artifact = AntoniaReferenceRigAdapter.Load(Fixture());
        string Rename(string value) => "foreign::" + value.Replace('.', '_');
        var renamed = artifact with
        {
            SourceRigType = "DifferentlyNamedTestRig",
            SourceJoints = artifact.SourceJoints.Select(source => source with
            {
                SourceJointId = Rename(source.SourceJointId),
                ParentSourceJointId = source.ParentSourceJointId is null ? null : Rename(source.ParentSourceJointId)
            }).ToArray(),
            CanonicalJoints = artifact.CanonicalJoints.Select(mapping => mapping with
            {
                SourceJointId = mapping.SourceJointId is null ? null : Rename(mapping.SourceJointId),
                CollapsedSourceHelpers = mapping.CollapsedSourceHelpers.Select(Rename).ToArray()
            }).ToArray()
        };

        var skeleton = CanonicalReferenceRigAdapter.BuildSkeleton(renamed, "foreign-hash", "test.foreign-rig.v1");

        Assert.Equal(55, skeleton.Joints.Count);
        Assert.Equal("test.foreign-rig.v1", skeleton.Reference!.AdapterId);
        Assert.Equal("foreign::thigh_L", skeleton.Reference.JointMappings.Single(mapping => mapping.CanonicalJoint == HumanoidJointKind.LeftHip).SourceJointId);
    }

    [Fact]
    public void ExactCanonicalDomainCannotSubstituteJawForAnAdmittedJoint()
    {
        var artifact = AntoniaReferenceRigAdapter.Load(Fixture());
        var mappings = artifact.CanonicalJoints.ToArray();
        var wrist = Array.FindIndex(mappings, mapping => mapping.CanonicalJoint == HumanoidJointKind.LeftWrist);
        mappings[wrist] = mappings[wrist] with { CanonicalJoint = HumanoidJointKind.Jaw };

        Assert.Throws<InvalidDataException>(() => CanonicalReferenceRigAdapter.Validate(
            artifact with { CanonicalJoints = mappings }, artifact.TopologyId));
    }

    private static void AssertMatrixNear(Matrix4x4 expected, Matrix4x4 actual, float tolerance)
    {
        for (var row = 0; row < 4; row++)
        for (var column = 0; column < 4; column++)
            Assert.InRange(Math.Abs(Value(expected, row, column) - Value(actual, row, column)), 0, tolerance);
    }

    private static float Value(Matrix4x4 value, int row, int column) => (row, column) switch
    {
        (0, 0) => value.M11, (0, 1) => value.M12, (0, 2) => value.M13, (0, 3) => value.M14,
        (1, 0) => value.M21, (1, 1) => value.M22, (1, 2) => value.M23, (1, 3) => value.M24,
        (2, 0) => value.M31, (2, 1) => value.M32, (2, 2) => value.M33, (2, 3) => value.M34,
        (3, 0) => value.M41, (3, 1) => value.M42, (3, 2) => value.M43, (3, 3) => value.M44,
        _ => throw new ArgumentOutOfRangeException()
    };
}
