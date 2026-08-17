using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Surfacing;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentV2Document(string ModelName, string Units, IReadOnlyList<FirmamentV2SolidBinding> Solids, IReadOnlyList<FirmamentV2ModifyBlock>? ModifyBlocks = null, IReadOnlyList<FirmamentV2TemplateDecl>? Templates = null, IReadOnlyList<FirmamentV2PmiDecl>? Pmi = null, IReadOnlyList<FirmamentV2RecognizedRegion>? RecognizedRegions = null, IReadOnlyList<FirmamentV2ReplacementDecl>? Replacements = null, IReadOnlyList<FirmamentV2LetDeclaration>? Lets = null, IReadOnlyList<FirmamentV2BoundLet>? BoundLets = null, IReadOnlyList<FirmamentV2LetRecordDeclaration>? LetRecords = null, IReadOnlyList<FirmamentV2BoundLetRecord>? BoundLetRecords = null, IReadOnlyList<FirmamentV2ManufacturingConceptDeclaration>? ManufacturingConcepts = null, IReadOnlyList<FirmamentV2FeatureConceptDeclaration>? FeatureConcepts = null, FirmamentV2PmiBlock? PmiBlock = null, FirmamentV2BoundPmiBlock? BoundPmi = null, ConceptIrDocument? ConceptIr = null, IReadOnlyList<FirmamentV2LatticeFillDecl>? LatticeFills = null, IReadOnlyList<FirmamentV2StandaloneLatticeFillDecl>? StandaloneLatticeFills = null, IReadOnlyList<FirmamentV2ProfileDecl>? Profiles = null, IReadOnlyList<FirmamentV2ComposeDecl>? Composes = null, IReadOnlyList<FirmamentV2SelectionDecl>? Selections = null, FirmamentV2StaticAuthoringDocument? StaticAuthoring = null, FirmamentV2CanonicalSymbolTable? SymbolTable = null, IReadOnlyList<FirmamentV2VolumeAssertion>? VolumeAssertions = null, IReadOnlyList<ConceptIrTemplateInstantiation>? TemplateInstantiations = null, IReadOnlyList<PanelIr>? Panels = null, IReadOnlyList<FirmamentV2BossDecl>? Bosses = null, IReadOnlyList<FirmamentV2PocketDecl>? Pockets = null)
{
    public FirmamentV2SolidBinding Solid => Solids[^1];
    public FirmamentV2SideHoleIntent? SideHoleIntent => ModifyBlocks?.SelectMany(m => m.Regions.Select(r =>
    {
        var targetSolid = Solids.Single(s => string.Equals(s.Name, m.TargetSolid, StringComparison.Ordinal));
        var route = FirmamentV2SideHoleRoutePolicy.Resolve(r.Attachment.Axis, r.Cut.Tool.Through.Axis, targetSolid.Box!.Size, r.Cut.Tool.Radius, r.Cut.Tool.Center?.U ?? 0, r.Cut.Tool.Center?.V ?? 0).Route!;
        return new FirmamentV2SideHoleIntent(m.TargetSolid, r.Name, r.Attachment.Source, r.Attachment.Kind, r.Attachment.Axis, r.Cut.Tool.Through.Source, r.Cut.Tool.Through.Kind, r.Cut.Tool.Through.Axis, r.Cut.Tool.ToolType, r.Cut.Tool.Radius, r.Cut.Tool.Center?.U ?? 0, r.Cut.Tool.Center?.V ?? 0, r.Cut.Tool.Center is not null, route.CenterFrame, Units, route);
    })).SingleOrDefault();
}

