using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament;

/// <summary>Program-level selection of an existing Firmament frontend.  This is deliberately
/// not a second parser or semantic IR: it only makes the current parser boundary explicit.</summary>
public enum FirmamentFrontendSchema { Mechanical, WireForm, Sweep, SectionChain }

public sealed record FirmamentFrontendSelection(FirmamentFrontendSchema? Schema, string Source, IReadOnlyList<string> Diagnostics)
{
    public bool IsExplicit => Schema is not null;
    public bool IsSuccess => Diagnostics.Count == 0;
}

public static class FirmamentFrontendSchemas
{
    private static readonly Regex Declaration = new(@"(?m)^\s*schema\s+(?<name>[A-Za-z_]\w*)\s*;?\s*$", RegexOptions.CultureInvariant);
    public static readonly IReadOnlyList<string> Names = ["Mechanical", "WireForm", "Sweep", "SectionChain"];

    public static FirmamentFrontendSelection Select(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var declarations = Declaration.Matches(source).Cast<Match>().ToArray();
        if (declarations.Length == 0) return new(null, source, []);
        if (declarations.Length != 1) return new(null, source, ["firmament-schema-duplicate"]);
        var declaration = declarations[0];
        var prefix = source[..declaration.Index];
        if (prefix.Split(['\r', '\n']).Any(line => { var text = line.Trim(); return text.Length > 0 && !text.StartsWith("//", StringComparison.Ordinal) && !text.StartsWith('#'); }))
            return new(null, source, ["firmament-schema-late"]);
        var name = declaration.Groups["name"].Value;
        if (!Enum.TryParse<FirmamentFrontendSchema>(name, false, out var schema))
            return new(null, source, [$"firmament-schema-unknown:{name}:available={string.Join(',', Names)}"]);
        var normalized = source.Remove(declaration.Index, declaration.Length);
        var detected = DetectDistinctFrontend(normalized);
        if (detected is not null && detected != schema)
            return new(schema, normalized, [$"firmament-schema-mismatch:declared={schema}:source={detected}"]);
        return new(schema, normalized, []);
    }

    private static FirmamentFrontendSchema? DetectDistinctFrontend(string source) =>
        WireFormAuthoring.IsWireFormSource(source) ? FirmamentFrontendSchema.WireForm :
        CircularSweepAuthoring.IsSweepSource(source) ? FirmamentFrontendSchema.Sweep :
        SectionChainAuthoringParser.IsSectionChainSource(source) ? FirmamentFrontendSchema.SectionChain : null;
}
