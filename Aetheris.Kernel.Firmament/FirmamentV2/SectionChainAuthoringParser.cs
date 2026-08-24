using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Surfacing;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record SectionChainAuthoringResult(
    bool IsSuccess,
    SectionChain? Chain,
    SectionChainMaterializationResult? Materialization,
    IReadOnlyList<string> Diagnostics);

/// <summary>
/// Domain binder for Firmament SectionChain declarations. Profiles remain ordinary
/// Concept Path-derived Profiles; this layer only associates them with ordered frames,
/// seams, correspondence, transition law, and termination intent.
/// </summary>
public static class SectionChainAuthoringParser
{
    public static bool IsSectionChainSource(string source) =>
        Regex.IsMatch(source, @"\bSectionChain\s+[A-Za-z_]\w*\s*\{", RegexOptions.CultureInvariant);

    public static SectionChainAuthoringResult Compile(string source, bool materialize = true)
    {
        ArgumentNullException.ThrowIfNull(source);
        var diagnostics = new List<string>();
        if (!Regex.IsMatch(source, @"\bUnits\s*:\s*mm\b", RegexOptions.CultureInvariant))
            diagnostics.Add("section-chain-units-invalid:millimetres-required");
        var declarations = Blocks(source, "SectionChain").ToArray();
        if (declarations.Length != 1)
            return Fail(diagnostics.Append($"section-chain-count-invalid:expected=1:actual={declarations.Length}"));

        var declaration = declarations[0];
        var profiles = ProfileAuthoringParser.BindPathDerivedProfiles(source, diagnostics);
        var transitionText = Field(declaration.Body, "Transition") ?? "Ruled";
        if (!Enum.TryParse<SectionTransitionPolicy>(transitionText, false, out var transition))
            diagnostics.Add($"section-chain-transition-policy-invalid:{transitionText}");
        var startText = Field(declaration.Body, "Start") ?? "Open";
        var endText = Field(declaration.Body, "End") ?? "Open";
        if (!Enum.TryParse<SectionTermination>(startText, false, out var start))
            diagnostics.Add($"section-chain-termination-invalid:Start:{startText}");
        if (!Enum.TryParse<SectionTermination>(endText, false, out var end))
            diagnostics.Add($"section-chain-termination-invalid:End:{endText}");

        var sections = new List<Section>();
        foreach (var authored in Blocks(declaration.Body, "Section"))
        {
            var frameName = Field(authored.Body, "Frame");
            var profileName = Field(authored.Body, "Profile");
            var seam = Field(authored.Body, "Seam");
            if (frameName is null) diagnostics.Add($"section-chain-frame-missing:{authored.Name}");
            ResolvedProfile2D? profile = null;
            if (profileName is null || !profiles.TryGetValue(profileName ?? string.Empty, out profile))
                diagnostics.Add($"section-chain-profile-unresolved:{authored.Name}:{profileName ?? "<missing>"}");
            var plane = frameName is null ? null : ProfileAuthoringParser.ResolveNamedConstructionPlane(source, frameName, diagnostics);
            if (frameName is not null && plane is null) diagnostics.Add($"section-chain-frame-unresolved:{authored.Name}:{frameName}");
            if (plane is null || profile is null) continue;
            var outer = profile.Loops.Single(loop => loop.IsOuter);
            if (profile.Loops.Count != 1) diagnostics.Add($"section-chain-profile-loop-count-invalid:{authored.Name}");
            var spans = outer.Segments.Select(segment => Convert(segment, diagnostics, authored.Name)).Where(item => item is not null).Cast<SectionProfileSpan>().ToArray();
            var seamId = seam ?? spans.FirstOrDefault()?.SpanId ?? string.Empty;
            sections.Add(new(authored.Name, new(plane.Origin, plane.AxisX, plane.AxisY, plane.AxisZ), new(profile.Name, spans, seamId)));
        }
        if (sections.Count < 2) diagnostics.Add("section-chain-section-count-invalid:minimum=2");

        var correspondence = new List<AdjacentSectionCorrespondence>();
        foreach (var authored in Blocks(declaration.Body, "Correspond"))
        {
            var from = Field(authored.Body, "From"); var to = Field(authored.Body, "To");
            if (from is null || to is null) { diagnostics.Add($"section-chain-correspondence-endpoint-missing:{authored.Name}"); continue; }
            var mappings = Regex.Matches(authored.Body, @"(?m)^\s*(?<a>[A-Za-z_]\w*)\s*->\s*(?<b>[A-Za-z_]\w*)\s*;?\s*$", RegexOptions.CultureInvariant)
                .Cast<Match>().Select(match => new SectionSpanCorrespondence(match.Groups["a"].Value, match.Groups["b"].Value)).ToArray();
            foreach (var duplicate in mappings.GroupBy(item => item.TargetSpanId, StringComparer.Ordinal).Where(group => group.Count() > 1))
                diagnostics.Add($"section-chain-correspondence-duplicate:{from}:{to}:{duplicate.Key}");
            correspondence.Add(new(from, to, mappings));
        }

        if (diagnostics.Count != 0) return Fail(diagnostics);
        var chain = new SectionChain(declaration.Name, sections, correspondence, transition, start, end);
        if (!materialize) return new(true, chain, null, []);
        var result = SectionChainMaterializer.Materialize(chain);
        if (!result.IsSuccess)
            diagnostics.AddRange(result.Diagnostics.Select(item => $"{item.Code}:{item.Message}"));
        return new(result.IsSuccess, chain, result, diagnostics);

        SectionChainAuthoringResult Fail(IEnumerable<string> items) => new(false, null, null, items.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static SectionProfileSpan? Convert(ResolvedProfileSegment2D segment, List<string> diagnostics, string section) => segment.Geometry switch
    {
        LineArcLineSegment2D line => new(segment.Name, new SectionProfileCurve.Line(new(line.Start.X, line.Start.Y), new(line.End.X, line.End.Y))),
        LineArcCircularArc2D arc => new(segment.Name, new SectionProfileCurve.Arc(new(arc.Center.X, arc.Center.Y), arc.Radius, arc.StartAngleRadians, arc.SweepAngleRadians)),
        _ => Unsupported(segment, diagnostics, section)
    };

    private static IEnumerable<Block> Blocks(string source, string keyword)
    {
        var regex = new Regex($@"\b{Regex.Escape(keyword)}(?:\s+(?<name>[A-Za-z_]\w*))?\s*\{{", RegexOptions.CultureInvariant);
        for (var offset = 0; offset < source.Length;)
        {
            var match = regex.Match(source, offset); if (!match.Success) yield break;
            var open = source.IndexOf('{', match.Index + match.Length - 1); var close = MatchingBrace(source, open);
            if (close < 0) yield break;
            yield return new(match.Groups["name"].Success ? match.Groups["name"].Value : keyword, source[(open + 1)..close]);
            offset = close + 1;
        }
    }

    private static int MatchingBrace(string source, int open)
    {
        var depth = 0; var quoted = false;
        for (var index = open; index < source.Length; index++)
        {
            if (source[index] == '"' && (index == 0 || source[index - 1] != '\\')) quoted = !quoted;
            if (quoted) continue;
            if (source[index] == '{') depth++;
            else if (source[index] == '}' && --depth == 0) return index;
        }
        return -1;
    }

    private static string? Field(string body, string name)
    {
        var match = Regex.Match(body, $@"(?m)^\s*{Regex.Escape(name)}\s*:\s*(?<value>[A-Za-z_]\w*)\s*;?\s*$", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static SectionProfileSpan? Unsupported(ResolvedProfileSegment2D segment, List<string> diagnostics, string section)
    {
        diagnostics.Add($"section-chain-profile-curve-unsupported:{section}:{segment.Name}:{segment.Geometry.GetType().Name}");
        return null;
    }

    private sealed record Block(string Name, string Body);
}
