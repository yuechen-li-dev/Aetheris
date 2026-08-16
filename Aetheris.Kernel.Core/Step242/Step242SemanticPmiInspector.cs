namespace Aetheris.Kernel.Core.Step242;

public sealed record Step242SemanticPmiInspectionItem(
    int EntityId,
    string Kind,
    string Name,
    string Target,
    double? Value,
    double? TolerancePlus,
    double? ToleranceMinus,
    int? Quantity,
    IReadOnlyList<string> DatumReferences,
    string? Text)
{
    public IReadOnlyList<int> GeometricFaceEntityIds { get; init; } = [];
}

public sealed record Step242SemanticPmiInspectionResult(
    bool Success,
    IReadOnlyList<Step242SemanticPmiInspectionItem> Items,
    IReadOnlyList<string> Diagnostics)
{
    public int DatumCount => Items.Count(item => item.Kind == "Datum");
    public int DimensionCount => Items.Count(item => item.Kind is "Diameter" or "Dimension");
    public int GeometricToleranceCount => Items.Count(item => item.Kind == "Position");
    public int AnnotationCount => Items.Count(item => item.Kind == "Annotation");
}

/// <summary>
/// Reinspects the bounded semantic PMI subset emitted by <see cref="Step242Exporter"/>.
/// This reads product-definition semantics only; graphical annotation placement is intentionally absent.
/// </summary>
public static class Step242SemanticPmiInspector
{
    public static Step242SemanticPmiInspectionResult Inspect(string stepText)
    {
        var parsed = Step242SubsetParser.Parse(stepText);
        if (!parsed.IsSuccess)
            return new(false, [], parsed.Diagnostics.Select(diagnostic => diagnostic.Message).ToArray());

        var document = parsed.Value;
        var entities = document.Entities.ToDictionary(entity => entity.Id);
        var representations = BuildRepresentations(document, entities);
        var aspectTargets = BuildAspectTargets(document);
        var geometricAssociations = BuildGeometricAssociations(document, aspectTargets);
        var quantities = BuildQuantities(document);
        var items = new List<Step242SemanticPmiInspectionItem>();

        foreach (var entity in document.Entities.OrderBy(entity => entity.Id))
        {
            if (Step242SubsetDecoder.TryGetConstructor(entity.Instance, "DATUM_FEATURE") is { } datumFeature
                && String(datumFeature, 0) is { } datumName
                && datumName.StartsWith("firmament-datum:", StringComparison.Ordinal))
            {
                var label = datumName["firmament-datum:".Length..];
                items.Add(new(entity.Id, "Datum", label, Target(String(datumFeature, 1)), null, null, null, null, [], null));
                continue;
            }

            if (Step242SubsetDecoder.TryGetConstructor(entity.Instance, "PROPERTY_DEFINITION") is not { } property
                || String(property, 0) is not { } propertyName)
                continue;

            var aspectId = Reference(property, 2);
            var target = aspectId.HasValue && aspectTargets.TryGetValue(aspectId.Value, out var associatedTarget)
                ? associatedTarget
                : string.Empty;

            if (propertyName.StartsWith("diameter:", StringComparison.Ordinal))
            {
                target = propertyName["diameter:".Length..];
                var nominal = representations.GetValueOrDefault(propertyName);
                var tolerance = representations.GetValueOrDefault($"diameter_tolerance:{target}");
                items.Add(new(entity.Id, "Diameter", propertyName, target, nominal?.FirstOrDefault(), tolerance?.ElementAtOrDefault(0), tolerance?.ElementAtOrDefault(1), Quantity(quantities, target), [], null));
            }
            else if (propertyName.StartsWith("dimension:", StringComparison.Ordinal))
            {
                var parts = propertyName.Split(':', 3);
                if (parts.Length < 3) continue;
                var name = parts[1];
                target = parts[2];
                var nominal = representations.GetValueOrDefault($"dimension:{name}");
                var tolerance = representations.GetValueOrDefault($"dimension_tolerance:{name}");
                items.Add(new(entity.Id, "Dimension", name, target, nominal?.FirstOrDefault(), tolerance?.ElementAtOrDefault(0), tolerance?.ElementAtOrDefault(1), Quantity(quantities, target), [], null));
            }
            else if (propertyName.StartsWith("note:", StringComparison.Ordinal))
            {
                var name = propertyName["note:".Length..];
                items.Add(new(entity.Id, "Annotation", name, target, null, null, null, null, [], String(property, 1)));
            }
        }

        foreach (var entity in document.Entities.OrderBy(entity => entity.Id))
        {
            if (Step242SubsetDecoder.TryGetConstructor(entity.Instance, "GEOMETRIC_TOLERANCE") is not { } geometric
                || Step242SubsetDecoder.TryGetConstructor(entity.Instance, "POSITION_TOLERANCE") is null)
                continue;
            var name = String(geometric, 0) ?? $"position-{entity.Id}";
            var target = Target(String(geometric, 1));
            var magnitude = Reference(geometric, 2) is { } magnitudeId && entities.TryGetValue(magnitudeId, out var magnitudeEntity)
                ? FirstNumber(magnitudeEntity.Instance)
                : null;
            var datumReferences = ResolveDatumReferences(entity, entities);
            items.Add(new(entity.Id, "Position", name, target, magnitude, null, null, Quantity(quantities, target), datumReferences, null));
        }

        return new(true, items.OrderBy(item => item.EntityId).Select(item => item with
        {
            GeometricFaceEntityIds = geometricAssociations.GetValueOrDefault(item.Target) ?? []
        }).ToArray(), []);
    }

