using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public enum ConceptIrValueKind { Length, Point2, Point3, Vector3, Axis, Plane, Box2, Box3, Region2, PointSet }

public sealed record ConceptIrType(string Name, bool IsCollection = false)
{
    public override string ToString() => Name + (IsCollection ? "[]" : string.Empty);
}

public sealed record ConceptIrMemberRequirement(string Name, ConceptIrType Type, FirmamentV2SourceSpan SourceSpan);
public sealed record ConceptIrDefinition(string Name, IReadOnlyDictionary<string, ConceptIrMemberRequirement> Members, FirmamentV2SourceSpan SourceSpan);
public sealed record ConceptIrPoint3(double X, double Y, double Z);
public sealed record ConceptIrVector3(double X, double Y, double Z);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$valueType")]
[JsonDerivedType(typeof(ConceptIrBox3Value), "Box3")]
[JsonDerivedType(typeof(ConceptIrPlaneValue), "Plane")]
[JsonDerivedType(typeof(ConceptIrAxisValue), "Axis")]
[JsonDerivedType(typeof(ConceptIrRegion2Value), "Region2")]
[JsonDerivedType(typeof(ConceptIrPoint3Value), "Point3")]
[JsonDerivedType(typeof(ConceptIrPointSetValue), "PointSet")]
public abstract record ConceptIrValue(string StableId, ConceptIrValueKind Kind, string Provenance);
public sealed record ConceptIrBox3Value(string StableId, ConceptIrPoint3 Min, ConceptIrPoint3 Max, string Provenance)
    : ConceptIrValue(StableId, ConceptIrValueKind.Box3, Provenance)
{
    public IReadOnlyList<double> Size => [Max.X - Min.X, Max.Y - Min.Y, Max.Z - Min.Z];
    public ConceptIrPoint3 Center => new((Min.X + Max.X) / 2d, (Min.Y + Max.Y) / 2d, (Min.Z + Max.Z) / 2d);
}
public sealed record ConceptIrPlaneValue(string StableId, ConceptIrPoint3 Origin, ConceptIrVector3 Normal, string Provenance)
    : ConceptIrValue(StableId, ConceptIrValueKind.Plane, Provenance);
public sealed record ConceptIrAxisValue(string StableId, ConceptIrPoint3 Origin, ConceptIrVector3 Direction, string Provenance)
    : ConceptIrValue(StableId, ConceptIrValueKind.Axis, Provenance);
public sealed record ConceptIrRegion2Value(string StableId, ConceptIrPoint3 Center, ConceptIrVector3 U, ConceptIrVector3 V, double MinU, double MaxU, double MinV, double MaxV, string FaceAxis, string Provenance)
    : ConceptIrValue(StableId, ConceptIrValueKind.Region2, Provenance);
public sealed record ConceptIrPoint3Value(string StableId, ConceptIrPoint3 Point, string Provenance, int? Ordinal = null)
    : ConceptIrValue(StableId, ConceptIrValueKind.Point3, Provenance);
public sealed record ConceptIrPointSetValue(string StableId, IReadOnlyList<ConceptIrPoint3Value> Points, string Provenance)
    : ConceptIrValue(StableId, ConceptIrValueKind.PointSet, Provenance);

public enum ConceptIrSemanticPhase { ConceptIr, FeatureAir }
public enum ConceptIrMaterializationCategory { CompileTimeValue, MaterializedSemanticReference }

public sealed record ConceptIrSemanticMember(
    string Name,
    ConceptIrType Type,
    ConceptIrValue? Value,
    string? SemanticReference,
    ConceptIrSemanticPhase Phase,
    ConceptIrMaterializationCategory MaterializationCategory,
    string Provenance,
    FirmamentV2SourceSpan SourceSpan,
    string StableId);

public sealed record ConceptIrStructInstance(
    string Name,
    IReadOnlyList<string> Satisfies,
    IReadOnlyDictionary<string, ConceptIrValue> Members,
    bool Materialized,
    string ErasureStatus,
    FirmamentV2SourceSpan SourceSpan);

public sealed record ConceptIrMaterializedStruct(
    string Name,
    string SourceSpelling,
    IReadOnlyList<string> Satisfies,
    FirmamentV2SourceSpan SourceSpan,
    IReadOnlyList<ConceptIrSemanticMember> ExposedMembers,
    string Conformance);
public sealed record ConceptIrBinding(string Consumer, string Input, string Provenance, string Kind);
public sealed record ConceptIrDocument(
    IReadOnlyList<ConceptIrDefinition> Concepts,
    IReadOnlyList<ConceptIrStructInstance> Structs,
    IReadOnlyList<ConceptIrValue> ResolvedValues,
    ConceptIrMaterializedStruct MaterializedStruct,
    IReadOnlyList<ConceptIrBinding> Bindings,
    string ErasureStatus = "ErasedBeforeFeatureAir");

internal sealed record ConceptPhase3Resolution(
    string ModelName,
    string SourceSpelling,
    string Units,
    string BoxName,
    IReadOnlyList<double> BoxSize,
    string BoxBoundsProvenance,
    FirmamentV2ModifyBlock ModifyBlock,
    ConceptIrDocument ConceptIr);

