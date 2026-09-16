using System.Numerics;
using Aetheris.Humanoid;
using Aetheris.Kernel.Core.Math;
using Xunit;

namespace Aetheris.Humanoid.Tests;

public sealed class HumanoidX2Tests
{
    private static HumanoidSkeleton Mechanism()
    {
        var specs = new (HumanoidJointKind Kind, int? Parent, Vector3 Position)[]
        {
            (HumanoidJointKind.Root, null, Vector3.Zero),
            (HumanoidJointKind.Pelvis, 0, new(0, 0, 1000)),
            (HumanoidJointKind.LeftHip, 1, new(-100, 0, 1000)),
            (HumanoidJointKind.LeftKnee, 2, new(-100, 0, 600)),
            (HumanoidJointKind.LeftAnkle, 3, new(-100, 0, 200)),
            (HumanoidJointKind.LeftShoulder, 1, new(-200, 0, 1400)),
            (HumanoidJointKind.LeftElbow, 5, new(-200, 0, 1100)),
            (HumanoidJointKind.LeftWrist, 6, new(-200, 0, 800))
        };
        return new("mechanism", "rest", "row-vector", specs.Select(s =>
        {
            var translation = s.Position - (s.Parent is int p ? specs[p].Position : Vector3.Zero);
            var bind = Matrix4x4.CreateTranslation(s.Position);
            Matrix4x4.Invert(bind, out var inverse);
            return new HumanoidJoint(s.Kind.ToString(), s.Kind, s.Kind.ToString().StartsWith("Left") ? AnatomicalSide.Left : AnatomicalSide.Center,
                s.Parent, HumanoidTransform.FromTranslation(new(translation.X, translation.Y, translation.Z)), bind, inverse);
        }).ToArray(), new Dictionary<HumanoidJointKind, HumanoidJointKind>());
    }
    private static RequestedHumanoidPose Request(params AnatomicalJointRequest[] joints) => new("test", "mechanism", "rest", 0, joints);

    [Fact]
    public void HipAndKnee90HaveAnalyticEndpointAndPreserveEveryLink()
    {
        var result = HumanoidKinematicSolver.Solve(Mechanism(), Request(new AnatomicalJointRequest(HumanoidJointKind.LeftHip, 90), new(HumanoidJointKind.LeftKnee, 90)));
        Assert.True(result.IsSolved);
        var ankle = result.Pose!.GlobalTransforms[4].Translation;
        Assert.InRange(Vector3.Distance(ankle, new(-100, 400, 600)), 0, .001);
        Assert.All(result.Pose.Residuals, r => { Assert.InRange(r.CenterMm, 0, .001); Assert.InRange(r.LinkLengthMm, 0, .001); });
    }
    [Theory]
    [InlineData(HumanoidJointKind.LeftElbow, -45)]
    [InlineData(HumanoidJointKind.LeftKnee, -30)]
    [InlineData(HumanoidJointKind.LeftHip, 160)]
    public void InvalidCoordinatesRejectOrProjectExplicitly(HumanoidJointKind joint, double degrees)
    {
        var request = Request(new AnatomicalJointRequest(joint, degrees));
        var rejected = HumanoidKinematicSolver.Solve(Mechanism(), request);
        Assert.False(rejected.IsSolved);
        Assert.Equal("HUM200", Assert.Single(rejected.Diagnostics).Code);
        var projected = HumanoidKinematicSolver.Solve(Mechanism(), request, policy: AnatomicalSolvePolicy.Project);
        Assert.True(projected.IsSolved);
        Assert.Single(projected.Projections);
        Assert.Equal("HUM205", Assert.Single(projected.Diagnostics).Code);
    }
    [Fact]
    public void HipEllipsePreventsIndependentLimitCornerAndReducesTwistAtBoundary()
    {
        var result = HumanoidKinematicSolver.Solve(Mechanism(), Request(new AnatomicalJointRequest(HumanoidJointKind.LeftHip, 120, 45, 45)), policy: AnatomicalSolvePolicy.Project);
        var solved = Assert.Single(result.Pose!.Joints);
        Assert.InRange(Math.Pow(solved.FlexionDegrees / 120, 2) + Math.Pow(solved.AbductionDegrees / 45, 2), .999999, 1.000001);
        Assert.Equal(22.5, solved.TwistDegrees);
    }
    [Fact]
    public void KneeSecondaryDofsCannotBypassHinge()
    {
        var result = HumanoidKinematicSolver.Solve(Mechanism(), Request(new AnatomicalJointRequest(HumanoidJointKind.LeftKnee, 90, 20, 10)), policy: AnatomicalSolvePolicy.Project);
        Assert.Equal(new(HumanoidJointKind.LeftKnee, 90), Assert.Single(result.Pose!.Joints));
    }
    [Fact]
    public void ElbowHingeBendsForwardInsteadOfAbducting()
    {
        var result = HumanoidKinematicSolver.Solve(Mechanism(), Request(new AnatomicalJointRequest(HumanoidJointKind.LeftElbow, 90)));
        Assert.InRange(Vector3.Distance(result.Pose!.GlobalTransforms[7].Translation, new(-200, 300, 1100)), 0, .001);
    }
    [Fact]
    public void SolveIsDeterministicAndDoesNotChangeRestFrames()
    {
        var skeleton = Mechanism(); var before = skeleton.Joints.ToArray();
        var request = Request(new AnatomicalJointRequest(HumanoidJointKind.LeftHip, 90));
        var a = HumanoidKinematicSolver.Solve(skeleton, request).Pose!;
        var b = HumanoidKinematicSolver.Solve(skeleton, request).Pose!;
        Assert.Equal(a.GlobalTransforms, b.GlobalTransforms);
        Assert.Equal(before, skeleton.Joints);
    }
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void NonfiniteIsRejected(double value) => Assert.False(HumanoidKinematicSolver.Solve(Mechanism(), Request(new AnatomicalJointRequest(HumanoidJointKind.LeftHip, value))).IsSolved);

