using Aetheris.Forge.Abstractions.FirmamentInterop;

namespace Aetheris.TestForgePack.Duplicate;

public sealed class DuplicateCountersinkForgePack : IForgeConceptPack
{
    public string Id => "Aetheris.TestForgePack.Duplicate";

    public Version Version => new(1, 0, 0);

    public void Register(IForgeRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new DuplicateCountersinkConcept());
    }
}

public sealed class DuplicateCountersinkConcept : IForgeConcept
{
    public ConceptId Id => new("hole", "Countersink");

    public void Define(ConceptSchemaBuilder schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        schema.RequiredTarget("target");
        schema.RequiredLength("diameter");
        schema.RequiredLength("countersinkDiameter");
        schema.RequiredAngle("angle");
    }

    public IEnumerable<FirmamentDiagnostic> Validate(ConceptValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        yield break;
    }
}
