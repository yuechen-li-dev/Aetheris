using System.Globalization;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Step242;

/// <summary>Semantic AP242 annotation graph with source topology correspondence and an unchanged-body witness.
/// Source presentation glyphs are excluded: they can contain inch text and must be regenerated from semantics.</summary>
public sealed class Step242PmiTransfer
{
    private readonly Step242ParsedDocument document;
    private readonly BrepBody witness;
    private readonly Dictionary<int, Step242ParsedEntity> entities;
    private readonly HashSet<int> selected = [];
    private readonly HashSet<int> solidGeometry = [];
    private readonly HashSet<int> primaryProductShapes = [];
    private readonly HashSet<int> solidRepresentations = [];
    private readonly Dictionary<string, int> datumEntities;
    public IReadOnlyList<string> DatumLabels => datumEntities.Keys.ToArray();
    public int SemanticRecordCount { get; }
    public bool HasSourcePresentation { get; }

    private Step242PmiTransfer(Step242ParsedDocument document, BrepBody body)
    {
        this.document = document; witness = body.Clone();
        entities = document.Entities.ToDictionary(e => e.Id);
        foreach (var representation in document.Entities.Where(ContainsSolidDirectly)) solidRepresentations.Add(representation.Id);
        bool expanded;
        do
        {
            expanded = false;
            foreach (var relation in document.Entities.Where(e => Has(e, "SHAPE_REPRESENTATION_RELATIONSHIP")
                && !Has(e, "REPRESENTATION_RELATIONSHIP_WITH_TRANSFORMATION")))
            {
                var definition = Step242SubsetDecoder.TryGetConstructor(relation.Instance, "REPRESENTATION_RELATIONSHIP") ?? relation.Instance.PrimaryConstructor;
                var related = definition.Arguments.Skip(2).OfType<Step242EntityReference>().Select(r => r.TargetId).ToArray();
                if (related.Any(solidRepresentations.Contains))
                    foreach (var id in related) expanded |= solidRepresentations.Add(id);
            }
        } while (expanded);
        foreach (var definition in document.Entities.Where(e => e.Name == "SHAPE_DEFINITION_REPRESENTATION"))
            if (definition.Arguments.ElementAtOrDefault(0) is Step242EntityReference shape
                && entities.TryGetValue(shape.TargetId, out var productShape) && productShape.Name == "PRODUCT_DEFINITION_SHAPE"
                && productShape.Arguments.ElementAtOrDefault(2) is Step242EntityReference product
                && entities.TryGetValue(product.TargetId, out var owner) && Has(owner, "PRODUCT_DEFINITION")
                && definition.Arguments.ElementAtOrDefault(1) is Step242EntityReference representation
                && entities.TryGetValue(representation.TargetId, out var rep) && IsSolidRepresentation(rep))
                primaryProductShapes.Add(shape.TargetId);
        var pendingGeometry = new Stack<int>(document.Entities.Where(e => e.Name is "MANIFOLD_SOLID_BREP" or "BREP_WITH_VOIDS").Select(e => e.Id));
        while (pendingGeometry.TryPop(out var geometryId))
            if (solidGeometry.Add(geometryId) && entities.TryGetValue(geometryId, out var geometry))
                foreach (var child in References(geometry.Instance)) pendingGeometry.Push(child);
        foreach (var entity in document.Entities.Where(IsSemanticRoot)) selected.Add(entity.Id);
        SemanticRecordCount = selected.Count;
        if (SemanticRecordCount > 0 && document.Entities.Count(e => e.Name is "MANIFOLD_SOLID_BREP" or "BREP_WITH_VOIDS") != 1)
            throw new PmiTransferException("pmi-source-solid-scope-unsupported: Semantic transfer requires exactly one exact solid; product/occurrence PMI needs an assembly-aware transfer.");
        HasSourcePresentation = document.Entities.Any(e => e.Name.StartsWith("ANNOTATION_", StringComparison.Ordinal)
            || e.Name is "DRAUGHTING_MODEL" or "TESSELLATED_ANNOTATION_OCCURRENCE");
        // Follow semantic dependencies and inverse association owners. Never pull graphical glyphs
        // into the semantic graph merely because they also reference a shape aspect.
        bool changed;
        do
        {
            changed = false;
            foreach (var id in selected.ToArray())
            {
                if (IsBoundary(entities[id])) continue;
                foreach (var reference in References(entities[id].Instance))
                {
                    if (!entities.ContainsKey(reference)) throw new PmiTransferException($"Missing annotation dependency #{reference} from #{id}.");
                    changed |= selected.Add(reference);
                }
            }
            foreach (var entity in document.Entities.Where(IsAssociation))
                if (References(entity.Instance).Any(id => selected.Contains(id) && !IsBoundary(entities[id]))) changed |= selected.Add(entity.Id);
        } while (changed);
        var semanticDependencies = selected.Where(id => !IsBoundary(entities[id]) &&
            (IsSemanticRoot(entities[id]) || entities[id].Name.Contains("SHAPE_ASPECT", StringComparison.Ordinal)
                || entities[id].Name.StartsWith("DATUM_", StringComparison.Ordinal) || entities[id].Name is "SHAPE_DIMENSION_REPRESENTATION" or "TOLERANCE_ZONE")).ToHashSet();
        var omitted = document.Entities.FirstOrDefault(e => !selected.Contains(e.Id)
            && !IsPresentation(e) && References(e.Instance).Any(semanticDependencies.Contains));
        if (omitted is not null) throw new PmiTransferException($"Annotation owner #{omitted.Id} ({omitted.Name}) is not yet qualified for transfer; retaining a partial semantic graph would lose intent.");
        if (SemanticRecordCount > 0 && primaryProductShapes.Count != 1)
            throw new PmiTransferException("pmi-source-product-scope-unsupported: Cannot identify a unique product definition for the exact solid.");
        datumEntities = document.Entities.Select(e => (Entity: e, Datum: Step242SubsetDecoder.TryGetConstructor(e.Instance, "DATUM")))
            .Where(item => selected.Contains(item.Entity.Id) && item.Datum?.Arguments.ElementAtOrDefault(4) is Step242StringValue { Value.Length: > 0 })
            .GroupBy(item => ((Step242StringValue)item.Datum!.Arguments[4]).Value, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1).ToDictionary(group => group.Key, group => group.Single().Entity.Id, StringComparer.Ordinal);
    }

