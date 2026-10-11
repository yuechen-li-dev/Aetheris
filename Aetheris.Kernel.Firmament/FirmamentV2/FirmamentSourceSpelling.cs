using System.Text;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>
/// Bounded, case-only aliases at vocabulary positions. Schema identities and
/// authored symbols remain case-sensitive. Unknown scopes are left alone.
/// </summary>
public static class FirmamentSourceSpelling
{
    private sealed record Scope(string Owner, bool AuthoredData, int Parentheses, int Angles);
    private static readonly Dictionary<string, HashSet<string>> Fields = CreateFields();
    private static readonly Dictionary<string, string> Links = new(StringComparer.Ordinal)
    {
        ["Using"] = "using", ["On"] = "on", ["Over"] = "over",
        ["Include"] = "include", ["Expose"] = "expose", ["Bind"] = "bind"
    };

    public static string PreferredField(string name) => name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
    public static string Normalize(string source) => Rewrite(source, preferred: false);
    public static string Prefer(string source) => Rewrite(source, preferred: true);

    private static string Rewrite(string source, bool preferred)
    {
        ArgumentNullException.ThrowIfNull(source);
        var tokens = FirmamentLanguageAnalysisService.Lex(source).Where(t => t.Shape != "comment").ToArray();
        var output = new StringBuilder(source);
        var scopes = new Stack<Scope>();
        var parentheses = 0;
        var angles = 0;
        for (var i = 0; i < tokens.Length; i++)
        {
            var t = tokens[i];
            // Assembly's XML-shaped occurrence scopes are independent of braces.
            if (t.Text == "<" && i + 1 < tokens.Length && tokens[i + 1].Text is "Part" or "Assembly")
            {
                var kind = tokens[i + 1].Text;
                var depth = 1;
                var tagEnd = i + 1;
                while (++tagEnd < tokens.Length && depth > 0)
                {
                    if (tokens[tagEnd].Text == "<") depth++;
                    else if (tokens[tagEnd].Text == ">") depth--;
                }
                tagEnd--;
                // Do not rewrite named generic arguments inside the tag.
                i = tagEnd;
                scopes.Push(new(kind, false, parentheses, angles));
                continue;
            }
            if (t.Text == "<" && i + 2 < tokens.Length && tokens[i + 1].Text == "/" && tokens[i + 2].Text is "Part" or "Assembly")
            {
                if (scopes.Count > 0) scopes.Pop();
                while (i < tokens.Length && tokens[i].Text != ">") i++;
                continue;
            }
            if (t.Text == "(") parentheses++;
            else if (t.Text == ")") parentheses--;
            else if (t.Text == "<") angles++;
            else if (t.Text == ">" && angles > 0) angles--;
            if (t.Text == "{")
            {
                var owner = BlockOwner(tokens, i);
                var authored = (scopes.TryPeek(out var parent) && parent.AuthoredData) || owner is "Record" or "Static" or "Table" or "Stations" or "Legacy";
                // Appearance overrides inherit the material owner's admitted fields.
                if (owner == "with" && scopes.TryPeek(out parent) && parent.Owner is "Part" or "Assembly") owner = "Material";
                // A bound route parameter name is authored; its site fields belong to Bind.
                if (!Fields.ContainsKey(owner) && scopes.TryPeek(out parent) && parent.Owner == "Bind") owner = "RouteSite";
                scopes.Push(new(owner, authored, parentheses, angles));
                continue;
            }
            if (t.Text == "}") { if (scopes.Count > 0) scopes.Pop(); continue; }
            if (t.Shape != "identifier" || scopes.Any(s => s.AuthoredData)) continue;
            var fieldPosition = i + 1 < tokens.Length && tokens[i + 1].Text == ":";
            string? canonical = null;
            if (fieldPosition && scopes.TryPeek(out var scope) && parentheses == scope.Parentheses && angles == scope.Angles
                && Fields.TryGetValue(scope.Owner, out var fields))
                canonical = fields.FirstOrDefault(f => t.Text == f || t.Text == PreferredField(f));
            else if (!fieldPosition && (i == 0 || tokens[i - 1].Text != "."))
            {
                // Positive linking positions only; never convert a value/reference.
                canonical = Links.FirstOrDefault(pair => (t.Text == pair.Key || t.Text == pair.Value) &&
                    IsLink(tokens, i, pair.Key)).Key;
            }
            if (canonical is null) continue;
            var spelling = preferred ? Links.GetValueOrDefault(canonical) ?? PreferredField(canonical) : canonical;
            // All supported spelling aliases retain spans (important for diagnostics).
            if (spelling.Length != t.Length) throw new InvalidOperationException("Vocabulary alias changed a source span.");
            for (var c = 0; c < t.Length; c++) output[t.Start + c] = spelling[c];
        }
        return output.ToString();
    }

