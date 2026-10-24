using System.Globalization;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public enum ConceptIrValueKind { Length, Angle, Bool, Int, Float, String, Enum, Point2, Point3, Vector3, Axis, Plane, Box2, Box3, Region2, PointSet }

public sealed record ConceptIrType(string Name, bool IsCollection = false)
{
    public override string ToString() => Name + (IsCollection ? "[]" : string.Empty);
}

public sealed record ConceptIrMemberRequirement(string Name, ConceptIrType Type, FirmamentV2SourceSpan SourceSpan);
public sealed record ConceptIrDefinition(string Name, IReadOnlyDictionary<string, ConceptIrMemberRequirement> Members, FirmamentV2SourceSpan SourceSpan);
public sealed record ConceptIrEnumDefinition(string Name, IReadOnlyList<string> Variants, FirmamentV2SourceSpan SourceSpan);
public sealed record ConceptIrPoint3(double X, double Y, double Z);
public sealed record ConceptIrVector3(double X, double Y, double Z);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$valueType")]
[JsonDerivedType(typeof(ConceptIrBox3Value), "Box3")]
[JsonDerivedType(typeof(ConceptIrPlaneValue), "Plane")]
[JsonDerivedType(typeof(ConceptIrAxisValue), "Axis")]
[JsonDerivedType(typeof(ConceptIrRegion2Value), "Region2")]
[JsonDerivedType(typeof(ConceptIrPoint3Value), "Point3")]
[JsonDerivedType(typeof(ConceptIrPointSetValue), "PointSet")]
[JsonDerivedType(typeof(ConceptIrKeyedPointSetValue), "KeyedPointSet")]
[JsonDerivedType(typeof(ConceptIrLengthValue), "Length")]
[JsonDerivedType(typeof(ConceptIrAngleValue), "Angle")]
[JsonDerivedType(typeof(ConceptIrBoolValue), "Bool")]
[JsonDerivedType(typeof(ConceptIrIntValue), "Int")]
[JsonDerivedType(typeof(ConceptIrFloatValue), "Float")]
[JsonDerivedType(typeof(ConceptIrStringValue), "String")]
[JsonDerivedType(typeof(ConceptIrEnumValue), "Enum")]
public abstract record ConceptIrValue(string StableId, ConceptIrValueKind Kind, string Provenance);
public sealed record ConceptIrLengthValue(string StableId, double Value, string Unit, string Provenance)
    : ConceptIrValue(StableId, ConceptIrValueKind.Length, Provenance);
public sealed record ConceptIrAngleValue(string StableId, double Value, string Unit, string Provenance)
    : ConceptIrValue(StableId, ConceptIrValueKind.Angle, Provenance);
public sealed record ConceptIrBoolValue(string StableId, bool Value, string Provenance)
    : ConceptIrValue(StableId, ConceptIrValueKind.Bool, Provenance);
public sealed record ConceptIrIntValue(string StableId, long Value, string Provenance)
    : ConceptIrValue(StableId, ConceptIrValueKind.Int, Provenance);
public sealed record ConceptIrFloatValue(string StableId, double Value, string Provenance)
    : ConceptIrValue(StableId, ConceptIrValueKind.Float, Provenance);
public sealed record ConceptIrStringValue(string StableId, string Value, string Provenance)
    : ConceptIrValue(StableId, ConceptIrValueKind.String, Provenance);
public sealed record ConceptIrEnumValue(string StableId, string EnumType, string Variant, int Ordinal, string Provenance)
    : ConceptIrValue(StableId, ConceptIrValueKind.Enum, Provenance);
public sealed record ConceptIrBox3Value(string StableId, ConceptIrPoint3 Min, ConceptIrPoint3 Max, string Provenance)
    : ConceptIrValue(StableId, ConceptIrValueKind.Box3, Provenance)
{
    public IReadOnlyList<double> Size => [Max.X - Min.X, Max.Y - Min.Y, Max.Z - Min.Z];
    public ConceptIrPoint3 Center => new((Min.X + Max.X) / 2d, (Min.Y + Max.Y) / 2d, (Min.Z + Max.Z) / 2d);
}
public sealed record ConceptIrPlaneValue(string StableId, ConceptIrPoint3 Origin, ConceptIrVector3 Normal, string Provenance, ConceptIrVector3? OrientationHint = null)
    : ConceptIrValue(StableId, ConceptIrValueKind.Plane, Provenance);
public sealed record ConceptIrAxisValue(string StableId, ConceptIrPoint3 Origin, ConceptIrVector3 Direction, string Provenance)
    : ConceptIrValue(StableId, ConceptIrValueKind.Axis, Provenance);
public sealed record ConceptIrRegion2Value(string StableId, ConceptIrPoint3 Center, ConceptIrVector3 U, ConceptIrVector3 V, double MinU, double MaxU, double MinV, double MaxV, string FaceAxis, string Provenance)
    : ConceptIrValue(StableId, ConceptIrValueKind.Region2, Provenance);
public sealed record ConceptIrPoint3Value(string StableId, ConceptIrPoint3 Point, string Provenance, int? Ordinal = null)
    : ConceptIrValue(StableId, ConceptIrValueKind.Point3, Provenance);
public sealed record ConceptIrPointSetValue(string StableId, IReadOnlyList<ConceptIrPoint3Value> Points, string Provenance)
    : ConceptIrValue(StableId, ConceptIrValueKind.PointSet, Provenance);

/// <summary>Finite local points; ElementType preserves dimensionality without an implicit 2D-to-3D lift.</summary>
public sealed record ConceptIrKeyedPointValue(string StableId, string Key, int Ordinal, string ElementType,
    IReadOnlyList<double> Coordinates, string Provenance)
{ public FirmamentV2SourceSpan? SourceSpan { get; init; } }
public sealed record ConceptIrKeyedPointSetValue(string StableId, string Name, string ElementType,
    IReadOnlyList<ConceptIrKeyedPointValue> Points, string Recipe, string Provenance)
    : ConceptIrValue(StableId, ConceptIrValueKind.PointSet, Provenance)
{
    public FirmamentV2SourceSpan? SourceSpan { get; init; }
    public string? RecipeKind { get; init; }
}

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
public sealed record ConceptIrStaticSelection(
    string Member,
    string Scrutinee,
    string ScrutineeType,
    string ScrutineeValue,
    string SelectedArm,
    string ResultKind,
    string? Result,
    FirmamentV2SourceSpan SourceSpan,
    string Provenance);
public sealed record ConceptIrPatternExpansion(string Pattern, string Source, string ElementType, int Count, IReadOnlyList<string> GeneratedDeclarations, FirmamentV2SourceSpan SourceSpan, string Status = "ExpandedBeforeFeatureAir");
public sealed record ConceptIrTemplateRecordArgument(
    string RecordType,
    string StaticValue,
    IReadOnlyDictionary<string, string> Members,
    FirmamentV2SourceSpan SourceSpan,
    string Provenance);