    public static KernelResult<Step242PmiTransfer> Read(string source, BrepBody body)
    {
        var sourceHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source)));
        if (body.SourceStepTextHash != sourceHash)
            return Failure<Step242PmiTransfer>("pmi-source-body-mismatch", "The body must come from Step242Importer.ImportBody on this exact source. Matching entity numbers alone do not prove annotation ownership.");
        var parsed = Step242SubsetParser.Parse(source);
        if (!parsed.IsSuccess) return KernelResult<Step242PmiTransfer>.Failure(parsed.Diagnostics);
        try
        {
            var transfer = new Step242PmiTransfer(parsed.Value, body);
            foreach (var entity in parsed.Value.Entities)
            {
                var tolerance = GeometricTolerance(entity);
                if (tolerance is null) continue;
                if (tolerance.Arguments.ElementAtOrDefault(2) is not Step242EntityReference magnitude || !transfer.entities.TryGetValue(magnitude.TargetId, out var measure))
                    throw new PmiTransferException($"Tolerance #{entity.Id} has no typed magnitude.");
                var value = Step242SubsetDecoder.TryGetConstructor(measure.Instance, "MEASURE_WITH_UNIT") ?? measure.Instance.PrimaryConstructor;
                if (value.Arguments.ElementAtOrDefault(0) is not Step242TypedValue { Name: "LENGTH_MEASURE" or "POSITIVE_LENGTH_MEASURE", Arguments: [Step242NumberValue number] }
                    || !double.IsFinite(number.Value) || number.Value < 0
                    || value.Arguments.ElementAtOrDefault(1) is not Step242EntityReference unit || !transfer.entities.TryGetValue(unit.TargetId, out var unitEntity) || !Has(unitEntity, "LENGTH_UNIT"))
                    throw new PmiTransferException($"Tolerance #{entity.Id} requires a finite nonnegative length measure with a declared length unit.");
            }
            if (transfer.SemanticRecordCount == 0 && transfer.HasSourcePresentation)
                return Failure<Step242PmiTransfer>("pmi-semantic-representation-missing", "Source has graphical annotations without a supported semantic representation; metric regeneration cannot be proved.");
            return KernelResult<Step242PmiTransfer>.Success(transfer);
        }
        catch (PmiTransferException error) { return Failure<Step242PmiTransfer>("pmi-source-dependency-invalid", error.Message); }
    }

    internal KernelResult<bool> Emit(Step242TextWriter writer, BrepBody body, string shape, string representation,
        string context, string millimetreUnit, string brep, IReadOnlyDictionary<FaceId, string> faces, IReadOnlyDictionary<EdgeId, string> edges,
        bool requireNonRationalAuxiliaryGeometry, out IReadOnlyDictionary<string, string> emittedDatums)
    {
        emittedDatums = new Dictionary<string, string>();
        if (!Unchanged(body)) return Failure<bool>("pmi-source-body-changed", "Imported semantic PMI requires unchanged geometry and topology. Rebind annotations explicitly after editing, or request geometryOnly before editing.");
        try
        {
            var mapping = new Dictionary<int, string>();
            foreach (var face in body.Bindings.FaceBindings)
                if (face.SourceStepEntityId is { } id && faces.TryGetValue(face.FaceId, out var target) && !mapping.TryAdd(id, target))
                    throw new PmiTransferException($"Source face #{id} has multiple descendants; annotation rebinding is not qualified for split topology.");
            foreach (var edge in body.Bindings.EdgeBindings)
                if (edge.SourceStepEntityId is { } id && edges.TryGetValue(edge.EdgeId, out var target) && !mapping.TryAdd(id, target))
                    throw new PmiTransferException($"Source edge #{id} has multiple descendants; annotation rebinding is not qualified for split topology.");
            foreach (var id in selected)
            {
                var entity = entities[id];
                if (Has(entity, "LENGTH_UNIT")) { mapping[id] = millimetreUnit; continue; }
                if (Has(entity, "GLOBAL_UNIT_ASSIGNED_CONTEXT")) { mapping[id] = context; continue; }
                if (IsProductShape(entity)) { mapping[id] = shape; continue; }
                if (IsSolidRepresentation(entity)) { mapping[id] = representation; continue; }
                if (entity.Name is "MANIFOLD_SOLID_BREP" or "BREP_WITH_VOIDS") { mapping[id] = brep; continue; }
                if (entity.Name is "ADVANCED_FACE" or "EDGE_CURVE" && solidGeometry.Contains(id) && !mapping.ContainsKey(id))
                    throw new PmiTransferException($"Source geometry #{id} ({entity.Name}) has no proved topology correspondence.");
            }
            var copied = selected.Where(id => !mapping.ContainsKey(id)).Order().ToArray();
            if (requireNonRationalAuxiliaryGeometry && copied.FirstOrDefault(id => Has(entities[id], "RATIONAL_B_SPLINE_SURFACE")
                || Has(entities[id], "RATIONAL_B_SPLINE_CURVE")) is var rational && rational != 0)
                throw new PmiTransferException($"pmi-source-rational-geometry-not-canonical: Auxiliary annotation geometry #{rational} requires analytic or non-rational recovery before production export.");
            if (document.SourceMillimetresPerUnit != 1d && copied.Any(id => entities[id].Name == "PCURVE"))
                throw new PmiTransferException("pmi-source-auxiliary-pcurve-units-unsupported: Auxiliary PMI surfaces carry non-metric pcurves whose length domains cannot yet be normalized safely.");
            for (var i = 0; i < copied.Length; i++) mapping[copied[i]] = $"#{writer.EntityCount + i + 1}";
            // Materialize everything before mutating the writer: a rejected transfer emits no partial graph.
            var instances = copied.Select(id => Serialize(entities[id].Instance, mapping)).ToArray();
            foreach (var instance in instances) writer.AddRawEntity(instance);
            emittedDatums = datumEntities.ToDictionary(item => item.Key, item => mapping[item.Value], StringComparer.Ordinal);
            IReadOnlyList<KernelDiagnostic> warnings = HasSourcePresentation ? [new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Warning,
                "pmi-source-presentation-excluded: Semantic PMI, units and geometry associations were retained. Foreign graphical glyphs were excluded; regenerate metric labels from semantic PMI.", "Step242.Pmi")] : [];
            return KernelResult<bool>.Success(true, warnings);
        }
        catch (PmiTransferException error) { return Failure<bool>("pmi-source-transfer-unsupported", error.Message); }
    }

    private bool Unchanged(BrepBody body) =>
        witness.Topology.Vertices.SequenceEqual(body.Topology.Vertices) && witness.Topology.Edges.SequenceEqual(body.Topology.Edges)
        && witness.Topology.Coedges.SequenceEqual(body.Topology.Coedges) && witness.Topology.Loops.SequenceEqual(body.Topology.Loops)
        && witness.Topology.Faces.SequenceEqual(body.Topology.Faces) && witness.Topology.Shells.SequenceEqual(body.Topology.Shells)
        && witness.Topology.Bodies.SequenceEqual(body.Topology.Bodies)
        && witness.Geometry.Curves.SequenceEqual(body.Geometry.Curves) && witness.Geometry.Surfaces.SequenceEqual(body.Geometry.Surfaces)
        && witness.Bindings.FaceBindings.SequenceEqual(body.Bindings.FaceBindings) && witness.Bindings.EdgeBindings.SequenceEqual(body.Bindings.EdgeBindings)
        // Optional UV acceleration may be omitted/reconstructed by strict export.
        // The topology, 3D curves/supports, edge trims and face associations above
        // remain the identity witness for semantic PMI.
        && witness.Topology.Vertices.All(v => witness.TryGetVertexPoint(v.Id, out var before) == body.TryGetVertexPoint(v.Id, out var after) && before == after);

    internal static Step242EntityConstructor? GeometricTolerance(Step242ParsedEntity entity) =>
        Step242SubsetDecoder.TryGetConstructor(entity.Instance, "GEOMETRIC_TOLERANCE")
        ?? (entity.Instance is Step242SimpleEntityInstance && entity.Name.EndsWith("_TOLERANCE", StringComparison.Ordinal)
            && entity.Name != "PLUS_MINUS_TOLERANCE" ? entity.Instance.PrimaryConstructor : null);

    internal static bool IsSemanticRoot(Step242ParsedEntity e) => GeometricTolerance(e) is not null
        || Has(e, "DIMENSIONAL_SIZE") || Has(e, "DIMENSIONAL_LOCATION") || Has(e, "DATUM_FEATURE")
        || e.Name is "DIMENSIONAL_SIZE" or "ANGULAR_SIZE" or "DIMENSIONAL_LOCATION" or "DIRECTED_DIMENSIONAL_LOCATION" or "ANGULAR_LOCATION" or "DATUM_FEATURE" or "DATUM_TARGET" or "PLACED_DATUM_TARGET_FEATURE" or "DATUM"
            or "DATUM_SYSTEM" or "DATUM_REFERENCE" or "DATUM_REFERENCE_COMPARTMENT" or "DATUM_REFERENCE_ELEMENT"
        || e.Name == "PROPERTY_DEFINITION" && e.Arguments.FirstOrDefault() is Step242StringValue name
            && new[] { "diameter:", "dimension:", "note:" }.Any(prefix => name.Value.StartsWith(prefix, StringComparison.Ordinal))
        // Compatibility with prior Aetheris exports: these named association aspects were
        // separate from the property aspect. Preserve their explicit native identity receipt.
        || e.Name == "SHAPE_ASPECT" && e.Arguments.FirstOrDefault() is Step242StringValue aspectName
            && aspectName.Value.StartsWith("firmament-geometric-target:", StringComparison.Ordinal);
    private static bool IsAssociation(Step242ParsedEntity e) => e.Name is
        "SHAPE_ASPECT_RELATIONSHIP" or "SHAPE_ASPECT_DERIVING_RELATIONSHIP" or "GEOMETRIC_ITEM_SPECIFIC_USAGE"
        or "DIMENSIONAL_CHARACTERISTIC_REPRESENTATION" or "PLUS_MINUS_TOLERANCE" or "TOLERANCE_ZONE"
        or "TOLERANCE_ZONE_DEFINITION" or "PROJECTED_ZONE_DEFINITION" or "RUNOUT_ZONE_DEFINITION"
        or "GEOMETRIC_TOLERANCE_RELATIONSHIP" or "PROPERTY_DEFINITION_REPRESENTATION" or "MEASURE_QUALIFICATION"
        or "QUALIFIED_REPRESENTATION_ITEM" or "PROPERTY_DEFINITION" or "ID_ATTRIBUTE" or "NAME_ATTRIBUTE" or "DESCRIPTION_ATTRIBUTE"
        or "FEATURE_FOR_DATUM_TARGET_RELATIONSHIP" or "SHAPE_DEFINITION_REPRESENTATION"
        // Retain auxiliary face ownership so an importer cannot mistake a datum face for
        // an orphan belonging to the material solid.
        or "OPEN_SHELL" or "CLOSED_SHELL";
    private static bool IsPresentation(Step242ParsedEntity e) => e.Name.Contains("ANNOTATION", StringComparison.Ordinal)
        || e.Name.Contains("DRAUGHTING", StringComparison.Ordinal) || e.Name.Contains("PRESENTATION", StringComparison.Ordinal)
        || e.Name is "STYLED_ITEM" or "OVER_RIDING_STYLED_ITEM" or "INVISIBILITY";
    private bool IsSolidRepresentation(Step242ParsedEntity e) => solidRepresentations.Contains(e.Id);
    private bool ContainsSolidDirectly(Step242ParsedEntity e) => e.Name is "SHAPE_REPRESENTATION" or "ADVANCED_BREP_SHAPE_REPRESENTATION"
        && e.Arguments.ElementAtOrDefault(1) is Step242ListValue list
        && list.Items.OfType<Step242EntityReference>().Any(r => entities.TryGetValue(r.TargetId, out var item) && item.Name is "MANIFOLD_SOLID_BREP" or "BREP_WITH_VOIDS");
    // Auxiliary datum geometry may own separate products. Preserve those graphs; only the
    // product that owns the exact solid is rebound to the output part.
    private bool IsProductShape(Step242ParsedEntity e) => primaryProductShapes.Contains(e.Id);
    private bool IsBoundary(Step242ParsedEntity e) => Has(e, "LENGTH_UNIT") || Has(e, "GLOBAL_UNIT_ASSIGNED_CONTEXT")
        || e.Name is "ADVANCED_FACE" or "EDGE_CURVE" && solidGeometry.Contains(e.Id)
        || e.Name is "MANIFOLD_SOLID_BREP" or "BREP_WITH_VOIDS" || IsProductShape(e) || IsSolidRepresentation(e);
    private static bool Has(Step242ParsedEntity e, string kind) => Step242SubsetDecoder.TryGetConstructor(e.Instance, kind) is not null;
    internal static IEnumerable<int> References(Step242EntityInstance instance) => Constructors(instance).SelectMany(c => c.Arguments).SelectMany(References);
    private static IEnumerable<int> References(Step242Value value) => value switch
    { Step242EntityReference r => [r.TargetId], Step242ListValue list => list.Items.SelectMany(References), Step242TypedValue typed => typed.Arguments.SelectMany(References), _ => [] };
    private static IEnumerable<Step242EntityConstructor> Constructors(Step242EntityInstance instance) => instance is Step242ComplexEntityInstance complex ? complex.Constructors : [instance.PrimaryConstructor];

    private string Serialize(Step242EntityInstance instance, Dictionary<int, string> mapping)
    {
        var constructors = Constructors(instance).Select(constructor => Serialize(constructor, mapping));
        return instance is Step242ComplexEntityInstance ? "(" + string.Concat(constructors) + ")" : constructors.Single();
    }
    private string Serialize(Step242EntityConstructor constructor, Dictionary<int, string> mapping)
    {
        var arguments = constructor.Arguments.ToArray();
        var unitIndex = constructor.Name == "MEASURE_REPRESENTATION_ITEM" ? 2 : 1;
        var valueIndex = constructor.Name == "MEASURE_REPRESENTATION_ITEM" ? 1 : 0;
        if ((constructor.Name.EndsWith("MEASURE_WITH_UNIT", StringComparison.Ordinal) || constructor.Name == "MEASURE_REPRESENTATION_ITEM")
            && arguments.ElementAtOrDefault(unitIndex) is Step242EntityReference compoundUnit && entities.TryGetValue(compoundUnit.TargetId, out var compound)
            && compound.Name == "DERIVED_UNIT")
        {
            if (!Step242LengthUnitNormalizer.TryResolvePmiScale(entities, compoundUnit.TargetId, [], out var compoundScale))
                throw new PmiTransferException($"pmi-source-compound-unit-unsupported: Cannot resolve compound unit #{compoundUnit.TargetId}.");
            if (compoundScale != 1d) arguments[valueIndex] = ScaleMeasure(arguments[valueIndex], compoundScale);
        }
        if (arguments.ElementAtOrDefault(valueIndex) is Step242TypedValue { Name: "LENGTH_MEASURE" or "POSITIVE_LENGTH_MEASURE" }
            && (arguments.ElementAtOrDefault(unitIndex) is not Step242EntityReference declaredUnit || !entities.TryGetValue(declaredUnit.TargetId, out var declared) || !Has(declared, "LENGTH_UNIT")))
            throw new PmiTransferException("Typed PMI length measure requires a resolvable named length unit.");
        if (constructor.Name is "MEASURE_WITH_UNIT" or "LENGTH_MEASURE_WITH_UNIT" or "POSITIVE_LENGTH_MEASURE_WITH_UNIT" or "UNCERTAINTY_MEASURE_WITH_UNIT" or "MEASURE_REPRESENTATION_ITEM"
            && arguments.ElementAtOrDefault(unitIndex) is Step242EntityReference unit && entities.TryGetValue(unit.TargetId, out var unitEntity) && Has(unitEntity, "LENGTH_UNIT"))
        {
            if (!Step242LengthUnitNormalizer.TryResolveMetresPerUnit(entities, unit.TargetId, [], out var metres))
                throw new PmiTransferException($"Cannot resolve length unit #{unit.TargetId} for {constructor.Name}.");
            arguments[valueIndex] = ScaleMeasure(arguments[valueIndex], metres * 1000d);
        }
        return constructor.Name + "(" + string.Join(",", arguments.Select(value => Serialize(value, mapping))) + ")";
    }
    private static Step242Value ScaleMeasure(Step242Value value, double scale) => value switch
    {
        Step242NumberValue n when double.IsFinite(n.Value * scale) => new Step242NumberValue(Step242LengthUnitNormalizer.ScalePmiMeasure(n.Value, scale)),
        Step242TypedValue t when t.Name is "LENGTH_MEASURE" or "POSITIVE_LENGTH_MEASURE" or "AREA_MEASURE" or "VOLUME_MEASURE" && t.Arguments.Count == 1 => t with { Arguments = [ScaleMeasure(t.Arguments[0], scale)] },
        _ => throw new PmiTransferException("Length-valued PMI measure is malformed or incompatible with its unit.")
    };
    private static string Serialize(Step242Value value, Dictionary<int, string> mapping) => value switch
    {
        Step242EntityReference r => mapping.TryGetValue(r.TargetId, out var id) ? id : throw new PmiTransferException($"Missing transfer mapping for #{r.TargetId}."),
        Step242StringValue s => Step242TextWriter.String(s.Value),
        Step242NumberValue n => n.Value.ToString("R", CultureInfo.InvariantCulture),
        Step242EnumValue e => Step242TextWriter.Enum(e.Value),
        Step242BooleanValue b => Step242TextWriter.BooleanLogical(b.Value),
        Step242OmittedValue omitted => omitted.IsDerived ? "*" : "$",
        Step242ListValue list => "(" + string.Join(",", list.Items.Select(item => Serialize(item, mapping))) + ")",
        Step242TypedValue t => t.Name + "(" + string.Join(",", t.Arguments.Select(item => Serialize(item, mapping))) + ")",
        _ => throw new PmiTransferException("Unsupported STEP annotation value.")
    };
    private sealed class PmiTransferException(string message) : Exception(message);
    private static KernelResult<T> Failure<T>(string code, string message) => KernelResult<T>.Failure([new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, $"{code}: {message}", "Step242.Pmi")]);
}
