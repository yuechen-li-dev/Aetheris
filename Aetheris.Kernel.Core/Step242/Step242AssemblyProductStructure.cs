using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Step242;

public sealed record Step242AssemblyDefinition(string StableId, string Name, BrepBody? Body);

public sealed record Step242AssemblyOccurrence(
    string StableId,
    string Name,
    string? ParentStableId,
    string? DefinitionStableId,
    IReadOnlyList<double> LocalTransform);

public sealed record Step242AssemblyExportModel(
    string Name,
    string RootOccurrenceStableId,
    IReadOnlyList<Step242AssemblyDefinition> Definitions,
    IReadOnlyList<Step242AssemblyOccurrence> Occurrences);

public sealed record Step242ImportedProductDefinition(
    string StableId,
    string Name,
    int ProductDefinitionEntityId,
    int? RepresentationEntityId,
    int? RigidRootEntityId,
    BrepBody? Geometry,
    string? GeometrySha256);

public sealed record Step242ImportedProductOccurrence(
    string StableId,
    string Name,
    string? ParentStableId,
    string DefinitionStableId,
    IReadOnlyList<double> LocalTransform,
    int StepEntityId);

public sealed record Step242ProductStructure(
    string RootDefinitionStableId,
    IReadOnlyList<Step242ImportedProductDefinition> Definitions,
    IReadOnlyList<Step242ImportedProductOccurrence> Occurrences);