public sealed record FirmamentV2SourceSpan(int Start, int Length);
public enum FirmamentV2PrimitiveType { Int, Float, Length, Angle, String, Bool }
public enum FirmamentV2ToleranceKind { Bilateral, Asymmetric }
public sealed record FirmamentV2Tolerance(FirmamentV2ToleranceKind Kind, double Plus, double Minus, string Unit, FirmamentV2PrimitiveType Type, FirmamentV2SourceSpan SourceSpan);
public sealed record FirmamentV2LiteralValue(FirmamentV2PrimitiveType Type, object Value, double? NumericValue = null, string? Unit = null, string? Raw = null);
public abstract record FirmamentV2ValueExpression;
public sealed record FirmamentV2LiteralExpression(FirmamentV2LiteralValue Value) : FirmamentV2ValueExpression;
public sealed record FirmamentV2DottedReferenceExpression(string RecordName, string FieldName, string Source) : FirmamentV2ValueExpression;
public sealed record FirmamentV2IdentifierReferenceExpression(string Name, string Source) : FirmamentV2ValueExpression;
public sealed record FirmamentV2BinaryExpression(FirmamentV2ValueExpression Left, string Operator, FirmamentV2ValueExpression Right, string Source) : FirmamentV2ValueExpression;
public sealed record FirmamentV2LetDeclaration(string Name, FirmamentV2PrimitiveType DeclaredType, FirmamentV2ValueExpression ValueExpression, FirmamentV2SourceSpan SourceSpan, FirmamentV2Tolerance? Tolerance = null)
{
    public FirmamentV2LiteralValue LiteralValue => ValueExpression is FirmamentV2LiteralExpression literal ? literal.Value : throw new InvalidOperationException("Let value is not a literal.");
}
public sealed record FirmamentV2LetRecordDeclaration(string Name, IReadOnlyList<FirmamentV2LetRecordField> Fields, FirmamentV2SourceSpan SourceSpan);
public sealed record FirmamentV2LetRecordField(string Name, FirmamentV2PrimitiveType DeclaredType, FirmamentV2ValueExpression ValueExpression, FirmamentV2SourceSpan SourceSpan, FirmamentV2Tolerance? Tolerance = null)
{
    public FirmamentV2LiteralValue LiteralValue => ValueExpression is FirmamentV2LiteralExpression literal ? literal.Value : throw new InvalidOperationException("Record field value is not a literal.");
}
public sealed record FirmamentV2BoundExpression(FirmamentV2PrimitiveType InferredType, FirmamentV2LiteralValue Value, IReadOnlySet<string> Dependencies, FirmamentV2SourceSpan SourceSpan, FirmamentV2Tolerance? AliasTolerance = null, bool UsesTolerancedValueInArithmetic = false);
public sealed record FirmamentV2BoundLet(string Name, FirmamentV2PrimitiveType Type, FirmamentV2LiteralValue Value, FirmamentV2SourceSpan SourceSpan, FirmamentV2BoundExpression? Expression = null, IReadOnlySet<string>? Dependencies = null, FirmamentV2Tolerance? Tolerance = null);
public sealed record FirmamentV2BoundLetRecord(string Name, IReadOnlyDictionary<string, FirmamentV2BoundLet> Fields, FirmamentV2SourceSpan SourceSpan);

public sealed record FirmamentV2ConceptApplication(string FamilyName, string ConceptName, FirmamentV2SourceSpan SourceSpan);
public sealed record FirmamentV2ConceptField(string Name, FirmamentV2ValueExpression ValueExpression, string Source, FirmamentV2SourceSpan SourceSpan);
public sealed record FirmamentV2BoundConceptField(string Name, FirmamentV2ConceptField Field, FirmamentV2BoundExpression? BoundValue, string? TargetSource = null);
public sealed record FirmamentV2ManufacturingConceptDeclaration(FirmamentV2ConceptApplication Application, IReadOnlyList<FirmamentV2ConceptField> Fields, FirmamentV2SourceSpan SourceSpan, IReadOnlyList<FirmamentV2BoundConceptField>? BoundFields = null);
public sealed record FirmamentV2FeatureConceptDeclaration(string Name, FirmamentV2ConceptApplication Application, IReadOnlyList<FirmamentV2ConceptField> Fields, FirmamentV2SourceSpan SourceSpan, IReadOnlyList<FirmamentV2BoundConceptField>? BoundFields = null);

public enum FirmamentV2ConstructionPolicy { Solid, Hollow }

