using System.Text.Json;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Modules;

internal sealed partial class FirmamentModuleGraph
{
    internal FirmamentDependencyLock? Locked { get; private set; }
    internal HashSet<string> SurgicalAssets { get; } = new(StringComparer.Ordinal);
    internal string LockPath => Project is null ? Path.Combine(identityRoot, FirmamentDependencyLock.FileName)
        : (identityRoot.Length == 0 ? "" : identityRoot + "/") + FirmamentDependencyLock.FileName;
    internal string Relative(string path) => Project is null ? Path.GetRelativePath(allowedRoot, path).Replace('\\', '/') : path;

    private void ReadLock()
    {
        string? text = Project is null ? File.Exists(LockPath) ? File.ReadAllText(LockPath) : null
            : Project.TryResolve(LockPath, out var content) ? content : null;
        if (text is null) return;
        try { Locked = FirmamentDependencyLock.Parse(text); }
        catch (ArgumentException error) { Error("firmament-lock-invalid", error.Message, new(LockPath, 0, text.Length)); }
    }

    private void CheckLock(bool hasRootIdentity)
    {
        if (Locked is null) return;
        if (hasRootIdentity && Locked.Root != Relative(Root.Path))
        { Error("firmament-lock-root-mismatch", "The lock belongs to '" + Locked.Root + "', not this project root.", new(LockPath, 0, 0)); return; }
        foreach (var dependency in Dependencies.Where(d => !d.IsRoot))
        {
            var path = Relative(dependency.Path);
            var locked = Locked.Dependencies.SingleOrDefault(d => d.Path == path);
            var format = FirmamentProjectSnapshot.IsStepAsset(path) ? "step" : "firmament";
            var kind = format == "step" ? "sha256-bytes" : "sha256-utf8";
            if (locked is null || locked.Format != format || locked.HashKind != kind
                || !locked.Sha256.Equals(dependency.Sha256, StringComparison.OrdinalIgnoreCase))
                Error("firmament-lock-dependency-mismatch", "Dependency '" + path + "' differs from the lock or has no matching entry. Reinspect affected edits before updating it.", new(LockPath, 0, 0));
        }
    }

    private void BindInlineSteps(Module module, IReadOnlyList<StatementSyntax> statements, Scope scope)
    {
        foreach (var step in statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text == "InlineStep" && d.Body is not null))
        {
            var fields = step.Body!.Statements.OfType<FieldSyntax>().ToArray();
            var from = fields.Where(f => f.Name.Text is "from" or "From").ToArray();
            var paths = fields.Where(f => f.Name.Text is "path" or "Path").ToArray();
            string? path = null;
            if (from.Length > 0)
            {
                Reference? reference = from[0].Value is MemberSyntax { Target: ReferenceSyntax catalog } member
                    ? scope.Resolve(catalog.Name.Text)?.GetValueOrDefault(member.Member.Text)
                    : from[0].Value is ReferenceSyntax direct ? scope.ResolveDirect(direct.Name.Text) : null;
                if (from.Length != 1 || paths.Length > 0 || reference is not { Kind: "Part", Module: null, ImportedBody: null })
                { Error("firmament-inline-step-source-unresolved", "from requires one imported STEP Part reference and cannot coexist with path.", At(module, step.Span)); continue; }
                path = reference.Path;
                var folder = Path.GetDirectoryName(module.Path);
                var relative = Path.GetRelativePath(string.IsNullOrEmpty(folder) ? "." : folder, path).Replace('\\', '/');
                module.Edits.Add(new(from[0].Span, "path: " + JsonSerializer.Serialize(relative) + ";"));
            }
            else if (paths is [{ Value: LiteralSyntax { Kind: LiteralKind.String } literal }])
            {
                var locator = Locator(module, literal);
                if (locator is not null) path = Resolve(module, locator, "Step", At(module, paths[0].Span));
            }
            if (path is null) continue;
            Asset(path);
            if (statements.OfType<DeclarationSyntax>().Any(d => d.Type.Name.Text is "Recognize" or "Replace" or "Remove"
                && (d.Name?.Text == step.Name?.Text || d.Name?.Text.StartsWith(step.Name!.Text + ".", StringComparison.Ordinal) == true))) SurgicalAssets.Add(Relative(path));
            var locked = Locked?.Dependencies.SingleOrDefault(d => d.Path == Relative(path));
            if (locked is not null && !fields.Any(f => f.Name.Text is "sha256" or "Sha256" or "textSha256" or "TextSha256"))
                module.Edits.Add(new(new(step.Body.Span.Start + 1, 0), "\nsha256: " + JsonSerializer.Serialize(locked.Sha256) + ";\n"));
        }
    }
}

public sealed record FirmamentDependencyInspection(FirmamentDependencyLock? Manifest,
    IReadOnlyList<string> SurgicalAssets, IReadOnlyList<AssemblyDiagnostic> Diagnostics)
{
    public bool IsSuccess => Manifest is not null && Diagnostics.Count == 0;
}

public static class FirmamentDependencies
{
    /// <summary>Checks the same dependency graph used by compilation. Never writes or refreshes a lock.</summary>
    public static IReadOnlyList<AssemblyDiagnostic> VerifyFile(string path, bool requireLock = false)
    {
        var full = Path.GetFullPath(path);
        var lockPath = Path.Combine(Path.GetDirectoryName(full)!, FirmamentDependencyLock.FileName);
        if (!File.Exists(lockPath)) return requireLock
            ? [new("firmament-lock-required", "Locked compilation requires " + lockPath) { SourceSpan = new(full, 0, 0) }] : [];
        return FirmamentModuleGraph.Load(File.ReadAllText(full), full).Diagnostics;
    }

    public static FirmamentDependencyInspection InspectFile(string path, bool update = false)
        => InspectSource(File.ReadAllText(path), path, update);

    public static FirmamentDependencyInspection InspectSource(string source, string path, bool update = false)
    {
        var full = Path.GetFullPath(path);
        var graph = FirmamentModuleGraph.Load(source, full, validateLock: false);
        if (graph.Diagnostics.Count > 0) return new(null, graph.SurgicalAssets.ToArray(), graph.Diagnostics);
        var entries = graph.Dependencies.Where(d => !d.IsRoot).Select(d => new FirmamentLockedDependency(graph.Relative(d.Path),
            FirmamentProjectSnapshot.IsStepAsset(d.Path) ? "step" : "firmament",
            FirmamentProjectSnapshot.IsStepAsset(d.Path) ? "sha256-bytes" : "sha256-utf8", d.Sha256.ToLowerInvariant())).ToArray();
        if (graph.Locked is { } previous)
        {
            if (previous.Root != graph.Relative(full)) return new(null, graph.SurgicalAssets.ToArray(), [new("firmament-lock-root-mismatch", "The existing lock belongs to another root document.")]);
            var changed = entries.Where(d => previous.Dependencies.SingleOrDefault(old => old.Path == d.Path)?.Sha256 != d.Sha256).ToArray();
            if (!update && changed.Length > 0) return new(null, graph.SurgicalAssets.ToArray(), [new("firmament-lock-update-required", "Use an explicit lock update for changed dependencies.")]);
            if (changed.Any(d => graph.SurgicalAssets.Contains(d.Path)))
                return new(null, graph.SurgicalAssets.ToArray(), [new("firmament-lock-rebind-required", "A changed STEP source has bound surgery. Reinspect and regenerate its selections before replacing the lock; a hash update cannot rebind authoring intent.")]);
        }
        return new(new(graph.Relative(full), entries), graph.SurgicalAssets.ToArray(), []);
    }
}
