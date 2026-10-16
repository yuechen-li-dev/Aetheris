namespace Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

/// <summary>Shared recursive descent over the editor's lexer. No expansion, name
/// resolution, casing rewrite, engineering admission or geometry is performed.</summary>
public static class FirmamentSyntaxParser
{
    public static SyntaxExpressionParseResult ParseExpression(string source) => new Parser(source, "inline").ParseExpression();
    public static SyntaxParseResult Parse(string source, string sourceFile = "inline")
    {
        ArgumentNullException.ThrowIfNull(source);
        return new Parser(source, sourceFile).Parse();
    }

    private sealed class ParseFailure : Exception;
    private sealed class Parser
    {
        private readonly string source;
        private readonly string file;
        private readonly SyntaxToken[] all;
        private readonly SyntaxToken[] tokens;
        private readonly List<FirmamentV2ValidationDiagnostic> diagnostics = [];
        private readonly Stack<string> contexts = [];
        private int at;
        private int depth;
        private const int MaxDepth = 128;
        private SyntaxToken Current => tokens[at];
        private SyntaxToken Previous => tokens[Math.Max(0, at - 1)];
        private bool End => Current.Kind == SyntaxTokenKind.End;
        private bool LineBreak => at > 0 && source.AsSpan(Previous.Span.Start + Previous.Span.Length,
            Current.Span.Start - Previous.Span.Start - Previous.Span.Length).ContainsAny('\r', '\n');

        public Parser(string source, string file)
        {
            this.source = source;
            this.file = file;
            var lexemes = FirmamentLanguageAnalysisService.Lex(source, lossless: true);
            all = lexemes.Select(t => new SyntaxToken(t.Shape switch
            {
                "identifier" => SyntaxTokenKind.Identifier, "number" => SyntaxTokenKind.Number,
                "string" => SyntaxTokenKind.String, "whitespace" => SyntaxTokenKind.Whitespace,
                "comment" => SyntaxTokenKind.Comment, "end" => SyntaxTokenKind.End,
                _ => SyntaxTokenKind.Punctuation
            }, t.Text, new(t.Start, t.Length))).ToArray();
            tokens = all.Where(t => t.Kind is not (SyntaxTokenKind.Whitespace or SyntaxTokenKind.Comment)).ToArray();
            foreach (var t in lexemes.Where(t => t.Error is not null))
                Error("firmament-syntax-unterminated-token", t.Error!, new(t.Start, t.Length));
        }

        public SyntaxParseResult Parse() => new(new(file, Statements(null), all, new(0, source.Length)), diagnostics.ToArray());

        public SyntaxExpressionParseResult ParseExpression()
        {
            ExpressionSyntax? expression = null;
            try
            {
                expression = Expression();
                if (!End) throw Fail("firmament-syntax-expression", $"Unexpected '{Current.Text}' after expression.");
            }
            catch (ParseFailure) { expression = null; }
            return new(expression, diagnostics.ToArray());
        }

        private IReadOnlyList<StatementSyntax> Statements(string? close)
        {
            var result = new List<StatementSyntax>();
            while (!End && (close is null || !Is(close)))
            {
                if (Take(";")) continue;
                var before = at;
                try { result.Add(Statement()); }
                catch (ParseFailure) { Recover(before, close); }
                if (at == before && !End) at++; // recovery must always advance
            }
            return result.ToArray();
        }

        private StatementSyntax Statement()
        {
            Enter();
            try { return StatementCore(); }
            finally { depth--; }
        }

