using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.Drawing;

/// <summary>
/// Authored drawing placement for PMI balloons, written in a model's <c>Pmi</c> block:
/// <code>
/// Label SlotPosition {
///     record: "#38"          // a PMI record: a STEP entity of imported PMI, or a record name of this Pmi block
///     view: Front            // Top, Front or Right: the view sheet that shows it
///     side: Left             // Left, Right, Above or Below its view
///     at: [60mm, 70mm]       // or exactly: balloon centre, mm from the sheet's top-left corner
/// }
/// </code>
/// A label changes where a balloon is drawn, never the requirement.
/// </summary>
public static partial class DrawingLabelDirectives
{
    public const string Invalid = "firmament-v2-pmi-label-invalid";

    public sealed record Directive(string Name, string Record, string? View, string? Side, (double X, double Y)? At);

    internal static readonly IReadOnlyDictionary<string, string> Views = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    { ["Top"] = "TOP", ["Front"] = "FRONT", ["Right"] = "RIGHT" };

    /// <summary>Every <c>Label</c> of the source's Pmi blocks; malformed ones are reported, not dropped silently.</summary>
    public static IReadOnlyList<Directive> Read(string source, List<string> diagnostics)
    {
        var result = new List<Directive>();
        var document = FirmamentSyntaxParser.Parse(source).Document;
        foreach (var pmi in FirmamentSyntaxTraversal.Declarations(document).Where(d => d.Type.Name.Text is "Pmi" or "pmi" && d.Name is null))
            foreach (var label in (pmi.Body?.Statements ?? []).OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text == "Label" && d.Name is not null))
            {
                var name = label.Name!.Text;
                var fields = (label.Body?.Statements ?? []).OfType<FieldSyntax>()
                    .ToDictionary(f => f.Name.Text, f => source.Substring(f.Value.Span.Start, f.Value.Span.Length).Trim().TrimEnd(';').Trim(), StringComparer.OrdinalIgnoreCase);
                string? Field(string key) => fields.TryGetValue(key, out var v) ? v.Trim('"') : null;
                foreach (var unknown in fields.Keys.Where(k => k is not ("record" or "view" or "side" or "at")))
                    diagnostics.Add($"{Invalid}:{name}: '{unknown}' is not a Label field (record, view, side, at)");
                var record = Field("record");
                if (string.IsNullOrWhiteSpace(record)) { diagnostics.Add($"{Invalid}:{name}: record names the PMI record to place (\"#38\" or a record name)"); continue; }
                var view = Field("view");
                if (view is not null && !Views.ContainsKey(view)) { diagnostics.Add($"{Invalid}:{name}: view is Top, Front or Right"); view = null; }
                var side = Field("side");
                if (side is not null && !Enum.TryParse<DrawingBalloonSide>(side, true, out _)) { diagnostics.Add($"{Invalid}:{name}: side is Left, Right, Above or Below"); side = null; }
                (double, double)? at = null;
                if (Field("at") is { } raw)
                {
                    var m = AtPattern().Match(raw);
                    if (m.Success) at = (double.Parse(m.Groups["x"].Value, CultureInfo.InvariantCulture), double.Parse(m.Groups["y"].Value, CultureInfo.InvariantCulture));
                    else diagnostics.Add($"{Invalid}:{name}: at is [x mm, y mm] from the sheet's top-left corner");
                }
                if (view is null && side is null && at is null) diagnostics.Add($"{Invalid}:{name}: a Label sets view, side or at");
                result.Add(new(name, record, view, side, at));
            }
        return result;
    }

    [GeneratedRegex(@"^\[\s*(?<x>-?\d+(?:\.\d+)?)\s*mm\s*,\s*(?<y>-?\d+(?:\.\d+)?)\s*mm\s*\]$")]
    private static partial Regex AtPattern();
}
