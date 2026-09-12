using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>
/// Bounded, pure expansion for Firmament's function-like Feature declarations.
/// Feature bodies are deliberately expression-only: immutable local derivations and one
/// terminal return. Expansion produces ordinary semantic declarations before Feature AIR.
/// </summary>
internal static class FirmamentV2FeatureExpansion
{
    internal const string Prefix = "firmament-feature-";
    internal const string DuplicateName = Prefix + "duplicate-name";
    internal const string Unknown = Prefix + "unknown";
    internal const string DuplicateParameter = Prefix + "duplicate-parameter";
    internal const string InvalidParameter = Prefix + "invalid-parameter";
    internal const string ArgumentCount = Prefix + "argument-count";
    internal const string UnknownArgument = Prefix + "unknown-argument";
    internal const string DuplicateArgument = Prefix + "duplicate-argument";
    internal const string ArgumentType = Prefix + "argument-type";
    internal const string MissingReturn = Prefix + "missing-return";
    internal const string MultipleReturns = Prefix + "multiple-returns";
    internal const string ReturnType = Prefix + "return-type";
    internal const string ControlFlowUnsupported = Prefix + "control-flow-unsupported";
    internal const string Recursion = Prefix + "recursion";
    internal const string LocalType = Prefix + "local-type";
    internal const string LocalDuplicate = Prefix + "local-duplicate";
    internal const string UnsupportedBody = Prefix + "body-unsupported";

    private sealed record Parameter(string Name, string Type, string? DefaultExpression, FirmamentV2SourceSpan Span);
    private sealed record Declaration(string Name, ImmutableArray<Parameter> Parameters, string ReturnTypeName,
        string ReturnExpression, ImmutableArray<Local> Locals, FirmamentV2SourceSpan Span);
    private sealed record Local(string Name, string Type, string Expression);

    internal sealed record Result(
        string Source,
        IReadOnlyList<FirmamentV2FeatureDefinition> Definitions,
        IReadOnlyList<FirmamentV2FeatureInvocation> Invocations,
        FirmamentV2FeatureExpansionMetrics Metrics);

