using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Humanoid;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Humanoid;

namespace Aetheris.Kernel.Firmament.Garment;

public sealed record GarmentFigureBinding(string Figure, HumanoidGameplayBody Body, string? ArtifactHash = null);
public sealed record GarmentFigureFitEvidence(string Identity, string Figure, string? ArtifactHash,
    HumanoidFigureFit Measurements)
{
    public string Pose { get; init; } = "Rest";
}

/// <summary>A figure-bound Concept publication. Values are measured once at authoring time,
/// then consumed by the ordinary Feature, point, Profile and placement authorities.</summary>
internal static class GarmentFigureAuthoring
{
    public static string Expand(string source, GarmentFigureBinding? binding, List<GarmentFigureFitEvidence> evidence,
        FirmamentPoseDocument posing)
    {
        var tokens = FirmamentLanguageAnalysisService.Lex(source).Where(token => token.Shape != "comment").ToArray();
        var changes = new List<(int Start, int Length)>();
        var publications = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index + 3 < tokens.Length; index++)
        {
            if (tokens[index].Text != "Concept" || tokens[index + 1].Text != "Struct")
            {
                continue;
            }
            string owner = tokens[index + 2].Text;
            int open = index + 3;
            while (open < tokens.Length && tokens[open].Text != "{")
            {
                open++;
            }
            int close = Closing(open);
            int depth = 1;
            for (int child = open + 1; child < close; child++)
            {
                if (tokens[child].Text == "{")
                {
                    depth++;
                }
                if (tokens[child].Text == "}")
                {
                    depth--;
                }
                if (depth != 1 || tokens[child].Text != "FigureFit")
                {
                    continue;
                }
                if (binding is null)
                {
                    GarmentAuthoring.Fail("garment-fit-body-required", "Bind FigureFit with --body gameplay-body.json, or supply a GarmentFigureBinding.");
                }
                if (child + 2 >= close || tokens[child + 1].Shape != "identifier" || tokens[child + 2].Text != "{")
                {
                    GarmentAuthoring.Fail("garment-fit-syntax", "Expected FigureFit name { ... } inside Concept Struct.");
                }
                int end = Closing(child + 2);
                string identity = owner + "." + tokens[child + 1].Text;
                string body = source[(tokens[child + 2].Start + 1)..tokens[end].Start];
                var fields = GarmentAuthoring.Fields(body, ["Figure", "Pose", "ChestHeight", "HipHeight"]);
                if (fields.GetValueOrDefault("Figure") != binding!.Figure)
                {
                    GarmentAuthoring.Fail("garment-fit-binding-mismatch", identity + " must name the supplied figure.");
                }
                double? chest = fields.TryGetValue("ChestHeight", out string? chestText) ? Length(chestText) : null;
                double? hip = fields.TryGetValue("HipHeight", out string? hipText) ? Length(hipText) : null;
                HumanoidFigureFit fit;
                string poseName = fields.GetValueOrDefault("Pose", "Rest");
                var recipe = posing.Resolve(poseName, binding.Figure);
                try
                {
                    SolvedHumanoidPose? pose = recipe.Joints.Count == 0 && recipe.RestOverrides.Count == 0
                        ? null : HumanoidPoseAuthoring.Solve(recipe, binding.Body);
                    fit = HumanoidFigureFitting.Measure(binding.Body, chest, hip, pose);
                }
                catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
                {
                    GarmentAuthoring.Fail("garment-fit-measurement-invalid", exception.Message);
                    return source;
                }
                evidence.Add(new(identity, binding.Figure, binding.ArtifactHash, fit) { Pose = poseName });
                Publish("shoulderWidth", Math.Abs(fit.RightShoulder.X - fit.LeftShoulder.X));
                Publish("shoulderHeight", (fit.LeftShoulder.Z + fit.RightShoulder.Z) / 2);
                Publish("neckHeight", fit.Neck.Z);
                Publish("centerX", fit.Neck.X);
                Publish("centerY", fit.Neck.Y);
                Publish("chestHeight", fit.Chest.HeightMm);
                Publish("hipHeight", fit.Hip.HeightMm);
                Publish("chestCircumference", fit.Chest.CircumferenceMm);
                Publish("hipCircumference", fit.Hip.CircumferenceMm);
                Publish("torsoCircumference", Math.Max(fit.Chest.CircumferenceMm, fit.Hip.CircumferenceMm));
                Publish("frontY", Math.Max(fit.Chest.MaximumY, fit.Hip.MaximumY));
                Publish("backY", Math.Min(fit.Chest.MinimumY, fit.Hip.MinimumY));
                changes.Add((tokens[child].Start, tokens[end].Start + tokens[end].Length - tokens[child].Start));
                child = end;

                void Publish(string name, double value)
                {
                    if (!publications.TryAdd(identity + "." + name, value.ToString("R", CultureInfo.InvariantCulture) + "mm"))
                    {
                        GarmentAuthoring.Fail("garment-fit-name-duplicate", identity);
                    }
                }
            }
            index = close;
        }
        foreach (var change in changes.OrderByDescending(change => change.Start))
        {
            source = source.Remove(change.Start, change.Length);
        }
        foreach (var publication in publications)
        {
            source = Regex.Replace(source, $@"(?<![\w.]){Regex.Escape(publication.Key)}(?![\w.])", publication.Value);
        }
        foreach (var fit in evidence)
        {
            if (Regex.IsMatch(source, $@"\b{Regex.Escape(fit.Identity)}\."))
            {
                GarmentAuthoring.Fail("garment-fit-member-unknown", fit.Identity);
            }
        }
        return source;

        int Closing(int open)
        {
            int depth = 0;
            for (int token = open; token < tokens.Length; token++)
            {
                if (tokens[token].Text == "{")
                {
                    depth++;
                }
                if (tokens[token].Text == "}")
                {
                    depth--;
                    if (depth == 0)
                    {
                        return token;
                    }
                }
            }
            GarmentAuthoring.Fail("garment-fit-syntax", "Unclosed Concept block.");
            return tokens.Length;
        }
    }

    private static double Length(string text)
    {
        if (!FirmamentNumericEvaluation.TryEvaluateScalar(text, out double value, out string unit) || unit != "mm")
        {
            GarmentAuthoring.Fail("garment-fit-unit-invalid", "Section height must be a Length: " + text);
        }
        return value;
    }
}