    private static Dictionary<string, IReadOnlyList<double>> BuildRepresentations(
        Step242ParsedDocument document,
        IReadOnlyDictionary<int, Step242ParsedEntity> entities)
    {
        var result = new Dictionary<string, IReadOnlyList<double>>(StringComparer.Ordinal);
        foreach (var entity in document.Entities)
        {
            if (Step242SubsetDecoder.TryGetConstructor(entity.Instance, "SHAPE_DIMENSION_REPRESENTATION") is not { } representation
                || String(representation, 0) is not { } name
                || representation.Arguments.ElementAtOrDefault(1) is not Step242ListValue list)
                continue;
            result[name] = list.Items.OfType<Step242EntityReference>()
                .Where(reference => entities.ContainsKey(reference.TargetId))
                .Select(reference => FirstNumber(entities[reference.TargetId].Instance))
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToArray();
        }
        return result;
    }

    private static Dictionary<int, string> BuildAspectTargets(Step242ParsedDocument document)
    {
        var result = new Dictionary<int, string>();
        foreach (var entity in document.Entities)
        {
            var constructor = Step242SubsetDecoder.TryGetConstructor(entity.Instance, "SHAPE_ASPECT")
                ?? Step242SubsetDecoder.TryGetConstructor(entity.Instance, "COMPOSITE_SHAPE_ASPECT")
                ?? Step242SubsetDecoder.TryGetConstructor(entity.Instance, "DATUM_FEATURE");
            if (constructor is not null) result[entity.Id] = Target(String(constructor, 1));
        }
        return result;
    }