        private StatementSyntax StatementCore()
        {
            var start = Current.Span.Start;
            RejectGeneralControlFlow();
            if (contexts.TryPeek(out var context) && context.StartsWith("Interface ", StringComparison.Ordinal)
                && (Current.Text is "Role" or "Lower" or "Fit" or "Allow" or "ClearancePolicy" or "Variation"
                    || Is("Mate") && Peek(2).Text != ":")) return InterfaceClause();
            if (Take("replace"))
            {
                var target = Expression(allowObject: false); Expect("with");
                return new ReplacementSyntax(target, Declaration(occurrence: false), Span(start));
            }
            if (Is("<") && Peek(1).Kind == SyntaxTokenKind.Identifier) return Declaration(occurrence: true);
            if (Current.Kind == SyntaxTokenKind.Identifier)
            {
                if (Peek(1).Text == ":")
                {
                    var name = Identifier(); Expect(":");
                    var value = Expression();
                    if (Take("=")) value = new TypedValueSyntax(value, Expression(), Span(value.Span.Start));
                    return new FieldSyntax(name, value, Span(start));
                }
                if (Pair("=", ">", offset: 1))
                {
                    var key = Identifier(); Expect("="); Expect(">");
                    var valueStart = Current.Span.Start;
                    var value = Is("{") ? new StatementBlockSyntax(Block(context: "Pattern mapping"), Span(valueStart))
                        : Peek(1).Text == "with" ? new ExpressionStatementSyntax(Expression(), Span(valueStart))
                        : Current.Kind == SyntaxTokenKind.Identifier || Is("<") ? Statement()
                        : new ExpressionStatementSyntax(Expression(), Span(valueStart));
                    return new MappingSyntax(key, value, Span(start));
                }
                // The historical unit header has the same field meaning as units: mm.
                if (Is("units") && Peek(1).Kind == SyntaxTokenKind.Identifier)
                {
                    var name = Identifier();
                    return new FieldSyntax(name, Expression(allowObject: false), Span(start));
                }
                if (Is("return")) { at++; return new ReturnSyntax(Expression(), Span(start)); }
                if (Is("schema") || Is("include") || Is("Include") || Is("Use"))
                {
                    var kind = Current.Text switch { "schema" => DirectiveKind.Schema, "Use" => DirectiveKind.Use, _ => DirectiveKind.Include };
                    at++;
                    try { return new DirectiveSyntax(kind, Expression(allowObject: false), Span(start)); }
                    catch (ParseFailure)
                    {
                        Error(kind == DirectiveKind.Schema ? "firmament-schema-malformed" : "firmament-v2-directive-malformed",
                            $"Expected a valid value for the {kind.ToString().ToLowerInvariant()} directive.", Span(start));
                        throw;
                    }
                }
                // A call or a pipeline is an expression statement. A bare name in an
                // Enum/Loop is also syntax, never implicitly an admitted declaration.
                if (Is("Reverse") || Peek(1).Text == "-" && Peek(2).Text == ">" || Peek(1).Text is "(" or "." or "|" or "[" or "As" or "as"
                    || Peek(1).Text is "From" or "from" or "To" or "to" && Peek(2).Kind == SyntaxTokenKind.Identifier
                        && (Peek(3).Text is "." or "To" or "to" or "As" or "as" or "|" or ";" or "}" || Peek(3).Kind == SyntaxTokenKind.End)
                    || Peek(1).Text is ";" or "}" || Peek(1).Kind == SyntaxTokenKind.End)
                    {
                    var expression = Expression();
                    // Selector aliases historically used =>; retain it as the
                    // same alias node, with admission owned by expose.
                    if (Pair("=", ">")) { at += 2; expression = new AliasSyntax(expression, Identifier(), Span(start)); }
                    return new ExpressionStatementSyntax(expression, Span(start));
                }
                var declaration = Declaration(occurrence: false);
                if (declaration.Name is null && declaration.Body is null && declaration.Initializer is null
                    && declaration.Annotation is null && declaration.ResultType is null && declaration.Modifiers.Count == 0
                    && declaration.Parameters.Count == 0 && declaration.Clauses.Count == 0)
                {
                    ExpressionSyntax reference = new ReferenceSyntax(declaration.Type.Name, declaration.Type.Name.Span);
                    if (declaration.Type.Span.Length > declaration.Type.Name.Span.Length)
                        reference = new SpecializationSyntax(reference, declaration.Type.Arguments, declaration.Type.Span);
                    return new ExpressionStatementSyntax(reference, declaration.Span);
                }
                return declaration;
            }
            throw Fail("firmament-syntax-statement", $"Expected a declaration, field or expression; found '{Current.Text}'.");
        }

