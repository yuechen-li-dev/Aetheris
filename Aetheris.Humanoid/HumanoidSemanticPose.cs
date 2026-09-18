using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Humanoid;

/// <summary>Frozen X1 neutral. Angles are anatomical states, never source-bone deltas.</summary>
public static class CanonicalHumanoidRestPose
{
    public const string Id = "aetheris.humanoid.rest.apose.v1";
    public const string SemanticFrameId = "aetheris.humanoid.anatomical-frame.v1";
    public const string DeformationPolicyId = "aetheris.humanoid.lbs.v1";
    public const double ShoulderAbductionDegrees = 35d;
    public const double ShoulderFlexionDegrees = 0d;
    public const double ShoulderTwistDegrees = 0d;
    public const double ElbowFlexionDegrees = 0d;
    public const double HipFlexionDegrees = 0d;
    public const double HipAbductionDegrees = 0d;
    public const double HipTwistDegrees = 0d;
    public const double KneeFlexionDegrees = 0d;
    public const string PalmOrientation = "source-neutral palm frame preserved; wrist rotation is 0 degrees in the canonical frame";
    public const string FootOrientation = "feet parallel to canonical forward with soles grounded";

    public static IReadOnlyList<AnatomicalJointRequest> Requests =>
    [
        new(HumanoidJointKind.LeftShoulder, ShoulderFlexionDegrees, ShoulderAbductionDegrees, ShoulderTwistDegrees),
        new(HumanoidJointKind.RightShoulder, ShoulderFlexionDegrees, ShoulderAbductionDegrees, ShoulderTwistDegrees),
        new(HumanoidJointKind.LeftElbow, ElbowFlexionDegrees), new(HumanoidJointKind.RightElbow, ElbowFlexionDegrees),
        new(HumanoidJointKind.LeftHip, HipFlexionDegrees, HipAbductionDegrees, HipTwistDegrees),
        new(HumanoidJointKind.RightHip, HipFlexionDegrees, HipAbductionDegrees, HipTwistDegrees),
        new(HumanoidJointKind.LeftKnee, KneeFlexionDegrees), new(HumanoidJointKind.RightKnee, KneeFlexionDegrees)
    ];
}

public sealed record AnatomicalJointState(HumanoidJointKind Joint, double FlexionDegrees,
    double AbductionDegrees, double TwistDegrees);

/// <summary>Measures canonical anatomical state from joint centers in +X right, +Y forward, +Z up.</summary>
public static class HumanoidPoseSemantics
{
    public static bool TryMeasure(HumanoidSkeleton skeleton, IReadOnlyList<Matrix4x4> globals,
        HumanoidJointKind kind, out AnatomicalJointState state)
    {
        try { state = Measure(skeleton, globals, kind); return true; }
        catch (InvalidDataException) { state = new(kind, 0, 0, 0); return false; }
    }

