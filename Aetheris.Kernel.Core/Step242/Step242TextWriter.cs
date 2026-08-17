using System.Globalization;
using System.Text;

namespace Aetheris.Kernel.Core.Step242;

internal sealed class Step242TextWriter
{
    private readonly List<string> _entities = [];

    public string AddEntity(string entityName, params string[] arguments)
    {
        var id = $"#{_entities.Count + 1}";
        _entities.Add($"{id}={entityName}({string.Join(",", arguments)});");
        return id;
    }

    public string AddRawEntity(string entityInstance)
    {
        var id = $"#{_entities.Count + 1}";
        _entities.Add($"{id}={entityInstance};");
        return id;
    }

    public string Build(Step242HeaderMetadata headerMetadata)
    {
        var sb = new StringBuilder();
        AppendCanonicalLine(sb, "ISO-10303-21;");
        AppendCanonicalLine(sb, "HEADER;");
        AppendCanonicalLine(sb, $"FILE_DESCRIPTION(('{EscapeString(headerMetadata.Description)}'),'2;1');");
        AppendCanonicalLine(sb, $"FILE_NAME('{EscapeString(headerMetadata.FileName)}','{EscapeString(headerMetadata.CreationTimestamp)}',('{EscapeString(headerMetadata.Author)}'),('{EscapeString(headerMetadata.Organization)}'),'{EscapeString(headerMetadata.PreprocessorVersion)}','{EscapeString(headerMetadata.OriginatingSystem)}','{EscapeString(headerMetadata.Authorization)}');");
        AppendCanonicalLine(sb, "FILE_SCHEMA(('AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF'));");
        AppendCanonicalLine(sb, "ENDSEC;");
        AppendCanonicalLine(sb, "DATA;");

        foreach (var entity in _entities)
        {
            AppendCanonicalLine(sb, entity);
        }

        AppendCanonicalLine(sb, "ENDSEC;");
        AppendCanonicalLine(sb, "END-ISO-10303-21;");
        return sb.ToString();
    }

    internal static StringBuilder AppendCanonicalLine(StringBuilder builder, string value)
        => builder.Append(value).Append("\r\n");

    public static string Ref(string entityId) => entityId;

    public static string String(string value) => $"'{EscapeString(value)}'";

    public static string Enum(string value) => $".{value}.";

    // Thirteen fractional digits keep exported geometry well below kernel tolerances while
    // suppressing the final-bit libm variation that can otherwise change STEP bytes by OS.
    public static string Number(double value) => value.ToString("0.#############", CultureInfo.InvariantCulture);

    public static string BooleanLogical(bool value) => value ? ".T." : ".F.";

    public static string List(params string[] values) => $"({string.Join(",", values)})";

    private static string EscapeString(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
