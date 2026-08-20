namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Authoritative shipped Firmament source for public Standard Products.</summary>
public static class StandardProductTemplateLibrary
{
    private const string ResourceName = "Aetheris.Standard.StandardProducts.firmament";

    public static string Source
    {
        get
        {
            using var stream = typeof(StandardProductTemplateLibrary).Assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException($"Embedded Firmament module '{ResourceName}' was not found.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }

    /// <summary>
    /// Produces the bounded one-family module required by the current native geometry
    /// route selector. Schema inspection still uses <see cref="Source"/> as a catalog.
    /// </summary>
    public static string GetTemplateSource(string templateName, string recordName, bool includeDefaultInstance = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordName);
        var source = Source;
        var record = BlockStartingAt(source, source.IndexOf("Record " + recordName, StringComparison.Ordinal));
        var templateNameIndex = source.IndexOf("Struct " + templateName, StringComparison.Ordinal);
        if (templateNameIndex < 0) throw new InvalidOperationException($"Standard product Template '{templateName}' was not found.");
        var templateStart = source.LastIndexOf("Template<", templateNameIndex, StringComparison.Ordinal);
        var template = BlockStartingAt(source, templateStart);
        var staticPrefix = "Static ";
        var staticType = ": " + recordName;
        var staticTypeIndex = source.IndexOf(staticType, StringComparison.Ordinal);
        var staticStart = staticTypeIndex < 0 ? -1 : source.LastIndexOf(staticPrefix, staticTypeIndex, StringComparison.Ordinal);
        var standard = staticStart < 0 ? string.Empty : BlockStartingAt(source, staticStart);
        var staticName = staticStart < 0 ? string.Empty : source[(staticStart + staticPrefix.Length)..source.IndexOf(':', staticStart)].Trim();
        var instance = includeDefaultInstance && staticName.Length > 0
            ? $"\nStruct StandardProduct = {templateName}<P: {staticName}>\n"
            : string.Empty;
        return $"Model StandardProduct {{\nUnits: mm\n{record}\n{standard}\n{template}\n{instance}}}\n";
    }

    internal static string GetExportedDeclarations(string templateName, string recordName)
    {
        var module = GetTemplateSource(templateName, recordName);
        var units = module.IndexOf("Units: mm", StringComparison.Ordinal);
        var bodyStart = module.IndexOf('\n', units) + 1;
        var bodyEnd = module.LastIndexOf('}');
        return module[bodyStart..bodyEnd].Trim();
    }

    private static string BlockStartingAt(string source, int start)
    {
        if (start < 0) throw new InvalidOperationException("Standard product source declaration was not found.");
        var open = source.IndexOf('{', start);
        if (open < 0) throw new InvalidOperationException("Standard product declaration has no body.");
        var depth = 0;
        for (var index = open; index < source.Length; index++)
        {
            if (source[index] == '{') depth++;
            else if (source[index] == '}' && --depth == 0) return source[start..(index + 1)];
        }
        throw new InvalidOperationException("Standard product declaration body is unterminated.");
    }
}

/// <summary>Compatibility facade retained for X0 Paperclip consumers.</summary>
public static class PaperclipTemplateLibrary
{
    public const string TemplateId = "Standard.Products.Office.Paperclip";
    public static string Source => StandardProductTemplateLibrary.GetTemplateSource(
        "PaperclipTemplate", "PaperclipPolicy", includeDefaultInstance: true);
}
