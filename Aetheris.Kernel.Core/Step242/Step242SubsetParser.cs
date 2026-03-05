using System.Globalization;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Step242;

internal static class Step242SubsetParser
{
    public static KernelResult<Step242ParsedDocument> Parse(string stepText)
    {
        if (string.IsNullOrWhiteSpace(stepText))
        {
            return Failure("STEP text must be non-empty.", "Parser");
        }

        var parser = new Reader(stepText);
        var entities = new List<Step242ParsedEntity>();

        try
        {
            while (!parser.End)
            {
                parser.SkipWhitespace();
                if (parser.End)
                {
                    break;
                }

                if (parser.Peek() == '#')
                {
                    var entity = parser.ReadEntity();
                    entities.Add(entity);
                }
                else
                {
                    parser.ReadToStatementTerminator();
                }
            }
        }
        catch (Step242ParseException ex)
        {
            return Failure(ex.Message, ex.SourceTag);
        }
        catch (Exception ex)
        {
            return Failure($"Unexpected parser failure: {ex.Message}", "Parser.Semantics");
        }

        var duplicateId = entities
            .GroupBy(e => e.Id)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicateId is not null)
        {
            return Failure($"Duplicate entity id #{duplicateId.Key} detected.", "Parser.Semantics");
        }

        return KernelResult<Step242ParsedDocument>.Success(new Step242ParsedDocument(entities));
    }

    private static KernelResult<Step242ParsedDocument> Failure(string message, string source)
    {
        var code = string.Equals(source, "Importer.StepSyntax.ComplexInstance", StringComparison.Ordinal)
            || string.Equals(source, "Importer.StepSyntax.TypedValue", StringComparison.Ordinal)
            ? KernelDiagnosticCode.ValidationFailed
            : KernelDiagnosticCode.InvalidArgument;

        return KernelResult<Step242ParsedDocument>.Failure([
            new KernelDiagnostic(code, KernelDiagnosticSeverity.Error, message, source)
        ]);
    }

    private sealed class Reader(string text)
    {
        private int _index;

        public bool End => _index >= text.Length;

        public char Peek() => text[_index];

        private bool TryPeekNext(out char c)
        {
            var next = _index + 1;
            if (next < text.Length)
            {
                c = text[next];
                return true;
            }

            c = default;
            return false;
        }

        public void SkipWhitespace()
        {
            while (!End)
            {
                if (char.IsWhiteSpace(text[_index]))
                {
                    _index++;
                    continue;
                }

                if (text[_index] == '/' && TryPeekNext(out var next) && next == '*')
                {
                    _index += 2;
                    var terminated = false;
                    while (!End)
                    {
                        if (text[_index] == '*' && TryPeekNext(out next) && next == '/')
                        {
                            _index += 2;
                            terminated = true;
                            break;
                        }

                        _index++;
                    }

                    if (!terminated)
                    {
                        throw Error("Unterminated comment block.");
                    }

                    continue;
                }

                break;
            }
        }

        public Step242ParsedEntity ReadEntity()
        {
            Expect('#');
            var id = ReadInteger();
            SkipWhitespace();
            Expect('=');
            SkipWhitespace();
            var instance = ReadEntityInstance();
            SkipWhitespace();
            Expect(';');
            return new Step242ParsedEntity(id, instance);
        }

        private Step242EntityInstance ReadEntityInstance()
        {
            SkipWhitespace();
            if (End)
            {
                throw Error("Unexpected end of text in entity instance.");
            }

            if (Peek() != '(')
            {
                return new Step242SimpleEntityInstance(ReadEntityConstructor());
            }

            const string complexSource = "Importer.StepSyntax.ComplexInstance";
            Expect('(');

            var constructors = new List<Step242EntityConstructor>();
            while (true)
            {
                SkipWhitespace();
                if (End)
                {
                    throw Error("Unexpected end of text in complex instance.", complexSource);
                }

                if (Peek() == ')')
                {
                    _index++;
                    break;
                }

                if (!IsIdentifierStart(Peek()))
                {
                    throw Error("Invalid complex instance element.", complexSource);
                }

                constructors.Add(ReadEntityConstructor());
            }

            if (constructors.Count == 0)
            {
                throw Error("Complex instance must contain at least one entity.", complexSource);
            }

            return new Step242ComplexEntityInstance(constructors);
        }

        private Step242EntityConstructor ReadEntityConstructor()
        {
            var name = ReadIdentifier();
            name = name.ToUpperInvariant();
            SkipWhitespace();
            Expect('(');
            var args = ReadArgumentList();
            return new Step242EntityConstructor(name, args);
        }

        public void ReadToStatementTerminator()
        {
            var depth = 0;
            while (!End)
            {
                var c = text[_index++];
                if (c == '\'')
                {
                    SkipStringLiteralBody();
                    continue;
                }

                if (c == '/' && !End && Peek() == '*')
                {
                    _index++;
                    SkipCommentBody();
                    continue;
                }

                if (c == '(')
                {
                    depth++;
                    continue;
                }

                if (c == ')' && depth > 0)
                {
                    depth--;
                    continue;
                }

                if (c == ';')
                {
                    return;
                }
            }

            throw Error("Unable to recover statement boundary before end of text.", "Parser.Semantics");
        }

        private List<Step242Value> ReadArgumentList(string sourceTag = "Parser.Lexer")
        {
            var values = new List<Step242Value>();
            SkipWhitespace();
            if (!End && Peek() == ')')
            {
                _index++;
                return values;
            }

            while (true)
            {
                values.Add(ReadValue());
                SkipWhitespace();

                if (End)
                {
                    throw Error("Unexpected end of text in argument list.", sourceTag);
                }

                var next = text[_index++];
                if (next == ')')
                {
                    break;
                }

                if (next != ',')
                {
                    throw Error($"Expected ',' or ')' in argument list, found '{next}'.", sourceTag);
                }

                SkipWhitespace();
            }

            return values;
        }

        private Step242Value ReadValue()
        {
            SkipWhitespace();
            if (End)
            {
                throw Error("Unexpected end of text while reading value.");
            }

            var c = Peek();
            return c switch
            {
                '#' => ReadReference(),
                '\'' => ReadString(),
                '$' => ReadOmitted(),
                '*' => ReadDerivedOmitted(),
                '(' => ReadList(),
                '.' => TryPeekNext(out var next) && char.IsDigit(next) ? ReadNumber() : ReadEnumOrLogical(),
                '+' or '-' or >= '0' and <= '9' => ReadNumber(),
                _ when IsIdentifierStart(c) => ReadTypedValue(),
                _ => throw Error($"Unsupported value token '{c}'.")
            };
        }

        private Step242Value ReadTypedValue()
        {
            const string typedValueSource = "Importer.StepSyntax.TypedValue";
            var name = ReadIdentifier();
            name = name.ToUpperInvariant();

            SkipWhitespace();
            if (End || Peek() != '(')
            {
                throw Error("Typed parameter value must include parenthesized arguments.", typedValueSource);
            }

            Expect('(');
            var args = ReadArgumentList(typedValueSource);
            return new Step242TypedValue(name, args);
        }

        private Step242Value ReadReference()
        {
            Expect('#');
            return new Step242EntityReference(ReadInteger());
        }

        private Step242Value ReadString()
        {
            Expect('\'');
            var chars = new List<char>();
            while (!End)
            {
                var c = text[_index++];
                if (c == '\'')
                {
                    if (!End && Peek() == '\'')
                    {
                        _index++;
                        chars.Add('\'');
                        continue;
                    }

                    return new Step242StringValue(new string(chars.ToArray()));
                }

                chars.Add(c);
            }

            throw Error("Unterminated string literal.");
        }

        private Step242Value ReadOmitted()
        {
            Expect('$');
            return Step242OmittedValue.Instance;
        }

        private Step242Value ReadDerivedOmitted()
        {
            Expect('*');
            return Step242OmittedValue.Instance;
        }

        private Step242Value ReadList()
        {
            Expect('(');
            var items = new List<Step242Value>();
            SkipWhitespace();
            if (!End && Peek() == ')')
            {
                _index++;
                return new Step242ListValue(items);
            }

            while (true)
            {
                items.Add(ReadValue());
                SkipWhitespace();
                if (End)
                {
                    throw Error("Unexpected end of text in list literal.");
                }

                var next = text[_index++];
                if (next == ')')
                {
                    break;
                }

                if (next != ',')
                {
                    throw Error($"Expected ',' or ')' in list literal, found '{next}'.");
                }

                SkipWhitespace();
            }

            return new Step242ListValue(items);
        }

        private Step242Value ReadEnumOrLogical()
        {
            Expect('.');
            var token = ReadIdentifier();
            Expect('.');

            token = token.ToUpperInvariant();

            if (string.Equals(token, "T", StringComparison.OrdinalIgnoreCase))
            {
                return new Step242BooleanValue(true);
            }

            if (string.Equals(token, "F", StringComparison.OrdinalIgnoreCase))
            {
                return new Step242BooleanValue(false);
            }

            return new Step242EnumValue(token);
        }

        private Step242Value ReadNumber()
        {
            var start = _index;
            if (!End && (Peek() == '+' || Peek() == '-'))
            {
                _index++;
            }

            var hasLeadingDigits = false;
            while (!End && char.IsDigit(Peek()))
            {
                _index++;
                hasLeadingDigits = true;
            }

            var hasFractionDigits = false;
            if (!End && Peek() == '.')
            {
                _index++;
                while (!End && char.IsDigit(Peek()))
                {
                    _index++;
                    hasFractionDigits = true;
                }
            }

            if (!hasLeadingDigits && !hasFractionDigits)
            {
                throw Error("Invalid numeric literal.");
            }

            if (!End && (Peek() == 'E' || Peek() == 'e'))
            {
                _index++;
                if (!End && (Peek() == '+' || Peek() == '-'))
                {
                    _index++;
                }

                var exponentDigits = 0;
                while (!End && char.IsDigit(Peek()))
                {
                    _index++;
                    exponentDigits++;
                }

                if (exponentDigits == 0)
                {
                    throw Error("Invalid exponent in numeric literal.");
                }
            }

            var token = text[start.._index];
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                throw Error($"Invalid numeric literal '{token}'.");
            }

            return new Step242NumberValue(value);
        }

        private string ReadIdentifier()
        {
            var start = _index;
            while (!End && (char.IsLetterOrDigit(Peek()) || Peek() == '_'))
            {
                _index++;
            }

            if (start == _index)
            {
                throw Error("Expected identifier.");
            }

            return text[start.._index];
        }

        private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

        private int ReadInteger()
        {
            var start = _index;
            while (!End && char.IsDigit(Peek()))
            {
                _index++;
            }

            if (start == _index || !int.TryParse(text[start.._index], NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                throw Error("Expected integer id.");
            }

            return value;
        }

        private void Expect(char expected)
        {
            if (End || text[_index] != expected)
            {
                throw Error($"Expected '{expected}'.");
            }

            _index++;
        }

        private void SkipStringLiteralBody()
        {
            while (!End)
            {
                var c = text[_index++];
                if (c != '\'')
                {
                    continue;
                }

                if (!End && Peek() == '\'')
                {
                    _index++;
                    continue;
                }

                return;
            }

            throw Error("Unterminated string literal.");
        }

        private void SkipCommentBody()
        {
            while (!End)
            {
                if (text[_index] == '*' && TryPeekNext(out var next) && next == '/')
                {
                    _index += 2;
                    return;
                }

                _index++;
            }

            throw Error("Unterminated comment block.");
        }

        private Step242ParseException Error(string message, string sourceTag = "Parser.Lexer") => new($"{message} (position {_index}).", sourceTag);
    }
}

