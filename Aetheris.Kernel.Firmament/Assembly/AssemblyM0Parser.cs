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
        var templateRanges = new List<(int Start, int Length)>();
        var assemblyDefinitions = ParseAssemblyDefinitions(source, sourceIdentity, interfaces, diagnostics, templateRanges);
        // Template-produced Assembly bodies are declarations, not exported roots.
        // Blank them without changing offsets, then locate the single root Assembly.
        var rootSearch = source.ToCharArray();
        foreach (var range in templateRanges) Array.Fill(rootSearch, ' ', range.Start, range.Length);
        var rootSource = new string(rootSearch);
        var assemblyHeader = Regex.Match(rootSource, @"\bAssembly\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant);
        if (!assemblyHeader.Success)
        { diagnostics.Add(new("assembly-parse-missing-root", "Expected 'Assembly Name { ... }'.")); return Done(null); }
        var body = BalancedBody(source, assemblyHeader.Index + assemblyHeader.Length - 1, diagnostics, "Assembly");
        if (body is null) return Done(null);
        var specializationCache = new Dictionary<string, AssemblyMemberSource>(StringComparer.Ordinal);
        var tree = ParseTree(body, sourceIdentity, diagnostics, assemblyDefinitions, interfaces, specializationCache);
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
        var declarationChars = source.ToCharArray();
        foreach (var range in templateRanges) Array.Fill(declarationChars, ' ', range.Start, range.Length);
        var declarations = new string(declarationChars);
        var definitionSource = definitionBoundary > 0 ? declarations[..definitionBoundary].Trim() : null;
        var result = new AssemblySource(assemblyHeader.Groups["name"].Value, tree, interfaces, mates, anchor, relations, asserts, sourceIdentity, definitionSource, assemblyDefinitions);
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
        public bool IsEncapsulatedDefinition { get; set; }
        public AssemblyDefinitionIr? SolvedAssemblyDefinition { get; set; }
    }

    private static IReadOnlyList<AssemblyDefinitionSource> ParseAssemblyDefinitions(string source, string sourceIdentity,
        IReadOnlyList<InterfaceDefinition> interfaces, List<AssemblyDiagnostic> diagnostics, List<(int Start, int Length)> ranges)
    {
        var result = new List<AssemblyDefinitionSource>();
        foreach (Match template in Regex.Matches(source,
            @"\bTemplate\s*<\s*(?<parameter>[A-Za-z_]\w*)\s*:\s*(?<type>[A-Za-z_]\w*)\s*>\s*Assembly\s+(?<name>[A-Za-z_]\w*)\s*\{",
            RegexOptions.CultureInvariant))
        {
            var body = BalancedBody(source, template.Index + template.Length - 1, diagnostics, "Template Assembly");
            if (body is null) continue;
            var close = template.Index + template.Length + body.Length + 1;
            ranges.Add((template.Index, close - template.Index));
            var root = ParseTree(body, sourceIdentity, diagnostics);
            if (root is null) continue;
            var exposed = ParseAssemblyExposes(body, root, sourceIdentity, diagnostics);
            root = root with { ExposedSemantics = exposed, IsEncapsulatedDefinition = true };
            var name = template.Groups["name"].Value;
            if (result.Any(item => item.Name == name))
            {
                diagnostics.Add(new("assembly-template-duplicate-definition", $"Assembly Template '{name}' is declared more than once."));
                continue;
            }
            var anchorMatch = Regex.Match(RemoveBlocks(body, "Mate", @"Assert\s+ToleranceStackup", "Expose"), @"\bAnchor\s*:\s*(?<path>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*;", RegexOptions.CultureInvariant);
            var anchor = anchorMatch.Success ? AssemblyPath.Parse(anchorMatch.Groups["path"].Value) : new AssemblyPath([root.Name]);
            var exposedRelations = ParseExposedRelations(body);
            var staticProvenance = Regex.Matches(source, @"\bStatic\s+Table\s+(?<name>[A-Za-z_]\w*)[^\{]*\{", RegexOptions.CultureInvariant)
                .Select(match => new SemanticProvenance("static-table", match.Groups["name"].Value, match.Value.Trim(), SemanticSourceSpan.Generated(sourceIdentity)))
                .Concat(Regex.Matches(source, @"\bStatic\s+(?<name>[A-Za-z_]\w*)[^=]*=\s*(?<base>[A-Za-z_]\w*)\s+with\s*\{", RegexOptions.CultureInvariant)
                    .Select(match => new SemanticProvenance("static-with", match.Groups["name"].Value, "derivedFrom:" + match.Groups["base"].Value, SemanticSourceSpan.Generated(sourceIdentity))))
                .Concat(Regex.Matches(source, @"\bStatic\s+(?<name>[A-Za-z_]\w*)\s*:\s*(?<type>[A-Za-z_]\w*)\s*=\s*\k<type>\s*\{", RegexOptions.CultureInvariant)
                    .Select(match => new SemanticProvenance("static-record", match.Groups["name"].Value, match.Groups["type"].Value, SemanticSourceSpan.Generated(sourceIdentity))))
                .ToArray();
            result.Add(new(name, template.Groups["parameter"].Value, template.Groups["type"].Value, root, name,
                [new("assembly-template-definition", name, template.Groups["parameter"].Value + ":" + template.Groups["type"].Value, SemanticSourceSpan.Generated(sourceIdentity)), .. staticProvenance],
                anchor, ParseMates(body, sourceIdentity, diagnostics), ParseRelations(body, diagnostics), ParseAsserts(body, sourceIdentity, diagnostics), exposedRelations));
        }
        return result;
    }

    private static IReadOnlyList<AssemblyExposedRelationSource> ParseExposedRelations(string body)
    {
        var header = Regex.Match(body, @"\bExpose\s*\{", RegexOptions.CultureInvariant);
        if (!header.Success) return [];
        var diagnostics = new List<AssemblyDiagnostic>();
        var block = BalancedBody(body, header.Index + header.Length - 1, diagnostics, "Assembly Expose") ?? string.Empty;
        return Regex.Matches(block, @"\bRelation\s+(?<name>[A-Za-z_]\w*)\s*:\s*(?<publicFrom>[A-Za-z_]\w*)\s*->\s*(?<publicTo>[A-Za-z_]\w*)\s*=\s*(?<internalFrom>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*->\s*(?<internalTo>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*;", RegexOptions.CultureInvariant)
            .Select(match => new AssemblyExposedRelationSource(match.Groups["name"].Value, match.Groups["publicFrom"].Value, match.Groups["publicTo"].Value,
                AssemblyPath.Parse(match.Groups["internalFrom"].Value), AssemblyPath.Parse(match.Groups["internalTo"].Value))).ToArray();
    }

    private static IReadOnlyList<SemanticValue> ParseAssemblyExposes(string body, AssemblyMemberSource root, string sourceIdentity, List<AssemblyDiagnostic> diagnostics)
    {
        var header = Regex.Match(body, @"\bExpose\s*\{", RegexOptions.CultureInvariant);
        if (!header.Success) return [];
        var block = BalancedBody(body, header.Index + header.Length - 1, diagnostics, "Assembly Expose") ?? string.Empty;
        var result = new List<SemanticValue>();
        foreach (Match expose in Regex.Matches(block, @"\b(?:Semantic|Axis|Plane|Point|Dimension)\s+(?<name>[A-Za-z_]\w*)\s*=\s*(?<path>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*;", RegexOptions.CultureInvariant))
        {
            var alias = expose.Groups["name"].Value;
            if (result.Any(value => value.ExposedName == alias)) { diagnostics.Add(new("assembly-expose-duplicate-name", $"Assembly Expose declares '{alias}' more than once.")); continue; }
            var segments = expose.Groups["path"].Value.Split('.');
            var child = root.Children.SingleOrDefault(item => item.Name == segments[0]);
            SemanticValue? value = child is null ? null : child.ExposedSemantics.SingleOrDefault(item => item.ExposedName == segments.ElementAtOrDefault(1));
            for (var i = 2; value is not null && i < segments.Length; i++) value = value.ExposedMembers.GetValueOrDefault(segments[i]);
            if (value is null)
            {
                diagnostics.Add(new("assembly-expose-unresolved-path", $"Assembly Expose '{alias}' references nonexistent internal path '{expose.Groups["path"].Value}'."));
                continue;
            }
            result.Add(new SemanticValue(value.StableIdentity + ":expose:" + alias, value.Type, value.Capabilities.Values, value.Bindings,
                value.ExposedMembers.Values, [.. value.Provenance, new("assembly-expose", alias, expose.Groups["path"].Value, SemanticSourceSpan.Generated(sourceIdentity))],
                value.AuthoredSourceSpan, value.GeneratedSourceSpan, alias));
        }
        return result;
    }

    private static AssemblyMemberSource? InstantiateAssemblyDefinition(string occurrenceName, string identity,
        IReadOnlyList<AssemblyDefinitionSource> definitions, IReadOnlyList<InterfaceDefinition> interfaces,
        IDictionary<string, AssemblyMemberSource> specializationCache, string sourceIdentity, List<AssemblyDiagnostic> diagnostics)
    {
        var application = Regex.Match(identity, @"^(?<name>[A-Za-z_]\w*)\s*<\s*(?<parameter>[A-Za-z_]\w*)\s*:\s*(?<argument>[^>]+)\s*>$", RegexOptions.CultureInvariant);
        if (!application.Success) { diagnostics.Add(new("assembly-template-unresolved-definition", $"Assembly occurrence '{occurrenceName}' references unresolved definition '{identity}'.")); return null; }
        var definition = definitions.SingleOrDefault(item => item.Name == application.Groups["name"].Value);
        if (definition is null) { diagnostics.Add(new("assembly-template-unresolved-definition", $"Assembly Template '{application.Groups["name"].Value}' was not found.")); return null; }
        if (application.Groups["parameter"].Value != definition.ParameterName) { diagnostics.Add(new("assembly-template-argument-mismatch", $"Assembly Template '{definition.Name}' expects argument '{definition.ParameterName}'.")); return null; }
        var argument = application.Groups["argument"].Value.Trim();
        var specializationIdentity = NormalizeIdentity(identity);
        if (specializationCache.TryGetValue(specializationIdentity, out var cached))
            return RenameOccurrence(cached, occurrenceName);
        AssemblyMemberSource Replace(AssemblyMemberSource item) => item with
        {
            Name = item.Name,
            DefinitionIdentity = Regex.Replace(item.DefinitionIdentity, $@"(?<prefix>:\s*)\b{Regex.Escape(definition.ParameterName)}\b(?=\s*>)", "${prefix}" + argument),
            Children = item.Children.Select(Replace).ToArray(),
            Provenance = [.. item.Provenance ?? [], new("assembly-template-specialization", definition.Name, specializationIdentity, SemanticSourceSpan.Generated(sourceIdentity))],
            IsEncapsulatedDefinition = false
        };
        var specializedRoot = Replace(definition.LocalRoot);
        var localSource = new AssemblySource(definition.Name, specializedRoot, interfaces, definition.LocalMates ?? [], definition.LocalAnchor ?? new([definition.Name]),
            definition.LocalDimensionalRelations ?? [], definition.LocalStackupAsserts ?? [], sourceIdentity);
        var watch = Stopwatch.StartNew();
        var local = new AssemblyM0Compiler().Compile(localSource);
        watch.Stop();
        var localFailures = local.Diagnostics.Where(diagnostic => diagnostic.Severity == AssemblyDiagnosticSeverity.Error).ToArray();
        var invalidPlacement = local.Ir?.Placements.FirstOrDefault(placement => placement.InstanceStableId != local.Ir.RootInstanceStableId
            && placement.Status is PlacementStatus.Underconstrained or PlacementStatus.Overconstrained or PlacementStatus.Unresolved);
        if (localFailures.Length > 0 || invalidPlacement is not null)
        {
            foreach (var failure in localFailures) diagnostics.Add(new("assembly-template-internal-" + failure.Code, $"Assembly Template specialization '{specializationIdentity}' is internally invalid: {failure.Message}"));
            if (invalidPlacement is not null) diagnostics.Add(new("assembly-template-internal-constraint-failure", $"Assembly Template specialization '{specializationIdentity}' has internal placement '{invalidPlacement.InstanceStableId}' in state {invalidPlacement.Status}."));
            return null;
        }
        if (local.Ir is null) return null;
        AssemblyMemberSource ApplyLocal(AssemblyMemberSource item, AssemblyPath path)
        {
            var localInstance = local.Ir.Instances.Single(instance => instance.Path.ToString() == path.ToString());
            return item with { Children = item.Children.Select(child => ApplyLocal(child, path.Append(child.Name))).ToArray(), ExplicitTransform = item == specializedRoot ? null : localInstance.ResolvedTransform };
        }
        specializedRoot = ApplyLocal(specializedRoot, new([specializedRoot.Name]));
        var publicSemantics = specializedRoot.ExposedSemantics.Select(value => BindPublicSemantic(value, local.Ir, sourceIdentity)).ToArray();
        var publicRelations = BuildPublicRelations(definition, specializationIdentity, publicSemantics, local.Ir, diagnostics);
        var stableId = "assembly-definition:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(specializationIdentity)))[..16];
        var definitionIr = new AssemblyDefinitionIr(stableId, specializationIdentity, definition.Name, specializationIdentity,
            [.. definition.Provenance, new("assembly-template-specialization", definition.Name, specializationIdentity, SemanticSourceSpan.Generated(sourceIdentity))],
            local.Ir.Instances, local.Ir.Mates, local.Ir.Placements, local.Ir.DimensionalRelations, local.Ir.ToleranceStackups,
            publicSemantics, publicRelations, watch.Elapsed.TotalMilliseconds);
        specializedRoot = specializedRoot with { ExposedSemantics = publicSemantics, IsEncapsulatedDefinition = true, SolvedAssemblyDefinition = definitionIr, DefinitionIdentity = specializationIdentity };
        specializationCache[specializationIdentity] = specializedRoot;
        return RenameOccurrence(specializedRoot, occurrenceName);

        static AssemblyMemberSource RenameOccurrence(AssemblyMemberSource root, string name) => root with { Name = name };
    }

    private static SemanticValue BindPublicSemantic(SemanticValue exposed, AssemblyIr localIr, string sourceIdentity)
    {
        var internalPath = exposed.Provenance.LastOrDefault(item => item.Stage == "assembly-expose")?.Evidence;
        if (internalPath is null || !AssemblyM0Compiler.TryResolve(AssemblyPath.Parse(localIr.Name + "." + internalPath), localIr.Instances, out var reference)) return exposed;
        var bindings = exposed.Bindings.Where(binding => binding is TolerancedDimensionBinding).ToList();
        try { bindings.Add(AssemblyWorldQuery.Resolve(localIr, reference!.Value.StableIdentity)); } catch (InvalidOperationException) { }
        return new(exposed.StableIdentity, exposed.Type, exposed.Capabilities.Values, bindings, exposed.ExposedMembers.Values, exposed.Provenance,
            exposed.AuthoredSourceSpan, SemanticSourceSpan.Generated(sourceIdentity), exposed.ExposedName);
    }

    private static IReadOnlyList<DimensionalRelationIr> BuildPublicRelations(AssemblyDefinitionSource definition, string specializationIdentity,
        IReadOnlyList<SemanticValue> publicSemantics, AssemblyIr localIr, List<AssemblyDiagnostic> diagnostics)
    {
        var result = new List<DimensionalRelationIr>();
        foreach (var exposed in definition.ExposedRelations ?? [])
        {
            var fromPath = Prefix(exposed.InternalFrom, definition.Name); var toPath = Prefix(exposed.InternalTo, definition.Name);
            if (!AssemblyM0Compiler.TryResolve(fromPath, localIr.Instances, out var from) || !AssemblyM0Compiler.TryResolve(toPath, localIr.Instances, out var to))
            { diagnostics.Add(new("assembly-exposed-relation-unresolved", $"Exposed relation '{exposed.Name}' has an unresolved internal endpoint.")); continue; }
            var paths = FindRelationPaths(from!.Value.StableIdentity, to!.Value.StableIdentity, localIr.DimensionalRelations, 2);
            if (paths.Count != 1) { diagnostics.Add(new(paths.Count == 0 ? "assembly-exposed-relation-missing-chain" : "assembly-exposed-relation-ambiguous", $"Exposed relation '{exposed.Name}' requires exactly one internal dimensional chain.")); continue; }
            var contributions = paths[0].Select(step => new StackupContributionIr(step.Edge.StableId, step.Sign, step.Sign * step.Edge.Nominal,
                step.Sign > 0 ? step.Edge.LowerTolerance : -step.Edge.UpperTolerance, step.Sign > 0 ? step.Edge.UpperTolerance : -step.Edge.LowerTolerance,
                step.Edge.Unit, step.Edge.OriginInstancePath, step.Edge.Provenance, step.Edge.MateStableId, step.Edge.InterfaceStableId, step.Edge.SourceProvenance)).ToArray();
            if (!publicSemantics.Any(value => value.ExposedName == exposed.PublicFrom) || !publicSemantics.Any(value => value.ExposedName == exposed.PublicTo))
            { diagnostics.Add(new("assembly-exposed-relation-public-endpoint-missing", $"Exposed relation '{exposed.Name}' references a public semantic name that is not exposed.")); continue; }
            var nominal = contributions.Sum(item => item.Nominal); var lower = contributions.Sum(item => item.LowerTolerance); var upper = contributions.Sum(item => item.UpperTolerance);
            result.Add(new("assembly-public-relation:" + specializationIdentity + ":" + exposed.Name, exposed.PublicFrom, exposed.PublicTo,
                nominal, lower, upper, contributions[0].Unit, 1, definition.Name, exposed.Name, SourceProvenance: definition.Provenance, ExpandedContributors: contributions));
        }
        return result;
        static AssemblyPath Prefix(AssemblyPath path, string root) => path.Segments.FirstOrDefault() == root ? path : new([root, .. path.Segments]);
    }

    private static List<List<(DimensionalRelationIr Edge, int Sign)>> FindRelationPaths(string start, string end, IReadOnlyList<DimensionalRelationIr> edges, int limit)
    {
        var result = new List<List<(DimensionalRelationIr, int)>>();
        void Visit(string node, HashSet<string> visited, List<(DimensionalRelationIr, int)> path)
        {
            if (result.Count >= limit) return;
            if (node == end) { result.Add([.. path]); return; }
            foreach (var edge in edges.OrderBy(item => item.StableId, StringComparer.Ordinal))
            {
                var next = edge.FromSemanticValueId == node ? edge.ToSemanticValueId : edge.ToSemanticValueId == node ? edge.FromSemanticValueId : null;
                if (next is null || visited.Contains(next)) continue;
                visited.Add(next); path.Add((edge, edge.FromSemanticValueId == node ? 1 : -1)); Visit(next, visited, path); path.RemoveAt(path.Count - 1); visited.Remove(next);
            }
        }
        Visit(start, new([start], StringComparer.Ordinal), []); return result;
    }

    private static string NormalizeIdentity(string value) => Regex.Replace(value, @"\s+", string.Empty);

    private static AssemblyMemberSource? ParseTree(string body, string sourceIdentity, List<AssemblyDiagnostic> diagnostics,
        IReadOnlyList<AssemblyDefinitionSource>? definitions = null, IReadOnlyList<InterfaceDefinition>? interfaces = null,
        IDictionary<string, AssemblyMemberSource>? specializationCache = null)
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
                    ? Regex.Match(rest, @"^(?<name>[A-Za-z_]\w*)(?:\s*=\s*(?<definition>[A-Za-z_]\w*(?:\s*<[^>]+>)?))?$")
                    : Regex.Match(rest, @"^(?<name>[A-Za-z_]\w*)\s*=\s*(?<definition>[A-Za-z_]\w*(?:\s*<[^>]+>)?)$");
                if (!nodeMatch.Success) { diagnostics.Add(new("assembly-tree-invalid-tag", $"Invalid <{kind}> tree tag '{tag.Value}'.")); continue; }
                var node = new MutableNode(nodeMatch.Groups["name"].Value, kind == "Assembly" ? AssemblyInstanceKind.Assembly : AssemblyInstanceKind.Part,
                    kind == "Assembly" ? (nodeMatch.Groups["definition"].Success ? NormalizeIdentity(nodeMatch.Groups["definition"].Value) : nodeMatch.Groups["name"].Value) : nodeMatch.Groups["definition"].Value);
                if (kind == "Assembly" && nodeMatch.Groups["definition"].Success)
                {
                    var instantiated = InstantiateAssemblyDefinition(node.Name, node.Definition, definitions ?? [], interfaces ?? [], specializationCache ?? new Dictionary<string, AssemblyMemberSource>(StringComparer.Ordinal), sourceIdentity, diagnostics);
                    if (instantiated is not null)
                    {
                        node.Children.AddRange(instantiated.Children.Select(ToMutable));
                        node.Semantics.AddRange(instantiated.ExposedSemantics);
                        node.ExplicitTransform = instantiated.ExplicitTransform;
                        node.PlacementAuthority = instantiated.PlacementAuthority;
                        node.IsEncapsulatedDefinition = true;
                        node.SolvedAssemblyDefinition = instantiated.SolvedAssemblyDefinition;
                    }
                }
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
            node.ExplicitTransform, node.PlacementAuthority, node.IsEncapsulatedDefinition, node.SolvedAssemblyDefinition);
        return root is null ? null : Freeze(root);

        MutableNode ToMutable(AssemblyMemberSource source)
        {
            var mutable = new MutableNode(source.Name, source.Kind, source.DefinitionIdentity) { ExplicitTransform = source.ExplicitTransform, PlacementAuthority = source.PlacementAuthority, IsEncapsulatedDefinition = source.IsEncapsulatedDefinition, SolvedAssemblyDefinition = source.SolvedAssemblyDefinition };
            mutable.Semantics.AddRange(source.ExposedSemantics);
            mutable.Children.AddRange(source.Children.Select(ToMutable));
            return mutable;
        }
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
