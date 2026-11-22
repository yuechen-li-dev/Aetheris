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
        var file = new Step242FileStructure();

        try
        {
            parser.ReadFile(entities, file);
        }
        catch (Step242ParseException ex)
        {
            // Text that never showed the exchange-structure keyword is not a damaged STEP file but another format.
            return file.SawStart || file.DataSections > 0 || entities.Count > 0
                ? Failure(ex.Message, ex.SourceTag)
                : Failure("This is not a STEP file: an ISO 10303-21 file begins with 'ISO-10303-21;' and holds a DATA section of '#n = ENTITY(...);' instances.", "Parser.Structure");
        }
        catch (Exception ex)
        {
            return Failure($"Unexpected parser failure: {ex.Message}", "Parser.Semantics");
        }

        // ISO 10303-21 structure: the exchange-structure keyword, a HEADER, a DATA section closed by ENDSEC,
        // and the terminating keyword. A file that breaks it is refused here, with the reason, rather than
        // failing later on whatever the missing part would have held.
        if (!file.SawStart)
            return Failure(entities.Count == 0
                ? "This is not a STEP file: an ISO 10303-21 file begins with 'ISO-10303-21;' and holds a DATA section of '#n = ENTITY(...);' instances."
                : "The STEP file does not begin with 'ISO-10303-21;' as ISO 10303-21 requires; its header is missing or damaged.", "Parser.Structure");
        if (file.DataSections == 0)
            return Failure("The STEP file has no DATA section, so it holds no geometry.", "Parser.Structure");
        if (file.ClosedDataSections < file.DataSections || !file.SawEnd)
            return Failure($"The STEP file is incomplete: {(file.ClosedDataSections < file.DataSections ? "its DATA section is not closed by 'ENDSEC;'" : "it does not end with 'END-ISO-10303-21;'")}. "
                + "It was probably truncated while it was written, copied or downloaded.", "Parser.Structure");
        if (file.Schemas.FirstOrDefault(schema => schema.StartsWith("IFC", StringComparison.OrdinalIgnoreCase)) is { } ifc)
            return Failure($"This is an IFC building-model file (schema {ifc}), not a STEP part or assembly.", "Parser.Structure");
        var notes = new List<KernelDiagnostic>();
        if (!file.SawHeader || file.Schemas.Count == 0)
            notes.Add(new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Warning,
                "The STEP file names no FILE_SCHEMA in its HEADER; it is read as AP242, which also covers AP203 and AP214 geometry.", "Parser.Header.Schema"));
        else if (file.Schemas.All(schema => !Step242FileStructure.IsKnownSchema(schema)))
            notes.Add(new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Warning,
                $"The STEP file declares schema {string.Join(", ", file.Schemas.Select(schema => $"'{schema}'"))}, which is not AP203, AP214 or AP242; only the geometry it shares with AP242 is read.", "Parser.Header.Schema"));
        if (file.Edition3Sections.Count > 0)
            notes.Add(new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Warning,
                $"The STEP file uses ISO 10303-21 edition 3 {string.Join(", ", file.Edition3Sections.Distinct())} section(s); they are skipped, so references into other files are not followed.", "Parser.Header.Edition3"));
        if (file.UserDefinedEntities > 0)
            notes.Add(new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Info,
                $"The STEP file holds {file.UserDefinedEntities} user-defined ('!') entity instance(s); they are kept but not interpreted.", "Parser.UserDefinedEntity"));

        var duplicateId = entities
            .GroupBy(e => e.Id)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicateId is not null)
        {
            return Failure($"Duplicate entity id #{duplicateId.Key} detected.", "Parser.Semantics");
        }

        var explicitEntities = Step242EquivalentForms.Normalize(Step242ImplicitKnots.Normalize(entities), notes, out var orientedAway);
        var millimetresPerUnit = Step242LengthUnitNormalizer.ResolveMillimetresPerUnit(explicitEntities, out var unitDeclared);
        // A file whose representation context assigns no length unit; a bare entity fragment has no context to ask.
        if (!unitDeclared && explicitEntities.Any(e => Step242SubsetDecoder.TryGetConstructor(e.Instance, "GEOMETRIC_REPRESENTATION_CONTEXT") is not null))
            notes.Add(new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Warning,
                "The STEP file declares no length unit this importer reads (no LENGTH_UNIT in a GLOBAL_UNIT_ASSIGNED_CONTEXT); lengths are read as millimetres.", "Parser.Units.LengthUnitMissing"));
        var normalizedEntities = Step242LengthUnitNormalizer.Normalize(explicitEntities, millimetresPerUnit);
        var declaredAccuracy = Step242LengthUnitNormalizer.ResolveDistanceAccuracyMillimetres(entities, millimetresPerUnit);
        return KernelResult<Step242ParsedDocument>.Success(new Step242ParsedDocument(normalizedEntities, millimetresPerUnit, declaredAccuracy) { Notes = notes, OrientedAwayFaceIds = orientedAway }, notes);
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

        private const int MaximumNesting = 256;
        private int _depth;
        private int? _entityId;
        private List<Step242ParsedEntity>? _entities;
        private Step242FileStructure? _file;

        /// <summary>
        /// Reads the exchange structure by section: HEADER statements (FILE_SCHEMA is kept), DATA instances,
        /// edition 3 ANCHOR/REFERENCE/SIGNATURE sections (skipped) and the terminating keyword.
        /// </summary>
        public void ReadFile(List<Step242ParsedEntity> entities, Step242FileStructure file)
        {
            _entities = entities; _file = file;
            if (!End && Peek() == '\uFEFF') _index++;
            var inHeader = false; var inData = false;
            while (!End)
            {
                SkipWhitespace();
                if (End) break;
                if (Peek() == '#')
                {
                    entities.Add(ReadEntity());
                    continue;
                }
                if (!IsKeywordStart(Peek()))
                {
                    ReadToStatementTerminator();
                    continue;
                }
                var keyword = ReadKeyword().ToUpperInvariant();
                switch (keyword)
                {
                    case "ISO-10303-21": file.SawStart = true; ExpectStatementEnd(keyword); break;
                    case "HEADER": file.SawHeader = true; inHeader = true; ExpectStatementEnd(keyword); break;
                    case "DATA": file.DataSections++; inData = true; inHeader = false; ReadToStatementTerminator(); break;
                    case "ENDSEC":
                        if (inData) file.ClosedDataSections++;
                        inData = false; inHeader = false; ExpectStatementEnd(keyword); break;
                    case "ANCHOR" or "REFERENCE" or "SIGNATURE":
                        file.Edition3Sections.Add(keyword); ExpectStatementEnd(keyword); SkipSection(); break;
                    case "END-ISO-10303-21": file.SawEnd = true; return;
                    default:
                        if (inHeader && keyword == "FILE_SCHEMA" && !End)
                        {
                            SkipWhitespace();
                            Expect('(');
                            foreach (var value in ReadArgumentList())
                                CollectStrings(value, file.Schemas);
                            ExpectStatementEnd(keyword);
                        }
                        else ReadToStatementTerminator();
                        break;
                }
            }
        }

        private static void CollectStrings(Step242Value value, List<string> into)
        {
            if (value is Step242StringValue text) into.Add(text.Value.Split(' ', 2)[0].Trim());
            else if (value is Step242ListValue list) foreach (var item in list.Items) CollectStrings(item, into);
        }

        /// <summary>An edition 3 section's statements up to its ENDSEC; strings and comments may hold anything.</summary>
        private void SkipSection()
        {
            while (!End)
            {
                SkipWhitespace();
                if (End) break;
                if (IsKeywordStart(Peek()))
                {
                    var save = _index;
                    if (ReadKeyword().Equals("ENDSEC", StringComparison.OrdinalIgnoreCase)) { ExpectStatementEnd("ENDSEC"); return; }
                    _index = save;
                }
                ReadToStatementTerminator();
            }
            throw Error("An ANCHOR, REFERENCE or SIGNATURE section is not closed by 'ENDSEC;'.", "Parser.Structure");
        }

        private void ExpectStatementEnd(string keyword)
        {
            SkipWhitespace();
            if (End || text[_index] != ';') throw Error($"Expected ';' after '{keyword}'.", "Parser.Structure");
            _index++;
        }

        private static bool IsKeywordStart(char c) => char.IsLetter(c) || c == '_';

        private string ReadKeyword()
        {
            var start = _index;
            while (!End && (char.IsLetterOrDigit(Peek()) || Peek() == '_' || Peek() == '-')) _index++;
            return text[start.._index];
        }

        public Step242ParsedEntity ReadEntity()
        {
            Expect('#');
            var id = ReadInteger();
            _entityId = id;
            SkipWhitespace();
            Expect('=');
            SkipWhitespace();
            if (!End && Peek() == '&') ReadScope();
            var instance = ReadEntityInstance();
            SkipWhitespace();
            if (End || text[_index] != ';')
                throw Error(End ? "The file ends before this entity's closing ';'." : $"Expected ';' after the entity, found '{text[_index]}'.");
            _index++;
            _entityId = null;
            return new Step242ParsedEntity(id, instance);
        }

        /// <summary>
        /// A SCOPE structure (ISO 10303-21 §10.3): '&SCOPE' instances 'ENDSCOPE' [export list], then the
        /// owning instance. The scoped instances are read as ordinary instances; their ids stay unique per file.
        /// </summary>
        private void ReadScope()
        {
            var owner = _entityId;
            _index++;
            if (!ReadKeyword().Equals("SCOPE", StringComparison.OrdinalIgnoreCase)) throw Error("Expected 'SCOPE' after '&'.");
            while (true)
            {
                SkipWhitespace();
                if (End) throw Error("A SCOPE structure is not closed by 'ENDSCOPE'.");
                if (Peek() == '#') { _entities!.Add(ReadEntity()); continue; }
                if (!ReadKeyword().Equals("ENDSCOPE", StringComparison.OrdinalIgnoreCase)) throw Error("Expected an instance or 'ENDSCOPE' in a SCOPE structure.");
                break;
            }
            _entityId = owner;
            SkipWhitespace();
            if (!End && Peek() == '/')
            {
                _index++;
                while (!End && text[_index] != '/') _index++;
                if (End) throw Error("A SCOPE export list is not closed by '/'.");
                _index++;
                SkipWhitespace();
            }
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
                if (!IsIdentifierStart(Peek()) && Peek() != '!')
                    throw Error($"Expected an entity name after '=', found '{Peek()}'.");
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

                if (!IsIdentifierStart(Peek()) && Peek() != '!')
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
            // A user-defined entity is '!' followed by its keyword; it is kept under that name.
            var userDefined = !End && Peek() == '!';
            if (userDefined) { _index++; _file!.UserDefinedEntities++; }
            var name = ReadIdentifier();
            name = (userDefined ? "!" : string.Empty) + name.ToUpperInvariant();
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
            using var nesting = Nest();
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
                '"' => ReadBinary(),
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

        /// <summary>Real files nest lists a few levels deep; a bound keeps a hostile file from exhausting the stack.</summary>
        private NestingScope Nest()
        {
            if (++_depth > MaximumNesting)
                throw Error($"Lists are nested more than {MaximumNesting} levels deep; no STEP schema nests that far.", "Parser.Structure");
            return new NestingScope(this);
        }

        private readonly struct NestingScope(Reader reader) : IDisposable
        {
            public void Dispose() => reader._depth--;
        }

        /// <summary>A binary literal: '"' then a digit giving the unused leading bits and hexadecimal digits.</summary>
        private Step242Value ReadBinary()
        {
            Expect('"');
            var start = _index;
            while (!End && Uri.IsHexDigit(Peek())) _index++;
            if (End || Peek() != '"' || _index == start) throw Error("Malformed binary literal; expected '\"' then hexadecimal digits then '\"'.");
            var digits = text[start.._index];
            _index++;
            return new Step242BinaryValue(digits);
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

                    return new Step242StringValue(Step242StringDecoding.Decode(new string(chars.ToArray())));
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
            return Step242OmittedValue.Derived;
        }

        private Step242Value ReadList()
        {
            using var nesting = Nest();
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

        private Step242ParseException Error(string message, string sourceTag = "Parser.Lexer")
        {
            var line = 1; var lineStart = 0;
            for (var i = 0; i < _index && i < text.Length; i++)
                if (text[i] == '\n') { line++; lineStart = i + 1; }
            var where = _entityId is { } id ? $"in entity #{id}, " : string.Empty;
            var truncated = End ? " The file ends here; it was probably truncated." : string.Empty;
            return new($"{message} ({where}line {line}, column {_index - lineStart + 1}).{truncated}", sourceTag);
        }
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

    public Step242ParsedDocument(
        IReadOnlyList<Step242ParsedEntity> entities,
        double sourceMillimetresPerUnit = 1d,
        double? sourceDistanceAccuracyMillimetres = null)
    {
        Entities = entities;
        SourceMillimetresPerUnit = sourceMillimetresPerUnit;
        SourceDistanceAccuracyMillimetres = sourceDistanceAccuracyMillimetres;
        _entitiesById = entities.ToDictionary(e => e.Id);
        PlaneAngleToRadiansScale = ResolvePlaneAngleToRadiansScale();
    }

    public IReadOnlyList<Step242ParsedEntity> Entities { get; }

    /// <summary>Warnings about the file's structure (header, edition 3 sections); every import reports them.</summary>
    public IReadOnlyList<KernelDiagnostic> Notes { get; init; } = [];

    /// <summary>Faces an ORIENTED_FACE stands in for; the shell lists the oriented face, never these.</summary>
    public IReadOnlySet<int> OrientedAwayFaceIds { get; init; } = new HashSet<int>();

    public double PlaneAngleToRadiansScale { get; }

    /// <summary>Millimetres per length unit declared by the source file; entity lengths have already been multiplied by this.</summary>
    public double SourceMillimetresPerUnit { get; }

    /// <summary>Distance accuracy the source file declares for its geometry, in millimetres, or null when it declares none.</summary>
    public double? SourceDistanceAccuracyMillimetres { get; }

    /// <summary>Engineering budget for recovered geometry after source lengths have been normalized to millimetres.</summary>
    public double RecoveryToleranceMillimetres { get; set; } = 0.1d;

    /// <summary>Base edge-to-lift budget, separately configurable from geometry recovery.</summary>
    public double PcurveQualificationToleranceMillimetres { get; set; } = 1e-3d;

    /// <summary>Explicit compatibility policy for bounded crossings of a declared planar inner loop.</summary>
    public bool AllowBoundedNearCoincidentInnerLoop { get; set; }

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

    private double ResolvePlaneAngleToRadiansScale()
    {
        foreach (var entity in Entities)
        {
            var unitContext = Step242SubsetDecoder.TryGetConstructor(entity.Instance, "GLOBAL_UNIT_ASSIGNED_CONTEXT");
            if (unitContext is null || unitContext.Arguments.Count == 0 || unitContext.Arguments[0] is not Step242ListValue unitList)
            {
                continue;
            }

            foreach (var unitValue in unitList.Items)
            {
                if (unitValue is not Step242EntityReference unitRef)
                {
                    continue;
                }

                if (TryResolvePlaneAngleUnitScale(unitRef.TargetId, new HashSet<int>(), out var scale))
                {
                    return scale;
                }
            }
        }

        return 1d;
    }

    internal bool TryResolvePlaneAngleUnitScale(int unitEntityId, HashSet<int> visited, out double scale)
    {
        scale = default;
        if (!visited.Add(unitEntityId) || !_entitiesById.TryGetValue(unitEntityId, out var unitEntity))
        {
            return false;
        }

        if (Step242SubsetDecoder.TryGetConstructor(unitEntity.Instance, "PLANE_ANGLE_UNIT") is null)
        {
            return false;
        }

        var siUnit = Step242SubsetDecoder.TryGetConstructor(unitEntity.Instance, "SI_UNIT");
        if (siUnit is not null
            && siUnit.Arguments.Count >= 2
            && siUnit.Arguments[1] is Step242EnumValue siName
            && string.Equals(siName.Value, "RADIAN", StringComparison.Ordinal))
        {
            scale = 1d;
            return true;
        }

        var conversionBasedUnit = Step242SubsetDecoder.TryGetConstructor(unitEntity.Instance, "CONVERSION_BASED_UNIT");
        if (conversionBasedUnit is null
            || conversionBasedUnit.Arguments.Count < 2
            || conversionBasedUnit.Arguments[1] is not Step242EntityReference conversionRef
            || !_entitiesById.TryGetValue(conversionRef.TargetId, out var conversionEntity))
        {
            return false;
        }

        var conversionConstructor = conversionEntity.Instance.PrimaryConstructor;
        if (conversionConstructor.Arguments.Count < 2)
        {
            return false;
        }

        if (conversionConstructor.Arguments[0] is not Step242TypedValue typedMeasure
            || !string.Equals(typedMeasure.Name, "PLANE_ANGLE_MEASURE", StringComparison.Ordinal)
            || typedMeasure.Arguments.Count != 1
            || typedMeasure.Arguments[0] is not Step242NumberValue baseMagnitude
            || conversionConstructor.Arguments[1] is not Step242EntityReference baseUnitRef)
        {
            return false;
        }

        if (!TryResolvePlaneAngleUnitScale(baseUnitRef.TargetId, visited, out var baseScale))
        {
            return false;
        }

        scale = baseMagnitude.Value * baseScale;
        return true;
    }
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

/// <summary>A binary literal's hexadecimal digits; the first digit counts the unused leading bits.</summary>
internal sealed record Step242BinaryValue(string Digits) : Step242Value;

/// <summary>What the file says about its own structure, beyond its entity instances.</summary>
internal sealed class Step242FileStructure
{
    public bool SawStart { get; set; }
    public bool SawHeader { get; set; }
    public bool SawEnd { get; set; }
    public int DataSections { get; set; }
    public int ClosedDataSections { get; set; }
    public int UserDefinedEntities { get; set; }
    public List<string> Schemas { get; } = [];
    public List<string> Edition3Sections { get; } = [];

    private static readonly string[] KnownSchemaPrefixes =
    [
        "AP242_MANAGED_MODEL_BASED_3D_ENGINEERING", "AP203_CONFIGURATION_CONTROLLED_3D_DESIGN", "CONFIG_CONTROL_DESIGN",
        "AUTOMOTIVE_DESIGN", "AP214", "AP242", "AP203",
    ];

    public static bool IsKnownSchema(string schema) => KnownSchemaPrefixes.Any(prefix => schema.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// ISO 10303-21 §6.4.3 string control directives: '\\' a backslash, '\S\c' the ISO 8859-1 character above c,
/// '\X\hh' one ISO 8859-1 character, '\X2\…\X0\' UTF-16 code units, '\X4\…\X0\' code points, '\Px\' a code
/// page switch (pages other than A are read as A). A malformed directive is kept as written.
/// </summary>
internal static class Step242StringDecoding
{
    public static string Decode(string raw)
    {
        if (raw.IndexOf('\\') < 0) return raw;
        var output = new System.Text.StringBuilder(raw.Length);
        var i = 0;
        while (i < raw.Length)
        {
            var c = raw[i];
            if (c != '\\') { output.Append(c); i++; continue; }
            if (At(raw, i, "\\\\")) { output.Append('\\'); i += 2; continue; }
            if (At(raw, i, "\\S\\") && i + 3 < raw.Length) { output.Append((char)(raw[i + 3] + 128)); i += 4; continue; }
            if (raw.Length > i + 3 && raw[i + 1] == 'P' && raw[i + 3] == '\\' && raw[i + 2] is >= 'A' and <= 'I') { i += 4; continue; }
            if (At(raw, i, "\\X\\") && i + 5 <= raw.Length && TryHex(raw, i + 3, 2, out var latin)) { output.Append((char)latin); i += 5; continue; }
            if (At(raw, i, "\\X2\\") || At(raw, i, "\\X4\\"))
            {
                var width = raw[i + 2] == '2' ? 4 : 8;
                var end = raw.IndexOf("\\X0\\", i + 4, StringComparison.Ordinal);
                var body = end < 0 ? null : raw[(i + 4)..end];
                if (body is not null && body.Length % width == 0 && body.All(Uri.IsHexDigit))
                {
                    for (var k = 0; k < body.Length; k += width)
                    {
                        var code = int.Parse(body.AsSpan(k, width), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
                        if (width == 4) output.Append((char)code);
                        else if (code is >= 0 and <= 0x10FFFF and not (>= 0xD800 and <= 0xDFFF)) output.Append(char.ConvertFromUtf32(code));
                    }
                    i = end + 4;
                    continue;
                }
            }
            output.Append(c); i++;
        }
        return output.ToString();
    }

    private static bool At(string raw, int index, string token) => string.CompareOrdinal(raw, index, token, 0, token.Length) == 0;

    private static bool TryHex(string raw, int index, int count, out int value)
        => int.TryParse(raw.AsSpan(index, count), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out value);
}

internal sealed record Step242StringValue(string Value) : Step242Value;

internal sealed record Step242NumberValue(double Value) : Step242Value;

internal sealed record Step242BooleanValue(bool Value) : Step242Value;

internal sealed record Step242EnumValue(string Value) : Step242Value;

internal sealed record Step242ListValue(IReadOnlyList<Step242Value> Items) : Step242Value;

internal sealed record Step242TypedValue(string Name, IReadOnlyList<Step242Value> Arguments) : Step242Value;

internal sealed record Step242OmittedValue : Step242Value
{
    public bool IsDerived { get; init; }
    public static Step242OmittedValue Instance { get; } = new();
    public static Step242OmittedValue Derived { get; } = new() { IsDerived = true };
}
