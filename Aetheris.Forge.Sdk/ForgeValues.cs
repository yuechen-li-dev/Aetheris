using System.Globalization;

namespace Aetheris.Forge.Sdk;

public abstract record ForgeValue(string TypeName)
{
    internal abstract string CanonicalLiteral { get; }
}

public sealed record ForgeLength(double Millimeters) : ForgeValue("Length")
{
    internal override string CanonicalLiteral => Millimeters.ToString("R", CultureInfo.InvariantCulture) + "mm";
}

public sealed record ForgeAngle(double Degrees) : ForgeValue("Angle")
{
    internal override string CanonicalLiteral => Degrees.ToString("R", CultureInfo.InvariantCulture) + "deg";
}

public sealed record ForgeInteger(int Value) : ForgeValue("Int")
{
    internal override string CanonicalLiteral => Value.ToString(CultureInfo.InvariantCulture);
}

public sealed record ForgeReal(double Value) : ForgeValue("Float")
{
    internal override string CanonicalLiteral => Value.ToString("R", CultureInfo.InvariantCulture);
}

public sealed record ForgeBoolean(bool Value) : ForgeValue("Bool")
{
    internal override string CanonicalLiteral => Value ? "true" : "false";
}

public sealed record ForgeString(string Value) : ForgeValue("String")
{
    internal override string CanonicalLiteral => "\"" + Value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}

public sealed record ForgeType(string Name) : ForgeValue("Type")
{
    internal override string CanonicalLiteral => Name;
}

public sealed record ForgeRecord(string RecordType, IReadOnlyDictionary<string, ForgeValue> Fields) : ForgeValue(RecordType)
{
    internal override string CanonicalLiteral => RecordType;
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
