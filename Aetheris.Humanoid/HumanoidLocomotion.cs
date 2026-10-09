using System.Numerics;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Humanoid;

/// <summary>Rest-relative authored rotations and bounded visual root offset, in canonical millimetres.</summary>
public sealed record AuthoredHumanoidPose(string PoseId, string SkeletonId, string RestPoseId,
    IReadOnlyList<JointPose> Rotations, Vector3 RootOffsetMm);

public sealed record HumanoidLegTarget(AnatomicalSide Side, Vector3 AnkleMm, Vector3 SoleNormal);
public sealed record HumanoidLegSolve(SolvedHumanoidPose Pose, float ResidualMm, bool ReachClamped);

public static partial class HumanoidKinematicSolver
{
    /// <summary>
    /// Gameplay animation admission. Preserves rigid rest links, validates every explicit
    /// rotation against a named engineering envelope, and issues the existing skinning token.
    /// These rest-relative coordinates are distinct from absolute anatomical requests.
    /// </summary>
    public static HumanoidSolveResult SolveAuthored(HumanoidSkeleton skeleton, AuthoredHumanoidPose request)
    {
        HumanoidSolveResult Reject(string message) => new(null, [new("HUM320", message)], []);
        var error = ValidateRest(skeleton);
        if (error is not null || request.SkeletonId != skeleton.SkeletonId || request.RestPoseId != skeleton.RestPoseId)
        {
            return Reject(error ?? "Authored pose belongs to a different rest skeleton.");
        }
        if (!Finite(request.RootOffsetMm) || request.RootOffsetMm.Length() > 250 || request.Rotations is null ||
            request.Rotations.Select(item => item.Joint).Distinct().Count() != request.Rotations.Count)
        {
            return Reject("Authored root offset must be finite and at most 250 mm; rotations must be unique.");
        }
        var known = skeleton.Joints.Select(joint => joint.Kind).ToHashSet();
        foreach (var rotation in request.Rotations)
        {
            var q = rotation.LocalRotation;
            if (!known.Contains(rotation.Joint) || !Finite(q) || MathF.Abs(q.LengthSquared() - 1) > .0001f)
            {
                return Reject("Authored rotations must name existing joints and be finite unit quaternions.");
            }
            float angle = 2 * MathF.Acos(Math.Clamp(MathF.Abs(q.W), 0, 1)) * 180 / MathF.PI;
            if (angle > AuthoredEnvelopeDegrees(rotation.Joint) + .001f)
            {
                return Reject($"Authored joint '{rotation.Joint}' exceeds its rest-relative engineering envelope ({angle:F3} degrees).");
            }
        }
        skeleton = skeleton with
        {
            Joints = Array.AsReadOnly(skeleton.Joints.ToArray()),
            Symmetry = new System.Collections.ObjectModel.ReadOnlyDictionary<HumanoidJointKind, HumanoidJointKind>(skeleton.Symmetry.ToDictionary()),
        };
        var offset = request.RootOffsetMm;
        var state = new HumanoidPoseState(request.PoseId, Array.AsReadOnly(request.Rotations.ToArray()),
            HumanoidTransform.FromTranslation(new Point3D(offset.X, offset.Y, offset.Z)));
        var globals = HumanoidPosing.GlobalPose(skeleton, state);
        var measured = new List<AnatomicalJointRequest>();
        foreach (var joint in Interfaces(skeleton))
        {
            if (HumanoidPoseSemantics.TryMeasure(skeleton, globals, joint.Joint, out var value))
            {
                // Corrective activation uses measured absolute flexion, not imported bone Euler angles.
                measured.Add(new(value.Joint, value.FlexionDegrees, value.AbductionDegrees, value.TwistDegrees));
                if (joint is AnatomicalHingeInterface && value.FlexionDegrees > joint.MaximumFlexion + .01)
                {
                    return Reject($"Authored hinge '{joint.Joint}' exceeds its flexion limit.");
                }
            }
        }
        return IssuePose(skeleton, state, measured, 0, [], []);
    }

    private static float AuthoredEnvelopeDegrees(HumanoidJointKind joint)
    {
        string name = joint.ToString();
        if (name.EndsWith("Shoulder")) return 165;
        if (name.EndsWith("Hip")) return 135;
        if (name.EndsWith("Knee") || name.EndsWith("Elbow")) return 150;
        if (name.EndsWith("Ankle")) return 95;
        // This gameplay hand frame includes pronation from collapsed forearm helpers.
        if (name.EndsWith("Wrist")) return 120;
        if (name.EndsWith("ToeBase")) return 70;
        if (joint is HumanoidJointKind.Pelvis) return 35;
        if (joint is HumanoidJointKind.SpineLower or HumanoidJointKind.SpineMid or HumanoidJointKind.Chest) return 35;
        if (joint is HumanoidJointKind.Neck or HumanoidJointKind.Head) return 55;
        if (name.EndsWith("Clavicle")) return 35;
        // Fingers and root rotation are deliberately outside this locomotion profile.
        return 0;
    }

