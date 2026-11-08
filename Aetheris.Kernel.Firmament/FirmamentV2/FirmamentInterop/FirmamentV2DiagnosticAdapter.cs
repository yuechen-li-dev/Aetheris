using Aetheris.Forge.Abstractions.FirmamentInterop;

namespace Aetheris.Kernel.Firmament.FirmamentV2.FirmamentInterop;

public static class FirmamentV2DiagnosticAdapter
{
    public static FirmamentDiagnostic FromValidationDiagnostic(
        FirmamentV2ValidationDiagnostic diagnostic,
        FirmamentV2SourceSpan? sourceSpan = null,
        string? target = null,
        string? fieldName = null)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        return new FirmamentDiagnostic(
            diagnostic.Code,
            diagnostic.Severity switch
            {
                "fatal" or "error" => FirmamentDiagnosticSeverity.Fatal,
                "warning" => FirmamentDiagnosticSeverity.Warning,
                _ => FirmamentDiagnosticSeverity.Info
            },
            diagnostic.Message,
            FirmamentV2InteropValueAdapter.AdaptSourceSpan(sourceSpan),
            target,
            fieldName);
    }

    public static FirmamentDiagnostic FromParserDiagnosticCode(
        string diagnosticCode,
        FirmamentV2SourceSpan? sourceSpan = null,
        string? target = null,
        string? fieldName = null) =>
        new(
            diagnosticCode,
            ParserSeverity(diagnosticCode),
            MessageFor(diagnosticCode),
            FirmamentV2InteropValueAdapter.AdaptSourceSpan(sourceSpan),
            target,
            fieldName);

    // Legacy producers still return strings. At this boundary unclassified diagnostics
    // are errors; adding an error must never require updating an admission allowlist.
    public static FirmamentDiagnosticSeverity ParserSeverity(string code) => code switch
    {
        FirmamentV2Parser.ToleranceDroppedThroughArithmetic => FirmamentDiagnosticSeverity.Warning,
        "firmament-v2-parser-invoked" or "firmament-v2-parse-succeeded"
            or "firmament-concept-ir-resolved" or "firmament-concept-struct-erased-before-feature-air"
            or "firmament-template-expanded-before-feature-air" => FirmamentDiagnosticSeverity.Info,
        _ when code.EndsWith("-parsed", StringComparison.Ordinal)
            || code.EndsWith("-adapted", StringComparison.Ordinal)
            || code.EndsWith("-symbols-bound", StringComparison.Ordinal) => FirmamentDiagnosticSeverity.Info,
        _ => FirmamentDiagnosticSeverity.Fatal
    };

    public static string MessageFor(string diagnosticCode) => diagnosticCode switch
    {
        FirmamentV2Parser.CanonicalPrimitiveMalformed => "Malformed primitive: check the required dimension fields and their length units.",
        FirmamentV2Parser.CanonicalModifyMalformed => "Malformed Modify feature. Check the target, feature variant, required fields and termination.",
        FirmamentV2Parser.CanonicalUnitsInvalid => "This mechanical document requires Units: mm; conflicting unit declarations are not admitted.",
        FirmamentV2Parser.HoleVariantUnknown => "Unknown Hole variant. Use an admitted Hole<Shaft>, Hole<Counterbore> or Hole<Countersink> contract.",
        FirmamentV2Parser.CanonicalDocumentMalformed => "Malformed Model document. Check its declarations and closing braces.",
        "firmament-v2-parse-failed" => "Firmament V2 parsing or binding failed; the program has not been admitted for compilation.",
        FirmamentV2Parser.PmiDimensionMissingTolerance => "PMI dimension resolves to a value without tolerance evidence.",
        FirmamentV2Parser.ConceptMissingRequiredField => "Forge concept application is missing a required descriptor field.",
        FirmamentV2Parser.PmiUnknownDatum => "PMI relation references an unknown datum.",
        "firmament-v2-pmi-name-list-invalid" => "Use a datum or modifier name, or an ordered list of names such as [A, B, C].",
        "firmament-v2-pmi-datum-precedence-invalid" => "datumRefs requires up to three distinct datum names in primary, secondary and tertiary order.",
        "firmament-v2-pmi-datum-not-applicable" => "Form controls omit datums; orientation and runout controls require declared datum references.",
        "firmament-v2-pmi-modifier-invalid" => "Use admitted, distinct modifiers. MaximumMaterial and LeastMaterial cannot be combined.",
        "firmament-v2-pmi-zone-form-invalid" => "zone requires Cylindrical, Spherical or BetweenSurfaces.",
        FirmamentV2Parser.ToleranceDroppedThroughArithmetic => "A toleranced value was used in arithmetic and only the nominal value was preserved.",
        _ when diagnosticCode.StartsWith(FirmamentV2Parser.AssertVolumeKindUnsupported + ":", StringComparison.Ordinal)
            => "This part admits Assert Volume, Assert Bounds and Assert Topology. Assert ToleranceStackup belongs on an Assembly with dimensional relations.",
        _ when diagnosticCode.StartsWith(FirmamentV2Parser.AssertVolumeExpectedDimensionMismatch + ":", StringComparison.Ordinal)
            => "Assert Volume expected requires a finite nonnegative volume literal in mm^3.",
        _ when diagnosticCode.StartsWith(FirmamentV2Parser.AssertVolumeToleranceDimensionMismatch + ":", StringComparison.Ordinal)
            => "Assert Volume tolerance requires a finite nonnegative volume literal in mm^3.",
        _ when diagnosticCode.StartsWith(FirmamentV2Parser.AssertVolumeToleranceNegative + ":", StringComparison.Ordinal)
            => "Assert Volume tolerance cannot be negative.",
        _ when diagnosticCode.StartsWith("firmament-v2-assert-bounds-malformed:", StringComparison.Ordinal)
            => "Assert Bounds requires three-component min and max length vectors in ascending order, and a nonnegative length tolerance.",
        _ when diagnosticCode.StartsWith("firmament-v2-assert-topology-malformed:", StringComparison.Ordinal)
            => "Assert Topology requires at least one finite nonnegative integer count: faces, edges, vertices or shells.",
        _ when diagnosticCode.StartsWith("firmament-v2-field-duplicate:", StringComparison.Ordinal)
            => $"Field '{diagnosticCode.Split(':')[^1]}' is specified more than once in {diagnosticCode.Split(':')[1]}. Keep one value.",
        _ when diagnosticCode.StartsWith("firmament-v2-field-value-invalid:", StringComparison.Ordinal)
            => $"Invalid value for field '{diagnosticCode.Split(':')[^1]}' in {diagnosticCode.Split(':')[1]}. Check the value type, units and admitted vocabulary.",
        _ when diagnosticCode.StartsWith(FirmamentV2Parser.CanonicalFieldUnknown + ":", StringComparison.Ordinal)
            => $"Unknown field '{diagnosticCode.Split(':')[^1]}' in {diagnosticCode.Split(':')[1]}. Check this construct's documented fields.",
        _ when diagnosticCode.StartsWith("firmament-v2-thread-field-invalid:", StringComparison.Ordinal)
            => FirmamentSchemaAuthoringFields.For("Thread").Any(f => f.Name == diagnosticCode.Split(':')[^1])
                ? $"Thread field '{FirmamentSourceSpelling.PreferredField(diagnosticCode.Split(':')[^1])}' is specified more than once. Keep one value."
                : $"Unknown Thread field '{diagnosticCode.Split(':')[^1]}'. Use " + string.Join(", ", FirmamentSchemaAuthoringFields.For("Thread").Select(f => f.Spelling)) + ".",
        _ when diagnosticCode.StartsWith(FirmamentV2Parser.CanonicalDeclarationUnknown + ":", StringComparison.Ordinal)
            => $"Unknown statement '{diagnosticCode.Split(':')[^1]}'{(diagnosticCode.Split(':').Length > 2 ? " in " + diagnosticCode.Split(':')[1] : "")}. Use an admitted construct in this context.",
        // Producers write "code[:parameters]: explanation"; the explanation is the message.
        _ when diagnosticCode.IndexOf(": ", StringComparison.Ordinal) is > 0 and var split && !diagnosticCode[..split].Any(char.IsWhiteSpace)
            => diagnosticCode[(split + 2)..],
        _ when PathMessage(diagnosticCode) is { } path => path,
        _ when BoundaryMessage(diagnosticCode) is { } boundary => boundary,
        _ => diagnosticCode
    };

    // concept-path-<kind>:<path>[:<step>[:detail...]]
    private static string? PathMessage(string code)
    {
        if (!code.StartsWith("concept-path-", StringComparison.Ordinal)) return null;
        var parts = code.Split(':');
        var kind = parts[0]["concept-path-".Length..];
        var path = parts.ElementAtOrDefault(1) ?? "?";
        var step = parts.ElementAtOrDefault(2);
        var where = step is null ? $"Path '{path}'" : $"Step '{step}' of Path '{path}'";
        return kind switch
        {
            "zero-length" => $"{where} has zero length: it ends where it starts. Remove it, or end the path one step earlier.",
            "unknown-target" => $"{where} goes To '{parts.ElementAtOrDefault(3)}', which is not a named point. Use Start, a Concept point, or an earlier step's End.",
            "length-invalid" => $"{where} needs a positive length.",
            "turn-and-heading" => $"{where} sets both turn and heading; give one.",
            "to-mixed-direction-or-length" => $"{where} combines To with a length, turn or heading. To fixes the endpoint, so give it alone.",
            "field-unknown" => $"{where} has unknown field '{parts.ElementAtOrDefault(3)}'. Allowed: {parts.ElementAtOrDefault(4)?.Replace("allowed=", "", StringComparison.Ordinal).Replace(",", ", ", StringComparison.Ordinal)}.",
            "duplicate-step" => $"Path '{path}' has two steps named '{step}'. Step names must be unique within a path.",
            "name-collision" => $"'{path}' is already declared. Path and step names must not collide with other names.",
            "start-invalid" => $"{where} needs start: Point2(x, y) with lengths.",
            "heading-invalid" => $"{where} heading must be an angle, for example 90deg.",
            _ when kind.EndsWith("-invalid", StringComparison.Ordinal) && step is not null
                => $"{where} is not a valid {kind[..^"-invalid".Length]} step{(parts.Length > 3 ? ": " + string.Join(":", parts[3..]) : ".")}",
            _ => null
        };
    }

    // firmament-boundary2-<reason>:<name>
    private static string? BoundaryMessage(string code)
    {
        const string prefix = "firmament-boundary2-";
        if (!code.StartsWith(prefix, StringComparison.Ordinal) || code.LastIndexOf(':') is not (> 0 and var colon)) return null;
        var reason = code[prefix.Length..colon];
        var name = code[(colon + 1)..];
        // A one-line Profile is lowered through a generated "<name>__Boundary" shape; report the authored name.
        if (name.EndsWith("__Boundary", StringComparison.Ordinal)) name = name[..^"__Boundary".Length];
        return reason switch
        {
            "profile-value-unsupported" => $"Profile '{name}' = ... needs a shape value (Rect2, Circle2, Slot2, RoundedRect2, RegularPolygon2<N>, ...) or the name of a closed Path or shape.",
            "profile-value-header" => $"Profile '{name}' = value takes no type annotation, parameters or clauses.",
            "duplicate-name" => $"'{name}' is already declared.",
            "reserved-value-name" => $"'{name}' is reserved for a generated shape name; choose another name.",
            "variant-unsupported" => $"Shape '{name}' has a variant this shape does not take; only Triangle2, Polygon2 and RegularPolygon2 take <...>.",
            "invalid-dimensions" => $"Shape '{name}' has missing or non-positive dimensions. Check its required fields and length units.",
            _ => $"Shape '{name}' is invalid: {reason.Replace('-', ' ')}."
        };
    }
}
