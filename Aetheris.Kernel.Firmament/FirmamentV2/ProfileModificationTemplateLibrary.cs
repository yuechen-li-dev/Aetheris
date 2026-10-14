using System.Reflection;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public static class ProfileModificationTemplateLibrary
{
    private const string ResourceName="Aetheris.Kernel.Firmament.Resources.ProfileModifications.firmament";
    internal static bool IsImport(DirectiveSyntax directive) => directive.Kind == DirectiveKind.Use
        && FirmamentSyntaxValues.Reference(directive.Value) == "Profile.Modifications";
    public static string ResolveImports(string source)
    {
        if (!source.Contains("Use", StringComparison.Ordinal)) return source;
        var syntax = FirmamentSyntaxParser.Parse(source);
        var imports = syntax.Document.Statements.OfType<DirectiveSyntax>().Where(IsImport).ToArray();
        if (imports.Length == 0) return source;
        foreach (var import in imports.OrderByDescending(d => d.Span.Start))
        {
            var length = import.Span.Length;
            if (import.Span.Start + length < source.Length && source[import.Span.Start + length] == ';') length++;
            source = source.Remove(import.Span.Start, length);
        }
        return Source + Environment.NewLine + source;
    }
    public static string Source
    {
        get
        {
            using var stream=typeof(ProfileModificationTemplateLibrary).Assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException($"Embedded Firmament module '{ResourceName}' was not found.");
            using var reader=new StreamReader(stream);return reader.ReadToEnd();
        }
    }
}
