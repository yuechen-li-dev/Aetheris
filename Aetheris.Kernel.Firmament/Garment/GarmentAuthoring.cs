using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aetheris.Cloth3D;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Garment;

public sealed record GarmentDiagnostic(string Code, string Message, string SourceIdentity);
public sealed record GarmentPanel(string Name, string Identity, ResolvedProfile2D Profile,
    Vector3 Origin, Vector3 U, Vector3 V, Vector2 Grain, ImmutableArray<string> Pins,
    float? WrapRadius, float WrapAngle, float? WrapTopRadius, Vector3? WrapTopOrigin, float WrapTopAngle);
public sealed record GarmentStitch(string Name, string A, string B, bool Reversed, float Ease, float Compliance);
public sealed record GarmentSource(string Name, string SourceIdentity, string SourceHash,
    ClothMaterial3D Fabric, float MeshSize, ImmutableArray<GarmentPanel> Panels,
    ImmutableArray<GarmentStitch> Stitches, string? Figure, string? Pose,
    IReadOnlyList<FirmamentV2FeatureInvocation> Features,
    IReadOnlyList<FirmamentV2CanonicalPatternDecl> Patterns, float BodyClearance);
public sealed record GarmentParseResult(GarmentSource? Source, IReadOnlyList<GarmentDiagnostic> Diagnostics)
{
    public bool IsSuccess => Source is not null && Diagnostics.Count == 0;
}

/// <summary>Garment owns panel arrangement and distributed stitch semantics. Profiles,
/// pure Feature recipes, finite Pattern expansion and lexical analysis retain their existing owners.</summary>
public static class GarmentAuthoring
{
    public static bool HasRoot(string source)
    {
        var tokens = FirmamentLanguageAnalysisService.Lex(source).Where(token => token.Shape != "comment").ToArray();
        int depth = 0;
        for (int i = 0; i + 1 < tokens.Length; i++)
        {
            if (depth == 0 && tokens[i].Text == "Garment" && tokens[i + 1].Shape == "identifier")
            {
                return true;
            }
            if (tokens[i].Text == "{")
            {
                depth++;
            }
            if (tokens[i].Text == "}")
            {
                depth--;
            }
        }
        return false;
    }

