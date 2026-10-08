using System.Numerics;
using Aetheris.Humanoid;
using Aetheris.Kernel.Core.Math;
using Xunit;

namespace Aetheris.Humanoid.Tests;

public sealed class HumanoidSkinningTests
{
    [Fact]
    public void RigidPaletteMatchesMatrixWithRotationAndTranslation()
    {
        var matrix = Matrix4x4.CreateFromYawPitchRoll(.7f, -.4f, 1.1f) *
            Matrix4x4.CreateTranslation(125, -37, 860);
        var point = new Vector3(42, 73, -116);
        var skin = HumanoidSkinDualQuaternion.FromRigidTransform(matrix);
        Assert.InRange(Vector3.Distance(Vector3.Transform(point, matrix), skin.TransformPoint(point)), 0, .0002);
    }

    [Fact]
    public void HalfHipRotationPreservesRadiusAndMatchesAnalyticPoint()
    {
        var (surface, skeleton) = Fixture();
        var binding = new HumanoidSkinningBinding(surface, skeleton);
        var solved = Solve(skeleton, 90);
        var point = binding.Evaluate(solved)[0];
        var expected = new Point3D(-8, Math.Sqrt(50), 100 - Math.Sqrt(50));
        Assert.InRange((point - expected).Length, 0, .0001);
        Assert.Equal(3, binding.CreatePalette(solved).Count);
        Assert.Equal(2, binding.MaximumInfluences);
    }

    [Fact]
    public void BindPoseReconstructsRestVertices()
    {
        var (surface, skeleton) = Fixture();
        var binding = new HumanoidSkinningBinding(surface, skeleton);
        var points = binding.Evaluate(Solve(skeleton, 0));
        for (var index = 0; index < points.Count; index++)
        {
            Assert.InRange((points[index] - surface.Vertices[index].Position).Length, 0, .0001);
        }
    }

    [Fact]
    public void BindingRetainsVertexAndWeightSnapshots()
    {
        var (surface, skeleton) = Fixture();
        var weights = surface.SkinWeights[0].Weights.ToArray();
        var vertices = surface.Vertices.ToArray();
        surface = surface with
        {
            Vertices = vertices,
            SkinWeights = [new(0, weights), surface.SkinWeights[1], surface.SkinWeights[2]],
        };
        var binding = new HumanoidSkinningBinding(surface, skeleton);
        var solved = Solve(skeleton, 70);
        var before = binding.Evaluate(solved).ToArray();
        weights[0] = new(0, 1);
        vertices[0] = vertices[0] with { Position = new(1000, 1000, 1000) };
        Assert.Equal(before, binding.Evaluate(solved));
    }

    [Fact]
    public void StaleShapeAndRestSkeletonAreRejected()
    {
        var (surface, skeleton) = Fixture();
        var binding = new HumanoidSkinningBinding(surface, skeleton, 1);
        Assert.Throws<InvalidOperationException>(() => binding.Evaluate(Solve(skeleton, 45)));
        binding = new(surface, skeleton);
        var differentRest = skeleton with { RestPoseId = "different-rest" };
        Assert.Throws<InvalidOperationException>(() => binding.Evaluate(Solve(differentRest, 45)));
    }