/// <summary>Bounded AP242 product-structure lowering: product definitions, NAUO occurrences, rigid transforms, and shared shape representations.</summary>
public static class Step242AssemblyExporter
{
    public static KernelResult<string> Export(Step242AssemblyExportModel model)
    {
        var diagnostics = Validate(model);
        if (diagnostics.Count > 0) return KernelResult<string>.Failure(diagnostics);

        var entities = new List<string>();
        var definitionRefs = new Dictionary<string, (int ProductDefinition, int Representation)>(StringComparer.Ordinal);
        foreach (var definition in model.Definitions.Where(item => item.Body is not null).OrderBy(item => item.StableId, StringComparer.Ordinal))
        {
            var exported = Step242Exporter.ExportBody(definition.Body!, options: new Step242ExportOptions
            {
                ProductId = definition.StableId,
                ProductName = definition.Name,
                ProductDescription = "Aetheris shared assembly definition"
            });
            if (!exported.IsSuccess) return KernelResult<string>.Failure(exported.Diagnostics);
            var block = DataEntities(exported.Value);
            var offset = entities.Count;
            var remapped = block.Select(line => Remap(line, offset)).ToArray();
            var productDefinition = FindEntityId(block, "PRODUCT_DEFINITION") + offset;
            var representation = FindEntityId(block, "SHAPE_REPRESENTATION") + offset;
            entities.AddRange(remapped);
            definitionRefs[definition.StableId] = (productDefinition, representation);
        }

        int Add(string name, params string[] args)
        {
            var id = entities.Count + 1;
            entities.Add($"#{id}={name}({string.Join(",", args)});");
            return id;
        }
        int AddRaw(string instance)
        {
            var id = entities.Count + 1;
            entities.Add($"#{id}={instance};");
            return id;
        }
        string Ref(int id) => $"#{id}";
        string Str(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
        string Num(double value) => Step242TextWriter.Number(value);

        var app = Add("APPLICATION_CONTEXT", Str("mechanical design"));
        var productContext = Add("PRODUCT_CONTEXT", Str(""), Ref(app), Str("mechanical"));
        var definitionContext = Add("PRODUCT_DEFINITION_CONTEXT", Str("design"), Ref(app), Str("design"));
        var lengthUnit = AddRaw("(LENGTH_UNIT()NAMED_UNIT(*)SI_UNIT(.MILLI.,.METRE.))");
        var angleUnit = AddRaw("(NAMED_UNIT(*)PLANE_ANGLE_UNIT()SI_UNIT($,.RADIAN.))");
        var solidAngleUnit = AddRaw("(NAMED_UNIT(*)SI_UNIT($,.STERADIAN.)SOLID_ANGLE_UNIT())");
        var representationContext = AddRaw($"(GEOMETRIC_REPRESENTATION_CONTEXT(3)GLOBAL_UNIT_ASSIGNED_CONTEXT(({Ref(lengthUnit)},{Ref(angleUnit)},{Ref(solidAngleUnit)}))REPRESENTATION_CONTEXT('3','3D'))");

        foreach (var definition in model.Definitions.Where(item => item.Body is null).OrderBy(item => item.StableId, StringComparer.Ordinal))
        {
            var product = Add("PRODUCT", Str(definition.StableId), Str(definition.Name), Str("Aetheris reusable assembly definition"), $"({Ref(productContext)})");
            var formation = Add("PRODUCT_DEFINITION_FORMATION", Str(""), Str(""), Ref(product));
            var productDefinition = Add("PRODUCT_DEFINITION", Str(definition.StableId), Str(""), Ref(formation), Ref(definitionContext));
            var shape = Add("PRODUCT_DEFINITION_SHAPE", Str(""), Str(""), Ref(productDefinition));
            var representation = Add("SHAPE_REPRESENTATION", Str(definition.Name), "()", Ref(representationContext));
            Add("SHAPE_DEFINITION_REPRESENTATION", Ref(shape), Ref(representation));
            definitionRefs[definition.StableId] = (productDefinition, representation);
        }

        var assemblyRefs = new Dictionary<string, (int ProductDefinition, int Representation)>(StringComparer.Ordinal);
        foreach (var occurrence in model.Occurrences.Where(item => item.DefinitionStableId is null).OrderBy(item => item.StableId, StringComparer.Ordinal))
        {
            var product = Add("PRODUCT", Str(occurrence.StableId), Str(occurrence.Name), Str("Aetheris assembly product"), $"({Ref(productContext)})");
            var formation = Add("PRODUCT_DEFINITION_FORMATION", Str(""), Str(""), Ref(product));
            var definition = Add("PRODUCT_DEFINITION", Str(occurrence.StableId), Str(""), Ref(formation), Ref(definitionContext));
            var shape = Add("PRODUCT_DEFINITION_SHAPE", Str(""), Str(""), Ref(definition));
            var representation = Add("SHAPE_REPRESENTATION", Str(occurrence.Name), "()", Ref(representationContext));
            Add("SHAPE_DEFINITION_REPRESENTATION", Ref(shape), Ref(representation));
            assemblyRefs[occurrence.StableId] = (definition, representation);
        }

        var identityPoint = Add("CARTESIAN_POINT", Str(""), "(0.,0.,0.)");
        var identityAxis = Add("AXIS2_PLACEMENT_3D", Str(""), Ref(identityPoint), "$", "$" );
        var emittedDefinitionLocalUsages = new HashSet<string>(StringComparer.Ordinal);
        foreach (var occurrence in model.Occurrences.Where(item => item.ParentStableId is not null).OrderBy(item => item.StableId, StringComparer.Ordinal))
        {
            var parentOccurrence = model.Occurrences.Single(item => item.StableId == occurrence.ParentStableId);
            var parent = parentOccurrence.DefinitionStableId is not null ? definitionRefs[parentOccurrence.DefinitionStableId] : assemblyRefs[parentOccurrence.StableId];
            var child = occurrence.DefinitionStableId is null ? assemblyRefs[occurrence.StableId] : definitionRefs[occurrence.DefinitionStableId];
            var definitionLocalKey = parentOccurrence.DefinitionStableId is null ? occurrence.StableId
                : parentOccurrence.DefinitionStableId + "|" + occurrence.Name + "|" + (occurrence.DefinitionStableId ?? occurrence.StableId) + "|" + string.Join(",", occurrence.LocalTransform.Select(Num));
            if (!emittedDefinitionLocalUsages.Add(definitionLocalKey)) continue;
            var usageStableId = parentOccurrence.DefinitionStableId is null ? occurrence.StableId : "definition-usage:" + definitionLocalKey;
            var usage = Add("NEXT_ASSEMBLY_USAGE_OCCURRENCE", Str(usageStableId), Str(occurrence.Name), Str(""), Ref(parent.ProductDefinition), Ref(child.ProductDefinition), "$" );
            var usageShape = Add("PRODUCT_DEFINITION_SHAPE", Str(""), Str(""), Ref(usage));
            var m = occurrence.LocalTransform;
            var point = Add("CARTESIAN_POINT", Str(""), $"({Num(m[12])},{Num(m[13])},{Num(m[14])})");
            var z = Add("DIRECTION", Str(""), $"({Num(m[8])},{Num(m[9])},{Num(m[10])})");
            var x = Add("DIRECTION", Str(""), $"({Num(m[0])},{Num(m[1])},{Num(m[2])})");
            var placement = Add("AXIS2_PLACEMENT_3D", Str(""), Ref(point), Ref(z), Ref(x));
            var transformation = Add("ITEM_DEFINED_TRANSFORMATION", Str(usageStableId), Str("Aetheris rigid occurrence transform"), Ref(identityAxis), Ref(placement));
            // AP242 maps representation_1 (the child) into representation_2 (the parent)
            // using the item-defined occurrence transform. Writing the parent first reverses
            // that mapping and makes standards-compliant consumers apply the inverse placement.
            var relationship = AddRaw($"(REPRESENTATION_RELATIONSHIP({Str(usageStableId)},{Str("")},{Ref(child.Representation)},{Ref(parent.Representation)})" +
                $"REPRESENTATION_RELATIONSHIP_WITH_TRANSFORMATION({Ref(transformation)})SHAPE_REPRESENTATION_RELATIONSHIP())");
            Add("CONTEXT_DEPENDENT_SHAPE_REPRESENTATION", Ref(relationship), Ref(usageShape));
        }

        var builder = new StringBuilder();
        Step242TextWriter.AppendCanonicalLine(builder, "ISO-10303-21;");
        Step242TextWriter.AppendCanonicalLine(builder, "HEADER;");
        Step242TextWriter.AppendCanonicalLine(builder, "FILE_DESCRIPTION(('Aetheris AP242 assembly product structure'),'2;1');");
        Step242TextWriter.AppendCanonicalLine(builder, "FILE_NAME('aetheris_assembly.step','1970-01-01T00:00:00',('Aetheris'),('Aetheris'),'Aetheris.Kernel','Aetheris.Kernel','');");
        Step242TextWriter.AppendCanonicalLine(builder, "FILE_SCHEMA(('AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF'));");
        Step242TextWriter.AppendCanonicalLine(builder, "ENDSEC;");
        Step242TextWriter.AppendCanonicalLine(builder, "DATA;");
        foreach (var entity in entities) Step242TextWriter.AppendCanonicalLine(builder, entity);
        Step242TextWriter.AppendCanonicalLine(builder, "ENDSEC;");
        Step242TextWriter.AppendCanonicalLine(builder, "END-ISO-10303-21;");
        return KernelResult<string>.Success(builder.ToString());
    }

    private static IReadOnlyList<KernelDiagnostic> Validate(Step242AssemblyExportModel model)
    {
        var diagnostics = new List<KernelDiagnostic>();
        var occurrences = model.Occurrences.ToDictionary(item => item.StableId, StringComparer.Ordinal);
        var definitions = model.Definitions.Select(item => item.StableId).ToHashSet(StringComparer.Ordinal);
        if (!occurrences.TryGetValue(model.RootOccurrenceStableId, out var root) || root.ParentStableId is not null || root.DefinitionStableId is not null)
            diagnostics.Add(Diagnostic("AP242 assembly root must identify a root assembly occurrence.", "Exporter.Assembly.Root"));
        foreach (var occurrence in model.Occurrences)
        {
            if (occurrence.ParentStableId is not null && !occurrences.ContainsKey(occurrence.ParentStableId)) diagnostics.Add(Diagnostic($"Occurrence '{occurrence.StableId}' has missing parent '{occurrence.ParentStableId}'.", "Exporter.Assembly.UnresolvedParent"));
            if (occurrence.DefinitionStableId is not null && !definitions.Contains(occurrence.DefinitionStableId)) diagnostics.Add(Diagnostic($"Occurrence '{occurrence.StableId}' has missing definition '{occurrence.DefinitionStableId}'.", "Exporter.Assembly.MissingGeometryDefinition"));
            if (occurrence.LocalTransform.Count != 16 || occurrence.LocalTransform.Any(value => !double.IsFinite(value))) diagnostics.Add(Diagnostic($"Occurrence '{occurrence.StableId}' has an invalid transform.", "Exporter.Assembly.UnsupportedOccurrenceTransform"));
            var visited = new HashSet<string>(StringComparer.Ordinal); var current = occurrence;
            while (current.ParentStableId is not null && occurrences.TryGetValue(current.ParentStableId, out current!))
                if (!visited.Add(current.StableId)) { diagnostics.Add(Diagnostic("Assembly occurrence hierarchy is cyclic.", "Exporter.Assembly.Cycle")); break; }
        }
        return diagnostics;
    }

    private static KernelDiagnostic Diagnostic(string message, string source) => new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message, source);
    private static string[] DataEntities(string step) => step[(step.IndexOf("DATA;", StringComparison.Ordinal) + 5)..step.LastIndexOf("ENDSEC;", StringComparison.Ordinal)].Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static int FindEntityId(IEnumerable<string> lines, string name) => lines.Select(line => Regex.Match(line, $@"^#(?<id>\d+)={name}\(", RegexOptions.CultureInvariant)).First(match => match.Success).Groups["id"].Value is var value ? int.Parse(value, CultureInfo.InvariantCulture) : 0;
    private static string Remap(string line, int offset) => Regex.Replace(line, @"#(?<id>\d+)", match => "#" + (int.Parse(match.Groups["id"].Value, CultureInfo.InvariantCulture) + offset).ToString(CultureInfo.InvariantCulture));
}

