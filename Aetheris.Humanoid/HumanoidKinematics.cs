using System.Numerics;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Humanoid;

public enum AnatomicalSolvePolicy { Reject, Project }
public sealed record AnatomicalJointRequest(HumanoidJointKind Joint, double FlexionDegrees = 0,
    double AbductionDegrees = 0, double TwistDegrees = 0);
public sealed record RequestedHumanoidPose(string PoseId, string SkeletonId, string RestPoseId,
    long ShapeRevision, IReadOnlyList<AnatomicalJointRequest> Joints);
public sealed record KinematicDiagnostic(string Code, string Message, HumanoidJointKind? Joint = null);
public sealed record AnatomicalLink(string Id, HumanoidJointKind Parent, HumanoidJointKind Child, double LengthMm);
public sealed record JointConstraintResidual(HumanoidJointKind Joint, double CenterMm, double LinkLengthMm);
public sealed record JointProjection(AnatomicalJointRequest Requested, AnatomicalJointRequest Solved,
    double ParameterCorrectionDegrees);

/// <summary>Parent socket and child origin coincide. Rotation is subordinate to this interface.</summary>
public abstract record AnatomicalJointInterface(HumanoidJointKind Joint, HumanoidJointKind Parent,
    Point3D ParentSocket, double MinimumFlexion, double MaximumFlexion);
public sealed record AnatomicalHingeInterface(HumanoidJointKind Joint, HumanoidJointKind Parent,
    Point3D ParentSocket, Vector3 Axis, double MinimumFlexion, double MaximumFlexion)
    : AnatomicalJointInterface(Joint, Parent, ParentSocket, MinimumFlexion, MaximumFlexion);
public sealed record AnatomicalBallInterface(HumanoidJointKind Joint, HumanoidJointKind Parent,
    Point3D ParentSocket, double MinimumFlexion, double MaximumFlexion,
    double MinimumAbduction, double MaximumAbduction, double MaximumTwist)
    : AnatomicalJointInterface(Joint, Parent, ParentSocket, MinimumFlexion, MaximumFlexion);

/// <summary>Unforgeable through public constructors; immutable snapshots bind a solve to one rest skeleton.</summary>
public sealed class SolvedHumanoidPose
{
    internal SolvedHumanoidPose(HumanoidSkeleton skeleton, long revision, HumanoidPoseState state,
        Matrix4x4[] globals, AnatomicalJointRequest[] joints, JointConstraintResidual[] residuals)
    {
        Skeleton = skeleton; ShapeRevision = revision; State = state;
        GlobalTransforms = Array.AsReadOnly(globals); Joints = Array.AsReadOnly(joints);
        Residuals = Array.AsReadOnly(residuals);
    }
    internal HumanoidSkeleton Skeleton { get; }
    internal HumanoidPoseState State { get; }
    public long ShapeRevision { get; }
    public IReadOnlyList<Matrix4x4> GlobalTransforms { get; }
    public IReadOnlyList<AnatomicalJointRequest> Joints { get; }
    public IReadOnlyList<JointConstraintResidual> Residuals { get; }
}
public sealed record HumanoidSolveResult(SolvedHumanoidPose? Pose,
    IReadOnlyList<KinematicDiagnostic> Diagnostics, IReadOnlyList<JointProjection> Projections)
{
    public bool IsSolved => Pose is not null;
}

/// <summary>
/// Bounded X2 progression: hip ball and elbow/knee hinge interfaces. Other joints remain at rest.
/// Shoulder requests fail closed until a qualified compound shoulder interface exists.
/// Angles are engineering coordinates relative to the supplied rest pose, not clinical measurements.
/// </summary>
public static class HumanoidKinematicSolver
{
    public const double LinearToleranceMm = .001;
    public const string Version = "humanoid.x2.lower-limb-and-hinge.v1";