    public static AnatomicalJointState Measure(HumanoidSkeleton skeleton,
        IReadOnlyList<Matrix4x4> globals, HumanoidJointKind kind)
    {
        var index = skeleton.Joints.ToList().FindIndex(joint => joint.Kind == kind);
        if (index < 0 || globals.Count != skeleton.Joints.Count)
            throw new InvalidDataException("HUM210: Semantic pose measurement requires the complete skeleton.");
        var joint = skeleton.Joints[index];
        if (kind.ToString().EndsWith("Shoulder") || kind.ToString().EndsWith("Hip"))
        {
            var childKind = Child(kind, kind.ToString().EndsWith("Shoulder") ? "Elbow" : "Knee");
            var direction = Vector3.Normalize(Position(globals[Index(skeleton, childKind)]) - Position(globals[index]));
            var outward = (joint.Side == AnatomicalSide.Left ? -1f : 1f) * direction.X;
            double flexion, abduction;
            var coronalLength = Math.Sqrt(outward * outward + direction.Z * direction.Z);
            if (coronalLength < 1e-5)
            {
                flexion = direction.Y >= 0 ? 90 : -90;
                abduction = 0; // abduction is singular at exactly 90 degrees flexion
            }
            else
            {
                var firstFlexion = Degrees(Math.Asin(Math.Clamp(direction.Y, -1f, 1f)));
                var firstAbduction = Degrees(Math.Atan2(outward, -direction.Z));
                var secondFlexion = direction.Y >= 0 ? 180 - firstFlexion : -180 - firstFlexion;
                var secondAbduction = NormalizeDegrees(firstAbduction + 180);
                var shoulder = kind.ToString().EndsWith("Shoulder");
                var firstValid = firstFlexion >= (shoulder ? -30 : -20) && firstFlexion <= 120 &&
                    firstAbduction >= (shoulder ? -30 : -25) && firstAbduction <= (shoulder ? 120 : 45);
                (flexion, abduction) = firstValid
                    ? (firstFlexion, firstAbduction)
                    : (secondFlexion, secondAbduction);
            }
            return new(kind, Clean(flexion), Clean(abduction), 0);
        }
        if (kind.ToString().EndsWith("Elbow") || kind.ToString().EndsWith("Knee"))
        {
            if (joint.ParentIndex is not int parent) throw new InvalidDataException("HUM210: Hinge has no parent.");
            var childKind = Child(kind, kind.ToString().EndsWith("Elbow") ? "Wrist" : "Ankle");
            var proximal = Vector3.Normalize(Position(globals[index]) - Position(globals[parent]));
            var distal = Vector3.Normalize(Position(globals[Index(skeleton, childKind)]) - Position(globals[index]));
            var flexion = Degrees(Math.Acos(Math.Clamp(Vector3.Dot(proximal, distal), -1f, 1f)));
            return new(kind, Clean(flexion), 0, 0);
        }
        throw new InvalidDataException("HUM209: Joint has no qualified semantic pose coordinates: " + kind);
    }

    private static int Index(HumanoidSkeleton skeleton, HumanoidJointKind kind)
    {
        var result = skeleton.Joints.ToList().FindIndex(joint => joint.Kind == kind);
        return result >= 0 ? result : throw new InvalidDataException("HUM210: Missing semantic child " + kind);
    }
    private static HumanoidJointKind Child(HumanoidJointKind kind, string childStem)
        => Enum.Parse<HumanoidJointKind>((kind.ToString().StartsWith("Left") ? "Left" : "Right") + childStem);
    private static Vector3 Position(Matrix4x4 matrix) => new(matrix.M41, matrix.M42, matrix.M43);
    private static double Degrees(double radians) => radians * 180d / Math.PI;
    private static double NormalizeDegrees(double value)
    {
        while (value > 180) value -= 360;
        while (value <= -180) value += 360;
        return value;
    }
    private static double Clean(double value) => Math.Abs(value) < 1e-6 ? 0 : value;
}

public sealed record HumanoidRestNormalization(
    HumanoidSurface Surface,
    HumanoidSkeleton Skeleton,
    string SourceRestPoseId,
    string RestPoseId,
    IReadOnlyList<AnatomicalJointState> SourceNeutral,
    IReadOnlyList<SemanticPoseResidual> Residuals,
    double MaximumBindReconstructionMm,
    HumanoidPoseState AppliedSourcePose);