public static class Step242AssemblyImporter
{
    public static KernelResult<Step242ProductStructure> Import(string stepText)
    {
        var parsed = Step242SubsetParser.Parse(stepText);
        if (!parsed.IsSuccess) return KernelResult<Step242ProductStructure>.Failure(parsed.Diagnostics);
        var document = parsed.Value;
        var products = document.Entities.Where(entity => entity.Name == "PRODUCT").ToDictionary(entity => entity.Id);
        var formations = document.Entities.Where(entity => entity.Name == "PRODUCT_DEFINITION_FORMATION").ToDictionary(entity => entity.Id);
        var definitionsByEntity = document.Entities.Where(entity => entity.Name == "PRODUCT_DEFINITION").ToDictionary(entity => entity.Id);
        var shapeByDefinition = document.Entities.Where(entity => entity.Name == "PRODUCT_DEFINITION_SHAPE")
            .Where(entity => Ref(entity, 2) is not null && definitionsByEntity.ContainsKey(Ref(entity, 2)!.Value))
            .ToDictionary(entity => Ref(entity, 2)!.Value, entity => entity.Id);
        var representationsByShape = document.Entities.Where(entity => entity.Name == "SHAPE_DEFINITION_REPRESENTATION")
            .Where(entity => Ref(entity, 0) is not null && Ref(entity, 1) is not null)
            .GroupBy(entity => Ref(entity, 0)!.Value)
            .ToDictionary(group => group.Key, group => group.Select(entity => Ref(entity, 1)!.Value).Distinct().ToArray());

        var importedDefinitions = new List<Step242ImportedProductDefinition>();
        var stableByPd = new Dictionary<int, string>();
        foreach (var definition in definitionsByEntity.Values.OrderBy(entity => entity.Id))
        {
            var formationId = Ref(definition, 2); if (formationId is null || !formations.TryGetValue(formationId.Value, out var formation)) continue;
            var productId = Ref(formation, 2); if (productId is null || !products.TryGetValue(productId.Value, out var product)) continue;
            var stableId = Text(product, 0) ?? $"step-product:{product.Id}";
            var name = Text(product, 1) ?? stableId;
            stableByPd[definition.Id] = stableId;
            int? representationId = shapeByDefinition.TryGetValue(definition.Id, out var shapeId) && representationsByShape.TryGetValue(shapeId, out var repIds)
                ? repIds.FirstOrDefault(repId => document.TryGetEntity(repId).Value.Arguments.ElementAtOrDefault(1) is Step242ListValue items
                    && items.Items.OfType<Step242EntityReference>().Any(item => document.TryGetEntity(item.TargetId).Value.Name is "MANIFOLD_SOLID_BREP" or "BREP_WITH_VOIDS")) is var selected && selected != 0 ? selected : repIds.FirstOrDefault()
                : null;
            int? rigidRoot = null;
            if (representationId is not null && document.TryGetEntity(representationId.Value).Value is { } representation && representation.Arguments.ElementAtOrDefault(1) is Step242ListValue items)
                rigidRoot = items.Items.OfType<Step242EntityReference>().Select(item => item.TargetId).FirstOrDefault(id => document.TryGetEntity(id).Value.Name is "MANIFOLD_SOLID_BREP" or "BREP_WITH_VOIDS") is var candidate && candidate != 0 ? candidate : null;
            BrepBody? body = null; string? hash = null;
            if (rigidRoot is not null)
            {
                var geometry = Step242Importer.ImportExactBrepCore(document, rigidRoot.Value);
                if (!geometry.IsSuccess) return KernelResult<Step242ProductStructure>.Failure(geometry.Diagnostics);
                body = geometry.Value;
                hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Step242Exporter.ExportBody(body).Value)));
            }
            importedDefinitions.Add(new(stableId, name, definition.Id, representationId, rigidRoot, body, hash));
        }

        var usages = document.Entities.Where(entity => entity.Name == "NEXT_ASSEMBLY_USAGE_OCCURRENCE").OrderBy(entity => entity.Id).ToArray();
        if (usages.Length == 0)
        {
            var geometricDefinitions = importedDefinitions.Where(definition => definition.Geometry is not null).OrderBy(definition => definition.ProductDefinitionEntityId).ToArray();
            if (geometricDefinitions.Length <= 1)
                return KernelResult<Step242ProductStructure>.Failure([new(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, "STEP contains neither an admitted AP242 occurrence hierarchy nor ambiguous multipart multiplicity; retain the ordinary single-part import path.", "Importer.Assembly.ProductStructure")]);
            var flat = geometricDefinitions.Select((definition, index) => new Step242ImportedProductOccurrence(
                $"normalized-occurrence:{index + 1:D4}", definition.Name, null, definition.StableId, Identity(), definition.RigidRootEntityId ?? definition.ProductDefinitionEntityId)).ToArray();
            return KernelResult<Step242ProductStructure>.Success(new("aetheris:normalized-multipart-assembly", importedDefinitions, flat), [
                new(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Info,
                    "Incoming STEP provides multiple independent rigid products without trustworthy hierarchy; Aetheris normalized multiplicity to a flat Assembly.", "Importer.Assembly.MultiplicityNormalization")]);
        }
        var occurrenceStableByUsage = usages.ToDictionary(entity => entity.Id, entity => Text(entity, 0) ?? $"step-occurrence:{entity.Id}");
        var usageByShape = document.Entities.Where(entity => entity.Name == "PRODUCT_DEFINITION_SHAPE").Where(entity => Ref(entity, 2) is { } id && occurrenceStableByUsage.ContainsKey(id)).ToDictionary(entity => entity.Id, entity => Ref(entity, 2)!.Value);
        var transformByUsage = new Dictionary<int, double[]>();
        foreach (var cdsr in document.Entities.Where(entity => Has(entity, "CONTEXT_DEPENDENT_SHAPE_REPRESENTATION")))
        {
            var relationshipId = Ref(cdsr, 0); var usageShapeId = Ref(cdsr, 1);
            if (relationshipId is null || usageShapeId is null || !usageByShape.TryGetValue(usageShapeId.Value, out var usageId)) continue;
            var relationship = document.TryGetEntity(relationshipId.Value).Value;
            var relationshipWithTransform = Step242SubsetDecoder.TryGetConstructor(relationship.Instance, "REPRESENTATION_RELATIONSHIP_WITH_TRANSFORMATION");
            int? transformId = relationshipWithTransform is null ? null
                : relationshipWithTransform.Arguments.Count >= 5 && relationshipWithTransform.Arguments[4] is Step242EntityReference fullReference ? fullReference.TargetId
                : relationshipWithTransform.Arguments.ElementAtOrDefault(0) is Step242EntityReference complexReference ? complexReference.TargetId : null;
            if (transformId is null) continue;
            var transform = document.TryGetEntity(transformId.Value).Value;
            var placementId = Ref(transform, 3); if (placementId is not null) transformByUsage[usageId] = Placement(document, placementId.Value);
        }
        var childPds = usages.Select(usage => Ref(usage, 4)!.Value).ToHashSet();
        var rootPd = usages.Select(usage => Ref(usage, 3)!.Value).First(id => !childPds.Contains(id));
        var usagesByParentDefinition = usages.GroupBy(usage => Ref(usage, 3)!.Value).ToDictionary(group => group.Key, group => group.OrderBy(item => item.Id).ToArray());
        var occurrences = new List<Step242ImportedProductOccurrence>();
        var usedOccurrenceStableIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var usage in usagesByParentDefinition.GetValueOrDefault(rootPd, [])) Expand(usage, null, 0);
        void Expand(Step242ParsedEntity usage, string? parentStableId, int depth)
        {
            if (depth > usages.Length) throw new InvalidOperationException("AP242 assembly definition hierarchy is cyclic.");
            var baseStableId = occurrenceStableByUsage[usage.Id];
            var stableId = usedOccurrenceStableIds.Add(baseStableId) ? baseStableId : parentStableId + "/" + baseStableId;
            usedOccurrenceStableIds.Add(stableId);
            var childPd = Ref(usage, 4)!.Value;
            occurrences.Add(new(stableId, Text(usage, 1) ?? baseStableId, parentStableId, stableByPd[childPd],
                transformByUsage.TryGetValue(usage.Id, out var matrix) ? matrix : Identity(), usage.Id));
            foreach (var childUsage in usagesByParentDefinition.GetValueOrDefault(childPd, [])) Expand(childUsage, stableId, depth + 1);
        }
        return KernelResult<Step242ProductStructure>.Success(new(stableByPd[rootPd], importedDefinitions, occurrences));
    }

    private static int? Ref(Step242ParsedEntity entity, int index) => entity.Arguments.ElementAtOrDefault(index) is Step242EntityReference reference ? reference.TargetId : null;
    private static string? Text(Step242ParsedEntity entity, int index) => entity.Arguments.ElementAtOrDefault(index) is Step242StringValue text ? text.Value : null;
    private static bool Has(Step242ParsedEntity entity, string constructor) => Step242SubsetDecoder.TryGetConstructor(entity.Instance, constructor) is not null;
    private static double[] Placement(Step242ParsedDocument document, int placementId)
    {
        var placement = document.TryGetEntity(placementId).Value;
        var origin = Ref(placement, 1) is { } point ? Tuple(document.TryGetEntity(point).Value) : [0d,0d,0d];
        var z = Ref(placement, 2) is { } axis ? Tuple(document.TryGetEntity(axis).Value) : [0d,0d,1d];
        var x = Ref(placement, 3) is { } reference ? Tuple(document.TryGetEntity(reference).Value) : [1d,0d,0d];
        var y = new[] { z[1]*x[2]-z[2]*x[1], z[2]*x[0]-z[0]*x[2], z[0]*x[1]-z[1]*x[0] };
        return [x[0],x[1],x[2],0, y[0],y[1],y[2],0, z[0],z[1],z[2],0, origin[0],origin[1],origin[2],1];
    }
    private static double[] Tuple(Step242ParsedEntity entity) => entity.Arguments.ElementAtOrDefault(1) is Step242ListValue list ? list.Items.OfType<Step242NumberValue>().Select(value => value.Value).ToArray() : [];
    private static double[] Identity() => [1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1];
}
