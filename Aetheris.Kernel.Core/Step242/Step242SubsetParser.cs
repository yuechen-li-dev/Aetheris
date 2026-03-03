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

        var duplicateId = entities
            .GroupBy(e => e.Id)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicateId is not null)
        {
            return Failure($"Duplicate entity id #{duplicateId.Key} detected.", "Parser.Semantics");
        }

        return KernelResult<Step242ParsedDocument>.Success(new Step242ParsedDocument(entities));
    }

    private static KernelResult<Step242ParsedDocument> Failure(string message, string source) =>
        KernelResult<Step242ParsedDocument>.Failure([
            new KernelDiagnostic(KernelDiagnosticCode.InvalidArgument, KernelDiagnosticSeverity.Error, message, source)
        ]);

    private sealed class Reader(string text)
    {
        private int _index;

        public bool End => _index >= text.Length;

        public char Peek() => text[_index];

        public void SkipWhitespace()
        {
            while (!End && char.IsWhiteSpace(text[_index]))
            {
                _index++;
            }
        }

        public Step242ParsedEntity ReadEntity()
        {
            Expect('#');
            var id = ReadInteger();
            SkipWhitespace();
            Expect('=');
            SkipWhitespace();
            var name = ReadIdentifier();
            SkipWhitespace();
            Expect('(');
            var args = ReadArgumentList();
            SkipWhitespace();
            Expect(';');
            return new Step242ParsedEntity(id, name, args);
        }

        public void ReadToStatementTerminator()
        {
            while (!End)
            {
                var c = text[_index++];
                if (c == ';')
                {
                    return;
                }
            }
        }

        private List<Step242Value> ReadArgumentList()
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
                    throw Error("Unexpected end of text in argument list.");
                }

                var next = text[_index++];
                if (next == ')')
                {
                    break;
                }

                if (next != ',')
                {
                    throw Error($"Expected ',' or ')' in argument list, found '{next}'.");
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
                '(' => ReadList(),
                '.' => ReadEnumOrLogical(),
                '+' or '-' or >= '0' and <= '9' => ReadNumber(),
                _ => throw Error($"Unsupported value token '{c}'.")
            };
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

            while (!End && char.IsDigit(Peek()))
            {
                _index++;
            }

            if (!End && Peek() == '.')
            {
                _index++;
                while (!End && char.IsDigit(Peek()))
                {
                    _index++;
                }
            }

            if (!End && (Peek() == 'E' || Peek() == 'e'))
            {
                _index++;
                if (!End && (Peek() == '+' || Peek() == '-'))
                {
                    _index++;
                }

                while (!End && char.IsDigit(Peek()))
                {
                    _index++;
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

        private Step242ParseException Error(string message) => new($"{message} (position {_index}).", "Parser.Lexer");
    }
}

internal sealed class Step242ParseException(string message, string sourceTag) : Exception(message)
{
    public string SourceTag { get; } = sourceTag;
}

internal sealed record Step242ParsedEntity(int Id, string Name, IReadOnlyList<Step242Value> Arguments);

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

        if (expectedName is not null && !string.Equals(entity.Name, expectedName, StringComparison.OrdinalIgnoreCase))
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

internal sealed record Step242EntityReference(int TargetId) : Step242Value;

internal sealed record Step242StringValue(string Value) : Step242Value;

internal sealed record Step242NumberValue(double Value) : Step242Value;

internal sealed record Step242BooleanValue(bool Value) : Step242Value;

internal sealed record Step242EnumValue(string Value) : Step242Value;

internal sealed record Step242ListValue(IReadOnlyList<Step242Value> Items) : Step242Value;

internal sealed record Step242OmittedValue : Step242Value
{
    public static Step242OmittedValue Instance { get; } = new();
}