    public static Result? Expand(string source, List<string> diagnostics)
    {
        var declarations = ParseDeclarations(source, diagnostics);
        var byName = declarations.ToDictionary(item => item.Name, StringComparer.Ordinal);
        DetectCycles(declarations, byName, diagnostics);
        if (declarations.Length > 0)
        {
            foreach (Match possibleCall in Regex.Matches(source, @"(?m)^\s*(?<name>[A-Za-z_]\w*)\s*\(", RegexOptions.CultureInvariant))
            {
                if (declarations.Any(item => possibleCall.Index >= item.Span.Start && possibleCall.Index < item.Span.Start + item.Span.Length)) continue;
                var name = possibleCall.Groups["name"].Value;
                if (!byName.ContainsKey(name)) diagnostics.Add(Unknown + ":" + name);
            }
        }
        if (HasErrors(diagnostics)) return null;

        var invocations = new List<FirmamentV2FeatureInvocation>();
        var ordinal = 0;
        string ExpandCall(Declaration declaration, string argumentText, FirmamentV2SourceSpan span,
            IReadOnlyList<string> stack, bool patternContext)
        {
            var bound = Bind(declaration, argumentText, span.Start, diagnostics);
            if (bound is null) return string.Empty;
            var invocationOrdinal = ordinal++;
            var environment = new Dictionary<string, string>(bound, StringComparer.Ordinal);
            foreach (var local in declaration.Locals)
            {
                if (environment.ContainsKey(local.Name))
                {
                    diagnostics.Add(LocalDuplicate + $":{declaration.Name}:{local.Name}");
                    return string.Empty;
                }
                var expression = Substitute(local.Expression, environment);
                if (!TypeMatches(expression, local.Type))
                {
                    diagnostics.Add(LocalType + $":{declaration.Name}:{local.Name}:expected-{local.Type}:actual-{DescribeType(expression)}");
                    return string.Empty;
                }
                environment[local.Name] = NormalizeExpression(expression, local.Type);
            }

            var returned = Substitute(declaration.ReturnExpression, environment).Trim();
            var nested = Regex.Match(returned, @"^(?<name>[A-Za-z_]\w*)\s*\((?<args>[\s\S]*)\)$", RegexOptions.CultureInvariant);
            string expanded;
            string expandedKind;
            if (nested.Success && byName.TryGetValue(nested.Groups["name"].Value, out var nestedDeclaration))
            {
                if (stack.Contains(nestedDeclaration.Name, StringComparer.Ordinal))
                {
                    diagnostics.Add(Recursion + ":" + string.Join(":", stack.Append(nestedDeclaration.Name)));
                    return string.Empty;
                }
                if (!SameType(declaration.ReturnTypeName, nestedDeclaration.ReturnTypeName))
                {
                    diagnostics.Add(ReturnType + $":{declaration.Name}:expected-{declaration.ReturnTypeName}:actual-{nestedDeclaration.ReturnTypeName}");
                    return string.Empty;
                }
                expanded = ExpandCall(nestedDeclaration, nested.Groups["args"].Value, span,
                    stack.Append(nestedDeclaration.Name).ToArray(), patternContext);
                expandedKind = nestedDeclaration.ReturnTypeName;
            }
            else
            {
                var constructor = Regex.Match(returned,
                    @"^(?<kind>Hole\s*<\s*(?:Shaft|Counterbore|Countersink)\s*>|Boss|Pocket|EdgeFinish)\s*\{",
                    RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
                if (!constructor.Success)
                {
                    diagnostics.Add(UnsupportedBody + ":" + declaration.Name);
                    return string.Empty;
                }
                var constructorOpen = returned.IndexOf('{', constructor.Index);
                var constructorClose = Matching(returned, constructorOpen, '{', '}');
                if (constructorClose < 0 || !string.IsNullOrWhiteSpace(returned[(constructorClose + 1)..]))
                {
                    diagnostics.Add(UnsupportedBody + ":" + declaration.Name + ":return-must-be-terminal");
                    return string.Empty;
                }
                expandedKind = Regex.Replace(constructor.Groups["kind"].Value, @"\s+", string.Empty, RegexOptions.CultureInvariant);
                if (!SameType(declaration.ReturnTypeName, expandedKind))
                {
                    diagnostics.Add(ReturnType + $":{declaration.Name}:expected-{declaration.ReturnTypeName}:actual-{expandedKind}");
                    return string.Empty;
                }
                var generatedName = patternContext
                    ? PatternItemName(returned, declaration.Name, invocationOrdinal)
                    : declaration.Name + "__" + invocationOrdinal.ToString(CultureInfo.InvariantCulture);
                expanded = returned.Insert(constructor.Index + constructor.Length - 1, " " + generatedName + " ");
            }

            invocations.Add(new(declaration.Name, invocationOrdinal, declaration.ReturnTypeName,
                bound, span, expandedKind, "ExpandedBeforeFeatureAir"));
            return expanded;
        }

        var changes = declarations.Select(item => (item.Span.Start, item.Span.Length, Text: string.Empty)).ToList();
        var callSites = declarations.SelectMany(declaration =>
        {
            var callRegex = new Regex($@"\b{Regex.Escape(declaration.Name)}\s*\(", RegexOptions.CultureInvariant);
            return callRegex.Matches(source).Cast<Match>()
                .Where(call => !declarations.Any(item => call.Index >= item.Span.Start && call.Index < item.Span.Start + item.Span.Length))
                .Select(call => (Declaration: declaration, Call: call));
        }).OrderBy(item => item.Call.Index).ToArray();
        foreach (var site in callSites)
        {
            var open = source.IndexOf('(', site.Call.Index); var close = Matching(source, open, '(', ')');
            if (close < 0) { diagnostics.Add(ArgumentCount + ":" + site.Declaration.Name); continue; }
            var span = new FirmamentV2SourceSpan(site.Call.Index, close - site.Call.Index + 1);
            var patternContext = IsInsideBlock(source, site.Call.Index, "Pattern");
            var expansion = ExpandCall(site.Declaration, source[(open + 1)..close], span, [site.Declaration.Name], patternContext);
            if (expansion.Length > 0) changes.Add((span.Start, span.Length, expansion));
        }
        if (HasErrors(diagnostics)) return null;
        foreach (var change in changes.OrderByDescending(item => item.Start))
            source = source.Remove(change.Start, change.Length).Insert(change.Start, change.Text);

        var definitions = declarations.Select(item => new FirmamentV2FeatureDefinition(item.Name,
            item.Parameters.Select(parameter => new FirmamentV2FeatureParameter(parameter.Name, parameter.Type, parameter.DefaultExpression)).ToArray(),
            item.ReturnTypeName, item.Span, "FileLocal", "PureTerminalReturn")).ToArray();
        var orderedInvocations = invocations.OrderBy(item => item.Ordinal).ToArray();
        return new(source, definitions, orderedInvocations,
            new(definitions.Length, invocations.Count, invocations.Count, "BoundedSourceOrder"));
    }

    private static ImmutableArray<Declaration> ParseDeclarations(string source, List<string> diagnostics)
    {
        var result = ImmutableArray.CreateBuilder<Declaration>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match header in Regex.Matches(source,
                     @"\bFeature\s+(?<name>[A-Za-z_]\w*)\s*\([^()]*\)\s*->\s*(?<return>[A-Za-z_]\w*(?:\s*<\s*[A-Za-z_]\w*\s*>)?)\s*\{",
                     RegexOptions.CultureInvariant))
        {
            var returnType = NormalizeType(header.Groups["return"].Value);
            if (!Regex.IsMatch(returnType, @"^(?:Hole<(?:Shaft|Counterbore|Countersink)>|Boss|Pocket|EdgeFinish)$", RegexOptions.CultureInvariant))
                diagnostics.Add(ReturnType + $":{header.Groups["name"].Value}:unsupported-{returnType}");
        }
        foreach (Match header in Regex.Matches(source,
                     @"\bFeature\s+(?<name>[A-Za-z_]\w*)\s*\((?<parameters>[^()]*)\)\s*->\s*(?<return>Hole\s*<\s*[A-Za-z_]\w*\s*>|Boss|Pocket|EdgeFinish)\s*\{",
                     RegexOptions.CultureInvariant))
        {
            var open = source.IndexOf('{', header.Index); var close = Matching(source, open, '{', '}');
            var name = header.Groups["name"].Value;
            if (close < 0) { diagnostics.Add(MissingReturn + ":" + name); continue; }
            if (!names.Add(name)) { diagnostics.Add(DuplicateName + ":" + name); continue; }
            var parameters = ParseParameters(header.Groups["parameters"].Value, header.Groups["parameters"].Index, name, diagnostics);
            var body = source[(open + 1)..close];
            foreach (Match keyword in Regex.Matches(body, @"\b(if|else|while|for|foreach|switch|match|goto|throw|try|catch|var)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                diagnostics.Add(ControlFlowUnsupported + ":" + keyword.Value.ToLowerInvariant());
            var returns = Regex.Matches(body, @"\breturn\b", RegexOptions.CultureInvariant);
            if (returns.Count == 0) { diagnostics.Add(MissingReturn + ":" + name); continue; }
            if (returns.Count != 1) { diagnostics.Add(MultipleReturns + ":" + name); continue; }
            var returnMatch = returns[0];
            var returnExpression = body[(returnMatch.Index + returnMatch.Length)..].Trim();
            if (returnExpression.EndsWith(';')) returnExpression = returnExpression[..^1].TrimEnd();
            var beforeReturn = body[..returnMatch.Index];
            var locals = ImmutableArray.CreateBuilder<Local>();
            var consumed = new List<(int Start, int Length)>();
            foreach (Match local in Regex.Matches(beforeReturn,
                         @"\blet\s+(?<name>[A-Za-z_]\w*)\s*:\s*(?<type>[A-Za-z_]\w*)\s*=\s*(?<expression>[^;\r\n]+)\s*;?",
                         RegexOptions.CultureInvariant))
            {
                locals.Add(new(local.Groups["name"].Value, local.Groups["type"].Value, local.Groups["expression"].Value.Trim()));
                consumed.Add((local.Index, local.Length));
            }
            var residue = beforeReturn;
            foreach (var item in consumed.OrderByDescending(item => item.Start)) residue = residue.Remove(item.Start, item.Length);
            if (!string.IsNullOrWhiteSpace(residue)) diagnostics.Add(UnsupportedBody + ":" + name);
            result.Add(new(name, parameters, NormalizeType(header.Groups["return"].Value),
                returnExpression, locals.ToImmutable(), new(header.Index, close - header.Index + 1)));
        }
        return result.ToImmutable();
    }

    private static ImmutableArray<Parameter> ParseParameters(string text, int offset, string feature, List<string> diagnostics)
    {
        var result = ImmutableArray.CreateBuilder<Parameter>(); var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (raw, start) in SplitTopLevel(text))
        {
            var match = Regex.Match(raw.Trim(), @"^(?<name>[A-Za-z_]\w*)\s*:\s*(?<type>[A-Za-z_]\w*)(?:\s*=\s*(?<default>.+))?$", RegexOptions.CultureInvariant);
            if (!match.Success) { diagnostics.Add(InvalidParameter + ":" + feature + ":" + raw.Trim()); continue; }
            var name = match.Groups["name"].Value;
            if (!names.Add(name)) { diagnostics.Add(DuplicateParameter + ":" + feature + ":" + name); continue; }
            result.Add(new(name, match.Groups["type"].Value,
                match.Groups["default"].Success ? match.Groups["default"].Value.Trim() : null,
                new(offset + start, raw.Length)));
        }
        return result.ToImmutable();
    }

    private static ImmutableDictionary<string, string>? Bind(Declaration declaration, string argumentText, int offset, List<string> diagnostics)
    {
        var supplied = new Dictionary<string, string>(StringComparer.Ordinal); var position = 0;
        foreach (var (raw, _) in SplitTopLevel(argumentText))
        {
            var colon = TopLevelColon(raw); string name; string expression;
            if (colon >= 0) { name = raw[..colon].Trim(); expression = raw[(colon + 1)..].Trim(); }
            else
            {
                if (position >= declaration.Parameters.Length) { diagnostics.Add(ArgumentCount + ":" + declaration.Name); continue; }
                name = declaration.Parameters[position++].Name; expression = raw.Trim();
            }
            if (!declaration.Parameters.Any(parameter => parameter.Name == name)) { diagnostics.Add(UnknownArgument + $":{declaration.Name}:{name}"); continue; }
            if (!supplied.TryAdd(name, expression)) diagnostics.Add(DuplicateArgument + $":{declaration.Name}:{name}");
        }
        foreach (var parameter in declaration.Parameters)
            if (!supplied.ContainsKey(parameter.Name))
            {
                if (parameter.DefaultExpression is null) diagnostics.Add(ArgumentCount + $":{declaration.Name}:missing-{parameter.Name}");
                else supplied[parameter.Name] = Substitute(parameter.DefaultExpression, supplied);
            }
        foreach (var parameter in declaration.Parameters)
            if (supplied.TryGetValue(parameter.Name, out var expression))
            {
                if (!TypeMatches(expression, parameter.Type))
                    diagnostics.Add(ArgumentType + $":{declaration.Name}:{parameter.Name}:expected-{parameter.Type}:actual-{DescribeType(expression)}:at-{offset}");
                else supplied[parameter.Name] = NormalizeExpression(expression, parameter.Type);
            }
        return HasErrors(diagnostics) ? null : supplied.ToImmutableDictionary(StringComparer.Ordinal);
    }

    private static void DetectCycles(IEnumerable<Declaration> declarations, IReadOnlyDictionary<string, Declaration> byName, List<string> diagnostics)
    {
        foreach (var declaration in declarations)
        {
            var stack = new List<string>(); var complete = new HashSet<string>(StringComparer.Ordinal);
            bool Visit(string name)
            {
                var at = stack.IndexOf(name);
                if (at >= 0) { diagnostics.Add(Recursion + ":" + string.Join(":", stack.Skip(at).Append(name))); return true; }
                if (!complete.Add(name)) return false;
                stack.Add(name);
                foreach (var target in byName.Keys.Where(candidate => Regex.IsMatch(byName[name].ReturnExpression, $@"^\s*{Regex.Escape(candidate)}\s*\(", RegexOptions.CultureInvariant)))
                    if (Visit(target)) return true;
                stack.RemoveAt(stack.Count - 1); return false;
            }
            _ = Visit(declaration.Name);
        }
    }

    private static string Substitute(string expression, IReadOnlyDictionary<string, string> values)
    {
        foreach (var pair in values.OrderByDescending(item => item.Key.Length))
            expression = Regex.Replace(expression, $@"\b{Regex.Escape(pair.Key)}\b(?!\s*:)", _ => pair.Value, RegexOptions.CultureInvariant);
        return expression;
    }

    private static bool TypeMatches(string expression, string type)
    {
        expression = expression.Trim(); type = NormalizeType(type);
        if (type is "FaceId" or "EdgeId" or "Body") return false;
        if (type == "Length") return TryEvaluateScalar(expression, out _, out var unit) && unit == "mm";
        if (type == "Angle") return TryEvaluateScalar(expression, out _, out var unit) && unit == "deg";
        if (type == "Int") return TryEvaluateScalar(expression, out var integer, out var intUnit) && intUnit.Length == 0 && integer == Math.Truncate(integer);
        if (type == "Float") return TryEvaluateScalar(expression, out _, out var floatUnit) && floatUnit.Length == 0;
        if (type == "Bool") return bool.TryParse(expression, out _);
        if (type is "Point2" or "Point3")
        {
            var arity = type == "Point2" ? 2 : 3;
            var match = Regex.Match(expression, @"^(?:Point[23]\s*\()?\s*\[?(?<body>[^\]\)]+)\]?\s*\)?$", RegexOptions.CultureInvariant);
            return (match.Success && SplitTopLevel(match.Groups["body"].Value).Count == arity
                    && SplitTopLevel(match.Groups["body"].Value).All(item => TypeMatches(item.Text, "Length")))
                || Regex.IsMatch(expression, @"^[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)?(?:\[\d+\])?$", RegexOptions.CultureInvariant);
        }
        // Semantic references and Concept-bound values retain their typed spelling for the owning frontend.
        return Regex.IsMatch(expression, @"^[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)?(?:\[\d+\])?$", RegexOptions.CultureInvariant);
    }

    private static string NormalizeExpression(string expression, string type)
    {
        if (NormalizeType(type) is "Length" or "Angle" && TryEvaluateScalar(expression, out var value, out var unit))
            return value.ToString("R", CultureInfo.InvariantCulture) + unit;
        if (NormalizeType(type) is "Int" or "Float" && TryEvaluateScalar(expression, out value, out unit) && unit.Length == 0)
            return value.ToString("R", CultureInfo.InvariantCulture);
        if (NormalizeType(type) is "Point2" or "Point3")
        {
            var match = Regex.Match(expression.Trim(), @"^(?<prefix>Point[23]\s*\()?\s*(?<open>\[)?(?<body>[^\]\)]+)(?:\])?\s*(?:\))?$", RegexOptions.CultureInvariant);
            if (match.Success)
            {
                var values = SplitTopLevel(match.Groups["body"].Value)
                    .Select(item => TryEvaluateScalar(item.Text, out var component, out var componentUnit)
                        ? component.ToString("R", CultureInfo.InvariantCulture) + componentUnit
                        : item.Text.Trim()).ToArray();
                if (match.Groups["prefix"].Success) return NormalizeType(type) + "(" + string.Join(", ", values) + ")";
                if (match.Groups["open"].Success) return "[" + string.Join(", ", values) + "]";
            }
        }
        return expression.Trim();
    }

    private static bool TryEvaluateScalar(string expression, out double value, out string unit)
    {
        var parser = new ScalarParser(expression); return parser.TryParse(out value, out unit);
    }

    private sealed class ScalarParser(string text)
    {
        private int _at;
        public bool TryParse(out double value, out string unit)
        {
            try { var result = Add(); White(); if (_at != text.Length || !double.IsFinite(result.Value)) throw new FormatException(); value = result.Value; unit = result.Unit; return true; }
            catch { value = 0; unit = string.Empty; return false; }
        }
        private (double Value, string Unit) Add()
        {
            var left = Multiply();
            while (true) { White(); if (!Take('+') && !Take('-')) return left; var op = text[_at - 1]; var right = Multiply(); if (left.Unit != right.Unit) throw new FormatException(); left = (op == '+' ? left.Value + right.Value : left.Value - right.Value, left.Unit); }
        }
        private (double Value, string Unit) Multiply()
        {
            var left = Atom();
            while (true)
            {
                White(); if (!Take('*') && !Take('/')) return left; var op = text[_at - 1]; var right = Atom();
                if (op == '*') { if (left.Unit.Length > 0 && right.Unit.Length > 0) throw new FormatException(); left = (left.Value * right.Value, left.Unit.Length > 0 ? left.Unit : right.Unit); }
                else { if (right.Value == 0 || right.Unit.Length > 0) throw new FormatException(); left = (left.Value / right.Value, left.Unit); }
            }
        }
        private (double Value, string Unit) Atom()
        {
            White(); if (Take('(')) { var value = Add(); White(); if (!Take(')')) throw new FormatException(); return value; }
            var start = _at; if (_at < text.Length && (text[_at] == '+' || text[_at] == '-')) _at++;
            while (_at < text.Length && (char.IsDigit(text[_at]) || text[_at] == '.')) _at++;
            if (start == _at || !double.TryParse(text[start.._at], NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) throw new FormatException();
            var unit = text.AsSpan(_at).StartsWith("mm") ? "mm" : text.AsSpan(_at).StartsWith("deg") ? "deg" : string.Empty; _at += unit.Length; return (number, unit);
        }
        private void White() { while (_at < text.Length && char.IsWhiteSpace(text[_at])) _at++; }
        private bool Take(char token) { if (_at >= text.Length || text[_at] != token) return false; _at++; return true; }
    }

    private static string DescribeType(string expression)
    {
        foreach (var type in new[] { "Length", "Angle", "Int", "Float", "Bool", "Point2", "Point3" }) if (TypeMatches(expression, type)) return type;
        return "Unknown";
    }
    private static string NormalizeType(string type) => Regex.Replace(type, @"\s+", string.Empty, RegexOptions.CultureInvariant);
    private static bool SameType(string left, string right) => string.Equals(NormalizeType(left), NormalizeType(right), StringComparison.OrdinalIgnoreCase);
    private static bool HasErrors(IEnumerable<string> diagnostics) => diagnostics.Any(item => item.StartsWith(Prefix, StringComparison.Ordinal));
    private static string PatternItemName(string returned, string feature, int ordinal)
    {
        var center = Regex.Match(returned, @"\bCenter\s*:\s*(?<item>[A-Za-z_]\w*)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        return center.Success ? center.Groups["item"].Value : feature + "__" + ordinal.ToString(CultureInfo.InvariantCulture);
    }
    private static int TopLevelColon(string text)
    {
        var round = 0; var square = 0; var curly = 0;
        for (var i = 0; i < text.Length; i++) { switch (text[i]) { case '(': round++; break; case ')': round--; break; case '[': square++; break; case ']': square--; break; case '{': curly++; break; case '}': curly--; break; case ':' when round == 0 && square == 0 && curly == 0: return i; } }
        return -1;
    }
    private static List<(string Text, int Start)> SplitTopLevel(string text)
    {
        var result = new List<(string, int)>(); var start = 0; var round = 0; var square = 0; var curly = 0;
        for (var i = 0; i < text.Length; i++)
        {
            switch (text[i]) { case '(': round++; break; case ')': round--; break; case '[': square++; break; case ']': square--; break; case '{': curly++; break; case '}': curly--; break; case ',' when round == 0 && square == 0 && curly == 0: result.Add((text[start..i], start)); start = i + 1; break; }
        }
        if (!string.IsNullOrWhiteSpace(text[start..])) result.Add((text[start..], start));
        return result;
    }
    private static int Matching(string source, int open, char begin, char end) { var depth = 0; for (var i = open; i < source.Length; i++) { if (source[i] == begin) depth++; else if (source[i] == end && --depth == 0) return i; } return -1; }
    private static bool IsInsideBlock(string source, int index, string kind)
    {
        foreach (Match header in Regex.Matches(source, $@"\b{Regex.Escape(kind)}\s+[A-Za-z_]\w*\s*\{{", RegexOptions.CultureInvariant))
        { var open = source.IndexOf('{', header.Index); var close = Matching(source, open, '{', '}'); if (header.Index < index && index < close) return true; }
        return false;
    }
}
