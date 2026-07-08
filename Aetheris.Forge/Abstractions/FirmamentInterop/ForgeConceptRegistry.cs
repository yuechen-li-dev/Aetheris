namespace Aetheris.Forge.Abstractions.FirmamentInterop;

public interface IForgeConcept
{
    ConceptId Id { get; }
    void Define(ConceptSchemaBuilder schema);
    IEnumerable<FirmamentDiagnostic> Validate(ConceptValidationContext context);
}

public interface IForgeConceptPack
{
    string Id { get; }
    Version Version { get; }
    void Register(IForgeRegistry registry);
}

public interface IForgeRegistry
{
    void Register(IForgeConcept concept);
    bool TryResolve(ConceptId id, out IForgeConcept concept);
}

public sealed class ForgeConceptRegistry : IForgeRegistry
{
    private readonly Dictionary<ConceptId, IForgeConcept> concepts = new();

    public void Register(IForgeConcept concept)
    {
        ArgumentNullException.ThrowIfNull(concept);
        if (concepts.ContainsKey(concept.Id))
        {
            throw new InvalidOperationException($"Forge concept '{concept.Id}' is already registered.");
        }

        concepts.Add(concept.Id, concept);
    }

    public bool TryResolve(ConceptId id, out IForgeConcept concept) => concepts.TryGetValue(id, out concept!);
}

public sealed class ConceptSchemaBuilder
{
    private readonly Dictionary<string, ConceptSchemaField> fields = new(StringComparer.Ordinal);

    public IReadOnlyList<ConceptSchemaField> Fields => fields.Values.OrderBy(schemaField => schemaField.Name, StringComparer.Ordinal).ToArray();

    public ConceptSchemaFieldBuilder RequiredTarget(string name) => Add(name, ConceptSchemaValueKind.Target);
    public ConceptSchemaFieldBuilder RequiredLength(string name) => Add(name, ConceptSchemaValueKind.Length);
    public ConceptSchemaFieldBuilder RequiredAngle(string name) => Add(name, ConceptSchemaValueKind.Angle);
    public ConceptSchemaFieldBuilder RequiredString(string name) => Add(name, ConceptSchemaValueKind.String);
    public ConceptSchemaFieldBuilder RequiredBool(string name) => Add(name, ConceptSchemaValueKind.Bool);
    public ConceptSchemaFieldBuilder RequiredFloat(string name) => Add(name, ConceptSchemaValueKind.Float);
    public ConceptSchemaFieldBuilder RequiredInt(string name) => Add(name, ConceptSchemaValueKind.Int);

    private ConceptSchemaFieldBuilder Add(string name, ConceptSchemaValueKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (fields.ContainsKey(name))
        {
            throw new InvalidOperationException($"Concept schema field '{name}' is already defined.");
        }

        fields.Add(name, new ConceptSchemaField(name, kind, true, false));
        return new ConceptSchemaFieldBuilder(this, name);
    }

    internal void RequireTolerance(string name)
    {
        var field = fields[name];
        fields[name] = field with { RequiresTolerance = true };
    }
}

public sealed class ConceptSchemaFieldBuilder
{
    private readonly ConceptSchemaBuilder builder;
    private readonly string name;

    internal ConceptSchemaFieldBuilder(ConceptSchemaBuilder builder, string name)
    {
        this.builder = builder;
        this.name = name;
    }

    public ConceptSchemaFieldBuilder RequireTolerance()
    {
        builder.RequireTolerance(name);
        return this;
    }
}

public sealed record ConceptSchemaField(
    string Name,
    ConceptSchemaValueKind Kind,
    bool Required,
    bool RequiresTolerance);

public enum ConceptSchemaValueKind
{
    Target,
    Length,
    Angle,
    String,
    Bool,
    Float,
    Int
}

public sealed record ConceptValidationContext(
    FirmamentConceptApplicationView Application,
    IFirmamentVariables Variables);