        private DeclarationSyntax Declaration(bool occurrence)
        {
            var start = Current.Span.Start;
            if (occurrence) Expect("<");
            var modifiers = new List<TypeSyntax>();
            var type = Type();
            // Prefixes are finite syntax roles, not a list of admitted engineering types.
            while ((type.Name.Text is "Template" or "Concept" or "Construction" or "Radial" or "Linear" or "Mirror" or "Mirrored" or "Semantic"
                && Current.Kind == SyntaxTokenKind.Identifier && (Peek(1).Kind == SyntaxTokenKind.Identifier || Peek(1).Text == "<"))
                || type.Name.Text == "Static" && Is("Table")
                || type.Name.Text == "Inventory" && Is("Record")
                || type.Name.Text is "Assert" or "Analysis" or "analysis" or "Construct" or "Extend" or "Comptime" or "proposes" or "manufacturing" && Current.Kind == SyntaxTokenKind.Identifier)
            {
                modifiers.Add(type); type = Type(qualified: type.Name.Text == "Construct");
            }
            IdentifierSyntax? name = null;
            // Lowercase from was already a legal authored name. It becomes a link
            // only after the declaration name, never a newly reserved identifier.
            if (Current.Kind == SyntaxTokenKind.Identifier && !LineBreak && (Current.Text == "from" || !IsClause(Current.Text))) name = Identifier();
            ExpressionSyntax? target = null;
            if (type.Name.Text is "CornerProfile" or "EdgeProfile" && name is not null)
            {
                target = new ReferenceSyntax(name, name.Span);
                while (Take(".")) target = new MemberSyntax(target, Identifier(), Span(name.Span.Start));
                name = null; // a target path, not a newly declared dotted identifier
            }
            // Drawing review identifiers historically allow hyphen-separated words.
            if (type.Name.Text is "Review" or "Issue" or "Proposal" or "Resolution" && name is not null)
                while (Take("-"))
                {
                    var token = Current;
                    if (token.Kind is not (SyntaxTokenKind.Identifier or SyntaxTokenKind.Number))
                        throw Fail("firmament-syntax-identifier", "Expected a review identifier segment after '-'.");
                    at++; var suffix = new IdentifierSyntax(token.Text, token.Span);
                    name = new(name.Text + "-" + suffix.Text, new(name.Span.Start, (suffix.Span.Start + suffix.Span.Length) - name.Span.Start));
                }
            // Historical `Model Name mm` is a unit-bearing header. Admit it in
            // this grammar and bind its implicit file body, rather than selecting
            // an EdgeFinish-specific whole-document parser.
            IdentifierSyntax? headerUnit = null;
            if (type.Name.Text is "Model" or "Struct" && name is not null && Current.Kind == SyntaxTokenKind.Identifier
                && !LineBreak && !IsClause(Current.Text)) headerUnit = Identifier();
            var hasParameterList = Take("(");
            var parameters = hasParameterList ? Arguments(")", declarationParameters: true) : [];
            if (type.Name.Text == "Replace" && Take("."))
            {
                var member = Identifier();
                name = new IdentifierSyntax(name!.Text + "." + member.Text, new(name.Span.Start, member.Span.Start + member.Span.Length - name.Span.Start));
            }
            ExpressionSyntax? annotation = null, result = null, initializer = null;
            if (Take(":")) annotation = Expression(allowObject: false, stopGreater: occurrence);
            if (Pair("-", ">")) { at += 2; result = Expression(allowObject: false); }
            var clauses = new List<ClauseSyntax>();
            while (TryClause(Current.Text, out var clause))
            {
                var clauseStart = Current.Span.Start; at++;
                if (clause == ClauseKind.Key) Take(":");
                clauses.Add(new(clause, Expression(allowObject: false), Span(clauseStart)));
            }
            if (Pair("=", ">")) { at += 2; initializer = Expression(stopGreater: occurrence); }
            else if (Take("=")) initializer = Expression(stopGreater: occurrence);
            if (type.Name.Text == "Replace" && Take("With"))
            {
                result = Expression(allowObject: false);
                if (Current.Kind == SyntaxTokenKind.Identifier) initializer = new ReferenceSyntax(Identifier(), Previous.Span);
            }
            BlockSyntax? body = null;
            if (type.Name.Text == "solid" && Take("with"))
            {
                body = Block(context: $"solid {name?.Text}");
                if (annotation is not null) annotation = new WithSyntax(annotation, body, Span(annotation.Span.Start));
            }
            if (occurrence)
            {
                Expect(">");
                var bodyStart = Previous.Span.Start + Previous.Span.Length;
                var statements = new List<StatementSyntax>();
                while (!End && !Pair("<", "/"))
                {
                    if (Take(";")) continue;
                    var before = at;
                    try { statements.Add(Statement()); }
                    catch (ParseFailure) { Recover(before, "<"); }
                    if (at == before && !End) at++;
                }
                var bodyEnd = Current.Span.Start;
                Expect("<"); Expect("/");
                var closeName = Identifier();
                if (closeName.Text != type.Name.Text) Error("firmament-syntax-tag-mismatch", $"Expected closing tag for {type.Name.Text}, found {closeName.Text}.", closeName.Span);
                Expect(">"); body = new(statements.ToArray(), new(bodyStart, bodyEnd - bodyStart));
            }
            else if (body is not null) { /* Compatibility with body already consumed. */ }
            else if (Is("{")) body = Block(type.Name.Text == "Enum", $"{type.Name.Text} {name?.Text}".TrimEnd());
            else if (headerUnit is not null && contexts.Count == 0)
            {
                var bodyStart = Current.Span.Start;
                body = new(Statements(null), new(bodyStart, source.Length - bodyStart)) { Delimited = false };
            }
            else if (initializer is null && !End && !LineBreak && !Is(";") && !Is("}")
                && !(type.Name.Text == "concept" && annotation is not null && Is("concept")))
                throw Fail("firmament-syntax-declaration-tail", $"Unexpected '{Current.Text}' after {type.Name.Text} {name?.Text}; expected a body, initializer or separator.");
            if (headerUnit is not null)
            {
                var unit = new ReferenceSyntax(headerUnit, headerUnit.Span);
                var field = new FieldSyntax(new("Units", headerUnit.Span), unit, headerUnit.Span);
                body = body is null ? new([field], headerUnit.Span) : body with { Statements = new StatementSyntax[] { field }.Concat(body.Statements).ToArray() };
            }
            return new(type, name, modifiers.ToArray(), parameters, annotation, result, clauses.ToArray(), initializer, body, occurrence, Span(start)) { Target = target, HasParameterList = hasParameterList };
        }