/// <summary>Source-owned hollow intent.  This is deliberately not a Boolean tool description.</summary>
public sealed record FirmamentV2HollowIntent(double WallThickness, IReadOnlyList<string> Openings, FirmamentV2SourceSpan SourceSpan);

public sealed record FirmamentV2SolidBinding(string Name, string RecordType, FirmamentV2PrimitiveRecord Primitive, string? DerivedFrom = null, IReadOnlyDictionary<string, IReadOnlyList<double>>? Overrides = null, IReadOnlyDictionary<string, string>? Provenance = null, FirmamentV2ConstructionPolicy ConstructionPolicy = FirmamentV2ConstructionPolicy.Solid, FirmamentV2HollowIntent? Hollow = null)
{
    public bool IsDerived => !string.IsNullOrWhiteSpace(DerivedFrom);
    public FirmamentV2BoxRecord? Box => Primitive as FirmamentV2BoxRecord;
    public FirmamentV2CylinderRecord? Cylinder => Primitive as FirmamentV2CylinderRecord;
    public FirmamentV2ConeRecord? Cone => Primitive as FirmamentV2ConeRecord;
    public FirmamentV2SphereRecord? Sphere => Primitive as FirmamentV2SphereRecord;
    public FirmamentV2TorusRecord? Torus => Primitive as FirmamentV2TorusRecord;
    public FirmamentV2RoundedBoxRecord? RoundedBox => Primitive as FirmamentV2RoundedBoxRecord;
    public FirmamentV2InlineStepRecord? InlineStep => Primitive as FirmamentV2InlineStepRecord;
    public FirmamentV2StandardPartRecord? StandardPart => Primitive as FirmamentV2StandardPartRecord;
    public FirmamentV2ExactCoaxialPartRecord? ExactCoaxialPart => Primitive as FirmamentV2ExactCoaxialPartRecord;
}

public abstract record FirmamentV2PrimitiveRecord;
public sealed record FirmamentV2BoxRecord(IReadOnlyList<double> Size, IReadOnlyList<FirmamentV2Exposure> Exposures) : FirmamentV2PrimitiveRecord;
public sealed record FirmamentV2CylinderRecord(double Radius, double Height) : FirmamentV2PrimitiveRecord;
public sealed record FirmamentV2ConeRecord(double BottomRadius, double TopRadius, double Height) : FirmamentV2PrimitiveRecord;
public sealed record FirmamentV2SphereRecord(double Radius) : FirmamentV2PrimitiveRecord;
public sealed record FirmamentV2TorusRecord(double MajorRadius, double MinorRadius) : FirmamentV2PrimitiveRecord;
/// <summary>Silhouette-defined rounded rectangle swept along +Z; CornerRadius is not an edge finish.</summary>
public sealed record FirmamentV2RoundedBoxRecord(IReadOnlyList<double> Size, double CornerRadius) : FirmamentV2PrimitiveRecord;
/// <summary>Exact conical-frustum primitive identity; unlike Cone it cannot silently collapse into a generic primitive name.</summary>
public sealed record FirmamentV2FrustumRecord(double BottomRadius, double TopRadius, double Height) : FirmamentV2PrimitiveRecord;
/// <summary>Resolved by the profile/composition materialization routes rather than primitive lowering.</summary>
public sealed record FirmamentV2AdvancedMaterialRecord(string Kind) : FirmamentV2PrimitiveRecord;
/// <summary>Record-shaped V2 invocation of a reusable StandardLibrary part family.</summary>
public sealed record FirmamentV2StandardPartRecord(
    string Family,
    IReadOnlyDictionary<string, string> Parameters) : FirmamentV2PrimitiveRecord;
