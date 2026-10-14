using Aetheris.Kernel.Core.Results;
using System.Text.Json;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Queries;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Assembly;

public sealed record AssemblyDefinitionReuse(string DefinitionIdentity, bool Reused, string Reason);
public sealed record AssemblyCompilationReuse(IReadOnlyList<AssemblyDefinitionReuse> Definitions)
{
    public int ReusedDefinitions => Definitions.Count(d => d.Reused);
    public int RebuiltDefinitions => Definitions.Count(d => !d.Reused);
}

/// <summary>Explicit, bounded, in-memory reuse of successful exact part materializations.
/// Parsing, assembly solving and physical validation still run for each build.</summary>
public sealed class FirmamentCompilationSession : IDisposable
{
    private readonly object gate = new();
    private readonly AssemblyDefinitionCache cache;
    private readonly AssemblyM1Pipeline pipeline;
    private bool disposed;

    public FirmamentCompilationSession(int maximumDefinitions = 256)
    {
        if (maximumDefinitions < 1) throw new ArgumentOutOfRangeException(nameof(maximumDefinitions));
        cache = new(maximumDefinitions);
        pipeline = new(cache);
    }

    public AssemblyM1CompilationResult CompileFile(string path) => Run(() => pipeline.CompileFile(path));
    public AssemblyM1CompilationResult Compile(string source, string sourceIdentity = "<memory>") => Run(() => pipeline.Compile(source, sourceIdentity));
    public AssemblyM1CompilationResult CompileProject(FirmamentProjectSnapshot project) => Run(() => pipeline.CompileProject(project));

    /// <summary>The display mesh of a compilation from this session. Definitions whose input key is
    /// unchanged since the previous build reuse their prepared mesh instead of tessellating again.</summary>
    public AssemblyDisplayMeshDocument ExportDisplayMesh(AssemblyM1CompilationResult compilation)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return AssemblyDisplayMeshExporter.ExportCore(compilation, null, null, (prepare, identity) =>
                compilation.DefinitionInputKeys?.GetValueOrDefault(identity) is { } inputKey
                    ? cache.DisplayDefinitions.GetOrAdd(inputKey + "|" + identity, prepare)
                    : prepare());
        }
    }

    public void Clear()
    {
        lock (gate) { ObjectDisposedException.ThrowIf(disposed, this); cache.Clear(); }
    }

    public void Dispose()
    {
        lock (gate) { cache.Clear(); disposed = true; }
    }

    private AssemblyM1CompilationResult Run(Func<AssemblyM1CompilationResult> compile)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cache.BeginBuild();
            return compile() with { Reuse = new(cache.Evidence.ToArray()) };
        }
    }
}

/// <summary>Values derived from cached definitions, kept for one build: entries a build does not
/// use are dropped when the next build begins, so the working set stays bounded.</summary>
internal sealed class DerivedValueCache<T>
{
    private Dictionary<string, T> current = new(StringComparer.Ordinal);
    private Dictionary<string, T> previous = new(StringComparer.Ordinal);
    internal void BeginBuild() { previous = current; current = new(StringComparer.Ordinal); }
    internal void Clear() { current.Clear(); previous.Clear(); }
    internal void Forget(string key) { current.Remove(key); previous.Remove(key); }
    internal T GetOrAdd(string key, Func<T> create)
    {
        if (current.TryGetValue(key, out var value)) return value;
        if (!previous.Remove(key, out value!)) value = create();
        current[key] = value;
        return value;
    }
}

/// <summary>A published-port binding and the diagnostics it reported, replayed on reuse.</summary>
internal sealed record PublishedPortBinding(IReadOnlyList<SemanticValue> Ports, IReadOnlyList<AssemblyDiagnostic> Diagnostics);

