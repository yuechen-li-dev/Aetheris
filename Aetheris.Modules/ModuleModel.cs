using System.Collections.ObjectModel;

namespace Aetheris.Modules;

public readonly record struct AetherisModuleId
{
    public AetherisModuleId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("Aetheris.", StringComparison.Ordinal)
            || value.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '.')))
            throw new ArgumentException("Module IDs must be explicit dotted Aetheris identities.", nameof(value));
        Value = value;
    }
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct ModuleVersion : IComparable<ModuleVersion>
{
    public ModuleVersion(int major, int minor, int patch)
    {
        if (major < 0 || minor < 0 || patch < 0) throw new ArgumentOutOfRangeException(nameof(major));
        Major = major; Minor = minor; Patch = patch;
    }
    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public int CompareTo(ModuleVersion other) => (Major, Minor, Patch).CompareTo((other.Major, other.Minor, other.Patch));
    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

public sealed record ModuleDependency(AetherisModuleId ModuleId, ModuleVersion MinimumVersion);

/// <summary>Compiler/domain functionality. This is intentionally unrelated to ISemanticCapability.</summary>
public sealed record ModuleCapability(
    string Id,
    AetherisModuleId OwningModule,
    ModuleVersion SinceVersion,
    string Description);

public sealed record ModuleDocumentation(string ArchitecturePath, string? PublicPath = null);

public sealed record AetherisModule(
    AetherisModuleId Id,
    string Name,
    ModuleVersion Version,
    IReadOnlyList<ModuleCapability> Capabilities,
    IReadOnlyList<string> Concepts,
    IReadOnlyList<string> Templates,
    IReadOnlyList<string> Lowerings,
    IReadOnlyList<string> DiagnosticCodes,
    IReadOnlyList<ModuleDependency> Dependencies,
    ModuleDocumentation Documentation,
    bool BuiltIn = true);

public enum ModuleDiagnosticKind { DuplicateModuleId, DuplicateCapability, CapabilityOwnerMismatch, MissingDependency, DependencyVersion, DependencyCycle, CapabilityUnavailable }

public sealed record ModuleDiagnostic(
    ModuleDiagnosticKind Kind,
    string Code,
    string Message,
    string? CapabilityId = null,
    AetherisModuleId? OwningModule = null,
    ModuleVersion? RequiredVersion = null);

public sealed class ModuleCatalogException : InvalidOperationException
{
    public ModuleCatalogException(IReadOnlyList<ModuleDiagnostic> diagnostics)
        : base(string.Join(Environment.NewLine, diagnostics.Select(d => d.Message))) => Diagnostics = diagnostics;
    public IReadOnlyList<ModuleDiagnostic> Diagnostics { get; }
}

/// <summary>Deterministic explicit catalog. Registration is code-owned; no assembly scanning or service location.</summary>
public sealed class AetherisModuleCatalog
{
    private readonly IReadOnlyDictionary<AetherisModuleId, AetherisModule> modules;
    private readonly IReadOnlyDictionary<string, ModuleCapability> capabilities;

    private AetherisModuleCatalog(IReadOnlyList<AetherisModule> ordered)
    {
        Modules = ordered;
        modules = new ReadOnlyDictionary<AetherisModuleId, AetherisModule>(ordered.ToDictionary(m => m.Id));
        capabilities = new ReadOnlyDictionary<string, ModuleCapability>(ordered.SelectMany(m => m.Capabilities).ToDictionary(c => c.Id, StringComparer.Ordinal));
    }

    public IReadOnlyList<AetherisModule> Modules { get; }
    public IReadOnlyList<ModuleCapability> Capabilities => capabilities.Values.OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
    public bool TryGetModule(AetherisModuleId id, out AetherisModule module) => modules.TryGetValue(id, out module!);
    public bool TryGetCapability(string id, out ModuleCapability capability) => capabilities.TryGetValue(id, out capability!);

    public ModuleDiagnostic? RequireCapability(string id, AetherisModuleId owner, ModuleVersion minimum)
    {
        if (capabilities.TryGetValue(id, out var available) && available.OwningModule == owner
            && modules[owner].Version.CompareTo(minimum) >= 0) return null;
        return new(ModuleDiagnosticKind.CapabilityUnavailable, "module-capability-unavailable",
            $"Capability '{id}' requires module '{owner}' version {minimum} or later.", id, owner, minimum);
    }

    public static AetherisModuleCatalog Create(IEnumerable<AetherisModule> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        var input = registrations.ToArray();
        var diagnostics = new List<ModuleDiagnostic>();
        foreach (var duplicate in input.GroupBy(m => m.Id).Where(g => g.Count() > 1).OrderBy(g => g.Key.Value, StringComparer.Ordinal))
            diagnostics.Add(new(ModuleDiagnosticKind.DuplicateModuleId, "module-id-duplicate", $"Module ID '{duplicate.Key}' is registered more than once."));
        foreach (var duplicate in input.SelectMany(m => m.Capabilities).GroupBy(c => c.Id, StringComparer.Ordinal).Where(g => g.Count() > 1).OrderBy(g => g.Key, StringComparer.Ordinal))
            diagnostics.Add(new(ModuleDiagnosticKind.DuplicateCapability, "module-capability-duplicate", $"Capability '{duplicate.Key}' has more than one owner.", duplicate.Key));
        foreach (var module in input.OrderBy(m => m.Id.Value, StringComparer.Ordinal))
        foreach (var capability in module.Capabilities.Where(c => c.OwningModule != module.Id).OrderBy(c => c.Id, StringComparer.Ordinal))
            diagnostics.Add(new(ModuleDiagnosticKind.CapabilityOwnerMismatch, "module-capability-owner-mismatch", $"Capability '{capability.Id}' declares owner '{capability.OwningModule}' but is registered by '{module.Id}'.", capability.Id, capability.OwningModule));
        if (diagnostics.Count > 0) throw new ModuleCatalogException(diagnostics);

        var byId = input.ToDictionary(m => m.Id);
        foreach (var module in input.OrderBy(m => m.Id.Value, StringComparer.Ordinal))
        foreach (var dependency in module.Dependencies.OrderBy(d => d.ModuleId.Value, StringComparer.Ordinal))
        {
            if (!byId.TryGetValue(dependency.ModuleId, out var target))
                diagnostics.Add(new(ModuleDiagnosticKind.MissingDependency, "module-dependency-missing", $"Module '{module.Id}' requires missing module '{dependency.ModuleId}'."));
            else if (target.Version.CompareTo(dependency.MinimumVersion) < 0)
                diagnostics.Add(new(ModuleDiagnosticKind.DependencyVersion, "module-dependency-version", $"Module '{module.Id}' requires '{target.Id}' {dependency.MinimumVersion} or later; found {target.Version}."));
        }
        if (diagnostics.Count > 0) throw new ModuleCatalogException(diagnostics);

        var state = new Dictionary<AetherisModuleId, int>();
        var stack = new List<AetherisModuleId>();
        var ordered = new List<AetherisModule>();
        foreach (var module in input.OrderBy(m => m.Id.Value, StringComparer.Ordinal)) Visit(module);
        if (diagnostics.Count > 0) throw new ModuleCatalogException(diagnostics);
        return new AetherisModuleCatalog(ordered);

        void Visit(AetherisModule module)
        {
            if (state.TryGetValue(module.Id, out var current))
            {
                if (current == 2) return;
                if (current == 1)
                {
                    var start = stack.IndexOf(module.Id);
                    var cycle = stack.Skip(start).Append(module.Id).Select(id => id.Value);
                    diagnostics.Add(new(ModuleDiagnosticKind.DependencyCycle, "module-dependency-cycle", $"Module dependency cycle: {string.Join(" -> ", cycle)}."));
                }
                return;
            }
            state[module.Id] = 1; stack.Add(module.Id);
            foreach (var dependency in module.Dependencies.OrderBy(d => d.ModuleId.Value, StringComparer.Ordinal)) Visit(byId[dependency.ModuleId]);
            stack.RemoveAt(stack.Count - 1); state[module.Id] = 2; ordered.Add(module);
        }
    }
}

public static class CoreModule
{
    public static readonly AetherisModuleId Id = new("Aetheris.Core");
    public static AetherisModule Definition { get; } = new(Id, "Core", new(1, 0, 0), [], [], [],
        ["Shared exact geometry, BRep, AIR/ConstructionIR, compiler substrate"], [], [], new("docs/modules/architecture.md"));
}
