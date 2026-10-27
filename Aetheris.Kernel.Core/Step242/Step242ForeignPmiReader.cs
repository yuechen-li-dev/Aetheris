namespace Aetheris.Kernel.Core.Step242;

/// <summary>Read semantic AP242 controls by their schema relationships, independently of vendor names.</summary>
internal sealed class Step242ForeignPmiReader
{
    private readonly Step242ParsedDocument document;
    private readonly Dictionary<int, Step242ParsedEntity> entities;
    private readonly Dictionary<int, List<int>> aspectLinks = [];
    private readonly Dictionary<int, List<int>> aspectGeometry = [];
    private readonly Dictionary<int, HashSet<int>> edgeFaces = [];
    private readonly Dictionary<int, int> dimensionRepresentations = [];
    private readonly Dictionary<int, int> dimensionTolerances = [];
    private readonly Dictionary<int, List<string>> zoneForms = [];
    private readonly HashSet<int> advancedZones = [];
    internal Step242ForeignPmiReader(Step242ParsedDocument document)
    {
        this.document = document;
        entities = document.Entities.ToDictionary(e => e.Id);
        foreach (var entity in document.Entities)
        {
            if (Step242SubsetDecoder.TryGetConstructor(entity.Instance, "SHAPE_ASPECT_RELATIONSHIP") is { } link
                && Ref(link.Arguments.ElementAtOrDefault(2)) is { } a && Ref(link.Arguments.ElementAtOrDefault(3)) is { } b)
            { Add(aspectLinks, a, b); Add(aspectLinks, b, a); }
            if (entity.Name == "GEOMETRIC_ITEM_SPECIFIC_USAGE" && Ref(entity.Arguments.ElementAtOrDefault(2)) is { } aspect)
                foreach (var geometry in Refs(entity.Arguments.ElementAtOrDefault(4))) Add(aspectGeometry, aspect, geometry);
            if (entity.Name == "DIMENSIONAL_CHARACTERISTIC_REPRESENTATION" && Ref(entity.Arguments.ElementAtOrDefault(0)) is { } dimension && Ref(entity.Arguments.ElementAtOrDefault(1)) is { } representation)
                dimensionRepresentations[dimension] = representation;
            if (entity.Name == "PLUS_MINUS_TOLERANCE" && Ref(entity.Arguments.ElementAtOrDefault(0)) is { } tolerance && Ref(entity.Arguments.ElementAtOrDefault(1)) is { } controlled)
                dimensionTolerances[controlled] = tolerance;
            if (entity.Name == "TOLERANCE_ZONE" && Ref(entity.Arguments.ElementAtOrDefault(5)) is { } form && entities.TryGetValue(form, out var zoneForm) && Str(zoneForm.Arguments.FirstOrDefault()) is { } formName)
                foreach (var target in Refs(entity.Arguments.ElementAtOrDefault(4)))
                { if (!zoneForms.TryGetValue(target, out var forms)) zoneForms[target] = forms = []; forms.Add(formName); }
            if (entity.Name.EndsWith("ZONE_DEFINITION", StringComparison.Ordinal) || entity.Name.EndsWith("TOLERANCE_ZONE", StringComparison.Ordinal) && entity.Name != "TOLERANCE_ZONE")
                foreach (var reference in Step242PmiTransfer.References(entity.Instance)) advancedZones.Add(reference);
        }
        foreach (var face in document.Entities.Where(e => e.Name == "ADVANCED_FACE"))
        {
            var pending = new Stack<int>(Step242PmiTransfer.References(face.Instance)); var seen = new HashSet<int>();
            while (pending.TryPop(out var id))
            {
                if (!seen.Add(id) || !entities.TryGetValue(id, out var entity)) continue;
                if (entity.Name == "EDGE_CURVE")
                { if (!edgeFaces.TryGetValue(id, out var faces)) edgeFaces[id] = faces = []; faces.Add(face.Id); continue; }
                if (entity.Name is "FACE_OUTER_BOUND" or "FACE_BOUND" or "EDGE_LOOP" or "ORIENTED_EDGE")
                    foreach (var child in Step242PmiTransfer.References(entity.Instance)) pending.Push(child);
            }
        }
    }
    internal IReadOnlyList<Step242SemanticPmiInspectionItem> Read(IReadOnlyList<Step242SemanticPmiInspectionItem> existing)
    {
        var result = new List<Step242SemanticPmiInspectionItem>(); var skip = existing.Select(i => i.EntityId).ToHashSet();
        foreach (var entity in entities.Values.OrderBy(e => e.Id))
        {
            if (skip.Contains(entity.Id)) continue;
            if (Step242SubsetDecoder.TryGetConstructor(entity.Instance, "DATUM") is { } datum)
            {
                var label = Str(datum.Arguments.ElementAtOrDefault(4));
                if (label is null || existing.Any(i => i.Kind == "Datum" && i.Name == label)) continue;
                result.Add(Item(entity.Id, "Datum", label, entity.Id, null) with { GeometricFaceEntityIds = Faces(entity.Id) });
                continue;
            }
            var geometric = Step242PmiTransfer.GeometricTolerance(entity);
            var kind = Step242GeometricToleranceKinds.All.FirstOrDefault(k => Step242SubsetDecoder.TryGetConstructor(entity.Instance, k.Entity) is not null);
            if (geometric is not null)
            {
                var target = Ref(geometric.Arguments.ElementAtOrDefault(3));
                var value = Ref(geometric.Arguments.ElementAtOrDefault(2)) is { } magnitude ? Measure(magnitude) : null;
                var frames = DatumFrames(entity);
                var modifiers = Step242SubsetDecoder.TryGetConstructor(entity.Instance, "GEOMETRIC_TOLERANCE_WITH_MODIFIERS") is { } modified ? Enums(modified.Arguments.FirstOrDefault()).ToArray() : [];
                var constructors = entity.Instance is Step242ComplexEntityInstance complex ? complex.Constructors : [entity.Instance.PrimaryConstructor];
                var basic = kind is not null && constructors.All(c => c.Name == kind.Entity || c.Name is "GEOMETRIC_TOLERANCE" or "GEOMETRIC_TOLERANCE_WITH_DATUM_REFERENCE" or "GEOMETRIC_TOLERANCE_WITH_MODIFIERS")
                    && frames.All(f => f.PresentationSupported) && !advancedZones.Contains(entity.Id)
                    && (zoneForms.GetValueOrDefault(entity.Id) ?? []).All(form => Step242GeometricToleranceKinds.ZoneForms.Values.Contains(form))
                    && !entities.Values.Any(e => e.Name == "TOLERANCE_ZONE" && advancedZones.Contains(e.Id) && Refs(e.Arguments.ElementAtOrDefault(4)).Contains(entity.Id));
                result.Add(Item(entity.Id, kind?.Name ?? "GeometricTolerance", Str(geometric.Arguments.ElementAtOrDefault(0)) ?? $"tolerance-{entity.Id}", target, value) with
                { DatumReferences = frames.SelectMany(frame => frame.Labels).ToArray(), DatumReferenceFrames = frames, Modifiers = modifiers, ZoneForms = zoneForms.GetValueOrDefault(entity.Id) ?? [], PresentationSupported = basic });
                continue;
            }
            if (entity.Name is "DIMENSIONAL_SIZE" or "ANGULAR_SIZE" or "DIMENSIONAL_LOCATION" or "ANGULAR_LOCATION" or "DIRECTED_DIMENSIONAL_LOCATION")
            {
                var location = entity.Name is "DIMENSIONAL_LOCATION" or "ANGULAR_LOCATION" or "DIRECTED_DIMENSIONAL_LOCATION";
                var definition = Step242SubsetDecoder.TryGetConstructor(entity.Instance, location ? "DIMENSIONAL_LOCATION" : "DIMENSIONAL_SIZE") ?? entity.Instance.PrimaryConstructor;
                var target = Ref(definition.Arguments.FirstOrDefault());
                var targets = location ? definition.Arguments.Skip(2).OfType<Step242EntityReference>().Select(r => r.TargetId).Take(2).ToArray() : target is { } id ? [id] : [];
                var representation = dimensionRepresentations.GetValueOrDefault(entity.Id);
                var measureIds = entities.TryGetValue(representation, out var rep) ? Refs(rep.Arguments.ElementAtOrDefault(1)).ToArray() : [];
                var measured = measureIds.Select(Measure).FirstOrDefault(v => v.HasValue);
                var lower = default(double?); var upper = default(double?);
                if (dimensionTolerances.TryGetValue(entity.Id, out var toleranceId) && entities.TryGetValue(toleranceId, out var tolerance) && tolerance.Name == "TOLERANCE_VALUE")
                {
                    lower = Ref(tolerance.Arguments.ElementAtOrDefault(0)) is { } lo ? Measure(lo) : null;
                    upper = Ref(tolerance.Arguments.ElementAtOrDefault(1)) is { } hi ? Measure(hi) : null;
                }
                var dimensionKind = Str(definition.Arguments.ElementAtOrDefault(location ? 0 : 1)) ?? "Length";
                var name = targets.Select(id => entities.GetValueOrDefault(id)?.Arguments.FirstOrDefault()).OfType<Step242StringValue>().Select(s => s.Value).FirstOrDefault(s => s.Length > 0) ?? $"dimension-{entity.Id}";
                result.Add(Item(entity.Id, "Dimension", name, targets.FirstOrDefault(), measured) with
                { DimensionKind = dimensionKind, ValueUnit = entity.Name is "ANGULAR_SIZE" or "ANGULAR_LOCATION" ? "rad" : "mm", GeometricFaceEntityIds = targets.SelectMany(Faces).Distinct().Order().ToArray(),
                    GeometricItemEntityIds = targets.SelectMany(GeometryItems).Distinct().Order().ToArray(), TolerancePlus = upper, ToleranceMinus = lower.HasValue ? -lower.Value : null,
                    PresentationSupported = !measureIds.Any(id => entities.TryGetValue(id, out var measure) && Step242SubsetDecoder.TryGetConstructor(measure.Instance, "QUALIFIED_REPRESENTATION_ITEM") is not null) });
            }
        }
        return result;
    }
    private Step242SemanticPmiInspectionItem Item(int id, string kind, string name, int? target, double? value)
        => new(id, kind, name, target.HasValue ? $"shape-aspect:#{target}" : "", value, null, null, null, [], null)
        { GeometricFaceEntityIds = target.HasValue ? Faces(target.Value) : [], GeometricItemEntityIds = target.HasValue ? GeometryItems(target.Value) : [] };
    private IReadOnlyList<int> Faces(int aspect)
        => GeometryItems(aspect).SelectMany(geometry => entities.GetValueOrDefault(geometry)?.Name == "ADVANCED_FACE"
            ? [geometry] : edgeFaces.GetValueOrDefault(geometry) ?? []).Distinct().Order().ToArray();
    private IReadOnlyList<int> GeometryItems(int aspect)
    {
        var pending = new Stack<int>([aspect]); var seen = new HashSet<int>(); var geometry = new HashSet<int>();
        while (pending.TryPop(out var id))
        {
            if (!seen.Add(id)) continue;
            geometry.UnionWith(aspectGeometry.GetValueOrDefault(id) ?? []);
            foreach (var linked in aspectLinks.GetValueOrDefault(id) ?? []) pending.Push(linked);
        }
        return geometry.Order().ToArray();
    }
    private IReadOnlyList<Step242PmiDatumFrame> DatumFrames(Step242ParsedEntity entity)
    {
        var clause = Step242SubsetDecoder.TryGetConstructor(entity.Instance, "GEOMETRIC_TOLERANCE_WITH_DATUM_REFERENCE");
        var frames = new List<Step242PmiDatumFrame>();
        var roots = Refs(clause?.Arguments.FirstOrDefault()).ToArray();
        // AP214 datum references are a SET ordered by priority; AP242 constituents are a LIST.
        if (roots.All(id => entities.GetValueOrDefault(id)?.Name == "DATUM_REFERENCE"))
            roots = roots.OrderBy(id => (entities[id].Arguments.FirstOrDefault() as Step242NumberValue)?.Value ?? double.MaxValue).ToArray();
        foreach (var id in roots)
        {
            if (!entities.TryGetValue(id, out var system)) continue;
            var compartments = system.Name == "DATUM_SYSTEM" ? Refs(system.Arguments.ElementAtOrDefault(4)) : new[] { id };
            foreach (var compartmentId in compartments)
            {
                var labels = new List<string>(); var modifiers = new List<string>(); var seen = new HashSet<int>();
                var supported = true;
                void Walk(int current)
                {
                    if (!seen.Add(current) || !entities.TryGetValue(current, out var datum)) return;
                    if (Step242SubsetDecoder.TryGetConstructor(datum.Instance, "DATUM") is { } definition)
                    { if (Str(definition.Arguments.ElementAtOrDefault(4)) is { } label) labels.Add(label); return; }
                    if (datum.Name == "DATUM_REFERENCE_MODIFIER_WITH_VALUE") { supported = false; return; }
                    if (datum.Name is not ("DATUM_REFERENCE" or "DATUM_REFERENCE_COMPARTMENT" or "DATUM_REFERENCE_ELEMENT" or "COMMON_DATUM")) return;
                    if (datum.Name is "COMMON_DATUM" or "DATUM_REFERENCE_ELEMENT") supported = false;
                    modifiers.AddRange(datum.Arguments.SelectMany(Enums));
                    foreach (var child in Step242PmiTransfer.References(datum.Instance)) Walk(child);
                }
                Walk(compartmentId); frames.Add(new(frames.Count + 1, labels, modifiers) { PresentationSupported = supported });
            }
        }
        return frames;
    }
    internal double? Measure(int id)
    {
        if (!entities.TryGetValue(id, out var entity)) return null;
        var measure = Step242SubsetDecoder.TryGetConstructor(entity.Instance, "MEASURE_WITH_UNIT") ?? entity.Instance.PrimaryConstructor;
        var unitIndex = measure.Name == "MEASURE_REPRESENTATION_ITEM" ? 2 : 1;
        var valueIndex = measure.Name == "MEASURE_REPRESENTATION_ITEM" ? 1 : 0;
        var value = Number(measure.Arguments.ElementAtOrDefault(valueIndex));
        if (value is null) return null;
        if (Ref(measure.Arguments.ElementAtOrDefault(unitIndex)) is { } unit && entities.TryGetValue(unit, out var u) && Step242SubsetDecoder.TryGetConstructor(u.Instance, "LENGTH_UNIT") is not null)
            return Step242LengthUnitNormalizer.TryResolveMetresPerUnit(entities, unit, [], out var metres) ? Step242LengthUnitNormalizer.ScalePmiMeasure(value.Value, metres * 1000d) : null;
        if (Ref(measure.Arguments.ElementAtOrDefault(unitIndex)) is { } angleUnit && document.TryResolvePlaneAngleUnitScale(angleUnit, [], out var radians)) return value * radians;
        if (Ref(measure.Arguments.ElementAtOrDefault(unitIndex)) is { } compoundUnit && entities.GetValueOrDefault(compoundUnit)?.Name == "DERIVED_UNIT")
            return Step242LengthUnitNormalizer.TryResolvePmiScale(entities, compoundUnit, [], out var scale) ? Step242LengthUnitNormalizer.ScalePmiMeasure(value.Value, scale) : null;
        return value;
    }
    private static double? Number(Step242Value? value) => value switch { Step242NumberValue n => n.Value, Step242TypedValue t => t.Arguments.Select(Number).FirstOrDefault(v => v.HasValue), _ => null };
    private static int? Ref(Step242Value? value) => value is Step242EntityReference r ? r.TargetId : null;
    private static string? Str(Step242Value? value) => value is Step242StringValue s ? s.Value : null;
    private static IEnumerable<int> Refs(Step242Value? value) => value switch { Step242EntityReference r => [r.TargetId], Step242ListValue list => list.Items.SelectMany(Refs), _ => [] };
    private static IEnumerable<string> Enums(Step242Value? value) => value switch { Step242EnumValue e => [e.Value], Step242ListValue list => list.Items.SelectMany(Enums), _ => [] };
    private static void Add(Dictionary<int, List<int>> map, int key, int value) { if (!map.TryGetValue(key, out var values)) map[key] = values = []; values.Add(value); }
}
