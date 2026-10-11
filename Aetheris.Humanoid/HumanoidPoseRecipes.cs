namespace Aetheris.Humanoid;

public enum HumanoidPosePreset { Rest, APose, TPose }

/// <summary>Reusable absolute anatomical pose recipes. The original bind skeleton remains authoritative.</summary>
public static class HumanoidPoseRecipes
{
    public static IReadOnlyList<AnatomicalJointRequest> Preset(HumanoidPosePreset preset)
    {
        if (preset == HumanoidPosePreset.Rest)
        {
            return [];
        }
        if (preset is not (HumanoidPosePreset.APose or HumanoidPosePreset.TPose))
        {
            throw new ArgumentOutOfRangeException(nameof(preset));
        }
        return Array.AsReadOnly(CanonicalHumanoidRestPose.Requests.Select(request =>
        {
            if (preset == HumanoidPosePreset.TPose
                && request.Joint is HumanoidJointKind.LeftShoulder or HumanoidJointKind.RightShoulder)
            {
                return request with { AbductionDegrees = 90 };
            }
            return request;
        }).ToArray());
    }

    public static IReadOnlyList<AnatomicalJointRequest> Blend(HumanoidGameplayBody body,
        IReadOnlyList<AnatomicalJointRequest> from, IReadOnlyList<AnatomicalJointRequest> to, double fraction)
    {
        if (!double.IsFinite(fraction) || fraction is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(fraction));
        }
        var first = from.ToDictionary(request => request.Joint);
        var second = to.ToDictionary(request => request.Joint);
        var rest = body.Skeleton.Joints.Select(joint => joint.GlobalBind).ToArray();
        return Array.AsReadOnly(first.Keys.Union(second.Keys).Order().Select(joint =>
        {
            var start = At(first, joint);
            var end = At(second, joint);
            return new AnatomicalJointRequest(joint,
                start.FlexionDegrees + (end.FlexionDegrees - start.FlexionDegrees) * fraction,
                start.AbductionDegrees + (end.AbductionDegrees - start.AbductionDegrees) * fraction,
                start.TwistDegrees + (end.TwistDegrees - start.TwistDegrees) * fraction);
        }).ToArray());

        AnatomicalJointRequest At(Dictionary<HumanoidJointKind, AnatomicalJointRequest> requests, HumanoidJointKind joint)
        {
            if (requests.TryGetValue(joint, out var requested))
            {
                return requested;
            }
            var measured = HumanoidPoseSemantics.Measure(body.Skeleton, rest, joint);
            return new(joint, measured.FlexionDegrees, measured.AbductionDegrees, measured.TwistDegrees);
        }
    }
}