internal sealed class AssemblyDefinitionCache(int capacity)
{
    // The materialized body is the cache payload. The cache keeps a private clone and every hit
    // returns a fresh clone, so the public mutable topology/geometry stores can never corrupt a
    // later build, and a rebuild sees the same body as the build that populated the cache.
    // (BUILD-PERF-M1: previously STEP text, re-imported on every hit.)
    // BUILD-PERF-M2: an entry also carries the definition's content key (identity, canonical STEP,
    // retained CIR). Everything derived downstream (display mesh, seat queries) is a function of
    // exactly those, so a definition rebuilt from changed source text but producing the same
    // geometry reuses them; an input-key change alone no longer re-tessellates.
    private sealed record Entry(string Key, MaterializedAssemblyDefinition Metadata,
        IReadOnlyList<AssemblyDiagnostic> Diagnostics, string ContentKey);
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly Queue<string> insertionOrder = new();
    internal List<AssemblyDefinitionReuse> Evidence { get; } = [];
    internal DerivedValueCache<BrepPlanarSeatQuery> SeatQueries { get; } = new();
    internal DerivedValueCache<AssemblyDisplayMeshDefinition> DisplayDefinitions { get; } = new();
    internal DerivedValueCache<PublishedPortBinding> PublishedPorts { get; } = new();
    internal void BeginBuild() { Evidence.Clear(); SeatQueries.BeginBuild(); DisplayDefinitions.BeginBuild(); PublishedPorts.BeginBuild(); }
    internal void Clear() { entries.Clear(); insertionOrder.Clear(); Evidence.Clear(); SeatQueries.Clear(); DisplayDefinitions.Clear(); PublishedPorts.Clear(); }

    internal MaterializedAssemblyDefinition? Materialize(string identity, string? declarations, string sourceIdentity,
        List<AssemblyDiagnostic> diagnostics, FirmamentProjectSnapshot? project,
        Func<MaterializedAssemblyDefinition?> build)
    {
        string? key;
        using (BuildPerfTrace.Phase("assembly.cache-key")) key = Key(identity, declarations, sourceIdentity, project);
        var reason = key is null ? "unsupported-resource-dependency" : "first-build-or-evicted";
        if (key is not null && entries.TryGetValue(identity, out var entry))
        {
            reason = "definition-inputs-changed";
            if (entry.Key == key)
            {
                BrepBody body;
                using (BuildPerfTrace.Phase("assembly.cache-hit-clone")) body = entry.Metadata.Body.Clone();
                diagnostics.AddRange(entry.Diagnostics);
                Evidence.Add(new(identity, true, "unchanged-definition-inputs"));
                return CopyMetadata(entry.Metadata) with { Body = body, InputKey = entry.ContentKey };
            }
        }
        var diagnosticStart = diagnostics.Count;
        var result = build();
        Evidence.Add(new(identity, false, result is null ? "materialization-failed" : reason));
        if (key is null || result is null || diagnostics.Skip(diagnosticStart).Any(d => d.Severity == AssemblyDiagnosticSeverity.Error))
            return result;
        string? keyAfterBuild;
        using (BuildPerfTrace.Phase("assembly.cache-key")) keyAfterBuild = Key(identity, declarations, sourceIdentity, project);
        if (keyAfterBuild != key)
        {
            Evidence[^1] = new(identity, false, "resource-changed-during-build");
            return result;
        }
        // This bounded cache only admits immutable scalar/datum semantic bindings.
        // Domain materializers and geometry-valued ports stay on the uncached path.
        if (!result.Semantics.All(CanCopy) || result.CanonicalStep is null)
        {
            Evidence[^1] = new(identity, false, "producer-or-semantics-not-cacheable");
            return result;
        }
        // The private body is never exposed; hits return clones of it.
        MaterializedAssemblyDefinition metadata;
        using (BuildPerfTrace.Phase("assembly.cache-store-clone")) metadata = CopyMetadata(result) with { Body = result.Body.Clone() };
        if (!entries.ContainsKey(identity))
        {
            while (entries.Count >= capacity) entries.Remove(insertionOrder.Dequeue());
            insertionOrder.Enqueue(identity);
        }
        var contentKey = ContentKey(result);
        entries[identity] = new(key, metadata, diagnostics.Skip(diagnosticStart).ToArray(), contentKey);
        return result with { InputKey = contentKey };
    }

