using System.Text;

namespace Aetheris.Kernel.Firmament.Formatting;

internal static class FirmamentFormatValueParser
{
    public static FirmamentFormatValue Parse(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var parser = new Parser(raw);
        var value = parser.ParseValue();
        parser.SkipWhitespace();
        if (!parser.IsAtEnd)
        {
            throw new FormatException($"Unexpected trailing content in formatter value '{raw}'.");
        }

        return value;
    }

    private sealed class Parser(string text)
    {
        private int _index;

        public bool IsAtEnd => _index >= text.Length;

        public FirmamentFormatValue ParseValue()
        {
            SkipWhitespace();
            if (IsAtEnd)
            {
                return new FirmamentScalarValue(string.Empty);
            }

            return text[_index] switch
            {
                '{' => ParseObject(),
                '[' => ParseArray(),
                _ => ParseScalarUntilDelimiter()
            };
        }

        public void SkipWhitespace()
        {
            while (!IsAtEnd && char.IsWhiteSpace(text[_index]))
            {
                _index++;
            }
        }

        private FirmamentObjectValue ParseObject()
        {
            Expect('{');
            SkipWhitespace();
            var members = new List<FirmamentObjectMember>();
            while (!IsAtEnd && text[_index] != '}')
            {
                var name = NormalizeFieldName(ParseName());
                SkipWhitespace();
                Expect(':');
                var value = ParseValueUntilMemberBoundary();
                members.Add(new FirmamentObjectMember(name, value));
                SkipWhitespace();
                if (!IsAtEnd && text[_index] == ',')
                {
                    _index++;
                    SkipWhitespace();
                }
            }

            Expect('}');
            return new FirmamentObjectValue(members);
        }

        private FirmamentArrayValue ParseArray()
        {
            Expect('[');
            SkipWhitespace();
            var items = new List<FirmamentFormatValue>();
            while (!IsAtEnd && text[_index] != ']')
            {
                var item = ParseValueUntilArrayBoundary();
                items.Add(item);
                SkipWhitespace();
                if (!IsAtEnd && text[_index] == ',')
                {
                    _index++;
                    SkipWhitespace();
                }
            }

            Expect(']');
            return new FirmamentArrayValue(items);
        }

        private FirmamentFormatValue ParseValueUntilMemberBoundary()
        {
            SkipWhitespace();
            if (IsAtEnd)
            {
                return new FirmamentScalarValue(string.Empty);
            }

            return text[_index] switch
            {
                '{' => ParseObject(),
                '[' => ParseArray(),
                _ => new FirmamentScalarValue(ParseTokenUntilBoundary(',', '}'))
            };
        }

        private FirmamentFormatValue ParseValueUntilArrayBoundary()
        {
            SkipWhitespace();
            if (IsAtEnd)
            {
                return new FirmamentScalarValue(string.Empty);
            }

            return text[_index] switch
            {
                '{' => ParseObject(),
                '[' => ParseArray(),
                _ => new FirmamentScalarValue(ParseTokenUntilBoundary(',', ']'))
            };
        }

        private FirmamentScalarValue ParseScalarUntilDelimiter()
            => new(ParseTokenUntilBoundary('\0'));


        private static string NormalizeFieldName(string fieldName)
        {
            var bracketIndex = fieldName.IndexOf('[', StringComparison.Ordinal);
            return bracketIndex > 0 && fieldName.EndsWith("]", StringComparison.Ordinal)
                ? fieldName[..bracketIndex]
                : fieldName;
        }

        private string ParseName()
        {
            SkipWhitespace();
            var start = _index;
            while (!IsAtEnd && text[_index] != ':' && !char.IsWhiteSpace(text[_index]))
            {
                _index++;
            }

            return text[start.._index].Trim();
        }

        private string ParseTokenUntilBoundary(params char[] boundaries)
        {
            var builder = new StringBuilder();
            var squareDepth = 0;
            var curlyDepth = 0;

            while (!IsAtEnd)
            {
                var current = text[_index];
                if (current == '[')
                {
                    squareDepth++;
                }
                else if (current == ']')
                {
                    if (squareDepth == 0 && boundaries.Contains(']'))
                    {
                        break;
                    }

                    squareDepth = Math.Max(0, squareDepth - 1);
                }
                else if (current == '{')
                {
                    curlyDepth++;
                }
                else if (current == '}')
                {
                    if (curlyDepth == 0 && boundaries.Contains('}'))
                    {
                        break;
                    }

                    curlyDepth = Math.Max(0, curlyDepth - 1);
                }
                else if (squareDepth == 0 && curlyDepth == 0 && boundaries.Contains(current))
                {
                    break;
                }

                builder.Append(current);
                _index++;
            }

            return builder.ToString().Trim();
        }

        private void Expect(char ch)
        {
            if (IsAtEnd || text[_index] != ch)
            {
                throw new FormatException($"Expected '{ch}' while parsing formatter value '{text}'.");
            }

            _index++;
        }
    }
}

internal abstract record FirmamentFormatValue(string RawText);

internal sealed record FirmamentScalarValue(string Value) : FirmamentFormatValue(Value);

internal sealed record FirmamentArrayValue(IReadOnlyList<FirmamentFormatValue> Items)
    : FirmamentFormatValue("[" + string.Join(", ", Items.Select(item => item.RawText)) + "]");

internal sealed record FirmamentObjectValue(IReadOnlyList<FirmamentObjectMember> Members)
    : FirmamentFormatValue("{ " + string.Join(", ", Members.Select(member => $"{member.Name}: {member.Value.RawText}")) + " }");

internal sealed record FirmamentObjectMember(string Name, FirmamentFormatValue Value);