internal sealed class Step242ParseException(string message, string sourceTag) : Exception(message)
{
    public string SourceTag { get; } = sourceTag;
}

internal sealed record Step242ParsedEntity(int Id, Step242EntityInstance Instance)
{
    public string Name => Instance.PrimaryConstructor.Name;

    public IReadOnlyList<Step242Value> Arguments => Instance.PrimaryConstructor.Arguments;
}

internal sealed class Step242ParsedDocument
{
    private readonly Dictionary<int, Step242ParsedEntity> _entitiesById;

    public Step242ParsedDocument(IReadOnlyList<Step242ParsedEntity> entities)
    {
        Entities = entities;
        _entitiesById = entities.ToDictionary(e => e.Id);
    }

    public IReadOnlyList<Step242ParsedEntity> Entities { get; }

    public KernelResult<Step242ParsedEntity> TryGetEntity(int id, string? expectedName = null)
    {
        if (!_entitiesById.TryGetValue(id, out var entity))
        {
            return Failure($"Missing referenced entity #{id}.", $"Entity:{id}");
        }

        if (expectedName is not null && Step242SubsetDecoder.TryGetConstructor(entity.Instance, expectedName) is null)
        {
            return Failure($"Entity #{id} expected '{expectedName}' but found '{entity.Name}'.", $"Entity:{id}");
        }

        return KernelResult<Step242ParsedEntity>.Success(entity);
    }

