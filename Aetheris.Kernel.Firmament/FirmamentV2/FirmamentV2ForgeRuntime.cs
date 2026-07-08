using Aetheris.Forge.Abstractions.FirmamentInterop;
using Aetheris.Forge.Standard;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentV2ExternalForgePackReference(
    string Id,
    string Version,
    string Assembly);

public sealed record FirmamentV2ForgeRuntimeMetadata(
    string BuiltInPack,
    IReadOnlyList<FirmamentV2ExternalForgePackReference> ExternalPacks);

public sealed class FirmamentV2ForgeRuntimeConfiguration
{
    private FirmamentV2ForgeRuntimeConfiguration(
        FirmamentV2ForgeConceptCatalog catalog,
        IForgeRegistry registry,
        IReadOnlyDictionary<ConceptId, string> conceptProviders,
        FirmamentV2ForgeRuntimeMetadata metadata)
    {
        Catalog = catalog;
        Registry = registry;
        ConceptProviders = conceptProviders;
        Metadata = metadata;
    }

    public FirmamentV2ForgeConceptCatalog Catalog { get; }

    public IForgeRegistry Registry { get; }

    public IReadOnlyDictionary<ConceptId, string> ConceptProviders { get; }

    public FirmamentV2ForgeRuntimeMetadata Metadata { get; }

    public static FirmamentV2ForgeRuntimeConfiguration CreateDefault() => Create([]);

    public static FirmamentV2ForgeRuntimeConfiguration Create(
        IReadOnlyList<(IForgeConceptPack Pack, string AssemblyPath)> externalPacks)
    {
        ArgumentNullException.ThrowIfNull(externalPacks);

        var trackingRegistry = new TrackingForgeRegistry();
        var builtInPack = new StandardForgeRuntimeConceptPack();
        trackingRegistry.RegisterPack(builtInPack, null);

        var externalPackMetadata = new List<FirmamentV2ExternalForgePackReference>(externalPacks.Count);
        foreach (var (pack, assemblyPath) in externalPacks)
        {
            trackingRegistry.RegisterPack(pack, assemblyPath);
            externalPackMetadata.Add(new FirmamentV2ExternalForgePackReference(
                pack.Id,
                pack.Version.ToString(),
                Path.GetFileName(assemblyPath)));
        }

        return new FirmamentV2ForgeRuntimeConfiguration(
            FirmamentV2ForgeConceptCatalog.FromConcepts(trackingRegistry.EnumerateConcepts()),
            trackingRegistry,
            new Dictionary<ConceptId, string>(trackingRegistry.ConceptProviders),
            new FirmamentV2ForgeRuntimeMetadata(builtInPack.Id, externalPackMetadata));
    }

    private sealed class TrackingForgeRegistry : IForgeRegistry
    {
        private readonly ForgeConceptRegistry inner = new();
        private readonly Dictionary<ConceptId, string> conceptProviders = new();
        private string? currentPackId;
        private string? currentAssemblyPath;

        public IReadOnlyDictionary<ConceptId, string> ConceptProviders => conceptProviders;

        public void RegisterPack(IForgeConceptPack pack, string? assemblyPath)
        {
            ArgumentNullException.ThrowIfNull(pack);

            currentPackId = pack.Id;
            currentAssemblyPath = assemblyPath;
            try
            {
                pack.Register(this);
            }
            catch (Exception ex) when (ex is not InvalidOperationException || assemblyPath is not null)
            {
                var prefix = assemblyPath is null
                    ? $"Built-in forge concept pack '{pack.Id}' failed to register."
                    : $"External forge concept pack '{pack.Id}' from '{assemblyPath}' failed to register.";
                throw new InvalidOperationException($"{prefix} {ex.Message}", ex);
            }
            finally
            {
                currentPackId = null;
                currentAssemblyPath = null;
            }
        }

        public void Register(IForgeConcept concept)
        {
            ArgumentNullException.ThrowIfNull(concept);

            if (conceptProviders.ContainsKey(concept.Id))
            {
                var packLabel = currentPackId ?? "<unknown-pack>";
                var sourceLabel = currentAssemblyPath is null ? packLabel : $"{packLabel} ({currentAssemblyPath})";
                throw new InvalidOperationException($"Forge concept pack '{sourceLabel}' attempted to register duplicate concept '{concept.Id}'.");
            }

            inner.Register(concept);
            conceptProviders.Add(concept.Id, currentPackId ?? string.Empty);
        }

        public bool TryResolve(ConceptId id, out IForgeConcept concept) => inner.TryResolve(id, out concept!);

        public IReadOnlyList<IForgeConcept> EnumerateConcepts() => inner.EnumerateConcepts();
    }
}
