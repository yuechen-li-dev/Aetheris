using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Humanoid;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Humanoid;

public sealed record FirmamentHumanoidPose(string Name, string Figure,
    IReadOnlyList<AnatomicalJointRequest> Joints)
{
    public IReadOnlyList<FirmamentJointOverride> RestOverrides { get; init; } = [];
}
public sealed record FirmamentJointOverride(HumanoidJointKind Joint, double? Flexion, double? Abduction, double? Twist);
public sealed record FirmamentPoseTransition(string Name, string From, string To,
    double DurationSeconds, string Easing);
public sealed record FirmamentPoseDocument([property: System.Text.Json.Serialization.JsonIgnore] string RemainingSource,
    IReadOnlyDictionary<string, FirmamentHumanoidPose> Poses,
    IReadOnlyDictionary<string, FirmamentPoseTransition> Transitions)
{
    public FirmamentHumanoidPose Resolve(string name, string figure)
    {
        if (Enum.TryParse<HumanoidPosePreset>(name, out var preset) && Enum.IsDefined(preset))
        {
            return new(name, figure, HumanoidPoseRecipes.Preset(preset));
        }
        if (!Poses.TryGetValue(name, out var pose) || pose.Figure != figure)
        {
            throw new InvalidDataException("humanoid-pose-reference: Unknown pose or mismatched figure: " + name);
        }
        return pose;
    }
}

/// <summary>Finite named pose recipes lowered to the existing anatomical solver. No second skeleton or skinning path.</summary>
public static class HumanoidPoseAuthoring
{
    private sealed record Declaration(string Name, string Figure, string From,
        IReadOnlyDictionary<HumanoidJointKind, IReadOnlyDictionary<string, double>> Overrides);

