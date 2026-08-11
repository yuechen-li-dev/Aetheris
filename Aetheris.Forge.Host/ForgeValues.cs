using System.Globalization;

namespace Aetheris.Forge.Host;

public abstract record ForgeValue(string TypeName)
{
    internal abstract string CanonicalLiteral { get; }

    public static ForgeValue From(int value) => new ForgeInteger(value);
    public static ForgeValue From(double value) => new ForgeReal(value);
    public static ForgeValue From(bool value) => new ForgeBoolean(value);
    public static ForgeValue From(string value) => new ForgeString(value);
    public static ForgeValue From(Length value) => new ForgeLength(value.Millimeters);
    public static ForgeValue From(Angle value) => new ForgeAngle(value.Degrees);
    public static ForgeValue From(Version value) => new ForgeVersion(value);
    public static ForgeValue EnumCase(string enumType, string caseName) => new ForgeEnumCase(enumType, caseName);
}

/// <summary>An application-side length expressed explicitly in millimetres.</summary>
public readonly record struct Length(double Millimeters);

/// <summary>An application-side angle expressed explicitly in degrees.</summary>
public readonly record struct Angle(double Degrees);

public sealed record ForgeLength(double Millimeters) : ForgeValue("Length")
{
    internal override string CanonicalLiteral => Millimeters.ToString("R", CultureInfo.InvariantCulture) + "mm";
}

public sealed record ForgeAngle(double Degrees) : ForgeValue("Angle")
{
    internal override string CanonicalLiteral => Degrees.ToString("R", CultureInfo.InvariantCulture) + "deg";
}

public sealed record ForgeInteger(int Value) : ForgeValue("int")
{
    internal override string CanonicalLiteral => Value.ToString(CultureInfo.InvariantCulture);
}

public sealed record ForgeReal(double Value) : ForgeValue("float")
{
    internal override string CanonicalLiteral => Value.ToString("R", CultureInfo.InvariantCulture);
}

public sealed record ForgeBoolean(bool Value) : ForgeValue("bool")
{
    internal override string CanonicalLiteral => Value ? "true" : "false";
}

public sealed record ForgeString(string Value) : ForgeValue("String")
{
    internal override string CanonicalLiteral => "\"" + Value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}

public sealed record ForgeVersion(Version Value) : ForgeValue("Version")
{
    internal override string CanonicalLiteral => Value.ToString();
}

public sealed record ForgeType(string Name) : ForgeValue("Type")
{
    internal override string CanonicalLiteral => Name;
}

/// <summary>A typed Firmament enum case. It is emitted as a symbol, never coerced to a string.</summary>
public sealed record ForgeEnumCase : ForgeValue
{
    public ForgeEnumCase(string enumType, string caseName) : base(ValidateIdentifier(enumType, nameof(enumType)))
    {
        CaseName = ValidateIdentifier(caseName, nameof(caseName));
    }

    public string EnumType => TypeName;
    public string CaseName { get; }
    internal override string CanonicalLiteral => CaseName;

    private static string ValidateIdentifier(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (!(char.IsLetter(value[0]) || value[0] == '_') || value.Skip(1).Any(character => !(char.IsLetterOrDigit(character) || character == '_')))
            throw new ArgumentException("Firmament enum type and case names must be identifiers.", parameterName);
        return value;
    }
}

/// <summary>Typed seam from a Template parameter to an ImportedStepResource on the invocation.</summary>
public sealed record ForgeImportedStep(string ResourceName) : ForgeValue("ImportedStep")
{
    internal override string CanonicalLiteral => "$" + ResourceName;
}

public sealed record ForgeRecord(string RecordType, IReadOnlyDictionary<string, ForgeValue> Fields) : ForgeValue(RecordType)
{
    internal override string CanonicalLiteral => RecordType;
}

/// <summary>
/// Explicit, reflection-free mapping from an application record/DTO to a
/// Firmament Record value. Field insertion is canonicalized by ordinal name.
/// </summary>
public sealed class ForgeRecordDescriptor<T>
{
    private readonly IReadOnlyDictionary<string, Func<T, ForgeValue>> fields;

    public ForgeRecordDescriptor(string recordType, IReadOnlyDictionary<string, Func<T, ForgeValue>> fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordType);
        ArgumentNullException.ThrowIfNull(fields);
        if (fields.Count == 0) throw new ArgumentException("At least one Record field mapping is required.", nameof(fields));
        if (fields.Keys.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Record field names cannot be blank.", nameof(fields));
        RecordType = recordType;
        this.fields = fields.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    public string RecordType { get; }

    public ForgeRecord Map(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ForgeRecord(RecordType, fields.ToDictionary(
            pair => pair.Key,
            pair => pair.Value(value) ?? throw new InvalidOperationException($"Mapper for field '{pair.Key}' returned null."),
            StringComparer.Ordinal));
    }
}

public abstract record ForgeResource(string Name, string ContentHash);

public sealed record ImportedStepResource(
    string Name,
    string Path,
    string ContentHash,
    bool Canonical,
    Aetheris.Kernel.Core.Brep.BrepBody Body) : ForgeResource(Name, ContentHash)
{
    public static ImportedStepResource Load(string name, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = System.IO.Path.GetFullPath(path);
        var bytes = File.ReadAllBytes(fullPath);
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        var canonical = text.Contains("AETHERIS", StringComparison.OrdinalIgnoreCase)
            && text.Contains("ISO-10303-21", StringComparison.Ordinal);
        if (!canonical) throw new InvalidDataException("Imported STEP resource must be an Aetheris-canonical AP242 artifact. Canonicalize it through the ordinary InlineStep pipeline first.");
        var imported = Aetheris.Kernel.Core.Step242.Step242Importer.ImportBody(text);
        if (!imported.IsSuccess || imported.Value is null)
            throw new InvalidDataException("Imported STEP resource failed the ordinary Aetheris STEP importer: " + string.Join("; ", imported.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return new(name, fullPath, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)), canonical, imported.Value);
    }
}
