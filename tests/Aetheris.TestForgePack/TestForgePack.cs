using Aetheris.Forge.Abstractions.FirmamentInterop;

namespace Aetheris.TestForgePack;

public sealed class TestForgePack : IForgeConceptPack
{
    public string Id => "Aetheris.TestForgePack";

    public Version Version => new(1, 0, 0);

    public void Register(IForgeRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new TestBossHoleConcept());
    }
}

public sealed class TestBossHoleConcept : IForgeConcept
{
    public ConceptId Id => new("hole", "BossTest");

    public void Define(ConceptSchemaBuilder schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        schema.RequiredTarget("target");
        schema.RequiredLength("diameter");
    }

    public IEnumerable<FirmamentDiagnostic> Validate(ConceptValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        yield return new FirmamentDiagnostic(
            "testforge.boss-hole.seen",
            FirmamentDiagnosticSeverity.Warning,
            "External test forge pack validated hole<BossTest>.");
    }
}
