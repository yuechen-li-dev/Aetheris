using System.Numerics;
using Aetheris.Humanoid;
using Aetheris.Kernel.Core.Math;
using Xunit;

namespace Aetheris.Humanoid.Tests;

public sealed class HumanoidX1Tests
{
    [Fact]
    public void UnpromotedSurfaceUsesRealSkinEvaluatorWithoutInventingCanonicalSemantics()
    {
        var bind = Matrix4x4.CreateTranslation(10, 0, 0);
        Matrix4x4.Invert(bind, out var inverse);
        var skeleton = new HumanoidSkeleton("test", "bind", "row-vector LBS",
            [new("joint", HumanoidJointKind.Root, AnatomicalSide.Center, null,
                HumanoidTransform.FromTranslation(new(10, 0, 0)), bind, inverse)],
            new Dictionary<HumanoidJointKind, HumanoidJointKind>());
        var surface = new HumanoidSurface("unpromoted", "triangle", "test",
            [new("v", new(12, 0, 0), HumanoidRegionKind.Chest, 0)], [], [], null,
            [new(0, [new(0, 1)])], [], new Dictionary<HumanoidRegionKind, HumanoidRegionKind>());
        var neutral = HumanoidPosing.EvaluateVertices(surface, skeleton, new("bind", [], HumanoidTransform.Identity));
        Assert.Equal(new Point3D(12, 0, 0), neutral[0]);
        var posed = HumanoidPosing.EvaluateVertices(surface, skeleton,
            new("quarter-turn", [new(HumanoidJointKind.Root, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2))], HumanoidTransform.Identity));
        Assert.InRange((posed[0] - new Point3D(10, 2, 0)).Length, 0, .00001);
    }

    [Theory]
    [InlineData("{\"status\":\"NeedsReview-NONCANONICAL\"}")]
    [InlineData("{\"schemaVersion\":\"aetheris.humanoid.canonical.v1\"}")]
    public void CandidateAndIncompleteArtifactsCannotLoadAsCanonical(string json)
    {
        var path = Path.GetTempFileName();
        try { File.WriteAllText(path, json); Assert.Throws<InvalidDataException>(() => HumanoidArtifactIO.Load(path)); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void AdmissionRejectsUnpinnedSourceBeforeParsingOrReadingOtherInputs()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "v 0 0 0\nf 1 1 1");
            var error = Assert.Throws<HumanoidDomainException>(() => AntoniaSurfaceAdoption.Prepare(path, "absent-notice"));
            Assert.Equal(HumanoidDiagnosticCode.HUM002_ReferenceHashMismatch, error.Diagnostic.Code);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void SweepIncludesAllSixRequiredMovementsWithFixedAngles()
    {
        var poses = AntoniaSurfaceAdoption.QualificationPoses();
        Assert.Equal(12, poses.Count);
        Assert.Equal(12, poses.Select(p => p.PoseId).Distinct().Count());
        Assert.Contains(poses, p => p.PoseId == "shoulder-flexion-70");
        Assert.Contains(poses, p => p.PoseId == "hip-abduction-70");
        Assert.All(poses, p => Assert.InRange(p.LocalRotations.Single().LocalRotation.Length(), .999999f, 1.000001f));
    }
}
