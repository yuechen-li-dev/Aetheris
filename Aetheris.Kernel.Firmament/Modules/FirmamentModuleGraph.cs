using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Modules;

public sealed record FirmamentInventoryBinding(string Name, string Kind, string SourceFile, string Export,
    string DefinitionIdentity, SemanticSourceSpan SourceSpan);

/// <summary>Per-compilation dependency and export authority. Inventories bind module
/// references, never inject imported declarations into the caller. Legacy includes
/// retain their existing declaration composition through an explicitly mapped projection.</summary>
[FirmamentConstruct("Inventory", "Inventory", Context = "Document", Entry = "Inventory Record Components { }",
    Description = "Compile-time catalog of typed Part, Subassembly, Assembly and Scene module references.")]
internal sealed class FirmamentModuleGraph
{
    internal sealed class Module(string path, string text, DocumentSyntax syntax)
    {
        internal string Path { get; } = path;
        internal string Text { get; } = text;
        internal DocumentSyntax Syntax { get; } = syntax;
        internal List<Module> Includes { get; } = [];
        internal List<Edit> Edits { get; } = [];
    }
    internal sealed record Reference(string Identity, string Kind, string Path, Module? Module,
        DeclarationSyntax? Export, string Definition, SemanticSourceSpan Origin)
    {
        internal SemanticSourceSpan DeclarationOrigin { get; init; } = Export is null ? Origin : new(Path,Export.Span.Start,Export.Span.Length);
    }
    internal sealed record Edit(FirmamentV2SourceSpan Span, string Text);
    internal sealed record Segment(int Start, int Length, SemanticSourceSpan Origin, bool Exact);
    internal sealed record Projection(string Text, IReadOnlyList<Segment> Segments, string SourceIdentity)
    {
        internal SemanticSourceSpan? Original(FirmamentV2SourceSpan span) => Segments
            .Where(s => span.Start >= s.Start && span.Start + span.Length <= s.Start + s.Length)
            .OrderBy(s => s.Length).Select(s => s.Exact
                ? new SemanticSourceSpan(s.Origin.Source, s.Origin.Start + span.Start - s.Start, span.Length) : s.Origin).FirstOrDefault();
        internal AssemblyDiagnostic Map(AssemblyDiagnostic diagnostic) => diagnostic.SourceSpan is { Length: > 0 } span && span.Source == SourceIdentity
            && Original(new(span.Start, span.Length)) is { } original ? diagnostic with { SourceSpan = original } : diagnostic;
    }
    private sealed class Scope(Scope? parent)
    {
        internal readonly Dictionary<string, Dictionary<string, Reference>> Inventories = new(StringComparer.Ordinal);
        internal readonly HashSet<string> Values = new(StringComparer.Ordinal);
        internal Dictionary<string, Reference>? Resolve(string name) => Inventories.GetValueOrDefault(name)
            ?? (Values.Contains(name) ? null : parent?.Resolve(name));
    }
    private readonly Dictionary<string, Module> modules;
    private readonly Dictionary<string, Reference> references = new(StringComparer.Ordinal);
    private readonly List<string> active = [];
    private readonly string allowedRoot;
    private readonly string identityRoot;
    internal FirmamentProjectSnapshot? Project { get; }
    internal List<AssemblyDiagnostic> Diagnostics { get; } = [];
    internal Module Root { get; private set; } = null!;
    internal bool HasModuleBindings => references.Count > 0;
    internal List<FirmamentInventoryBinding> InventoryBindings { get; } = [];
    internal IReadOnlyList<AssemblySourceDependencyIr> Dependencies => modules.Values.Select(m =>
        new AssemblySourceDependencyIr(m.Path, Hash(m.Text), ReferenceEquals(m, Root))).Concat(references.Values
            .Where(r => r.Module is null && File.Exists(r.Path)).Select(r => new AssemblySourceDependencyIr(r.Path,
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(r.Path))), false))).DistinctBy(d => d.Path).ToArray();

    private FirmamentModuleGraph(string root, FirmamentProjectSnapshot? project)
    {
        Project = project;
        allowedRoot = project is null ? (!root.StartsWith('<') ? AssemblyM0Parser.FindAllowedSourceRoot(Path.GetFullPath(root)) : "") : "";
        identityRoot = project is null ? (!root.StartsWith('<') ? Path.GetDirectoryName(Path.GetFullPath(root))! : "")
            : (root.Contains('/') ? root[..root.LastIndexOf('/')] : "");
        modules = new(project is null ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    }
    internal static FirmamentModuleGraph Load(string text, string identity, FirmamentProjectSnapshot? project = null)
    {
        var graph = new FirmamentModuleGraph(identity, project);
        graph.Root = graph.Read(identity, text, null)!;
        return graph;
    }
    internal static bool HasImports(string source) => FirmamentLanguageAnalysisService.Lex(source)
        .Any(t => t.Shape == "identifier" && (t.Text.Equals("Inventory", StringComparison.OrdinalIgnoreCase) || t.Text is "Include" or "include"));
    internal bool TryReference(string identity, out Reference reference) => references.TryGetValue(Base(identity), out reference!);
    private static string Base(string identity) => identity.Contains('<') ? identity[..identity.IndexOf('<')].Trim() : identity.Trim();
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private Module? Read(string path, string? supplied, SemanticSourceSpan? use, bool include = false)
    {
        var cycleAt=active.FindIndex(p => modules.Comparer.Equals(p,path));
        if (cycleAt >= 0)
        {
            var chain=active.Skip(cycleAt).Append(path);
            if (include && Project is null) chain=chain.Select(p => Path.GetFileName(p));
            Error(include ? "assembly-include-cycle" : "firmament-module-cycle", "Module dependency cycle: " + string.Join(" -> ",chain),use);
            return null;
        }
        if (modules.TryGetValue(path, out var existing)) return existing;
        if (active.Count >= 64 || modules.Count >= 1024)
        { Error("firmament-module-limit", "A project supports at most 64 dependency levels and 1024 source modules.", use); return null; }
        string text;
        if (supplied is not null) text = supplied;
        else if (Project is not null && Project.TryResolve(path, out var document)) text = document;
        else if (Project is null && File.Exists(path)) text = File.ReadAllText(path);
        else { Error("assembly-include-file-not-found", $"Module '{path}' was not found.", use); return null; }
        var parsed = FirmamentSyntaxParser.Parse(text, path);
        var module = new Module(path, text, parsed.Document); modules.Add(path, module);
        if (!parsed.IsSuccess)
        {
            Diagnostics.AddRange(parsed.Diagnostics.Select(d => new AssemblyDiagnostic(d.Code, d.Message)
                { SourceSpan = d.SourceSpan is { } span ? new(path, span.Start, span.Length) : use }));
            return module;
        }
        active.Add(path);
        Bind(module, module.Syntax.Statements, new(null));
        active.RemoveAt(active.Count - 1);
        return module;
    }

    private void Bind(Module module, IReadOnlyList<StatementSyntax> statements, Scope scope)
    {
        foreach (var declaration in statements.OfType<DeclarationSyntax>().Where(d => !IsInventory(d) && d.Name is not null))
            scope.Values.Add(declaration.Name!.Text);
        foreach (var malformed in statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text == "Inventory"
            || d.Modifiers.Any(m => m.Name.Text == "Inventory") && !IsInventory(d)))
            Error("firmament-inventory-header-invalid", "Inventory must qualify a Record: Inventory Record Name { ... }.", At(module, malformed.Span));
        foreach (var directive in statements.OfType<DirectiveSyntax>().Where(d => d.Kind == DirectiveKind.Include))
        {
            if (directive.Value is not LiteralSyntax { Kind: LiteralKind.String })
            { Error("firmament-module-include-invalid", "include requires a quoted Firmament locator.", At(module, directive.Span)); continue; }
            var locator = Locator(module,(LiteralSyntax)directive.Value);
            if (locator is null) continue;
            var childPath = Resolve(module, locator, "Firmament", At(module, directive.Span), include: true);
            if (childPath is not null && Read(childPath, null, At(module, directive.Span), include: true) is { } child) module.Includes.Add(child);
            module.Edits.Add(new(directive.Span, Blank(module.Text, directive.Span)));
        }
        foreach (var inventory in statements.OfType<DeclarationSyntax>().Where(IsInventory))
        {
            if (inventory.Name is null || inventory.Body is null || inventory.HasParameterList || inventory.Occurrence
                || inventory.Annotation is not null || inventory.Initializer is not null || inventory.ResultType is not null
                || inventory.Clauses.Count > 0 || inventory.Modifiers.Count != 1 || inventory.Type.Span.Length != inventory.Type.Name.Span.Length
                || inventory.Modifiers[0].Span.Length != inventory.Modifiers[0].Name.Span.Length)
            { Error("firmament-inventory-header-invalid", "Use Inventory Record Name { member: Kind = Adapter(\"locator\").Export }.", At(module, inventory.Span)); continue; }
            var fields = new Dictionary<string, Reference>(StringComparer.Ordinal);
            if (!scope.Inventories.TryAdd(inventory.Name.Text, fields)
                || statements.OfType<DeclarationSyntax>().Any(d => !IsInventory(d) && d.Name?.Text == inventory.Name.Text))
                Error("firmament-inventory-duplicate-name", $"Inventory '{inventory.Name.Text}' conflicts with another declaration in this scope.", At(module, inventory.Name.Span));
            foreach (var statement in inventory.Body.Statements)
            {
                if (statement is not FieldSyntax { Value: TypedValueSyntax typed } field
                    || typed.Type is not ReferenceSyntax kind || kind.Name.Text is not ("Part" or "Subassembly" or "Assembly" or "Scene"))
                { Error("firmament-inventory-field-invalid", "Inventory members require an explicit Part, Subassembly, Assembly or Scene type and one resource binding.", At(module, statement.Span)); continue; }
                var origin = At(module, field.Span);
                var export = typed.Value is MemberSyntax member ? member.Member.Text : null;
                var value = typed.Value is MemberSyntax selected ? selected.Target : typed.Value;
                if (value is not CallSyntax { Target: ReferenceSyntax adapter } call || adapter.Name.Text is not ("Firmament" or "Step")
                    || call.Arguments.Count != 1 || call.Arguments[0].Name is not null
                    || call.Arguments[0].Value is not LiteralSyntax { Kind: LiteralKind.String })
                { Error("firmament-inventory-resource-invalid", "Use Firmament(\"locator\").Export or Step(\"locator\").", origin); continue; }
                if (adapter.Name.Text == "Firmament" && export is null || adapter.Name.Text == "Step" && (export is not null || kind.Name.Text != "Part"))
                { Error("firmament-inventory-kind-mismatch", "Firmament requires a named export. Step currently supplies one Part, with no member selector.", origin); continue; }
                var locator = Locator(module,(LiteralSyntax)call.Arguments[0].Value);
                if (locator is null) continue;
                var path = Resolve(module, locator, adapter.Name.Text, origin);
                if (path is null) continue;
                Module? child = null; DeclarationSyntax? declaration = null;
                if (adapter.Name.Text == "Firmament")
                {
                    child = Read(path, null, origin);
                    if (child is null) continue;
                    var candidates = Exports(child).Where(d => d.Name?.Text == export).ToArray();
                    if (candidates.Length != 1)
                    { Error("firmament-inventory-export-unresolved", $"Module '{locator}' must export exactly one declaration named '{export}'; found {candidates.Length}.", origin); continue; }
                    declaration = candidates[0];
                    var actual = declaration.Type.Name.Text;
                    if (!(kind.Name.Text == "Part" ? actual is "Model" or "Struct" or "Part" : actual == kind.Name.Text))
                    { Error("firmament-inventory-kind-mismatch", $"'{locator}.{export}' is {actual}, but '{field.Name.Text}' requires {kind.Name.Text}.", origin); continue; }
                    if (declaration.Modifiers.Any(m => m.Name.Text == "Template") && actual is not ("Subassembly" or "Struct" or "Model" or "Part"))
                    { Error("firmament-inventory-specialization-required", "Specialize this Template in its defining module and export its concrete Part, Subassembly, Assembly or Scene result.", origin); continue; }
                }
                var identity = "__Module_" + Hash(IdentityPath(path) + "\0" + kind.Name.Text + "\0" + export)[..20];
                var reference = new Reference(identity, kind.Name.Text, path, child, declaration, export ?? "", origin)
                    { DeclarationOrigin = declaration is null ? origin : DeclarationOrigin(declaration) };
                references.TryAdd(identity, reference);
                InventoryBindings.Add(new(inventory.Name.Text+"."+field.Name.Text,kind.Name.Text,path,export ?? "",identity,origin));
                if (!fields.TryAdd(field.Name.Text, reference)) Error("firmament-inventory-duplicate-field", $"Inventory member '{field.Name.Text}' is repeated.", origin);
            }
            module.Edits.Add(new(inventory.Span, Blank(module.Text, inventory.Span)));
        }
        foreach (var declaration in statements.OfType<DeclarationSyntax>().Where(d => !IsInventory(d)))
        {
            if (declaration.Initializer is { } initializer)
            {
                // Concise includes also expose concrete Parts under their authored
                // names. Existing Template applications keep their established owner.
                if (module.Includes.Count > 0 && declaration.Occurrence && declaration.Type.Name.Text == "Part" && initializer is ReferenceSyntax bare)
                {
                    var candidates = Exports(module).Where(d => d.Name?.Text == bare.Name.Text && d.Type.Name.Text is "Model" or "Struct" or "Part"
                        && !d.Modifiers.Any(m => m.Name.Text is "Template" or "Concept")).ToArray();
                    if (candidates.Length > 1)
                        Error("firmament-module-export-ambiguous", $"Included Part '{bare.Name.Text}' is ambiguous. Use an Inventory Record to qualify its source.", At(module,bare.Span));
                    else if (candidates.Length == 1)
                    {
                        var identity = "__Module_" + Hash(IdentityPath(module.Path)+"\0included-part\0"+bare.Name.Text)[..20];
                        references.TryAdd(identity,new Reference(identity,"Part",module.Path,module,candidates[0],bare.Name.Text,At(module,bare.Span))
                            { DeclarationOrigin = DeclarationOrigin(candidates[0]) });
                        module.Edits.Add(new(bare.Span,identity));
                    }
                }
                var target = initializer is SpecializationSyntax specialization ? specialization.Target : initializer;
                if (target is MemberSyntax { Target: ReferenceSyntax owner } member)
                {
                    var fields = scope.Resolve(owner.Name.Text);
                    if (fields is null)
                    {
                        if (FirmamentSyntaxTraversal.Declarations(module.Syntax).Any(d => IsInventory(d) && d.Name?.Text == owner.Name.Text))
                            Error("firmament-inventory-not-visible", $"Inventory '{owner.Name.Text}' is shadowed or not visible in this scope.", At(module, target.Span));
                        continue;
                    }
                    if (!declaration.Occurrence)
                        Error("firmament-inventory-use-unsupported", "Use an Inventory module reference as a Part, Assembly or Scene occurrence definition.", At(module, target.Span));
                    else if (!fields.TryGetValue(member.Member.Text, out var reference))
                        Error("firmament-inventory-member-unresolved", $"Inventory '{owner.Name.Text}' has no member '{member.Member.Text}'.", At(module, member.Span));
                    else if (!(declaration.Type.Name.Text == "Assembly" ? reference.Kind is "Assembly" or "Subassembly" : declaration.Type.Name.Text == reference.Kind))
                        Error("firmament-inventory-kind-mismatch", $"A {declaration.Type.Name.Text} occurrence cannot use {reference.Kind} '{owner.Name.Text}.{member.Member.Text}'.", At(module, target.Span));
                    else if (initializer is SpecializationSyntax && reference.Export?.Modifiers.Where(m => m.Name.Text == "Template")
                        .SelectMany(m => m.Arguments).Any(a => a.Value is TypeParameterSyntax) == true)
                        Error("firmament-inventory-type-argument-unsupported", "Imported Templates currently accept compile-time values, not caller-defined types. Specialize type parameters in the defining module and export a concrete product.", At(module, initializer.Span));
                    else if (initializer is SpecializationSyntax && reference.Kind == "Part" && reference.Export is { } partExport
                        && partExport.Modifiers.Where(m => m.Name.Text == "Template").SelectMany(m => m.Arguments).Any(a =>
                            a.Value is ReferenceSyntax parameterType && Exports(reference.Module!)
                                .Any(d => d.Type.Name.Text == "Record" && d.Name?.Text == parameterType.Name.Text)))
                        Error("firmament-inventory-part-record-argument-unsupported", "Imported Part Templates currently admit scalar/enum parameters. Bind Record/type parameters inside their defining module or a typed Subassembly; caller Records are never resolved against private module values.", At(module, initializer.Span));
                    else if (initializer is SpecializationSyntax && reference.Export?.Type.Name.Text is not ("Subassembly" or "Struct" or "Model" or "Part"))
                        Error("firmament-inventory-specialization-required", "This module kind does not yet admit imported Template specialization.", At(module, initializer.Span));
                    else if (initializer is not SpecializationSyntax && reference.Export?.Modifiers.Any(m => m.Name.Text == "Template") == true)
                        Error("firmament-inventory-specialization-required", "This Template export requires named typed arguments at its occurrence.", At(module, initializer.Span));
                    else module.Edits.Add(new(target.Span, reference.Identity));
                }
            }
            if (declaration.Body is not null)
            {
                var bodyScope=new Scope(scope);
                foreach (var parameter in declaration.Parameters.Concat(declaration.Modifiers.Where(m => m.Name.Text == "Template").SelectMany(m => m.Arguments)))
                    if (parameter.Name is not null) bodyScope.Values.Add(parameter.Name.Text);
                Bind(module,declaration.Body.Statements,bodyScope);
            }
        }
        foreach (var mapping in statements.OfType<MappingSyntax>())
        {
            var bodyScope=new Scope(scope); bodyScope.Values.Add(mapping.Key.Text);
            Bind(module,mapping.Value is StatementBlockSyntax block ? block.Body.Statements : [mapping.Value],bodyScope);
        }
    }
    private SemanticSourceSpan DeclarationOrigin(DeclarationSyntax declaration)
    {
        var owner=modules.Values.First(m => m.Syntax.Statements.Any(d => ReferenceEquals(d,declaration)));
        return At(owner,declaration.Span);
    }
    private string IdentityPath(string path) => Project is null ? Path.GetRelativePath(identityRoot,path).Replace('\\','/')
        : identityRoot.Length == 0 ? path : Path.GetRelativePath(identityRoot,path).Replace('\\','/');
    private string? Locator(Module module,LiteralSyntax literal)
    {
        try { return FirmamentSyntaxValues.Reference(literal); }
        catch (System.Text.Json.JsonException)
        { Error("firmament-module-locator-invalid", "Invalid quoted locator. Use / between path segments and valid string escapes.",At(module,literal.Span)); return null; }
    }
    private static bool IsInventory(DeclarationSyntax d) => d.Type.Name.Text == "Record" && d.Modifiers.Any(m => m.Name.Text == "Inventory");
    private static SemanticSourceSpan At(Module module, FirmamentV2SourceSpan span) => new(module.Path, span.Start, span.Length);
    private static string Blank(string text, FirmamentV2SourceSpan span) => new(text.Substring(span.Start, span.Length).Select(c => c is '\r' or '\n' ? c : ' ').ToArray());
    private static IEnumerable<DeclarationSyntax> Exports(Module module) => module.Includes.SelectMany(Exports)
        .Concat(module.Syntax.Statements.OfType<DeclarationSyntax>().Where(d => !IsInventory(d))).Distinct<DeclarationSyntax>(ReferenceEqualityComparer.Instance);
    private void Error(string code, string message, SemanticSourceSpan? span) => Diagnostics.Add(new(code, message) { SourceSpan = span });

    private string? Resolve(Module owner, string locator, string adapter, SemanticSourceSpan origin, bool include = false)
    {
        if (Project is null && owner.Path.StartsWith('<'))
        { Error("firmament-module-context-required", "Resource binding requires a source file identity or a project snapshot.", origin); return null; }
        string path;
        try
        {
            if (string.IsNullOrWhiteSpace(locator) || (!include || Project is not null) && (Path.IsPathRooted(locator) || locator.Contains(':')) || locator.Contains('#') || locator.Contains('?')) throw new ArgumentException();
            path = Project is null ? Path.GetFullPath(Path.Combine(Path.GetDirectoryName(owner.Path)!, locator.Replace('/', Path.DirectorySeparatorChar)))
                : FirmamentProjectSnapshot.NormalizePath((owner.Path.Contains('/') ? owner.Path[..(owner.Path.LastIndexOf('/') + 1)] : "") + locator);
            if (Project is null && !path.StartsWith(Path.TrimEndingDirectorySeparator(allowedRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException();
        }
        catch (ArgumentException) { Error("assembly-include-outside-root", $"Resource '{locator}' escapes the project root or has an invalid path.", origin); return null; }
        var extensions = adapter == "Step" ? new[] { ".step", ".stp" } : new[] { ".firmament", ".firmasm" };
        var extension = Path.GetExtension(path);
        // Preserve the historical snapshot Include spelling when its document-relative target is absent.
        if (include && Project is not null && !Project.TryResolve(path, out _))
        {
            try { var legacy = FirmamentProjectSnapshot.NormalizePath(locator); if (Project.TryResolve(legacy, out _)) path = legacy; }
            catch (ArgumentException) { /* The enclosing path check owns rejection. */ }
        }
        if (extension.Length > 0 && !extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        { Error(include ? "assembly-include-extension-invalid" : "firmament-inventory-extension-invalid", $"{adapter} resource '{locator}' requires {string.Join(" or ", extensions)}.", origin); return null; }
        if (adapter == "Step" && Project is not null)
        { Error("assembly-profile-external-resource-unsupported", "This project snapshot contains source documents only; provide STEP through a file-backed project.", origin); return null; }
        bool Exists(string candidate) => Project is null ? File.Exists(candidate) : Project.TryResolve(candidate, out _);
        var matches = extension.Length == 0 ? extensions.Select(e => path + e).Where(Exists).ToArray() : Exists(path) ? [path] : Array.Empty<string>();
        if (matches.Length != 1)
        { Error(matches.Length == 0 ? "assembly-include-file-not-found" : "firmament-module-locator-ambiguous", $"Resource '{locator}' matched {matches.Length} files. " + (matches.Length == 0 ? "Check its path." : "Specify its extension to select one."), origin); return null; }
        return matches[0];
    }

    internal Projection ProjectSource(Module module, DeclarationSyntax? selected = null, bool assemblyRoot = false, bool partContext = false)
    {
        var text = new StringBuilder(); var segments = new List<Segment>(); var emitted = new HashSet<Module>();
        void Append(Module node)
        {
            if (!emitted.Add(node)) return;
            foreach (var include in node.Includes) Append(include);
            var edits = node.Edits.ToList();
            if (partContext)
                foreach (var d in node.Syntax.Statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text is "Assembly" or "Subassembly" or "Scene"))
                    edits.Add(new(d.Span, Blank(node.Text, d.Span)));
            if (selected is not null)
            {
                foreach (var d in node.Syntax.Statements.OfType<DeclarationSyntax>().Where(d => d != selected && !IsInventory(d)
                    && !d.Modifiers.Any(m => m.Name.Text is "Template" or "Concept") && d.Type.Name.Text is "Model" or "Struct" or "Part" or "Assembly" or "Subassembly" or "Scene"))
                    edits.Add(new(d.Span, Blank(node.Text, d.Span)));
                if (node.Syntax.Statements.Contains(selected) && assemblyRoot && selected.Type.Name.Text == "Subassembly" && !selected.Modifiers.Any(m => m.Name.Text == "Template")) edits.Add(new(selected.Type.Name.Span, "Assembly"));
                if (node.Syntax.Statements.Contains(selected) && selected.Type.Name.Text == "Part") edits.Add(new(selected.Type.Name.Span, "Struct"));
            }
            var cursor = 0;
            foreach (var edit in edits.OrderBy(e => e.Span.Start).ThenByDescending(e => e.Span.Length))
            {
                if (edit.Span.Start < cursor) continue; // an erased enclosing Inventory/root owns its entire span
                Copy(cursor, edit.Span.Start - cursor);
                segments.Add(new(text.Length, edit.Text.Length, At(node, edit.Span), edit.Text.Length == edit.Span.Length)); text.Append(edit.Text);
                cursor = edit.Span.Start + edit.Span.Length;
            }
            Copy(cursor, node.Text.Length - cursor); text.AppendLine();
            void Copy(int start, int length) { if (length == 0) return; segments.Add(new(text.Length, length, new(node.Path, start, length), true)); text.Append(node.Text, start, length); }
        }
        // Keep the established include transport order and newline spelling for geometry identities.
        if (module.Includes.Count == 0 && module.Edits.Count == 0 && selected is null && !partContext)
            return new(module.Text, [new(0, module.Text.Length, new(module.Path, 0, module.Text.Length), true)], module.Path);
        Append(module);
        return new(text.ToString(), segments, module.Path);
    }

    internal Reference LocalPart(Module module, string definition)
    {
        if (TryReference(definition, out var existing)) return existing;
        // The reference identifies the declaration; QualifyParts/PartIdentity
        // carry invocation arguments exactly once on each occurrence.
        definition = Base(definition);
        var identity = "__Module_" + Hash(IdentityPath(module.Path) + "\0local-part\0" + definition)[..20];
        var reference = new Reference(identity, "Part", module.Path, module, null, definition, new(module.Path, 0, 0));
        references.TryAdd(identity, reference); return reference;
    }

    internal AssemblyM0Parser.ParseResult ParseAssembly(Reference reference, string? use = null, string? callerSource = null)
    {
        var projection = ProjectSource(reference.Module!, reference.Export, assemblyRoot: true);
        if (reference.Export!.Modifiers.Any(m => m.Name.Text == "Template"))
        {
            var codes = new List<string>();
            var application = FirmamentSyntaxParser.ParseExpression(use ?? reference.Identity).Expression as SpecializationSyntax;
            if (application is null)
                return new(null, [new("firmament-template-missing-required-argument", $"Subassembly '{reference.Definition}' requires its typed Template arguments.") { SourceSpan = reference.Origin }], 0);
            var arguments = new Dictionary<string, FirmamentV2TemplateExpansion.HostArgument>(StringComparer.Ordinal);
            var signature = FirmamentV2TemplateExpansion.Inspect(projection.Text, codes).SingleOrDefault(t => t.Name == reference.Definition);
            foreach (var argument in application.Arguments)
            {
                var name = argument.Name?.Text ?? "";
                if (arguments.ContainsKey(name)) { codes.Add(FirmamentV2TemplateExpansion.DuplicateArgument + ":" + name); continue; }
                var expression = FirmamentSyntaxWriter.Expression(argument.Value);
                var parameter = signature?.Parameters.SingleOrDefault(p => p.Name == name);
                var records = FirmamentV2TemplateExpansion.InspectRecords(projection.Text, codes);
                var isRecord = records.Any(r => r.Name == parameter?.TypeName);
                var fields = isRecord ? FirmamentV2TemplateExpansion.BindRecordExpression(callerSource ?? "", parameter!.TypeName,
                    expression, "__ModuleArgument_" + name, codes, hostView: true) : null;
                arguments[name] = new(expression, isRecord ? parameter!.TypeName : null, fields);
            }
            var specialized = codes.Count == 0 ? FirmamentV2TemplateExpansion.ExpandHostInvocation(projection.Text,
                reference.Definition, reference.Definition, arguments, codes) : null;
            if (specialized is null || codes.Count > 0)
                return new(null, codes.Select(code => new AssemblyDiagnostic(code.Split(':')[0], code) { SourceSpan = reference.Origin }).ToArray(), 0);
            var concrete = specialized.Source;
            var declaration = FirmamentSyntaxParser.Parse(concrete).Document.Statements.OfType<DeclarationSyntax>()
                .Single(d => d.Name?.Text == reference.Definition && d.Type.Name.Text == "Subassembly");
            concrete = concrete.Remove(declaration.Type.Name.Span.Start, declaration.Type.Name.Span.Length).Insert(declaration.Type.Name.Span.Start, "Assembly");
            // Expanded offsets cannot be mapped exactly. Preserve the declaration's
            // honest enclosing origin rather than pretending generated spans are authored.
            projection = new(concrete, [new(0, concrete.Length, reference.DeclarationOrigin, false)], reference.Path);
        }
        return new AssemblyM0Parser().ParseBound(projection.Text, reference.Path, this, reference.Module!, projection, reference.Export);
    }
    internal AssemblyMemberSource QualifyParts(Module module, AssemblyMemberSource root) => root with
    {
        DefinitionIdentity = root.Kind == AssemblyInstanceKind.Part ? LocalPart(module, root.DefinitionIdentity).Identity + (root.DefinitionIdentity.Contains('<') ? root.DefinitionIdentity[root.DefinitionIdentity.IndexOf('<')..] : "") : root.DefinitionIdentity,
        Children = root.Children.Select(child => QualifyParts(module, child)).ToArray()
    };
    internal IReadOnlyList<SemanticValue> Ports(Reference reference, string use, List<AssemblyDiagnostic> diagnostics,
        DerivedValueCache<PublishedPortBinding>? cache = null)
    {
        if (reference.Module is null || reference.Kind != "Part") return [];
        var projection = ProjectSource(reference.Module, reference.Export, partContext: true);
        var identity = PartIdentity(reference, use);
        return reference.Export is null || reference.Export.Modifiers.Any(m => m.Name.Text == "Template")
            ? AssemblyPublishedPorts.BindAuthored(identity, projection.Text, reference.Path, diagnostics, Project, [], cache)
            : AssemblyPublishedPorts.Read(projection.Text, reference.Definition, use, reference.Path, diagnostics, includeWinding: true);
    }
    internal string PartIdentity(Reference reference, string use)
    {
        var suffix = use.Contains('<') ? use[use.IndexOf('<')..] : "";
        return reference.Definition + (suffix.Length > 0 ? suffix : reference.Export?.Modifiers.Any(m => m.Name.Text == "Template") == true ? "<>" : "");
    }
}
