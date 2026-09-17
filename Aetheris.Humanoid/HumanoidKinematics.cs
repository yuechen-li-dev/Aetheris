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
public sealed record SemanticPoseResidual(HumanoidJointKind Joint,
    double FlexionDegrees, double AbductionDegrees, double TwistDegrees)
{
    public double MaximumAbsoluteDegrees => Math.Max(Math.Abs(FlexionDegrees),
        Math.Max(Math.Abs(AbductionDegrees), Math.Abs(TwistDegrees)));
}
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
    double MinimumAbduction, double MaximumAbduction, double MaximumTwist,
    Vector3 FlexionAxis, Vector3 AbductionAxis, Vector3 TwistAxis)
    : AnatomicalJointInterface(Joint, Parent, ParentSocket, MinimumFlexion, MaximumFlexion);

/// <summary>Unforgeable through public constructors; immutable snapshots bind a solve to one rest skeleton.</summary>
public sealed class SolvedHumanoidPose
{
    internal SolvedHumanoidPose(HumanoidSkeleton skeleton, long revision, HumanoidPoseState state,
        Matrix4x4[] globals, AnatomicalJointRequest[] joints, JointConstraintResidual[] residuals,
        SemanticPoseResidual[] semanticResiduals)
    {
        Skeleton = skeleton; ShapeRevision = revision; State = state;
        GlobalTransforms = Array.AsReadOnly(globals); Joints = Array.AsReadOnly(joints);
        Residuals = Array.AsReadOnly(residuals);
        SemanticResiduals = Array.AsReadOnly(semanticResiduals);
    }
    internal HumanoidSkeleton Skeleton { get; }
    internal HumanoidPoseState State { get; }
    public HumanoidPoseState PoseState => State;
    public long ShapeRevision { get; }
    public IReadOnlyList<Matrix4x4> GlobalTransforms { get; }
    public IReadOnlyList<AnatomicalJointRequest> Joints { get; }
    public IReadOnlyList<JointConstraintResidual> Residuals { get; }
    public IReadOnlyList<SemanticPoseResidual> SemanticResiduals { get; }
}
public sealed record HumanoidSolveResult(SolvedHumanoidPose? Pose,
    IReadOnlyList<KinematicDiagnostic> Diagnostics, IReadOnlyList<JointProjection> Projections)
{
    public bool IsSolved => Pose is not null;
}

/// <summary>
/// Bounded hip/shoulder ball and elbow/knee hinge interfaces. Axes are expressed in each
/// adopted joint's source-derived rest frame. Public angles are absolute canonical anatomical
/// states; source-rest offsets are measured and removed inside the adapter.
/// </summary>
public static class HumanoidKinematicSolver
{
    public const double LinearToleranceMm = .001;
    public const string Version = "aetheris.humanoid.semantic-pose.v1";