    private static string ContentKey(MaterializedAssemblyDefinition definition) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("assembly-definition-content-v1\0"
            + definition.DefinitionIdentity + "\0" + definition.CanonicalStep + "\0"
            + (definition.Cir is null ? "" : JsonSerializer.Serialize(definition.Cir)))));

    /// <summary>The body of canonical STEP text: a clone of a cached definition imported from the
    /// identical text, otherwise a fresh import. The importer is deterministic, so both are equal.</summary>
    internal KernelResult<BrepBody> ImportCanonical(string stepText)
    {
        foreach (var entry in entries.Values)
            if (entry.Metadata.CanonicalStep is { } step && step.Length == stepText.Length && string.Equals(step, stepText, StringComparison.Ordinal))
            {
                using (BuildPerfTrace.Phase("assembly.cache-import-clone"))
                    return KernelResult<BrepBody>.Success(entry.Metadata.Body.Clone());
            }
        return Step242Importer.ImportBody(stepText);
    }

    private static string? Key(string identity, string? declarations, string sourceIdentity, FirmamentProjectSnapshot? project)
    {
        var file = Regex.Match(identity, "^(?:SectionChainFile|LoftFile|ExternalStep)<\\\"(?<path>[^\\\"]+)\\\">$", RegexOptions.CultureInvariant);
        string inputs;
        if (file.Success)
        {
            var requested = file.Groups["path"].Value;
            if (project is not null)
            {
                if (identity.StartsWith("ExternalStep", StringComparison.Ordinal)) return null;
                try { if (!project.TryResolve(requested, out inputs!)) return null; }
                catch (ArgumentException) { return null; }
            }
            else
            {
                var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sourceIdentity))!, requested));
                if (!File.Exists(path)) return null;
                inputs = File.ReadAllText(path);
            }
            if (!identity.StartsWith("ExternalStep", StringComparison.Ordinal))
            {
                var resourcePath = project is not null ? requested
                    : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sourceIdentity))!, requested));
                var errors = new List<AssemblyDiagnostic>();
                var loaded = AssemblyM0Parser.LoadResource(resourcePath, project, [], errors);
                if (loaded is null || errors.Count != 0) return null;
                inputs = loaded;
            }
        }
        else
        {
            if (declarations is null || Regex.IsMatch(declarations, @"\bInlineStep\b", RegexOptions.CultureInvariant)) return null;
            // Keep exact text, including offsets, because cached semantic evidence
            // carries source spans. The parser already excludes assembly bodies.
            inputs = declarations;
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            "assembly-definition-cache-v1\0" + sourceIdentity + "\0" + identity + "\0" + inputs)));
    }

    internal static bool CanCopy(SemanticValue value) => value.Bindings.All(b => b is
        ExactAxisBinding or ExactPlaneBinding or ExactPointBinding or ExactDatumFrameBinding or TolerancedDimensionBinding)
        && value.ExposedMembers.Values.All(CanCopy);

    internal static SemanticValue Copy(SemanticValue value) => new(value.StableIdentity, value.Type,
        value.Capabilities.Values, value.Bindings, value.ExposedMembers.Values.Select(Copy), value.Provenance.ToArray(),
        value.AuthoredSourceSpan, value.GeneratedSourceSpan, value.ExposedName);

    private static MaterializedAssemblyDefinition CopyMetadata(MaterializedAssemblyDefinition definition) => definition with
    {
        Cir = definition.Cir is null ? null : definition.Cir with
        { MinimumMm = definition.Cir.MinimumMm?.ToArray(), MaximumMm = definition.Cir.MaximumMm?.ToArray() },
        Semantics = definition.Semantics.Select(Copy).ToArray(),
        Artifact = definition.Artifact with
        {
            Provenance = definition.Artifact.Provenance.ToArray(),
            Metrics = definition.Artifact.Metrics with
            {
                Minimum = definition.Artifact.Metrics.Minimum.ToArray(), Maximum = definition.Artifact.Metrics.Maximum.ToArray()
            }
        }
    };
}
