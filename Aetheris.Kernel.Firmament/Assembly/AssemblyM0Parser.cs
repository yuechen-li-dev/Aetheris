using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Assembly;

/// <summary>Bounded parser for the M0 relational assembly lane. XML-like tags are used only for product-tree containment.</summary>
public sealed class AssemblyM0Parser
{
    public sealed record ParseResult(AssemblySource? Source, IReadOnlyList<AssemblyDiagnostic> Diagnostics, double ElapsedMilliseconds)
    { public bool IsSuccess => Source is not null && Diagnostics.All(x => x.Severity != AssemblyDiagnosticSeverity.Error); }

    public ParseResult ParseFile(string path) => Parse(File.ReadAllText(path), Path.GetFullPath(path));

    public ParseResult Parse(string input, string sourceIdentity = "<memory>")
    {
        var watch = Stopwatch.StartNew();
        var diagnostics = new List<AssemblyDiagnostic>();
        var source = Regex.Replace(input, @"//[^\r\n]*", string.Empty);
        var interfaces = ParseInterfaces(source, sourceIdentity, diagnostics);
        var assemblyHeader = Regex.Match(source, @"\bAssembly\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant);
        if (!assemblyHeader.Success)
        { diagnostics.Add(new("assembly-parse-missing-root", "Expected 'Assembly Name { ... }'.")); return Done(null); }
        var body = BalancedBody(source, assemblyHeader.Index + assemblyHeader.Length - 1, diagnostics, "Assembly");
        if (body is null) return Done(null);
        var tree = ParseTree(body, sourceIdentity, diagnostics);
        if (tree is null) return Done(null);
        var mates = ParseMates(body, sourceIdentity, diagnostics);
        var relations = ParseRelations(body, diagnostics);
        var asserts = ParseAsserts(body, sourceIdentity, diagnostics);
        var anchorMatch = Regex.Match(RemoveBlocks(body, "Mate", @"Assert\s+ToleranceStackup"), @"\bAnchor\s*:\s*(?<path>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*;", RegexOptions.CultureInvariant);
        var anchor = anchorMatch.Success ? AssemblyPath.Parse(anchorMatch.Groups["path"].Value) : new AssemblyPath([tree.Name]);
        var definitionBoundary = new[]
        {
            Regex.Match(source, @"^[ \t]*Interface\s+[A-Za-z_]\w*\s*\{", RegexOptions.CultureInvariant | RegexOptions.Multiline),
            Regex.Match(source, @"^[ \t]*Assembly\s+[A-Za-z_]\w*\s*\{", RegexOptions.CultureInvariant | RegexOptions.Multiline)
        }.Where(match => match.Success).Select(match => match.Index).DefaultIfEmpty(0).Min();
        var definitionSource = definitionBoundary > 0 ? source[..definitionBoundary].Trim() : null;
        var result = new AssemblySource(assemblyHeader.Groups["name"].Value, tree, interfaces, mates, anchor, relations, asserts, sourceIdentity, definitionSource);
        return Done(result);

        ParseResult Done(AssemblySource? value) { watch.Stop(); return new(value, diagnostics, watch.Elapsed.TotalMilliseconds); }
    }

    private static IReadOnlyList<InterfaceDefinition> ParseInterfaces(string source, string sourceIdentity, List<AssemblyDiagnostic> diagnostics)
    {
        var result = new List<InterfaceDefinition>();
        foreach (Match header in Regex.Matches(source, @"\bInterface\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant))
        {
            var body = BalancedBody(source, header.Index + header.Length - 1, diagnostics, "Interface");
            if (body is null) continue;
            var name = header.Groups["name"].Value;
            var roles = Regex.Matches(body, @"\bRole\s+(?<name>[A-Za-z_]\w*)\s+requires\s+(?<caps>[A-Za-z, ]+)\s*;", RegexOptions.CultureInvariant)
                .Select(m => new InterfaceRoleDefinition(m.Groups["name"].Value,
                    m.Groups["caps"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))).ToArray();
            var requirements = Regex.Matches(body, @"\bLower\s+(?<kind>AxisCoincident|AxisAligned|PlaneCoincident|PointCoincident|OffsetAlongAxis)\s+(?<a>[A-Za-z_]\w*)\.(?<am>[A-Za-z_]\w*)\s+(?<b>[A-Za-z_]\w*)\.(?<bm>[A-Za-z_]\w*)(?:\s+(?<offset>[-+]?\d+(?:\.\d+)?)mm)?\s*;", RegexOptions.CultureInvariant)
                .Select(m => new InterfaceRequirementDefinition(Enum.Parse<PlacementConstraintKind>(m.Groups["kind"].Value),
                    m.Groups["a"].Value, m.Groups["am"].Value, m.Groups["b"].Value, m.Groups["bm"].Value,
                    m.Groups["offset"].Success ? Number(m.Groups["offset"].Value) : 0)).ToArray();
            var fitMatch = Regex.Match(body, @"\bFit\s+(?<a>[A-Za-z_]\w*)\.(?<am>[A-Za-z_]\w*)\s+inside\s+(?<b>[A-Za-z_]\w*)\.(?<bm>[A-Za-z_]\w*)\s*;", RegexOptions.CultureInvariant);
            InterfaceFitDefinition? fit = fitMatch.Success ? new(fitMatch.Groups["a"].Value, fitMatch.Groups["am"].Value, fitMatch.Groups["b"].Value, fitMatch.Groups["bm"].Value) : null;
            var free = Regex.Matches(body, @"\bAllow\s+(?<kind>rotation|translation):(?<axis>[A-Za-z-]+)\s*;", RegexOptions.CultureInvariant)
                .Select(m => m.Groups["kind"].Value + ":" + m.Groups["axis"].Value).ToArray();
            if (roles.Length == 0) diagnostics.Add(new("assembly-interface-no-roles", $"Interface '{name}' must define at least one Role."));
            result.Add(new($"interface:{name}", name, roles, requirements, fit, free, SemanticSourceSpan.Generated(sourceIdentity)));
        }
        return result.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
    }

    private sealed class MutableNode(string name, AssemblyInstanceKind kind, string definition)
    {
        public string Name { get; } = name;
        public AssemblyInstanceKind Kind { get; } = kind;
        public string Definition { get; } = definition;
        public List<MutableNode> Children { get; } = [];
        public List<SemanticValue> Semantics { get; } = [];
        public MutableNode? Parent { get; set; }
        public AssemblyTransform? ExplicitTransform { get; set; }
        public PlacementAuthority PlacementAuthority { get; set; } = PlacementAuthority.MateDerived;
    }

    private static AssemblyMemberSource? ParseTree(string body, string sourceIdentity, List<AssemblyDiagnostic> diagnostics)
    {
        // One bounded generic argument list is admitted in a Part definition. The
        // inner '>' belongs to the Firmament Template application, not the XML-like tag.
        var tagPattern = new Regex(@"<(?<close>/)?(?<kind>Assembly|Part)\s*(?<rest>[^<>]*(?:<[^<>]*>[^<>]*)?)>", RegexOptions.CultureInvariant);
        var matches = tagPattern.Matches(body).Cast<Match>().ToArray();
        MutableNode? root = null;
        var stack = new Stack<(MutableNode node, int bodyStart)>();
        foreach (var tag in matches)
        {
            var closing = tag.Groups["close"].Success;
            var kind = tag.Groups["kind"].Value;
            if (!closing)
            {
                var rest = tag.Groups["rest"].Value.Trim();
                var nodeMatch = kind == "Assembly"
                    ? Regex.Match(rest, @"^(?<name>[A-Za-z_]\w*)$")
                    : Regex.Match(rest, @"^(?<name>[A-Za-z_]\w*)\s*=\s*(?<definition>[A-Za-z_]\w*(?:\s*<[^>]+>)?)$");
                if (!nodeMatch.Success) { diagnostics.Add(new("assembly-tree-invalid-tag", $"Invalid <{kind}> tree tag '{tag.Value}'.")); continue; }
                var node = new MutableNode(nodeMatch.Groups["name"].Value, kind == "Assembly" ? AssemblyInstanceKind.Assembly : AssemblyInstanceKind.Part,
                    kind == "Assembly" ? nodeMatch.Groups["name"].Value : nodeMatch.Groups["definition"].Value);
                if (stack.Count > 0) { node.Parent = stack.Peek().node; node.Parent.Children.Add(node); }
                else if (root is null) root = node;
                else diagnostics.Add(new("assembly-tree-multiple-roots", "Assembly source must contain exactly one XML-like tree root."));
                stack.Push((node, tag.Index + tag.Length));
            }
            else
            {
                if (stack.Count == 0 || stack.Peek().node.Kind.ToString() != kind)
                { diagnostics.Add(new("assembly-tree-unbalanced", $"Unexpected closing tag '{tag.Value}'.")); continue; }
                var open = stack.Pop();
                var nodeBody = body[open.bodyStart..tag.Index];
                if (open.node.Kind == AssemblyInstanceKind.Part)
                {
                    open.node.Semantics.AddRange(ParseSemantics(nodeBody, open.node.Definition, sourceIdentity, diagnostics));
                }
                var nestedTag = nodeBody.IndexOf('<');
                var placementBody = open.node.Kind == AssemblyInstanceKind.Assembly && nestedTag >= 0 ? nodeBody[..nestedTag] : nodeBody;
                var placement = Regex.Match(placementBody, @"\bPlacement\s+(?<authority>ImportedOccurrence|LegacyExplicit)\s*=\s*\[(?<matrix>[^]]+)\]\s*;", RegexOptions.CultureInvariant);
                if (placement.Success)
                {
                    var matrix = placement.Groups["matrix"].Value.Split(',', StringSplitOptions.TrimEntries)
                        .Select(value => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : double.NaN).ToArray();
                    if (matrix.Length != 16 || matrix.Any(value => !double.IsFinite(value)))
                        diagnostics.Add(new("assembly-placement-invalid-transform", $"Instance '{open.node.Name}' placement must contain 16 finite row-major matrix values."));
                    else
                    {
                        open.node.ExplicitTransform = new(matrix);
                        open.node.PlacementAuthority = Enum.Parse<PlacementAuthority>(placement.Groups["authority"].Value);
                    }
                }
            }
        }
        if (stack.Count > 0) diagnostics.Add(new("assembly-tree-unbalanced", $"Missing closing tag for '{stack.Peek().node.Name}'."));
        if (root is null) diagnostics.Add(new("assembly-tree-missing", "Assembly body requires a nested <Assembly Name> product tree."));
        AssemblyMemberSource Freeze(MutableNode node) => new(node.Name, node.Kind, node.Definition,
            node.Children.Select(Freeze).ToArray(), node.Semantics, [], [new("assembly-source", node.Name, node.Definition, SemanticSourceSpan.Generated(sourceIdentity))],
            node.ExplicitTransform, node.PlacementAuthority);
        return root is null ? null : Freeze(root);
    }

    private static IEnumerable<SemanticValue> ParseSemantics(string body, string definition, string sourceIdentity, List<AssemblyDiagnostic> diagnostics)
    {
        foreach (Match header in Regex.Matches(body, @"\bSemantic\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant))
        {
            var block = BalancedBody(body, header.Index + header.Length - 1, diagnostics, "Semantic");
            if (block is null) continue;
            var name = header.Groups["name"].Value;
            var members = new List<SemanticValue>();
            var caps = new List<ISemanticCapability>();
            var bindings = new List<SemanticBinding>();
            foreach (Match axis in Regex.Matches(block, @"\bAxis\s+(?<name>[A-Za-z_]\w*)\s*=\s*\[(?<o>[^]]+)\]\s*->\s*\[(?<d>[^]]+)\]\s*;", RegexOptions.CultureInvariant))
            {
                var o = Vector(axis.Groups["o"].Value); var d = Vector(axis.Groups["d"].Value);
                if (o is null || d is null) { diagnostics.Add(new("assembly-axis-invalid", $"Semantic '{name}' has an invalid Axis.")); continue; }
                var binding = new ExactAxisBinding(o[0], o[1], o[2], d[0], d[1], d[2], $"definition:{definition}:{name}:{axis.Groups["name"].Value}");
                members.Add(new SemanticValue(binding.AxisStableId, new("Axis"), [new AxisCapability()], [binding], exposedName: axis.Groups["name"].Value));
                caps.Add(new AxisCapability()); if (!bindings.OfType<ExactAxisBinding>().Any()) bindings.Add(binding);
            }
            foreach (Match plane in Regex.Matches(block, @"\bPlane\s+(?<name>[A-Za-z_]\w*)\s*=\s*\[(?<o>[^]]+)\]\s+normal\s*\[(?<n>[^]]+)\]\s*;", RegexOptions.CultureInvariant))
            {
                var o = Vector(plane.Groups["o"].Value); var n = Vector(plane.Groups["n"].Value);
                if (o is null || n is null) { diagnostics.Add(new("assembly-plane-invalid", $"Semantic '{name}' has an invalid Plane.")); continue; }
                var binding = new ExactPlaneBinding(o[0], o[1], o[2], n[0], n[1], n[2], $"definition:{definition}:{name}:{plane.Groups["name"].Value}");
                members.Add(new SemanticValue(binding.PlaneStableId, new("Plane"), [new PlaneCapability()], [binding], exposedName: plane.Groups["name"].Value));
                caps.Add(new PlaneCapability()); if (!bindings.OfType<ExactPlaneBinding>().Any()) bindings.Add(binding);
            }
            foreach (Match point in Regex.Matches(block, @"\bPoint\s+(?<name>[A-Za-z_]\w*)\s*=\s*\[(?<p>[^]]+)\]\s*;", RegexOptions.CultureInvariant))
            {
                var p = Vector(point.Groups["p"].Value); if (p is null) continue;
                var binding = new ExactPointBinding(p[0], p[1], p[2], $"definition:{definition}:{name}:{point.Groups["name"].Value}");
                members.Add(new SemanticValue(binding.PointStableId, new("Point"), [new PointCapability()], [binding], exposedName: point.Groups["name"].Value));
                caps.Add(new PointCapability()); if (!bindings.OfType<ExactPointBinding>().Any()) bindings.Add(binding);
            }
            foreach (Match dim in Regex.Matches(block, @"\bDimension\s+(?<name>[A-Za-z_]\w*)\s*=\s*(?<nom>[-+]?\d+(?:\.\d+)?)mm(?:\s+tol\s+(?:(?<bil>\d+(?:\.\d+)?)mm|\+(?<plus>\d+(?:\.\d+)?)mm\s+-(?<minus>\d+(?:\.\d+)?)mm))?\s*;", RegexOptions.CultureInvariant))
            {
                var plus = dim.Groups["bil"].Success ? Number(dim.Groups["bil"].Value) : dim.Groups["plus"].Success ? Number(dim.Groups["plus"].Value) : 0;
                var minus = dim.Groups["bil"].Success ? Number(dim.Groups["bil"].Value) : dim.Groups["minus"].Success ? Number(dim.Groups["minus"].Value) : 0;
                var binding = new TolerancedDimensionBinding(Number(dim.Groups["nom"].Value), -minus, plus, "mm", $"definition:{definition}:{name}:{dim.Groups["name"].Value}");
                members.Add(new SemanticValue(binding.DimensionStableId, new("Length"), [new DimensionalCapability()], [binding], exposedName: dim.Groups["name"].Value,
                    provenance: [new("Firmament-tol", dim.Groups["name"].Value, dim.Value, SemanticSourceSpan.Generated(sourceIdentity))]));
                caps.Add(new DimensionalCapability()); if (!bindings.OfType<TolerancedDimensionBinding>().Any()) bindings.Add(binding);
            }
            yield return new SemanticValue($"definition:{definition}:{name}", new("Concept"), caps.DistinctBy(x => x.GetType()), bindings, members,
                [new("part-definition", definition, name, SemanticSourceSpan.Generated(sourceIdentity))], exposedName: name);
        }
    }

    private static IReadOnlyList<MateSource> ParseMates(string body, string sourceIdentity, List<AssemblyDiagnostic> diagnostics)
    {
        var result = new List<MateSource>();
        foreach (Match header in Regex.Matches(body, @"\bMate\s+(?<name>[A-Za-z_]\w*)\s*:\s*(?<interface>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant))
        {
            var block = BalancedBody(body, header.Index + header.Length - 1, diagnostics, "Mate"); if (block is null) continue;
            var roles = Regex.Matches(block, @"\b(?<role>[A-Za-z_]\w*)\s*:\s*(?<path>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*;", RegexOptions.CultureInvariant)
                .Select(m => new MateRoleAssignment(m.Groups["role"].Value, AssemblyPath.Parse(m.Groups["path"].Value))).ToArray();
            result.Add(new(header.Groups["name"].Value, header.Groups["interface"].Value, roles, SemanticSourceSpan.Generated(sourceIdentity)));
        }
        return result;
    }

    private static IReadOnlyList<DimensionalRelationSource> ParseRelations(string body, List<AssemblyDiagnostic> diagnostics) =>
        Regex.Matches(body, @"\bRelation\s+(?<name>[A-Za-z_]\w*)\s*:\s*(?<from>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*->\s*(?<to>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*=\s*(?<nom>[-+]?\d+(?:\.\d+)?)mm(?:\s+tol\s+(?:(?<bil>\d+(?:\.\d+)?)mm|\+(?<plus>\d+(?:\.\d+)?)mm\s+-(?<minus>\d+(?:\.\d+)?)mm))?\s+from\s+""(?<provenance>[^""]+)""\s*;", RegexOptions.CultureInvariant)
            .Select(m => { var plus = m.Groups["bil"].Success ? Number(m.Groups["bil"].Value) : m.Groups["plus"].Success ? Number(m.Groups["plus"].Value) : 0; var minus = m.Groups["bil"].Success ? Number(m.Groups["bil"].Value) : m.Groups["minus"].Success ? Number(m.Groups["minus"].Value) : 0; return new DimensionalRelationSource(m.Groups["name"].Value, AssemblyPath.Parse(m.Groups["from"].Value), AssemblyPath.Parse(m.Groups["to"].Value), Number(m.Groups["nom"].Value), -minus, plus, "mm", m.Groups["provenance"].Value); }).ToArray();

    private static IReadOnlyList<ToleranceStackupAssertSource> ParseAsserts(string body, string sourceIdentity, List<AssemblyDiagnostic> diagnostics)
    {
        var result = new List<ToleranceStackupAssertSource>();
        foreach (Match header in Regex.Matches(body, @"\bAssert\s+ToleranceStackup\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant))
        {
            var block = BalancedBody(body, header.Index + header.Length - 1, diagnostics, "Assert ToleranceStackup"); if (block is null) continue;
            var between = Regex.Match(block, @"\bBetween\s*:\s*\[(?<from>[^,]+),\s*(?<to>[^]]+)\]\s*;");
            var require = Regex.Match(block, @"\bRequire\s*:\s*Clearance\s*>=\s*(?<min>[-+]?\d+(?:\.\d+)?)mm\s*;");
            if (!between.Success || !require.Success) { diagnostics.Add(new("assembly-tolerance-assert-invalid", $"Assert ToleranceStackup '{header.Groups["name"].Value}' requires Between and Clearance fields.")); continue; }
            result.Add(new(header.Groups["name"].Value, AssemblyPath.Parse(between.Groups["from"].Value.Trim()), AssemblyPath.Parse(between.Groups["to"].Value.Trim()), Number(require.Groups["min"].Value), "mm", SemanticSourceSpan.Generated(sourceIdentity)));
        }
        return result;
    }

    private static string? BalancedBody(string source, int openingBrace, List<AssemblyDiagnostic> diagnostics, string construct)
    {
        var depth = 0;
        for (var i = openingBrace; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return source[(openingBrace + 1)..i];
        }
        diagnostics.Add(new("assembly-parse-unbalanced", $"{construct} has an unclosed body.")); return null;
    }

    private static string RemoveBlocks(string source, params string[] names)
    {
        var value = source;
        foreach (var name in names) value = Regex.Replace(value, $@"\b{name}\b[^{{]*\{{(?:[^{{}}]|\{{[^{{}}]*\}})*\}}", string.Empty, RegexOptions.CultureInvariant);
        return value;
    }
    private static double[]? Vector(string text)
    { var values = text.Split(',', StringSplitOptions.TrimEntries).Select(x => double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : double.NaN).ToArray(); return values.Length == 3 && values.All(double.IsFinite) ? values : null; }
    private static double Number(string text) => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
}

public sealed class AssemblyM0Pipeline
{
    public AssemblyCompilationResult CompileFile(string path)
    {
        var parsed = new AssemblyM0Parser().ParseFile(path);
        if (!parsed.IsSuccess) return new(null, parsed.Diagnostics);
        return new AssemblyM0Compiler().Compile(parsed.Source!, parsed.ElapsedMilliseconds);
    }
}