internal static class ConceptIrResolver
{
    public const string MissingMember = "firmament-concept-missing-member";
    public const string UnknownMember = "firmament-concept-unknown-member";
    public const string TypeMismatch = "firmament-concept-type-mismatch";
    public const string InvalidSpatialDerivation = "firmament-concept-invalid-spatial-derivation";
    public const string IndexOutOfRange = "firmament-concept-index-out-of-range";
    public const string CircularDependency = "firmament-concept-circular-dependency";
    public const string MaterializedPhaseReference = "firmament-concept-materialized-value-in-compile-time-phase";
    public const string DuplicateDeclaration = "firmament-concept-duplicate-declaration";
    public const string DuplicateExposedMember = "firmament-concept-duplicate-exposed-member";
    public const string InvalidMaterializedReference = "firmament-concept-invalid-materialized-reference";
    public const string ExposedMemberUnrepresentable = "firmament-concept-exposed-member-cannot-be-represented-semantically";
    public const string CircularExposureDependency = "firmament-concept-circular-exposure-dependency";
    public const string PointNotOnPlacementPlane = "firmament-concept-point-not-on-placement-plane";
    public const string PointOutsidePlacementFace = "firmament-concept-point-outside-placement-face";
    public const string PointProjectionUnsupported = "firmament-concept-point-projection-unsupported";

    private static readonly Regex ConceptHeader = new(@"\bConcept\s+(?!Struct\b)(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex ConceptStructHeader = new(@"\bConcept\s+Struct\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?::\s*(?<concept>[A-Za-z_][A-Za-z0-9_]*))?\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex MaterializedHeader = new(@"\b(?<kind>Struct|Model)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?::\s*(?<concept>[A-Za-z_][A-Za-z0-9_]*))?\s*(?<units>mm\s*)?\{", RegexOptions.CultureInvariant);

    public static ConceptPhase3Resolution? Resolve(string source, List<string> diagnostics)
    {
        var definitions = ParseDefinitions(source, diagnostics);
        var materializedMatches = MaterializedHeader.Matches(source).Cast<Match>()
            .Where(m => !IsPrecededByConcept(source, m.Index)).ToArray();
        if (materializedMatches.Length != 1) { diagnostics.Add(FirmamentV2Parser.Phase3EdgeFinishSyntaxInvalid); return null; }
        var materialized = materializedMatches[0];
        var materializedOpen = source.IndexOf('{', materialized.Index);
        var materializedClose = FindMatchingBrace(source, materializedOpen);
        if (materializedClose < 0) { diagnostics.Add(FirmamentV2Parser.Phase3EdgeFinishSyntaxInvalid); return null; }

        var instanceList = new List<ConceptIrStructInstance>();
        var resolved = new List<ConceptIrValue>();
        var instanceNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in ConceptStructHeader.Matches(source))
        {
            var name = match.Groups["name"].Value;
            if (!instanceNames.Add(name)) { diagnostics.Add(DuplicateDeclaration); continue; }
            var open = source.IndexOf('{', match.Index);
            var close = FindMatchingBrace(source, open);
            if (close < 0) { diagnostics.Add(InvalidSpatialDerivation); continue; }
            var body = source[(open + 1)..close];
            var members = ResolveMembers(name, body, open + 1, materialized.Groups["name"].Value, diagnostics);
            var satisfies = match.Groups["concept"].Success ? new[] { match.Groups["concept"].Value } : [];
            ValidateConformance(name, satisfies, members, definitions, diagnostics);
            var instance = new ConceptIrStructInstance(name, satisfies, members, false, "CompileTimeOnlyErased", new(match.Index, close - match.Index + 1));
            instanceList.Add(instance);
            foreach (var value in members.Values)
            {
                resolved.Add(value);
                if (value is ConceptIrPointSetValue set) resolved.AddRange(set.Points);
            }
        }