    [Fact]
    public void ExtremeFiniteRequestProducesFiniteProjectionEvidence()
    {
        var result = HumanoidKinematicSolver.Solve(Mechanism(), Request(new AnatomicalJointRequest(HumanoidJointKind.LeftHip, double.MaxValue, double.MaxValue, double.MaxValue)), policy: AnatomicalSolvePolicy.Project);
        Assert.True(result.IsSolved);
        Assert.True(double.IsFinite(Assert.Single(result.Projections).ParameterCorrectionDegrees));
    }
    [Fact]
    public void DuplicateStaleUnknownAndUnsupportedRequestsFailClosed()
    {
        var s = Mechanism(); var j = new AnatomicalJointRequest(HumanoidJointKind.LeftHip);
        Assert.False(HumanoidKinematicSolver.Solve(s, Request(j, j)).IsSolved);
        Assert.False(HumanoidKinematicSolver.Solve(s, Request(j) with { ShapeRevision = 1 }).IsSolved);
        Assert.False(HumanoidKinematicSolver.Solve(s, Request(j) with { RestPoseId = "other" }).IsSolved);
        Assert.False(HumanoidKinematicSolver.Solve(s, Request(new AnatomicalJointRequest((HumanoidJointKind)999))).IsSolved);
        Assert.True(HumanoidKinematicSolver.Solve(s, Request(new AnatomicalJointRequest(HumanoidJointKind.LeftShoulder, AbductionDegrees: 90))).IsSolved);
        Assert.Equal("HUM209", Assert.Single(HumanoidKinematicSolver.Solve(s, Request(new AnatomicalJointRequest(HumanoidJointKind.LeftWrist, 90))).Diagnostics).Code);
    }
    [Fact]
    public void CorruptBindSideAndHierarchyAreRejected()
    {
        var s = Mechanism();
        foreach (var bad in new[] { s.Joints[3] with { ParentIndex = 7 }, s.Joints[3] with { Side = AnatomicalSide.Right }, s.Joints[3] with { GlobalBind = Matrix4x4.Identity } })
        {
            var joints = s.Joints.ToArray(); joints[3] = bad;
            Assert.False(HumanoidKinematicSolver.Solve(s with { Joints = joints }, Request()).IsSolved);
        }
    }
    [Fact]
    public void SurfaceRequiresMatchingSolveAndRetainsFiniteRigidPatch()
    {
        var skeleton = Mechanism();
        var solved = HumanoidKinematicSolver.Solve(skeleton, Request(new AnatomicalJointRequest(HumanoidJointKind.LeftHip, 90))).Pose!;
        var surface = Patch();
        var output = HumanoidConstrainedSurface.Evaluate(surface, skeleton, 0, solved);
        Assert.True(output.Evidence.IsAdmissible);
        Assert.Throws<InvalidOperationException>(() => HumanoidConstrainedSurface.Evaluate(surface, skeleton, 1, solved));
        var joints = skeleton.Joints.ToArray(); joints[3] = joints[3] with { Id = "changed" };
        Assert.Throws<InvalidOperationException>(() => HumanoidConstrainedSurface.Evaluate(surface, skeleton with { Joints = joints }, 0, solved));
    }
    [Fact]
    public void SurfaceCollapseAndReversalAreNotAccepted()
    {
        var s = Mechanism(); var surface = Patch();
        var globals = HumanoidPosing.GlobalPose(s, new("rest", [], HumanoidTransform.Identity));
        var collapsed = HumanoidConstrainedSurface.Inspect(surface, s, globals, [Point3D.Origin, Point3D.Origin, Point3D.Origin]);
        Assert.False(collapsed.IsAdmissible); Assert.Equal(1, collapsed.CollapsedTriangles);
        var reversed = HumanoidConstrainedSurface.Inspect(surface, s, globals, [surface.Vertices[0].Position, surface.Vertices[2].Position, surface.Vertices[1].Position]);
        Assert.False(reversed.IsAdmissible); Assert.Single(reversed.FlaggedFaceIds);
    }
    private static HumanoidSurface Patch() => new("patch", "triangle", "test",
        [new("a", new(-100,0,900), HumanoidRegionKind.LeftThigh, 0), new("b", new(-90,0,900), HumanoidRegionKind.LeftThigh, 1), new("c", new(-100,10,900), HumanoidRegionKind.LeftThigh, 2)],
        [new("f",0,1,2,HumanoidRegionKind.LeftThigh,null)], [], null,
        [new(0,[new(2,1)]),new(1,[new(2,1)]),new(2,[new(2,1)])],[],new Dictionary<HumanoidRegionKind,HumanoidRegionKind>());
}