        private TypeSyntax Type(bool qualified = false)
        {
            var name = Identifier();
            if (qualified)
                while (Take("."))
                {
                    var segment = Identifier();
                    name = new(name.Text + "." + segment.Text, new(name.Span.Start, segment.Span.Start + segment.Span.Length - name.Span.Start));
                }
            var args = Take("<") ? Arguments(">") : [];
            return new(name, args, Span(name.Span.Start));
        }

        private StatementSyntax InterfaceClause()
        {
            var start = Current.Span.Start;
            var keyword = Identifier();
            StatementSyntax statement;
            if (keyword.Text == "Role")
            {
                var name = Identifier(); Expect("requires");
                var capabilities = new List<IdentifierSyntax> { Identifier() };
                while (Take(",")) capabilities.Add(Identifier());
                statement = new RoleRequirementSyntax(name, capabilities.ToArray(), Span(start));
            }
            else if (keyword.Text is "Lower" or "Mate")
            {
                var kind = Identifier(); var first = EndpointReference(); var second = EndpointReference();
                var option = Is(";") || Is("}") || End || LineBreak ? null : Expression(allowObject: false);
                statement = new InterfaceRelationSyntax(kind, first, second, option, Span(start));
            }
            else if (keyword.Text == "Fit")
            {
                var shaft = EndpointReference(); Expect("inside"); var bore = EndpointReference();
                var perSide = Take("per"); if (perSide) { Expect("-"); Expect("side"); }
                statement = new InterfaceFitSyntax(shaft, bore, perSide, Span(start));
            }
            else if (keyword.Text == "Allow")
            {
                var kind = Identifier(); Expect(":"); var axis = Identifier();
                while (Take("-")) { var tail = Identifier(); axis = new(axis.Text + "-" + tail.Text, Span(axis.Span.Start)); }
                statement = new InterfaceMotionSyntax(kind, axis, Span(start));
            }
            else
            {
                var values = new List<ArgumentSyntax>();
                while (!End && !Is(";") && !Is("}") && !LineBreak)
                {
                    var name = Identifier(); var value = Expression(allowObject: false);
                    values.Add(new(name, value, null, new(name.Span.Start, (value.Span.Start + value.Span.Length) - name.Span.Start)));
                }
                statement = new InterfacePolicySyntax(keyword, values.ToArray(), Span(start));
            }
            if (!End && !Is(";") && !Is("}") && !LineBreak)
                throw Fail("firmament-syntax-interface-clause", $"Unexpected '{Current.Text}' in Interface {keyword.Text} clause.");
            return statement;
        }

