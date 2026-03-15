using System.Text.Json;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament.Parsing;

internal static class FirmamentTopLevelParser
{
    public static KernelResult<FirmamentParsedDocument> Parse(string sourceText)
    {
        ArgumentNullException.ThrowIfNull(sourceText);

        if (TryParseJson(sourceText, out var jsonRootResult))
        {
            return ParseFromRoot(jsonRootResult);
        }

        var toonResult = ParseToonTopLevel(sourceText);
        if (!toonResult.IsSuccess)
        {
            return KernelResult<FirmamentParsedDocument>.Failure(toonResult.Diagnostics);
        }

        return ParseFromToon(toonResult.Value);
    }

    private static bool TryParseJson(string sourceText, out JsonElement root)
    {
        root = default;
        try
        {
            using var document = JsonDocument.Parse(sourceText);
            root = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static KernelResult<FirmamentParsedDocument> ParseFromRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return KernelResult<FirmamentParsedDocument>.Failure(
            [
                CreateDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    FirmamentDiagnosticCodes.StructureInvalidSectionShape,
                    "Top-level Firmament document must be an object.")
            ]);
        }

        var unknownSection = root
            .EnumerateObject()
            .Select(p => p.Name)
            .Where(name => !KnownTopLevelSections.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .FirstOrDefault();

        if (unknownSection is not null)
        {
            return KernelResult<FirmamentParsedDocument>.Failure(
            [
                CreateDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    FirmamentDiagnosticCodes.StructureUnknownTopLevelSection,
                    $"Unknown top-level section '{unknownSection}'.")
            ]);
        }

        if (!root.TryGetProperty("firmament", out var firmamentSection))
        {
            return MissingSection("firmament");
        }

        if (!root.TryGetProperty("model", out var modelSection))
        {
            return MissingSection("model");
        }

        if (!root.TryGetProperty("ops", out var opsSection))
        {
            return MissingSection("ops");
        }

        if (firmamentSection.ValueKind != JsonValueKind.Object)
        {
            return InvalidShape("firmament", "object");
        }

        if (modelSection.ValueKind != JsonValueKind.Object)
        {
            return InvalidShape("model", "object");
        }

        if (opsSection.ValueKind != JsonValueKind.Array)
        {
            return InvalidShape("ops", "array");
        }

        if (!firmamentSection.TryGetProperty("version", out var versionElement))
        {
            return MissingField("firmament", "version");
        }

        if (!modelSection.TryGetProperty("name", out var nameElement))
        {
            return MissingField("model", "name");
        }

        if (!modelSection.TryGetProperty("units", out var unitsElement))
        {
            return MissingField("model", "units");
        }

        var parsedOpsResult = ParseJsonOpsEntries(opsSection);
        if (!parsedOpsResult.IsSuccess)
        {
            return KernelResult<FirmamentParsedDocument>.Failure(parsedOpsResult.Diagnostics);
        }

