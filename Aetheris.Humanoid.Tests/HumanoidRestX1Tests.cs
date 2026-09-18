using System.Numerics;
using Aetheris.Humanoid;
using Aetheris.Kernel.Core.Math;
using Xunit;

namespace Aetheris.Humanoid.Tests;

public sealed class HumanoidRestX1Tests
{
    [Fact]
    public void TAndAPoseSourcesReachSameAbsoluteShoulderState()
    {
        var tPose = ArmSkeleton(90);
        var aPose = ArmSkeleton(CanonicalHumanoidRestPose.ShoulderAbductionDegrees);
        var requestT = new RequestedHumanoidPose("shoulder90", tPose.SkeletonId, tPose.RestPoseId, 0,
            [new(HumanoidJointKind.LeftShoulder, AbductionDegrees: 90)]);
        var requestA = requestT with { SkeletonId = aPose.SkeletonId, RestPoseId = aPose.RestPoseId };

        var tSolved = HumanoidKinematicSolver.Solve(tPose, requestT).Pose!;
        var aSolved = HumanoidKinematicSolver.Solve(aPose, requestA).Pose!;
        var tActual = HumanoidPoseSemantics.Measure(tPose, tSolved.GlobalTransforms, HumanoidJointKind.LeftShoulder);
        var aActual = HumanoidPoseSemantics.Measure(aPose, aSolved.GlobalTransforms, HumanoidJointKind.LeftShoulder);

        Assert.InRange(Math.Abs(tActual.AbductionDegrees - 90), 0, .001);
        Assert.InRange(Math.Abs(aActual.AbductionDegrees - 90), 0, .001);
        AssertVectorNear(tSolved.GlobalTransforms[4].Translation, aSolved.GlobalTransforms[4].Translation, .001f);
        Assert.Equal(Quaternion.Identity, tSolved.PoseState.LocalRotations.Single().LocalRotation);
        Assert.NotEqual(Quaternion.Identity, aSolved.PoseState.LocalRotations.Single().LocalRotation);
    }

    [Fact]
    public void CanonicalNormalizationPreservesIdentityRebuildsBindsAttachmentsAndMorphs()
    {
        var source = CanonicalAdultTemplate.Create();
        var normalized = HumanoidRestPoseNormalizer.Normalize(source);

        Assert.Equal(CanonicalHumanoidRestPose.Id, normalized.Skeleton.RestPoseId);
        Assert.Equal(source.Surface.TopologyId, normalized.Surface.TopologyId);
        Assert.Equal(source.Surface.ConnectivityHash, normalized.Surface.ConnectivityHash);
        Assert.Equal(source.Surface.Vertices.Count, normalized.Surface.Vertices.Count);
        Assert.Equal(source.Surface.Faces.Select(face => (face.A, face.B, face.C)),
            normalized.Surface.Faces.Select(face => (face.A, face.B, face.C)));
        var rest = HumanoidPosing.EvaluateVertices(normalized, normalized.PoseState);
        Assert.InRange(rest.Select((point, index) => (point - normalized.Surface.Vertices[index].Position).Length).Max(), 0, .001);
        Assert.All(CanonicalHumanoidRestPose.Requests, request =>
        {
            var actual = HumanoidPoseSemantics.Measure(normalized.Skeleton,
                normalized.Skeleton.Joints.Select(joint => joint.GlobalBind).ToArray(), request.Joint);
            Assert.InRange(Math.Abs(actual.FlexionDegrees - request.FlexionDegrees), 0, .02);
            Assert.InRange(Math.Abs(actual.AbductionDegrees - request.AbductionDegrees), 0, .02);
        });
        Assert.All(normalized.Attachments, attachment =>
            Assert.Equal(Resolve(normalized.Surface, attachment.PositionBinding), attachment.NeutralFrame.Position));
        foreach (var morph in new[] { HumanoidMorphId.Height, HumanoidMorphId.ArmLength,
                     HumanoidMorphId.LegLength, HumanoidMorphId.ShoulderWidth })
        {
            var channel = normalized.MorphChannels.Single(item => item.Id == morph);
            var value = channel.Default == channel.Maximum ? channel.Minimum : channel.Maximum;
            var shaped = HumanoidMorphing.Apply(normalized, new Dictionary<HumanoidMorphId, double> { [morph] = value });
            Assert.Equal(normalized.Surface.ConnectivityHash, shaped.Surface.ConnectivityHash);
            var validation = HumanoidValidator.Validate(shaped);
            Assert.True(validation.IsValid, morph + ": " + string.Join("; ", validation.Diagnostics.Select(x => x.Code + "[" + x.EntityId + "]: " + x.Message)));
        }
    }

    [Fact]
    public void AnatomicalLeftRightIsIndependentOfCameraView()
    {
        var skeleton = MirroredArmSkeleton();
        var solved = HumanoidKinematicSolver.Solve(skeleton, new("mirrored", skeleton.SkeletonId,
            skeleton.RestPoseId, 0,
            [new(HumanoidJointKind.LeftShoulder, AbductionDegrees: 60),
             new(HumanoidJointKind.RightShoulder, AbductionDegrees: 60)])).Pose!;
        var left = solved.GlobalTransforms[skeleton.Joints.ToList().FindIndex(j => j.Kind == HumanoidJointKind.LeftElbow)].Translation;
        var right = solved.GlobalTransforms[skeleton.Joints.ToList().FindIndex(j => j.Kind == HumanoidJointKind.RightElbow)].Translation;
        Assert.InRange(Math.Abs(left.X + right.X), 0, .001);
        Assert.InRange(Math.Abs(left.Y - right.Y), 0, .001);
        Assert.InRange(Math.Abs(left.Z - right.Z), 0, .001);
    }

