namespace Aetheris.Kernel.Firmament.FirmamentV2;

// Only declarations explicitly marked with these attributes enter the generated registry.
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class FirmamentConstructAttribute(string stableId, string name) : Attribute
{
    public string StableId { get; } = stableId;
    public string Name { get; } = name;
    public string? Context { get; init; }
    public string? Entry { get; init; }
    public string? Description { get; init; }
    public string? CompatibilityAlias { get; init; }
}

public enum FirmamentSchemaValueKind { Length, Angle, Scalar, Count, Boolean, String, Choice, Vector, Point, Profile, ConstructionPlane, FaceSelector, BodySelector, ConstructReference, Box3, TypeRequirement }
public enum FirmamentUnitKind { None, Length, Angle }

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class FirmamentFieldAttribute(string stableId, string name, FirmamentSchemaValueKind kind) : Attribute
{
    public string StableId { get; } = stableId;
    public string Name { get; } = name;
    public FirmamentSchemaValueKind Kind { get; } = kind;
    public FirmamentUnitKind Unit { get; init; }
    public bool Required { get; init; }
    public string? Default { get; init; }
    public string? Description { get; init; }
    public string[]? Choices { get; init; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class FirmamentOutputAttribute(string stableId, string name, string kind) : Attribute
{
    public string StableId { get; } = stableId;
    public string Name { get; } = name;
    public string Kind { get; } = kind;
    public bool SourceAddressable { get; init; }
    public string? SourceRole { get; init; }
}

public readonly record struct FirmamentConstructId(string Value);
public readonly record struct FirmamentFieldId(string Value);
public readonly record struct FirmamentOutputId(string Value);
public sealed record FirmamentFieldSchema(FirmamentFieldId Id, string Name, FirmamentSchemaValueKind Kind, FirmamentUnitKind Unit,
    bool Required, string? Default, IReadOnlyList<string> Choices, string? Description, bool SourceEditable);
public sealed record FirmamentOutputSchema(FirmamentOutputId Id, string Name, string Kind, bool SourceAddressable, string? SourceRole);
public sealed record FirmamentConstructSchema(FirmamentConstructId Id, string Name, string? Context, string? Entry,
    string? Description, string? CompatibilityAlias, IReadOnlyList<FirmamentFieldSchema> Fields,
    IReadOnlyList<FirmamentOutputSchema> Outputs);

