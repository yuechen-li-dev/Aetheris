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

    public string Build(string applicationName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ISO-10303-21;");
        sb.AppendLine("HEADER;");
        sb.AppendLine($"FILE_DESCRIPTION(('Aetheris AP242 subset export'),'2;1');");
        sb.AppendLine($"FILE_NAME('aetheris_export.step','1970-01-01T00:00:00',('Aetheris'),('{EscapeString(applicationName)}'),'Aetheris.Kernel','Aetheris.Kernel','');");
        sb.AppendLine("FILE_SCHEMA(('AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF'));");
        sb.AppendLine("ENDSEC;");
        sb.AppendLine("DATA;");

        foreach (var entity in _entities)
        {
            sb.AppendLine(entity);
        }

        sb.AppendLine("ENDSEC;");
        sb.AppendLine("END-ISO-10303-21;");
        return sb.ToString();
    }

    public static string Ref(string entityId) => entityId;

    public static string String(string value) => $"'{EscapeString(value)}'";

    public static string Enum(string value) => $".{value}.";

    public static string Number(double value) => value.ToString("0.###############", CultureInfo.InvariantCulture);

    public static string BooleanLogical(bool value) => value ? ".T." : ".F.";

    public static string List(params string[] values) => $"({string.Join(",", values)})";

    private static string EscapeString(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