    private static bool IsLink(FirmamentLanguageAnalysisService.Lexeme[] tokens, int i, string word)
    {
        var next = i + 1 < tokens.Length ? tokens[i + 1] : null;
        if (word == "Include") return next?.Shape == "string";
        if (word is "Expose" or "Bind") return next?.Text == "{";
        if (next?.Shape != "identifier") return false;
        var start = i - 1;
        while (start >= 0 && tokens[start].Text is not ("{" or "}" or ";")) start--;
        var header = tokens[(start + 1)..i].Select(t => t.Text).ToArray();
        return word switch
        {
            "Using" => header.Contains("Profile"),
            "On" => header.Length >= 3 && header[^3] == "Concept" && header[^2] == "Struct",
            "Over" => header.Length >= 2 && header[^2] == "Pattern",
            _ => false
        };
    }

    private static string BlockOwner(FirmamentLanguageAnalysisService.Lexeme[] tokens, int open)
    {
        if (open == 0) return "";
        var boundary = open - 1;
        while (boundary >= 0 && tokens[boundary].Text is not ("{" or "}" or ";")) boundary--;
        var header = tokens[(boundary + 1)..open];
        // Historical lowercase documents have a distinct grammar (not merely
        // different casing). Keep their complete scopes with that owner.
        if (header.Any(t => t.Text is "model" or "solid" or "modify" or "feature")) return "Legacy";
        if (header.Any(t => t.Text == "Static")) return "Static";
        if (header.Any(t => t.Text == "Record")) return "Record";
        if (header.Any(t => t.Text is "Over" or "over") && header.Any(t => t.Text == "Pattern")) return "Pattern";
        if (header.Any(t => t.Text == "Mate")) return "Mate";
        if (tokens[open - 1].Text == ":") return open >= 2 ? CanonicalOwner(tokens[open - 2].Text) : "";
        var end = open - 1;
        if (end > 0 && tokens[end].Shape == "identifier" && tokens[end - 1].Text == ">")
        {
            var generic = end - 1;
            var depth = 1;
            while (generic > 0 && depth > 0)
            {
                generic--;
                if (tokens[generic].Text == ">") depth++;
                else if (tokens[generic].Text == "<") depth--;
            }
            if (depth == 0 && generic > 0 && tokens[generic - 1].Shape == "identifier")
                return CanonicalOwner(tokens[generic - 1].Text);
            // A bare Placement after an XML occurrence tag is not a generic declaration.
        }
        if (end > 0 && tokens[end].Shape == "identifier" && Fields.ContainsKey(tokens[end - 1].Text))
            return tokens[end - 1].Text; // named construct, even if its name resembles vocabulary
        var last = CanonicalOwner(tokens[end].Text);
        if (Fields.ContainsKey(last) && end > 0 && tokens[end - 1].Shape == "identifier" && tokens[end - 1].Text != "return")
            return tokens[end - 1].Text; // unknown owner with a vocabulary-shaped authored name
        if (Fields.ContainsKey(last) || last is "Record" or "Static" or "Table" or "Stations" or "with") return last;
        if (tokens[end].Shape == "identifier") end--; // authored name
        if (end >= 0 && tokens[end].Text == ">")
        {
            var depth = 1;
            while (end > 0 && depth > 0)
            {
                end--;
                if (tokens[end].Text == ">") depth++;
                else if (tokens[end].Text == "<") depth--;
            }
            end--;
        }
        var owner = end >= 0 ? CanonicalOwner(tokens[end].Text) : "";
        if (owner == "Pattern" && end > 0 && tokens[end - 1].Text == "Linear") return "Linear";
        if (owner == "Sites" && end > 0 && tokens[end - 1].Text == "Linear") return "Linear";
        // Static name: Type { ... } and Profile name using Layout { ... }.
        if (end >= 1 && tokens[end].Text == ":" && end >= 2 && tokens[end - 2].Text == "Static") return "Static";
        if (end >= 2 && tokens[end].Text is "Using" or "using" && tokens[end - 2].Text == "Profile") return "Profile";
        return owner;
    }

