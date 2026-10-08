using System.Numerics;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Humanoid;

public sealed record HumanoidVertexCorrection(int VertexIndex, Vector3 DeltaMm);

/// <summary>An authored pre-skin shape, activated by solved absolute anatomical flexion.</summary>
public sealed record HumanoidPoseCorrective(
    string Id, HumanoidJointKind Joint, double StartFlexionDegrees, double FullFlexionDegrees,
    IReadOnlyList<HumanoidVertexCorrection> Vertices);

/// <summary>Two float4 values per joint, in System.Numerics XYZW quaternion order.</summary>
public readonly record struct HumanoidSkinDualQuaternion(Quaternion Real, Quaternion Dual)
{
    public static HumanoidSkinDualQuaternion FromRigidTransform(Matrix4x4 transform)
    {
        if (!Matrix4x4.Decompose(transform, out var scale, out var rotation, out var translation) ||
            Vector3.Distance(scale, Vector3.One) > 1e-4f ||
            !Finite(rotation) || !Finite(translation))
        {
            throw new InvalidDataException("HUM310: Dual-quaternion skinning requires finite rigid transforms.");
        }

        var reconstructed = Matrix4x4.CreateFromQuaternion(rotation) *
            Matrix4x4.CreateTranslation(translation);
        if (MaximumDifference(transform, reconstructed) > 1e-3f)
        {
            throw new InvalidDataException("HUM310: A skin transform contains shear or a non-affine frame.");
        }

        var real = Quaternion.Normalize(rotation);
        var dual = Quaternion.Multiply(new Quaternion(translation, 0), real) * .5f;
        return new(real, dual);
    }

    public Vector3 TransformPoint(Vector3 point)
    {
        var translation = Quaternion.Multiply(Dual, Quaternion.Conjugate(Real)) * 2;
        return Vector3.Transform(point, Real) + new Vector3(translation.X, translation.Y, translation.Z);
    }

    private static float MaximumDifference(Matrix4x4 a, Matrix4x4 b)
    {
        float[] differences =
        [
            a.M11 - b.M11, a.M12 - b.M12, a.M13 - b.M13, a.M14 - b.M14,
            a.M21 - b.M21, a.M22 - b.M22, a.M23 - b.M23, a.M24 - b.M24,
            a.M31 - b.M31, a.M32 - b.M32, a.M33 - b.M33, a.M34 - b.M34,
            a.M41 - b.M41, a.M42 - b.M42, a.M43 - b.M43, a.M44 - b.M44,
        ];
        if (differences.Any(value => !float.IsFinite(value)))
        {
            return float.PositiveInfinity;
        }
        return differences.Max(MathF.Abs);
    }

    private static bool Finite(Quaternion value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) &&
        float.IsFinite(value.Z) && float.IsFinite(value.W);

    private static bool Finite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}

/// <summary>
/// A retained rest surface and skin binding. Pose palettes are O(joints); vertex evaluation is
/// an explicit CPU reference. The same palette can be uploaded for GPU deformation.
/// Binding compilation is not canonical or production admission of a character asset.
/// </summary>
public sealed class HumanoidSkinningBinding
{
    private readonly HumanoidSkeleton skeleton;
    private readonly long shapeRevision;
    private readonly Vector3[] positions;
    private readonly JointWeight[][] influences;
    private readonly HumanoidPoseCorrective[] correctives;

    public HumanoidSkinningBinding(HumanoidSurface surface, HumanoidSkeleton skeleton, long shapeRevision = 0,
        IReadOnlyList<HumanoidPoseCorrective>? correctives = null)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(skeleton);
        if (surface.Vertices.Count == 0 || surface.SkinWeights.Count != surface.Vertices.Count ||
            skeleton.Joints.Count == 0)
        {
            throw new InvalidDataException("HUM005: A skin binding needs a complete rest surface and skeleton.");
        }

        this.skeleton = skeleton with { Joints = Array.AsReadOnly(skeleton.Joints.ToArray()) };
        this.shapeRevision = shapeRevision;
        positions = new Vector3[surface.Vertices.Count];
        influences = new JointWeight[surface.Vertices.Count][];
        for (var index = 0; index < positions.Length; index++)
        {
            var point = surface.Vertices[index].Position;
            var skin = surface.SkinWeights[index];
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(point.Z) ||
                skin.VertexIndex != index || skin.Weights.Count == 0 ||
                skin.Weights.Any(weight => weight.JointIndex < 0 || weight.JointIndex >= skeleton.Joints.Count ||
                    !double.IsFinite(weight.Weight) || weight.Weight < 0) ||
                skin.Weights.Select(weight => weight.JointIndex).Distinct().Count() != skin.Weights.Count ||
                Math.Abs(skin.Weights.Sum(weight => weight.Weight) - 1) > CanonicalAdultStandardV1.WeightTolerance)
            {
                throw new InvalidDataException($"HUM005: Invalid rest vertex or normalized skin binding at {index}.");
            }