    private static KernelResult<Step242ParsedEntity> Failure(string message, string source) =>
        KernelResult<Step242ParsedEntity>.Failure([
            new KernelDiagnostic(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, message, source)
        ]);
}

internal abstract record Step242Value;

internal abstract record Step242EntityInstance
{
    public abstract Step242EntityConstructor PrimaryConstructor { get; }
}

internal sealed record Step242SimpleEntityInstance(Step242EntityConstructor Constructor) : Step242EntityInstance
{
    public override Step242EntityConstructor PrimaryConstructor => Constructor;
}

internal sealed record Step242ComplexEntityInstance(IReadOnlyList<Step242EntityConstructor> Constructors) : Step242EntityInstance
{
    public override Step242EntityConstructor PrimaryConstructor => Constructors[0];
}

internal sealed record Step242EntityConstructor(string Name, IReadOnlyList<Step242Value> Arguments);

internal sealed record Step242EntityReference(int TargetId) : Step242Value;

internal sealed record Step242StringValue(string Value) : Step242Value;

internal sealed record Step242NumberValue(double Value) : Step242Value;

internal sealed record Step242BooleanValue(bool Value) : Step242Value;

internal sealed record Step242EnumValue(string Value) : Step242Value;

internal sealed record Step242ListValue(IReadOnlyList<Step242Value> Items) : Step242Value;

internal sealed record Step242TypedValue(string Name, IReadOnlyList<Step242Value> Arguments) : Step242Value;

internal sealed record Step242OmittedValue : Step242Value
{
    public static Step242OmittedValue Instance { get; } = new();
}
