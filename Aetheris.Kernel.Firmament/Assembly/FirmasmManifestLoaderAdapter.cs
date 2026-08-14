using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Firmament.Assembly;

/// <summary>
/// Compatibility facade retained for existing integrations. New code should use
/// <see cref="LegacyFirmasmJsonReader"/> to make the historical JSON format explicit.
/// </summary>
public sealed class FirmasmManifestLoader
{
    private readonly LegacyFirmasmJsonReader _reader = new();

    public KernelResult<FirmasmLoadedAssembly> LoadFromFile(string manifestPath) => _reader.LoadFromFile(manifestPath);

    public KernelResult<FirmasmManifest> Parse(string sourceText) => _reader.Parse(sourceText);
}