        private BlockSyntax Block(bool enumValues = false, string? context = null)
        {
            Enter();
            if (context is not null) contexts.Push(context);
            try
            {
                var start = Current.Span.Start; Expect("{");
                IReadOnlyList<StatementSyntax> statements;
                if (enumValues)
                {
                    var values = new List<StatementSyntax>();
                    while (!End && !Is("}"))
                    {
                        if (Take(";") || Take(",")) continue;
                        var name = Identifier();
                        values.Add(new ExpressionStatementSyntax(new ReferenceSyntax(name, name.Span), name.Span));
                    }
                    statements = values.ToArray();
                }
                else statements = Statements("}");
                var closed = Take("}");
                if (!closed) Error("firmament-syntax-expected-token", $"Expected '}}'; found '{(End ? "end of file" : Current.Text)}'.", Current.Span);
                return new(statements, closed ? Span(start) : new(start, Current.Span.Start - start)) { IsClosed = closed };
            }
            finally { if (context is not null) contexts.Pop(); depth--; }
        }

        private ExpressionSyntax Expression(int minimum = 0, bool allowObject = true, bool stopGreater = false)
        {
            RejectGeneralControlFlow();
            Enter();
            try
            {
                var start = Current.Span.Start;
                ExpressionSyntax left;
                if (Take("+") || Take("-") || Take("!") || Take("Reverse"))
                {
                    var op = Previous.Text switch { "+" => UnaryOperator.Positive, "-" => UnaryOperator.Negative, "Reverse" => UnaryOperator.Reverse, _ => UnaryOperator.Not };
                    left = new UnarySyntax(op, Expression(80, allowObject, stopGreater), Span(start));
                }
                else if (Current.Kind is SyntaxTokenKind.Number or SyntaxTokenKind.String || Is("true") || Is("false"))
                {
                    var t = Current; at++;
                    left = new LiteralSyntax(t.Kind == SyntaxTokenKind.String ? LiteralKind.String : t.Kind == SyntaxTokenKind.Number ? LiteralKind.Number : LiteralKind.Boolean, t.Text, t.Span);
                    if (t.Kind == SyntaxTokenKind.Number && Current.Kind == SyntaxTokenKind.Number && Current.Text.StartsWith('.')
                        && (t.Span.Start + t.Span.Length) == Current.Span.Start && t.Text.Count(c => c == '.') == 1)
                    { at++; left = new LiteralSyntax(LiteralKind.Version, source[start..(Previous.Span.Start + Previous.Span.Length)], Span(start)); }
                }
                else if (Take("#"))
                {
                    if (Current.Kind != SyntaxTokenKind.Number || Current.Text.Any(c => !char.IsAsciiDigit(c)))
                        throw Fail("firmament-syntax-entity-reference", "Expected a numeric STEP entity identifier after '#'.");
                    at++; left = new LiteralSyntax(LiteralKind.EntityReference, source[start..(Previous.Span.Start + Previous.Span.Length)], Span(start));
                }
                else if (Take("Match"))
                {
                    var scrutinee = Expression(allowObject: false);
                    Expect("{");
                    var arms = new List<MatchArmSyntax>();
                    while (!End && !Is("}"))
                    {
                        if (Take(";") || Take(",")) continue;
                        var armStart = Current.Span.Start;
                        var pattern = Expression(allowObject: false);
                        Expect("="); Expect(">");
                        arms.Add(new(pattern, Expression(), Span(armStart)));
                    }
                    Expect("}");
                    left = new MatchSyntax(scrutinee, arms.ToArray(), Span(start));
                }
                else if (Current.Kind == SyntaxTokenKind.Identifier)
                {
                    var name = Identifier(); left = new ReferenceSyntax(name, name.Span);
                    var compatibilityDepthEnd = name.Text == "depth" && at >= 3 && tokens[at - 2].Text == ":" && tokens[at - 3].Text is "end" or "End";
                    if ((name.Text is "Blind" or "CenteredAt" || compatibilityDepthEnd) && !Is("(") && !Is(">") && !Is(",") && !LineBreak)
                    {
                        var argument = Expression(80, allowObject, stopGreater);
                        left = new CallSyntax(left, [new(null, argument, null, argument.Span)], Span(start));
                    }
                }
                else if (allowObject && Is("{")) left = new ObjectSyntax(null, Block(), Span(start));
                else if (Is("[") || Is("("))
                {
                    var list = Take("["); if (!list) Expect("(");
                    left = new SequenceSyntax(list ? SequenceKind.List : SequenceKind.Tuple, Arguments(list ? "]" : ")"), Span(start));
                }
                else throw Fail("firmament-syntax-expression", $"Expected an expression; found '{(End ? "end of file" : Current.Text)}'.");

                var suffixCount = 0;
                while (!End)
                {
                    if (++suffixCount > MaxDepth) throw Fail("firmament-syntax-depth", $"Expression chain exceeds the bounded parser limit of {MaxDepth}.");
                    // tol binds below arithmetic: (nominal expression) tol allowance.
                    // A leading + selects the two-sided +upper -lower spelling.
                    if (minimum <= 5 && Take("tol"))
                    {
                        var toleranceStart = Previous.Span.Start;
                        var plus = Expression(80, allowObject: false);
                        ExpressionSyntax? minus = null;
                        if (plus is UnarySyntax { Operator: UnaryOperator.Positive } && Take("-"))
                            minus = Expression(80, allowObject: false);
                        left = new ToleranceSyntax(left, plus, minus, Span(toleranceStart), Span(start));
                        continue;
                    }
                    if (Current.Text is "As" or "as" && Peek(1).Text != ":") { at++; left = new AliasSyntax(left, Identifier(), Span(start)); continue; }
                    if (Current.Text is "From" or "from" or "To" or "to" && Peek(1).Text != ":")
                    {
                        ExpressionSyntax? from = null, to = null;
                        if (Take("From") || Take("from")) from = Current.Kind == SyntaxTokenKind.String ? Expression(80, allowObject: false) : EndpointReference();
                        if (from is LiteralSyntax { Kind: LiteralKind.String } provenance)
                        { left = new ProvenanceSyntax(left, provenance, Span(start)); continue; }
                        if (Take("To") || Take("to")) to = EndpointReference();
                        left = new TraceSyntax(left, from, to, Span(start)); continue;
                    }
                    if (left is SequenceSyntax && Current.Text is "x" or "y" or "z" or "normal" && Peek(1).Text == "[")
                    {
                        var axes = new List<ArgumentSyntax>();
                        while (Current.Text is "x" or "y" or "z" or "normal" && Peek(1).Text == "[")
                        {
                            var axis = Identifier(); var vectorStart = Current.Span.Start; Expect("[");
                            var vector = new SequenceSyntax(SequenceKind.List, Arguments("]"), Span(vectorStart));
                            axes.Add(new(axis, vector, null, Span(axis.Span.Start)));
                        }
                        left = new FrameSyntax(left, axes.ToArray(), Span(start)); continue;
                    }
                    if (Take("."))
                    {
                        left = Take("[") ? new MemberSetSyntax(left, Arguments("]"), Span(start))
                            : new MemberSyntax(left, Identifier(), Span(start));
                        continue;
                    }
                    if (Take("(")) { left = new CallSyntax(left, Arguments(")"), Span(start)); continue; }
                    if (Take("["))
                    {
                        var index = Is("]") ? null : Expression(); Expect("]");
                        left = new IndexSyntax(left, index, Span(start)); continue;
                    }
                    // Angle suffixes belong to type/call-shaped names. Comparisons
                    // require a non-adjacent '<'; ambiguous forms require explicit spacing.
                    if (Is("<") && left is ReferenceSyntax or MemberSyntax && (Current.Span.Start == left.Span.Start + left.Span.Length
                        || Peek(1).Text == ">" || Peek(1).Kind == SyntaxTokenKind.Identifier && Peek(2).Text == ":"))
                    {
                        at++; left = new SpecializationSyntax(left, Arguments(">"), Span(start)); continue;
                    }
                    // Existing surface section lists contain named object values, e.g.
                    // Line S0 { ... }. Preserve the identity as syntax; its owner binds it.
                    if (allowObject && left is ReferenceSyntax or SpecializationSyntax && Current.Kind == SyntaxTokenKind.Identifier
                        && !LineBreak && Current.Text != "with" && Peek(1).Text == "{")
                    {
                        var objectName = Identifier();
                        left = new ObjectSyntax(left, Block(), Span(start)) { Name = objectName }; continue;
                    }
                    if (allowObject && Is("{") && left is ReferenceSyntax or SpecializationSyntax or AliasSyntax)
                    { left = new ObjectSyntax(left, Block(), Span(start)); continue; }
                    if (allowObject && Take("with")) { left = new WithSyntax(left, Block(), Span(start)); continue; }
                    if (Current.Kind == SyntaxTokenKind.Identifier && !LineBreak &&
                        (Current.Text is "mm" or "cm" or "m" or "deg" or "rad" or "N" or "kN" or "MN" or "Pa" or "kPa" or "MPa" or "GPa"
                            || left is LiteralSyntax { Kind: LiteralKind.Number } && Current.Span.Start == left.Span.Start + left.Span.Length)
                        && left is LiteralSyntax or SequenceSyntax or ReferenceSyntax)
                    { left = new QuantitySyntax(left, Identifier(), Span(start)); continue; }
                    // A product-tree tag starts the next statement, including after
                    // a newline-terminated field such as units: mm. It is not '<'
                    // arithmetic or a generic argument list on the preceding value.
                    if (Pair("<", "/") || Is("<") && Peek(1).Kind == SyntaxTokenKind.Identifier && Peek(2).Kind == SyntaxTokenKind.Identifier) break;
                    if (!Operator(out var binary, out var precedence, out var width) || precedence < minimum || stopGreater && Is(">")) break;
                    at += width;
                    left = new BinarySyntax(left, binary, Expression(precedence + (binary == BinaryOperator.Power ? 0 : 1), allowObject, stopGreater), Span(start));
                }
                return left;
            }
            finally { depth--; }
        }