/// <summary>A bounded exact analytic construction recipe assembled from reusable
/// regular-prism, coaxial cone, torus-blend, cylinder, and cone-frustum operations.</summary>
public sealed record FirmamentV2ExactCoaxialPartRecord(IReadOnlyDictionary<string, string> Parameters) : FirmamentV2PrimitiveRecord;
public sealed record FirmamentV2InlineStepRecord(string SourcePath, string NormalizedPath, string ContentHash, bool CanonicalInput, string CanonicalEvidence, ImportedStepTopologyMap TopologyMap) : FirmamentV2PrimitiveRecord;
public sealed record ImportedStepTopologyMap(IReadOnlyDictionary<string, string> FaceEntityToFaceId, IReadOnlyDictionary<string, string> FaceIdToFaceEntity)
{
    public bool TryResolveFaceEntity(string entityRef, out string faceId) => FaceEntityToFaceId.TryGetValue(entityRef, out faceId!);
    public bool TryResolveSequentialFaceId(int faceId, out string entityRef) => FaceIdToFaceEntity.TryGetValue($"face-{faceId}", out entityRef!);
    public bool TryResolveFaceReference(string reference, out string entityRef)
    {
        if (reference.StartsWith('#'))
        {
            if (FaceEntityToFaceId.ContainsKey(reference)) { entityRef = reference; return true; }
            entityRef = string.Empty;
            return false;
        }
        if (int.TryParse(reference, out var sequential) && TryResolveSequentialFaceId(sequential, out var resolved))
        {
            entityRef = resolved;
            return true;
        }
        entityRef = string.Empty;
        return false;
    }
}
public sealed record FirmamentV2RecognizedRegion(string BodyName, string RegionName, string Kind, IReadOnlyList<string> FaceRefs, string Confidence, FirmamentV2RecognitionEvidence? Evidence = null, FirmamentV2SemanticProposal? Proposal = null)
{
    public string TargetSource => $"{BodyName}.region(\"{RegionName}\")";
}

public sealed record FirmamentV2RecognitionEvidence(IReadOnlyList<string> SurfaceFamilies, double? Radius = null, string? Axis = null, FirmamentV2FaceLocalPoint2D? Center = null, bool? Through = null, IReadOnlyList<string>? Notes = null);
public sealed record FirmamentV2SemanticProposal(string ProposalKind, string FeatureName, string? PlacementTarget = null, FirmamentV2FaceLocalPoint2D? Center = null, double? Radius = null, string? EndCondition = null);

public sealed record FirmamentV2ReplacementDecl(string ImportedBodyName, string RecognizedRegionName, string ReplacementKind, string ReplacementFeatureName, string PlacementTarget, FirmamentV2FaceLocalPoint2D Center, double Radius, string EndCondition, IReadOnlyList<double> HostSize, string Source)
{
    public string TargetSource => $"{ImportedBodyName}.region(\"{RecognizedRegionName}\")";
}

public sealed record FirmamentV2ImportedStepFaceTarget(string BodySymbol, string EntityRef)
{
    public string Source => $"{BodySymbol}.face(\"{EntityRef}\")";
}
public sealed record FirmamentV2Exposure(string Alias, string SelectorKind, string Selector, string RefType, string Axis, string? Subselector);
public sealed record FirmamentV2FaceTarget(string Source, string Kind, string Axis, string ResolvedSelector, string RefType)
{
    public static FirmamentV2FaceTarget Direct(string axis) => new($"face({axis})", "DirectSelector", axis, $"face({axis})", "FaceRef");
    public static FirmamentV2FaceTarget Alias(string alias, string axis) => new(alias, "Alias", axis, $"face({axis})", "FaceRef");
}

public sealed record FirmamentV2FaceSelector(string Axis)
{
    public string Source => $"face({Axis})";
}
public sealed record FirmamentV2ModifyBlock(
    string TargetSolid,
    IReadOnlyList<FirmamentV2RegionDecl> Regions,
    IReadOnlyList<FirmamentV2SemanticHoleDecl> SemanticHoles,
    IReadOnlyList<FirmamentV2EdgeFinishDecl>? EdgeFinishes = null)
{
    public FirmamentV2ModifyBlock(string TargetSolid, IReadOnlyList<FirmamentV2RegionDecl> Regions) : this(TargetSolid, Regions, []) { }
}
public sealed record FirmamentV2EdgeFinishDecl(
    string Name,
    string FaceAxis,
    string Target,
    string Kind,
    double Distance,
    FirmamentV2SourceSpan SourceSpan,
    IReadOnlyDictionary<string, string>? Provenance = null);
