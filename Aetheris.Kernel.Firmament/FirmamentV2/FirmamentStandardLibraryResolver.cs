using System.Text.RegularExpressions;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentLibraryResolution(
    string Source,
    IReadOnlyList<string> Modules,
    IReadOnlyList<string> Declarations);

/// <summary>
/// Resolves the finite shipped Standard Products namespace into named declarations. This is a
/// semantic catalog link: only known exports can be selected, no file path or arbitrary source
/// include is admitted, and the embedded catalog remains the sole definition authority.
/// </summary>
public static class FirmamentStandardLibraryResolver
{
    public const string UnknownModule = "firmament-library-unknown-module";
    public const string UnknownDeclaration = "firmament-library-unknown-declaration";
    public const string MissingUse = "firmament-library-module-not-used";
    public const string AmbiguousSymbol = "firmament-library-ambiguous-symbol";

    private sealed record Export(
        string Module,
        string PublicName,
        string TemplateName,
        string RecordName,
        string StaticName);

    private static readonly Export[] Exports =
    [
        new("Standard.Products.Office", "Paperclip", "PaperclipTemplate", "PaperclipPolicy", "StandardPaperclip"),
        new("Standard.Products.Mechanical", "MountingPlate", "MountingPlateTemplate", "MountingPlatePolicy", "StandardMountingPlate"),
        new("Standard.Products.Mechanical", "BearingBlock", "BearingBlockTemplate", "BearingBlockPolicy", "StandardBearingBlock"),
        new("Standard.Products.Mechanical", "MachinedAngleBracket", "MachinedAngleBracketTemplate", "MachinedAngleBracketPolicy", "StandardMachinedAngleBracket"),
        new("Standard.Products.Mechanical", "ShaftCollar", "ShaftCollarTemplate", "ShaftCollarPolicy", "StandardShaftCollar"),
        new("Standard.Products.Mechanical", "FlangedAdapter", "FlangedAdapterTemplate", "FlangedAdapterPolicy", "StandardFlangedAdapter"),
        new("Standard.Products.Mechanical", "Standoff", "StandoffTemplate", "StandoffPolicy", "StandardStandoff"),
        new("Standard.Products.Electronics", "RackPanel", "RackPanelTemplate", "RackPanelPolicy", "StandardRackPanel"),
    ];

    public static FirmamentLibraryResolution? Resolve(string source, out IReadOnlyList<string> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(source);
        var errors = new List<string>();
        var uses = Regex.Matches(source, @"(?m)^\s*Use\s+(?<module>[A-Za-z_][A-Za-z0-9_.]*)\s*$", RegexOptions.CultureInvariant)
            .Cast<Match>().ToArray();
        if (uses.Length == 0)
        {
            diagnostics = [];
            return new(source, [], []);
        }

        var knownModules = Exports.Select(item => item.Module).ToHashSet(StringComparer.Ordinal);
        var imported = uses.Select(match => match.Groups["module"].Value).Distinct(StringComparer.Ordinal).ToArray();
        foreach (var module in imported.Where(module => !knownModules.Contains(module)))
            errors.Add($"{UnknownModule}:{module}: shipped Standard Library module could not be resolved");

        var references = Regex.Matches(source,
                @"\bStandard\.Products\.(?:Office|Mechanical|Electronics)\.[A-Za-z_][A-Za-z0-9_]*\b",
                RegexOptions.CultureInvariant)
            .Cast<Match>().Select(match => match.Value).Distinct(StringComparer.Ordinal).ToArray();
        var selected = new HashSet<Export>();
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var reference in references)
        {
            var split = reference.LastIndexOf('.');
            var module = reference[..split];
            var symbol = reference[(split + 1)..];
            if (!imported.Contains(module, StringComparer.Ordinal))
            {
                errors.Add($"{MissingUse}:{module}:{reference}");
                continue;
            }
            var export = Exports.FirstOrDefault(item => item.Module == module
                && (item.PublicName == symbol || item.RecordName == symbol || item.StaticName == symbol));
            if (export is null)
            {
                errors.Add($"{UnknownDeclaration}:{reference}");
                continue;
            }
            selected.Add(export);
            replacements[reference] = symbol == export.PublicName ? export.TemplateName : symbol;
        }
        if (errors.Count > 0)
        {
            diagnostics = errors.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            return null;
        }

        foreach (var export in selected)
        {
            foreach (var localName in new[] { export.TemplateName, export.RecordName, export.StaticName })
            {
                if (Regex.IsMatch(source, $@"\b(?:Template\s*<[^>]*>\s*(?:Struct\s+)?|Record\s+|Static\s+){Regex.Escape(localName)}\b", RegexOptions.CultureInvariant))
                    errors.Add($"{AmbiguousSymbol}:{localName}: local declaration collides with shipped export");
            }
        }
        if (errors.Count > 0)
        {
            diagnostics = errors;
            return null;
        }

        var resolved = source;
        foreach (var replacement in replacements.OrderByDescending(item => item.Key.Length))
            resolved = resolved.Replace(replacement.Key, replacement.Value, StringComparison.Ordinal);
        resolved = Regex.Replace(resolved, @"(?m)^\s*Use\s+[A-Za-z_][A-Za-z0-9_.]*\s*\r?\n?", string.Empty, RegexOptions.CultureInvariant);
        if (selected.Count > 0)
        {
            var declarations = string.Join(Environment.NewLine + Environment.NewLine,
                selected.OrderBy(item => item.PublicName, StringComparer.Ordinal)
                    .Select(item => StandardProductTemplateLibrary.GetExportedDeclarations(item.TemplateName, item.RecordName)));
            var close = resolved.LastIndexOf('}');
            if (close < 0)
            {
                diagnostics = ["firmament-library-invalid-consuming-module: canonical Model body was not found"];
                return null;
            }
            resolved = resolved.Insert(close, Environment.NewLine + declarations + Environment.NewLine);
        }

        diagnostics = [];
        return new(resolved, imported.Order(StringComparer.Ordinal).ToArray(),
            selected.Select(item => item.Module + "." + item.PublicName).Order(StringComparer.Ordinal).ToArray());
    }
}