    public static IReadOnlyList<AnatomicalJointInterface> Interfaces(HumanoidSkeleton skeleton)
    {
        var result = new List<AnatomicalJointInterface>();
        foreach (var joint in skeleton.Joints)
        {
            if (joint.ParentIndex is not int parent) continue;
            var kind = joint.Kind.ToString();
            if (kind.EndsWith("Hip") || kind.EndsWith("Shoulder"))
            {
                var shoulder = kind.EndsWith("Shoulder");
                result.Add(new AnatomicalBallInterface(joint.Kind, skeleton.Joints[parent].Kind,
                    joint.LocalRest.Translation, shoulder ? -30 : -20, 120, shoulder ? -30 : -25,
                    shoulder ? 120 : 45, 45,
                    ToLocalAxis(joint, Vector3.UnitX), ToLocalAxis(joint, Vector3.UnitY), ToLocalAxis(joint, -Vector3.UnitZ)));
            }
            if (kind.EndsWith("Knee") || kind.EndsWith("Elbow"))
            {
                var isKnee = kind.EndsWith("Knee");
                var nextKind = Enum.Parse<HumanoidJointKind>((joint.Side == AnatomicalSide.Left ? "Left" : "Right") + (isKnee ? "Ankle" : "Wrist"));
                var next = skeleton.Joints.SingleOrDefault(j => j.Kind == nextKind);
                if (next is null) continue;
                var direction = Position(next.GlobalBind) - Position(joint.GlobalBind);
                var axis = Vector3.Cross(direction, Vector3.UnitY);
                if (axis.LengthSquared() < 1e-10f) continue;
                axis = ToLocalAxis(joint, Vector3.Normalize(axis) * (isKnee ? -1 : 1));
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
            // Legacy reduced test mechanisms may omit the distal measurement child. Their
            // historical zero remains the only available adapter state; complete source rigs
            // are measured geometrically and never expose this fallback publicly.
            HumanoidPoseSemantics.TryMeasure(skeleton, skeleton.Joints.Select(j => j.GlobalBind).ToArray(),
                input.Joint, out var native);
            Quaternion rotation;
            if (joint is AnatomicalHingeInterface hinge)
            {
                var restCorrection = HingeRestCorrection(skeleton, input.Joint, native.FlexionDegrees);
                rotation = Quaternion.Concatenate(restCorrection, Rotate(hinge.Axis, solved.FlexionDegrees));
            }
            else
            {
                // Solve the absolute anatomical direction directly. This deliberately does not
                // apply equal deltas to unlike source rests.
                var sign = input.Joint.ToString().StartsWith("Left") ? 1 : -1;
                var ball = (AnatomicalBallInterface)joint;
                var q = BallSwing(skeleton, input.Joint, solved.FlexionDegrees, solved.AbductionDegrees);
                rotation = Quaternion.Concatenate(Rotate(ball.TwistAxis, sign * (solved.TwistDegrees - native.TwistDegrees)), q);
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
        var semanticResiduals = new List<SemanticPoseResidual>();
        foreach (var target in states)
        {
            if (!HumanoidPoseSemantics.TryMeasure(skeleton, globals, target.Joint, out var actual))
                continue;
            semanticResiduals.Add(new SemanticPoseResidual(target.Joint,
                actual.FlexionDegrees - target.FlexionDegrees,
                actual.AbductionDegrees - target.AbductionDegrees,
                actual.TwistDegrees - target.TwistDegrees));
        }
        return new(new(skeleton, shapeRevision, state, globals, states.ToArray(), residuals.ToArray(), semanticResiduals.ToArray()), diagnostics.AsReadOnly(), projections.AsReadOnly());
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
            var rotation = j.LocalRest.Rotation;
            if (!float.IsFinite(rotation.X) || !float.IsFinite(rotation.Y) || !float.IsFinite(rotation.Z) || !float.IsFinite(rotation.W) ||
                Math.Abs(rotation.LengthSquared() - 1) > .0001f) return "Rest-frame rotation must be finite and normalized.";
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
        // System.Numerics stores transforms as float; at human-scale millimetre translations
        // the representable step is larger than 0.0001 mm. Keep this consistent with the
        // public kinematic residual contract instead of rejecting valid decomposed rest frames.
        return Values(a).Zip(Values(b)).All(x => float.IsFinite(x.First) && float.IsFinite(x.Second) && Math.Abs(x.First - x.Second) <= LinearToleranceMm);
    }
    private static Quaternion Rotate(Vector3 axis, double degrees) => Quaternion.CreateFromAxisAngle(axis, (float)(degrees * Math.PI / 180));
    private static Quaternion HingeRestCorrection(HumanoidSkeleton skeleton, HumanoidJointKind kind, double nativeFlexionDegrees)
    {
        if (nativeFlexionDegrees < 1e-6) return Quaternion.Identity;
        var jointIndex = skeleton.Joints.ToList().FindIndex(joint => joint.Kind == kind);
        var joint = skeleton.Joints[jointIndex];
        if (joint.ParentIndex is not int parent) return Quaternion.Identity;
        var childName = (kind.ToString().StartsWith("Left") ? "Left" : "Right") +
            (kind.ToString().EndsWith("Elbow") ? "Wrist" : "Ankle");
        var child = skeleton.Joints.ToList().FindIndex(candidate => candidate.Kind.ToString() == childName);
        if (child < 0) return Quaternion.Identity;
        var proximal = Vector3.Normalize(Position(joint.GlobalBind) - Position(skeleton.Joints[parent].GlobalBind));
        var distal = Vector3.Normalize(Position(skeleton.Joints[child].GlobalBind) - Position(joint.GlobalBind));
        var axis = Vector3.Cross(distal, proximal);
        if (axis.LengthSquared() < 1e-10f) return Quaternion.Identity;
        return Rotate(ToLocalAxis(joint, Vector3.Normalize(axis)), nativeFlexionDegrees);
    }
    private static Quaternion BallSwing(HumanoidSkeleton skeleton, HumanoidJointKind kind,
        double flexionDegrees, double abductionDegrees)
    {
        var jointIndex = skeleton.Joints.ToList().FindIndex(joint => joint.Kind == kind);
        var joint = skeleton.Joints[jointIndex];
        var childName = (kind.ToString().StartsWith("Left") ? "Left" : "Right") +
            (kind.ToString().EndsWith("Shoulder") ? "Elbow" : "Knee");
        var child = skeleton.Joints.ToList().FindIndex(candidate => candidate.Kind.ToString() == childName);
        if (child < 0)
        {
            var sign = kind.ToString().StartsWith("Left") ? 1f : -1f;
            var fallback = ToLocalAxis(joint, Vector3.UnitX) * (float)flexionDegrees +
                ToLocalAxis(joint, Vector3.UnitY) * (float)(sign * abductionDegrees);
            var fallbackAngle = fallback.Length();
            return fallbackAngle == 0 ? Quaternion.Identity : Rotate(fallback / fallbackAngle, fallbackAngle);
        }
        var source = Vector3.Normalize(Position(skeleton.Joints[child].GlobalBind) - Position(joint.GlobalBind));
        var flexion = flexionDegrees * Math.PI / 180d;
        var abduction = abductionDegrees * Math.PI / 180d;
        var side = kind.ToString().StartsWith("Left") ? -1d : 1d;
        var target = Vector3.Normalize(new((float)(side * Math.Cos(flexion) * Math.Sin(abduction)),
            (float)Math.Sin(flexion), (float)(-Math.Cos(flexion) * Math.Cos(abduction))));
        var dot = Math.Clamp(Vector3.Dot(source, target), -1f, 1f);
        if (dot > 1 - 1e-7f) return Quaternion.Identity;
        var axis = Vector3.Cross(source, target);
        if (axis.LengthSquared() < 1e-10f)
            axis = Math.Abs(source.X) < .9f ? Vector3.Cross(source, Vector3.UnitX) : Vector3.Cross(source, Vector3.UnitY);
        return Rotate(ToLocalAxis(joint, Vector3.Normalize(axis)), Math.Acos(dot) * 180d / Math.PI);
    }
    private static Vector3 ToLocalAxis(HumanoidJoint joint, Vector3 canonicalAxis)
    {
        var local = Vector3.TransformNormal(canonicalAxis, joint.InverseBind);
        if (local.LengthSquared() < 1e-10f) throw new InvalidDataException("Degenerate adopted joint axis: " + joint.Kind);
        return Vector3.Normalize(local);
    }
    private static double Square(double x) => x * x;
    internal static Vector3 Position(Matrix4x4 m) => new(m.M41, m.M42, m.M43);
}
