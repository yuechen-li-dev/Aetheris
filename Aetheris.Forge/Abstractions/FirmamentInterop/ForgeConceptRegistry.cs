namespace Aetheris.Forge.Abstractions.FirmamentInterop;

public interface IForgeConcept
{
    ConceptId Id { get; }
    void Define(ConceptSchemaBuilder schema);
    IEnumerable<FirmamentDiagnostic> Validate(ConceptValidationContext context);
}

public interface IForgePmiObligationProvider
{
    IEnumerable<PmiObligation> GetPmiObligations(ConceptValidationContext context);
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
    public ConceptSchemaFieldBuilder RequiredMaterial(string name) => Add(name, ConceptSchemaValueKind.Material);
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
    Material,
    String,
    Bool,
    Float,
    Int
}

public sealed class ConceptValidationContext
{
    private readonly IReadOnlyDictionary<string, FirmamentConceptFieldView> fields;

    public ConceptValidationContext(
        FirmamentConceptApplicationView application,
        IFirmamentVariables variables)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(variables);

        Application = application;
        Variables = variables;
        fields = application.Fields.ToDictionary(field => field.Name, StringComparer.Ordinal);
    }

    public FirmamentConceptApplicationView Application { get; }

    public IFirmamentVariables Variables { get; }

    public IReadOnlyDictionary<string, FirmamentConceptFieldView> Fields => fields;

    public bool TryGetField(string name, out FirmamentConceptFieldView field) => fields.TryGetValue(name, out field!);

    public bool TryGetScalar(string name, out FirmamentScalarValue value)
    {
        if (TryGetField(name, out var field) && field.Value is FirmamentScalarValue scalar)
        {
            value = scalar;
            return true;
        }

        value = null!;
        return false;
    }

    public bool TryGetNumeric(string name, FirmamentValueKind expectedKind, out double numericValue)
    {
        numericValue = 0;
        if (!TryGetScalar(name, out var value) || value.Kind != expectedKind || value.NumericValue is null)
        {
            return false;
        }

        numericValue = value.NumericValue.Value;
        return true;
    }

    public bool TryGetTargetSource(string name, out string targetSource)
    {
        if (TryGetField(name, out var field) && !string.IsNullOrWhiteSpace(field.TargetSource))
        {
            targetSource = field.TargetSource;
            return true;
        }

        targetSource = null!;
        return false;
    }
}

public sealed record PmiObligation(
    string Kind,
    ConceptId SourceConcept,
    string? SourceName,
    string? TargetSource,
    string? ExpectedDimensionField,
    FirmamentDiagnosticSeverity Severity);