    public static IReadOnlyList<AnatomicalJointInterface> Interfaces(HumanoidSkeleton skeleton)
    {
        var result = new List<AnatomicalJointInterface>();
        foreach (var joint in skeleton.Joints)
        {
            if (joint.ParentIndex is not int parent) continue;
            var kind = joint.Kind.ToString();
            if (kind.EndsWith("Hip"))
                result.Add(new AnatomicalBallInterface(joint.Kind, skeleton.Joints[parent].Kind,
                    joint.LocalRest.Translation, -20, 120, -25, 45, 45));
            if (kind.EndsWith("Knee") || kind.EndsWith("Elbow"))
            {
                var isKnee = kind.EndsWith("Knee");
                var nextKind = Enum.Parse<HumanoidJointKind>((joint.Side == AnatomicalSide.Left ? "Left" : "Right") + (isKnee ? "Ankle" : "Wrist"));
                var next = skeleton.Joints.SingleOrDefault(j => j.Kind == nextKind);
                if (next is null) continue;
                var direction = Position(next.GlobalBind) - Position(joint.GlobalBind);
                var axis = Vector3.Cross(direction, Vector3.UnitY);
                if (axis.LengthSquared() < 1e-10f) continue;
                axis = Vector3.Normalize(axis) * (isKnee ? -1 : 1);
                result.Add(new AnatomicalHingeInterface(joint.Kind, skeleton.Joints[parent].Kind,
                    joint.LocalRest.Translation, axis, 0, isKnee ? 140 : 145));
            }
        }
        return result.AsReadOnly();
    }

    public static IReadOnlyList<AnatomicalLink> Links(HumanoidSkeleton skeleton) => skeleton.Joints
        .Where(j => j.ParentIndex is not null).Select(j => new AnatomicalLink("link:" + j.Kind,
            skeleton.Joints[j.ParentIndex!.Value].Kind, j.Kind,
            Vector3.Distance(Position(j.GlobalBind), Position(skeleton.Joints[j.ParentIndex.Value].GlobalBind))))
        .ToArray();

