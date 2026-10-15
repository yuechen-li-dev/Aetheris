using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Numerics;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Assembly;

/// <summary>Bounded parser for the M0 relational assembly lane. XML-like tags are used only for product-tree containment.</summary>
public sealed class AssemblyM0Parser
{
    /// <summary>Published-port bindings reused across a session's builds (BUILD-PERF-M1).</summary>
    internal DerivedValueCache<PublishedPortBinding>? PortCache { get; init; }
    public sealed record ParseResult(AssemblySource? Source, IReadOnlyList<AssemblyDiagnostic> Diagnostics, double ElapsedMilliseconds)
    { public bool IsSuccess => Source is not null && Diagnostics.All(x => x.Severity != AssemblyDiagnosticSeverity.Error); }

    public ParseResult ParseFile(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var loadDiagnostics = new List<AssemblyDiagnostic>();
        var dependencies = new List<AssemblySourceDependencyIr>();
        var allowedRoot = FindAllowedSourceRoot(fullPath);
        var source = LoadSourceGraph(fullPath, allowedRoot, [], dependencies, loadDiagnostics);
        if (source is null) return new(null, loadDiagnostics, 0);
        var parsed = Parse(source, fullPath);
        var diagnostics = loadDiagnostics.Concat(GraphDiagnostics(parsed.Diagnostics, fullPath, dependencies.Count)).ToArray();
        return new(parsed.Source is null ? null : parsed.Source with { SourceDependencies = dependencies.Concat(parsed.Source.SourceDependencies ?? []).DistinctBy(d => d.Path).ToArray() }, diagnostics, parsed.ElapsedMilliseconds);
    }

    public ParseResult ParseProject(FirmamentProjectSnapshot project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var diagnostics = new List<AssemblyDiagnostic>();
        var dependencies = new List<AssemblySourceDependencyIr>();
        var source = LoadProjectSource(project.RootDocument, project, [], dependencies, diagnostics);
        if (source is null) return new(null, diagnostics, 0);
        var parsed = Parse(source, project.RootDocument, project);
        return new(parsed.Source is null ? null : parsed.Source with { SourceDependencies = dependencies.Concat(parsed.Source.SourceDependencies ?? []).DistinctBy(d => d.Path).ToArray() },
            [.. diagnostics, .. GraphDiagnostics(parsed.Diagnostics, project.RootDocument, dependencies.Count)], parsed.ElapsedMilliseconds);
    }

    private static IEnumerable<AssemblyDiagnostic> GraphDiagnostics(IEnumerable<AssemblyDiagnostic> diagnostics, string root, int documents)
        => documents <= 1 ? diagnostics : diagnostics.Select(d => d.SourceSpan is { } span && span.Source == root
            ? d with { SourceSpan = span with { Source = root + "#expanded-includes" },
                LocationNote = "Location is in the expanded include graph; original included-file mapping is unavailable." } : d);

    private static string? LoadProjectSource(string path, FirmamentProjectSnapshot project,
        IReadOnlyList<string> stack, List<AssemblySourceDependencyIr> dependencies, List<AssemblyDiagnostic> diagnostics)
    {
        var cycleAt = stack.ToList().FindIndex(item => string.Equals(item, path, StringComparison.Ordinal));
        if (cycleAt >= 0)
        {
            diagnostics.Add(new("assembly-include-cycle", $"Compile-time Include cycle: {string.Join(" -> ", stack.Skip(cycleAt).Append(path))}."));
            return null;
        }
        if (!project.TryResolve(path, out var text))
        {
            diagnostics.Add(new("assembly-include-file-not-found", $"Project document '{path}' was not found."));
            return null;
        }
        if (stack.Count > 0 && !path.EndsWith(".firmament", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new("assembly-include-extension-invalid", $"Compile-time Include '{path}' must reference a .firmament semantic source file."));
            return null;
        }
        if (dependencies.Any(item => string.Equals(item.Path, path, StringComparison.Ordinal))) return string.Empty;
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
        dependencies.Add(new(path, hash, stack.Count == 0));
        var declarations = new List<string>();
        foreach (Match include in Regex.Matches(text, @"^[ \t]*(?:Include|include)\s+""(?<path>[^""]+)""\s*;", RegexOptions.CultureInvariant | RegexOptions.Multiline))
        {
            string childPath;
            try
            {
                childPath = FirmamentProjectSnapshot.NormalizePath(include.Groups["path"].Value);
            }
            catch (ArgumentException)
            {
                diagnostics.Add(new("assembly-include-outside-root", $"Include '{include.Groups["path"].Value}' in '{path}' escapes the project root or uses an invalid path."));
                return null;
            }
            var child = LoadProjectSource(childPath, project, [.. stack, path], dependencies, diagnostics);
            if (child is null) return null;
            declarations.Add(child);
        }
        declarations.Add(Regex.Replace(text, @"^[ \t]*(?:Include|include)\s+""[^""]+""\s*;[ \t]*(?:\r?\n)?", string.Empty, RegexOptions.CultureInvariant | RegexOptions.Multiline));
        return string.Join(Environment.NewLine, declarations);
    }

