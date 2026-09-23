using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Aetheris.Firmament.SchemaGenerator;

[Generator]
public sealed class SemanticSchemaGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor Invalid = new("FMS001", "Invalid Firmament schema", "{0}",
        "Firmament schema", DiagnosticSeverity.Error, true);
    private static readonly HashSet<string> Kinds = new(StringComparer.Ordinal)
    { "Length", "Angle", "Scalar", "Count", "Boolean", "String", "Choice", "Vector", "Point", "Profile", "ConstructionPlane", "FaceSelector", "BodySelector", "ConstructReference", "Box3", "TypeRequirement" };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var declarations = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Aetheris.Kernel.Firmament.FirmamentV2.FirmamentConstructAttribute",
            static (node, _) => node is Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax,
            static (ctx, _) => (INamedTypeSymbol)ctx.TargetSymbol);
        context.RegisterSourceOutput(declarations.Collect(), static (output, symbols) => Generate(output, symbols));
    }

    private static void Generate(SourceProductionContext output, System.Collections.Immutable.ImmutableArray<INamedTypeSymbol> symbols)
    {
        var entries = new List<Entry>();
        var constructIds = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var symbol in symbols.OrderBy(s => s.ToDisplayString(), StringComparer.Ordinal))
        {
            var construct = symbol.GetAttributes().Single(a => a.AttributeClass?.Name == "FirmamentConstructAttribute");
            var id = Arg(construct, 0); var name = Arg(construct, 1);
            if (!ValidId(id) || !ValidId(name) || !constructIds.Add(id) || !names.Add(name))
            { Error(output, symbol, $"construct '{name}' needs a unique, nonempty stable ID and name"); continue; }
            var fields = new List<Field>();
            var outputs = new List<Output>();
            var fieldIds = new HashSet<string>(StringComparer.Ordinal);
            var fieldNames = new HashSet<string>(StringComparer.Ordinal);
            var outputIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var attribute in symbol.GetAttributes())
            {
                if (attribute.AttributeClass?.Name == "FirmamentFieldAttribute")
                {
                    var fieldId = Arg(attribute, 0); var fieldName = Arg(attribute, 1);
                    var kind = attribute.ConstructorArguments[2].Value is int value ? EnumName(attribute.ConstructorArguments[2], value) : "";
                    var unitConstant = Named(attribute, "Unit");
                    var unit = unitConstant.Value is int unitValue ? EnumName(unitConstant, unitValue) : kind == "Length" ? "Length" : kind == "Angle" ? "Angle" : "None";
                    var choiceConstant = Named(attribute, "Choices");
                    var choices = choiceConstant.Kind == TypedConstantKind.Array ? choiceConstant.Values.Select(v => v.Value?.ToString() ?? "").ToArray() : Array.Empty<string>();
                    if (!ValidId(fieldId) || !ValidId(fieldName) || !fieldIds.Add(fieldId) || !fieldNames.Add(fieldName) || !Kinds.Contains(kind)
                        || kind == "Choice" && choices.Length == 0 || kind != "Choice" && choices.Length != 0
                        || unit is not ("None" or "Length" or "Angle")
                        || kind == "Length" && unit != "Length" || kind == "Angle" && unit != "Angle"
                        || kind is not ("Length" or "Angle" or "Vector" or "Point") && unit != "None")
                    { Error(output, symbol, $"field '{fieldName}' in {name} has a duplicate/missing ID, unsupported kind, or invalid choices"); continue; }
                    var editable = Bool(attribute, "SourceEditable");
                    var projectorName = Str(attribute, "ProjectionMember");
                    IMethodSymbol? projector = null;
                    if (projectorName is not null)
                        projector = symbol.GetMembers(projectorName).OfType<IMethodSymbol>().SingleOrDefault(method =>
                            method.IsStatic && method.DeclaredAccessibility == Accessibility.Public &&
                            method.Parameters.Length == 1 && method.ReturnType.SpecialType == SpecialType.System_Double);
                    if (editable != (projector is not null) || projectorName is not null && projector is null)
                    { Error(output, symbol, $"source-editable field '{fieldName}' in {name} requires one public static double projector with one semantic-instance parameter"); continue; }
                    fields.Add(new(fieldId, fieldName, kind, unit, Bool(attribute, "Required"), Str(attribute, "Default"), Str(attribute, "Description"), choices,
                        editable, projectorName, projector?.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                }
                else if (attribute.AttributeClass?.Name == "FirmamentOutputAttribute")
                {
                    var outputId = Arg(attribute, 0); var outputName = Arg(attribute, 1); var outputKind = Arg(attribute, 2);
                    if (!ValidId(outputId) || !ValidId(outputName) || !outputIds.Add(outputId))
                    { Error(output, symbol, $"output '{outputName}' in {name} has a duplicate/missing ID"); continue; }
                    outputs.Add(new(outputId, outputName, outputKind, Bool(attribute, "SourceAddressable"), Str(attribute, "SourceRole")));
                }
            }
            entries.Add(new(id, name, Str(construct, "Context"), Str(construct, "Entry"), Str(construct, "Description"), Str(construct, "CompatibilityAlias"), fields.OrderBy(f => f.Id, StringComparer.Ordinal).ToArray(), outputs.OrderBy(o => o.Id, StringComparer.Ordinal).ToArray()));
        }
        entries.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
        var source = new StringBuilder("// <auto-generated/>\n#nullable enable\nnamespace Aetheris.Kernel.Firmament.FirmamentV2;\npublic static class FirmamentSemanticSchemas\n{\n");
        source.Append("    public const string Version = \"firmament-semantic-schema/1\";\n");
        source.Append("    public static readonly System.Collections.Generic.IReadOnlyList<FirmamentConstructSchema> All = System.Array.AsReadOnly(new FirmamentConstructSchema[]\n    {\n");
        foreach (var entry in entries)
        {
            source.Append("        new(new FirmamentConstructId(").Append(Q(entry.Id)).Append("), ").Append(Q(entry.Name)).Append(", ").Append(Q(entry.Context)).Append(", ").Append(Q(entry.Snippet)).Append(", ").Append(Q(entry.Description)).Append(", ").Append(Q(entry.Alias)).Append(",\n            System.Array.AsReadOnly(new FirmamentFieldSchema[] {\n");
            foreach (var field in entry.Fields)
            {
                source.Append("                new(new FirmamentFieldId(").Append(Q(entry.Id + "." + field.Id)).Append("), ").Append(Q(field.Name)).Append(", FirmamentSchemaValueKind.").Append(field.Kind).Append(", FirmamentUnitKind.").Append(field.Unit).Append(", ").Append(field.Required ? "true" : "false").Append(", ").Append(Q(field.Default)).Append(", System.Array.AsReadOnly(new string[] { ").Append(string.Join(", ", field.Choices.Select(Q))).Append(" }), ").Append(Q(field.Description)).Append(", ").Append(field.Editable ? "true" : "false").Append("),\n");
            }
            source.Append("            }), System.Array.AsReadOnly(new FirmamentOutputSchema[] {\n");
            foreach (var item in entry.Outputs)
                source.Append("                new(new FirmamentOutputId(").Append(Q(entry.Id + "." + item.Id)).Append("), ").Append(Q(item.Name)).Append(", ").Append(Q(item.Kind)).Append(", ").Append(item.Addressable ? "true" : "false").Append(", ").Append(Q(item.Role)).Append("),\n");
            source.Append("            })),\n");
        }
        source.Append("    });\n    public static FirmamentConstructSchema? Get(string name) => System.Linq.Enumerable.FirstOrDefault(All, x => x.Name == name);\n");
        foreach (var entry in entries)
        foreach (var field in entry.Fields.Where(field => field.Editable))
            source.Append("    public static double Project").Append(entry.Id).Append(field.Id).Append("(")
                .Append(field.ProjectorType).Append(" instance) => ").Append(field.ProjectorOwner).Append(".")
                .Append(field.ProjectorName).Append("(instance);\n");
        source.Append("}\n");
        output.AddSource("FirmamentSemanticSchemas.g.cs", source.ToString());
    }

    private static string EnumName(TypedConstant constant, int value) => constant.Type?.GetMembers().OfType<IFieldSymbol>().FirstOrDefault(f => f.HasConstantValue && f.ConstantValue is int i && i == value)?.Name ?? "";
    private static string Arg(AttributeData attribute, int index) => attribute.ConstructorArguments[index].Value?.ToString() ?? "";
    private static TypedConstant Named(AttributeData attribute, string name) => attribute.NamedArguments.FirstOrDefault(p => p.Key == name).Value;
    private static string? Str(AttributeData attribute, string name) => Named(attribute, name).Value?.ToString();
    private static bool Bool(AttributeData attribute, string name) => Named(attribute, name).Value is true;
    private static bool ValidId(string value) => !string.IsNullOrWhiteSpace(value) && value.All(c => char.IsLetterOrDigit(c) || c == '_');
    private static string Q(string? value) => value is null ? "null" : SymbolDisplay.FormatLiteral(value, true);
    private static void Error(SourceProductionContext output, INamedTypeSymbol symbol, string message) => output.ReportDiagnostic(Diagnostic.Create(Invalid, symbol.Locations.FirstOrDefault(), message));

    private sealed record Entry(string Id, string Name, string? Context, string? Snippet, string? Description, string? Alias, Field[] Fields, Output[] Outputs);
    private sealed record Field(string Id, string Name, string Kind, string Unit, bool Required, string? Default, string? Description, string[] Choices,
        bool Editable, string? ProjectorName, string? ProjectorType, string ProjectorOwner);
    private sealed record Output(string Id, string Name, string Kind, bool Addressable, string? Role);
}