    public static FirmamentPoseDocument Parse(string original)
    {
        string source = FirmamentSourceSpelling.Normalize(original);
        var tokens = FirmamentLanguageAnalysisService.Lex(source).Where(token => token.Shape != "comment").ToArray();
        var poses = new Dictionary<string, Declaration>(StringComparer.Ordinal);
        var transitions = new Dictionary<string, FirmamentPoseTransition>(StringComparer.Ordinal);
        var removals = new List<(int Start, int Length)>();
        int depth = 0;
        for (int index = 0; index < tokens.Length; index++)
        {
            string kind = tokens[index].Text;
            if (depth == 0 && kind is "Pose" or "PoseTransition")
            {
                if (index + 2 >= tokens.Length || tokens[index + 1].Shape != "identifier" || tokens[index + 2].Text != "{")
                {
                    Fail("humanoid-pose-syntax", "Expected " + kind + " name { ... }.");
                }
                string name = tokens[index + 1].Text;
                int end = index + 3;
                int blockDepth = 1;
                for (; end < tokens.Length; end++)
                {
                    if (tokens[end].Text == "{")
                    {
                        blockDepth++;
                    }
                    if (tokens[end].Text == "}" && --blockDepth == 0)
                    {
                        break;
                    }
                }
                if (end == tokens.Length)
                {
                    Fail("humanoid-pose-syntax", "Unclosed " + name);
                }
                string body = source[(tokens[index + 2].Start + 1)..tokens[end].Start];
                if (poses.ContainsKey(name) || transitions.ContainsKey(name) || Enum.TryParse<HumanoidPosePreset>(name, out _))
                {
                    Fail("humanoid-pose-name-duplicate", name);
                }
                if (kind == "Pose")
                {
                    poses.Add(name, ReadPose(name, body));
                }
                else
                {
                    var fields = Fields(body, ["From", "To", "Duration", "Easing"]);
                    string easing = fields.GetValueOrDefault("Easing", "SmoothStep");
                    if (easing is not ("SmoothStep" or "Linear"))
                    {
                        Fail("humanoid-pose-easing", easing);
                    }
                    var duration = Regex.Match(Required(fields, "Duration"), @"^([0-9]+(?:\.[0-9]+)?)\s*s$");
                    double seconds = 0;
                    if (!duration.Success || !double.TryParse(duration.Groups[1].Value, CultureInfo.InvariantCulture, out seconds)
                        || seconds is < .016666 or > 10)
                    {
                        Fail("humanoid-pose-duration", "Duration requires 1/60..10 seconds with an explicit s suffix.");
                    }
                    transitions.Add(name, new(name, Required(fields, "From"), Required(fields, "To"), seconds, easing));
                }
                removals.Add((tokens[index].Start, tokens[end].Start + tokens[end].Length - tokens[index].Start));
                index = end;
                continue;
            }
            if (kind == "{")
            {
                depth++;
            }
            if (kind == "}")
            {
                depth--;
            }
        }
        var resolved = new Dictionary<string, FirmamentHumanoidPose>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        foreach (string name in poses.Keys)
        {
            Resolve(name);
        }
        foreach (var transition in transitions.Values)
        {
            if (!resolved.TryGetValue(transition.From, out var from) || !resolved.TryGetValue(transition.To, out var to)
                || from.Figure != to.Figure)
            {
                Fail("humanoid-pose-transition-binding", "Transition endpoints must be named poses of the same figure.");
            }
        }
        foreach (var removal in removals.OrderByDescending(removal => removal.Start))
        {
            source = source.Remove(removal.Start, removal.Length);
        }
        return new(source, new System.Collections.ObjectModel.ReadOnlyDictionary<string, FirmamentHumanoidPose>(resolved),
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, FirmamentPoseTransition>(transitions));

        FirmamentHumanoidPose Resolve(string name)
        {
            if (resolved.TryGetValue(name, out var existing))
            {
                return existing;
            }
            if (!visiting.Add(name) || visiting.Count > 32)
            {
                Fail("humanoid-pose-cycle", "Pose inheritance must be acyclic and at most 32 levels deep.");
            }
            var declaration = poses[name];
            IReadOnlyList<AnatomicalJointRequest> inherited;
            IReadOnlyList<FirmamentJointOverride> inheritedRest = [];
            if (Enum.TryParse<HumanoidPosePreset>(declaration.From, out var preset) && Enum.IsDefined(preset))
            {
                inherited = HumanoidPoseRecipes.Preset(preset);
            }
            else
            {
                if (!poses.ContainsKey(declaration.From))
                {
                    Fail("humanoid-pose-reference", declaration.From);
                }
                var parent = Resolve(declaration.From);
                if (parent.Figure != declaration.Figure)
                {
                    Fail("humanoid-pose-figure-mismatch", name);
                }
                inherited = parent.Joints;
                inheritedRest = parent.RestOverrides;
            }
            var joints = inherited.ToDictionary(joint => joint.Joint);
            var restOverrides = inheritedRest.ToDictionary(joint => joint.Joint);
            foreach (var joint in declaration.Overrides)
            {
                if (joints.TryGetValue(joint.Key, out var basis))
                {
                    joints[joint.Key] = new(joint.Key,
                        joint.Value.GetValueOrDefault("Flexion", basis.FlexionDegrees),
                        joint.Value.GetValueOrDefault("Abduction", basis.AbductionDegrees),
                        joint.Value.GetValueOrDefault("Twist", basis.TwistDegrees));
                }
                else
                {
                    var prior = restOverrides.GetValueOrDefault(joint.Key) ?? new FirmamentJointOverride(joint.Key, null, null, null);
                    restOverrides[joint.Key] = new(joint.Key,
                        Override("Flexion", prior.Flexion), Override("Abduction", prior.Abduction), Override("Twist", prior.Twist));

                    double? Override(string field, double? previous)
                    {
                        return joint.Value.TryGetValue(field, out double value) ? value : previous;
                    }
                }
            }
            var result = new FirmamentHumanoidPose(name, declaration.Figure, Array.AsReadOnly(joints.Values.OrderBy(joint => joint.Joint).ToArray()))
            {
                RestOverrides = Array.AsReadOnly(restOverrides.Values.OrderBy(joint => joint.Joint).ToArray()),
            };
            visiting.Remove(name);
            resolved.Add(name, result);
            return result;
        }
    }