        private IReadOnlyList<ArgumentSyntax> Arguments(string close, bool declarationParameters = false)
        {
            var args = new List<ArgumentSyntax>();
            while (!End && !Is(close))
            {
                var start = Current.Span.Start;
                IdentifierSyntax? name = null;
                if (close == ">" && Is("type") && Peek(1).Kind == SyntaxTokenKind.Identifier)
                {
                    at++; name = Identifier();
                    var constraint = Take("satisfies") ? Expression(allowObject: false, stopGreater: true) : null;
                    var parameter = new TypeParameterSyntax(name, constraint, Span(start));
                    args.Add(new(name, parameter, null, Span(start)));
                    if (!Take(",")) break;
                    continue;
                }
                if (Current.Kind == SyntaxTokenKind.Identifier && Peek(1).Text == ":") { name = Identifier(); Expect(":"); }
                var value = Expression(stopGreater: close == ">");
                if (declarationParameters && name is null && value is ReferenceSyntax && Current.Kind == SyntaxTokenKind.Identifier)
                    name = Identifier(); // historical Template(Type parameter), same typed parameter node
                ExpressionSyntax? fallback = Take("=") ? Expression(stopGreater: close == ">") : null;
                args.Add(new(name, value, fallback, Span(start)));
                if (!Take(",") && !(close == "]" && !Is(close) && (LineBreak || value is ObjectSyntax { Name: not null }
                    || value is ObjectSyntax && Current.Kind == SyntaxTokenKind.Identifier && Peek(1).Text == "{"))) break;
            }
            Expect(close); return args.ToArray();
        }

