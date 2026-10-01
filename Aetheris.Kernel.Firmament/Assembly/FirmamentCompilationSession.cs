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

internal sealed class AssemblyDefinitionCache(int capacity)
{
    // STEP text is the immutable cache payload. A hit reimports a fresh body so
    // public mutable topology/geometry stores can never corrupt a later build.
    private sealed record Entry(string Key, string Step, MaterializedAssemblyDefinition Metadata,
        IReadOnlyList<AssemblyDiagnostic> Diagnostics);
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly Queue<string> insertionOrder = new();
    internal List<AssemblyDefinitionReuse> Evidence { get; } = [];
    internal void BeginBuild() => Evidence.Clear();
    internal void Clear() { entries.Clear(); insertionOrder.Clear(); Evidence.Clear(); }

    internal MaterializedAssemblyDefinition? Materialize(string identity, string? declarations, string sourceIdentity,
        List<AssemblyDiagnostic> diagnostics, FirmamentProjectSnapshot? project,
        Func<MaterializedAssemblyDefinition?> build)
    {
        var key = Key(identity, declarations, sourceIdentity, project);
        var reason = key is null ? "unsupported-resource-dependency" : "first-build-or-evicted";
        if (key is not null && entries.TryGetValue(identity, out var entry))
        {
            reason = "definition-inputs-changed";
            if (entry.Key == key)
            {
                var import = Step242Importer.ImportBody(entry.Step);
                if (import.IsSuccess && import.Value is not null)
                {
                    diagnostics.AddRange(entry.Diagnostics);
                    Evidence.Add(new(identity, true, "unchanged-definition-inputs"));
                    return CopyMetadata(entry.Metadata) with { Body = import.Value };
                }
                reason = "cached-step-reimport-failed";
            }
        }
        var diagnosticStart = diagnostics.Count;
        var result = build();
        Evidence.Add(new(identity, false, result is null ? "materialization-failed" : reason));
        if (key is null || result is null || diagnostics.Skip(diagnosticStart).Any(d => d.Severity == AssemblyDiagnosticSeverity.Error))
            return result;
        if (Key(identity, declarations, sourceIdentity, project) != key)
        {
            Evidence[^1] = new(identity, false, "resource-changed-during-build");
            return result;
        }
        // This bounded cache only admits immutable scalar/datum semantic bindings.
        // Domain materializers and geometry-valued ports stay on the uncached path.
        if (!result.Semantics.All(CanCopy) || result.CanonicalStep is not { } step)
        {
            Evidence[^1] = new(identity, false, "producer-or-semantics-not-cacheable");
            return result;
        }
        var metadata = CopyMetadata(result);
        // The private body is never exposed; fresh imports supply returned bodies.
        var privateImport = Step242Importer.ImportBody(step);
        if (!privateImport.IsSuccess || privateImport.Value is null) return result;
        metadata = metadata with { Body = privateImport.Value };
        if (!entries.ContainsKey(identity))
        {
            while (entries.Count >= capacity) entries.Remove(insertionOrder.Dequeue());
            insertionOrder.Enqueue(identity);
        }
        entries[identity] = new(key, step, metadata, diagnostics.Skip(diagnosticStart).ToArray());
        return result;
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

    private static bool CanCopy(SemanticValue value) => value.Bindings.All(b => b is
        ExactAxisBinding or ExactPlaneBinding or ExactPointBinding or ExactDatumFrameBinding or TolerancedDimensionBinding)
        && value.ExposedMembers.Values.All(CanCopy);

    private static SemanticValue Copy(SemanticValue value) => new(value.StableIdentity, value.Type,
        value.Capabilities.Values, value.Bindings, value.ExposedMembers.Values.Select(Copy), value.Provenance.ToArray(),
        value.AuthoredSourceSpan, value.GeneratedSourceSpan, value.ExposedName);

    private static MaterializedAssemblyDefinition CopyMetadata(MaterializedAssemblyDefinition definition) => definition with
    {
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
