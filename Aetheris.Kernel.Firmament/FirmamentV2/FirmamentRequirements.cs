using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>The outcome of one <c>Require</c>: passed, failed at a named member, or not a valid Boolean requirement.</summary>
public sealed record FirmamentRequirementResult(string Name, bool Passed, bool Valid, string Expression, string Message);

/// <summary>
/// Applies <c>Require name => Concept(...)</c> declarations to values a tool has measured, with the source's own predicate
/// and bundle Concepts. Each measured name in the requirement (such as <c>wall.minimum</c>) is replaced by its value as a
/// quantity before the ordinary Concept checker evaluates it, so a requirement means exactly what it would mean over
/// Static values.
/// </summary>
public static class FirmamentRequirements
{
    public static IReadOnlyList<FirmamentRequirementResult> Check(string source, IReadOnlyList<(string Name, string Expression)> requires, IReadOnlyDictionary<string, string> values)
    {
        var results = new List<FirmamentRequirementResult>();
        var concepts = new FirmamentConcepts(source);
        var definitionErrors = concepts.Diagnostics.Select(d => d.Message).ToArray();
        foreach (var (name, expression) in requires)
        {
            var bound = values.OrderByDescending(v => v.Key.Length).Aggregate(expression, (text, value) =>
                Regex.Replace(text, $@"(?<![\w.]){Regex.Escape(value.Key)}(?![\w])", $"({value.Value})"));
            var parsed = FirmamentSyntaxParser.ParseExpression(bound);
            if (parsed.Expression is null)
            {
                results.Add(new(name, false, false, expression, $"Require {name}: '{expression}' is not an expression."));
                continue;
            }
            var before = concepts.Diagnostics.Count;
            var passed = concepts.Check(parsed.Expression, name);
            var message = passed ? $"Require {name} holds." : (concepts.Diagnostics.Skip(before).LastOrDefault()?.Message ?? $"Require {name} failed.").Replace("; no geometry was admitted", "", StringComparison.Ordinal);
            var valid = passed || concepts.Diagnostics.Skip(before).LastOrDefault()?.Code == "firmament-concept-requirement-failed";
            results.Add(new(name, passed, valid, expression, message));
        }
        if (definitionErrors.Length > 0)
            results.Insert(0, new("<concepts>", false, false, "", string.Join(" ", definitionErrors)));
        return results;
    }
}