/// <summary>Source-map-only evidence for a compile-time template expansion.  This is deliberately not AIR.</summary>
public sealed record ConceptIrTemplateInstantiation(
    string Template,
    string Instance,
    IReadOnlyDictionary<string, string> TypeArguments,
    IReadOnlyDictionary<string, string> ValueArguments,
    IReadOnlyList<string> DefaultedArguments,
    string SpecializationIdentity,
    IReadOnlyList<string> GeneratedDeclarations,
    FirmamentV2SourceSpan TemplateSourceSpan,
    FirmamentV2SourceSpan ApplicationSourceSpan,
    string Status = "ExpandedBeforeFeatureAir",
    IReadOnlyDictionary<string, string>? SelectedMatchArms = null,
    IReadOnlyDictionary<string, ConceptIrTemplateRecordArgument>? RecordArguments = null,
    IReadOnlyDictionary<string, string>? RequireResults = null)
{
    public string? DefinitionScope { get; init; }
    public string? BindingScope { get; init; }
}
public sealed record ConceptIrDocument(
    IReadOnlyList<ConceptIrDefinition> Concepts,
    IReadOnlyList<ConceptIrStructInstance> Structs,
    IReadOnlyList<ConceptIrValue> ResolvedValues,
    ConceptIrMaterializedStruct MaterializedStruct,
    IReadOnlyList<ConceptIrBinding> Bindings,
    string ErasureStatus = "ErasedBeforeFeatureAir",
    IReadOnlyList<ConceptIrEnumDefinition>? Enums = null,
    IReadOnlyList<ConceptIrStaticSelection>? StaticSelections = null,
    IReadOnlyList<ConceptIrTemplateInstantiation>? TemplateInstantiations = null,
    IReadOnlyList<ConceptIrPatternExpansion>? PatternExpansions = null);

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
    internal sealed record SpatialField(string Type, bool Required = true);
    // Published by the spatial owner for compile-time admission and spelling.
    internal static IReadOnlyDictionary<string, IReadOnlyDictionary<string, SpatialField>> SpatialFields { get; } =
        new Dictionary<string, IReadOnlyDictionary<string, SpatialField>>(StringComparer.Ordinal)
        {
            ["Box3"] = new Dictionary<string, SpatialField> { ["Size"] = new("Length[3]") },
            ["Grid"] = new Dictionary<string, SpatialField> { ["Within"] = new("Region2"), ["Columns"] = new("Int"), ["Rows"] = new("Int") },
            ["Plane"] = new Dictionary<string, SpatialField> { ["Origin"] = new("Point3|Length[3]"), ["Normal"] = new("Int[3]|Float[3]|Vector3"), ["Up"] = new("Int[3]|Float[3]|Vector3", false) }
        };
    // Static expansion is deliberately bounded: Pattern is compile-time declaration generation, not iteration.
    private const int MaxStaticPatternExpansion = 1024;
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
    public const string UnknownEnumType = "firmament-static-enum-unknown-type";
    public const string UnknownEnumVariant = "firmament-static-enum-unknown-variant";
    public const string DuplicateEnumVariant = "firmament-static-enum-duplicate-variant";
    public const string DuplicateMatchArm = "firmament-static-match-duplicate-arm";
    public const string NonExhaustiveMatch = "firmament-static-match-non-exhaustive";
    public const string InvalidBooleanArm = "firmament-static-match-invalid-boolean-arm";
    public const string MatchArmTypeMismatch = "firmament-static-match-arm-type-mismatch";
    public const string InvalidMatchScrutinee = "firmament-static-match-invalid-scrutinee-type";
    public const string SelectedBranchEvaluationFailure = "firmament-static-match-selected-branch-evaluation-failure";
    public const string InvalidEnumName = "firmament-static-enum-invalid-pascal-case";

    private static readonly Regex ConceptStructHeader = new(@"\bConcept\s+Struct\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?::\s*(?<concept>[A-Za-z_][A-Za-z0-9_]*))?\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex MaterializedHeader = new(@"\b(?<kind>Struct|Model)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?::\s*(?<concept>[A-Za-z_][A-Za-z0-9_]*))?\s*(?<units>mm\s*)?\{", RegexOptions.CultureInvariant);

    public static ConceptPhase3Resolution? Resolve(string source, List<string> diagnostics) => Resolve(source, diagnostics, null);

    /// <summary>Presentation retention uses the same value evaluator without imposing the
    /// Phase3 Box/Modify materialization shape on other admitted construction families.</summary>
    internal static IReadOnlyList<ConceptIrStructInstance> RetainSpatialStructs(string source)
    {
        var declarations = FirmamentSyntaxTraversal.Declarations(FirmamentSyntaxParser.Parse(source).Document)
            .Where(d => d.Type.Name.Text == "Struct" && d.Modifiers.Any(m => m.Name.Text == "Concept") && d.Body is not null && d.Name is not null).ToArray();
        if (declarations.Length == 0) return [];
        var scalars = new FirmamentCompileTimeValues(source);
        var initialization = new List<string>();
        scalars.Initialize(new Dictionary<string, TemplateRecordTypeIr>(), initialization);
        var instances = new List<ConceptIrStructInstance>();
        foreach (var declaration in declarations)
        {
            var span = declaration.Body!.ContentSpan;
            var diagnostics = new List<string>();
            var members = ResolveMembers(declaration.Name!.Text, source.Substring(span.Start, span.Length), span.Start,
                "", [], [], diagnostics, scalars);
            // Nonspatial requirements remain compile-time values, not invented geometry.
            // Retain only values the existing resolver actually produced.
            instances.Add(new(declaration.Name.Text, [], members, false, "CompileTimeOnlyErased",
                new(declaration.Span.Start, declaration.Span.Length)));
        }
        return instances;
    }

    internal static ConceptPhase3Resolution? Resolve(string source, List<string> diagnostics, FirmamentCompileTimeValues? scalars)
    {
        // Match roles are compile-time conformance metadata. They deliberately do not lower to AIR,
        // but they must remain available to the static evaluator so invalid domains and selected-arm
        // failures are diagnosed before feature lowering.
        if (scalars is null)
        {
            scalars = new(source);
            scalars.Initialize(new Dictionary<string, TemplateRecordTypeIr>(), diagnostics);
        }
        var choiceErrorStart = scalars.Diagnostics.Count;
        var enums = scalars.Choices.Types.Values.Select(t => new ConceptIrEnumDefinition(t.Name, t.Cases, t.Span)).ToArray();
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
        var staticSelections = new List<ConceptIrStaticSelection>();
        var instanceNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in ConceptStructHeader.Matches(source))
        {
            var name = match.Groups["name"].Value;
            if (!instanceNames.Add(name)) { diagnostics.Add(DuplicateDeclaration); continue; }
            var open = source.IndexOf('{', match.Index);
            var close = FindMatchingBrace(source, open);
            if (close < 0) { diagnostics.Add(InvalidSpatialDerivation); continue; }
            var body = source[(open + 1)..close];
            var members = ResolveMembers(name, body, open + 1, materialized.Groups["name"].Value, enums, staticSelections, diagnostics, scalars);
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

        if (scalars.Diagnostics.Skip(choiceErrorStart).Any(d => d.Severity is "error" or "fatal")) return null;
        var bodyText = source[(materializedOpen + 1)..materializedClose];
        ValidateIndexedReferences(bodyText, instanceList, diagnostics);
        var box = Regex.Match(bodyText, @"\bBox\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
        var modify = Regex.Match(bodyText, @"\bModify\s+(?<target>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
        var edge = Regex.Match(bodyText, @"\bEdgeFinish\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
        if (!box.Success || !modify.Success)
        {
            var constructionPlaneHole = Regex.IsMatch(bodyText, @"\bHole\s*<\s*Shaft\s*>[\s\S]*?\bFrom\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            diagnostics.Add(constructionPlaneHole ? FirmamentV2Parser.HoleConstructionPlaneHostUnsupported : FirmamentV2Parser.Phase3EdgeFinishSyntaxInvalid);
            return null;
        }
        var boxOpen = materializedOpen + 1 + bodyText.IndexOf('{', box.Index);
        var boxClose = FindMatchingBrace(source, boxOpen);
        var modifyStart = materializedOpen + 1 + modify.Index;
        var modifyOpen = source.IndexOf('{', modifyStart);
        var modifyClose = FindMatchingBrace(source, modifyOpen);
        if (boxClose < 0 || modifyClose < 0) { diagnostics.Add(FirmamentV2Parser.Phase3EdgeFinishSyntaxInvalid); return null; }

        var allInstances = instanceList.ToDictionary(i => i.Name, StringComparer.Ordinal);
        var boxBody = source[(boxOpen + 1)..boxClose];
        var syntaxDeclarations = FirmamentSyntaxTraversal.Declarations(FirmamentSyntaxParser.Parse(source).Document).ToArray();
        var materializedSyntax = syntaxDeclarations.FirstOrDefault(d => d.Body?.Span.Start == materializedOpen);
        if (materializedSyntax?.Body is not null) FirmamentV2Parser.BindMechanicalScope(materializedSyntax.Body.Statements, diagnostics);
        var boxSyntax = syntaxDeclarations.FirstOrDefault(d => d.Type.Name.Text == "Box" && d.Body?.Span.Start == boxOpen);
        if (boxSyntax is null) { diagnostics.Add(FirmamentV2Parser.CanonicalPrimitiveMalformed); return null; }
        var boxFields = new FirmamentSyntaxFields(boxSyntax, diagnostics);
        var boundsExpression = boxFields.Take("Bounds"); var sizeExpression = boxFields.Take("Size");
        if (boundsExpression is not null && sizeExpression is not null) diagnostics.Add("firmament-v2-field-conflict:Box:Bounds:Size");
        boxFields.Complete();
        var modifySyntax = syntaxDeclarations.FirstOrDefault(d => d.Type.Name.Text == "Modify" && d.Body?.Span.Start == modifyOpen);
        foreach (var statement in modifySyntax?.Body?.Statements ?? [])
        {
            if (statement is not DeclarationSyntax feature || !(feature.Type.Name.Text.Equals("Hole", StringComparison.OrdinalIgnoreCase) || feature.Type.Name.Text is "Pattern" or "EdgeFinish"))
                diagnostics.Add($"{FirmamentV2Parser.CanonicalDeclarationUnknown}:Modify:{(statement as DeclarationSyntax)?.Type.Name.Text ?? "expression"}");
            // The hole reader below looks fields up by name, so any other field would be
            // dropped silently. This path admits shaft holes with exactly these fields.
            else if (feature.Type.Name.Text.Equals("Hole", StringComparison.OrdinalIgnoreCase))
                foreach (var holeField in feature.Body?.Statements.OfType<FieldSyntax>() ?? [])
                    if (!ConceptHoleFields.Any(name => FirmamentSyntaxFields.SameField(holeField.Name.Text, name)))
                        diagnostics.Add($"{FirmamentV2Parser.CanonicalFieldUnknown}:Hole:{holeField.Name.Text}");
        }
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
            size = FirmamentSyntaxValues.Vector(sizeExpression, "mm", out var values) && values.Length == 3 && values.All(v => v > 0) ? values : null;
            boundsProvenance = $"{materialized.Groups["name"].Value}.{box.Groups["name"].Value}.Size";
            if (size is not null) resolvedBounds = new("materialized:" + boundsProvenance,
                new(-size[0] / 2, -size[1] / 2, 0), new(size[0] / 2, size[1] / 2, size[2]), boundsProvenance);
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
            var edgeSyntax = syntaxDeclarations.First(d => d.Type.Name.Text == "EdgeFinish" && d.Body?.Span.Start == edgeOpen);
            var edgeFields = new FirmamentSyntaxFields(edgeSyntax, diagnostics);
            string EdgeField(string name) => edgeFields.Take(name) is { } value ? FirmamentSyntaxWriter.Expression(value) : "";
            var faceSource = EdgeField("Face");
            var faceAxis = faceSource;
            var faceProvenance = $"{materialized.Groups["name"].Value}.{edge.Groups["name"].Value}.Face";
            var faceRef = Regex.Match(faceSource, @"^(?<instance>[A-Za-z_][A-Za-z0-9_]*)\.(?<member>[A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.CultureInvariant);
            if (faceRef.Success && TryMember(allInstances, faceRef, out ConceptIrPlaneValue? plane, diagnostics))
            {
                faceAxis = AxisOf(plane!.Normal);
                faceProvenance = plane.Provenance;
            }
            var target = EdgeField("Target");
            var kind = EdgeField("Kind");
            var distanceSource = EdgeField("Distance");
            edgeFields.Complete();
            var distance = ParseLength(distanceSource);
            var distanceProvenance = $"{materialized.Groups["name"].Value}.{edge.Groups["name"].Value}.Distance";
            var distanceRef = Regex.Match(distanceSource, @"^(?<instance>[A-Za-z_][A-Za-z0-9_]*)\.(?<member>[A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.CultureInvariant);
            if (distanceRef.Success && TryMember(allInstances, distanceRef, out ConceptIrLengthValue? resolvedDistance, diagnostics))
            {
                distance = resolvedDistance!.Value;
                distanceProvenance = resolvedDistance.Provenance;
            }
            if (!Regex.IsMatch(faceAxis, @"^[+-][XYZ]$", RegexOptions.CultureInvariant) || target.Length == 0 || kind.Length == 0 || !double.IsFinite(distance))
            { diagnostics.Add(FirmamentV2Parser.Phase3EdgeFinishSyntaxInvalid); return null; }
            finishes.Add(new(edge.Groups["name"].Value, faceAxis, target, kind, distance, new(edgeStart, edgeClose - edgeStart + 1),
                new Dictionary<string, string>(StringComparer.Ordinal) { ["Face"] = faceProvenance, ["Distance"] = distanceProvenance }));
            bindings.Add(new($"{materialized.Groups["name"].Value}.{edge.Groups["name"].Value}.Face", faceSource, faceProvenance, "Plane"));
            bindings.Add(new($"{materialized.Groups["name"].Value}.{edge.Groups["name"].Value}.Distance", distanceSource, distanceProvenance, "Length"));
        }

        var modifyBody = source[(modifyOpen + 1)..modifyClose];
        var holes = ParseConceptHoles(modifyBody, modifyOpen + 1, box.Groups["name"].Value, resolvedBounds,
            allInstances, materialized.Groups["name"].Value, bindings, diagnostics, source);
        var patterns = ParseConceptPatterns(modifyBody, modifyOpen + 1, box.Groups["name"].Value, resolvedBounds, allInstances, materialized.Groups["name"].Value, bindings, diagnostics, out var patternHoles);
        holes = holes.Concat(patternHoles).ToArray();
        if (holes.Count == 0 && finishes.Count == 0)
        {
            if (!diagnostics.Any(FirmamentV2Parser.IsFatalDiagnosticCode)) diagnostics.Add(FirmamentV2Parser.Phase3EdgeFinishSyntaxInvalid);
            return null;
        }

        var satisfiesMaterialized = materialized.Groups["concept"].Success ? new[] { materialized.Groups["concept"].Value } : [];
        var exposed = ParseExposedMembers(bodyText, materializedOpen + 1, materialized.Groups["name"].Value, box.Groups["name"].Value, resolvedBounds,
            allInstances, holes, diagnostics);
        ValidateMaterializedConformance(materialized.Groups["name"].Value, satisfiesMaterialized, exposed, definitions, diagnostics);
        var conformance = satisfiesMaterialized.Length == 0 ? "NotDeclared" : diagnostics.Any(d => IsConformanceDiagnostic(d)) ? "Invalid" : "Valid";
        var ir = new ConceptIrDocument(definitions, instanceList, resolved,
            new(materialized.Groups["name"].Value, materialized.Groups["kind"].Value, satisfiesMaterialized, new(materialized.Index, materializedClose - materialized.Index + 1), exposed, conformance), bindings,
            Enums: enums, StaticSelections: staticSelections, PatternExpansions: patterns);
        var modifyBlock = new FirmamentV2ModifyBlock(modify.Groups["target"].Value, [], holes, finishes);
        return new(materialized.Groups["name"].Value, materialized.Groups["kind"].Value, "mm", box.Groups["name"].Value, size, boundsProvenance,
            modifyBlock, ir);
    }

    /// <summary>Resolves a compile-time Concept plane for a Construction Plane trace without materializing a BRep feature.</summary>
    public static bool TryResolvePlane(string source, string reference, out ConceptIrPlaneValue? plane, out string? diagnostic)
    {
        source = FirmamentSourceSpelling.Normalize(source);
        plane = null; diagnostic = null;
        var match = Regex.Match(reference, @"^(?<instance>[A-Za-z_][A-Za-z0-9_]*)\.(?<member>[A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.CultureInvariant);
        if (!match.Success) { diagnostic = "ConstructionPlaneTraceMissing: Trace must name ConceptStruct.Plane"; return false; }
        var diagnostics = new List<string>(); var selections = new List<ConceptIrStaticSelection>();
        var instances = new Dictionary<string, ConceptIrStructInstance>(StringComparer.Ordinal);
        foreach (Match declaration in ConceptStructHeader.Matches(source))
        {
            var open = source.IndexOf('{', declaration.Index); var close = FindMatchingBrace(source, open);
            if (close < 0) { diagnostic = InvalidSpatialDerivation; return false; }
            var name = declaration.Groups["name"].Value;
            var body = source[(open + 1)..close];
            var resolvedInstance = new ConceptIrStructInstance(name, [], ResolveMembers(name, body, open + 1, "", ParseEnums(source, diagnostics), selections, diagnostics), false, "CompileTimeOnlyErased", new(declaration.Index, close - declaration.Index + 1));
            if (!instances.TryAdd(name, resolvedInstance))
            {
                diagnostics.Add(DuplicateDeclaration);
            }
        }
        if (diagnostics.Count > 0)
        {
            diagnostic = string.Join("; ", diagnostics);
            return false;
        }
        if (!instances.TryGetValue(match.Groups["instance"].Value, out var instance) || !instance.Members.TryGetValue(match.Groups["member"].Value, out var value))
        { diagnostic = "ConceptPlaneNotFound: " + reference; return false; }
        if (value is not ConceptIrPlaneValue resolved) { diagnostic = "ConstructionPlaneTraceMissing: " + reference + " is not a Plane"; return false; }
        plane = resolved; return true;
    }

    private static readonly string[] ConceptHoleFields = ["From", "On", "Center", "Diameter", "End", "Termination"];

    private static IReadOnlyList<FirmamentV2SemanticHoleDecl> ParseConceptHoles(
        string body,
        int bodyOffset,
        string boxName,
        ConceptIrBox3Value? bounds,
        IReadOnlyDictionary<string, ConceptIrStructInstance> instances,
        string materializedName,
        List<ConceptIrBinding> bindings,
        List<string> diagnostics,
        string? fullSource = null)
    {
        const double tolerance = 1e-9;
        var result = new List<FirmamentV2SemanticHoleDecl>();
        var headers = Regex.Matches(body, @"\bhole\s*<\s*(?<variant>[A-Za-z_][A-Za-z0-9_]*)\s*>\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        foreach (Match header in headers)
        {
            if (IsInsidePattern(body, header.Index)) continue;
            if (!string.Equals(header.Groups["variant"].Value, "shaft", StringComparison.OrdinalIgnoreCase)) { diagnostics.Add(FirmamentV2Parser.HoleVariantUnknown); continue; }
            var open = body.IndexOf('{', header.Index); var close = FindMatchingBrace(body, open);
            if (close < 0) { diagnostics.Add(FirmamentV2Parser.RegionUnsupported); continue; }
            var holeBody = body[(open + 1)..close];
            var holeSyntax = FirmamentSyntaxParser.Parse(body.Substring(header.Index, close - header.Index + 1))
                .Document.Statements.OfType<DeclarationSyntax>().FirstOrDefault();
            if (holeSyntax is null) { diagnostics.Add(FirmamentV2Parser.CanonicalModifyMalformed); continue; }
            var holeFields = new FirmamentSyntaxFields(holeSyntax, diagnostics);
            foreach (var field in ConceptHoleFields) holeFields.Take(field);
            holeFields.Complete();
            var from = FieldValue(holeBody, "from");
            var on = FieldValue(holeBody, "on");
            if (FieldCount(holeBody, "from") > 1 || FieldCount(holeBody, "on") > 1)
            { diagnostics.Add(FirmamentV2Parser.HolePlacementDuplicate); continue; }
            if (!string.IsNullOrWhiteSpace(from))
            {
                if (!string.IsNullOrWhiteSpace(on)) { diagnostics.Add(FirmamentV2Parser.HolePlacementMixed); continue; }
                if (!TryResolveConstructionPlane(fullSource, from, out var constructionPlane, out var planeDiagnostic))
                { diagnostics.Add(planeDiagnostic ?? FirmamentV2Parser.HoleConstructionPlaneNotFound); continue; }
                var localCenter = ParsePoint2(FieldValue(holeBody, "center"));
                if (localCenter is null) { diagnostics.Add(FirmamentV2Parser.HoleConstructionPlaneCenterMissing); continue; }
                var localDiameter = ParseLengthOrUnitless(FieldValue(holeBody, "diameter"));
                if (!double.IsFinite(localDiameter) || localDiameter <= 0) { diagnostics.Add(FirmamentV2Parser.HoleDiameterInvalid); continue; }
                var localEndText = FieldValue(holeBody, "end");
                var localEnd = ParseConstructionPlaneHoleEnd(localEndText);
                if (localEnd is null) { diagnostics.Add(string.Equals(localEndText, "Blind", StringComparison.OrdinalIgnoreCase) ? FirmamentV2Parser.HoleConstructionPlaneExtentUnsupported : FirmamentV2Parser.HoleBlindDepthInvalid); continue; }
                var termination = ParseConstructionPlaneHoleTermination(holeBody, diagnostics);
                if (localEnd.Kind == FirmamentV2SemanticHoleEndKind.ThroughAll)
                {
                    if (termination is not null) { diagnostics.Add(FirmamentV2Parser.HoleTerminationConflictsWithExtent); continue; }
                }
                else
                {
                    if (termination is null) { diagnostics.Add(FirmamentV2Parser.HoleBlindDepthMissing); continue; }
                    if (termination.Kind != FirmamentV2SemanticHoleTerminationKind.DrillPoint) { diagnostics.Add(FirmamentV2Parser.HoleConstructionPlaneExtentUnsupported); continue; }
                }
                var constructionSourceSpan = new FirmamentV2SourceSpan(bodyOffset + header.Index, close - header.Index + 1);
                var center = new FirmamentV2FaceLocalPoint2D(localCenter.Value.U, localCenter.Value.V, "ConstructionPlaneLocalXY");
                result.Add(new(header.Groups["name"].Value, FirmamentV2SemanticHoleVariant.Shaft, FirmamentV2FaceTarget.Direct("+Z"), center,
                    localDiameter, localEnd, SourceSpan: constructionSourceSpan,
                    Placement: new FirmamentV2ConstructionPlaneHolePlacement(constructionPlane!, center, constructionSourceSpan), Termination: termination));
                bindings.Add(new($"{materializedName}.{header.Groups["name"].Value}.ConstructionPlane", from, constructionPlane!.SourceConceptId, "ConstructionPlane"));
                continue;
            }
            var faceAxis = on switch
            {
                "face(+Z)" or "Top" or "+Z" => "+Z",
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

    private static IReadOnlyList<ConceptIrPatternExpansion> ParseConceptPatterns(string body, int bodyOffset, string boxName, ConceptIrBox3Value? bounds, IReadOnlyDictionary<string, ConceptIrStructInstance> instances, string materializedName, List<ConceptIrBinding> bindings, List<string> diagnostics, out IReadOnlyList<FirmamentV2SemanticHoleDecl> holes)
    {
        var reports = new List<ConceptIrPatternExpansion>(); var expanded = new List<FirmamentV2SemanticHoleDecl>();
        var generatedPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match pattern in Regex.Matches(body, @"\bPattern\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant))
        {
            var open = body.IndexOf('{', pattern.Index); var close = FindMatchingBrace(body, open);
            if (close < 0) { diagnostics.Add("firmament-pattern-invalid-source"); continue; }
            var patternBody = body[(open + 1)..close];
            var patternSyntax = FirmamentSyntaxParser.Parse("Pattern " + pattern.Groups["name"].Value + " {" + patternBody + "}")
                .Document.Statements.OfType<DeclarationSyntax>().Single();
            var fields = new FirmamentSyntaxFields(patternSyntax, diagnostics);
            var source = fields.Take("Source") is { } sourceValue ? FirmamentSyntaxWriter.Expression(sourceValue) : "";
            var child = patternSyntax.Body?.Statements.OfType<DeclarationSyntax>().FirstOrDefault(d => d.Type.Name.Text == "Hole");
            fields.Complete(child is null ? new HashSet<StatementSyntax>() : new HashSet<StatementSyntax> { child });
            var reference = Regex.Match(source, @"^(?<instance>[A-Za-z_][A-Za-z0-9_]*)\.(?<member>[A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.CultureInvariant);
            if (!reference.Success || !instances.TryGetValue(reference.Groups["instance"].Value, out var instance) || !instance.Members.TryGetValue(reference.Groups["member"].Value, out var member) || member is not ConceptIrPointSetValue points)
            { diagnostics.Add("firmament-pattern-source-not-static-point3-collection:" + source); continue; }
            if (points.Points.Count > MaxStaticPatternExpansion)
            { diagnostics.Add("firmament-pattern-expansion-limit-exceeded:" + points.Points.Count.ToString(CultureInfo.InvariantCulture)); continue; }
            var feature = Regex.Match(patternBody, @"\bHole\s*<\s*Shaft\s*>\s+(?<item>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!feature.Success) { diagnostics.Add("firmament-pattern-element-type-mismatch"); continue; }
            var featureOpen = patternBody.IndexOf('{', feature.Index); var featureClose = FindMatchingBrace(patternBody, featureOpen);
            if (featureClose < 0) { diagnostics.Add("firmament-pattern-element-type-mismatch"); continue; }
            var item = feature.Groups["item"].Value; var featureBody = patternBody[(featureOpen + 1)..featureClose];
            if (!Regex.IsMatch(featureBody, $@"\bCenter\s*:\s*{Regex.Escape(item)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) { diagnostics.Add("firmament-pattern-unbound-item"); continue; }
            var generatedNames = new List<string>();
            for (var index = 0; index < points.Points.Count; index++)
            {
                var generatedName = pattern.Groups["name"].Value + "_" + index.ToString(CultureInfo.InvariantCulture);
                var featureSyntax = FirmamentSyntaxParser.Parse(featureBody);
                var element = FirmamentSyntaxParser.ParseExpression(source + "[" + index.ToString(CultureInfo.InvariantCulture) + "]").Expression;
                if (!featureSyntax.IsSuccess || element is null) { diagnostics.Add("firmament-pattern-element-type-mismatch"); continue; }
                var substitution = new FirmamentSyntaxSubstitution(new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal) { [item] = element });
                var concreteBody = FirmamentSyntaxWriter.Statements(featureSyntax.Document.Statements.Select(substitution.Statement));
                var concrete = $"hole<Shaft> {generatedName} {{{concreteBody}}}";
                var one = ParseConceptHoles(concrete, bodyOffset + open + featureOpen, boxName, bounds, instances, materializedName, bindings, diagnostics);
                if (one.Count != 1) continue;
                var readable = pattern.Groups["name"].Value + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                var generatedPath = materializedName + "::" + readable;
                if (!generatedPaths.Add(generatedPath)) { diagnostics.Add("firmament-pattern-generated-declaration-collision:" + generatedPath); continue; }
                expanded.Add(one[0] with { Name = readable }); generatedNames.Add(generatedPath);
            }
            reports.Add(new(materializedName + "::" + pattern.Groups["name"].Value, materializedName + "::" + source, "Point3", points.Points.Count, generatedNames, new(bodyOffset + pattern.Index, close - pattern.Index + 1)));
        }
        holes = expanded; return reports;
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
        var expose = FirmamentSyntaxParser.Parse(body).Document.Statements.OfType<DeclarationSyntax>()
            .FirstOrDefault(d => d.Type.Name.Text == "Expose");
        if (expose?.Body is null) return [];
        var result = new List<ConceptIrSemanticMember>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var fields = expose.Body.Statements.OfType<FieldSyntax>().ToArray();
        foreach (var statement in expose.Body.Statements.Where(s => s is not FieldSyntax))
            diagnostics.Add($"{FirmamentV2Parser.CanonicalDeclarationUnknown}:Expose:{(statement as DeclarationSyntax)?.Type.Name.Text ?? "expression"}");
        var fieldNames = fields.Select(f => f.Name.Text).ToHashSet(StringComparer.Ordinal);
        var dependencies = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in fields) dependencies.TryAdd(field.Name.Text, FirmamentSyntaxWriter.Expression(field.Value));
        var cycleMembers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var start in dependencies.Keys)
        {
            var path = new HashSet<string>(StringComparer.Ordinal); var current = start;
            while (fieldNames.Contains(current) && path.Add(current) && dependencies.TryGetValue(current, out current!)) { }
            if (path.Contains(current)) foreach (var member in path) cycleMembers.Add(member);
        }
        foreach (var field in fields)
        {
            var name = field.Name.Text; var reference = FirmamentSyntaxWriter.Expression(field.Value);
            if (!names.Add(name)) { diagnostics.Add($"{DuplicateExposedMember}:{name}"); continue; }
            if (cycleMembers.Contains(name)) { diagnostics.Add($"{CircularExposureDependency}:{name}->{reference}"); continue; }
            var span = new FirmamentV2SourceSpan(bodyOffset + field.Span.Start, field.Span.Length);
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

    private static ConceptIrType TypeOf(ConceptIrValue value) => value switch
    {
        ConceptIrPointSetValue => new("Point3", true),
        ConceptIrEnumValue e => new(e.EnumType),
        _ => new(value.Kind.ToString())
    };

    private static string ResolvePlaneAxis(string source, IReadOnlyDictionary<string, ConceptIrStructInstance> instances, List<string> diagnostics)
    {
        var match = Regex.Match(source, @"^(?<instance>[A-Za-z_][A-Za-z0-9_]*)\.(?<member>[A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.CultureInvariant);
        if (match.Success && TryMember(instances, match, out ConceptIrPlaneValue? plane, diagnostics)) return AxisOf(plane!.Normal);
        return string.Empty;
    }

    private static IReadOnlyList<ConceptIrEnumDefinition> ParseEnums(string source, List<string> diagnostics)
    {
        var binding = new FirmamentChoiceBinding(source);
        diagnostics.AddRange(binding.Diagnostics.Select(d => d.Code + ":" + d.Message));
        return binding.Types.Values.Select(t => new ConceptIrEnumDefinition(t.Name, t.Cases, t.Span)).ToArray();
    }

    private static IReadOnlyDictionary<string, ParsedConceptMember> ParseConceptMembers(string body, int bodyOffset, List<string> diagnostics)
    {
        var syntax = FirmamentSyntaxParser.Parse(body);
        diagnostics.AddRange(syntax.Diagnostics.Select(d => d.Code + ": " + d.Message));
        var result = new Dictionary<string, ParsedConceptMember>(StringComparer.Ordinal);
        // This resolver owns semantic value fields. Layout declarations in the same
        // Concept Struct belong to the profile/point/derived-frame binders.
        foreach (var field in syntax.Document.Statements.OfType<FieldSyntax>())
        {
            var value = field.Value;
            var declaredType = value is TypedValueSyntax typed ? body.Substring(typed.Type.Span.Start, typed.Type.Span.Length) : null;
            if (value is TypedValueSyntax assignment) value = assignment.Value;
            var expression = body.Substring(value.Span.Start, value.Span.Length);
            if (!result.TryAdd(field.Name.Text, new(field.Name.Text, declaredType, expression,
                    new(bodyOffset + field.Span.Start, field.Span.Length)))) diagnostics.Add($"{DuplicateDeclaration}:{field.Name.Text}");
        }
        return result;
    }

    private static bool MatchesDeclaredType(string? declaredType, ConceptIrValue value)
    {
        if (declaredType is null) return true;
        var normalized = declaredType == "bool" ? "Bool" : declaredType == "int" ? "Int" : declaredType == "float" ? "Float" : declaredType == "string" ? "String" : declaredType;
        return string.Equals(normalized, TypeOf(value).ToString(), StringComparison.Ordinal);
    }

    private static string? FormatValue(ConceptIrValue value) => value switch
    {
        ConceptIrLengthValue v => v.Value.ToString("R", CultureInfo.InvariantCulture) + v.Unit,
        ConceptIrAngleValue v => v.Value.ToString("R", CultureInfo.InvariantCulture) + v.Unit,
        ConceptIrBoolValue v => v.Value.ToString().ToLowerInvariant(),
        ConceptIrIntValue v => v.Value.ToString(CultureInfo.InvariantCulture),
        ConceptIrFloatValue v => v.Value.ToString("R", CultureInfo.InvariantCulture),
        ConceptIrStringValue v => v.Value,
        ConceptIrEnumValue v => v.Variant,
        _ => null
    };

    private static bool IsPascalCase(string value) => value.Length > 0 && char.IsUpper(value[0]) && value.All(c => char.IsLetterOrDigit(c) || c == '_');

    private static IReadOnlyList<ConceptIrDefinition> ParseDefinitions(string source, List<string> diagnostics)
    {
        var concepts = new FirmamentConcepts(source);
        diagnostics.AddRange(concepts.Diagnostics.Select(d => d.Code + ": " + d.Message));
        return concepts.Contracts;
    }

    internal static bool ProvidesContract(string source, DeclarationSyntax declaration, ConceptIrDefinition contract,
        out IReadOnlyList<string> reported)
    {
        reported = [];
        if (declaration.Body is null || declaration.Body.Statements.Any(s => s is not FieldSyntax))
        { reported = ["firmament-concept-structural-member-form-unsupported"]; return false; }
        var diagnostics = new List<string>();
        var selections = new List<ConceptIrStaticSelection>();
        var span = declaration.Body.ContentSpan;
        var members = ResolveMembers(declaration.Name!.Text, source.Substring(span.Start, span.Length), span.Start,
            "", ParseEnums(source, diagnostics), selections, diagnostics);
        ValidateConformance(declaration.Name.Text, [contract.Name], members, [contract], diagnostics);
        reported = diagnostics;
        return diagnostics.Count == 0;
    }

    private static IReadOnlyDictionary<string, ConceptIrValue> ResolveMembers(
        string instanceName,
        string body,
        int bodyOffset,
        string materializedName,
        IReadOnlyList<ConceptIrEnumDefinition> enums,
        List<ConceptIrStaticSelection> selections,
        List<string> diagnostics, FirmamentCompileTimeValues? scalars = null)
    {
        if (scalars is null)
        {
            scalars = new("");
            scalars.UseChoiceTypes(enums.Select(e => new FirmamentChoiceBinding.Choice(e.Name, e.Variants, e.SourceSpan)));
        }
        var members = ParseConceptMembers(body, bodyOffset, diagnostics);
        return new StaticMemberEvaluator(instanceName, materializedName, members, enums, selections, diagnostics, scalars).EvaluateAll();
    }

    private sealed record ParsedConceptMember(string Name, string? DeclaredType, string Expression, FirmamentV2SourceSpan SourceSpan);

    private sealed class StaticMemberEvaluator(
        string instanceName,
        string materializedName,
        IReadOnlyDictionary<string, ParsedConceptMember> members,
        IReadOnlyList<ConceptIrEnumDefinition> enums,
        List<ConceptIrStaticSelection> selections,
        List<string> diagnostics, FirmamentCompileTimeValues scalars)
    {
        private readonly Dictionary<string, ConceptIrValue> _resolved = new(StringComparer.Ordinal);
        private readonly List<string> _stack = [];

        public IReadOnlyDictionary<string, ConceptIrValue> EvaluateAll()
        {
            var before = scalars.Diagnostics.Count;
            foreach (var name in members.Keys)
            {
                EvaluateMember(name);
                if (scalars.Diagnostics.Skip(before).Any(d => d.Severity is "error" or "fatal")) break;
            }
            return _resolved;
        }

        private ConceptIrValue? EvaluateMember(string name)
        {
            if (_resolved.TryGetValue(name, out var existing)) return existing;
            if (!members.TryGetValue(name, out var member)) return null;
            var cycleAt = _stack.IndexOf(name);
            if (cycleAt >= 0)
            {
                diagnostics.Add(CircularDependency);
                diagnostics.Add($"{CircularDependency}:{string.Join(" -> ", _stack.Skip(cycleAt).Append(name))}");
                return null;
            }
            _stack.Add(name);
            var value = EvaluateExpression(member.Expression, member, true);
            _stack.RemoveAt(_stack.Count - 1);
            if (value is not null && !MatchesDeclaredType(member.DeclaredType, value))
            {
                diagnostics.Add($"{TypeMismatch}:{instanceName}.{name}:expected-{member.DeclaredType}:actual-{TypeOf(value)}");
                return null;
            }
            if (value is not null) _resolved[name] = value;
            return value;
        }

        private ConceptIrValue? EvaluateExpression(string expression, ParsedConceptMember target, bool reportFailure)
        {
            expression = expression.Trim();
            var provenance = $"{instanceName}.{target.Name}";
            if (expression.StartsWith(materializedName + ".", StringComparison.Ordinal)) { diagnostics.Add(MaterializedPhaseReference); return null; }
            var syntax = FirmamentSyntaxParser.ParseExpression(expression).Expression;
            if (syntax is MatchSyntax match) return EvaluateMatch(match, target);
            if (syntax is ObjectSyntax spatial && FirmamentSyntaxValues.Reference(spatial.Type) is { } spatialType
                && SpatialFields.TryGetValue(spatialType, out var schema))
            {
                var before = diagnostics.Count;
                var fields = FirmamentSyntaxFields.Object(spatial, target.Name, diagnostics);
                foreach (var field in schema.Keys) fields.Take(field);
                fields.Complete();
                if (diagnostics.Count > before) return null;
            }

            var box = Regex.Match(expression, @"^Box3\s*\{(?<body>.*)\}$", RegexOptions.Singleline | RegexOptions.CultureInvariant);
            if (box.Success)
            {
                var sizeMatch = Regex.Match(box.Groups["body"].Value, @"\bSize\s*:\s*\[(?<values>[^\]]+)\]", RegexOptions.CultureInvariant);
                var size = sizeMatch.Success ? sizeMatch.Groups["values"].Value.Split(',').Select(ParseLength).ToArray() : [];
                if (size.Length == 3 && size.All(v => double.IsFinite(v) && v > 0))
                    return new ConceptIrBox3Value(Id(provenance), new(-size[0] / 2d, -size[1] / 2d, 0), new(size[0] / 2d, size[1] / 2d, size[2]), provenance);
                diagnostics.Add(InvalidSpatialDerivation); return null;
            }

            var grid = Regex.Match(expression, @"^Grid\s*\{(?<body>.*)\}$", RegexOptions.Singleline | RegexOptions.CultureInvariant);
            if (grid.Success)
            {
                var gridBody = grid.Groups["body"].Value;
                var within = Regex.Match(gridBody, @"\bWithin\s*:\s*(?<box>[A-Za-z_][A-Za-z0-9_]*)\.Face\((?<axis>[+-][XYZ])\)\.Inset\((?<inset>[^)]+)\)", RegexOptions.CultureInvariant);
                var columns = IntField(gridBody, "Columns"); var rows = IntField(gridBody, "Rows");
                var boxValue = within.Success ? EvaluateMember(within.Groups["box"].Value) : null;
                if (!within.Success || columns < 1 || rows < 1 || boxValue is not ConceptIrBox3Value bounds) { diagnostics.Add(InvalidSpatialDerivation); return null; }
                var inset = ParseLength(within.Groups["inset"].Value); var region = Region(bounds, within.Groups["axis"].Value, inset, provenance + ".Within");
                if (region is null) { diagnostics.Add(InvalidSpatialDerivation); return null; }
                var points = new List<ConceptIrPoint3Value>();
                for (var row = 0; row < rows; row++) for (var column = 0; column < columns; column++)
                {
                    var u = columns == 1 ? (region.MinU + region.MaxU) / 2d : region.MinU + column * (region.MaxU - region.MinU) / (columns - 1);
                    var v = rows == 1 ? (region.MinV + region.MaxV) / 2d : region.MinV + row * (region.MaxV - region.MinV) / (rows - 1);
                    var point = new ConceptIrPoint3(region.Center.X + region.U.X * u + region.V.X * v, region.Center.Y + region.U.Y * u + region.V.Y * v, region.Center.Z + region.U.Z * u + region.V.Z * v);
                    var ordinal = points.Count; var pointProvenance = $"{provenance}[{ordinal}]";
                    points.Add(new(Id(pointProvenance), point, pointProvenance, ordinal));
                }
                return new ConceptIrPointSetValue(Id(provenance), points, provenance);
            }

            var face = Regex.Match(expression, @"^(?<box>[A-Za-z_][A-Za-z0-9_]*)\.Face\((?<axis>[+-][XYZ])\)$", RegexOptions.CultureInvariant);
            if (face.Success && EvaluateMember(face.Groups["box"].Value) is ConceptIrBox3Value faceBox)
                return new ConceptIrPlaneValue(Id(provenance), FaceCenter(faceBox, face.Groups["axis"].Value), Vector(face.Groups["axis"].Value), provenance);
            var plane = Regex.Match(expression, @"^Plane\s*\{(?<body>.*)\}$", RegexOptions.Singleline | RegexOptions.CultureInvariant);
            if (plane.Success)
            {
                var planeBody = plane.Groups["body"].Value;
                if (!TryVectorField(planeBody, "Origin", out var origin) || !TryVectorField(planeBody, "Normal", out var normal)) { diagnostics.Add(InvalidSpatialDerivation); return null; }
                var hasUp = TryVectorField(planeBody, "Up", out var up);
                return new ConceptIrPlaneValue(Id(provenance), new(origin.X, origin.Y, origin.Z), new(normal.X, normal.Y, normal.Z), provenance, hasUp ? new(up.X, up.Y, up.Z) : null);
            }
            var axis = Regex.Match(expression, @"^(?<box>[A-Za-z_][A-Za-z0-9_]*)\.Center\.Axis\((?<axis>[+-][XYZ])\)$", RegexOptions.CultureInvariant);
            if (axis.Success && EvaluateMember(axis.Groups["box"].Value) is ConceptIrBox3Value axisBox)
                return new ConceptIrAxisValue(Id(provenance), axisBox.Center, Vector(axis.Groups["axis"].Value), provenance);

            if (members.ContainsKey(expression)) return EvaluateMember(expression);
            if (expression is "true" or "false") return new ConceptIrBoolValue(Id(provenance), expression == "true", provenance);
            if (expression.StartsWith('"') && expression.EndsWith('"') && expression.Length >= 2) return new ConceptIrStringValue(Id(provenance), expression[1..^1], provenance);
            if (expression.EndsWith("mm", StringComparison.Ordinal) && double.TryParse(expression[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var length)) return new ConceptIrLengthValue(Id(provenance), length, "mm", provenance);
            if (expression.EndsWith("deg", StringComparison.Ordinal) && double.TryParse(expression[..^3], NumberStyles.Float, CultureInfo.InvariantCulture, out var angle)) return new ConceptIrAngleValue(Id(provenance), angle, "deg", provenance);
            if (long.TryParse(expression, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) return new ConceptIrIntValue(Id(provenance), integer, provenance);
            if (double.TryParse(expression, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return new ConceptIrFloatValue(Id(provenance), number, provenance);

            if (target.DeclaredType is { } declared)
            {
                var enumType = enums.SingleOrDefault(e => e.Name == declared);
                if (enumType is not null)
                {
                    var ordinal = enumType.Variants.ToList().IndexOf(expression);
                    if (ordinal >= 0) return new ConceptIrEnumValue(Id(provenance), enumType.Name, expression, ordinal, provenance);
                    diagnostics.Add($"{UnknownEnumVariant}:{declared}.{expression}"); return null;
                }
                if (IsPascalCase(declared) && declared is not ("Box3" or "Plane" or "Axis" or "Point3")) { diagnostics.Add($"{UnknownEnumType}:{declared}"); return null; }
            }
            if (reportFailure) diagnostics.Add($"{InvalidSpatialDerivation}:{instanceName}.{target.Name}:{expression}");
            return null;
        }

        private static bool TryVectorField(string body, string name, out (double X, double Y, double Z) vector)
        {
            vector = default;
            var match = Regex.Match(body, $@"\b{Regex.Escape(name)}\s*:\s*\[(?<values>[^\]]+)\]", RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return false;
            }
            var components = FirmamentSyntaxValues.Components(match.Groups["values"].Value);
            if (components.Count != 3)
            {
                return false;
            }
            var values = new double[3];
            for (int index = 0; index < components.Count; index++)
            {
                if (!FirmamentNumericEvaluation.TryEvaluateScalar(components[index].Text, out values[index], out string unit)
                    || !double.IsFinite(values[index]) || (unit != "" && (name != "Origin" || unit != "mm")))
                {
                    return false;
                }
            }
            vector = (values[0], values[1], values[2]);
            return true;
        }

        private ConceptIrValue? EvaluateMatch(MatchSyntax match, ParsedConceptMember target)
        {
            var inputs = members.ToDictionary(p => p.Key, p => new FirmamentCompileTimeValues.ChoiceInput(
                p.Value.DeclaredType ?? "", FirmamentSyntaxParser.ParseExpression(p.Value.Expression).Expression!), StringComparer.Ordinal);
            var selectedArms = new Dictionary<string, string>(StringComparer.Ordinal);
            var before = scalars.Diagnostics.Count;
            var resolved = scalars.ResolveChoices(match, diagnostics, inputs, expected: target.DeclaredType, selected: selectedArms);
            scalars.LocateChoiceErrors(before, scalars.OriginalDeclaration("Struct", instanceName, "Concept"),
                "Location identifies the original enclosing Concept Struct; match arms were bound before spatial evaluation.");
            if (scalars.Diagnostics.Skip(before).Any(d => d.Severity is "error" or "fatal")) return null;
            var selected = EvaluateExpression(FirmamentSyntaxWriter.Expression(resolved), target, false);
            var scrutineeName = FirmamentSyntaxValues.Reference(match.Scrutinee) ?? FirmamentSyntaxWriter.Expression(match.Scrutinee);
            var selectedPattern = selectedArms.GetValueOrDefault(scrutineeName) ?? "";
            if (selected is null) { diagnostics.Add($"{SelectedBranchEvaluationFailure}:{instanceName}.{target.Name}:{selectedPattern}"); return null; }
            var domain = inputs.GetValueOrDefault(scrutineeName)?.Type;
            if (string.IsNullOrEmpty(domain)) domain = selectedPattern is "true" or "false" ? "Bool"
                : enums.FirstOrDefault(e => e.Variants.Contains(selectedPattern, StringComparer.Ordinal))?.Name;
            selections.Add(new($"{instanceName}.{target.Name}", $"{instanceName}.{scrutineeName}", domain ?? "Bool", selectedPattern, selectedPattern,
                TypeOf(selected).ToString(), FormatValue(selected), target.SourceSpan, $"{instanceName}.{target.Name}"));
            return selected;
        }
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
    private static bool IsInsidePattern(string body, int index) => Regex.Matches(body, @"\bPattern\s+[A-Za-z_][A-Za-z0-9_]*\s*\{", RegexOptions.CultureInvariant).Cast<Match>().Any(p => { var close = FindMatchingBrace(body, body.IndexOf('{', p.Index)); return p.Index < index && close > index; });
    private static string FieldValue(string body, string name) { var m = Regex.Match(body, $@"\b{Regex.Escape(name)}\s*:\s*(?<value>[^\r\n;}}]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant); return m.Success ? m.Groups["value"].Value.Trim() : string.Empty; }
    private static FirmamentV2SemanticHoleEnd? ParseConstructionPlaneHoleEnd(string value)
    {
        if (string.Equals(value, "ThroughAll", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "Through", StringComparison.OrdinalIgnoreCase)) return new(FirmamentV2SemanticHoleEndKind.ThroughAll);
        var match = Regex.Match(value, @"^(?<kind>ShaftDepth|TotalDepth)\s*\(\s*(?<value>[^)]+)\s*\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success) return null;
        var depth = ParseLengthOrUnitless(match.Groups["value"].Value);
        if (!double.IsFinite(depth) || (string.Equals(match.Groups["kind"].Value, "ShaftDepth", StringComparison.OrdinalIgnoreCase) ? depth < 0d : depth <= 0d)) return null;
        return new(string.Equals(match.Groups["kind"].Value, "ShaftDepth", StringComparison.OrdinalIgnoreCase) ? FirmamentV2SemanticHoleEndKind.ShaftDepth : FirmamentV2SemanticHoleEndKind.TotalDepth, depth);
    }
    private static FirmamentV2SemanticHoleTermination? ParseConstructionPlaneHoleTermination(string body, List<string> diagnostics)
    {
        var match = Regex.Match(body, @"\btermination\s*:\s*(?<kind>DrillPoint)\b(?:\s*\{\s*PointAngle\s*:\s*(?<angle>[^}\r\n;]+)\s*\})?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success) return null;
        var angle = AirHoleTermination.DrillPoint.DefaultPointAngleDegrees;
        if (match.Groups["angle"].Success)
        {
            var raw = match.Groups["angle"].Value.Trim();
            if (raw.EndsWith("deg", StringComparison.OrdinalIgnoreCase)) raw = raw[..^3];
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out angle) || !double.IsFinite(angle) || angle <= 0d || angle >= 180d)
            { diagnostics.Add(FirmamentV2Parser.HoleDrillPointAngleInvalid); return null; }
        }
        return new(FirmamentV2SemanticHoleTerminationKind.DrillPoint, angle);
    }
    private static int FieldCount(string body, string name) => Regex.Matches(body, $@"\b{Regex.Escape(name)}\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count;
    private static (double U, double V)? ParsePoint2(string source)
    {
        var match = Regex.Match(source, @"^Point2\s*\(\s*(?<u>[^,]+)\s*,\s*(?<v>[^)]+)\s*\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success) return null;
        var u = ParseLengthOrUnitless(match.Groups["u"].Value.Trim()); var v = ParseLengthOrUnitless(match.Groups["v"].Value.Trim());
        return double.IsFinite(u) && double.IsFinite(v) ? (u, v) : null;
    }

    private static bool TryResolveConstructionPlane(string? source, string name, out ConstructionPlane? plane, out string? diagnostic)
    {
        plane = null; diagnostic = null;
        if (string.IsNullOrWhiteSpace(source)) { diagnostic = FirmamentV2Parser.HoleConstructionPlaneNotFound; return false; }
        var declaration = Regex.Match(source, $@"\bConstruction\s+Plane\s+{Regex.Escape(name)}\s*\{{\s*Trace\s*:\s*(?<trace>[A-Za-z_][A-Za-z0-9_]*\.[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!declaration.Success) { diagnostic = FirmamentV2Parser.HoleConstructionPlaneNotFound; return false; }
        if (!TryResolvePlane(source, declaration.Groups["trace"].Value, out var conceptPlane, out var conceptDiagnostic))
        { diagnostic = conceptDiagnostic ?? FirmamentV2Parser.HoleConstructionPlaneNotFound; return false; }
        if (!ConstructionPlane.TryTrace("construction:" + name, conceptPlane!, $"{declaration.Index}:{declaration.Length}", out plane, out var frameDiagnostic))
        { diagnostic = frameDiagnostic ?? "HoleConstructionPlaneInvalid"; return false; }
        return true;
    }
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
    private static string StripMatchBlocks(string source)
    {
        foreach (Match match in Regex.Matches(source, @"\bMatch\s*\{", RegexOptions.CultureInvariant).Cast<Match>().Reverse())
        {
            var open = source.IndexOf('{', match.Index);
            var close = FindMatchingBrace(source, open);
            if (close >= 0) source = source.Remove(open, close - open + 1).Insert(open, new string(' ', close - open + 1));
        }
        return source;
    }
}