    public static GarmentParseResult Parse(string source, string sourceIdentity = "<memory>")
    {
        try
        {
            return ParseCore(source, sourceIdentity);
        }
        catch (GarmentAuthoringException exception)
        {
            return new(null, [new(exception.Code, exception.Message, sourceIdentity)]);
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
        {
            return new(null, [new("garment-value-invalid", exception.Message, sourceIdentity)]);
        }
    }

    private static GarmentParseResult ParseCore(string original, string identity)
    {
        var selection = FirmamentFrontendSchemas.Select(original);
        if (!selection.IsSuccess)
        {
            return new(null, selection.Diagnostics.Select(message => new GarmentDiagnostic(
                message.Split(':')[0], message, identity)).ToArray());
        }
        if (selection.Schema != FirmamentFrontendSchema.Garment)
        {
            Fail("garment-schema-required", "Begin this document with schema Garment.");
        }
        string source = FirmamentSourceSpelling.Normalize(selection.Source);
        var characters = source.ToCharArray();
        foreach (var token in FirmamentLanguageAnalysisService.Lex(source).Where(token => token.Shape == "comment"))
        {
            Array.Fill(characters, ' ', token.Start, token.Length);
        }
        source = new string(characters);
        var expansionDiagnostics = new List<string>();
        var features = FirmamentV2FeatureExpansion.Expand(source, expansionDiagnostics);
        if (features is null || expansionDiagnostics.Count > 0)
        {
            return ExpansionFailure(expansionDiagnostics, identity);
        }
        var patterns = CanonicalStaticAuthoring.Expand(features.Source, expansionDiagnostics);
        if (patterns is null || expansionDiagnostics.Count > 0)
        {
            return ExpansionFailure(expansionDiagnostics, identity);
        }
        source = patterns.Source;
        var reader = new Reader(source);
        while (!reader.End && reader.Peek != "Garment")
        {
            if (reader.Optional(";"))
            {
                continue;
            }
            if (reader.Peek is not ("Point2" or "Line2" or "CubicBezier2" or "Circle2" or "Rect2"
                or "RoundedRect2" or "Polygon2" or "Profile" or "Curve2" or "Ellipse2Guide"))
            {
                Fail("garment-declaration-unsupported", "Unsupported garment module declaration: " + reader.Peek);
            }
            reader.SkipDeclaration();
        }
        reader.Expect("Garment");
        string name = reader.Identifier();
        string bodyText = reader.Block();
        reader.Optional(";");
        if (!reader.End)
        {
            Fail("garment-multiple-roots", "One Garment root is required, with geometry recipes before it.");
        }
        var body = new Reader(bodyText);
        var panels = ImmutableArray.CreateBuilder<GarmentPanel>();
        var stitches = ImmutableArray.CreateBuilder<GarmentStitch>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        ClothMaterial3D? fabric = null;
        float meshSize = .06f;
        bool hasMeshSize = false;
        string? figure = null;
        string? pose = null;
        float bodyClearance = .004f;
        while (!body.End)
        {
            if (body.Optional(";"))
            {
                continue;
            }
            string kind = body.Take();
            if (kind == "MeshSize")
            {
                if (hasMeshSize)
                {
                    Fail("garment-duplicate-field", "meshSize is repeated.");
                }
                hasMeshSize = true;
                body.Expect(":");
                meshSize = Length(body.Value());
                if (meshSize is < .015f or > .15f)
                {
                    Fail("garment-mesh-budget", "meshSize must be between 15mm and 150mm.");
                }
                continue;
            }
            if (kind == "Drape")
            {
                if (figure is not null)
                {
                    Fail("garment-duplicate-drape", "Declare only one Drape binding.");
                }
                var fields = Fields(body.Block(), ["Figure", "Pose", "Clearance"]);
                figure = Required(fields, "Figure");
                pose = fields.GetValueOrDefault("Pose", "Rest");
                bodyClearance = Length(fields.GetValueOrDefault("Clearance", "4mm"));
                if (bodyClearance is < 0 or > .03f)
                {
                    Fail("garment-body-clearance", "clearance must be between 0mm and 30mm.");
                }
                if (pose != "Rest")
                {
                    Fail("garment-pose-unsupported", "This drape milestone admits the loaded figure's Rest pose only.");
                }
                continue;
            }
            if (kind == "Interface")
            {
                body.Expect("<");
                body.Expect("Stitch");
                body.Expect(">");
            }
            string declarationName = body.Identifier();
            if (!names.Add(declarationName))
            {
                Fail("garment-duplicate-name", "Repeated garment declaration: " + declarationName);
            }
            string declarationBody = body.Block();
            switch (kind)
            {
                case "Fabric":
                {
                    if (fabric is not null)
                    {
                        Fail("garment-fabric-unsupported", "This milestone admits one shared fabric per garment.");
                    }
                    var fields = Fields(declarationBody, ["ArealDensity", "Thickness", "WarpCompliance", "WeftCompliance", "DiagonalCompliance", "BendCompliance"]);
                    fabric = new()
                    {
                        ArealDensity = Scalar(fields.GetValueOrDefault("ArealDensity", "0.2")),
                        Thickness = Length(fields.GetValueOrDefault("Thickness", "2mm")),
                        WarpCompliance = Scalar(fields.GetValueOrDefault("WarpCompliance", "0.000001")),
                        WeftCompliance = Scalar(fields.GetValueOrDefault("WeftCompliance", "0.000001")),
                        DiagonalCompliance = Scalar(fields.GetValueOrDefault("DiagonalCompliance", "0.000004")),
                        BendCompliance = Scalar(fields.GetValueOrDefault("BendCompliance", "1000")),
                    };
                    break;
                }
                case "Panel":
                {
                    var fields = Fields(declarationBody, ["Profile", "Origin", "U", "V", "Grain", "Pin", "PatternIdentity", "WrapRadius",
                        "WrapAngle", "WrapTopRadius", "WrapTopOrigin", "WrapTopAngle"]);
                    string profileName = Required(fields, "Profile");
                    var profile = ProfileAuthoringParser.ResolveNamedProfile(source, profileName, out var diagnostics);
                    if (profile is null || diagnostics.Count > 0)
                    {
                        Fail("garment-profile-invalid", profileName + ": " + string.Join("; ", diagnostics));
                    }
                    var validation = ResolvedProfile2DValidator.Validate(profile!);
                    if (!validation.IsValid)
                    {
                        Fail("garment-profile-invalid", string.Join("; ", validation.Diagnostics));
                    }
                    Vector3 u = Vector(fields.GetValueOrDefault("U", "[1, 0, 0]"), false);
                    Vector3 v = Vector(fields.GetValueOrDefault("V", "[0, 0, 1]"), false);
                    if (MathF.Abs(u.LengthSquared() - 1) > 1e-5f || MathF.Abs(v.LengthSquared() - 1) > 1e-5f
                        || MathF.Abs(Vector3.Dot(u, v)) > 1e-5f)
                    {
                        Fail("garment-panel-frame-invalid", "Panel u and v must be orthonormal; placement cannot scale the rest pattern.");
                    }
                    float[] grainValues = List(fields.GetValueOrDefault("Grain", "[0, 1]")).Select(Scalar).ToArray();
                    if (grainValues.Length != 2)
                    {
                        Fail("garment-grain-invalid", "grain requires a unit material-space Vector2.");
                    }
                    Vector2 grain = new(grainValues[0], grainValues[1]);
                    if (MathF.Abs(grain.LengthSquared() - 1) > 1e-5f)
                    {
                        Fail("garment-grain-invalid", "grain must be unit length.");
                    }
                    float? wrapRadius = fields.TryGetValue("WrapRadius", out string? radius) ? Length(radius) : null;
                    if (wrapRadius is <= 0)
                    {
                        Fail("garment-wrap-invalid", "wrapRadius must be positive.");
                    }
                    float wrapAngle = Angle(fields.GetValueOrDefault("WrapAngle", "0deg"));
                    float? topRadius = fields.TryGetValue("WrapTopRadius", out string? top) ? Length(top) : null;
                    Vector3? topOrigin = fields.TryGetValue("WrapTopOrigin", out string? topPosition) ? Vector(topPosition, true) : null;
                    float topAngle = Angle(fields.GetValueOrDefault("WrapTopAngle", fields.GetValueOrDefault("WrapAngle", "0deg")));
                    if (topRadius is <= 0 || (wrapRadius is null && (topRadius is not null || topOrigin is not null)))
                        Fail("garment-wrap-invalid", "Lofted wrap placement requires a positive base wrapRadius.");
                    panels.Add(new(declarationName, fields.GetValueOrDefault("PatternIdentity", declarationName), profile!,
                        Vector(Required(fields, "Origin"), true), u, v, grain,
                        fields.TryGetValue("Pin", out string? pin) ? List(pin).ToImmutableArray() : [], wrapRadius,
                        wrapAngle, topRadius, topOrigin, topAngle));
                    break;
                }
                case "Interface":
                {
                    var fields = Fields(declarationBody, ["A", "B", "Orientation", "Ease", "Compliance"]);
                    string orientation = fields.GetValueOrDefault("Orientation", "Reversed");
                    if (orientation is not ("Same" or "Reversed"))
                    {
                        Fail("garment-stitch-orientation", "orientation is Same or Reversed.");
                    }
                    float ease = Scalar(fields.GetValueOrDefault("Ease", "0"));
                    float compliance = Scalar(fields.GetValueOrDefault("Compliance", "0.00000001"));
                    if (ease is < 0 or > .25f || compliance < 0)
                    {
                        Fail("garment-stitch-properties", "ease is a 0..0.25 length mismatch allowance; compliance is nonnegative.");
                    }
                    stitches.Add(new(declarationName, Required(fields, "A"), Required(fields, "B"), orientation == "Reversed", ease, compliance));
                    break;
                }
                default:
                    Fail("garment-construct-unsupported", "Unsupported garment construct: " + kind);
                    break;
            }
        }
        if (panels.Count == 0)
        {
            Fail("garment-panel-missing", "A garment needs at least one Panel.");
        }
        if (fabric is null)
        {
            Fail("garment-fabric-missing", "Declare one Fabric with explicit authoring properties.");
        }
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(original)));
        return new(new(name, identity, hash, fabric!, meshSize, panels.ToImmutable(), stitches.ToImmutable(),
            figure, pose, features.Invocations, patterns.Document?.Patterns ?? [], bodyClearance), []);
    }

    private static GarmentParseResult ExpansionFailure(IEnumerable<string> diagnostics, string identity)
    {
        return new(null, diagnostics.Select(message => new GarmentDiagnostic(message.Split(':')[0], message, identity)).ToArray());
    }

    private static Dictionary<string, string> Fields(string text, string[] admitted)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var reader = new Reader(text);
        while (!reader.End)
        {
            if (reader.Optional(";"))
            {
                continue;
            }
            string name = reader.Identifier();
            if (!admitted.Contains(name))
            {
                Fail("garment-field-unknown", "Unknown field: " + name);
            }
            reader.Expect(":");
            if (!result.TryAdd(name, reader.Value()))
            {
                Fail("garment-duplicate-field", "Repeated field: " + name);
            }
        }
        return result;
    }

    private static string Required(Dictionary<string, string> fields, string key)
    {
        if (!fields.TryGetValue(key, out string? value))
        {
            Fail("garment-field-required", "Missing field: " + key);
        }
        return value!;
    }

    private static float Scalar(string value)
    {
        float result = float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (!float.IsFinite(result))
        {
            Fail("garment-nonfinite", "All garment values must be finite.");
        }
        return result;
    }

    private static float Length(string value)
    {
        var match = Regex.Match(value, @"^\s*(?<number>[-+\d.eE]+)\s*(?<unit>mm|m)\s*$", RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            Fail("garment-length-unit", "Length requires an explicit mm or m suffix: " + value);
        }
        return Scalar(match.Groups["number"].Value) * (match.Groups["unit"].Value == "mm" ? .001f : 1);
    }

    private static float Angle(string value)
    {
        if (!value.EndsWith("deg", StringComparison.Ordinal))
        {
            Fail("garment-angle-unit", "Wrap angles require a deg suffix.");
        }
        return Scalar(value[..^3]) * MathF.PI / 180;
    }

    private static Vector3 Vector(string value, bool length)
    {
        if (value.StartsWith("Point3(", StringComparison.Ordinal) && value.EndsWith(')'))
        {
            value = "[" + value[7..^1] + "]";
        }
        float[] coordinates = List(value).Select(item => length ? Length(item) : Scalar(item)).ToArray();
        if (coordinates.Length != 3)
        {
            Fail("garment-vector-invalid", "Expected three vector coordinates.");
        }
        return new(coordinates[0], coordinates[1], coordinates[2]);
    }

    private static string[] List(string value)
    {
        if (!value.StartsWith('[') || !value.EndsWith(']'))
        {
            Fail("garment-list-invalid", "Expected a bracketed list.");
        }
        return value[1..^1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    internal static void Fail(string code, string message) => throw new GarmentAuthoringException(code, message);

    private sealed class Reader
    {
        private readonly string source;
        private readonly (string Text, string Shape, int Start, int Length)[] tokens;
        private int position;

        public Reader(string source)
        {
            this.source = source;
            tokens = FirmamentLanguageAnalysisService.Lex(source).Where(token => token.Shape != "comment")
                .Select(token => (token.Text, token.Shape, token.Start, token.Length)).ToArray();
        }

        public bool End => position >= tokens.Length;
        public string Peek => End ? "<end>" : tokens[position].Text;
        public string Take()
        {
            if (End)
            {
                Fail("garment-syntax", "Unexpected end of garment document.");
            }
            return tokens[position++].Text;
        }
        public bool Optional(string text)
        {
            if (Peek != text)
            {
                return false;
            }
            position++;
            return true;
        }
        public void Expect(string text)
        {
            if (!Optional(text))
            {
                Fail("garment-syntax", "Expected '" + text + "', found '" + Peek + "'.");
            }
        }
        public string Identifier()
        {
            if (End || tokens[position].Shape != "identifier")
            {
                Fail("garment-syntax", "Expected an identifier, found " + Peek);
            }
            return Take();
        }
        public string Block()
        {
            Expect("{");
            int start = tokens[position - 1].Start + 1;
            int depth = 1;
            while (!End)
            {
                var token = tokens[position++];
                if (token.Text == "{")
                {
                    depth++;
                }
                if (token.Text == "}")
                {
                    depth--;
                }
                if (depth == 0)
                {
                    return source[start..token.Start];
                }
            }
            Fail("garment-syntax", "Unclosed garment block.");
            return "";
        }
        public void SkipDeclaration()
        {
            while (!End && Peek != "{")
            {
                Take();
            }
            Block();
            Optional(";");
        }
        public string Value()
        {
            if (End)
            {
                Fail("garment-syntax", "Missing field value.");
            }
            int start = tokens[position].Start;
            int end = start;
            int depth = 0;
            while (!End)
            {
                var token = tokens[position];
                if (depth == 0 && (token.Text == ";" || (position + 1 < tokens.Length && tokens[position + 1].Text == ":")))
                {
                    break;
                }
                if (depth == 0 && end > start && token.Text is "Panel" or "Fabric" or "Interface" or "Drape")
                {
                    break;
                }
                if (token.Text is "[" or "(")
                {
                    depth++;
                }
                if (token.Text is "]" or ")")
                {
                    depth--;
                }
                if (depth < 0)
                {
                    Fail("garment-syntax", "Unbalanced field value.");
                }
                end = token.Start + token.Length;
                position++;
            }
            if (depth != 0 || end == start)
            {
                Fail("garment-syntax", "Missing or unbalanced field value.");
            }
            Optional(";");
            return source[start..end].Trim();
        }
    }
}

internal sealed class GarmentAuthoringException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
