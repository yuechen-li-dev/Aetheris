using Aetheris.Forge.Abstractions.FirmamentInterop;

namespace Aetheris.Forge.Standard;

public sealed class StandardForgeRuntimeConceptPack : IForgeConceptPack
{
    public string Id => "Aetheris.Standard";

    public Version Version => new(2, 0, 0);

    public void Register(IForgeRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new CncProcessConcept());
        registry.Register(new ShaftHoleConcept());
        registry.Register(new CounterboreHoleConcept());
        registry.Register(new CountersinkHoleConcept());
    }
}