    [Fact]
    public void AbsoluteFlexionMeasurementDoesNotFoldPastNinetyDegrees()
    {
        var skeleton = LegSkeleton();
        var solved = HumanoidKinematicSolver.Solve(skeleton, new("hip120", skeleton.SkeletonId,
            skeleton.RestPoseId, 0, [new(HumanoidJointKind.LeftHip, FlexionDegrees: 120)])).Pose!;
        var actual = HumanoidPoseSemantics.Measure(skeleton, solved.GlobalTransforms, HumanoidJointKind.LeftHip);
        Assert.InRange(Math.Abs(actual.FlexionDegrees - 120), 0, .001);
    }

    private static HumanoidSkeleton ArmSkeleton(double abduction)
    {
        var radians = abduction * Math.PI / 180;
        var shoulder = new Vector3(-200, 0, 1400);
        var elbow = shoulder + new Vector3((float)(-300 * Math.Sin(radians)), 0, (float)(-300 * Math.Cos(radians)));
        var wrist = elbow + Vector3.Normalize(elbow - shoulder) * 250;
        return Build("source-" + abduction, [(HumanoidJointKind.Root, (int?)null, Vector3.Zero),
            (HumanoidJointKind.Pelvis, 0, new(0,0,1000)), (HumanoidJointKind.LeftClavicle, 1, new(-150,0,1400)),
            (HumanoidJointKind.LeftShoulder, 2, shoulder), (HumanoidJointKind.LeftElbow, 3, elbow),
            (HumanoidJointKind.LeftWrist, 4, wrist)]);
    }

    private static HumanoidSkeleton MirroredArmSkeleton() => Build("mirrored",
    [
        (HumanoidJointKind.Root, (int?)null, Vector3.Zero), (HumanoidJointKind.Pelvis, 0, new(0,0,1000)),
        (HumanoidJointKind.LeftClavicle, 1, new(-150,0,1400)), (HumanoidJointKind.LeftShoulder, 2, new(-200,0,1400)),
        (HumanoidJointKind.LeftElbow, 3, new(-200,0,1100)), (HumanoidJointKind.LeftWrist, 4, new(-200,0,850)),
        (HumanoidJointKind.RightClavicle, 1, new(150,0,1400)), (HumanoidJointKind.RightShoulder, 6, new(200,0,1400)),
        (HumanoidJointKind.RightElbow, 7, new(200,0,1100)), (HumanoidJointKind.RightWrist, 8, new(200,0,850))
    ]);

    private static HumanoidSkeleton LegSkeleton() => Build("leg",
    [
        (HumanoidJointKind.Root, (int?)null, Vector3.Zero), (HumanoidJointKind.Pelvis, 0, new(0,0,1000)),
        (HumanoidJointKind.LeftHip, 1, new(-100,0,1000)), (HumanoidJointKind.LeftKnee, 2, new(-100,0,600)),
        (HumanoidJointKind.LeftAnkle, 3, new(-100,0,200))
    ]);

    private static HumanoidSkeleton Build(string id, (HumanoidJointKind Kind, int? Parent, Vector3 Position)[] specs)
    {
        var joints = specs.Select(spec =>
        {
            var translation = spec.Position - (spec.Parent is int parent ? specs[parent].Position : Vector3.Zero);
            var global = Matrix4x4.CreateTranslation(spec.Position);
            Matrix4x4.Invert(global, out var inverse);
            var side = spec.Kind.ToString().StartsWith("Left") ? AnatomicalSide.Left :
                spec.Kind.ToString().StartsWith("Right") ? AnatomicalSide.Right : AnatomicalSide.Center;
            return new HumanoidJoint("joint:" + spec.Kind, spec.Kind, side, spec.Parent,
                HumanoidTransform.FromTranslation(new(translation.X, translation.Y, translation.Z)), global, inverse);
        }).ToArray();
        return new(id, id + ".rest", "row-vector LBS", joints,
            joints.ToDictionary(joint => joint.Kind, joint => joint.Kind));
    }

    private static void AssertVectorNear(Vector3 actual, Vector3 expected, float tolerance)
        => Assert.InRange(Vector3.Distance(actual, expected), 0, tolerance);

    private static Point3D Resolve(HumanoidSurface surface, SurfaceBinding binding)
    {
        var face = surface.Faces.Single(item => item.Id == binding.FaceId);
        var a = surface.Vertices[face.A].Position;
        var b = surface.Vertices[face.B].Position;
        var c = surface.Vertices[face.C].Position;
        return new(a.X * binding.BarycentricA + b.X * binding.BarycentricB + c.X * binding.BarycentricC,
            a.Y * binding.BarycentricA + b.Y * binding.BarycentricB + c.Y * binding.BarycentricC,
            a.Z * binding.BarycentricA + b.Z * binding.BarycentricB + c.Z * binding.BarycentricC);
    }
}