    private static string CanonicalOwner(string text) => Links.FirstOrDefault(p => p.Value == text).Key ??
        (text == "rotateLocal" ? "RotateLocal" : text);

    private static Dictionary<string, HashSet<string>> CreateFields()
    {
        var result = FirmamentSemanticSchemas.All.GroupBy(s => s.Name.Split('<')[0], StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.SelectMany(s => s.Fields).Select(f => f.Name).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        void Add(string owners, string names)
        {
            foreach (var owner in owners.Split(' '))
            {
                if (!result.TryGetValue(owner, out var fields)) result[owner] = fields = new(StringComparer.Ordinal);
                fields.UnionWith(names.Split(' '));
            }
        }
        // Bounded owner vocabularies not yet projected through generated schema.
        Add("Model", "Units Material");
        Add("Scene", "Units");
        Add("Garment", "Fabric MeshSize");
        Add("Panel", "Profile Origin U V Grain Pin PatternIdentity WrapRadius WrapAngle WrapTopRadius WrapTopOrigin WrapTopAngle");
        Add("Fabric", "ArealDensity Thickness WarpCompliance WeftCompliance DiagonalCompliance BendCompliance");
        Add("Interface", "A B Orientation Ease Compliance");
        Add("Drape", "Figure Pose Clearance");
        Add("Assembly Subassembly", "Anchor Provenance Appearance");
        Add("Part Occurrence", "Material Appearance");
        Add("Plane DatumPlane", "Origin Normal Up From Offset RotateLocal Clocking");
        Add("DatumFrame", "On At X");
        Add("Point2 Point3", "Position");
        Add("Axis Axis2 Line3", "Origin Direction Start End Reference");
        Add("Circle2", "Center Radius");
        Add("Rect2 RoundedRect2 Oval2", "Center Size Radius");
        Add("Polygon2", "Vertices");
        Add("Curve2", "From On Scale Pivot Translate Rotate");
        Add("CubicBezier2", "From Control1 Control2 To");
        Add("Line2", "From To");
        Add("Profile", "From");
        Add("Replace", "On Through Derivatives");
        Add("Path", "Start Heading");
        Add("Line", "Length To");
        Add("Extrude Base", "Profile From To Role");
        Add("Boss Pocket", "On Profile Height Depth");
        Add("Linear", "Source Direction Count Spacing Keys Start Step");
        Add("RotateLocal", "Axis Angle");
        Add("Mate", "Interface Member At Gap Clocking Orientation Support");
        Add("Note", "Target Text");
        Add("Appearance", "Color Metallic Roughness Opacity Emissive");
        Add("Material", "Identity Appearance");
        Add("Start End Corner RouteSite", "At On Radius");
        Add("SectionChain", "Continuity Start End");
        Add("Loft", "Sections Solid");
        Add("Continuity", "Order");
        return result;
    }
}