    public static HumanoidSolveResult Solve(HumanoidSkeleton skeleton, RequestedHumanoidPose request,
        long shapeRevision = 0, AnatomicalSolvePolicy policy = AnatomicalSolvePolicy.Reject)
    {
        var diagnostics = new List<KinematicDiagnostic>();
        var projections = new List<JointProjection>();
        HumanoidSolveResult Reject(string code, string message, HumanoidJointKind? joint = null)
        { diagnostics.Add(new(code, message, joint)); return new(null, diagnostics.AsReadOnly(), projections.AsReadOnly()); }
        if (!Enum.IsDefined(policy)) return Reject("HUM208", "Unknown solve policy.");
        if (request.SkeletonId != skeleton.SkeletonId || request.RestPoseId != skeleton.RestPoseId || request.ShapeRevision != shapeRevision)
            return Reject("HUM208", "Requested skeleton, rest pose or shape revision is stale.");
        var skeletonError = ValidateRest(skeleton);
        if (skeletonError is not null) return Reject("HUM208", skeletonError);
        // Freeze all collections used by the solve. Caller mutation cannot alter an issued pose.
        skeleton = skeleton with { Joints = Array.AsReadOnly(skeleton.Joints.ToArray()),
            Symmetry = new System.Collections.ObjectModel.ReadOnlyDictionary<HumanoidJointKind, HumanoidJointKind>(skeleton.Symmetry.ToDictionary()) };
        var inputs = request.Joints.ToArray();
        if (inputs.Select(j => j.Joint).Distinct().Count() != inputs.Length)
            return Reject("HUM208", "Duplicate requested joint.");
        var interfaces = Interfaces(skeleton).ToDictionary(j => j.Joint);
        var states = new List<AnatomicalJointRequest>();
        var rotations = new List<JointPose>();
        foreach (var input in inputs.OrderBy(j => j.Joint))
        {
            if (!double.IsFinite(input.FlexionDegrees) || !double.IsFinite(input.AbductionDegrees) || !double.IsFinite(input.TwistDegrees))
                return Reject("HUM208", "Joint coordinates must be finite.", input.Joint);
            if (!interfaces.TryGetValue(input.Joint, out var joint))
                return Reject("HUM209", "No qualified anatomical interface for this joint; arbitrary transforms are not admitted.", input.Joint);
            var solved = Project(input, joint);
            var deltas = new[] { Math.Abs(input.FlexionDegrees - solved.FlexionDegrees),
                Math.Abs(input.AbductionDegrees - solved.AbductionDegrees), Math.Abs(input.TwistDegrees - solved.TwistDegrees) };
            var scale = deltas.Max();
            var correction = scale == 0 ? 0 : Math.Min(double.MaxValue, scale * Math.Sqrt(deltas.Sum(d => Square(d / scale))));
            if (correction > 1e-9)
            {
                if (policy == AnatomicalSolvePolicy.Reject) return Reject("HUM200", "Requested joint coordinates exceed the coupled admissible domain.", input.Joint);
                projections.Add(new(input, solved, correction));
                diagnostics.Add(new("HUM205", "Projected joint coordinates into the engineering domain.", input.Joint));
            }
            states.Add(solved);
            Quaternion rotation;
            if (joint is AnatomicalHingeInterface hinge) rotation = Rotate(hinge.Axis, solved.FlexionDegrees);
            else
            {
                // Exponential-map swing in the anatomical sagittal/coronal plane, followed by axial twist.
                // This is a coupled ellipse, not independent Euler limits.
                var sign = input.Joint.ToString().StartsWith("Left") ? 1 : -1;
                var swing = new Vector3((float)solved.FlexionDegrees, (float)(sign * solved.AbductionDegrees), 0);
                var angle = swing.Length();
                var q = angle == 0 ? Quaternion.Identity : Rotate(swing / angle, angle);
                rotation = Quaternion.Concatenate(Rotate(-Vector3.UnitZ, sign * solved.TwistDegrees), q);
            }
            rotations.Add(new(input.Joint, rotation));
        }
        var state = new HumanoidPoseState(request.PoseId, rotations.AsReadOnly(), HumanoidTransform.Identity);
        var globals = HumanoidPosing.GlobalPose(skeleton, state).ToArray();
        var residuals = new List<JointConstraintResidual>();
        for (var i = 0; i < skeleton.Joints.Count; i++)
        {
            var j = skeleton.Joints[i];
            if (j.ParentIndex is not int p) continue;
            var socket = j.LocalRest.Translation;
            var expected = Vector3.Transform(new((float)socket.X, (float)socket.Y, (float)socket.Z), globals[p]);
            var center = Vector3.Distance(expected, Position(globals[i]));
            var length = Math.Abs(Vector3.Distance(Position(globals[i]), Position(globals[p])) -
                Vector3.Distance(Position(j.GlobalBind), Position(skeleton.Joints[p].GlobalBind)));
            residuals.Add(new(j.Kind, center, length));
            if (!double.IsFinite(center) || center > LinearToleranceMm) return Reject("HUM201", "Joint center residual exceeds 0.001 mm.", j.Kind);
            if (!double.IsFinite(length) || length > LinearToleranceMm) return Reject("HUM202", "Link length residual exceeds 0.001 mm.", j.Kind);
        }
        return new(new(skeleton, shapeRevision, state, globals, states.ToArray(), residuals.ToArray()), diagnostics.AsReadOnly(), projections.AsReadOnly());
    }