    [Theory]
    [InlineData(-.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidWeightsAreRejected(double value)
    {
        var (surface, skeleton) = Fixture();
        surface = surface with
        {
            SkinWeights = [new(0, [new(1, value), new(2, 1 - value)]),
                surface.SkinWeights[1], surface.SkinWeights[2]],
        };
        Assert.Throws<InvalidDataException>(() => new HumanoidSkinningBinding(surface, skeleton));
    }

    [Fact]
    public void DuplicateJointInfluencesAreRejected()
    {
        var (surface, skeleton) = Fixture();
        surface = surface with
        {
            SkinWeights = [new(0, [new(1, .5), new(1, .5)]),
                surface.SkinWeights[1], surface.SkinWeights[2]],
        };
        Assert.Throws<InvalidDataException>(() => new HumanoidSkinningBinding(surface, skeleton));
    }

    [Fact]
    public void ScaleShearAndNonfiniteFramesAreRejected()
    {
        Assert.Throws<InvalidDataException>(() =>
            HumanoidSkinDualQuaternion.FromRigidTransform(Matrix4x4.CreateScale(2)));
        var shear = Matrix4x4.Identity;
        shear.M12 = .5f;
        Assert.Throws<InvalidDataException>(() => HumanoidSkinDualQuaternion.FromRigidTransform(shear));
        var nonfinite = Matrix4x4.Identity;
        nonfinite.M14 = float.NaN;
        Assert.Throws<InvalidDataException>(() => HumanoidSkinDualQuaternion.FromRigidTransform(nonfinite));
    }

    [Fact]
    public void CorrectiveActivatesFromSolvedAnatomicalFlexionAndRetainsItsData()
    {
        var (surface, skeleton) = Fixture();
        var deltas = new[] { new HumanoidVertexCorrection(0, new(0, -10, -17)) };
        var shape = new HumanoidPoseCorrective("hip", HumanoidJointKind.LeftHip, 30, 90, deltas);
        var binding = new HumanoidSkinningBinding(surface, skeleton, correctives: [shape]);
        Assert.Equal(0, binding.ObserveCorrectives(Solve(skeleton, 20))["hip"]);
        Assert.Equal(.5, binding.ObserveCorrectives(Solve(skeleton, 60))["hip"], 5);
        Assert.Equal(1, binding.ObserveCorrectives(Solve(skeleton, 90))["hip"]);
        var before = binding.Evaluate(Solve(skeleton, 90)).ToArray();
        deltas[0] = new(0, new(1000, 1000, 1000));
        Assert.Equal(before, binding.Evaluate(Solve(skeleton, 90)));
        var uncorrected = new HumanoidSkinningBinding(surface, skeleton);
        Assert.NotEqual(uncorrected.Evaluate(Solve(skeleton, 90))[0], before[0]);
    }

    [Fact]
    public void InvalidCorrectiveBindingsAndDuplicateNamesAreRejected()
    {
        var (surface, skeleton) = Fixture();
        var valid = new HumanoidPoseCorrective("hip", HumanoidJointKind.LeftHip, 30, 90,
            [new(0, new(0, 0, -10))]);
        Assert.Throws<InvalidDataException>(() =>
            new HumanoidSkinningBinding(surface, skeleton, correctives: [valid, valid]));
        var wrongIndex = valid with { Vertices = [new(999, Vector3.Zero)] };
        Assert.Throws<InvalidDataException>(() =>
            new HumanoidSkinningBinding(surface, skeleton, correctives: [wrongIndex]));
        var wrongCurve = valid with { FullFlexionDegrees = 30 };
        Assert.Throws<InvalidDataException>(() =>
            new HumanoidSkinningBinding(surface, skeleton, correctives: [wrongCurve]));
    }

    [Fact]
    public void GameplayArtifactRoundtripUsesExplicitSourceGeneratedSerialization()
    {
        var (surface, skeleton) = Fixture();
        var provenance = CanonicalAdultTemplate.Create().Provenance;
        var shape = new HumanoidPoseCorrective("hip", HumanoidJointKind.LeftHip, 30, 90,
            [new(0, new(0, -10, -17))]);
        var artifact = new HumanoidGameplayBodyArtifact(HumanoidGameplayBody.Schema,
            "test-body", surface, skeleton, [shape], provenance);
        var path = Path.Combine(AppContext.BaseDirectory, "body-" + Guid.NewGuid() + ".json");
        try
        {
            HumanoidGameplayBody.Save(artifact, path);
            var body = HumanoidGameplayBody.Load(path);
            var pose = body.Solve("hip90", [new(HumanoidJointKind.LeftHip, 90)]).Pose!;
            var reference = new HumanoidSkinningBinding(surface, skeleton, correctives: [shape]);
            Assert.Equal(reference.Evaluate(Solve(skeleton, 90)), body.Evaluate(pose).Positions);
            Assert.Equal("test-body", body.Id);
            Assert.Equal(1, body.ObserveCorrectives(pose)["hip"]);
            Assert.Throws<InvalidDataException>(() => HumanoidArtifactIO.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static SolvedHumanoidPose Solve(HumanoidSkeleton skeleton, double angle)
    {
        var result = HumanoidKinematicSolver.Solve(skeleton,
            new("test", skeleton.SkeletonId, skeleton.RestPoseId, 0,
                [new(HumanoidJointKind.LeftHip, angle)]));
        Assert.True(result.IsSolved, string.Join("; ", result.Diagnostics));
        return result.Pose!;
    }

    private static (HumanoidSurface Surface, HumanoidSkeleton Skeleton) Fixture()
    {
        var root = Matrix4x4.Identity;
        var hip = Matrix4x4.CreateTranslation(-10, 0, 100);
        Matrix4x4.Invert(hip, out var inverse);
        var skeleton = new HumanoidSkeleton("skin-fixture", "rest", "row-vector",
        [
            new("root", HumanoidJointKind.Root, AnatomicalSide.Center, null,
                HumanoidTransform.Identity, root, root),
            new("pelvis", HumanoidJointKind.Pelvis, AnatomicalSide.Center, 0,
                HumanoidTransform.Identity, root, root),
            new("hip", HumanoidJointKind.LeftHip, AnatomicalSide.Left, 1,
                HumanoidTransform.FromTranslation(new(-10, 0, 100)), hip, inverse),
        ], new Dictionary<HumanoidJointKind, HumanoidJointKind>());
        var surface = new HumanoidSurface("skin-fixture", "triangles", "fixed",
        [
            new("a", new(-8, 0, 90), HumanoidRegionKind.LeftThigh, 0),
            new("b", new(-10, 2, 90), HumanoidRegionKind.LeftThigh, 1),
            new("c", new(-12, 0, 90), HumanoidRegionKind.LeftThigh, 2),
        ], [new("triangle", 0, 1, 2, HumanoidRegionKind.LeftThigh, null)], [], null,
        [
            new(0, [new(1, .5), new(2, .5)]),
            new(1, [new(1, .5), new(2, .5)]),
            new(2, [new(1, .5), new(2, .5)]),
        ], [], new Dictionary<HumanoidRegionKind, HumanoidRegionKind>());
        return (surface, skeleton);
    }
}