        return KernelResult<FirmamentParsedDocument>.Success(
            new FirmamentParsedDocument(
                new FirmamentParsedHeader(versionElement.ToString()),
                new FirmamentParsedModelHeader(nameElement.ToString(), unitsElement.ToString()),
                new FirmamentParsedOpsSection(parsedOpsResult.Value),
                HasSchema: root.TryGetProperty("schema", out _),
                HasPmi: root.TryGetProperty("pmi", out _)));
    }

    private static KernelResult<FirmamentParsedDocument> ParseFromToon(FirmamentToonTopLevel toon)
    {
        var unknownSection = toon.Sections.Keys
            .Where(name => !KnownTopLevelSections.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .FirstOrDefault();

        if (unknownSection is not null)
        {
            return KernelResult<FirmamentParsedDocument>.Failure(
            [
                CreateDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    FirmamentDiagnosticCodes.StructureUnknownTopLevelSection,
                    $"Unknown top-level section '{unknownSection}'.")
            ]);
        }

        if (!toon.Sections.TryGetValue("firmament", out var firmamentSection))
        {
            return MissingSection("firmament");
        }

        if (!toon.Sections.TryGetValue("model", out var modelSection))
        {
            return MissingSection("model");
        }

        if (!toon.Sections.TryGetValue("ops", out var opsSection))
        {
            return MissingSection("ops");
        }

        if (!firmamentSection.IsObjectLike)
        {
            return InvalidShape("firmament", "object");
        }

        if (!modelSection.IsObjectLike)
        {
            return InvalidShape("model", "object");
        }

        if (!opsSection.IsArrayLike)
        {
            return InvalidShape("ops", "array");
        }

        if (!firmamentSection.Fields.TryGetValue("version", out var version))
        {
            return MissingField("firmament", "version");
        }

        if (!modelSection.Fields.TryGetValue("name", out var name))
        {
            return MissingField("model", "name");
        }

        if (!modelSection.Fields.TryGetValue("units", out var units))
        {
            return MissingField("model", "units");
        }

        var parsedOpsResult = ParseToonOpsEntries(opsSection);
        if (!parsedOpsResult.IsSuccess)
        {
            return KernelResult<FirmamentParsedDocument>.Failure(parsedOpsResult.Diagnostics);
        }

        return KernelResult<FirmamentParsedDocument>.Success(
            new FirmamentParsedDocument(
                new FirmamentParsedHeader(version),
                new FirmamentParsedModelHeader(name, units),
                new FirmamentParsedOpsSection(parsedOpsResult.Value),
                HasSchema: toon.Sections.ContainsKey("schema"),
                HasPmi: toon.Sections.ContainsKey("pmi")));
    }

    private static KernelResult<IReadOnlyList<FirmamentParsedOpEntry>> ParseJsonOpsEntries(JsonElement opsSection)
    {
        var entries = new List<FirmamentParsedOpEntry>();
        var index = 0;
        foreach (var opElement in opsSection.EnumerateArray())
        {
            if (opElement.ValueKind != JsonValueKind.Object)
            {
                return InvalidOpsEntryShape(index);
            }

            if (!opElement.TryGetProperty("op", out var opNameElement))
            {
                return MissingOpField(index);
            }

            if (!IsValidOpScalar(opNameElement))
            {
                return InvalidOpFieldValue(index);
            }

            var opName = opNameElement.ToString();

            var rawFields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in opElement.EnumerateObject())
            {
                rawFields[property.Name] = property.Value.ToString();
            }

            if (!FirmamentKnownOpKinds.TryParse(opName, out var knownKind))
            {
                return UnknownOpKind(index, opName);
            }

            var family = FirmamentKnownOpKinds.ClassifyFamily(knownKind);
            entries.Add(new FirmamentParsedOpEntry(opName, knownKind, family, rawFields));
            index++;
        }

        return KernelResult<IReadOnlyList<FirmamentParsedOpEntry>>.Success(entries);
    }

    private static KernelResult<IReadOnlyList<FirmamentParsedOpEntry>> ParseToonOpsEntries(FirmamentToonSection opsSection)
    {
        var entries = new List<FirmamentParsedOpEntry>();
        for (var index = 0; index < opsSection.ArrayEntries.Count; index++)
        {
            var opEntry = opsSection.ArrayEntries[index];
            if (!opEntry.IsObjectLike)
            {
                return InvalidOpsEntryShape(index);
            }

            if (!opEntry.Fields.TryGetValue("op", out var opName))
            {
                return MissingOpField(index);
            }

            if (string.IsNullOrWhiteSpace(opName))
            {
                return InvalidOpFieldValue(index);
            }

            if (!FirmamentKnownOpKinds.TryParse(opName, out var knownKind))
            {
                return UnknownOpKind(index, opName);
            }

            var family = FirmamentKnownOpKinds.ClassifyFamily(knownKind);
            entries.Add(new FirmamentParsedOpEntry(opName, knownKind, family, new Dictionary<string, string>(opEntry.Fields, StringComparer.Ordinal)));
        }

        return KernelResult<IReadOnlyList<FirmamentParsedOpEntry>>.Success(entries);
    }

    private static KernelResult<FirmamentToonTopLevel> ParseToonTopLevel(string sourceText)
    {
        var sections = new Dictionary<string, FirmamentToonSection>(StringComparer.Ordinal);
        string? currentObjectSection = null;
        FirmamentToonObjectEntry? currentArrayObjectEntry = null;
        FirmamentToonSection? currentArraySection = null;

        var lines = sourceText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal).Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var rawLine = lines[index];
            var line = rawLine.TrimEnd();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (char.IsWhiteSpace(rawLine[0]))
            {
                var field = line.Trim();

                if (currentArraySection is not null)
                {
                    if (field == "-")
                    {
                        currentObjectSection = null;
                        currentArrayObjectEntry = new FirmamentToonObjectEntry();
                        currentArraySection.ArrayEntries.Add(currentArrayObjectEntry);
                        continue;
                    }

                    if (field.StartsWith("- ", StringComparison.Ordinal))
                    {
                        currentObjectSection = null;
                        currentArrayObjectEntry = null;
                        currentArraySection.ArrayEntries.Add(new FirmamentToonObjectEntry { IsObjectLike = false });
                        continue;
                    }

                    if (currentArrayObjectEntry is null)
                    {
                        return InvalidToonSyntax();
                    }

                    var arraySeparator = field.IndexOf(':');
                    if (arraySeparator <= 0)
                    {
                        return InvalidToonSyntax();
                    }

                    var arrayFieldName = field[..arraySeparator].Trim();
                    var arrayFieldValue = field[(arraySeparator + 1)..].Trim();
                    if (arrayFieldName.Length == 0)
                    {
                        return InvalidToonSyntax();
                    }

                    currentArrayObjectEntry.Fields[arrayFieldName] = arrayFieldValue;
                    continue;
                }

                if (currentObjectSection is null)
                {
                    return InvalidToonSyntax();
                }

                var separator = field.IndexOf(':');
                if (separator <= 0)
                {
                    return InvalidToonSyntax();
                }

                var fieldName = field[..separator].Trim();
                var fieldValue = field[(separator + 1)..].Trim();
                if (fieldName.Length == 0 || fieldValue.Length == 0)
                {
                    return InvalidToonSyntax();
                }

                sections[currentObjectSection].Fields[fieldName] = fieldValue;
                continue;
            }

            currentObjectSection = null;
            currentArrayObjectEntry = null;
            currentArraySection = null;

            var header = line.Trim();
            if (!header.EndsWith(":", StringComparison.Ordinal))
            {
                return InvalidToonSyntax();
            }

            header = header[..^1].Trim();
            if (header.Length == 0)
            {
                return InvalidToonSyntax();
            }

            if (TryParseArrayHeader(header, out var arraySectionName))
            {
                var arraySection = FirmamentToonSection.ArrayLike();
                sections[arraySectionName] = arraySection;
                currentArraySection = arraySection;
                continue;
            }

            if (sections.ContainsKey(header))
            {
                return InvalidToonSyntax();
            }

            sections[header] = FirmamentToonSection.ObjectLike();
            currentObjectSection = header;
        }

        return KernelResult<FirmamentToonTopLevel>.Success(new FirmamentToonTopLevel(sections));
    }

    private static bool TryParseArrayHeader(string header, out string sectionName)
    {
        sectionName = string.Empty;

        var bracketStart = header.IndexOf('[');
        var bracketEnd = header.LastIndexOf(']');
        if (bracketStart <= 0 || bracketEnd != header.Length - 1)
        {
            return false;
        }

        sectionName = header[..bracketStart].Trim();
        var countText = header[(bracketStart + 1)..bracketEnd].Trim();
        return sectionName.Length > 0
            && int.TryParse(countText, out _);
    }

    private static bool IsValidOpScalar(JsonElement opNameElement)
    {
        return opNameElement.ValueKind switch
        {
            JsonValueKind.String => !string.IsNullOrWhiteSpace(opNameElement.GetString()),
            JsonValueKind.Number => true,
            JsonValueKind.True => true,
            JsonValueKind.False => true,
            _ => false
        };
    }

    private static KernelResult<FirmamentToonTopLevel> InvalidToonSyntax() =>
        KernelResult<FirmamentToonTopLevel>.Failure(
        [
            CreateDiagnostic(
                KernelDiagnosticCode.InvalidArgument,
                FirmamentDiagnosticCodes.ParseInvalidDocumentSyntax,
                "Firmament source must be valid canonical TOON-style text or JSON with an object root.")
        ]);

    private static readonly HashSet<string> KnownTopLevelSections =
    [
        "firmament",
        "model",
        "ops",
        "schema",
        "pmi"
    ];

    private static KernelResult<FirmamentParsedDocument> MissingSection(string sectionName) =>
        KernelResult<FirmamentParsedDocument>.Failure(
        [
            CreateDiagnostic(
                KernelDiagnosticCode.ValidationFailed,
                FirmamentDiagnosticCodes.StructureMissingRequiredSection,
                $"Missing required top-level section '{sectionName}'.")
        ]);

    private static KernelResult<FirmamentParsedDocument> MissingField(string sectionName, string fieldName) =>
        KernelResult<FirmamentParsedDocument>.Failure(
        [
            CreateDiagnostic(
                KernelDiagnosticCode.ValidationFailed,
                FirmamentDiagnosticCodes.StructureMissingRequiredField,
                $"Missing required field '{fieldName}' in section '{sectionName}'.")
        ]);

    private static KernelResult<FirmamentParsedDocument> InvalidShape(string sectionName, string expectedShape) =>
        KernelResult<FirmamentParsedDocument>.Failure(
        [
            CreateDiagnostic(
                KernelDiagnosticCode.ValidationFailed,
                FirmamentDiagnosticCodes.StructureInvalidSectionShape,
                $"Section '{sectionName}' must be a {expectedShape}.")
        ]);

    private static KernelResult<IReadOnlyList<FirmamentParsedOpEntry>> InvalidOpsEntryShape(int index) =>
        KernelResult<IReadOnlyList<FirmamentParsedOpEntry>>.Failure(
        [
            CreateDiagnostic(
                KernelDiagnosticCode.ValidationFailed,
                FirmamentDiagnosticCodes.StructureInvalidOpsEntryShape,
                $"Operation entry at index {index} must be an object with fields.")
        ]);

    private static KernelResult<IReadOnlyList<FirmamentParsedOpEntry>> MissingOpField(int index) =>
        KernelResult<IReadOnlyList<FirmamentParsedOpEntry>>.Failure(
        [
            CreateDiagnostic(
                KernelDiagnosticCode.ValidationFailed,
                FirmamentDiagnosticCodes.StructureMissingRequiredOpField,
                $"Operation entry at index {index} is missing required field 'op'.")
        ]);

    private static KernelResult<IReadOnlyList<FirmamentParsedOpEntry>> InvalidOpFieldValue(int index) =>
        KernelResult<IReadOnlyList<FirmamentParsedOpEntry>>.Failure(
        [
            CreateDiagnostic(
                KernelDiagnosticCode.ValidationFailed,
                FirmamentDiagnosticCodes.StructureInvalidOpFieldValue,
                $"Operation entry at index {index} has invalid 'op' value; expected a non-empty scalar.")
        ]);

    private static KernelResult<IReadOnlyList<FirmamentParsedOpEntry>> UnknownOpKind(int index, string opName) =>
        KernelResult<IReadOnlyList<FirmamentParsedOpEntry>>.Failure(
        [
            CreateDiagnostic(
                KernelDiagnosticCode.ValidationFailed,
                FirmamentDiagnosticCodes.StructureUnknownOpKind,
                $"Operation entry at index {index} has unknown op kind '{opName}'.")
        ]);

    private static KernelDiagnostic CreateDiagnostic(KernelDiagnosticCode kernelCode, FirmamentDiagnosticCode firmamentCode, string message) =>
        new(
            kernelCode,
            KernelDiagnosticSeverity.Error,
            $"[{firmamentCode.Value}] {message}",
            Source: FirmamentDiagnosticConventions.Source);

    private sealed record FirmamentToonTopLevel(IReadOnlyDictionary<string, FirmamentToonSection> Sections);

    private sealed record FirmamentToonSection(bool IsObjectLike, bool IsArrayLike, Dictionary<string, string> Fields, List<FirmamentToonObjectEntry> ArrayEntries)
    {
        public static FirmamentToonSection ObjectLike() => new(true, false, new Dictionary<string, string>(StringComparer.Ordinal), []);

        public static FirmamentToonSection ArrayLike() => new(false, true, new Dictionary<string, string>(StringComparer.Ordinal), []);
    }

    private sealed class FirmamentToonObjectEntry
    {
        public bool IsObjectLike { get; set; } = true;

        public Dictionary<string, string> Fields { get; } = new(StringComparer.Ordinal);
    }
}