    private static AnatomicalJointRequest Project(AnatomicalJointRequest input, AnatomicalJointInterface joint)
    {
        if (joint is AnatomicalHingeInterface)
            return input with { FlexionDegrees = Math.Clamp(input.FlexionDegrees, joint.MinimumFlexion, joint.MaximumFlexion), AbductionDegrees = 0, TwistDegrees = 0 };
        var ball = (AnatomicalBallInterface)joint;
        // Clamp before squaring to avoid overflow on adversarial finite coordinates.
        var f = Math.Clamp(input.FlexionDegrees, ball.MinimumFlexion, ball.MaximumFlexion);
        var a = Math.Clamp(input.AbductionDegrees, ball.MinimumAbduction, ball.MaximumAbduction);
        var radius = Math.Sqrt(Square(f / (f < 0 ? -ball.MinimumFlexion : ball.MaximumFlexion)) +
            Square(a / (a < 0 ? -ball.MinimumAbduction : ball.MaximumAbduction)));
        if (radius > 1) { f /= radius; a /= radius; }
        var twistLimit = ball.MaximumTwist * (1 - .5 * Math.Min(radius, 1));
        return input with { FlexionDegrees = f, AbductionDegrees = a, TwistDegrees = Math.Clamp(input.TwistDegrees, -twistLimit, twistLimit) };
    }

    private static string? ValidateRest(HumanoidSkeleton skeleton)
    {
        if (skeleton.Joints.Count == 0 || skeleton.Joints.Select(j => j.Kind).Distinct().Count() != skeleton.Joints.Count)
            return "Skeleton must have nonempty, unique joints.";
        for (var i = 0; i < skeleton.Joints.Count; i++)
        {
            var j = skeleton.Joints[i];
            if (j.ParentIndex is int p && (p < 0 || p >= i)) return "Skeleton parents must precede children.";
            if (i == 0 ? j.ParentIndex is not null : j.ParentIndex is null) return "Skeleton must have exactly one root in first position.";
            if (j.LocalRest.Rotation != Quaternion.Identity) return "X2 progression requires translation-only rest frames; rotated rest frames are not silently reinterpreted.";
            var expectedSide = j.Kind.ToString().StartsWith("Left") ? AnatomicalSide.Left : j.Kind.ToString().StartsWith("Right") ? AnatomicalSide.Right : AnatomicalSide.Center;
            if (!Enum.IsDefined(j.Kind) || j.Side != expectedSide) return "Invalid joint kind or anatomical side.";
            var expected = j.ParentIndex is int parent ? j.LocalRest.Matrix * skeleton.Joints[parent].GlobalBind : j.LocalRest.Matrix;
            if (!Near(expected, j.GlobalBind) || !Near(j.GlobalBind * j.InverseBind, Matrix4x4.Identity)) return "Non-finite or inconsistent rest/bind frames.";
            var name = j.Kind.ToString();
            var stem = name.StartsWith("Left") ? name[4..] : name.StartsWith("Right") ? name[5..] : name;
            var expectedParent = stem switch { "Hip" => "Pelvis", "Knee" => name.Replace("Knee", "Hip"), "Ankle" => name.Replace("Ankle", "Knee"), "Elbow" => name.Replace("Elbow", "Shoulder"), "Wrist" => name.Replace("Wrist", "Elbow"), _ => null };
            if (expectedParent is not null && (j.ParentIndex is not int pi || skeleton.Joints[pi].Kind.ToString() != expectedParent)) return "Anatomical parent/side mismatch at " + name;
            if (expectedParent is not null && j.LocalRest.Translation == Point3D.Origin) return "Zero-length anatomical link at " + name;
        }
        return null;
    }

    internal static bool Near(Matrix4x4 a, Matrix4x4 b)
    {
        float[] Values(Matrix4x4 m) => [m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];
        return Values(a).Zip(Values(b)).All(x => float.IsFinite(x.First) && float.IsFinite(x.Second) && Math.Abs(x.First - x.Second) <= .0001);
    }
    private static Quaternion Rotate(Vector3 axis, double degrees) => Quaternion.CreateFromAxisAngle(axis, (float)(degrees * Math.PI / 180));
    private static double Square(double x) => x * x;
    internal static Vector3 Position(Matrix4x4 m) => new(m.M41, m.M42, m.M43);
}