    /// <summary>Analytic hip-knee-ankle solve; keeps link lengths and the animated knee pole.</summary>
    public static HumanoidLegSolve SolveLeg(HumanoidSkeleton skeleton, SolvedHumanoidPose source, HumanoidLegTarget target)
    {
        if (target.Side is not (AnatomicalSide.Left or AnatomicalSide.Right) ||
            !Finite(target.AnkleMm) || !Finite(target.SoleNormal) || target.SoleNormal.LengthSquared() < .5f ||
            !skeleton.Joints.SequenceEqual(source.Skeleton.Joints))
        {
            throw new ArgumentException("Leg IK needs a matching pose, finite ankle and a ground normal.");
        }
        string side = target.Side.ToString();
        int hip = Index(side + "Hip");
        int knee = Index(side + "Knee");
        int ankle = Index(side + "Ankle");
        Vector3 origin = source.GlobalTransforms[hip].Translation;
        Vector3 middle = source.GlobalTransforms[knee].Translation;
        Vector3 end = source.GlobalTransforms[ankle].Translation;
        float upper = Vector3.Distance(origin, middle);
        float lower = Vector3.Distance(middle, end);
        Vector3 direction = target.AnkleMm - origin;
        float requestedDistance = direction.Length();
        if (requestedDistance < .001f) direction = end - origin;
        direction = Vector3.Normalize(direction);
        float distance = Math.Clamp(requestedDistance, MathF.Abs(upper - lower) + .1f, upper + lower - .1f);
        Vector3 pole = middle - origin;
        pole -= direction * Vector3.Dot(pole, direction);
        if (pole.LengthSquared() < .01f)
        {
            pole = Vector3.UnitY - direction * Vector3.Dot(Vector3.UnitY, direction);
        }
        if (pole.LengthSquared() < .01f) pole = Vector3.UnitX;
        pole = Vector3.Normalize(pole);
        float along = (upper * upper - lower * lower + distance * distance) / (2 * distance);
        float height = MathF.Sqrt(MathF.Max(0, upper * upper - along * along));
        Vector3 solvedMiddle = origin + direction * along + pole * height;
        Vector3 solvedEnd = origin + direction * distance;
        var rotations = source.PoseState.LocalRotations.ToDictionary(item => item.Joint, item => item.LocalRotation);
        var globals = source.GlobalTransforms.ToArray();
        RotateJoint(hip, middle - origin, solvedMiddle - origin);
        Recompute();
        RotateJoint(knee, globals[ankle].Translation - globals[knee].Translation, solvedEnd - globals[knee].Translation);
        Recompute();
        // Preserve animated foot heading while aligning its transported rest sole to the contact normal.
        Vector3 restUpLocal = Vector3.TransformNormal(Vector3.UnitZ, skeleton.Joints[ankle].InverseBind);
        Vector3 footUp = Vector3.TransformNormal(restUpLocal, globals[ankle]);
        RotateJoint(ankle, footUp, Vector3.Normalize(target.SoleNormal));
        var authored = new AuthoredHumanoidPose(source.PoseState.PoseId + ".ik", skeleton.SkeletonId, skeleton.RestPoseId,
            rotations.OrderBy(pair => pair.Key).Select(pair => new JointPose(pair.Key, pair.Value)).ToArray(),
            source.PoseState.RootTransform.Matrix.Translation);
        var result = SolveAuthored(skeleton, authored);
        if (!result.IsSolved)
        {
            throw new InvalidDataException(string.Join("; ", result.Diagnostics.Select(item => item.Message)));
        }
        return new(result.Pose!, Vector3.Distance(result.Pose!.GlobalTransforms[ankle].Translation, target.AnkleMm),
            MathF.Abs(distance - requestedDistance) > .01f);

        int Index(string name)
        {
            int index = skeleton.Joints.ToList().FindIndex(joint => joint.Kind.ToString() == name);
            if (index < 0) throw new InvalidDataException("Leg IK is missing " + name);
            return index;
        }
        void Recompute()
        {
            var state = source.PoseState with
            {
                LocalRotations = rotations.Select(pair => new JointPose(pair.Key, pair.Value)).ToArray(),
            };
            globals = HumanoidPosing.GlobalPose(skeleton, state).ToArray();
        }
        void RotateJoint(int index, Vector3 from, Vector3 to)
        {
            Matrix4x4 parent = skeleton.Joints[index].ParentIndex is int p ? globals[p] : source.PoseState.RootTransform.Matrix;
            if (!Matrix4x4.Invert(parent, out var inverseParent) ||
                !Matrix4x4.Invert(skeleton.Joints[index].LocalRest.Matrix, out var inverseRest))
            {
                throw new InvalidDataException("Leg IK encountered a singular rest frame.");
            }
            Matrix4x4 desired = globals[index] * Matrix4x4.CreateFromQuaternion(Arc(from, to));
            desired.Translation = globals[index].Translation;
            Matrix4x4 delta = desired * inverseParent * inverseRest;
            rotations[skeleton.Joints[index].Kind] = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(delta));
        }
    }

    private static Quaternion Arc(Vector3 from, Vector3 to)
    {
        from = Vector3.Normalize(from);
        to = Vector3.Normalize(to);
        float dot = Math.Clamp(Vector3.Dot(from, to), -1, 1);
        if (dot > .999999f) return Quaternion.Identity;
        if (dot < -.999999f)
        {
            Vector3 axis = Vector3.Cross(from, Vector3.UnitX);
            if (axis.LengthSquared() < .001f) axis = Vector3.Cross(from, Vector3.UnitY);
            return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.PI);
        }
        var cross = Vector3.Cross(from, to);
        return Quaternion.Normalize(new Quaternion(cross, 1 + dot));
    }

    private static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    private static bool Finite(Quaternion value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z) && float.IsFinite(value.W);
}
