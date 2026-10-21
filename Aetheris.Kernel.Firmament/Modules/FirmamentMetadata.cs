using System.Text.Json;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.Modules;

internal sealed record FirmamentBoundMetadata(string Name, string Type,
    IReadOnlyDictionary<string, string> Fields, IReadOnlyDictionary<string, string> FieldTypes);

internal static class FirmamentMetadata
{
    internal static FirmamentBoundMetadata? Bind(string source, ExpressionSyntax expression, List<string> diagnostics)
    {
        if (expression is not ReferenceSyntax reference)
        { diagnostics.Add("firmament-metadata-record-required: Select a typed Static Record value."); return null; }
        var binding = new FirmamentDataBinding(source, moduleContext: true);
        diagnostics.AddRange(binding.Diagnostics.Select(d => d.Code + ": " + d.Message));
        var name = binding.ResolveIdentity("value", reference.Name.Text, expression.Span.Start);
        var value = FirmamentV2TemplateExpansion.InspectStaticRecords(binding.Source, diagnostics).SingleOrDefault(r => r.Name == name);
        if (value is null) { diagnostics.Add("firmament-metadata-record-unresolved: " + reference.Name.Text); return null; }
        var schemas = FirmamentV2TemplateExpansion.InspectRecords(binding.Source, diagnostics).ToDictionary(r => r.Name);
        string TypeOf(string path)
        {
            var type = value.TypeName;
            foreach (var component in path.Split('.'))
            {
                if (!schemas.TryGetValue(type, out var schema) || !schema.Fields.TryGetValue(component, out type!)) return "";
            }
            return type;
        }
        return new(reference.Name.Text, binding.DisplayName(value.TypeName),
            value.Fields.ToDictionary(p => p.Key, p => String(p.Value), StringComparer.Ordinal),
            value.Fields.ToDictionary(p => p.Key, p => TypeOf(p.Key), StringComparer.Ordinal));
    }
    internal static string String(string value) => value.StartsWith('"') ? JsonSerializer.Deserialize<string>(value)! : value;
}