            positions[index] = new((float)point.X, (float)point.Y, (float)point.Z);
            if (!float.IsFinite(positions[index].X) || !float.IsFinite(positions[index].Y) ||
                !float.IsFinite(positions[index].Z))
            {
                throw new InvalidDataException($"HUM005: Rest vertex {index} exceeds the float skinning domain.");
            }
            influences[index] = skin.Weights.Where(weight => weight.Weight > 0)
                .OrderByDescending(weight => weight.Weight).ThenBy(weight => weight.JointIndex).ToArray();
        }
        this.correctives = (correctives ?? []).Select(corrective =>
        {
            if (string.IsNullOrWhiteSpace(corrective.Id) ||
                !skeleton.Joints.Any(joint => joint.Kind == corrective.Joint) ||
                !double.IsFinite(corrective.StartFlexionDegrees) ||
                !double.IsFinite(corrective.FullFlexionDegrees) ||
                corrective.FullFlexionDegrees <= corrective.StartFlexionDegrees ||
                corrective.Vertices.Any(vertex => vertex.VertexIndex < 0 || vertex.VertexIndex >= VertexCount ||
                    !float.IsFinite(vertex.DeltaMm.X) || !float.IsFinite(vertex.DeltaMm.Y) ||
                    !float.IsFinite(vertex.DeltaMm.Z)) ||
                corrective.Vertices.Select(vertex => vertex.VertexIndex).Distinct().Count() != corrective.Vertices.Count)
            {
                throw new InvalidDataException($"HUM312: Invalid pose corrective {corrective.Id}.");
            }
            return corrective with { Vertices = Array.AsReadOnly(corrective.Vertices.ToArray()) };
        }).ToArray();
        if (this.correctives.Select(corrective => corrective.Id).Distinct(StringComparer.Ordinal).Count() !=
            this.correctives.Length)
        {
            throw new InvalidDataException("HUM312: Pose corrective IDs must be unique.");
        }
    }

    public int VertexCount => positions.Length;
    public int MaximumInfluences => influences.Max(weights => weights.Length);

    public IReadOnlyList<HumanoidSkinDualQuaternion> CreatePalette(SolvedHumanoidPose pose)
    {
        ArgumentNullException.ThrowIfNull(pose);
        if (pose.ShapeRevision != shapeRevision || skeleton.SkeletonId != pose.Skeleton.SkeletonId ||
            skeleton.RestPoseId != pose.Skeleton.RestPoseId || !skeleton.Joints.SequenceEqual(pose.Skeleton.Joints))
        {
            throw new InvalidOperationException("HUM208: Skin binding belongs to a different rest skeleton or shape revision.");
        }

        var palette = new HumanoidSkinDualQuaternion[skeleton.Joints.Count];
        for (var index = 0; index < palette.Length; index++)
        {
            var transform = skeleton.Joints[index].InverseBind * pose.GlobalTransforms[index];
            palette[index] = HumanoidSkinDualQuaternion.FromRigidTransform(transform);
        }
        return Array.AsReadOnly(palette);
    }

    public IReadOnlyList<Point3D> Evaluate(SolvedHumanoidPose pose)
    {
        var palette = CreatePalette(pose);
        var corrected = ApplyCorrectives(pose);
        var result = new Point3D[positions.Length];
        for (var index = 0; index < result.Length; index++)
        {
            var blended = Blend(influences[index], palette);
            var point = blended.TransformPoint(corrected[index]);
            result[index] = new(point.X, point.Y, point.Z);
        }
        return Array.AsReadOnly(result);
    }

    public IReadOnlyDictionary<string, double> ObserveCorrectives(SolvedHumanoidPose pose)
    {
        CreatePalette(pose);
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, double>(
            correctives.ToDictionary(corrective => corrective.Id,
                corrective => Activation(corrective, pose), StringComparer.Ordinal));
    }

    private Vector3[] ApplyCorrectives(SolvedHumanoidPose pose)
    {
        if (correctives.Length == 0)
        {
            return positions;
        }
        var corrected = positions.ToArray();
        foreach (var corrective in correctives)
        {
            var amount = (float)Activation(corrective, pose);
            foreach (var vertex in corrective.Vertices)
            {
                corrected[vertex.VertexIndex] += vertex.DeltaMm * amount;
            }
        }
        return corrected;
    }

    private static double Activation(HumanoidPoseCorrective corrective, SolvedHumanoidPose pose)
    {
        var joint = pose.Joints.FirstOrDefault(request => request.Joint == corrective.Joint);
        var degrees = joint?.FlexionDegrees ?? 0;
        var t = Math.Clamp((degrees - corrective.StartFlexionDegrees) /
            (corrective.FullFlexionDegrees - corrective.StartFlexionDegrees), 0, 1);
        return t * t * (3 - 2 * t);
    }

    internal static HumanoidSkinDualQuaternion Blend(
        IReadOnlyList<JointWeight> weights, IReadOnlyList<HumanoidSkinDualQuaternion> palette)
    {
        var real = new Quaternion(0, 0, 0, 0);
        var dual = real;
        var reference = palette[weights[0].JointIndex].Real;
        foreach (var weight in weights)
        {
            var transform = palette[weight.JointIndex];
            var sign = Quaternion.Dot(reference, transform.Real) < 0 ? -1 : 1;
            var amount = (float)(sign * weight.Weight);
            real += transform.Real * amount;
            dual += transform.Dual * amount;
        }

        var length = real.Length();
        if (!float.IsFinite(length) || length < 1e-8f)
        {
            throw new InvalidDataException("HUM311: Degenerate dual-quaternion skin blend.");
        }
        real *= 1 / length;
        dual *= 1 / length;
        dual -= real * Quaternion.Dot(real, dual);
        return new(real, dual);
    }
}
