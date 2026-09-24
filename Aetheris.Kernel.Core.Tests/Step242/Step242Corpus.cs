using System.Collections.Concurrent;
using System.Text;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

/// <summary>
/// Shared, memoized access to the on-disk STEP corpus.
/// <para>
/// The corpus holds 48 distinct files but the test project imports from it at roughly two
/// hundred call sites, and every one of those used to read the file and rebuild the body from
/// scratch. Importing every corpus file exactly once costs about ten seconds; the suite used to
/// spend about ninety-eight on it. Parsing is not the expensive part - under a second of that
/// ten - so caching the parsed document would buy almost nothing. The body is what is expensive
/// to build, so the body is what is cached.
/// </para>
/// <para>
/// Sharing one <see cref="BrepBody"/> across tests is safe because a body and everything it
/// exposes is read-only once constructed. Two kinds of test must still import for themselves and
/// should take only <see cref="Text"/> from here: those that install a
/// <c>Step242Importer.CaptureLoopRole*Diagnostics</c> scope, which collects during the import and
/// therefore needs a real one, and those that run under
/// <c>Step242Importer.PreserveRationalSurfaces()</c>, which changes what the import produces.
/// </para>
/// </summary>
internal static class Step242Corpus
{
    private static readonly ConcurrentDictionary<string, Lazy<string>> TextCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, Lazy<KernelResult<BrepBody>>> BodyCache = new(StringComparer.Ordinal);

    /// <summary>Absolute path of a corpus file named relative to the repository root.</summary>
    public static string Path(string relativePath) =>
        System.IO.Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

    /// <summary>The file's text, read at most once per path per test run.</summary>
    public static string Text(string relativePath) =>
        TextCache.GetOrAdd(relativePath, key => new Lazy<string>(
            () => File.ReadAllText(Path(key), Encoding.UTF8),
            LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    /// <summary>
    /// The imported body, built at most once per path per test run. The result is shared, so
    /// callers must treat it as read-only; anything that needs a private import should call
    /// <see cref="Step242Importer.ImportBody"/> on <see cref="Text"/> itself.
    /// </summary>
    public static KernelResult<BrepBody> Import(string relativePath) =>
        BodyCache.GetOrAdd(relativePath, key => new Lazy<KernelResult<BrepBody>>(
            () => Step242Importer.ImportBody(Text(key)),
            LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    /// <summary>The imported body, asserting that the import succeeded.</summary>
    public static BrepBody Body(string relativePath)
    {
        var import = Import(relativePath);
        Assert.True(import.IsSuccess, $"{relativePath}: {string.Join(" | ", import.Diagnostics.Select(d => d.Message))}");
        return import.Value;
    }
}
