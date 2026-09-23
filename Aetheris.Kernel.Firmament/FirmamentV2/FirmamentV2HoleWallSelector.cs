using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>The single canonical source spelling for a named Hole's wall output.</summary>
public sealed record FirmamentV2HoleWallSelector(string HoleName, string BodyName)
{
    private static readonly Regex Syntax = new(@"^face\((?<hole>[A-Za-z_][A-Za-z0-9_]*)\.Wall\)$", RegexOptions.CultureInvariant);

    public static bool TryParse(string text, out string holeName)
    {
        var match = Syntax.Match(text);
        holeName = match.Success ? match.Groups["hole"].Value : string.Empty;
        return match.Success;
    }

    public static string Format(string holeName) => $"face({holeName}.Wall)";

    public static string? Format(SemanticTopologyDescendant descendant, string holeName, string featureId) =>
        descendant.Role == SemanticTopologyRole.HoleWallFace
        && descendant.Face.HasValue
        && descendant.SourceStableId == $"hole:{featureId}"
        && descendant.Addressability is SemanticTopologyAddressability.AuthoredStable or SemanticTopologyAddressability.DerivedStable
            ? Format(holeName) : null;
}