    internal static string FindAllowedSourceRoot(string path)
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(path)!);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        return Path.GetDirectoryName(path)!;
    }

    internal static string? LoadResource(string path, FirmamentProjectSnapshot? project,
        List<AssemblySourceDependencyIr> dependencies, List<AssemblyDiagnostic> diagnostics)
        => project is not null ? LoadProjectSource(path, project, [], dependencies, diagnostics)
            : LoadSourceGraph(Path.GetFullPath(path), FindAllowedSourceRoot(Path.GetFullPath(path)), [], dependencies, diagnostics);

    public ParseResult Parse(string input, string sourceIdentity = "<memory>", FirmamentProjectSnapshot? project = null)
    {
        var watch = Stopwatch.StartNew();
        var diagnostics = new List<AssemblyDiagnostic>();
        var syntax = FirmamentSyntaxParser.Parse(input, sourceIdentity);
        if (!syntax.IsSuccess)
            return new(null, syntax.Diagnostics.Select(d => new AssemblyDiagnostic(d.Code, d.Message)
                { SourceSpan = d.SourceSpan is { } span ? new(sourceIdentity, span.Start, span.Length) : null }).ToArray(), watch.Elapsed.TotalMilliseconds);
        input = FirmamentSourceSpelling.Normalize(input);
        var blankcommentsPhase = Aetheris.Kernel.Core.Diagnostics.BuildPerfTrace.Phase("m0.blank-comments");
        var source = Regex.Replace(input, "\"(?:\\\\.|[^\"\\\\])*\"|//[^\\r\\n]*", m => m.Value.StartsWith('"') ? m.Value : new string(' ',m.Length));
        blankcommentsPhase.Dispose();
        var pmiPhase = Aetheris.Kernel.Core.Diagnostics.BuildPerfTrace.Phase("m0.pmi");
        var annotations = AssemblyPmiAuthoring.Parse(ref source, sourceIdentity, diagnostics);
        pmiPhase.Dispose();
        if (diagnostics.Count > 0) return new(null, diagnostics, watch.Elapsed.TotalMilliseconds);
        var patternDiagnostics = new List<string>();
        var patternsPhase = Aetheris.Kernel.Core.Diagnostics.BuildPerfTrace.Phase("m0.patterns");
        var expandedPatterns = CanonicalStaticAuthoring.ExpandAssemblyPatterns(source,patternDiagnostics);
        patternsPhase.Dispose();
        foreach (var error in patternDiagnostics) diagnostics.Add(new("assembly-pattern-invalid",error));
        if (expandedPatterns is null || patternDiagnostics.Count>0) return new(null,diagnostics,watch.Elapsed.TotalMilliseconds);
        source = expandedPatterns.Source;
        // Subassembly is a first-class non-parameterized Assembly definition. It
        // lowers through the existing, locally solved definition authority.
        source = Regex.Replace(source, @"\bSubassembly\s+(?<name>[A-Za-z_]\w*)\s*\{",
            "Template < __Unit: __Unit > Assembly ${name} {", RegexOptions.CultureInvariant);
        var appearancePhase = Aetheris.Kernel.Core.Diagnostics.BuildPerfTrace.Phase("m0.appearance");
        var appearanceCatalog = AssemblyAppearanceAuthoring.Parse(ref source, diagnostics, out var appearanceSpans);
        appearancePhase.Dispose();
        var gearDocument = GearAuthoring.HasGearDefinitions(source)
            ? GearAuthoring.ParseDefinitions(source)
            : new GearAuthoringDocument("GearModel", [], [], []);
        var gearAuthorities = gearDocument.Gears.ToDictionary(gear => gear.Name, StringComparer.Ordinal);
        foreach (var diagnostic in gearDocument.Diagnostics.Where(item => item != GearAuthoring.Prefix + "declaration-missing"))
            diagnostics.Add(new("assembly-" + diagnostic.Split(':')[0], diagnostic));
        var conceptsPhase = Aetheris.Kernel.Core.Diagnostics.BuildPerfTrace.Phase("m0.concepts");
        var concepts = ParseAssemblyConcepts(source, sourceIdentity, diagnostics);
        conceptsPhase.Dispose();
        var interfacesPhase = Aetheris.Kernel.Core.Diagnostics.BuildPerfTrace.Phase("m0.interfaces");
        var interfaces = ParseInterfaces(source, sourceIdentity, diagnostics);
        interfacesPhase.Dispose();
        var layoutsPhase = Aetheris.Kernel.Core.Diagnostics.BuildPerfTrace.Phase("m0.layouts");
        var layout = AssemblyDatumAuthoring.ParseLayouts(source, sourceIdentity, diagnostics, out var partDeclarations);
        layoutsPhase.Dispose();
        foreach (var contract in interfaces.Where(i => i.DatumContract is not null))
            if (!layout.Any(d => d.Identity == contract.DatumContract!.Datum && d.Plane == d.Identity))
                diagnostics.Add(new("assembly-datum-plane-unresolved", $"Interface '{contract.Name}' requires a declared Concept plane or Axis '{contract.DatumContract!.Datum}'."));
        var declarationChars = partDeclarations.ToCharArray();
        foreach (Match header in Regex.Matches(source, @"\b(?:Template\s*<[^>]+>\s*Assembly|Assembly|Interface(?:\s*<[^>]*>)?)\s+[A-Za-z_]\w*(?:\s*:\s*[A-Za-z_]\w*)?\s*\{"))
        {
            var block = BalancedBody(source, header.Index + header.Length - 1, diagnostics, "assembly declaration");
            if (block is not null) Array.Fill(declarationChars, ' ', header.Index, header.Length + block.Length + 1);
        }
        var definitionSource = new string(declarationChars);
        foreach (var span in appearanceSpans.OrderByDescending(s => s.Start)) definitionSource = definitionSource.Remove(span.Start, span.Length);
        definitionSource = definitionSource.Trim();
        var portDependencies = new List<AssemblySourceDependencyIr>();
        var portCache = new Dictionary<string, IReadOnlyList<SemanticValue>>(StringComparer.Ordinal);
        IReadOnlyList<SemanticValue> Ports(string identity)
        {
            if (!portCache.TryGetValue(identity, out var ports))
                using (var portsPhase = Aetheris.Kernel.Core.Diagnostics.BuildPerfTrace.Phase("m0.ports")) portCache[identity] = ports = AssemblyPublishedPorts.BindAuthored(identity, definitionSource, sourceIdentity, diagnostics, project, portDependencies, PortCache);
            return ports;
        }
        var templateRanges = new List<(int Start, int Length)>();
        var assemblydefinitionsPhase = Aetheris.Kernel.Core.Diagnostics.BuildPerfTrace.Phase("m0.assembly-definitions");
        var assemblyDefinitions = ParseAssemblyDefinitions(source, sourceIdentity, interfaces, concepts, gearAuthorities, diagnostics, templateRanges, Ports, layout);
        assemblydefinitionsPhase.Dispose();
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
        var treePhase = Aetheris.Kernel.Core.Diagnostics.BuildPerfTrace.Phase("m0.tree");
        var tree = ParseTree(body, sourceIdentity, diagnostics, assemblyDefinitions, interfaces, specializationCache, gearAuthorities, Ports);
        treePhase.Dispose();
        if (tree is null) return Done(null);
        var mates = ParseMates(body, sourceIdentity, diagnostics);
        var relations = ParseRelations(body, diagnostics);
        var asserts = ParseAsserts(body, sourceIdentity, diagnostics);
        var anchorMatch = Regex.Match(RemoveBlocks(body, "Mate", @"Assert\s+ToleranceStackup"), @"\bAnchor\s*:\s*(?<path>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*;", RegexOptions.CultureInvariant);
        var anchor = anchorMatch.Success ? AssemblyPath.Parse(anchorMatch.Groups["path"].Value) : new AssemblyPath([tree.Name]);
        var frames = ParseFrameTransforms(body, sourceIdentity, diagnostics);
        var datumbindPhase = Aetheris.Kernel.Core.Diagnostics.BuildPerfTrace.Phase("m0.datum-bind");
        tree = AssemblyDatumAuthoring.Bind(body, tree, layout, interfaces, diagnostics, out var datumFrames, Ports);
        datumbindPhase.Dispose();
        frames = frames.Concat(datumFrames).ToArray();
        var result = new AssemblySource(assemblyHeader.Groups["name"].Value, tree, interfaces, mates, anchor, relations, asserts, sourceIdentity, definitionSource, assemblyDefinitions, SourceDependencies: portDependencies, FrameTransforms: frames, Patterns: expandedPatterns.Document?.Patterns, ConceptPoints: expandedPatterns.Document?.ConceptPoints, AppearanceCatalog: appearanceCatalog, Annotations: annotations);
        return Done(result);

        ParseResult Done(AssemblySource? value) { watch.Stop(); return new(value, diagnostics, watch.Elapsed.TotalMilliseconds); }
    }

    private static IReadOnlyList<AssemblyFrameTransformSource> ParseFrameTransforms(string body, string sourceIdentity, List<AssemblyDiagnostic> diagnostics)
    {
        var frames = new List<AssemblyFrameTransformSource>();
        foreach (Match header in Regex.Matches(body, @"\bFrameTransform\s+(?<name>[A-Za-z_]\w*)\s*\{"))
        {
            var fields = BalancedBody(body, header.Index + header.Length - 1, diagnostics, "FrameTransform");
            if (fields is not null && AssemblyFrameAuthoring.Parse(header.Groups["name"].Value, fields, "From", sourceIdentity, diagnostics) is { } frame)
                frames.Add(frame);
        }
        return frames;
    }

    private static string? LoadSourceGraph(string path, string allowedRoot, IReadOnlyList<string> stack,
        List<AssemblySourceDependencyIr> dependencies, List<AssemblyDiagnostic> diagnostics)
    {
        var fullPath = Path.GetFullPath(path);
        var rootPrefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(allowedRoot)) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) && !string.Equals(fullPath, Path.TrimEndingDirectorySeparator(Path.GetFullPath(allowedRoot)), StringComparison.OrdinalIgnoreCase))
        { diagnostics.Add(new("assembly-include-outside-root", $"Include '{path}' resolves outside allowed root '{allowedRoot}'.")); return null; }
        var cycleAt = stack.ToList().FindIndex(item => string.Equals(item, fullPath, StringComparison.OrdinalIgnoreCase));
        if (cycleAt >= 0)
        {
            var cycle = stack.Skip(cycleAt).Append(fullPath).Select(Path.GetFileName);
            diagnostics.Add(new("assembly-include-cycle", $"Compile-time Include cycle: {string.Join(" -> ", cycle)}."));
            return null;
        }
        if (!File.Exists(fullPath))
        { diagnostics.Add(new("assembly-include-file-not-found", $"Included Firmament source '{fullPath}' was not found.")); return null; }
        if (stack.Count > 0 && !string.Equals(Path.GetExtension(fullPath), ".firmament", StringComparison.OrdinalIgnoreCase))
        { diagnostics.Add(new("assembly-include-extension-invalid", $"Compile-time Include '{fullPath}' must reference a .firmament semantic source file.")); return null; }
        var text = File.ReadAllText(fullPath);
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
        if (dependencies.Any(item => string.Equals(item.Path, fullPath, StringComparison.OrdinalIgnoreCase))) return string.Empty;
        dependencies.Add(new(fullPath, hash, stack.Count == 0));
        var declarations = new List<string>();
        foreach (Match include in Regex.Matches(text, @"^[ \t]*(?:Include|include)\s+""(?<path>[^""]+)""\s*;", RegexOptions.CultureInvariant | RegexOptions.Multiline))
        {
            var relative = include.Groups["path"].Value;
            var childPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fullPath)!, relative));
            var child = LoadSourceGraph(childPath, allowedRoot, [.. stack, fullPath], dependencies, diagnostics);
            if (child is null) return null;
            declarations.Add(child);
        }
        var local = Regex.Replace(text, @"^[ \t]*(?:Include|include)\s+""[^""]+""\s*;[ \t]*(?:\r?\n)?", string.Empty, RegexOptions.CultureInvariant | RegexOptions.Multiline);
        declarations.Add(local);
        return string.Join(Environment.NewLine, declarations);
    }

    private static IReadOnlyList<AssemblyConceptDefinition> ParseAssemblyConcepts(string source, string sourceIdentity, List<AssemblyDiagnostic> diagnostics)
    {
        var result = new List<AssemblyConceptDefinition>();
        foreach (Match header in Regex.Matches(source, @"\bConcept\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant))
        {
            var body = BalancedBody(source, header.Index + header.Length - 1, diagnostics, "Concept");
            if (body is null) continue;
            var members = Regex.Matches(body, @"\b(?<name>[A-Za-z_]\w*)\s*:\s*(?<type>Part|Assembly|Mate|Semantic)\b", RegexOptions.CultureInvariant)
                .Select(match => new AssemblyConceptMemberDefinition(match.Groups["name"].Value, match.Groups["type"].Value)).ToArray();
            if (members.Length > 0)
                result.Add(new(header.Groups["name"].Value, members, SemanticSourceSpan.Generated(sourceIdentity)));
        }
        return result;
    }

    private static IReadOnlyList<InterfaceDefinition> ParseInterfaces(string source, string sourceIdentity, List<AssemblyDiagnostic> diagnostics)
    {
        var result = new List<InterfaceDefinition>();
        var syntax = FirmamentSyntaxParser.Parse(source, sourceIdentity);
        foreach (var declaration in FirmamentSyntaxEvidence.Declarations(syntax.Document).Where(d => d.Type.Name.Text == "Interface"))
        {
            if (declaration.Name is null || declaration.Body is null)
            { diagnostics.Add(AssemblyInterfaceSyntax.Diagnostic("assembly-interface-malformed", "Interface requires a name and body.", sourceIdentity, declaration.Span)); continue; }
            var name = declaration.Name.Text;
            var explicitFamily = declaration.Type.Span.Length > declaration.Type.Name.Span.Length;
            if (!AssemblyInterfaceSyntax.HasContractHeader(declaration))
            { diagnostics.Add(AssemblyInterfaceSyntax.Diagnostic("assembly-interface-header-unsupported", "Interface admits a name, optional compiler-owned family and body; placement clauses and other header annotations are not supported.", sourceIdentity, declaration.Span)); continue; }
            var familyName = declaration.Type.Arguments.Count == 1 && declaration.Type.Arguments[0].Value is ReferenceSyntax reference
                ? reference.Name.Text : "Custom";
            if (!Enum.TryParse<MechanicalInterfaceFamily>(familyName, out var family) || !Enum.IsDefined(family)
                || explicitFamily && (declaration.Type.Arguments.Count != 1 || declaration.Type.Arguments[0] is not { Name: null, Default: null, Value: ReferenceSyntax }))
            { diagnostics.Add(AssemblyInterfaceSyntax.Diagnostic("assembly-interface-family-invalid",
                $"Invalid Interface family header '{source.Substring(declaration.Type.Span.Start, declaration.Type.Span.Length)}'; expected Custom, Fixed, Axial, Revolute, Prismatic or Gear.", sourceIdentity, declaration.Type.Span)); continue; }
            foreach (var error in syntax.Diagnostics.Where(d => d.Severity is "error" or "fatal"
                && d.SourceSpan is { } location && location.Start >= declaration.Span.Start && location.Start < declaration.Span.Start + declaration.Span.Length))
                diagnostics.Add(new(error.Code, error.Message) { SourceSpan = new(sourceIdentity, error.SourceSpan!.Start, error.SourceSpan.Length) });
            if (declaration.Body.Statements.OfType<FieldSyntax>().Any(f => FirmamentSyntaxFields.SameField(f.Name.Text, "Datum")))
            {
                var datumFamily = family == MechanicalInterfaceFamily.Revolute ? MechanicalInterfaceFamily.Revolute : MechanicalInterfaceFamily.Fixed;
                if (family is not (MechanicalInterfaceFamily.Fixed or MechanicalInterfaceFamily.Revolute)) diagnostics.Add(new("assembly-datum-family-invalid", "Datum contracts require Interface<Fixed> or Interface<Revolute>."));
                var contract = AssemblyInterfaceSyntax.BindDatumContract(declaration, sourceIdentity, diagnostics);
                if (datumFamily == MechanicalInterfaceFamily.Revolute && contract.Members.Count != 1)
                    diagnostics.Add(new("assembly-axis-revolute-member-count", "A datum Revolute contract requires exactly one moving occurrence."));
                if (result.Any(i => i.Name == name)) diagnostics.Add(new("assembly-interface-duplicate-name", $"Interface '{name}' is duplicated."));
                else result.Add(new("interface:" + name, name, [], [], Family: datumFamily, DatumContract: contract));
                continue;
            }
            var roles = declaration.Body.Statements.OfType<RoleRequirementSyntax>()
                .Select(r => new InterfaceRoleDefinition(r.Name.Text, r.Capabilities.Select(c => c.Text).ToArray())).ToArray();
            var requirements = AssemblyInterfaceSyntax.BindRelations(declaration, sourceIdentity, diagnostics);
            if (family is MechanicalInterfaceFamily.Fixed or MechanicalInterfaceFamily.Prismatic
                && requirements.Length > 0
                && (requirements.Length != 1 || requirements[0].Kind != PlacementConstraintKind.FrameCoincident))
                diagnostics.Add(new("assembly-interface-invalid-contract-lowering",
                    $"Interface<{family}> '{name}' requires one frame relationship; authored geometric Lower/Mate relations cannot replace its typed contract."));
            var fit = AssemblyInterfaceSyntax.BindFit(declaration, sourceIdentity, diagnostics);
            var free = declaration.Body.Statements.OfType<InterfaceMotionSyntax>()
                .Select(m => m.Kind.Text + ":" + m.Axis.Text).ToArray();
            AssemblyInterfaceSyntax.CheckContract(declaration, roles, requirements, sourceIdentity, diagnostics);
            if (explicitFamily && roles.Length == 0)
            {
                var capabilities = family switch
                {
                    MechanicalInterfaceFamily.Fixed => new[] { "DatumFrameCapable" },
                    MechanicalInterfaceFamily.Revolute => new[] { "AxisCapable", "PlaneCapable" },
                    MechanicalInterfaceFamily.Prismatic => new[] { "DatumFrameCapable" },
                    MechanicalInterfaceFamily.Axial => new[] { "AxisCapable" },
                    MechanicalInterfaceFamily.Gear => new[] { "GearCapable" },
                    _ => Array.Empty<string>()
                };
                roles = [new("A", capabilities), new("B", capabilities)];
            }
            if (explicitFamily && requirements.Length == 0)
            {
                requirements = family switch
                {
                    MechanicalInterfaceFamily.Fixed => [new(PlacementConstraintKind.FrameCoincident, "A", ".", "B", ".")],
                    MechanicalInterfaceFamily.Axial => [new(PlacementConstraintKind.AxisCoincident, "A", "Axis", "B", "Axis")],
                    MechanicalInterfaceFamily.Revolute => [new(PlacementConstraintKind.AxisCoincident, "A", "Axis", "B", "Axis"), new(PlacementConstraintKind.PlaneCoincident, "A", "Seat", "B", "Seat")],
                    MechanicalInterfaceFamily.Prismatic => [new(PlacementConstraintKind.FrameCoincident, "A", ".", "B", ".")],
                    _ => []
                };
            }
            if (explicitFamily && free.Length == 0)
                free = family switch
                {
                    MechanicalInterfaceFamily.Axial => ["translation:along-axis", "rotation:about-axis"],
                    MechanicalInterfaceFamily.Revolute => ["rotation:about-axis"],
                    MechanicalInterfaceFamily.Prismatic => ["translation:along-axis"],
                    _ => []
                };
            var predicates = AssemblyInterfaceSyntax.BindPredicates(declaration, sourceIdentity, diagnostics);
            var options = AssemblyInterfaceSyntax.BindOptions(declaration, family, sourceIdentity, diagnostics);
            if (roles.Length == 0) diagnostics.Add(new("assembly-interface-no-roles", $"Interface '{name}' must define at least one Role or use a compiler-owned typed family."));
            if (result.Any(item => item.Name == name)) { diagnostics.Add(new("assembly-interface-duplicate-name", $"Interface '{name}' is declared more than once.")); continue; }
            result.Add(new($"interface:{name}", name, roles, requirements, fit, free,
                new SemanticSourceSpan(sourceIdentity + "#expanded-assembly-scope", declaration.Span.Start, declaration.Span.Length),
                options.Continuity, options.Correspondence, options.GapTolerance, family, explicitFamily, predicates, options.Gear, options.Seating));
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
        public SemanticValue? TypedEndpoint { get; set; }
        public AssemblyFramePlacementSource? FramePlacement { get; set; }
        public AssemblyDatumSeat? DatumSeat { get; set; }
        public AssemblyMaterialSelection? MaterialSelection { get; set; }
        public IReadOnlyList<AssemblyRouteInput>? RouteInputs { get; set; }
    }

    private static IReadOnlyList<AssemblyDefinitionSource> ParseAssemblyDefinitions(string source, string sourceIdentity,
        IReadOnlyList<InterfaceDefinition> interfaces, IReadOnlyList<AssemblyConceptDefinition> concepts,
        IReadOnlyDictionary<string, GearAir> gearAuthorities,
        List<AssemblyDiagnostic> diagnostics, List<(int Start, int Length)> ranges,
        Func<string, IReadOnlyList<SemanticValue>>? publishedPorts = null,
        IReadOnlyList<AssemblyLayoutDatum>? layout = null)
    {
        var result = new List<AssemblyDefinitionSource>();
        foreach (Match template in Regex.Matches(source,
            @"\bTemplate\s*<\s*(?<parameter>[A-Za-z_]\w*)\s*:\s*(?<type>[A-Za-z_]\w*)\s*>\s*Assembly\s+(?<name>[A-Za-z_]\w*)(?:\s*:\s*(?<concept>[A-Za-z_]\w*))?\s*\{",
            RegexOptions.CultureInvariant))
        {
            var body = BalancedBody(source, template.Index + template.Length - 1, diagnostics, "Template Assembly");
            if (body is null) continue;
            var close = template.Index + template.Length + body.Length + 1;
            ranges.Add((template.Index, close - template.Index));
            var root = ParseTree(body, sourceIdentity, diagnostics, result, interfaces, new Dictionary<string, AssemblyMemberSource>(StringComparer.Ordinal), gearAuthorities, publishedPorts);
            if (root is null) continue;
            root = AssemblyDatumAuthoring.Bind(body, root, layout ?? [], interfaces, diagnostics, out var datumFrames, publishedPorts);
            if (root.Children.Any(c => c.FramePlacement?.JointFamily == MechanicalInterfaceFamily.Revolute))
                diagnostics.Add(new("assembly-axis-internal-motion-unsupported", "Reusable definitions have rigid solved children. Declare a Revolute mate on the complete occurrence in its owning root assembly."));
            var exposed = ParseAssemblyExposes(body, root, sourceIdentity, diagnostics);
            root = root with { ExposedSemantics = exposed, IsEncapsulatedDefinition = true };
            var name = template.Groups["name"].Value;
            var claimedConcept = template.Groups["concept"].Success ? template.Groups["concept"].Value : null;
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
            var localMates = ParseMates(body, sourceIdentity, diagnostics);
            if (claimedConcept is not null)
                ValidateAssemblyConcept(name, claimedConcept, concepts, root, localMates, diagnostics);
            result.Add(new(name, template.Groups["parameter"].Value, template.Groups["type"].Value, root, name,
                [new("assembly-template-definition", name, template.Groups["parameter"].Value + ":" + template.Groups["type"].Value, SemanticSourceSpan.Generated(sourceIdentity)),
                 .. claimedConcept is null ? [] : new[] { new SemanticProvenance("assembly-concept-satisfaction", claimedConcept, name, SemanticSourceSpan.Generated(sourceIdentity)) },
                 .. staticProvenance],
                anchor, localMates, ParseRelations(body, diagnostics), ParseAsserts(body, sourceIdentity, diagnostics), exposedRelations, claimedConcept,
                ParseFrameTransforms(body, sourceIdentity, diagnostics).Concat(datumFrames).ToArray()));
        }
        return result;
    }

    private static void ValidateAssemblyConcept(string templateName, string claimedConcept,
        IReadOnlyList<AssemblyConceptDefinition> concepts, AssemblyMemberSource root, IReadOnlyList<MateSource> mates,
        List<AssemblyDiagnostic> diagnostics)
    {
        var concept = concepts.SingleOrDefault(item => item.Name == claimedConcept);
        if (concept is null)
        {
            diagnostics.Add(new("assembly-concept-unknown", $"Assembly Template '{templateName}' claims unknown Concept '{claimedConcept}'."));
            return;
        }
        foreach (var member in concept.Members)
        {
            var actual = member.Type switch
            {
                "Part" => root.Children.Any(item => item.Name == member.Name && item.Kind == AssemblyInstanceKind.Part),
                "Assembly" => root.Children.Any(item => item.Name == member.Name && item.Kind == AssemblyInstanceKind.Assembly),
                "Mate" => mates.Any(item => item.Name == member.Name),
                "Semantic" => root.ExposedSemantics.Any(item => item.ExposedName == member.Name),
                _ => false
            };
            if (!actual)
                diagnostics.Add(new("assembly-concept-missing-member", $"Assembly Template '{templateName}' does not satisfy Concept '{claimedConcept}': required {member.Type} member '{member.Name}' is missing."));
        }
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
        foreach (Match expose in Regex.Matches(block, @"\b(?<type>Semantic|Axis|Plane|Point|Dimension|DatumFrame|Gear)\s+(?<name>[A-Za-z_]\w*)\s*=\s*(?<path>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*;", RegexOptions.CultureInvariant))
        {
            var alias = expose.Groups["name"].Value;
            if (result.Any(value => value.ExposedName == alias)) { diagnostics.Add(new("assembly-expose-duplicate-name", $"Assembly Expose declares '{alias}' more than once.")); continue; }
            var segments = expose.Groups["path"].Value.Split('.');
            var child = root.Children.SingleOrDefault(item => item.Name == segments[0]);
            var memberIndex = 1;
            // Structural pattern groups are owned by this definition. Traverse
            // those groups, but stop at reusable component privacy boundaries.
            while (child is not null && !child.IsEncapsulatedDefinition && memberIndex < segments.Length
                && child.Children.SingleOrDefault(item => item.Name == segments[memberIndex]) is { } nested)
            { child = nested; memberIndex++; }
            SemanticValue? value = child is null ? null : segments.Length == memberIndex ? child.TypedEndpoint
                : child.ExposedSemantics.SingleOrDefault(item => item.ExposedName == segments[memberIndex]);
            for (var i = memberIndex + 1; value is not null && i < segments.Length; i++) value = value.ExposedMembers.GetValueOrDefault(segments[i]);
            if (value is null)
            {
                diagnostics.Add(new("assembly-expose-unresolved-path", $"Assembly Expose '{alias}' references nonexistent internal path '{expose.Groups["path"].Value}'."));
                continue;
            }
            var expectedType = expose.Groups["type"].Value;
            var semanticType = expectedType switch { "DatumFrame" => "AssemblyDatumFrame", "Dimension" => "Length", _ => expectedType };
            if (expectedType != "Semantic" && !string.Equals(value.Type.Name, semanticType, StringComparison.Ordinal))
            {
                diagnostics.Add(new("assembly-expose-type-mismatch", $"Assembly Expose '{alias}' declares {expectedType} but '{expose.Groups["path"].Value}' is {value.Type}."));
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
        var bareName = Regex.Match(identity, @"^(?<name>[A-Za-z_]\w*)$", RegexOptions.CultureInvariant);
        var definitionName = application.Success ? application.Groups["name"].Value : bareName.Success ? bareName.Groups["name"].Value : string.Empty;
        var definition = definitions.SingleOrDefault(item => item.Name == definitionName);
        if (definition is null) { diagnostics.Add(new("assembly-template-unresolved-definition", $"Assembly definition '{definitionName}' was not found.")); return null; }
        if (application.Success && application.Groups["parameter"].Value != definition.ParameterName) { diagnostics.Add(new("assembly-template-argument-mismatch", $"Assembly Template '{definition.Name}' expects argument '{definition.ParameterName}'.")); return null; }
        if (!application.Success && definition.ParameterName != "__Unit") { diagnostics.Add(new("assembly-template-argument-mismatch", $"Parameterized Assembly Template '{definition.Name}' requires '{definition.ParameterName}: ...'.")); return null; }
        var argument = application.Success ? application.Groups["argument"].Value.Trim() : "__Unit";
        var specializationIdentity = application.Success ? NormalizeIdentity(identity) : definition.Name;
        if (specializationCache.TryGetValue(specializationIdentity, out var cached))
            return RenameOccurrence(cached, occurrenceName);
        AssemblyMemberSource Replace(AssemblyMemberSource item) => item with
        {
            Name = item.Name,
            // Preserve member projection (for example Spec.Body) so a domain
            // compiler can bind the nested typed Record through its ordinary
            // Template frontend.
            DefinitionIdentity = Regex.Replace(item.DefinitionIdentity, $@"(?<prefix>:\s*)\b{Regex.Escape(definition.ParameterName)}\b", "${prefix}" + argument),
            Children = item.Children.Select(Replace).ToArray(),
            Provenance = [.. item.Provenance ?? [], new("assembly-template-specialization", definition.Name, specializationIdentity, SemanticSourceSpan.Generated(sourceIdentity))],
            IsEncapsulatedDefinition = item.IsEncapsulatedDefinition
        };
        var specializedRoot = Replace(definition.LocalRoot) with { IsEncapsulatedDefinition = false };
        var localSource = new AssemblySource(definition.Name, specializedRoot, interfaces, definition.LocalMates ?? [], definition.LocalAnchor ?? new([definition.Name]),
            definition.LocalDimensionalRelations ?? [], definition.LocalStackupAsserts ?? [], sourceIdentity, FrameTransforms: definition.FrameTransforms);
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
            // The solved definition stores world frames within its local root.
            // A reusable member tree must store parent-relative frames, otherwise
            // each nested occurrence applies its ancestors' transforms again.
            AssemblyTransform? relative = null;
            if (item != specializedRoot)
            {
                var world = Aetheris.Kernel.Core.Math.Transform3D.FromRowMajor(localInstance.ResolvedTransform!.Matrix);
                var parent = local.Ir.Instances.Single(instance => instance.StableId == localInstance.ParentStableId);
                var parentWorld = Aetheris.Kernel.Core.Math.Transform3D.FromRowMajor(parent.ResolvedTransform!.Matrix);
                relative = new((world * parentWorld.Inverse()).ToRowMajor());
            }
            return item with { Children = item.Children.Select(child => ApplyLocal(child, path.Append(child.Name))).ToArray(), ExplicitTransform = relative, FramePlacement = null };
        }
        specializedRoot = ApplyLocal(specializedRoot, new([specializedRoot.Name]));
        var publicSemantics = specializedRoot.ExposedSemantics.Select(value => BindPublicSemantic(value, local.Ir, sourceIdentity)).ToArray();
        var publicRelations = BuildPublicRelations(definition, specializationIdentity, publicSemantics, local.Ir, diagnostics);
        var stableId = "assembly-definition:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(specializationIdentity)))[..16];
        var definitionIr = new AssemblyDefinitionIr(stableId, specializationIdentity, definition.Name, specializationIdentity,
            [.. definition.Provenance, new("assembly-template-specialization", definition.Name, specializationIdentity, SemanticSourceSpan.Generated(sourceIdentity))],
            local.Ir.Instances, local.Ir.Mates, local.Ir.Placements, local.Ir.DimensionalRelations, local.Ir.ToleranceStackups,
            publicSemantics, publicRelations, watch.Elapsed.TotalMilliseconds,
            local.Ir.PlacementConstraints, local.Ir.Datums, local.Ir.DatumMateSolutions, local.Ir.FitResults, local.Ir.AxisSeats);
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
        if (reference!.Value.TryBinding<TypedSemanticAuthorityBinding<GearAir>>(out var gear))
        {
            var owner = localIr.Instances.Where(instance => Flatten(instance.SemanticRoot).Any(value => value.StableIdentity == reference.Value.StableIdentity))
                .OrderByDescending(instance => instance.Path.Segments.Count).First();
            bindings.Add(gear with { RelativeOccurrencePath = [.. owner.Path.Segments.Skip(1), .. gear.RelativeOccurrencePath ?? []] });
        }
        try { bindings.Add(AssemblyWorldQuery.Resolve(localIr, reference!.Value.StableIdentity)); } catch (InvalidOperationException) { }
        return new(exposed.StableIdentity, exposed.Type, exposed.Capabilities.Values, bindings,
            exposed.ExposedMembers.Values.Select(member => reference!.Value.ExposedMembers.TryGetValue(member.ExposedName!, out var local)
                ? BindMember(member, local) : member), exposed.Provenance,
            exposed.AuthoredSourceSpan, SemanticSourceSpan.Generated(sourceIdentity), exposed.ExposedName);

        SemanticValue BindMember(SemanticValue member, SemanticValue local)
        {
            // Typed endpoints (notably Gear) can synthesize public members that
            // are not source children. Their typed authority retains ownership;
            // ordinary published composites rebase only corresponding children.
            var transformed = member.Bindings.Where(binding => binding is not (ExactAxisBinding or ExactPlaneBinding or ExactDatumFrameBinding or ExactPointBinding)).ToList();
            try { transformed.Add(AssemblyWorldQuery.Resolve(localIr, local.StableIdentity)); } catch (InvalidOperationException) { }
            return new(member.StableIdentity, member.Type, member.Capabilities.Values, transformed,
                member.ExposedMembers.Values.Select(child => local.ExposedMembers.TryGetValue(child.ExposedName!, out var value)
                    ? BindMember(child, value) : child),
                member.Provenance, member.AuthoredSourceSpan, member.GeneratedSourceSpan, member.ExposedName);
        }

        static IEnumerable<SemanticValue> Flatten(SemanticValue value)
        {
            yield return value;
            foreach (var child in value.ExposedMembers.Values)
                foreach (var descendant in Flatten(child)) yield return descendant;
        }
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
        IDictionary<string, AssemblyMemberSource>? specializationCache = null,
        IReadOnlyDictionary<string, GearAir>? gearAuthorities = null,
        Func<string, IReadOnlyList<SemanticValue>>? publishedPorts = null)
    {
        // One bounded generic argument list is admitted in a Part definition. The
        // inner '>' belongs to the Firmament Template application, not the XML-like tag.
        var tagPattern = new Regex(@"<(?<close>/)?(?<kind>Assembly|Part|Panel)\s*(?<rest>[^<>]*(?:<[^<>]*>[^<>]*)?)>", RegexOptions.CultureInvariant);
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
                    ? Regex.Match(rest, @"^(?<name>[A-Za-z_]\w*)(?:\s*=\s*(?<definition>[A-Za-z_]\w*(?:\s*<[^>]*>)?))?$")
                    : Regex.Match(rest, @"^(?<name>[A-Za-z_]\w*)\s*=\s*(?<definition>[A-Za-z_]\w*(?:\s*<[^>]*>)?)$");
                if (!nodeMatch.Success) { diagnostics.Add(new("assembly-tree-invalid-tag", $"Invalid <{kind}> tree tag '{tag.Value}'.")); continue; }
                var node = new MutableNode(nodeMatch.Groups["name"].Value, kind == "Assembly" ? AssemblyInstanceKind.Assembly : kind == "Panel" ? AssemblyInstanceKind.Panel : AssemblyInstanceKind.Part,
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
                        node.TypedEndpoint = instantiated.TypedEndpoint;
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
                if (open.node.Kind is AssemblyInstanceKind.Part or AssemblyInstanceKind.Panel)
                {
                    open.node.MaterialSelection = AssemblyAppearanceAuthoring.Selection(nodeBody, diagnostics);
                    open.node.RouteInputs = AssemblyRouteBindings.Parse(nodeBody, diagnostics);
                    open.node.Semantics.AddRange(ParseSemantics(nodeBody, open.node.Definition, sourceIdentity, diagnostics));
                    if (open.node.Kind == AssemblyInstanceKind.Part && publishedPorts is not null)
                        open.node.Semantics.AddRange(publishedPorts(open.node.Definition));
                    if (open.node.Kind == AssemblyInstanceKind.Part && gearAuthorities?.GetValueOrDefault(open.node.Definition) is { } gear)
                    {
                        var endpoint = GearEndpoint(gear, sourceIdentity);
                        open.node.Semantics.Add(endpoint);
                        open.node.TypedEndpoint = endpoint;
                    }
                }
                var nestedTag = nodeBody.IndexOf('<');
                var placementBody = open.node.Kind == AssemblyInstanceKind.Assembly && nestedTag >= 0 ? nodeBody[..nestedTag] : nodeBody;
                var placement = Regex.Match(placementBody, @"\bPlacement\s+(?<authority>ImportedOccurrence|LegacyExplicit)\s*=\s*\[(?<matrix>[^]]+)\]\s*;", RegexOptions.CultureInvariant);
                var readable = Regex.Matches(placementBody, @"\bPlacement\s*\{");
                if (readable.Count > 1 || (readable.Count > 0 && placement.Success))
                    diagnostics.Add(new("assembly-placement-authority-conflict", $"Instance '{open.node.Name}' declares multiple placements."));
                if (readable.Count > 0)
                {
                    var fields = BalancedBody(placementBody, readable[0].Index + readable[0].Length - 1, diagnostics, "Placement");
                    if (fields is not null)
                    {
                        var from = Regex.Match(fields, @"\bFrom\s*:\s*(?<path>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*;?");
                        if (!from.Success) diagnostics.Add(new("assembly-placement-missing-source", $"Placement '{open.node.Name}' requires From."));
                        var targetFields = from.Success ? fields.Remove(from.Index,from.Length) : fields;
                        if (AssemblyFrameAuthoring.Parse(open.node.Name, targetFields, "To", sourceIdentity, diagnostics) is { } target)
                        {
                            open.node.FramePlacement = new(from.Groups["path"].Value, target);
                            open.node.PlacementAuthority = PlacementAuthority.AuthoredFrame;
                        }
                    }
                }
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
            node.ExplicitTransform, node.PlacementAuthority, node.IsEncapsulatedDefinition, node.SolvedAssemblyDefinition, node.TypedEndpoint, node.FramePlacement, node.DatumSeat, node.MaterialSelection, RouteInputs: node.RouteInputs);
        return root is null ? null : Freeze(root);

        MutableNode ToMutable(AssemblyMemberSource source)
        {
            var mutable = new MutableNode(source.Name, source.Kind, source.DefinitionIdentity) { ExplicitTransform = source.ExplicitTransform, PlacementAuthority = source.PlacementAuthority, IsEncapsulatedDefinition = source.IsEncapsulatedDefinition, SolvedAssemblyDefinition = source.SolvedAssemblyDefinition, TypedEndpoint = source.TypedEndpoint, FramePlacement = source.FramePlacement, DatumSeat = source.DatumSeat, MaterialSelection = source.MaterialSelection, RouteInputs = source.RouteInputs };
            mutable.Semantics.AddRange(source.ExposedSemantics);
            mutable.Children.AddRange(source.Children.Select(ToMutable));
            return mutable;
        }
    }

    private static SemanticValue GearEndpoint(GearAir gear, string sourceIdentity)
    {
        var id = "gear-definition:" + gear.Name;
        var axis = new ExactAxisBinding(0, 0, 0, gear.Axis[0], gear.Axis[1], gear.Axis[2], id + ":axis");
        var direction = Vector3.Normalize(new((float)gear.Axis[0], (float)gear.Axis[1], (float)gear.Axis[2]));
        var x = Math.Abs(Vector3.Dot(direction, Vector3.UnitX)) < .9f ? Vector3.Normalize(Vector3.Cross(Vector3.UnitY, direction)) : Vector3.Normalize(Vector3.Cross(Vector3.UnitZ, direction));
        var y = Vector3.Normalize(Vector3.Cross(direction, x));
        var frame = new ExactDatumFrameBinding(0, 0, 0, x.X, x.Y, x.Z, y.X, y.Y, y.Z, direction.X, direction.Y, direction.Z, id + ":frame");
        return new(id, new("Gear"), [new GearCapability(), new AxisCapability(), new DatumFrameCapability()],
            [new TypedSemanticAuthorityBinding<GearAir>(new("Gear"), gear, id), axis, frame],
            [new SemanticValue(id + ":Axis", new("Axis"), [new AxisCapability()], [axis], exposedName: "Axis"),
             new SemanticValue(id + ":Frame", new("AssemblyDatumFrame"), [new DatumFrameCapability()], [frame], exposedName: "Frame")],
            [new("gear-authority", gear.Name, gear.Family.ToString(), new(sourceIdentity, gear.SourceSpan.Start, gear.SourceSpan.Length))],
            new(sourceIdentity, gear.SourceSpan.Start, gear.SourceSpan.Length), exposedName: "Gear");
    }

    internal static IEnumerable<SemanticValue> ParseSemantics(string body, string definition, string sourceIdentity, List<AssemblyDiagnostic> diagnostics)
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
            foreach (Match frame in Regex.Matches(block, @"\bDatumFrame\s+(?<name>[A-Za-z_]\w*)\s*=\s*\[(?<o>[^]]+)\]\s+x\s*\[(?<x>[^]]+)\]\s+y\s*\[(?<y>[^]]+)\]\s+z\s*\[(?<z>[^]]+)\]\s*;", RegexOptions.CultureInvariant))
            {
                var o = Vector(frame.Groups["o"].Value); var x = Vector(frame.Groups["x"].Value); var y = Vector(frame.Groups["y"].Value); var z = Vector(frame.Groups["z"].Value);
                if (o is null || x is null || y is null || z is null || !ValidFrame(x, y, z)) { diagnostics.Add(new("assembly-datum-frame-invalid", $"Semantic '{name}' DatumFrame requires a finite orthonormal right-handed X/Y/Z basis.")); continue; }
                var binding = new ExactDatumFrameBinding(o[0], o[1], o[2], x[0], x[1], x[2], y[0], y[1], y[2], z[0], z[1], z[2], $"definition:{definition}:{name}:{frame.Groups["name"].Value}");
                members.Add(new SemanticValue(binding.FrameStableId, new("AssemblyDatumFrame"), [new DatumFrameCapability()], [binding], exposedName: frame.Groups["name"].Value));
                caps.Add(new DatumFrameCapability()); if (!bindings.OfType<ExactDatumFrameBinding>().Any()) bindings.Add(binding);
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
            var duplicate = members.GroupBy(m => m.ExposedName).FirstOrDefault(g => g.Count() > 1);
            if (duplicate is not null)
            {
                diagnostics.Add(new("assembly-semantic-duplicate-member", $"Semantic '{name}' repeats member '{duplicate.Key}'."));
                continue;
            }
            var authoredMembers = Regex.Matches(block, @"\b[A-Za-z_]\w*\s+[A-Za-z_]\w*\s*=");
            if (authoredMembers.Count != members.Count)
                diagnostics.Add(new("assembly-semantic-unresolved-member", $"Semantic '{name}' contains an unresolved or malformed member."));
            yield return new SemanticValue($"definition:{definition}:{name}", new("Concept"), caps.DistinctBy(x => x.GetType()), bindings, members,
                [new("part-definition", definition, name, SemanticSourceSpan.Generated(sourceIdentity))], exposedName: name);
        }
    }

    private static IReadOnlyList<MateSource> ParseMates(string body, string sourceIdentity, List<AssemblyDiagnostic> diagnostics)
        => AssemblyInterfaceSyntax.BindMates(body, sourceIdentity, diagnostics);

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

    internal static string? BalancedBody(string source, int openingBrace, List<AssemblyDiagnostic> diagnostics, string construct)
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
    private static bool ValidFrame(double[] x, double[] y, double[] z)
    {
        static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        var cross = new[] { x[1] * y[2] - x[2] * y[1], x[2] * y[0] - x[0] * y[2], x[0] * y[1] - x[1] * y[0] };
        const double tolerance = 1e-6;
        return Math.Abs(Dot(x, x) - 1) <= tolerance && Math.Abs(Dot(y, y) - 1) <= tolerance && Math.Abs(Dot(z, z) - 1) <= tolerance
            && Math.Abs(Dot(x, y)) <= tolerance && Math.Abs(Dot(x, z)) <= tolerance && Math.Abs(Dot(y, z)) <= tolerance
            && Dot(cross, z) >= 1 - tolerance;
    }
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
