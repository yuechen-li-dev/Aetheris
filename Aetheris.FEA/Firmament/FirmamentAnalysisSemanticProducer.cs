using System.Text.RegularExpressions;
using Aetheris.FEA.Analysis;
using Aetheris.Semantics;

namespace Aetheris.FEA.Firmament;

internal static partial class FirmamentAnalysisSemanticProducer
{
    [GeneratedRegex(@"^(?<body>[A-Za-z_][A-Za-z0-9_]*)\.face\((?<side>[+-][XYZ]|\#?[0-9]+)\)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex FacePath();

    public static (SemanticValue? Value, SemanticDiagnostic? Diagnostic) Bind(
        string authoredPath,
        string expectedBody,
        IReadOnlyDictionary<string, string> exactFaceIds,
        SemanticSourceSpan sourceSpan,
        string origin)
    {
        var match = FacePath().Match(authoredPath);
        if (!match.Success || !string.Equals(match.Groups["body"].Value, expectedBody, StringComparison.Ordinal))
            return (null, new(SemanticValueValidator.PathMemberMissing,
                $"Boundary region '{authoredPath}' is not an exposed face of semantic body '{expectedBody}'.", sourceSpan));
        var side = match.Groups["side"].Value.ToUpperInvariant();
        var token = side switch { "-X" => "x-min", "+X" => "x-max", "-Y" => "y-min", "+Y" => "y-max", "-Z" => "z-min", "+Z" => "z-max", _ => side };
        exactFaceIds.TryGetValue(token, out var faceId);
        if (origin == "InlineStep" && (token.StartsWith('#') || char.IsDigit(token[0])) && faceId is null)
        {
            var available = exactFaceIds.Keys
                .Where(key => key.StartsWith('#'))
                .OrderBy(key => int.TryParse(key.AsSpan(1), out var entityId) ? entityId : int.MaxValue)
                .Take(12)
                .Select(key => $"{expectedBody}.face({key})")
                .ToArray();
            var suffix = available.Length == 0
                ? " The imported STEP artifact exposes no selectable ADVANCED_FACE identities."
                : $" Available selectors include {string.Join(", ", available)}.";
            return (null, new("firmament-analysis-inline-step-face-missing",
                $"Imported boundary selector '{authoredPath}' does not match an ADVANCED_FACE in the STEP artifact.{suffix}", sourceSpan));
        }
        var stableId = $"analysis-region:{origin}:{expectedBody}:face({side})" + (faceId is null ? string.Empty : ":" + faceId);
        return (new SemanticValue(stableId, new("BoundaryRegion"),
            [new BoundaryRegionCapability(), new AnalysisRegionCapability(), new ExactGeometryCapability()],
            [new ExactAnalysisRegionBinding(expectedBody, authoredPath, faceId)],
            provenance:
            [
                new(origin, expectedBody, faceId is null ? "exact analytic boundary" : "exact imported BRep face", sourceSpan),
                new("semantic-path", authoredPath, "structurally bound face selector", sourceSpan),
            ],
            authoredSourceSpan: sourceSpan,
            exposedName: $"face({side})"), null);
    }

    public static (SemanticRegionBinding? Region, AnalysisDiagnostic? Diagnostic) Normalize(
        string authoredPath,
        string body,
        IReadOnlyDictionary<string, string> exactFaceIds,
        AnalysisProvenance consumerProvenance,
        string origin)
    {
        var span = new SemanticSourceSpan(consumerProvenance.Source, consumerProvenance.Start, consumerProvenance.Length);
        var (value, bindDiagnostic) = Bind(authoredPath, body, exactFaceIds, span, origin);
        if (bindDiagnostic is not null)
            return (null, new(bindDiagnostic.Code, AnalysisDiagnosticSeverity.Error, bindDiagnostic.Message, consumerProvenance));
        var (region, normalizeDiagnostic) = AnalysisSemanticRegionNormalizer.Normalize(new(value!, [new(authoredPath, span)], span));
        return normalizeDiagnostic is null
            ? (region, null)
            : (null, new(normalizeDiagnostic.Code, AnalysisDiagnosticSeverity.Error, normalizeDiagnostic.Message, consumerProvenance));
    }
}