public sealed record FirmamentV2RegionDecl(string Name, string Kind, FirmamentV2FaceTarget Attachment, FirmamentV2CutOperation Cut);
public sealed record FirmamentV2CutOperation(string OperationKind, FirmamentV2CylinderTool Tool);
public sealed record FirmamentV2CylinderTool(string ToolType, double Radius, FirmamentV2FaceLocalPoint2D? Center, FirmamentV2FaceTarget Through);
public sealed record FirmamentV2FaceLocalPoint2D(double U, double V, string Convention)
{
    public const string PlusXConvention = "face(+X):u=+Y,v=+Z";
    public const string MinusXConvention = "face(-X):u=+Y,v=+Z";
    public const string PlusYConvention = "face(+Y):u=+X,v=+Z";
    public const string MinusYConvention = "face(-Y):u=+X,v=+Z";
    public const string PlusZConvention = "face(+Z):u=+X,v=+Y";
    public const string MinusZConvention = "face(-Z):u=+X,v=+Y";
    public static string ConventionFor(string attachFace) => attachFace switch
    {
        "-X" => MinusXConvention,
        "+Y" => PlusYConvention,
        "-Y" => MinusYConvention,
        "+Z" => PlusZConvention,
        "-Z" => MinusZConvention,
        _ => PlusXConvention
    };
}
public sealed record FirmamentV2SideHoleIntent(string TargetSolid, string RegionName, string AttachTargetSource, string AttachTargetKind, string AttachFace, string ThroughTargetSource, string ThroughTargetKind, string ThroughFace, string Tool, double Radius, double CenterU, double CenterV, bool CenterExplicit, string CenterSelectorFrame, string Units, FirmamentV2SideHoleRoutePolicyEvidence PolicyRoute)
{
    public string Route => PolicyRoute.Direction;
    public FirmamentV2SideHoleRoutePolicyEvidence RouteEvidence => PolicyRoute;
}
public enum FirmamentV2SemanticHoleVariant { Shaft, Counterbore, Countersink }
public enum FirmamentV2SemanticHoleEndKind { ThroughAll, Depth, ShaftDepth, TotalDepth }
public sealed record FirmamentV2SemanticHoleEnd(FirmamentV2SemanticHoleEndKind Kind, double? Depth = null);
public enum FirmamentV2SemanticHoleTerminationKind { FlatBottom, DrillPoint }
public sealed record FirmamentV2SemanticHoleTermination(FirmamentV2SemanticHoleTerminationKind Kind, double? PointAngleDegrees = null);
public sealed record FirmamentV2ResolvedPoint3(
    double X,
    double Y,
    double Z,
    string StableId,
    string SourceMember,
    int? Ordinal,
    string PlacementFace,
    double PlaneDistance,
    FirmamentV2SourceSpan SourceSpan);
/// <summary>Bound placement mode: new sources use a traced immutable Construction Plane; legacy sources retain face-local placement.</summary>
public abstract record FirmamentV2BoundHolePlacement;
public sealed record FirmamentV2FaceLocalHolePlacement(FirmamentV2FaceTarget EntryFace, FirmamentV2FaceLocalPoint2D Center) : FirmamentV2BoundHolePlacement;
public sealed record FirmamentV2ConstructionPlaneHolePlacement(ConstructionPlane Plane, FirmamentV2FaceLocalPoint2D Center, FirmamentV2SourceSpan SourceSpan) : FirmamentV2BoundHolePlacement;
public sealed record FirmamentV2SemanticHoleDecl(
    string Name,
    FirmamentV2SemanticHoleVariant Variant,
    FirmamentV2FaceTarget EntryFace,
    FirmamentV2FaceLocalPoint2D Center,
    double ShaftDiameter,
    FirmamentV2SemanticHoleEnd EndCondition,
    double? CounterboreDiameter = null,
    double? CounterboreDepth = null,
    double? CountersinkDiameter = null,
    double? CountersinkAngleDegrees = null,
    FirmamentV2ResolvedPoint3? ResolvedCenter = null,
    FirmamentV2SourceSpan? SourceSpan = null,
    FirmamentV2BoundHolePlacement? Placement = null,
    FirmamentV2SemanticHoleTermination? Termination = null);