        var bodyText = source[(materializedOpen + 1)..materializedClose];
        ValidateIndexedReferences(bodyText, instanceList, diagnostics);
        var box = Regex.Match(bodyText, @"\bBox\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
        var modify = Regex.Match(bodyText, @"\bModify\s+(?<target>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
        var edge = Regex.Match(bodyText, @"\bEdgeFinish\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
        if (!box.Success || !modify.Success) { diagnostics.Add(FirmamentV2Parser.Phase3EdgeFinishSyntaxInvalid); return null; }
        var boxOpen = materializedOpen + 1 + bodyText.IndexOf('{', box.Index);
        var boxClose = FindMatchingBrace(source, boxOpen);
        var modifyStart = materializedOpen + 1 + modify.Index;
        var modifyOpen = source.IndexOf('{', modifyStart);
        var modifyClose = FindMatchingBrace(source, modifyOpen);
        if (boxClose < 0 || modifyClose < 0) { diagnostics.Add(FirmamentV2Parser.Phase3EdgeFinishSyntaxInvalid); return null; }

        var allInstances = instanceList.ToDictionary(i => i.Name, StringComparer.Ordinal);
        var boxBody = source[(boxOpen + 1)..boxClose];
        IReadOnlyList<double>? size = null;
        ConceptIrBox3Value? resolvedBounds = null;
        string boundsProvenance;
        var boundsRef = Regex.Match(boxBody, @"\bBounds\s*:\s*(?<instance>[A-Za-z_][A-Za-z0-9_]*)\.(?<member>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant);
        if (boundsRef.Success && TryMember(allInstances, boundsRef, out ConceptIrBox3Value? bounds, diagnostics))
        {
            size = bounds!.Size;
            resolvedBounds = bounds;
            boundsProvenance = bounds.Provenance;
        }
        else
        {
            var sizeMatch = Regex.Match(boxBody, @"\bSize\s*:\s*\[(?<values>[^\]]+)\]", RegexOptions.CultureInvariant);
            var values = sizeMatch.Success ? sizeMatch.Groups["values"].Value.Split(',').Select(ParseLength).ToArray() : [];
            size = values.Length == 3 && values.All(v => double.IsFinite(v) && v > 0) ? values : null;
            boundsProvenance = $"{materialized.Groups["name"].Value}.{box.Groups["name"].Value}.Size";
        }

        if (size is null || box.Groups["name"].Value != modify.Groups["target"].Value)
        {
            diagnostics.Add(FirmamentV2Parser.Phase3EdgeFinishSyntaxInvalid);
            return null;
        }

        var bindings = new List<ConceptIrBinding>
        {
            new($"{materialized.Groups["name"].Value}.{box.Groups["name"].Value}.Bounds", boundsRef.Success ? boundsRef.Value.Split(':', 2)[1].Trim() : "Size", boundsProvenance, "Box3")
        };
        var finishes = new List<FirmamentV2EdgeFinishDecl>();
        if (edge.Success)
        {
            var edgeStart = materializedOpen + 1 + edge.Index;
            var edgeOpen = source.IndexOf('{', edgeStart);
            var edgeClose = FindMatchingBrace(source, edgeOpen);
            if (edgeClose < 0) { diagnostics.Add(FirmamentV2Parser.Phase3EdgeFinishSyntaxInvalid); return null; }
            var edgeBody = source[(edgeOpen + 1)..edgeClose];
            var faceSource = FieldValue(edgeBody, "Face");
            var faceAxis = faceSource;
            var faceProvenance = $"{materialized.Groups["name"].Value}.{edge.Groups["name"].Value}.Face";
            var faceRef = Regex.Match(faceSource, @"^(?<instance>[A-Za-z_][A-Za-z0-9_]*)\.(?<member>[A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.CultureInvariant);
            if (faceRef.Success && TryMember(allInstances, faceRef, out ConceptIrPlaneValue? plane, diagnostics))
            {
                faceAxis = AxisOf(plane!.Normal);
                faceProvenance = plane.Provenance;
            }
            var target = FieldValue(edgeBody, "Target");
            var kind = FieldValue(edgeBody, "Kind");
            var distance = ParseLength(FieldValue(edgeBody, "Distance"));
            if (!Regex.IsMatch(faceAxis, @"^[+-][XYZ]$", RegexOptions.CultureInvariant) || target.Length == 0 || kind.Length == 0 || !double.IsFinite(distance))
            { diagnostics.Add(FirmamentV2Parser.Phase3EdgeFinishSyntaxInvalid); return null; }
            finishes.Add(new(edge.Groups["name"].Value, faceAxis, target, kind, distance, new(edgeStart, edgeClose - edgeStart + 1),
                new Dictionary<string, string>(StringComparer.Ordinal) { ["Face"] = faceProvenance }));
            bindings.Add(new($"{materialized.Groups["name"].Value}.{edge.Groups["name"].Value}.Face", faceSource, faceProvenance, "Plane"));
        }

        var modifyBody = source[(modifyOpen + 1)..modifyClose];
        var holes = ParseConceptHoles(modifyBody, modifyOpen + 1, box.Groups["name"].Value, resolvedBounds,
            allInstances, materialized.Groups["name"].Value, bindings, diagnostics);
        if (holes.Count == 0 && finishes.Count == 0) { diagnostics.Add(FirmamentV2Parser.Phase3EdgeFinishSyntaxInvalid); return null; }

        var satisfiesMaterialized = materialized.Groups["concept"].Success ? new[] { materialized.Groups["concept"].Value } : [];
        var exposed = ParseExposedMembers(bodyText, materializedOpen + 1, materialized.Groups["name"].Value, box.Groups["name"].Value, resolvedBounds,
            allInstances, holes, diagnostics);
        ValidateMaterializedConformance(materialized.Groups["name"].Value, satisfiesMaterialized, exposed, definitions, diagnostics);
        var conformance = satisfiesMaterialized.Length == 0 ? "NotDeclared" : diagnostics.Any(d => IsConformanceDiagnostic(d)) ? "Invalid" : "Valid";
        var ir = new ConceptIrDocument(definitions, instanceList, resolved,
            new(materialized.Groups["name"].Value, materialized.Groups["kind"].Value, satisfiesMaterialized, new(materialized.Index, materializedClose - materialized.Index + 1), exposed, conformance), bindings);
        var modifyBlock = new FirmamentV2ModifyBlock(modify.Groups["target"].Value, [], holes, finishes);
        return new(materialized.Groups["name"].Value, materialized.Groups["kind"].Value, "mm", box.Groups["name"].Value, size, boundsProvenance,
            modifyBlock, ir);
    }

    private static IReadOnlyList<FirmamentV2SemanticHoleDecl> ParseConceptHoles(
        string body,
        int bodyOffset,
        string boxName,
        ConceptIrBox3Value? bounds,
        IReadOnlyDictionary<string, ConceptIrStructInstance> instances,
        string materializedName,
        List<ConceptIrBinding> bindings,
        List<string> diagnostics)
    {
        const double tolerance = 1e-9;
        var result = new List<FirmamentV2SemanticHoleDecl>();
        var headers = Regex.Matches(body, @"\bhole\s*<\s*(?<variant>[A-Za-z_][A-Za-z0-9_]*)\s*>\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        foreach (Match header in headers)
        {
            if (!string.Equals(header.Groups["variant"].Value, "shaft", StringComparison.OrdinalIgnoreCase)) { diagnostics.Add(FirmamentV2Parser.HoleVariantUnknown); continue; }
            var open = body.IndexOf('{', header.Index); var close = FindMatchingBrace(body, open);
            if (close < 0) { diagnostics.Add(FirmamentV2Parser.RegionUnsupported); continue; }
            var holeBody = body[(open + 1)..close];
            var on = FieldValue(holeBody, "on");
            var faceAxis = on switch
            {
                "face(+Z)" => "+Z",
                _ when string.Equals(on, boxName + ".Top", StringComparison.Ordinal) => "+Z",
                _ => ResolvePlaneAxis(on, instances, diagnostics)
            };
            if (faceAxis != "+Z") { diagnostics.Add(PointProjectionUnsupported + ":only-planar-+Z-placement-is-admitted"); continue; }
            if (bounds is null) { diagnostics.Add(InvalidSpatialDerivation); continue; }

            var centerMatch = Regex.Match(holeBody, @"\bcenter\s*:\s*(?<instance>[A-Za-z_][A-Za-z0-9_]*)\.(?<member>[A-Za-z_][A-Za-z0-9_]*)\[(?<index>[0-9]+)\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!centerMatch.Success || !instances.TryGetValue(centerMatch.Groups["instance"].Value, out var instance)
                || !instance.Members.TryGetValue(centerMatch.Groups["member"].Value, out var member) || member is not ConceptIrPointSetValue set)
            { diagnostics.Add(InvalidSpatialDerivation); continue; }
            var index = int.Parse(centerMatch.Groups["index"].Value, CultureInfo.InvariantCulture);
            if (index >= set.Points.Count) { diagnostics.Add($"{IndexOutOfRange}:{centerMatch.Value}:count-{set.Points.Count}"); continue; }
            var point = set.Points[index];
            var planeZ = bounds.Max.Z;
            var distance = point.Point.Z - planeZ;
            if (Math.Abs(distance) > tolerance) { diagnostics.Add($"{PointNotOnPlacementPlane}:{point.Provenance}:distance-{distance.ToString("R", CultureInfo.InvariantCulture)}"); continue; }
            if (point.Point.X < bounds.Min.X - tolerance || point.Point.X > bounds.Max.X + tolerance || point.Point.Y < bounds.Min.Y - tolerance || point.Point.Y > bounds.Max.Y + tolerance)
            { diagnostics.Add($"{PointOutsidePlacementFace}:{point.Provenance}"); continue; }
            var diameter = ParseLengthOrUnitless(FieldValue(holeBody, "diameter"));
            if (!double.IsFinite(diameter) || diameter <= 0) { diagnostics.Add(FirmamentV2Parser.HoleDiameterInvalid); continue; }
            var endSource = FieldValue(holeBody, "end");
            if (!string.Equals(endSource, "throughAll", StringComparison.OrdinalIgnoreCase) && !string.Equals(endSource, "Through", StringComparison.OrdinalIgnoreCase))
            { diagnostics.Add(FirmamentV2Parser.HoleEndMissing); continue; }

            var absoluteCenterStart = bodyOffset + open + 1 + centerMatch.Index;
            var resolved = new FirmamentV2ResolvedPoint3(point.Point.X, point.Point.Y, point.Point.Z, point.StableId, set.Provenance, point.Ordinal,
                on, distance, new(absoluteCenterStart, centerMatch.Length));
            var sourceSpan = new FirmamentV2SourceSpan(bodyOffset + header.Index, close - header.Index + 1);
            var face = new FirmamentV2FaceTarget(on, on == "face(+Z)" ? "DirectSelector" : "SemanticReference", "+Z", "face(+Z)", "FaceRef");
            result.Add(new(header.Groups["name"].Value, FirmamentV2SemanticHoleVariant.Shaft, face,
                new(point.Point.X, point.Point.Y, FirmamentV2FaceLocalPoint2D.PlusZConvention), diameter,
                new(FirmamentV2SemanticHoleEndKind.ThroughAll), ResolvedCenter: resolved, SourceSpan: sourceSpan));
            bindings.Add(new($"{materializedName}.{header.Groups["name"].Value}.Center", point.Provenance, point.Provenance, "Point3"));
        }
        return result;
    }

    private static IReadOnlyList<ConceptIrSemanticMember> ParseExposedMembers(
        string body,
        int bodyOffset,
        string materializedName,
        string boxName,
        ConceptIrBox3Value? bounds,
        IReadOnlyDictionary<string, ConceptIrStructInstance> instances,
        IReadOnlyList<FirmamentV2SemanticHoleDecl> holes,
        List<string> diagnostics)
    {
        var expose = Regex.Match(body, @"\bExpose\s*\{(?<body>.*?)\}", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        if (!expose.Success) return [];
        var result = new List<ConceptIrSemanticMember>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var fields = Regex.Matches(expose.Groups["body"].Value, @"(?m)^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<value>[^\r\n}]+?)\s*$", RegexOptions.CultureInvariant).Cast<Match>().ToArray();
        var fieldNames = fields.Select(f => f.Groups["name"].Value).ToHashSet(StringComparer.Ordinal);
        var dependencies = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in fields) dependencies.TryAdd(field.Groups["name"].Value, field.Groups["value"].Value.Trim());
        var cycleMembers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var start in dependencies.Keys)
        {
            var path = new HashSet<string>(StringComparer.Ordinal); var current = start;
            while (fieldNames.Contains(current) && path.Add(current) && dependencies.TryGetValue(current, out current!)) { }
            if (path.Contains(current)) foreach (var member in path) cycleMembers.Add(member);
        }
        foreach (var field in fields)
        {
            var name = field.Groups["name"].Value; var reference = field.Groups["value"].Value.Trim();
            if (!names.Add(name)) { diagnostics.Add($"{DuplicateExposedMember}:{name}"); continue; }
            if (cycleMembers.Contains(name)) { diagnostics.Add($"{CircularExposureDependency}:{name}->{reference}"); continue; }
            var span = new FirmamentV2SourceSpan(bodyOffset + expose.Groups["body"].Index + field.Index, field.Length);
            ConceptIrValue? value = null; string? semanticReference = null; ConceptIrSemanticPhase phase; ConceptIrMaterializationCategory category;
            var dotted = Regex.Match(reference, @"^(?<instance>[A-Za-z_][A-Za-z0-9_]*)\.(?<member>[A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.CultureInvariant);
            if (dotted.Success && instances.TryGetValue(dotted.Groups["instance"].Value, out var instance) && instance.Members.TryGetValue(dotted.Groups["member"].Value, out value))
            { phase = ConceptIrSemanticPhase.ConceptIr; category = ConceptIrMaterializationCategory.CompileTimeValue; }
            else if (string.Equals(reference, boxName + ".Top", StringComparison.Ordinal) && bounds is not null)
            {
                value = new ConceptIrPlaneValue("materialized:" + reference, FaceCenter(bounds, "+Z"), Vector("+Z"), reference);
                semanticReference = reference; phase = ConceptIrSemanticPhase.FeatureAir; category = ConceptIrMaterializationCategory.MaterializedSemanticReference;
            }
            else
            {
                var holeCenter = holes.FirstOrDefault(h => string.Equals(reference, h.Name + ".Center", StringComparison.Ordinal));
                if (holeCenter?.ResolvedCenter is { } center)
                {
                    value = new ConceptIrPoint3Value(center.StableId, new(center.X, center.Y, center.Z), center.SourceMember, center.Ordinal);
                    semanticReference = reference; phase = ConceptIrSemanticPhase.FeatureAir; category = ConceptIrMaterializationCategory.MaterializedSemanticReference;
                }
                else if ((reference.StartsWith(boxName + ".", StringComparison.Ordinal) || holes.Any(h => reference.StartsWith(h.Name + ".", StringComparison.Ordinal)))
                    && (reference.Contains("BRep", StringComparison.Ordinal) || reference.Contains("Topology", StringComparison.Ordinal) || reference.EndsWith("FaceId", StringComparison.Ordinal)))
                { diagnostics.Add($"{ExposedMemberUnrepresentable}:{name}:{reference}"); continue; }
                else { diagnostics.Add($"{InvalidMaterializedReference}:{name}:{reference}"); continue; }
            }
            result.Add(new(name, TypeOf(value), value, semanticReference, phase, category, value.Provenance, span, $"materialized:{materializedName}.exposed:{name}"));
        }
        return result;
    }

    private static void ValidateMaterializedConformance(string instance, IReadOnlyList<string> satisfies, IReadOnlyList<ConceptIrSemanticMember> members, IReadOnlyList<ConceptIrDefinition> definitions, List<string> diagnostics)
    {
        var byName = members.ToDictionary(m => m.Name, StringComparer.Ordinal);
        foreach (var conceptName in satisfies)
        {
            var definition = definitions.SingleOrDefault(d => d.Name == conceptName);
            if (definition is null) { diagnostics.Add(FirmamentV2Parser.ConceptUnknownConcept); continue; }
            foreach (var required in definition.Members.Values)
            {
                if (!byName.TryGetValue(required.Name, out var member)) { diagnostics.Add($"{MissingMember}:{instance}.{required.Name}"); continue; }
                if (member.Type != required.Type) diagnostics.Add($"{TypeMismatch}:{instance}.{required.Name}:expected-{required.Type}:actual-{member.Type}");
            }
            foreach (var member in members.Where(m => !definition.Members.ContainsKey(m.Name))) diagnostics.Add($"{UnknownMember}:{instance}.{member.Name}");
        }
    }

    private static bool IsConformanceDiagnostic(string diagnostic) => diagnostic.StartsWith(MissingMember, StringComparison.Ordinal)
        || diagnostic.StartsWith(UnknownMember, StringComparison.Ordinal) || diagnostic.StartsWith(TypeMismatch, StringComparison.Ordinal)
        || diagnostic.StartsWith(DuplicateExposedMember, StringComparison.Ordinal) || diagnostic.StartsWith(InvalidMaterializedReference, StringComparison.Ordinal)
        || diagnostic.StartsWith(CircularExposureDependency, StringComparison.Ordinal) || diagnostic.StartsWith(ExposedMemberUnrepresentable, StringComparison.Ordinal);

    private static ConceptIrType TypeOf(ConceptIrValue value) => value is ConceptIrPointSetValue ? new("Point3", true) : new(value.Kind.ToString());

    private static string ResolvePlaneAxis(string source, IReadOnlyDictionary<string, ConceptIrStructInstance> instances, List<string> diagnostics)
    {
        var match = Regex.Match(source, @"^(?<instance>[A-Za-z_][A-Za-z0-9_]*)\.(?<member>[A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.CultureInvariant);
        if (match.Success && TryMember(instances, match, out ConceptIrPlaneValue? plane, diagnostics)) return AxisOf(plane!.Normal);
        return string.Empty;
    }

    private static IReadOnlyList<ConceptIrDefinition> ParseDefinitions(string source, List<string> diagnostics)
    {
        var result = new List<ConceptIrDefinition>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in ConceptHeader.Matches(source))
        {
            var open = source.IndexOf('{', match.Index); var close = FindMatchingBrace(source, open);
            if (close < 0) { diagnostics.Add(InvalidSpatialDerivation); continue; }
            var name = match.Groups["name"].Value;
            if (!names.Add(name)) { diagnostics.Add(DuplicateDeclaration); continue; }
            var members = new Dictionary<string, ConceptIrMemberRequirement>(StringComparer.Ordinal);
            var body = source[(open + 1)..close];
            foreach (Match field in Regex.Matches(body, @"(?m)^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<type>[A-Za-z_][A-Za-z0-9_]*)(?<array>\[\])?\s*$", RegexOptions.CultureInvariant))
            {
                var memberName = field.Groups["name"].Value;
                if (!members.TryAdd(memberName, new(memberName, new(field.Groups["type"].Value, field.Groups["array"].Success), new(open + 1 + field.Index, field.Length)))) diagnostics.Add(DuplicateDeclaration);
            }
            result.Add(new(name, members, new(match.Index, close - match.Index + 1)));
        }
        return result;
    }

    private static IReadOnlyDictionary<string, ConceptIrValue> ResolveMembers(string instanceName, string body, int bodyOffset, string materializedName, List<string> diagnostics)
    {
        var result = new Dictionary<string, ConceptIrValue>(StringComparer.Ordinal);
        DetectCircularDependencies(body, diagnostics);
        var boundsMatch = Regex.Match(body, @"\b(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*Box3\s*\{", RegexOptions.CultureInvariant);
        if (boundsMatch.Success)
        {
            var open = body.IndexOf('{', boundsMatch.Index); var close = FindMatchingBrace(body, open);
            var sizeMatch = close > open ? Regex.Match(body[(open + 1)..close], @"\bSize\s*:\s*\[(?<values>[^\]]+)\]", RegexOptions.CultureInvariant) : Match.Empty;
            var size = sizeMatch.Success ? sizeMatch.Groups["values"].Value.Split(',').Select(ParseLength).ToArray() : [];
            if (size.Length == 3 && size.All(v => double.IsFinite(v) && v > 0))
            {
                var name = boundsMatch.Groups["name"].Value; var provenance = $"{instanceName}.{name}";
                result[name] = new ConceptIrBox3Value(Id(provenance), new(-size[0] / 2d, -size[1] / 2d, 0), new(size[0] / 2d, size[1] / 2d, size[2]), provenance);
            }
            else diagnostics.Add(InvalidSpatialDerivation);
        }

        foreach (var field in TopLevelFields(body))
        {
            if (result.ContainsKey(field.Name) || field.Value.StartsWith("Box3", StringComparison.Ordinal) || field.Value.StartsWith("Grid", StringComparison.Ordinal)) continue;
            if (field.Value.StartsWith(materializedName + ".", StringComparison.Ordinal)) { diagnostics.Add(MaterializedPhaseReference); continue; }
            var face = Regex.Match(field.Value, @"^(?<box>[A-Za-z_][A-Za-z0-9_]*)\.Face\((?<axis>[+-][XYZ])\)$", RegexOptions.CultureInvariant);
            var axis = Regex.Match(field.Value, @"^(?<box>[A-Za-z_][A-Za-z0-9_]*)\.Center\.Axis\((?<axis>[+-][XYZ])\)$", RegexOptions.CultureInvariant);
            if (face.Success && result.TryGetValue(face.Groups["box"].Value, out var boxValue) && boxValue is ConceptIrBox3Value box)
            {
                var provenance = $"{instanceName}.{field.Name}"; var a = face.Groups["axis"].Value;
                result[field.Name] = new ConceptIrPlaneValue(Id(provenance), FaceCenter(box, a), Vector(a), provenance);
            }
            else if (axis.Success && result.TryGetValue(axis.Groups["box"].Value, out boxValue) && boxValue is ConceptIrBox3Value axisBox)
            {
                var provenance = $"{instanceName}.{field.Name}";
                result[field.Name] = new ConceptIrAxisValue(Id(provenance), axisBox.Center, Vector(axis.Groups["axis"].Value), provenance);
            }
            else diagnostics.Add(InvalidSpatialDerivation);
        }

        foreach (var grid in Regex.Matches(body, @"\b(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*Grid\s*\{", RegexOptions.CultureInvariant).Cast<Match>())
        {
            var open = body.IndexOf('{', grid.Index); var close = FindMatchingBrace(body, open);
            if (close < 0) { diagnostics.Add(InvalidSpatialDerivation); continue; }
            var gridBody = body[(open + 1)..close];
            var within = Regex.Match(gridBody, @"\bWithin\s*:\s*(?<box>[A-Za-z_][A-Za-z0-9_]*)\.Face\((?<axis>[+-][XYZ])\)\.Inset\((?<inset>[^)]+)\)", RegexOptions.CultureInvariant);
            var columns = IntField(gridBody, "Columns"); var rows = IntField(gridBody, "Rows");
            if (!within.Success || columns < 1 || rows < 1 || !result.TryGetValue(within.Groups["box"].Value, out var value) || value is not ConceptIrBox3Value box) { diagnostics.Add(InvalidSpatialDerivation); continue; }
            var inset = ParseLength(within.Groups["inset"].Value); var region = Region(box, within.Groups["axis"].Value, inset, $"{instanceName}.{grid.Groups["name"].Value}.Within");
            if (region is null) { diagnostics.Add(InvalidSpatialDerivation); continue; }
            var points = new List<ConceptIrPoint3Value>();
            for (var row = 0; row < rows; row++) for (var column = 0; column < columns; column++)
            {
                var u = columns == 1 ? (region.MinU + region.MaxU) / 2d : region.MinU + column * (region.MaxU - region.MinU) / (columns - 1);
                var v = rows == 1 ? (region.MinV + region.MaxV) / 2d : region.MinV + row * (region.MaxV - region.MinV) / (rows - 1);
                var point = new ConceptIrPoint3(region.Center.X + region.U.X * u + region.V.X * v, region.Center.Y + region.U.Y * u + region.V.Y * v, region.Center.Z + region.U.Z * u + region.V.Z * v);
                var ordinal = points.Count; var provenance = $"{instanceName}.{grid.Groups["name"].Value}[{ordinal}]";
                points.Add(new(Id(provenance), point, provenance, ordinal));
            }
            var memberName = grid.Groups["name"].Value; var setProvenance = $"{instanceName}.{memberName}";
            result[memberName] = new ConceptIrPointSetValue(Id(setProvenance), points, setProvenance);
        }
        return result;
    }

    private static void ValidateIndexedReferences(string body, IReadOnlyList<ConceptIrStructInstance> instances, List<string> diagnostics)
    {
        var byName = instances.ToDictionary(i => i.Name, StringComparer.Ordinal);
        foreach (Match reference in Regex.Matches(body, @"(?<instance>[A-Za-z_][A-Za-z0-9_]*)\.(?<member>[A-Za-z_][A-Za-z0-9_]*)\[(?<index>[0-9]+)\]", RegexOptions.CultureInvariant))
        {
            if (!byName.TryGetValue(reference.Groups["instance"].Value, out var instance)
                || !instance.Members.TryGetValue(reference.Groups["member"].Value, out var value)
                || value is not ConceptIrPointSetValue points)
            {
                diagnostics.Add(InvalidSpatialDerivation);
                continue;
            }
            var index = int.Parse(reference.Groups["index"].Value, CultureInfo.InvariantCulture);
            if (index >= points.Points.Count) diagnostics.Add($"{IndexOutOfRange}:{reference.Value}:count-{points.Points.Count}");
        }
    }

    private static void DetectCircularDependencies(string body, List<string> diagnostics)
    {
        var fields = TopLevelFields(body).ToArray();
        var names = fields.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
        var dependencies = fields.ToDictionary(
            f => f.Name,
            f => Regex.Matches(f.Value, @"\b[A-Za-z_][A-Za-z0-9_]*\b", RegexOptions.CultureInvariant).Cast<Match>().Select(m => m.Value).Where(names.Contains).Distinct(StringComparer.Ordinal).ToArray(),
            StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        bool Visit(string name)
        {
            if (visiting.Contains(name)) return true;
            if (!visited.Add(name)) return false;
            visiting.Add(name);
            foreach (var dependency in dependencies[name]) if (Visit(dependency)) return true;
            visiting.Remove(name);
            return false;
        }
        if (dependencies.Keys.Any(Visit)) diagnostics.Add(CircularDependency);
    }

    private static void ValidateConformance(string instance, IReadOnlyList<string> satisfies, IReadOnlyDictionary<string, ConceptIrValue> members, IReadOnlyList<ConceptIrDefinition> definitions, List<string> diagnostics)
    {
        foreach (var conceptName in satisfies)
        {
            var definition = definitions.SingleOrDefault(d => d.Name == conceptName);
            if (definition is null) { diagnostics.Add(FirmamentV2Parser.ConceptUnknownConcept); continue; }
            foreach (var required in definition.Members.Values)
            {
                if (!members.TryGetValue(required.Name, out var value)) { diagnostics.Add($"{MissingMember}:{instance}.{required.Name}"); continue; }
                if (!Compatible(required.Type, value)) diagnostics.Add($"{TypeMismatch}:{instance}.{required.Name}:expected-{required.Type}:actual-{value.Kind}");
            }
            foreach (var member in members.Keys.Where(k => !definition.Members.ContainsKey(k))) diagnostics.Add($"{UnknownMember}:{instance}.{member}");
        }
    }

    private static bool Compatible(ConceptIrType type, ConceptIrValue value) =>
        type.IsCollection ? type.Name == "Point3" && value is ConceptIrPointSetValue : string.Equals(type.Name, value.Kind.ToString(), StringComparison.Ordinal);

    private static bool TryMember<T>(IReadOnlyDictionary<string, ConceptIrStructInstance> instances, Match reference, out T? value, List<string> diagnostics) where T : ConceptIrValue
    {
        value = null;
        if (!instances.TryGetValue(reference.Groups["instance"].Value, out var instance) || !instance.Members.TryGetValue(reference.Groups["member"].Value, out var raw)) { diagnostics.Add(InvalidSpatialDerivation); return false; }
        if (raw is not T typed) { diagnostics.Add(TypeMismatch); return false; }
        value = typed; return true;
    }

    private static IEnumerable<(string Name, string Value)> TopLevelFields(string body)
    {
        var depth = 0;
        foreach (var raw in body.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (depth == 0)
            {
                var match = Regex.Match(line, @"^(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<value>.+?)\s*$", RegexOptions.CultureInvariant);
                if (match.Success) yield return (match.Groups["name"].Value, match.Groups["value"].Value);
            }
            depth += line.Count(c => c == '{') - line.Count(c => c == '}');
        }
    }

    private static ConceptIrRegion2Value? Region(ConceptIrBox3Value box, string axis, double inset, string provenance)
    {
        if (!double.IsFinite(inset) || inset < 0) return null;
        var center = FaceCenter(box, axis);
        (ConceptIrVector3 U, ConceptIrVector3 V, double HalfU, double HalfV) frame = axis switch
        {
            "+Z" or "-Z" => (new(1, 0, 0), new(0, 1, 0), box.Size[0] / 2d, box.Size[1] / 2d),
            "+X" or "-X" => (new(0, 1, 0), new(0, 0, 1), box.Size[1] / 2d, box.Size[2] / 2d),
            "+Y" or "-Y" => (new(1, 0, 0), new(0, 0, 1), box.Size[0] / 2d, box.Size[2] / 2d),
            _ => default
        };
        if (frame.U is null || inset > frame.HalfU || inset > frame.HalfV) return null;
        return new(Id(provenance), center, frame.U, frame.V, -frame.HalfU + inset, frame.HalfU - inset, -frame.HalfV + inset, frame.HalfV - inset, axis, provenance);
    }

    private static ConceptIrPoint3 FaceCenter(ConceptIrBox3Value box, string axis) => axis switch
    {
        "+X" => box.Center with { X = box.Max.X }, "-X" => box.Center with { X = box.Min.X },
        "+Y" => box.Center with { Y = box.Max.Y }, "-Y" => box.Center with { Y = box.Min.Y },
        "+Z" => box.Center with { Z = box.Max.Z }, "-Z" => box.Center with { Z = box.Min.Z },
        _ => box.Center
    };
    private static ConceptIrVector3 Vector(string axis) => axis switch { "+X" => new(1, 0, 0), "-X" => new(-1, 0, 0), "+Y" => new(0, 1, 0), "-Y" => new(0, -1, 0), "+Z" => new(0, 0, 1), "-Z" => new(0, 0, -1), _ => new(0, 0, 0) };
    private static string AxisOf(ConceptIrVector3 v) => (v.X, v.Y, v.Z) switch { (1, 0, 0) => "+X", (-1, 0, 0) => "-X", (0, 1, 0) => "+Y", (0, -1, 0) => "-Y", (0, 0, 1) => "+Z", (0, 0, -1) => "-Z", _ => string.Empty };
    private static string FieldValue(string body, string name) { var m = Regex.Match(body, $@"\b{Regex.Escape(name)}\s*:\s*(?<value>[^\r\n}}]+)", RegexOptions.CultureInvariant); return m.Success ? m.Groups["value"].Value.Trim() : string.Empty; }
    private static int IntField(string body, string name) => int.TryParse(FieldValue(body, name), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : -1;
    private static double ParseLength(string source) { source = source.Trim(); return source.EndsWith("mm", StringComparison.Ordinal) && double.TryParse(source[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN; }
    private static double ParseLengthOrUnitless(string source)
    {
        source = source.Trim();
        if (source.EndsWith("mm", StringComparison.Ordinal)) source = source[..^2];
        return double.TryParse(source, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN;
    }
    private static string Id(string provenance) => "concept:" + provenance;
    private static bool IsPrecededByConcept(string source, int index) { var start = Math.Max(0, index - 10); return Regex.IsMatch(source[start..index], @"Concept\s+$", RegexOptions.CultureInvariant); }
    private static int FindMatchingBrace(string source, int open) { if (open < 0 || open >= source.Length || source[open] != '{') return -1; var depth = 0; for (var i = open; i < source.Length; i++) { if (source[i] == '{') depth++; else if (source[i] == '}' && --depth == 0) return i; } return -1; }
}