    public static SolvedHumanoidPose Solve(FirmamentHumanoidPose pose, HumanoidGameplayBody body)
    {
        var solved = body.Solve(pose.Name, Requests(pose, body));
        if (!solved.IsSolved)
        {
            Fail("humanoid-pose-solve", string.Join("; ", solved.Diagnostics.Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message)));
        }
        return solved.Pose!;
    }

    public static IReadOnlyList<AnatomicalJointRequest> Requests(FirmamentHumanoidPose pose, HumanoidGameplayBody body)
    {
        var result = pose.Joints.ToList();
        var globals = body.Skeleton.Joints.Select(joint => joint.GlobalBind).ToArray();
        foreach (var joint in pose.RestOverrides)
        {
            var rest = HumanoidPoseSemantics.Measure(body.Skeleton, globals, joint.Joint);
            result.Add(new(joint.Joint, joint.Flexion ?? rest.FlexionDegrees,
                joint.Abduction ?? rest.AbductionDegrees, joint.Twist ?? rest.TwistDegrees));
        }
        return Array.AsReadOnly(result.OrderBy(joint => joint.Joint).ToArray());
    }

    private static Declaration ReadPose(string name, string body)
    {
        var reader = new FirmamentBlockReader(body, Fail, "humanoid-pose-syntax");
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var joints = new Dictionary<HumanoidJointKind, IReadOnlyDictionary<string, double>>();
        while (!reader.End)
        {
            if (reader.Optional(";"))
            {
                continue;
            }
            if (reader.Optional("Joint"))
            {
                string jointName = reader.Identifier();
                if (!Enum.TryParse<HumanoidJointKind>(jointName, out var joint) || !Enum.IsDefined(joint))
                {
                    Fail("humanoid-pose-joint", jointName);
                }
                var angles = Fields(reader.Block(), ["Flexion", "Abduction", "Twist"]);
                var values = angles.ToDictionary(pair => pair.Key, pair => Angle(pair.Value));
                if (values.Count == 0 || !joints.TryAdd(joint, values))
                {
                    Fail("humanoid-pose-joint-duplicate", jointName);
                }
                continue;
            }
            string field = reader.Identifier();
            reader.Expect(":");
            if (field is not ("Figure" or "From") || !fields.TryAdd(field, reader.Value()))
            {
                Fail("humanoid-pose-field", field);
            }
        }
        return new(name, Required(fields, "Figure"), fields.GetValueOrDefault("From", "Rest"), joints);
    }

    private static Dictionary<string, string> Fields(string body, string[] admitted)
    {
        var reader = new FirmamentBlockReader(body, Fail, "humanoid-pose-syntax");
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        while (!reader.End)
        {
            if (reader.Optional(";"))
            {
                continue;
            }
            string name = reader.Identifier();
            reader.Expect(":");
            if (!admitted.Contains(name) || !fields.TryAdd(name, reader.Value()))
            {
                Fail("humanoid-pose-field", name);
            }
        }
        return fields;
    }

    private static double Angle(string text)
    {
        if (!FirmamentV2FeatureExpansion.TryEvaluateScalar(text, out double value, out string unit)
            || unit != "deg" || !double.IsFinite(value))
        {
            Fail("humanoid-pose-angle", "Anatomical angles require finite degree-valued expressions: " + text);
        }
        return value;
    }

    private static string Required(Dictionary<string, string> fields, string field)
    {
        if (!fields.TryGetValue(field, out string? value))
        {
            Fail("humanoid-pose-field-required", field);
        }
        return value!;
    }

    private static void Fail(string code, string message)
    {
        throw new InvalidDataException(code + ": " + message);
    }
}
