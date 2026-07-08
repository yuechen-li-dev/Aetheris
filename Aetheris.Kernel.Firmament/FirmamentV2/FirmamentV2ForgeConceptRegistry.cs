namespace Aetheris.Kernel.Firmament.FirmamentV2;

public enum FirmamentV2ForgeFieldKind
{
    Length,
    Angle,
    Target,
    Material
}

public sealed record FirmamentV2ForgeFieldDescriptor(string Name, FirmamentV2ForgeFieldKind Kind, bool Required)
{
    public bool Accepts(FirmamentV2PrimitiveType type) => Kind switch
    {
        FirmamentV2ForgeFieldKind.Length => type == FirmamentV2PrimitiveType.Length,
        FirmamentV2ForgeFieldKind.Angle => type == FirmamentV2PrimitiveType.Angle,
        FirmamentV2ForgeFieldKind.Material => type == FirmamentV2PrimitiveType.String,
        FirmamentV2ForgeFieldKind.Target => false,
        _ => false
    };
}

public sealed record FirmamentV2ForgeConceptDescriptor(string FamilyName, string ConceptName, IReadOnlyDictionary<string, FirmamentV2ForgeFieldDescriptor> Fields);

public static class FirmamentV2ForgeConceptRegistry
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, FirmamentV2ForgeConceptDescriptor>> Families = Build();

    public static bool HasFamily(string familyName) => Families.ContainsKey(familyName);

    public static bool TryGet(string familyName, string conceptName, out FirmamentV2ForgeConceptDescriptor descriptor)
    {
        descriptor = null!;
        if (!Families.TryGetValue(familyName, out var concepts)) return false;
        return concepts.TryGetValue(conceptName, out descriptor!);
    }

    internal static IReadOnlyList<FirmamentV2ForgeConceptDescriptor> EnumerateDescriptors() =>
        Families.Values
            .SelectMany(concepts => concepts.Values)
            .OrderBy(descriptor => descriptor.FamilyName, StringComparer.Ordinal)
            .ThenBy(descriptor => descriptor.ConceptName, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, FirmamentV2ForgeConceptDescriptor>> Build()
    {
        var process = new Dictionary<string, FirmamentV2ForgeConceptDescriptor>(StringComparer.Ordinal)
        {
            ["CNC"] = Descriptor("process", "CNC",
                Field("material", FirmamentV2ForgeFieldKind.Material),
                Field("minimumToolRadius", FirmamentV2ForgeFieldKind.Length))
        };

        var hole = new Dictionary<string, FirmamentV2ForgeConceptDescriptor>(StringComparer.Ordinal)
        {
            ["Countersink"] = Descriptor("hole", "Countersink",
                Field("target", FirmamentV2ForgeFieldKind.Target),
                Field("diameter", FirmamentV2ForgeFieldKind.Length),
                Field("countersinkDiameter", FirmamentV2ForgeFieldKind.Length),
                Field("angle", FirmamentV2ForgeFieldKind.Angle)),
            ["Shaft"] = Descriptor("hole", "Shaft",
                Field("target", FirmamentV2ForgeFieldKind.Target),
                Field("diameter", FirmamentV2ForgeFieldKind.Length)),
            ["Counterbore"] = Descriptor("hole", "Counterbore",
                Field("target", FirmamentV2ForgeFieldKind.Target),
                Field("diameter", FirmamentV2ForgeFieldKind.Length),
                Field("counterboreDiameter", FirmamentV2ForgeFieldKind.Length),
                Field("counterboreDepth", FirmamentV2ForgeFieldKind.Length))
        };

        return new Dictionary<string, IReadOnlyDictionary<string, FirmamentV2ForgeConceptDescriptor>>(StringComparer.Ordinal)
        {
            ["process"] = process,
            ["hole"] = hole
        };
    }

    private static FirmamentV2ForgeConceptDescriptor Descriptor(string family, string concept, params FirmamentV2ForgeFieldDescriptor[] fields) =>
        new(family, concept, fields.ToDictionary(f => f.Name, StringComparer.Ordinal));

    private static FirmamentV2ForgeFieldDescriptor Field(string name, FirmamentV2ForgeFieldKind kind) => new(name, kind, true);
}
