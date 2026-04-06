using System.Globalization;
using System.Linq;
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

        var parsedSchema = ParseSchemaFromJsonRoot(root);
        var parsedPmiResult = ParsePmiFromJsonRoot(root);
        if (!parsedPmiResult.IsSuccess)
        {
            return KernelResult<FirmamentParsedDocument>.Failure(parsedPmiResult.Diagnostics);
        }

        return KernelResult<FirmamentParsedDocument>.Success(
            new FirmamentParsedDocument(
                new FirmamentParsedHeader(versionElement.ToString()),
                new FirmamentParsedModelHeader(nameElement.ToString(), unitsElement.ToString()),
                new FirmamentParsedOpsSection(parsedOpsResult.Value),
                Schema: parsedSchema,
                Pmi: parsedPmiResult.Value));
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

        var parsedSchema = ParseSchemaFromToonSections(toon.Sections);
        var parsedPmiResult = ParsePmiFromToonSections(toon.Sections);
        if (!parsedPmiResult.IsSuccess)
        {
            return KernelResult<FirmamentParsedDocument>.Failure(parsedPmiResult.Diagnostics);
        }

        return KernelResult<FirmamentParsedDocument>.Success(
            new FirmamentParsedDocument(
                new FirmamentParsedHeader(version),
                new FirmamentParsedModelHeader(name, units),
                new FirmamentParsedOpsSection(parsedOpsResult.Value),
                Schema: parsedSchema,
                Pmi: parsedPmiResult.Value));
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
            var placement = ParsePlacementFromJson(opElement);
            entries.Add(new FirmamentParsedOpEntry(opName, knownKind, family, rawFields, placement));
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
            var placement = ParsePlacementFromToon(opEntry);
            entries.Add(new FirmamentParsedOpEntry(opName, knownKind, family, new Dictionary<string, string>(opEntry.Fields, StringComparer.Ordinal), placement));
        }

        return KernelResult<IReadOnlyList<FirmamentParsedOpEntry>>.Success(entries);
    }

    private static KernelResult<FirmamentToonTopLevel> ParseToonTopLevel(string sourceText)
    {
        var sections = new Dictionary<string, FirmamentToonSection>(StringComparer.Ordinal);
        string? currentObjectSection = null;
        FirmamentToonObjectEntry? currentArrayObjectEntry = null;
        FirmamentToonSection? currentArraySection = null;
        string? currentNestedFieldName = null;
        int currentNestedFieldIndent = -1;
        var currentNestedLines = new List<string>();
        Action<string, string>? currentNestedFieldWriter = null;

        static string CollapseNestedFieldValue(IReadOnlyList<string> nestedLines)
        {
            var nonEmptyLines = nestedLines.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Trim()).ToList();
            if (nonEmptyLines.Count == 0)
            {
                return string.Empty;
            }

            if (nonEmptyLines.Any(line => line.Contains(':', StringComparison.Ordinal)))
            {
                var fields = new List<string>();
                string? currentKey = null;
                string? currentScalarValue = null;
                var currentArrayValues = new List<string>();

                void FlushCurrent()
                {
                    if (string.IsNullOrWhiteSpace(currentKey))
                    {
                        return;
                    }

                    if (currentArrayValues.Count > 0)
                    {
                        fields.Add($"{currentKey}: [{string.Join(", ", currentArrayValues)}]");
                    }
                    else
                    {
                        fields.Add($"{currentKey}: {currentScalarValue}");
                    }
                }

                foreach (var line in nonEmptyLines)
                {
                    var separator = line.IndexOf(':');
                    if (separator > 0)
                    {
                        FlushCurrent();
                        currentKey = line[..separator].Trim();
                        currentScalarValue = line[(separator + 1)..].Trim();
                        currentArrayValues.Clear();
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(currentKey))
                    {
                        currentArrayValues.Add(line);
                    }
                }

                FlushCurrent();
                return "{ " + string.Join(", ", fields) + " }";
            }

            return "[" + string.Join(", ", nonEmptyLines) + "]";
        }

        void FlushNestedFieldIfNeeded()
        {
            if (currentNestedFieldWriter is null || string.IsNullOrWhiteSpace(currentNestedFieldName))
            {
                currentNestedFieldName = null;
                currentNestedFieldIndent = -1;
                currentNestedLines.Clear();
                currentNestedFieldWriter = null;
                return;
            }

            currentNestedFieldWriter(currentNestedFieldName, CollapseNestedFieldValue(currentNestedLines));
            currentNestedFieldName = null;
            currentNestedFieldIndent = -1;
            currentNestedLines.Clear();
            currentNestedFieldWriter = null;
        }

        var lines = sourceText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal).Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var rawLine = lines[index];
            var line = rawLine.TrimEnd();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var indentation = rawLine.Length - rawLine.TrimStart().Length;

            if (char.IsWhiteSpace(rawLine[0]))
            {
                var field = line.Trim();

                if (!string.IsNullOrWhiteSpace(currentNestedFieldName) && indentation <= currentNestedFieldIndent)
                {
                    FlushNestedFieldIfNeeded();
                }

                if (!string.IsNullOrWhiteSpace(currentNestedFieldName) && indentation > currentNestedFieldIndent)
                {
                    currentNestedLines.Add(field);
                    continue;
                }

                if (currentArraySection is not null)
                {

                    if (field == "-")
                    {
                        FlushNestedFieldIfNeeded();
                        currentObjectSection = null;
                        currentArrayObjectEntry = new FirmamentToonObjectEntry();
                        currentArraySection.ArrayEntries.Add(currentArrayObjectEntry);
                        continue;
                    }

                    if (field.StartsWith("- ", StringComparison.Ordinal))
                    {
                        FlushNestedFieldIfNeeded();
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
                    var normalizedArrayFieldName = TryParseArrayHeader(arrayFieldName, out var normalizedSectionName)
                        ? normalizedSectionName
                        : arrayFieldName;
                    var arrayFieldValue = field[(arraySeparator + 1)..].Trim();
                    if (normalizedArrayFieldName.Length == 0)
                    {
                        return InvalidToonSyntax();
                    }

                    if (arrayFieldValue.Length == 0)
                    {
                        currentNestedFieldName = normalizedArrayFieldName;
                        currentNestedFieldIndent = indentation;
                        currentNestedLines.Clear();
                        currentNestedFieldWriter = (key, value) => currentArrayObjectEntry.Fields[key] = value;
                        continue;
                    }

                    currentArrayObjectEntry.Fields[normalizedArrayFieldName] = arrayFieldValue;
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
                if (fieldName.Length == 0)
                {
                    return InvalidToonSyntax();
                }

                if (fieldValue.Length == 0)
                {
                    var objectSectionName = currentObjectSection;
                    currentNestedFieldName = fieldName;
                    currentNestedFieldIndent = indentation;
                    currentNestedLines.Clear();
                    currentNestedFieldWriter = (key, value) => sections[objectSectionName].Fields[key] = value;
                    continue;
                }

                sections[currentObjectSection].Fields[fieldName] = fieldValue;
                continue;
            }

            FlushNestedFieldIfNeeded();
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

        FlushNestedFieldIfNeeded();
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

    private static FirmamentParsedSchema? ParseSchemaFromJsonRoot(JsonElement root)
    {
        if (!root.TryGetProperty("schema", out var schemaElement))
        {
            return null;
        }

        if (schemaElement.ValueKind != JsonValueKind.Object)
        {
            return new FirmamentParsedSchema(false, null, FirmamentParsedSchemaProcess.Unknown);
        }

        schemaElement.TryGetProperty("process", out var processElement);
        var processRaw = processElement.ToString();
        var process = ParseSchemaProcess(processRaw);

        schemaElement.TryGetProperty("minimum_tool_radius", out var minimumToolRadiusElement);
        var minimumToolRadiusRaw = minimumToolRadiusElement.ToString();
        double? minimumToolRadius = null;
        if (minimumToolRadiusElement.ValueKind == JsonValueKind.Number)
        {
            minimumToolRadius = minimumToolRadiusElement.GetDouble();
        }
        schemaElement.TryGetProperty("minimum_wall_thickness", out var minimumWallThicknessElement);
        var minimumWallThicknessRaw = minimumWallThicknessElement.ToString();
        double? minimumWallThickness = null;
        if (minimumWallThicknessElement.ValueKind == JsonValueKind.Number)
        {
            minimumWallThickness = minimumWallThicknessElement.GetDouble();
        }

        string? partingPlane = null;
        if (schemaElement.TryGetProperty("parting_plane", out var partingPlaneElement))
        {
            partingPlane = partingPlaneElement.ToString();
        }

        var hasGateLocation = schemaElement.TryGetProperty("gate_location", out var gateLocationElement);
        var gateLocationIsObjectLike = hasGateLocation && gateLocationElement.ValueKind == JsonValueKind.Object;
        FirmamentParsedSchemaGateLocation? gateLocation = null;
        if (gateLocationIsObjectLike)
        {
            gateLocationElement.TryGetProperty("x", out var xElement);
            gateLocationElement.TryGetProperty("y", out var yElement);
            gateLocationElement.TryGetProperty("z", out var zElement);
            gateLocation = new FirmamentParsedSchemaGateLocation(xElement.ToString(), yElement.ToString(), zElement.ToString());
        }

        schemaElement.TryGetProperty("draft_angle", out var draftAngleElement);
        var draftAngleRaw = draftAngleElement.ToString();
        double? draftAngle = null;
        if (draftAngleElement.ValueKind == JsonValueKind.Number)
        {
            draftAngle = draftAngleElement.GetDouble();
        }

        schemaElement.TryGetProperty("printer_resolution", out var printerResolutionElement);
        var printerResolutionRaw = printerResolutionElement.ToString();
        double? printerResolution = null;
        if (printerResolutionElement.ValueKind == JsonValueKind.Number)
        {
            printerResolution = printerResolutionElement.GetDouble();
        }

        return new FirmamentParsedSchema(true, processRaw, process, minimumToolRadiusRaw, minimumToolRadius, minimumWallThicknessRaw, minimumWallThickness, partingPlane, hasGateLocation, gateLocationIsObjectLike, gateLocation, draftAngleRaw, draftAngle, printerResolutionRaw, printerResolution);
    }

    private static FirmamentParsedSchema? ParseSchemaFromToonSections(IReadOnlyDictionary<string, FirmamentToonSection> sections)
    {
        if (!sections.TryGetValue("schema", out var schemaSection))
        {
            return null;
        }

        if (!schemaSection.IsObjectLike)
        {
            return new FirmamentParsedSchema(false, null, FirmamentParsedSchemaProcess.Unknown);
        }

        schemaSection.Fields.TryGetValue("process", out var processRaw);
        var process = ParseSchemaProcess(processRaw);

        schemaSection.Fields.TryGetValue("minimum_tool_radius", out var minimumToolRadiusRaw);
        double? minimumToolRadius = null;
        if (!string.IsNullOrWhiteSpace(minimumToolRadiusRaw)
            && TryParseNumeric(minimumToolRadiusRaw, out var minimumToolRadiusValue))
        {
            minimumToolRadius = minimumToolRadiusValue;
        }
        schemaSection.Fields.TryGetValue("minimum_wall_thickness", out var minimumWallThicknessRaw);
        double? minimumWallThickness = null;
        if (!string.IsNullOrWhiteSpace(minimumWallThicknessRaw)
            && TryParseNumeric(minimumWallThicknessRaw, out var minimumWallThicknessValue))
        {
            minimumWallThickness = minimumWallThicknessValue;
        }

        schemaSection.Fields.TryGetValue("parting_plane", out var partingPlane);

        var hasGateLocation = schemaSection.Fields.TryGetValue("gate_location", out var gateLocationRaw);
        var gateLocationIsObjectLike = false;
        var gateLocationFields = new Dictionary<string, string>(StringComparer.Ordinal);
        if (hasGateLocation && TryParseStructuredFields(gateLocationRaw!, out var parsedGateLocationFields))
        {
            gateLocationIsObjectLike = true;
            gateLocationFields = new Dictionary<string, string>(parsedGateLocationFields, StringComparer.Ordinal);
        }

        FirmamentParsedSchemaGateLocation? gateLocation = null;
        if (gateLocationIsObjectLike)
        {
            gateLocationFields.TryGetValue("x", out var xRaw);
            gateLocationFields.TryGetValue("y", out var yRaw);
            gateLocationFields.TryGetValue("z", out var zRaw);
            gateLocation = new FirmamentParsedSchemaGateLocation(xRaw, yRaw, zRaw);
        }

        schemaSection.Fields.TryGetValue("draft_angle", out var draftAngleRaw);
        double? draftAngle = null;
        if (!string.IsNullOrWhiteSpace(draftAngleRaw)
            && TryParseNumeric(draftAngleRaw, out var draftAngleValue))
        {
            draftAngle = draftAngleValue;
        }

        schemaSection.Fields.TryGetValue("printer_resolution", out var printerResolutionRaw);
        double? printerResolution = null;
        if (!string.IsNullOrWhiteSpace(printerResolutionRaw)
            && TryParseNumeric(printerResolutionRaw, out var printerResolutionValue))
        {
            printerResolution = printerResolutionValue;
        }

        return new FirmamentParsedSchema(true, processRaw, process, minimumToolRadiusRaw, minimumToolRadius, minimumWallThicknessRaw, minimumWallThickness, partingPlane, hasGateLocation, gateLocationIsObjectLike, gateLocation, draftAngleRaw, draftAngle, printerResolutionRaw, printerResolution);
    }

    private static FirmamentParsedSchemaProcess ParseSchemaProcess(string? raw)
    {
        return raw?.Trim() switch
        {
            "cnc" => FirmamentParsedSchemaProcess.Cnc,
            "injection_molded" => FirmamentParsedSchemaProcess.InjectionMolded,
            "additive" => FirmamentParsedSchemaProcess.Additive,
            _ => FirmamentParsedSchemaProcess.Unknown
        };
    }

    private static KernelResult<FirmamentParsedPmiSection?> ParsePmiFromJsonRoot(JsonElement root)
    {
        if (!root.TryGetProperty("pmi", out var pmiSection))
        {
            return KernelResult<FirmamentParsedPmiSection?>.Success(null);
        }

        if (pmiSection.ValueKind != JsonValueKind.Array)
        {
            return KernelResult<FirmamentParsedPmiSection?>.Failure([
                CreateDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    FirmamentDiagnosticCodes.StructureInvalidSectionShape,
                    "Section 'pmi' must be an array.")
            ]);
        }

        var parseEntries = ParsePmiJsonEntries(pmiSection);
        if (!parseEntries.IsSuccess)
        {
            return KernelResult<FirmamentParsedPmiSection?>.Failure(parseEntries.Diagnostics);
        }

        return KernelResult<FirmamentParsedPmiSection?>.Success(
            new FirmamentParsedPmiSection(true, parseEntries.Value));
    }

    private static KernelResult<IReadOnlyList<FirmamentParsedPmiEntry>> ParsePmiJsonEntries(JsonElement pmiSection)
    {
        var entries = new List<FirmamentParsedPmiEntry>();
        var index = 0;
        foreach (var pmiElement in pmiSection.EnumerateArray())
        {
            if (pmiElement.ValueKind != JsonValueKind.Object)
            {
                return InvalidPmiEntryShape(index);
            }

            if (!pmiElement.TryGetProperty("kind", out var kindElement))
            {
                return MissingPmiField(index, "kind");
            }

            var kindRaw = kindElement.ToString().Trim();
            if (string.IsNullOrWhiteSpace(kindRaw))
            {
                return InvalidPmiFieldValue(index, "kind", "expected non-empty scalar.");
            }

            var rawFields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in pmiElement.EnumerateObject())
            {
                rawFields[property.Name] = property.Value.ToString();
            }

            entries.Add(new FirmamentParsedPmiEntry(kindRaw, ParsePmiKind(kindRaw), rawFields));
            index++;
        }

        return KernelResult<IReadOnlyList<FirmamentParsedPmiEntry>>.Success(entries);
    }

    private static KernelResult<FirmamentParsedPmiSection?> ParsePmiFromToonSections(IReadOnlyDictionary<string, FirmamentToonSection> sections)
    {
        if (!sections.TryGetValue("pmi", out var pmiSection))
        {
            return KernelResult<FirmamentParsedPmiSection?>.Success(null);
        }

        if (!pmiSection.IsArrayLike)
        {
            return KernelResult<FirmamentParsedPmiSection?>.Failure([
                CreateDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    FirmamentDiagnosticCodes.StructureInvalidSectionShape,
                    "Section 'pmi' must be an array.")
            ]);
        }

        var parseEntries = ParsePmiToonEntries(pmiSection);
        if (!parseEntries.IsSuccess)
        {
            return KernelResult<FirmamentParsedPmiSection?>.Failure(parseEntries.Diagnostics);
        }

        return KernelResult<FirmamentParsedPmiSection?>.Success(
            new FirmamentParsedPmiSection(true, parseEntries.Value));
    }

    private static KernelResult<IReadOnlyList<FirmamentParsedPmiEntry>> ParsePmiToonEntries(FirmamentToonSection pmiSection)
    {
        var entries = new List<FirmamentParsedPmiEntry>();
        for (var index = 0; index < pmiSection.ArrayEntries.Count; index++)
        {
            var entry = pmiSection.ArrayEntries[index];
            if (!entry.IsObjectLike)
            {
                return InvalidPmiEntryShape(index);
            }

            if (!entry.Fields.TryGetValue("kind", out var kindRaw)
                || string.IsNullOrWhiteSpace(kindRaw))
            {
                return MissingPmiField(index, "kind");
            }

            var normalizedKind = kindRaw.Trim();
            entries.Add(new FirmamentParsedPmiEntry(
                normalizedKind,
                ParsePmiKind(normalizedKind),
                new Dictionary<string, string>(entry.Fields, StringComparer.Ordinal)));
        }

        return KernelResult<IReadOnlyList<FirmamentParsedPmiEntry>>.Success(entries);
    }

    private static FirmamentParsedPmiKind ParsePmiKind(string raw) =>
        raw switch
        {
            "hole" => FirmamentParsedPmiKind.Hole,
            "datum" => FirmamentParsedPmiKind.Datum,
            "note" => FirmamentParsedPmiKind.Note,
            _ => FirmamentParsedPmiKind.Unknown
        };

    private static bool TryParseStructuredFields(string raw, out IReadOnlyDictionary<string, string> fields)
    {
        fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var trimmed = raw.Trim();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal) || !trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            return false;
        }

        var body = trimmed[1..^1].Trim();
        var parsedFields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in SplitTopLevelCommaSeparated(body))
        {
            var separator = pair.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var name = pair[..separator].Trim();
            var value = pair[(separator + 1)..].Trim();
            if (name.Length == 0 || value.Length == 0)
            {
                continue;
            }

            parsedFields[name] = value;
        }

        fields = parsedFields;
        return true;
    }

    private static bool TryParseNumeric(string raw, out double value)
    {
        return double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static FirmamentParsedPlacement? ParsePlacementFromJson(JsonElement opElement)
    {
        if (!opElement.TryGetProperty("place", out var placeElement))
        {
            return null;
        }

        if (placeElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        FirmamentParsedPlacementAnchor? anchor = null;
        if (placeElement.TryGetProperty("on", out var onElement))
        {
            var onRaw = onElement.ToString();
            anchor = string.Equals(onRaw, "origin", StringComparison.Ordinal)
                ? (FirmamentParsedPlacementAnchor)new FirmamentParsedPlacementOriginAnchor()
                : new FirmamentParsedPlacementSelectorAnchor(onRaw);
        }

        var offset = new List<double>();
        if (placeElement.TryGetProperty("offset", out var offsetElement)
            && offsetElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var component in offsetElement.EnumerateArray())
            {
                if (component.ValueKind == JsonValueKind.Number)
                {
                    offset.Add(component.GetDouble());
                }
            }
        }

        var onFace = placeElement.TryGetProperty("on_face", out var onFaceElement) ? onFaceElement.ToString() : null;
        var centeredOn = placeElement.TryGetProperty("centered_on", out var centeredOnElement) ? centeredOnElement.ToString() : null;
        var aroundAxis = placeElement.TryGetProperty("around_axis", out var aroundAxisElement) ? aroundAxisElement.ToString() : null;
        var radialOffset = placeElement.TryGetProperty("radial_offset", out var radialOffsetElement) && radialOffsetElement.ValueKind == JsonValueKind.Number
            ? radialOffsetElement.GetDouble()
            : (double?)null;
        var angleDegrees = placeElement.TryGetProperty("angle_degrees", out var angleDegreesElement) && angleDegreesElement.ValueKind == JsonValueKind.Number
            ? angleDegreesElement.GetDouble()
            : (double?)null;
        var unknownFields = placeElement
            .EnumerateObject()
            .Select(p => p.Name)
            .Where(name => !KnownPlacementFields.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        return new FirmamentParsedPlacement(anchor, offset, onFace, centeredOn, aroundAxis, radialOffset, angleDegrees, unknownFields);
    }

    private static FirmamentParsedPlacement? ParsePlacementFromToon(FirmamentToonObjectEntry opEntry)
    {
        if (!opEntry.Fields.TryGetValue("place", out var placeRaw) || string.IsNullOrWhiteSpace(placeRaw))
        {
            return null;
        }

        var trimmed = placeRaw.Trim();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal) || !trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            return null;
        }

        var body = trimmed[1..^1].Trim();
        var pairs = SplitTopLevelCommaSeparated(body);
        string? on = null;
        string? onFace = null;
        string? centeredOn = null;
        string? aroundAxis = null;
        double? radialOffset = null;
        double? angleDegrees = null;
        var offset = new List<double>();
        var unknownFields = new List<string>();

        foreach (var pair in pairs)
        {
            var separator = pair.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var keyRaw = pair[..separator].Trim();
            var key = TryParseArrayHeader(keyRaw, out var normalizedFieldName) ? normalizedFieldName : keyRaw;
            var value = pair[(separator + 1)..].Trim();
            if (string.Equals(key, "on", StringComparison.Ordinal))
            {
                on = value;
                continue;
            }
            if (string.Equals(key, "on_face", StringComparison.Ordinal))
            {
                onFace = value;
                continue;
            }
            if (string.Equals(key, "centered_on", StringComparison.Ordinal))
            {
                centeredOn = value;
                continue;
            }
            if (string.Equals(key, "around_axis", StringComparison.Ordinal))
            {
                aroundAxis = value;
                continue;
            }
            if (string.Equals(key, "radial_offset", StringComparison.Ordinal))
            {
                if (TryParseNumeric(value, out var parsedRadialOffset))
                {
                    radialOffset = parsedRadialOffset;
                }

                continue;
            }
            if (string.Equals(key, "angle_degrees", StringComparison.Ordinal))
            {
                if (TryParseNumeric(value, out var parsedAngleDegrees))
                {
                    angleDegrees = parsedAngleDegrees;
                }

                continue;
            }

            if (string.Equals(key, "offset", StringComparison.Ordinal)
                && value.StartsWith("[", StringComparison.Ordinal)
                && value.EndsWith("]", StringComparison.Ordinal))
            {
                var rawParts = value[1..^1].Split(',', StringSplitOptions.TrimEntries);
                foreach (var part in rawParts)
                {
                    if (double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var component))
                    {
                        offset.Add(component);
                    }
                }

                continue;
            }

            if (!KnownPlacementFields.Contains(key))
            {
                unknownFields.Add(key);
            }
        }

        FirmamentParsedPlacementAnchor? anchor = null;
        if (!string.IsNullOrWhiteSpace(on))
        {
            anchor = string.Equals(on, "origin", StringComparison.Ordinal)
                ? (FirmamentParsedPlacementAnchor)new FirmamentParsedPlacementOriginAnchor()
                : new FirmamentParsedPlacementSelectorAnchor(on);
        }

        return new FirmamentParsedPlacement(anchor, offset, onFace, centeredOn, aroundAxis, radialOffset, angleDegrees, unknownFields.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    private static readonly HashSet<string> KnownPlacementFields = new(StringComparer.Ordinal)
    {
        "on",
        "offset",
        "on_face",
        "centered_on",
        "around_axis",
        "radial_offset",
        "angle_degrees"
    };

    private static IReadOnlyList<string> SplitTopLevelCommaSeparated(string raw)
    {
        var parts = new List<string>();
        var start = 0;
        var squareDepth = 0;
        var curlyDepth = 0;

        for (var i = 0; i < raw.Length; i++)
        {
            var ch = raw[i];
            switch (ch)
            {
                case '[':
                    squareDepth++;
                    break;
                case ']':
                    squareDepth = Math.Max(0, squareDepth - 1);
                    break;
                case '{':
                    curlyDepth++;
                    break;
                case '}':
                    curlyDepth = Math.Max(0, curlyDepth - 1);
                    break;
                case ',':
                    if (squareDepth == 0 && curlyDepth == 0)
                    {
                        var part = raw[start..i].Trim();
                        if (part.Length > 0)
                        {
                            parts.Add(part);
                        }

                        start = i + 1;
                    }

                    break;
            }
        }

        var finalPart = raw[start..].Trim();
        if (finalPart.Length > 0)
        {
            parts.Add(finalPart);
        }

        return parts;
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

    private static KernelResult<IReadOnlyList<FirmamentParsedPmiEntry>> InvalidPmiEntryShape(int index) =>
        KernelResult<IReadOnlyList<FirmamentParsedPmiEntry>>.Failure(
        [
            CreateDiagnostic(
                KernelDiagnosticCode.ValidationFailed,
                FirmamentDiagnosticCodes.StructureInvalidSectionShape,
                $"PMI entry at index {index} must be an object with fields.")
        ]);

    private static KernelResult<IReadOnlyList<FirmamentParsedPmiEntry>> MissingPmiField(int index, string fieldName) =>
        KernelResult<IReadOnlyList<FirmamentParsedPmiEntry>>.Failure(
        [
            CreateDiagnostic(
                KernelDiagnosticCode.ValidationFailed,
                FirmamentDiagnosticCodes.StructureMissingRequiredField,
                $"PMI entry at index {index} is missing required field '{fieldName}'.")
        ]);

    private static KernelResult<IReadOnlyList<FirmamentParsedPmiEntry>> InvalidPmiFieldValue(int index, string fieldName, string expectation) =>
        KernelResult<IReadOnlyList<FirmamentParsedPmiEntry>>.Failure(
        [
                CreateDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    FirmamentDiagnosticCodes.StructureInvalidSectionShape,
                    $"PMI entry at index {index} has invalid field '{fieldName}'; {expectation}")
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