/// <summary>Reposes a skinned source rest into the frozen A-pose and rebuilds all bind data.</summary>
public static class HumanoidRestPoseNormalizer
{
    public static CanonicalHumanoid Normalize(CanonicalHumanoid source)
    {
        var normalized = Normalize(source.Surface, source.Skeleton);
        var attachments = source.Attachments.Select(site => site with
        {
            NeutralFrame = HumanoidPosing.EvaluateAttachment(source, site.Id, normalized.AppliedSourcePose)
        }).ToArray();
        static string MatrixText(Matrix4x4 m) => FormattableString.Invariant(
            $"{m.M11:R},{m.M12:R},{m.M13:R},{m.M14:R},{m.M21:R},{m.M22:R},{m.M23:R},{m.M24:R},{m.M31:R},{m.M32:R},{m.M33:R},{m.M34:R},{m.M41:R},{m.M42:R},{m.M43:R},{m.M44:R}");
        var outputHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",
            normalized.Surface.Vertices.Select(vertex => FormattableString.Invariant(
                $"{vertex.Id}:{vertex.Position.X:R},{vertex.Position.Y:R},{vertex.Position.Z:R}"))
            .Concat(normalized.Skeleton.Joints.Select(joint => $"{joint.Id}:{MatrixText(joint.GlobalBind)}"))))));
        return source with
        {
            Surface = normalized.Surface,
            Skeleton = normalized.Skeleton,
            Attachments = attachments,
            PoseState = new(CanonicalHumanoidRestPose.Id, [], HumanoidTransform.Identity),
            Provenance = source.Provenance with
            {
                Transformations = source.Provenance.Transformations.Concat(
                    [$"semantic rest normalization {source.Skeleton.RestPoseId} -> {CanonicalHumanoidRestPose.Id}"]).ToArray(),
                GeneratorVersion = source.Provenance.GeneratorVersion + "+rest-x1",
                OutputHash = outputHash
            }
        };
    }

    public static HumanoidRestNormalization Normalize(HumanoidSurface surface, HumanoidSkeleton source)
    {
        var sourceNeutral = CanonicalHumanoidRestPose.Requests
            .Select(request => HumanoidPoseSemantics.Measure(source, source.Joints.Select(j => j.GlobalBind).ToArray(), request.Joint))
            .ToArray();
        var solve = HumanoidKinematicSolver.Solve(source,
            new("canonical-apose-normalization", source.SkeletonId, source.RestPoseId, 0, CanonicalHumanoidRestPose.Requests));
        if (!solve.IsSolved) throw new InvalidDataException("HUM211: A-pose normalization failed: " +
            string.Join("; ", solve.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var pose = solve.Pose!;
        var positions = HumanoidPosing.EvaluateVertices(surface, source, pose.State);
        var vertices = surface.Vertices.Select((vertex, index) => vertex with { Position = positions[index] }).ToArray();
        var normalizedSurface = surface with { Vertices = vertices };
        var joints = new HumanoidJoint[source.Joints.Count];
        for (var i = 0; i < joints.Length; i++)
        {
            var old = source.Joints[i];
            var global = pose.GlobalTransforms[i];
            var local = old.ParentIndex is int parent ? global * Inverse(pose.GlobalTransforms[parent]) : global;
            if (!Matrix4x4.Decompose(local, out var scale, out var rotation, out var translation) ||
                Vector3.Distance(scale, Vector3.One) > 1e-4f || !Matrix4x4.Invert(global, out var inverse))
                throw new InvalidDataException("HUM211: Normalized bind is not rigid for " + old.Kind);
            joints[i] = old with
            {
                LocalRest = new(new(translation.X, translation.Y, translation.Z), Quaternion.Normalize(rotation)),
                GlobalBind = global,
                InverseBind = inverse
            };
        }
        var skeleton = source with { RestPoseId = CanonicalHumanoidRestPose.Id, Joints = joints };
        var reconstructed = HumanoidPosing.EvaluateVertices(normalizedSurface, skeleton,
            new(CanonicalHumanoidRestPose.Id, [], HumanoidTransform.Identity));
        var bindError = reconstructed.Select((point, index) => (point - vertices[index].Position).Length).DefaultIfEmpty().Max();
        if (bindError > HumanoidKinematicSolver.LinearToleranceMm)
            throw new InvalidDataException($"HUM211: Normalized bind reconstruction residual {bindError:R} mm exceeds tolerance.");
        return new(normalizedSurface, skeleton, source.RestPoseId, CanonicalHumanoidRestPose.Id,
            sourceNeutral, pose.SemanticResiduals, bindError, pose.State);
    }

    private static Matrix4x4 Inverse(Matrix4x4 matrix) => Matrix4x4.Invert(matrix, out var inverse)
        ? inverse : throw new InvalidDataException("HUM211: Singular normalized parent bind.");
}