        private bool Operator(out BinaryOperator op, out int precedence, out int width)
        {
            width = 1;
            (op, precedence) = Current.Text switch
            {
                "+" => (BinaryOperator.Add, 50), "-" => (BinaryOperator.Subtract, 50),
                "*" => (BinaryOperator.Multiply, 60), "/" => (BinaryOperator.Divide, 60), "%" => (BinaryOperator.Remainder, 60),
                "^" => (BinaryOperator.Power, 70), "<" => (BinaryOperator.Less, 40), ">" => (BinaryOperator.Greater, 40),
                _ => (default, -1)
            };
            if (Pair("|", ">")) { op = BinaryOperator.Pipeline; precedence = 10; width = 2; }
            else if (Pair("-", ">")) { op = BinaryOperator.Direction; precedence = 15; width = 2; }
            else if (Pair("|", "|")) { op = BinaryOperator.Or; precedence = 20; width = 2; }
            else if (Pair("&", "&")) { op = BinaryOperator.And; precedence = 30; width = 2; }
            else if (Pair("=", "=")) { op = BinaryOperator.Equal; precedence = 40; width = 2; }
            else if (Pair("!", "=")) { op = BinaryOperator.NotEqual; precedence = 40; width = 2; }
            else if (Pair("<", "=")) { op = BinaryOperator.LessEqual; precedence = 40; width = 2; }
            else if (Pair(">", "=")) { op = BinaryOperator.GreaterEqual; precedence = 40; width = 2; }
            return precedence >= 0;
        }