public enum FirmamentV2PmiKind { HoleDiameter, DatumPlane, Distance, Flatness, Parallel, Perpendicular, Coplanar }
public sealed record FirmamentV2PmiDecl(string Name, FirmamentV2PmiKind Kind, string Target, double? Value = null);
public sealed record FirmamentV2PmiBlock(IReadOnlyList<FirmamentV2PmiRecord> Records, FirmamentV2SourceSpan SourceSpan);
public sealed record FirmamentV2PmiRecord(FirmamentV2PmiKind Kind, string Name, IReadOnlyDictionary<string, FirmamentV2PmiField> Fields, FirmamentV2SourceSpan SourceSpan, FirmamentV2BoundPmiRecord? Bound = null, FirmamentV2PmiProjection? Projection = null);
public sealed record FirmamentV2PmiField(string Name, string Source, FirmamentV2SourceSpan SourceSpan, FirmamentV2ValueExpression? ValueExpression = null);
public sealed record FirmamentV2BoundPmiBlock(IReadOnlyList<FirmamentV2BoundPmiRecord> Datums, IReadOnlyList<FirmamentV2BoundPmiRecord> Dimensions, IReadOnlyList<FirmamentV2BoundPmiRecord> Controls, IReadOnlyList<string> Diagnostics);
public sealed record FirmamentV2BoundPmiRecord(FirmamentV2PmiKind Kind, string Name, IReadOnlyList<string> Targets, FirmamentV2LiteralValue? DimensionValue, FirmamentV2Tolerance? DimensionTolerance, FirmamentV2LiteralValue? ControlTolerance, IReadOnlyList<string> DatumRefs, FirmamentV2SourceSpan SourceSpan, string? ProjectionSource = null);
/// <summary>Reusable, validated dimensional intent. Only equality of a semantic feature property
/// to a length expectation is admitted in Preview 1.</summary>
public sealed record FirmamentV2SemanticConstraint(string Id, string Subject, string Property, FirmamentV2LiteralValue NominalValue, FirmamentV2Tolerance? Tolerance, bool ValidationSucceeded, FirmamentV2SourceSpan SourceSpan, string ExpectedProvenance);
public sealed record FirmamentV2PmiProjection(string SourceRequireId, FirmamentV2PmiKind AsKind, FirmamentV2SourceSpan SourceSpan);
public sealed record FirmamentV2ConceptDecl(string Name, string RawValue, double NumericValue, string? Unit);
public sealed record FirmamentV2TemplateDecl(string Process, string Name, IReadOnlyList<FirmamentV2ConceptDecl> Concepts);
public sealed record FirmamentV2FillRegionDecl(string Name, IReadOnlyList<double> Size, IReadOnlyList<double> Center, FirmamentV2SourceSpan SourceSpan);
/// <summary>Parser-owned normalized declaration evidence for the advanced material routes.
/// The materializers consume the same resolved profile/composition semantics regardless of source origin.</summary>
public sealed record FirmamentV2ProfileDecl(string Name, string Frame, double From, double To, FirmamentV2SourceSpan SourceSpan);
public sealed record FirmamentV2ComposeDecl(string Name, IReadOnlyList<string> Operations, FirmamentV2SourceSpan SourceSpan);
public sealed record FirmamentV2BossDecl(string Name, string Host, string On, string Profile, double Height, string StableId, FirmamentV2SourceSpan SourceSpan);
public sealed record FirmamentV2PocketDecl(string Name, string Host, string On, string Profile, double Depth, double HostThickness, double RemainingFloor, double MinimumFloorThickness, string MinimumFloorPolicySource, string StableId, FirmamentV2SourceSpan SourceSpan);
public sealed record FirmamentV2SelectionDecl(string Name, string Target, string Source, string Requirement, FirmamentV2SourceSpan SourceSpan);
/// <summary>Normalized, erased-before-materialization evidence for canonical static authoring.</summary>
public sealed record FirmamentV2StaticAuthoringDocument(IReadOnlyList<FirmamentV2RecordTypeDecl> RecordTypes, IReadOnlyList<FirmamentV2StaticArrayDecl> Arrays, IReadOnlyList<FirmamentV2CanonicalTemplateDecl> Templates, IReadOnlyList<FirmamentV2CanonicalPatternDecl> Patterns, IReadOnlyList<FirmamentV2RequireDecl> Requires, IReadOnlyList<FirmamentV2SemanticConstraint>? SemanticConstraints = null, IReadOnlyDictionary<string, FirmamentV2PmiProjection>? PmiProjections = null, IReadOnlyList<FirmamentV2StaticRecordDecl>? StaticRecords = null, IReadOnlyList<FirmamentV2StaticTableDecl>? Tables = null);
public sealed record FirmamentV2RecordTypeDecl(string Name, IReadOnlyDictionary<string, string> Fields, FirmamentV2SourceSpan SourceSpan);
public sealed record FirmamentV2StaticArrayDecl(string Name, string ElementType, IReadOnlyList<IReadOnlyDictionary<string, string>> Elements, FirmamentV2SourceSpan SourceSpan);
public sealed record FirmamentV2StaticRecordDecl(string Name, string RecordType, IReadOnlyDictionary<string, string> Fields, FirmamentV2SourceSpan SourceSpan);
/// <summary>Columnar compile-time table evidence. Rows are created only on static lookup and never enter AIR.</summary>
public sealed record FirmamentV2StaticTableDecl(string Name, string RowType, string? KeyField, IReadOnlyDictionary<string, IReadOnlyList<string>> Columns, int RowCount, FirmamentV2SourceSpan SourceSpan);
public sealed record FirmamentV2CanonicalTemplateDecl(string Name, string ParameterType, string ParameterName, string Body, FirmamentV2SourceSpan SourceSpan);
public sealed record FirmamentV2CanonicalPatternDecl(string Name, string Source, string Template, int GeneratedCount, IReadOnlyList<string> GeneratedIds, FirmamentV2SourceSpan SourceSpan);
public sealed record FirmamentV2RequireDecl(string Name, string Expression, bool Value, FirmamentV2SourceSpan SourceSpan, string? Provenance = null, string? Subject = null, string? Expected = null, string? ToleranceSource = null);
public sealed record FirmamentV2LatticeFillDecl(string Name, string Host, FirmamentV2FillRegionDecl Region, string Pattern, double CellSize, double StrutRadius, string BoundaryPolicy, FirmamentV2SourceSpan SourceSpan);
/// <summary>M9R's admitted standalone material body. It is intentionally distinct from the deferred host-replacement Fill.</summary>
public sealed record FirmamentV2StandaloneLatticeFillDecl(string Name, FirmamentV2FillRegionDecl Region, string Pattern, int CellsX, int CellsY, int CellsZ, double CellSize, double StrutRadius, double NodeRadius, string Placement, FirmamentV2SourceSpan SourceSpan);

/// <summary>Whether the V2 parser admitted the source into one of its dialects.</summary>
public enum FirmamentV2ParseDisposition
{
    NotRecognized,
    RecognizedInvalid,
    RecognizedValid
}

public sealed record FirmamentV2ParseResult(
    bool IsSuccess,
    FirmamentV2Document? Document,
    IReadOnlyList<string> Diagnostics,
    FirmamentV2ParseDisposition Disposition = FirmamentV2ParseDisposition.NotRecognized)
{
    public static FirmamentV2ParseResult Success(FirmamentV2Document document, IReadOnlyList<string> diagnostics) => new(true, document, diagnostics, FirmamentV2ParseDisposition.RecognizedValid);
    public static FirmamentV2ParseResult Failure(IReadOnlyList<string> diagnostics, FirmamentV2ParseDisposition disposition = FirmamentV2ParseDisposition.RecognizedInvalid) => new(false, null, diagnostics, disposition);
}
