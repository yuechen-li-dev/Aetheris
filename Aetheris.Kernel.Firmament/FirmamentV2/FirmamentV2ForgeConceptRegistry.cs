using Aetheris.Forge.Abstractions.FirmamentInterop;
using Aetheris.Forge.Standard;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public enum FirmamentV2ForgeFieldKind
{
    Length,
    Angle,
    Target,
    Material,
    String,
    Bool,
    Float,
    Int
}

public sealed record FirmamentV2ForgeFieldDescriptor(string Name, FirmamentV2ForgeFieldKind Kind, bool Required)
{
    public bool Accepts(FirmamentV2PrimitiveType type) => Kind switch
    {
        FirmamentV2ForgeFieldKind.Length => type == FirmamentV2PrimitiveType.Length,
        FirmamentV2ForgeFieldKind.Angle => type == FirmamentV2PrimitiveType.Angle,
        FirmamentV2ForgeFieldKind.Material => type == FirmamentV2PrimitiveType.String,
        FirmamentV2ForgeFieldKind.String => type == FirmamentV2PrimitiveType.String,
        FirmamentV2ForgeFieldKind.Bool => type == FirmamentV2PrimitiveType.Bool,
        FirmamentV2ForgeFieldKind.Float => type == FirmamentV2PrimitiveType.Float,
        FirmamentV2ForgeFieldKind.Int => type == FirmamentV2PrimitiveType.Int,
        FirmamentV2ForgeFieldKind.Target => false,
        _ => false
    };
}

public sealed record FirmamentV2ForgeConceptDescriptor(string FamilyName, string ConceptName, IReadOnlyDictionary<string, FirmamentV2ForgeFieldDescriptor> Fields);

public sealed class FirmamentV2ForgeConceptCatalog
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, FirmamentV2ForgeConceptDescriptor>> families;

    public FirmamentV2ForgeConceptCatalog(IReadOnlyDictionary<string, IReadOnlyDictionary<string, FirmamentV2ForgeConceptDescriptor>> families)
    {
        ArgumentNullException.ThrowIfNull(families);
        this.families = families;
    }

    public bool HasFamily(string familyName) => families.ContainsKey(familyName);

    public bool TryGet(string familyName, string conceptName, out FirmamentV2ForgeConceptDescriptor descriptor)
    {
        descriptor = null!;
        if (!families.TryGetValue(familyName, out var concepts))
        {
            return false;
        }

        return concepts.TryGetValue(conceptName, out descriptor!);
    }

    public IReadOnlyList<FirmamentV2ForgeConceptDescriptor> EnumerateDescriptors() =>
        families.Values
            .SelectMany(concepts => concepts.Values)
            .OrderBy(descriptor => descriptor.FamilyName, StringComparer.Ordinal)
            .ThenBy(descriptor => descriptor.ConceptName, StringComparer.Ordinal)
            .ToArray();

    public static FirmamentV2ForgeConceptCatalog FromConcepts(IEnumerable<IForgeConcept> concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);

        var families = new Dictionary<string, Dictionary<string, FirmamentV2ForgeConceptDescriptor>>(StringComparer.Ordinal);
        foreach (var concept in concepts)
        {
            var descriptor = ToDescriptor(concept);
            if (!families.TryGetValue(descriptor.FamilyName, out var family))
            {
                family = new Dictionary<string, FirmamentV2ForgeConceptDescriptor>(StringComparer.Ordinal);
                families.Add(descriptor.FamilyName, family);
            }

            family.Add(descriptor.ConceptName, descriptor);
        }

        return new FirmamentV2ForgeConceptCatalog(
            families.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyDictionary<string, FirmamentV2ForgeConceptDescriptor>)pair.Value,
                StringComparer.Ordinal));
    }

    public static FirmamentV2ForgeConceptCatalog CreateBuiltIn()
    {
        var registry = new ForgeConceptRegistry();
        new StandardForgeRuntimeConceptPack().Register(registry);
        return FromConcepts(registry.EnumerateConcepts());
    }

    private static FirmamentV2ForgeConceptDescriptor ToDescriptor(IForgeConcept concept)
    {
        ArgumentNullException.ThrowIfNull(concept);

        var schema = new ConceptSchemaBuilder();
        concept.Define(schema);
        return new FirmamentV2ForgeConceptDescriptor(
            concept.Id.Family,
            concept.Id.Concept,
            schema.Fields.ToDictionary(
                field => field.Name,
                field => new FirmamentV2ForgeFieldDescriptor(field.Name, MapFieldKind(field.Kind), field.Required),
                StringComparer.Ordinal));
    }

    private static FirmamentV2ForgeFieldKind MapFieldKind(ConceptSchemaValueKind kind) => kind switch
    {
        ConceptSchemaValueKind.Length => FirmamentV2ForgeFieldKind.Length,
        ConceptSchemaValueKind.Angle => FirmamentV2ForgeFieldKind.Angle,
        ConceptSchemaValueKind.Target => FirmamentV2ForgeFieldKind.Target,
        ConceptSchemaValueKind.Material => FirmamentV2ForgeFieldKind.Material,
        ConceptSchemaValueKind.String => FirmamentV2ForgeFieldKind.String,
        ConceptSchemaValueKind.Bool => FirmamentV2ForgeFieldKind.Bool,
        ConceptSchemaValueKind.Float => FirmamentV2ForgeFieldKind.Float,
        ConceptSchemaValueKind.Int => FirmamentV2ForgeFieldKind.Int,
        _ => throw new InvalidOperationException($"Concept schema kind '{kind}' is not supported by Firmament V2 parser descriptors.")
    };
}

public static class FirmamentV2ForgeConceptRegistry
{
    private static readonly FirmamentV2ForgeConceptCatalog DefaultCatalog = FirmamentV2ForgeConceptCatalog.CreateBuiltIn();

    public static FirmamentV2ForgeConceptCatalog Catalog => DefaultCatalog;

    public static bool HasFamily(string familyName) => DefaultCatalog.HasFamily(familyName);

    public static bool TryGet(string familyName, string conceptName, out FirmamentV2ForgeConceptDescriptor descriptor)
    {
        return DefaultCatalog.TryGet(familyName, conceptName, out descriptor);
    }

    internal static IReadOnlyList<FirmamentV2ForgeConceptDescriptor> EnumerateDescriptors() =>
        DefaultCatalog.EnumerateDescriptors();
}