        private static bool IsClause(string text) => TryClause(text, out _);
        private void RejectGeneralControlFlow()
        {
            if (Current.Text is "if" or "else" or "for" or "while" && (Peek(1).Kind == SyntaxTokenKind.Identifier || Peek(1).Text == "{"))
                throw Fail("firmament-syntax-control-flow-unsupported", $"General control flow '{Current.Text}' is not admitted; use finite Pattern expansion or compile-time Match where supported.");
        }
        private ExpressionSyntax EndpointReference()
        {
            var name = Identifier();
            ExpressionSyntax result = new ReferenceSyntax(name, name.Span);
            while (Take(".")) result = new MemberSyntax(result, Identifier(), Span(name.Span.Start));
            return result;
        }
        private static bool TryClause(string text, out ClauseKind kind)
        {
            kind = text switch { "On" or "on" => ClauseKind.On, "Using" or "using" => ClauseKind.Using,
                "Over" or "over" => ClauseKind.Over, "From" or "from" => ClauseKind.From, "Key" => ClauseKind.Key, _ => (ClauseKind)(-1) };
            return (int)kind >= 0;
        }
        private IdentifierSyntax Identifier()
        {
            if (Current.Kind != SyntaxTokenKind.Identifier) throw Fail("firmament-syntax-identifier", $"Expected an identifier; found '{Current.Text}'.");
            var t = Current; at++; return new(t.Text, t.Span);
        }
        private void Enter()
        {
            if (depth >= MaxDepth) throw Fail("firmament-syntax-depth", $"Syntax nesting exceeds the bounded parser limit of {MaxDepth}.");
            depth++;
        }
        private void Recover(int before, string? close)
        {
            // Skip a damaged statement's balanced groups, not the enclosing scope.
            var nesting = 0;
            while (!End)
            {
                if (nesting == 0 && (Is("}") || close is not null && Is(close))) return;
                if (at > before && nesting == 0 && (LineBreak || Take(";"))) return;
                if (Is("{") || Is("[") || Is("(")) nesting++;
                if (Is("}") || Is("]") || Is(")")) nesting = Math.Max(0, nesting - 1);
                at++;
            }
        }
        private ParseFailure Fail(string code, string message) { Error(code, message, Current.Span); return new(); }
        private void Error(string code, string message, FirmamentV2SourceSpan span)
        {
            var preceding = source.AsSpan(0, span.Start);
            var line = 1; foreach (var c in preceding) if (c == '\n') line++;
            var lastBreak = preceding.LastIndexOf('\n');
            diagnostics.Add(new(code, "fatal", message, Target: contexts.TryPeek(out var context) ? context : null) { SourceFile = file, SourceSpan = span, Line = line,
                Column = span.Start - lastBreak, Stage = "Syntax", Source = "UnifiedParser" });
        }
        private SyntaxToken Peek(int offset) => tokens[Math.Min(tokens.Length - 1, at + offset)];
        private bool Is(string text) => Current.Text == text;
        private bool Pair(string first, string second, int offset = 0) => Peek(offset).Text == first && Peek(offset + 1).Text == second
            && Peek(offset).Span.Start + Peek(offset).Span.Length == Peek(offset + 1).Span.Start;
        private bool Take(string text) { if (!Is(text)) return false; at++; return true; }
        private void Expect(string text) { if (!Take(text)) throw Fail("firmament-syntax-expected-token", $"Expected '{text}'; found '{(End ? "end of file" : Current.Text)}'."); }
        private FirmamentV2SourceSpan Span(int start) => new(start, Math.Max(0, Previous.Span.Start + Previous.Span.Length - start));
    }
}