    private static Dictionary<string, IReadOnlyList<int>> BuildGeometricAssociations(
        Step242ParsedDocument document,
        IReadOnlyDictionary<int, string> aspectTargets)
    {
        return document.Entities
            .Select(entity => Step242SubsetDecoder.TryGetConstructor(entity.Instance, "GEOMETRIC_ITEM_SPECIFIC_USAGE"))
            .Where(constructor => constructor is not null)
            .Select(constructor => new { Aspect = Reference(constructor!, 2), Face = Reference(constructor!, 4) })
            .Where(item => item.Aspect.HasValue && item.Face.HasValue && aspectTargets.ContainsKey(item.Aspect.Value))
            .GroupBy(item => aspectTargets[item.Aspect!.Value], StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<int>)group.Select(item => item.Face!.Value).Distinct().OrderBy(id => id).ToArray(), StringComparer.Ordinal);
    }

    private static Dictionary<string, int> BuildQuantities(Step242ParsedDocument document)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var entity in document.Entities)
        {
            if (Step242SubsetDecoder.TryGetConstructor(entity.Instance, "PROPERTY_DEFINITION") is not { } property
                || String(property, 0) is not { } name
                || !name.StartsWith("quantity:", StringComparison.Ordinal)
                || !int.TryParse(String(property, 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var quantity))
                continue;
            result[name["quantity:".Length..]] = quantity;
        }
        return result;
    }

    private static int? Quantity(IReadOnlyDictionary<string, int> quantities, string target) =>
        quantities.TryGetValue(target, out var quantity) ? quantity : null;

    private static IReadOnlyList<string> ResolveDatumReferences(
        Step242ParsedEntity toleranceEntity,
        IReadOnlyDictionary<int, Step242ParsedEntity> entities)
    {
        var withDatum = Step242SubsetDecoder.TryGetConstructor(toleranceEntity.Instance, "GEOMETRIC_TOLERANCE_WITH_DATUM_REFERENCE");
        if (withDatum?.Arguments.ElementAtOrDefault(0) is not Step242ListValue systems) return [];
        var labels = new List<string>();
        foreach (var systemReference in systems.Items.OfType<Step242EntityReference>())
        {
            if (!entities.TryGetValue(systemReference.TargetId, out var system)
                || Step242SubsetDecoder.TryGetConstructor(system.Instance, "DATUM_SYSTEM") is not { } systemConstructor
                || systemConstructor.Arguments.ElementAtOrDefault(4) is not Step242ListValue compartments)
                continue;
            foreach (var compartmentReference in compartments.Items.OfType<Step242EntityReference>())
            {
                if (!entities.TryGetValue(compartmentReference.TargetId, out var compartment)
                    || Step242SubsetDecoder.TryGetConstructor(compartment.Instance, "DATUM_REFERENCE_COMPARTMENT") is not { } compartmentConstructor
                    || Reference(compartmentConstructor, 4) is not { } datumId
                    || !entities.TryGetValue(datumId, out var datum)
                    || Step242SubsetDecoder.TryGetConstructor(datum.Instance, "DATUM") is not { } datumConstructor
                    || String(datumConstructor, 4) is not { } label)
                    continue;
                labels.Add(label);
            }
        }
        return labels;
    }

    private static string Target(string? description)
    {
        if (description is null) return string.Empty;
        const string marker = "target=";
        var index = description.IndexOf(marker, StringComparison.Ordinal);
        return index < 0 ? string.Empty : description[(index + marker.Length)..].Split(' ', 2)[0];
    }

    private static string? String(Step242EntityConstructor constructor, int index) =>
        constructor.Arguments.ElementAtOrDefault(index) is Step242StringValue value ? value.Value : null;

    private static int? Reference(Step242EntityConstructor constructor, int index) =>
        constructor.Arguments.ElementAtOrDefault(index) is Step242EntityReference reference ? reference.TargetId : null;

    private static double? FirstNumber(Step242EntityInstance instance)
    {
        foreach (var constructor in instance is Step242ComplexEntityInstance complex ? complex.Constructors : [instance.PrimaryConstructor])
            foreach (var argument in constructor.Arguments)
                if (FirstNumber(argument) is { } value) return value;
        return null;
    }

    private static double? FirstNumber(Step242Value value) => value switch
    {
        Step242NumberValue number => number.Value,
        Step242TypedValue typed => typed.Arguments.Select(FirstNumber).FirstOrDefault(result => result.HasValue),
        Step242ListValue list => list.Items.Select(FirstNumber).FirstOrDefault(result => result.HasValue),
        _ => null
    };
}
